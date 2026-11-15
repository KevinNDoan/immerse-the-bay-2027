using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace Zap.Constellation
{
    [RequireComponent(typeof(Rigidbody), typeof(SphereCollider), typeof(XRGrabInteractable))]
    public sealed class TopicBall : MonoBehaviour
    {
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
        private bool _selected;
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
            var orb = new GameObject("Crystal world");
            _orb = orb.transform;
            _orb.SetParent(transform, false);
            _orb.localScale = Vector3.one * _scale;
            Crystal = orb.AddComponent<CrystalBallVisual>();
            var note = new GameObject("Topic note");
            note.transform.SetParent(transform, false);
            _note = note.AddComponent<TextMeshPro>();
            _note.fontSize = .38f;
            _note.alignment = TextAlignmentOptions.Top;
            _note.color = Color.white;
            _note.rectTransform.sizeDelta = new Vector2(.62f, .35f);
            _note.rectTransform.pivot = new Vector2(.5f, 1f);
            _note.overflowMode = TextOverflowModes.Ellipsis;
            _note.richText = false;
            Refresh();
        }

        public void SetSelected(bool value) { _selected = value; }
        public void SetDraft(bool draft)
        {
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
            _scale = Mathf.MoveTowards(_scale, Grab.isSelected ? .18f : _selected ? .165f : .15f, Time.unscaledDeltaTime*.09f);
            _orb.localScale = Vector3.one * _scale;
            Crystal.SetEmphasis(Grab.isSelected, _selected);
            var distance = Vector3.Distance(transform.position, camera.transform.position);
            if (distance < 2 && _previewImage != Data.image)
            { _previewImage = Data.image; Crystal.SetPreview(Data.image); }
            _note.transform.position = transform.position + Vector3.down * .13f;
            _note.transform.rotation = Quaternion.LookRotation(_note.transform.position - camera.transform.position);
            bool showSummary = (Grab.isSelected || (_selected && distance < .85f)) && !string.IsNullOrEmpty(Data.summary);
            _note.text = showSummary ? Data.topic + "\n" + Data.summary : Data.topic;
            _note.fontSize = showSummary ? .24f : .38f;
            _note.gameObject.SetActive(distance < 4);
        }
    }
}
