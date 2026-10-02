using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using ClustrGalaxy.Core.Data;
using ClustrGalaxy.Core.Activities;
using ClustrGalaxy.Core.Domain;
using ClustrGalaxy.Core.Navigation;
using ClustrGalaxy.Core.Presentation;
using ClustrGalaxy.Core.Survey;
using ClustrGalaxy.Core.World;

namespace ClustrGalaxy.Core.Session
{
    public sealed partial class GalaxySession : IDisposable
    {
        public FlightSimulationState Flight { get; } = new();
        public CockpitInputContext CockpitInput { get; } = new();
        public GalaxyTravel Travel { get; private set; }
        public GalaxyEntityId CurrentSystemId { get; private set; }
        private GalaxyEntityId jumpingTo, demoHomeSystem;
        private readonly GalaxyEntityId demoStartingSystem;
        private string TravelEligibility(TravelIntent intent)
        {
            if (Flight.Recovering) return "Resume flight after recovery before engaging a drive.";
            if (!Resident.TryFindLoaded(intent.Target, out var destination) ||
                (!explore && Survey?.State.IsKnown(intent.Target) != true)) return "The destination is not resident and known.";
            if (intent.Action == TravelAction.Cruise)
                return (destination.Kind == GalaxyEntityKind.Post && (destination.SystemId == CurrentSystemId ||
                    destination.SystemId.IsEmpty && snapshots.Mode == SnapshotDataMode.DeterministicDemo && CurrentSystemId == demoHomeSystem)) ||
                    (destination.Kind == GalaxyEntityKind.Subreddit && destination.Id == CurrentSystemId) ?
                    "" : "System cruise requires a body in the current system.";
            if (destination.Kind != GalaxyEntityKind.Subreddit || intent.Target == CurrentSystemId || CurrentSystemId.IsEmpty)
                return "A jump requires a different connected system.";
            foreach (var scene in snapshots.Scenes.Values)
                foreach (var route in scene.Routes)
                    if ((route.SourceId == CurrentSystemId && route.TargetId == intent.Target ||
                        !route.Directed && route.TargetId == CurrentSystemId && route.SourceId == intent.Target) &&
                        Resident.LoadedRoutes.Any(value => value.Id == route.Id) &&
                        (explore || Survey?.State.KnownRoutes.Contains(route.Id) == true)) return "";
            return "No eligible published connection links the current system to that destination.";
        }
        private void TravelChanged(TravelOutcome outcome)
        {
            if (outcome.State.Phase == TravelPhase.Charging || outcome.State.Phase == TravelPhase.JumpCharging)
            {
                CancelFlightActivity(); Navigation.Apply(new ClearFlightGuidanceIntent());
                if (outcome.State.Action == TravelAction.Jump) jumpingTo = outcome.State.Target;
            }
            if (outcome.Aborted) { snapshots.CancelPending(); preparedJump = null; }
            if (outcome.State.Phase == TravelPhase.LocalFlight)
            {
                if (!outcome.Aborted && outcome.State.Action == TravelAction.Jump) CurrentSystemId = jumpingTo;
                jumpingTo = default;
            }
            RefreshNavigationReferences();
        }

