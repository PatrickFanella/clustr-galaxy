using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ClustrGalaxy.World
{
    /// <summary>
    /// Bounded scene adapter for gameplay systems that need entity and route
    /// collections. The adapter owns the one-off scene enumeration; gameplay
    /// rules receive this registry instead of searching the entire scene.
    /// </summary>
    public sealed class GalaxyEntityRegistry : MonoBehaviour
    {
        private readonly List<GalaxyEntity> entities = new();
        private readonly List<GalaxySurveyRoute> routes = new();

        public IReadOnlyList<GalaxyEntity> Entities => entities;
        public IReadOnlyList<GalaxySurveyRoute> Routes => routes;

        public void RefreshFromScene()
        {
            entities.Clear();
            entities.AddRange(UnityEngine.Object.FindObjectsByType<GalaxyEntity>(FindObjectsInactive.Include));
            routes.Clear();
            routes.AddRange(UnityEngine.Object.FindObjectsByType<GalaxySurveyRoute>(FindObjectsInactive.Include));
            entities.Sort((left, right) => string.CompareOrdinal(left.StableId, right.StableId));
            routes.Sort((left, right) => string.CompareOrdinal(left.StableId, right.StableId));
        }

        public GalaxyEntity FindEntity(string stableId)
        {
            if (string.IsNullOrWhiteSpace(stableId)) return null;
            return entities.FirstOrDefault(entity => string.Equals(entity.StableId, stableId, StringComparison.Ordinal));
        }
    }
}
