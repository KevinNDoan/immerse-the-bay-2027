using System;
using UnityEngine;
using Zap.Net;

namespace Zap.World
{
    public class WorldSession : MonoBehaviour
    {
        public string State { get; private set; } = "idle";

        public event Action<StatusMessage> OnStatus;
        public event Action<string, uint> OnPrompt;
        public event Action<ErrorMessage> OnError;
        public bool IsEntered { get; private set; }

        private RelayClient _relayClient;
        private DomeView _domeView;
        private WorldStream _worldStream;
        private bool _awaitingEnter;

        private void Awake()
        {
            _relayClient = GetComponent<RelayClient>();
            _domeView = GetComponent<DomeView>();
            _worldStream = GetComponent<WorldStream>();
        }

        private void OnEnable()
        {
            if (_relayClient == null)
            {
                _relayClient = GetComponent<RelayClient>();
            }

            if (_relayClient != null)
            {
                _relayClient.OnStatus += HandleRelayStatus;
                _relayClient.OnPrompt += HandleRelayPrompt;
                _relayClient.OnError += HandleRelayError;
            }
        }

        private void OnDisable()
        {
            if (_relayClient != null)
            {
                _relayClient.OnStatus -= HandleRelayStatus;
                _relayClient.OnPrompt -= HandleRelayPrompt;
                _relayClient.OnError -= HandleRelayError;
            }
        }

        public void Enter(string nodeId, string topic, string prompt = "", string imageBase64 = "")
        {
            IsEntered = true;
            _awaitingEnter = true;
            State = "connecting";

            if (_domeView == null)
            {
                _domeView = GetComponent<DomeView>();
            }

            if (_domeView != null)
            {
                _domeView.SetVisible(true);
            }

            if (_relayClient == null)
            {
                _relayClient = GetComponent<RelayClient>();
            }

            if (_relayClient != null)
            {
                _relayClient.Enter(nodeId, topic, prompt, imageBase64);
            }
        }

        public void Exit()
        {
            IsEntered = false;
            _awaitingEnter = false;
            if (_domeView == null)
            {
                _domeView = GetComponent<DomeView>();
            }

            if (_domeView != null)
            {
                _domeView.SetVisible(false);
            }

            if (_relayClient == null)
            {
                _relayClient = GetComponent<RelayClient>();
            }

            if (_relayClient != null)
            {
                _relayClient.Exit();
            }

            State = "idle";
        }

        public void Steer(string kind, string text)
        {
            if (_relayClient == null)
            {
                _relayClient = GetComponent<RelayClient>();
            }

            if (_relayClient != null)
            {
                _relayClient.Steer(kind, text);
            }
        }

        private void HandleRelayStatus(StatusMessage status)
        {
            if (_awaitingEnter && status.state == "idle" && string.IsNullOrEmpty(status.detail)) return;
            if (status.state == "connecting" || status.state == "streaming" || status.state == "error") _awaitingEnter = false;
            if (status != null && !string.IsNullOrEmpty(status.state))
            {
                State = status.state;
                if (State == "error" || State == "idle")
                {
                    IsEntered = false;
                    if (_domeView != null) _domeView.SetVisible(false);
                }
            }
            OnStatus?.Invoke(status);
        }

        private void HandleRelayPrompt(string text, uint appliesAtSeq)
        {
            OnPrompt?.Invoke(text, appliesAtSeq);
        }

        private void HandleRelayError(ErrorMessage error)
        {
            OnError?.Invoke(error);
        }
    }
}
