using System;
using System.Collections.Generic;
using System.Linq;

namespace ClustrGalaxy.World
{
    /// <summary>
    /// Bounded scene adapter for gameplay systems that need entity and route
    /// collections. The adapter owns the one-off scene enumeration; gameplay
    /// rules receive this registry instead of searching the entire scene.
    /// </summary>
    public sealed class GalaxyEntityRegistry
    {
        private readonly Dictionary<string, GalaxyEntity> entities = new(StringComparer.Ordinal);
        private readonly HashSet<string> activeEntityIds = new(StringComparer.Ordinal);
        private readonly Dictionary<string, GalaxySurveyRoute> routes = new(StringComparer.Ordinal);

        public IReadOnlyList<GalaxyEntity> Entities => entities.Values.OrderBy(value => value.StableId, StringComparer.Ordinal).ToArray();
        public IReadOnlyList<GalaxyEntity> ActiveEntities => activeEntityIds.Where(entities.ContainsKey)
            .Select(id => entities[id]).OrderBy(value => value.StableId, StringComparer.Ordinal).ToArray();
        public IReadOnlyList<GalaxySurveyRoute> Routes => routes.Values.OrderBy(value => value.StableId, StringComparer.Ordinal).ToArray();

        public void Register(GalaxyEntity entity)
        {
            if (entity == null || string.IsNullOrWhiteSpace(entity.StableId)) return;
            entities[entity.StableId] = entity;
            if (entity.isActiveAndEnabled) activeEntityIds.Add(entity.StableId);
        }

        public void MarkActive(GalaxyEntity entity) { Register(entity); if (entity != null) activeEntityIds.Add(entity.StableId); }
        public void MarkInactive(GalaxyEntity entity) { if (entity != null) activeEntityIds.Remove(entity.StableId); }
        public void Unregister(GalaxyEntity entity)
        {
            if (entity == null || string.IsNullOrWhiteSpace(entity.StableId)) return;
            if (entities.TryGetValue(entity.StableId, out var current) && current == entity) entities.Remove(entity.StableId);
            activeEntityIds.Remove(entity.StableId);
        }
        public void Register(GalaxySurveyRoute route)
        { if (route != null && !string.IsNullOrWhiteSpace(route.StableId)) routes[route.StableId] = route; }
        public void Unregister(GalaxySurveyRoute route)
        { if (route != null && routes.TryGetValue(route.StableId, out var current) && current == route) routes.Remove(route.StableId); }

        public GalaxyEntity FindEntity(string stableId)
        {
            if (string.IsNullOrWhiteSpace(stableId)) return null;
            return entities.TryGetValue(stableId, out var entity) ? entity : null;
        }

        public GalaxyEntity FindActiveEntity(string stableId) =>
            activeEntityIds.Contains(stableId) ? FindEntity(stableId) : null;
    }
}
