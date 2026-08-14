using System;
using System.Collections.Generic;
using System.Linq;
using ClustrGalaxy.Gameplay;

namespace ClustrGalaxy.Constellations
{
    public enum ConstellationProvenance { AuthoredOverlay, Personal }

    /// <summary>Immutable authored overlay. It never changes graph facts.</summary>
    public sealed class ConstellationDefinition
    {
        public ConstellationDefinition(string id, string displayName, string description,
            IEnumerable<string> memberStableIds, string rewardId, ConstellationProvenance provenance,
            long? revisionId = null, long? catalogId = null)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("A constellation ID is required.", nameof(id));
            if (string.IsNullOrWhiteSpace(displayName)) throw new ArgumentException("A display name is required.", nameof(displayName));
            Id = id;
            DisplayName = displayName;
            Description = description ?? string.Empty;
            MemberStableIds = NormalizeMembers(memberStableIds);
            if (MemberStableIds.Count == 0) throw new ArgumentException("At least one member is required.", nameof(memberStableIds));
            RewardId = rewardId ?? string.Empty;
            Provenance = provenance;
            RevisionId = revisionId;
            CatalogId = catalogId;
        }

        public string Id { get; }
        public string DisplayName { get; }
        public string Description { get; }
        public IReadOnlyList<string> MemberStableIds { get; }
        public string RewardId { get; }
        public ConstellationProvenance Provenance { get; }
        public long? RevisionId { get; }
        public long? CatalogId { get; }

        public bool IsCompatibleWith(SurveySnapshotKey snapshot) =>
            (!RevisionId.HasValue || RevisionId.Value == snapshot.RevisionId) &&
            (!CatalogId.HasValue || CatalogId.Value == snapshot.CatalogId);

        public bool IsAvailable(SurveySnapshotKey snapshot, IEnumerable<string> resolvedStableIds)
        {
            if (!IsCompatibleWith(snapshot)) return false;
            var resolved = new HashSet<string>(resolvedStableIds ?? Array.Empty<string>(), StringComparer.Ordinal);
            return MemberStableIds.All(resolved.Contains);
        }

        private static IReadOnlyList<string> NormalizeMembers(IEnumerable<string> values) =>
            (values ?? Array.Empty<string>()).Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();

        public static ConstellationDefinition Example { get; } = new(
            "example-constellation", "Example Constellation", "A local authored demo overlay.", new[]
            {
                "subreddit:technology", "post:open-source-ai", "post:linux-handhelds",
                "post:right-to-repair", "post:privacy-tools", "post:retro-computing"
            }, "reward:example-constellation", ConstellationProvenance.AuthoredOverlay,
            revisionId: 9, catalogId: 3);
    }
}