        private ExpeditionPlan pendingExpedition;
        private GalaxyEntityId pendingVisit;
        private ActivityIntent pendingExpeditionIntent;
        public string ExpeditionError {get;private set;}="";
        private bool CanStartExpedition(ExpeditionPlan plan)
        {
            if(!ReferenceEquals(plan,pendingExpedition))return false;
            foreach(var id in plan.Stops)if(!Resident.TryFindLoaded(id,out _)||(!explore&&Survey?.State.IsIdentified(id)!=true))return false;
            if(plan.Mood==ExpeditionMood.UnexpectedConnection)
                for(var i=1;i<plan.Stops.Count;i++)if(!Resident.LoadedRoutes.Any(x=>(explore||Survey?.State.KnownRoutes.Contains(x.Id)==true)&&
                    ((x.SourceId==plan.Stops[i-1]&&x.TargetId==plan.Stops[i])||(x.TargetId==plan.Stops[i-1]&&x.SourceId==plan.Stops[i]))))return false;
            return true;
        }
        private bool CanVisitExpedition(GalaxyEntityId id)=>id==pendingVisit&&Resident.TryFindLoaded(id,out _)&&(explore||Survey?.State.IsIdentified(id)==true);
        public ActivityOutcome StartExpedition(ExpeditionMood mood)
        {
            if(Activities==null)throw new InvalidOperationException("Activity storage unavailable.");
            var eligible=Resident.LoadedEntities.Where(x=>x.Kind!=GalaxyEntityKind.Comment&&x.Kind!=GalaxyEntityKind.User&&(explore||Survey?.State.IsIdentified(x.Id)==true)).Select(x=>x.Id);
            var routes=Resident.LoadedRoutes.Where(x=>explore||Survey?.State.KnownRoutes.Contains(x.Id)==true);
            pendingExpedition=ExpeditionPlan.Select(mood,eligible,routes);
            return ChangeExpedition(ActivityOperation.StartExpedition,pendingExpedition);
        }
        public ActivityOutcome ChangeExpedition(ActivityOperation operation,ExpeditionPlan plan=null)
        {
            if(operation!=ActivityOperation.StartExpedition&&operation!=ActivityOperation.PauseExpedition&&operation!=ActivityOperation.ResumeExpedition&&operation!=ActivityOperation.AbandonExpedition&&operation!=ActivityOperation.CloseExpedition)throw new ArgumentException("Invalid expedition command.");
            pendingExpeditionIntent=new ActivityIntent(Guid.NewGuid().ToString("N"),Activities.State.Identity,operation,DateTime.UtcNow,expedition:plan);
            return RetryExpedition();
        }
        public ActivityOutcome VisitExpedition(GalaxyEntityId id,double distance)
        {
            if (Travel?.State.Active == true || Flight.Recovering) return null;
            var trip=Activities?.State.Expedition;
            if(trip?.Status!=ExpeditionStatus.Active||trip.Plan.Mood==ExpeditionMood.PilotPractice||ExpeditionError.Length!=0||trip.Plan.Stops[trip.Current]!=id||double.IsNaN(distance)||double.IsInfinity(distance)||distance<0||distance>220)return null;
            pendingVisit=id;
            pendingExpeditionIntent=new ActivityIntent("visit:"+trip.Plan.Id+":"+trip.Current,Activities.State.Identity,ActivityOperation.VisitExpeditionStop,DateTime.UtcNow,value:id.Value);
            return RetryExpedition();
        }
        public ActivityOutcome RetryExpedition()
        {
            if(pendingExpeditionIntent==null)return null;
            var outcome=Activities.Apply(pendingExpeditionIntent);ExpeditionError=outcome.Committed?"":outcome.Error;
            if(outcome.Committed){pendingExpeditionIntent=null;pendingExpedition=null;pendingVisit=default;SyncExpeditionReferences();}
            return outcome;
        }
        public void SyncExpeditionReferences()
        {
            ReleaseActivityEntities("expedition");var trip=Activities?.State.Expedition;
            if(trip?.Status!=ExpeditionStatus.Active||trip.Plan.Mood==ExpeditionMood.PilotPractice)return;
            var eligible=trip.Plan.Stops.Where(x=>Resident.TryFindLoaded(x,out _)&&(explore||Survey?.State.IsIdentified(x)==true));
            RetainActivityEntities("expedition",eligible);
        }
        private ActivitySession campaignOwner;
        private CampaignDiscovery campaign;
        public CampaignDiscovery Campaign
        {
            get
            {
                if(Activities==null)return null;
                if(!ReferenceEquals(campaignOwner,Activities)){campaignOwner=Activities;campaign=new CampaignDiscovery(Activities);}
                return campaign;
            }
        }
        public JourneyReplayBuffer ReplayBuffer { get; } = new();
        public FlightActivityRun FlightActivity { get; private set; }
        public FlightGhost SelectedPracticeGhost {get;private set;}
        public void SelectPracticeGhost(JourneyReplay replay)
        {
            if(replay==null||!replay.CourseAnchor.HasValue||Activities==null||replay.Identity.Snapshot!=Activities.State.Identity.Snapshot||replay.Identity.WorldMode!=Activities.State.Identity.Mode||replay.Identity.FlightMode==NavigationFlightMode.Autopilot)
                throw new InvalidOperationException("This recording is standalone playback only; a matching snapshot and explicit course frame are required.");
            if(replay.Identity.ContentVersion!=1||replay.Identity.CourseVersion!=FlightCourse.CurrentVersion||
                (replay.Identity.CourseId!="ring-run"&&replay.Identity.CourseId!="figure-eight"&&replay.Identity.CourseId!="stable-orbit"))
                throw new InvalidOperationException("Only current playground courses support imported practice ghosts; standalone playback remains available.");
            SelectedPracticeGhost=new FlightGhost(replay,replay.CourseAnchor.Value);
        }
        public void ClearPracticeGhost()
        {if(FlightActivity?.BestGhost==SelectedPracticeGhost)FlightActivity?.AttachGhost(null);SelectedPracticeGhost=null;}
        private FlightRecord pendingFlightRecord;
        public string FlightError {get;private set;}="";
        public void StartFlightActivity(FlightCourse course, NavigationFlightMode mode, bool preview, string tuning,
            IEnumerable<GalaxyEntityId> anchors, GalaxyRouteId routeId=default)
        {
            if (Travel?.State.Active == true || Flight.Recovering) throw new InvalidOperationException("End travel or recovery before starting a flight activity.");
            if(Activities==null)throw new InvalidOperationException("Activity storage is unavailable.");
            // A completed record whose save failed stays retryable; a new run would discard it.
            if(pendingFlightRecord!=null)throw new InvalidOperationException("Retry saving the completed flight record before starting another.");
            var ids=anchors.ToArray();
            if(course.Kind==FlightActivityKind.Scenic)
            {
                var route=Resident.LoadedRoutes.FirstOrDefault(r=>r.Id==routeId);
                if(route==null||!ids.Contains(route.SourceId)||!ids.Contains(route.TargetId)||
                    (!explore&&(Survey==null||!Survey.State.KnownRoutes.Contains(routeId))))
                    throw new InvalidOperationException("A published visible route with resident endpoints is required.");
            }
            var candidate=new FlightActivityRun(course,mode,preview,tuning,Activities.State.Identity,Activities.State.Preferences.SaveBestGhosts);
            var previousBest=Activities.State.Flights.FirstOrDefault(x=>x.CourseId==course.Id&&x.Version==course.Version&&x.Mode==mode&&x.TuningId==tuning);
            candidate.AttachGhost(SelectedPracticeGhost?.Compatible(candidate)==true?SelectedPracticeGhost:previousBest?.Ghost);
            RetainActivityEntities("flight",ids);
            Navigation.Apply(new ClearFlightGuidanceIntent());RefreshNavigationReferences();
            FlightActivity?.Cancel();FlightActivity=candidate;pendingFlightRecord=null;FlightError="";
        }
        public void CancelFlightActivity()
        { FlightActivity?.Cancel();ReleaseActivityEntities("flight");pendingFlightRecord=null; }
        public void AdvanceFlightActivity(ReplayPosition position,double delta,NavigationFlightMode mode,Quaternion? rotation=null)
        {
            var run=FlightActivity;if(run==null||!run.Active)return;
            run.Advance(position,delta,mode,rotation);
            if(!run.Active)
            {
                ReleaseActivityEntities("flight");
                if(run.Completed&&!run.Preview&&run.Course.Kind!=FlightActivityKind.Scenic)
                { pendingFlightRecord=run.Result();CommitFlightActivity(); }
            }
        }
        public ActivityOutcome CommitFlightActivity()
        {
            if(pendingFlightRecord==null||Activities==null)return null;
            var outcome=Activities.Apply(new ActivityIntent("flight:"+FlightActivity.Id,Activities.State.Identity,
                ActivityOperation.CompleteFlight,DateTime.UtcNow,flight:pendingFlightRecord));
            FlightError=outcome.Committed?"":outcome.Error;
            if(outcome.Committed)pendingFlightRecord=null;
            return outcome;
        }
        private bool CanCommitFlight(FlightRecord record)=>ReferenceEquals(record,pendingFlightRecord);


