using System;

namespace ClustrGalaxy.Data
{
    public static class SpatialSceneContract
    {
        public const string Version = "spatial-scene-v1";
    }

    [Serializable]
    public sealed class SpatialManifest
    {
        public long revision_id;
        public long spatial_catalog_id;
        public string source_watermark = string.Empty;
        public string published_at = string.Empty;
        public long node_count;
        public long resident_node_count;
        public long link_count;
        public long resident_link_count;
        public long community_count;
        public string layout_algorithm = string.Empty;
        public int dimensions;
        public SpatialBounds bounds = new();
        public int[] hierarchy_levels = Array.Empty<int>();
        public string[] supported_formats = Array.Empty<string>();
        public string scene_contract = string.Empty;
        public string bot_policy_version = string.Empty;
        public string metric_policy_version = string.Empty;
        public string catalog_checksum = string.Empty;
        public long excluded_user_count;
        public long excluded_post_count;
        public long excluded_comment_count;
        public SpatialNormalizationQuantiles normalization_quantiles = new();

        public bool IsCompatible =>
            revision_id > 0 &&
            dimensions == 3 &&
            scene_contract == SpatialSceneContract.Version &&
            bounds.IsFiniteAndOrdered;
    }

    [Serializable]
    public sealed class SpatialNormalizationQuantiles
    {
        public SpatialQuantile subreddit = new();
        public SpatialQuantile user = new();
        public SpatialQuantile post = new();
        public SpatialQuantile comment = new();
    }

    [Serializable]
    public sealed class SpatialQuantile
    {
        public long q05;
        public long q95;
    }

    [Serializable]
    public sealed class SpatialBounds
    {
        public float min_x;
        public float max_x;
        public float min_y;
        public float max_y;
        public float min_z;
        public float max_z;

        public bool IsFiniteAndOrdered =>
            IsFinite(min_x) && IsFinite(max_x) && min_x <= max_x &&
            IsFinite(min_y) && IsFinite(max_y) && min_y <= max_y &&
            IsFinite(min_z) && IsFinite(max_z) && min_z <= max_z;

        private static bool IsFinite(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);
    }

    [Serializable]
    public sealed class SpatialScene
    {
        public long revision_id;
        public long spatial_catalog_id;
        public SpatialNode[] nodes = Array.Empty<SpatialNode>();
        public SpatialLink[] links = Array.Empty<SpatialLink>();
        public string scope = string.Empty;
        public string root_id = string.Empty;
        public bool truncated;
        public string next_cursor = string.Empty;

        public bool BelongsTo(SpatialManifest manifest) =>
            manifest != null &&
            revision_id == manifest.revision_id &&
            (manifest.spatial_catalog_id == 0 || spatial_catalog_id == manifest.spatial_catalog_id);
    }

    [Serializable]
    public sealed class SpatialNode
    {
        public string id = string.Empty;
        public string name = string.Empty;
        public long val;
        public string type = string.Empty;
        public string author_name = string.Empty;
        public bool automated;
        public int distinct_communities;
        public string parent_id = string.Empty;
        public string anchor_id = string.Empty;
        public string author_id = string.Empty;
        public string community_id = string.Empty;
        public string position_provenance = string.Empty;
        public string source_updated_at = string.Empty;
        // Optional discussion text fields. Older graph responses omit both.
        public string body = string.Empty;
        public string excerpt = string.Empty;
        public SpatialNodeMetrics metrics = new();
        public float x;
        public float y;
        public float z;

        public bool IsSuppressedAutomatedActivity => BotActivityPolicy.ShouldSuppress(
            string.IsNullOrWhiteSpace(author_name) && type == "user" ? name : author_name,
            automated,
            distinct_communities);
    }

    [Serializable]
    public sealed class SpatialNodeMetrics
    {
        public long subscribers;
        public long post_count;
        public long comment_count;
        public long top_level_comment_count;
        public long reply_count;
        public long unique_users;
        public long distinct_communities;
        public long activity_count;
        public long score;
        public int depth;
        public string created_at = string.Empty;
        public string last_seen = string.Empty;
    }

    [Serializable]
    public sealed class SpatialLink
    {
        public string source = string.Empty;
        public string target = string.Empty;
        public string type = string.Empty;
        public bool directed;
        public long weight;
    }
}
