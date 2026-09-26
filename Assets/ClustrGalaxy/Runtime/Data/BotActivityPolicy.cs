using System;
using System.Collections.Generic;

namespace ClustrGalaxy.Data
{
    public static class BotActivityPolicy
    {
        private const int BroadCommunityThreshold = 20;

        public static bool ShouldSuppress(
            string authorName,
            bool explicitlyAutomated,
            int distinctCommunityCount)
        {
            if (explicitlyAutomated)
            {
                return true;
            }

            var normalized = Normalize(authorName);
            if (normalized.Equals("automoderator", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (distinctCommunityCount < BroadCommunityThreshold)
            {
                return false;
            }

            return normalized.EndsWith("bot", StringComparison.OrdinalIgnoreCase) ||
                normalized.Contains("_bot", StringComparison.OrdinalIgnoreCase) ||
                normalized.Contains("-bot", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Removes suppressed automated activity, and routes touching it, from a
        /// validated scene before presentation. The scope root is always kept.
        /// </summary>
        public static SpatialScene WithoutSuppressedActivity(SpatialScene scene)
        {
            if (scene?.nodes == null) return scene;
            HashSet<string> suppressed = null;
            foreach (var node in scene.nodes)
            {
                if (node == null || !node.IsSuppressedAutomatedActivity ||
                    string.Equals(node.id, scene.root_id, StringComparison.Ordinal)) continue;
                (suppressed ??= new HashSet<string>(StringComparer.Ordinal)).Add(node.id);
            }
            if (suppressed == null) return scene;
            scene.nodes = Array.FindAll(scene.nodes, node => node == null || !suppressed.Contains(node.id));
            if (scene.links != null)
                scene.links = Array.FindAll(scene.links, link => link == null ||
                    !suppressed.Contains(link.source ?? string.Empty) && !suppressed.Contains(link.target ?? string.Empty));
            return scene;
        }

        private static string Normalize(string authorName)
        {
            var normalized = (authorName ?? string.Empty).Trim();
            if (normalized.StartsWith("u/", StringComparison.OrdinalIgnoreCase))
            {
                normalized = normalized.Substring(2);
            }
            else if (normalized.StartsWith("@", StringComparison.Ordinal))
            {
                normalized = normalized.Substring(1);
            }

            return normalized;
        }
    }
}
