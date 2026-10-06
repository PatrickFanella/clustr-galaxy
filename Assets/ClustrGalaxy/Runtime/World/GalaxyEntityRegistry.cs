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

        private GalaxyEntity[] entityView = Array.Empty<GalaxyEntity>();
        private GalaxyEntity[] activeView = Array.Empty<GalaxyEntity>();
        private GalaxySurveyRoute[] routeView = Array.Empty<GalaxySurveyRoute>();
        private bool entitiesDirty = true, activeDirty = true, routesDirty = true;
        public IReadOnlyList<GalaxyEntity> Entities
        {
            get { if (entitiesDirty) { entityView = entities.Values.OrderBy(value => value.StableId, StringComparer.Ordinal).ToArray(); entitiesDirty = false; } return entityView; }
        }
        public IReadOnlyList<GalaxyEntity> ActiveEntities
        {
            get { if (activeDirty) { activeView = activeEntityIds.Where(entities.ContainsKey).Select(id => entities[id]).OrderBy(value => value.StableId, StringComparer.Ordinal).ToArray(); activeDirty = false; } return activeView; }
        }
        public IReadOnlyList<GalaxySurveyRoute> Routes
        {
            get { if (routesDirty) { routeView = routes.Values.OrderBy(value => value.StableId, StringComparer.Ordinal).ToArray(); routesDirty = false; } return routeView; }
        }

        public void Register(GalaxyEntity entity)
        {
            if (entity == null || string.IsNullOrWhiteSpace(entity.StableId)) return;
            entities[entity.StableId] = entity;
            entitiesDirty = activeDirty = true;
            if (entity.isActiveAndEnabled) activeEntityIds.Add(entity.StableId);
            else activeEntityIds.Remove(entity.StableId);
        }

        public void MarkActive(GalaxyEntity entity) { Register(entity); }
        public void MarkInactive(GalaxyEntity entity)
        {
            if (entity != null && entities.TryGetValue(entity.StableId, out var current) && current == entity &&
                activeEntityIds.Remove(entity.StableId)) activeDirty = true;
        }
        public void Unregister(GalaxyEntity entity)
        {
            if (entity == null || string.IsNullOrWhiteSpace(entity.StableId)) return;
            if (!entities.TryGetValue(entity.StableId, out var current) || current != entity) return;
            entities.Remove(entity.StableId);
            activeEntityIds.Remove(entity.StableId);
            entitiesDirty = activeDirty = true;
        }
        public void Register(GalaxySurveyRoute route)
        { if (route != null && !string.IsNullOrWhiteSpace(route.StableId)) { routes[route.StableId] = route; routesDirty = true; } }
        public void Unregister(GalaxySurveyRoute route)
        { if (route != null && routes.TryGetValue(route.StableId, out var current) && current == route) { routes.Remove(route.StableId); routesDirty = true; } }

        public GalaxyEntity FindEntity(string stableId)
        {
            if (string.IsNullOrWhiteSpace(stableId)) return null;
            return entities.TryGetValue(stableId, out var entity) ? entity : null;
        }

        public GalaxyEntity FindActiveEntity(string stableId) =>
            activeEntityIds.Contains(stableId) ? FindEntity(stableId) : null;
    }
}
