using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using ClustrGalaxy.Core.Domain;
using ClustrGalaxy.Core.Activities;

namespace ClustrGalaxy.Core.Survey
{
    public sealed class SurveySession
    {
        public const string QueuedLocallyStatus = "QUEUED LOCALLY — SUBMISSION SERVICE UNAVAILABLE";
        private const string FirstScan = "achievement:first-scan";
        private const string FirstSystem = "achievement:first-new-system";
        private const string FirstConstellation = "achievement:first-constellation";
        private const string FirstPersonal = "achievement:first-personal-constellation";
        private const string FirstMastery = "achievement:first-pilot-mastery-arrival";
        private const string AuthoredConstellation = "example-constellation";
        private const string AuthoredDiscovery = "easter-egg:signal-afterimage";
        private const string CosmeticReward = "cosmetic:starfield-afterimage";

        private static readonly GalaxyEntityId[] AuthoredMembers =
        {
            (GalaxyEntityId)"subreddit:technology", (GalaxyEntityId)"post:open-source-ai",
            (GalaxyEntityId)"post:linux-handhelds", (GalaxyEntityId)"post:right-to-repair",
            (GalaxyEntityId)"post:privacy-tools", (GalaxyEntityId)"post:retro-computing"
        };
        private static readonly GalaxyEntityId[] DiscoverySequence =
        {
            (GalaxyEntityId)"subreddit:technology", (GalaxyEntityId)"post:retro-computing",
            (GalaxyEntityId)"post:open-source-ai"
        };

        private readonly ISurveyRepository repository;
        private SurveyState state;

        public SurveySession(ISurveyRepository repository, string profileId, SurveySnapshotId snapshot,
            GalaxyEntityId startingSystemId)
        {
            this.repository = repository ?? throw new ArgumentNullException(nameof(repository));
            state = repository.LoadOrCreate(profileId, snapshot, startingSystemId) ??
                throw new InvalidOperationException("The Survey repository returned no state.");
            if (state.Snapshot != snapshot || !string.Equals(state.ProfileId, profileId, StringComparison.Ordinal))
                throw new InvalidOperationException("The Survey repository returned a different snapshot.");
        }

        public SurveyState State => state;

