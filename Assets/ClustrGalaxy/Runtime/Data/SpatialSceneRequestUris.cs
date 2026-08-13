using System;

namespace ClustrGalaxy.Data
{
    public static class SpatialSceneRequestUris
    {
        private const string OverviewQuery =
            "graph/overview?level=0&max_nodes=300&max_links=180&with_positions=true";

        public static string Manifest(string apiBase) =>
            NormalizeBase(apiBase) + "graph/manifest";

        public static string Overview(string apiBase, SpatialManifest manifest) =>
            NormalizeBase(apiBase) + OverviewQuery + $"&revision={manifest.revision_id}";

        public static string LegacyOverview(string apiBase) =>
            NormalizeBase(apiBase) + OverviewQuery;

        public static string Community(string apiBase, SpatialManifest manifest, string communityId) =>
            NormalizeBase(apiBase) + "graph/community/" + Uri.EscapeDataString(communityId ?? string.Empty) +
            $"?lod=near&max_nodes=500&max_links=1500&revision={manifest.revision_id}&spatial_catalog_id={manifest.spatial_catalog_id}";

        public static string System(string apiBase, SpatialManifest manifest, string subredditId) =>
            NormalizeBase(apiBase) + "graph/entity/" + Uri.EscapeDataString(subredditId ?? string.Empty) +
            $"?expand=system&max_nodes=512&max_links=5000&revision={manifest.revision_id}&spatial_catalog_id={manifest.spatial_catalog_id}";

        public static string Discussion(string apiBase, SpatialManifest manifest, string postId) =>
            NormalizeBase(apiBase) + "graph/entity/" + Uri.EscapeDataString(postId ?? string.Empty) +
            $"?expand=discussion&max_nodes=512&max_links=5000&revision={manifest.revision_id}&spatial_catalog_id={manifest.spatial_catalog_id}";

        public static string Thread(string apiBase, SpatialManifest manifest, string commentId) =>
            NormalizeBase(apiBase) + "graph/entity/" + Uri.EscapeDataString(commentId ?? string.Empty) +
            $"?expand=thread&depth=3&max_nodes=250&max_links=1500&revision={manifest.revision_id}&spatial_catalog_id={manifest.spatial_catalog_id}";

        private static string NormalizeBase(string apiBase) =>
            (apiBase ?? string.Empty).TrimEnd('/') + "/";
    }
}
