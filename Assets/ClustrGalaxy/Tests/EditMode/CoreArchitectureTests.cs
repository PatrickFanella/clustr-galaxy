using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ClustrGalaxy.Core.Domain;
using ClustrGalaxy.Core.Activities;
using ClustrGalaxy.Core.Persistence;
using ClustrGalaxy.Core.Data;
using ClustrGalaxy.Core.Navigation;
using ClustrGalaxy.Core.Presentation;
using ClustrGalaxy.Core.Survey;
using ClustrGalaxy.Core.World;
using System.Numerics;
using NUnit.Framework;

namespace ClustrGalaxy.Tests
{
    public sealed class CoreArchitectureTests
    {
        [Test]
        public async System.Threading.Tasks.Task ScenicActivitiesRequirePublishedRoutesAndReleaseSharedOwners()
        {
            var manifest=new GalaxySnapshotManifest(9,3,"spatial-scene-v1","demo");var a=(GalaxyEntityId)"subreddit:a";var b=(GalaxyEntityId)"subreddit:b";
            var route=(GalaxyRouteId)"route:a-b";var scope=new SpatialScopeRequest(SpatialScope.Overview);
            var scene=new GalaxySnapshotScene(9,3,scope,new[]{new SnapshotNode(a,GalaxyEntityKind.Subreddit,"a",1),new SnapshotNode(b,GalaxyEntityKind.Subreddit,"b",1)},
                new[]{new SnapshotRoute(route,a,b,1,false)});
            using var session=new ClustrGalaxy.Core.Session.GalaxySession(new DeterministicSnapshotSource(manifest,new[]{scene}),new MemorySurveyRepository(manifest.SurveySnapshot,"pilot",a),"pilot");
            await session.OpenAsync();
            var course=ClustrGalaxy.Core.Activities.FlightCourse.Scenic(0,default,new ClustrGalaxy.Core.Activities.ReplayPosition(1000,0,0));
            Assert.Throws<InvalidOperationException>(()=>session.StartFlightActivity(course,NavigationFlightMode.PilotAssist,false,"tuning",new[]{a,b}));
            session.StartFlightActivity(course,NavigationFlightMode.PilotAssist,false,"tuning",new[]{a,b},route);
            session.SetSelection(a);session.ReleaseScope(scope);Assert.That(session.Resident.TryFindLoaded(b,out _),Is.True);
            session.CancelFlightActivity();Assert.That(session.Resident.TryFindLoaded(a,out _),Is.True);Assert.That(session.Resident.TryFindLoaded(b,out _),Is.False);
            Assert.That(session.Survey,Is.Null);
        }

        [Test] public async System.Threading.Tasks.Task ImportedPracticeGhostDoesNotGrantProgressOrOwnSourceEntities()
        {
            var manifest=new GalaxySnapshotManifest(9,3,"spatial-scene-v1","demo");var a=(GalaxyEntityId)"subreddit:a";
            var scene=new GalaxySnapshotScene(9,3,new SpatialScopeRequest(SpatialScope.Overview),new[]{new SnapshotNode(a,GalaxyEntityKind.Subreddit,"a",1)},Array.Empty<SnapshotRoute>());
            using var session=new ClustrGalaxy.Core.Session.GalaxySession(new DeterministicSnapshotSource(manifest,new[]{scene}),new MemorySurveyRepository(manifest.SurveySnapshot,"other-profile",a),"other-profile");
            await session.OpenAsync();var before=session.Activities.State;
            var course=FlightCourse.Playground(FlightActivityKind.RingRun,new ReplayPosition(100,200,300));
            var replay=new JourneyReplay(new ReplayIdentity(manifest.SurveySnapshot,ActivityWorldMode.Explore,1,"tuning",NavigationFlightMode.PilotAssist,course.Id,course.Version),
                new[]{new ReplayPose(0,course.Points[0],Quaternion.Identity),new ReplayPose(.1,course.Points[1],Quaternion.Identity)},course.Points[0]);
            using var file=new MemoryStream();ClustrGalaxy.Persistence.ReplayPackage.Write(file,replay);file.Position=0;
            var oldReplay = new JourneyReplay(new ReplayIdentity(manifest.SurveySnapshot,ActivityWorldMode.Explore,1,"tuning",NavigationFlightMode.PilotAssist,course.Id,1),replay.Poses,course.Points[0]);
            Assert.Throws<InvalidOperationException>(()=>session.SelectPracticeGhost(oldReplay));
            Assert.That(oldReplay.Poses.Count,Is.EqualTo(2),"Historical recordings remain declarative playback data.");
            session.SelectPracticeGhost(ClustrGalaxy.Persistence.ReplayPackage.Read(file));
            Assert.That(session.Activities.State,Is.SameAs(before));Assert.That(session.Survey,Is.Null);
            session.StartFlightActivity(course,NavigationFlightMode.PilotAssist,false,"tuning",new[]{a});Assert.That(session.FlightActivity.BestGhost,Is.SameAs(session.SelectedPracticeGhost));
            session.StartFlightActivity(course,NavigationFlightMode.PilotMastery,false,"tuning",new[]{a});Assert.That(session.FlightActivity.BestGhost,Is.Null);
            Assert.Throws<InvalidOperationException>(()=>session.SelectPracticeGhost(new JourneyReplay(replay.Identity,replay.Poses)));
            Assert.That(session.Activities.State,Is.SameAs(before));session.ApplySurveyVisibility(false);Assert.That(session.SelectedPracticeGhost,Is.Null);
        }

