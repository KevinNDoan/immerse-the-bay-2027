using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Zap.Core
{
    public sealed class VoidEnvironment : MonoBehaviour
    {
        const string VoidSceneName = "Void";
        const string RigName = "XR Origin (XR Rig)";

        static readonly string[] LocomotionPaths =
        {
            "Locomotion/Move",
            "Locomotion/Turn",
            "Locomotion/Teleportation",
            "Locomotion/Climb",
            "Locomotion/Gravity",
            "Locomotion/Jump",
            "Camera Offset/Left Controller/Teleport Interactor",
            "Camera Offset/Right Controller/Teleport Interactor",
        };

        static readonly string[] ControllerPaths =
        {
            "Camera Offset/Left Controller",
            "Camera Offset/Right Controller",
        };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if (SceneManager.GetActiveScene().name != VoidSceneName)
                return;

            new GameObject(nameof(VoidEnvironment)).AddComponent<VoidEnvironment>();
        }

        void Awake()
        {
            var disabled = new List<string>();
            var missing = new List<string>();

            ConfigureCamera(missing);
            ConfigureLighting();
            DisableLocomotion(disabled, missing);

            Debug.Log($"ZAP Void disabled: {string.Join(", ", disabled)}.");

            if (missing.Count > 0)
                Debug.LogWarning($"ZAP Void expected but did not find: {string.Join(", ", missing)}.");
        }

        static void ConfigureCamera(List<string> missing)
        {
            var mainCamera = Camera.main;
            if (mainCamera == null)
            {
                missing.Add("Main Camera");
                return;
            }

            mainCamera.clearFlags = CameraClearFlags.SolidColor;
            mainCamera.backgroundColor = ZapColors.Void;
        }

        static void ConfigureLighting()
        {
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = ZapColors.VoidLift;
        }

        static void DisableLocomotion(List<string> disabled, List<string> missing)
        {
            var rig = GameObject.Find(RigName);
            if (rig == null)
            {
                missing.Add(RigName);
                return;
            }

            foreach (var path in LocomotionPaths)
            {
                var target = rig.transform.Find(path);
                if (target == null)
                {
                    missing.Add($"{RigName}/{path}");
                    continue;
                }

                target.gameObject.SetActive(false);
                disabled.Add($"{RigName}/{path}");
            }

            foreach (var path in ControllerPaths)
                DisableControllerActionManager(rig.transform, path, disabled, missing);
        }

        static void DisableControllerActionManager(
            Transform rig,
            string path,
            List<string> disabled,
            List<string> missing)
        {
            var controller = rig.Find(path);
            if (controller == null)
            {
                missing.Add($"{RigName}/{path}");
                return;
            }

            foreach (var behaviour in controller.GetComponents<MonoBehaviour>())
            {
                if (behaviour == null || behaviour.GetType().Name != "ControllerInputActionManager")
                    continue;

                behaviour.enabled = false;
                disabled.Add($"{RigName}/{path}/ControllerInputActionManager");
                return;
            }

            missing.Add($"{RigName}/{path}/ControllerInputActionManager");
        }
    }
}
