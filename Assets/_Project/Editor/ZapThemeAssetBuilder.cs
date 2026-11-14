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
