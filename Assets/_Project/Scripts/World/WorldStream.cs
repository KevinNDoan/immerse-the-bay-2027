using System;
using UnityEngine;
using Zap.Net;

namespace Zap.World
{
    public class WorldStream : MonoBehaviour
    {
        public event Action<uint> OnFrameDecoded;

        public Texture2D Texture => _texture;
        public uint LatestSeq { get; private set; }
        public int FrameCount { get; private set; }

        private RelayClient _relayClient;
        private DomeView _domeView;
        private Texture2D _texture;

        private byte[] _pendingFrameBytes;
        private uint _pendingSeq;
        private bool _hasNewFrame;

        private void Awake()
        {
            _relayClient = GetComponent<RelayClient>();
            _domeView = GetComponent<DomeView>();

            _texture = new Texture2D(2, 2, TextureFormat.RGB24, false)
            {
                name = "ZapDome_StreamTexture",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
        }

        private void OnEnable()
        {
            if (_relayClient == null)
            {
                _relayClient = GetComponent<RelayClient>();
            }

            if (_relayClient != null)
            {
                _relayClient.OnVideoFrame += HandleVideoFrame;
            }
        }

        private void OnDisable()
        {
            if (_relayClient != null)
            {
                _relayClient.OnVideoFrame -= HandleVideoFrame;
            }
        }

        private void HandleVideoFrame(byte[] jpegBytes, uint seq)
        {
            // Keep only the newest frame
            _pendingFrameBytes = jpegBytes;
            _pendingSeq = seq;
            _hasNewFrame = true;
        }

        private void Update()
        {
            // At most once per Update
            if (_hasNewFrame && _pendingFrameBytes != null)
            {
                _hasNewFrame = false;
                byte[] frame = _pendingFrameBytes;
                _pendingFrameBytes = null;
                LatestSeq = _pendingSeq;

                if (!ImageConversion.LoadImage(_texture, frame)) return;

                if (_domeView == null)
                {
                    _domeView = GetComponent<DomeView>();
                }

                if (_domeView != null)
                {
                    _domeView.SetTexture(_texture);
                }

                FrameCount++;
                OnFrameDecoded?.Invoke(LatestSeq);
            }
        }

        private void OnDestroy()
        {
            if (_texture != null)
            {
                Destroy(_texture);
                _texture = null;
            }
        }
    }
}
