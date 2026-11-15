using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace Zap.Constellation
{
    public sealed class VoiceRecorder : MonoBehaviour
    {
        public bool IsRecording { get; private set; }
        public bool IsStarting { get; private set; }
        public float Level { get; private set; }
        public event Action<string> Error;
        private AudioClip _clip;
        private string _device;
        private int _startVersion;
        private float _began;
        private readonly float[] _meter = new float[128];

        public IEnumerator Begin()
        {
            if (IsRecording || IsStarting) yield break;
            IsStarting = true;
            int version = ++_startVersion;
#if UNITY_ANDROID && !UNITY_EDITOR
            if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Microphone))
            {
                UnityEngine.Android.Permission.RequestUserPermission(UnityEngine.Android.Permission.Microphone);
                float deadline = Time.realtimeSinceStartup + 20;
                while (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Microphone)
                    && Time.realtimeSinceStartup < deadline && version == _startVersion) yield return null;
                if (version != _startVersion) yield break;
                if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Microphone))
                { Fail("Allow microphone access, then hold the button again."); yield break; }
            }
#else
            if (!Application.HasUserAuthorization(UserAuthorization.Microphone))
                yield return Application.RequestUserAuthorization(UserAuthorization.Microphone);
            if (version != _startVersion) yield break;
            if (!Application.HasUserAuthorization(UserAuthorization.Microphone))
            { Fail("Allow microphone access, then try again."); yield break; }
#endif
            if (version != _startVersion) yield break;
            if (Microphone.devices.Length == 0) { Fail("No microphone detected."); yield break; }
            _device = Microphone.devices[0];
            _clip = Microphone.Start(_device, false, 21, 16000);
            _began = Time.realtimeSinceStartup;
            IsRecording = _clip != null;
            IsStarting = false;
            if (!IsRecording) Fail("Could not start the microphone.");
        }

        private void Update()
        {
            if (!IsRecording || _clip == null) return;
            int position = Microphone.GetPosition(_device);
            if (position >= _meter.Length && _clip.GetData(_meter, position - _meter.Length))
            {
                float sum = 0;
                foreach (float value in _meter) sum += value * value;
                Level = Mathf.Sqrt(sum / _meter.Length);
            }
        }

        public byte[] Finish()
        {
            _startVersion++; IsStarting = false;
            if (!IsRecording || _clip == null) return null;
            int count = Microphone.GetPosition(_device);
            if (count <= 0 && Time.realtimeSinceStartup - _began > 20) count = _clip.samples;
            count = Mathf.Min(count, _clip.frequency * 20);
            Microphone.End(_device);
            IsRecording = false;
            Level = 0;
            var clip = _clip; _clip = null;
            if (count < clip.frequency * .3f) { Destroy(clip); Fail("Hold the button while speaking, then release."); return null; }
            float[] samples = new float[count * clip.channels];
            clip.GetData(samples, 0);
            var bytes = EncodeWav(samples, clip.frequency, clip.channels);
            Destroy(clip);
            return bytes;
        }

        public void Cancel()
        {
            _startVersion++; IsStarting = false;
            if (IsRecording) Microphone.End(_device);
            IsRecording = false; Level = 0;
            if (_clip != null) Destroy(_clip);
            _clip = null;
        }

        public static byte[] EncodeWav(float[] samples, int rate, int channels = 1)
        {
            int count = samples.Length / channels;
            using var output = new MemoryStream(44 + count * 2);
            using var writer = new BinaryWriter(output);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); writer.Write(36 + count * 2);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16);
            writer.Write((short)1); writer.Write((short)1); writer.Write(rate); writer.Write(rate * 2);
            writer.Write((short)2); writer.Write((short)16);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("data")); writer.Write(count * 2);
            for (int i = 0; i < count; i++)
            {
                float value = 0;
                for (int c = 0; c < channels; c++) value += samples[i * channels + c];
                writer.Write((short)Mathf.RoundToInt(Mathf.Clamp(value / channels, -1, 1) * 32767));
            }
            return output.ToArray();
        }

        private void Fail(string message) { IsStarting = false; Error?.Invoke(message); }
        private void OnDisable() { Cancel(); }
    }
}
