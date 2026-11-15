using System;
using System.IO;
using UnityEngine;

namespace Zap.Constellation
{
    [Serializable]
    public class NodeData
    {
        public string id = Guid.NewGuid().ToString("N");
        public string topic = "New topic";
        public string summary = "";
        public string worldPrompt = "";
        public string image = "";
        public string[] parentIds = Array.Empty<string>();
        public string imageContext = "";
        public string[] suggestions = Array.Empty<string>();
        public Vector3 position;
    }

    [Serializable] public class NodeEdge { public string from; public string to; }
    [Serializable] public class ConstellationData
    {
        public int version = 1;
        public string name = "";
        public string updated = "";
        public NodeData[] nodes = Array.Empty<NodeData>();
        public NodeEdge[] edges = Array.Empty<NodeEdge>();
    }

    public static class NodeStore
    {
        public static ConstellationData Load(string path)
        {
            return TryLoad(path, out var data) ? data : new ConstellationData();
        }

        internal static bool TryLoad(string path, out ConstellationData data)
        {
            foreach (var candidate in new[] { path, path + ".bak" })
            {
                if (!File.Exists(candidate)) continue;
                try
                {
                    data = JsonUtility.FromJson<ConstellationData>(File.ReadAllText(candidate));
                    if (data != null && data.version == 1 && data.nodes != null) return true;
                }
                catch (Exception exception) { Debug.LogWarning("Could not read saved constellation: " + exception.Message); }
            }
            data = null;
            return false;
        }

        public static void Save(string path, ConstellationData data)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path + ".tmp", JsonUtility.ToJson(data));
            if (File.Exists(path)) File.Replace(path + ".tmp", path, path + ".bak");
            else File.Move(path + ".tmp", path);
        }
    }
}
