using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using Zap.Core;

namespace Zap.Constellation
{
    [RequireComponent(typeof(Rigidbody), typeof(SphereCollider))]
    public sealed class TopicBall : MonoBehaviour
    {
        public const float TossSpeed = 2.5f;
        private const float HighlightScale = 1.25f;
        public NodeData Data { get; private set; }
        public XRGrabInteractable Grab { get; private set; }
        public Rigidbody Body { get; private set; }
        public CrystalBallVisual Crystal { get; private set; }
        public Vector3 LastThrowVelocity { get; private set; }
        public bool IsTossed { get; private set; }
        public bool Highlighted { get; set; }
        public event Action<TopicBall> Selected;
        public event Action<TopicBall> Grabbed;
        public event Action<TopicBall> EnterRequested;
        public event Action<TopicBall> Tossed;
        private TextMeshPro _note;
        private FrostPanel _noteGlass;
        private string _glassText;
        private float _glassFontSize;
        private Vector2 _glassBox;
        private Bounds _glassBounds;
        private Transform _orb;
        private Transform _star;
        private SpriteRenderer _core, _glow, _ring, _sparks, _orbit;
        private SpriteRenderer[] _layers = Array.Empty<SpriteRenderer>();
        private bool _selected, _draft;
        public bool HasWorldTravelHint { get; private set; }
        private float _heldSince;
        private bool _releasePending, _travelPending;
        private float _releasedAfter;
        private Vector3 _releaseFromHead, _releaseGaze, _releaseVelocity;
        private float _scale = .15f;
        private string _previewImage;
        private float _tossedAt;
        private float _nextImpactSoundAt;
        private Vector3 _tossStartScale, _tossStarStartScale;

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
            // Construct this runtime-only component explicitly rather than relying on
            // dependency attributes to install it in IL2CPP player builds.
            var travelGrab = GetComponent<TravelGrabInteractable>();
            if (travelGrab == null) travelGrab = gameObject.AddComponent<TravelGrabInteractable>();
            Grab = travelGrab;
            travelGrab.Detached += CaptureRelease;
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
            // Frosted glass behind the note when the theme turns it on; sits just behind the text.
            _noteGlass = FrostPanel.Create(note.transform, "Note glass", new Vector3(0f, 0f, .002f));
            Refresh();
        }

        // Sizes the note's glass to its text, re-measuring only when the text changes.
        private void UpdateNoteGlass(ZapTheme theme)
        {
            if (_noteGlass == null) return;
            if (_glassText != _note.text || _glassFontSize != _note.fontSize || _glassBox != _note.rectTransform.sizeDelta)
            {
                _note.ForceMeshUpdate();
                _glassBounds = _note.textInfo.characterCount > 0 ? _note.textBounds : new Bounds();
                _glassText = _note.text; _glassFontSize = _note.fontSize; _glassBox = _note.rectTransform.sizeDelta;
            }
            float pad = theme.glassPadding;
            _noteGlass.SetSize(new Vector2(_glassBounds.size.x + pad * 2f, _glassBounds.size.y + pad * 2f));
            _noteGlass.transform.localPosition = new Vector3(_glassBounds.center.x, _glassBounds.center.y, .002f);
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
            Body.constraints = RigidbodyConstraints.None;
            _heldSince = Time.unscaledTime;
            _releasePending = _travelPending = false;
            LastThrowVelocity = Vector3.zero;
            Selected?.Invoke(this);
            Grabbed?.Invoke(this);
        }
        private void OnRelease(SelectExitEventArgs args)
        {
            // Disabling a node during travel must never start another journey.
            if (args.isCanceled || !isActiveAndEnabled || IsTossed) return;
            _releasePending = Camera.main != null;
            _releasedAfter = Time.unscaledTime - _heldSince;
            if (_releasePending)
            {
                _releaseFromHead = transform.position - Camera.main.transform.position;
                _releaseGaze = Camera.main.transform.forward;
            }
        }

        private void CaptureRelease(Vector3 velocity)
        {
            // Capture XRI's actual detach velocity before room collisions or locomotion can change it.
            if (_releasePending && isActiveAndEnabled && !_draft && !IsTossed && !Grab.isSelected)
                OrbAudio.PlayThrow(transform.position, velocity.magnitude);
            _travelPending = _releasePending && IsTravelThrow(velocity, _releaseFromHead, _releaseGaze, _releasedAfter);
            _releaseVelocity = velocity;
            _releasePending = false;
        }

