using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Zap.Constellation;
using Zap.Core;

namespace Zap.Desktop
{
    public sealed class DesktopHud : MonoBehaviour
    {
        private const string ConstellationHint = "Click: select · Drag: move · Shift-drag: link · Option-drag: cut · Double-click: enter · Right-drag: orbit · Scroll: zoom · Hold V: speak";
        private const string WorldHint = "Arrows: move and look · Space: event · Esc: back · Hold V: speak";
        private const string ConstellationPlaceholder = "Type a topic and press Enter";
        private const string WorldPlaceholder = "Ask to see something and press Enter";
        private const float DeleteConfirmSeconds = 3f;

        private ConstellationController _controller;
        private TextMeshProUGUI _worldTopic;
        private TextMeshProUGUI _status;
        private TextMeshProUGUI _sideTopic;
        private TextMeshProUGUI _sideSummary;
        private TextMeshProUGUI _hint;
        private TextMeshProUGUI _deleteLabel;
        private TMP_InputField _topicInput;
        private TMP_InputField _editInput;
        private Button _backButton;
        private Button _enterButton;
        private Button _editButton;
        private Button _deleteButton;
        private GameObject _canvasObject;
        private GameObject _backObject;
        private GameObject _sidePanel;
        private GameObject _editObject;
        private Image _topPanel;
        private TopicBall _deleteArmedFor;
        private float _deleteConfirmUntil;
        private bool _lastBusy;
        private bool _lastInWorld;
        private bool _hasState;

        private void Awake()
        {
            BuildCanvas();
            if (EventSystem.current == null)
                Debug.LogError("ZAP desktop mode needs the EventSystem from the Void scene for mouse input.");
        }

        private void Update()
        {
            DesktopUi.SetActive(_canvasObject, !ZapPlatform.IsMenuOpen);

            if (_controller == null)
            {
                _controller = FindAnyObjectByType<ConstellationController>();
                if (_controller == null)
                {
                    ZapPlatform.IsTyping = _topicInput.isFocused || _editInput.isFocused;
                    return;
                }
            }

            bool inWorld = _controller.InWorld;
            bool busy = _controller.Busy;
            var current = _controller.Current;
            bool showSide = current != null && !inWorld;

            DesktopUi.SetActive(_backObject, inWorld);
            DesktopUi.SetActive(_worldTopic.gameObject, inWorld);
            DesktopUi.SetActive(_sidePanel, showSide);
            if (!showSide) DesktopUi.SetActive(_editObject, false);

            DesktopUi.SetText(_worldTopic, inWorld && current != null ? current.Data.topic : string.Empty);
            DesktopUi.SetText(_status, _controller.IsListening ? "Listening…" : _controller.Status);
            DesktopUi.SetText(_hint, inWorld ? WorldHint : ConstellationHint);
            if (showSide)
            {
                DesktopUi.SetText(_sideTopic, current.Data.topic);
                DesktopUi.SetText(_sideSummary, current.Data.summary);
            }

            if (_deleteArmedFor != null && (current != _deleteArmedFor || Time.unscaledTime >= _deleteConfirmUntil))
                ClearDeleteConfirm();

            if (!_hasState || inWorld != _lastInWorld)
            {
                _topPanel.enabled = inWorld;
                var placeholder = (TextMeshProUGUI)_topicInput.placeholder;
                DesktopUi.SetText(placeholder, inWorld ? WorldPlaceholder : ConstellationPlaceholder);
                _lastInWorld = inWorld;
            }

            if (!_hasState || busy != _lastBusy)
            {
                _topicInput.interactable = !busy;
                _editInput.interactable = !busy;
                _backButton.interactable = !busy;
                _enterButton.interactable = !busy;
                _editButton.interactable = !busy;
                _deleteButton.interactable = !busy;
                _lastBusy = busy;
            }

            _hasState = true;
            ZapPlatform.IsTyping = _topicInput.isFocused || _editInput.isFocused;
        }

        private void OnDisable()
        {
            ZapPlatform.IsTyping = false;
        }

