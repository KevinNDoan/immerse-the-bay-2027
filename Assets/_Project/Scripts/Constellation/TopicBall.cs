using System;
using TMPro;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using Zap.Core;

namespace Zap.Constellation
{
    [RequireComponent(typeof(Rigidbody), typeof(SphereCollider), typeof(XRGrabInteractable))]
    public sealed class TopicBall : MonoBehaviour
    {
        public const float TossSpeed = 2.5f;
        // How much larger a link-target preview is drawn.
        private const float HighlightScale = 1.25f;
        // Compact note exports: 16 Figma px of shadow padding plus a 6 px corner, at 4x.
        private const float CompactNoteBorderPixels = 88f;
        private const float CompactNoteInset = 64f / ZapArt.PixelsPerMetre;
        // Expanded note exports: a 6 px corner at 4x, no padding.
        private const float ExpandedNoteBorderPixels = 24f;

        public NodeData Data { get; private set; }
        public XRGrabInteractable Grab { get; private set; }
        public Rigidbody Body { get; private set; }
        public bool IsTossed { get; private set; }
        public bool Highlighted { get; set; }
        public event Action<TopicBall> Selected;
        public event Action<TopicBall> EnterRequested;
        public event Action<TopicBall> Tossed;
        private TextMeshPro _note;
        private Transform _orb;
        private Transform _star;
        private SpriteRenderer _core, _glow, _ring, _sparks, _orbit, _paper;
        private SpriteRenderer[] _layers = Array.Empty<SpriteRenderer>();
        private string _paperText;
        private float _paperFontSize;
        private Vector2 _paperBox;
        private Bounds _textBounds;
        private bool _selected;
        private float _heldSince;
        private float _nearFaceSince = -1;
        private float _tossedAt;
        private Vector3 _tossStartScale;
        private const int MotionSampleCount = 16;
        private readonly Vector3[] _motionPositions = new Vector3[MotionSampleCount];
        private readonly float[] _motionTimes = new float[MotionSampleCount];
        private int _motionWrite;
        private int _motionCount;
        private Vector3 _releaseVelocity;
        private int _releaseFrame = -1;
        private bool _entered;

        public void Initialize(NodeData data, Material material)
        {
            Data = data;
            Body = GetComponent<Rigidbody>();
            Body.useGravity = false;
            Body.mass = .1f;
            Body.linearDamping = .7f;
            Body.angularDamping = 2f;
            Body.interpolation = RigidbodyInterpolation.Interpolate;
            Body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            Body.linearVelocity = Vector3.zero;
            Body.angularVelocity = Vector3.zero;
            Body.constraints = RigidbodyConstraints.FreezeAll;
            GetComponent<SphereCollider>().radius = .065f;
            Grab = GetComponent<XRGrabInteractable>();
            Grab.movementType = XRBaseInteractable.MovementType.VelocityTracking;
            Grab.throwOnDetach = true;
            Grab.forceGravityOnDetach = false;
            Grab.throwVelocityScale = 1.1f;
            Grab.throwAngularVelocityScale = .5f;
            Grab.selectEntered.AddListener(OnGrab);
            Grab.selectExited.AddListener(OnRelease);
            Grab.hoverEntered.AddListener(_ => Selected?.Invoke(this));
            var theme = ZapTheme.Current;
            if (theme.nodeCore != null) BuildStar(theme);
            else BuildOrb(material, theme);
            var note = new GameObject("Topic note");
            note.transform.SetParent(transform, false);
            _note = note.AddComponent<TextMeshPro>();
            _note.alignment = TextAlignmentOptions.Top;
            _note.rectTransform.pivot = new Vector2(.5f, 1f);
            _note.overflowMode = TextOverflowModes.Ellipsis;
            _note.richText = false;
            _note.sortingOrder = 1;
            // The paper sits behind the text; the text faces away from its forward axis.
            _paper = ZapArt.Layer(note.transform, "Note paper", theme.noteBackground, 0, CompactNoteBorderPixels);
            if (_paper != null) _paper.transform.localPosition = new Vector3(0, 0, .001f);
            Refresh();
        }

        private void BuildOrb(Material material, ZapTheme theme)
        {
            var orb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            orb.name = "Topic orb";
            Destroy(orb.GetComponent<Collider>());
            _orb = orb.transform;
            _orb.SetParent(transform, false);
            _orb.localScale = Vector3.one * theme.nodeDiameter;
            orb.GetComponent<Renderer>().sharedMaterial = material;
        }

