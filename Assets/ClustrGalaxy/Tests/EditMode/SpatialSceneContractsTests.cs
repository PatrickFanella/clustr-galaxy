using System;
using System.IO;
using ClustrGalaxy.Data;
using ClustrGalaxy.Flight;
using ClustrGalaxy.Generation;
using ClustrGalaxy.Interaction;
using ClustrGalaxy.Simulation;
using ClustrGalaxy.UI;
using ClustrGalaxy.World;
using ClustrGalaxy.Core.Domain;
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
        public void CacheBudgetIncludesQuarantineAndRejectsOversizedProtectedReplacement()
        {
            var root = Path.Combine(Path.GetTempPath(), "clustr-cache-budget-" + Guid.NewGuid().ToString("N"));
            try
            {
                var cache = new SnapshotCache(root,4000);
                cache.Write("manifest","old",true);
                cache.Write("scope",new string('s',1200),false);
                File.AppendAllText(cache.PathForKey("scope"),"tamper");
                Assert.That(cache.TryRead("scope",out _),Is.False);
                Assert.That(cache.UsedBytes,Is.LessThanOrEqualTo(4000));
                Assert.Throws<IOException>(() => cache.Write("manifest",new string('n',4000),true));
                Assert.That(new SnapshotCache(root,4000).TryRead("manifest",out var previous),Is.True);
                Assert.That(previous,Is.EqualTo("old"));
            }
            finally { if (Directory.Exists(root)) Directory.Delete(root,true); }
        }

        [Test]
        public void InterruptedCacheReplacementKeepsPreviousManifestReadable()
        {
            var root = Path.Combine(Path.GetTempPath(), "clustr-cache-interrupt-" + Guid.NewGuid().ToString("N"));
            try
            {
                var cache = new SnapshotCache(root);
                cache.Write("manifest", "old", true);
                var failing = new SnapshotCache(root, SnapshotCache.DefaultMaximumBytes,
                    () => throw new IOException("simulated interruption before index commit"));
                Assert.Throws<IOException>(() => failing.Write("manifest", "new", true));
                Assert.That(new SnapshotCache(root).TryRead("manifest", out var value), Is.True);
                Assert.That(value, Is.EqualTo("old"));
            }
            finally { if (Directory.Exists(root)) Directory.Delete(root,true); }
        }

        [Test]
        public void SnapshotCache_RejectsTamperingAndKeepsPinnedBaseEntries()
        {
            var root = Path.Combine(Path.GetTempPath(), "clustr-cache-test-" + Guid.NewGuid().ToString("N"));
            try
            {
                var cache = new SnapshotCache(root, 3000);
                cache.Write("manifest", "{\"revision_id\":9}", pinnedBase: true);
                cache.Write("old-system", new string('a', 1400), pinnedBase: false);
                cache.Write("new-system", new string('b', 1400), pinnedBase: false);

                Assert.That(cache.TryRead("manifest", out var manifestJson), Is.True);
                Assert.That(manifestJson, Does.Contain("revision_id"));
                Assert.That(cache.Contains("old-system"), Is.False);
                Assert.That(cache.UsedBytes,Is.LessThanOrEqualTo(3000));

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
        public void Store_KeepsRoutesWhoseEndpointArrivesInALaterResidentScene()
        {
            var manifest = CompatibleManifest(revision: 9, catalog: 3);
            var system = new SpatialScene
            {
                revision_id = 9, spatial_catalog_id = 3, scope = "system", root_id = "subreddit-y",
                nodes = new[] { new SpatialNode { id = "subreddit-y", type = "subreddit" } },
                links = new[] { new SpatialLink { source = "subreddit-y", target = "subreddit-b", type = "route" } }
            };
            var community = new SpatialScene
            {
                revision_id = 9, spatial_catalog_id = 3, scope = "community", root_id = "region-b",
                nodes = new[] { new SpatialNode { id = "subreddit-b", type = "subreddit" } }
            };
            var store = new SpatialSceneStore(manifest);

            store.MergeAll(new[] { system, community });

            Assert.That(store.LinkCount, Is.EqualTo(1));
            Assert.That(store.IsScopeLoaded("system", "subreddit-y"), Is.True);
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
            Assert.That(GalaxyMapSelection.IsSelectable(GalaxyNavigationLevel.Galaxy, GalaxyEntityKind.Community), Is.True);
            Assert.That(GalaxyMapSelection.IsSelectable(GalaxyNavigationLevel.Galaxy, GalaxyEntityKind.Post), Is.False);
            Assert.That(GalaxyMapSelection.IsSelectable(GalaxyNavigationLevel.Region, GalaxyEntityKind.Subreddit), Is.True);
            Assert.That(GalaxyMapSelection.IsSelectable(GalaxyNavigationLevel.System, GalaxyEntityKind.Post), Is.True);
            Assert.That(GalaxyMapSelection.IsSelectable(GalaxyNavigationLevel.Inspect, GalaxyEntityKind.Comment), Is.True);
        }

        [Test]
        public void MapSelection_PicksTheNearestVisibleCandidateWithinRadius()
        {
            var cursor = new Vector2(0.5f, 0.5f);
            var candidates = new[]
            {
                new GalaxyMapCandidate("far", GalaxyEntityKind.Community, new Vector2(0.6f, 0.5f), true),
                new GalaxyMapCandidate("near", GalaxyEntityKind.Community, new Vector2(0.52f, 0.5f), true),
                new GalaxyMapCandidate("behind", GalaxyEntityKind.Community, cursor, false)
            };

            Assert.That(GalaxyMapSelection.NearestId(
                GalaxyNavigationLevel.Galaxy, cursor, candidates, 0.15f), Is.EqualTo("near"));
            Assert.That(GalaxyMapSelection.NearestId(
                GalaxyNavigationLevel.Galaxy, Vector2.zero, candidates, 0.05f), Is.Empty);
        }

        [Test]
        public void MapSelection_ReturnsTheNearestCandidateForControllerPreview()
        {
            var candidates = new[]
            {
                new GalaxyMapCandidate("far", GalaxyEntityKind.Community, new Vector2(0.6f, 0.5f), true),
                new GalaxyMapCandidate("near", GalaxyEntityKind.Community, new Vector2(0.51f, 0.5f), true)
            };

            var selected = GalaxyMapSelection.Nearest(
                GalaxyNavigationLevel.Galaxy, new Vector2(0.5f, 0.5f), candidates, 0.12f);

            Assert.That(selected.HasValue, Is.True);
            Assert.That(selected.Value.StableId, Is.EqualTo("near"));
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
        public void Registry_ObsoleteObjectCallbacksCannotHideReplacement()
        {
            var oldObject = new GameObject("old registry object");
            var newObject = new GameObject("replacement registry object");
            try
            {
                var registry = new GalaxyEntityRegistry();
                var oldEntity = oldObject.AddComponent<GalaxyEntity>();
                oldEntity.Initialize("post:same", "Old", "post", 1, registry);
                var replacement = newObject.AddComponent<GalaxyEntity>();
                replacement.Initialize("post:same", "New", "post", 2, registry);
                registry.MarkInactive(oldEntity);
                registry.Unregister(oldEntity);
                Assert.That(registry.FindActiveEntity("post:same"), Is.SameAs(replacement));
                Assert.That(registry.ActiveEntities.Count, Is.EqualTo(1));
                newObject.SetActive(false);
                registry.Register(replacement);
                Assert.That(registry.FindActiveEntity("post:same"), Is.Null);
            }
            finally { UnityEngine.Object.DestroyImmediate(oldObject); UnityEngine.Object.DestroyImmediate(newObject); }
        }

        [Test]
        public void EntityPool_ReusesObjectsAndRemovesReleasedIdsFromRegistry()
        {
            var owner = new GameObject("pool owner");
            try
            {
                var registry = new GalaxyEntityRegistry();
                var pool = new GalaxyEntityPool(owner.transform);
                var first = pool.Acquire(PrimitiveType.Sphere, owner.transform);
                first.GetComponent<GalaxyEntity>().Initialize("post_old", "Old", "post planet", 1, registry);

                Assert.That(first.activeSelf, Is.False, "Acquisition must not expose an unvalidated entity.");
                first.SetActive(true);
                registry.MarkActive(first.GetComponent<GalaxyEntity>()); // EditMode does not dispatch MonoBehaviour callbacks.
                Assert.That(registry.FindActiveEntity("post_old"), Is.Not.Null);
                pool.Release(first);
                Assert.That(registry.FindActiveEntity("post_old"), Is.Null);

                var reused = pool.Acquire(PrimitiveType.Sphere, owner.transform);
                Assert.That(reused, Is.SameAs(first));
                reused.GetComponent<GalaxyEntity>().Initialize("post_new", "New", "post planet", 2, registry);
                Assert.That(reused.activeSelf, Is.False);
                reused.SetActive(true);
                registry.MarkActive(reused.GetComponent<GalaxyEntity>());
                Assert.That(registry.FindActiveEntity("post_new"), Is.Not.Null);
                reused.SetActive(false); // Survey-hidden residents must also return to the pool.
                pool.Release(reused);
                pool.Release(reused); // A duplicate release must not enqueue the same object twice.
                Assert.That(pool.Acquire(PrimitiveType.Sphere, owner.transform), Is.SameAs(reused));
                Assert.That(pool.Acquire(PrimitiveType.Sphere, owner.transform), Is.Not.SameAs(reused));
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

        [TestCase(16f / 9f)]
        [TestCase(1920f / 840f)]
        [TestCase(1f)]
        public void OverviewChart_FitsProjectedEdgesAndOffCenterClusters(float aspect)
        {
            var view = new GameObject("Overview framing regression", typeof(Camera));
            try
            {
                var camera = view.GetComponent<Camera>();
                camera.fieldOfView = 58f; camera.aspect = aspect; camera.farClipPlane = 250000f;
                var distance = GalacticLandmarkLayout.OverviewCameraDistance(camera.fieldOfView, aspect);
                var position = new Vector3(0, distance * .3f, -distance);
                view.transform.SetPositionAndRotation(position, Quaternion.LookRotation(-position, Vector3.up));
                var bounds = new SpatialBounds { min_x = 0, max_x = 300, min_y = 0, max_y = 300, min_z = 0, max_z = 300 };
                for (var x = 0; x <= 300; x += 30)
                for (var y = 0; y <= 300; y += 30)
                for (var z = 0; z <= 300; z += 300)
                {
                    var point = GalacticLandmarkLayout.Project(new SpatialNode { id = "edge", x = x, y = y, z = z }, bounds);
                    var screen = camera.WorldToViewportPoint(point);
                    Assert.That(screen.z, Is.GreaterThan(camera.nearClipPlane));
                    Assert.That(screen.x, Is.InRange(.01f, .99f), "Horizontal projection clipped at " + point);
                    Assert.That(screen.y, Is.InRange(.01f, .99f), "Vertical projection clipped at " + point);
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(view); }
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
        public void LaunchOptions_ParseABoundedBetaProfileDuration()
        {
            Assert.That(GalaxyLaunchOptions.IsBetaProfile(
                new[] { "ClustrGalaxy", "--clustr-beta-profile=600" }), Is.True);
            Assert.That(GalaxyLaunchOptions.ProfileDurationSeconds(
                new[] { "ClustrGalaxy", "--clustr-beta-profile=600" }), Is.EqualTo(600));
            Assert.That(GalaxyLaunchOptions.ProfileDurationSeconds(
                new[] { "ClustrGalaxy", "--clustr-beta-profile=2" }), Is.EqualTo(30));
            Assert.That(GalaxyLaunchOptions.ProfileDurationSeconds(
                new[] { "ClustrGalaxy", "--clustr-beta-profile=99999" }), Is.EqualTo(3600));
        }

        [Test]
        public void FixtureLaunch_RequiresExplicitLoopbackAndSeparateMode()
        {
            Assert.That(GalaxyLaunchOptions.FixtureApi(new[] { "--clustr-fixture-url=http://127.0.0.1:18765/api" }),
                Is.EqualTo("http://127.0.0.1:18765/api"));
            foreach (var endpoint in new[] { "https://clustr.subcult.tv/api", "http://localhost/api", "http://127.0.0.1/api?x=1", "http://user@127.0.0.1/api" })
                Assert.Throws<ArgumentException>(() => GalaxyLaunchOptions.FixtureApi(new[] { "--clustr-fixture-url=" + endpoint }));
            var offlineFixture = new[] { "--clustr-fixture-url=http://127.0.0.1/api", "--clustr-offline" };
            Assert.That(GalaxyLaunchOptions.FixtureApi(offlineFixture), Is.EqualTo("http://127.0.0.1/api"));
            Assert.That(GalaxyLaunchOptions.IsOfflineOnly(offlineFixture), Is.True);
            Assert.Throws<ArgumentException>(() => GalaxyLaunchOptions.FixtureApi(new[] { "--clustr-fixture-url=http://127.0.0.1/api", "--clustr-demo" }));
        }

        [TestCase("none")]
        [TestCase("missing-gpu")]
        [TestCase("short")]
        [TestCase("flight-mixed")]
        [TestCase("stall")]
        [TestCase("functional")]
        public void InteractionDiagnosticsKeepUiSeparateAndMissingCountersFail(string fault)
        {
            var report=new InteractionPerformanceReport{source_sha="working-source",build_id="artifact",width=2560,height=1440,
                sample_count=3600,valid_gpu_samples=3600,other_gameplay_samples=3600,measured_seconds=30,
                cpu_p95_ms=8,gpu_p95_ms=2,maximum_frame_ms=30,peak_gc_bytes=4096,peak_player_memory_bytes=1024,peak_gpu_memory_bytes=1024,
                cpu_available=true,gc_available=true,memory_available=true,gpu_available=true,gpu_memory_available=true,gpu_complete=true,functional_passed=true};
            switch(fault)
            {
                case "missing-gpu":report.valid_gpu_samples--;break;
                case "short":report.measured_seconds=10;break;
                case "flight-mixed":report.ordinary_flight_samples=1;report.other_gameplay_samples--;break;
                case "stall":report.maximum_frame_ms=51;break;
                case "functional":report.functional_passed=false;break;
            }
            Assert.That(report.Evaluate().Length==0,Is.EqualTo(fault=="none"));
        }

        [Test]
        public void LocalProfilesAreExplicitSafeAndBackwardCompatible()
        {
            Assert.That(GalaxyLaunchOptions.LocalProfileId(null), Is.EqualTo("offline-pilot"));
            Assert.That(GalaxyLaunchOptions.LocalProfileId(new[] { "--clustr-profile=second-pilot" }), Is.EqualTo("second-pilot"));
            foreach (var value in new[] { "", "../pilot", "pilot/name", "https://remote", new string('x', 65) })
                Assert.Throws<ArgumentException>(() => GalaxyLaunchOptions.LocalProfileId(new[] { "--clustr-profile=" + value }));
            Assert.Throws<ArgumentException>(() => GalaxyLaunchOptions.LocalProfileId(new[] { "--clustr-profile=a", "--clustr-profile=b" }));
        }

        [Test]
        public void BetaProfileVerdict_EnforcesEveryLinuxAcceptanceThreshold()
        {
            var passing = PassingProfile();

            Assert.That(GalaxyBetaProfile.Evaluate(passing).Qualified, Is.True);

            passing.p95_gpu_frame_ms = 18f;
            passing.p95_gc_allocated_bytes_per_frame = 64;
            passing.ordinary_peak_gc_bytes = 64;
            var failed = GalaxyBetaProfile.Evaluate(passing);
            Assert.That(failed.Qualified, Is.False);
            Assert.That(failed.Failures, Has.Some.Contains("GPU"));
            Assert.That(failed.Failures, Has.Some.Contains("GC"));
        }

        private static GalaxyBetaProfileReport PassingProfile() => new GalaxyBetaProfileReport
            {
                width = 2560, height = 1440, measured_seconds = 600, source_sha = "test-sha", build_id = "test-build",
                gpu_recorded_samples = 36000, gpu_drain_complete = true, timestamp_clock_compatible = true,
                gpu_cpu_timer_frequency = 1000, capture_start_timestamp = 1000, capture_end_timestamp = 601000,
                first_gpu_timestamp = 1001, last_gpu_timestamp = 600999,
                warm_cache_available = true, warm_cache_overview_seconds = 1, cache_usage_bytes = 1024,
                moving_flight_verified = true, measured_flight_distance = 1000,
                smoke_complete = true, sample_count = 36000, valid_gpu_samples = 36000,
                ordinary_flight_samples = 36000, ordinary_flight_seconds = 600, ordinary_maximum_frame_stall_ms = 40,
                cpu_counter_available = true, gc_counter_available = true, memory_counter_available = true,
                p95_cpu_frame_ms = 12.5f,
                p95_gpu_frame_ms = 15.5f,
                maximum_frame_stall_ms = 40f,
                peak_gc_allocated_bytes_per_frame = 0,
                p95_gc_allocated_bytes_per_frame = 0,
                peak_player_memory_bytes = 3L * 1024 * 1024 * 1024,
                peak_gpu_memory_bytes = 5L * 1024 * 1024 * 1024,
                gpu_timing_available = true,
                gpu_memory_available = true
            };

        [TestCase("missing-cpu")]
        [TestCase("missing-gpu")]
        [TestCase("missing-gc")]
        [TestCase("missing-memory")]
        [TestCase("invalid-counter")]
        [TestCase("insufficient-samples")]
        [TestCase("short-duration")]
        [TestCase("resolution")]
        [TestCase("smoke")]
        [TestCase("warm-cache")]
        [TestCase("slow-cache")]
        [TestCase("cache-cap")]
        [TestCase("cache-missing")]
        [TestCase("stationary")]
        [TestCase("overflow")]
        [TestCase("nonfinite")]
        [TestCase("negative-gc")]
        [TestCase("gpu-clock")]
        [TestCase("gpu-drain")]
        [TestCase("gpu-bounds")]
        [TestCase("gpu-duration")]
        public void QualificationCannotPassWithMissingOrInvalidEvidence(string fault)
        {
            var report = PassingProfile();
            switch (fault)
            {
                case "missing-cpu": report.cpu_counter_available = false; break;
                case "missing-gpu": report.valid_gpu_samples--; break;
                case "missing-gc": report.gc_counter_available = false; break;
                case "missing-memory": report.memory_counter_available = false; break;
                case "invalid-counter": report.invalid_measured_samples = 1; break;
                case "insufficient-samples": report.sample_count = 1; break;
                case "short-duration": report.measured_seconds = 30; break;
                case "resolution": report.width = 1920; break;
                case "smoke": report.smoke_complete = false; break;
                case "warm-cache": report.warm_cache_available = false; break;
                case "slow-cache": report.warm_cache_overview_seconds = 3.1f; break;
                case "cache-cap": report.cache_usage_bytes = 10L * 1024 * 1024 * 1024 + 1; break;
                case "cache-missing": report.cache_usage_bytes = -1; break;
                case "stationary": report.moving_flight_verified = false; break;
                case "overflow": report.sample_overflow = true; break;
                case "nonfinite": report.p95_cpu_frame_ms = float.NaN; break;
                case "gpu-clock": report.gpu_cpu_timer_frequency = 0; break;
                case "gpu-drain": report.gpu_drain_complete = false; break;
                case "gpu-bounds": report.last_gpu_timestamp = report.capture_end_timestamp; break;
                case "gpu-duration": report.gpu_cpu_timer_frequency = 2000; break;
                case "negative-gc": report.ordinary_peak_gc_bytes = -1; break;
            }
            Assert.That(GalaxyBetaProfile.Evaluate(report).Qualified, Is.False);
        }

        [TestCase("none")]
        [TestCase("initialization")]
        [TestCase("guidance")]
        [TestCase("ghost")]
        [TestCase("offscreen")]
        [TestCase("silent")]
        [TestCase("effects")]
        [TestCase("voice-cap")]
        [TestCase("effect-cap")]
        public void ActiveFeatureQualificationRequiresObservedWorkload(string fault)
        {
            var report = PassingProfile();
            report.active_features_requested = report.active_features_initialized = true;
            report.feature_frames = report.guidance_frames = report.ghost_frames = 36000;
            report.ghost_in_view_frames = report.audio_frames = report.celebration_frames = 10000;
            report.maximum_audio_voices = 8; report.maximum_celebrations = 3;
            switch (fault)
            {
                case "initialization": report.active_features_initialized = false; break;
                case "guidance": report.guidance_frames--; break;
                case "ghost": report.ghost_frames--; break;
                case "offscreen": report.ghost_in_view_frames = 0; break;
                case "silent": report.audio_frames = 0; break;
                case "effects": report.celebration_frames = 0; break;
                case "voice-cap": report.maximum_audio_voices = 9; break;
                case "effect-cap": report.maximum_celebrations = 4; break;
            }
            Assert.That(GalaxyBetaProfile.Evaluate(report).Qualified, Is.EqualTo(fault == "none"));
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
