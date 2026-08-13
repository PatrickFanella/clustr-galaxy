using UnityEngine;
using System;
using System.Collections.Generic;

namespace ClustrGalaxy.World
{
    public sealed class GalaxyEntity : MonoBehaviour
    {
        private static readonly Dictionary<string, GalaxyEntity> Registry = new(StringComparer.Ordinal);

        public string StableId { get; private set; } = string.Empty;
        public string DisplayName { get; private set; } = string.Empty;
        public string EntityType { get; private set; } = string.Empty;
        public long Value { get; private set; }

        public void Initialize(string stableId, string displayName, string entityType, long value)
        {
            if (!string.IsNullOrWhiteSpace(StableId))
            {
                Registry.Remove(StableId);
            }
            StableId = stableId;
            DisplayName = displayName;
            EntityType = entityType;
            Value = value;
            gameObject.name = $"{entityType}: {displayName}";
            if (isActiveAndEnabled && !string.IsNullOrWhiteSpace(StableId))
            {
                Registry[StableId] = this;
            }
        }

        public static bool TryGet(string stableId, out GalaxyEntity entity) =>
            Registry.TryGetValue(stableId, out entity) && entity != null;

        public static IEnumerable<GalaxyEntity> ActiveEntities => Registry.Values;

        public void ClearIdentity()
        {
            RemoveFromRegistry();
            StableId = string.Empty;
            DisplayName = string.Empty;
            EntityType = string.Empty;
            Value = 0;
        }

        private void OnEnable()
        {
            if (!string.IsNullOrWhiteSpace(StableId)) Registry[StableId] = this;
        }

        private void OnDisable() => RemoveFromRegistry();

        private void OnDestroy() => RemoveFromRegistry();

        private void RemoveFromRegistry()
        {
            if (!string.IsNullOrWhiteSpace(StableId) && Registry.TryGetValue(StableId, out var current) && current == this)
            {
                Registry.Remove(StableId);
            }
        }
    }
}
