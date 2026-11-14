using UnityEngine;
using Zap.Net;

namespace Zap.World
{
    public sealed class WorldAudio : MonoBehaviour
    {
        private readonly float[] _ring = new float[48000];
        private readonly object _gate = new object();
        private int _read, _write, _count;
        private RelayClient _client;
        private AudioClip _clip;
        private AudioSource _source;
        private void Awake()
        {
            _client = GetComponent<RelayClient>();
            _source = gameObject.AddComponent<AudioSource>();
            _source.spatialBlend = 0; _source.loop = true; _source.volume = .65f;
            _clip = AudioClip.Create("Orbis live PCM", 48000, 1, 48000, true, ReadAudio);
            _source.clip = _clip;
            _client.OnAudioFrame += Receive;
            _client.OnStatus += Status;
        }
        private void Status(StatusMessage status)
        {
            if (status.state == "streaming") { if (!_source.isPlaying) _source.Play(); }
            else { _source.Stop(); lock (_gate) { _read = _write = _count = 0; } }
        }
        private void Receive(byte[] bytes)
        {
            lock (_gate)
                for (int i = 0; i + 1 < bytes.Length; i += 2)
                {
                    if (_count == _ring.Length) { _read = (_read + 1) % _ring.Length; _count--; }
                    _ring[_write] = (short)(bytes[i] | bytes[i + 1] << 8) / 32768f;
                    _write = (_write + 1) % _ring.Length; _count++;
                }
        }
        private void ReadAudio(float[] output)
        {
            lock (_gate)
                for (int i = 0; i < output.Length; i++)
                {
                    output[i] = _count > 0 ? _ring[_read] : 0;
                    if (_count > 0) { _read = (_read + 1) % _ring.Length; _count--; }
                }
        }
        public void SetMuted(bool muted) { if (_source) _source.mute = muted; }
        private void OnDestroy()
        {
            if (_client) { _client.OnAudioFrame -= Receive; _client.OnStatus -= Status; }
            if (_clip) Destroy(_clip);
        }
    }
}
