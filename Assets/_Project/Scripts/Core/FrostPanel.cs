using UnityEngine;
using UnityEngine.Rendering;

namespace Zap.Core
{
    /// <summary>
    /// A frosted-glass UI panel: a rounded quad drawn with Zap/Frost, styled from the theme's glass values.
    /// Place it just behind the text it backs (the side away from the viewer) and size it in metres.
    /// </summary>
    public sealed class FrostPanel : MonoBehaviour
    {
        static readonly int SizeId = Shader.PropertyToID("_Size");
        static readonly int RadiusId = Shader.PropertyToID("_Radius");
        static readonly int TintId = Shader.PropertyToID("_Tint");
        static readonly int SkyTexId = Shader.PropertyToID("_SkyTex");
        static readonly int SkyLodId = Shader.PropertyToID("_SkyLod");
        static readonly int FrostId = Shader.PropertyToID("_Frost");
        static readonly int GlowId = Shader.PropertyToID("_Glow");
        static readonly int GlowStrengthId = Shader.PropertyToID("_GlowStrength");
        static readonly int EdgeStrengthId = Shader.PropertyToID("_EdgeStrength");
        static readonly int OpacityId = Shader.PropertyToID("_Opacity");

        static Material _shared;
        static bool _shaderMissing;

        Renderer _renderer;
        MaterialPropertyBlock _block;
        Vector2 _size = new Vector2(-1f, -1f);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { _shared = null; _shaderMissing = false; }

        /// <summary>True when the theme asks for glass and the shader is available.</summary>
        public static bool Enabled => ZapTheme.Current.useFrostedGlass && SharedMaterial != null;

        static Material SharedMaterial
        {
            get
            {
                if (_shared != null || _shaderMissing) return _shared;
                var shader = Shader.Find("Zap/Frost");
                if (shader == null)
                {
                    _shaderMissing = true;
                    Debug.LogError("ZAP: shader Zap/Frost not found. UI panels fall back to their old look.");
                    return null;
                }
                _shared = new Material(shader) { name = "ZAP frosted glass (runtime)" };
                return _shared;
            }
        }

        /// <summary>Creates a panel under the parent, or returns null if glass is off or unavailable.</summary>
        public static FrostPanel Create(Transform parent, string name, Vector3 localPosition)
        {
            if (!Enabled) return null;
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = name;
            var collider = quad.GetComponent<Collider>();
            collider.enabled = false;
            Destroy(collider);
            quad.transform.SetParent(parent, false);
            quad.transform.localPosition = localPosition;
            var renderer = quad.GetComponent<Renderer>();
            renderer.sharedMaterial = SharedMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            var panel = quad.AddComponent<FrostPanel>();
            panel._renderer = renderer;
            panel._block = new MaterialPropertyBlock();
            panel.SetSize(new Vector2(.1f, .1f));
            return panel;
        }

        /// <summary>Panel size in metres. Cheap to call every frame; it only updates when the size changes.</summary>
        public void SetSize(Vector2 size)
        {
            if (size == _size) return;
            _size = size;
            transform.localScale = new Vector3(Mathf.Max(size.x, .001f), Mathf.Max(size.y, .001f), 1f);
            Apply();
        }

        /// <summary>Re-reads the theme's glass values, for example after tuning them in the Inspector.</summary>
        public void Apply()
        {
            if (_renderer == null) return;
            var theme = ZapTheme.Current;
            _block.SetVector(SizeId, new Vector4(_size.x, _size.y, 0f, 0f));
            _block.SetFloat(RadiusId, theme.glassCornerRadius);
            _block.SetColor(TintId, theme.glassTint);
            if (theme.skyPanorama != null) _block.SetTexture(SkyTexId, theme.skyPanorama);
            _block.SetFloat(SkyLodId, theme.glassSkyBlur);
            _block.SetFloat(FrostId, theme.skyPanorama != null ? theme.glassFrost : 0f);
            _block.SetFloat(GlowId, theme.glassGlowWidth);
            _block.SetFloat(GlowStrengthId, theme.glassGlowStrength);
            _block.SetFloat(EdgeStrengthId, theme.glassEdgeStrength);
            _block.SetFloat(OpacityId, theme.glassOpacity);
            _renderer.SetPropertyBlock(_block);
        }

        void OnEnable() { Apply(); }
    }
}
