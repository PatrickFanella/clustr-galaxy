using System;
using System.Collections.Generic;
using System.Linq;
using ClustrGalaxy.Gameplay;

namespace ClustrGalaxy.Progression
{
    public readonly struct SurveyContractProgress
    {
        public SurveyContractProgress(SurveyContractDefinition definition, int current, bool completedNow)
        { Definition = definition; Current = current; CompletedNow = completedNow; }
        public SurveyContractDefinition Definition { get; }
        public int Current { get; }
        public bool CompletedNow { get; }
    }

    /// <summary>
    /// Pure, snapshot-bound objective reducer. It only consumes typed local
    /// events; it does not inspect Unity scene objects or control flight modes.
    /// </summary>
    public sealed class SurveyProgression : IDisposable
    {
        public const string FirstScanAchievement = "achievement:first-scan";
        public const string FirstNewSystemAchievement = "achievement:first-new-system";
        public const string FirstConstellationAchievement = "achievement:first-constellation";
        public const string FirstPersonalConstellationAchievement = "achievement:first-personal-constellation";
        public const string FirstMasteryArrivalAchievement = "achievement:first-pilot-mastery-arrival";

        private readonly SurveyDiscoveryStore store;
        private readonly SurveyEventBus bus;
        private readonly SurveyContractDefinition[] definitions;

        public SurveyProgression(SurveyDiscoveryStore discoveryStore, SurveyEventBus sharedEventBus,
            IEnumerable<SurveyContractDefinition> contractDefinitions = null)
        {
            store = discoveryStore ?? throw new ArgumentNullException(nameof(discoveryStore));
            bus = sharedEventBus ?? throw new ArgumentNullException(nameof(sharedEventBus));
            definitions = (contractDefinitions ?? SurveyContractDefinition.Defaults)
                .Where(definition => definition != null).OrderBy(definition => definition.Id, StringComparer.Ordinal).ToArray();
            bus.Published += OnSurveyEvent;
        }

        public event Action<SurveyContractProgress> ContractProgressed = delegate { };
        public event Action<string> AchievementUnlocked = delegate { };
        public event Action<string> Notification = delegate { };
        public IReadOnlyList<SurveyContractDefinition> Definitions => definitions;
        public int ScannerLevel => store.State.scanner_level;
        public string LastAchievementId { get; private set; } = string.Empty;

        public SurveyContractProgress ProgressOf(string contractId)
        {
            var definition = definitions.FirstOrDefault(item => string.Equals(item.Id, contractId, StringComparison.Ordinal));
            return definition == null ? default : new SurveyContractProgress(definition, Counter(definition.Metric), false);
        }

        public SurveyContractProgress PinnedContract => definitions.Select(definition => ProgressOf(definition.Id))
            .FirstOrDefault(progress => !store.IsContractComplete(progress.Definition?.Id));

        public void Dispose() => bus.Published -= OnSurveyEvent;

        private void OnSurveyEvent(SurveyEvent surveyEvent)
        {
            if (surveyEvent == null || surveyEvent.SnapshotKey != store.SnapshotKey) return;
            var changed = false;
            switch (surveyEvent.Type)
            {
                case SurveyEventType.ScanCompleted:
                    changed = ProcessScan(surveyEvent);
                    break;
                case SurveyEventType.SystemCompleted:
                    changed = ProcessUnique("system:" + surveyEvent.ScannedId, () => store.State.completed_system_count++);
                    break;
                case SurveyEventType.AuthoredConstellationCompleted:
                    changed = ProcessUnique("authored-constellation:" + surveyEvent.ScannedId,
                        () => store.State.completed_authored_constellation_count++);
                    if (changed) Unlock(FirstConstellationAchievement);
                    break;
                case SurveyEventType.PersonalConstellationSaved:
                    changed = ProcessUnique("personal-constellation:" + surveyEvent.ScannedId, () => { });
                    if (changed) Unlock(FirstPersonalConstellationAchievement);
                    break;
                case SurveyEventType.PilotMasteryArrival:
                    changed = ProcessUnique("mastery-arrival:" + surveyEvent.ScannedId, () => { });
                    if (changed) Unlock(FirstMasteryArrivalAchievement);
                    break;
            }
            if (!changed) return;
            EvaluateContracts();
            store.Save();
        }

        private bool ProcessScan(SurveyEvent surveyEvent)
        {
            if (!ProcessUnique("scan:" + surveyEvent.ScannedId, () => { })) return false;
            Unlock(FirstScanAchievement);
            if (surveyEvent.ScannedId.StartsWith("post:", StringComparison.Ordinal)) store.State.surveyed_post_scan_count++;
            var newSystem = surveyEvent.NewlyRevealedEntityIds.Any(id => id.StartsWith("subreddit:", StringComparison.Ordinal));
            if (newSystem) Unlock(FirstNewSystemAchievement);
            foreach (var routeId in surveyEvent.NewlyRevealedRouteIds ?? Array.Empty<string>())
                ProcessUnique("route:" + routeId, () => store.State.revealed_route_count++);
            return true;
        }

        private bool ProcessUnique(string id, Action mutation)
        {
            if (!store.RecordProgressEvent(id)) return false;
            mutation();
            return true;
        }

        private void EvaluateContracts()
        {
            foreach (var definition in definitions)
            {
                if (store.IsContractComplete(definition.Id) || Counter(definition.Metric) < definition.Required) continue;
                if (!store.CompleteContract(definition.Id)) continue;
                var progress = new SurveyContractProgress(definition, Counter(definition.Metric), true);
                ContractProgressed(progress);
                Notification($"CONTRACT COMPLETE // {definition.DisplayName}");
            }
            var count = store.State.completed_contract_ids?.Length ?? 0;
            store.SetScannerLevel(count >= definitions.Length ? 3 : count >= 2 ? 2 : 1);
        }

        private void Unlock(string id)
        {
            if (!store.UnlockAchievement(id)) return;
            LastAchievementId = id;
            AchievementUnlocked(id);
            Notification("ACHIEVEMENT // " + id.Replace("achievement:", string.Empty).Replace('-', ' ').ToUpperInvariant());
        }

        private int Counter(SurveyContractMetric metric) => metric switch
        {
            SurveyContractMetric.ScannedPosts => store.State.surveyed_post_scan_count,
            SurveyContractMetric.RevealedRoutes => store.State.revealed_route_count,
            SurveyContractMetric.CompletedSystems => store.State.completed_system_count,
            SurveyContractMetric.CompletedAuthoredConstellations => store.State.completed_authored_constellation_count,
            _ => 0
        };
    }
}