        private readonly GalaxySnapshotSession snapshots;
        private readonly ISurveyRepository surveyRepository;
        private readonly string profileId;
        private bool explore = true;
        private readonly IActivityRepository activityRepository;
        private ActivitySession exploreActivities, surveyActivities;
        private readonly HashSet<string> activityOwners = new(StringComparer.Ordinal);
        public ActivitySession Activities => explore ? exploreActivities : surveyActivities;
        public bool ReconcileSurveyActivityDiary()
        {
            if (Survey == null || surveyActivities == null) return false;
            foreach (var discovery in Survey.State.Discoveries)
            {
                if (!discovery.StartsWith("clean-signal:", StringComparison.Ordinal)) continue;
                var key = "survey:" + discovery;
                if (surveyActivities.State.ProcessedIntents.Contains(key)) continue;
                var outcome = surveyActivities.Apply(new ActivityIntent(key, surveyActivities.State.Identity,
                    ActivityOperation.Bookmark, Survey.State.SavedUtc ?? DateTime.UtcNow,
                    value: "Clean signal — artistic tuning — " + discovery.Substring("clean-signal:".Length)));
                if (!outcome.Committed) { ActivityError = outcome.Error; return false; }
            }
            ActivityError = "";
            return true;
        }
        public string ActivityError { get; private set; } = "";