        [Test]
        public void NavigationBack_RestoresScopeAndClearsThreadSelection()
        {
            var navigation = new GalaxyNavigation();
            var pose = new NavigationPose(Vector3.Zero, Quaternion.Identity);
            navigation.Apply(new InspectNavigationTargetIntent(new NavigationTarget((GalaxyEntityId)"post:a",
                GalaxyEntityKind.Post, "Post", Vector3.Zero, true, true), pose));
            navigation.Apply(new InspectNavigationTargetIntent(new NavigationTarget((GalaxyEntityId)"comment:a",
                GalaxyEntityKind.Comment, "Comment", Vector3.One, true, true), pose));
            var back = navigation.Apply(new ReturnNavigationIntent());
            Assert.That(back.State.SelectedId.IsEmpty, Is.True);
            Assert.That(back.Effect.ScopeToLoad.HasValue, Is.True);
            Assert.That(back.Effect.ScopeToLoad.Value.Scope, Is.EqualTo(SpatialScope.Discussion));
            Assert.That(back.Effect.ScopeToLoad.Value.RootId.Value, Is.EqualTo("post:a"));
        }

        [Test]
        public void SurveyStartScreenAlwaysOffersReturnToExplore()
        {
            var presentation = new ProbeHudPresenter().Present(new ProbeHudReadModel
                { Survey = true, RequiresSurveyStart = true });
            Assert.That(presentation.Actions, Does.Contain(ProbeHudAction.ChooseStartingSystem));
            Assert.That(presentation.Actions, Does.Contain(ProbeHudAction.ReturnToExplore));
            Assert.That(presentation.Actions.Contains(ProbeHudAction.Scan), Is.False);
        }

        [Test]
        public void HudDraft_EnablesSaveOnlyForNamedIdentifiedMembership()
        {
            var members = new[] { (GalaxyEntityId)"post:a", (GalaxyEntityId)"post:b", (GalaxyEntityId)"post:c" };
            var model = new ProbeHudReadModel { Survey = true, DraftName = "Local group", DraftReason = "Shared interest",
                DraftMembers = members, IdentifiedEntities = members };
            var presenter = new ProbeHudPresenter();
            Assert.That(presenter.Present(model).Actions.Contains(ProbeHudAction.SavePersonalConstellation), Is.True);
            model.DraftName = "";
            Assert.That(presenter.Present(model).Actions.Contains(ProbeHudAction.SavePersonalConstellation), Is.False);
            model.DraftName = "Local group"; model.IdentifiedEntities = new[] { members[0] };
            Assert.That(presenter.Present(model).Actions.Contains(ProbeHudAction.SavePersonalConstellation), Is.False);
            Assert.That(model.DraftValidation, Does.Contain("identified"));
        }