        public SurveyTransitionOutcome Apply(SurveyIntent intent)
        {
            if (intent == null) throw new ArgumentNullException(nameof(intent));
            if (intent.Snapshot != state.Snapshot) return Rejected("Survey intent belongs to a different snapshot.");
            if (state.IsProcessed(intent.IntentId)) return Accepted(false);

            var mutable = new Mutable(state);
            var revealedEntities = new List<GalaxyEntityId>();
            var revealedRoutes = new List<GalaxyRouteId>();
            var notifications = new List<string>();
            var achievements = new List<string>();
            var cosmetic = string.Empty;
            var personalId = string.Empty;
            var queueStatus = string.Empty;
            string rejection;

            switch (intent)
            {
                case StartSurveyIntent start:
                    rejection = ApplyStart(mutable, start);
                    break;
                case CompleteScanIntent scan:
                    rejection = ApplyScan(mutable, scan, revealedEntities, revealedRoutes, notifications,
                        achievements, ref cosmetic);
                    break;
                case SavePersonalConstellationIntent save:
                    rejection = ApplyPersonalSave(mutable, save, notifications, achievements, out personalId);
                    break;
                case RequestLocalQueueIntent request:
                    rejection = ApplyQueueRequest(mutable, request, out queueStatus);
                    break;
                case CancelLocalQueueIntent _:
                    mutable.PendingQueueId = string.Empty;
                    queueStatus = "LOCAL QUEUE CANCELLED";
                    rejection = string.Empty;
                    break;
                case ConfirmLocalQueueIntent _:
                    rejection = ApplyQueueConfirmation(mutable, out personalId, out queueStatus);
                    break;
                case PilotMasteryArrivalIntent mastery:
                    rejection = ApplyMastery(mutable, mastery, notifications, achievements);
                    break;
                default:
                    rejection = "Unsupported Survey intent.";
                    break;
            }

            if (!string.IsNullOrEmpty(rejection)) return Rejected(rejection);
            var celebrations = new List<CelebrationCue>();
            if (intent is CompleteScanIntent completedScan)
            {
                celebrations.Add(new CelebrationCue("discovery:" + intent.IntentId, CelebrationKind.Discovery, new[] { completedScan.TargetId }));
                if (mutable.CompletedSystemCount > state.CompletedSystemCount)
                    celebrations.Add(new CelebrationCue("system:" + intent.IntentId, CelebrationKind.SystemRoutes, new[] { completedScan.SystemId }));
                if (mutable.CompletedAuthoredConstellationCount > state.CompletedAuthoredConstellationCount)
                    celebrations.Add(new CelebrationCue("constellation:" + intent.IntentId, CelebrationKind.Constellation, AuthoredMembers));
            }
            if (intent is SavePersonalConstellationIntent)
                celebrations.Add(new CelebrationCue("personal:" + intent.IntentId, CelebrationKind.Constellation,
                    mutable.PersonalConstellations.First(item => item.Id == personalId).Members));
            if (intent is PilotMasteryArrivalIntent)
                celebrations.Add(new CelebrationCue("flight:" + intent.IntentId, CelebrationKind.FlightRibbon, null));
            mutable.ProcessedIntentIds.Add(intent.IntentId);
            mutable.SavedUtc = intent.Utc;
            var candidate = mutable.Freeze();
            try { repository.Save(candidate); }
            catch (System.IO.IOException) { return Rejected("Progress could not be saved. Please retry."); }
            catch (UnauthorizedAccessException) { return Rejected("Progress storage is not writable. Please retry."); }
            state = candidate;
            return new SurveyTransitionOutcome(true, true, string.Empty, state, revealedEntities, revealedRoutes,
                notifications, achievements, cosmetic, personalId, queueStatus, celebrations.AsReadOnly());
        }

        private string ApplyStart(Mutable mutable, StartSurveyIntent intent)
        {
            if (intent.StartingSystemId != state.StartingSystemId)
                return "Survey is already bound to a different starting system.";
            mutable.KnownEntities.Add(intent.StartingSystemId);
            mutable.IdentifiedEntities.Add(intent.StartingSystemId);
            foreach (var entity in intent.SeedIdentifiedEntities)
            {
                mutable.KnownEntities.Add(entity);
                mutable.IdentifiedEntities.Add(entity);
            }
            return string.Empty;
        }

