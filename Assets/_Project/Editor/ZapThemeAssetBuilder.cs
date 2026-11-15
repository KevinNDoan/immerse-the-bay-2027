using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using Zap.Core;

namespace Zap.EditorTools
{
    public static class ZapThemeAssetBuilder
    {
        public const string ThemeAssetPath = "Assets/_Project/Art/Theme/Resources/ZapTheme.asset";

        [MenuItem("ZAP/Create Theme Asset")]
        public static void CreateIfMissing()
        {
            try
            {
                if (File.Exists(ThemeAssetPath))
                    return;

                EnsureFolder("Assets/_Project/Art/Theme/Resources");
                var theme = ScriptableObject.CreateInstance<ZapTheme>();
                AssetDatabase.CreateAsset(theme, ThemeAssetPath);
                AssetDatabase.SaveAssets();
                if (AssetDatabase.LoadAssetAtPath<ZapTheme>(ThemeAssetPath) == null)
                    throw new InvalidOperationException($"Failed to create the ZAP theme asset at {ThemeAssetPath}.");
                Debug.Log($"ZAP theme asset created at {ThemeAssetPath}.");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);

                if (Application.isBatchMode)
                    EditorApplication.Exit(1);

                throw;
            }
        }

        [MenuItem("ZAP/Fill Theme Images")]
        public static void FillAllImages()
        {
            if (FillImages(false)) Debug.Log("ZAP theme images filled from Art/FX and Art/UI.");
            else Debug.Log("ZAP theme images already match Art/FX and Art/UI, or the theme asset is missing.");
        }

        /// <summary>Points the theme's image slots at the designer's exports. Returns true if anything changed.</summary>
        public static bool FillImages(bool onlyEmpty)
        {
            var theme = AssetDatabase.LoadAssetAtPath<ZapTheme>(ThemeAssetPath);
            if (theme == null) return false;
            bool changed = false;
            changed |= Assign(ref theme.nodeCore, ZapArtImporter.FxFolder + "star_core.png", onlyEmpty);
            changed |= Assign(ref theme.nodeGlow, ZapArtImporter.FxFolder + "star_glow.png", onlyEmpty);
            changed |= Assign(ref theme.nodeRing, ZapArtImporter.FxFolder + "star_ring.png", onlyEmpty);
            changed |= Assign(ref theme.nodeSparks, ZapArtImporter.FxFolder + "star_sparks.png", onlyEmpty);
            changed |= Assign(ref theme.nodeOrbit, ZapArtImporter.FxFolder + "star_orbit.png", onlyEmpty);
            changed |= Assign(ref theme.lineImage, ZapArtImporter.FxFolder + "line_soft.png", onlyEmpty);
            changed |= Assign(ref theme.noteBackground, ZapArtImporter.UiFolder + "note_compact.png", onlyEmpty);
            changed |= Assign(ref theme.noteBackgroundPointed, ZapArtImporter.UiFolder + "note_compact_pointed.png", onlyEmpty);
            changed |= Assign(ref theme.noteBackgroundHeld, ZapArtImporter.UiFolder + "note_compact_held.png", onlyEmpty);
            changed |= Assign(ref theme.noteExpanded, ZapArtImporter.UiFolder + "note_expanded.png", onlyEmpty);
            if (changed)
            {
                EditorUtility.SetDirty(theme);
                AssetDatabase.SaveAssetIfDirty(theme);
            }
            return changed;
        }

        private static bool Assign(ref Texture2D slot, string path, bool onlyEmpty)
        {
            if (onlyEmpty && slot != null) return false;
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (texture == null || texture == slot) return false;
            slot = texture;
            return true;
        }

        private static void EnsureFolder(string path)
        {
            var parts = path.Split('/');
            var parent = parts[0];
            for (var i = 1; i < parts.Length; i++)
            {
                var child = parent + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(child))
                {
                    AssetDatabase.CreateFolder(parent, parts[i]);
                    if (!AssetDatabase.IsValidFolder(child))
                        throw new InvalidOperationException($"Failed to create folder {child}.");
                }
                parent = child;
            }
        }
    }
}