        private void BuildStar(ZapTheme theme)
        {
            _star = new GameObject("Star").transform;
            _star.SetParent(transform, false);
            _glow = ZapArt.Layer(_star, "Glow", theme.nodeGlow, 0);
            _orbit = ZapArt.Layer(_star, "Orbit", theme.nodeOrbit, 1);
            _ring = ZapArt.Layer(_star, "Ring", theme.nodeRing, 2);
            _sparks = ZapArt.Layer(_star, "Sparks", theme.nodeSparks, 3);
            _core = ZapArt.Layer(_star, "Core", theme.nodeCore, 4);
            _layers = new[] { _glow, _orbit, _ring, _sparks, _core };
            // Nudge each layer toward the viewer so they never fight for the same depth.
            foreach (var layer in _layers)
                if (layer != null) layer.transform.localPosition = Vector3.back * (.0005f * layer.sortingOrder);
        }

        // The sphere, or the star artwork when the theme has it.
        private Transform Visual => _orb != null ? _orb : _star;

        public void SetSelected(bool value) { _selected = value; }
        public void Refresh() { if (_note != null) _note.text = Data.topic; }
        private void OnGrab(SelectEnterEventArgs args)
        {
            _releaseFrame = -1;
            _motionWrite = 0;
            _motionCount = 0;
            Body.constraints = RigidbodyConstraints.None;
            _heldSince = Time.unscaledTime;
            _entered = false;
            Selected?.Invoke(this);
        }
        private void OnRelease(SelectExitEventArgs args)
        {
            _nearFaceSince = -1;
            _releaseVelocity = VelocityFromMotion();
            _releaseFrame = Time.frameCount;
        }

        private void Update()
        {
            // Not set up yet: Initialize has not run.
            if (Grab == null) return;
            if (Grab.isSelected)
            {
                _motionPositions[_motionWrite] = transform.position;
                _motionTimes[_motionWrite] = Time.time;
                _motionWrite = (_motionWrite + 1) % MotionSampleCount;
                if (_motionCount < MotionSampleCount) _motionCount++;
            }
            if (_releaseFrame < 0 || Time.frameCount <= _releaseFrame || Grab.isSelected) return;
            _releaseFrame = -1;
            Release(_releaseVelocity);
        }

        private Vector3 VelocityFromMotion()
        {
            if (_motionCount < 2) return Vector3.zero;
            int latest = (_motionWrite + MotionSampleCount - 1) % MotionSampleCount;
            float latestTime = _motionTimes[latest];
            float target = latestTime - .1f;
            int best = -1;
            float bestGap = float.MaxValue;
            for (int age = 1; age < _motionCount; age++)
            {
                int index = (_motionWrite - 1 - age + MotionSampleCount * 2) % MotionSampleCount;
                float gap = Mathf.Abs(_motionTimes[index] - target);
                if (gap < bestGap)
                {
                    bestGap = gap;
                    best = index;
                }
            }
            float dt = latestTime - _motionTimes[best];
            if (dt < .02f) return Vector3.zero;
            return (_motionPositions[latest] - _motionPositions[best]) / dt;
        }

        public static bool IsToss(Vector3 releaseVelocity)
        {
            return releaseVelocity.sqrMagnitude >= TossSpeed * TossSpeed;
        }

        public void Release(Vector3 velocity)
        {
            if (IsTossed) return;
            Body.angularVelocity = Vector3.zero;
            if (!IsToss(velocity))
            {
                Body.linearVelocity = Vector3.zero;
                Body.constraints = RigidbodyConstraints.FreezeAll;
                return;
            }

            IsTossed = true;
            Tossed?.Invoke(this);
            Body.constraints = RigidbodyConstraints.None;
            Body.linearVelocity = velocity;
            Grab.enabled = false;
            GetComponent<SphereCollider>().enabled = false;
            _note.gameObject.SetActive(false);
            _tossedAt = Time.unscaledTime;
            var visual = Visual;
            _tossStartScale = visual != null ? visual.localScale : Vector3.one;
        }

        private void FixedUpdate()
        {
            if (Body == null || Grab.isSelected || Body.isKinematic) return;
            Body.linearVelocity = Vector3.ClampMagnitude(Body.linearVelocity, 8);
        }