        private void BuildCanvas()
        {
            var theme = ZapTheme.Current;
            _canvasObject = new GameObject("Desktop HUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            _canvasObject.transform.SetParent(transform, false);

            var canvas = _canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = _canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = .5f;

            _topPanel = DesktopUi.CreatePanel("Top left", _canvasObject.transform, theme);
            DesktopUi.SetRect(_topPanel.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(510f, -56f), new Vector2(980f, 72f));
            var logo = DesktopUi.CreateText("Logo", _topPanel.transform, "ZAP!", 36f, TextAlignmentOptions.MidlineLeft, theme);
            DesktopUi.SetRect(logo.rectTransform, new Vector2(0f, .5f), new Vector2(0f, .5f), new Vector2(83f, 0f), new Vector2(110f, 56f));
            _worldTopic = DesktopUi.CreateText("World topic", _topPanel.transform, string.Empty, 24f, TextAlignmentOptions.MidlineLeft, theme);
            DesktopUi.SetRect(_worldTopic.rectTransform, new Vector2(0f, .5f), new Vector2(0f, .5f), new Vector2(360f, 0f), new Vector2(400f, 48f));
            _backButton = DesktopUi.CreateButton("Back", _topPanel.transform, "Back", theme);
            DesktopUi.SetRect((RectTransform)_backButton.transform, new Vector2(1f, .5f), new Vector2(1f, .5f), new Vector2(-70f, 0f), new Vector2(112f, 44f));
            _backObject = _backButton.gameObject;
            _backButton.onClick.AddListener(LeaveWorld);
            var settingsButton = DesktopUi.CreateButton("Settings", _canvasObject.transform, "Settings", theme);
            DesktopUi.SetRect((RectTransform)settingsButton.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-204f, -56f), new Vector2(124f, 44f));
            settingsButton.onClick.AddListener(ShowSettings);
            var quitButton = DesktopUi.CreateButton("Quit", _canvasObject.transform, "Quit", theme);
            DesktopUi.SetRect((RectTransform)quitButton.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-76f, -56f), new Vector2(112f, 44f));
            quitButton.onClick.AddListener(Application.Quit);

            _topicInput = DesktopUi.CreateInputField("Topic input", _canvasObject.transform, ConstellationPlaceholder, theme);
            DesktopUi.SetRect((RectTransform)_topicInput.transform, new Vector2(.5f, 0f), new Vector2(.5f, 0f), new Vector2(0f, 56f), new Vector2(720f, 56f));
            _topicInput.onSubmit.AddListener(SubmitTopic);

            _status = DesktopUi.CreateText("Status", _canvasObject.transform, string.Empty, 20f, TextAlignmentOptions.Center, theme);
            DesktopUi.SetRect(_status.rectTransform, new Vector2(.5f, 0f), new Vector2(.5f, 0f), new Vector2(0f, 112f), new Vector2(900f, 34f));

