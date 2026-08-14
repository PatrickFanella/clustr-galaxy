using System;
using System.Collections.Generic;
using ClustrGalaxy.Data;

namespace ClustrGalaxy.UI
{
    /// <summary>
    /// A bounded, data-only view of one inspected comment thread. It deliberately
    /// owns no scene objects: comment bodies remain the only physical discussion
    /// objects and replies are presented solely by the HUD.
    /// </summary>
    public sealed class ThreadInspectionModel
    {
        public const int DefaultMaximumDepth = 3;
        public const int DefaultMaximumNodes = 250;
        /// <summary>HUD presentation limit; logical inspection data retains all 250 nodes.</summary>
        public const int DefaultMaximumPresentationRows = 48;
        /// <summary>HUD presentation limit per visible reply row.</summary>
        public const int DefaultMaximumPresentationExcerptCharacters = 96;

        private ThreadInspectionModel(string rootId, IReadOnlyList<ThreadInspectionEntry> entries,
            int nodeCount, bool truncated)
        {
            RootId = rootId;
            Entries = entries;
            NodeCount = nodeCount;
            Truncated = truncated;
        }

        public string RootId { get; }
        public IReadOnlyList<ThreadInspectionEntry> Entries { get; }
        public int NodeCount { get; }
        public bool Truncated { get; }

        /// <summary>
        /// Selects a deterministic depth-first HUD slice without changing the
        /// complete bounded inspection model.
        /// </summary>
        public ThreadInspectionPresentation Presentation(
            int maximumRows = DefaultMaximumPresentationRows,
            int maximumExcerptCharacters = DefaultMaximumPresentationExcerptCharacters)
        {
            maximumRows = Math.Max(0, maximumRows);
            maximumExcerptCharacters = Math.Max(0, maximumExcerptCharacters);
            var rows = new List<ThreadInspectionPresentationRow>(Math.Min(NodeCount, maximumRows));
            AppendPresentationRows(Entries, maximumRows, maximumExcerptCharacters, rows);
            var renderCapped = rows.Count < NodeCount;
            return new ThreadInspectionPresentation(rows, NodeCount, renderCapped, Truncated || renderCapped);
        }

        public static ThreadInspectionModel Empty(string rootId = "") =>
            new(rootId ?? string.Empty, Array.Empty<ThreadInspectionEntry>(), 0, false);

        public static ThreadInspectionModel Build(SpatialScene scene, int maxDepth = DefaultMaximumDepth,
            int maxNodes = DefaultMaximumNodes)
        {
            if (scene == null) throw new ArgumentNullException(nameof(scene));
            return Build(scene.root_id, scene.nodes, scene.truncated, maxDepth, maxNodes);
        }

        public static ThreadInspectionModel Build(string rootId, IEnumerable<SpatialNode> sourceNodes,
            bool sourceTruncated = false, int maxDepth = DefaultMaximumDepth, int maxNodes = DefaultMaximumNodes)
        {
            rootId ??= string.Empty;
            maxDepth = Math.Max(0, maxDepth);
            maxNodes = Math.Max(0, maxNodes);

            var childrenByParent = new Dictionary<string, List<SpatialNode>>(StringComparer.Ordinal);
            foreach (var node in sourceNodes ?? Array.Empty<SpatialNode>())
            {
                if (node == null || node.type != "comment" || string.IsNullOrWhiteSpace(node.id) ||
                    string.Equals(node.id, rootId, StringComparison.Ordinal) || string.IsNullOrWhiteSpace(node.parent_id))
                {
                    continue;
                }

                if (!childrenByParent.TryGetValue(node.parent_id, out var children))
                {
                    children = new List<SpatialNode>();
                    childrenByParent.Add(node.parent_id, children);
                }
                children.Add(node);
            }

            foreach (var children in childrenByParent.Values)
            {
                children.Sort(CompareNodes);
            }

            var remaining = maxNodes;
            var truncated = sourceTruncated;
            var entries = BuildChildren(rootId, 1, childrenByParent, maxDepth, ref remaining, ref truncated);
            return new ThreadInspectionModel(rootId, entries, maxNodes - remaining, truncated);
        }

