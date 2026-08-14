using System;
using System.Collections.Generic;
using System.Linq;
using ClustrGalaxy.Gameplay;

namespace ClustrGalaxy.Discovery
{
    public enum EasterEggTriggerKind
    {
        OrderedScanSequence,
        UnorderedScanSet
    }

    /// <summary>
    /// An authored local overlay. Definitions are deliberately separate from
    /// graph facts and must be labelled as such in every presentation.
    /// </summary>
    public sealed class EasterEggDefinition
    {
        public const string AuthoredProvenance = "Authored discovery — not source data";

        public EasterEggDefinition(string id, EasterEggTriggerKind triggerKind, IEnumerable<string> requiredStableIds,
            string title, string shortMessage, string localCosmeticReward = "", long? revisionId = null,
            long? catalogId = null)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("An easter-egg ID is required.", nameof(id));
            if (string.IsNullOrWhiteSpace(title)) throw new ArgumentException("A title is required.", nameof(title));
            if (string.IsNullOrWhiteSpace(shortMessage)) throw new ArgumentException("A short message is required.", nameof(shortMessage));

            Id = id;
            TriggerKind = triggerKind;
            RequiredStableIds = NormalizeRequired(requiredStableIds, triggerKind);
            if (RequiredStableIds.Count == 0) throw new ArgumentException("At least one stable ID is required.", nameof(requiredStableIds));
            Title = title;
            ShortMessage = shortMessage;
            LocalCosmeticReward = localCosmeticReward ?? string.Empty;
            RevisionId = revisionId;
            CatalogId = catalogId;
        }

        public string Id { get; }
        public EasterEggTriggerKind TriggerKind { get; }
        public IReadOnlyList<string> RequiredStableIds { get; }
        public string Title { get; }
        public string ShortMessage { get; }
        public string LocalCosmeticReward { get; }
        public string Provenance => AuthoredProvenance;
        public long? RevisionId { get; }
        public long? CatalogId { get; }

        public bool IsCompatibleWith(SurveySnapshotKey snapshot) =>
            (!RevisionId.HasValue || RevisionId.Value == snapshot.RevisionId) &&
            (!CatalogId.HasValue || CatalogId.Value == snapshot.CatalogId);

        public bool IsSatisfiedByUnorderedScans(IEnumerable<string> scannedStableIds)
        {
            if (TriggerKind != EasterEggTriggerKind.UnorderedScanSet) return false;
            var scanned = new HashSet<string>(scannedStableIds ?? Array.Empty<string>(), StringComparer.Ordinal);
            return RequiredStableIds.All(scanned.Contains);
        }

        /// <summary>Advances an ordered predicate, retaining a valid first-token restart after a mismatch.</summary>
        public int AdvanceOrderedProgress(int currentProgress, string scannedStableId)
        {
            if (TriggerKind != EasterEggTriggerKind.OrderedScanSequence) return 0;
            if (currentProgress < 0 || currentProgress >= RequiredStableIds.Count) currentProgress = 0;
            if (string.Equals(RequiredStableIds[currentProgress], scannedStableId, StringComparison.Ordinal)) return currentProgress + 1;
            return string.Equals(RequiredStableIds[0], scannedStableId, StringComparison.Ordinal) ? 1 : 0;
        }

        private static IReadOnlyList<string> NormalizeRequired(IEnumerable<string> values, EasterEggTriggerKind kind)
        {
            var ids = (values ?? Array.Empty<string>()).Where(value => !string.IsNullOrWhiteSpace(value)).ToArray();
            if (ids.Length != ids.Distinct(StringComparer.Ordinal).Count())
            {
                throw new ArgumentException("Easter-egg trigger IDs must be unique.", nameof(values));
            }
            return kind == EasterEggTriggerKind.OrderedScanSequence
                ? ids
                : ids.OrderBy(value => value, StringComparer.Ordinal).ToArray();
        }

        public static EasterEggDefinition SignalAfterimage { get; } = new(
            "easter-egg:signal-afterimage", EasterEggTriggerKind.OrderedScanSequence,
            new[] { "subreddit:technology", "post:retro-computing", "post:open-source-ai" },
            "Signal Afterimage", "A quiet harmonic lingers in the survey receiver.",
            "cosmetic:starfield-afterimage", revisionId: 9, catalogId: 3);
    }
}
