using System;
using System.Collections.Generic;
using System.Linq;
using ClustrGalaxy.Core.Domain;

namespace ClustrGalaxy.Core.Survey
{
    public readonly struct SurveySnapshotId : IEquatable<SurveySnapshotId>
    {
        public SurveySnapshotId(long revisionId, long catalogId, string storageNamespace)
        {
            if (revisionId < 0) throw new ArgumentOutOfRangeException(nameof(revisionId));
            if (catalogId < 0) throw new ArgumentOutOfRangeException(nameof(catalogId));
            if (string.IsNullOrWhiteSpace(storageNamespace))
                throw new ArgumentException("A snapshot namespace is required.", nameof(storageNamespace));
            RevisionId = revisionId;
            CatalogId = catalogId;
            StorageNamespace = storageNamespace.Trim();
        }

        public long RevisionId { get; }
        public long CatalogId { get; }
        public string StorageNamespace { get; }
        public bool Equals(SurveySnapshotId other) => RevisionId == other.RevisionId && CatalogId == other.CatalogId &&
            string.Equals(StorageNamespace, other.StorageNamespace, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is SurveySnapshotId other && Equals(other);
        public override int GetHashCode() => ((RevisionId.GetHashCode() * 397) ^ CatalogId.GetHashCode()) * 397 ^
            StringComparer.Ordinal.GetHashCode(StorageNamespace ?? string.Empty);
        public override string ToString() => $"{StorageNamespace}:{RevisionId}-{CatalogId}";
        public static bool operator ==(SurveySnapshotId left, SurveySnapshotId right) => left.Equals(right);
        public static bool operator !=(SurveySnapshotId left, SurveySnapshotId right) => !left.Equals(right);
    }

    public sealed class SurveyRoute
    {
        public SurveyRoute(GalaxyRouteId id, GalaxyEntityId sourceId, GalaxyEntityId targetId, float weight)
        { Id = id; SourceId = sourceId; TargetId = targetId; Weight = weight; }
        public GalaxyRouteId Id { get; }
        public GalaxyEntityId SourceId { get; }
        public GalaxyEntityId TargetId { get; }
        public float Weight { get; }
    }

    public sealed class PersonalConstellationRecord
    {
        public PersonalConstellationRecord(string id, string name, string reason, IEnumerable<GalaxyEntityId> members,
            DateTime createdUtc)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("An ID is required.", nameof(id));
            ValidateText(name, 1, 60, nameof(name));
            ValidateText(reason, 1, 500, nameof(reason));
            var normalized = (members ?? Array.Empty<GalaxyEntityId>()).Distinct().OrderBy(value => value).ToArray();
            if (normalized.Length < 3 || normalized.Length > 12)
                throw new ArgumentException("A personal constellation requires 3 to 12 unique entities.", nameof(members));
            Id = id; Name = name.Trim(); Reason = reason.Trim(); Members = normalized; CreatedUtc = createdUtc;
        }
        public string Id { get; }
        public string Name { get; }
        public string Reason { get; }
        public IReadOnlyList<GalaxyEntityId> Members { get; }
        public DateTime CreatedUtc { get; }
        private static void ValidateText(string value, int minimum, int maximum, string parameter)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Trim().Length < minimum || value.Trim().Length > maximum || value.Any(char.IsControl))
                throw new ArgumentException($"Value must contain {minimum} to {maximum} non-control characters.", parameter);
        }
    }

    public sealed class SurveyState
    {
        public const int SchemaVersion = 3;

        public SurveyState(SurveySnapshotId snapshot, string profileId, GalaxyEntityId startingSystemId,
            IEnumerable<GalaxyEntityId> knownEntities = null, IEnumerable<GalaxyEntityId> identifiedEntities = null,
            IEnumerable<GalaxyRouteId> knownRoutes = null, IEnumerable<GalaxyEntityId> completedScans = null,
            IEnumerable<string> completedConstellations = null, IEnumerable<string> discoveries = null,
            IEnumerable<string> completedContracts = null, IEnumerable<string> achievements = null,
            IEnumerable<string> processedIntentIds = null, IEnumerable<PersonalConstellationRecord> personalConstellations = null,
            IEnumerable<string> queuedPersonalConstellationIds = null, string pendingQueueId = "", int scannedPostCount = 0,
            int revealedRouteCount = 0, int completedSystemCount = 0, int completedAuthoredConstellationCount = 0,
            int scannerLevel = 1, int authoredSequenceProgress = 0, DateTime? savedUtc = null)
        {
            if (string.IsNullOrWhiteSpace(profileId)) throw new ArgumentException("A profile ID is required.", nameof(profileId));
            Snapshot = snapshot; ProfileId = profileId; StartingSystemId = startingSystemId;
            KnownEntities = Sorted(knownEntities); IdentifiedEntities = Sorted(identifiedEntities);
            KnownRoutes = Sorted(knownRoutes); CompletedScans = Sorted(completedScans);
            CompletedConstellations = SortedStrings(completedConstellations); Discoveries = SortedStrings(discoveries);
            CompletedContracts = SortedStrings(completedContracts); Achievements = SortedStrings(achievements);
            ProcessedIntentIds = SortedStrings(processedIntentIds);
            PersonalConstellations = (personalConstellations ?? Array.Empty<PersonalConstellationRecord>())
                .Where(value => value != null).OrderBy(value => value.Id, StringComparer.Ordinal).ToArray();
            QueuedPersonalConstellationIds = SortedStrings(queuedPersonalConstellationIds);
            PendingQueueId = pendingQueueId ?? string.Empty;
            ScannedPostCount = Math.Max(0, scannedPostCount); RevealedRouteCount = Math.Max(0, revealedRouteCount);
            CompletedSystemCount = Math.Max(0, completedSystemCount);
            CompletedAuthoredConstellationCount = Math.Max(0, completedAuthoredConstellationCount);
            ScannerLevel = Math.Max(1, scannerLevel); AuthoredSequenceProgress = Math.Max(0, authoredSequenceProgress);
            SavedUtc = savedUtc;
        }

        public SurveySnapshotId Snapshot { get; }
        public string ProfileId { get; }
        public GalaxyEntityId StartingSystemId { get; }
        public IReadOnlyList<GalaxyEntityId> KnownEntities { get; }
        public IReadOnlyList<GalaxyEntityId> IdentifiedEntities { get; }
        public IReadOnlyList<GalaxyRouteId> KnownRoutes { get; }
        public IReadOnlyList<GalaxyEntityId> CompletedScans { get; }
        public IReadOnlyList<string> CompletedConstellations { get; }
        public IReadOnlyList<string> Discoveries { get; }
        public IReadOnlyList<string> CompletedContracts { get; }
        public IReadOnlyList<string> Achievements { get; }
        public IReadOnlyList<string> ProcessedIntentIds { get; }
        public IReadOnlyList<PersonalConstellationRecord> PersonalConstellations { get; }
        public IReadOnlyList<string> QueuedPersonalConstellationIds { get; }
        public string PendingQueueId { get; }
        public int ScannedPostCount { get; }
        public int RevealedRouteCount { get; }
        public int CompletedSystemCount { get; }
        public int CompletedAuthoredConstellationCount { get; }
        public int ScannerLevel { get; }
        public int AuthoredSequenceProgress { get; }
        public DateTime? SavedUtc { get; }
        public bool IsKnown(GalaxyEntityId id) => BinaryContains(KnownEntities, id);
        public bool IsIdentified(GalaxyEntityId id) => BinaryContains(IdentifiedEntities, id);
        public bool IsScanComplete(GalaxyEntityId id) => BinaryContains(CompletedScans, id);
        public bool IsProcessed(string id) => ProcessedIntentIds.Contains(id, StringComparer.Ordinal);

        private static IReadOnlyList<T> Sorted<T>(IEnumerable<T> values) where T : struct, IComparable<T> =>
            (values ?? Array.Empty<T>()).Distinct().OrderBy(value => value).ToArray();
        private static IReadOnlyList<string> SortedStrings(IEnumerable<string> values) =>
            (values ?? Array.Empty<string>()).Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
        private static bool BinaryContains<T>(IReadOnlyList<T> values, T target) where T : struct, IComparable<T>
        {
            var low = 0; var high = values.Count - 1;
            while (low <= high)
            {
                var middle = low + ((high - low) / 2);
                var comparison = values[middle].CompareTo(target);
                if (comparison == 0) return true;
                if (comparison < 0) low = middle + 1; else high = middle - 1;
            }
            return false;
        }
    }

    public interface ISurveyRepository
    {
        SurveyState LoadOrCreate(string profileId, SurveySnapshotId snapshot, GalaxyEntityId startingSystemId);
        void Save(SurveyState state);
    }
}
