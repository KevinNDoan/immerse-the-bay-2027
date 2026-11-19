using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Transformers;

namespace Zap.Constellation
{
    // Floating, inspectable representations share the same controls for every topic.
    public sealed class StudyExhibit : MonoBehaviour
    {
        public string ObjectId { get; internal set; } = Guid.NewGuid().ToString("N");
        public StudyModelData Data { get; private set; }
        public bool IsHeld => _grab != null && _grab.isSelected;
        public bool IsFocused => !float.IsPositiveInfinity(FocusDistance);
        public bool Exploded { get; private set; }
        public int PartCount => _parts.Count;
        public XRGrabInteractable Grab => _grab;
        public bool HasModel => _parts.Count > 0;
        public Transform Pose => _model != null ? _model.transform : null;
        public int StepIndex { get; private set; } = -1;
        public float Parameter { get; private set; }
        public string FocusedName => _focused >= 0 ? _parts[_focused].data.name : "";
        public string LearningText { get; private set; }
        public string Answer => _answer;
        public ExplorationPane Pane { get; private set; }
        public bool Active { get; set; } = true;
        public float FocusDistance { get; private set; } = float.PositiveInfinity;
        private GameObject _model;
        private Transform _assembly;
        private XRGrabInteractable _grab;
        private readonly List<Part> _parts = new List<Part>();
        private readonly List<Mesh> _meshes = new List<Mesh>();
        private int _focused = -1;
        private float _spread, _clock, _fit, _parameterVisual, _nextFocus;
        private bool _paused, _experimentActive;
        private BoxCollider _collider;
        private string _answer, _requestStatus, _requestBody;
        private bool _requestPending, _requestError;
        private Vector3 _boundsSize;
        private Vector3 _captionOffset;
        public void SetEnvironment(Zap.World.GeneratedEnvironment environment) { }
        private sealed class Part
        {
            public StudyPartData data;
            public Transform transform;
            public MeshCollider hit;
            public Material material;
            public Renderer renderer;
            public Color color;
            public Vector3 position;
            public bool highlighted;
        }