        private string ApplyScan(Mutable mutable, CompleteScanIntent intent, ICollection<GalaxyEntityId> entities,
            ICollection<GalaxyRouteId> routes, ICollection<string> notifications, ICollection<string> achievements,
            ref string cosmetic)
        {
            if (!mutable.KnownEntities.Contains(intent.TargetId)) return "Only a known Survey signal can be scanned.";
            if (mutable.CompletedScans.Contains(intent.TargetId)) return "That Survey signal is already complete.";

            mutable.CompletedScans.Add(intent.TargetId);
            if (intent.CleanSignal)
            {
                mutable.Discoveries.Add("clean-signal:" + intent.TargetId.Value);
                notifications.Add("CLEAN SIGNAL — artistic tuning");
            }
            mutable.IdentifiedEntities.Add(intent.TargetId);
            mutable.KnownEntities.Add(intent.TargetId);
            foreach (var post in intent.LoadedPostIds.OrderBy(value => value))
                AddKnown(mutable, post, entities);

            var candidates = intent.Routes.Where(route => route.SourceId == intent.TargetId && !mutable.KnownRoutes.Contains(route.Id))
                .GroupBy(route => route.Id).Select(group => group.First())
                .OrderByDescending(route => route.Weight).ThenBy(route => route.Id).ToArray();
            var revealLimit = mutable.ScannerLevel >= 3 ? 4 : mutable.ScannerLevel >= 2 ? 2 : 1;
            foreach (var route in candidates.Take(revealLimit)) AddRoute(mutable, route, entities, routes);
            // The scanner reports "complete after this scan"; only the first completion rewards the system.
            var firstSystemCompletion = intent.SystemCompletedNow && !intent.SystemId.IsEmpty &&
                !mutable.ProgressKeys.Contains("system:" + intent.SystemId.Value);
            if (firstSystemCompletion)
                foreach (var route in intent.Routes.Where(route => route.SourceId == intent.SystemId &&
                    route.Weight <= 2f && !mutable.KnownRoutes.Contains(route.Id))
                    .GroupBy(route => route.Id).Select(group => group.First()).OrderBy(route => route.Id))
                    AddRoute(mutable, route, entities, routes);

            if (intent.TargetKind == GalaxyEntityKind.Post) mutable.ScannedPostCount++;
            mutable.RevealedRouteCount += routes.Count;
            Unlock(mutable, FirstScan, achievements);
            if (entities.Any(entity => entity.Value.StartsWith("subreddit:", StringComparison.Ordinal)))
                Unlock(mutable, FirstSystem, achievements);

            if (firstSystemCompletion && mutable.ProgressKeys.Add("system:" + intent.SystemId.Value))
                mutable.CompletedSystemCount++;

            if (state.Snapshot.StorageNamespace == "demo" && AuthoredMembers.Contains(intent.TargetId))
                notifications.Add("AUTHORED // Example Constellation " + AuthoredMembers.Count(mutable.CompletedScans.Contains) + "/6");
            if (state.Snapshot.StorageNamespace == "demo" && !mutable.CompletedConstellations.Contains(AuthoredConstellation) &&
                AuthoredMembers.All(mutable.CompletedScans.Contains))
            {
                mutable.CompletedConstellations.Add(AuthoredConstellation);
                mutable.CompletedAuthoredConstellationCount++;
                Unlock(mutable, FirstConstellation, achievements);
                notifications.Add("CONSTELLATION COMPLETE // Example Constellation");
            }

            if (state.Snapshot.StorageNamespace == "demo" && !mutable.Discoveries.Contains(AuthoredDiscovery))
            {
                var expected = DiscoverySequence[Math.Min(mutable.AuthoredSequenceProgress, DiscoverySequence.Length - 1)];
                mutable.AuthoredSequenceProgress = intent.TargetId == expected
                    ? mutable.AuthoredSequenceProgress + 1
                    : intent.TargetId == DiscoverySequence[0] ? 1 : 0;
                if (mutable.AuthoredSequenceProgress >= DiscoverySequence.Length)
                {
                    mutable.Discoveries.Add(AuthoredDiscovery);
                    mutable.AuthoredSequenceProgress = 0;
                    cosmetic = CosmeticReward;
                    notifications.Add("TRANSMISSION // Signal Afterimage");
                }
            }

            EvaluateContracts(mutable, notifications);
            return string.Empty;
        }

        private static string ApplyPersonalSave(Mutable mutable, SavePersonalConstellationIntent intent,
            ICollection<string> notifications, ICollection<string> achievements, out string personalId)
        {
            var members = intent.Members.Distinct().OrderBy(value => value).ToArray();
            if (members.Any(member => !mutable.IdentifiedEntities.Contains(member)))
            {
                personalId = string.Empty;
                return "Every personal constellation member must be identified in Survey.";
            }
            personalId = "personal:" + StableToken(intent.IntentId);
            PersonalConstellationRecord record;
            try { record = new PersonalConstellationRecord(personalId, intent.Name, intent.Reason, members, intent.Utc); }
            catch (ArgumentException exception) { personalId = string.Empty; return exception.Message; }
            mutable.PersonalConstellations.Add(record);
            Unlock(mutable, FirstPersonal, achievements);
            notifications.Add("SAVED LOCALLY");
            return string.Empty;
        }