        [Test]
        public void ReferenceOwnersKeepSelectionAndNavigationUntilBothRelease()
        {
            var resident = new ResidentGalaxy();
            var scope = new SpatialScopeRequest(SpatialScope.System,(GalaxyEntityId)"system:a");
            var id = (GalaxyEntityId)"post:a";
            resident.ApplyProjection(scope,new[]{new ResidentEntity(id,GalaxyEntityKind.Post,
                GalaxyKinds.VisualFor(GalaxyEntityKind.Post),scope,"A",1)},null);
            resident.SetReferences("selection",new[]{id});
            resident.SetReferences("navigation",new[]{id});
            Assert.That(resident.Release(scope).Released,Is.Empty);
            Assert.That(resident.TryFindLoaded(id,out _),Is.True);
            resident.SetReferences("selection",Array.Empty<GalaxyEntityId>());
            Assert.That(resident.TryFindLoaded(id,out _),Is.True);
            var released = resident.SetReferences("navigation",Array.Empty<GalaxyEntityId>()).Released;
            Assert.That(released.Count,Is.EqualTo(1));
            Assert.That(released[0].Equals(id),Is.True);
            Assert.That(resident.LoadedEntities,Is.Empty);
        }

        [Test]
        public void CleanSignalAwardCommitsWithScanAndSurvivesRetry()
        {
            var snapshot = new SurveySnapshotId(9,3,"demo"); var id = (GalaxyEntityId)"subreddit:technology";
            var repository = new MemorySurveyRepository(snapshot, "pilot", id) { FailSave = true };
            var session = new SurveySession(repository, "pilot", snapshot, id);
            var intent = new CompleteScanIntent("clean", snapshot, DateTime.UtcNow, id, GalaxyEntityKind.Subreddit,
                Array.Empty<SurveyRoute>(), Array.Empty<GalaxyEntityId>(), cleanSignal: true);
            var failed = session.Apply(intent);
            Assert.That(failed.Succeeded, Is.False); Assert.That(failed.Celebrations, Is.Empty);
            Assert.That(session.State.Discoveries, Is.Empty);
            repository.FailSave = false; var success = session.Apply(intent); Assert.That(success.Succeeded, Is.True);
            Assert.That(success.Celebrations.Count, Is.EqualTo(1)); Assert.That(session.Apply(intent).Celebrations, Is.Empty);
            Assert.That(session.State.Discoveries, Does.Contain("clean-signal:subreddit:technology"));
            Assert.That(session.Apply(intent).Changed, Is.False);
            Assert.That(new SurveySession(repository, "pilot", snapshot, id).State.Discoveries,
                Does.Contain("clean-signal:subreddit:technology"));
        }

        [Test]
        public async System.Threading.Tasks.Task ActivityReferencesRespectOtherOwnersAndReleaseOnModeChange()
        {
            var manifest = new GalaxySnapshotManifest(9, 3, "spatial-scene-v1", "demo");
            var id = (GalaxyEntityId)"post:activity";
            var scope = new SpatialScopeRequest(SpatialScope.Overview);
            var scene = new GalaxySnapshotScene(9, 3, scope,
                new[] { new SnapshotNode(id, GalaxyEntityKind.Post, "Activity", 1) }, null);
            using var session = new ClustrGalaxy.Core.Session.GalaxySession(
                new DeterministicSnapshotSource(manifest, new[] { scene }),
                new MemorySurveyRepository(manifest.SurveySnapshot, "pilot", id), "pilot");
            await session.OpenAsync();
            session.RetainActivityEntities("course", new[] { id });
            session.RetainActivityEntities("expedition", new[] { id });
            session.SetSelection(id);
            session.ReleaseScope(scope);
            session.ReleaseActivityEntities("course");
            Assert.That(session.Resident.TryFindLoaded(id, out _), Is.True);
            session.ApplySurveyVisibility(false);
            Assert.That(session.Resident.TryFindLoaded(id, out _), Is.True);
            session.SetSelection(default);
            Assert.That(session.Resident.TryFindLoaded(id, out _), Is.False);
            Assert.Throws<InvalidOperationException>(() => session.RetainActivityEntities("missing", new[] { id }));
            Assert.That(session.Activities.State.Identity.Mode, Is.EqualTo(ClustrGalaxy.Core.Activities.ActivityWorldMode.Survey));
        }

