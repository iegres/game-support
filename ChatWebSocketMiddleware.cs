/* Главный класс для организации WebSocket Api */
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Net.WebSockets;
using System.Collections.Concurrent;
using System.IO;
using System.Text;
using System.Threading;
using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;
using GameSupport.Data;
using GameSupport.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;

namespace GameSupport
{
    public class ChatWebSocketMiddleware
    {
        private static ConcurrentDictionary<string, WebSocket> _sockets = new ConcurrentDictionary<string, WebSocket>();

        private readonly RequestDelegate _next;
        private readonly IDistributedCache _cache;

        public ChatWebSocketMiddleware(RequestDelegate next, IDistributedCache cache)
        {
            _next = next;
            _cache = cache;
        }

        public async Task Invoke(HttpContext context, ApplicationDbContext dbContext)
        {
            if (!context.WebSockets.IsWebSocketRequest)
            {
                await _next.Invoke(context);
                return;
            }

            string token = context.Request.Headers[HeaderNames.SecWebSocketProtocol].ToString();
            Player player = await dbContext.Player.FirstOrDefaultAsync(p => p.DeviceToken == token);
            string[] requestPieces = context.Request.Path.Value.Split('/');
            string id = requestPieces[3];
            if (player == null || id != player.Id.ToString())
            {
                await _next.Invoke(context);
                return;
            }

            CancellationToken ct = context.RequestAborted;
            WebSocket currentSocket = await context.WebSockets.AcceptWebSocketAsync(token);
            string socketId = Guid.NewGuid().ToString();

            _sockets.TryAdd(socketId, currentSocket);

            var oldMessages = await dbContext
                .Message
                .Where(m => m.Player == player)
                .OrderByDescending(m => m.Time)
                .ToListAsync();
            int pageSize = 15;
            int page = 1;
            Int32.TryParse(requestPieces[4], out page);
            oldMessages = oldMessages.Skip((page - 1) * pageSize).Take(pageSize).Reverse().ToList();
            // Структура элементов oldMessage - такая же, как у textMessage (см. ниже)
            string[] oldMessage = new string[] { "new", "", "", "", "" };
            string oldResponse;
            for (int i = 0; i < oldMessages.Count; i++)
            {
                oldMessage[1] = oldMessages[i].Id.ToString();
                oldMessage[2] = oldMessages[i].Time.ToString() + ' '
                    + (oldMessages[i].From ? "Operator" : "Player " + player.Name) + ": "
                    + oldMessages[i].Text;
                oldMessage[3] = oldMessages[i].Read ? "true" : "false";
                oldMessage[4] = oldMessages[i].From ? "true" : "false";
                oldResponse = JsonSerializer.Serialize(oldMessage);
                await SendStringAsync(currentSocket, oldResponse, ct);
            }

            while (true)
            {
                if (ct.IsCancellationRequested)
                {
                    break;
                }

                string response = await ReceiveStringAsync(currentSocket, ct);
                if (string.IsNullOrEmpty(response))
                {
                    if (currentSocket.State != WebSocketState.Open)
                    {
                        break;
                    }

                    continue;
                }
                Console.WriteLine(response);
                 /* Структура textMessage: [{new/update}, {adminName/null or messageId}, {text}, {read/unread}, {from}], где:
                 0-й эл-т - новое сообщение или обновление старого (пометка прочитано/не прочитано)
                 1-й - если 0-й = "new", то:
                     имя админа, отправившего сообщение (null для сообщений от игрока)
                 если "update", то:
                     Id старого сообщения
                 2-й - текст сообщения
                 3-й - прочитано/не прочитано
                 4-q - от кого идет запрос (true для админа, false для игрока */
                string[] textMessage = JsonSerializer.Deserialize<string[]>(response);
                bool from = (textMessage[4] == "true");
                if (textMessage[0] == "new")
                {
                    Admin admin = null;
                    if (from)
                    {
                        string adminId = (await dbContext.Users.FirstOrDefaultAsync(u => u.UserName == textMessage[1])).Id;
                        string adminRoleId = (await dbContext.UserRoles.FirstOrDefaultAsync(ur => ur.UserId == adminId)).RoleId;
                        string adminRole = (await dbContext.Roles.FirstOrDefaultAsync(r => r.Id == adminRoleId)).Name;
                        if (adminRole != "Operator")
                        {
                            continue;
                        }
                        admin = await dbContext.Admin.FirstOrDefaultAsync(a => a.Mail == textMessage[1]);
                        // Удаляем запись в кэше, если пришло новое сообщение от админа
                        _cache.Remove(token);
                    }
                    Message message = new Message
                    {
                        Player = player,
                        Admin = admin,
                        Read = false,
                        From = from,
                        Text = textMessage[2],
                        Time = DateTime.Now
                    };
                    await dbContext.Message.AddAsync(message);
                    dbContext.SaveChanges();
                    textMessage[1] = message.Id.ToString();
                    textMessage[2] = message.Time.ToString() + ' '
                        + (admin != null ? "Operator" : "Player " + player.Name) + ": "
                        + textMessage[2];
                }
                else
                {
                    int messageId = Int32.Parse(textMessage[1]);
                    Message message = await dbContext.Message.FirstOrDefaultAsync(m => m.Id == messageId);
                    message.Read = (textMessage[3] == "true");
                    dbContext.SaveChanges();
                    // Удаляем запись в кэше, если игрок пометил или снял пометку о прочтении сообщения
                    if (!from) _cache.Remove(token);
                }
                response = JsonSerializer.Serialize(textMessage);

                foreach (var socket in _sockets)
                {
                    if (socket.Value.State != WebSocketState.Open || socket.Value.SubProtocol != token)
                    {
                        continue;
                    }

                    await SendStringAsync(socket.Value, response, ct);
                }
            }

            WebSocket dummy;
            _sockets.TryRemove(socketId, out dummy);

            await currentSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", ct);
            currentSocket.Dispose();
        }

        private static Task SendStringAsync(WebSocket socket, string data, CancellationToken ct = default(CancellationToken))
        {
            var buffer = Encoding.UTF8.GetBytes(data);
            var segment = new ArraySegment<byte>(buffer);
            return socket.SendAsync(segment, WebSocketMessageType.Text, true, ct);
        }

        private static async Task<string> ReceiveStringAsync(WebSocket socket, CancellationToken ct = default(CancellationToken))
        {
            var buffer = new ArraySegment<byte>(new byte[8192]);
            using (var ms = new MemoryStream())
            {
                WebSocketReceiveResult result;
                do
                {
                    ct.ThrowIfCancellationRequested();

                    result = await socket.ReceiveAsync(buffer, ct);
                    ms.Write(buffer.Array, buffer.Offset, result.Count);
                }
                while (!result.EndOfMessage);

                ms.Seek(0, SeekOrigin.Begin);
                if (result.MessageType != WebSocketMessageType.Text)
                {
                    return null;
                }

                // Кодировка UTF8 (по WebSocket протоколу: https://tools.ietf.org/html/rfc6455#section-5.6)
                using (var reader = new StreamReader(ms, Encoding.UTF8))
                {
                    return await reader.ReadToEndAsync();
                }
            }
        }
    }
}
