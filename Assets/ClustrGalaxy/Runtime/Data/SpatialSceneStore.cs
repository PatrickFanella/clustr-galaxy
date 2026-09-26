using System;
using System.Collections.Generic;
using System.Linq;

namespace ClustrGalaxy.Data
{
    public sealed class SpatialSceneStore
    {
        private readonly SpatialManifest manifest;
        private readonly Dictionary<string, SpatialNode> nodes = new(StringComparer.Ordinal);
        private readonly Dictionary<string, SpatialLink> links = new(StringComparer.Ordinal);
        private readonly HashSet<string> loadedScopes = new(StringComparer.Ordinal);

        public SpatialSceneStore(SpatialManifest pinnedManifest)
        {
            manifest = pinnedManifest ?? throw new ArgumentNullException(nameof(pinnedManifest));
            if (!manifest.IsCompatible)
            {
                throw new ArgumentException("A compatible pinned manifest is required.", nameof(pinnedManifest));
            }
        }

        public int NodeCount => nodes.Count;
        public int LinkCount => links.Count;

        public void Merge(SpatialScene scene) => MergeAll(new[] { scene });

        /// <summary>
        /// Merges every scene's nodes before any links, so a route whose endpoint
        /// arrives in another resident scope is kept regardless of merge order.
        /// </summary>
        public void MergeAll(IReadOnlyList<SpatialScene> scenes)
        {
            if (scenes == null) throw new ArgumentNullException(nameof(scenes));
            foreach (var scene in scenes)
            {
                if (scene == null)
                {
                    throw new ArgumentNullException(nameof(scenes));
                }
                if (!scene.BelongsTo(manifest))
                {
                    throw new InvalidOperationException("Cannot merge a scene from a different revision or spatial catalog.");
                }
            }

            foreach (var scene in scenes)
            {
                foreach (var node in scene.nodes ?? Array.Empty<SpatialNode>())
                {
                    if (node == null || string.IsNullOrWhiteSpace(node.id) || node.IsSuppressedAutomatedActivity)
                    {
                        continue;
                    }
                    nodes[node.id] = node;
                }
            }

            foreach (var scene in scenes)
            {
                foreach (var link in scene.links ?? Array.Empty<SpatialLink>())
                {
                    if (link == null || !nodes.ContainsKey(link.source) || !nodes.ContainsKey(link.target))
                    {
                        continue;
                    }
                    links[LinkKey(link)] = link;
                }

                if (!string.IsNullOrWhiteSpace(scene.scope) && !string.IsNullOrWhiteSpace(scene.root_id))
                {
                    loadedScopes.Add(ScopeKey(scene.scope, scene.root_id));
                }
            }
        }

        public SpatialNode GetNode(string stableId) => nodes[stableId];

        public bool TryGetNode(string stableId, out SpatialNode node) =>
            nodes.TryGetValue(stableId, out node);

        public bool IsScopeLoaded(string scope, string rootId) =>
            loadedScopes.Contains(ScopeKey(scope, rootId));

        public IReadOnlyCollection<SpatialNode> Nodes => nodes.Values;
        public IReadOnlyCollection<SpatialLink> Links => links.Values;

        /// <summary>Deterministic, bounded logical adjacency for interaction systems.</summary>
        public SpatialLink[] GetAdjacentLinks(string stableId, int maximum = 64)
        {
            if (string.IsNullOrWhiteSpace(stableId) || maximum <= 0) return Array.Empty<SpatialLink>();
            return links.Values
                .Where(link => string.Equals(link.source, stableId, StringComparison.Ordinal) ||
                               string.Equals(link.target, stableId, StringComparison.Ordinal))
                .OrderByDescending(link => link.weight)
                .ThenBy(LinkKey, StringComparer.Ordinal)
                .Take(maximum)
                .ToArray();
        }

        private static string ScopeKey(string scope, string rootId) => scope + "\u001f" + rootId;

        private static string LinkKey(SpatialLink link) =>
            link.source + "\u001f" + link.target + "\u001f" + link.type;
    }
}