        public GalaxySession(IGalaxySnapshotSource snapshotSource, ISurveyRepository surveyRepository, string profileId, IActivityRepository activityRepository = null, GalaxyEntityId demoStartingSystem = default)
        {
            snapshots = new GalaxySnapshotSession(snapshotSource);
            this.demoStartingSystem = demoStartingSystem;
            this.surveyRepository = surveyRepository ?? throw new ArgumentNullException(nameof(surveyRepository));
            if (string.IsNullOrWhiteSpace(profileId)) throw new ArgumentException("A profile ID is required.", nameof(profileId));
            this.profileId = profileId;
            this.activityRepository = activityRepository ?? new MemoryActivityRepository();
            Resident = new ResidentGalaxy();
            Navigation = new GalaxyNavigation();
            Hud = new ProbeHudPresenter();
        }

        public event Action<ResidentGalaxyChangeSet> ResidentChanged = delegate { };
        public ResidentGalaxy Resident { get; }
        public void SetSelection(GalaxyEntityId id) => Publish(Resident.SetReferences("selection", new[] { id }));
        private bool CanSaveDesign(ConstellationDesign design)
        {
            if (explore || Survey == null) return false;
            var personal = Survey.State.PersonalConstellations.FirstOrDefault(item => item.Id == design.PersonalId);
            return personal != null && personal.Members.SequenceEqual(design.Nodes.Select(node => node.Id)) &&
                design.Nodes.All(node => Survey.State.IsIdentified(node.Id));
        }
        public void RetainActivityEntities(string activityId, IEnumerable<GalaxyEntityId> ids)
        {
            if (string.IsNullOrWhiteSpace(activityId)) throw new ArgumentException("Activity owner is required.");
            var entities = (ids ?? throw new ArgumentNullException(nameof(ids))).Distinct().ToArray();
            foreach (var id in entities)
                if (!Resident.TryFindLoaded(id, out _) || (!explore && (Survey == null || !Survey.State.IsKnown(id))))
                    throw new InvalidOperationException("Activity references require resident, visible entities.");
            activityOwners.Add(activityId);
            Publish(Resident.SetReferences("activity:" + activityId, entities));
        }
        public void ReleaseActivityEntities(string activityId)
        {
            if (!activityOwners.Remove(activityId)) return;
            Publish(Resident.SetReferences("activity:" + activityId, Array.Empty<GalaxyEntityId>()));
        }
        private void ReleaseAllActivities()
        {
            foreach (var owner in activityOwners.ToArray()) ReleaseActivityEntities(owner);
        }
        public void ReleaseScope(SpatialScopeRequest scope)
        {
            RefreshNavigationReferences();
            Publish(Resident.Release(scope));
            snapshots.ReleaseScene(scope);
        }
        public void RefreshNavigationReferences()
        {
            var state = Navigation.State;
            var ids = new List<GalaxyEntityId> { state.FocusedId, state.SelectedId, state.PendingTravelId, state.PlottedDestinationId, Navigation.Orbit.TargetId, Flight.RecoveryTarget, Travel?.State.Target ?? default };
            foreach (var frame in state.History) ids.Add(frame.Id);
            Publish(Resident.SetReferences("navigation", ids));
        }
        private ResidentGalaxyChangeSet Publish(ResidentGalaxyChangeSet change)
        { if (!change.IsEmpty) ResidentChanged(change); return change; }
        public GalaxyNavigation Navigation { get; }
        public ProbeHudPresenter Hud { get; }
        public SurveySession Survey { get; private set; }
        public GalaxySnapshotSession Snapshots => snapshots;

