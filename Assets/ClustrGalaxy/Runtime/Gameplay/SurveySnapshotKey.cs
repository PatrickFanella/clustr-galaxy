using System;

namespace ClustrGalaxy.Gameplay
{
    /// <summary>
    /// The immutable data snapshot to which Survey discoveries belong. The
    /// namespace is part of the identity so prototype progress can never be
    /// mistaken for progress against a published catalog with the same IDs.
    /// </summary>
    public readonly struct SurveySnapshotKey : IEquatable<SurveySnapshotKey>
    {
        public const string PinnedNamespace = "pinned";
        public const string DemoNamespace = "demo";

        public SurveySnapshotKey(long revisionId, long catalogId, string storageNamespace)
        {
            RevisionId = revisionId;
            CatalogId = catalogId;
            StorageNamespace = string.IsNullOrWhiteSpace(storageNamespace)
                ? throw new ArgumentException("A snapshot namespace is required.", nameof(storageNamespace))
                : storageNamespace;
        }

        public long RevisionId { get; }
        public long CatalogId { get; }
        public string StorageNamespace { get; }
        public bool IsDemo => string.Equals(StorageNamespace, DemoNamespace, StringComparison.Ordinal);

        public static SurveySnapshotKey Pinned(long revisionId, long catalogId) =>
            new SurveySnapshotKey(revisionId, catalogId, PinnedNamespace);

        public static SurveySnapshotKey Demo => new SurveySnapshotKey(9, 3, DemoNamespace);

        public bool Equals(SurveySnapshotKey other) =>
            RevisionId == other.RevisionId &&
            CatalogId == other.CatalogId &&
            string.Equals(StorageNamespace, other.StorageNamespace, StringComparison.Ordinal);

        public override bool Equals(object obj) => obj is SurveySnapshotKey other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = RevisionId.GetHashCode();
                hash = (hash * 397) ^ CatalogId.GetHashCode();
                return (hash * 397) ^ StringComparer.Ordinal.GetHashCode(StorageNamespace);
            }
        }

        public static bool operator ==(SurveySnapshotKey left, SurveySnapshotKey right) => left.Equals(right);
        public static bool operator !=(SurveySnapshotKey left, SurveySnapshotKey right) => !left.Equals(right);

        public override string ToString() => $"{StorageNamespace}:{RevisionId}-{CatalogId}";
    }
}
