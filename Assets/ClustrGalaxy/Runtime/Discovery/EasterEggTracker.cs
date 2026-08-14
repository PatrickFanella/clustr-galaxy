using System;
using System.Collections.Generic;
using System.Linq;
using ClustrGalaxy.Gameplay;

namespace ClustrGalaxy.Discovery
{
    public readonly struct EasterEggDiscovery
    {
        public EasterEggDiscovery(EasterEggDefinition definition)
        {
            Definition = definition;
        }

        public EasterEggDefinition Definition { get; }
        public string Id => Definition?.Id ?? string.Empty;
        public string Title => Definition?.Title ?? string.Empty;
        public string Message => Definition?.ShortMessage ?? string.Empty;
        public string Provenance => Definition?.Provenance ?? EasterEggDefinition.AuthoredProvenance;
        public string LocalCosmeticReward => Definition?.LocalCosmeticReward ?? string.Empty;
    }

    /// <summary>
    /// Snapshot-bound reducer for explicitly-authored discoveries. It consumes
    /// only completed Survey scans, never scene data or graph facts.
    /// </summary>
    public sealed class EasterEggTracker : IDisposable
    {
        private readonly SurveyDiscoveryStore store;
        private readonly GalaxyModeState modes;
        private readonly SurveyEventBus bus;
        private readonly EasterEggDefinition[] definitions;
        private readonly HashSet<string> scannedIds;
        private readonly Dictionary<string, int> orderedProgress = new(StringComparer.Ordinal);

        public EasterEggTracker(SurveyDiscoveryStore discoveryStore, GalaxyModeState modeState,
            SurveyEventBus sharedEventBus, IEnumerable<EasterEggDefinition> authoredDefinitions)
        {
            store = discoveryStore ?? throw new ArgumentNullException(nameof(discoveryStore));
            modes = modeState ?? throw new ArgumentNullException(nameof(modeState));
            bus = sharedEventBus ?? throw new ArgumentNullException(nameof(sharedEventBus));
            definitions = (authoredDefinitions ?? Array.Empty<EasterEggDefinition>())
                .Where(definition => definition != null).OrderBy(definition => definition.Id, StringComparer.Ordinal).ToArray();
            scannedIds = new HashSet<string>(store.State.completed_scan_ids ?? Array.Empty<string>(), StringComparer.Ordinal);
            bus.Published += OnSurveyEvent;
        }

        public event Action<EasterEggDiscovery> Discovered = delegate { };
        public int AwardCount { get; private set; }
        public EasterEggDiscovery LastDiscovery { get; private set; }
        public IReadOnlyList<EasterEggDefinition> Definitions => definitions;
        public bool IsDiscovered(string stableId) => store.IsEasterEggDiscovered(stableId);

        public void Dispose() => bus.Published -= OnSurveyEvent;

        private void OnSurveyEvent(SurveyEvent surveyEvent)
        {
            if (surveyEvent == null || surveyEvent.Type != SurveyEventType.ScanCompleted ||
                surveyEvent.SnapshotKey != store.SnapshotKey || modes.WorldMode != GalaxyWorldMode.Survey ||
                string.IsNullOrWhiteSpace(surveyEvent.ScannedId))
            {
                return;
            }

            scannedIds.Add(surveyEvent.ScannedId);
            foreach (var definition in definitions)
            {
                if (store.IsEasterEggDiscovered(definition.Id) || !definition.IsCompatibleWith(store.SnapshotKey)) continue;
                var satisfied = definition.TriggerKind == EasterEggTriggerKind.UnorderedScanSet
                    ? definition.IsSatisfiedByUnorderedScans(scannedIds)
                    : AdvanceOrdered(definition, surveyEvent.ScannedId);
                if (satisfied) Award(definition);
            }
        }

        private bool AdvanceOrdered(EasterEggDefinition definition, string scannedId)
        {
            orderedProgress.TryGetValue(definition.Id, out var current);
            var next = definition.AdvanceOrderedProgress(current, scannedId);
            orderedProgress[definition.Id] = next;
            return next == definition.RequiredStableIds.Count;
        }

        private void Award(EasterEggDefinition definition)
        {
            if (!store.DiscoverEasterEgg(definition.Id)) return;
            store.Save();
            AwardCount++;
            LastDiscovery = new EasterEggDiscovery(definition);
            Discovered(LastDiscovery);
        }
    }
}
