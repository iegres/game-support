/* Скрипт для реализации HTTP Api в среде Unity */
using System;
using UnityEngine;
using UnityEngine.UI;
using BestHTTP;

public class HttpScript : MonoBehaviour
{
    public Button registerButton, unreadMessagesButton;
    public InputField inputNameField;
    public Text output;

    private string host = "https://localhost:44398/";  // Введите адрес сервера
    private string token;

    void Start()
    {
        registerButton.onClick.AddListener(Register);
        unreadMessagesButton.onClick.AddListener(UnreadMessages);
        inputNameField.characterLimit = 16;  // Строка для никнейма не более 16 символов
        token = SystemInfo.deviceUniqueIdentifier;  // Уникальный токен устройства игрока
    }

    void Register()
    {
        if (string.IsNullOrWhiteSpace(inputNameField.text))
        {
            output.text = "Заполните поле для никнейма";
        }
        else
        {
            string json = $"[ \"{token}\",\"{inputNameField.text}\" ]";
            inputNameField.text = "";
            output.text = "Процесс регистрации...";
            var request = new HTTPRequest(new Uri(host + "api/Registration"), HTTPMethods.Post, OnRegisterRequestFinished);
            request.SetHeader("Content-Type", "application/json; charset=UTF-8");
            request.RawData = System.Text.Encoding.UTF8.GetBytes(json);
            request.Send();
        }
    }

    void OnRegisterRequestFinished(HTTPRequest request, HTTPResponse response)
    {
        Debug.Log("Запрос выполнен! Получен ответ: " + response.DataAsText);
        if (PlayerPrefs.GetString("Id") == "")
        {
            string[] responsePieces = response.DataAsText.Split(new string[] { "ID: ", "Никнейм: " }, StringSplitOptions.None);
            PlayerPrefs.SetString("Id", responsePieces[1]);
            PlayerPrefs.SetString("Name", responsePieces[2]);
        }
        output.text = response.DataAsText;
    }

    void UnreadMessages()
    {
        string json = $"[ \"{token}\" ]";
        output.text = "Ваш запрос выполняется...";
        var request = new HTTPRequest(new Uri(host + "api/UnreadMessages"), HTTPMethods.Post, OnUnreadMessagesRequestFinished);
        request.SetHeader("Content-Type", "application/json; charset=UTF-8");
        request.RawData = System.Text.Encoding.UTF8.GetBytes(json);
        request.Send();
    }

    void OnUnreadMessagesRequestFinished(HTTPRequest request, HTTPResponse response)
    {
        Debug.Log("Запрос выполнен! Получен ответ: " + response.DataAsText);
        output.text = response.DataAsText;
    }
}