        public void Build(StudyModelData data)
        {
            if (data?.parts == null || data.parts.Length > 28) return;
            Clear(); Data = data;
            _model = new GameObject("Explore: " + data.title);
            _model.transform.SetParent(transform, false);
            if (data.parts.Length == 0) { BuildPane(); return; }
            _assembly = new GameObject("Generated geometry").transform;
            _assembly.SetParent(_model.transform, false);
            Bounds bounds = default;
            foreach (var item in data.parts)
            {
                if (item == null) continue;
                var mesh = StudyGeometry.Build(item, data.parts);
                if (mesh == null) continue;
                if (_parts.Count == 0) bounds = mesh.bounds; else bounds.Encapsulate(mesh.bounds);
                Vector3 center = mesh.bounds.center;
                var vertices = mesh.vertices;
                for (int i = 0; i < vertices.Length; i++) vertices[i] -= center;
                mesh.vertices = vertices; mesh.RecalculateBounds(); _meshes.Add(mesh);
                var obj = new GameObject(item.name); obj.transform.SetParent(_assembly, false);
                obj.transform.localPosition = center;
                obj.AddComponent<MeshFilter>().sharedMesh = mesh;
                var hit = obj.AddComponent<MeshCollider>(); hit.sharedMesh = mesh;
                var color = ColorUtility.TryParseHtmlString(item.color, out var parsed) ? parsed : Color.cyan;
                var material = new Material(Shader.Find("Zap/Study")); material.SetColor("_BaseColor", color);
                var renderer = obj.AddComponent<MeshRenderer>(); renderer.sharedMaterial = material;
                _parts.Add(new Part { data = item, transform = obj.transform, hit = hit, renderer = renderer, material = material, color = color, position = center });
            }
            if (_parts.Count == 0) { Clear(); return; }
            _fit = .8f * Mathf.Clamp(data.displayScale <= 0 ? 1 : data.displayScale, .5f, 1.8f) /
                Mathf.Max(.1f, Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z)));
            _assembly.localScale = Vector3.one * _fit;
            _assembly.localPosition = -bounds.center * _fit;
            _boundsSize = bounds.size * _fit;
            _collider = _model.AddComponent<BoxCollider>(); _collider.size = _boundsSize + Vector3.one * .06f;
            _collider.isTrigger = true;
            var body = _model.AddComponent<Rigidbody>(); body.useGravity = false; body.isKinematic = true;
            _grab = _model.AddComponent<XRGrabInteractable>();
            _grab.movementType = XRBaseInteractable.MovementType.Instantaneous;
            _grab.throwOnDetach = false; _grab.forceGravityOnDetach = false;
            _grab.selectMode = InteractableSelectMode.Multiple;
            _grab.useDynamicAttach = true;
            _grab.colliders.Clear(); _grab.colliders.Add(_collider);
            foreach (var part in _parts) _grab.colliders.Add(part.hit);
            var transformer = _model.AddComponent<XRGeneralGrabTransformer>();
            transformer.allowTwoHandedScaling = true; transformer.allowOneHandedScaling = false;
            transformer.minimumScaleRatio = .12f; transformer.maximumScaleRatio = 5;
            _grab.AddSingleGrabTransformer(transformer); _grab.AddMultipleGrabTransformer(transformer);
            BuildPane();
        }

        private void BuildPane()
        {
            var pane = new GameObject("Exploration glass pane"); pane.transform.SetParent(transform, false);
            Pane = pane.AddComponent<ExplorationPane>(); Pane.Initialize();
            LearningText = Data.explanation;
            Recenter();
        }

        public void Recenter()
        {
            if (_model == null || Camera.main == null || IsHeld) return;
            var camera = Camera.main.transform;
            var forward = Vector3.ProjectOnPlane(camera.forward, Vector3.up).normalized;
            if (forward.sqrMagnitude < .1f) forward = Vector3.forward;
            _model.transform.position = camera.position + forward * 1.35f + Vector3.down * .06f;
            _model.transform.rotation = Quaternion.Euler(0, camera.eulerAngles.y, 0);
            _model.transform.localScale = Vector3.one;
            _captionOffset = HasModel ? forward * .3f + Vector3.Cross(Vector3.up,forward) * .85f + Vector3.down * .09f :
                forward * .2f + Vector3.Cross(Vector3.up,forward) * .65f + Vector3.down * .08f;
            Pane?.Attach();
        }

        public void Focus(Ray ray)
        {
            if (Time.unscaledTime < _nextFocus) return;
            _nextFocus = Time.unscaledTime + .055f;
            _focused = -1; FocusDistance = float.PositiveInfinity;
            for (int i = 0; i < _parts.Count; i++)
                if (_parts[i].hit.enabled && _parts[i].hit.Raycast(ray, out var hit, 6) && hit.distance < FocusDistance)
                { FocusDistance = hit.distance; _focused = i; }
            // Thin curves still have an easy-to-target interaction volume between their strands.
            if (_focused < 0 && _collider != null && _collider.Raycast(ray, out var volume, 6)) FocusDistance = volume.distance;
        }

        public void Zoom(float amount)
        {
            if (_model == null || Mathf.Abs(amount) < .15f || (_grab != null && _grab.interactorsSelecting.Count > 1)) return;
            float scale = Mathf.Clamp(_model.transform.localScale.x * Mathf.Exp(amount * Time.unscaledDeltaTime * 1.1f), .12f, 5);
            _model.transform.localScale = Vector3.one * scale;
            if (IsHeld) _grab.SetTargetLocalScale(Vector3.one * scale);
        }
        public void AdjustParameter(float amount)
        {
            if (Data?.experiment == null || string.IsNullOrEmpty(Data.experiment.label) || Mathf.Abs(amount) < .15f) return;
            if (!_requestPending) ClearRequest();
            Parameter = Mathf.Clamp01(Parameter + amount * Time.unscaledDeltaTime * .45f); _answer = null; _experimentActive = true;
            StepIndex = -1;
            LearningText = Parameter < .5f ? Data.experiment.lowExplanation : Data.experiment.highExplanation;
        }
        public void NextStep()
        {
            if (!_requestPending) ClearRequest();
            _answer = null; _experimentActive = false; Parameter = 0;
            if (Data?.steps == null || Data.steps.Length == 0) { ToggleExploded(); return; }
            StepIndex = (StepIndex + 1) % (Data.steps.Length + 1);
            if (StepIndex == Data.steps.Length) StepIndex = -1;
            LearningText = StepIndex < 0 ? Data.explanation : Data.steps[StepIndex].explanation;
        }
        public void Explain(string answer)
        { ClearRequest(); _answer = answer; LearningText = answer; }
        public void RequestFeedback(string status, string message, bool pending = true, bool error = false)
        { _requestStatus = status; _requestBody = message; _requestPending = pending; _requestError = error; }
        public void ClearRequest()
        { _requestStatus = _requestBody = null; _requestPending = _requestError = false; }
        public void ToggleExploded() { Exploded = !Exploded; }
        public void ToggleAnimation() { _paused = !_paused; }
        public void ResetExploration() { if (!_requestPending) ClearRequest(); StepIndex = -1; Parameter = 0; Exploded = false; _answer = null; _experimentActive = false; LearningText = Data?.explanation; }
        public void RestoreExploration(int step, float parameter)
        {
            StepIndex = Data?.steps == null ? -1 : Mathf.Clamp(step, -1, Data.steps.Length - 1);
            Parameter = Mathf.Clamp01(parameter);
            _experimentActive = Parameter > 0;
            LearningText = StepIndex < 0 ? Data?.explanation : Data.steps[StepIndex].explanation;
            if (_experimentActive) LearningText = Parameter < .5f ? Data.experiment?.lowExplanation : Data.experiment?.highExplanation;
        }

        private void LateUpdate()
        {
            if (_model == null) return;
            float dt = Mathf.Min(Time.unscaledDeltaTime, .05f), blend = 1 - Mathf.Exp(-dt * 10);
            if (!_paused && !IsHeld) _clock += dt;
            _spread = Mathf.MoveTowards(_spread, Exploded ? 1 : 0, dt * 2);
            _parameterVisual = Mathf.Lerp(_parameterVisual, Parameter, blend);
            StudyStepData step = StepIndex >= 0 ? Data.steps[StepIndex] : null;
            for (int i = 0; i < _parts.Count; i++)
            {
                var part = _parts[i]; var item = part.data;
                Vector3 position = part.position, scale = Vector3.one;
                Quaternion rotation = Quaternion.identity; bool visible = true;
                float angle = _clock * Mathf.Clamp(item.speed, -30, 30);
                if (item.motion == "spin") rotation = Quaternion.Euler(0, angle, 0);
                if (item.motion == "orbit") position = Quaternion.Euler(0, angle, 0) * position;
                if (item.motion == "pulse") scale *= 1 + .08f * Mathf.Sin(_clock * Mathf.Clamp(item.speed, -1, 1) * 2 * Mathf.PI);
                ApplyChanges(step?.changes, i, 1, ref position, ref rotation, ref scale, ref visible);
                ApplyChanges(Data.experiment?.changes, i, _parameterVisual, ref position, ref rotation, ref scale, ref visible);
                Vector3 radial = part.position.sqrMagnitude > .0025f ? part.position.normalized : Quaternion.Euler(0, i * 137.5f, 0) * Vector3.right;
                position += radial * (_spread * .32f);
                if ((part.transform.localPosition-position).sqrMagnitude > .0000001f)
                    part.transform.localPosition = Vector3.Lerp(part.transform.localPosition, position, blend);
                if (Quaternion.Angle(part.transform.localRotation,rotation) > .01f)
                    part.transform.localRotation = Quaternion.Slerp(part.transform.localRotation, rotation, blend);
                if ((part.transform.localScale-scale).sqrMagnitude > .0000001f)
                    part.transform.localScale = Vector3.Lerp(part.transform.localScale, scale, blend);
                if (part.hit.enabled != visible) part.hit.enabled = visible;
                if (part.renderer.enabled != visible) part.renderer.enabled = visible;
                bool emphasized = i == _focused || (step?.focus != null && Array.IndexOf(step.focus, i) >= 0);
                if (part.highlighted != emphasized) { part.highlighted = emphasized; part.material.SetFloat("_Highlight", emphasized ? 1 : 0); }
            }
            if (Pane != null && Camera.main != null)
            {
                Pane.gameObject.SetActive(Active || Pane.IsHeld);
                var head = Camera.main.transform;
                // Stay beside the object, so turning toward the card actually brings it into view.
                // Enlargement does not scale or move the reading surface.
                Pane.Follow(_model.transform.position + _captionOffset, head);
                string title = _answer != null || _requestStatus != null || step == null ? Data.title : step.title;
                string body = _requestBody ?? _answer ?? (step != null || _experimentActive ? LearningText :
                    _focused >= 0 ? _parts[_focused].data.name + ": " + _parts[_focused].data.detail : LearningText);
                string experiment = step != null || string.IsNullOrEmpty(Data.experiment?.label) ? "" :
                    Data.experiment.label + "  " + Mathf.RoundToInt(Parameter * 100) + "%  ·  " + (Parameter < .5f ? Data.experiment.lowLabel : Data.experiment.highLabel);
                string footer = _requestStatus ?? (_answer != null ? "ANSWER" : !HasModel ? "EXPLORE THIS WORLD · HOLD LEFT TRIGGER TO ASK" : step != null ? "DISCOVERY " + (StepIndex+1) + " / " + Data.steps.Length : experiment);
                Pane.SetContent(title, body, footer, _requestPending, _requestError);
            }
        }

        private static void ApplyChanges(StudyChangeData[] changes, int index, float weight, ref Vector3 position, ref Quaternion rotation, ref Vector3 scale, ref bool visible)
        {
            if (changes == null) return;
            foreach (var change in changes)
                if (change != null && change.part == index)
                {
                    position += change.offset * weight;
                    rotation *= Quaternion.Euler(change.rotation * weight);
                    scale = Vector3.Scale(scale, Vector3.Lerp(Vector3.one, change.scale, weight));
                    if (!change.visible && weight > .005f) visible = false;
                }
        }

        public static Mesh Tube(Vector3[] points, float radius) => StudyGeometry.Tube(points, radius);
        public void Clear()
        {
            if (_model != null) { _model.SetActive(false); Destroy(_model); }
            if (Pane != null) { Pane.gameObject.SetActive(false); Destroy(Pane.gameObject); }
            foreach (var part in _parts) if (part.material != null) Destroy(part.material);
            foreach (var mesh in _meshes) if (mesh != null) Destroy(mesh);
            _parts.Clear(); _meshes.Clear(); _grab = null; _model = null; _focused = -1;
            Pane = null; _collider = null; Data = null; Exploded = false; StepIndex = -1;
            _spread = _clock = _parameterVisual = Parameter = 0; _answer = null; _experimentActive = false;
            ClearRequest();
        }
        private void OnDestroy() { Clear(); }
    }
}
