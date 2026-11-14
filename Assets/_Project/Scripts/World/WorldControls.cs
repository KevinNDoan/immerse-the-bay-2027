using System;
using UnityEngine;
using Zap.Core;
using Zap.Net;

namespace Zap.World
{
    public sealed class WorldControls : MonoBehaviour
    {
        public const float SteerInterval = 1.8f;

        public event Action<string> StatusChanged;

        public string PendingInstruction { get; private set; }

        private WorldSession _session;
        private WorldStream _stream;
        private readonly ControllerActions _input = new ControllerActions();
        private float _lastSteer;
        private uint _pendingSeq;

        private void Awake()
        {
            _session = GetComponent<WorldSession>();
            _stream = GetComponent<WorldStream>();
        }

        private void OnEnable()
        {
            if (_session != null)
            {
                _session.OnStatus += HandleStatus;
                _session.OnError += HandleError;
                _session.OnPrompt += HandlePrompt;
            }

            if (_stream != null)
            {
                _stream.OnFrameDecoded += HandleFrame;
            }
        }

        private void OnDisable()
        {
            if (_session != null)
            {
                _session.OnStatus -= HandleStatus;
                _session.OnError -= HandleError;
                _session.OnPrompt -= HandlePrompt;
            }

            if (_stream != null)
            {
                _stream.OnFrameDecoded -= HandleFrame;
            }
        }

        private void Update()
        {
            if (_session == null) return;

            _input.Poll();

            if (!_session.IsEntered)
            {
                PendingInstruction = null;
                return;
            }

            if (_session.State != "streaming" || Time.unscaledTime - _lastSteer < SteerInterval) return;

            if (_input.EventDown)
            {
                Send("event", "Show an interesting change that teaches something about " + _session.Topic);
            }
            else if (_input.Look.sqrMagnitude > .35f)
            {
                string text = Mathf.Abs(_input.Look.x) > Mathf.Abs(_input.Look.y)
                    ? (_input.Look.x > 0 ? "slowly pan right" : "slowly pan left")
                    : (_input.Look.y > 0 ? "slowly look up" : "slowly look down");
                Send("look", text);
            }
            else if (_input.Move.sqrMagnitude > .35f)
            {
                string text = Mathf.Abs(_input.Move.y) > Mathf.Abs(_input.Move.x)
                    ? (_input.Move.y > 0 ? "slowly move forward" : "slowly move backward")
                    : (_input.Move.x > 0 ? "slowly move right" : "slowly move left");
                Send("move", text);
            }
        }

        public void Send(string kind, string text)
        {
            if (_session == null || !_session.IsEntered) return;

            _lastSteer = Time.unscaledTime;
            _pendingSeq = uint.MaxValue;
            PendingInstruction = text;
            Raise("Sending: " + text);
            _session.Steer(kind, text);
        }

        private void HandlePrompt(string text, uint sequence)
        {
            if (PendingInstruction != null)
            {
                _pendingSeq = sequence;
            }
        }

        private void HandleFrame(uint sequence)
        {
            if (PendingInstruction != null && sequence >= _pendingSeq)
            {
                Raise("World updated: " + PendingInstruction);
                PendingInstruction = null;
            }
        }

        private void HandleStatus(StatusMessage status)
        {
            if (status == null) return;

            if (status.state == "error")
            {
                Raise(status.detail);
            }
            else if (status.state == "streaming")
            {
                Raise("Explore with the sticks. Hold A to speak. Y returns to your constellation.");
            }
            else if (!string.IsNullOrEmpty(status.detail))
            {
                Raise(status.detail);
            }
        }

        private void HandleError(ErrorMessage error)
        {
            if (error != null)
            {
                Raise(error.detail);
            }
        }

        private void Raise(string text)
        {
            StatusChanged?.Invoke(text);
        }
    }
}
