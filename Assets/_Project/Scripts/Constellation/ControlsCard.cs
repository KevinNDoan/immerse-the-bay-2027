using System;
using TMPro;
using UnityEngine;
using Zap.Core;

namespace Zap.Constellation
{
    public sealed class ControlsCard : MonoBehaviour
    {
        const float SurfaceZ = .001f;
        const float PanelZ = .002f;
        const float TextZ = 0f;
        const float DividerHeight = .002f;
        const float LevelBarHeight = .006f;
        const float KeyColumnWidth = .15f;
        const float Gap = .003f;

        const string Keys = "A\nB\nGrip\nBoth grips\nTrigger\nX";
        const string Labels = "Hold and say a new topic\nHold and say a change\nGrab and throw\nPull and stretch\nLink topics, sweep to cut\nEnter the selected topic";
        const string CompactHint = "Sticks: move and look · Trigger: event\nA: speak · B: edit topic · Y: return";

        readonly Transform[] _outline = new Transform[4];

        ZapTheme _theme;
        Transform _panel;
        Transform _divider;
        Transform _levelBar;
        TextMeshPro _title;
        TextMeshPro _status;
        TextMeshPro _keys;
        TextMeshPro _labels;
        TextMeshPro _hint;
        Material _panelMaterial;
        Material _outlineMaterial;
        Material _barMaterial;
        string _lastTitle;
        string _lastStatus;
        int _homeFrameVersion = -1;
        bool _built;

        public bool Visible
        {
            get => gameObject.activeSelf;
            set
            {
                if (value && !_built) return;
                gameObject.SetActive(value);
            }
        }

        public bool IsCompact { get; private set; }

        public static ControlsCard Create(Transform parent)
        {
            var root = new GameObject("ZAP controls card");
            root.transform.SetParent(parent, false);
            var card = root.AddComponent<ControlsCard>();
            card.Build();
            return card;
        }

        public void Show(string title, string status, bool inWorld, bool listening, float level, bool busy)
        {
            if (!_built) return;

            if (inWorld != IsCompact)
                ApplyLayout(inWorld);

            var nextTitle = title ?? "ZAP!";
            if (!string.Equals(_lastTitle, nextTitle, StringComparison.Ordinal))
            {
                _lastTitle = nextTitle;
                _title.text = nextTitle;
            }

            var nextStatus = status ?? string.Empty;
            if (!string.Equals(_lastStatus, nextStatus, StringComparison.Ordinal))
            {
                _lastStatus = nextStatus;
                _status.text = nextStatus;
            }

            UpdateLevelBar(listening, level, busy);
            if (_homeFrameVersion != HomeFrame.Version)
                AlignToHomeFrame();
        }

        void Build()
        {
            var solidShader = Shader.Find("Zap/Solid");
            if (solidShader == null)
            {
                Debug.LogError("ZAP controls card: shader Zap/Solid not found; the card is hidden.");
                gameObject.SetActive(false);
                return;
            }

            _theme = ZapTheme.Current;

            var panelShader = Shader.Find("Zap/Glass");
            if (panelShader == null) panelShader = solidShader;
            _panelMaterial = new Material(panelShader);
            _panelMaterial.SetColor("_BaseColor", _theme.cardColor);
            _panelMaterial.renderQueue = 2999;

            _outlineMaterial = new Material(solidShader);
            _outlineMaterial.SetColor("_BaseColor", _theme.cardOutlineColor);
            _barMaterial = new Material(solidShader);
            _barMaterial.SetColor("_BaseColor", _theme.cardTextColor);

            _panel = CreateQuad("Panel", _panelMaterial, PanelZ);
            _outline[0] = CreateQuad("Outline top", _outlineMaterial, SurfaceZ);
            _outline[1] = CreateQuad("Outline bottom", _outlineMaterial, SurfaceZ);
            _outline[2] = CreateQuad("Outline left", _outlineMaterial, SurfaceZ);
            _outline[3] = CreateQuad("Outline right", _outlineMaterial, SurfaceZ);
            _divider = CreateQuad("Divider", _outlineMaterial, SurfaceZ);
            _levelBar = CreateQuad("Level bar", _barMaterial, SurfaceZ);

            _title = CreateText("Title", _theme.cardTitleSize, _theme.cardTextColor, 1);
            _status = CreateText("Status", _theme.cardStatusSize, _theme.cardTextColor, 3);
            _keys = CreateText("Keys", _theme.cardBodySize, _theme.cardDimTextColor, 6);
            _labels = CreateText("Labels", _theme.cardBodySize, _theme.cardTextColor, 6);
            _hint = CreateText("Hint", _theme.cardHintSize, _theme.cardDimTextColor, 2);
            _title.textWrappingMode = TextWrappingModes.NoWrap;
            _keys.textWrappingMode = TextWrappingModes.NoWrap;
            _labels.textWrappingMode = TextWrappingModes.NoWrap;
            _keys.text = Keys;
            _labels.text = Labels;
            _hint.text = CompactHint;

            ApplyLayout(false);
            _levelBar.gameObject.SetActive(false);
            _built = true;
        }

