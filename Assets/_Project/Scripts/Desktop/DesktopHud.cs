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

        private ConstellationController _controller;
        private TextMeshProUGUI _worldTopic;
        private TextMeshProUGUI _status;
        private TextMeshProUGUI _sideTopic;
        private TextMeshProUGUI _sideSummary;
        private TextMeshProUGUI _hint;
        private TMP_InputField _topicInput;
        private TMP_InputField _editInput;
        private Button _backButton;
        private Button _enterButton;
        private Button _editButton;
        private GameObject _backObject;
        private GameObject _sidePanel;
        private GameObject _editObject;
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

            SetActive(_backObject, inWorld);
            SetActive(_worldTopic.gameObject, inWorld);
            SetActive(_sidePanel, showSide);
            if (!showSide) SetActive(_editObject, false);

            SetText(_worldTopic, inWorld && current != null ? current.Data.topic : string.Empty);
            SetText(_status, _controller.IsListening ? "Listening…" : _controller.Status);
            SetText(_hint, inWorld ? WorldHint : ConstellationHint);
            if (showSide)
            {
                SetText(_sideTopic, current.Data.topic);
                SetText(_sideSummary, current.Data.summary);
            }

            if (!_hasState || inWorld != _lastInWorld)
            {
                var placeholder = (TextMeshProUGUI)_topicInput.placeholder;
                SetText(placeholder, inWorld ? WorldPlaceholder : ConstellationPlaceholder);
                _lastInWorld = inWorld;
            }

            if (!_hasState || busy != _lastBusy)
            {
                _topicInput.interactable = !busy;
                _editInput.interactable = !busy;
                _backButton.interactable = !busy;
                _enterButton.interactable = !busy;
                _editButton.interactable = !busy;
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
            var canvasObject = new GameObject("Desktop HUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);

            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = .5f;

            var top = CreatePanel("Top left", canvasObject.transform, theme);
            SetRect(top.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(20f, -92f), new Vector2(700f, 72f));
            var logo = CreateText("Logo", top.transform, "ZAP!", 36f, TextAlignmentOptions.MidlineLeft, theme);
            SetRect(logo.rectTransform, new Vector2(0f, .5f), new Vector2(0f, .5f), new Vector2(18f, 0f), new Vector2(110f, 56f));
            _worldTopic = CreateText("World topic", top.transform, string.Empty, 24f, TextAlignmentOptions.MidlineLeft, theme);
            SetRect(_worldTopic.rectTransform, new Vector2(0f, .5f), new Vector2(0f, .5f), new Vector2(128f, 0f), new Vector2(400f, 48f));
            _backButton = CreateButton("Back", top.transform, "Back", theme);
            SetRect((RectTransform)_backButton.transform, new Vector2(1f, .5f), new Vector2(1f, .5f), new Vector2(-70f, 0f), new Vector2(112f, 44f));
            _backObject = _backButton.gameObject;
            _backButton.onClick.AddListener(LeaveWorld);

            _topicInput = CreateInputField("Topic input", canvasObject.transform, ConstellationPlaceholder, theme);
            SetRect((RectTransform)_topicInput.transform, new Vector2(.5f, 0f), new Vector2(.5f, 0f), new Vector2(0f, 56f), new Vector2(720f, 56f));
            _topicInput.onSubmit.AddListener(SubmitTopic);

            _status = CreateText("Status", canvasObject.transform, string.Empty, 20f, TextAlignmentOptions.Center, theme);
            SetRect(_status.rectTransform, new Vector2(.5f, 0f), new Vector2(.5f, 0f), new Vector2(0f, 112f), new Vector2(900f, 34f));

            _sidePanel = CreatePanel("Selected topic", canvasObject.transform, theme).gameObject;
            SetRect((RectTransform)_sidePanel.transform, new Vector2(1f, .5f), new Vector2(1f, .5f), new Vector2(-200f, 0f), new Vector2(360f, 560f));
            _sideTopic = CreateText("Topic", _sidePanel.transform, string.Empty, 28f, TextAlignmentOptions.TopLeft, theme);
            SetRect(_sideTopic.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -52f), new Vector2(-40f, 64f));
            _sideSummary = CreateText("Summary", _sidePanel.transform, string.Empty, 18f, TextAlignmentOptions.TopLeft, theme);
            _sideSummary.enableWordWrapping = true;
            _sideSummary.overflowMode = TextOverflowModes.Ellipsis;
            SetRect(_sideSummary.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0f, 22f), new Vector2(-40f, -154f));
            _enterButton = CreateButton("Enter world", _sidePanel.transform, "Enter world", theme);
            SetRect((RectTransform)_enterButton.transform, new Vector2(0f, 0f), new Vector2(.5f, 0f), new Vector2(20f, 22f), new Vector2(-30f, 48f));
            _enterButton.onClick.AddListener(EnterWorld);
            _editButton = CreateButton("Edit", _sidePanel.transform, "Edit", theme);
            SetRect((RectTransform)_editButton.transform, new Vector2(.5f, 0f), new Vector2(1f, 0f), new Vector2(10f, 22f), new Vector2(-40f, 48f));
            _editButton.onClick.AddListener(ShowEdit);
            _editInput = CreateInputField("Edit input", _sidePanel.transform, "Describe the change and press Enter", theme);
            SetRect((RectTransform)_editInput.transform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(20f, 84f), new Vector2(-40f, 52f));
            _editObject = _editInput.gameObject;
            _editInput.onSubmit.AddListener(SubmitEdit);

            _hint = CreateText("Controls", canvasObject.transform, ConstellationHint, 14f, TextAlignmentOptions.BottomLeft, theme);
            _hint.color = new Color(1f, 1f, 1f, .7f);
            SetRect(_hint.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(20f, 18f), new Vector2(920f, 30f));

            _backObject.SetActive(false);
            _worldTopic.gameObject.SetActive(false);
            _sidePanel.SetActive(false);
            _editObject.SetActive(false);
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

        private static void KeepFocus(TMP_InputField field)
        {
            field.Select();
            field.ActivateInputField();
        }

        private static Image CreatePanel(string name, Transform parent, ZapTheme theme)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(Image));
            obj.transform.SetParent(parent, false);
            var image = obj.GetComponent<Image>();
            var color = theme.voidColor;
            color.a = .85f;
            image.color = color;
            return image;
        }

        private static TextMeshProUGUI CreateText(string name, Transform parent, string value, float size, TextAlignmentOptions alignment, ZapTheme theme)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            obj.transform.SetParent(parent, false);
            var text = obj.GetComponent<TextMeshProUGUI>();
            text.text = value;
            text.fontSize = size;
            text.alignment = alignment;
            text.color = Color.white;
            text.raycastTarget = false;
            text.richText = false;
            if (theme.font != null) text.font = theme.font;
            return text;
        }

        private static Button CreateButton(string name, Transform parent, string label, ZapTheme theme)
        {
            var image = CreatePanel(name, parent, theme);
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            var text = CreateText("Label", image.transform, label, 18f, TextAlignmentOptions.Center, theme);
            SetRect(text.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            return button;
        }

        private static TMP_InputField CreateInputField(string name, Transform parent, string placeholderValue, ZapTheme theme)
        {
            var image = CreatePanel(name, parent, theme);
            var input = image.gameObject.AddComponent<TMP_InputField>();
            input.targetGraphic = image;

            var textArea = new GameObject("Text Area", typeof(RectTransform), typeof(RectMask2D));
            textArea.transform.SetParent(image.transform, false);
            var viewport = (RectTransform)textArea.transform;
            SetRect(viewport, Vector2.zero, Vector2.one, new Vector2(18f, 8f), new Vector2(-36f, -16f));

            var placeholder = CreateText("Placeholder", textArea.transform, placeholderValue, 20f, TextAlignmentOptions.MidlineLeft, theme);
            placeholder.color = new Color(1f, 1f, 1f, .5f);
            SetRect(placeholder.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            var text = CreateText("Text", textArea.transform, string.Empty, 20f, TextAlignmentOptions.MidlineLeft, theme);
            SetRect(text.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            input.textViewport = viewport;
            input.textComponent = text;
            input.placeholder = placeholder;
            input.lineType = TMP_InputField.LineType.SingleLine;
            input.caretColor = Color.white;
            input.selectionColor = new Color(1f, 1f, 1f, .25f);
            return input;
        }

        private static void SetRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition, Vector2 sizeDelta)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = sizeDelta;
        }

        private static void SetText(TMP_Text target, string value)
        {
            value ??= string.Empty;
            if (target.text != value) target.text = value;
        }

        private static void SetActive(GameObject target, bool value)
        {
            if (target.activeSelf != value) target.SetActive(value);
        }
    }
}
