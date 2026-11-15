using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR;

namespace Zap.Core
{
    public sealed class HomeFrame : MonoBehaviour
    {
        const string RigName = "XR Origin (XR Rig)";
        const float MinProjectedSqr = 1e-4f;
        const float MinLevelForwardSqr = 0.03f;
        const float TrackedPoseSqr = 1e-6f;
        const float AlignAfterTracked = 0.3f;
        const float AlignAnywayAfter = 1.5f;
        const float SubscribeInterval = 1f;
        const float PresenceWait = 8f;

        public static int Version { get; private set; }
        public static float HeadHeight { get; private set; } = 1.5f;
        public static bool Hold { get; set; }

        static HomeFrame _instance;

        readonly List<XRInputSubsystem> _inputSubsystems = new List<XRInputSubsystem>();
        readonly List<XRInputSubsystem> _subscribed = new List<XRInputSubsystem>();
        Transform _rig;
        Transform _head;
        Vector3 _samplePosition;
        Quaternion _sampleRotation;
        float _requestedAt;
        float _absentSince = -1f;
        float _nextSubscribeAt;
        bool _pending;
        bool _sampled;
        bool _provisional;
        bool _headWasOff;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Hold = false;
            Version = 0;
            HeadHeight = 1.5f;
            _instance = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if (SceneManager.GetActiveScene().name != "Void" || ZapPlatform.IsDesktop)
                return;
            if (FindAnyObjectByType<HomeFrame>() != null)
                return;

            new GameObject("[ZAP Home Frame]").AddComponent<HomeFrame>();
        }

        public static void Align(Transform rig, Transform head)
        {
            if (rig == null || head == null) return;

            var projected = head.forward;
            projected.y = 0f;
            if (projected.sqrMagnitude >= MinProjectedSqr)
            {
                var yaw = Vector3.SignedAngle(Vector3.forward, projected, Vector3.up);
                rig.RotateAround(head.position, Vector3.up, -yaw);
            }

            var position = head.position;
            rig.position += new Vector3(-position.x, 0f, -position.z);
        }

        public static void Request()
        {
            if (_instance == null) return;
            _instance._pending = true;
            _instance._requestedAt = Time.unscaledTime;
            _instance._provisional = false;
            _instance._sampled = false;
        }

        void Awake()
        {
            _instance = this;
            Request();
            SubscribeInputs();
            _nextSubscribeAt = Time.unscaledTime + SubscribeInterval;
        }

        void OnDestroy()
        {
            for (var i = 0; i < _subscribed.Count; i++)
            {
                var subsystem = _subscribed[i];
                if (subsystem != null)
                    subsystem.trackingOriginUpdated -= OnTrackingOriginUpdated;
            }

            _subscribed.Clear();
            if (_instance == this) _instance = null;
        }

        void OnApplicationPause(bool paused)
        {
            if (!paused) Request();
        }

        void Update()
        {
            if (Time.unscaledTime >= _nextSubscribeAt)
            {
                _nextSubscribeAt = Time.unscaledTime + SubscribeInterval;
                SubscribeInputs();
            }

            var head = InputDevices.GetDeviceAtXRNode(XRNode.Head);
            bool present = false;
            bool known = head.isValid && head.TryGetFeatureValue(UnityEngine.XR.CommonUsages.userPresence, out present);
            if (known && present)
            {
                _absentSince = -1f;
                if (_headWasOff)
                {
                    _headWasOff = false;
                    Request();
                }
            }
            else if (known)
            {
                _headWasOff = true;
                if (_absentSince < 0f)
                    _absentSince = Time.unscaledTime;
                if (Time.unscaledTime - _absentSince < PresenceWait)
                {
                    _requestedAt = Time.unscaledTime;
                    return;
                }
            }
            else
            {
                _absentSince = -1f;
            }

            if (!_pending && !_provisional) return;

            if (_rig == null)
            {
                var rigObject = GameObject.Find(RigName);
                if (rigObject != null) _rig = rigObject.transform;
            }

            if (_head == null)
            {
                var camera = Camera.main;
                if (camera != null) _head = camera.transform;
            }

            if (_rig == null || _head == null) return;

            if (_pending && !_sampled)
                SampleHead();

            if (Hold) return;

            if (!_pending)
            {
                if (PoseReady())
                {
                    ApplyAlignment();
                    _provisional = false;
                }
                return;
            }

            var elapsed = Time.unscaledTime - _requestedAt;
            if (elapsed >= AlignAfterTracked && PoseReady())
            {
                ApplyAlignment();
                _pending = false;
                _provisional = false;
                return;
            }

            if (elapsed >= AlignAnywayAfter)
            {
                ApplyAlignment();
                SampleHead();
                _pending = false;
                _provisional = true;
            }
        }

        void SubscribeInputs()
        {
            for (var i = 0; i < _subscribed.Count; i++)
            {
                var subsystem = _subscribed[i];
                if (subsystem != null)
                    subsystem.trackingOriginUpdated -= OnTrackingOriginUpdated;
            }

            _subscribed.Clear();
            SubsystemManager.GetSubsystems(_inputSubsystems);
            for (var i = 0; i < _inputSubsystems.Count; i++)
            {
                var subsystem = _inputSubsystems[i];
                if (subsystem == null) continue;
                subsystem.trackingOriginUpdated += OnTrackingOriginUpdated;
                _subscribed.Add(subsystem);
            }
        }

        void SampleHead()
        {
            _samplePosition = _head.localPosition;
            _sampleRotation = _head.localRotation;
            _sampled = true;
        }

        bool PoseReady()
        {
            var projected = _head.forward;
            projected.y = 0f;
            var tracked = _head.localPosition.sqrMagnitude > TrackedPoseSqr
                || _head.localRotation != Quaternion.identity;
            var fresh = _head.localPosition != _samplePosition
                || _head.localRotation != _sampleRotation;
            return tracked && fresh && projected.sqrMagnitude >= MinLevelForwardSqr;
        }

        void ApplyAlignment()
        {
            Align(_rig, _head);
            HeadHeight = _head.position.y;
            Version += 1;
        }

        void OnTrackingOriginUpdated(XRInputSubsystem subsystem)
        {
            Request();
        }
    }
}
