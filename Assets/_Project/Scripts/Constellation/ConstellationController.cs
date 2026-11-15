using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using Zap.Core;
using Zap.Net;
using Zap.World;

namespace Zap.Constellation
{
    public sealed class ConstellationController : MonoBehaviour
    {
        public TopicBall Current { get; private set; }
        public IReadOnlyList<TopicBall> Balls => _balls;
        public string Status { get; private set; } = "Hold A and say a topic. Release to write it on a ball.";
        public bool Busy => _busy;
        public bool InWorld => _worldVisible;
        public bool IsListening => _voice != null && (_voice.IsRecording || _voice.IsStarting);
        public bool ShowControlsCard { get; set; } = true;
        public string ConstellationId { get; private set; }
        public string ConstellationName { get; private set; }
        private readonly List<TopicBall> _balls = new List<TopicBall>();
        private readonly List<NodeEdge> _edges = new List<NodeEdge>();
        private readonly List<LineRenderer> _lines = new List<LineRenderer>();
        private readonly ControllerActions _input = new ControllerActions();
        private VoiceRecorder _voice;
        private WorldSession _world;
        private WorldControls _worldControls;
        private Transform _left, _right, _content;
        private Material _material, _lineMaterial;
        private TextMeshPro _hud;
        private int _hudFrame;
        private TopicBall _voiceTarget, _worldNode, _lineStart;
        private string _voiceAction;
        private string _savePath;
        private bool _busy, _recordButton, _trigger, _worldVisible, _pulling, _dirty;
        private Vector3 _pullMidpoint;
        private float _pullDistance, _lastSave, _recordStart;
        private bool _cutting;
        private Vector3 _cutOrigin;
        private Vector3 _cutDirection;
        private const float CutRange = 4f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (SceneManager.GetActiveScene().name == "Void" && FindAnyObjectByType<ConstellationController>() == null)
                new GameObject("[ZAP Constellation]").AddComponent<ConstellationController>();
        }

        private IEnumerator Start()
        {
            // Unity forbids asking about the XR device in a field initializer, so the desktop wording is set here.
            if (ZapPlatform.IsDesktop) Status = "Type a topic below and press Enter, or hold V and speak.";
            _content = new GameObject("Stars and connections").transform;
            _content.SetParent(transform, false);
            gameObject.AddComponent<SuggestedStars>().Initialize(this, _content);
            _material = new Material(Shader.Find("Zap/Solid"));
            _material.SetColor("_BaseColor", ZapTheme.Current.nodeColor);
            _lineMaterial = new Material(Shader.Find("Zap/Solid"));
            _lineMaterial.SetColor("_BaseColor", ZapTheme.Current.lineColor);
            _voice = gameObject.AddComponent<VoiceRecorder>();
            _voice.Error += SetStatus;
            var entry = ConstellationLibrary.EnsureDefault();
            LoadConstellation(entry);
            ConstellationLibrary.LastOpenedId = entry.id;
            while ((_world = FindAnyObjectByType<WorldSession>()) == null) yield return null;
            _worldControls = _world.GetComponent<WorldControls>();
            _worldControls.StatusChanged += SetStatus;
            BuildHud();
        }

