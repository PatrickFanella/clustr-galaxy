using System;
using System.Threading;
using System.Threading.Tasks;
using ClustrGalaxy.Core.Data;
using ClustrGalaxy.Core.Domain;
using ClustrGalaxy.Core.Survey;
using ClustrGalaxy.Core.Session;
using NUnit.Framework;
namespace ClustrGalaxy.Tests
{
    public sealed class SnapshotStagingTests
    {
        private sealed class Source : IGalaxySnapshotSource
        {
            public readonly GalaxySnapshotManifest Manifest = new(9, 3, "spatial-scene-v1");
            public TaskCompletionSource<SnapshotSourceResult> Pending;
            public SnapshotSourceResult Next;
            public Task<SnapshotSourceResult> OpenAsync(CancellationToken token) => Task.FromResult(
                SnapshotSourceResult.Success(Manifest, Scene(new SpatialScopeRequest(SpatialScope.Overview, default))));
            public GalaxySnapshotScene Scene(SpatialScopeRequest scope) => new(9, 3, scope,
                new[] { new SnapshotNode(scope.RootId.IsEmpty ? (GalaxyEntityId)"community:test" : scope.RootId, GalaxyEntityKind.Subreddit, "Fixture", 1) }, null);
            public Task<SnapshotSourceResult> LoadAsync(GalaxySnapshotManifest manifest, SpatialScopeRequest request, CancellationToken token) =>
                Pending?.Task ?? Task.FromResult(Next ?? SnapshotSourceResult.Success(Manifest, Scene(request), true));
        }
        private sealed class UnusedSurveyRepository : ISurveyRepository
        {
            public SurveyState LoadOrCreate(string profile, SurveySnapshotId snapshot, GalaxyEntityId start) => throw new InvalidOperationException("Travel must not load Survey progression.");
            public void Save(SurveyState state) => throw new InvalidOperationException("Travel must not save Survey progression.");
        }
        [Test] public async Task SessionPublishesResidentChangeOnlyForCommittedArrivalReceipt()
        {
            var source = new Source(); using var session = new GalaxySession(source, new UnusedSurveyRepository(), "pilot");
            await session.OpenAsync(); var events = 0; session.ResidentChanged += _ => events++;
            var scope = System("subreddit:destination"); var receipt = await session.StageSceneAsync(scope);
            Assert.That(events, Is.Zero); Assert.That(session.Resident.TryFindLoaded(scope.RootId, out _), Is.False);
            Assert.That(session.CommitStagedScene(receipt).Succeeded, Is.True);
            Assert.That(events, Is.EqualTo(1)); Assert.That(session.Resident.TryFindLoaded(scope.RootId, out _), Is.True);
            Assert.That(session.CommitStagedScene(receipt).IsStale, Is.True); Assert.That(events, Is.EqualTo(1));
        }
        [Test] public async Task OrbitReleaseRetainsSelectionThenReleasesRetiredBody()
        {
            var source = new Source(); using var session = new GalaxySession(source, new UnusedSurveyRepository(), "pilot");
            await session.OpenAsync(); var scope = System("subreddit:orbit"); await session.LoadAsync(scope);
            Assert.That(session.Navigation.Orbit.Begin(scope.RootId, new System.Numerics.Vector3(2, 0, 0), default, 1, 1), Is.True);
            session.RefreshNavigationReferences(); session.SetSelection(scope.RootId); session.ReleaseScope(scope);
            Assert.That(session.Resident.TryFindLoaded(scope.RootId, out _), Is.True);
            session.Navigation.Orbit.Cancel(); session.RefreshNavigationReferences();
            Assert.That(session.Resident.TryFindLoaded(scope.RootId, out _), Is.True);
            session.SetSelection(default);
            Assert.That(session.Resident.TryFindLoaded(scope.RootId, out _), Is.False);
        }
        private static SpatialScopeRequest System(string id) => new(SpatialScope.System, (GalaxyEntityId)id);

