using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
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
        public TopicBall WorldNode => _worldNode;
        public IReadOnlyList<TopicBall> Balls => _balls;
        public string Status { get; private set; } = "Hold A and say a topic. Let go to create it.";
        public bool Busy => _busy || PendingDeletion != null;
        public bool CanCancelVoice => (_busy && !_preparingWorld) || _draftBall != null || (_voice != null && (_voice.IsRecording || _voice.IsStarting));
        private readonly List<TopicBall> _balls = new List<TopicBall>();
        private readonly List<NodeEdge> _edges = new List<NodeEdge>();
        private readonly List<LineRenderer> _lines = new List<LineRenderer>();
        private readonly Dictionary<TopicBall, Vector3> _branchPositions = new Dictionary<TopicBall, Vector3>();
        private readonly HashSet<TopicBall> _previewRequests = new HashSet<TopicBall>();
        public int LinkCount => _edges.Count;
        public int TopicsCreated { get; private set; }
        public int LinkChanges { get; private set; }
        public bool IsPulling => _pulling;
        public bool IsHolding => AnyBallHeld();
        public bool IsListening => _voice != null && (_voice.IsRecording || _voice.IsStarting);
        public bool WorldActive => _worldVisible || InWorld || _preparingWorld || Travelling;
        public bool IsTravelling => Travelling;
        public bool ShowControlsCard { get; set; } = true;
        public string ConstellationId { get; private set; }
        public string ConstellationName { get; private set; }
        private readonly List<Vector3> _linkPositions = new List<Vector3>();
        private readonly ControllerActions _input = new ControllerActions();
        private readonly WorldNavigation _navigation = new WorldNavigation();
        private StudyWorkspace _space;
        private StudyExhibit _study => _space?.Current;
        private TopicBall _draftBall;
        private StudyExhibit _stickStudy;
        private GeneratedEnvironment _environment;
        private WorldJourney _journey;
        private TopicBall _branchSelection, _departureNode;
        private string _departureContext;
        private GeneratedEnvironment _arrivingEnvironment;
        private string _preparationError;
        // X follows the orb the user is actually pointing at or holding, not a stale hover.
        private TopicBall LinkedTravelTarget => _branchSelection != null && _branchPositions.ContainsKey(_branchSelection) &&
            _branchSelection.gameObject.activeInHierarchy && (_branchSelection.Grab.isSelected || _branchSelection.Grab.isHovered)
                ? _branchSelection : null;
        private float _loadingStarted;
        private bool LoadingTimedOut => (_preparingWorld || Travelling) && Time.unscaledTime - _loadingStarted > 240;
        public bool InWorld => (_environment != null && _environment.IsVisible) || (_world != null && _world.IsEntered);
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
        private WorldControls _worldControls;
        private Transform _left, _right, _content, _branches;
        private Material _material, _lineMaterial;
        private ControlsCard _card, _deleteCard;
        public TopicBall PendingDeletion { get; private set; }
        private ControllerHints _hints;
        private Onboarding _onboarding;
        private TopicBall _voiceTarget, _worldNode, _lineStart, _lineCandidate;
        private LineRenderer _linkPreview;
        private string _voiceAction;
        private string _savePath;
        private bool _busy, _recordButton, _trigger, _worldVisible, _pulling, _dirty, _lineTextured, _tapLinkArmed;
        private Vector3 _pullMidpoint;
        private float _pullDistance, _lastSave, _lastSteer, _recordStart;
        private uint _pendingSeq;
        private string _pendingInstruction;
        private float _frameSeconds;
        private int _frameCount, _slowFrames;
        private bool _cutting;
        private Vector3 _cutOrigin;
        private Vector3 _cutDirection;
        private const float CutRange = 4f;
        private const float CardPointerRange = 6f;
        private const string TutorialHint = "Hold the left stick to skip the tutorial";
        private const string WorldHint = "Left trigger: ask / create · A: new idea\nGrip: object / pane · B: edit · Y: map";
        private const string VoidHint = "Point at an orb + Y: delete…\nThrow: place · X: enter · A: new idea";

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
            _branches = new GameObject("Ideas created inside this world").transform;
            _branches.SetParent(transform, false);
            gameObject.AddComponent<SuggestedStars>().Initialize(this, _content);
            _material = new Material(Shader.Find("Zap/Solid"));
            _material.SetColor("_BaseColor", ZapTheme.Current.nodeColor);
            var lineImage = ZapTheme.Current.lineImage;
            _lineTextured = lineImage != null && ZapArt.Material != null;
            _lineMaterial = _lineTextured ? new Material(ZapArt.Material) : new Material(Shader.Find("Zap/Solid"));
            if (_lineTextured) _lineMaterial.SetTexture("_MainTex", lineImage);
            _lineMaterial.SetColor("_BaseColor", ZapTheme.Current.lineColor);
            var preview = new GameObject("Link preview");
            preview.transform.SetParent(_content, false);
            _linkPreview = preview.AddComponent<LineRenderer>();
            _linkPreview.sharedMaterial = _lineMaterial;
            _linkPreview.positionCount = 2;
            // Same as the connection lines: the line image repeats instead of stretching.
            if (_lineTextured) _linkPreview.textureMode = LineTextureMode.Tile;
            _linkPreview.enabled = false;
            _voice = gameObject.AddComponent<VoiceRecorder>();
            _space = gameObject.AddComponent<StudyWorkspace>();
            _environment = gameObject.AddComponent<GeneratedEnvironment>();
            _journey = gameObject.AddComponent<WorldJourney>();
            _voice.Error += VoiceError;
            var entry = ConstellationLibrary.EnsureDefault();
            LoadConstellation(entry);
            ConstellationLibrary.LastOpenedId = entry.id;
            while ((_world = FindAnyObjectByType<WorldSession>()) == null) yield return null;
            _worldControls = _world.GetComponent<WorldControls>();
            if (_worldControls != null) _worldControls.InputOwner = this;
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
            // A one-shot development launch lets a prepared demo be checked on a
            // connected headset without synthesizing XR controller input.
            string launchId = null;
            if (Debug.isDebugBuild || Application.isEditor)
            {
                string launchPath = Path.Combine(Application.persistentDataPath,"zap-enter-node.txt");
                try
                {
                    if (File.Exists(launchPath)) { launchId = File.ReadAllText(launchPath).Trim(); File.Delete(launchPath); }
                }
                catch (IOException) { Debug.LogWarning("Could not read the requested demo entry."); }
            }
            if (!string.IsNullOrEmpty(launchId))
            {
                while (IntroSplash.Playing) yield return null;
                yield return new WaitForSecondsRealtime(.5f);
                var launch = _balls.FirstOrDefault(ball => ball.Data.id == launchId);
                if (launch != null) EnterWorld(launch);
            }
        }

        private void Update()
        {
            if (_voice == null || _world == null) return;
            // The start-up intro covers the whole view. Nothing is listened to or changed until it has gone.
            // Input is still read so a button held through the intro does not count as a new press afterwards.
            if (IntroSplash.Playing) { _input.Poll(); return; }
            FindControllers();
            _input.Poll();
            UpdateCardPointers();
            if (PendingDeletion != null)
            {
                if (_input.ExitDown) CancelDeletion();
                else if (_input.EnterDown) ConfirmDeletion();
                _releaseBeforeVoice = true;
                _recordButton = false;
                return;
            }
            bool voiceHeld = _input.CreateHeld || _input.EditHeld || _input.AskHeld;
            // Cancel wins even when Y and the voice-button release arrive in the same frame.
            bool cancelled = _input.ExitDown && CancelPendingVoice();
            if (_input.ExitDown && _preparingWorld && _departureNode != null)
            { RecoverJourney("Travel cancelled. Your new idea is saved in the map."); cancelled = true; }
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
            if (_input.ExitDown && !cancelled && !WorldActive && !CanCancelVoice)
            { DeleteBall(Current); if (PendingDeletion != null) return; }
            if (_input.ExitDown && !cancelled && (InWorld || _preparingWorld || Travelling)) LeaveWorld();
            if (_input.EnterDown && !InWorld && !_preparingWorld && Current != null && !_busy) EnterWorld(Current);
            if (LoadingTimedOut)
            { RecoverJourney("This world is still preparing on the laptop. Try entering the saved idea again to check its progress."); }
            if (_worldVisible && !InWorld && !_preparingWorld) ShowConstellation();
            foreach (var exhibit in _space.Objects)
            {
                if (exhibit.Grab != null) exhibit.Grab.enabled = !Travelling;
                if (exhibit.Pane?.Grab != null) exhibit.Pane.Grab.enabled = !Travelling;
            }
            if (InWorld && !Travelling) UpdateWorldControls();
            else if (!_worldVisible && !_busy && !_voice.IsRecording && !Travelling) UpdateConstellationControls();
            else
            {
                ClearLinkDrag();
                _trigger = false;
                _cutting = false;
                _tapLinkArmed = false;
            }
            if (Time.unscaledTime - _lastSave > 3) Save();
            HomeFrame.Hold = WorldActive || _pulling || _cutting || AnyBallHeld() || (_card != null && _card.IsDragging);
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
            TopicsCreated++;
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
            ball.EnterRequested += HandleOrbThrow;
            ball.Grabbed += RequestPreview;
            ball.Tossed += RemoveFromModel;
            _balls.Add(ball);
            if (branch)
            {
                _branchPositions[ball] = _worldNode.Data.position + new Vector3(.5f + _branchPositions.Count * .25f, -.2f, 0);
                ball.SetWorldTravelHint(true);
            }
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
            using var request = UnityWebRequest.Get(RelayConfig.BaseUrl.TrimEnd('/') + "/world/preview?nodeId=" + UnityWebRequest.EscapeURL(ball.Data.id));
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
            if (Busy || (_voice != null && (_voice.IsRecording || _voice.IsStarting)) || Travelling) return;
            if (edit && Current == null) { SetStatus("Select a ball first."); return; }
            StartCoroutine(ApplyText(text, edit ? "edit" : "create", InWorld ? (edit ? _branchSelection ?? _worldNode : _worldNode) : Current));
        }

        public void AskText(string text)
        {
            if (_busy || IsListening || Travelling || !InWorld || !WorldReady || string.IsNullOrWhiteSpace(text)) return;
            var study = _study;
            StartCoroutine(ApplyText(text, "steer", _worldNode, study: study, studyContext: study != null ? CaptureStudy(study) : null));
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

        private IEnumerator ApplyText(string text, string action, TopicBall target, int operation = -1, StudyExhibit study = null, StudyRequest studyContext = null, Vector3? position = null)
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
                if (action == "edit" && !_balls.Contains(target))
                {
                    SetStatus("That topic is no longer available.");
                    FinishOperation(operation);
                    yield break;
                }
                NodeResult result = null;
                try { result = JsonUtility.FromJson<NodeResult>(response); } catch (ArgumentException) { }
                if (result == null || string.IsNullOrWhiteSpace(result.topic)) OperationError("The note was empty. Please try again.");
                else
                {
                    var parent = target;
                    if (action != "edit" && parent != null && !_balls.Contains(parent)) parent = null;
                    var data = action == "edit" ? target.Data : new NodeData
                    {
                        parentIds = parent != null ? contextNode.parentIds : Array.Empty<string>()
                    };
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
                    InvalidateChangedPreviews();
                    RequestPreview(target);
                    Save();
                    SetStatus(action == "edit" ? "Updated " + data.topic : data.topic + (ZapPlatform.IsDesktop ? " is ready. Drag to move; double-click to enter." : InWorld ? " is ready. Grip and throw to open its world." : " is saved. Throw to place; point + X to open its world."));
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

        public void KeepSuggestion(TopicBall parent, string topic, Vector3 position)
        {
            if (_busy || IsListening || WorldActive || parent == null || !_balls.Contains(parent) || string.IsNullOrWhiteSpace(topic)) return;
            StartCoroutine(ApplyText(topic, "create", parent, position: position));
        }

        public void Open(string id)
        {
            if (Busy || IsListening || WorldActive) return;
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
            if (Busy || IsListening || WorldActive) return;
            if (!TrySave()) return;
            var entry = ConstellationLibrary.Create(name);
            Open(entry.id);
        }

        // Every user-facing deletion first opens a confirmation for this exact node.
        public void DeleteBall(TopicBall ball)
        {
            if (_busy || IsListening || WorldActive || ball == null || !_balls.Contains(ball)) return;
            PendingDeletion = ball;
            ClearLinkDrag();
            _pulling = _cutting = _tapLinkArmed = false;
            if (_deleteCard == null) _deleteCard = ControlsCard.Create(transform);
            _deleteCard.Visible = true;
            _deleteCard.Show("DELETE IDEA?", ball.Data.topic,
                "Remove this node and its links? Connected worlds are kept. This cannot be undone.",
                "X: delete this node     Y: keep it", false, 0, false, true);
            if (_card != null) _card.Visible = false;
        }

        public void CancelDeletion()
        {
            PendingDeletion = null;
            if (_deleteCard != null) _deleteCard.Visible = false;
            _releaseBeforeVoice = true;
            SetStatus("Idea kept.");
        }

        public void ConfirmDeletion()
        {
            var ball = PendingDeletion;
            CancelDeletion();
            if (_busy || IsListening || WorldActive || ball == null || !_balls.Contains(ball)) return;
            RemoveFromModel(ball);
            ball.gameObject.SetActive(false);
            Destroy(ball.gameObject);
        }

        private void RemoveFromModel(TopicBall ball)
        {
            if (ball == null || !_balls.Contains(ball)) return;
            if (_lineStart == ball) ClearLinkDrag();
            else if (_lineCandidate == ball) SetLinkCandidate(null);
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
            RebuildLines();
            _dirty = true;
            Save();
            SetStatus("Deleted " + topic);
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
                if (endpoint == "/world/prepare") _preparationError = detail;
                if (operation >= 0) OperationError(detail); else SetStatus(detail);
            }
        }

        private void HandleOrbThrow(TopicBall ball)
        {
            if (InWorld) { EnterWorld(ball, true); return; }
            if (WorldActive || PendingDeletion != null || ball == null) return;
            // Home throws arrange ideas; only an explicit entry starts generation here.
            ball.Release(Vector3.zero);
            Select(ball); _dirty = true; Save();
            SetStatus("Idea placed. Explore its suggestions, or point + X to open its world.");
        }

        public void EnterWorld(TopicBall ball, bool thrown = false)
        {
            ClearLinkDrag();
            _trigger = false;
            _cutting = false;
            if (PendingDeletion != null || _world == null || _preparingWorld || _busy || Travelling || _voice.IsRecording || _voice.IsStarting || ball == null || ball.IsTossed || (InWorld && ball == _worldNode)) return;
            string prompt = NodeContext.WorldPrompt(ball.Data, _balls.Select(b => b.Data));
            if (ball.Data.imageContext != "panorama-v2\n" + prompt)
            { ball.Data.image = ""; ball.Refresh(); }
            Vector3 origin = ball.transform.position;
            Vector3 direction = ball.LastThrowVelocity.sqrMagnitude > .01f ? ball.LastThrowVelocity.normalized : Camera.main.transform.forward;
            ball.Body.linearVelocity = Vector3.zero; ball.Body.angularVelocity = Vector3.zero;
            Texture preview = ball.Crystal.PreviewTexture;
            // Keep the departure world and tracked pose alive until the destination is ready.
            // Calling LeaveWorld here used to expose the map and destroy the recovery path.
            _departureNode = InWorld ? _worldNode : null;
            _departureContext = _worldContext;
            _preparationError = null;
            _space.Save();
            foreach (var exhibit in _space.Objects) exhibit.gameObject.SetActive(false);
            _branches.gameObject.SetActive(false);
            _journey.Begin(origin, direction, preview, thrown ? WorldJourney.ThrowShatterDistance : 0,
                ball.LastThrowVelocity.magnitude);
            _worldNode = ball;
            _branchSelection = null;
            Save();
            _worldVisible = true;
            HomeFrame.Hold = true;
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

        private bool AcceptPreparationHeartbeat(PreparedWorld prepared, int version)
        {
            if (!_preparingWorld || version != _worldRequest || prepared?.projection != "geometry" || prepared.state != "preparing" ||
                string.IsNullOrEmpty(prepared.cacheKey) || !System.Text.RegularExpressions.Regex.IsMatch(prepared.cacheKey, @"^[a-f0-9]{64}$")) return false;
            // A healthy generation/export may take much longer than four minutes. Time out silence, not an active job.
            _loadingStarted = Time.unscaledTime;
            return true;
        }

        private IEnumerator PrepareAndEnter(TopicBall ball, string context, int version)
        {
            string response = null;
            var request = new PrepareRequest { nodeId = ball.Data.id, topic = ball.Data.topic, context = context };
            yield return Post("/world/prepare", Encoding.UTF8.GetBytes(JsonUtility.ToJson(request)), "application/json", result => response = result, 170);
            if (version != _worldRequest || !_preparingWorld) yield break;
            PreparedWorld prepared = null;
            if (!string.IsNullOrEmpty(response))
            {
                try { prepared = JsonUtility.FromJson<PreparedWorld>(response); }
                catch (ArgumentException) { }
            }
            while (AcceptPreparationHeartbeat(prepared, version))
            {
                SetStatus(prepared.notice);
                yield return new WaitForSeconds(3);
                if (version != _worldRequest) yield break;
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
                string error = prepared?.notice ?? _preparationError ?? "World preparation could not finish. Try entering the saved idea again.";
                RecoverJourney(error); yield break;
            }
            if (prepared.projection == "geometry")
            {
                // Finish the orb's distance-based flight and cover before revealing
                // any arriving geometry, including an immediately cached destination.
                while (!_journey.HasCover && version == _worldRequest) yield return null;
                if (version != _worldRequest) yield break;
                string error = null;
                // Stage into a separate component: a bad download must not destroy the current room.
                var arriving = new GameObject("Arriving world").AddComponent<GeneratedEnvironment>();
                arriving.transform.SetParent(transform, false);
                _arrivingEnvironment = arriving;
                yield return arriving.Load(prepared.environment, RelayConfig.BaseUrl, SetStatus, failure => error = failure, prepared.HasArtifact);
                if (version != _worldRequest) yield break;
                if (error != null) { RecoverJourney(error); yield break; }
                // A cached destination can arrive immediately; allow the shatter and cover to finish.
                while (_journey.Elapsed < 1.0f && version == _worldRequest) yield return null;
                if (version != _worldRequest) yield break;
                _world.Exit();
                _environment.Clear();
                if (_environment.gameObject == gameObject) Destroy(_environment); else Destroy(_environment.gameObject);
                _environment = arriving; _arrivingEnvironment = null;
                _navigation.Enter(_environment);
                CommitJourney();
                _space.Open(ball.Data.id, prepared.HasArtifact ? prepared.plan : null, prepared.pane);
                ShowLinkedWorlds();
                _preparingWorld = false; _busy = false;
                _journey.MarkReady();
                SetStatus("Your exploration space is ready. Grip to inspect; hold left trigger to ask or create an object.");
                yield break;
            }
            _environment.Clear();
            CommitJourney();
            _space.Open(ball.Data.id, prepared?.HasArtifact == true ? prepared.plan : null, prepared?.pane);
            ShowLinkedWorlds();
            string image = !string.IsNullOrEmpty(ball.Data.image) ? ball.Data.image : prepared?.image ?? "";
            if (!string.IsNullOrEmpty(image)) { ball.Data.image = image; ball.Refresh(); _dirty = true; }
            _navigation.Enter();
            _world.Enter(ball.Data.id, ball.Data.topic, context, image);
            _preparingWorld = false; _busy = false;
            SetStatus(string.IsNullOrEmpty(prepared?.notice) ? "Surroundings ready. Connecting the live world…" : prepared.notice);
        }

        private void CommitJourney()
        {
            RestoreBranchesToMap();
            _departureNode = null; _departureContext = null;
            _branches.gameObject.SetActive(true);
        }

        private void RecoverJourney(string error)
        {
            _worldRequest++; _preparingWorld = false; _busy = false;
            _preparationRequest?.Abort(); _preparationRequest = null;
            if (_arrivingEnvironment != null)
            { _arrivingEnvironment.Clear(); Destroy(_arrivingEnvironment.gameObject); _arrivingEnvironment = null; }
            if (_departureNode != null && InWorld)
            {
                _worldNode = _departureNode; _worldContext = _departureContext;
                _departureNode = null; _departureContext = null;
                _branches.gameObject.SetActive(true);
                foreach (var exhibit in _space.Objects) exhibit.gameObject.SetActive(true);
                _journey.MarkReady(); // Fade back into the same room, never the map.
                _study?.RequestFeedback("WORLD NOT OPENED", error + " Your idea is saved in the map.", false, true);
            }
            else ShowConstellation();
            SetStatus(error);
            Save();
        }

        private void RestoreBranchesToMap()
        {
            foreach (var branch in _branchPositions)
            {
                if (branch.Key == null) continue;
                branch.Key.transform.SetParent(_content, true);
                branch.Key.transform.position = branch.Value;
                branch.Key.Body.linearVelocity = Vector3.zero; branch.Key.Body.angularVelocity = Vector3.zero;
                branch.Key.SetWorldTravelHint(false);
            }
            _branchPositions.Clear(); _branchSelection = null;
        }

        private void ShowLinkedWorlds()
        {
            if (_worldNode == null || Camera.main == null) return;
            var head = Camera.main.transform;
            var forward = Vector3.ProjectOnPlane(head.forward,Vector3.up).normalized;
            if (forward.sqrMagnitude < .1f) forward = Vector3.forward;
            var right = Vector3.Cross(Vector3.up,forward);
            var linked = NodeContext.Neighbours(_worldNode.Data,_balls.Select(b => b.Data));
            int index = 0;
            foreach (var node in linked.Take(6))
            {
                var ball = _balls.FirstOrDefault(item => item.Data.id == node.id);
                if (ball == null || _branchPositions.ContainsKey(ball)) continue;
                _branchPositions[ball] = ball.Data.position;
                ball.transform.SetParent(_branches,true);
                ball.transform.position = head.position + forward * (1.4f + index/3*.35f) - right * 1.05f + Vector3.down * (.1f + index%3*.3f);
                ball.Body.linearVelocity = Vector3.zero; ball.Body.angularVelocity = Vector3.zero;
                ball.SetSelected(false); ball.SetWorldTravelHint(true); index++;
            }
        }

        private StudyRequest CaptureStudy(StudyExhibit target)
        {
            return new StudyRequest { topic = _worldNode.Data.topic, context = _worldContext,
                current = target != null && target.HasModel ? target.Data : null, focused = target?.FocusedName ?? "",
                step = target?.StepIndex ?? -1, parameter = target?.Parameter ?? 0,
                objectActions = true, objectStates = _space.CaptureObjects(), selectedId = target != null && target.HasModel ? target.ObjectId : "",
                objects = _space.Objects.Where(item => item.HasModel).Select(item => item.Data.title).ToArray() };
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
            SetStatus(reply.action == "apply" ? "Objects updated. See the pane." : reply.action == "add" ? "Object added. See its pane." : reply.action == "revise" ? "Object updated. See its pane." : "Answer ready on the pane.");
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
            RestoreBranchesToMap();
            _branches.gameObject.SetActive(true);
            _departureNode = null; _departureContext = null;
            if (_arrivingEnvironment != null)
            { _arrivingEnvironment.Clear(); Destroy(_arrivingEnvironment.gameObject); _arrivingEnvironment = null; }
            if (_voidStars != null) _voidStars.SetActive(true);
            _worldVisible = false; _pendingInstruction = null;
            HomeFrame.Hold = false;
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
            var destination = LinkedTravelTarget;
            if (_input.EnterDown && destination != null) { EnterWorld(destination); return; }
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
            if (!_preparingWorld && _world.IsEntered && _journey.IsActive) _journey.MarkReady();
            if (_pendingInstruction != null && sequence >= _pendingSeq)
            { SetStatus("Instruction sent. The live world is responding."); _pendingInstruction = null; }
        }
        private void WorldStatus(StatusMessage status)
        {
            if (_preparingWorld || (_environment != null && _environment.IsVisible)) return;
            if (status.state == "error") SetStatus(status.detail);
            else if (status.state == "streaming") SetStatus("Look all around. A: new idea · Left trigger: ask · Y: map.");
            else if (!string.IsNullOrEmpty(status.detail)) SetStatus(status.detail);
        }

        private void UpdateConstellationControls()
        {
            if (_lineStart != null) _tapLinkArmed = false;
            if (_input.RightTrigger) RefreshLinkPositions();
            if (_input.RightTrigger && !_trigger)
            {
                _lineStart = _right == null ? null : BallAt(LinkAim.Nearest(_linkPositions, _right.position, _right.forward, 6f, 6f, -1));
                _cutting = _lineStart == null && _right != null;
                if (_cutting) { _cutOrigin = _right.position; _cutDirection = _right.forward; }
            }
            if (_lineStart != null && _input.RightTrigger && _right != null)
            {
                int startIndex = _balls.IndexOf(_lineStart);
                SetLinkCandidate(BallAt(LinkAim.Nearest(_linkPositions, _right.position, _right.forward, 14f, 6f, startIndex)));
                float distance = Vector3.Distance(_right.position, _lineStart.transform.position);
                _linkPreview.SetPosition(0, _lineStart.transform.position);
                _linkPreview.SetPosition(1, _lineCandidate != null ? _lineCandidate.transform.position : _right.position + _right.forward * distance);
                _linkPreview.enabled = true;
            }
            else if (_cutting && _input.RightTrigger && _right != null)
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
                if (_lineStart != null && _lineCandidate != null) ToggleLink(_lineStart, _lineCandidate);
                ClearLinkDrag();
            }
            _trigger = _input.RightTrigger;
            UpdateTapLink();
            bool canPull = _input.LeftGrip && _input.RightGrip && _left != null && _right != null && !AnyBallHeld();
            if (canPull)
            {
                var mid = (_left.position + _right.position) * .5f;
                float distance = Vector3.Distance(_left.position, _right.position);
                if (_pulling && distance > .08f && _pullDistance > .08f)
                {
                    float scale = Mathf.Clamp(distance / _pullDistance, .9f, 1.1f);
                    foreach (var ball in _balls)
                    {
                        if (ball.IsTossed) continue;
                        ball.transform.position = mid + (ball.transform.position - _pullMidpoint) * scale;
                        ball.Body.linearVelocity = Vector3.zero;
                    }
                }
                _pullMidpoint = mid; _pullDistance = distance;
            }
            _pulling = canPull;
        }

        private void RefreshLinkPositions()
        {
            _linkPositions.Clear();
            for (int i = 0; i < _balls.Count; i++)
                _linkPositions.Add(_balls[i] == null || _balls[i].IsTossed ? Vector3.one * 9999f : _balls[i].transform.position);
        }

        private TopicBall BallAt(int index)
        {
            if (index < 0 || index >= _balls.Count) return null;
            var ball = _balls[index];
            return ball != null && !ball.IsTossed ? ball : null;
        }

        private void SetLinkCandidate(TopicBall ball)
        {
            if (_lineCandidate == ball) return;
            if (_lineCandidate != null) _lineCandidate.Highlighted = false;
            _lineCandidate = ball;
            if (_lineCandidate != null) _lineCandidate.Highlighted = true;
        }

        private void ClearLinkDrag()
        {
            SetLinkCandidate(null);
            _lineStart = null;
            if (_linkPreview != null) _linkPreview.enabled = false;
        }

        private void UpdateTapLink()
        {
            TopicBall first = null;
            TopicBall second = null;
            int held = 0;
            for (int i = 0; i < _balls.Count; i++)
            {
                var ball = _balls[i];
                if (ball == null || ball.IsTossed || !ball.Grab.isSelected) continue;
                if (held == 0) first = ball;
                else if (held == 1) second = ball;
                held++;
            }

            if (held != 2)
            {
                _tapLinkArmed = false;
                return;
            }
            if (_lineStart != null) return;
            float distance = Vector3.Distance(first.transform.position, second.transform.position);
            if (distance > .22f)
            {
                _tapLinkArmed = true;
                return;
            }
            if (_tapLinkArmed && distance < .15f)
            {
                ToggleLink(first, second);
                _tapLinkArmed = false;
            }
        }

        private bool AnyBallHeld()
        {
            for (int i = 0; i < _balls.Count; i++)
            {
                var ball = _balls[i];
                if (ball != null && !ball.IsTossed && ball.Grab.isSelected) return true;
            }
            return false;
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
            if (from == null || to == null || from == to || from.IsTossed || to.IsTossed) return;
            // Counts the gesture even when the link is refused as a loop, so the tutorial still moves on.
            LinkChanges++;
            var edge = _edges.FirstOrDefault(e => (e.from == from.Data.id && e.to == to.Data.id) || (e.from == to.Data.id && e.to == from.Data.id));
            if (edge != null)
            {
                _edges.Remove(edge);
                var child = _balls.FirstOrDefault(b => b.Data.id == edge.to);
                if (child != null) child.Data.parentIds = (child.Data.parentIds ?? Array.Empty<string>()).Where(id => id != edge.from).ToArray();
                SetStatus(ZapPlatform.IsDesktop ? "Parent connection removed." : "Link removed.");
            }
            else
            {
                if (NodeContext.WouldCycle(from.Data, to.Data, _balls.Select(b => b.Data)))
                { SetStatus(ZapPlatform.IsDesktop ? "That parent link would make a loop. Choose another topic." : "Those two are already connected another way."); return; }
                _edges.Add(new NodeEdge { from = from.Data.id, to = to.Data.id });
                to.Data.parentIds = (to.Data.parentIds ?? Array.Empty<string>()).Append(from.Data.id).Distinct().ToArray();
                SetStatus(ZapPlatform.IsDesktop ? from.Data.topic + " is now a parent of " + to.Data.topic : "Linked " + from.Data.topic + " and " + to.Data.topic + ".");
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
                line.sharedMaterial = _lineMaterial; line.positionCount = 2;
                // The line image repeats along the line instead of stretching.
                if (_lineTextured) line.textureMode = LineTextureMode.Tile;
                _lines.Add(line);
            }
        }
        private void UpdateLines()
        {
            var theme = ZapTheme.Current;
            _material.SetColor("_BaseColor", theme.nodeColor);
            _lineMaterial.SetColor("_BaseColor", theme.lineColor);
            float width = _lineTextured ? theme.lineImageWidth : theme.lineWidth;
            // One repeat of the image is as long as its aspect ratio times the line width.
            var tiling = Vector2.one;
            if (_lineTextured && theme.lineImage != null && width > 0)
                tiling.x = theme.lineImage.height / (theme.lineImage.width * width);
            if (_linkPreview != null)
            {
                _linkPreview.widthMultiplier = width * 2f;
                if (_lineTextured) _linkPreview.textureScale = tiling;
            }
            for (int i = 0; i < _edges.Count; i++)
            {
                var from = _balls.FirstOrDefault(b => b.Data.id == _edges[i].from);
                var to = _balls.FirstOrDefault(b => b.Data.id == _edges[i].to);
                _lines[i].widthMultiplier = width;
                if (_lineTextured) _lines[i].textureScale = tiling;
                _lines[i].enabled = from && to && !from.IsTossed && !to.IsTossed;
                if (_lines[i].enabled) { _lines[i].SetPosition(0, from.transform.position); _lines[i].SetPosition(1, to.transform.position); }
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
            Debug.Log($"[Constellation] Loaded {_balls.Count} nodes and {_edges.Count} links.");
            _dirty = false;
        }
        private void ClearConstellation()
        {
            Select(null);
            _operationVersion++; _worldRequest++; _previewRequests.Clear();
            ClearLinkDrag();
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
            _card = ControlsCard.Create(transform, true);
            if (!ZapPlatform.IsDesktop)
            {
                _hints = ControllerHints.Create(transform);
                _onboarding = Onboarding.Create(this, _hints);
            }
        }
        private void UpdateHud()
        {
            if (_card == null) return;
            // In a world this is an on-demand voice/status card, not a permanent hand HUD.
            // Answers and request progress already live on the movable learning pane.
            bool visible = ShowControlsCard && (!WorldActive || _input.AskHeld);
            _card.Visible = visible;
            if (!visible) return;
            bool tutorial = _onboarding != null && _onboarding.Running && !WorldActive && !CanCancelVoice;
            string title = tutorial ? _onboarding.Title : (_worldVisible ? _worldNode?.Data.topic : (Current != null ? Current.Data.topic : "ZAP!"));
            string hint = tutorial ? TutorialHint : (_worldVisible ? WorldHint : VoidHint);
            if (_worldVisible && _branchPositions.Count > 0)
                hint = (LinkedTravelTarget != null ? "X: enter " + LinkedTravelTarget.Data.topic : "Point + X: linked world · Grip + throw: travel")
                    + "\nLeft trigger: ask / create · A: new idea · Y: map";
            if (CanCancelVoice) hint = IsListening ? "Y: cancel · Release voice button to send\nYou can keep moving and looking around." : "Y: cancel this request\nYou can keep moving and looking around.";
            if (_preparingWorld && _departureNode != null) hint = "Opening your next world…\nY: cancel and stay here";
            string status = Status + (Travelling ? "\n" + Mathf.FloorToInt(_journey.Elapsed) + "s elapsed · Y returns" : "");
            // The card stays where the person left it, in the void and inside a world. It never rides a controller.
            _card.Show(title, tutorial ? _onboarding.Text : null, status, hint, _voice.IsRecording, _voice.Level, _busy);
        }

        // How far along the ray the nearest star, object or pane is. The card leaves the trigger alone when one of
        // them is in front of it, so a link, a cut or an object press never turns into a card drag.
        private float NearestTargetAlong(Ray ray)
        {
            float nearest = float.PositiveInfinity;
            if (Physics.Raycast(ray, out var hit, CardPointerRange, ~0, QueryTriggerInteraction.Collide)) nearest = hit.distance;
            if (_worldVisible) return nearest;
            // Stars are linked by aiming near them, so they count within the same cone the link gesture uses.
            RefreshLinkPositions();
            var ball = BallAt(LinkAim.Nearest(_linkPositions, ray.origin, ray.direction, 6f, CardPointerRange, -1));
            if (ball != null) nearest = Mathf.Min(nearest, Vector3.Distance(ray.origin, ball.transform.position));
            return nearest;
        }

        // The card is a window: while a controller points at it, that hand's trigger moves, resizes or hides the
        // card and does nothing else. A trigger pressed elsewhere keeps its job even if it sweeps across the card.
        private void UpdateCardPointers()
        {
            if (_card == null) return;
            var leftRay = _left != null ? new Ray(_left.position, _left.forward) : default;
            var rightRay = _right != null ? new Ray(_right.position, _right.forward) : default;
            bool left = _card.Point(0, _left != null, leftRay, _input.AskHeld, _input.Move.y,
                _left != null ? NearestTargetAlong(leftRay) : float.PositiveInfinity);
            bool right = _card.Point(1, _right != null, rightRay, _input.RightTrigger, _input.Look.y,
                _right != null ? NearestTargetAlong(rightRay) : float.PositiveInfinity);
            if (left)
            {
                _input.AskHeld = false;
                if (_card.DragHand == 0) _input.Move = Vector2.zero;
            }
            if (right)
            {
                _input.RightTrigger = false;
                _input.EventDown = false;
                if (_card.DragHand == 1) _input.Look = Vector2.zero;
            }
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
            {
                Vector3 position = _branchPositions.TryGetValue(ball, out var mapPosition) ? mapPosition : ball.transform.position;
                if (Vector3.Distance(ball.Data.position, position) > .002f)
                { ball.Data.position = position; _dirty = true; }
            }
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
        private void OnApplicationPause(bool paused) { if (paused) { if (_world != null && (InWorld || _preparingWorld)) LeaveWorld(); Save(); } }
        private void OnApplicationQuit() { Save(); }
        // Update no longer runs while disabled, so release the hold or the home frame could never re-align.
        private void OnDisable() { HomeFrame.Hold = false; }
        private void OnDestroy()
        {
            if (_worldControls != null && _worldControls.InputOwner == this) _worldControls.InputOwner = null;
            HomeFrame.Hold = false;
            ClearLinkDrag();
            if (_material) Destroy(_material);
            if (_lineMaterial) Destroy(_lineMaterial);
        }

        private void OnGUI()
        {
            if (ZapPlatform.IsDesktop && PendingDeletion != null)
            {
                GUILayout.BeginArea(new Rect(Screen.width / 2f - 220, Screen.height / 2f - 100, 440, 200), GUI.skin.box);
                GUILayout.Label("Delete “" + PendingDeletion.Data.topic + "”?");
                GUILayout.Label("This removes this node and its links. Connected worlds are kept.");
                if (GUILayout.Button("Keep idea")) CancelDeletion();
                if (GUILayout.Button("Delete idea")) ConfirmDeletion();
                GUILayout.EndArea();
            }
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
            if (CanCancelVoice && GUILayout.Button("Cancel request (Esc / Y)")) CancelPendingVoice();
            GUILayout.EndArea();
        }

        [Serializable] private class Transcript { public string text; }
        [Serializable] private class TranscriptImage { public string image; }
        [Serializable] private class ApiError { public string detail; }
        [Serializable] private class ParentNote { public string id; public string topic; public string summary; }
        [Serializable] private class NodeRequest { public string action; public string text; public NodeData node; public ParentNote[] parents; }
        [Serializable] private class NodeResult { public string topic; public string summary; public string worldPrompt; public string[] suggestions; public string mode; }
        [Serializable] private class PrepareRequest { public string nodeId; public string topic; public string context; }
        [Serializable] private class StudyRequest { public string topic; public string context; public string instruction; public StudyModelData current; public string focused; public string[] objects; public int step; public float parameter; public bool objectActions; public StudyObjectData[] objectStates; public string selectedId; }
    }
}
