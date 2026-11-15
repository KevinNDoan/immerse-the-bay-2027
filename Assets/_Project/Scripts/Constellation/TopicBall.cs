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
        // Compact note exports: 16 Figma px of shadow padding plus a 6 px corner, at 4x.
        private const float CompactNoteBorderPixels = 88f;
        private const float CompactNoteInset = 64f / ZapArt.PixelsPerMetre;
        // Expanded note exports: a 6 px corner at 4x, no padding.
        private const float ExpandedNoteBorderPixels = 24f;

        public NodeData Data { get; private set; }
        public XRGrabInteractable Grab { get; private set; }
        public Rigidbody Body { get; private set; }
        public event Action<TopicBall> Selected;
        public event Action<TopicBall> EnterRequested;
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

        public void SetSelected(bool value) { _selected = value; }
        public void Refresh() { if (_note != null) _note.text = Data.topic; }
        private void OnGrab(SelectEnterEventArgs args)
        {
            _heldSince = Time.unscaledTime;
            _entered = false;
            Selected?.Invoke(this);
        }
        private void OnRelease(SelectExitEventArgs args) { _nearFaceSince = -1; }

        private void FixedUpdate()
        {
            if (Body == null || Grab.isSelected || Body.isKinematic) return;
            Body.linearVelocity = Vector3.ClampMagnitude(Body.linearVelocity, 8);
            if (Body.linearVelocity.sqrMagnitude < .0009f) Body.linearVelocity = Vector3.zero;
        }

        private void LateUpdate()
        {
            var camera = Camera.main;
            if (Data == null || camera == null) return;
            var theme = ZapTheme.Current;
            bool held = Grab.isSelected;
            if (_orb != null) _orb.localScale = Vector3.one * (_selected ? theme.nodeSelectedDiameter : theme.nodeDiameter);
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
            float coreScale = held ? theme.starGrabbedCoreScale : _selected ? theme.starPointedCoreScale : 1f;
            float glowScale = held ? theme.starGrabbedGlowScale : _selected ? theme.starPointedGlowScale : 1f;
            ZapArt.SetDiameter(_core, size * coreScale);
            ZapArt.SetDiameter(_glow, size * theme.starGlowScale * glowScale);
            ZapArt.SetDiameter(_ring, size * theme.starRingScale);
            ZapArt.SetDiameter(_sparks, size * theme.starSparksScale);
            ZapArt.SetDiameter(_orbit, size * theme.starOrbitScale);
            ZapArt.Show(_ring, _selected && !held);
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
