using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ImmerseTheBay.Editor
{
    public static class QuestBuild
    {
        private const string RelayUrlAssetPath = "Assets/_Project/Scripts/Net/Resources/zap_relay_url.txt";

        [MenuItem("Immerse the Bay/Build Quest Development APK")]
        public static void BuildDevelopment()
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
                throw new BuildFailedException("Switch to Android or Meta Quest in File > Build Profiles first.");

            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(Zap.EditorTools.ZapSceneBuilder.VoidScenePath) == null)
                Zap.EditorTools.ZapSceneBuilder.BuildVoidScene();

            Directory.CreateDirectory("Builds/Quest");
            var previousRelayUrl = File.Exists(RelayUrlAssetPath) ? File.ReadAllText(RelayUrlAssetPath) : null;
            Zap.EditorTools.QuestNetworkSetup.ConfigureRelayAddress();
            var previousHttp = PlayerSettings.insecureHttpOption;
            PlayerSettings.insecureHttpOption = InsecureHttpOption.DevelopmentOnly;
            BuildReport report;
            try
            {
                report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { Zap.EditorTools.ZapSceneBuilder.VoidScenePath },
                    target = BuildTarget.Android,
                    locationPathName = "Builds/Quest/ImmerseTheBay.apk",
                    options = BuildOptions.Development,
                });
            }
            finally
            {
                PlayerSettings.insecureHttpOption = previousHttp;
                // The address is baked into the APK but must not stay in the working tree.
                if (previousRelayUrl != null)
                    File.WriteAllText(RelayUrlAssetPath, previousRelayUrl);
                else if (File.Exists(RelayUrlAssetPath))
                    File.WriteAllText(RelayUrlAssetPath, "");
                AssetDatabase.Refresh();
            }

            if (report.summary.result != BuildResult.Succeeded)
                throw new BuildFailedException($"Quest build failed: {report.summary.result} ({report.summary.totalErrors} errors).");

            Debug.Log($"Quest APK created: {Path.GetFullPath(report.summary.outputPath)}");
        }
    }
}