        public static bool IsTravelThrow(Vector3 velocity, Vector3 fromHead, Vector3 gaze, float heldSeconds)
        {
            return heldSeconds >= .12f && velocity.sqrMagnitude >= 1.35f*1.35f
                && fromHead.sqrMagnitude > .04f
                && Vector3.Dot(velocity.normalized, fromHead.normalized) > .45f
                && Vector3.Dot(velocity.normalized, gaze.normalized) > .2f;
        }

        private void OnDisable() { _releasePending = _travelPending = false; }

        private void OnCollisionEnter(Collision collision)
        {
            if (!isActiveAndEnabled || _draft || IsTossed || collision.contactCount == 0
                || Time.unscaledTime < _nextImpactSoundAt) return;
            float speed = collision.relativeVelocity.magnitude;
            if (speed < OrbAudio.MinimumImpactSpeed) return;
            // Both orbs receive this contact; share the cooldown so the first callback plays it once.
            var other = collision.collider.GetComponentInParent<TopicBall>();
            if (other != null && Time.unscaledTime < other._nextImpactSoundAt) return;
            _nextImpactSoundAt = Time.unscaledTime + .15f;
            if (other != null) other._nextImpactSoundAt = _nextImpactSoundAt;
            OrbAudio.PlayImpact(collision.GetContact(0).point, speed);
        }

        public static bool IsToss(Vector3 releaseVelocity)
        {
            return releaseVelocity.sqrMagnitude >= TossSpeed * TossSpeed;
        }

        // Explicit place/discard API retained for callers that requested removal.
        // XR selectExited uses CheckRelease above: a physical throw travels, never deletes.
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

            _releasePending = _travelPending = false;
            IsTossed = true;
            Tossed?.Invoke(this);
            Body.constraints = RigidbodyConstraints.None;
            Body.linearVelocity = velocity;
            Grab.enabled = false;
            GetComponent<SphereCollider>().enabled = false;
            _note.gameObject.SetActive(false);
            _tossedAt = Time.unscaledTime;
            _tossStartScale = _orb != null ? _orb.localScale : Vector3.one;
            _tossStarStartScale = _star != null ? _star.localScale : Vector3.one;
        }

        private void FixedUpdate()
        {
            if (Body == null || Grab == null || Grab.isSelected || Body.isKinematic) return;
            Body.linearVelocity = Vector3.ClampMagnitude(Body.linearVelocity, 8);
            if (Body.linearVelocity.sqrMagnitude < .0009f) Body.linearVelocity = Vector3.zero;
        }

        private void LateUpdate()
        {
            if (Grab == null || Crystal == null || _note == null) return;
            // Dispatch outside XRI's detach callback: travel may deactivate/reparent this ball.
            if (_travelPending)
            {
                _travelPending = false;
                if (!Grab.isSelected && !IsTossed)
                {
                    LastThrowVelocity = _releaseVelocity;
                    EnterRequested?.Invoke(this);
                    if (!isActiveAndEnabled) return;
                }
            }
            if (IsTossed)
            {
                float progress = Mathf.Clamp01((Time.unscaledTime - _tossedAt) / .5f);
                if (_orb != null) _orb.localScale = Vector3.Lerp(_tossStartScale, Vector3.zero, progress);
                if (_star != null) _star.localScale = Vector3.Lerp(_tossStarStartScale, Vector3.zero, progress);
                if (progress >= 1f) Destroy(gameObject);
                return;
            }
            var camera = Camera.main;
            if (Data == null || camera == null) return;
            var theme = ZapTheme.Current;
            bool held = Grab.isSelected;
            float targetScale = (held ? .18f : (_selected || Highlighted) ? .165f : .15f) * (Highlighted ? HighlightScale : 1f);
            _scale = Mathf.MoveTowards(_scale, targetScale, Time.unscaledDeltaTime*.09f);
            _orb.localScale = Vector3.one * _scale;
            Crystal.SetEmphasis(held, _selected || Highlighted);
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
            _note.color = _noteGlass != null ? theme.glassTextColor : theme.textColor;
            if (theme.font != null) _note.font = theme.font;
            bool showSummary = (held || (_selected && distance < theme.summaryShowDistance)) && !string.IsNullOrEmpty(Data.summary);
            _note.text = showSummary ? Data.topic + "\n" + Data.summary : Data.topic;
            if (HasWorldTravelHint) _note.text += "\nPoint + X to enter · or grip and throw";
            _note.fontSize = showSummary || HasWorldTravelHint ? theme.summaryTextSize : theme.topicTextSize;
            _note.gameObject.SetActive(distance < theme.noteHideDistance);
            _note.maxVisibleLines = HasWorldTravelHint ? 5 : showSummary ? 4 : 2;
            if (_note.gameObject.activeSelf) UpdateNoteGlass(theme);
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

    }
}
