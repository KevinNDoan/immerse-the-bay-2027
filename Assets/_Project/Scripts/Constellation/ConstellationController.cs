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
        public bool CanCancelVoice => _busy || _draftBall != null || (_voice != null && (_voice.IsRecording || _voice.IsStarting));
        private readonly List<TopicBall> _balls = new List<TopicBall>();
        private readonly List<NodeEdge> _edges = new List<NodeEdge>();
        private readonly List<LineRenderer> _lines = new List<LineRenderer>();
        private readonly Dictionary<TopicBall, Vector3> _branchPositions = new Dictionary<TopicBall, Vector3>();
        private readonly HashSet<TopicBall> _previewRequests = new HashSet<TopicBall>();
        private readonly ControllerActions _input = new ControllerActions();
        private readonly WorldNavigation _navigation = new WorldNavigation();
        private StudyWorkspace _space;
        private StudyExhibit _study => _space?.Current;
        private TopicBall _draftBall;
        private StudyExhibit _stickStudy;
        private GeneratedEnvironment _environment;
        private WorldJourney _journey;
        private TopicBall _branchSelection;
        private float _loadingStarted;
        private bool InWorld => (_environment != null && _environment.IsVisible) || (_world != null && _world.IsEntered);
        private bool WorldReady => (_environment != null && _environment.IsVisible) || (_world != null && _world.State == "streaming");
        private bool Travelling => _journey != null && _journey.IsActive;
        private bool _preparingWorld;
        private int _worldRequest;
        private UnityWebRequest _preparationRequest;
        private string _worldContext;
        private int _operationVersion;
        private UnityWebRequest _operationRequest;
        private StudyExhibit _voiceStudy, _operationStudy;
        private StudyRequest _voiceStudyContext;
        private bool _releaseBeforeVoice;
        private GameObject _voidStars;
        private VoiceRecorder _voice;
        private WorldSession _world;
        private Transform _left, _right, _content, _branches;
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
        private float _frameSeconds;
        private int _frameCount, _slowFrames;

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
            _branches = new GameObject("Ideas created inside this world").transform;
            _branches.SetParent(transform, false);
            _material = new Material(Shader.Find("Zap/Solid"));
            _material.SetColor("_BaseColor", Color.white);
            _voice = gameObject.AddComponent<VoiceRecorder>();
            _space = gameObject.AddComponent<StudyWorkspace>();
            _environment = gameObject.AddComponent<GeneratedEnvironment>();
            _journey = gameObject.AddComponent<WorldJourney>();
            _voice.Error += VoiceError;
            var data = NodeStore.Load(_savePath);
            foreach (var node in data.nodes)
                if (node != null && !string.IsNullOrWhiteSpace(node.topic)) CreateBall(node, node.position);
            if (data.edges != null) _edges.AddRange(data.edges.Where(e => e != null));
            RebuildLines();
            while ((_world = FindAnyObjectByType<WorldSession>()) == null) yield return null;
            _world.OnStatus += WorldStatus;
            _world.OnError += e =>
            {
                if (Travelling && !_preparingWorld) LeaveWorld();
                SetStatus(e.detail);
            };
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
            bool voiceHeld = _input.CreateHeld || _input.EditHeld || _input.AskHeld;
            // Cancel wins even when Y and the voice-button release arrive in the same frame.
            bool cancelled = _input.ExitDown && CancelPendingVoice();
            if (!voiceHeld) _releaseBeforeVoice = false;
            if (voiceHeld && !_recordButton && !_releaseBeforeVoice && !_input.ExitDown && !_busy && !Travelling)
            {
                _voiceAction = _input.AskHeld ? "steer" : _input.EditHeld ? "edit" : "create";
                _voiceTarget = InWorld ? (_voiceAction == "edit" ? _branchSelection ?? _worldNode : _worldNode) : Current;
                if (_input.AskHeld && !InWorld) SetStatus("Enter a world before asking about its learning object.");
                else if (InWorld && !WorldReady) SetStatus("Wait for this world to open before speaking.");
                else if (_voiceAction == "edit" && _voiceTarget == null) SetStatus("Point at or grab a ball before holding B to edit.");
                else
                {
                    _voiceStudy = InWorld && (_voiceAction == "steer" || (_voiceAction == "edit" && _voiceTarget == _worldNode)) ? _study : null;
                    _voiceStudyContext = _voiceStudy != null ? CaptureStudy(_voiceStudy) : null;
                    _operationStudy = _voiceStudy;
                    if (_voiceStudy != null)
                    {
                        _space.SetCurrent(_voiceStudy);
                        _voiceStudy.RequestFeedback("LISTENING · Y CANCEL", _voiceAction == "edit" ?
                            "Describe how this idea should change. Release B when finished." :
                            "Ask a question or describe an object you want to explore. Release the left trigger when finished.");
                    }
                    if (_voiceAction == "create") BeginDraft();
                    _recordStart = Time.unscaledTime;
                    SetStatus(_voiceAction == "edit" ? "Listening: edit " + _voiceTarget.Data.topic : "Listening… release the button when finished.");
                    StartCoroutine(_voice.Begin());
                }
            }
            if (!cancelled && !_releaseBeforeVoice && ((!voiceHeld && _recordButton) || (_voice.IsRecording && Time.unscaledTime - _recordStart >= 20)))
            {
                var wav = _voice.Finish();
                if (wav != null) StartCoroutine(TranscribeAndApply(wav, _voiceAction, _voiceTarget, _voiceStudy, _voiceStudyContext));
                else if (!_busy) CancelDraft();
            }
            _recordButton = voiceHeld;
            UpdateDraft();
            _world.GetComponent<WorldAudio>()?.SetMuted(_voice.IsRecording || _voice.IsStarting);
            if (_input.ExitDown && !cancelled && (InWorld || _preparingWorld || Travelling)) LeaveWorld();
            if (_input.EnterDown && !InWorld && !_preparingWorld && Current != null && !_busy) EnterWorld(Current);
            if ((_preparingWorld || Travelling) && Time.unscaledTime - _loadingStarted > 240)
            { LeaveWorld(); SetStatus("This world is still preparing on the laptop. Re-enter to check its progress."); }
            if (_worldVisible && !InWorld && !_preparingWorld) ShowConstellation();
            foreach (var exhibit in _space.Objects)
            {
                if (exhibit.Grab != null) exhibit.Grab.enabled = !Travelling;
                if (exhibit.Pane?.Grab != null) exhibit.Pane.Grab.enabled = !Travelling;
            }
            if (InWorld && !Travelling) UpdateWorldControls();
            else if (!_worldVisible && !_busy && !_voice.IsRecording && !Travelling) UpdateConstellationControls();
            if (Time.unscaledTime - _lastSave > 3) Save();
            UpdateHud();
            UpdateLines();
            if (Debug.isDebugBuild && InWorld && !Travelling)
            {
                _frameSeconds += Time.unscaledDeltaTime; _frameCount++;
                if (Time.unscaledDeltaTime > .022f) _slowFrames++;
                if (_frameSeconds >= 15)
                {
                    Debug.Log($"[Exploration performance] {_frameCount/_frameSeconds:F1} app FPS; {_slowFrames}/{_frameCount} frames over 22ms; {_space.Objects.Count} objects.");
                    _frameSeconds = 0; _frameCount = _slowFrames = 0;
                }
            }
            else { _frameSeconds = 0; _frameCount = _slowFrames = 0; }
        }

        public TopicBall CreateBall(NodeData data, Vector3 position)
        {
            var draft = _draftBall; _draftBall = null;
            var obj = draft != null ? draft.gameObject : new GameObject("Ball: " + data.topic);
            if (draft != null) position = draft.transform.position;
            bool branch = InWorld;
            obj.transform.SetParent(branch ? _branches : _content, false);
            obj.transform.position = position;
            data.position = position;
            var ball = draft != null ? draft : obj.AddComponent<TopicBall>();
            if (draft != null) { ball.SetData(data); ball.SetDraft(false); }
            else ball.Initialize(data, _material);
            ball.Selected += Select;
            ball.EnterRequested += EnterWorld;
            ball.Grabbed += RequestPreview;
            _balls.Add(ball);
            if (branch)
                _branchPositions[ball] = _worldNode.Data.position + new Vector3(.5f + _branchPositions.Count * .25f, -.2f, 0);
            _dirty = true;
            Select(ball);
            RequestPreview(ball);
            return ball;
        }

        public TopicBall BeginDraft()
        {
            CancelDraft();
            var obj = new GameObject("Listening orb"); obj.transform.SetParent(transform, false);
            _draftBall = obj.AddComponent<TopicBall>();
            _draftBall.Initialize(new NodeData { topic = "Listening…" }, _material);
            _draftBall.SetDraft(true);
            UpdateDraft();
            _draftBall.Crystal.SetRecording(true);
            return _draftBall;
        }
        public void CancelDraft()
        {
            if (_draftBall != null) { _draftBall.gameObject.SetActive(false); Destroy(_draftBall.gameObject); }
            _draftBall = null;
        }
        public bool CancelPendingVoice()
        {
            if (!CanCancelVoice) return false;
            bool idea = _draftBall != null || _voiceAction == "create";
            _operationVersion++; _operationRequest?.Abort(); _operationRequest = null;
            _voice?.Cancel(); _operationStudy?.ClearRequest(); _operationStudy = null;
            _voiceStudy = null; _voiceStudyContext = null;
            _busy = false; _releaseBeforeVoice = true; _recordButton = false;
            CancelDraft();
            SetStatus(idea ? "New idea cancelled." : "Request cancelled.");
            return true;
        }
        private void VoiceError(string message)
        {
            if (_operationStudy != null) _space?.SetCurrent(_operationStudy);
            _operationStudy?.RequestFeedback("PLEASE TRY AGAIN", message + RetryVoiceHint, false, true);
            SetStatus(_operationStudy != null ? "Voice request needs attention. See the pane." : message);
            CancelDraft();
        }
        private void UpdateDraft()
        {
            if (_draftBall == null || Camera.main == null) return;
            var cam = Camera.main.transform;
            _draftBall.transform.position = _right != null ? _right.position + _right.forward * .2f + Vector3.up * .04f :
                cam.position + cam.forward * .55f + Vector3.down * .12f;
            bool listening = _input.CreateHeld && !_busy;
            _draftBall.Crystal.SetRecording(listening, _voice != null ? _voice.Level : 0);
            _draftBall.Data.topic = _busy ? "Writing… · Y cancel" : "Listening… · Y cancel";
        }

        private void RequestPreview(TopicBall ball)
        {
            if (ball == null || ball.Data == null || !_previewRequests.Add(ball)) return;
            StartCoroutine(LoadPreview(ball));
        }

        private void InvalidateChangedPreviews()
        {
            foreach (var ball in _balls)
            {
                string expected = "panorama-v2\n" + NodeContext.WorldPrompt(ball.Data, _balls.Select(b => b.Data));
                if (ball.Data.imageContext == expected) continue;
                ball.Data.image = ""; ball.Refresh(); _dirty = true;
            }
        }

        private IEnumerator LoadPreview(TopicBall ball)
        {
            string context = NodeContext.WorldPrompt(ball.Data, _balls.Select(b => b.Data));
            string stamp = "panorama-v2\n" + context;
            if (ball.Data.imageContext == stamp && !string.IsNullOrEmpty(ball.Data.image))
            { _previewRequests.Remove(ball); yield break; }
            ball.Data.image = ""; ball.Refresh();
            using var request = UnityWebRequest.Get(RelayConfig.BaseUrl.TrimEnd('/') + "/world/preview");
            request.timeout = 15; yield return request.SendWebRequest();
            _previewRequests.Remove(ball);
            if (ball == null || request.result != UnityWebRequest.Result.Success ||
                context != NodeContext.WorldPrompt(ball.Data, _balls.Select(b => b.Data))) yield break;
            TranscriptImage preview = null;
            try { preview = JsonUtility.FromJson<TranscriptImage>(request.downloadHandler.text); } catch (ArgumentException) { }
            if (!string.IsNullOrEmpty(preview?.image))
            {
                ball.Data.image = preview.image; ball.Data.imageContext = stamp;
                ball.Refresh(); _dirty = true;
            }
        }

        public void Select(TopicBall ball)
        {
            if (_worldVisible)
            {
                if (ball == null || !_branchPositions.ContainsKey(ball)) return;
                _branchSelection = ball;
            }
            if (Current != null) Current.SetSelected(false);
            Current = ball;
            if (Current != null) Current.SetSelected(true);
        }

        public void SubmitText(string text, bool edit)
        {
            if (_busy || (_voice != null && (_voice.IsRecording || _voice.IsStarting)) || Travelling) return;
            if (edit && Current == null) { SetStatus("Select a ball first."); return; }
            StartCoroutine(ApplyText(text, edit ? "edit" : "create", InWorld ? (edit ? _branchSelection ?? _worldNode : _worldNode) : Current));
        }

        private IEnumerator TranscribeAndApply(byte[] wav, string action, TopicBall target, StudyExhibit study, StudyRequest studyContext)
        {
            int operation = ++_operationVersion;
            _busy = true; _operationStudy = study;
            study?.RequestFeedback("TRANSCRIBING · Y CANCEL", "Turning your voice into text…");
            SetStatus("Transcribing your voice…");
            string response = null;
            yield return Post("/speech/transcribe", wav, "audio/wav", result => response = result, 120, operation);
            if (operation != _operationVersion) yield break;
            if (response == null) { FinishOperation(operation); yield break; }
            string text = null;
            try { text = JsonUtility.FromJson<Transcript>(response)?.text; } catch (ArgumentException) { }
            if (string.IsNullOrWhiteSpace(text)) { OperationError("No speech heard. Hold the button while speaking and release when finished."); FinishOperation(operation); yield break; }
            yield return ApplyText(text, action, target, operation, study, studyContext);
        }

        private IEnumerator ApplyText(string text, string action, TopicBall target, int operation = -1, StudyExhibit study = null, StudyRequest studyContext = null)
        {
            if (operation < 0) operation = ++_operationVersion;
            if (operation != _operationVersion) yield break;
            _busy = true; _voiceAction = action; _operationStudy = study;
            if (action == "steer")
            {
                if (InWorld && target == _worldNode)
                {
                    SendSteer("voice", text);
                    yield return UpdateStudy(text, _worldRequest, operation, study, studyContext);
                }
                else SetStatus("You left that world. Hold A to speak again.");
                FinishOperation(operation);
                yield break;
            }
            if (action == "edit" && target == null) { SetStatus("That ball is no longer available."); FinishOperation(operation); yield break; }
            study?.RequestFeedback("UPDATING IDEA · Y CANCEL", "You asked:\n" + text);
            SetStatus(action == "edit" ? "Updating the idea…" : "Writing your new idea…");
            // Saved world images do not belong in a text-edit request.
            var request = new NodeRequest { action = action, text = text, node = target != null
                ? new NodeData { topic = target.Data.topic, summary = target.Data.summary, worldPrompt = target.Data.worldPrompt } : null };
            var contextNode = action == "edit" ? target.Data : new NodeData { parentIds = target != null ? new[] { target.Data.id } : Array.Empty<string>() };
            request.parents = NodeContext.Parents(contextNode, _balls.Select(b => b.Data))
                .Select(n => new ParentNote { id = n.id, topic = n.topic, summary = n.summary }).ToArray();
            string response = null;
            yield return Post("/nodes/update", Encoding.UTF8.GetBytes(JsonUtility.ToJson(request)), "application/json", result => response = result, 50, operation);
            if (operation != _operationVersion) yield break;
            if (response != null)
            {
                NodeResult result = null;
                try { result = JsonUtility.FromJson<NodeResult>(response); } catch (ArgumentException) { }
                if (result == null || string.IsNullOrWhiteSpace(result.topic)) OperationError("The note was empty. Please try again.");
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
                    InvalidateChangedPreviews();
                    RequestPreview(target);
                    Save();
                    SetStatus(action == "edit" ? "Updated " + data.topic : data.topic + " is ready. Grip to grab; release to throw.");
                    if (action == "edit" && InWorld && target == _worldNode)
                    {
                        _worldContext = NodeContext.WorldPrompt(data, _balls.Select(b => b.Data));
                        data.imageContext = "panorama-v2\n" + _worldContext;
                        SendSteer("scene", _worldContext);
                        if (studyContext != null) { studyContext.topic = data.topic; studyContext.context = _worldContext; }
                        yield return UpdateStudy("Rebuild the teaching model for this edited topic.", _worldRequest, operation, study, studyContext);
                    }
                }
            }
            FinishOperation(operation);
        }

        private void FinishOperation(int operation)
        {
            if (operation != _operationVersion) return;
            _busy = false; _operationStudy = null; CancelDraft();
        }
        private void OperationError(string message)
        {
            if (_operationStudy != null) _space?.SetCurrent(_operationStudy);
            _operationStudy?.RequestFeedback("REQUEST NOT COMPLETED", message + RetryVoiceHint, false, true);
            SetStatus(_operationStudy != null ? "Request needs attention. See the pane." : message);
        }
        private string RetryVoiceHint => _voiceAction == "edit" ? " Hold B to try again." : " Hold left trigger to try again.";

        private IEnumerator Post(string endpoint, byte[] bytes, string contentType, Action<string> success, int timeout, int operation = -1)
        {
            int version = _worldRequest;
            bool scoped = _worldVisible || endpoint.StartsWith("/world/", StringComparison.Ordinal);
            using var request = new UnityWebRequest(RelayConfig.BaseUrl.TrimEnd('/') + endpoint, "POST");
            request.uploadHandler = new UploadHandlerRaw(bytes);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", contentType);
            request.timeout = timeout;
            if (endpoint == "/world/prepare") _preparationRequest = request;
            if (operation >= 0) _operationRequest = request;
            try { yield return request.SendWebRequest(); }
            finally
            {
                if (_preparationRequest == request) _preparationRequest = null;
                if (_operationRequest == request) _operationRequest = null;
            }
            if ((scoped && version != _worldRequest) || (operation >= 0 && operation != _operationVersion)) yield break;
            if (request.result == UnityWebRequest.Result.Success) success(request.downloadHandler.text);
            else
            {
                string detail = null;
                try { detail = JsonUtility.FromJson<ApiError>(request.downloadHandler.text)?.detail; } catch (ArgumentException) { }
                detail = string.IsNullOrEmpty(detail) ? "Cannot reach the laptop. Check its relay connection, then try again." : detail;
                if (operation >= 0) OperationError(detail); else SetStatus(detail);
            }
        }

        public void EnterWorld(TopicBall ball)
        {
            if (_world == null || _preparingWorld || _busy || Travelling || _voice.IsRecording || _voice.IsStarting || ball == null || (InWorld && ball == _worldNode)) return;
            string prompt = NodeContext.WorldPrompt(ball.Data, _balls.Select(b => b.Data));
            if (ball.Data.imageContext != "panorama-v2\n" + prompt)
            { ball.Data.image = ""; ball.Refresh(); }
            Vector3 origin = ball.transform.position;
            Vector3 direction = ball.LastThrowVelocity.sqrMagnitude > .01f ? ball.LastThrowVelocity.normalized : Camera.main.transform.forward;
            Vector3 fromHead = origin - Camera.main.transform.position;
            ball.Body.linearVelocity = Vector3.zero; ball.Body.angularVelocity = Vector3.zero;
            Texture preview = ball.Crystal.PreviewTexture;
            if (InWorld) { LeaveWorld(); origin = Camera.main.transform.position + fromHead; }
            _journey.Begin(origin, direction, preview);
            _worldNode = ball;
            _branchSelection = null;
            Save();
            _worldVisible = true;
            _content.gameObject.SetActive(false);
            if (_voidStars == null) _voidStars = GameObject.Find("Temporary void stars — replace with skybox");
            if (_voidStars != null) _voidStars.SetActive(false);
            _preparingWorld = true; _busy = true;
            _loadingStarted = Time.unscaledTime;
            SetStatus("Preparing your world… Y returns.");
            _worldContext = prompt;
            if (ball.Data.imageContext != "panorama-v2\n" + prompt) ball.Data.image = "";
            ball.Data.imageContext = "panorama-v2\n" + prompt;
            _dirty = true;
            StartCoroutine(PrepareAndEnter(ball, prompt, ++_worldRequest));
        }

        private IEnumerator PrepareAndEnter(TopicBall ball, string context, int version)
        {
            string response = null;
            var request = new PrepareRequest { topic = ball.Data.topic, context = context };
            yield return Post("/world/prepare", Encoding.UTF8.GetBytes(JsonUtility.ToJson(request)), "application/json", result => response = result, 170);
            if (version != _worldRequest || !_preparingWorld) yield break;
            PreparedWorld prepared = null;
            if (!string.IsNullOrEmpty(response))
            {
                try { prepared = JsonUtility.FromJson<PreparedWorld>(response); }
                catch (ArgumentException) { }
            }
            while (prepared?.projection == "geometry" && prepared.state == "preparing")
            {
                SetStatus(prepared.notice);
                yield return new WaitForSeconds(3);
                if (version != _worldRequest) yield break;
                if (string.IsNullOrEmpty(prepared.cacheKey) || !System.Text.RegularExpressions.Regex.IsMatch(prepared.cacheKey, @"^[a-f0-9]{64}$")) break;
                using var poll = UnityWebRequest.Get(RelayConfig.BaseUrl.TrimEnd('/') + "/world/jobs/" + prepared.cacheKey);
                poll.timeout = 30; _preparationRequest = poll;
                yield return poll.SendWebRequest();
                if (_preparationRequest == poll) _preparationRequest = null;
                if (version != _worldRequest) yield break;
                if (poll.result != UnityWebRequest.Result.Success) { prepared = null; break; }
                try { prepared = JsonUtility.FromJson<PreparedWorld>(poll.downloadHandler.text); }
                catch (ArgumentException) { prepared = null; }
            }
            if (prepared == null || prepared.state == "error")
            {
                string error = prepared?.notice ?? "World preparation could not finish. Re-enter to resume.";
                ShowConstellation(); SetStatus(error); yield break;
            }
            if (prepared.projection == "geometry")
            {
                string error = null;
                _world.GetComponent<DomeView>().SetVisible(false);
                yield return _environment.Load(prepared.environment, RelayConfig.BaseUrl, SetStatus, failure => error = failure);
                if (version != _worldRequest) yield break;
                if (error != null) { ShowConstellation(); SetStatus(error); yield break; }
                _navigation.Enter(_environment);
                _space.Open(ball.Data.id, prepared.plan);
                _preparingWorld = false; _busy = false;
                _journey.MarkReady();
                SetStatus("Your exploration space is ready. Grip to inspect; hold left trigger to ask or create an object.");
                yield break;
            }
            _space.Open(ball.Data.id, prepared?.plan);
            string image = !string.IsNullOrEmpty(ball.Data.image) ? ball.Data.image : prepared?.image ?? "";
            if (!string.IsNullOrEmpty(image)) { ball.Data.image = image; ball.Refresh(); _dirty = true; }
            _navigation.Enter();
            _world.Enter(ball.Data.id, ball.Data.topic, context, image);
            _preparingWorld = false; _busy = false;
            SetStatus(string.IsNullOrEmpty(prepared?.notice) ? "Surroundings ready. Connecting the live world…" : prepared.notice);
        }

        private StudyRequest CaptureStudy(StudyExhibit target)
        {
            return new StudyRequest { topic = _worldNode.Data.topic, context = _worldContext,
                current = target?.Data, focused = target?.FocusedName ?? "",
                step = target?.StepIndex ?? -1, parameter = target?.Parameter ?? 0,
                objects = _space.Objects.Select(item => item.Data.title).ToArray() };
        }
        private IEnumerator UpdateStudy(string instruction, int version, int operation, StudyExhibit target = null, StudyRequest request = null)
        {
            if (operation != _operationVersion) yield break;
            if (target == null) target = _study;
            request ??= CaptureStudy(target);
            request.instruction = instruction;
            _operationStudy = target;
            if (target != null)
            {
                _space.SetCurrent(target);
                target.RequestFeedback("THINKING · Y CANCEL", "You asked:\n" + instruction);
            }
            SetStatus("Thinking… You can keep exploring.");
            string response = null;
            yield return Post("/world/explore", Encoding.UTF8.GetBytes(JsonUtility.ToJson(request)), "application/json", result => response = result, 100, operation);
            if (operation != _operationVersion || version != _worldRequest || !InWorld || response == null) yield break;
            StudyReply reply = null;
            try { reply = JsonUtility.FromJson<StudyReply>(response); } catch (ArgumentException) { }
            if (!_space.ApplyReply(reply, target))
            { OperationError("That response could not be applied. Your existing objects are kept."); yield break; }
            SetStatus(reply.action == "add" ? "Object added. See its pane." : reply.action == "revise" ? "Object updated. See its pane." : "Answer ready on the pane.");
            _space.Save();
        }

        public void LeaveWorld()
        {
            if (_world == null) return;
            // Leaving while holding A cancels that unfinished recording as well as its orb.
            CancelPendingVoice(); _voice?.Cancel();
            _worldRequest++; _preparingWorld = false; _busy = false;
            _operationVersion++;
            _preparationRequest?.Abort();
            var texture = _world.GetComponent<WorldStream>().Texture;
            if (!_environment.IsVisible && _worldNode != null && WorldReady && texture != null && texture.width > 2)
            { _worldNode.Data.image = _world.GetComponent<DomeView>().CapturePanorama(); _dirty = true; }
            _world.Exit();
            ShowConstellation();
            SetStatus("Back in your constellation.");
            Save();
        }

        private void ShowConstellation()
        {
            if (_worldVisible)
            {
                _worldRequest++; _operationVersion++;
                _preparingWorld = false; _busy = false;
                _preparationRequest?.Abort();
                _operationRequest?.Abort(); _operationRequest = null;
                _voice?.Cancel(); _releaseBeforeVoice = true;
            }
            _navigation.Exit();
            _space.Close();
            _stickStudy = null;
            _operationStudy = _voiceStudy = null; _voiceStudyContext = null;
            CancelDraft();
            _environment.Clear();
            _journey.Cancel();
            foreach (var branch in _branchPositions)
            {
                if (branch.Key == null) continue;
                branch.Key.transform.SetParent(_content, true);
                branch.Key.transform.position = branch.Value;
                branch.Key.Body.linearVelocity = Vector3.zero; branch.Key.Body.angularVelocity = Vector3.zero;
            }
            _branchPositions.Clear(); _branchSelection = null;
            if (_voidStars != null) _voidStars.SetActive(true);
            _worldVisible = false; _pendingInstruction = null;
            _content.gameObject.SetActive(true);
        }

        private void UpdateWorldControls()
        {
            if (_right != null) _space.Focus(new Ray(_right.position, _right.forward));
            Vector2 look = _input.Look;
            if (look.sqrMagnitude < .025f) _stickStudy = null;
            if (_stickStudy == null && look.sqrMagnitude >= .025f && _study != null && (_study.IsHeld || _study.IsFocused))
            { _stickStudy = _study; _space.SetCurrent(_stickStudy); }
            if (_stickStudy != null) _space.SetCurrent(_stickStudy, false);
            bool inspecting = _study != null && (_stickStudy != null || _study.IsHeld || _study.IsFocused);
            if (inspecting)
            {
                _study.Zoom(look.y);
                _study.AdjustParameter(look.x);
                look = Vector2.zero;
            }
            // Voice and network requests must not freeze local navigation.
            _navigation.Tick(_input.Move, look, WorldReady, SendSteer);
            if (_input.EnterDown && _branchSelection != null) { EnterWorld(_branchSelection); return; }
            if (_input.EnterDown && _study != null) { _space.SetCurrent(_study); _study.Recenter(); SetStatus("Object and pane brought in front of you."); }
            if (_input.PauseDown && _study != null) { _study.ToggleAnimation(); SetStatus("Object animation toggled."); }
            if (_input.ViewDown && _study != null) { _space.SetCurrent(_study); _study.ResetExploration(); SetStatus("Exploration reset; your objects are kept."); }
            if (!_input.EventDown) return;
            if (inspecting)
            { _space.SetCurrent(_study); _study.NextStep(); SetStatus("Discovery updated on the pane."); _space.Save(); }
            else SetStatus("Point at an object: trigger explores; right stick zooms or adjusts its experiment. Hold left trigger to wish for another object.");
        }

        private void SendSteer(string kind, string text)
        {
            if (_environment.IsVisible)
            {
                if (kind == "voice" || kind == "scene") SetStatus("Updating the learning object…");
                return;
            }
            _lastSteer = Time.unscaledTime; _pendingSeq = uint.MaxValue; _pendingInstruction = text;
            SetStatus("Sending your request…");
            _world.Steer(kind, text);
        }
        private void PromptReceived(string text, uint sequence) { if (_pendingInstruction != null) _pendingSeq = sequence; }
        private void FrameReceived(uint sequence)
        {
            if (_world.IsEntered && _journey.IsActive) _journey.MarkReady();
            if (_pendingInstruction != null && sequence >= _pendingSeq)
            { SetStatus("Instruction sent. The live world is responding."); _pendingInstruction = null; }
        }
        private void WorldStatus(StatusMessage status)
        {
            if (_environment != null && _environment.IsVisible) return;
            if (status.state == "error") SetStatus(status.detail);
            else if (status.state == "streaming") SetStatus("Look all around. A: new idea · Left trigger: ask · Y: map.");
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
            _dirty = true; InvalidateChangedPreviews(); RebuildLines(); Save();
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
            string controls = _worldVisible ? "A: new idea · B: edit idea · Left trigger: ask / create\nGrip: move object / pane · Right stick: zoom / experiment\nLeft stick: walk · Y: map" : "A: new topic · B: edit selected\nGrip: grab · Throw: travel · X: enter";
            if (CanCancelVoice) controls = "Y: cancel this request\n" + (_voice.IsRecording || _voice.IsStarting ? "Release the voice button to send.\n" : "") + "You can still move and look around.";
            string progress = Travelling ? "\n" + Mathf.FloorToInt(_journey.Elapsed) + "s elapsed · Y returns" : "";
            _hud.text = title + "\n\n" + (_voice.IsRecording ? "● Listening " + new string('|', Mathf.Clamp(Mathf.RoundToInt(_voice.Level * 150), 1, 12)) : Status + progress)
                + "\n\n" + controls;
        }
        public void SetStatus(string text) { Status = text; }
        public void Save()
        {
            if (_savePath == null) return;
            foreach (var ball in _balls)
            {
                Vector3 position = _branchPositions.TryGetValue(ball, out var mapPosition) ? mapPosition : ball.transform.position;
                if (Vector3.Distance(ball.Data.position, position) > .002f)
                { ball.Data.position = position; _dirty = true; }
            }
            _lastSave = Time.unscaledTime;
            if (!_dirty) return;
            try
            {
                NodeStore.Save(_savePath, new ConstellationData { nodes = _balls.Select(b => b.Data).ToArray(), edges = _edges.ToArray() });
                _dirty = false;
            }
            catch (Exception exception) { SetStatus("Could not save your constellation: " + exception.Message); }
        }
        private void OnApplicationPause(bool paused) { if (paused) { if (_world != null && (InWorld || _preparingWorld)) LeaveWorld(); Save(); } }
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
            if (CanCancelVoice && GUILayout.Button("Cancel request (Esc / Y)")) CancelPendingVoice();
            GUILayout.EndArea();
        }

        [Serializable] private class Transcript { public string text; }
        [Serializable] private class TranscriptImage { public string image; }
        [Serializable] private class ApiError { public string detail; }
        [Serializable] private class ParentNote { public string id; public string topic; public string summary; }
        [Serializable] private class NodeRequest { public string action; public string text; public NodeData node; public ParentNote[] parents; }
        [Serializable] private class NodeResult { public string topic; public string summary; public string worldPrompt; public string[] suggestions; public string mode; }
        [Serializable] private class PrepareRequest { public string topic; public string context; }
        [Serializable] private class StudyRequest { public string topic; public string context; public string instruction; public StudyModelData current; public string focused; public string[] objects; public int step; public float parameter; }
    }
}