            _sidePanel = DesktopUi.CreatePanel("Selected topic", _canvasObject.transform, theme).gameObject;
            DesktopUi.SetRect((RectTransform)_sidePanel.transform, new Vector2(1f, .5f), new Vector2(1f, .5f), new Vector2(-200f, 0f), new Vector2(360f, 560f));
            _sideTopic = DesktopUi.CreateText("Topic", _sidePanel.transform, string.Empty, 28f, TextAlignmentOptions.TopLeft, theme);
            DesktopUi.SetRect(_sideTopic.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -52f), new Vector2(-40f, 64f));
            _sideSummary = DesktopUi.CreateText("Summary", _sidePanel.transform, string.Empty, 18f, TextAlignmentOptions.TopLeft, theme);
            _sideSummary.textWrappingMode = TextWrappingModes.Normal;
            _sideSummary.overflowMode = TextOverflowModes.Ellipsis;
            DesktopUi.SetRect(_sideSummary.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0f, 63f), new Vector2(-40f, -236f));
            _enterButton = DesktopUi.CreateButton("Enter world", _sidePanel.transform, "Enter world", theme);
            DesktopUi.SetRect((RectTransform)_enterButton.transform, new Vector2(0f, 0f), new Vector2(.5f, 0f), new Vector2(20f, 80f), new Vector2(-30f, 48f));
            _enterButton.onClick.AddListener(EnterWorld);
            _editButton = DesktopUi.CreateButton("Edit", _sidePanel.transform, "Edit", theme);
            DesktopUi.SetRect((RectTransform)_editButton.transform, new Vector2(.5f, 0f), new Vector2(1f, 0f), new Vector2(10f, 80f), new Vector2(-40f, 48f));
            _editButton.onClick.AddListener(ShowEdit);
            _deleteButton = DesktopUi.CreateButton("Delete", _sidePanel.transform, "Delete", theme);
            DesktopUi.SetRect((RectTransform)_deleteButton.transform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(20f, 22f), new Vector2(-40f, 48f));
            _deleteLabel = _deleteButton.GetComponentInChildren<TextMeshProUGUI>();
            _deleteButton.onClick.AddListener(DeleteCurrent);
            _editInput = DesktopUi.CreateInputField("Edit input", _sidePanel.transform, "Describe the change and press Enter", theme);
            DesktopUi.SetRect((RectTransform)_editInput.transform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(20f, 142f), new Vector2(-40f, 52f));
            _editObject = _editInput.gameObject;
            _editInput.onSubmit.AddListener(SubmitEdit);

            _hint = DesktopUi.CreateText("Controls", _canvasObject.transform, ConstellationHint, 16f, TextAlignmentOptions.BottomLeft, theme);
            _hint.color = new Color(1f, 1f, 1f, .7f);
            DesktopUi.SetRect(_hint.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(720f, 18f), new Vector2(1400f, 30f));

            _backObject.SetActive(false);
            _worldTopic.gameObject.SetActive(false);
            _sidePanel.SetActive(false);
            _editObject.SetActive(false);
            _topPanel.enabled = false;
            DesktopUi.SetActive(_canvasObject, !ZapPlatform.IsMenuOpen);
        }

        private void SubmitTopic(string value)
        {
            if (_controller == null || _controller.Busy || string.IsNullOrWhiteSpace(value)) return;
            _controller.SubmitText(value.Trim(), false);
            _topicInput.SetTextWithoutNotify(string.Empty);
            KeepFocus(_topicInput);
        }

        private void SubmitEdit(string value)
        {
            if (_controller == null || _controller.Busy || string.IsNullOrWhiteSpace(value)) return;
            _controller.SubmitText(value.Trim(), true);
            _editInput.SetTextWithoutNotify(string.Empty);
            _editObject.SetActive(false);
        }

        private void ShowEdit()
        {
            if (_controller == null || _controller.Busy) return;
            _editObject.SetActive(true);
            KeepFocus(_editInput);
        }

        private void EnterWorld()
        {
            if (_controller != null && _controller.Current != null)
                _controller.EnterWorld(_controller.Current);
        }

        private void LeaveWorld()
        {
            if (_controller != null) _controller.LeaveWorld();
        }

        private void ShowSettings()
        {
            if (DesktopSettings.Instance != null) DesktopSettings.Instance.Show();
        }

        private void DeleteCurrent()
        {
            if (_controller == null || _controller.Busy) return;
            var current = _controller.Current;
            if (current == null) return;
            if (_deleteArmedFor == current && Time.unscaledTime < _deleteConfirmUntil)
            {
                ClearDeleteConfirm();
                _controller.DeleteBall(current);
                return;
            }

            _deleteArmedFor = current;
            _deleteConfirmUntil = Time.unscaledTime + DeleteConfirmSeconds;
            DesktopUi.SetText(_deleteLabel, "Click again to delete");
        }

        private void ClearDeleteConfirm()
        {
            _deleteArmedFor = null;
            _deleteConfirmUntil = 0f;
            DesktopUi.SetText(_deleteLabel, "Delete");
        }

        private static void KeepFocus(TMP_InputField field)
        {
            field.Select();
            field.ActivateInputField();
        }
    }
}
