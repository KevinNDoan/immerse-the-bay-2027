using System;
using TMPro;
using UnityEngine;
using Zap.Core;

namespace Zap.Constellation
{
    // The controls card is a window. It sits where the person leaves it in their play space: it never follows a
    // controller. An interactive card can be dragged, resized from its corners and tucked away with a controller ray.
    public sealed class ControlsCard : MonoBehaviour
    {
        const float SurfaceZ = .001f;
        const float PanelZ = .002f;
        const float TextZ = 0f;
        const float CursorZ = -.002f;
        const float LevelBarHeight = .006f;
        const float Gap = .003f;

        // Window controls, in card metres before the person's own scale.
        const float HitMargin = .02f;
        const float ButtonInset = .045f;
        const float GrabBarDrop = .022f;
        const float GrabBarWidth = .12f;
        const float GrabBarHeight = .006f;
        const float CornerGap = .008f;
        const float CornerLength = .03f;
        const float CornerThickness = .004f;
        const float IconLength = .022f;
        const float IconThickness = .004f;
        const float CursorSize = .012f;
        const float MinReach = .3f;
        const float MaxReach = 4f;
        const float MaxPointerReach = 6f;
        const float PushSpeed = 1f;
        const float PushDeadZone = .2f;
        const float RigSearchInterval = 1f;
        const string RigName = "XR Origin (XR Rig)";
        const string PointerHint = "Trigger: drag to move · corners resize\nTop-right button: hide or show";

        enum Zone { None, Body, Corner, Button }

        struct Pointer
        {
            public bool Pressed;
            public bool StartedOffCard;
            public bool Swallow;
            public Zone Hover;
            public Zone Drag;
            public Vector2 Grab;
            public float GrabRadius;
            public float Reach;
            public Transform Cursor;
        }

        readonly Transform[] _outline = new Transform[4];
        readonly Transform[] _corners = new Transform[8];
        readonly Pointer[] _pointers = new Pointer[2];

        ZapTheme _theme;
        FrostPanel _frost;
        Transform _panel;
        Transform _levelBar;
        Transform _grabBar;
        Transform _buttonBar;
        Transform _buttonCross;
        TextMeshPro _title;
        TextMeshPro _step;
        TextMeshPro _status;
        TextMeshPro _hint;
        Material _panelMaterial;
        Material _outlineMaterial;
        Material _barMaterial;
        Material _handleMaterial;
        string _lastTitle;
        string _lastStep;
        string _lastStatus;
        string _lastHint;
        int _homeFrameVersion = -1;
        int _handleState = -1;
        bool _built;

        // The pose is kept relative to the rig, so the card stays put in the play space while a world moves the rig.
        Transform _rig;
        Transform _poseRig;
        float _nextRigSearch;
        Vector3 _localPosition;
        Quaternion _localRotation = Quaternion.identity;
        Vector3 _worldPosition;
        Quaternion _worldRotation = Quaternion.identity;
        float _scale = 1f;

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

        // True when a controller can move, resize and hide the card.
        public bool Interactive { get; private set; }

        // True while the card is tucked away to its title bar.
        public bool Collapsed { get; private set; }

        // True once the person has moved the card themselves.
        public bool Placed { get; private set; }

        // The person's own size for the card, as a multiple of its normal size.
        public float Scale => _scale;

        // Current size in card metres, before the person's own scale.
        public Vector2 Size => CurrentSize;

        // The hand moving or resizing the card (0 left, 1 right), or -1.
        public int DragHand
        {
            get
            {
                for (int i = 0; i < _pointers.Length; i++)
                    if (_pointers[i].Drag != Zone.None) return i;
                return -1;
            }
        }

        public bool IsDragging => DragHand >= 0;

        // Where the hide and show button is, in the world.
        public Vector3 ButtonPosition => transform.TransformPoint(ButtonCentre(CurrentSize));

        // Where a corner is, in the world. Pass 1 or -1 for right or left, and for top or bottom.
        public Vector3 CornerPosition(int right, int top)
        {
            var size = CurrentSize;
            return transform.TransformPoint(new Vector3(Mathf.Sign(right) * size.x * .5f, Mathf.Sign(top) * size.y * .5f, 0f));
        }

