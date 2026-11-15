using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Zap.Constellation
{
    // One persistent collection per idea; generating another object never rebuilds the room.
    public sealed class StudyWorkspace : MonoBehaviour
    {
        public StudyExhibit Current { get; private set; }
        public IReadOnlyList<StudyExhibit> Objects => _objects;
        public bool Full => _objects.Count >= 6;
        private readonly List<StudyExhibit> _objects = new List<StudyExhibit>();
        private string _path;
        private Vector3 _origin;
        private Quaternion _rotation;
        private float _lastSave;
        [Serializable] private class SavedObject
        {
            public StudyModelData model;
            public Vector3 position, rotation, scale;
            public int step;
            public float parameter;
        }
        [Serializable] private class SavedSpace { public int version = 1; public SavedObject[] objects; }

        public void Open(string nodeId, StudyModelData initial)
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
                    if (saved?.version == 1 && saved.objects != null && saved.objects.Length <= 6) break;
                    saved = null;
                }
                catch (Exception e) when (e is IOException || e is ArgumentException) { saved = null; }
            }
            if (saved != null)
                foreach (var item in saved.objects)
                {
                    var exhibit = Add(item.model, false);
                    if (exhibit?.Grab == null) continue;
                    exhibit.Grab.transform.SetPositionAndRotation(_origin + _rotation * item.position, _rotation * Quaternion.Euler(item.rotation));
                    exhibit.Grab.transform.localScale = Vector3.one * Mathf.Clamp(item.scale.x, .12f, 5);
                    exhibit.RestoreExploration(item.step, item.parameter);
                }
            if (_objects.Count == 0 && initial != null) Add(initial);
        }

        public StudyExhibit Add(StudyModelData data, bool arrange = true)
        {
            if (Full || data?.parts == null || data.parts.Length == 0) return null;
            var obj = new GameObject("Exploration object"); obj.transform.SetParent(transform, false);
            var exhibit = obj.AddComponent<StudyExhibit>(); exhibit.Build(data);
            if (exhibit.Grab == null) { Destroy(obj); return null; }
            if (arrange && Camera.main != null)
            {
                foreach (var other in _objects)
                    if (!other.IsHeld && Vector3.Distance(other.Grab.transform.position, exhibit.Grab.transform.position) < .8f)
                        other.Grab.transform.position += Camera.main.transform.right * (_objects.Count % 2 == 0 ? 1 : -1);
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
                if (item.IsHeld) held = item;
                if (item.IsFocused && (nearest == null || item.FocusDistance < nearest.FocusDistance)) nearest = item;
            }
            if (held != null || nearest != null) SetCurrent(held ?? nearest);
        }
        public void SetCurrent(StudyExhibit exhibit)
        {
            Current = exhibit;
            foreach (var item in _objects) item.Active = item == exhibit;
        }
        public void Save()
        {
            if (string.IsNullOrEmpty(_path)) return;
            var list = new List<SavedObject>();
            foreach (var item in _objects)
            {
                var pose = item.Grab.transform;
                list.Add(new SavedObject { model = item.Data, position = Quaternion.Inverse(_rotation) * (pose.position - _origin),
                    rotation = (Quaternion.Inverse(_rotation) * pose.rotation).eulerAngles, scale = pose.localScale,
                    step = item.StepIndex, parameter = item.Parameter });
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
