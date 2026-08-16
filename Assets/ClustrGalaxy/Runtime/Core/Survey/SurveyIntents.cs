using System;
using System.Collections.Generic;
using ClustrGalaxy.Core.Domain;

namespace ClustrGalaxy.Core.Survey
{
    public abstract class SurveyIntent
    {
        protected SurveyIntent(string intentId, SurveySnapshotId snapshot, DateTime utc)
        {
            if (string.IsNullOrWhiteSpace(intentId)) throw new ArgumentException("An intent ID is required.", nameof(intentId));
            IntentId = intentId; Snapshot = snapshot; Utc = utc;
        }
        public string IntentId { get; }
        public SurveySnapshotId Snapshot { get; }
        public DateTime Utc { get; }
    }

    public sealed class StartSurveyIntent : SurveyIntent
    {
        public StartSurveyIntent(string intentId, SurveySnapshotId snapshot, DateTime utc, GalaxyEntityId startingSystemId,
            IEnumerable<GalaxyEntityId> seedIdentifiedEntities) : base(intentId, snapshot, utc)
        { StartingSystemId = startingSystemId; SeedIdentifiedEntities = seedIdentifiedEntities ?? Array.Empty<GalaxyEntityId>(); }
        public GalaxyEntityId StartingSystemId { get; }
        public IEnumerable<GalaxyEntityId> SeedIdentifiedEntities { get; }
    }

    public sealed class CompleteScanIntent : SurveyIntent
    {
        public CompleteScanIntent(string intentId, SurveySnapshotId snapshot, DateTime utc, GalaxyEntityId targetId,
            GalaxyEntityKind targetKind, IEnumerable<SurveyRoute> routes, IEnumerable<GalaxyEntityId> loadedPostIds,
            GalaxyEntityId systemId = default, bool systemCompletedNow = false) : base(intentId, snapshot, utc)
        {
            TargetId = targetId; TargetKind = targetKind; Routes = routes ?? Array.Empty<SurveyRoute>();
            LoadedPostIds = loadedPostIds ?? Array.Empty<GalaxyEntityId>(); SystemId = systemId;
            SystemCompletedNow = systemCompletedNow;
        }
        public GalaxyEntityId TargetId { get; }
        public GalaxyEntityKind TargetKind { get; }
        public IEnumerable<SurveyRoute> Routes { get; }
        public IEnumerable<GalaxyEntityId> LoadedPostIds { get; }
        public GalaxyEntityId SystemId { get; }
        public bool SystemCompletedNow { get; }
    }

    public sealed class SavePersonalConstellationIntent : SurveyIntent
    {
        public SavePersonalConstellationIntent(string intentId, SurveySnapshotId snapshot, DateTime utc, string name,
            string reason, IEnumerable<GalaxyEntityId> members) : base(intentId, snapshot, utc)
        { Name = name; Reason = reason; Members = members ?? Array.Empty<GalaxyEntityId>(); }
        public string Name { get; }
        public string Reason { get; }
        public IEnumerable<GalaxyEntityId> Members { get; }
    }

    public sealed class RequestLocalQueueIntent : SurveyIntent
    {
        public RequestLocalQueueIntent(string intentId, SurveySnapshotId snapshot, DateTime utc, string personalId)
            : base(intentId, snapshot, utc) { PersonalId = personalId ?? string.Empty; }
        public string PersonalId { get; }
    }
    public sealed class CancelLocalQueueIntent : SurveyIntent
    {
        public CancelLocalQueueIntent(string intentId, SurveySnapshotId snapshot, DateTime utc) : base(intentId, snapshot, utc) { }
    }
    public sealed class ConfirmLocalQueueIntent : SurveyIntent
    {
        public ConfirmLocalQueueIntent(string intentId, SurveySnapshotId snapshot, DateTime utc) : base(intentId, snapshot, utc) { }
    }
    public sealed class PilotMasteryArrivalIntent : SurveyIntent
    {
        public PilotMasteryArrivalIntent(string intentId, SurveySnapshotId snapshot, DateTime utc, GalaxyEntityId destinationId)
            : base(intentId, snapshot, utc) { DestinationId = destinationId; }
        public GalaxyEntityId DestinationId { get; }
    }

    public sealed class SurveyTransitionOutcome
    {
        internal SurveyTransitionOutcome(bool succeeded, bool changed, string rejection, SurveyState state,
            IReadOnlyList<GalaxyEntityId> entities, IReadOnlyList<GalaxyRouteId> routes,
            IReadOnlyList<string> notifications, IReadOnlyList<string> achievements, string cosmeticReward,
            string personalConstellationId, string queueStatus)
        {
            Succeeded = succeeded; Changed = changed; Rejection = rejection ?? string.Empty; State = state;
            RevealedEntities = entities ?? Array.Empty<GalaxyEntityId>(); RevealedRoutes = routes ?? Array.Empty<GalaxyRouteId>();
            Notifications = notifications ?? Array.Empty<string>(); Achievements = achievements ?? Array.Empty<string>();
            CosmeticReward = cosmeticReward ?? string.Empty; PersonalConstellationId = personalConstellationId ?? string.Empty;
            QueueStatus = queueStatus ?? string.Empty;
        }
        public bool Succeeded { get; }
        public bool Changed { get; }
        public string Rejection { get; }
        public SurveyState State { get; }
        public IReadOnlyList<GalaxyEntityId> RevealedEntities { get; }
        public IReadOnlyList<GalaxyRouteId> RevealedRoutes { get; }
        public IReadOnlyList<string> Notifications { get; }
        public IReadOnlyList<string> Achievements { get; }
        public string CosmeticReward { get; }
        public string PersonalConstellationId { get; }
        public string QueueStatus { get; }
    }
}
