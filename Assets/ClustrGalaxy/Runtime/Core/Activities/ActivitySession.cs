using System;
using System.Collections.Generic;
using System.Linq;

namespace ClustrGalaxy.Core.Activities
{
    public sealed class ActivitySession
    {
        private readonly IActivityRepository repository;
        private readonly Dictionary<string, ActivityDefinition> definitions;
        private bool applying;
        private readonly Func<ConstellationDesign, bool> designEligibility;
        private readonly Func<FlightRecord,bool> flightEligibility;
        private readonly Func<ExpeditionPlan,bool> expeditionEligibility;
        private readonly Func<Domain.GalaxyEntityId,bool> visitEligibility;
        public ActivitySession(IActivityRepository repository, ActivityIdentity identity, IEnumerable<ActivityDefinition> definitions, Func<ConstellationDesign,bool> designEligibility = null, Func<FlightRecord,bool> flightEligibility = null, Func<ExpeditionPlan,bool> expeditionEligibility = null, Func<Domain.GalaxyEntityId,bool> visitEligibility = null)
        {
            this.designEligibility = designEligibility; this.flightEligibility=flightEligibility;this.expeditionEligibility=expeditionEligibility;this.visitEligibility=visitEligibility;
            this.repository = repository ?? throw new ArgumentNullException(nameof(repository));
            this.definitions = (definitions ?? throw new ArgumentNullException(nameof(definitions))).ToDictionary(x => x.Id, StringComparer.Ordinal);
            State = repository.LoadOrCreate(identity);
            if (State == null || !State.Identity.Equals(identity)) throw new InvalidOperationException("Activity profile/snapshot/mode mismatch.");
            Validate(State);
        }
        public ActivityState State { get; private set; }
        public event Action<ActivityOutcome> Outcome = delegate { };
        public ActivityOutcome Apply(ActivityIntent intent)
        {
            if (applying) throw new InvalidOperationException("Activity listeners cannot perform nested mutations.");
            if (intent == null || !intent.Identity.Equals(State.Identity)) throw new InvalidOperationException("Activity profile/snapshot/mode mismatch.");
            applying = true;
            try
            {
                if (State.ProcessedIntents.Contains(intent.Id)) return Publish(new ActivityOutcome(State, true, false));
                ActivityState candidate;
                try { candidate = BuildCandidate(intent); Validate(candidate); }
                catch (ArgumentException exception) { return Publish(new ActivityOutcome(State, false, false, exception.Message)); }
                try { repository.Save(candidate); }
                catch (Exception exception) { return Publish(new ActivityOutcome(State, false, false, "Could not save activity. Retry: " + exception.Message)); }
                State = candidate;
                return Publish(new ActivityOutcome(State, true, true));
            }
            finally { applying = false; }
        }
        private ActivityOutcome Publish(ActivityOutcome outcome) { Outcome(outcome); return outcome; }
        private ActivityState BuildCandidate(ActivityIntent intent)
        {
            var boards = State.EvidenceBoards.ToList();
            var records = State.Records.ToList(); var diary = State.Diary.ToList();
            var followed = State.FollowedTrail;var expedition=State.Expedition;
            var preferences = State.Preferences;
            var designs = State.Designs.ToList(); var flights=State.Flights.ToList(); var displays=State.Displays.ToList();
            if (intent.Operation == ActivityOperation.Bookmark)
                AddDiary(diary, new DiaryEntry(intent.Id, intent.Value, intent.Utc));
            else if(intent.Operation==ActivityOperation.SaveBookmark)
                diary.Add(new DiaryEntry(intent.Id,intent.Value,intent.Utc,true));
            else if(intent.Operation==ActivityOperation.RetainDiaryEntry||intent.Operation==ActivityOperation.DeleteBookmark)
            {
                var index=diary.FindIndex(x=>x.Id==intent.Value);if(index<0)throw new ArgumentException("The diary moment is no longer available.");
                var entry=diary[index];
                if(intent.Operation==ActivityOperation.DeleteBookmark)
                {
                    if(!entry.Retained)throw new ArgumentException("Only a saved bookmark can be removed explicitly.");diary.RemoveAt(index);
                    // An explicitly removed bookmark also leaves the observatory; automatic eviction does not.
                    displays.RemoveAll(x=>x.Kind==ObservatoryItemKind.Journal&&x.Reference==entry.Id);
                }
                else diary[index]=new DiaryEntry(entry.Id,entry.Text,entry.Utc,true);
            }
            else if(intent.Operation==ActivityOperation.StartExpedition)
            {
                if(intent.Expedition==null||expeditionEligibility?.Invoke(intent.Expedition)!=true)throw new ArgumentException("Expedition requires eligible loaded destinations.");
                if(expedition!=null&&(expedition.Status==ExpeditionStatus.Active||expedition.Status==ExpeditionStatus.Paused||expedition.Status==ExpeditionStatus.ReadyToClose))throw new ArgumentException("Close or abandon the current expedition first.");
                expedition=new ExpeditionRecord(intent.Expedition);
            }
            else if(intent.Operation==ActivityOperation.PauseExpedition||intent.Operation==ActivityOperation.ResumeExpedition||intent.Operation==ActivityOperation.AbandonExpedition||intent.Operation==ActivityOperation.CloseExpedition||intent.Operation==ActivityOperation.VisitExpeditionStop)
            {
                if(expedition==null||expedition.Status==ExpeditionStatus.Completed||expedition.Status==ExpeditionStatus.Abandoned)throw new ArgumentException("No unfinished expedition.");
                if(intent.Operation==ActivityOperation.PauseExpedition)
                {if(expedition.Status!=ExpeditionStatus.Active)throw new ArgumentException("Expedition is not active.");expedition=new ExpeditionRecord(expedition.Plan,expedition.Current,ExpeditionStatus.Paused);}
                else if(intent.Operation==ActivityOperation.ResumeExpedition)
                {if(expedition.Status!=ExpeditionStatus.Paused)throw new ArgumentException("Expedition is not paused.");expedition=new ExpeditionRecord(expedition.Plan,expedition.Current);}
                else if(intent.Operation==ActivityOperation.AbandonExpedition)expedition=new ExpeditionRecord(expedition.Plan,expedition.Current,ExpeditionStatus.Abandoned);
                else if(intent.Operation==ActivityOperation.CloseExpedition)
                {
                    if(expedition.Status!=ExpeditionStatus.ReadyToClose)throw new ArgumentException("Visit the remaining stops before closing this expedition.");
                    expedition=new ExpeditionRecord(expedition.Plan,expedition.Current,ExpeditionStatus.Completed);
                    AddDiary(diary,new DiaryEntry("expedition:"+expedition.Plan.Id,"A journey completed — "+expedition.Plan.Mood+" — "+expedition.Plan.Count+" stops. "+intent.Explanation,intent.Utc));
                }
                else
                {
                    if(expedition.Status!=ExpeditionStatus.Active||expedition.Plan.Mood==ExpeditionMood.PilotPractice||expedition.Plan.Stops[expedition.Current].Value!=intent.Value||visitEligibility?.Invoke(expedition.Plan.Stops[expedition.Current])!=true)throw new ArgumentException("The current eligible stop has not been reached.");
                    expedition=expedition.Advance();
                }
            }
            else if(intent.Operation==ActivityOperation.ClearDisplay)
            {
                if(!int.TryParse(intent.Value,System.Globalization.NumberStyles.None,System.Globalization.CultureInfo.InvariantCulture,out var slot)||slot<0||slot>=6)
                    throw new ArgumentException("Choose one of the six display slots.");
                displays.RemoveAll(x=>x.Slot==slot);
            }
            else if(intent.Operation==ActivityOperation.PlaceDisplay)
            {
                var display=intent.Display;
                if(display==null||!display.Eligible(State))throw new ArgumentException("Choose a discovered keepsake or saved local item.");
                var caption=display.Kind switch
                {
                    ObservatoryItemKind.Keepsake=>CampaignContent.Keepsake(display.Reference),
                    ObservatoryItemKind.Journal=>State.Diary.First(x=>x.Id==display.Reference).Text,
                    ObservatoryItemKind.Flight=>"Flight record — "+display.Reference,
                    _=>"Personal constellation — "+display.Reference
                };
                display=new ObservatoryDisplay(display.Slot,display.Kind,display.Reference,caption);
                var index=displays.FindIndex(x=>x.Slot==display.Slot);
                if(index<0)displays.Add(display);else displays[index]=display;
            }
            else if(intent.Operation==ActivityOperation.CompleteFlight)
            {
                var record=intent.Flight;
                if(record==null||flightEligibility?.Invoke(record)!=true)throw new ArgumentException("No completed session flight is available.");
                var index=flights.FindIndex(x=>x.Key==record.Key);
                if(index<0)flights.Add(record);else if(record.Improves(flights[index]))flights[index]=record;
                if(expedition?.Status==ExpeditionStatus.Active&&expedition.Plan.Mood==ExpeditionMood.PilotPractice&&expedition.CourseId==record.CourseId)expedition=expedition.Advance();
                AddDiary(diary,new DiaryEntry(intent.Id,"Flight completed — "+record.CourseId+" — "+record.Mode,intent.Utc));
            }
            else if (intent.Operation == ActivityOperation.SaveDesign)
            {
                var design = intent.Design;
                if (design == null || State.Identity.Mode != ActivityWorldMode.Survey || designEligibility?.Invoke(design) != true)
                    throw new ArgumentException("Save a constellation with identified members before editing its appearance.");
                var index = designs.FindIndex(d => d.PersonalId == design.PersonalId);
                if (index < 0) designs.Add(design); else designs[index] = design;
            }
            else if (intent.Operation == ActivityOperation.SetPreferences)
                preferences = intent.Preferences ?? throw new ArgumentException("Preferences are required.");
            else if (intent.Operation == ActivityOperation.UnpinTrail) followed = "";
            else
            {
                if (!definitions.TryGetValue(intent.ActivityId, out var definition)) throw new ArgumentException("Unknown authored activity.");
                if (State.Identity.Snapshot.StorageNamespace != "demo") throw new ArgumentException("Authored stories are available in the deterministic demo only.");
                var record = State.Find(definition.Id) ?? new ActivityRecord(definition.Id, definition.ContentVersion, null);
                if (intent.Operation == ActivityOperation.FollowTrail)
                {
                    if (definition.Kind != ActivityKind.Trail || record.Observations.Count == 0 || record.Completed)
                        throw new ArgumentException("Discover an unfinished trail before following it.");
                    followed = definition.Id;
                }
                else if (intent.Operation == ActivityOperation.Observe)
                {
                    if (!definition.Observations.Contains(intent.Value)) throw new ArgumentException("Observation does not belong to this activity.");
                    if (!record.Observations.Contains(intent.Value))
                    {
                        if (definition.Kind == ActivityKind.Trail && definition.Observations[record.Observations.Count] != intent.Value)
                            throw new ArgumentException("Find the current trail clue first.");
                        var observations = record.Observations.Concat(new[] { intent.Value }).ToArray();
                        var complete = definition.Kind != ActivityKind.Mystery && observations.Length == definition.Observations.Count;
                        record = new ActivityRecord(record.Id, record.ContentVersion, observations, complete);
                        Replace(records, record);
                        AddDiary(diary, new DiaryEntry("observation:" + record.Id + ":" + intent.Value,
                            definition.Title + " — " + (CampaignContent.Find(record.Id,intent.Value)?.Text ?? intent.Value), intent.Utc));
                        if (complete && followed == record.Id) followed = "";
                    }
                }
                else if (intent.Operation == ActivityOperation.SaveEvidenceBoard)
                {
                    var board = intent.EvidenceBoard ?? throw new ArgumentException("Choose an evidence board.");
                    board.Validate(definition, record, false);
                    boards.RemoveAll(x => x.ActivityId == board.ActivityId); boards.Add(board);
                }
                else if (intent.Operation == ActivityOperation.Interpret)
                {
                    if (definition.Kind != ActivityKind.Mystery || record.Observations.Count != definition.Observations.Count ||
                        !definition.Interpretations.Contains(intent.Value)) throw new ArgumentException("Gather all evidence and choose a supported interpretation.");
                    var board = intent.EvidenceBoard ?? boards.Find(x => x.ActivityId == definition.Id);
                    if (!record.Completed || board != null)
                    {
                        if (board == null) throw new ArgumentException("Arrange the evidence board before submitting an interpretation.");
                        board.Validate(definition, record, true);
                        boards.RemoveAll(x => x.ActivityId == board.ActivityId); boards.Add(board);
                    }
                    if (!record.Completed)
                    {
                        record = new ActivityRecord(record.Id, record.ContentVersion, record.Observations, true, intent.Value, intent.Explanation);
                        Replace(records, record);
                        AddDiary(diary, new DiaryEntry("interpretation:" + record.Id, definition.Title + " — " + (CampaignContent.Find(record.Id,intent.Value)?.Text ?? intent.Value), intent.Utc));
                    }
                    else if (record.Interpretation != intent.Value || record.Explanation != (intent.Explanation ?? string.Empty))
                    {
                        // A revised reading replaces the recorded answer; completion and its diary entry are unchanged.
                        record = new ActivityRecord(record.Id, record.ContentVersion, record.Observations, true, intent.Value, intent.Explanation);
                        Replace(records, record);
                    }
                }
                else throw new ArgumentException("Unsupported activity operation.");
            }
            return new ActivityState(State.Identity, records, State.ProcessedIntents.Concat(new[] { intent.Id }), diary, followed, preferences, designs, flights, displays, expedition, boards);
        }
        private static void Replace(List<ActivityRecord> records, ActivityRecord value)
        { var index = records.FindIndex(x => x.Id == value.Id); if (index < 0) records.Add(value); else records[index] = value; }
        private static void AddDiary(List<DiaryEntry> diary, DiaryEntry entry)
        {
            if (diary.Any(x => x.Id == entry.Id)) return;
            diary.Add(entry); if (diary.Count(x=>!x.Retained) > 200) diary.RemoveAt(diary.FindIndex(x=>!x.Retained));
        }
        private void Validate(ActivityState state)
        {
            if (state.Identity.Snapshot.StorageNamespace != "demo" && state.Records.Count != 0)
                throw new ArgumentException("Authored progress cannot be attached to a live snapshot.");
            foreach (var board in state.EvidenceBoards)
            {
                if (state.Identity.Snapshot.StorageNamespace != "demo" || !definitions.TryGetValue(board.ActivityId, out var definition))
                    throw new ArgumentException("Unknown demo evidence board.");
                board.Validate(definition, state.Find(board.ActivityId), false);
            }
            foreach (var record in state.Records)
            {
                if (!definitions.TryGetValue(record.Id, out var definition) || record.ContentVersion != definition.ContentVersion ||
                    record.Observations.Any(x => !definition.Observations.Contains(x))) throw new ArgumentException("Unknown activity content/version/observation.");
                if (definition.Kind == ActivityKind.Trail && !record.Observations.SequenceEqual(definition.Observations.Take(record.Observations.Count)))
                    throw new ArgumentException("Trail observations must form an ordered prefix.");
                var all = record.Observations.Count == definition.Observations.Count;
                if (definition.Kind == ActivityKind.Mystery)
                {
                    if (record.Completed != (all && definition.Interpretations.Contains(record.Interpretation)) ||
                        (!record.Completed && (record.Interpretation.Length != 0 || record.Explanation.Length != 0)))
                        throw new ArgumentException("Invalid mystery completion.");
                }
                else if (record.Completed != all || record.Interpretation.Length != 0 || record.Explanation.Length != 0)
                    throw new ArgumentException("Invalid activity completion.");
            }
            if (state.FollowedTrail.Length != 0)
            {
                var followed = state.Find(state.FollowedTrail);
                if (followed == null || followed.Completed || followed.Observations.Count == 0 || definitions[followed.Id].Kind != ActivityKind.Trail)
                    throw new ArgumentException("Invalid followed trail.");
            }
        }
    }
}
