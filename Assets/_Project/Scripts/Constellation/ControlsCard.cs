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
        const float LevelBarHeight = .006f;
        const float Gap = .003f;

        readonly Transform[] _outline = new Transform[4];

        ZapTheme _theme;
        Transform _panel;
        Transform _levelBar;
        TextMeshPro _title;
        TextMeshPro _step;
        TextMeshPro _status;
        TextMeshPro _hint;
        Material _panelMaterial;
        Material _outlineMaterial;
        Material _barMaterial;
        string _lastTitle;
        string _lastStep;
        string _lastStatus;
        string _lastHint;
        int _homeFrameVersion = -1;
        bool _followingViewer;
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

        public void Show(string title, string step, string status, string hint, bool listening, float level, bool busy, bool followViewer = false, Transform hand = null)
        {
            if (!_built) return;

            var compact = string.IsNullOrEmpty(step);
            if (compact != IsCompact)
                ApplyLayout(compact);

            var nextTitle = title ?? "ZAP!";
            if (!string.Equals(_lastTitle, nextTitle, StringComparison.Ordinal))
            {
                _lastTitle = nextTitle;
                _title.text = nextTitle;
            }

            var nextStep = step ?? string.Empty;
            if (!string.Equals(_lastStep, nextStep, StringComparison.Ordinal))
            {
                _lastStep = nextStep;
                _step.text = nextStep;
            }

            var nextStatus = status ?? string.Empty;
            if (!string.Equals(_lastStatus, nextStatus, StringComparison.Ordinal))
            {
                _lastStatus = nextStatus;
                _status.text = nextStatus;
            }

            var nextHint = hint ?? string.Empty;
            if (!string.Equals(_lastHint, nextHint, StringComparison.Ordinal))
            {
                _lastHint = nextHint;
                _hint.text = nextHint;
                _hint.gameObject.SetActive(nextHint.Length != 0);
            }

            UpdateLevelBar(listening, level, busy);
            if (followViewer)
                AlignToViewer(hand);
            else if (_followingViewer || _homeFrameVersion != HomeFrame.Version)
                AlignToHomeFrame();
            _followingViewer = followViewer;
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
            _levelBar = CreateQuad("Level bar", _barMaterial, SurfaceZ);

            _title = CreateText("Title", _theme.cardTitleSize, _theme.cardTextColor, 1);
            _step = CreateText("Step", _theme.cardStepSize, _theme.cardTextColor, 3);
            _status = CreateText("Status", _theme.cardStatusSize, _theme.cardTextColor, 3);
            _hint = CreateText("Hint", _theme.cardHintSize, _theme.cardDimTextColor, 2);
            _title.textWrappingMode = TextWrappingModes.NoWrap;

            ApplyLayout(true);
            _hint.gameObject.SetActive(false);
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

            _step.gameObject.SetActive(!compact);
            _status.maxVisibleLines = compact ? 3 : 2;
            _status.color = compact ? _theme.cardTextColor : _theme.cardDimTextColor;
            Layout(size, compact);

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

        void Layout(Vector2 size, bool compact)
        {
            var left = -size.x * .5f + _theme.cardPadding;
            var top = size.y * .5f - _theme.cardPadding;
            var innerWidth = Mathf.Max(0f, size.x - _theme.cardPadding * 2f);

            var titleHeight = LineHeight(_theme.cardTitleSize);
            PlaceText(_title, left, top, innerWidth, titleHeight);
            top -= titleHeight + Gap;

            if (!compact)
            {
                var stepHeight = 3 * LineHeight(_theme.cardStepSize);
                PlaceText(_step, left, top, innerWidth, stepHeight);
                top -= stepHeight + Gap;
            }

            var statusLines = compact ? 3 : 2;
            var statusHeight = statusLines * LineHeight(_theme.cardStatusSize);
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
            transform.rotation = Quaternion.identity;
            _homeFrameVersion = HomeFrame.Version;
        }

        void AlignToViewer(Transform hand)
        {
            var camera = Camera.main;
            if (camera == null) return;
            var head = camera.transform;
            Vector3 offset = hand != null ? hand.position + Vector3.up * .13f + hand.forward * .18f - head.position
                : head.forward * 1.1f + Vector3.down * .3f;
            if (offset.sqrMagnitude < .01f) offset = head.forward;
            transform.position = head.position + offset.normalized * Mathf.Max(_theme.controlsMinDistanceFromFace, offset.magnitude);
            transform.rotation = Quaternion.LookRotation(transform.position - head.position);
        }

        void OnDestroy()
        {
            if (_panelMaterial != null) Destroy(_panelMaterial);
            if (_outlineMaterial != null) Destroy(_outlineMaterial);
            if (_barMaterial != null) Destroy(_barMaterial);
        }
    }
}
