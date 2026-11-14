using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Zap.Net
{
    public class RelayClient : MonoBehaviour
    {
        private static readonly int[] BackoffDelays = { 1, 2, 4, 8 };

        // Events
        public event Action<byte[], uint> OnVideoFrame;
        public event Action<byte[]> OnAudioFrame;
        public event Action<StatusMessage> OnStatus;
        public event Action<string, uint> OnPrompt;
        public event Action<ErrorMessage> OnError;

        // Thread sync
        private readonly ConcurrentQueue<Action> _mainThreadQueue = new ConcurrentQueue<Action>();
        private readonly ConcurrentQueue<string> _sendQueue = new ConcurrentQueue<string>();
        private readonly SemaphoreSlim _sendSignal = new SemaphoreSlim(0);

        // Frame swapping
        private readonly object _frameLock = new object();
        private byte[] _pendingFrame;
        private uint _pendingFrameSeq;

        // WebSocket & background tasks
        private ClientWebSocket _ws;
        private CancellationTokenSource _cts;
        private Task _connectionTask;
        private bool _isQuitting;
        private string _endpoint;

        public bool IsConnected => _ws != null && _ws.State == WebSocketState.Open;

        private void Awake()
        {
            StartConnection();
        }

        public void Connect()
        {
            StartConnection();
        }

        private void StartConnection()
        {
            if (_cts != null) return;
            // Resources.Load must run on Unity's main thread, before Task.Run.
            _endpoint = RelayConfig.WorldWsUrl;
            _cts = new CancellationTokenSource();
            _connectionTask = Task.Run(() => ConnectionLoopAsync(_cts.Token));
        }

        private async Task ConnectionLoopAsync(CancellationToken ct)
        {
            int backoffIndex = 0;

            while (!ct.IsCancellationRequested && !_isQuitting)
            {
                try
                {
                    _ws = new ClientWebSocket();
                    var uri = new Uri(_endpoint);
                    await _ws.ConnectAsync(uri, ct);

                    // Successfully connected
                    backoffIndex = 0;

                    using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                    var sendTask = ProcessSendQueueAsync(_ws, linkedCts.Token);
                    var receiveTask = ReceiveLoopAsync(_ws, linkedCts.Token);

                    // If either task completes or faults, complete the other
                    var finished = await Task.WhenAny(sendTask, receiveTask);
                    linkedCts.Cancel();

                    try
                    {
                        await Task.WhenAll(sendTask, receiveTask);
                    }
                    catch
                    {
                        // Suppress cancellation / socket abort when draining child tasks
                    }
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    EnqueueMainThread(() =>
                    {
                        // Notify error or debug log
                        Debug.Log($"[RelayClient] WebSocket disconnected or failed: {ex.Message}");
                    });
                }
                finally
                {
                    CleanupSocket();
                    EnqueueMainThread(() => OnStatus?.Invoke(new StatusMessage {
                        state = "idle", detail = "Relay disconnected. Reconnecting; enter the world again when ready."
                    }));
                }

                if (ct.IsCancellationRequested || _isQuitting)
                {
                    break;
                }

                int delaySec = BackoffDelays[Math.Min(backoffIndex, BackoffDelays.Length - 1)];
                backoffIndex++;

                try
                {
                    await Task.Delay(delaySec * 1000, ct);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        private async Task ProcessSendQueueAsync(ClientWebSocket ws, CancellationToken ct)
        {
            while (!ct.IsCancellationRequested && ws.State == WebSocketState.Open)
            {
                try
                {
                    await _sendSignal.WaitAsync(ct);
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                while (_sendQueue.TryDequeue(out string json))
                {
                    if (ws.State != WebSocketState.Open || ct.IsCancellationRequested)
                    {
                        break;
                    }

                    byte[] bytes = Encoding.UTF8.GetBytes(json);
                    await ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, ct);
                }
            }
        }

        private async Task ReceiveLoopAsync(ClientWebSocket ws, CancellationToken ct)
        {
            byte[] segmentBuffer = new byte[65536];
            using var ms = new MemoryStream();

            while (!ct.IsCancellationRequested && ws.State == WebSocketState.Open)
            {
                ms.SetLength(0);
                WebSocketReceiveResult result;

                do
                {
                    result = await ws.ReceiveAsync(new ArraySegment<byte>(segmentBuffer), ct);

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        try
                        {
                            await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", ct);
                        }
                        catch
                        {
                            // Ignored during close
                        }
                        return;
                    }

                    ms.Write(segmentBuffer, 0, result.Count);
                    if (ms.Length > 8 * 1024 * 1024)
                        throw new InvalidDataException("Relay message exceeds 8 MB.");
                } while (!result.EndOfMessage);

                if (result.MessageType == WebSocketMessageType.Binary)
                {
                    ProcessBinaryMessage(ms);
                }
                else if (result.MessageType == WebSocketMessageType.Text)
                {
                    ProcessTextMessage(ms);
                }
            }
        }

        private void ProcessBinaryMessage(MemoryStream ms)
        {
            if (ms.Length < 13) return;

            byte[] raw = ms.GetBuffer();
            byte type = raw[0];

            if (type == 1) // Video frame
            {
                // Little-endian u32 seq
                uint seq = (uint)(raw[1] | (raw[2] << 8) | (raw[3] << 16) | (raw[4] << 24));
                int payloadLength = (int)ms.Length - 13;

                if (payloadLength > 0)
                {
                    byte[] jpegBytes = new byte[payloadLength];
                    Buffer.BlockCopy(raw, 13, jpegBytes, 0, payloadLength);

                    lock (_frameLock)
                    {
                        if (_pendingFrame == null || seq >= _pendingFrameSeq)
                        {
                            _pendingFrame = jpegBytes;
                            _pendingFrameSeq = seq;
                        }
                    }
                }
            }
            else if (type == 2)
            {
                var pcm = new byte[(int)ms.Length - 13];
                Buffer.BlockCopy(raw, 13, pcm, 0, pcm.Length);
                OnAudioFrame?.Invoke(pcm); // Audio consumer uses a bounded, thread-safe ring.
            }
        }

        private void ProcessTextMessage(MemoryStream ms)
        {
            string json = Encoding.UTF8.GetString(ms.GetBuffer(), 0, (int)ms.Length);

            try
            {
                var header = JsonUtility.FromJson<MessageHeader>(json);
                if (header == null || string.IsNullOrEmpty(header.t)) return;

                switch (header.t)
                {
                    case "status":
                        var status = JsonUtility.FromJson<StatusMessage>(json);
                        if (status != null)
                        {
                            EnqueueMainThread(() => OnStatus?.Invoke(status));
                        }
                        break;

                    case "prompt":
                        var prompt = JsonUtility.FromJson<PromptMessage>(json);
                        if (prompt != null)
                        {
                            EnqueueMainThread(() => OnPrompt?.Invoke(prompt.text, prompt.appliesAtSeq));
                        }
                        break;

                    case "error":
                        var error = JsonUtility.FromJson<ErrorMessage>(json);
                        if (error != null)
                        {
                            EnqueueMainThread(() => OnError?.Invoke(error));
                        }
                        break;

                    case "pong":
                        // pong received
                        break;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[RelayClient] Error parsing incoming text message: {ex.Message}");
            }
        }

        public void Enter(string nodeId, string topic, string prompt = "", string imageBase64 = "")
        {
            ClearFrames();
            var msg = new EnterMessage
            {
                t = "enter",
                nodeId = nodeId ?? "",
                topic = topic ?? "",
                prompt = prompt ?? "",
                image = imageBase64 ?? ""
            };
            SendJson(JsonUtility.ToJson(msg));
        }

        public void Steer(string kind, string text)
        {
            var msg = new SteerMessage
            {
                t = "steer",
                kind = kind ?? "",
                text = text ?? ""
            };
            SendJson(JsonUtility.ToJson(msg));
        }

        public void Exit()
        {
            ClearFrames();
            var msg = new ExitMessage { t = "exit" };
            SendJson(JsonUtility.ToJson(msg));
        }

        private void ClearFrames()
        {
            lock (_frameLock) { _pendingFrame = null; _pendingFrameSeq = 0; }
        }

        public void Ping()
        {
            var msg = new PingMessage { t = "ping" };
            SendJson(JsonUtility.ToJson(msg));
        }

        private void SendJson(string json)
        {
            _sendQueue.Enqueue(json);
            _sendSignal.Release();
        }

        private void EnqueueMainThread(Action action)
        {
            _mainThreadQueue.Enqueue(action);
        }

        private void Update()
        {
            // 1. Drain main thread actions
            while (_mainThreadQueue.TryDequeue(out var action))
            {
                try
                {
                    action?.Invoke();
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                }
            }

            // 2. Dispatch latest video frame
            byte[] frame = null;
            uint seq = 0;

            lock (_frameLock)
            {
                if (_pendingFrame != null)
                {
                    frame = _pendingFrame;
                    seq = _pendingFrameSeq;
                    _pendingFrame = null;
                }
            }

            if (frame != null)
            {
                try
                {
                    OnVideoFrame?.Invoke(frame, seq);
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                }
            }
        }

        private void OnDestroy()
        {
            StopConnection();
        }

        private void OnApplicationQuit()
        {
            _isQuitting = true;
            StopConnection();
        }

        private void StopConnection()
        {
            if (_cts != null)
            {
                try
                {
                    _cts.Cancel();
                    _cts.Dispose();
                }
                catch
                {
                    // Ignore on shutdown
                }
                _cts = null;
            }

            CleanupSocket();
        }

        private void CleanupSocket()
        {
            if (_ws != null)
            {
                try
                {
                    if (_ws.State == WebSocketState.Open || _ws.State == WebSocketState.CloseReceived)
                    {
                        _ws.Abort();
                    }
                    _ws.Dispose();
                }
                catch
                {
                    // Ignore on shutdown
                }
                _ws = null;
            }
        }
    }
}
