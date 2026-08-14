using System;
using System.Collections.Generic;
using System.Linq;
using ClustrGalaxy.Core.Data;
using ClustrGalaxy.Core.Domain;

namespace ClustrGalaxy.Core.World
{
    public sealed class ResidentEntity
    {
        public ResidentEntity(GalaxyEntityId id, GalaxyEntityKind kind, GalaxyVisualKind visualKind,
            SpatialScopeRequest owner, string displayName, long value, GalaxyEntityId systemId = default)
        {
            Id = id; Kind = kind; VisualKind = visualKind; Owner = owner;
            DisplayName = displayName ?? string.Empty; Value = value; SystemId = systemId;
        }
        public GalaxyEntityId Id { get; }
        public GalaxyEntityKind Kind { get; }
        public GalaxyVisualKind VisualKind { get; }
        public SpatialScopeRequest Owner { get; }
        public string DisplayName { get; }
        public long Value { get; }
        public GalaxyEntityId SystemId { get; }
    }

    public sealed class ResidentRoute
    {
        public ResidentRoute(GalaxyRouteId id, GalaxyEntityId sourceId, GalaxyEntityId targetId, SpatialScopeRequest owner)
        { Id = id; SourceId = sourceId; TargetId = targetId; Owner = owner; }
        public GalaxyRouteId Id { get; }
        public GalaxyEntityId SourceId { get; }
        public GalaxyEntityId TargetId { get; }
        public SpatialScopeRequest Owner { get; }
    }

    public sealed class ResidentGalaxyChangeSet
    {
        internal ResidentGalaxyChangeSet(IEnumerable<GalaxyEntityId> added, IEnumerable<GalaxyEntityId> updated,
            IEnumerable<GalaxyEntityId> activated, IEnumerable<GalaxyEntityId> deactivated,
            IEnumerable<GalaxyEntityId> released, IEnumerable<GalaxyRouteId> routeChanges)
        {
            Added = Sort(added); Updated = Sort(updated); Activated = Sort(activated);
            Deactivated = Sort(deactivated); Released = Sort(released);
            RouteChanges = (routeChanges ?? Array.Empty<GalaxyRouteId>()).Distinct().OrderBy(value => value).ToArray();
        }
        public IReadOnlyList<GalaxyEntityId> Added { get; }
        public IReadOnlyList<GalaxyEntityId> Updated { get; }
        public IReadOnlyList<GalaxyEntityId> Activated { get; }
        public IReadOnlyList<GalaxyEntityId> Deactivated { get; }
        public IReadOnlyList<GalaxyEntityId> Released { get; }
        public IReadOnlyList<GalaxyRouteId> RouteChanges { get; }
        public bool IsEmpty => Added.Count + Updated.Count + Activated.Count + Deactivated.Count + Released.Count + RouteChanges.Count == 0;
        private static IReadOnlyList<GalaxyEntityId> Sort(IEnumerable<GalaxyEntityId> values) =>
            (values ?? Array.Empty<GalaxyEntityId>()).Distinct().OrderBy(value => value).ToArray();
    }

    public sealed class ResidentGalaxy
    {
        private readonly Dictionary<GalaxyEntityId, ResidentEntity> loaded = new();
        private readonly HashSet<GalaxyEntityId> active = new();
        private readonly Dictionary<GalaxyRouteId, ResidentRoute> routes = new();
        private readonly HashSet<GalaxyRouteId> activeRoutes = new();

        public IReadOnlyList<ResidentEntity> LoadedEntities => loaded.Values.OrderBy(value => value.Id).ToArray();
        public IReadOnlyList<ResidentEntity> ActiveEntities => active.Select(id => loaded[id]).OrderBy(value => value.Id).ToArray();
        public IReadOnlyList<ResidentRoute> LoadedRoutes => routes.Values.OrderBy(value => value.Id).ToArray();
        public bool TryFindLoaded(GalaxyEntityId id, out ResidentEntity entity) => loaded.TryGetValue(id, out entity);
        public bool TryFindActive(GalaxyEntityId id, out ResidentEntity entity)
        {
            if (active.Contains(id) && loaded.TryGetValue(id, out entity)) return true;
            entity = null;
            return false;
        }

        public ResidentGalaxyChangeSet ApplyProjection(SpatialScopeRequest scope, IEnumerable<ResidentEntity> entities,
            IEnumerable<ResidentRoute> projectedRoutes)
        {
            var incoming = (entities ?? Array.Empty<ResidentEntity>()).ToDictionary(value => value.Id);
            var incomingRoutes = (projectedRoutes ?? Array.Empty<ResidentRoute>()).ToDictionary(value => value.Id);
            var released = loaded.Values.Where(value => value.Owner.Equals(scope) && !incoming.ContainsKey(value.Id))
                .Select(value => value.Id).ToArray();
            var releasedRoutes = routes.Values.Where(value => value.Owner.Equals(scope) && !incomingRoutes.ContainsKey(value.Id))
                .Select(value => value.Id).ToArray();
            foreach (var id in released) { loaded.Remove(id); active.Remove(id); }
            foreach (var id in releasedRoutes) { routes.Remove(id); activeRoutes.Remove(id); }

            var added = new List<GalaxyEntityId>(); var updated = new List<GalaxyEntityId>();
            foreach (var pair in incoming)
            {
                if (loaded.ContainsKey(pair.Key)) updated.Add(pair.Key); else added.Add(pair.Key);
                loaded[pair.Key] = pair.Value;
                active.Add(pair.Key);
            }
            foreach (var pair in incomingRoutes) { routes[pair.Key] = pair.Value; activeRoutes.Add(pair.Key); }
            return new ResidentGalaxyChangeSet(added, updated, added, null, released,
                incomingRoutes.Keys.Concat(releasedRoutes));
        }

        public ResidentGalaxyChangeSet ApplySurveyVisibility(bool explore, IEnumerable<GalaxyEntityId> knownEntities,
            IEnumerable<GalaxyRouteId> knownRoutes)
        {
            var known = new HashSet<GalaxyEntityId>(knownEntities ?? Array.Empty<GalaxyEntityId>());
            var knownRouteSet = new HashSet<GalaxyRouteId>(knownRoutes ?? Array.Empty<GalaxyRouteId>());
            var nextActive = new HashSet<GalaxyEntityId>(explore ? loaded.Keys : loaded.Keys.Where(known.Contains));
            var nextRoutes = new HashSet<GalaxyRouteId>(explore ? routes.Keys : routes.Keys.Where(knownRouteSet.Contains));
            var activated = nextActive.Except(active).ToArray();
            var deactivated = active.Except(nextActive).ToArray();
            var routeChanges = nextRoutes.SymmetricDifference(activeRoutes);
            active.Clear(); active.UnionWith(nextActive);
            activeRoutes.Clear(); activeRoutes.UnionWith(nextRoutes);
            return new ResidentGalaxyChangeSet(null, null, activated, deactivated, null, routeChanges);
        }

        public ResidentGalaxyChangeSet Release(SpatialScopeRequest scope) => ApplyProjection(scope, null, null);
    }

    internal static class SetExtensions
    {
        public static IEnumerable<T> SymmetricDifference<T>(this HashSet<T> left, HashSet<T> right)
        {
            var result = new HashSet<T>(left);
            result.SymmetricExceptWith(right);
            return result.ToArray();
        }
    }
}
