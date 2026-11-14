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

            if (Application.isEditor || Debug.isDebugBuild)
            {
                root.AddComponent<WorldDebugKeys>();
            }
        }
    }
}