        [Test]
        public async System.Threading.Tasks.Task SurveyStartUpdatesNavigationOnlyAfterPersistenceSucceeds()
        {
            var manifest = new GalaxySnapshotManifest(9, 3, "spatial-scene-v1", "demo");
            var id = (GalaxyEntityId)"subreddit:technology";
            var scene = new GalaxySnapshotScene(9, 3, new SpatialScopeRequest(SpatialScope.Overview),
                new[] { new SnapshotNode(id, GalaxyEntityKind.Subreddit, "Technology", 1) }, null);
            var repository = new MemorySurveyRepository(manifest.SurveySnapshot, "pilot", id) { FailSave = true };
            using var session = new ClustrGalaxy.Core.Session.GalaxySession(
                new DeterministicSnapshotSource(manifest, new[] { scene }), repository, "pilot");
            await session.OpenAsync();
            session.Navigation.Apply(new EnterSurveyNavigationIntent());
            Assert.That(session.StartSurvey(id, new[] { id }, "start", DateTime.UtcNow).Succeeded, Is.False);
            Assert.That(session.Survey, Is.Null);
            Assert.That(session.Navigation.State.RequiresSurveyStart, Is.True);
            repository.FailSave = false;
            Assert.That(session.StartSurvey(id, new[] { id }, "start", DateTime.UtcNow).Succeeded, Is.True);
            Assert.That(session.Navigation.State.RequiresSurveyStart, Is.False);
            Assert.That(session.Navigation.State.FocusedId, Is.EqualTo(id));
            Assert.That(session.Navigation.State.Status, Is.EqualTo("SURVEY STARTED"));
        }

        [Test]
        public async System.Threading.Tasks.Task OpenedSnapshotCannotBeReplacedWithinTheSession()
        {
            var manifest = new GalaxySnapshotManifest(9,3,"spatial-scene-v1","demo");
            var scene = new GalaxySnapshotScene(9,3,new SpatialScopeRequest(SpatialScope.Overview),null,null);
            using var session = new GalaxySnapshotSession(new DeterministicSnapshotSource(manifest,new[]{scene}));
            Assert.That((await session.OpenAsync()).Succeeded,Is.True);
            Assert.That((await session.OpenAsync()).Succeeded,Is.False);
            Assert.That(session.Manifest,Is.SameAs(manifest));
            session.Dispose();
            Assert.DoesNotThrow(session.CancelPending);
        }

        [Test]
        public void SystemCompletionRewardsOnlyTheFirstCompletingScan()
        {
            var snapshot = new SurveySnapshotId(9, 3, "demo");
            var system = (GalaxyEntityId)"subreddit:technology"; var post = (GalaxyEntityId)"post:a";
            var repository = new MemorySurveyRepository(snapshot, "pilot", system);
            var session = new SurveySession(repository, "pilot", snapshot, system);
            var weakRoute = new[] { new SurveyRoute((GalaxyRouteId)"route:weak", system, (GalaxyEntityId)"subreddit:next", 1f) };

            var first = session.Apply(new CompleteScanIntent("scan:system", snapshot, DateTime.UtcNow, system,
                GalaxyEntityKind.Subreddit, Array.Empty<SurveyRoute>(), new[] { post }, system, systemCompletedNow: true));
            var later = session.Apply(new CompleteScanIntent("scan:post", snapshot, DateTime.UtcNow, post,
                GalaxyEntityKind.Post, weakRoute, new[] { post }, system, systemCompletedNow: true));

            Assert.That(first.Celebrations.Count(cue => cue.Kind == CelebrationKind.SystemRoutes), Is.EqualTo(1));
            Assert.That(later.Succeeded, Is.True, later.Rejection);
            Assert.That(later.Celebrations.Any(cue => cue.Kind == CelebrationKind.SystemRoutes), Is.False);
            Assert.That(later.RevealedRoutes, Is.Empty);
            Assert.That(session.State.CompletedSystemCount, Is.EqualTo(1));
        }

