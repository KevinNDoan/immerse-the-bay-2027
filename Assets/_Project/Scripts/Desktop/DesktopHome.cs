using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using Zap.Constellation;
using Zap.Core;
using Zap.Net;

namespace Zap.Desktop
{
    public sealed class DesktopHome : MonoBehaviour
    {
        private const int OtherCount = 6;

        public static DesktopHome Instance { get; private set; }
        public bool IsOpen => _canvasObject != null && _canvasObject.activeSelf;

        private ConstellationController _controller;
        private GameObject _canvasObject;
        private GameObject _homePage;
        private GameObject _settingsPage;
        private TMP_InputField _nameInput;
        private Button _continueButton;
        private TextMeshProUGUI _continueName;
        private TextMeshProUGUI _continueDetails;
        private TextMeshProUGUI _emptyMessage;
        private TextMeshProUGUI _connectionStatus;
        private Button _createButton;
        private readonly Button[] _otherButtons = new Button[OtherCount];
        private readonly TextMeshProUGUI[] _otherLabels = new TextMeshProUGUI[OtherCount];
        private readonly string[] _otherIds = new string[OtherCount];

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
            Show();
        }

        private void Update()
        {
            if (_controller == null)
            {
                _controller = FindAnyObjectByType<ConstellationController>();
                if (_controller != null)
                {
                    SetControllerButtonsInteractable(true);
                    Refresh();
                }
            }

            if (IsOpen && _nameInput.isFocused)
                ZapPlatform.IsTyping = true;
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
            ShowHomePage();
            Refresh();
            ZapPlatform.IsMenuOpen = true;
        }

        public void Hide()
        {
            if (_canvasObject != null) _canvasObject.SetActive(false);
            ZapPlatform.IsMenuOpen = false;
        }