        private static IReadOnlyList<ThreadInspectionEntry> BuildChildren(string parentId, int depth,
            IReadOnlyDictionary<string, List<SpatialNode>> childrenByParent, int maxDepth,
            ref int remaining, ref bool truncated)
        {
            if (!childrenByParent.TryGetValue(parentId, out var children) || children.Count == 0)
            {
                return Array.Empty<ThreadInspectionEntry>();
            }

            if (depth > maxDepth || remaining == 0)
            {
                truncated = true;
                return Array.Empty<ThreadInspectionEntry>();
            }

            var entries = new List<ThreadInspectionEntry>(Math.Min(children.Count, remaining));
            for (var index = 0; index < children.Count; index++)
            {
                if (remaining == 0)
                {
                    truncated = true;
                    break;
                }

                var node = children[index];
                remaining--;
                var childEntries = BuildChildren(node.id, depth + 1, childrenByParent, maxDepth,
                    ref remaining, ref truncated);
                var knownChildren = childrenByParent.TryGetValue(node.id, out var known) ? known.Count : 0;
                var declaredReplies = Math.Max(0L, node.metrics?.reply_count ?? 0L);
                var hasMore = knownChildren > childEntries.Count || declaredReplies > childEntries.Count;
                entries.Add(new ThreadInspectionEntry(node.id, node.author_name, node.metrics?.score ?? 0L,
                    Excerpt(node), depth, declaredReplies, hasMore, childEntries));
            }
            return entries;
        }

        private static int CompareNodes(SpatialNode left, SpatialNode right)
        {
            var score = (right.metrics?.score ?? 0L).CompareTo(left.metrics?.score ?? 0L);
            return score != 0 ? score : string.CompareOrdinal(left.id, right.id);
        }

        private static string Excerpt(SpatialNode node)
        {
            var value = !string.IsNullOrWhiteSpace(node.excerpt) ? node.excerpt : node.body;
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;
            const int maximumLength = 280;
            return value.Length <= maximumLength ? value : value.Substring(0, maximumLength - 1) + "…";
        }

        private static void AppendPresentationRows(IReadOnlyList<ThreadInspectionEntry> entries, int maximumRows,
            int maximumExcerptCharacters, List<ThreadInspectionPresentationRow> rows)
        {
            foreach (var entry in entries)
            {
                if (rows.Count >= maximumRows)
                {
                    return;
                }

                rows.Add(new ThreadInspectionPresentationRow(entry, PresentationExcerpt(entry.BodyExcerpt,
                    maximumExcerptCharacters)));
                AppendPresentationRows(entry.Children, maximumRows, maximumExcerptCharacters, rows);
                if (rows.Count >= maximumRows)
                {
                    return;
                }
            }
        }

        private static string PresentationExcerpt(string excerpt, int maximumCharacters)
        {
            if (maximumCharacters <= 0 || string.IsNullOrWhiteSpace(excerpt)) return string.Empty;
            if (excerpt.Length <= maximumCharacters) return excerpt;
            return maximumCharacters == 1 ? "…" : excerpt.Substring(0, maximumCharacters - 1) + "…";
        }
    }

    public sealed class ThreadInspectionPresentation
    {
        public ThreadInspectionPresentation(IReadOnlyList<ThreadInspectionPresentationRow> rows, int totalNodeCount,
            bool isRenderCapped, bool isPartial)
        {
            Rows = rows ?? Array.Empty<ThreadInspectionPresentationRow>();
            TotalNodeCount = Math.Max(0, totalNodeCount);
            IsRenderCapped = isRenderCapped;
            IsPartial = isPartial;
        }

        public IReadOnlyList<ThreadInspectionPresentationRow> Rows { get; }
        public int TotalNodeCount { get; }
        public bool IsRenderCapped { get; }
        public bool IsPartial { get; }
    }

    public sealed class ThreadInspectionPresentationRow
    {
        public ThreadInspectionPresentationRow(ThreadInspectionEntry entry, string excerpt)
        {
            Entry = entry ?? throw new ArgumentNullException(nameof(entry));
            Excerpt = excerpt ?? string.Empty;
        }

        public ThreadInspectionEntry Entry { get; }
        public string StableId => Entry.StableId;
        public string Excerpt { get; }
    }

    public sealed class ThreadInspectionEntry
    {
        public ThreadInspectionEntry(string stableId, string author, long score, string bodyExcerpt, int depth,
            long replyCount, bool hasMore, IReadOnlyList<ThreadInspectionEntry> children)
        {
            StableId = stableId ?? string.Empty;
            Author = author ?? string.Empty;
            Score = score;
            BodyExcerpt = bodyExcerpt ?? string.Empty;
            Depth = depth;
            ReplyCount = Math.Max(0L, replyCount);
            HasMore = hasMore;
            Children = children ?? Array.Empty<ThreadInspectionEntry>();
        }

        public string StableId { get; }
        public string Author { get; }
        public long Score { get; }
        public string BodyExcerpt { get; }
        public int Depth { get; }
        public long ReplyCount { get; }
        public bool HasMore { get; }
        public IReadOnlyList<ThreadInspectionEntry> Children { get; }
    }
}