        private void LateUpdate()
        {
            if (IsTossed)
            {
                float progress = Mathf.Clamp01((Time.unscaledTime - _tossedAt) / .5f);
                var visual = Visual;
                if (visual != null) visual.localScale = Vector3.Lerp(_tossStartScale, Vector3.zero, progress);
                if (progress >= 1f) Destroy(gameObject);
                return;
            }
            var camera = Camera.main;
            if (Data == null || camera == null) return;
            var theme = ZapTheme.Current;
            bool held = Grab.isSelected;
            if (_orb != null) _orb.localScale = Vector3.one * (Highlighted ? theme.nodeSelectedDiameter * HighlightScale : (_selected ? theme.nodeSelectedDiameter : theme.nodeDiameter));
            if (_star != null) UpdateStar(theme, camera.transform.position, held);
            var distance = Vector3.Distance(transform.position, camera.transform.position);
            _note.transform.position = transform.position + Vector3.down * theme.noteOffsetBelowNode;
            _note.transform.rotation = Quaternion.LookRotation(_note.transform.position - camera.transform.position);
            _note.rectTransform.sizeDelta = theme.noteBoxSize;
            _note.color = _paper != null ? theme.ink : theme.textColor;
            if (theme.font != null) _note.font = theme.font;
            bool showSummary = (held || (_selected && distance < theme.summaryShowDistance)) && !string.IsNullOrEmpty(Data.summary);
            _note.text = showSummary ? Data.topic + "\n" + Data.summary : Data.topic;
            _note.fontSize = showSummary ? theme.summaryTextSize : theme.topicTextSize;
            _note.gameObject.SetActive(distance < theme.noteHideDistance);
            if (_paper != null && _note.gameObject.activeSelf) UpdatePaper(theme, showSummary, held);
            if (held && !_entered && Time.unscaledTime - _heldSince > .8f && distance < .26f)
            {
                if (_nearFaceSince < 0) _nearFaceSince = Time.unscaledTime;
                if (Time.unscaledTime - _nearFaceSince > .65f)
                { _entered = true; EnterRequested?.Invoke(this); }
            }
            else _nearFaceSince = -1;
        }

        private void UpdateStar(ZapTheme theme, Vector3 cameraPosition, bool held)
        {
            _star.rotation = Quaternion.LookRotation(_star.position - cameraPosition);
            float size = theme.starCoreDiameter;
            // A link-target preview looks pointed-at, and larger still.
            bool pointed = _selected || Highlighted;
            float highlight = Highlighted ? HighlightScale : 1f;
            float coreScale = (held ? theme.starGrabbedCoreScale : pointed ? theme.starPointedCoreScale : 1f) * highlight;
            float glowScale = (held ? theme.starGrabbedGlowScale : pointed ? theme.starPointedGlowScale : 1f) * highlight;
            ZapArt.SetDiameter(_core, size * coreScale);
            ZapArt.SetDiameter(_glow, size * theme.starGlowScale * glowScale);
            ZapArt.SetDiameter(_ring, size * theme.starRingScale);
            ZapArt.SetDiameter(_sparks, size * theme.starSparksScale);
            ZapArt.SetDiameter(_orbit, size * theme.starOrbitScale);
            ZapArt.Show(_ring, pointed && !held);
            ZapArt.Show(_sparks, held);
            ZapArt.Show(_orbit, !string.IsNullOrEmpty(Data.image));
            foreach (var layer in _layers)
                if (layer != null) layer.color = theme.nodeColor;
        }

        private void UpdatePaper(ZapTheme theme, bool expanded, bool held)
        {
            expanded &= theme.noteExpanded != null;
            Texture2D texture = expanded ? theme.noteExpanded
                : held && theme.noteBackgroundHeld != null ? theme.noteBackgroundHeld
                : _selected && theme.noteBackgroundPointed != null ? theme.noteBackgroundPointed
                : theme.noteBackground;
            var sprite = ZapArt.GetSprite(texture, expanded ? ExpandedNoteBorderPixels : CompactNoteBorderPixels);
            if (_paper.sprite != sprite) _paper.sprite = sprite;
            // Measure the text only when it changes.
            if (_paperText != _note.text || _paperFontSize != _note.fontSize || _paperBox != theme.noteBoxSize)
            {
                _note.ForceMeshUpdate();
                _textBounds = _note.textInfo.characterCount > 0 ? _note.textBounds : new Bounds();
                _paperText = _note.text;
                _paperFontSize = _note.fontSize;
                _paperBox = theme.noteBoxSize;
            }
            var bounds = _textBounds;
            float inset = expanded ? 0f : CompactNoteInset;
            float pad = theme.notePadding + inset;
            var size = new Vector2(bounds.size.x + 2 * pad, bounds.size.y + 2 * pad);
            if (expanded) size.x = Mathf.Max(size.x, theme.noteExpandedMinWidth);
            else size.x = size.y = Mathf.Max(size.x, size.y, theme.noteMinSize + 2 * inset);
            _paper.size = size;
            _paper.transform.localPosition = new Vector3(bounds.center.x, bounds.center.y, .001f);
        }
    }
}
