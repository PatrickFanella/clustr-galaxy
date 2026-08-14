using System;
using System.Collections.Generic;
using System.IO;
using ClustrGalaxy.Core.Domain;
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

            Assert.That(galaxy.LoadedEntities, Has.Count.EqualTo(2));
            Assert.That(galaxy.ActiveEntities, Has.Count.EqualTo(1));
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
        public void Navigation_MapCloseRestoresMomentumAndFastTravelClearsIt()
        {
            var navigation = new GalaxyNavigation();
            navigation.Apply(new SetMapIntent(true, new NavigationPose(Vector3.Zero, Quaternion.Identity),
                new Vector3(4, 0, 0), new Vector3(0, 2, 0)));
            var close = navigation.Apply(new SetMapIntent(false, new NavigationPose(Vector3.Zero, Quaternion.Identity),
                Vector3.Zero, Vector3.Zero));
            Assert.That(close.Effect.Velocity.X, Is.EqualTo(4));

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
            public void Save(SurveyState value) { state = value; SaveCount++; }
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
