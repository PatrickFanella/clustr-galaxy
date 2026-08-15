using System;
using System.IO;
using ClustrGalaxy.Data;
using ClustrGalaxy.Flight;
using ClustrGalaxy.Generation;
using ClustrGalaxy.Interaction;
using ClustrGalaxy.Simulation;
using ClustrGalaxy.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Networking;

namespace ClustrGalaxy.Tests
{
    public sealed class SpatialSceneContractsTests
    {
        [Test]
        public void Manifest_ParsesThePublishedSceneContract()
        {
            const string json = "{\"revision_id\":9,\"spatial_catalog_id\":3,\"dimensions\":3," +
                "\"scene_contract\":\"spatial-scene-v1\",\"bounds\":{" +
                "\"min_x\":-10,\"max_x\":10,\"min_y\":-20,\"max_y\":20," +
                "\"min_z\":-30,\"max_z\":30}}";

            var manifest = JsonUtility.FromJson<SpatialManifest>(json);

            Assert.That(manifest.revision_id, Is.EqualTo(9));
            Assert.That(manifest.spatial_catalog_id, Is.EqualTo(3));
            Assert.That(manifest.IsCompatible, Is.True);
        }

        [Test]
        public void Scene_RejectsAMixedRevision()
        {
            var manifest = CompatibleManifest(revision: 9, catalog: 3);
            var scene = new SpatialScene { revision_id = 10, spatial_catalog_id = 3 };

            Assert.That(scene.BelongsTo(manifest), Is.False);
        }

        [Test]
        public void Scene_RejectsAMixedSpatialCatalog()
        {
            var manifest = CompatibleManifest(revision: 9, catalog: 3);
            var scene = new SpatialScene { revision_id = 9, spatial_catalog_id = 4 };

            Assert.That(scene.BelongsTo(manifest), Is.False);
        }

        [Test]
        public void ScopeContract_RecoversMetadataOmittedByThePublishedCommunityResponse()
        {
            const string liveShape = "{\"revision_id\":1,\"spatial_catalog_id\":3," +
                "\"nodes\":[{\"id\":\"subreddit_1569\",\"type\":\"subreddit\"}],\"links\":[]}";
            var scene = SpatialSceneJson.ParseScene(liveShape);

            SpatialSceneScopeContract.ApplyOrValidate(
                scene, "community", "c:new:c3875a74587b5df7");

            Assert.That(scene.scope, Is.EqualTo("community"));
            Assert.That(scene.root_id, Is.EqualTo("c:new:c3875a74587b5df7"));
        }

        [Test]
        public void ScopeContract_RejectsContradictoryPublishedMetadata()
        {
            var scene = new SpatialScene { scope = "system", root_id = "subreddit_other" };

            Assert.Throws<InvalidOperationException>(() =>
                SpatialSceneScopeContract.ApplyOrValidate(
                    scene, "community", "c:new:c3875a74587b5df7"));
        }

        [Test]
        public void SceneStore_MergesStableIdsAndRejectsMixedSnapshots()
        {
            var manifest = CompatibleManifest(revision: 9, catalog: 3);
            var store = new SpatialSceneStore(manifest);
            store.Merge(new SpatialScene
            {
                revision_id = 9,
                spatial_catalog_id = 3,
                scope = "system",
                root_id = "subreddit_1",
                nodes = new[] { new SpatialNode { id = "post_a", name = "first", type = "post" } }
            });
            store.Merge(new SpatialScene
            {
                revision_id = 9,
                spatial_catalog_id = 3,
                scope = "discussion",
                root_id = "post_a",
                nodes = new[] { new SpatialNode { id = "post_a", name = "updated", type = "post" } }
            });

            Assert.That(store.NodeCount, Is.EqualTo(1));
            Assert.That(store.GetNode("post_a").name, Is.EqualTo("updated"));
            Assert.That(store.IsScopeLoaded("discussion", "post_a"), Is.True);
            Assert.Throws<InvalidOperationException>(() => store.Merge(new SpatialScene
            {
                revision_id = 10,
                spatial_catalog_id = 3
            }));
        }