        public async Task<SnapshotLoadOutcome> OpenAsync(CancellationToken cancellationToken = default)
        {
            var outcome = await snapshots.OpenAsync(cancellationToken);
            if (outcome.Succeeded)
            {
                if (snapshots.Mode == SnapshotDataMode.DeterministicDemo && !demoStartingSystem.IsEmpty &&
                    !outcome.Scene.Nodes.Any(node => node.Id == demoStartingSystem && node.Kind == GalaxyEntityKind.Subreddit))
                    return new SnapshotLoadOutcome(outcome.OperationId, false, false, SnapshotDataMode.Failed, snapshots.Manifest, null,
                        "AUTHORED START UNAVAILABLE", "The authored departure system is missing.");
                ApplyScene(outcome.Scene);
                if (snapshots.Mode == SnapshotDataMode.DeterministicDemo)
                    CurrentSystemId = demoHomeSystem = !demoStartingSystem.IsEmpty ? demoStartingSystem : outcome.Scene.Nodes.FirstOrDefault(node => node.Kind == GalaxyEntityKind.Subreddit)?.Id ?? default;
                Travel = new GalaxyTravel(Flight.SessionId, snapshots.Manifest.SurveySnapshot, TravelEligibility, operation => committingJump && preparedJump?.OperationId == operation);
                Travel.Changed += TravelChanged;
                try
                {
                    exploreActivities = new ActivitySession(activityRepository,
                        new ActivityIdentity(profileId, snapshots.Manifest.SurveySnapshot, ActivityWorldMode.Explore), AuthoredActivityCatalog.Definitions, flightEligibility: CanCommitFlight, expeditionEligibility: CanStartExpedition, visitEligibility: CanVisitExpedition);
                    surveyActivities = new ActivitySession(activityRepository,
                        new ActivityIdentity(profileId, snapshots.Manifest.SurveySnapshot, ActivityWorldMode.Survey), AuthoredActivityCatalog.Definitions, CanSaveDesign, CanCommitFlight, CanStartExpedition, CanVisitExpedition);
                    ActivityError = "";
                }
                catch (Exception exception) { ActivityError = exception.Message; }
            }
            return outcome;
        }

        public async Task<SnapshotLoadOutcome> LoadAsync(SpatialScopeRequest request,
            CancellationToken cancellationToken = default)
        {
            var outcome = await snapshots.LoadAsync(request, cancellationToken);
            if (outcome.Succeeded) ApplyScene(outcome.Scene);
            return outcome;
        }

        public Task<SnapshotLoadOutcome> StageSceneAsync(SpatialScopeRequest request,
            CancellationToken cancellationToken = default) => snapshots.StageAsync(request, cancellationToken);

        public SnapshotLoadOutcome CommitStagedScene(SnapshotLoadOutcome receipt)
        {
            var outcome = snapshots.CommitStaged(receipt);
            if (outcome.Succeeded) ApplyScene(outcome.Scene);
            return outcome;
        }