        Transform CreateQuad(string objectName, Material material, float z)
        {
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = objectName;
            quad.transform.SetParent(transform, false);
            quad.transform.localPosition = new Vector3(0f, 0f, z);
            UnityEngine.Object.Destroy(quad.GetComponent<Collider>());
            quad.GetComponent<Renderer>().sharedMaterial = material;
            return quad.transform;
        }

        TextMeshPro CreateText(string objectName, float size, Color color, int maxLines)
        {
            var textObject = new GameObject(objectName);
            textObject.transform.SetParent(transform, false);
            textObject.transform.localPosition = new Vector3(0f, 0f, TextZ);
            var text = textObject.AddComponent<TextMeshPro>();
            text.richText = false;
            text.enableAutoSizing = false;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.alignment = TextAlignmentOptions.TopLeft;
            text.rectTransform.pivot = new Vector2(0f, 1f);
            text.fontSize = size;
            text.color = color;
            text.maxVisibleLines = maxLines;
            if (_theme.font != null) text.font = _theme.font;
            return text;
        }

        void ApplyLayout(bool compact)
        {
            IsCompact = compact;
            var size = compact ? _theme.cardCompactSize : _theme.cardSize;
            ResizeFrame(size);

            _title.gameObject.SetActive(!compact);
            _divider.gameObject.SetActive(!compact);
            _keys.gameObject.SetActive(!compact);
            _labels.gameObject.SetActive(!compact);
            _hint.gameObject.SetActive(compact);

            if (compact)
                LayoutCompact(size);
            else
                LayoutFull(size);

            AlignToHomeFrame();
        }

        void ResizeFrame(Vector2 size)
        {
            _panel.localScale = new Vector3(size.x, size.y, 1f);
            var edge = _theme.cardOutlineWidth;
            _outline[0].localPosition = new Vector3(0f, size.y * .5f - edge * .5f, SurfaceZ);
            _outline[0].localScale = new Vector3(size.x, edge, 1f);
            _outline[1].localPosition = new Vector3(0f, -size.y * .5f + edge * .5f, SurfaceZ);
            _outline[1].localScale = new Vector3(size.x, edge, 1f);
            _outline[2].localPosition = new Vector3(-size.x * .5f + edge * .5f, 0f, SurfaceZ);
            _outline[2].localScale = new Vector3(edge, size.y, 1f);
            _outline[3].localPosition = new Vector3(size.x * .5f - edge * .5f, 0f, SurfaceZ);
            _outline[3].localScale = new Vector3(edge, size.y, 1f);
        }

        static float LineHeight(float fontSize) => fontSize * .125f;