        [Test]
        public void SnapshotCache_RejectsTamperingAndKeepsPinnedBaseEntries()
        {
            var root = Path.Combine(Path.GetTempPath(), "clustr-cache-test-" + Guid.NewGuid().ToString("N"));
            try
            {
                var cache = new SnapshotCache(root, 80);
                cache.Write("manifest", "{\"revision_id\":9}", pinnedBase: true);
                cache.Write("old-system", new string('a', 60), pinnedBase: false);
                cache.Write("new-system", new string('b', 60), pinnedBase: false);

                Assert.That(cache.TryRead("manifest", out var manifestJson), Is.True);
                Assert.That(manifestJson, Does.Contain("revision_id"));
                Assert.That(cache.Contains("old-system"), Is.False);

                File.AppendAllText(cache.PathForKey("manifest"), "tampered");
                Assert.That(cache.TryRead("manifest", out _), Is.False);
            }
            finally
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, recursive: true);
                }
            }
        }

        [Test]
        public void SceneClient_RetriesOnlyTransientFailures()
        {
            Assert.That(SpatialSceneClient.ShouldRetry(UnityWebRequest.Result.ProtocolError, 429), Is.True);
            Assert.That(SpatialSceneClient.ShouldRetry(UnityWebRequest.Result.ProtocolError, 503), Is.True);
            Assert.That(SpatialSceneClient.ShouldRetry(UnityWebRequest.Result.ConnectionError, 0), Is.True);
            Assert.That(SpatialSceneClient.ShouldRetry(UnityWebRequest.Result.ProtocolError, 404), Is.False);
            Assert.That(SpatialSceneClient.ShouldRetry(UnityWebRequest.Result.DataProcessingError, 200), Is.False);
        }

        [Test]
        public void FloatingOrigin_RebasesOnlyBeyondThresholdAndTracksLogicalOffset()
        {
            Assert.That(FloatingOriginMath.RequiredShift(new Vector3(1999f, 0f, 0f), 2000f), Is.EqualTo(Vector3.zero));

            var renderedPosition = new Vector3(2100f, -25f, 40f);
            var shift = FloatingOriginMath.RequiredShift(renderedPosition, 2000f);
            var logicalOrigin = DoubleVector3.Zero.Add(-shift);

            Assert.That(shift, Is.EqualTo(-renderedPosition));
            Assert.That(logicalOrigin.X, Is.EqualTo(2100d));
            Assert.That(logicalOrigin.Y, Is.EqualTo(-25d));
            Assert.That(logicalOrigin.Z, Is.EqualTo(40d));
        }

        [Test]
        public void StarfieldRendering_RequiresGraphicsInstancingSupport()
        {
            var mesh = new Mesh();
            var shader = Shader.Find("Sprites/Default");
            Assert.That(shader, Is.Not.Null);
            var material = new Material(shader);
            try
            {
                Assert.That(InstancedStarfield.CanRender(false, mesh, material, 100), Is.False);
                Assert.That(InstancedStarfield.CanRender(true, mesh, material, 100), Is.True);
                Assert.That(InstancedStarfield.CanRender(true, mesh, material, 0), Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(material);
                UnityEngine.Object.DestroyImmediate(mesh);
            }
        }

        [Test]
        public void OverviewRequest_PinsTheManifestRevision()
        {
            var manifest = CompatibleManifest(revision: 9, catalog: 3);

            var uri = SpatialSceneRequestUris.Overview(
                "https://clustr.subcult.tv/api/", manifest);

            Assert.That(uri, Is.EqualTo(
                "https://clustr.subcult.tv/api/graph/overview?level=0&max_nodes=300&max_links=180&with_positions=true&revision=9"));
        }

        [Test]
        public void CommunityRequest_IsBoundedAndPinnedToRevisionAndCatalog()
        {
            var manifest = CompatibleManifest(revision: 9, catalog: 3);

            var uri = SpatialSceneRequestUris.Community(
                "https://clustr.subcult.tv/api/", manifest, "community_12");

            Assert.That(uri, Is.EqualTo(
                "https://clustr.subcult.tv/api/graph/community/community_12?lod=near&max_nodes=500&max_links=1500&revision=9&spatial_catalog_id=3"));
        }

        [Test]
        public void SystemRequest_LoadsOnlyBoundedPostsAndParticipants()
        {
            var manifest = CompatibleManifest(revision: 9, catalog: 3);

            var uri = SpatialSceneRequestUris.System(
                "https://clustr.subcult.tv/api/", manifest, "subreddit_42");

            Assert.That(uri, Is.EqualTo(
                "https://clustr.subcult.tv/api/graph/entity/subreddit_42?expand=system&max_nodes=512&max_links=5000&revision=9&spatial_catalog_id=3"));
        }

        [Test]
        public void DiscussionAndThreadRequests_KeepRepliesLazyAndBounded()
        {
            var manifest = CompatibleManifest(revision: 9, catalog: 3);

            var discussion = SpatialSceneRequestUris.Discussion(
                "https://clustr.subcult.tv/api/", manifest, "post_abc");
            var thread = SpatialSceneRequestUris.Thread(
                "https://clustr.subcult.tv/api/", manifest, "comment_parent");

            Assert.That(discussion, Is.EqualTo(
                "https://clustr.subcult.tv/api/graph/entity/post_abc?expand=discussion&max_nodes=512&max_links=5000&revision=9&spatial_catalog_id=3"));
            Assert.That(thread, Is.EqualTo(
                "https://clustr.subcult.tv/api/graph/entity/comment_parent?expand=thread&depth=3&max_nodes=250&max_links=1500&revision=9&spatial_catalog_id=3"));
        }

        [Test]
        public void ScopePaging_BindsEncodedCursorWithoutChangingSnapshotParameters()
        {
            const string pinned = "https://clustr.subcult.tv/api/graph/community/region-a?lod=near&revision=9&spatial_catalog_id=3";

            var next = SpatialScopePaging.WithCursor(pinned, "catalog:3 offset:500/+?");

            Assert.That(next, Is.EqualTo(pinned + "&cursor=catalog%3A3%20offset%3A500%2F%2B%3F"));
        }

        [Test]
        public void ScopePaging_UsesPublishedCumulativeCeilings()
        {
            Assert.That(SpatialScopePaging.For("community").MaximumNodes, Is.EqualTo(5000));
            Assert.That(SpatialScopePaging.For("community").MaximumLinks, Is.EqualTo(15000));
            Assert.That(SpatialScopePaging.For("discussion").MaximumNodes, Is.EqualTo(2048));
            Assert.That(SpatialScopePaging.For("thread").MaximumNodes, Is.EqualTo(2000));
        }

        [Test]
        public void ScopePaging_FlagsAFullPublishedPageWithoutContinuationMetadata()
        {
            var page = new SpatialScene
            {
                scope = "community",
                nodes = new SpatialNode[500],
                links = new SpatialLink[1500]
            };
            var limits = SpatialScopePaging.For("community");

            Assert.That(SpatialScopePaging.CompletionFor(page, limits, 500, 1500),
                Is.EqualTo(SpatialScopeCompletion.ContinuationUnavailable));
        }

        [Test]
        public void ScopePaging_AggregatesMatchingPagesAndKeepsTheFinalCursorState()
        {
            var manifest = CompatibleManifest(revision: 9, catalog: 3);
            var pages = new[]
            {
                new SpatialScene
                {
                    revision_id = 9, spatial_catalog_id = 3, scope = "community", root_id = "region-a",
                    truncated = true, next_cursor = "page-2",
                    nodes = new[]
                    {
                        new SpatialNode { id = "subreddit-a", type = "subreddit" },
                        new SpatialNode { id = "user-a", type = "user" }
                    },
                    links = new[]
                    {
                        new SpatialLink { source = "user-a", target = "subreddit-a", type = "activity" }
                    }
                },
                new SpatialScene
                {
                    revision_id = 9, spatial_catalog_id = 3, scope = "community", root_id = "region-a",
                    truncated = false,
                    nodes = new[]
                    {
                        new SpatialNode { id = "subreddit-b", type = "subreddit" },
                        new SpatialNode { id = "subreddit-a", type = "subreddit", name = "updated" }
                    }
                }
            };

            var aggregate = SpatialScopePaging.Aggregate(manifest, "community", "region-a", pages);

            Assert.That(aggregate.nodes, Has.Length.EqualTo(3));
            Assert.That(Array.Find(aggregate.nodes, node => node.id == "subreddit-a").name, Is.EqualTo("updated"));
            Assert.That(aggregate.links, Has.Length.EqualTo(1));
            Assert.That(aggregate.truncated, Is.False);
            Assert.That(aggregate.next_cursor, Is.Empty);
        }

        [Test]
        public void ScopePaging_RejectsAContradictoryPageBeforeAggregation()
        {
            var manifest = CompatibleManifest(revision: 9, catalog: 3);
            var pages = new[]
            {
                new SpatialScene
                {
                    revision_id = 9, spatial_catalog_id = 3, scope = "system", root_id = "subreddit-other"
                }
            };

            Assert.Throws<InvalidOperationException>(() =>
                SpatialScopePaging.Aggregate(manifest, "community", "region-a", pages));
        }

        [Test]
        public void LegacyOverview_ParsesAsAnExplicitlyUnpinnedScene()
        {
            const string json = "{\"nodes\":[{\"id\":\"community_1\",\"name\":\"Faces\"," +
                "\"val\":31358,\"type\":\"community\",\"x\":47.9,\"y\":59.1,\"z\":0}],\"links\":[]}";

            var scene = SpatialSceneJson.ParseScene(json);

            Assert.That(scene.revision_id, Is.Zero);
            Assert.That(scene.nodes, Has.Length.EqualTo(1));
            Assert.That(scene.nodes[0].name, Is.EqualTo("Faces"));
            Assert.That(scene.nodes[0].val, Is.EqualTo(31358));
        }

        [Test]
        public void DataStatus_DistinguishesLiveOfflineLegacyAndLocalModes()
        {
            var manifest = CompatibleManifest(revision: 9, catalog: 3);
            manifest.published_at = "2026-08-12T23:42:00Z";

            Assert.That(GalaxyDataStatus.Pinned(manifest, offline: false, 42),
                Is.EqualTo("LIVE REV 9 / CAT 3  42 LANDMARKS"));
            Assert.That(GalaxyDataStatus.Pinned(manifest, offline: true, 42),
                Is.EqualTo("OFFLINE REV 9 / CAT 3  42 LANDMARKS  •  PUBLISHED 2026-08-12 23:42 UTC"));
            Assert.That(GalaxyDataStatus.Legacy(42),
                Is.EqualTo("DEMO / LEGACY / UNPINNED  42 LANDMARKS"));
            Assert.That(GalaxyDataStatus.Fallback("ConnectionError: offline"),
                Is.EqualTo("LOCAL DEMO FALLBACK  ConnectionError: offline"));
        }

        [Test]
        public void NavigationHistory_ReturnsToTheExactPriorPoseAndHierarchy()
        {
            var galaxyPose = new NavigationPose(
                new Vector3(10f, 20f, 30f), Quaternion.Euler(1f, 2f, 3f));
            var regionPose = new NavigationPose(
                new Vector3(110f, 120f, 130f), Quaternion.Euler(4f, 5f, 6f));
            var history = new GalaxyNavigationHistory();

            history.Enter(GalaxyNavigationLevel.Region, "community_7", galaxyPose);
            history.Enter(GalaxyNavigationLevel.System, "subreddit_9", regionPose);

            Assert.That(history.Level, Is.EqualTo(GalaxyNavigationLevel.System));
            Assert.That(history.StableId, Is.EqualTo("subreddit_9"));
            Assert.That(history.TryBack(out var restored), Is.True);
            Assert.That(history.Level, Is.EqualTo(GalaxyNavigationLevel.Region));
            Assert.That(history.StableId, Is.EqualTo("community_7"));
            Assert.That(restored.Position, Is.EqualTo(regionPose.Position));
            Assert.That(restored.Rotation, Is.EqualTo(regionPose.Rotation));
            Assert.That(history.TryBack(out restored), Is.True);
            Assert.That(history.Level, Is.EqualTo(GalaxyNavigationLevel.Galaxy));
            Assert.That(restored.Position, Is.EqualTo(galaxyPose.Position));
            Assert.That(history.TryBack(out _), Is.False);
        }

        [Test]
        public void MapSelection_OnlyAllowsTheNextSemanticLevel()
        {
            Assert.That(GalaxyMapSelection.IsSelectable(GalaxyNavigationLevel.Galaxy, "community"), Is.True);
            Assert.That(GalaxyMapSelection.IsSelectable(GalaxyNavigationLevel.Galaxy, "post"), Is.False);
            Assert.That(GalaxyMapSelection.IsSelectable(GalaxyNavigationLevel.Region, "subreddit"), Is.True);
            Assert.That(GalaxyMapSelection.IsSelectable(GalaxyNavigationLevel.System, "post"), Is.True);
            Assert.That(GalaxyMapSelection.IsSelectable(GalaxyNavigationLevel.Inspect, "comment"), Is.True);
        }

        [Test]
        public void MapSelection_PicksTheNearestVisibleCandidateWithinRadius()
        {
            var cursor = new Vector2(0.5f, 0.5f);
            var candidates = new[]
            {
                new GalaxyMapCandidate("far", "community", new Vector2(0.6f, 0.5f), true),
                new GalaxyMapCandidate("near", "community", new Vector2(0.52f, 0.5f), true),
                new GalaxyMapCandidate("behind", "community", cursor, false)
            };

            Assert.That(GalaxyMapSelection.NearestId(
                GalaxyNavigationLevel.Galaxy, cursor, candidates, 0.15f), Is.EqualTo("near"));
            Assert.That(GalaxyMapSelection.NearestId(
                GalaxyNavigationLevel.Galaxy, Vector2.zero, candidates, 0.05f), Is.Empty);
        }

        [Test]
        public void GalaxyMapFraming_CentersTheOverviewWhenNothingIsSelected()
        {
            var focus = GalaxyNavigationController.ResolveMapFocus(
                GalaxyNavigationLevel.Galaxy,
                hasFocusedEntity: false, Vector3.zero,
                hasTarget: false, Vector3.zero,
                new Vector3(10f, 20f, 30f), Vector3.forward);

            Assert.That(focus, Is.EqualTo(Vector3.zero));
            Assert.That(GalaxyNavigationController.ResolveMapFocus(
                    GalaxyNavigationLevel.Region,
                    hasFocusedEntity: false, Vector3.zero,
                    hasTarget: false, Vector3.zero,
                    new Vector3(10f, 20f, 30f), Vector3.forward),
                Is.EqualTo(new Vector3(10f, 20f, 270f)));
        }

        [Test]
        public void LayerVisibility_KeepsThePreviousLayerVisibleDuringPrefetch()
        {
            var loadingSystem = GalaxyLayerVisibility.For(
                GalaxyNavigationLevel.System, hasRegion: true, hasSystem: false,
                hasDiscussion: false, hasThread: false);
            var inspectingThread = GalaxyLayerVisibility.For(
                GalaxyNavigationLevel.Inspect, hasRegion: true, hasSystem: true,
                hasDiscussion: true, hasThread: true);

            Assert.That(loadingSystem.Region, Is.True);
            Assert.That(loadingSystem.System, Is.False);
            Assert.That(inspectingThread.Region, Is.False);
            Assert.That(inspectingThread.System, Is.True);
            Assert.That(inspectingThread.Discussion, Is.True);
            Assert.That(inspectingThread.Thread, Is.True);
        }

        [Test]
        public void EntityPool_ReusesObjectsAndRemovesReleasedIdsFromRegistry()
        {
            var owner = new GameObject("pool owner");
            try
            {
                var pool = new GalaxyEntityPool(owner.transform);
                var first = pool.Acquire(PrimitiveType.Sphere, owner.transform);
                first.GetComponent<GalaxyEntity>().Initialize("post_old", "Old", "post planet", 1);

                Assert.That(GalaxyEntity.TryGet("post_old", out _), Is.True);
                pool.Release(first);
                Assert.That(GalaxyEntity.TryGet("post_old", out _), Is.False);

                var reused = pool.Acquire(PrimitiveType.Sphere, owner.transform);
                Assert.That(reused, Is.SameAs(first));
                reused.GetComponent<GalaxyEntity>().Initialize("post_new", "New", "post planet", 2);
                Assert.That(GalaxyEntity.TryGet("post_new", out _), Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void LandmarkProjection_IsStableAndAddsDepthToFlatLegacyCoordinates()
        {
            var bounds = new SpatialBounds
            {
                min_x = 0f, max_x = 100f,
                min_y = 0f, max_y = 100f,
                min_z = 0f, max_z = 0f
            };
            var node = new SpatialNode
            {
                id = "community_1436434", x = 47.9f, y = 59.1f, z = 0f
            };

            var first = GalacticLandmarkLayout.Project(node, bounds);
            var repeat = GalacticLandmarkLayout.Project(node, bounds);

            Assert.That(repeat, Is.EqualTo(first));
            Assert.That(first.y, Is.Not.Zero);
            Assert.That(first.magnitude, Is.GreaterThan(360f));
        }

        [Test]
        public void RouteStrength_PreservesAFaintLegacyRouteAndHighlightsStrongTraffic()
        {
            var legacy = GalacticRouteStyle.NormalizeStrength(0, 1000);
            var strong = GalacticRouteStyle.NormalizeStrength(1000, 1000);

            Assert.That(legacy, Is.GreaterThan(0f));
            Assert.That(strong, Is.EqualTo(1f));
            Assert.That(strong, Is.GreaterThan(legacy));
        }

        [Test]
        public void CommentRingPlacement_IsStablePerCommentAndVariesAcrossParents()
        {
            var first = CommentRingLayout.AngleRadians("comment:parent-1");
            var repeat = CommentRingLayout.AngleRadians("comment:parent-1");
            var other = CommentRingLayout.AngleRadians("comment:parent-2");

            Assert.That(repeat, Is.EqualTo(first));
            Assert.That(first, Is.InRange(0d, Math.PI * 2d));
            Assert.That(other, Is.Not.EqualTo(first));
        }

        [Test]
        public void CommentThread_StoresReplyMetadataWithoutCreatingAWorldVisual()
        {
            var parent = new GameObject("parent comment");
            try
            {
                var thread = parent.AddComponent<CommentThreadNode>();
                thread.Initialize(4);

                Assert.That(thread.ReplyCount, Is.EqualTo(4));
                Assert.That(thread.DemoModel, Is.Null);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(parent);
            }
        }

        [Test]
        public void BotActivityPolicy_SuppressesAutomationWithoutHidingBroadHumanActivity()
        {
            Assert.That(BotActivityPolicy.ShouldSuppress("AutoModerator", false, 1), Is.True);
            Assert.That(BotActivityPolicy.ShouldSuppress("RemindMeBot", false, 80), Is.True);
            Assert.That(BotActivityPolicy.ShouldSuppress("widely_traveled_human", false, 80), Is.False);
            Assert.That(BotActivityPolicy.ShouldSuppress("robotics_fan", false, 80), Is.False);
            Assert.That(BotActivityPolicy.ShouldSuppress("ordinary_account", true, 1), Is.True);
        }

        [Test]
        public void TargetingScore_PrioritizesADirectReticleHitOverANearbyAngularCandidate()
        {
            var directComment = ProbeTargetingModel.Score(
                angleDegrees: 0.5f, distance: 80f, directReticleHit: true);
            var nearbyPlanet = ProbeTargetingModel.Score(
                angleDegrees: 0.1f, distance: 30f, directReticleHit: false);

            Assert.That(directComment, Is.LessThan(nearbyPlanet));
        }

        [Test]
        public void VisualCatalog_WrapsShipVariantsDeterministically()
        {
            var first = new GameObject("first ship");
            var second = new GameObject("second ship");
            var catalog = ScriptableObject.CreateInstance<GalaxyVisualCatalog>();
            try
            {
                catalog.userShipPrefabs = new[] { first, second };

                Assert.That(catalog.UserShipForIndex(0), Is.SameAs(first));
                Assert.That(catalog.UserShipForIndex(2), Is.SameAs(first));
                Assert.That(catalog.UserShipForIndex(3), Is.SameAs(second));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(catalog);
                UnityEngine.Object.DestroyImmediate(first);
                UnityEngine.Object.DestroyImmediate(second);
            }
        }

        [Test]
        public void FlightModel_AcceleratesWithoutOvershootingDesiredVelocity()
        {
            var velocity = ProbeFlightModel.MoveTowardsVelocity(
                Vector3.zero, new Vector3(0f, 0f, 10f), 80f, 1f);

            Assert.That(velocity, Is.EqualTo(new Vector3(0f, 0f, 10f)));
        }

        [Test]
        public void FlightModel_DeadzoneRemovesControllerDrift()
        {
            Assert.That(ProbeFlightModel.ApplyDeadzone(0.08f), Is.Zero);
            Assert.That(ProbeFlightModel.ApplyDeadzone(-0.08f), Is.Zero);
            Assert.That(ProbeFlightModel.ApplyDeadzone(1f), Is.EqualTo(1f));
        }

        [Test]
        public void MetricNormalizer_UsesBoundedLogarithmicScaling()
        {
            Assert.That(MetricNormalizer.NormalizeLog(1, 1, 10000), Is.EqualTo(0d));
            Assert.That(MetricNormalizer.NormalizeLog(10000, 1, 10000), Is.EqualTo(1d));

            var middle = MetricNormalizer.NormalizeLog(100, 1, 10000);
            Assert.That(middle, Is.GreaterThan(0d).And.LessThan(1d));
        }

        [Test]
        public void OrbitGenerator_IsStableForIdentityAndVariesByEntity()
        {
            var first = StableOrbitGenerator.GeneratePlanet(
                9, 3, "subreddit:technology", "post:alpha", 42d);
            var repeat = StableOrbitGenerator.GeneratePlanet(
                9, 3, "subreddit:technology", "post:alpha", 42d);
            var other = StableOrbitGenerator.GeneratePlanet(
                9, 3, "subreddit:technology", "post:beta", 42d);

            Assert.That(repeat.Eccentricity, Is.EqualTo(first.Eccentricity));
            Assert.That(repeat.InclinationRadians, Is.EqualTo(first.InclinationRadians));
            Assert.That(repeat.MeanAnomalyAtEpochRadians, Is.EqualTo(first.MeanAnomalyAtEpochRadians));
            Assert.That(first.Eccentricity, Is.InRange(0d, 0.12d));
            Assert.That(first.InclinationRadians, Is.InRange(0d, 12d * Mathf.Deg2Rad));
            Assert.That(other.MeanAnomalyAtEpochRadians, Is.Not.EqualTo(first.MeanAnomalyAtEpochRadians));
        }

        [Test]
        public void MoonOrbitGenerator_UsesStableLowEccentricityElements()
        {
            var first = StableOrbitGenerator.GenerateMoon(
                9, 3, "post:alpha", "comment:first", 6d);
            var repeat = StableOrbitGenerator.GenerateMoon(
                9, 3, "post:alpha", "comment:first", 6d);

            Assert.That(repeat.MeanAnomalyAtEpochRadians,
                Is.EqualTo(first.MeanAnomalyAtEpochRadians));
            Assert.That(first.Eccentricity, Is.InRange(0d, 0.06d));
            Assert.That(first.InclinationRadians, Is.InRange(0d, 8d * Mathf.Deg2Rad));
        }

        [Test]
        public void ProgradeMoonLimit_IsThirtyFivePercentOfTheHillRadius()
        {
            var maximumAxis = OrbitStability.MaximumProgradeMoonAxis(
                planetSemiMajorAxis: 100d,
                planetGravitationalParameter: 70d,
                starGravitationalParameter: 700d);
            var hillRadius = 100d * Math.Pow(70d / (3d * 700d), 1d / 3d);

            Assert.That(maximumAxis, Is.EqualTo(0.35d * hillRadius).Within(0.000001d));
        }

        [Test]
        public void VisualMoonClearance_KeepsPeriapsisOutsideTheRenderedPlanet()
        {
            var axis = OrbitStability.MinimumVisualMoonAxis(
                planetVisualRadius: 4d,
                moonVisualRadius: 0.7d,
                moonEccentricity: 0.06d,
                visualGap: 0.8d);

            Assert.That(axis * (1d - 0.06d), Is.GreaterThanOrEqualTo(5.5d));
        }

        [Test]
        public void KeplerOrbit_ReturnsToItsStartingPositionAfterOnePeriod()
        {
            var elements = new OrbitalElements(
                42d, 0.12d, 7d * Mathf.Deg2Rad, 0.4d, 1.1d, 0.7d);
            var period = KeplerOrbitMath.Period(elements, 700d);
            var start = KeplerOrbitMath.PositionAtTime(elements, 700d, 0d);
            var completed = KeplerOrbitMath.PositionAtTime(elements, 700d, period);

            Assert.That(period, Is.GreaterThan(0d));
            Assert.That(Vector3.Distance(start, completed), Is.LessThan(0.0001f));
            Assert.That(start.magnitude, Is.InRange(
                (float)(elements.SemiMajorAxis * (1d - elements.Eccentricity)),
                (float)(elements.SemiMajorAxis * (1d + elements.Eccentricity))));
        }

        [Test]
        public void Gravity_PullsTowardSourceWithoutExceedingSafetyCap()
        {
            var acceleration = GravityMath.Acceleration(
                new Vector3(10f, 0f, 0f), Vector3.zero, 700d, 4d, 12d);
            var extreme = GravityMath.Acceleration(
                new Vector3(0.01f, 0f, 0f), Vector3.zero, 700d, 4d, 12d);

            Assert.That(acceleration.x, Is.LessThan(0f));
            Assert.That(acceleration.y, Is.EqualTo(0f));
            Assert.That(acceleration.z, Is.EqualTo(0f));
            Assert.That(acceleration.magnitude, Is.LessThanOrEqualTo(12f));
            Assert.That(extreme.magnitude, Is.LessThanOrEqualTo(12f));
        }

        [Test]
        public void OrbitLaneAllocator_ClearsThePreviousApoapsis()
        {
            var previous = new OrbitalElements(30d, 0.1d, 0d, 0d, 0d, 0d);
            var allocated = OrbitLaneAllocator.AllocateAfter(
                previous, 3d, 0.12d, 4d, 5d);
            var previousApoapsis = previous.SemiMajorAxis * (1d + previous.Eccentricity);
            var nextPeriapsis = allocated * (1d - 0.12d);
            var requiredClearance = Math.Max(3d * (3d + 4d), 5d);

            Assert.That(nextPeriapsis - previousApoapsis,
                Is.GreaterThanOrEqualTo(requiredClearance - 0.000001d));
        }

        [Test]
        public void LaunchOptions_RecognizeExplicitOfflineModeCaseInsensitively()
        {
            Assert.That(GalaxyLaunchOptions.IsOfflineOnly(new[] { "ClustrGalaxy", "-clustr-offline" }), Is.True);
            Assert.That(GalaxyLaunchOptions.IsOfflineOnly(new[] { "ClustrGalaxy", "-CLUSTR-OFFLINE" }), Is.True);
            Assert.That(GalaxyLaunchOptions.IsOfflineOnly(new[] { "ClustrGalaxy", "--clustr-offline" }), Is.True);
            Assert.That(GalaxyLaunchOptions.IsOfflineOnly(new[] { "ClustrGalaxy" }), Is.False);
            Assert.That(GalaxyLaunchOptions.IsOfflineOnly(null), Is.False);
            Assert.That(GalaxyLaunchOptions.IsLiveSmoke(new[] { "ClustrGalaxy", "--CLUSTR-LIVE-SMOKE" }), Is.True);
            Assert.That(GalaxyLaunchOptions.IsLiveSmoke(new[] { "ClustrGalaxy", "--clustr-offline" }), Is.False);
        }

        [Test]
        public void LiveSmokeSelection_PicksHighestActivityThenStableId()
        {
            var scene = new SpatialScene
            {
                nodes = new[]
                {
                    new SpatialNode { id = "community_z", type = "community", val = 20 },
                    new SpatialNode { id = "community_b", type = "community", val = 50 },
                    new SpatialNode { id = "community_a", type = "community", val = 50 },
                    new SpatialNode { id = "post_1", type = "post", val = 500 }
                }
            };

            var selected = GalaxyLiveDataSmokeRunner.SelectHighestActivity(scene, "community");

            Assert.That(selected.id, Is.EqualTo("community_a"));
            Assert.That(GalaxyLiveDataSmokeRunner.SelectHighestActivity(scene, "comment"), Is.Null);
        }

        private static SpatialManifest CompatibleManifest(long revision, long catalog) => new()
        {
            revision_id = revision,
            spatial_catalog_id = catalog,
            dimensions = 3,
            scene_contract = SpatialSceneContract.Version,
            bounds = new SpatialBounds
            {
                min_x = -1,
                max_x = 1,
                min_y = -1,
                max_y = 1,
                min_z = -1,
                max_z = 1
            }
        };
    }
}
