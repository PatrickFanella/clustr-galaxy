using UnityEngine;
using ClustrGalaxy.Core.Domain;

namespace ClustrGalaxy.World
{
    public sealed class GalaxyEntity : MonoBehaviour
    {
        private GalaxyEntityRegistry registry;
        private SphereCollider surface;
        public float SurfaceRadius => surface != null ? surface.radius * Mathf.Max(Mathf.Abs(transform.lossyScale.x), Mathf.Max(Mathf.Abs(transform.lossyScale.y), Mathf.Abs(transform.lossyScale.z))) : 0f;
        public float SurfaceDistance(Vector3 point) => Mathf.Max(0f, Vector3.Distance(point, transform.position) - SurfaceRadius);

        public string StableId { get; private set; } = string.Empty;
        public string DisplayName { get; private set; } = string.Empty;
        public string EntityType { get; private set; } = string.Empty;
        public GalaxyEntityKind Kind { get; private set; }
        public GalaxyVisualKind VisualKind { get; private set; }
        public long Value { get; private set; }

        public void Initialize(string stableId, string displayName, string entityType, long value,
            GalaxyEntityRegistry entityRegistry = null)
        {
            registry?.Unregister(this);
            surface = GetComponent<SphereCollider>();
            StableId = stableId;
            DisplayName = displayName;
            EntityType = entityType;
            (Kind, VisualKind) = GalaxyEntitySemantics.FromVisualLabel(entityType);
            Value = value;
            gameObject.name = $"{entityType}: {displayName}";
            registry = entityRegistry;
            registry?.Register(this);
        }

        public void ClearIdentity()
        {
            registry?.Unregister(this);
            StableId = string.Empty;
            DisplayName = string.Empty;
            EntityType = string.Empty;
            Kind = default;
            VisualKind = default;
            Value = 0;
        }

        private void OnEnable()
        {
            registry?.MarkActive(this);
        }

        private void OnDisable() => registry?.MarkInactive(this);

        private void OnDestroy() => registry?.Unregister(this);
    }

    public static class GalaxyEntitySemantics
    {
        public static (GalaxyEntityKind Kind, GalaxyVisualKind Visual) FromVisualLabel(string value)
        {
            if (value == "community" || value == "community region") return (GalaxyEntityKind.Community, GalaxyVisualKind.Region);
            if (value == "subreddit" || value == "subreddit star") return (GalaxyEntityKind.Subreddit, GalaxyVisualKind.Star);
            if (value == "post" || value == "post planet") return (GalaxyEntityKind.Post, GalaxyVisualKind.Planet);
            if (value == "user" || value == "user ship") return (GalaxyEntityKind.User, GalaxyVisualKind.Ship);
            if (value != null && (value.Contains("comment") || value.Contains("reply") || value == "discussion satellite"))
                return (GalaxyEntityKind.Comment,
                    value.Contains("ring") || value.Contains("particle") ? GalaxyVisualKind.RingParticle : GalaxyVisualKind.Satellite);
            throw new System.ArgumentException($"Unknown galaxy visual label '{value}'.", nameof(value));
        }
    }
}