        public static ControlsCard Create(Transform parent, bool interactive = false)
        {
            var root = new GameObject("ZAP controls card");
            root.transform.SetParent(parent, false);
            var card = root.AddComponent<ControlsCard>();
            card.Interactive = interactive;
            card.Build();
            return card;
        }

        public void Show(string title, string step, string status, string hint, bool listening, float level, bool busy, bool followViewer = false)
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

            // While a controller points at the card, the hint explains the window controls.
            var nextHint = AnyHover && !IsDragging ? PointerHint : hint ?? string.Empty;
            if (!string.Equals(_lastHint, nextHint, StringComparison.Ordinal))
            {
                _lastHint = nextHint;
                _hint.text = nextHint;
                _hint.gameObject.SetActive(!Collapsed && nextHint.Length != 0);
            }

            UpdateLevelBar(listening, level, busy);
            RefreshHandles();
            if (followViewer)
                AlignToViewer();
            else
                ApplyPose();
        }

        /// <summary>
        /// Feeds one controller's pointing ray to the card. Call once per frame for each hand (0 left, 1 right).
        /// Returns true while this hand's trigger belongs to the card: it is over the card or dragging it.
        /// </summary>
        /// <param name="blockedBeyond">Distance along the ray to the nearest thing the person could be aiming at
        /// instead (a star, an object, a pane). A card farther away than that leaves the trigger alone.</param>
        public bool Point(int hand, bool tracked, Ray ray, bool pressed, float push = 0f, float blockedBeyond = float.PositiveInfinity)
        {
            if (!_built || !Interactive || hand < 0 || hand >= _pointers.Length) return false;
            ref var pointer = ref _pointers[hand];
            bool down = pressed && !pointer.Pressed;
            pointer.Pressed = pressed;
            if (!pressed) pointer.Swallow = false;
            if (!tracked || !gameObject.activeInHierarchy)
            {
                Release(ref pointer);
                return pointer.Swallow;
            }
            // A drag cut short (the card was hidden under it) keeps the trigger until it is let go.
            if (pointer.Swallow) return true;

            bool onPlane = TryHit(ray, out var local, out var reach);
            var zone = onPlane ? ZoneAt(local) : Zone.None;
            // Something nearer is in the way: this hand is aiming at that, not at the card.
            if (pointer.Drag == Zone.None && reach > blockedBeyond) zone = Zone.None;

            if (down)
            {
                // A press that starts elsewhere (a link or a cut in progress) never turns into a card drag.
                if (zone == Zone.None || IsDragging) pointer.StartedOffCard = true;
                else
                {
                    pointer.Drag = zone;
                    pointer.Grab = local;
                    pointer.GrabRadius = Mathf.Max(local.magnitude, .05f);
                    pointer.Reach = Mathf.Clamp(reach, MinReach, MaxReach);
                }
            }

            if (pointer.Drag != Zone.None)
            {
                if (pressed)
                {
                    if (pointer.Drag == Zone.Body) Move(ref pointer, ray, push);
                    else if (pointer.Drag == Zone.Corner && onPlane) Resize(ref pointer, local);
                }
                else
                {
                    if (pointer.Drag == Zone.Button && zone == Zone.Button) SetCollapsed(!Collapsed);
                    pointer.Drag = Zone.None;
                }
            }
            if (!pressed) pointer.StartedOffCard = false;

            pointer.Hover = pointer.Drag != Zone.None ? pointer.Drag : (pointer.StartedOffCard ? Zone.None : zone);
            // Dragging can move or rescale the card, so the cursor is placed from a fresh hit.
            bool showCursor = pointer.Hover != Zone.None && TryHit(ray, out local, out _);
            if (pointer.Cursor != null)
            {
                if (pointer.Cursor.gameObject.activeSelf != showCursor) pointer.Cursor.gameObject.SetActive(showCursor);
                if (showCursor) pointer.Cursor.localPosition = new Vector3(local.x, local.y, CursorZ);
            }
            return pointer.Hover != Zone.None;
        }

