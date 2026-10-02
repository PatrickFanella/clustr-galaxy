using System;

namespace ClustrGalaxy.Core.Domain
{
    public readonly struct GalaxyEntityId : IEquatable<GalaxyEntityId>, IComparable<GalaxyEntityId>
    {
        public GalaxyEntityId(string value)
        {
            Value = Require(value, nameof(value));
        }

        public string Value { get; }
        public bool IsEmpty => string.IsNullOrEmpty(Value);
        public int CompareTo(GalaxyEntityId other) => string.CompareOrdinal(Value, other.Value);
        public bool Equals(GalaxyEntityId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is GalaxyEntityId other && Equals(other);
        public override int GetHashCode() => Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
        public override string ToString() => Value ?? string.Empty;
        public static implicit operator string(GalaxyEntityId id) => id.Value;
        public static explicit operator GalaxyEntityId(string value) => new(value);
        public static bool operator ==(GalaxyEntityId left, GalaxyEntityId right) => left.Equals(right);
        public static bool operator !=(GalaxyEntityId left, GalaxyEntityId right) => !left.Equals(right);

        private static string Require(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("A stable galaxy entity ID is required.", parameterName);
            var normalized = value.Trim();
            if (normalized.IndexOf('\r') >= 0 || normalized.IndexOf('\n') >= 0 || normalized.IndexOf('\0') >= 0)
                throw new ArgumentException("A stable galaxy entity ID cannot contain control separators.", parameterName);
            return normalized;
        }
    }

    public readonly struct GalaxyRouteId : IEquatable<GalaxyRouteId>, IComparable<GalaxyRouteId>
    {
        public GalaxyRouteId(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("A stable galaxy route ID is required.", nameof(value));
            var normalized = value.Trim();
            if (normalized.IndexOf('\r') >= 0 || normalized.IndexOf('\n') >= 0 || normalized.IndexOf('\0') >= 0)
                throw new ArgumentException("A stable galaxy route ID cannot contain control separators.", nameof(value));
            Value = normalized;
        }

        public string Value { get; }
        public int CompareTo(GalaxyRouteId other) => string.CompareOrdinal(Value, other.Value);
        public bool Equals(GalaxyRouteId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is GalaxyRouteId other && Equals(other);
        public override int GetHashCode() => Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
        public override string ToString() => Value ?? string.Empty;
        public static implicit operator string(GalaxyRouteId id) => id.Value;
        public static explicit operator GalaxyRouteId(string value) => new(value);
        public static bool operator ==(GalaxyRouteId left, GalaxyRouteId right) => left.Equals(right);
        public static bool operator !=(GalaxyRouteId left, GalaxyRouteId right) => !left.Equals(right);
    }

    public enum GalaxyEntityKind { Community, Subreddit, Post, Comment, User }
    public enum GalaxyVisualKind { Region, Star, Planet, Satellite, RingParticle, Ship }

    public static class GalaxyKinds
    {
        public static GalaxyEntityKind ParseEntityKind(string value) => value switch
        {
            "community" => GalaxyEntityKind.Community,
            "subreddit" => GalaxyEntityKind.Subreddit,
            "post" => GalaxyEntityKind.Post,
            "comment" => GalaxyEntityKind.Comment,
            "user" => GalaxyEntityKind.User,
            _ => throw new ArgumentException($"Unknown factual galaxy entity kind '{value}'.", nameof(value))
        };

        public static GalaxyVisualKind VisualFor(GalaxyEntityKind kind, bool significantComment = true) => kind switch
        {
            GalaxyEntityKind.Community => GalaxyVisualKind.Region,
            GalaxyEntityKind.Subreddit => GalaxyVisualKind.Star,
            GalaxyEntityKind.Post => GalaxyVisualKind.Planet,
            GalaxyEntityKind.Comment => significantComment ? GalaxyVisualKind.Satellite : GalaxyVisualKind.RingParticle,
            GalaxyEntityKind.User => GalaxyVisualKind.Ship,
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };

        public static bool IsNavigable(GalaxyEntityKind kind) => kind == GalaxyEntityKind.Subreddit;
    }
}
