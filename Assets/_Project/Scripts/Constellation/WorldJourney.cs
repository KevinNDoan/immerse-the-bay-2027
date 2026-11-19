using UnityEngine;
using UnityEngine.Rendering;

namespace Zap.Constellation
{
    // All travel is visual: the tracked camera and rig are never animated.
    // Completion is explicitly signalled by destination readiness, not a fake timer.
    public sealed class WorldJourney : MonoBehaviour
    {
        public bool IsActive { get; private set; }
        public float Elapsed => IsActive ? Time.unscaledTime - _started : 0;
        private const int ShardCount = 40;
        private GameObject _tunnel, _burst;
        private Material _tunnelMaterial, _burstMaterial;
        private Mesh _mesh;
        private readonly Vector3[] _vertices = new Vector3[ShardCount * 3];
        private readonly Vector3[] _directions = new Vector3[ShardCount];
        private float _started, _readyAt = -1;
        private Vector3 _flight;
        private AudioSource _sound;
        private AudioClip _launchSound, _impactSound;
        public const float ThrowShatterDistance = 2f;
        public bool HasShattered { get; private set; }
        public bool HasCover => IsActive && HasShattered && Elapsed >= _flightDuration + 1;
        private GameObject _flyingOrb;
        private Material _flightMaterial;
        private Vector3 _flightOrigin;
        private float _flightDuration, _flightDistance;

        private void Awake()
        {
            _tunnel = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            _tunnel.name = "Blue hyperspace";
            _tunnel.transform.SetParent(transform, false);
            _tunnel.transform.localScale = Vector3.one * 60;
            var collider = _tunnel.GetComponent<Collider>(); collider.enabled = false; Destroy(collider);
            _tunnelMaterial = new Material(Shader.Find("Zap/Hyperspace"));
            Configure(_tunnel.GetComponent<MeshRenderer>(), _tunnelMaterial);
            _burst = new GameObject("Crystal fracture"); _burst.transform.SetParent(transform, false);
            _burstMaterial = new Material(Shader.Find("Zap/Crystal Shards"));
            Configure(_burst.AddComponent<MeshRenderer>(), _burstMaterial);
            _mesh = new Mesh { name = "Crystal shards (runtime)" }; _mesh.MarkDynamic();
            _burst.AddComponent<MeshFilter>().sharedMesh = _mesh;
            var indices = new int[ShardCount * 3];
            var colors = new Color[ShardCount * 3];
            var uv = new Vector2[ShardCount * 3];
            var edges = new Vector2[ShardCount * 3];
            for (int i = 0; i < ShardCount; i++)
            {
                float y = 1 - 2 * (i + .5f) / ShardCount;
                float angle = i * 2.399963f;
                _directions[i] = new Vector3(Mathf.Sqrt(1-y*y)*Mathf.Cos(angle), y, Mathf.Sqrt(1-y*y)*Mathf.Sin(angle));
                for (int j = 0; j < 3; j++)
                {
                    int n = i*3+j; indices[n] = n;
                    colors[n] = Color.Lerp(new Color(.22f,.46f,1), new Color(.85f,.95f,1), j*.4f);
                    uv[n] = new Vector2((i%8+j*.25f)/8, (i/8+j*.2f)/5);
                    edges[n] = j == 0 ? Vector2.right : j == 1 ? Vector2.up : Vector2.zero;
                }
            }
            _mesh.vertices = _vertices; _mesh.triangles = indices; _mesh.colors = colors; _mesh.uv = uv; _mesh.uv2 = edges;
            _flyingOrb=GameObject.CreatePrimitive(PrimitiveType.Sphere);
            _flyingOrb.name="Travelling crystal"; _flyingOrb.transform.SetParent(transform,false);
            _flyingOrb.transform.localScale=Vector3.one*.15f;
            var flightCollider=_flyingOrb.GetComponent<Collider>(); flightCollider.enabled=false; Destroy(flightCollider);
            _flightMaterial=new Material(Shader.Find("Zap/Crystal"));
            _flightMaterial.SetInt("_ZTest",(int)CompareFunction.Always);
            Configure(_flyingOrb.GetComponent<MeshRenderer>(),_flightMaterial);
            BuildSound();
            Cancel();
        }