        public void SetCollapsed(bool collapsed)
        {
            if (!_built || Collapsed == collapsed) return;
            float before = CurrentSize.y;
            Collapsed = collapsed;
            ApplyLayout(IsCompact);
            // The top edge stays where it is, so the title bar and its button do not jump away from the pointer.
            float shift = (before - CurrentSize.y) * .5f * _scale;
            SetWorldPose(_worldPosition + _worldRotation * Vector3.up * shift, _worldRotation);
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

            _panel = CreateQuad("Panel", _panelMaterial, PanelZ);
            _outline[0] = CreateQuad("Outline top", _outlineMaterial, SurfaceZ);
            _outline[1] = CreateQuad("Outline bottom", _outlineMaterial, SurfaceZ);
            _outline[2] = CreateQuad("Outline left", _outlineMaterial, SurfaceZ);
            _outline[3] = CreateQuad("Outline right", _outlineMaterial, SurfaceZ);
            _levelBar = CreateQuad("Level bar", _barMaterial, SurfaceZ);

            // Frosted glass replaces the dark panel and its outline bars when the theme turns it on.
            _frost = FrostPanel.Create(transform, "Frosted panel", new Vector3(0f, 0f, PanelZ));
            if (_frost != null)
            {
                _panel.gameObject.SetActive(false);
                foreach (var edge in _outline) edge.gameObject.SetActive(false);
            }
            _barMaterial.SetColor("_BaseColor", TextColor);

            _title = CreateText("Title", _theme.cardTitleSize, TextColor, 1);
            _step = CreateText("Step", _theme.cardStepSize, TextColor, 3);
            _status = CreateText("Status", _theme.cardStatusSize, TextColor, 3);
            _hint = CreateText("Hint", _theme.cardHintSize, DimTextColor, 2);
            _title.textWrappingMode = TextWrappingModes.NoWrap;

            if (Interactive) BuildHandles(solidShader);

            _lastHint = string.Empty;
            ApplyLayout(true);
            _hint.gameObject.SetActive(false);
            _levelBar.gameObject.SetActive(false);
            SetWorldPose(DefaultPosition, Quaternion.identity);
            _built = true;
        }

        void BuildHandles(Shader solidShader)
        {
            _handleMaterial = new Material(solidShader);
            _handleMaterial.SetColor("_BaseColor", HandleColor(false));
            _grabBar = CreateQuad("Grab bar", _handleMaterial, SurfaceZ);
            for (int i = 0; i < _corners.Length; i++)
            {
                _corners[i] = CreateQuad("Corner handle " + i, _barMaterial, SurfaceZ);
                _corners[i].gameObject.SetActive(false);
            }
            _buttonBar = CreateQuad("Hide button", _barMaterial, SurfaceZ);
            _buttonCross = CreateQuad("Show button", _barMaterial, SurfaceZ);
            _buttonCross.gameObject.SetActive(false);
            for (int i = 0; i < _pointers.Length; i++)
            {
                var cursor = CreateQuad("Pointer cursor " + i, _barMaterial, CursorZ);
                cursor.localScale = new Vector3(CursorSize, CursorSize, 1f);
                cursor.localRotation = Quaternion.Euler(0f, 0f, 45f);
                cursor.gameObject.SetActive(false);
                _pointers[i].Cursor = cursor;
            }
        }

        Color TextColor => _frost != null ? _theme.glassTextColor : _theme.cardTextColor;
        Color DimTextColor => _frost != null ? _theme.glassDimTextColor : _theme.cardDimTextColor;

        // Zap/Solid has no transparency, so a resting handle is the text colour mixed toward grey.
        Color HandleColor(bool hot)
        {
            var color = hot ? TextColor : Color.Lerp(TextColor, Color.gray, .6f);
            color.a = 1f;
            return color;
        }

