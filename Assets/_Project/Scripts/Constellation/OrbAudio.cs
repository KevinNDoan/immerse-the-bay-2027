using UnityEngine;
using UnityEngine.SceneManagement;

namespace Zap.Constellation
{
    // Separate from the orbs: travel hides them while their release sound is still playing.
    public sealed class OrbAudio : MonoBehaviour
    {
        public const float MinimumImpactSpeed = .65f;
        private const float MinimumThrowSpeed = .75f;
        private const int VoiceCount = 8;
        private static OrbAudio _instance;
        private readonly AudioSource[] _voices = new AudioSource[VoiceCount];
        private AudioClip _throwClip, _impactClip;
        private int _nextVoice;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetCache() { _instance = null; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (SceneManager.GetActiveScene().name == "Void") EnsureInstance();
        }

        private static OrbAudio EnsureInstance()
        {
            if (_instance == null) _instance = FindAnyObjectByType<OrbAudio>();
            if (_instance == null) _instance = new GameObject("Orb sounds").AddComponent<OrbAudio>();
            return _instance;
        }

        private void Awake()
        {
            _instance = this;
            _throwClip = Resources.Load<AudioClip>("ZapOrbThrow");
            _impactClip = Resources.Load<AudioClip>("ZapOrbImpact");
            if (_throwClip == null || _impactClip == null)
                Debug.LogWarning("Orb sounds are missing. Import ZapOrbThrow.wav and ZapOrbImpact.wav from Resources.");
            for (int i = 0; i < _voices.Length; i++)
            {
                var voice = new GameObject("Orb sound " + i);
                voice.transform.SetParent(transform, false);
                var source = voice.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.spatialBlend = 1;
                source.dopplerLevel = 0;
                source.minDistance = .75f;
                source.maxDistance = 12;
                source.rolloffMode = AudioRolloffMode.Linear;
                _voices[i] = source;
            }
        }

        public static void PlayThrow(Vector3 position, float speed)
        {
            if (speed < MinimumThrowSpeed) return;
            var audio = EnsureInstance();
            float strength = Mathf.InverseLerp(MinimumThrowSpeed, 6, speed);
            audio.Play(audio._throwClip, position, Mathf.Lerp(.25f, .5f, strength), Mathf.Lerp(.95f, 1.15f, strength));
        }

        public static void PlayImpact(Vector3 position, float speed)
        {
            if (speed < MinimumImpactSpeed) return;
            var audio = EnsureInstance();
            float strength = Mathf.InverseLerp(MinimumImpactSpeed, 6, speed);
            audio.Play(audio._impactClip, position, Mathf.Lerp(.18f, .55f, strength), Mathf.Lerp(1.1f, .9f, strength));
        }

        private void Play(AudioClip clip, Vector3 position, float volume, float pitch)
        {
            if (clip == null || !isActiveAndEnabled) return;
            // Prefer an idle voice; a burst can replace the next slot without allocating objects.
            int index = _nextVoice;
            for (int offset = 0; offset < _voices.Length; offset++)
            {
                int candidate = (_nextVoice + offset) % _voices.Length;
                if (!_voices[candidate].isPlaying) { index = candidate; break; }
            }
            _nextVoice = (index + 1) % _voices.Length;
            var source = _voices[index];
            source.Stop();
            source.transform.position = position;
            source.clip = clip;
            source.volume = volume;
            source.pitch = pitch;
            source.Play();
        }

        private void OnDisable()
        {
            foreach (var source in _voices) if (source != null) source.Stop();
        }

        private void OnDestroy() { if (_instance == this) _instance = null; }
    }
}
