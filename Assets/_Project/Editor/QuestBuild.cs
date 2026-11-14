using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ImmerseTheBay.Editor
{
    public static class QuestBuild
    {
        [MenuItem("Immerse the Bay/Build Quest Development APK")]
        public static void BuildDevelopment()
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
                throw new BuildFailedException("Switch to Android or Meta Quest in File > Build Profiles first.");

            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            var scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray();
            if (scenes.Length == 0)
                throw new BuildFailedException("Add at least one enabled scene in File > Build Profiles > Scene List.");

            Directory.CreateDirectory("Builds/Quest");
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                target = BuildTarget.Android,
                locationPathName = "Builds/Quest/ImmerseTheBay.apk",
                options = BuildOptions.Development,
            });

            if (report.summary.result != BuildResult.Succeeded)
                throw new BuildFailedException($"Quest build failed: {report.summary.result} ({report.summary.totalErrors} errors).");

            Debug.Log($"Quest APK created: {Path.GetFullPath(report.summary.outputPath)}");
        }
    }
}
