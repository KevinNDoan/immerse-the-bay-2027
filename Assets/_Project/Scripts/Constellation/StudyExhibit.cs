using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Transformers;

namespace Zap.Constellation
{
    // Floating, inspectable representations share the same controls for every topic.
    public sealed class StudyExhibit : MonoBehaviour
    {
        public StudyModelData Data { get; private set; }
        public bool IsHeld => _grab != null && _grab.isSelected;
        public bool IsFocused => _focused >= 0;
        public bool Exploded { get; private set; }
        public int PartCount => _parts.Count;
        public XRGrabInteractable Grab => _grab;
        public int StepIndex { get; private set; } = -1;
        public float Parameter { get; private set; }
        public string FocusedName => _focused >= 0 ? _parts[_focused].data.name : "";
        public string LearningText { get; private set; }
        public bool Active { get; set; } = true;
        public float FocusDistance { get; private set; } = float.PositiveInfinity;
        private GameObject _model;
        private Transform _assembly;
        private XRGrabInteractable _grab;
        private TextMeshPro _caption;
        private readonly List<Part> _parts = new List<Part>();
        private readonly List<Mesh> _meshes = new List<Mesh>();
        private int _focused = -1;
        private float _spread, _clock, _fit, _parameterVisual, _nextFocus;
        private bool _paused, _experimentActive;
        private BoxCollider _collider;
        private string _answer, _captionText;
        private Vector3 _boundsSize;
        public void SetEnvironment(Zap.World.GeneratedEnvironment environment) { }
        private sealed class Part
        {
            public StudyPartData data;
            public Transform transform;
            public MeshCollider hit;
            public Material material;
            public Color color;
            public Vector3 position;
        }

        public void Build(StudyModelData data)
        {
            if (data?.parts == null || data.parts.Length == 0 || data.parts.Length > 28) return;
            Clear(); Data = data;
            _model = new GameObject("Explore: " + data.title);
            _model.transform.SetParent(transform, false);
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
                obj.AddComponent<MeshRenderer>().sharedMaterial = material;
                _parts.Add(new Part { data = item, transform = obj.transform, hit = hit, material = material, color = color, position = center });
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
            var caption = new GameObject("Exploration card"); caption.transform.SetParent(transform, false);
            _caption = caption.AddComponent<TextMeshPro>(); _caption.fontSize = .28f;
            _caption.enableAutoSizing = true; _caption.fontSizeMin = .22f; _caption.fontSizeMax = .28f;
            _caption.richText = false; _caption.alignment = TextAlignmentOptions.TopLeft;
            _caption.rectTransform.sizeDelta = new Vector2(1.05f, .65f);
            _caption.overflowMode = TextOverflowModes.Ellipsis;
            LearningText = data.explanation;
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
        }

        public void Focus(Ray ray)
        {
            if (Time.unscaledTime < _nextFocus) return;
            _nextFocus = Time.unscaledTime + .055f;
            _focused = -1; FocusDistance = float.PositiveInfinity;
            for (int i = 0; i < _parts.Count; i++)
                if (_parts[i].hit.enabled && _parts[i].hit.Raycast(ray, out var hit, 6) && hit.distance < FocusDistance)
                { FocusDistance = hit.distance; _focused = i; }
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
            Parameter = Mathf.Clamp01(Parameter + amount * Time.unscaledDeltaTime * .45f); _answer = null; _experimentActive = true;
            LearningText = Parameter < .5f ? Data.experiment.lowExplanation : Data.experiment.highExplanation;
        }
        public void NextStep()
        {
            _answer = null; _experimentActive = false;
            if (Data?.steps == null || Data.steps.Length == 0) { ToggleExploded(); return; }
            StepIndex = (StepIndex + 1) % (Data.steps.Length + 1);
            if (StepIndex == Data.steps.Length) StepIndex = -1;
            LearningText = StepIndex < 0 ? Data.explanation : Data.steps[StepIndex].explanation;
        }
        public void Explain(string answer) { _answer = answer; LearningText = answer; }
        public void ToggleExploded() { Exploded = !Exploded; }
        public void ToggleAnimation() { _paused = !_paused; }
        public void ResetExploration() { StepIndex = -1; Parameter = 0; Exploded = false; _answer = null; _experimentActive = false; LearningText = Data?.explanation; }
        public void RestoreExploration(int step, float parameter)
        {
            StepIndex = Data?.steps == null ? -1 : Mathf.Clamp(step, -1, Data.steps.Length - 1);
            Parameter = Mathf.Clamp01(parameter);
            _experimentActive = Parameter > 0;
            LearningText = StepIndex < 0 ? Data?.explanation : Data.steps[StepIndex].explanation;
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
                part.transform.localPosition = Vector3.Lerp(part.transform.localPosition, position, blend);
                part.transform.localRotation = Quaternion.Slerp(part.transform.localRotation, rotation, blend);
                part.transform.localScale = Vector3.Lerp(part.transform.localScale, visible ? scale : Vector3.one * .001f, blend);
                part.hit.enabled = visible;
                bool emphasized = i == _focused || (step?.focus != null && Array.IndexOf(step.focus, i) >= 0);
                part.material.SetFloat("_Highlight", emphasized ? 1 : 0);
            }
            if (_caption != null && Camera.main != null)
            {
                _caption.gameObject.SetActive(Active);
                var head = Camera.main.transform;
                Vector3 forward = Vector3.ProjectOnPlane(head.forward, Vector3.up).normalized;
                // Stable, readable card independent of model enlargement; never follows a tiny part into the face.
                _caption.transform.position = head.position + forward * 1.5f + Vector3.down * .63f;
                _caption.transform.rotation = Quaternion.LookRotation(_caption.transform.position - head.position);
                string title = step == null ? Data.title : step.title;
                string body = _answer ?? (step != null || _experimentActive ? LearningText :
                    IsFocused ? _parts[_focused].data.name + ": " + _parts[_focused].data.detail : LearningText);
                string experiment = string.IsNullOrEmpty(Data.experiment?.label) ? "" :
                    "\n" + Data.experiment.label + "  " + Mathf.RoundToInt(Parameter * 100) + "%  ·  " + (Parameter < .5f ? Data.experiment.lowLabel : Data.experiment.highLabel);
                string text = title + "\n" + body + experiment + "\nGrip: move · Stick ↑↓: zoom · ←→: experiment · Trigger: explore";
                if (text != _captionText) { _caption.text = text; _captionText = text; }
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
                    if (!change.visible && weight > .98f) visible = false;
                }
        }

        public static Mesh Tube(Vector3[] points, float radius) => StudyGeometry.Tube(points, radius);
        public void Clear()
        {
            if (_model != null) { _model.SetActive(false); Destroy(_model); }
            if (_caption != null) Destroy(_caption.gameObject);
            foreach (var part in _parts) if (part.material != null) Destroy(part.material);
            foreach (var mesh in _meshes) if (mesh != null) Destroy(mesh);
            _parts.Clear(); _meshes.Clear(); _grab = null; _model = null; _focused = -1;
            _caption = null; _collider = null; Data = null; Exploded = false; StepIndex = -1;
            _spread = _clock = _parameterVisual = Parameter = 0; _answer = _captionText = null; _experimentActive = false;
        }
        private void OnDestroy() { Clear(); }
    }
}
