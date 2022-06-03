/* Классы для HTTP запросов:
Регистрация игрока и Количество непрочитанных сообщений */
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GameSupport.Models;
using GameSupport.Data;


namespace GameSupport.Controllers
{
    // POST: api/Registration
    [Route("api/[controller]")]
    [ApiController]
    public class RegistrationController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public RegistrationController(ApplicationDbContext context)
        {
            _context = context;
        }

        public string Post(string[] deviceInfo)
        {
            var player = _context.Player.FirstOrDefault(p => p.DeviceToken == deviceInfo[0]);
            if (player != null)
            {
                return $"Вы уже были зарегистрированы. Ваш ID: {player.Id} Ваш Никнейм: {player.Name}";
            }
            player = new Player();
            player.DeviceToken = deviceInfo[0];
            player.Name = deviceInfo[1];
            _context.Player.Add(player);
            _context.SaveChanges();
            return $"Регистрация прошла успешно. Ваш ID: {player.Id} Ваш Никнейм: {player.Name}";
        }
    }

    // POST: api/UnreadMessages
    [Route("api/[controller]")]
    [ApiController]
    public class UnreadMessagesController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IRedisCacheService _cache;

        public UnreadMessagesController(ApplicationDbContext context, IRedisCacheService cache)
        {
            _context = context;
            _cache = cache;
        }

        public string Post(string[] deviceInfo)
        {
            var cached = _cache.Get<int?>(deviceInfo[0]);
            if (cached != null)
            {
                return "(Кэш) Количество непрочитанных сообщений: " + cached;
            }

            var player = _context.Player.FirstOrDefault(p => p.DeviceToken == deviceInfo[0]);
            if (player == null)
            {
                return "Вы не зарегистрированы. Сначала зарегистрируйтесь";
            }
            int? unread = _context.Message.Where(m => m.Player == player && m.Read == false && m.From == true).Count();
            _cache.Set<int?>(deviceInfo[0], unread);
            return "Количество непрочитанных сообщений: " + unread;
        }
    }
}