        private static string ApplyQueueRequest(Mutable mutable, RequestLocalQueueIntent intent, out string status)
        {
            if (!mutable.PersonalConstellations.Any(value => string.Equals(value.Id, intent.PersonalId, StringComparison.Ordinal)))
            { status = string.Empty; return "Save this constellation locally before queuing it for review."; }
            mutable.PendingQueueId = intent.PersonalId;
            status = "CONFIRM LOCAL QUEUE — NO SUBMISSION SERVICE IS CONNECTED";
            return string.Empty;
        }

        private static string ApplyQueueConfirmation(Mutable mutable, out string personalId, out string status)
        {
            personalId = mutable.PendingQueueId;
            if (string.IsNullOrWhiteSpace(personalId))
            { status = string.Empty; return "Choose QUEUE FOR REVIEW before confirming the local queue."; }
            mutable.QueuedPersonalConstellationIds.Add(personalId);
            mutable.PendingQueueId = string.Empty;
            status = QueuedLocallyStatus;
            return string.Empty;
        }

        private static string ApplyMastery(Mutable mutable, PilotMasteryArrivalIntent intent,
            ICollection<string> notifications, ICollection<string> achievements)
        {
            if (!mutable.KnownEntities.Contains(intent.DestinationId)) return "Pilot Mastery destination is not known in Survey.";
            if (mutable.ProgressKeys.Add("mastery:" + intent.DestinationId.Value))
            {
                Unlock(mutable, FirstMastery, achievements);
                notifications.Add("MASTERY ARRIVAL // " + intent.DestinationId.Value);
            }
            return string.Empty;
        }

        private static void AddKnown(Mutable mutable, GalaxyEntityId entity, ICollection<GalaxyEntityId> revealed)
        { if (mutable.KnownEntities.Add(entity)) revealed.Add(entity); }
        private static void AddRoute(Mutable mutable, SurveyRoute route, ICollection<GalaxyEntityId> entities,
            ICollection<GalaxyRouteId> routes)
        {
            if (mutable.KnownRoutes.Add(route.Id)) routes.Add(route.Id);
            AddKnown(mutable, route.TargetId, entities);
        }
        private static void Unlock(Mutable mutable, string achievement, ICollection<string> unlocked)
        { if (mutable.Achievements.Add(achievement)) unlocked.Add(achievement); }

        private static void EvaluateContracts(Mutable mutable, ICollection<string> notifications)
        {
            CompleteContract(mutable, "contract:scan-three-posts", mutable.ScannedPostCount >= 3, notifications);
            CompleteContract(mutable, "contract:reveal-two-routes", mutable.RevealedRouteCount >= 2, notifications);
            CompleteContract(mutable, "contract:complete-one-system", mutable.CompletedSystemCount >= 1, notifications);
            CompleteContract(mutable, "contract:complete-one-authored-constellation",
                mutable.CompletedAuthoredConstellationCount >= 1, notifications);
            var count = mutable.CompletedContracts.Count;
            mutable.ScannerLevel = count >= 4 ? 3 : count >= 2 ? 2 : 1;
        }
        private static void CompleteContract(Mutable mutable, string id, bool complete, ICollection<string> notifications)
        { if (complete && mutable.CompletedContracts.Add(id)) notifications.Add("CONTRACT COMPLETE // " + id.Substring("contract:".Length)); }

        private SurveyTransitionOutcome Rejected(string reason) => new(false, false, reason, state, null, null, null, null, "", "", "");
        private SurveyTransitionOutcome Accepted(bool changed) => new(true, changed, "", state, null, null, null, null, "", "", "");
        private static string StableToken(string value)
        {
            using var sha = SHA256.Create();
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(value));
            var result = new StringBuilder(24);
            for (var index = 0; index < 12; index++) result.Append(bytes[index].ToString("x2"));
            return result.ToString();
        }

