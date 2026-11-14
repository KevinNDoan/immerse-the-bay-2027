using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
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
        private readonly List<TopicBall> _balls = new List<TopicBall>();
        private readonly List<NodeEdge> _edges = new List<NodeEdge>();
        private readonly List<LineRenderer> _lines = new List<LineRenderer>();
        private readonly ControllerActions _input = new ControllerActions();
        private VoiceRecorder _voice;
        private WorldSession _world;
        private Transform _left, _right, _content;
        private Material _material;
        private TextMeshPro _hud;
        private TopicBall _voiceTarget, _worldNode, _lineStart;
        private string _voiceAction;
        private string _savePath;
        private string _typed = "Volcanoes";
        private bool _busy, _recordButton, _trigger, _worldVisible, _pulling, _dirty;
        private Vector3 _pullMidpoint;
        private float _pullDistance, _lastSave, _lastSteer, _recordStart;
        private uint _pendingSeq;
        private string _pendingInstruction;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (SceneManager.GetActiveScene().name == "Void" && FindAnyObjectByType<ConstellationController>() == null)
                new GameObject("[ZAP Constellation]").AddComponent<ConstellationController>();
        }

        private IEnumerator Start()
        {
            _savePath = Path.Combine(Application.persistentDataPath, "zap-constellation.json");
            _content = new GameObject("Stars and connections").transform;
            _content.SetParent(transform, false);
            _material = new Material(Shader.Find("Zap/Solid"));
            _material.SetColor("_BaseColor", Color.white);
            _voice = gameObject.AddComponent<VoiceRecorder>();
            _voice.Error += SetStatus;
            var data = NodeStore.Load(_savePath);
            foreach (var node in data.nodes)
                if (node != null && !string.IsNullOrWhiteSpace(node.topic)) CreateBall(node, node.position);
            if (data.edges != null) _edges.AddRange(data.edges.Where(e => e != null));
            RebuildLines();
            while ((_world = FindAnyObjectByType<WorldSession>()) == null) yield return null;
            _world.OnStatus += WorldStatus;
            _world.OnError += e => SetStatus(e.detail);
            _world.OnPrompt += PromptReceived;
            var stream = _world.GetComponent<WorldStream>();
            stream.OnFrameDecoded += FrameReceived;
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
                else if (_voiceAction == "edit" && _voiceTarget == null) SetStatus("Point at or grab a ball before holding B to edit.");
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
            if (_world.IsEntered) UpdateWorldControls();
            else if (!_busy && !_voice.IsRecording) UpdateConstellationControls();
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

        private IEnumerator ApplyText(string text, string action, TopicBall target)
        {
            _busy = true;
            if (action == "steer")
            {
                if (_world.IsEntered) SendSteer("voice", text);
                else SetStatus("You left that world. Hold A to speak again.");
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
                        var location = _right != null ? _right.position + _right.forward * .16f : cam.position + cam.forward * .65f + Vector3.down * .15f;
                        target = CreateBall(data, location);
                        if (parent != null) { _edges.Add(new NodeEdge { from = parent.Data.id, to = data.id }); RebuildLines(); }
                    }
                    _dirty = true;
                    Save();
                    SetStatus(action == "edit" ? "Updated " + data.topic : data.topic + " is ready. Grip to grab; release to throw.");
                    if (action == "edit" && _world.IsEntered && target == _worldNode)
                        SendSteer("scene", NodeContext.WorldPrompt(data, _balls.Select(b => b.Data)));
                }
            }
            _busy = false;
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
            _worldVisible = false; _pendingInstruction = null;
            _content.gameObject.SetActive(true);
        }

        private void UpdateWorldControls()
        {
            if (_world.State != "streaming" || Time.unscaledTime - _lastSteer < 1.8f) return;
            if (_input.EventDown) SendSteer("event", "Show an interesting change that teaches something about " + _worldNode.Data.topic);
            else if (_input.Look.sqrMagnitude > .35f)
                SendSteer("look", Mathf.Abs(_input.Look.x) > Mathf.Abs(_input.Look.y)
                    ? (_input.Look.x > 0 ? "slowly pan right" : "slowly pan left") : (_input.Look.y > 0 ? "slowly look up" : "slowly look down"));
            else if (_input.Move.sqrMagnitude > .35f)
                SendSteer("move", Mathf.Abs(_input.Move.y) > Mathf.Abs(_input.Move.x)
                    ? (_input.Move.y > 0 ? "slowly move forward" : "slowly move backward") : (_input.Move.x > 0 ? "slowly move right" : "slowly move left"));
        }

        private void SendSteer(string kind, string text)
        {
            _lastSteer = Time.unscaledTime; _pendingSeq = uint.MaxValue; _pendingInstruction = text;
            SetStatus("Sending: " + text);
            _world.Steer(kind, text);
        }
        private void PromptReceived(string text, uint sequence) { if (_pendingInstruction != null) _pendingSeq = sequence; }
        private void FrameReceived(uint sequence)
        {
            if (_pendingInstruction != null && sequence >= _pendingSeq)
            { SetStatus("World updated: " + _pendingInstruction); _pendingInstruction = null; }
        }
        private void WorldStatus(StatusMessage status)
        {
            if (status.state == "error") SetStatus(status.detail);
            else if (status.state == "streaming") SetStatus("Explore with the sticks. Hold A to speak. Y returns to your constellation.");
            else if (!string.IsNullOrEmpty(status.detail)) SetStatus(status.detail);
        }

        private void UpdateConstellationControls()
        {
            if (_input.RightTrigger && !_trigger) _lineStart = Current;
            if (!_input.RightTrigger && _trigger && _lineStart != null && Current != null && Current != _lineStart)
            { ToggleLink(_lineStart, Current); _lineStart = null; }
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
                line.sharedMaterial = _material; line.positionCount = 2; line.widthMultiplier = .0025f;
                _lines.Add(line);
            }
        }
        private void UpdateLines()
        {
            for (int i = 0; i < _edges.Count; i++)
            {
                var from = _balls.FirstOrDefault(b => b.Data.id == _edges[i].from);
                var to = _balls.FirstOrDefault(b => b.Data.id == _edges[i].to);
                _lines[i].enabled = from && to;
                if (from && to) { _lines[i].SetPosition(0, from.transform.position); _lines[i].SetPosition(1, to.transform.position); }
            }
        }

        private void FindControllers()
        {
            if (_left == null) _left = GameObject.Find("XR Origin (XR Rig)/Camera Offset/Left Controller")?.transform;
            if (_right == null) _right = GameObject.Find("XR Origin (XR Rig)/Camera Offset/Right Controller")?.transform;
        }
        private void BuildHud()
        {
            var obj = new GameObject("ZAP controls"); obj.transform.SetParent(transform, false);
            _hud = obj.AddComponent<TextMeshPro>(); _hud.fontSize = .35f; _hud.richText = false;
            _hud.enableAutoSizing = true; _hud.fontSizeMin = .22f; _hud.fontSizeMax = .35f;
            _hud.overflowMode = TextOverflowModes.Ellipsis;
            _hud.rectTransform.sizeDelta = new Vector2(.5f, .32f);
            if (Camera.main) obj.transform.position = Camera.main.transform.position + Camera.main.transform.forward * 1.1f + Vector3.down * .25f;
        }
        private void UpdateHud()
        {
            if (_hud == null || Camera.main == null) return;
            if (_left != null)
            {
                Vector3 offset = _left.position + Vector3.up * .13f + _left.forward * .18f - Camera.main.transform.position;
                // Keep the wrist card readable when the controller comes near the face.
                if (offset.sqrMagnitude < .01f) offset = Camera.main.transform.forward;
                _hud.transform.position = Camera.main.transform.position + offset.normalized * Mathf.Max(.6f, offset.magnitude);
            }
            _hud.transform.rotation = Quaternion.LookRotation(_hud.transform.position - Camera.main.transform.position);
            string title = _worldVisible ? _worldNode?.Data.topic : (Current != null ? Current.Data.topic : "ZAP!");
            string controls = _worldVisible ? "Sticks: move / look · A: steer by voice\nB: edit this topic · Trigger: event · Y: return" : "A: new topic · B: edit selected\nGrip: grab / throw · X: enter world";
            _hud.text = title + "\n\n" + (_voice.IsRecording ? "● Listening " + new string('|', Mathf.Clamp(Mathf.RoundToInt(_voice.Level * 150), 1, 12)) : Status)
                + "\n\n" + controls;
        }
        public void SetStatus(string text) { Status = text; }
        public void Save()
        {
            if (_savePath == null) return;
            foreach (var ball in _balls)
                if (Vector3.Distance(ball.Data.position, ball.transform.position) > .002f)
                { ball.Data.position = ball.transform.position; _dirty = true; }
            _lastSave = Time.unscaledTime;
            if (!_dirty) return;
            try
            {
                NodeStore.Save(_savePath, new ConstellationData { nodes = _balls.Select(b => b.Data).ToArray(), edges = _edges.ToArray() });
                _dirty = false;
            }
            catch (Exception exception) { SetStatus("Could not save your constellation: " + exception.Message); }
        }
        private void OnApplicationPause(bool paused) { if (paused) { if (_world != null && _world.IsEntered) LeaveWorld(); Save(); } }
        private void OnApplicationQuit() { Save(); }
        private void OnDestroy() { if (_material) Destroy(_material); }

        private void OnGUI()
        {
            if (!(Application.isEditor || Debug.isDebugBuild) || Application.platform == RuntimePlatform.Android || _voice == null) return;
            GUILayout.BeginArea(new Rect(16, 16, 440, 250), GUI.skin.box);
            GUILayout.Label("ZAP! — V: speak new · B: speak edit · Enter: world · Esc: return");
            GUILayout.Label(Status);
            _typed = GUILayout.TextField(_typed, 2000);
            GUI.enabled = !_busy && !_voice.IsRecording;
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("New ball")) SubmitText(_typed, false);
            if (GUILayout.Button("Edit selected")) SubmitText(_typed, true);
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
