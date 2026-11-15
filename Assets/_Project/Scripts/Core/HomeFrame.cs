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
        const float TrackedPoseSqr = 1e-6f;
        const float AlignAfterTracked = 0.3f;
        const float AlignAnywayAfter = 1.5f;
        const float SubscribeInterval = 1f;

        public static int Version { get; private set; }
        public static float HeadHeight { get; private set; } = 1.5f;

        static HomeFrame _instance;

        readonly List<XRInputSubsystem> _inputSubsystems = new List<XRInputSubsystem>();
        Transform _rig;
        Transform _head;
        float _requestedAt;
        float _nextSubscribeAt;
        bool _pending;
        bool _inputsSubscribed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
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
        }

        void Awake()
        {
            _instance = this;
            Request();
            TrySubscribeInputs();
            _nextSubscribeAt = Time.unscaledTime + SubscribeInterval;
        }

        void OnDestroy()
        {
            for (var i = 0; i < _inputSubsystems.Count; i++)
            {
                var subsystem = _inputSubsystems[i];
                if (subsystem != null)
                    subsystem.trackingOriginUpdated -= OnTrackingOriginUpdated;
            }

            _inputSubsystems.Clear();
            if (_instance == this) _instance = null;
        }

        void OnApplicationPause(bool paused)
        {
            if (!paused) Request();
        }

        void Update()
        {
            if (!_inputsSubscribed && Time.unscaledTime >= _nextSubscribeAt)
            {
                _nextSubscribeAt = Time.unscaledTime + SubscribeInterval;
                TrySubscribeInputs();
            }

            if (!_pending) return;

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

            var elapsed = Time.unscaledTime - _requestedAt;
            var tracked = _head.localPosition.sqrMagnitude > TrackedPoseSqr
                || _head.localRotation != Quaternion.identity;
            if ((elapsed >= AlignAfterTracked && tracked) || elapsed >= AlignAnywayAfter)
            {
                Align(_rig, _head);
                HeadHeight = _head.position.y;
                Version += 1;
                _pending = false;
            }
        }

        void TrySubscribeInputs()
        {
            if (_inputsSubscribed) return;

            SubsystemManager.GetSubsystems(_inputSubsystems);
            for (var i = 0; i < _inputSubsystems.Count; i++)
            {
                var subsystem = _inputSubsystems[i];
                if (subsystem == null) continue;
                subsystem.trackingOriginUpdated += OnTrackingOriginUpdated;
                _inputsSubscribed = true;
            }
        }

        void OnTrackingOriginUpdated(XRInputSubsystem subsystem)
        {
            Request();
        }
    }
}