        public SurveyTransitionOutcome StartSurvey(GalaxyEntityId startingSystemId,
            IEnumerable<GalaxyEntityId> seedIdentifiedEntities, string intentId, DateTime utc)
        {
            if (snapshots.Manifest == null) throw new InvalidOperationException("A pinned snapshot must be open before Survey starts.");
            var snapshot = snapshots.Manifest.SurveySnapshot;
            var candidate = Survey ?? new SurveySession(surveyRepository, profileId, snapshot, startingSystemId);
            if (candidate.State.StartingSystemId != startingSystemId)
                throw new InvalidOperationException("The active Survey session has a different starting system.");
            var outcome = candidate.Apply(new StartSurveyIntent(intentId, snapshot, utc, startingSystemId, seedIdentifiedEntities));
            if (outcome.Succeeded)
            {
                Survey = candidate;
                Navigation.Apply(new ChooseSurveyStartIntent(startingSystemId));
                RefreshNavigationReferences();
                ApplySurveyVisibility(false);
            }
            return outcome;
        }

        public SurveyTransitionOutcome ApplySurvey(SurveyIntent intent)
        {
            if (Survey == null) throw new InvalidOperationException("Survey has not started.");
            if (explore) throw new InvalidOperationException("Explore cannot mutate Survey progress.");
            var outcome = Survey.Apply(intent);
            if (outcome.Succeeded) ApplySurveyVisibility(false);
            return outcome;
        }

        public NavigationOutcome AdvanceNavigationPhysics(NavigationPose pose, Vector3 velocity,
            NavigationFlightMode flightMode, Func<GalaxyEntityId, NavigationTarget> resolve, float brakeAcceleration,
            DateTime utc)
        {
            var outcome = Navigation.AdvancePhysics(pose, velocity, flightMode, resolve, brakeAcceleration);
            if (!outcome.Effect.MasteryArrival.IsEmpty && Survey != null && !explore)
            {
                var id = "mastery:" + outcome.Effect.MasteryArrival.Value + ":" + utc.Ticks;
                ApplySurvey(new PilotMasteryArrivalIntent(id, Survey.State.Snapshot, utc, outcome.Effect.MasteryArrival));
            }
            return outcome;
        }

        public ResidentGalaxyChangeSet ApplySurveyVisibility(bool explore)
        {
            if (this.explore != explore) { Travel?.Abort(Travel.State.OperationId, "WORLD MODE CHANGED · TRAVEL ABORTED"); CancelFlightActivity(); ClearPracticeGhost(); ReleaseAllActivities(); ReplayBuffer.Clear(); pendingExpeditionIntent=null;pendingExpedition=null;pendingVisit=default;ExpeditionError=""; }
            this.explore = explore;
            return Publish(Resident.ApplySurveyVisibility(explore,
                Survey?.State.KnownEntities ?? Array.Empty<GalaxyEntityId>(),
                Survey?.State.KnownRoutes ?? Array.Empty<GalaxyRouteId>()));
        }

        public void Dispose() { Travel?.Abort(Travel.State.OperationId, "SESSION ENDED · TRAVEL ABORTED"); if (Travel != null) Travel.Changed -= TravelChanged; CancelFlightActivity(); ClearPracticeGhost(); ReleaseAllActivities(); ReplayBuffer.Clear(); snapshots.Dispose(); }

        private ResidentGalaxyChangeSet ApplyScene(GalaxySnapshotScene scene)
        {
            var system = scene.Scope.Scope == SpatialScope.System ? scene.Scope.RootId : default;
            if (CurrentSystemId.IsEmpty && !system.IsEmpty) CurrentSystemId = system;
            var entities = scene.Nodes.Select(node => new ResidentEntity(node.Id, node.Kind,
                GalaxyKinds.VisualFor(node.Kind), scene.Scope, node.Name, node.Value, system));
            var routes = scene.Routes.Select(route => new ResidentRoute(route.Id, route.SourceId, route.TargetId, scene.Scope));
            RefreshNavigationReferences();
            var obsolete = snapshots.Scenes.Keys.Where(scope => scope.Scope != SpatialScope.Overview &&
                (int)scope.Scope >= (int)scene.Scope.Scope && !scope.Equals(scene.Scope)).ToArray();
            foreach (var scope in obsolete) snapshots.ReleaseScene(scope);
            return Publish(Resident.ApplyProjection(scene.Scope, entities, routes, obsolete));
        }
    }
}
