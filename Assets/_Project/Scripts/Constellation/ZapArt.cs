using System.Collections.Generic;
using UnityEngine;

namespace Zap.Constellation
{
    /// <summary>
    /// Turns the designer's images into sprites at their real size.
    /// Exports are 4x Figma and 1 Figma px is 0.5 mm, so 8000 export px make 1 m.
    /// </summary>
    internal static class ZapArt
    {
        public const float PixelsPerMetre = 8000f;
        private static readonly Dictionary<(Texture2D, float), Sprite> SpriteCache = new Dictionary<(Texture2D, float), Sprite>();
        private static Material _material;
        private static bool _materialMissing;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetCache()
        {
            SpriteCache.Clear();
            _material = null;
            _materialMissing = false;
        }

        /// <summary>Shared see-through material for images, or null if the shader is missing.</summary>
        public static Material Material
        {
            get
            {
                if (_material != null || _materialMissing) return _material;
                var shader = Shader.Find("Zap/Textured");
                if (shader == null)
                {
                    _materialMissing = true;
                    Debug.LogError("ZAP: shader Zap/Textured not found. Images fall back to the default sprite material.");
                    return null;
                }
                _material = new Material(shader) { name = "ZAP textured (runtime)" };
                return _material;
            }
        }

        /// <summary>A sprite for the texture. A border in export pixels makes it stretch as a 9-slice.</summary>
        public static Sprite GetSprite(Texture2D texture, float borderPixels = 0)
        {
            if (texture == null) return null;
            if (SpriteCache.TryGetValue((texture, borderPixels), out var sprite) && sprite != null) return sprite;
            sprite = UnityEngine.Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(.5f, .5f),
                PixelsPerMetre, 0, SpriteMeshType.FullRect, Vector4.one * borderPixels);
            sprite.name = texture.name;
            SpriteCache[(texture, borderPixels)] = sprite;
            return sprite;
        }

        /// <summary>A child image layer. Higher order draws on top. Returns null if the texture is not set.</summary>
        public static SpriteRenderer Layer(Transform parent, string name, Texture2D texture, int order, float borderPixels = 0)
        {
            if (texture == null) return null;
            var obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            var renderer = obj.AddComponent<SpriteRenderer>();
            renderer.sprite = GetSprite(texture, borderPixels);
            if (Material != null) renderer.sharedMaterial = Material;
            renderer.sortingOrder = order;
            if (borderPixels > 0) renderer.drawMode = SpriteDrawMode.Sliced;
            return renderer;
        }

        /// <summary>Scales a square layer so its image is this many metres across.</summary>
        public static void SetDiameter(SpriteRenderer renderer, float metres)
        {
            if (renderer == null || renderer.sprite == null) return;
            float native = renderer.sprite.rect.width / PixelsPerMetre;
            renderer.transform.localScale = Vector3.one * (metres / native);
        }

        public static void Show(SpriteRenderer renderer, bool visible)
        {
            if (renderer != null && renderer.enabled != visible) renderer.enabled = visible;
        }
    }
}