        private void Update()
        {
            if (_voice == null || _world == null) return;
            FindControllers();
            _input.Poll();
            bool voiceHeld = _input.CreateHeld || _input.EditHeld;
            if (voiceHeld && !_recordButton && !_busy)
            {
                _voiceAction = _input.EditHeld ? "edit" : (_world.IsEntered ? "steer" : "create");
                _voiceTarget = _world.IsEntered ? _worldNode : Current;
                if (_world.IsEntered && _world.State != "streaming") SetStatus("Wait for this world to open before speaking.");
                else if (_voiceAction == "edit" && _voiceTarget == null) SetStatus(ZapPlatform.IsDesktop ? "Select a topic before holding B to edit it." : "Point at or grab a ball before holding B to edit.");
                else
                {
                    _recordStart = Time.unscaledTime;
                    SetStatus(_voiceAction == "edit" ? "Listening: edit " + _voiceTarget.Data.topic : "Listening… release the button when finished.");
                    StartCoroutine(_voice.Begin());
                }
            }
            if ((!voiceHeld && _recordButton) || (_voice.IsRecording && Time.unscaledTime - _recordStart >= 20))
            {
                var wav = _voice.Finish();
                if (wav != null) StartCoroutine(TranscribeAndApply(wav, _voiceAction, _voiceTarget));
            }
            _recordButton = voiceHeld;
            _world.GetComponent<WorldAudio>()?.SetMuted(_voice.IsRecording || _voice.IsStarting);
            if (_input.ExitDown && _world.IsEntered) LeaveWorld();
            if (_input.EnterDown && !_world.IsEntered && Current != null && !_busy) EnterWorld(Current);
            if (_worldVisible && !_world.IsEntered) ShowConstellation();
            if (!_world.IsEntered && !_busy && !_voice.IsRecording) UpdateConstellationControls();
            if (Time.unscaledTime - _lastSave > 3) Save();
            UpdateHud();
            UpdateLines();
        }

        public TopicBall CreateBall(NodeData data, Vector3 position)
        {
            var obj = new GameObject("Ball: " + data.topic);
            obj.transform.SetParent(_content, false);
            obj.transform.position = position;
            data.position = position;
            var ball = obj.AddComponent<TopicBall>();
            ball.Initialize(data, _material);
            ball.Selected += Select;
            ball.EnterRequested += EnterWorld;
            _balls.Add(ball);
            _dirty = true;
            Select(ball);
            return ball;
        }

        public void Select(TopicBall ball)
        {
            if (_worldVisible) return;
            if (Current != null) Current.SetSelected(false);
            Current = ball;
            if (Current != null) Current.SetSelected(true);
        }

        public void SubmitText(string text, bool edit)
        {
            if (_busy || _voice.IsRecording) return;
            if (edit && Current == null) { SetStatus("Select a ball first."); return; }
            StartCoroutine(ApplyText(text, edit ? "edit" : (_world.IsEntered ? "steer" : "create"), _world.IsEntered ? _worldNode : Current));
        }

        private IEnumerator TranscribeAndApply(byte[] wav, string action, TopicBall target)
        {
            _busy = true;
            SetStatus("Transcribing your voice…");
            string response = null;
            yield return Post("/speech/transcribe", wav, "audio/wav", result => response = result, 120);
            if (response == null) { _busy = false; yield break; }
            var text = JsonUtility.FromJson<Transcript>(response)?.text;
            if (string.IsNullOrWhiteSpace(text)) { SetStatus("No speech heard. Try again."); _busy = false; yield break; }
            yield return ApplyText(text, action, target);
        }

