using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
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
            if (!onlyEmpty || theme.linkSound == null)
            {
                var zap = AssetDatabase.LoadAssetAtPath<AudioClip>(AudioFolder + "zap_connect.wav");
                if (zap != null && zap != theme.linkSound) { theme.linkSound = zap; changed = true; }
            }
            if (changed)
            {
                EditorUtility.SetDirty(theme);
                AssetDatabase.SaveAssetIfDirty(theme);
            }
            return changed;
        }

        public const string FontFolder = "Assets/_Project/Art/Fonts/";
        public const string AudioFolder = "Assets/_Project/Audio/";
        private static readonly string[] FontWeights = { "Regular", "SemiBold" };

        [MenuItem("ZAP/Build Font Assets")]
        public static void BuildFontAssetsMenu() => BuildFontAssets(false);

        /// <summary>
        /// Makes a dynamic TextMeshPro font asset for each Bricolage Grotesque weight the app uses,
        /// then sets the Regular one as the theme font. Returns true if anything changed.
        /// </summary>
        public static bool BuildFontAssets(bool onlyMissing)
        {
            bool changed = false;
            TMP_FontAsset regular = null;
            foreach (var weight in FontWeights)
            {
                string fontPath = FontFolder + "BricolageGrotesque-" + weight + ".ttf";
                string assetPath = FontFolder + "BricolageGrotesque-" + weight + " SDF.asset";
                var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
                if (existing != null && onlyMissing)
                {
                    if (weight == "Regular") regular = existing;
                    continue;
                }
                var font = AssetDatabase.LoadAssetAtPath<Font>(fontPath);
                if (font == null) { Debug.LogWarning($"ZAP: font file missing at {fontPath}."); continue; }
                if (existing != null) AssetDatabase.DeleteAsset(assetPath);

                var asset = TMP_FontAsset.CreateFontAsset(font, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024,
                    AtlasPopulationMode.Dynamic, true);
                if (asset == null) { Debug.LogError($"ZAP: could not create a font asset from {fontPath}."); continue; }
                asset.name = "BricolageGrotesque-" + weight + " SDF";
                // Pre-fill the characters the app shows most, so the first frame does not stall.
                asset.TryAddCharacters(" !\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~·—–’“”…●", out _);

                AssetDatabase.CreateAsset(asset, assetPath);
                for (int i = 0; i < asset.atlasTextures.Length; i++)
                {
                    var atlas = asset.atlasTextures[i];
                    if (atlas == null) continue;
                    atlas.name = asset.name + " Atlas" + (i > 0 ? " " + i : "");
                    AssetDatabase.AddObjectToAsset(atlas, asset);
                }
                asset.material.name = asset.name + " Material";
                AssetDatabase.AddObjectToAsset(asset.material, asset);
                EditorUtility.SetDirty(asset);
                AssetDatabase.SaveAssetIfDirty(asset);
                Debug.Log($"ZAP: font asset created at {assetPath}.");
                if (weight == "Regular") regular = asset;
                changed = true;
            }

            var theme = AssetDatabase.LoadAssetAtPath<ZapTheme>(ThemeAssetPath);
            if (theme != null && regular != null && theme.font != regular)
            {
                theme.font = regular;
                EditorUtility.SetDirty(theme);
                AssetDatabase.SaveAssetIfDirty(theme);
                Debug.Log("ZAP: theme font set to Bricolage Grotesque Regular.");
                changed = true;
            }
            return changed;
        }

        public const string SkyMaterialPath = "Assets/_Project/Materials/ZapSky.mat";
        // Unity's six-sided skybox: +Z front, -Z back, -X left, +X right, +Y up, -Y down.
        private static readonly (string property, string file)[] SkyFaces =
        {
            ("_FrontTex", "sky_front.png"), ("_BackTex", "sky_back.png"),
            ("_LeftTex", "sky_left.png"), ("_RightTex", "sky_right.png"),
            ("_UpTex", "sky_up.png"), ("_DownTex", "sky_down.png"),
        };

        [MenuItem("ZAP/Build Sky")]
        public static void BuildSkyMenu() => BuildSky(false);

        /// <summary>
        /// Builds the six-sided sky material from Art/Sky and sets it as the theme's skybox.
        /// Returns true if anything changed.
        /// </summary>
        public static bool BuildSky(bool onlyMissing)
        {
            var theme = AssetDatabase.LoadAssetAtPath<ZapTheme>(ThemeAssetPath);
            if (theme == null) return false;
            var material = AssetDatabase.LoadAssetAtPath<Material>(SkyMaterialPath);
            if (material != null && onlyMissing && theme.skybox == material && theme.skyPanorama != null) return false;

            var faces = new Texture2D[SkyFaces.Length];
            for (int i = 0; i < SkyFaces.Length; i++)
            {
                faces[i] = AssetDatabase.LoadAssetAtPath<Texture2D>(ZapArtImporter.SkyFolder + SkyFaces[i].file);
                if (faces[i] == null) { Debug.LogWarning($"ZAP sky: {SkyFaces[i].file} is missing from {ZapArtImporter.SkyFolder}."); return false; }
            }

            var shader = Shader.Find("Skybox/6 Sided");
            if (shader == null) { Debug.LogError("ZAP sky: shader Skybox/6 Sided not found."); return false; }
            if (material == null)
            {
                EnsureFolder("Assets/_Project/Materials");
                material = new Material(shader) { name = "ZapSky" };
                AssetDatabase.CreateAsset(material, SkyMaterialPath);
            }
            material.shader = shader;
            for (int i = 0; i < SkyFaces.Length; i++) material.SetTexture(SkyFaces[i].property, faces[i]);
            material.SetColor("_Tint", new Color(.5f, .5f, .5f, .5f)); // neutral: shows the images as drawn
            material.SetFloat("_Exposure", 1f);
            material.SetFloat("_Rotation", 0f);
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssetIfDirty(material);

            theme.skybox = material;
            var panorama = AssetDatabase.LoadAssetAtPath<Texture2D>(ZapArtImporter.SkyFolder + "sky_panorama.png");
            if (panorama != null) theme.skyPanorama = panorama;
            EditorUtility.SetDirty(theme);
            AssetDatabase.SaveAssetIfDirty(theme);
            Debug.Log($"ZAP sky: {SkyMaterialPath} built from Art/Sky and set as the theme skybox.");
            return true;
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
