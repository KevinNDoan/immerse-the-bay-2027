using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Zap.Constellation
{
    // A destination image is viewed through a curved glass lens. It is a preview,
    // not a claim that the panorama has been reconstructed as miniature geometry.
    public sealed class CrystalBallVisual : MonoBehaviour
    {
        public bool PreviewReady { get; private set; }
        public Texture2D PreviewTexture => _preview;
        private Material _material;
        private Texture2D _preview;
        private string _image;
        private float _focus;
        private Material _glowMaterial;
        private GameObject _glow;

        private void Awake()
        {
            var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.name = "Glass world lens";
            sphere.transform.SetParent(transform, false);
            var collider = sphere.GetComponent<Collider>();
            collider.enabled = false;
            Destroy(collider);
            var renderer = sphere.GetComponent<MeshRenderer>();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            _material = new Material(Shader.Find("Zap/Crystal")) { name = "Crystal glass (runtime)" };
            renderer.sharedMaterial = _material;
            _glow = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            _glow.name = "Recording aura"; _glow.transform.SetParent(transform, false);
            _glow.transform.localScale = Vector3.one * 1.6f;
            _glow.GetComponent<Collider>().enabled = false; Destroy(_glow.GetComponent<Collider>());
            _glowMaterial = new Material(Shader.Find("Zap/OrbGlow"));
            _glow.GetComponent<Renderer>().sharedMaterial = _glowMaterial;
            _glow.SetActive(false);
        }

        public bool SetPreview(string base64)
        {
            if (_image == base64) return PreviewReady;
            _image = base64;
            PreviewReady = false;
            _material.SetFloat("_HasPreview", 0);
            if (_preview != null) { Destroy(_preview); _preview = null; }
            if (string.IsNullOrEmpty(base64) || base64.Length > 12000000) return false;
            Texture2D source = null;
            try
            {
                source = new Texture2D(2, 2, TextureFormat.RGB24, true);
                if (!ImageConversion.LoadImage(source, Convert.FromBase64String(base64)))
                    return false;
                // Keep one small texture per saved node instead of retaining full panoramas.
                if (source.width > 1024 && SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null)
                {
                    int height = Mathf.Max(1, Mathf.RoundToInt(1024f * source.height / source.width));
                    var target = RenderTexture.GetTemporary(1024, height, 0, RenderTextureFormat.ARGB32);
                    var previous = RenderTexture.active;
                    try
                    {
                        Graphics.Blit(source, target);
                        RenderTexture.active = target;
                        _preview = new Texture2D(1024, height, TextureFormat.RGB24, true);
                        _preview.ReadPixels(new Rect(0, 0, 1024, height), 0, 0);
                        _preview.Apply();
                    }
                    finally { RenderTexture.active = previous; RenderTexture.ReleaseTemporary(target); }
                }
                else { _preview = source; source = null; }
                _preview.name = "Crystal destination thumbnail";
                _preview.wrapModeU = TextureWrapMode.Repeat;
                _preview.wrapModeV = TextureWrapMode.Clamp;
                _preview.filterMode = FilterMode.Trilinear;
                _material.SetTexture("_PreviewTex", _preview);
                _material.SetFloat("_HasPreview", 1);
                PreviewReady = true;
                return true;
            }
            catch (Exception error) when (error is FormatException || error is ArgumentException)
            { return false; }
            finally { if (source != null) Destroy(source); }
        }

        public void SetEmphasis(bool held, bool selected)
        {
            _focus = Mathf.MoveTowards(_focus, held ? 1 : selected ? .45f : 0, Time.unscaledDeltaTime * 3);
            _material.SetFloat("_Focus", _focus);
        }

        public void SetRecording(bool active, float level = 0)
        {
            _material.SetFloat("_Recording", active ? 1 : 0);
            _glow.SetActive(active);
            if (active) _glowMaterial.SetFloat("_Level", Mathf.Clamp01(level * 20));
        }

        private void OnDestroy()
        {
            if (_preview != null) Destroy(_preview);
            if (_material != null) Destroy(_material);
            if (_glowMaterial != null) Destroy(_glowMaterial);
        }
    }
}