        private IEnumerator ApplyText(string text, string action, TopicBall target, Vector3? position = null)
        {
            _busy = true;
            if (action == "steer")
            {
                if (_world.IsEntered) _worldControls.Send("voice", text);
                else SetStatus(ZapPlatform.IsDesktop ? "You left that world. Type, or hold V to speak." : "You left that world. Hold A to speak again.");
                _busy = false;
                yield break;
            }
            if (action == "edit" && target == null) { SetStatus("That ball is no longer available."); _busy = false; yield break; }
            SetStatus((action == "edit" ? "Editing: " : "Writing: ") + text);
            // Saved world images do not belong in a text-edit request.
            var request = new NodeRequest { action = action, text = text, node = target != null
                ? new NodeData { topic = target.Data.topic, summary = target.Data.summary, worldPrompt = target.Data.worldPrompt } : null };
            var contextNode = action == "edit" ? target.Data : new NodeData { parentIds = target != null ? new[] { target.Data.id } : Array.Empty<string>() };
            request.parents = NodeContext.Parents(contextNode, _balls.Select(b => b.Data))
                .Select(n => new ParentNote { id = n.id, topic = n.topic, summary = n.summary }).ToArray();
            string response = null;
            yield return Post("/nodes/update", Encoding.UTF8.GetBytes(JsonUtility.ToJson(request)), "application/json", result => response = result, 50);
            if (response != null)
            {
                var result = JsonUtility.FromJson<NodeResult>(response);
                if (result == null || string.IsNullOrWhiteSpace(result.topic)) SetStatus("The note was empty. Please try again.");
                else
                {
                    var parent = target;
                    var data = action == "edit" ? target.Data : new NodeData { parentIds = contextNode.parentIds };
                    data.topic = result.topic; data.summary = result.summary ?? "";
                    data.worldPrompt = result.worldPrompt ?? "";
                    data.suggestions = result.suggestions ?? Array.Empty<string>();
                    if (action == "edit") { data.image = ""; target.Refresh(); Select(target); }
                    else
                    {
                        var cam = Camera.main.transform;
                        var location = position ?? (_right != null ? _right.position + _right.forward * .16f : cam.position + cam.forward * .65f + Vector3.down * .15f);
                        target = CreateBall(data, location);
                        if (parent != null)
                        {
                            parent.Data.suggestions = SuggestionPicker.Without(SuggestionPicker.Without(parent.Data.suggestions, text), data.topic);
                            _edges.Add(new NodeEdge { from = parent.Data.id, to = data.id });
                            RebuildLines();
                        }
                    }
                    _dirty = true;
                    Save();
                    SetStatus(action == "edit" ? "Updated " + data.topic : data.topic + (ZapPlatform.IsDesktop ? " is ready. Drag to move it; double-click to enter its world." : " is ready. Grip to grab; release to throw."));
                    if (action == "edit" && _world.IsEntered && target == _worldNode)
                        _worldControls.Send("scene", NodeContext.WorldPrompt(data, _balls.Select(b => b.Data)));
                }
            }
            _busy = false;
        }

        public void KeepSuggestion(TopicBall parent, string topic, Vector3 position)
        {
            if (_busy || IsListening || InWorld || parent == null || string.IsNullOrWhiteSpace(topic)) return;
            StartCoroutine(ApplyText(topic, "create", parent, position));
        }

        public void Open(string id)
        {
            if (_busy || IsListening || InWorld) return;
            var entry = ConstellationLibrary.Find(id);
            if (entry == null || entry.id == ConstellationId) return;
            if (!TrySave()) return;
            ClearConstellation();
            LoadConstellation(entry);
            ConstellationLibrary.LastOpenedId = entry.id;
            SetStatus("Opened " + ConstellationName);
        }

        public void CreateAndOpen(string name)
        {
            if (_busy || IsListening || InWorld) return;
            if (!TrySave()) return;
            var entry = ConstellationLibrary.Create(name);
            Open(entry.id);
        }

        public void DeleteBall(TopicBall ball)
        {
            if (_busy || IsListening || InWorld || ball == null || !_balls.Contains(ball)) return;
            string topic = ball.Data.topic;
            string id = ball.Data.id;
            _edges.RemoveAll(edge => edge.from == id || edge.to == id);
            for (int i = 0; i < _balls.Count; i++)
            {
                var other = _balls[i];
                if (other == ball) continue;
                other.Data.parentIds = (other.Data.parentIds ?? Array.Empty<string>()).Where(parentId => parentId != id).ToArray();
            }
            if (Current == ball) Select(null);
            _balls.Remove(ball);
            Destroy(ball.gameObject);
            RebuildLines();
            _dirty = true;
            Save();
            SetStatus("Deleted " + topic);
        }

        private IEnumerator Post(string endpoint, byte[] bytes, string contentType, Action<string> success, int timeout)
        {
            using var request = new UnityWebRequest(RelayConfig.BaseUrl.TrimEnd('/') + endpoint, "POST");
            request.uploadHandler = new UploadHandlerRaw(bytes);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", contentType);
            request.timeout = timeout;
            yield return request.SendWebRequest();
            if (request.result == UnityWebRequest.Result.Success) success(request.downloadHandler.text);
            else
            {
                string detail = null;
                try { detail = JsonUtility.FromJson<ApiError>(request.downloadHandler.text)?.detail; } catch (ArgumentException) { }
                SetStatus(string.IsNullOrEmpty(detail) ? "Cannot reach the relay. Check the laptop address and Wi-Fi, then try again." : detail);
            }
        }

