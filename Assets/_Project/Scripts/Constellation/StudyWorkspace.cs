using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Zap.Constellation
{
    // One persistent collection per idea; generating another object never rebuilds the room.
    public sealed partial class StudyWorkspace : MonoBehaviour
    {
        public StudyExhibit Current { get; private set; }
        public IReadOnlyList<StudyExhibit> Objects => _objects;
        public bool Full => _objects.FindAll(item => item.HasModel).Count >= 6;
        private readonly List<StudyExhibit> _objects = new List<StudyExhibit>();
        private string _path;
        private Vector3 _origin;
        private Quaternion _rotation;
        private float _lastSave;
        [Serializable] private class SavedObject
        {
            public string id;
            public StudyModelData model;
            public Vector3 position, rotation, scale;
            public int step;
            public float parameter;
            public string answer;
            public bool panePlaced;
            public Vector3 panePosition, paneRotation;
        }
        [Serializable] private class SavedSpace { public int version = 1; public SavedObject[] objects; }

        public void Open(string nodeId, StudyModelData initial, WorldPaneData pane = null)
        {
            Close();
            if (Camera.main == null) return;
            _origin = Camera.main.transform.position;
            _rotation = Quaternion.Euler(0, Camera.main.transform.eulerAngles.y, 0);
            string key;
            using (var hash = System.Security.Cryptography.SHA256.Create())
                key = BitConverter.ToString(hash.ComputeHash(System.Text.Encoding.UTF8.GetBytes(nodeId))).Replace("-", "");
            _path = Path.Combine(Application.persistentDataPath, "exploration", key + ".json");
            SavedSpace saved = null;
            foreach (string candidate in new[] { _path, _path + ".bak" })
            {
                try
                {
                    if (File.Exists(candidate)) saved = JsonUtility.FromJson<SavedSpace>(File.ReadAllText(candidate));
                    if (saved?.version == 1 && saved.objects != null && saved.objects.Length <= 7) break;
                    saved = null;
                }
                catch (Exception e) when (e is IOException || e is ArgumentException) { saved = null; }
            }
            if (saved != null)
                foreach (var item in saved.objects)
                {
                    var exhibit = Add(item.model, false);
                    if (exhibit?.Pose == null) continue;
                    if (!string.IsNullOrEmpty(item.id) && !_objects.Exists(other => other != exhibit && other.ObjectId == item.id))
                        exhibit.ObjectId = item.id;
                    exhibit.Pose.SetPositionAndRotation(_origin + _rotation * item.position, _rotation * Quaternion.Euler(item.rotation));
                    exhibit.Pose.localScale = Vector3.one * Mathf.Clamp(item.scale.x, .12f, 5);
                    exhibit.RestoreExploration(item.step, item.parameter);
                    if (!string.IsNullOrEmpty(item.answer)) exhibit.Explain(item.answer);
                    if (item.panePlaced) exhibit.Pane.Place(_origin + _rotation * item.panePosition, _rotation * Quaternion.Euler(item.paneRotation));
                }
            if (_objects.Count == 0)
            {
                if (initial != null) Add(initial);
                if (_objects.Count == 0) Add(new StudyModelData {title=pane?.title ?? "Explore this world",
                    explanation=pane?.text ?? "Look around and explore the space. Hold left trigger to ask a question or wish an object into creation.", parts=Array.Empty<StudyPartData>()});
            }
        }

        public StudyExhibit Add(StudyModelData data, bool arrange = true)
        {
            if (data?.parts == null || (Full && data.parts.Length > 0) || (data.parts.Length == 0 && _objects.Exists(item => !item.HasModel))) return null;
            var obj = new GameObject("Exploration object"); obj.transform.SetParent(transform, false);
            var exhibit = obj.AddComponent<StudyExhibit>(); exhibit.Build(data);
            if (exhibit.Pane == null) { Destroy(obj); return null; }
            if (arrange && Camera.main != null)
            {
                foreach (var other in _objects)
                    if (!other.IsHeld && Vector3.Distance(other.Pose.position, exhibit.Pose.position) < .8f)
                        other.Pose.position += Camera.main.transform.right * (_objects.Count % 2 == 0 ? 1 : -1);
            }
            _objects.Add(exhibit); SetCurrent(exhibit);
            return exhibit;
        }

        public void Focus(Ray ray)
        {
            StudyExhibit nearest = null, held = null;
            foreach (var item in _objects)
            {
                item.Focus(ray);
                if (item.IsHeld || item.Pane.IsHeld) held = item;
                if (item.IsFocused && (nearest == null || item.FocusDistance < nearest.FocusDistance)) nearest = item;
            }
            // Hover targets the next action without hiding the answer someone is reading.
            if (held != null || nearest != null) SetCurrent(held ?? nearest, held != null);
        }
        public void SetCurrent(StudyExhibit exhibit, bool showPane = true)
        {
            Current = exhibit;
            if (showPane) foreach (var item in _objects) item.Active = item == exhibit;
        }
        public bool ApplyReply(StudyReply reply, StudyExhibit target)
        {
            if (reply == null || string.IsNullOrWhiteSpace(reply.answer)) return false;
            if (reply.action == "apply") return ApplyActions(reply, target);
            if (reply.action == "add" || reply.action == "revise")
            {
                if (reply.model?.parts == null || reply.model.parts.Length == 0 || reply.model.parts.Length > 28) return false;
                if (reply.action == "add" || target == null)
                {
                    if (Full) return false;
                    target?.ClearRequest(); target = Add(reply.model);
                }
                else
                {
                    if (!target.HasModel && Full) return false;
                    var pose = target.Pose;
                    Vector3 position = pose.position, scale = pose.localScale; Quaternion rotation = pose.rotation;
                    bool panePlaced = target.Pane.IsPlaced;
                    Vector3 panePosition = target.Pane.transform.position; Quaternion paneRotation = target.Pane.transform.rotation;
                    target.Build(reply.model);
                    if (target.Grab == null) return false;
                    target.Grab.transform.SetPositionAndRotation(position,rotation); target.Grab.transform.localScale = scale;
                    if (panePlaced) target.Pane.Place(panePosition,paneRotation);
                }
            }
            else if (reply.action != "answer") return false;
            if (target == null) return false;
            SetCurrent(target); target.Explain(reply.answer);
            return true;
        }
        public void Save()
        {
            if (string.IsNullOrEmpty(_path)) return;
            var list = new List<SavedObject>();
            foreach (var item in _objects)
            {
                var pose = item.Pose;
                list.Add(new SavedObject { id = item.ObjectId, model = item.Data, position = Quaternion.Inverse(_rotation) * (pose.position - _origin),
                    rotation = (Quaternion.Inverse(_rotation) * pose.rotation).eulerAngles, scale = pose.localScale,
                    step = item.StepIndex, parameter = item.Parameter, answer = item.Answer,
                    panePlaced = item.Pane.IsPlaced,
                    panePosition = Quaternion.Inverse(_rotation) * (item.Pane.transform.position - _origin),
                    paneRotation = (Quaternion.Inverse(_rotation) * item.Pane.transform.rotation).eulerAngles });
            }
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_path));
                File.WriteAllText(_path + ".tmp", JsonUtility.ToJson(new SavedSpace { objects = list.ToArray() }));
                if (File.Exists(_path)) File.Replace(_path + ".tmp", _path, _path + ".bak"); else File.Move(_path + ".tmp", _path);
            }
            catch (IOException e) { Debug.LogWarning("Exploration could not be saved: " + e.Message); }
        }
        private void Update() { if (Time.unscaledTime - _lastSave > 5) { Save(); _lastSave = Time.unscaledTime; } }
        public void Close()
        {
            Save(); _path = null;
            foreach (var item in _objects) { item.Clear(); Destroy(item.gameObject); }
            _objects.Clear(); Current = null;
        }
        private void OnApplicationPause(bool paused) { if (paused) Save(); }
        private void OnDestroy() { Close(); }
    }
}
