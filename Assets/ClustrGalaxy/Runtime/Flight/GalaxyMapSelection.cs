using System;
using System.Collections.Generic;
using UnityEngine;

namespace ClustrGalaxy.Flight
{
    public readonly struct GalaxyMapCandidate
    {
        public GalaxyMapCandidate(string stableId, string entityType, Vector2 viewportPosition, bool inFront)
        {
            StableId = stableId;
            EntityType = entityType;
            ViewportPosition = viewportPosition;
            InFront = inFront;
        }

        public string StableId { get; }
        public string EntityType { get; }
        public Vector2 ViewportPosition { get; }
        public bool InFront { get; }
    }

    public static class GalaxyMapSelection
    {
        public static bool IsSelectable(GalaxyNavigationLevel level, string entityType) => level switch
        {
            GalaxyNavigationLevel.Galaxy => entityType == "community",
            GalaxyNavigationLevel.Region => entityType == "subreddit",
            GalaxyNavigationLevel.System => entityType == "post",
            GalaxyNavigationLevel.Inspect => entityType == "comment",
            _ => false
        };

        public static string NearestId(
            GalaxyNavigationLevel level,
            Vector2 cursor,
            IReadOnlyList<GalaxyMapCandidate> candidates,
            float maximumRadius)
        {
            if (maximumRadius <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(maximumRadius));
            }

            var bestId = string.Empty;
            var bestDistance = maximumRadius * maximumRadius;
            for (var index = 0; index < candidates.Count; index++)
            {
                var candidate = candidates[index];
                if (!candidate.InFront || !IsSelectable(level, candidate.EntityType))
                {
                    continue;
                }

                var distance = (candidate.ViewportPosition - cursor).sqrMagnitude;
                if (distance <= bestDistance)
                {
                    bestDistance = distance;
                    bestId = candidate.StableId;
                }
            }

            return bestId;
        }
    }
}
