using System;
using ClustrGalaxy.Core.Domain;

namespace ClustrGalaxy.Core.Data
{
    public enum SpatialScope { Overview, Community, System, Discussion, Thread }

    public readonly struct SpatialScopeRequest : IEquatable<SpatialScopeRequest>
    {
        public SpatialScopeRequest(SpatialScope scope, GalaxyEntityId rootId = default)
        {
            if (scope != SpatialScope.Overview && rootId.IsEmpty)
                throw new ArgumentException("A bounded spatial scope requires a root entity ID.", nameof(rootId));
            Scope = scope;
            RootId = rootId;
        }

        public SpatialScope Scope { get; }
        public GalaxyEntityId RootId { get; }
        public bool Equals(SpatialScopeRequest other) => Scope == other.Scope && RootId.Equals(other.RootId);
        public override bool Equals(object obj) => obj is SpatialScopeRequest other && Equals(other);
        public override int GetHashCode() => ((int)Scope * 397) ^ RootId.GetHashCode();
        public override string ToString() => Scope == SpatialScope.Overview ? "overview" : $"{Scope}:{RootId}";
    }
}
