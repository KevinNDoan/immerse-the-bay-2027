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
        public NodeData Data { get; private set; }
        public XRGrabInteractable Grab { get; private set; }
        public Rigidbody Body { get; private set; }
        public event Action<TopicBall> Selected;
        public event Action<TopicBall> EnterRequested;
        private TextMeshPro _note;
        private Transform _orb;
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
            _orb.localScale = Vector3.one * (_selected ? theme.nodeSelectedDiameter : theme.nodeDiameter);
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