        [Test] public async Task StagingLeavesActiveSceneAndModeUntilExactOnceCommit()
        {
            var source = new Source(); using var session = new GalaxySnapshotSession(source);
            await session.OpenAsync(); var originalMode = session.Mode;
            var request = System("subreddit:next"); var staged = await session.StageAsync(request);
            Assert.That(staged.Succeeded, Is.True); Assert.That(session.Scenes.ContainsKey(request), Is.False);
            Assert.That(session.Mode, Is.EqualTo(originalMode));
            Assert.That(session.CommitStaged(staged).Succeeded, Is.True);
            Assert.That(session.Mode, Is.EqualTo(SnapshotDataMode.OfflinePinned));
            Assert.That(session.Scenes[request], Is.SameAs(staged.Scene));
            Assert.That(session.CommitStaged(staged).IsStale, Is.True);
        }
        [Test] public async Task CancelAfterValidationRejectsReceiptWithoutReplacingCurrentScene()
        {
            var source = new Source(); using var session = new GalaxySnapshotSession(source); await session.OpenAsync();
            var old = await session.LoadAsync(System("subreddit:old"));
            var receipt = await session.StageAsync(System("subreddit:new")); session.CancelPending();
            Assert.That(session.CommitStaged(receipt).IsStale, Is.True);
            Assert.That(session.Scenes.Count, Is.EqualTo(2)); Assert.That(session.Scenes[old.Scene.Scope], Is.SameAs(old.Scene));
        }
        [Test] public async Task CallerCancellationRejectsProviderThatIgnoresCancellation()
        {
            var source = new Source(); using var session = new GalaxySnapshotSession(source); await session.OpenAsync();
            using var cancellation = new CancellationTokenSource();
            source.Pending = new TaskCompletionSource<SnapshotSourceResult>();
            var request = System("subreddit:late"); var task = session.StageAsync(request, cancellation.Token);
            cancellation.Cancel(); source.Pending.SetResult(SnapshotSourceResult.Success(source.Manifest, source.Scene(request)));
            var receipt = await task; Assert.That(receipt.IsStale, Is.True); Assert.That(session.Scenes.Count, Is.EqualTo(1));
        }
        [Test] public async Task NewOperationAndOtherSessionInvalidateOldReceipt()
        {
            var source = new Source(); using var session = new GalaxySnapshotSession(source); using var other = new GalaxySnapshotSession(source);
            await session.OpenAsync(); await other.OpenAsync();
            var receipt = await session.StageAsync(System("subreddit:a"));
            Assert.That(other.CommitStaged(receipt).IsStale, Is.True);
            await session.StageAsync(System("subreddit:b"));
            Assert.That(session.CommitStaged(receipt).IsStale, Is.True); Assert.That(session.Scenes.Count, Is.EqualTo(1));
        }
        [Test] public async Task FailureAndWrongNamespaceCannotChangeActiveScene()
        {
            var source = new Source(); using var session = new GalaxySnapshotSession(source); await session.OpenAsync();
            var request = System("subreddit:a");
            source.Next = SnapshotSourceResult.Failure("Unavailable");
            Assert.That((await session.StageAsync(request)).Succeeded, Is.False);
            source.Next = SnapshotSourceResult.Success(new GalaxySnapshotManifest(9, 3, "spatial-scene-v1", "demo"), source.Scene(request));
            Assert.That((await session.StageAsync(request)).Succeeded, Is.False); Assert.That(session.Scenes.Count, Is.EqualTo(1));
        }
        [Test] public async Task CancelledCallerCannotCommitAlreadyValidatedReceipt()
        {
            var source = new Source(); using var session = new GalaxySnapshotSession(source); await session.OpenAsync();
            using var cancellation = new CancellationTokenSource();
            var receipt = await session.StageAsync(System("subreddit:a"), cancellation.Token); cancellation.Cancel();
            Assert.That(session.CommitStaged(receipt).IsStale, Is.True); Assert.That(session.Scenes.Count, Is.EqualTo(1));
        }
    }
}
