using System;
using System.Collections;
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
        public CrystalBallVisual Crystal { get; private set; }
        public Vector3 LastThrowVelocity { get; private set; }
        public event Action<TopicBall> Selected;
        public event Action<TopicBall> Grabbed;
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
        private bool _selected, _draft;
        public bool HasWorldTravelHint { get; private set; }
        private float _heldSince;
        private int _releaseVersion;
        private float _scale = .15f;
        private string _previewImage;

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
            GetComponent<SphereCollider>().radius = .085f;
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
            // Keep a functional crystal for previews, recording and journey effects in every art mode.
            var orb = new GameObject("Crystal world");
            _orb = orb.transform;
            _orb.SetParent(transform, false);
            _orb.localScale = Vector3.one * _scale;
            Crystal = orb.AddComponent<CrystalBallVisual>();
            if (theme.useStarNodeArt && theme.nodeCore != null) BuildStar(theme);
            var note = new GameObject("Topic note");
            note.transform.SetParent(transform, false);
            _note = note.AddComponent<TextMeshPro>();
            _note.alignment = TextAlignmentOptions.Top;
            _note.rectTransform.pivot = new Vector2(.5f, 1f);
            _note.overflowMode = TextOverflowModes.Ellipsis;
            _note.richText = false;
            _note.sortingOrder = 1;
            _note.fontSize = theme.topicTextSize;
            _note.rectTransform.sizeDelta = theme.noteBoxSize;
            _note.color = theme.textColor;
            // The paper sits behind the text; the text faces away from its forward axis.
            _paper = ZapArt.Layer(note.transform, "Note paper", theme.noteBackground, 0, CompactNoteBorderPixels);
            if (_paper != null) _paper.transform.localPosition = new Vector3(0, 0, .001f);
            Refresh();
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
        public void SetWorldTravelHint(bool value)
        {
            HasWorldTravelHint = value;
            UpdateNoteBox(ZapTheme.Current);
        }
        private void UpdateNoteBox(ZapTheme theme)
        {
            _note.rectTransform.sizeDelta = HasWorldTravelHint ? Vector2.Max(theme.noteBoxSize, new Vector2(.72f,.48f)) : theme.noteBoxSize;
        }
        public void SetDraft(bool draft)
        {
            _draft = draft;
            if (draft)
            {
                _orb.gameObject.SetActive(true);
                if (_star != null) _star.gameObject.SetActive(false);
            }
            Grab.enabled = !draft;
            if (draft) Body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            Body.isKinematic = draft;
            if (!draft) Body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            Crystal.SetRecording(draft);
        }
        public void SetData(NodeData data) { Data = data; Refresh(); }
        public void Refresh()
        {
            if (_note != null) _note.text = Data.topic;
            // Refresh thumbnails lazily when the node is visible nearby.
            _previewImage = null;
            if (string.IsNullOrEmpty(Data.image)) Crystal.SetPreview("");
        }
        private void OnGrab(SelectEnterEventArgs args)
        {
            _heldSince = Time.unscaledTime;
            _releaseVersion++;
            LastThrowVelocity = Vector3.zero;
            Selected?.Invoke(this);
            Grabbed?.Invoke(this);
        }
        private void OnRelease(SelectExitEventArgs args)
        {
            // Disabling a node during travel must never start another journey.
            if (args.isCanceled || !isActiveAndEnabled) return;
            StartCoroutine(CheckRelease(++_releaseVersion, Time.unscaledTime - _heldSince));
        }

        private IEnumerator CheckRelease(int version, float heldSeconds)
        {
            // XRI applies detach velocity after selectExited, at the end of its update.
            yield return new WaitForFixedUpdate();
            yield return null;
            if (version != _releaseVersion || Grab.isSelected || Camera.main == null) yield break;
            var head = Camera.main.transform;
            var velocity = Body.linearVelocity;
            if (!IsTravelThrow(velocity, transform.position-head.position, head.forward, heldSeconds)) yield break;
            LastThrowVelocity = velocity;
            EnterRequested?.Invoke(this);
        }

        public static bool IsTravelThrow(Vector3 velocity, Vector3 fromHead, Vector3 gaze, float heldSeconds)
        {
            return heldSeconds >= .12f && velocity.sqrMagnitude >= 1.35f*1.35f
                && fromHead.sqrMagnitude > .04f
                && Vector3.Dot(velocity.normalized, fromHead.normalized) > .45f
                && Vector3.Dot(velocity.normalized, gaze.normalized) > .2f;
        }

        private void OnDisable() { _releaseVersion++; StopAllCoroutines(); }

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
            _scale = Mathf.MoveTowards(_scale, held ? .18f : _selected ? .165f : .15f, Time.unscaledDeltaTime*.09f);
            _orb.localScale = Vector3.one * _scale;
            Crystal.SetEmphasis(held, _selected);
            var distance = Vector3.Distance(transform.position, camera.transform.position);
            if (distance < 2 && _previewImage != Data.image)
            { _previewImage = Data.image; Crystal.SetPreview(Data.image); }
            // Optional designer stars decorate unpreviewed nodes; recorded drafts and world previews stay crystals.
            bool showStar = _star != null && theme.useStarNodeArt && !_draft && !Crystal.PreviewReady;
            _orb.gameObject.SetActive(!showStar);
            if (_star != null)
            {
                _star.gameObject.SetActive(showStar);
                if (showStar) UpdateStar(theme, camera.transform.position, held);
            }
            _note.transform.position = transform.position + Vector3.down * Mathf.Max(theme.noteOffsetBelowNode, _scale * .5f + .04f);
            _note.transform.rotation = Quaternion.LookRotation(_note.transform.position - camera.transform.position);
            UpdateNoteBox(theme);
            _note.color = _paper != null ? theme.ink : theme.textColor;
            if (theme.font != null) _note.font = theme.font;
            bool showSummary = (held || (_selected && distance < theme.summaryShowDistance)) && !string.IsNullOrEmpty(Data.summary);
            _note.text = showSummary ? Data.topic + "\n" + Data.summary : Data.topic;
            if (HasWorldTravelHint) _note.text += "\nPoint + X to enter · or grip and throw";
            _note.fontSize = showSummary || HasWorldTravelHint ? theme.summaryTextSize : theme.topicTextSize;
            _note.gameObject.SetActive(distance < theme.noteHideDistance);
            if (_paper != null && _note.gameObject.activeSelf) UpdatePaper(theme, showSummary || HasWorldTravelHint, held);
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
            if (_paperText != _note.text || _paperFontSize != _note.fontSize || _paperBox != _note.rectTransform.sizeDelta)
            {
                _note.ForceMeshUpdate();
                _textBounds = _note.textInfo.characterCount > 0 ? _note.textBounds : new Bounds();
                _paperText = _note.text;
                _paperFontSize = _note.fontSize;
                _paperBox = _note.rectTransform.sizeDelta;
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
