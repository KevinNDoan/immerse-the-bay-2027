using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace Zap.EditorTools
{
    public static class ZapSceneBuilder
    {
        public const string VoidScenePath = "Assets/_Project/Scenes/Void.unity";

        const string RigPrefabPath =
            "Assets/Samples/XR Interaction Toolkit/3.5.1/Starter Assets/Prefabs/XR Origin (XR Rig).prefab";

        [MenuItem("ZAP/Build Void Scene")]
        public static void BuildVoidScene()
        {
            try
            {
                if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                    return;

                BuildVoidSceneInternal();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);

                if (Application.isBatchMode)
                    EditorApplication.Exit(1);

                throw;
            }
        }

        static void BuildVoidSceneInternal()
        {
            var rigPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(RigPrefabPath);
            if (rigPrefab == null)
                throw new InvalidOperationException($"XR rig prefab was not found at {RigPrefabPath}.");

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var rigInstance = PrefabUtility.InstantiatePrefab(rigPrefab) as GameObject;
            if (rigInstance == null)
                throw new InvalidOperationException("Failed to instantiate the XR rig prefab.");

            var interactionManager = new GameObject("XR Interaction Manager");
            interactionManager.AddComponent<XRInteractionManager>();

            var eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<EventSystem>();
            eventSystem.AddComponent<XRUIInputModule>();

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, VoidScenePath))
                throw new InvalidOperationException($"Failed to save the Void scene at {VoidScenePath}.");

            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(VoidScenePath, true),
            };

            AssetDatabase.SaveAssets();
            Debug.Log($"ZAP Void scene built at {VoidScenePath}.");
        }
    }
}