        void LayoutFull(Vector2 size)
        {
            var left = -size.x * .5f + _theme.cardPadding;
            var top = size.y * .5f - _theme.cardPadding;
            var bottom = -size.y * .5f + _theme.cardPadding;
            var innerWidth = Mathf.Max(0f, size.x - _theme.cardPadding * 2f);

            var titleHeight = LineHeight(_theme.cardTitleSize);
            PlaceText(_title, left, top, innerWidth, titleHeight);
            top -= titleHeight + Gap;

            var statusHeight = 3 * LineHeight(_theme.cardStatusSize);
            PlaceText(_status, left, top, innerWidth, statusHeight);
            top -= statusHeight + Gap;

            PlaceLevelBar(left, top, innerWidth);
            top -= LevelBarHeight + Gap;

            _divider.localPosition = new Vector3(0f, top - DividerHeight * .5f, SurfaceZ);
            _divider.localScale = new Vector3(innerWidth, DividerHeight, 1f);
            top -= DividerHeight + Gap;

            var bodyHeight = Mathf.Max(0f, top - bottom);
            PlaceText(_keys, left, top, KeyColumnWidth, bodyHeight);
            PlaceText(_labels, left + KeyColumnWidth, top, Mathf.Max(0f, innerWidth - KeyColumnWidth), bodyHeight);
        }

        void LayoutCompact(Vector2 size)
        {
            var left = -size.x * .5f + _theme.cardPadding;
            var top = size.y * .5f - _theme.cardPadding;
            var innerWidth = Mathf.Max(0f, size.x - _theme.cardPadding * 2f);

            var statusHeight = 3 * LineHeight(_theme.cardStatusSize);
            PlaceText(_status, left, top, innerWidth, statusHeight);
            top -= statusHeight + Gap;

            PlaceLevelBar(left, top, innerWidth);
            top -= LevelBarHeight + Gap;

            PlaceText(_hint, left, top, innerWidth, 2 * LineHeight(_theme.cardHintSize));
        }

        void PlaceText(TextMeshPro text, float left, float top, float width, float height)
        {
            text.rectTransform.sizeDelta = new Vector2(width, height);
            text.transform.localPosition = new Vector3(left, top, TextZ);
        }

        void PlaceLevelBar(float left, float top, float innerWidth)
        {
            _levelBar.localPosition = new Vector3(left, top - LevelBarHeight * .5f, SurfaceZ);
            _levelBar.localScale = new Vector3(innerWidth, LevelBarHeight, 1f);
        }

        void UpdateLevelBar(bool listening, float level, bool busy)
        {
            var visible = listening || busy;
            _levelBar.gameObject.SetActive(visible);
            if (!visible) return;

            var size = IsCompact ? _theme.cardCompactSize : _theme.cardSize;
            var innerWidth = Mathf.Max(0f, size.x - _theme.cardPadding * 2f);
            var fraction = listening
                ? Mathf.Clamp01(level)
                : Mathf.Lerp(.1f, .4f, Mathf.PingPong(Time.unscaledTime, 1f));
            var width = innerWidth * fraction;
            var left = -size.x * .5f + _theme.cardPadding;
            var position = _levelBar.localPosition;
            position.x = left + width * .5f;
            _levelBar.localPosition = position;
            _levelBar.localScale = new Vector3(width, LevelBarHeight, 1f);
        }

        void AlignToHomeFrame()
        {
            var headHeight = HomeFrame.Version == 0 ? 1.5f : HomeFrame.HeadHeight;
            transform.position = new Vector3(0f, headHeight - _theme.controlsDropBelowEyes, _theme.controlsDistance);
            var tilt = Mathf.Atan2(_theme.controlsDropBelowEyes, _theme.controlsDistance) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(tilt, 0f, 0f);
            _homeFrameVersion = HomeFrame.Version;
        }

        void OnDestroy()
        {
            if (_panelMaterial != null) Destroy(_panelMaterial);
            if (_outlineMaterial != null) Destroy(_outlineMaterial);
            if (_barMaterial != null) Destroy(_barMaterial);
        }
    }
}
