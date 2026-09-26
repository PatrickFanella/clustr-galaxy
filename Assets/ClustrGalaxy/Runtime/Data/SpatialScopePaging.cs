using System;
using System.Collections.Generic;
using System.Linq;

namespace ClustrGalaxy.Data
{
    public readonly struct SpatialScopeLimits
    {
        public SpatialScopeLimits(int maximumNodes, int maximumLinks, int publishedPageNodes,
            int publishedPageLinks)
        {
            MaximumNodes = maximumNodes;
            MaximumLinks = maximumLinks;
            PublishedPageNodes = publishedPageNodes;
            PublishedPageLinks = publishedPageLinks;
        }

        public int MaximumNodes { get; }
        public int MaximumLinks { get; }
        public int PublishedPageNodes { get; }
        public int PublishedPageLinks { get; }
    }

    public enum SpatialScopeCompletion
    {
        Complete,
        ClientLimitReached,
        ContinuationUnavailable
    }

    /// <summary>
    /// Pure policy for revision-bound continuation requests. The server owns
    /// opaque cursor validation; the client owns cumulative memory ceilings.
    /// </summary>
    public static class SpatialScopePaging
    {
        public static SpatialScopeLimits For(string scope) => scope switch
        {
            "community" => new SpatialScopeLimits(5000, 15000, 500, 1500),
            "system" => new SpatialScopeLimits(5000, 15000, 512, 5000),
            "discussion" => new SpatialScopeLimits(2048, 15000, 512, 5000),
            "thread" => new SpatialScopeLimits(2000, 12000, 250, 1500),
            _ => throw new ArgumentOutOfRangeException(nameof(scope), scope, "Unknown bounded scope.")
        };

        public static string WithCursor(string pinnedUri, string cursor)
        {
            if (string.IsNullOrWhiteSpace(pinnedUri))
            {
                throw new ArgumentException("A pinned request URI is required.", nameof(pinnedUri));
            }
            if (string.IsNullOrWhiteSpace(cursor))
            {
                throw new ArgumentException("A continuation cursor is required.", nameof(cursor));
            }

            return pinnedUri + (pinnedUri.Contains("?", StringComparison.Ordinal) ? "&" : "?") +
                   "cursor=" + Uri.EscapeDataString(cursor);
        }

        public static bool CanAccept(SpatialScopeLimits limits, int acceptedNodes, int acceptedLinks,
            SpatialScene nextPage)
        {
            var nodes = nextPage?.nodes?.Length ?? 0;
            var links = nextPage?.links?.Length ?? 0;
            return acceptedNodes + nodes <= limits.MaximumNodes &&
                   acceptedLinks + links <= limits.MaximumLinks;
        }

        public static SpatialScopeCompletion CompletionFor(SpatialScene finalPage, SpatialScopeLimits limits,
            int acceptedNodes, int acceptedLinks)
        {
            if (acceptedNodes >= limits.MaximumNodes || acceptedLinks >= limits.MaximumLinks)
            {
                return SpatialScopeCompletion.ClientLimitReached;
            }
            if (finalPage != null && finalPage.truncated && string.IsNullOrWhiteSpace(finalPage.next_cursor))
            {
                return SpatialScopeCompletion.ContinuationUnavailable;
            }
            if (finalPage != null && string.IsNullOrWhiteSpace(finalPage.next_cursor) &&
                ((finalPage.nodes?.Length ?? 0) >= limits.PublishedPageNodes ||
                 (finalPage.links?.Length ?? 0) >= limits.PublishedPageLinks))
            {
                return SpatialScopeCompletion.ContinuationUnavailable;
            }
            return SpatialScopeCompletion.Complete;
        }

        public static SpatialScene Aggregate(SpatialManifest manifest, string expectedScope, string expectedRootId,
            IReadOnlyList<SpatialScene> pages, SpatialScopeCompletion completion = SpatialScopeCompletion.Complete)
        {
            if (manifest == null) throw new ArgumentNullException(nameof(manifest));
            if (pages == null || pages.Count == 0)
            {
                throw new ArgumentException("At least one bounded page is required.", nameof(pages));
            }

            var limits = For(expectedScope);
            var nodes = new Dictionary<string, SpatialNode>(StringComparer.Ordinal);
            var links = new Dictionary<string, SpatialLink>(StringComparer.Ordinal);
            var acceptedNodeCount = 0;
            var acceptedLinkCount = 0;
            for (var index = 0; index < pages.Count; index++)
            {
                var page = pages[index] ?? throw new InvalidOperationException("A bounded response page was null.");
                if (!page.BelongsTo(manifest))
                {
                    throw new InvalidOperationException("A bounded response page belongs to a different revision or catalog.");
                }
                SpatialSceneScopeContract.ApplyOrValidate(page, expectedScope, expectedRootId);
                if (!CanAccept(limits, acceptedNodeCount, acceptedLinkCount, page))
                {
                    throw new InvalidOperationException($"The {expectedScope} response exceeded its cumulative client ceiling.");
                }

                acceptedNodeCount += page.nodes?.Length ?? 0;
                acceptedLinkCount += page.links?.Length ?? 0;
                foreach (var node in page.nodes ?? Array.Empty<SpatialNode>())
                {
                    if (node != null && !string.IsNullOrWhiteSpace(node.id)) nodes[node.id] = node;
                }
                foreach (var link in page.links ?? Array.Empty<SpatialLink>())
                {
                    if (link == null) continue;
                    links[LinkKey(link)] = link;
                }
            }

            var finalPage = pages[pages.Count - 1];
            return new SpatialScene
            {
                revision_id = manifest.revision_id,
                spatial_catalog_id = manifest.spatial_catalog_id,
                scope = expectedScope,
                root_id = expectedRootId,
                nodes = nodes.Values.OrderBy(node => node.id, StringComparer.Ordinal).ToArray(),
                links = links.Values.OrderBy(LinkKey, StringComparer.Ordinal).ToArray(),
                // An aggregate that stopped early is incomplete; a retained cursor marks the client limit.
                truncated = finalPage.truncated || completion != SpatialScopeCompletion.Complete,
                next_cursor = completion == SpatialScopeCompletion.ClientLimitReached ? finalPage.next_cursor ?? string.Empty : string.Empty
            };
        }

        /// <summary>Completion of a stored aggregate, independent of its total size.</summary>
        public static SpatialScopeCompletion AggregateCompletion(SpatialScene aggregate)
        {
            if (aggregate == null) return SpatialScopeCompletion.Complete;
            if (!string.IsNullOrWhiteSpace(aggregate.next_cursor)) return SpatialScopeCompletion.ClientLimitReached;
            return aggregate.truncated ? SpatialScopeCompletion.ContinuationUnavailable : SpatialScopeCompletion.Complete;
        }

        public static string StatusSuffix(SpatialScopeCompletion completion) => completion switch
        {
            SpatialScopeCompletion.ClientLimitReached => " / CLIENT LIMIT REACHED",
            SpatialScopeCompletion.ContinuationUnavailable => " / SERVER DID NOT PUBLISH CONTINUATION",
            _ => string.Empty
        };

        private static string LinkKey(SpatialLink link) =>
            (link.source ?? string.Empty) + "\u001f" + (link.target ?? string.Empty) + "\u001f" +
            (link.type ?? string.Empty);
    }
}