        private static void Configure(MeshRenderer renderer, Material material)
        {
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        public void Begin(Vector3 origin, Vector3 direction, Texture preview = null, float flightDistance = 0, float flightSpeed = 4)
        {
            var camera = Camera.main;
            _flight = direction.sqrMagnitude > .01f ? direction.normalized : camera != null ? camera.transform.forward : Vector3.forward;
            // Lock the tunnel's orientation once; looking around remains freely tracked.
            Vector3 forward = camera != null ? camera.transform.forward : _flight;
            forward = Vector3.ProjectOnPlane(forward, Vector3.up).normalized;
            if (forward.sqrMagnitude < .01f) forward = Vector3.forward;
            _tunnel.transform.rotation = Quaternion.LookRotation(forward, Vector3.up);
            _burst.transform.SetPositionAndRotation(origin, Quaternion.identity);
            _burstMaterial.SetTexture("_PreviewTex", preview != null ? preview : Texture2D.whiteTexture);
            _burstMaterial.SetFloat("_HasPreview", preview != null ? 1 : 0);
            _flightOrigin=origin; _flightDistance=Mathf.Clamp(flightDistance,0,5);
            _flightDuration=_flightDistance/Mathf.Clamp(flightSpeed,3,8);
            _flyingOrb.transform.position=origin;
            _flightMaterial.SetTexture("_PreviewTex",preview!=null?preview:Texture2D.whiteTexture);
            _flightMaterial.SetFloat("_HasPreview",preview!=null?1:0);
            _flightMaterial.SetFloat("_Focus",1);
            _started = Time.unscaledTime; _readyAt = -1; IsActive = true;
            HasShattered=false; _tunnel.SetActive(false); _burst.SetActive(false);
            _flyingOrb.SetActive(_flightDistance>0);
            _sound.transform.position = origin;
            _sound.Stop();
            if (_flightDistance<=0) Shatter();
        }

        public void MarkReady()
        {
            if (IsActive && _readyAt < 0) _readyAt = Mathf.Max(Elapsed, _flightDuration+1.8f);
        }

        public void Cancel()
        {
            IsActive = false; _readyAt = -1;
            if (_tunnel != null) _tunnel.SetActive(false);
            if (_burst != null) _burst.SetActive(false);
            if (_sound != null) _sound.Stop();
            if (_flyingOrb != null) _flyingOrb.SetActive(false);
        }

        private void BuildSound()
        {
            _impactSound = Resources.Load<AudioClip>("ZapOrbImpact");
            const int rate = 24000;
            var samples = new float[rate];
            uint random = 2137;
            float filtered = 0;
            for (int i = 0; i < samples.Length; i++)
            {
                float t = (float)i/rate;
                random = random*1664525u+1013904223u;
                float noise = (random>>8)/8388608f-1;
                filtered = Mathf.Lerp(filtered,noise,.09f+.3f*t);
                float air = filtered*Mathf.Sin(t*Mathf.PI)*.45f;
                float chime = (Mathf.Sin(t*2*Mathf.PI*1318)+Mathf.Sin(t*2*Mathf.PI*2093)*.45f)*Mathf.Exp(-t*8)*.14f;
                samples[i] = (air+chime)*Mathf.Min(1,t*100)*(1-t);
            }
            _launchSound = AudioClip.Create("Crystal opening",samples.Length,1,rate,false);
            _launchSound.SetData(samples,0);
            var audioObject = new GameObject("Crystal opening sound"); audioObject.transform.SetParent(transform,false);
            _sound = audioObject.AddComponent<AudioSource>();
            _sound.playOnAwake = false; _sound.clip = _launchSound;
            _sound.volume = .3f; _sound.spatialBlend = .35f;
            _sound.minDistance = .5f; _sound.maxDistance = 8;
        }

        private void LateUpdate()
        {
            if (!IsActive) return;
            float age = Elapsed;
            if (!HasShattered)
            {
                float distance=Mathf.Min(_flightDistance,age/Mathf.Max(.001f,_flightDuration)*_flightDistance);
                _flyingOrb.transform.position=_flightOrigin+_flight*distance;
                if(distance<_flightDistance) return;
                Shatter();
            }
            float fade = _readyAt >= 0 ? Mathf.SmoothStep(0, 1, (age-_readyAt)/.8f) : 0;
            RenderAt(age-_flightDuration, fade);
            if (fade >= 1) Cancel();
        }

        private void Shatter()
        {
            HasShattered=true; _flyingOrb.SetActive(false);
            _burst.transform.position=_flightOrigin+_flight*_flightDistance;
            _tunnel.SetActive(true);_burst.SetActive(true);
            _sound.transform.position=_burst.transform.position;_sound.Play();
            if (_impactSound!=null) _sound.PlayOneShot(_impactSound,1.4f);
            RenderAt(0,0);
        }

        private void RenderAt(float age, float fade)
        {
            if (Camera.main != null) _tunnel.transform.position = Camera.main.transform.position;
            float opacity = Mathf.SmoothStep(0, 1, age/.8f) * (1-fade);
            _tunnelMaterial.SetFloat("_Opacity", opacity);
            // Keep the same travel speed for the entire provider wait. Opacity
            // still eases in/out, but motion never slows after the first seconds.
            _tunnelMaterial.SetFloat("_Travel", age);
            if (age > 1.5f) { _burst.SetActive(false); return; }
            _burstMaterial.SetFloat("_Opacity", (1-Mathf.SmoothStep(0,1,(age-.35f)/1.15f))*(1-fade));
            for (int i = 0; i < ShardCount; i++)
            {
                Vector3 axis = _directions[i];
                Vector3 center = axis * (.06f+age*(.5f+(i%5)*.13f)) + _flight*(age*.7f);
                Quaternion spin = Quaternion.AngleAxis(age*(80+i*7),axis);
                Vector3 tangent = Vector3.Cross(axis, Vector3.up).normalized;
                Vector3 bitangent = Vector3.Cross(axis,tangent);
                float size = .022f + (i%4)*.006f;
                _vertices[i*3] = center + spin*(tangent*size);
                _vertices[i*3+1] = center + spin*(-tangent*size*.7f+bitangent*size*.8f);
                _vertices[i*3+2] = center + spin*(-tangent*size*.4f-bitangent*size*1.4f);
            }
            _mesh.vertices = _vertices; _mesh.RecalculateBounds();
        }

        private void OnDisable() { Cancel(); }
        private void OnDestroy()
        {
            if (_mesh != null) Destroy(_mesh);
            if (_tunnelMaterial != null) Destroy(_tunnelMaterial);
            if (_burstMaterial != null) Destroy(_burstMaterial);
            if (_launchSound != null) Destroy(_launchSound);
            if (_flightMaterial != null) Destroy(_flightMaterial);
        }
    }
}
