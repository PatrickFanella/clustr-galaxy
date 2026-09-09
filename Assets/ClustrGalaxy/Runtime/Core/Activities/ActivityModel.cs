using System;
using System.Collections.Generic;
using System.Linq;
using ClustrGalaxy.Core.Survey;

namespace ClustrGalaxy.Core.Activities
{
    public enum ActivityWorldMode { Explore, Survey }
    public enum ActivityKind { Trail, Anomaly, Mystery }
    public readonly struct ActivityIdentity : IEquatable<ActivityIdentity>
    {
        public ActivityIdentity(string profileId, SurveySnapshotId snapshot, ActivityWorldMode mode)
        {
            if (string.IsNullOrWhiteSpace(profileId)) throw new ArgumentException("Profile is required.");
            if (!Enum.IsDefined(typeof(ActivityWorldMode), mode)) throw new ArgumentException("Unknown world mode.");
            // Validate using the same v2 path policy as Survey.
            Persistence.ProfilePaths.Survey(profileId, snapshot.StorageNamespace, snapshot.RevisionId, snapshot.CatalogId);
            ProfileId = profileId; Snapshot = snapshot; Mode = mode;
        }
        public string ProfileId { get; }
        public SurveySnapshotId Snapshot { get; }
        public ActivityWorldMode Mode { get; }
        public bool Equals(ActivityIdentity other) => ProfileId == other.ProfileId && Snapshot == other.Snapshot && Mode == other.Mode;
        public override bool Equals(object value) => value is ActivityIdentity other && Equals(other);
        public override int GetHashCode() => (Snapshot.GetHashCode() * 397 ^ ProfileId.GetHashCode()) * 397 ^ (int)Mode;
    }
    public sealed class ActivityDefinition
    {
        public ActivityDefinition(string id, string title, ActivityKind kind, IEnumerable<string> observations,
            IEnumerable<string> interpretations = null, int contentVersion = 1)
        {
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(title) || contentVersion < 1)
                throw new ArgumentException("An activity requires stable identity, title and version.");
            if (!Enum.IsDefined(typeof(ActivityKind), kind)) throw new ArgumentException("Unknown activity kind.");
            var steps = (observations ?? throw new ArgumentNullException(nameof(observations))).ToArray();
            var choices = (interpretations ?? Array.Empty<string>()).ToArray();
            if (steps.Length == 0 || steps.Any(string.IsNullOrWhiteSpace) || steps.Distinct(StringComparer.Ordinal).Count() != steps.Length)
                throw new ArgumentException("Observations must be unique and nonempty.");
            if ((kind == ActivityKind.Mystery && choices.Length != 3) ||
                choices.Any(string.IsNullOrWhiteSpace) || choices.Distinct(StringComparer.Ordinal).Count() != choices.Length ||
                (kind != ActivityKind.Mystery && choices.Length != 0)) throw new ArgumentException("Invalid interpretations.");
            Id = id; Title = title; Kind = kind; ContentVersion = contentVersion;
            Observations = Array.AsReadOnly(steps); Interpretations = Array.AsReadOnly(choices);
        }
        public string Id { get; }
        public string Title { get; }
        public ActivityKind Kind { get; }
        public int ContentVersion { get; }
        public IReadOnlyList<string> Observations { get; }
        public IReadOnlyList<string> Interpretations { get; }
    }
    public sealed class ActivityRecord
    {
        public ActivityRecord(string id, int version, IEnumerable<string> observations, bool completed = false,
            string interpretation = "", string explanation = "")
        {
            if (string.IsNullOrWhiteSpace(id) || version < 1) throw new ArgumentException("Invalid activity record identity.");
            var values = (observations ?? Array.Empty<string>()).ToArray();
            if (values.Any(string.IsNullOrWhiteSpace) || values.Distinct(StringComparer.Ordinal).Count() != values.Length)
                throw new ArgumentException("Invalid observation identities.");
            if (explanation == null || explanation.Length > 500 || explanation.Any(char.IsControl)) throw new ArgumentException("Invalid explanation.");
            Id = id; ContentVersion = version; Observations = Array.AsReadOnly(values); Completed = completed;
            Interpretation = interpretation ?? ""; Explanation = explanation;
        }
        public string Id { get; }
        public int ContentVersion { get; }
        public IReadOnlyList<string> Observations { get; }
        public bool Completed { get; }
        public string Interpretation { get; }
        public string Explanation { get; }
    }
    public sealed class DiaryEntry
    {
        public DiaryEntry(string id, string text, DateTime utc, bool retained = false)
        {
            if (string.IsNullOrWhiteSpace(id) || id.Length > 256 || string.IsNullOrWhiteSpace(text) || text.Length > 1000 ||
                text.Any(char.IsControl) || utc.Kind != DateTimeKind.Utc) throw new ArgumentException("Invalid diary entry.");
            Id = id; Text = text; Utc = utc; Retained = retained;
        }
        public string Id { get; }
        public bool Retained { get; }
        public string Text { get; }
        public DateTime Utc { get; }
    }
    public sealed class ActivityState
    {
        public const int SchemaVersion = 1;
        public ActivityState(ActivityIdentity identity, IEnumerable<ActivityRecord> records = null,
            IEnumerable<string> processed = null, IEnumerable<DiaryEntry> diary = null, string followedTrail = "", ActivityPreferences preferences = null, IEnumerable<ConstellationDesign> designs = null, IEnumerable<FlightRecord> flights = null, IEnumerable<ObservatoryDisplay> displays = null, ExpeditionRecord expedition = null, IEnumerable<EvidenceBoard> evidenceBoards = null)
        {
            var entries = (records ?? Array.Empty<ActivityRecord>()).ToArray();
            var intents = (processed ?? Array.Empty<string>()).ToArray();
            var notes = (diary ?? Array.Empty<DiaryEntry>()).ToArray();
            if (entries.Any(x => x == null) || entries.Select(x => x.Id).Distinct(StringComparer.Ordinal).Count() != entries.Length ||
                intents.Any(string.IsNullOrWhiteSpace) || intents.Distinct(StringComparer.Ordinal).Count() != intents.Length ||
                notes.Any(x => x == null) || notes.Count(x=>!x.Retained) > 200 || notes.Select(x => x.Id).Distinct(StringComparer.Ordinal).Count() != notes.Length)
                throw new ArgumentException("Invalid or duplicate activity state.");
            var styles = (designs ?? Array.Empty<ConstellationDesign>()).ToArray();
            if (styles.Any(x => x == null) || styles.Select(x => x.PersonalId).Distinct(StringComparer.Ordinal).Count() != styles.Length ||
                (styles.Length > 0 && identity.Mode != ActivityWorldMode.Survey)) throw new ArgumentException("Invalid constellation companions.");
            var flightRecords = (flights ?? Array.Empty<FlightRecord>()).ToArray();
            if (flightRecords.Any(x=>x==null) || flightRecords.Select(x=>x.Key).Distinct().Count()!=flightRecords.Length)
                throw new ArgumentException("Duplicate flight records.");
            if(flightRecords.Any(x=>x.Ghost!=null&&(x.Ghost.Replay.Identity.Snapshot!=identity.Snapshot||x.Ghost.Replay.Identity.WorldMode!=identity.Mode)))
                throw new ArgumentException("Ghost snapshot or world mode mismatch.");
            var exhibit=(displays??Array.Empty<ObservatoryDisplay>()).ToArray();
            if(exhibit.Length>6||exhibit.Any(x=>x==null)||exhibit.Select(x=>x.Slot).Distinct().Count()!=exhibit.Length)throw new ArgumentException("Invalid observatory slots.");
            var boards = (evidenceBoards ?? Array.Empty<EvidenceBoard>()).ToArray();
            if (boards.Any(x => x == null) || boards.Select(x => x.ActivityId).Distinct(StringComparer.Ordinal).Count() != boards.Length)
                throw new ArgumentException("Duplicate evidence boards.");
            EvidenceBoards = Array.AsReadOnly(boards);
            Expedition=expedition;
            Displays=Array.AsReadOnly(exhibit);
            Flights=Array.AsReadOnly(flightRecords);
            Designs = Array.AsReadOnly(styles);
            Preferences = preferences ?? new ActivityPreferences();
            Identity = identity; Records = Array.AsReadOnly(entries); ProcessedIntents = Array.AsReadOnly(intents);
            Diary = Array.AsReadOnly(notes); FollowedTrail = followedTrail ?? "";
        }
        public ActivityIdentity Identity { get; }
        public ActivityPreferences Preferences { get; }
        public IReadOnlyList<ConstellationDesign> Designs { get; }
        public IReadOnlyList<FlightRecord> Flights { get; }
        public IReadOnlyList<ObservatoryDisplay> Displays {get;}
        public IReadOnlyList<EvidenceBoard> EvidenceBoards { get; }
        public ExpeditionRecord Expedition {get;}
        public IReadOnlyList<ActivityRecord> Records { get; }
        public IReadOnlyList<string> ProcessedIntents { get; }
        public IReadOnlyList<DiaryEntry> Diary { get; }
        public string FollowedTrail { get; }
        public ActivityRecord Find(string id) { for(var i=0;i<Records.Count;i++)if(Records[i].Id==id)return Records[i];return null; }
    }
    public enum ActivityOperation { Observe, FollowTrail, UnpinTrail, Interpret, Bookmark, SetPreferences, SaveDesign, CompleteFlight, PlaceDisplay, StartExpedition, PauseExpedition, ResumeExpedition, AbandonExpedition, VisitExpeditionStop, CloseExpedition, SaveEvidenceBoard, ClearDisplay, SaveBookmark, RetainDiaryEntry, DeleteBookmark }
    public sealed class ActivityIntent
    {
        public ActivityIntent(string id, ActivityIdentity identity, ActivityOperation operation, DateTime utc,
            string activityId = "", string value = "", string explanation = "", ActivityPreferences preferences = null, ConstellationDesign design = null, FlightRecord flight = null, ObservatoryDisplay display = null, ExpeditionPlan expedition = null, EvidenceBoard evidenceBoard = null)
        {
            if (string.IsNullOrWhiteSpace(id) || id.Length > 256 || utc.Kind != DateTimeKind.Utc ||
                !Enum.IsDefined(typeof(ActivityOperation), operation)) throw new ArgumentException("Invalid activity intent.");
            Id = id; Identity = identity; Operation = operation; Utc = utc; ActivityId = activityId ?? "";
            Value = value ?? ""; Explanation = explanation ?? ""; Preferences = preferences; Design = design; Flight = flight; Display=display; Expedition=expedition; EvidenceBoard=evidenceBoard;
        }
        public string Id { get; }
        public ActivityIdentity Identity { get; }
        public ActivityPreferences Preferences { get; }
        public ConstellationDesign Design { get; }
        public FlightRecord Flight { get; }
        public ObservatoryDisplay Display {get;}
        public ExpeditionPlan Expedition {get;}
        public EvidenceBoard EvidenceBoard {get;}
        public ActivityOperation Operation { get; }
        public DateTime Utc { get; }
        public string ActivityId { get; }
        public string Value { get; }
        public string Explanation { get; }
    }
    public sealed class ActivityOutcome
    {
        public ActivityOutcome(ActivityState state, bool committed, bool changed, string error = "")
        { State = state; Committed = committed; Changed = changed; Error = error; }
        public ActivityState State { get; }
        public ActivityIdentity Identity => State.Identity;
        public bool Committed { get; }
        public bool Changed { get; }
        public string Error { get; }
    }
    public interface IActivityRepository
    {
        ActivityState LoadOrCreate(ActivityIdentity identity);
        void Save(ActivityState state);
    }
    public sealed class MemoryActivityRepository : IActivityRepository
    {
        private readonly Dictionary<ActivityIdentity, ActivityState> states = new();
        public ActivityState LoadOrCreate(ActivityIdentity identity) => states.TryGetValue(identity, out var state) ? state : new ActivityState(identity);
        public void Save(ActivityState state) => states[state.Identity] = state;
    }
}
