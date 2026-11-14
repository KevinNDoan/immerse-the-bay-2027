using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Zap.Constellation
{
    public static class NodeContext
    {
        // Traverse real parent links, not spatial proximity or a global chat history.
        public static NodeData[] Parents(NodeData node, IEnumerable<NodeData> nodes)
        {
            var byId = nodes.Where(n => n != null && !string.IsNullOrEmpty(n.id)).GroupBy(n => n.id).ToDictionary(g => g.Key, g => g.First());
            var visited = new HashSet<string> { node.id };
            var result = new List<NodeData>();
            void Visit(NodeData child, int depth)
            {
                if (depth >= 6 || child.parentIds == null) return;
                foreach (var id in child.parentIds)
                {
                    if (string.IsNullOrEmpty(id) || !visited.Add(id) || !byId.TryGetValue(id, out var parent)) continue;
                    Visit(parent, depth + 1);
                    result.Add(parent);
                }
            }
            Visit(node, 0);
            // Keep the nearest six ancestors if a large graph exceeds the prompt budget.
            return result.Skip(System.Math.Max(0, result.Count - 6)).ToArray();
        }

        public static string WorldPrompt(NodeData node, IEnumerable<NodeData> nodes)
        {
            var parents = Parents(node, nodes);
            var text = new StringBuilder();
            text.AppendLine("Current study topic: " + Clip(node.topic, 160));
            text.AppendLine(Clip(node.worldPrompt, 1500));
            if (parents.Length > 0)
            {
                text.AppendLine("The following is the student's current learning path. Make the scene specifically relevant to these preceding topics and their relationships to the current topic:");
                foreach (var parent in parents)
                    text.AppendLine("- " + Clip(parent.topic, 100) + ": " + Clip(parent.summary, 180));
            }
            text.Append("Show a coherent environment about the current topic in this context, not unrelated scenery. Slow steady camera, no text overlays, a single unbroken take.");
            return text.ToString();
        }

        public static bool WouldCycle(NodeData parent, NodeData child, IEnumerable<NodeData> nodes)
        {
            var map = nodes.Where(n => n != null).GroupBy(n => n.id).ToDictionary(g => g.Key, g => g.First());
            var pending = new Stack<NodeData>(); pending.Push(parent);
            var seen = new HashSet<string>();
            while (pending.Count > 0)
            {
                var next = pending.Pop();
                if (next.id == child.id) return true;
                if (!seen.Add(next.id) || next.parentIds == null) continue;
                foreach (var id in next.parentIds) if (id != null && map.TryGetValue(id, out var ancestor)) pending.Push(ancestor);
            }
            return false;
        }
        private static string Clip(string text, int max) => string.IsNullOrEmpty(text) ? "" : text.Substring(0, System.Math.Min(text.Length, max));
    }
}
