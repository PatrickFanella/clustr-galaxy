using System;
using System.Collections.Generic;
using System.Linq;
using ClustrGalaxy.Core.Domain;

namespace ClustrGalaxy.Core.Activities
{
    public enum CelebrationKind { Discovery, SystemRoutes, Constellation, FlightRibbon }
    public sealed class CelebrationCue
    {
        public CelebrationCue(string id, CelebrationKind kind, IEnumerable<GalaxyEntityId> entities)
        {
            if (string.IsNullOrWhiteSpace(id) || !Enum.IsDefined(typeof(CelebrationKind),kind)) throw new ArgumentException("Invalid celebration.");
            var ids = (entities ?? Array.Empty<GalaxyEntityId>()).ToArray();
            if (ids.Length > 12 || ids.Any(value => value.IsEmpty)) throw new ArgumentException("Invalid celebration anchors.");
            Id = id; Kind = kind; Entities = Array.AsReadOnly(ids);
        }
        public string Id { get; }
        public CelebrationKind Kind { get; }
        public IReadOnlyList<GalaxyEntityId> Entities { get; }
    }
    public sealed class CelebrationSlots
    {
        private readonly double[] ends = new double[3];
        // Reserve the bounded qualification workload before flight; accepted IDs remain idempotent.
        private readonly HashSet<string> played = new(1024, StringComparer.Ordinal);
        public int TryStart(string eventId, double now, double seconds = 2)
        {
            if (string.IsNullOrWhiteSpace(eventId) || double.IsNaN(now) || double.IsInfinity(now) || now < 0 ||
                double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds <= 0) throw new ArgumentException("Invalid celebration clock.");
            if (played.Contains(eventId)) return -1;
            for (var i = 0; i < ends.Length; i++)
                if (ends[i] <= now) { ends[i] = now + seconds; played.Add(eventId); return i; }
            return -1;
        }
        public double Remaining(int slot, double now) => Math.Max(0,ends[slot]-now);
    }
}
