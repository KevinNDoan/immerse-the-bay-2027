using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEditor.XR.OpenXR.Features;
using UnityEngine;

namespace ImmerseTheBay.Editor
{
    public static class QuestBuild
    {
        private const string RelayUrlAssetPath = "Assets/_Project/Scripts/Net/Resources/zap_relay_url.txt";

        // Without one of these the headset build has no controllers.
        private static readonly string[] ControllerProfiles =
        {
            "com.unity.openxr.feature.input.oculustouch",
            "com.unity.openxr.feature.input.metaquestplus",
            "com.unity.openxr.feature.input.metaquestpro",
        };
        private const string MetaQuestSupport = "com.unity.openxr.feature.metaquest";

        [MenuItem("Immerse the Bay/Build Quest Development APK")]
        public static void BuildDevelopment()
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
                throw new BuildFailedException("Switch to Android or Meta Quest in File > Build Profiles first.");

            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            EnsureQuestOpenXRFeatures();

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

        // The OpenXR package switches the Quest controller profiles off by itself when the project was last
        // saved under the "Meta Quest" build profile and is then opened under plain Android
        // (BuildProfileUtilityOpenXR, flag isMetaQuestInitialized). That build runs but has no controllers,
        // so switch them back on before every build and stop if that is not possible.
        private static void EnsureQuestOpenXRFeatures()
        {
            FeatureHelpers.RefreshFeatures(BuildTargetGroup.Android);
            bool changed = false;
            int controllers = 0;
            foreach (var id in ControllerProfiles)
                if (Enable(id, ref changed)) controllers++;
            Enable(MetaQuestSupport, ref changed);
            if (changed) AssetDatabase.SaveAssets();
            if (controllers == 0)
                throw new BuildFailedException("No Quest controller profile is available in the OpenXR settings for Android. The build would have no controllers.");
        }

        private static bool Enable(string featureId, ref bool changed)
        {
            var feature = FeatureHelpers.GetFeatureWithIdForBuildTarget(BuildTargetGroup.Android, featureId);
            if (feature == null)
            {
                Debug.LogWarning($"Quest build: the OpenXR feature {featureId} was not found for Android.");
                return false;
            }
            if (!feature.enabled)
            {
                feature.enabled = true;
                EditorUtility.SetDirty(feature);
                changed = true;
                Debug.LogWarning($"Quest build: switched the OpenXR feature {featureId} back on.");
            }
            return true;
        }
    }
}
