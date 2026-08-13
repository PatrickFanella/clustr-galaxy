using System;
using System.Collections.Generic;

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

        public void Merge(SpatialScene scene)
        {
            if (scene == null)
            {
                throw new ArgumentNullException(nameof(scene));
            }
            if (!scene.BelongsTo(manifest))
            {
                throw new InvalidOperationException("Cannot merge a scene from a different revision or spatial catalog.");
            }

            foreach (var node in scene.nodes ?? Array.Empty<SpatialNode>())
            {
                if (node == null || string.IsNullOrWhiteSpace(node.id) || node.IsSuppressedAutomatedActivity)
                {
                    continue;
                }
                nodes[node.id] = node;
            }

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

        public SpatialNode GetNode(string stableId) => nodes[stableId];

        public bool TryGetNode(string stableId, out SpatialNode node) =>
            nodes.TryGetValue(stableId, out node);

        public bool IsScopeLoaded(string scope, string rootId) =>
            loadedScopes.Contains(ScopeKey(scope, rootId));

        public IReadOnlyCollection<SpatialNode> Nodes => nodes.Values;
        public IReadOnlyCollection<SpatialLink> Links => links.Values;

        private static string ScopeKey(string scope, string rootId) => scope + "\u001f" + rootId;

        private static string LinkKey(SpatialLink link) =>
            link.source + "\u001f" + link.target + "\u001f" + link.type;
    }
}