        private sealed class Mutable
        {
            private readonly SurveyState original;
            public Mutable(SurveyState state)
            {
                original = state;
                KnownEntities = new HashSet<GalaxyEntityId>(state.KnownEntities);
                IdentifiedEntities = new HashSet<GalaxyEntityId>(state.IdentifiedEntities);
                KnownRoutes = new HashSet<GalaxyRouteId>(state.KnownRoutes);
                CompletedScans = new HashSet<GalaxyEntityId>(state.CompletedScans);
                CompletedConstellations = new HashSet<string>(state.CompletedConstellations, StringComparer.Ordinal);
                Discoveries = new HashSet<string>(state.Discoveries, StringComparer.Ordinal);
                CompletedContracts = new HashSet<string>(state.CompletedContracts, StringComparer.Ordinal);
                Achievements = new HashSet<string>(state.Achievements, StringComparer.Ordinal);
                ProcessedIntentIds = new HashSet<string>(state.ProcessedIntentIds, StringComparer.Ordinal);
                ProgressKeys = new HashSet<string>(state.ProcessedIntentIds.Where(id => id.StartsWith("progress:", StringComparison.Ordinal))
                    .Select(id => id.Substring("progress:".Length)), StringComparer.Ordinal);
                PersonalConstellations = new List<PersonalConstellationRecord>(state.PersonalConstellations);
                QueuedPersonalConstellationIds = new HashSet<string>(state.QueuedPersonalConstellationIds, StringComparer.Ordinal);
                PendingQueueId = state.PendingQueueId; ScannedPostCount = state.ScannedPostCount;
                RevealedRouteCount = state.RevealedRouteCount; CompletedSystemCount = state.CompletedSystemCount;
                CompletedAuthoredConstellationCount = state.CompletedAuthoredConstellationCount;
                ScannerLevel = state.ScannerLevel; AuthoredSequenceProgress = state.AuthoredSequenceProgress;
            }
            public HashSet<GalaxyEntityId> KnownEntities { get; }
            public HashSet<GalaxyEntityId> IdentifiedEntities { get; }
            public HashSet<GalaxyRouteId> KnownRoutes { get; }
            public HashSet<GalaxyEntityId> CompletedScans { get; }
            public HashSet<string> CompletedConstellations { get; }
            public HashSet<string> Discoveries { get; }
            public HashSet<string> CompletedContracts { get; }
            public HashSet<string> Achievements { get; }
            public HashSet<string> ProcessedIntentIds { get; }
            public HashSet<string> ProgressKeys { get; }
            public List<PersonalConstellationRecord> PersonalConstellations { get; }
            public HashSet<string> QueuedPersonalConstellationIds { get; }
            public string PendingQueueId { get; set; }
            public int ScannedPostCount { get; set; }
            public int RevealedRouteCount { get; set; }
            public int CompletedSystemCount { get; set; }
            public int CompletedAuthoredConstellationCount { get; set; }
            public int ScannerLevel { get; set; }
            public int AuthoredSequenceProgress { get; set; }
            public DateTime SavedUtc { get; set; }
            public SurveyState Freeze()
            {
                var processed = ProcessedIntentIds.Concat(ProgressKeys.Select(key => "progress:" + key));
                return new SurveyState(original.Snapshot, original.ProfileId, original.StartingSystemId,
                    KnownEntities, IdentifiedEntities, KnownRoutes, CompletedScans, CompletedConstellations,
                    Discoveries, CompletedContracts, Achievements, processed, PersonalConstellations,
                    QueuedPersonalConstellationIds, PendingQueueId, ScannedPostCount, RevealedRouteCount,
                    CompletedSystemCount, CompletedAuthoredConstellationCount, ScannerLevel,
                    AuthoredSequenceProgress, SavedUtc);
            }
        }
    }
}
