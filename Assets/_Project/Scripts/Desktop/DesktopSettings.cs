using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using Zap.Core;
using Zap.Net;

namespace Zap.Desktop
{
    public sealed class DesktopSettings : MonoBehaviour
    {
        public static DesktopSettings Instance { get; private set; }
        public bool IsOpen => _canvasObject != null && _canvasObject.activeSelf;

        private GameObject _canvasObject;
        private TextMeshProUGUI _connectionStatus;
        private Coroutine _check;

        [Serializable]
        private sealed class Capabilities
        {
            public string orbisMode;
            public bool speechInstalled;
            public bool nodeAiConfigured;
        }

        private void Awake()
        {
            Instance = this;
            BuildCanvas();
            Hide();
        }

        private void OnDisable()
        {
            ZapPlatform.IsMenuOpen = false;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void Show()
        {
            if (_canvasObject == null) return;
            _canvasObject.SetActive(true);
            ZapPlatform.IsMenuOpen = true;
            CheckConnection();
        }

        public void Hide()
        {
            if (_canvasObject != null) _canvasObject.SetActive(false);
            ZapPlatform.IsMenuOpen = false;
            if (_check != null)
            {
                StopCoroutine(_check);
                _check = null;
            }
        }

        private void BuildCanvas()
        {
            var theme = ZapTheme.Current;
            _canvasObject = new GameObject("Desktop Settings", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            _canvasObject.transform.SetParent(transform, false);

            var canvas = _canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = _canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = .5f;

            var background = DesktopUi.CreatePanel("Background", _canvasObject.transform, theme);
            var backgroundColor = theme.voidColor;
            backgroundColor.a = .96f;
            background.color = backgroundColor;
            DesktopUi.SetRect(background.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            var settingsPage = CreatePage("Settings page", _canvasObject.transform);
            BuildSettingsPage(settingsPage.transform, theme);
        }

        private static GameObject CreatePage(string name, Transform parent)
        {
            var page = new GameObject(name, typeof(RectTransform));
            page.transform.SetParent(parent, false);
            DesktopUi.SetRect((RectTransform)page.transform, new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(560f, 900f));
            return page;
        }

        private void BuildSettingsPage(Transform page, ZapTheme theme)
        {
            var heading = DesktopUi.CreateText("Heading", page, "Settings", 42f, TextAlignmentOptions.Center, theme);
            DesktopUi.SetRect(heading.rectTransform, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(0f, 330f), new Vector2(560f, 64f));

            var relayLabel = DesktopUi.CreateText("Relay label", page, "Relay address", 18f, TextAlignmentOptions.MidlineLeft, theme);
            relayLabel.color = new Color(1f, 1f, 1f, .7f);
            DesktopUi.SetRect(relayLabel.rectTransform, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(0f, 240f), new Vector2(560f, 28f));
            var relayValue = DesktopUi.CreateText("Relay value", page, RelayConfig.BaseUrl, 20f, TextAlignmentOptions.MidlineLeft, theme);
            DesktopUi.SetRect(relayValue.rectTransform, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(0f, 202f), new Vector2(560f, 36f));

            var check = DesktopUi.CreateButton("Check connection", page, "Check connection", theme);
            DesktopUi.SetRect((RectTransform)check.transform, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(0f, 132f), new Vector2(560f, 52f));
            check.onClick.AddListener(CheckConnection);

            _connectionStatus = DesktopUi.CreateText("Connection status", page, string.Empty, 20f, TextAlignmentOptions.TopLeft, theme);
            DesktopUi.SetRect(_connectionStatus.rectTransform, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(0f, 30f), new Vector2(560f, 110f));

            var back = DesktopUi.CreateButton("Back", page, "Back", theme);
            DesktopUi.SetRect((RectTransform)back.transform, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(0f, -330f), new Vector2(560f, 52f));
            back.onClick.AddListener(Hide);
        }

        private void CheckConnection()
        {
            if (_check != null) StopCoroutine(_check);
            _check = StartCoroutine(CheckConnectionRequest());
        }

        private IEnumerator CheckConnectionRequest()
        {
            string address = RelayConfig.BaseUrl;
            DesktopUi.SetText(_connectionStatus, "Checking…");
            using (var request = UnityWebRequest.Get(address.TrimEnd('/') + "/capabilities"))
            {
                request.timeout = 3;
                yield return request.SendWebRequest();
                if (request.result != UnityWebRequest.Result.Success)
                {
                    DesktopUi.SetText(_connectionStatus, "Cannot reach the relay at " + address);
                    yield break;
                }

                Capabilities capabilities;
                try
                {
                    capabilities = JsonUtility.FromJson<Capabilities>(request.downloadHandler.text);
                }
                catch (ArgumentException)
                {
                    DesktopUi.SetText(_connectionStatus, "Cannot reach the relay at " + address);
                    yield break;
                }

                if (capabilities == null)
                {
                    DesktopUi.SetText(_connectionStatus, "Cannot reach the relay at " + address);
                    yield break;
                }

                string worlds = capabilities.orbisMode == "live" ? "live" : "stand-in picture";
                string voice = capabilities.speechInstalled ? "ready" : "not installed";
                string notes = capabilities.nodeAiConfigured ? "on" : "off (no key)";
                DesktopUi.SetText(_connectionStatus, "Worlds: " + worlds + "\nVoice: " + voice + "\nNote writing: " + notes);
            }
        }
    }
}
