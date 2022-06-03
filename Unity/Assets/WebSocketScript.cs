/* Скрипт для реализации WebSocket Api в среде Unity */
#if !BESTHTTP_DISABLE_WEBSOCKET

using System;
using BestHTTP.Examples.Helpers;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections;

namespace BestHTTP.Examples.Websockets
{
    public class WebSocketScript : BestHTTP.Examples.Helpers.SampleBase
    {
#pragma warning disable 0649

        private string address;

        [SerializeField]
        private InputField _input;

        [SerializeField]
        private ScrollRect _scrollRect;

        [SerializeField]
        private RectTransform _contentRoot;

        [SerializeField]
        private Button _connectButton;

        [SerializeField]
        private Button _closeButton;

        [SerializeField]
        private Button _prevButton;

        [SerializeField]
        private Button _nextButton;

        [SerializeField]
        private Text _output;

        [SerializeField]
        private Toggle _toggle;

        private int i;

        private int page = 1;

#pragma warning restore

        WebSocket.WebSocket webSocket;

        protected override void Start()
        {
            base.Start();

            SetButtons(true, false);
            this._input.interactable = false;
            this._prevButton.interactable = false;
            this._nextButton.interactable = false;

            _connectButton.onClick.AddListener(OnConnectButton);
            _closeButton.onClick.AddListener(OnCloseButton);
            _input.onEndEdit.AddListener(OnInputField);
            _prevButton.onClick.AddListener(OnPrevButton);
            _nextButton.onClick.AddListener(OnNextButton);
        }

        void OnDestroy()
        {
            if (this.webSocket != null)
            {
                this.webSocket.Close();
                this.webSocket = null;
            }
        }

        public void OnConnectButton()
        {
            if (PlayerPrefs.GetString("Id") != "")
            {
                address = "wss://localhost:44398/Players/Details/" + PlayerPrefs.GetString("Id") + '/' + page + "/ws";
            }
            else
            {
                _output.text = "Please Register First";
                return;
            }

            // Создаем экземпляр WebSocket
            this.webSocket = new WebSocket.WebSocket(new Uri(address));

#if !UNITY_WEBGL || UNITY_EDITOR
            this.webSocket.StartPingThread = true;

#if !BESTHTTP_DISABLE_PROXY
            if (HTTPManager.Proxy != null)
                this.webSocket.OnInternalRequestCreated = (ws, internalRequest) => internalRequest.Proxy = new HTTPProxy(HTTPManager.Proxy.Address, HTTPManager.Proxy.Credentials, false);
#endif
#endif
            // Добавляем протокол (используем токен устройства)
            this.webSocket.OnInternalRequestCreated += (ws, req) => req.AddHeader("Sec-WebSocket-Protocol", SystemInfo.deviceUniqueIdentifier);

            this.webSocket.OnOpen += OnOpen;
            this.webSocket.OnMessage += OnMessage;
            this.webSocket.OnClosed += OnClosed;
            this.webSocket.OnError += OnError;

            // Начинаем соединение с сервером
            this.webSocket.Open();
        }

        public void OnCloseButton()
        {
            // Завершаем соединение
            this.webSocket.Close(1000, "Конец связи");

            foreach (Transform child in _contentRoot.transform)
            {
                GameObject.Destroy(child.gameObject);
            }
        }
       
        public void OnInputField(string textToSend)
        {
            if ((!Input.GetKeyDown(KeyCode.KeypadEnter) && !Input.GetKeyDown(KeyCode.Return)) || string.IsNullOrEmpty(textToSend))
            {
                return;
            }
            string json = $"[\"new\",\"{PlayerPrefs.GetString("Name")}\",\"{textToSend}\",\"false\",\"false\"]";
            _input.text = "";
            // Отправляем сообщение на сервер
            this.webSocket.Send(json);
        }

        public void OnNextButton()
        {
            OnCloseButton();
            page++;
            StartCoroutine(ReopenSocketCoroutine());
        }

        public void OnPrevButton()
        {
            OnCloseButton();
            page--;
            StartCoroutine(ReopenSocketCoroutine());
        }

        IEnumerator ReopenSocketCoroutine()
        {
            yield return new WaitForSeconds(0.001f);
            OnConnectButton();
        }

        #region Обработчики событий вэбсокета

        void OnOpen(WebSocket.WebSocket ws)
        {
            SetButtons(false, true);
            this._input.interactable = true;
            this._prevButton.interactable = (page > 0) ? true : false;
            this._nextButton.interactable = true;

            Debug.Log("WebSocket начал соединение!");

            i = 0;
        }

        void OnMessage(WebSocket.WebSocket ws, string json)
        {
            string[] data = json.Split(new string[] { "[\"", "\",\"", "\"]" }, StringSplitOptions.None);
            string state = data[1];
            string id = data[2];
            string message = data[3];
            Debug.Log("Сообщение: " + message);
            string read = data[4];
            string from = data[5];
            bool isOn;
            FontStyle style;
            if (state == "new")
            {
                var toggle = Instantiate(_toggle);
                var label = toggle.gameObject.transform.GetChild(1).GetComponent<Text>();
                toggle.name = id;
                label.text = message;
                toggle.transform.SetParent(_contentRoot, false);
                RectTransform trt = toggle.GetComponent<RectTransform>();
                trt.localPosition = trt.localPosition + new Vector3(0, -20.0f * i, 0);
                if (read == "true")
                {
                    style = FontStyle.Normal;
                    isOn = true;
                }
                else
                {
                    style = FontStyle.Bold;
                    isOn = false;
                }
                if (from == "true")
                {
                    label.fontStyle = style;
                    toggle.isOn = isOn;
                    toggle.onValueChanged.AddListener(delegate {
                        ToggleValueChanged(toggle);
                    });
                }
                else
                {
                    label.fontStyle = style;
                    toggle.isOn = false;
                    toggle.interactable = false;
                }
                i++;
                _contentRoot.sizeDelta = new Vector2(0, (i + 1) * 20);
                _scrollRect.GetComponent<ScrollRect>().verticalNormalizedPosition = 0;
            }
            else
            {
                var toggle = GameObject.Find(id);
                var label = toggle.gameObject.transform.GetChild(1).GetComponent<Text>();
                label.fontStyle = (read == "true") ? FontStyle.Normal : FontStyle.Bold;
            }
        }

        void OnClosed(WebSocket.WebSocket ws, UInt16 code, string message)
        {
            webSocket = null;

            SetButtons(true, false);
            this._input.interactable = false;
            this._prevButton.interactable = false;
            this._nextButton.interactable = false;

            Debug.Log($"WebSocket завершил соединение! Код: {code} Сообщение: {message}");
        }

        void OnError(WebSocket.WebSocket ws, string error)
        {
            webSocket = null;

            SetButtons(true, false);
            this._input.interactable = false;
            this._prevButton.interactable = false;
            this._nextButton.interactable = false;

            Debug.Log("Ошибка: " + error);
        }

        void ToggleValueChanged(Toggle changedToggle)
        {
            string json = (changedToggle.isOn)
                ? $"[\"update\",\"{changedToggle.name}\",\"\",\"true\",\"false\"]"
                : $"[\"update\",\"{changedToggle.name}\",\"\",\"false\",\"false\"]";
            this.webSocket.Send(json);
        }

        #endregion

        private void SetButtons(bool connect, bool close)
        {
            if (this._connectButton != null)
                this._connectButton.interactable = connect;

            if (this._closeButton != null)
                this._closeButton.interactable = close;
        }
    }
}

#endif
