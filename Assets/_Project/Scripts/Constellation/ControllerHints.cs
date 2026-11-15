using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using Zap.Core;

namespace Zap.Constellation
{
    public enum ControlId
    {
        RightA,
        RightB,
        RightGrip,
        RightTrigger,
        RightStick,
        LeftX,
        LeftY,
        LeftGrip,
        LeftTrigger,
        LeftStick
    }

    public readonly struct ControlHint
    {
        public readonly ControlId Id;
        public readonly string Label;
        public readonly bool Highlight;

        public ControlHint(ControlId id, string label, bool highlight = false)
        {
            Id = id;
            Label = label;
            Highlight = highlight;
        }
    }

    public sealed class ControllerHints : MonoBehaviour
    {
        const float ColumnOffset = .13f;
        const float RowSpacing = .03f;
        const float LookAngle = 25f;
        const float LookDistance = .8f;
        const float LookDistanceSqr = LookDistance * LookDistance;
        const float ShowDelay = .25f;
        const float HideDelay = .5f;
        const float SearchInterval = 1f;
        const float PulseSpeed = 1.5f;
        const string LeftPath = "XR Origin (XR Rig)/Camera Offset/Left Controller";
        const string RightPath = "XR Origin (XR Rig)/Camera Offset/Right Controller";

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int ColorId = Shader.PropertyToID("_Color");

        sealed class LabelSlot
        {
            public bool Left;
            public string PartName;
            public GameObject Root;
            public TextMeshPro Text;
            public LineRenderer Line;
            public Transform Part;
            public Renderer Renderer;
            public Color BaseColor;
            public MaterialPropertyBlock SavedBlock;
            public MaterialPropertyBlock WorkingBlock;
            public bool HadBlock;
            public bool BlockSet;
            public bool Active;
            public bool Highlight;
            public bool Placed;
        }

        readonly int[] _order = new int[10];

        LabelSlot[] _slots;
        Material _lineMaterial;
        ZapTheme _theme;
        Transform _left;
        Transform _right;
        ControlHint[] _shown;
        float _nextSearch;
        float _leftTimer;
        float _rightTimer;
        bool _leftShown;
        bool _rightShown;
        bool _always;
        bool _built;

        public static ControllerHints Create(Transform parent)
        {
            var root = new GameObject("ZAP controller hints");
            root.transform.SetParent(parent, false);
            var hints = root.AddComponent<ControllerHints>();
            hints.Build();
            return hints;
        }

        public void Show(ControlHint[] hints, bool always)
        {
            if (!_built) return;
            _always = always;
            if (ReferenceEquals(hints, _shown)) return;
            _shown = hints;
            Apply();
        }

        void Build()
        {
            var shader = Shader.Find("Zap/Solid");
            if (shader == null)
            {
                Debug.LogError("ZAP controller hints: shader Zap/Solid not found; the hints are hidden.");
                return;
            }

            _theme = ZapTheme.Current;
            _lineMaterial = new Material(shader);
            _lineMaterial.SetColor(BaseColorId, _theme.cardOutlineColor);
            _slots = new LabelSlot[10];
            for (int i = 0; i < _slots.Length; i++)
                _slots[i] = CreateSlot((ControlId)i);
            _built = true;
        }

        LabelSlot CreateSlot(ControlId id)
        {
            bool left = id >= ControlId.LeftX;
            var root = new GameObject("Hint " + id);
            root.transform.SetParent(transform, false);

            var textObject = new GameObject("Text");
            textObject.transform.SetParent(root.transform, false);
            var text = textObject.AddComponent<TextMeshPro>();
            text.richText = false;
            text.enableAutoSizing = false;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Overflow;
            text.alignment = left ? TextAlignmentOptions.MidlineRight : TextAlignmentOptions.MidlineLeft;
            text.rectTransform.pivot = new Vector2(left ? 1f : 0f, .5f);
            text.rectTransform.sizeDelta = new Vector2(.48f, _theme.hintTextSize * .125f);
            text.fontSize = _theme.hintTextSize;
            text.color = _theme.cardTextColor;
            text.text = string.Empty;
            if (_theme.font != null) text.font = _theme.font;

            var lineObject = new GameObject("Line");
            lineObject.transform.SetParent(root.transform, false);
            var line = lineObject.AddComponent<LineRenderer>();
            line.sharedMaterial = _lineMaterial;
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.widthMultiplier = _theme.hintLineWidth;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.alignment = LineAlignment.View;

            root.SetActive(false);
            return new LabelSlot
            {
                Left = left,
                PartName = PartName(id),
                Root = root,
                Text = text,
                Line = line
            };
        }

