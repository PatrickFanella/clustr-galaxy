using System;
using System.Collections.Generic;
using System.Linq;
using ClustrGalaxy.Gameplay;

namespace ClustrGalaxy.Constellations
{
    public readonly struct ConstellationProgress
    {
        public ConstellationProgress(string constellationId, string message, int found, int total, bool available, bool completedNow)
        {
            ConstellationId = constellationId; Message = message; Found = found; Total = total;
            Available = available; CompletedNow = completedNow;
        }
        public string ConstellationId { get; }
        public string Message { get; }
        public int Found { get; }
        public int Total { get; }
        public bool Available { get; }
        public bool CompletedNow { get; }
    }

    /// <summary>Records only scan events from the shared bus; it never creates one.</summary>
    public sealed class ConstellationTracker : IDisposable
    {
        private readonly SurveyDiscoveryStore store;
        private readonly SurveyEventBus eventBus;
        private readonly HashSet<string> resolvedIds;
        private readonly ConstellationDefinition[] definitions;

        public ConstellationTracker(SurveyDiscoveryStore discoveryStore, SurveyEventBus sharedEventBus,
            IEnumerable<ConstellationDefinition> authoredDefinitions, IEnumerable<string> resolvedStableIds)
        {
            store = discoveryStore ?? throw new ArgumentNullException(nameof(discoveryStore));
            eventBus = sharedEventBus ?? throw new ArgumentNullException(nameof(sharedEventBus));
            definitions = (authoredDefinitions ?? Array.Empty<ConstellationDefinition>())
                .Where(definition => definition != null).OrderBy(definition => definition.Id, StringComparer.Ordinal).ToArray();
            resolvedIds = new HashSet<string>(resolvedStableIds ?? Array.Empty<string>(), StringComparer.Ordinal);
            eventBus.Published += OnSurveyEvent;
        }

        public event Action<ConstellationProgress> Progressed = delegate { };
        public int CompletionAwards { get; private set; }
        public IReadOnlyList<ConstellationDefinition> Definitions => definitions;

        public ConstellationProgress RecordScan(string stableId)
        {
            var first = default(ConstellationProgress);
            var hasFirst = false;
            foreach (var definition in definitions)
            {
                var progress = RecordScan(definition, stableId);
                if (!hasFirst && definition.MemberStableIds.Contains(stableId, StringComparer.Ordinal))
                {
                    first = progress;
                    hasFirst = true;
                }
            }
            return hasFirst ? first : new ConstellationProgress(string.Empty, string.Empty, 0, 0, false, false);
        }

        public ConstellationProgress ProgressOf(string constellationId)
        {
            var definition = definitions.FirstOrDefault(item => string.Equals(item.Id, constellationId, StringComparison.Ordinal));
            return definition == null ? new ConstellationProgress(string.Empty, string.Empty, 0, 0, false, false) : ProgressOf(definition, false);
        }

        public bool IsComplete(string constellationId) => Contains(store.State.completed_constellation_ids, constellationId);

        public void Dispose() => eventBus.Published -= OnSurveyEvent;

        private void OnSurveyEvent(SurveyEvent surveyEvent)
        {
            if (surveyEvent == null || surveyEvent.SnapshotKey != store.SnapshotKey) return;
            var progress = RecordScan(surveyEvent.ScannedId);
            if (!string.IsNullOrWhiteSpace(progress.ConstellationId)) Progressed(progress);
        }

        private ConstellationProgress RecordScan(ConstellationDefinition definition, string stableId)
        {
            if (!definition.IsAvailable(store.SnapshotKey, resolvedIds)) return ProgressOf(definition, false);
            if (!definition.MemberStableIds.Contains(stableId, StringComparer.Ordinal)) return ProgressOf(definition, false);
            store.RecordConstellationMember(stableId);
            var before = IsComplete(definition.Id);
            var progress = ProgressOf(definition, !before && AllRecorded(definition));
            if (progress.CompletedNow)
            {
                AddCompleted(definition.Id);
                CompletionAwards++;
                progress = ProgressOf(definition, true);
            }
            store.Save();
            return progress;
        }

        private ConstellationProgress ProgressOf(ConstellationDefinition definition, bool completedNow)
        {
            var available = definition.IsAvailable(store.SnapshotKey, resolvedIds);
            var found = available ? definition.MemberStableIds.Count(store.IsConstellationMemberRecorded) : 0;
            var total = definition.MemberStableIds.Count;
            return new ConstellationProgress(definition.Id,
                available ? $"{definition.DisplayName} {found}/{total}" : $"{definition.DisplayName} UNAVAILABLE",
                found, total, available, completedNow);
        }

        private bool AllRecorded(ConstellationDefinition definition) => definition.MemberStableIds.All(store.IsConstellationMemberRecorded);
        private static bool Contains(string[] values, string value) => values != null && Array.BinarySearch(values, value, StringComparer.Ordinal) >= 0;
        private void AddCompleted(string id)
        {
            if (IsComplete(id)) return;
            store.State.completed_constellation_ids = SurveyDiscoveryState.Normalize(
                (store.State.completed_constellation_ids ?? Array.Empty<string>()).Concat(new[] { id }));
        }
    }
}
