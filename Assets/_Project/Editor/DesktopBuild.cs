using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ImmerseTheBay.Editor
{
    public static class DesktopBuild
    {
        [MenuItem("Immerse the Bay/Build Desktop App (Mac)")]
        public static void BuildMac()
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.StandaloneOSX)
                throw new BuildFailedException("Switch to macOS in File > Build Profiles first.");

            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(Zap.EditorTools.ZapSceneBuilder.VoidScenePath) == null)
                Zap.EditorTools.ZapSceneBuilder.BuildVoidScene();

            Directory.CreateDirectory("Builds/Desktop");
            // The desktop app is built with Mono because the IL2CPP module for Mac is not installed, and the shared setting is restored afterwards.
            var previousBackend = PlayerSettings.GetScriptingBackend(NamedBuildTarget.Standalone);
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
            BuildReport report;
            try
            {
                report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { Zap.EditorTools.ZapSceneBuilder.VoidScenePath },
                    target = BuildTarget.StandaloneOSX,
                    locationPathName = "Builds/Desktop/ZAP.app",
                    options = BuildOptions.Development,
                });
            }
            finally
            {
                PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, previousBackend);
            }

            if (report.summary.result != BuildResult.Succeeded)
                throw new BuildFailedException($"Desktop build failed: {report.summary.result} ({report.summary.totalErrors} errors).");

            Debug.Log($"Desktop app created: {Path.GetFullPath(report.summary.outputPath)}");
        }
    }
}
