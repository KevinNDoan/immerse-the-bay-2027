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
        private bool _selected;
        private float _heldSince;
        private float _nearFaceSince = -1;
        private float _tossedAt;
        private float _tossStartDiameter;
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
            var orb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            orb.name = "Topic orb";
            Destroy(orb.GetComponent<Collider>());
            _orb = orb.transform;
            _orb.SetParent(transform, false);
            _orb.localScale = Vector3.one * ZapTheme.Current.nodeDiameter;
            orb.GetComponent<Renderer>().sharedMaterial = material;
            var note = new GameObject("Topic note");
            note.transform.SetParent(transform, false);
            _note = note.AddComponent<TextMeshPro>();
            _note.alignment = TextAlignmentOptions.Top;
            _note.rectTransform.pivot = new Vector2(.5f, 1f);
            _note.overflowMode = TextOverflowModes.Ellipsis;
            _note.richText = false;
            Refresh();
        }

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
            _tossStartDiameter = _orb.localScale.x;
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
                _orb.localScale = Vector3.one * Mathf.Lerp(_tossStartDiameter, 0f, progress);
                if (progress >= 1f) Destroy(gameObject);
                return;
            }
            var camera = Camera.main;
            if (Data == null || camera == null) return;
            var theme = ZapTheme.Current;
            _orb.localScale = Vector3.one * (Highlighted ? theme.nodeSelectedDiameter * 1.25f : (_selected ? theme.nodeSelectedDiameter : theme.nodeDiameter));
            var distance = Vector3.Distance(transform.position, camera.transform.position);
            _note.transform.position = transform.position + Vector3.down * theme.noteOffsetBelowNode;
            _note.transform.rotation = Quaternion.LookRotation(_note.transform.position - camera.transform.position);
            _note.rectTransform.sizeDelta = theme.noteBoxSize;
            _note.color = theme.textColor;
            if (theme.font != null) _note.font = theme.font;
            bool showSummary = (Grab.isSelected || (_selected && distance < theme.summaryShowDistance)) && !string.IsNullOrEmpty(Data.summary);
            _note.text = showSummary ? Data.topic + "\n" + Data.summary : Data.topic;
            _note.fontSize = showSummary ? theme.summaryTextSize : theme.topicTextSize;
            _note.gameObject.SetActive(distance < theme.noteHideDistance);
            if (Grab.isSelected && !_entered && Time.unscaledTime - _heldSince > .8f && distance < .26f)
            {
                if (_nearFaceSince < 0) _nearFaceSince = Time.unscaledTime;
                if (Time.unscaledTime - _nearFaceSince > .65f)
                { _entered = true; EnterRequested?.Invoke(this); }
            }
            else _nearFaceSince = -1;
        }
    }
}