        public void EnterWorld(TopicBall ball)
        {
            if (_world == null || _world.IsEntered || _busy || _voice.IsRecording || ball == null) return;
            _worldNode = ball;
            Save();
            _worldVisible = true;
            _content.gameObject.SetActive(false);
            SetStatus("Opening " + ball.Data.topic + "…");
            string prompt = NodeContext.WorldPrompt(ball.Data, _balls.Select(b => b.Data));
            if (ball.Data.imageContext != prompt) ball.Data.image = "";
            ball.Data.imageContext = prompt;
            _dirty = true;
            _world.Enter(ball.Data.id, ball.Data.topic, prompt, ball.Data.image);
        }

        public void LeaveWorld()
        {
            if (_world == null) return;
            var texture = _world.GetComponent<WorldStream>().Texture;
            if (_worldNode != null && _world.State == "streaming" && texture != null && texture.width > 2)
            { _worldNode.Data.image = Convert.ToBase64String(texture.EncodeToJPG(65)); _dirty = true; }
            _world.Exit();
            ShowConstellation();
            SetStatus("Back in your constellation.");
            Save();
        }

        private void ShowConstellation()
        {
            _worldVisible = false;
            _content.gameObject.SetActive(true);
        }

        private void UpdateConstellationControls()
        {
            if (_input.RightTrigger && !_trigger)
            {
                bool onBall = Current != null && Current.Grab.isHovered;
                _lineStart = onBall ? Current : null;
                _cutting = !onBall && _right != null;
                if (_cutting) { _cutOrigin = _right.position; _cutDirection = _right.forward; }
            }
            if (_cutting && _input.RightTrigger && _right != null)
            {
                var direction = _right.forward;
                bool cut = CutAcross(_cutOrigin, _cutDirection, _right.position, direction);
                _cutOrigin = _right.position;
                _cutDirection = direction;
                if (cut) _cutting = false;
            }
            if (!_input.RightTrigger && _trigger)
            {
                _cutting = false;
                if (_lineStart != null && Current != null && Current != _lineStart) ToggleLink(_lineStart, Current);
                _lineStart = null;
            }
            _trigger = _input.RightTrigger;
            bool canPull = _input.LeftGrip && _input.RightGrip && _left != null && _right != null && !_balls.Any(b => b.Grab.isSelected);
            if (canPull)
            {
                var mid = (_left.position + _right.position) * .5f;
                float distance = Vector3.Distance(_left.position, _right.position);
                if (_pulling && distance > .08f && _pullDistance > .08f)
                {
                    float scale = Mathf.Clamp(distance / _pullDistance, .9f, 1.1f);
                    foreach (var ball in _balls)
                    {
                        ball.transform.position = mid + (ball.transform.position - _pullMidpoint) * scale;
                        ball.Body.linearVelocity = Vector3.zero;
                    }
                }
                _pullMidpoint = mid; _pullDistance = distance;
            }
            _pulling = canPull;
        }

        public bool CutAcross(Vector3 previousOrigin, Vector3 previousDirection, Vector3 origin, Vector3 direction)
        {
            int index = -1;
            float closest = 0f;
            for (int i = 0; i < _lines.Count; i++)
            {
                if (!_lines[i].enabled) continue;
                if (!LineCutter.SweepCrosses(previousOrigin, previousDirection, origin, direction, CutRange,
                        _lines[i].GetPosition(0), _lines[i].GetPosition(1), out float distance)) continue;
                if (index < 0 || distance < closest) { closest = distance; index = i; }
            }
            if (index < 0) return false;
            CutEdge(_edges[index]);
            return true;
        }

        private void CutEdge(NodeEdge edge)
        {
            var from = _balls.FirstOrDefault(b => b.Data.id == edge.from);
            var to = _balls.FirstOrDefault(b => b.Data.id == edge.to);
            if (from != null && to != null) ToggleLink(from, to);
            else
            {
                _edges.Remove(edge);
                _dirty = true;
                RebuildLines();
                Save();
            }
            SetStatus("Connection cut.");
        }

