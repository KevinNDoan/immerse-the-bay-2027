using System;
using System.Collections.Generic;

namespace Zap.Constellation
{
    public static class SuggestionPicker
    {
        public const int MaxSuggestions = 3;

        public static string[] Pick(NodeData node, IEnumerable<NodeData> allNodes, int max = MaxSuggestions)
        {
            if (node == null || node.suggestions == null || max <= 0) return Array.Empty<string>();

            var blocked = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (node.topic != null)
            {
                var own = node.topic.Trim();
                if (own.Length > 0) blocked.Add(own);
            }

            if (allNodes != null)
            {
                foreach (var other in allNodes)
                {
                    if (other == null || other.topic == null) continue;
                    var topic = other.topic.Trim();
                    if (topic.Length == 0) continue;
                    blocked.Add(topic);
                }
            }

            var picked = new List<string>(Math.Min(max, node.suggestions.Length));
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var suggestion in node.suggestions)
            {
                if (picked.Count >= max) break;
                if (suggestion == null) continue;
                var text = suggestion.Trim();
                if (text.Length == 0) continue;
                if (blocked.Contains(text) || !seen.Add(text)) continue;
                picked.Add(text);
            }

            return picked.ToArray();
        }

        public static string[] Without(string[] suggestions, string topic)
        {
            if (suggestions == null) return Array.Empty<string>();
            if (string.IsNullOrEmpty(topic)) return (string[])suggestions.Clone();

            var needle = topic.Trim();
            if (needle.Length == 0) return (string[])suggestions.Clone();

            var kept = new List<string>(suggestions.Length);
            foreach (var suggestion in suggestions)
            {
                if (suggestion != null && string.Equals(suggestion.Trim(), needle, StringComparison.OrdinalIgnoreCase)) continue;
                kept.Add(suggestion);
            }

            return kept.ToArray();
        }
    }
}
