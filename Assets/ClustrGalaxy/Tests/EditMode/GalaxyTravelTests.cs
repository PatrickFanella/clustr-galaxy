using System;
using System.Threading.Tasks;
using ClustrGalaxy.Core.Data;
using ClustrGalaxy.Core.Activities;
using ClustrGalaxy.Core.Domain;
using ClustrGalaxy.Core.Navigation;
using ClustrGalaxy.Core.Session;
using ClustrGalaxy.Core.Survey;
using NUnit.Framework;
namespace ClustrGalaxy.Tests
{
    public sealed class GalaxyTravelTests
    {
        private static readonly SurveySnapshotId Snapshot = new(9, 3, "demo");
        private static TravelIntent Intent(GalaxyTravel travel, TravelAction action, string target = "post:target") =>
            new(travel.State.SessionId, travel.NextOperationId, travel.State.Snapshot, action, (GalaxyEntityId)target);
        private static GalaxyTravel Create() => new("session", Snapshot, _ => "");
        [TestCase(TravelPhase.Charging)] [TestCase(TravelPhase.SystemCruise)] [TestCase(TravelPhase.Approach)]
        [TestCase(TravelPhase.JumpCharging)] [TestCase(TravelPhase.JumpTransit)] [TestCase(TravelPhase.Arrival)]
        public void EveryTransitionCanBeCancelledAndLateArrivalIsRejected(TravelPhase phase)
        {
            var travel = Create(); var jump = phase == TravelPhase.JumpCharging || phase == TravelPhase.JumpTransit || phase == TravelPhase.Arrival;
            travel.Apply(Intent(travel, jump ? TravelAction.Jump : TravelAction.Cruise));
            var operation = travel.State.OperationId;
            if (phase != TravelPhase.Charging && phase != TravelPhase.JumpCharging) travel.AdvanceCharge(3);
            if (phase == TravelPhase.Approach) travel.BeginApproach(operation);
            if (phase == TravelPhase.Arrival) travel.PrepareArrival("session", operation, Snapshot, 100, 0);
            Assert.That(travel.State.Phase, Is.EqualTo(phase));
            var cancel = travel.Apply(Intent(travel, TravelAction.Cancel));
            Assert.That(cancel.Committed, Is.True); Assert.That(cancel.Aborted, Is.True);
            Assert.That(travel.State.Phase, Is.EqualTo(TravelPhase.LocalFlight));
            Assert.That(travel.PrepareArrival("session", operation, Snapshot, 100, 0).Committed, Is.False);
            Assert.That(travel.FinishArrival(operation).Committed, Is.False);
        }
        [Test] public void ChargeAndArrivalRequireExplicitSafeStateTransitions()
        {
            var travel = Create(); travel.Apply(Intent(travel, TravelAction.Cruise));
            travel.AdvanceCharge(2.99); Assert.That(travel.State.Phase, Is.EqualTo(TravelPhase.Charging));
            Assert.That(travel.AdvanceCharge(double.NaN).Committed, Is.False);
            travel.AdvanceCharge(.01); Assert.That(travel.State.Phase, Is.EqualTo(TravelPhase.SystemCruise));
            Assert.That(travel.PrepareArrival("session", 1, Snapshot, 100, 0).Committed, Is.False);
            travel.BeginApproach(1);
            Assert.That(travel.PrepareArrival("session", 1, Snapshot, 0, 0).Committed, Is.False);
            Assert.That(travel.PrepareArrival("session", 1, Snapshot, 100, 20).Committed, Is.False);
            Assert.That(travel.PrepareArrival("session", 1, Snapshot, 100, double.NaN).Committed, Is.False);
            Assert.That(travel.PrepareArrival("session", 1, Snapshot, 100, 19).Committed, Is.True);
            Assert.That(travel.FinishArrival(1).Committed, Is.True);
            Assert.That(travel.FinishArrival(1).Committed, Is.False);
        }
        [Test] public void CommandsAreIsolatedIdempotentAndCannotMutateFromListeners()
        {
            var travel = Create(); var count = 0; TravelOutcome nested = null;
            travel.Changed += _ => { count++; nested = travel.Apply(Intent(travel, TravelAction.Cancel)); };
            var intent = Intent(travel, TravelAction.Jump); var first = travel.Apply(intent);
            Assert.That(nested.Committed, Is.False); Assert.That(travel.Apply(intent), Is.SameAs(first)); Assert.That(count, Is.EqualTo(1));
            Assert.That(travel.Apply(new TravelIntent("other", 2, Snapshot, TravelAction.Cancel)).Committed, Is.False);
            Assert.That(travel.Apply(new TravelIntent("session", 2, new SurveySnapshotId(10, 3, "demo"), TravelAction.Cancel)).Committed, Is.False);
            Assert.That(travel.Apply(new TravelIntent("session", 1, Snapshot, TravelAction.Cruise)).Committed, Is.False);
            Assert.That(travel.State.Phase, Is.EqualTo(TravelPhase.JumpCharging));
        }
        private sealed class NoSurveyWrites : ISurveyRepository
        {
            public SurveyState LoadOrCreate(string profile, SurveySnapshotId snapshot, GalaxyEntityId id) => throw new InvalidOperationException("Travel cannot start Survey.");
            public void Save(SurveyState state) => throw new InvalidOperationException("Travel cannot award Survey progression.");
        }
        private sealed class JumpSource : IGalaxySnapshotSource
        {
            public readonly GalaxySnapshotManifest Manifest = new(9,3,"spatial-scene-v1");
            public TaskCompletionSource<SnapshotSourceResult> Pending;
            public bool Fail;
            public GalaxySnapshotScene Scene(SpatialScope scope,string root="") => new(9,3,new SpatialScopeRequest(scope,string.IsNullOrEmpty(root)?default:(GalaxyEntityId)root),
                new[]{new SnapshotNode((GalaxyEntityId)"subreddit:a",GalaxyEntityKind.Subreddit,"A",1),new SnapshotNode((GalaxyEntityId)"subreddit:b",GalaxyEntityKind.Subreddit,"B",1)},
                new[]{new SnapshotRoute((GalaxyRouteId)"route:a-b",(GalaxyEntityId)"subreddit:a",(GalaxyEntityId)"subreddit:b",1,false)});
            public Task<SnapshotSourceResult> OpenAsync(System.Threading.CancellationToken token)=>Task.FromResult(SnapshotSourceResult.Success(Manifest,Scene(SpatialScope.Overview)));
            public Task<SnapshotSourceResult> LoadAsync(GalaxySnapshotManifest manifest,SpatialScopeRequest scope,System.Threading.CancellationToken token)=>
                Pending?.Task ?? Task.FromResult(Fail?SnapshotSourceResult.Failure("fixture failure"):SnapshotSourceResult.Success(Manifest,Scene(scope.Scope,scope.RootId.Value),true));
        }
        private static async Task<GalaxySession> OpenLive(JumpSource source)
        {
            var session=new GalaxySession(source,new NoSurveyWrites(),"pilot"); await session.OpenAsync();
            await session.LoadAsync(new SpatialScopeRequest(SpatialScope.System,(GalaxyEntityId)"subreddit:a"));
            session.Travel.Apply(Intent(session.Travel,TravelAction.Jump,"subreddit:b")); session.Travel.AdvanceCharge(3); return session;
        }
        [Test] public async Task CancelledJumpRejectsProviderThatIgnoresCancellation()
        {
            var source=new JumpSource(); using var session=await OpenLive(source);
            var before=session.Activities.State;
            source.Pending=new TaskCompletionSource<SnapshotSourceResult>(); var pending=session.StageJumpAsync();
            session.Travel.Apply(Intent(session.Travel,TravelAction.Cancel));
            source.Pending.SetResult(SnapshotSourceResult.Success(source.Manifest,source.Scene(SpatialScope.System,"subreddit:b")));
            Assert.That(await pending,Is.Null); Assert.That(session.CurrentSystemId.Value,Is.EqualTo("subreddit:a"));
            Assert.That(session.Activities.State,Is.SameAs(before)); Assert.That(session.Snapshots.Scenes.Count,Is.EqualTo(2));
        }
        [Test] public async Task FailedJumpCanRetryOfflineWithoutPrematureOrDuplicateArrival()
        {
            var source=new JumpSource(); using var session=await OpenLive(source);
            source.Fail=true;Assert.That(await session.StageJumpAsync(),Is.Null);
            Assert.That(session.CurrentSystemId.Value,Is.EqualTo("subreddit:a"));
            source.Fail=false;var receipt=await session.StageJumpAsync();
            Assert.That(session.Snapshots.Scenes.Count,Is.EqualTo(2));
            Assert.That(session.CommitJump(receipt,500,20).Succeeded,Is.False);
            Assert.That(session.CommitJump(receipt,500,0).Succeeded,Is.True);
            Assert.That(session.CurrentSystemId.Value,Is.EqualTo("subreddit:b"));
            Assert.That(session.Snapshots.Mode,Is.EqualTo(SnapshotDataMode.OfflinePinned));
            Assert.That(session.CommitJump(receipt,500,0).Succeeded,Is.False);
        }
        [Test] public async Task CrossSessionAndExpiredJumpReceiptsCannotArrive()
        {
            var source=new JumpSource(); using var session=await OpenLive(source); using var other=await OpenLive(new JumpSource());
            var receipt=await session.StageJumpAsync();
            Assert.That(other.CommitJump(receipt,500,0).Succeeded,Is.False);
            session.Snapshots.CancelPending();
            Assert.That(session.CommitJump(receipt,500,0).Succeeded,Is.False);
            Assert.That(session.CurrentSystemId.Value,Is.EqualTo("subreddit:a"));
        }
        [Test] public async Task AuthoredDepartureDoesNotDependOnAlphabeticalRegistryOrder()
        {
            var manifest = new GalaxySnapshotManifest(9, 3, "spatial-scene-v1", "demo");
            var home = (GalaxyEntityId)"subreddit:technology";
            var scene = new GalaxySnapshotScene(9, 3, new SpatialScopeRequest(SpatialScope.Overview),
                new[] { new SnapshotNode((GalaxyEntityId)"subreddit:signal-horizon", GalaxyEntityKind.Subreddit, "Remote", 1),
                    new SnapshotNode(home, GalaxyEntityKind.Subreddit, "Home", 1) }, null);
            using var session = new GalaxySession(new DeterministicSnapshotSource(manifest, new[] { scene }), new NoSurveyWrites(), "pilot", demoStartingSystem: home);
            Assert.That((await session.OpenAsync()).Succeeded, Is.True);
            Assert.That(session.CurrentSystemId, Is.EqualTo(home));
        }
        [Test] public async Task SessionRequiresExistingRouteAndPreservesPublishedDirection()
        {
            var manifest = new GalaxySnapshotManifest(9, 3, "spatial-scene-v1", "demo");
            var a = (GalaxyEntityId)"subreddit:a"; var b = (GalaxyEntityId)"subreddit:b"; var c = (GalaxyEntityId)"subreddit:c";
            var scene = new GalaxySnapshotScene(9, 3, new SpatialScopeRequest(SpatialScope.Overview),
                new[] { new SnapshotNode(a, GalaxyEntityKind.Subreddit, "A", 1), new SnapshotNode(b, GalaxyEntityKind.Subreddit, "B", 1), new SnapshotNode(c, GalaxyEntityKind.Subreddit, "C", 1) },
                new[] { new SnapshotRoute((GalaxyRouteId)"route:a-b", a, b, 1, true) });
            using var session = new GalaxySession(new DeterministicSnapshotSource(manifest, new[] { scene }), new NoSurveyWrites(), "pilot");
            await session.OpenAsync(); var travel = session.Travel;
            Assert.That(session.JumpDestinations().Count, Is.EqualTo(1));
            Assert.That(session.JumpDestinations()[0].Id, Is.EqualTo(b));
            Assert.That(session.JumpDestinationLabel(session.JumpDestinations()[0]), Is.EqualTo("B"));
            Assert.That(travel.Apply(Intent(travel, TravelAction.Jump, c.Value)).Committed, Is.False);
            Assert.That(travel.Apply(Intent(travel, TravelAction.Jump, b.Value)).Committed, Is.True);
            var expedition = session.StartExpedition(ExpeditionMood.QuietDrift);
            Assert.That(expedition.Committed, Is.True);
            var before = session.Activities.State;
            var trip = before.Expedition;
            Assert.That(session.VisitExpedition(trip.Plan.Stops[trip.Current], 0), Is.Null);
            Assert.That(session.Activities.State, Is.SameAs(before));
            Assert.Throws<InvalidOperationException>(() => session.StartFlightActivity(null, NavigationFlightMode.PilotAssist, false, "test", Array.Empty<GalaxyEntityId>()));
            travel.AdvanceCharge(3);
            Assert.That(travel.PrepareArrival(travel.State.SessionId, 1, Snapshot, 100, 0).Committed, Is.False);
            var preparation = await session.StageJumpAsync();
            Assert.That(session.CommitJump(preparation,100,0).Succeeded, Is.True);
            Assert.That(session.CommitJump(preparation,100,0).Succeeded, Is.False);
            Assert.That(session.CurrentSystemId, Is.EqualTo(b));
            Assert.That(session.JumpDestinations(), Is.Empty, "Directed routes cannot offer a return jump");
            Assert.That(travel.Apply(Intent(travel, TravelAction.Jump, a.Value)).Committed, Is.False);
            session.ApplySurveyVisibility(false);
            Assert.That(travel.Apply(Intent(travel, TravelAction.Jump, c.Value)).Committed, Is.False);
            Assert.That(session.Survey, Is.Null);
            Assert.That(session.JumpDestinations(), Is.Empty);
        }
    }
}