        public void ToggleLink(TopicBall from, TopicBall to)
        {
            var edge = _edges.FirstOrDefault(e => (e.from == from.Data.id && e.to == to.Data.id) || (e.from == to.Data.id && e.to == from.Data.id));
            if (edge != null)
            {
                _edges.Remove(edge);
                var child = _balls.FirstOrDefault(b => b.Data.id == edge.to);
                if (child != null) child.Data.parentIds = (child.Data.parentIds ?? Array.Empty<string>()).Where(id => id != edge.from).ToArray();
                SetStatus("Parent connection removed.");
            }
            else
            {
                if (NodeContext.WouldCycle(from.Data, to.Data, _balls.Select(b => b.Data)))
                { SetStatus("That parent link would make a loop. Choose another topic."); return; }
                _edges.Add(new NodeEdge { from = from.Data.id, to = to.Data.id });
                to.Data.parentIds = (to.Data.parentIds ?? Array.Empty<string>()).Append(from.Data.id).Distinct().ToArray();
                SetStatus(from.Data.topic + " is now a parent of " + to.Data.topic);
            }
            _dirty = true; RebuildLines(); Save();
        }

        private void RebuildLines()
        {
            foreach (var line in _lines) if (line) Destroy(line.gameObject);
            _lines.Clear();
            foreach (var edge in _edges)
            {
                var obj = new GameObject("Connection"); obj.transform.SetParent(_content, false);
                var line = obj.AddComponent<LineRenderer>();
                line.sharedMaterial = _lineMaterial; line.positionCount = 2;
                _lines.Add(line);
            }
        }
        private void UpdateLines()
        {
            var theme = ZapTheme.Current;
            _material.SetColor("_BaseColor", theme.nodeColor);
            _lineMaterial.SetColor("_BaseColor", theme.lineColor);
            for (int i = 0; i < _edges.Count; i++)
            {
                var from = _balls.FirstOrDefault(b => b.Data.id == _edges[i].from);
                var to = _balls.FirstOrDefault(b => b.Data.id == _edges[i].to);
                _lines[i].widthMultiplier = theme.lineWidth;
                _lines[i].enabled = from && to;
                if (from && to) { _lines[i].SetPosition(0, from.transform.position); _lines[i].SetPosition(1, to.transform.position); }
            }
        }

