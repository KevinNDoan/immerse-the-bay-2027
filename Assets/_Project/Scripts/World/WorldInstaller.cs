using UnityEngine;
using Zap.Net;

namespace Zap.World
{
    public static class WorldInstaller
    {
        public const string RootGameObjectName = "[ZAP World]";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        public static void Install()
        {
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "Void") return;
            if (GameObject.Find(RootGameObjectName) != null)
            {
                return;
            }

            var root = new GameObject(RootGameObjectName);
            Object.DontDestroyOnLoad(root);

            root.AddComponent<RelayClient>();
            root.AddComponent<DomeView>();
            root.AddComponent<WorldStream>();
            root.AddComponent<WorldSession>();
            root.AddComponent<WorldAudio>();

            // ConstellationController owns product input; avoid duplicate debug steering.
        }
    }
}