        [Test]
        public void SurveyIntent_CommitsOneStateAndReplayDoesNotWrite()
        {
            var snapshot = new SurveySnapshotId(9, 3, "demo");
            var repository = new MemorySurveyRepository(snapshot, "pilot", (GalaxyEntityId)"subreddit:technology");
            var session = new SurveySession(repository, "pilot", snapshot, (GalaxyEntityId)"subreddit:technology");
            var intent = new CompleteScanIntent("scan:1", snapshot, DateTime.UtcNow,
                (GalaxyEntityId)"subreddit:technology", GalaxyEntityKind.Subreddit,
                new[] { new SurveyRoute((GalaxyRouteId)"route:a", (GalaxyEntityId)"subreddit:technology",
                    (GalaxyEntityId)"subreddit:next", 8f) }, Array.Empty<GalaxyEntityId>());

            var first = session.Apply(intent);
            var replay = session.Apply(intent);

            Assert.That(first.Succeeded, Is.True);
            Assert.That(first.RevealedEntities, Has.Count.EqualTo(1));
            Assert.That(repository.SaveCount, Is.EqualTo(1));
            Assert.That(replay.Succeeded, Is.True);
            Assert.That(replay.Changed, Is.False);
            Assert.That(repository.SaveCount, Is.EqualTo(1));
        }

        [Test]
        public void SurveyIntent_WrongSnapshotCannotMutateOrPersist()
        {
            var snapshot = new SurveySnapshotId(9, 3, "demo");
            var repository = new MemorySurveyRepository(snapshot, "pilot", (GalaxyEntityId)"subreddit:technology");
            var session = new SurveySession(repository, "pilot", snapshot, (GalaxyEntityId)"subreddit:technology");
            var result = session.Apply(new PilotMasteryArrivalIntent("mastery:1",
                new SurveySnapshotId(10, 4, "pinned"), DateTime.UtcNow, (GalaxyEntityId)"subreddit:technology"));

            Assert.That(result.Succeeded, Is.False);
            Assert.That(repository.SaveCount, Is.Zero);
        }

        [Test]
        public void PersonalConstellation_RequiresSaveRequestAndConfirmAsSeparateWrites()
        {
            var snapshot = new SurveySnapshotId(9, 3, "demo");
            var repository = new MemorySurveyRepository(snapshot, "pilot", (GalaxyEntityId)"subreddit:technology",
                (GalaxyEntityId)"post:a", (GalaxyEntityId)"post:b");
            var session = new SurveySession(repository, "pilot", snapshot, (GalaxyEntityId)"subreddit:technology");
            var save = session.Apply(new SavePersonalConstellationIntent("personal:save", snapshot, DateTime.UtcNow,
                "Local arc", "Three related signals", new[] { (GalaxyEntityId)"subreddit:technology",
                    (GalaxyEntityId)"post:a", (GalaxyEntityId)"post:b" }));
            var request = session.Apply(new RequestLocalQueueIntent("personal:request", snapshot, DateTime.UtcNow,
                save.PersonalConstellationId));
            var confirm = session.Apply(new ConfirmLocalQueueIntent("personal:confirm", snapshot, DateTime.UtcNow));

            Assert.That(save.QueueStatus, Is.Empty);
            Assert.That(request.QueueStatus, Does.StartWith("CONFIRM LOCAL QUEUE"));
            Assert.That(confirm.QueueStatus, Is.EqualTo(SurveySession.QueuedLocallyStatus));
            Assert.That(repository.SaveCount, Is.EqualTo(3));
        }

