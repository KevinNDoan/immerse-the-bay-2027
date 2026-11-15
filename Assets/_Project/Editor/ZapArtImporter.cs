using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Zap.EditorTools
{
    /// <summary>
    /// Sets import settings for the designer's images the first time Unity imports them,
    /// then fills any empty image slots in the theme.
    /// </summary>
    public sealed class ZapArtImporter : AssetPostprocessor
    {
        public const string FxFolder = "Assets/_Project/Art/FX/";
        public const string UiFolder = "Assets/_Project/Art/UI/";

        private static bool IsArt(string path) =>
            path.StartsWith(FxFolder, StringComparison.Ordinal) || path.StartsWith(UiFolder, StringComparison.Ordinal);

        public const string SkyFolder = "Assets/_Project/Art/Sky/";

        private void OnPreprocessTexture()
        {
            // Only on first import, so later changes in the Inspector are kept.
            if (!assetImporter.importSettingsMissing) return;
            if (assetPath.StartsWith(SkyFolder, StringComparison.Ordinal))
            {
                // Sky faces: opaque, clamped so the cube seams do not bleed, full size.
                var sky = (TextureImporter)assetImporter;
                sky.textureType = TextureImporterType.Default;
                sky.alphaSource = TextureImporterAlphaSource.None;
                sky.npotScale = TextureImporterNPOTScale.None;
                sky.mipmapEnabled = true;
                sky.maxTextureSize = 2048;
                sky.wrapMode = TextureWrapMode.Clamp;
                return;
            }
            if (!IsArt(assetPath)) return;
            var importer = (TextureImporter)assetImporter;
            bool fx = assetPath.StartsWith(FxFolder, StringComparison.Ordinal);
            importer.textureType = TextureImporterType.Default;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.mipmapEnabled = fx;
            importer.maxTextureSize = 2048;
            importer.wrapMode = TextureWrapMode.Clamp;
            var name = Path.GetFileNameWithoutExtension(assetPath);
            if (name == "line_soft" || name == "line_dash" || name.StartsWith("line_bolt", StringComparison.Ordinal))
            {
                importer.wrapModeU = TextureWrapMode.Repeat;
                importer.wrapModeV = TextureWrapMode.Clamp;
            }
        }

        private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            if (imported.Any(IsArt))
                EditorApplication.delayCall += () => ZapThemeAssetBuilder.FillImages(true);
            // Only the .ttf files trigger this, so the font assets it creates do not loop back here.
            if (imported.Any(p => p.StartsWith(ZapThemeAssetBuilder.FontFolder, StringComparison.Ordinal)
                                  && p.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase)))
                EditorApplication.delayCall += () => ZapThemeAssetBuilder.BuildFontAssets(true);
            if (imported.Any(p => p.StartsWith(SkyFolder, StringComparison.Ordinal) && p.EndsWith(".png", StringComparison.OrdinalIgnoreCase)))
                EditorApplication.delayCall += () => ZapThemeAssetBuilder.BuildSky(true);
        }
    }
}
