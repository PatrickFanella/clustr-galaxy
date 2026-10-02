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

        private readonly Dictionary<SpatialScopeRequest, Dictionary<GalaxyEntityId, ResidentEntity>> entityScopes = new();
        private readonly Dictionary<SpatialScopeRequest, Dictionary<GalaxyRouteId, ResidentRoute>> routeScopes = new();
        private readonly Dictionary<string, HashSet<GalaxyEntityId>> references = new(StringComparer.Ordinal);
        private IReadOnlyList<ResidentEntity> loadedView = Array.Empty<ResidentEntity>();
        private IReadOnlyList<ResidentEntity> activeView = Array.Empty<ResidentEntity>();
        private IReadOnlyList<ResidentRoute> routeView = Array.Empty<ResidentRoute>();
        private bool explore = true;
        private HashSet<GalaxyEntityId> known = new();
        private HashSet<GalaxyRouteId> knownRoutes = new();

        public IReadOnlyList<ResidentEntity> LoadedEntities => loadedView;
        public IReadOnlyList<ResidentEntity> ActiveEntities => activeView;
        public IReadOnlyList<ResidentRoute> LoadedRoutes => routeView;
        public bool TryFindLoaded(GalaxyEntityId id, out ResidentEntity entity) => loaded.TryGetValue(id, out entity);
        public bool TryFindActive(GalaxyEntityId id, out ResidentEntity entity)
        {
            if (active.Contains(id) && loaded.TryGetValue(id, out entity)) return true;
            entity = null;
            return false;
        }

        public ResidentGalaxyChangeSet ApplyProjection(SpatialScopeRequest scope, IEnumerable<ResidentEntity> entities,
            IEnumerable<ResidentRoute> projectedRoutes, IEnumerable<SpatialScopeRequest> releasedScopes = null)
        {
            var incoming = (entities ?? Array.Empty<ResidentEntity>()).ToDictionary(value => value.Id);
            var incomingRoutes = (projectedRoutes ?? Array.Empty<ResidentRoute>()).ToDictionary(value => value.Id);
            foreach (var released in releasedScopes ?? Array.Empty<SpatialScopeRequest>())
            { entityScopes.Remove(released); routeScopes.Remove(released); }
            entityScopes[scope] = incoming;
            routeScopes[scope] = incomingRoutes;
            if (incoming.Count == 0) entityScopes.Remove(scope);
            if (incomingRoutes.Count == 0) routeScopes.Remove(scope);
            return Rebuild(incoming.Keys);
        }

        public ResidentGalaxyChangeSet SetReferences(string owner, IEnumerable<GalaxyEntityId> ids)
        {
            if (string.IsNullOrWhiteSpace(owner)) throw new ArgumentException("A reference owner is required.", nameof(owner));
            var next = new HashSet<GalaxyEntityId>((ids ?? Array.Empty<GalaxyEntityId>()).Where(id => !id.IsEmpty));
            if ((!references.TryGetValue(owner, out var previous) && next.Count == 0) || previous != null && previous.SetEquals(next))
                return new ResidentGalaxyChangeSet(null,null,null,null,null,null);
            if (next.Count == 0) references.Remove(owner); else references[owner] = next;
            return Rebuild(Array.Empty<GalaxyEntityId>());
        }

        private ResidentGalaxyChangeSet Rebuild(IEnumerable<GalaxyEntityId> updated)
        {
            var previousIds = new HashSet<GalaxyEntityId>(loaded.Keys);
            var previousActive = new HashSet<GalaxyEntityId>(active);
            var previousRoutes = new HashSet<GalaxyRouteId>(routes.Keys);
            var retainedIds = new HashSet<GalaxyEntityId>(references.Values.SelectMany(ids => ids));
            var retainedEntities = loaded.Values.Where(entity => retainedIds.Contains(entity.Id)).ToArray();
            var retainedRoutes = routes.Values.Where(route => retainedIds.Contains(route.SourceId) || retainedIds.Contains(route.TargetId)).ToArray();
            loaded.Clear(); routes.Clear();
            foreach (var projection in entityScopes.Values)
                foreach (var pair in projection) loaded[pair.Key] = pair.Value;
            foreach (var projection in routeScopes.Values)
                foreach (var pair in projection) routes[pair.Key] = pair.Value;
            foreach (var entity in retainedEntities) if (!loaded.ContainsKey(entity.Id)) loaded.Add(entity.Id, entity);
            foreach (var route in retainedRoutes)
                if (!routes.ContainsKey(route.Id) && loaded.ContainsKey(route.SourceId) && loaded.ContainsKey(route.TargetId)) routes.Add(route.Id, route);
            loadedView = loaded.Values.OrderBy(value => value.Id).ToArray();
            routeView = routes.Values.OrderBy(value => value.Id).ToArray();
            ApplySurveyVisibility(explore, known, knownRoutes);
            return new ResidentGalaxyChangeSet(loaded.Keys.Except(previousIds), updated.Intersect(previousIds),
                active.Except(previousActive), previousActive.Except(active), previousIds.Except(loaded.Keys),
                previousRoutes.Union(routes.Keys));
        }

        public ResidentGalaxyChangeSet ApplySurveyVisibility(bool explore, IEnumerable<GalaxyEntityId> knownEntities,
            IEnumerable<GalaxyRouteId> knownRoutes)
        {
            this.explore = explore;
            known = new HashSet<GalaxyEntityId>(knownEntities ?? Array.Empty<GalaxyEntityId>());
            var knownRouteSet = new HashSet<GalaxyRouteId>(knownRoutes ?? Array.Empty<GalaxyRouteId>());
            this.knownRoutes = knownRouteSet;
            var nextActive = new HashSet<GalaxyEntityId>(explore ? loaded.Keys : loaded.Keys.Where(known.Contains));
            var nextRoutes = new HashSet<GalaxyRouteId>(explore ? routes.Keys : routes.Keys.Where(knownRouteSet.Contains));
            var activated = nextActive.Except(active).ToArray();
            var deactivated = active.Except(nextActive).ToArray();
            var routeChanges = nextRoutes.SymmetricDifference(activeRoutes);
            active.Clear(); active.UnionWith(nextActive);
            activeRoutes.Clear(); activeRoutes.UnionWith(nextRoutes);
            activeView = active.Select(id => loaded[id]).OrderBy(value => value.Id).ToArray();
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