        [Test]
        public void DurableDocuments_RejectTraversalAndQuarantineCorruption()
        {
            var root = Path.Combine(Path.GetTempPath(), "clustr-durable-" + Guid.NewGuid().ToString("N"));
            try
            {
                var store = new FileDurableDocumentStore(root);
                store.Write("profiles/v2/test.json", "valid", value => value == "valid");
                Assert.That(store.TryRead("profiles/v2/test.json", out var value), Is.True);
                Assert.That(value, Is.EqualTo("valid"));
                store.Quarantine("profiles/v2/test.json");
                Assert.That(store.TryRead("profiles/v2/test.json", out _), Is.False);
                Assert.Throws<ArgumentException>(() => store.TryRead("../escape.json", out _));
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }

        [Test]
        public void ResidentGalaxy_DistinguishesLoadedFromSurveyActiveAndReleasesScopes()
        {
            var galaxy = new ResidentGalaxy();
            var scope = new SpatialScopeRequest(SpatialScope.System, (GalaxyEntityId)"subreddit:technology");
            galaxy.ApplyProjection(scope, new[]
            {
                new ResidentEntity((GalaxyEntityId)"subreddit:technology", GalaxyEntityKind.Subreddit,
                    GalaxyVisualKind.Star, scope, "technology", 1),
                new ResidentEntity((GalaxyEntityId)"post:hidden", GalaxyEntityKind.Post,
                    GalaxyVisualKind.Planet, scope, "hidden", 1)
            }, null);

            var hidden = galaxy.ApplySurveyVisibility(false,
                new[] { (GalaxyEntityId)"subreddit:technology" }, Array.Empty<GalaxyRouteId>());

            Assert.That(galaxy.LoadedEntities.Count, Is.EqualTo(2));
            Assert.That(galaxy.ActiveEntities.Count, Is.EqualTo(1));
            Assert.That(hidden.Deactivated, Is.EquivalentTo(new[] { (GalaxyEntityId)"post:hidden" }));
            galaxy.Release(scope);
            Assert.That(galaxy.LoadedEntities, Is.Empty);
        }

        [Test]
        public void SnapshotSession_RejectsLateResultWithoutReplacingCurrentState()
        {
            var source = new ControlledSnapshotSource();
            var session = new GalaxySnapshotSession(source);
            var first = session.OpenAsync();
            var second = session.OpenAsync();
            source.CompleteSecond();
            source.CompleteFirst();

            Assert.That(second.Result.Succeeded, Is.True);
            Assert.That(first.Result.IsStale, Is.True);
            Assert.That(session.Manifest.RevisionId, Is.EqualTo(2));
        }

        [Test]
        public void Navigation_ChartDoesNotPauseOrRestoreStaleMomentumAndFastTravelClearsIt()
        {
            var navigation = new GalaxyNavigation();
            var open = navigation.Apply(new SetMapIntent(true, new NavigationPose(Vector3.Zero, Quaternion.Identity),
                new Vector3(4, 0, 0), new Vector3(0, 2, 0)));
            var close = navigation.Apply(new SetMapIntent(false, new NavigationPose(Vector3.Zero, Quaternion.Identity),
                Vector3.Zero, Vector3.Zero));
            Assert.That(open.Effect.PausePhysics, Is.False);
            Assert.That(close.Effect.ResumePhysics, Is.False);
            Assert.That(close.Effect.Teleport, Is.False);
            Assert.That(close.Effect.Velocity, Is.EqualTo(Vector3.Zero));

            var target = new NavigationTarget((GalaxyEntityId)"subreddit:next", GalaxyEntityKind.Subreddit,
                "next", new Vector3(100, 0, 0), true, true, 2, 3);
            navigation.Apply(new FastTravelIntent(target));
            var travel = navigation.AdvancePhysics(new NavigationPose(Vector3.Zero, Quaternion.Identity),
                new Vector3(9, 0, 0), NavigationFlightMode.PilotAssist, _ => target, 8);
            Assert.That(travel.Effect.Teleport, Is.True);
            Assert.That(travel.Effect.Velocity, Is.EqualTo(Vector3.Zero));
            Assert.That(travel.Effect.Rebase, Is.True);
        }

        [Test]
        public void Navigation_StatusNeverNamesAnUnidentifiedTarget()
        {
            var navigation = new GalaxyNavigation();
            var target = new NavigationTarget((GalaxyEntityId)"subreddit:secret", GalaxyEntityKind.Subreddit,
                "Secret Name", new Vector3(10, 0, 0), true, true, 2, 3, identified: false);
            navigation.Apply(new PlotApproachIntent(target, false));
            Assert.That(navigation.State.Status, Does.Not.Contain("Secret Name"));
            Assert.That(navigation.State.Status, Does.Contain(NavigationTarget.UnidentifiedLabel));

            var arrival = navigation.AdvancePhysics(new NavigationPose(new Vector3(10, 0, 0), Quaternion.Identity),
                Vector3.Zero, NavigationFlightMode.Autopilot, _ => target, 8, 100f);
            Assert.That(arrival.Succeeded, Is.True);
            Assert.That(navigation.State.AutopilotArrived, Is.True);
            Assert.That(navigation.State.Status, Does.Not.Contain("Secret Name"));

            navigation.Apply(new FastTravelIntent(target));
            navigation.AdvancePhysics(new NavigationPose(Vector3.Zero, Quaternion.Identity), Vector3.Zero,
                NavigationFlightMode.PilotAssist, _ => target, 8);
            Assert.That(navigation.State.Status, Does.Not.Contain("Secret Name"));
        }

        [Test]
        public void HudPresentation_HidesThreadRowsForUnidentifiedSurveyTargets()
        {
            var rows = new[] { new ProbeHudThreadRow(1, "author", 3, 0, "excerpt", false) };
            var hidden = new ProbeHudPresenter().Present(new ProbeHudReadModel
            { Survey = true, TargetId = "comment:a", TargetIdentified = false, ThreadRows = rows });
            var shown = new ProbeHudPresenter().Present(new ProbeHudReadModel
            { Survey = true, TargetId = "comment:a", TargetIdentified = true, ThreadRows = rows });
            Assert.That(hidden.ThreadRows, Is.Empty);
            Assert.That(shown.ThreadRows.Count, Is.EqualTo(1));
        }

        [Test]
        public void Navigation_AutopilotGuidanceDoesNotAllocatePerTick()
        {
            var navigation = new GalaxyNavigation();
            var target = new NavigationTarget((GalaxyEntityId)"subreddit:next", GalaxyEntityKind.Subreddit,
                "Next", new Vector3(500, 0, 0), true, true, 2, 3);
            Func<GalaxyEntityId, NavigationTarget> resolve = _ => target;
            navigation.Apply(new PlotApproachIntent(target, false));
            var enRoute = new NavigationPose(Vector3.Zero, Quaternion.Identity);
            var anchor = new NavigationPose(new Vector3(500 - 3 - GalaxyNavigation.DefaultClearance, 0, 0), Quaternion.Identity);

            foreach (var pose in new[] { enRoute, anchor })
            {
                for (var tick = 0; tick < 3; tick++)
                    navigation.AdvancePhysics(pose, Vector3.Zero, NavigationFlightMode.Autopilot, resolve, 8);
                var before = GC.GetAllocatedBytesForCurrentThread();
                NavigationOutcome outcome = null;
                for (var tick = 0; tick < 200; tick++)
                    outcome = navigation.AdvancePhysics(pose, Vector3.Zero, NavigationFlightMode.Autopilot, resolve, 8);
                Assert.That(GC.GetAllocatedBytesForCurrentThread() - before, Is.Zero);
                if (pose.Position == Vector3.Zero) Assert.That(outcome.Effect.Velocity.Length(), Is.GreaterThan(0f));
                else Assert.That(outcome.Effect.Velocity, Is.EqualTo(Vector3.Zero));
            }
            Assert.That(navigation.State.AutopilotArrived, Is.True);
            Assert.That(navigation.State.Status, Does.StartWith("AUTOPILOT ARRIVED"));
        }

        [Test]
        public void HudPresentation_PreservesLocalOnlyQueueWording()
        {
            var presentation = new ProbeHudPresenter().Present(new ProbeHudReadModel
            {
                Survey = true, AwaitingQueueConfirmation = true, DataStatus = "OFFLINE PINNED",
                NavigationLevel = "SYSTEM", FlightMode = "ASSIST"
            });
            Assert.That(presentation.Lines, Does.Contain(ProbeHudPresentation.LocalOnlyQueueNotice));
            Assert.That(presentation.Actions, Does.Contain(ProbeHudAction.ConfirmLocalQueue));
        }

        [Test]
        public void FailedSaveLeavesStateUntouchedAndSameIntentCanRetry()
        {
            var snapshot = new SurveySnapshotId(9, 3, "pinned");
            var repository = new MemorySurveyRepository(snapshot, "pilot", (GalaxyEntityId)"subreddit:a");
            var session = new SurveySession(repository, "pilot", snapshot, (GalaxyEntityId)"subreddit:a");
            var before = session.State;
            var intent = new StartSurveyIntent("start", snapshot, DateTime.UtcNow,
                (GalaxyEntityId)"subreddit:a", new[] { (GalaxyEntityId)"post:a" });
            repository.FailSave = true;
            Assert.That(session.Apply(intent).Succeeded, Is.False);
            Assert.That(session.State, Is.SameAs(before));
            Assert.That(session.State.IsKnown((GalaxyEntityId)"post:a"), Is.False);
            repository.FailSave = false;
            Assert.That(session.Apply(intent).Changed, Is.True);
            Assert.That(session.State.IsKnown((GalaxyEntityId)"post:a"), Is.True);
            Assert.That(repository.SaveCount, Is.EqualTo(1));
        }

        [Test]
        public void StreamingPreservesSurveyVisibilityAndSharedScopeOwnership()
        {
            var galaxy = new ResidentGalaxy();
            var first = new SpatialScopeRequest(SpatialScope.Community, (GalaxyEntityId)"community:a");
            var second = new SpatialScopeRequest(SpatialScope.System, (GalaxyEntityId)"subreddit:a");
            var id = (GalaxyEntityId)"subreddit:a";
            galaxy.ApplyProjection(first, new[] { new ResidentEntity(id, GalaxyEntityKind.Subreddit,
                GalaxyVisualKind.Star, first, "a", 1) }, null);
            galaxy.ApplySurveyVisibility(false, new[] { id }, Array.Empty<GalaxyRouteId>());
            galaxy.ApplyProjection(second, new[] {
                new ResidentEntity(id, GalaxyEntityKind.Subreddit, GalaxyVisualKind.Star, second, "a", 1),
                new ResidentEntity((GalaxyEntityId)"post:hidden", GalaxyEntityKind.Post, GalaxyVisualKind.Planet, second, "hidden", 1)
            }, null);
            Assert.That(galaxy.TryFindActive((GalaxyEntityId)"post:hidden", out _), Is.False);
            galaxy.Release(second);
            Assert.That(galaxy.TryFindActive(id, out _), Is.True);
            Assert.That(galaxy.TryFindLoaded((GalaxyEntityId)"post:hidden", out _), Is.False);
            galaxy.Release(first);
            Assert.That(galaxy.LoadedEntities, Is.Empty);
        }

        private sealed class MemorySurveyRepository : ISurveyRepository
        {
            private SurveyState state;
            public MemorySurveyRepository(SurveySnapshotId snapshot, string profile, GalaxyEntityId starting,
                params GalaxyEntityId[] additionalIdentified)
            {
                var identified = new List<GalaxyEntityId> { starting };
                identified.AddRange(additionalIdentified);
                state = new SurveyState(snapshot, profile, starting, identified, identified);
            }
            public int SaveCount { get; private set; }
            public SurveyState LoadOrCreate(string profileId, SurveySnapshotId snapshot, GalaxyEntityId startingSystemId) => state;
            public bool FailSave { get; set; }
            public void Save(SurveyState value)
            {
                if (FailSave) throw new IOException("simulated disk failure");
                state = value; SaveCount++;
            }
        }

        private sealed class ControlledSnapshotSource : IGalaxySnapshotSource
        {
            private readonly System.Threading.Tasks.TaskCompletionSource<SnapshotSourceResult> first = new();
            private readonly System.Threading.Tasks.TaskCompletionSource<SnapshotSourceResult> second = new();
            private int calls;
            public System.Threading.Tasks.Task<SnapshotSourceResult> OpenAsync(System.Threading.CancellationToken cancellationToken) =>
                calls++ == 0 ? first.Task : second.Task;
            public System.Threading.Tasks.Task<SnapshotSourceResult> LoadAsync(GalaxySnapshotManifest manifest,
                SpatialScopeRequest request, System.Threading.CancellationToken cancellationToken) =>
                System.Threading.Tasks.Task.FromResult(SnapshotSourceResult.Failure("unused"));
            public void CompleteFirst() => first.TrySetResult(Result(1));
            public void CompleteSecond() => second.TrySetResult(Result(2));
            private static SnapshotSourceResult Result(long revision)
            {
                var manifest = new GalaxySnapshotManifest(revision, 3, "spatial-scene-v1");
                var scene = new GalaxySnapshotScene(revision, 3,
                    new SpatialScopeRequest(SpatialScope.Overview), Array.Empty<SnapshotNode>(), Array.Empty<SnapshotRoute>());
                return SnapshotSourceResult.Success(manifest, scene);
            }
        }
    }
}