        static string PartName(ControlId id)
        {
            switch (id)
            {
                case ControlId.RightA:
                case ControlId.LeftX:
                    return "Button_A";
                case ControlId.RightB:
                case ControlId.LeftY:
                    return "Button_B";
                case ControlId.RightGrip:
                case ControlId.LeftGrip:
                    return "Bumper";
                case ControlId.RightTrigger:
                case ControlId.LeftTrigger:
                    return "Trigger";
                default:
                    return "ThumbStick";
            }
        }

        void Apply()
        {
            for (int i = 0; i < _slots.Length; i++)
                _slots[i].Active = false;

            var hints = _shown;
            if (hints != null)
            {
                for (int i = 0; i < hints.Length; i++)
                {
                    int index = (int)hints[i].Id;
                    if ((uint)index >= (uint)_slots.Length) continue;
                    var slot = _slots[index];
                    slot.Active = true;
                    slot.Text.text = hints[i].Label ?? string.Empty;
                    SetHighlight(slot, hints[i].Highlight);
                }
            }

            for (int i = 0; i < _slots.Length; i++)
            {
                if (!_slots[i].Active)
                    SetHighlight(_slots[i], false);
            }
        }

        void SetHighlight(LabelSlot slot, bool highlight)
        {
            if (slot.Highlight && !highlight)
                ClearBlock(slot);
            slot.Highlight = highlight;
        }

        void LateUpdate()
        {
            if (!_built) return;
            var camera = Camera.main;
            if (camera == null) return;

            if (NeedsSearch() && Time.unscaledTime >= _nextSearch)
            {
                Search();
                _nextSearch = Time.unscaledTime + SearchInterval;
            }

            var head = camera.transform;
            _lineMaterial.SetColor(BaseColorId, _theme.cardOutlineColor);
            if (_always)
            {
                _leftTimer = 0f;
                _rightTimer = 0f;
            }
            else
            {
                UpdateGaze(ref _leftShown, ref _leftTimer, _left, head);
                UpdateGaze(ref _rightShown, ref _rightTimer, _right, head);
            }

            LayoutHand(true, _left, head, _always || _leftShown);
            LayoutHand(false, _right, head, _always || _rightShown);
            Pulse();
        }

        bool NeedsSearch()
        {
            for (int i = 0; i < _slots.Length; i++)
            {
                var slot = _slots[i];
                if (slot.Active && slot.Part == null) return true;
            }
            return false;
        }

        void Search()
        {
            for (int i = 0; i < _slots.Length; i++)
            {
                var slot = _slots[i];
                if (!slot.Active || slot.Part != null) continue;
                var hand = slot.Left ? _left : _right;
                if (hand == null)
                {
                    hand = GameObject.Find(slot.Left ? LeftPath : RightPath)?.transform;
                    if (slot.Left) _left = hand;
                    else _right = hand;
                    if (hand == null) continue;
                }
                var part = FindNamed(hand, slot.PartName);
                if (part == null) continue;
                slot.Part = part;
                slot.Renderer = part.GetComponent<Renderer>();
                if (slot.Renderer == null)
                    slot.Renderer = FindChildRenderer(part);
            }
        }

        static Transform FindNamed(Transform node, string name)
        {
            for (int i = 0; i < node.childCount; i++)
            {
                var child = node.GetChild(i);
                if (child.name == name) return child;
                var found = FindNamed(child, name);
                if (found != null) return found;
            }
            return null;
        }

        static Renderer FindChildRenderer(Transform node)
        {
            for (int i = 0; i < node.childCount; i++)
            {
                var child = node.GetChild(i);
                var renderer = child.GetComponent<Renderer>();
                if (renderer != null) return renderer;
                renderer = FindChildRenderer(child);
                if (renderer != null) return renderer;
            }
            return null;
        }

        void UpdateGaze(ref bool shown, ref float timer, Transform controller, Transform head)
        {
            bool looking = false;
            if (controller != null)
            {
                Vector3 to = controller.position - head.position;
                float sqr = to.sqrMagnitude;
                if (sqr > .000001f && sqr <= LookDistanceSqr && Vector3.Angle(head.forward, to) < LookAngle)
                    looking = true;
            }

            float dt = Time.unscaledDeltaTime;
            if (looking)
            {
                if (shown)
                    timer = 0f;
                else
                {
                    timer += dt;
                    if (timer >= ShowDelay)
                    {
                        shown = true;
                        timer = 0f;
                    }
                }
            }
            else if (shown)
            {
                timer += dt;
                if (timer >= HideDelay)
                {
                    shown = false;
                    timer = 0f;
                }
            }
            else
                timer = 0f;
        }