        Vector2 CurrentSize
        {
            get
            {
                if (_theme == null) return Vector2.zero;
                if (Collapsed) return new Vector2(_theme.cardSize.x, _theme.cardPadding * 2f + LineHeight(_theme.cardTitleSize));
                return IsCompact ? _theme.cardCompactSize : _theme.cardSize;
            }
        }

        bool AnyHover
        {
            get
            {
                for (int i = 0; i < _pointers.Length; i++)
                    if (_pointers[i].Hover != Zone.None) return true;
                return false;
            }
        }

        static Transform Viewer
        {
            get
            {
                var camera = Camera.main;
                return camera != null ? camera.transform : null;
            }
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
            var size = CurrentSize;
            ResizeFrame(size);

            bool open = !Collapsed;
            _step.gameObject.SetActive(open && !compact);
            _status.gameObject.SetActive(open);
            _hint.gameObject.SetActive(open && !string.IsNullOrEmpty(_lastHint));
            if (!open) _levelBar.gameObject.SetActive(false);
            _status.maxVisibleLines = compact ? 3 : 2;
            _status.color = compact ? TextColor : DimTextColor;
            Layout(size, compact);
            if (Interactive) PlaceHandles(size);
        }

        void ResizeFrame(Vector2 size)
        {
            _panel.localScale = new Vector3(size.x, size.y, 1f);
            if (_frost != null) _frost.SetSize(size);
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

            // The title stops short of the hide and show button.
            var titleWidth = Interactive
                ? Mathf.Max(0f, size.x * .5f - ButtonInset - _theme.cardButtonSize - Gap - left)
                : innerWidth;
            var titleHeight = LineHeight(_theme.cardTitleSize);
            PlaceText(_title, left, top, titleWidth, titleHeight);
            top -= titleHeight + Gap;
            if (Collapsed) return;

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

        void PlaceHandles(Vector2 size)
        {
            float halfWidth = size.x * .5f, halfHeight = size.y * .5f;
            _grabBar.localPosition = new Vector3(0f, -halfHeight - GrabBarDrop, SurfaceZ);
            _grabBar.localScale = new Vector3(GrabBarWidth, GrabBarHeight, 1f);

            // Each corner gets an L drawn just outside the card: one flat piece and one upright piece.
            int index = 0;
            for (int right = -1; right <= 1; right += 2)
            {
                for (int top = -1; top <= 1; top += 2)
                {
                    float x = right * (halfWidth + CornerGap), y = top * (halfHeight + CornerGap);
                    _corners[index].localPosition = new Vector3(x - right * CornerLength * .5f, y, SurfaceZ);
                    _corners[index].localScale = new Vector3(CornerLength, CornerThickness, 1f);
                    index++;
                    _corners[index].localPosition = new Vector3(x, y - top * CornerLength * .5f, SurfaceZ);
                    _corners[index].localScale = new Vector3(CornerThickness, CornerLength, 1f);
                    index++;
                }
            }

            var button = ButtonCentre(size);
            _buttonBar.localPosition = new Vector3(button.x, button.y, SurfaceZ);
            _buttonCross.localPosition = new Vector3(button.x, button.y, SurfaceZ);
            _handleState = -1;
            RefreshHandles();
        }

        // Shows what the pointers are over: the corner handles while the card is pointed at, and the hot control.
        void RefreshHandles()
        {
            if (!Interactive || _grabBar == null) return;
            bool hover = false, body = false, button = false;
            for (int i = 0; i < _pointers.Length; i++)
            {
                var zone = _pointers[i].Hover;
                hover |= zone != Zone.None;
                body |= zone == Zone.Body;
                button |= zone == Zone.Button;
            }
            int state = (hover ? 1 : 0) | (body ? 2 : 0) | (button ? 4 : 0) | (Collapsed ? 8 : 0);
            if (state == _handleState) return;
            _handleState = state;

            _handleMaterial.SetColor("_BaseColor", HandleColor(body));
            bool corners = hover && !Collapsed;
            for (int i = 0; i < _corners.Length; i++)
                if (_corners[i].gameObject.activeSelf != corners) _corners[i].gameObject.SetActive(corners);
            float grow = button ? 1.3f : 1f;
            _buttonBar.localScale = new Vector3(IconLength * grow, IconThickness * grow, 1f);
            _buttonCross.localScale = new Vector3(IconThickness * grow, IconLength * grow, 1f);
            // A bar hides the card; the upright piece turns it into a plus that brings the card back.
            if (_buttonCross.gameObject.activeSelf != Collapsed) _buttonCross.gameObject.SetActive(Collapsed);
        }

        void UpdateLevelBar(bool listening, float level, bool busy)
        {
            var visible = (listening || busy) && !Collapsed;
            _levelBar.gameObject.SetActive(visible);
            if (!visible) return;

            var size = CurrentSize;
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

        Vector3 ButtonCentre(Vector2 size)
        {
            float half = _theme.cardButtonSize * .5f;
            return new Vector3(size.x * .5f - ButtonInset - half,
                size.y * .5f - _theme.cardPadding - LineHeight(_theme.cardTitleSize) * .5f, 0f);
        }

        // Where the ray meets the card's plane, in card metres. The card is read from its -Z side.
        bool TryHit(Ray ray, out Vector2 local, out float reach)
        {
            local = default;
            reach = 0f;
            var origin = transform.InverseTransformPoint(ray.origin);
            var direction = transform.InverseTransformPoint(ray.origin + ray.direction) - origin;
            if (origin.z >= 0f || !(direction.z > 1e-5f)) return false;
            reach = -origin.z / direction.z;
            if (!(reach <= MaxPointerReach)) return false;
            var hit = origin + direction * reach;
            local = new Vector2(hit.x, hit.y);
            return true;
        }

        Zone ZoneAt(Vector2 point)
        {
            var size = CurrentSize;
            float halfWidth = size.x * .5f, halfHeight = size.y * .5f;
            if (Mathf.Abs(point.x) > halfWidth + HitMargin || point.y > halfHeight + HitMargin
                || point.y < -halfHeight - GrabBarDrop - HitMargin)
                return Zone.None;

            var button = ButtonCentre(size);
            float half = _theme.cardButtonSize * .5f;
            if (Mathf.Abs(point.x - button.x) <= half && Mathf.Abs(point.y - button.y) <= half) return Zone.Button;

            // A tucked-away card is only a title bar, so its corners move it like the rest of the bar.
            if (!Collapsed && halfWidth - Mathf.Abs(point.x) <= _theme.cardCornerReach
                && halfHeight - Mathf.Abs(point.y) <= _theme.cardCornerReach && point.y >= -halfHeight - HitMargin)
                return Zone.Corner;
            return Zone.Body;
        }

        // The grabbed spot follows the ray at the distance it was grabbed; the stick pushes it away or pulls it in.
        void Move(ref Pointer pointer, Ray ray, float push)
        {
            if (Mathf.Abs(push) > PushDeadZone)
                pointer.Reach = Mathf.Clamp(pointer.Reach + push * PushSpeed * Time.unscaledDeltaTime, MinReach, MaxReach);
            var target = ray.origin + ray.direction * pointer.Reach;

            // Upright, facing the person: only the direction around them changes.
            var rotation = transform.rotation;
            var head = Viewer;
            if (head != null)
            {
                var away = Vector3.ProjectOnPlane(target - head.position, Vector3.up);
                if (away.sqrMagnitude > 1e-4f) rotation = Quaternion.LookRotation(away, Vector3.up);
            }
            var position = target - rotation * new Vector3(pointer.Grab.x * _scale, pointer.Grab.y * _scale, 0f);
            Placed = true;
            SetWorldPose(position, rotation);
        }

        // The card grows or shrinks about its centre so the grabbed corner stays under the pointer.
        void Resize(ref Pointer pointer, Vector2 local)
        {
            float min = Mathf.Min(_theme.cardMinScale, _theme.cardMaxScale);
            float max = Mathf.Max(_theme.cardMinScale, _theme.cardMaxScale);
            float next = Mathf.Clamp(_scale * local.magnitude / pointer.GrabRadius, min, max);
            if (float.IsNaN(next) || Mathf.Abs(next - _scale) < 1e-4f) return;
            _scale = next;
            transform.localScale = Vector3.one * _scale;
        }

        // Lets go of the card without forgetting a trigger that is still held: a drag that was cut short keeps the
        // trigger until it is released, and any other held press stays with whatever it was doing.
        void Release(ref Pointer pointer)
        {
            if (pointer.Pressed)
            {
                if (pointer.Drag != Zone.None) pointer.Swallow = true;
                else if (!pointer.Swallow) pointer.StartedOffCard = true;
            }
            else
            {
                pointer.StartedOffCard = false;
                pointer.Swallow = false;
            }
            pointer.Hover = Zone.None;
            pointer.Drag = Zone.None;
            if (pointer.Cursor != null && pointer.Cursor.gameObject.activeSelf) pointer.Cursor.gameObject.SetActive(false);
        }

        Vector3 DefaultPosition
        {
            get
            {
                var headHeight = HomeFrame.Version == 0 ? 1.5f : HomeFrame.HeadHeight;
                return new Vector3(0f, headHeight - _theme.controlsDropBelowEyes, _theme.controlsDistance);
            }
        }

        Transform Rig
        {
            get
            {
                if (_rig == null)
                {
                    // Drop a destroyed rig for a true null, so the pose code can tell it has gone.
                    _rig = null;
                    if (Time.unscaledTime >= _nextRigSearch)
                    {
                        _nextRigSearch = Time.unscaledTime + RigSearchInterval;
                        var found = GameObject.Find(RigName);
                        _rig = found != null ? found.transform : null;
                    }
                }
                return _rig;
            }
        }

        void SetWorldPose(Vector3 position, Quaternion rotation)
        {
            var rig = Rig;
            _poseRig = rig;
            _localPosition = rig != null ? rig.InverseTransformPoint(position) : position;
            _localRotation = rig != null ? Quaternion.Inverse(rig.rotation) * rotation : rotation;
            _worldPosition = position;
            _worldRotation = rotation;
            transform.SetPositionAndRotation(position, rotation);
        }

        void ApplyPose()
        {
            var rig = Rig;
            // The rig appeared or went away: carry the card over where it is.
            if (!ReferenceEquals(rig, _poseRig)) SetWorldPose(_worldPosition, _worldRotation);

            if (_homeFrameVersion != HomeFrame.Version)
            {
                _homeFrameVersion = HomeFrame.Version;
                // The home frame moved the rig under the person. A card they placed keeps its spot in the
                // workspace; an untouched card goes back to straight ahead.
                if (Placed) SetWorldPose(_worldPosition, _worldRotation);
                else SetWorldPose(DefaultPosition, Quaternion.identity);
                return;
            }

            _worldPosition = rig != null ? rig.TransformPoint(_localPosition) : _localPosition;
            _worldRotation = rig != null ? rig.rotation * _localRotation : _localRotation;
            transform.SetPositionAndRotation(_worldPosition, _worldRotation);
        }

        // Used by short-lived prompts (the delete confirmation) that must appear wherever the person is looking.
        void AlignToViewer()
        {
            var head = Viewer;
            if (head == null) return;
            Vector3 offset = head.forward * 1.1f + Vector3.down * .3f;
            if (offset.sqrMagnitude < .01f) offset = head.forward;
            transform.position = head.position + offset.normalized * Mathf.Max(_theme.controlsMinDistanceFromFace, offset.magnitude);
            transform.rotation = Quaternion.LookRotation(transform.position - head.position);
        }

        void OnDisable()
        {
            for (int i = 0; i < _pointers.Length; i++) Release(ref _pointers[i]);
        }

        void OnDestroy()
        {
            if (_panelMaterial != null) Destroy(_panelMaterial);
            if (_outlineMaterial != null) Destroy(_outlineMaterial);
            if (_barMaterial != null) Destroy(_barMaterial);
            if (_handleMaterial != null) Destroy(_handleMaterial);
        }
    }
}