        private void BuildCanvas()
        {
            var theme = ZapTheme.Current;
            _canvasObject = new GameObject("Desktop Home", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
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

            _homePage = CreatePage("Home page", _canvasObject.transform);
            BuildHomePage(_homePage.transform, theme);
            _settingsPage = CreatePage("Settings page", _canvasObject.transform);
            BuildSettingsPage(_settingsPage.transform, theme);
            _settingsPage.SetActive(false);

            SetControllerButtonsInteractable(false);
        }

        private static GameObject CreatePage(string name, Transform parent)
        {
            var page = new GameObject(name, typeof(RectTransform));
            page.transform.SetParent(parent, false);
            DesktopUi.SetRect((RectTransform)page.transform, new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(560f, 900f));
            return page;
        }

        private void BuildHomePage(Transform page, ZapTheme theme)
        {
            var logo = DesktopUi.CreateText("Logo", page, "ZAP!", 72f, TextAlignmentOptions.Center, theme);
            DesktopUi.SetRect(logo.rectTransform, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(0f, 390f), new Vector2(560f, 88f));

            var tagline = DesktopUi.CreateText("Tagline", page, "Your ideas, in space.", 20f, TextAlignmentOptions.Center, theme);
            tagline.color = new Color(1f, 1f, 1f, .7f);
            DesktopUi.SetRect(tagline.rectTransform, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(0f, 328f), new Vector2(560f, 32f));

            _continueButton = DesktopUi.CreateButton("Continue", page, string.Empty, theme);
            DesktopUi.SetRect((RectTransform)_continueButton.transform, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(0f, 256f), new Vector2(560f, 64f));
            var defaultContinueLabel = _continueButton.GetComponentInChildren<TextMeshProUGUI>();
            defaultContinueLabel.gameObject.SetActive(false);
            _continueName = DesktopUi.CreateText("Name", _continueButton.transform, string.Empty, 22f, TextAlignmentOptions.Center, theme);
            DesktopUi.SetRect(_continueName.rectTransform, Vector2.zero, Vector2.one, new Vector2(0f, 10f), new Vector2(-24f, -28f));
            _continueDetails = DesktopUi.CreateText("Details", _continueButton.transform, string.Empty, 16f, TextAlignmentOptions.Center, theme);
            _continueDetails.color = new Color(1f, 1f, 1f, .7f);
            DesktopUi.SetRect(_continueDetails.rectTransform, Vector2.zero, Vector2.one, new Vector2(0f, -16f), new Vector2(-24f, -36f));
            _continueButton.onClick.AddListener(Continue);

            var newHeading = DesktopUi.CreateText("New heading", page, "New constellation", 18f, TextAlignmentOptions.MidlineLeft, theme);
            DesktopUi.SetRect(newHeading.rectTransform, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(0f, 193f), new Vector2(560f, 28f));

            _nameInput = DesktopUi.CreateInputField("Constellation name", page, "Name it", theme);
            DesktopUi.SetRect((RectTransform)_nameInput.transform, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(-75f, 145f), new Vector2(410f, 52f));
            _nameInput.onSubmit.AddListener(CreateConstellation);
            _createButton = DesktopUi.CreateButton("Create", page, "Create", theme);
            DesktopUi.SetRect((RectTransform)_createButton.transform, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(215f, 145f), new Vector2(140f, 52f));
            _createButton.onClick.AddListener(CreateConstellation);

            var listHeading = DesktopUi.CreateText("List heading", page, "Your constellations", 18f, TextAlignmentOptions.MidlineLeft, theme);
            DesktopUi.SetRect(listHeading.rectTransform, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(0f, 93f), new Vector2(560f, 28f));

            for (int i = 0; i < OtherCount; i++)
            {
                int index = i;
                var button = DesktopUi.CreateButton("Constellation " + (i + 1), page, string.Empty, theme);
                DesktopUi.SetRect((RectTransform)button.transform, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(0f, 50f - i * 61f), new Vector2(560f, 53f));
                var label = button.GetComponentInChildren<TextMeshProUGUI>();
                label.fontSize = 18f;
                button.onClick.AddListener(() => OpenOther(index));
                _otherButtons[i] = button;
                _otherLabels[i] = label;
            }

            _emptyMessage = DesktopUi.CreateText("No others", page, "No others yet", 18f, TextAlignmentOptions.Center, theme);
            _emptyMessage.color = new Color(1f, 1f, 1f, .7f);
            DesktopUi.SetRect(_emptyMessage.rectTransform, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(0f, 48f), new Vector2(560f, 36f));

            var settings = DesktopUi.CreateButton("Settings", page, "Settings", theme);
            DesktopUi.SetRect((RectTransform)settings.transform, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(-145f, -394f), new Vector2(270f, 48f));
            settings.onClick.AddListener(ShowSettingsPage);
            var quit = DesktopUi.CreateButton("Quit", page, "Quit", theme);
            DesktopUi.SetRect((RectTransform)quit.transform, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(145f, -394f), new Vector2(270f, 48f));
            quit.onClick.AddListener(Application.Quit);
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
            back.onClick.AddListener(ShowHomePage);
        }

        private void Refresh()
        {
            if (_continueName == null) return;

            var entries = ConstellationLibrary.List();
            string currentId = _controller != null ? _controller.ConstellationId : ConstellationLibrary.LastOpenedId;
            ConstellationLibrary.Entry current = null;
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i].id == currentId)
                {
                    current = entries[i];
                    break;
                }
            }

            string currentName = current != null ? current.name : (_controller != null ? _controller.ConstellationName : "Untitled");
            int topicCount = current != null ? current.topicCount : (_controller != null ? _controller.Balls.Count : 0);
            DateTime updated = current != null ? current.updatedUtc : DateTime.UtcNow;
            DesktopUi.SetText(_continueName, "Continue: " + currentName);
            DesktopUi.SetText(_continueDetails, TopicCount(topicCount) + " · edited " + RelativeTime(updated));

            int shown = 0;
            for (int i = 0; i < entries.Count && shown < OtherCount; i++)
            {
                var entry = entries[i];
                if (entry.id == currentId) continue;
                _otherIds[shown] = entry.id;
                DesktopUi.SetText(_otherLabels[shown], entry.name + " · " + TopicCount(entry.topicCount) + " · edited " + RelativeTime(entry.updatedUtc));
                DesktopUi.SetActive(_otherButtons[shown].gameObject, true);
                shown++;
            }

            for (int i = shown; i < OtherCount; i++)
            {
                _otherIds[i] = null;
                DesktopUi.SetActive(_otherButtons[i].gameObject, false);
            }
            DesktopUi.SetActive(_emptyMessage.gameObject, shown == 0);
        }

        private void SetControllerButtonsInteractable(bool value)
        {
            _continueButton.interactable = value;
            _createButton.interactable = value;
            for (int i = 0; i < OtherCount; i++) _otherButtons[i].interactable = value;
        }

        private void Continue()
        {
            if (_controller == null) return;
            Hide();
        }

        private void CreateConstellation()
        {
            CreateConstellation(_nameInput.text);
        }

        private void CreateConstellation(string value)
        {
            if (_controller == null) return;
            _controller.CreateAndOpen((value ?? string.Empty).Trim());
            _nameInput.SetTextWithoutNotify(string.Empty);
            Hide();
        }

        private void OpenOther(int index)
        {
            if (_controller == null || string.IsNullOrEmpty(_otherIds[index])) return;
            _controller.Open(_otherIds[index]);
            Hide();
        }

        private void ShowSettingsPage()
        {
            DesktopUi.SetActive(_homePage, false);
            DesktopUi.SetActive(_settingsPage, true);
        }

        private void ShowHomePage()
        {
            DesktopUi.SetActive(_settingsPage, false);
            DesktopUi.SetActive(_homePage, true);
        }

        private void CheckConnection()
        {
            StartCoroutine(CheckConnectionRequest());
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

        private static string TopicCount(int count)
        {
            return count + (count == 1 ? " topic" : " topics");
        }

        private static string RelativeTime(DateTime updatedUtc)
        {
            TimeSpan age = DateTime.UtcNow - updatedUtc;
            if (age.TotalMinutes < 1) return "just now";
            if (age.TotalHours < 1) return (int)age.TotalMinutes + " min ago";
            if (age.TotalDays < 1) return (int)age.TotalHours + " h ago";
            return updatedUtc.ToLocalTime().ToString("d MMM");
        }
    }
}
