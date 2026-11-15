using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Video;

namespace Zap.Core
{
    // Plays the ZAP! logo animation once when the app starts, in front of the person and over everything else,
    // then fades into the workspace. It never blocks the app: a missing clip or a player error ends it at once.
    public sealed class IntroSplash : MonoBehaviour
    {
        const string VoidSceneName = "Void";
        const string RigName = "XR Origin (XR Rig)";
        const string ClipName = "ZapIntro";
        const string ShaderName = "Zap/Intro";
        const float BackdropRadius = 8f;
        // Longest wait for the first picture before giving up, in seconds. A cold start on the headset is slow.
        const float StartTimeout = 8f;
        // Notes and star art use sorting orders, which outrank the render queue, so the intro takes the highest.
        const int TopSortingOrder = 32767;

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int MainTexId = Shader.PropertyToID("_MainTex");

        VideoPlayer _player;
        RenderTexture _texture;
        Material _screenMaterial;
        Material _backdropMaterial;
        Transform _screen;
        Transform _head;
        Color _backdropColor;
        float _fadeSeconds;
        float _started;
        float _clipLength;
        float _playingSince = -1f;
        float _fadingSince = -1f;
        bool _locked;

        // True from start-up until the intro has faded out.
        public static bool Playing { get; private set; }

        // The intro plays once per start of the app, not each time the Void scene loads.
        static bool _attempted;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Playing = false; _attempted = false; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if (SceneManager.GetActiveScene().name != VoidSceneName || ZapPlatform.IsDesktop) return;
            if (_attempted) return;
            _attempted = true;

            var clip = Resources.Load<VideoClip>(ClipName);
            var shader = Shader.Find(ShaderName);
            if (clip == null || shader == null)
            {
                Debug.LogWarning($"ZAP intro skipped: {(clip == null ? "clip " + ClipName : "shader " + ShaderName)} not found.");
                return;
            }
            new GameObject("[ZAP Intro]").AddComponent<IntroSplash>().Build(clip, shader);
        }

        void Build(VideoClip clip, Shader shader)
        {
            var theme = ZapTheme.Current;
            _backdropColor = theme.introBackdrop;
            _fadeSeconds = Mathf.Max(.01f, theme.introFadeSeconds);
            _clipLength = (float)clip.length;
            _started = Time.unscaledTime;

            // The view is filled with the clip's own background colour, so the animation has no visible edge.
            var backdrop = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            backdrop.name = "Backdrop";
            Destroy(backdrop.GetComponent<Collider>());
            backdrop.transform.SetParent(transform, false);
            backdrop.transform.localScale = Vector3.one * (BackdropRadius * 2f);
            _backdropMaterial = new Material(shader) { renderQueue = 4000 };
            _backdropMaterial.SetColor(BaseColorId, _backdropColor);
            var backdropRenderer = backdrop.GetComponent<Renderer>();
            backdropRenderer.sharedMaterial = _backdropMaterial;
            backdropRenderer.sortingOrder = TopSortingOrder - 1;

            _texture = new RenderTexture(Mathf.Max(16, (int)clip.width), Mathf.Max(16, (int)clip.height), 0)
                { name = "ZAP intro picture" };
            _texture.Create();
            var previous = RenderTexture.active;
            RenderTexture.active = _texture;
            GL.Clear(true, true, _backdropColor);
            RenderTexture.active = previous;

            var screen = GameObject.CreatePrimitive(PrimitiveType.Quad);
            screen.name = "Screen";
            Destroy(screen.GetComponent<Collider>());
            _screen = screen.transform;
            _screen.SetParent(transform, false);
            _screen.localPosition = new Vector3(0f, 0f, theme.introDistance);
            float width = Mathf.Max(.1f, theme.introWidth);
            _screen.localScale = new Vector3(width, width * _texture.height / _texture.width, 1f);
            _screenMaterial = new Material(shader) { renderQueue = 4001 };
            _screenMaterial.SetTexture(MainTexId, _texture);
            var screenRenderer = screen.GetComponent<Renderer>();
            screenRenderer.sharedMaterial = _screenMaterial;
            screenRenderer.sortingOrder = TopSortingOrder;
            // Only the backdrop shows until the first picture is ready.
            screen.SetActive(false);

            _player = gameObject.AddComponent<VideoPlayer>();
            _player.playOnAwake = false;
            _player.source = VideoSource.VideoClip;
            _player.clip = clip;
            _player.isLooping = false;
            _player.skipOnDrop = true;
            _player.renderMode = VideoRenderMode.RenderTexture;
            _player.targetTexture = _texture;
            if (clip.audioTrackCount > 0)
            {
                var sound = gameObject.AddComponent<AudioSource>();
                sound.playOnAwake = false;
                sound.spatialBlend = 0f;
                _player.audioOutputMode = VideoAudioOutputMode.AudioSource;
                _player.SetTargetAudioSource(0, sound);
            }
            else _player.audioOutputMode = VideoAudioOutputMode.None;
            _player.loopPointReached += OnFinished;
            _player.errorReceived += OnError;
            _player.Play();

            Playing = true;
            FollowHead();
        }