        void LayoutHand(bool left, Transform controller, Transform head, bool show)
        {
            int count = 0;
            for (int i = 0; i < _slots.Length; i++)
            {
                var slot = _slots[i];
                if (slot.Left != left) continue;
                slot.Placed = false;
                if (!show || controller == null || !slot.Active || slot.Part == null) continue;
                slot.Placed = true;
                _order[count++] = i;
            }

            Vector3 up = head.up;
            if (count > 1) Sort(_order, count, up);

            Vector3 side = left ? -head.right : head.right;
            Vector3 origin = controller != null ? controller.position + side * ColumnOffset : Vector3.zero;
            float mid = (count - 1) * .5f;
            float width = _theme.hintLineWidth;

            for (int i = 0; i < count; i++)
            {
                var slot = _slots[_order[i]];
                Vector3 pos = origin + up * ((mid - i) * RowSpacing);
                Vector3 forward = pos - head.position;
                if (forward.sqrMagnitude < .000001f) forward = head.forward;
                if (!slot.Root.activeSelf) slot.Root.SetActive(true);
                slot.Root.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(forward, up));
                slot.Line.widthMultiplier = width;
                slot.Line.SetPosition(0, slot.Part.position);
                slot.Line.SetPosition(1, pos);
            }

            for (int i = 0; i < _slots.Length; i++)
            {
                var slot = _slots[i];
                if (slot.Left == left && !slot.Placed && slot.Root.activeSelf)
                    slot.Root.SetActive(false);
            }
        }

        void Sort(int[] order, int count, Vector3 up)
        {
            for (int i = 1; i < count; i++)
            {
                int key = order[i];
                float height = Vector3.Dot(_slots[key].Part.position, up);
                int j = i - 1;
                while (j >= 0 && Vector3.Dot(_slots[order[j]].Part.position, up) < height)
                {
                    order[j + 1] = order[j];
                    j--;
                }
                order[j + 1] = key;
            }
        }

        void Pulse()
        {
            float t = Mathf.PingPong(Time.unscaledTime * PulseSpeed, 1f);
            Color highlight = _theme.hintHighlightColor;
            for (int i = 0; i < _slots.Length; i++)
            {
                var slot = _slots[i];
                if (!slot.Highlight || slot.Renderer == null)
                {
                    ClearBlock(slot);
                    continue;
                }

                if (!slot.BlockSet)
                    CaptureBlock(slot);

                Color color = Color.Lerp(slot.BaseColor, highlight, t);
                slot.WorkingBlock.SetColor(BaseColorId, color);
                slot.WorkingBlock.SetColor(ColorId, color);
                slot.Renderer.SetPropertyBlock(slot.WorkingBlock);
            }
        }

        void CaptureBlock(LabelSlot slot)
        {
            var renderer = slot.Renderer;
            slot.HadBlock = renderer.HasPropertyBlock();
            if (slot.WorkingBlock == null)
                slot.WorkingBlock = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(slot.WorkingBlock);
            if (slot.HadBlock)
            {
                if (slot.SavedBlock == null)
                    slot.SavedBlock = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(slot.SavedBlock);
            }
            slot.BaseColor = ReadBaseColor(renderer);
            slot.BlockSet = true;
        }

        static Color ReadBaseColor(Renderer renderer)
        {
            var material = renderer.sharedMaterial;
            if (material == null) return Color.white;
            if (material.HasProperty(BaseColorId)) return material.GetColor(BaseColorId);
            if (material.HasProperty(ColorId)) return material.GetColor(ColorId);
            return Color.white;
        }

        void ClearBlock(LabelSlot slot)
        {
            if (!slot.BlockSet) return;
            slot.BlockSet = false;
            if (slot.Renderer == null) return;
            slot.Renderer.SetPropertyBlock(slot.HadBlock ? slot.SavedBlock : null);
        }

        void OnDestroy()
        {
            if (_slots != null)
            {
                for (int i = 0; i < _slots.Length; i++)
                    ClearBlock(_slots[i]);
            }
            if (_lineMaterial != null) Destroy(_lineMaterial);
        }
    }
}
