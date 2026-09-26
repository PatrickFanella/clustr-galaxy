using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ClustrGalaxy.Core.Data;
using ClustrGalaxy.Core.Domain;
using ClustrGalaxy.Core.Navigation;
using ClustrGalaxy.Core.Session;
using ClustrGalaxy.Core.Survey;
using ClustrGalaxy.Data;
using ClustrGalaxy.Gameplay;
using ClustrGalaxy.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace ClustrGalaxy.Tests.PlayMode
{
    public sealed class ChartMarkerPlayModeTests
    {
        private sealed class NoSurvey : ISurveyRepository
        {
            public SurveyState LoadOrCreate(string profile, SurveySnapshotId snapshot, GalaxyEntityId id) =>
                throw new InvalidOperationException("Chart presentation cannot start Survey.");
            public void Save(SurveyState state) => throw new InvalidOperationException("Chart presentation cannot write Survey.");
        }

        private sealed class ChartSource : IGalaxySnapshotSource
        {
            public readonly GalaxySnapshotManifest Manifest = new(9, 3, SpatialSceneContract.Version, sourceJson:
                JsonUtility.ToJson(new SpatialManifest { revision_id = 9, spatial_catalog_id = 3, dimensions = 3,
                    scene_contract = SpatialSceneContract.Version }));

            private static SpatialNode Node(string id, string type) => new() { id = id, type = type, name = id, val = 1 };

            private GalaxySnapshotScene Scene(SpatialScopeRequest request, params SpatialNode[] nodes)
            {
                // The community publishes the a–b route that makes the jump eligible.
                var links = request.Scope == SpatialScope.Community
                    ? new[] { new SpatialLink { source = "subreddit:a", target = "subreddit:b", type = "route", weight = 1 } }
                    : Array.Empty<SpatialLink>();
                var scene = new SpatialScene { revision_id = 9, spatial_catalog_id = 3, nodes = nodes, links = links,
                    scope = request.Scope == SpatialScope.Overview ? "" : request.Scope.ToString().ToLowerInvariant(),
                    root_id = request.Scope == SpatialScope.Overview ? "" : request.RootId.Value };
                return new GalaxySnapshotScene(9, 3, request, nodes.Select(node => new SnapshotNode((GalaxyEntityId)node.id,
                    GalaxyKinds.ParseEntityKind(node.type), node.name, node.val)),
                    links.Select(link => new SnapshotRoute((GalaxyRouteId)"route:a-b", (GalaxyEntityId)link.source,
                        (GalaxyEntityId)link.target, link.weight, false)), sourceJson: JsonUtility.ToJson(scene));
            }

            public Task<SnapshotSourceResult> OpenAsync(CancellationToken token) => Task.FromResult(SnapshotSourceResult.Success(
                Manifest, Scene(new SpatialScopeRequest(SpatialScope.Overview), Node("community:region", "community"))));

            public Task<SnapshotSourceResult> LoadAsync(GalaxySnapshotManifest manifest, SpatialScopeRequest request, CancellationToken token)
            {
                var root = request.RootId.Value;
                var scene = request.Scope == SpatialScope.Community
                    ? Scene(request, Node("subreddit:a", "subreddit"), Node("subreddit:b", "subreddit"), Node("user:u", "user"))
                    : Scene(request, Node(root, "subreddit"), Node("post:" + root, "post"), Node("user:u", "user"));
                return Task.FromResult(SnapshotSourceResult.Success(Manifest, scene));
            }
        }

        private static IEnumerator WaitFor(Func<bool> condition)
        {
            for (var frame = 0; frame < 300 && !condition(); frame++) yield return null;
            Assert.That(condition(), Is.True, "Timed out waiting for the loader.");
        }

        private static bool OnChart(GalaxyEntityRegistry registry, string id) =>
            registry.FindEntity(id)?.transform.parent?.name.StartsWith("Live Region", StringComparison.Ordinal) == true;

        [UnityTest]
        public IEnumerator EnteredSystemLeavesChartMarkersAndReturnsBodiesOnTheNextSystem()
        {
            var storage = Path.Combine(Application.temporaryCachePath, "clustr-chart-" + Guid.NewGuid().ToString("N"));
            var host = new GameObject("chart marker host");
            GalaxySession session = null;
            try
            {
                var loader = host.AddComponent<SpatialSceneLoader>();
                loader.Initialize("https://fixture.invalid/api");
                var registry = new GalaxyEntityRegistry();
                var visibility = host.AddComponent<GalaxySurveyVisibilityController>();
                visibility.BindRegistry(registry);
                visibility.Initialize(new GalaxyModeState(), SurveySnapshotKey.Demo, "pilot", storage);
                var materializer = host.AddComponent<LiveGalaxyMaterializer>();
                materializer.Initialize(loader);
                materializer.BindDiscoveryVisibility(visibility);
                session = new GalaxySession(new ChartSource(), new NoSurvey(), "pilot");
                var opened = loader.BindAndOpenAsync(session);
                yield return WaitFor(() => opened.IsCompleted);
                Assert.That(opened.Result, Is.True, loader.StatusText);

                loader.LoadCommunity("community:region");
                yield return WaitFor(() => loader.Scene?.scope == "community");
                Assert.That(OnChart(registry, "subreddit:a"), Is.True);
                Assert.That(OnChart(registry, "user:u"), Is.True);

                loader.LoadSystem("subreddit:a");
                yield return WaitFor(() => loader.Scene?.root_id == "subreddit:a");
                Assert.That(OnChart(registry, "subreddit:a"), Is.False, "The physical star is lent to the system.");
                Assert.That(materializer.TryGetChartMarker("subreddit:a", out var starMarker), Is.True);
                Assert.That(starMarker.parent.name, Does.StartWith("Live Region"));
                Assert.That(starMarker.GetComponent<Collider>(), Is.Null);
                Assert.That(starMarker.GetComponent<GalaxyEntity>(), Is.Null);
                Assert.That(materializer.TryGetChartMarker("user:u", out _), Is.True);
                Assert.That(materializer.TryGetChartMarker("subreddit:b", out _), Is.False);

                // Only a committed jump changes the physical system.
                var travel = session.Travel;
                Assert.That(travel.Apply(new TravelIntent(travel.State.SessionId, travel.NextOperationId, travel.State.Snapshot,
                    TravelAction.Jump, (GalaxyEntityId)"subreddit:b")).Committed, Is.True, session.JumpError);
                travel.AdvanceCharge(3);
                var staged = session.StageJumpAsync();
                yield return WaitFor(() => staged.IsCompleted);
                Assert.That(staged.Result, Is.Not.Null, session.JumpError);
                var arrival = session.CommitJump(staged.Result, 500, 0);
                Assert.That(arrival.Succeeded, Is.True, arrival.Error);
                loader.PresentCommittedJump(arrival);
                Assert.That(loader.Scene.root_id, Is.EqualTo("subreddit:b"));
                Assert.That(OnChart(registry, "subreddit:a"), Is.True, "Leaving a system returns its star to the chart.");
                Assert.That(materializer.TryGetChartMarker("subreddit:a", out _), Is.False);
                Assert.That(materializer.TryGetChartMarker("subreddit:b", out _), Is.True);
                Assert.That(materializer.TryGetChartMarker("user:u", out _), Is.True);
            }
            finally
            {
                session?.Dispose();
                Object.Destroy(host);
                if (Directory.Exists(storage)) Directory.Delete(storage, true);
            }
        }
    }
}
