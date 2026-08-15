using System.Collections;
using System.IO;
using System.Linq;
using ClustrGalaxy.Data;
using ClustrGalaxy.Flight;
using ClustrGalaxy.Gameplay;
using ClustrGalaxy.Interaction;
using ClustrGalaxy.Scanning;
using ClustrGalaxy.UI;
using ClustrGalaxy.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ClustrGalaxy.Tests
{
    public sealed class GalaxyNavigationPlayModeTests
    {
        [UnityTest]
        public IEnumerator ThreadInspection_ChangesTheHudModelWithoutCreatingReplyEntities()
        {
            var root = new GameObject("thread inspection test root");
            try
            {
                var targeting = root.AddComponent<ProbeTargeting>();
                var inspection = root.AddComponent<CommentThreadController>();
                inspection.Initialize(targeting);
                var parent = CreateSurveyEntity(root.transform, "comment:parent", "discussion satellite",
                    "subreddit:technology");
                parent.gameObject.AddComponent<CommentThreadNode>().Initialize(2);
                var before = GalaxyEntity.ActiveEntities.Count();

                targeting.SelectTarget(parent);
                inspection.PresentThread(new SpatialScene
                {
                    scope = "thread",
                    root_id = parent.StableId,
                    nodes = new[]
                    {
                        new SpatialNode { id = parent.StableId, type = "comment" },
                        new SpatialNode
                        {
                            id = "comment:reply-a", type = "comment", parent_id = parent.StableId,
                            author_name = "a", metrics = new SpatialNodeMetrics { score = 3, reply_count = 0 }
                        },
                        new SpatialNode
                        {
                            id = "comment:reply-b", type = "comment", parent_id = parent.StableId,
                            author_name = "b", metrics = new SpatialNodeMetrics { score = 2, reply_count = 0 }
                        }
                    }
                });
                yield return null;

                Assert.That(inspection.DisplayedModel, Is.Not.Null);
                Assert.That(inspection.DisplayedModel.NodeCount, Is.EqualTo(2));
                Assert.That(GalaxyEntity.ActiveEntities.Count(), Is.EqualTo(before));

                targeting.SelectTarget(null);
                Assert.That(inspection.DisplayedModel, Is.Null);
            }
            finally
            {
                Object.Destroy(root);
            }
        }

        [UnityTest]
        public IEnumerator SurveyStart_HidesEverythingOutsideTheStartingSystem()
        {
            var root = new GameObject("survey visibility test root");
            var storageRoot = Path.Combine(Path.GetTempPath(), "clustr-survey-playmode-" + System.Guid.NewGuid().ToString("N"));
            try
            {
                var modes = new GalaxyModeState();
                var store = SurveyDiscoveryStore.Open(storageRoot, "test-pilot", SurveySnapshotKey.Pinned(9, 3),
                    "subreddit:technology");
                var controller = root.AddComponent<GalaxySurveyVisibilityController>();
                controller.Initialize(modes, store);

                var technology = CreateSurveyEntity(root.transform, "subreddit:technology", "subreddit star",
                    "subreddit:technology");
                var localPost = CreateSurveyEntity(root.transform, "post:open-source-ai", "post planet",
                    "subreddit:technology");
                var localUser = CreateSurveyEntity(root.transform, "user:traveler-00", "user ship",
                    "subreddit:technology");
                var localComment = CreateSurveyEntity(root.transform, "comment:open-source-ai-1", "discussion satellite",
                    "subreddit:technology");
                var localReply = CreateSurveyEntity(root.transform, "comment:open-source-ai-1-reply-1", "reply depth 1",
                    "subreddit:technology");
                var remote = CreateSurveyEntity(root.transform, "subreddit:remote", "subreddit star",
                    "subreddit:remote");

                modes.SetWorldMode(GalaxyWorldMode.Survey);
                Assert.That(controller.StartAt(technology.StableId), Is.True);
                yield return null;

                Assert.That(GalaxyEntity.TryGet(technology.StableId, out _), Is.True);
                Assert.That(GalaxyEntity.TryGet(localPost.StableId, out _), Is.True);
                Assert.That(store.IsEntityKnown(localUser.StableId), Is.False);
                Assert.That(store.IsEntityKnown(localComment.StableId), Is.False);
                Assert.That(store.IsEntityKnown(localReply.StableId), Is.False);
                Assert.That(localUser.gameObject.activeSelf, Is.False);
                Assert.That(localComment.gameObject.activeSelf, Is.False);
                Assert.That(localReply.gameObject.activeSelf, Is.False);
                Assert.That(remote.gameObject.activeSelf, Is.False);
                Assert.That(GalaxyEntity.TryGet(remote.StableId, out _), Is.False);
                Assert.That(store.IsEntityKnown(remote.StableId), Is.False);
                Assert.That(File.Exists(store.StoragePath), Is.True);

                modes.SetWorldMode(GalaxyWorldMode.Explore);
                yield return null;
                Assert.That(remote.gameObject.activeSelf, Is.True);
                Assert.That(store.IsEntityKnown(remote.StableId), Is.False);

                modes.SetWorldMode(GalaxyWorldMode.Survey);
                yield return null;
                Assert.That(remote.gameObject.activeSelf, Is.False);
                Assert.That(store.IsEntityKnown(remote.StableId), Is.False);
            }
            finally
            {
                Object.Destroy(root);
                if (Directory.Exists(storageRoot)) Directory.Delete(storageRoot, recursive: true);
            }
        }

        [UnityTest]
        public IEnumerator Scan_RevealsConnectedRouteAndDestination()
        {
            var root = new GameObject("signal scanner test root");
            var storageRoot = Path.Combine(Path.GetTempPath(), "clustr-scan-playmode-" + System.Guid.NewGuid().ToString("N"));
            try
            {
                var modes = new GalaxyModeState();
                var store = SurveyDiscoveryStore.Open(storageRoot, "test-pilot", SurveySnapshotKey.Pinned(9, 3),
                    "subreddit:technology");
                var visibility = root.AddComponent<GalaxySurveyVisibilityController>();
                visibility.Initialize(modes, store);
                var source = CreateSurveyEntity(root.transform, "subreddit:technology", "subreddit star", "subreddit:technology");
                var remote = CreateSurveyEntity(root.transform, "subreddit:signal-horizon", "subreddit star", "subreddit:signal-horizon");
                var routeRoot = new GameObject("route");
                routeRoot.transform.SetParent(root.transform, false);
                var route = routeRoot.AddComponent<GalaxySurveyRoute>();
                route.Initialize("route:technology-signal-horizon", source.StableId, remote.StableId);
                var targeting = root.AddComponent<ProbeTargeting>();
                visibility.BindTargeting(targeting);
                modes.SetWorldMode(GalaxyWorldMode.Survey);
                Assert.That(visibility.StartAt(source.StableId), Is.True);
                yield return null;
                Assert.That(GalaxyEntity.TryGet(remote.StableId, out _), Is.False);

                var events = new SurveyEventBus();
                var eventCount = 0;
                events.Published += _ => eventCount++;
                var scanner = root.AddComponent<SignalScanner>();
                scanner.Initialize(modes, visibility, targeting, events, new[]
                {
                    new ScanRoute(route.StableId, source.StableId, remote.StableId, 8f)
                });
                targeting.SelectTarget(source);
                scanner.BeginScan();
                scanner.Tick(SignalScanner.RequiredHoldSeconds);
                yield return null;

                Assert.That(store.IsEntityKnown(remote.StableId), Is.True);
                Assert.That(store.IsEntityIdentified(remote.StableId), Is.False);
                Assert.That(GalaxyEntity.TryGet(remote.StableId, out _), Is.True);
                Assert.That(visibility.CanLabel(remote), Is.False);
                Assert.That(routeRoot.activeSelf, Is.True);
                Assert.That(eventCount, Is.EqualTo(1));

                scanner.BeginScan();
                scanner.Tick(SignalScanner.RequiredHoldSeconds);
                Assert.That(scanner.LastResult.AlreadyScanned, Is.True);
                Assert.That(eventCount, Is.EqualTo(1));
            }
            finally
            {
                Object.Destroy(root);
                if (Directory.Exists(storageRoot)) Directory.Delete(storageRoot, recursive: true);
            }
        }

        [UnityTest]
        public IEnumerator ModeSwitch_PreservesIndependentSurveyProgress()
        {
            var probe = new GameObject("mode-aware travel probe");
            var world = new GameObject("mode-aware travel world");
            var storageRoot = Path.Combine(Path.GetTempPath(), "clustr-travel-playmode-" + System.Guid.NewGuid().ToString("N"));
            try
            {
                var body = probe.AddComponent<Rigidbody>();
                var flight = probe.AddComponent<ObserverProbeController>();
                var modes = new GalaxyModeState();
                flight.Initialize(modes);
                var targeting = probe.AddComponent<ProbeTargeting>();
                var visibility = probe.AddComponent<GalaxySurveyVisibilityController>();
                var store = SurveyDiscoveryStore.Open(storageRoot, "test-pilot", SurveySnapshotKey.Pinned(9, 3),
                    "subreddit:technology");
                visibility.Initialize(modes, store);
                visibility.BindTargeting(targeting);
                var technology = CreateSurveyEntity(world.transform, "subreddit:technology", "subreddit star",
                    "subreddit:technology");
                var remote = CreateSurveyEntity(world.transform, "subreddit:remote", "subreddit star", "subreddit:remote");
                var comment = CreateSurveyEntity(world.transform, "comment:remote", "discussion satellite", "subreddit:remote");
                var cameraObject = new GameObject("mode-aware travel camera");
                cameraObject.transform.SetParent(probe.transform, false);
                var navigation = probe.AddComponent<GalaxyNavigationController>();
                navigation.Initialize(targeting, cameraObject.AddComponent<Camera>());
                navigation.BindDiscoveryVisibility(visibility);

                modes.SetWorldMode(GalaxyWorldMode.Survey);
                Assert.That(visibility.StartAt(technology.StableId), Is.True);
                yield return null;
                Assert.That(navigation.CanTravelTo(technology), Is.True);
                Assert.That(navigation.CanTravelTo(remote), Is.False);
                Assert.That(navigation.CanTravelTo(comment), Is.False);
                Assert.That(navigation.PlotAutopilot(remote), Is.False);

                modes.SetWorldMode(GalaxyWorldMode.Explore);
                yield return null;
                Assert.That(navigation.CanTravelTo(remote), Is.True);
                Assert.That(navigation.PlotAutopilot(remote), Is.True);

                // Place the probe on the deterministic approach anchor; this
                // verifies that an actual completed auto approach does not
                // lock out a freely chosen Mastery mode.
                body.position = GalaxyTravelRules.ArrivalAnchor(remote.transform.position,
                    remote.transform.position + Vector3.forward * 100f,
                    GalaxyTravelRules.ArrivalRadius(1f, 0d));
                body.linearVelocity = Vector3.zero;
                yield return new WaitForFixedUpdate();
                Assert.That(navigation.HasAutopilotArrival, Is.True);
                modes.SetFlightMode(FlightControlMode.PilotMastery);
                Assert.That(modes.FlightMode, Is.EqualTo(FlightControlMode.PilotMastery));

                modes.SetWorldMode(GalaxyWorldMode.Survey);
                yield return null;
                Assert.That(store.IsEntityKnown(remote.StableId), Is.False);
                Assert.That(remote.gameObject.activeSelf, Is.False);

                navigation.NavigateTo(GalaxyNavigationLevel.Region, technology.StableId);
                navigation.NavigateTo(GalaxyNavigationLevel.System, technology.StableId);
                navigation.NavigateBack();
                Assert.That(navigation.Level, Is.EqualTo(GalaxyNavigationLevel.Region));
            }
            finally
            {
                Object.Destroy(probe);
                Object.Destroy(world);
                if (Directory.Exists(storageRoot)) Directory.Delete(storageRoot, recursive: true);
            }
        }

        [UnityTest]
        public IEnumerator FastTravel_FromOpenMapClearsStoredMomentumAndMaintainsSafeArrival()
        {
            var probe = new GameObject("fast travel map probe");
            var world = new GameObject("fast travel map world");
            try
            {
                var body = probe.AddComponent<Rigidbody>();
                var flight = probe.AddComponent<ObserverProbeController>();
                flight.Initialize(new GalaxyModeState());
                var targeting = probe.AddComponent<ProbeTargeting>();
                var target = CreateSurveyEntity(world.transform, "subreddit:destination", "subreddit star",
                    "subreddit:destination");
                target.transform.position = new Vector3(0f, 0f, 240f);
                var cameraObject = new GameObject("fast travel map camera");
                cameraObject.transform.SetParent(probe.transform, false);
                var navigation = probe.AddComponent<GalaxyNavigationController>();
                navigation.Initialize(targeting, cameraObject.AddComponent<Camera>());

                body.linearVelocity = new Vector3(35f, -2f, 11f);
                body.angularVelocity = new Vector3(0.4f, 0.8f, -0.6f);
                navigation.SetMapOpen(true);
                Assert.That(body.isKinematic, Is.True);
                Assert.That(navigation.TryFastTravel(target), Is.True);
                yield return new WaitForFixedUpdate();

                navigation.SetMapOpen(false);
                Assert.That(body.isKinematic, Is.False);
                Assert.That(body.linearVelocity, Is.EqualTo(Vector3.zero));
                Assert.That(body.angularVelocity, Is.EqualTo(Vector3.zero));
                var collider = target.GetComponent<Collider>();
                var minimumDistance = GalaxyTravelRules.ArrivalRadius(collider.bounds.extents.magnitude, 0d);
                Assert.That(Vector3.Distance(body.position, target.transform.position),
                    Is.GreaterThanOrEqualTo(minimumDistance - 0.001f));
            }
            finally
            {
                Object.Destroy(probe);
                Object.Destroy(world);
            }
        }

        [UnityTest]
        public IEnumerator HierarchicalBack_RestoresTheExactRuntimeProbePose()
        {
            var probe = new GameObject("navigation test probe");
            var cameraObject = new GameObject("navigation test camera");
            try
            {
                var body = probe.AddComponent<Rigidbody>();
                probe.AddComponent<ObserverProbeController>();
                var targeting = probe.AddComponent<ProbeTargeting>();
                cameraObject.transform.SetParent(probe.transform, false);
                var camera = cameraObject.AddComponent<Camera>();
                var navigation = probe.AddComponent<GalaxyNavigationController>();
                navigation.Initialize(targeting, camera);

                body.position = new Vector3(10f, 20f, 30f);
                body.rotation = Quaternion.Euler(0f, 15f, 0f);
                navigation.NavigateTo(GalaxyNavigationLevel.Region, "community_1");
                body.position = new Vector3(110f, 120f, 130f);
                body.rotation = Quaternion.Euler(0f, 45f, 0f);
                navigation.NavigateTo(GalaxyNavigationLevel.System, "subreddit_2");

                navigation.NavigateBack();
                yield return new WaitForFixedUpdate();

                Assert.That(navigation.Level, Is.EqualTo(GalaxyNavigationLevel.Region));
                Assert.That(body.position, Is.EqualTo(new Vector3(110f, 120f, 130f)));
                Assert.That(Quaternion.Angle(body.rotation, Quaternion.Euler(0f, 45f, 0f)), Is.LessThan(0.001f));
            }
            finally
            {
                Object.Destroy(probe);
            }
        }

        [UnityTest]
        public IEnumerator PooledEntity_DoesNotRemainTargetableAfterRelease()
        {
            var owner = new GameObject("pool play mode owner");
            try
            {
                var pool = new GalaxyEntityPool(owner.transform);
                var body = pool.Acquire(PrimitiveType.Sphere, owner.transform);
                body.GetComponent<GalaxyEntity>().Initialize("comment_1", "Comment", "comment", 1);
                Assert.That(GalaxyEntity.TryGet("comment_1", out _), Is.True);

                pool.Release(body);
                yield return null;

                Assert.That(GalaxyEntity.TryGet("comment_1", out _), Is.False);
            }
            finally
            {
                Object.Destroy(owner);
            }
        }

        [UnityTest]
        public IEnumerator SurveyStart_RejectsExistingProfileWithDifferentStartingSystemWithoutMutation()
        {
            var root = new GameObject("survey mismatch test root");
            var storageRoot = Path.Combine(Path.GetTempPath(), "clustr-survey-mismatch-" + System.Guid.NewGuid().ToString("N"));
            try
            {
                var key = SurveySnapshotKey.Pinned(9, 3);
                var existing = SurveyDiscoveryStore.Open(storageRoot, "test-pilot", key, "subreddit:remote");
                existing.DiscoverEntity("post:remote-only");
                existing.Save();
                var before = File.ReadAllText(existing.StoragePath);

                var controller = root.AddComponent<GalaxySurveyVisibilityController>();
                controller.Initialize(new GalaxyModeState(), key, "test-pilot", storageRoot);
                CreateSurveyEntity(root.transform, "subreddit:technology", "subreddit star", "subreddit:technology");

                Assert.That(controller.StartAt("subreddit:technology"), Is.False);
                Assert.That(File.ReadAllText(existing.StoragePath), Is.EqualTo(before));
                Assert.That(controller.DiscoveryStore, Is.Null);
                yield return null;
            }
            finally
            {
                Object.Destroy(root);
                if (Directory.Exists(storageRoot)) Directory.Delete(storageRoot, recursive: true);
            }
        }

        [UnityTest]
        public IEnumerator SurveyMapCommand_EntersSurveyWithoutChoosingAStartingSystem()
        {
            var probe = new GameObject("survey map command probe");
            var cameraObject = new GameObject("survey map command camera");
            try
            {
                probe.AddComponent<Rigidbody>();
                var flight = probe.AddComponent<ObserverProbeController>();
                var modes = new GalaxyModeState();
                flight.Initialize(modes);
                var targeting = probe.AddComponent<ProbeTargeting>();
                var visibility = probe.AddComponent<GalaxySurveyVisibilityController>();
                visibility.Initialize(modes, SurveySnapshotKey.Demo, "test-pilot", Path.GetTempPath());
                visibility.BindTargeting(targeting);
                cameraObject.transform.SetParent(probe.transform, false);
                var camera = cameraObject.AddComponent<Camera>();
                var navigation = probe.AddComponent<GalaxyNavigationController>();
                navigation.Initialize(targeting, camera);
                navigation.BindDiscoveryVisibility(visibility);

                Assert.That(navigation.EnterSurvey(), Is.True);
                Assert.That(modes.WorldMode, Is.EqualTo(GalaxyWorldMode.Survey));
                Assert.That(navigation.IsMapOpen, Is.True);
                Assert.That(visibility.RequiresStartingSystemChoice, Is.True);
                yield return null;
            }
            finally
            {
                Object.Destroy(probe);
                Object.Destroy(cameraObject);
            }
        }

        [UnityTest]
        public IEnumerator GalaxyMapCursor_PreviewsACommunityWithoutSelectingIt()
        {
            var probe = new GameObject("map preview probe");
            var cameraObject = new GameObject("map preview camera");
            var landmark = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            try
            {
                probe.transform.position = new Vector3(0f, 0f, -10f);
                probe.AddComponent<Rigidbody>();
                var flight = probe.AddComponent<ObserverProbeController>();
                flight.Initialize(new GalaxyModeState());
                var targeting = probe.AddComponent<ProbeTargeting>();
                cameraObject.transform.SetParent(probe.transform, false);
                cameraObject.transform.localPosition = Vector3.zero;
                var camera = cameraObject.AddComponent<Camera>();
                landmark.transform.position = Vector3.zero;
                var entity = landmark.AddComponent<GalaxyEntity>();
                entity.Initialize("community-a", "Example Region", "community", 10);
                landmark.AddComponent<LiveEntityMetadata>().Initialize(new SpatialNode
                {
                    id = "community-a", name = "Example Region", type = "community"
                });
                var navigation = probe.AddComponent<GalaxyNavigationController>();
                navigation.Initialize(targeting, camera);
                navigation.SetMapOpen(true);

                yield return null;
                yield return null;

                Assert.That(navigation.MapHoveredStableId, Is.EqualTo("community-a"));
                Assert.That(navigation.MapHoverStatus, Is.EqualTo("Example Region  •  COMMUNITY"));
                Assert.That(targeting.Target, Is.Null);
            }
            finally
            {
                Object.Destroy(probe);
                Object.Destroy(cameraObject);
                Object.Destroy(landmark);
            }
        }

        private static GalaxyEntity CreateSurveyEntity(Transform parent, string stableId, string entityType,
            string systemStableId)
        {
            var body = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            body.transform.SetParent(parent, false);
            var entity = body.AddComponent<GalaxyEntity>();
            entity.Initialize(stableId, stableId, entityType, 1);
            body.AddComponent<GalaxySurveyMembership>().Initialize(systemStableId);
            return entity;
        }
    }
}