        void Update()
        {
            float now = Time.unscaledTime;
            if (_fadingSince >= 0f)
            {
                float alpha = 1f - Mathf.Clamp01((now - _fadingSince) / _fadeSeconds);
                SetAlpha(alpha);
                if (alpha <= 0f) Destroy(gameObject);
                return;
            }

            if (_playingSince < 0f)
            {
                // Until the first picture shows, the intro stays centred on where the person is looking.
                FollowHead();
                if (_player != null && _player.isPlaying && _player.frame > 0) { _playingSince = now; Lock(); }
                else if (now - _started > StartTimeout) { Debug.LogWarning("ZAP intro skipped: the animation did not start."); BeginFade(); }
                return;
            }

            // The player's own end event is the normal way out; this covers a player that never raises it.
            if (now - _playingSince > _clipLength + 1f) BeginFade();
        }

        // Head position and heading only: the intro stays upright however the head is tilted.
        void FollowHead()
        {
            if (_locked) return;
            if (_head == null)
            {
                var camera = Camera.main;
                if (camera == null) return;
                _head = camera.transform;
            }
            var forward = Vector3.ProjectOnPlane(_head.forward, Vector3.up);
            var rotation = forward.sqrMagnitude > 1e-4f ? Quaternion.LookRotation(forward, Vector3.up) : transform.rotation;
            transform.SetPositionAndRotation(_head.position, rotation);
        }

        // Once the animation shows, it stays put in the play space. The home frame may still move the rig under
        // the person; riding on the rig keeps the intro in front of them when it does.
        void Lock()
        {
            _locked = true;
            _screen.gameObject.SetActive(true);
            var rig = GameObject.Find(RigName);
            if (rig != null) transform.SetParent(rig.transform, true);
        }

        void OnFinished(VideoPlayer player) { BeginFade(); }

        void OnError(VideoPlayer player, string message)
        {
            Debug.LogWarning("ZAP intro skipped: " + message);
            BeginFade();
        }

        void BeginFade()
        {
            if (_fadingSince < 0f) _fadingSince = Time.unscaledTime;
        }

        void SetAlpha(float alpha)
        {
            var color = _backdropColor;
            color.a = alpha;
            _backdropMaterial.SetColor(BaseColorId, color);
            _screenMaterial.SetColor(BaseColorId, new Color(1f, 1f, 1f, alpha));
        }

        void OnDestroy()
        {
            Playing = false;
            if (_player != null)
            {
                _player.loopPointReached -= OnFinished;
                _player.errorReceived -= OnError;
                _player.targetTexture = null;
            }
            if (_texture != null) { _texture.Release(); Destroy(_texture); }
            if (_screenMaterial != null) Destroy(_screenMaterial);
            if (_backdropMaterial != null) Destroy(_backdropMaterial);
        }
    }
}