        private void FindControllers()
        {
            if (_left == null) _left = GameObject.Find("XR Origin (XR Rig)/Camera Offset/Left Controller")?.transform;
            if (_right == null) _right = GameObject.Find("XR Origin (XR Rig)/Camera Offset/Right Controller")?.transform;
        }
        private void LoadConstellation(ConstellationLibrary.Entry entry)
        {
            ConstellationId = entry.id;
            ConstellationName = entry.name;
            _savePath = entry.path;
            var data = NodeStore.Load(_savePath);
            if (!string.IsNullOrWhiteSpace(data.name)) ConstellationName = data.name;
            foreach (var node in data.nodes)
                if (node != null && !string.IsNullOrWhiteSpace(node.topic)) CreateBall(node, node.position);
            if (data.edges != null) _edges.AddRange(data.edges.Where(edge => edge != null));
            RebuildLines();
            _dirty = false;
        }
        private void ClearConstellation()
        {
            Select(null);
            foreach (var ball in _balls)
                if (ball != null) Destroy(ball.gameObject);
            _balls.Clear();
            foreach (var line in _lines)
                if (line != null) Destroy(line.gameObject);
            _lines.Clear();
            _edges.Clear();
        }
        private void BuildHud()
        {
            var obj = new GameObject("ZAP controls"); obj.transform.SetParent(transform, false);
            _hud = obj.AddComponent<TextMeshPro>(); _hud.richText = false;
            _hud.enableAutoSizing = true;
            _hud.overflowMode = TextOverflowModes.Ellipsis;
            obj.SetActive(ShowControlsCard);
            obj.transform.position = new Vector3(0f, 1.2f, ZapTheme.Current.controlsDistance);
            obj.transform.rotation = Quaternion.identity;
        }
        private void UpdateHud()
        {
            if (_hud != null && _hud.gameObject.activeSelf != ShowControlsCard) _hud.gameObject.SetActive(ShowControlsCard);
            if (!ShowControlsCard) return;
            if (_hud == null || Camera.main == null) return;
            var theme = ZapTheme.Current;
            _hud.rectTransform.sizeDelta = theme.controlsCardSize;
            _hud.fontSizeMin = theme.controlsTextSizeMin;
            _hud.fontSizeMax = theme.controlsTextSizeMax;
            _hud.color = theme.textColor;
            if (theme.font != null) _hud.font = theme.font;
            if (HomeFrame.Version != _hudFrame)
            {
                _hud.transform.position = new Vector3(0f, HomeFrame.HeadHeight - theme.controlsDropBelowEyes, theme.controlsDistance);
                _hud.transform.rotation = Quaternion.identity;
                _hudFrame = HomeFrame.Version;
            }
            string title = _worldVisible ? _worldNode?.Data.topic : (Current != null ? Current.Data.topic : "ZAP!");
            string controls = _worldVisible ? "Sticks: move / look · A: steer by voice\nB: edit this topic · Trigger: event · Y: return" : "A: new topic · B: edit selected\nGrip: grab / throw · X: enter world\nTrigger: link two balls · sweep a line to cut";
            _hud.text = title + "\n\n" + (_voice.IsRecording ? "● Listening " + new string('|', Mathf.Clamp(Mathf.RoundToInt(_voice.Level * 150), 1, 12)) : Status)
                + "\n\n" + controls;
        }
        public void SetStatus(string text) { Status = text; }
        public void Save()
        {
            TrySave();
        }
        private bool TrySave()
        {
            if (_savePath == null) return true;
            foreach (var ball in _balls)
                if (Vector3.Distance(ball.Data.position, ball.transform.position) > .002f)
                { ball.Data.position = ball.transform.position; _dirty = true; }
            _lastSave = Time.unscaledTime;
            if (!_dirty) return true;
            try
            {
                NodeStore.Save(_savePath, new ConstellationData
                {
                    name = ConstellationName,
                    updated = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                    nodes = _balls.Select(b => b.Data).ToArray(),
                    edges = _edges.ToArray()
                });
                _dirty = false;
                return true;
            }
            catch (Exception exception)
            {
                SetStatus("Could not save your constellation: " + exception.Message);
                return false;
            }
        }
        private void OnApplicationPause(bool paused) { if (paused) { if (_world != null && _world.IsEntered) LeaveWorld(); Save(); } }
        private void OnApplicationQuit() { Save(); }
        private void OnDestroy()
        {
            if (_worldControls != null) _worldControls.StatusChanged -= SetStatus;
            if (_material) Destroy(_material);
            if (_lineMaterial) Destroy(_lineMaterial);
        }

        private void OnGUI()
        {
            if (ZapPlatform.IsDesktop) return;
            if (!(Application.isEditor || Debug.isDebugBuild) || Application.platform == RuntimePlatform.Android || _voice == null) return;
            GUILayout.BeginArea(new Rect(16, 16, 440, 150), GUI.skin.box);
            GUILayout.Label("ZAP! — V: speak new · B: speak edit · Enter: world · Esc: return");
            GUILayout.Label(Status);
            GUI.enabled = !_busy && !_voice.IsRecording;
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Enter") && Current != null) EnterWorld(Current);
            if (GUILayout.Button("Return")) LeaveWorld();
            GUILayout.EndHorizontal();
            if (Current != null) GUILayout.Label("Selected: " + Current.Data.topic);
            GUILayout.Label("Relay: " + RelayConfig.BaseUrl);
            GUI.enabled = true;
            GUILayout.EndArea();
        }

        [Serializable] private class Transcript { public string text; }
        [Serializable] private class ApiError { public string detail; }
        [Serializable] private class ParentNote { public string id; public string topic; public string summary; }
        [Serializable] private class NodeRequest { public string action; public string text; public NodeData node; public ParentNote[] parents; }
        [Serializable] private class NodeResult { public string topic; public string summary; public string worldPrompt; public string[] suggestions; public string mode; }
    }
}
