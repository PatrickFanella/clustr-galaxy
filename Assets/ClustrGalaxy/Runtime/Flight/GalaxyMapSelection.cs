using System;
using System.Collections.Generic;
using ClustrGalaxy.Gameplay;
using ClustrGalaxy.World;
using UnityEngine;
using ClustrGalaxy.Core.Domain;

namespace ClustrGalaxy.Flight
{
    public readonly struct GalaxyMapCandidate
    {
        public GalaxyMapCandidate(string stableId, GalaxyEntityKind entityKind, Vector2 viewportPosition, bool inFront,
            bool known = true)
        {
            StableId = stableId;
            EntityKind = entityKind;
            ViewportPosition = viewportPosition;
            InFront = inFront;
            Known = known;
        }

        public string StableId { get; }
        public GalaxyEntityKind EntityKind { get; }
        public Vector2 ViewportPosition { get; }
        public bool InFront { get; }
        public bool Known { get; }
    }

    public static class GalaxyMapSelection
    {
        public static bool IsSelectable(GalaxyNavigationLevel level, GalaxyEntityKind entityKind) => level switch
        {
            GalaxyNavigationLevel.Galaxy => entityKind == GalaxyEntityKind.Community,
            GalaxyNavigationLevel.Region => entityKind == GalaxyEntityKind.Subreddit,
            GalaxyNavigationLevel.System => entityKind == GalaxyEntityKind.Post,
            GalaxyNavigationLevel.Inspect => entityKind == GalaxyEntityKind.Comment,
            _ => false
        };

        public static bool IsSelectable(GalaxyNavigationLevel level, GalaxyEntityKind entityKind,
            GalaxyWorldMode worldMode, bool known) =>
            IsSelectable(level, entityKind) && GalaxyDiscoveryVisibility.CanTarget(worldMode, known);

        public static string NearestId(
            GalaxyNavigationLevel level,
            Vector2 cursor,
            IReadOnlyList<GalaxyMapCandidate> candidates,
            float maximumRadius,
            GalaxyWorldMode worldMode = GalaxyWorldMode.Explore)
        {
            if (maximumRadius <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(maximumRadius));
            }

            return Nearest(level, cursor, candidates, maximumRadius, worldMode)?.StableId ?? string.Empty;
        }

        public static GalaxyMapCandidate? Nearest(
            GalaxyNavigationLevel level,
            Vector2 cursor,
            IReadOnlyList<GalaxyMapCandidate> candidates,
            float maximumRadius,
            GalaxyWorldMode worldMode = GalaxyWorldMode.Explore)
        {
            if (maximumRadius <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(maximumRadius));
            }
            if (candidates == null)
            {
                throw new ArgumentNullException(nameof(candidates));
            }
            var bestDistance = maximumRadius * maximumRadius;
            GalaxyMapCandidate? best = null;
            for (var index = 0; index < candidates.Count; index++)
            {
                var candidate = candidates[index];
                if (!candidate.InFront || !IsSelectable(level, candidate.EntityKind, worldMode, candidate.Known))
                {
                    continue;
                }

                var distance = (candidate.ViewportPosition - cursor).sqrMagnitude;
                if (distance <= bestDistance)
                {
                    bestDistance = distance;
                    best = candidate;
                }
            }

            return best;
        }
    }
}
