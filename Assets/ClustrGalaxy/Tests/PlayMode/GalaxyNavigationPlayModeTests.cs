using System.Collections;
using System.IO;
using System.Linq;
using ClustrGalaxy.Core.Domain;
using ClustrGalaxy.Core.Survey;
using ClustrGalaxy.Core.Persistence;
using ClustrGalaxy.Persistence;
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
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.UI;

namespace ClustrGalaxy.Tests
{
    public sealed class GalaxyNavigationPlayModeTests
    {
        private GalaxyEntityRegistry registry;
        [SetUp] public void SetupRegistry() => registry = new GalaxyEntityRegistry();
        private bool TryGetActive(string id, out GalaxyEntity entity)
        { entity = registry.FindActiveEntity(id); return entity != null; }

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
                var before = registry.ActiveEntities.Count();

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
                Assert.That(registry.ActiveEntities.Count(), Is.EqualTo(before));

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
                var controller = root.AddComponent<GalaxySurveyVisibilityController>();
                controller.BindRegistry(registry);
                controller.Initialize(modes, SurveySnapshotKey.Pinned(9, 3), "test-pilot", storageRoot);

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

                Assert.That(TryGetActive(technology.StableId, out _), Is.True);
                Assert.That(TryGetActive(localPost.StableId, out _), Is.True);
                Assert.That(controller.SurveySession.State.IsKnown((GalaxyEntityId)localUser.StableId), Is.False);
                Assert.That(controller.SurveySession.State.IsKnown((GalaxyEntityId)localComment.StableId), Is.False);
                Assert.That(controller.SurveySession.State.IsKnown((GalaxyEntityId)localReply.StableId), Is.False);
                Assert.That(localUser.gameObject.activeSelf, Is.False);
                Assert.That(localComment.gameObject.activeSelf, Is.False);
                Assert.That(localReply.gameObject.activeSelf, Is.False);
                Assert.That(remote.gameObject.activeSelf, Is.False);
                Assert.That(TryGetActive(remote.StableId, out _), Is.False);
                Assert.That(controller.SurveySession.State.IsKnown((GalaxyEntityId)remote.StableId), Is.False);
                Assert.That(File.Exists(Path.Combine(storageRoot, ProfilePaths.Survey("test-pilot", "pinned", 9, 3))), Is.True);

                modes.SetWorldMode(GalaxyWorldMode.Explore);
                yield return null;
                Assert.That(remote.gameObject.activeSelf, Is.True);
                Assert.That(controller.SurveySession.State.IsKnown((GalaxyEntityId)remote.StableId), Is.False);

                modes.SetWorldMode(GalaxyWorldMode.Survey);
                yield return null;
                Assert.That(remote.gameObject.activeSelf, Is.False);
                Assert.That(controller.SurveySession.State.IsKnown((GalaxyEntityId)remote.StableId), Is.False);
            }
            finally
            {
                Object.Destroy(root);
                if (Directory.Exists(storageRoot)) Directory.Delete(storageRoot, recursive: true);
            }
        }

        [UnityTest]
        public IEnumerator StudioNavigationReservesEditingStickAndRestoresPreviousBindings()
        {
            var root = new GameObject("studio input test",typeof(EventSystem),typeof(InputSystemUIInputModule));
            var pad = InputSystem.AddDevice<Gamepad>();
            var background = InputSystem.settings.backgroundBehavior;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            try
            {
                var module = root.GetComponent<InputSystemUIInputModule>(); var previous = module.move;
                using (new StudioNavigationScope(module))
                {
                    InputSystem.QueueStateEvent(pad,new GamepadState { rightStick = Vector2.right }); yield return null;
                    Assert.That(module.move.action.ReadValue<Vector2>(), Is.EqualTo(Vector2.zero));
                    InputSystem.QueueStateEvent(pad,new GamepadState().WithButton(GamepadButton.DpadRight)); yield return null;
                    Assert.That(module.move.action.ReadValue<Vector2>().x, Is.EqualTo(1));
                }
                Assert.That(module.move, Is.SameAs(previous));
            }
            finally { InputSystem.RemoveDevice(pad); InputSystem.settings.backgroundBehavior = background; Object.Destroy(root); }
        }

        [UnityTest]
        public IEnumerator ExploreTuningIsPracticeAndChangingTargetCancelsCharge()
        {
            var root = new GameObject("explore tuning test");
            try
            {
                var modes = new GalaxyModeState();
                var visibility = root.AddComponent<GalaxySurveyVisibilityController>();
                visibility.BindRegistry(registry); visibility.Initialize(modes, SurveySnapshotKey.Demo, "practice-pilot", Path.GetTempPath());
                var first = CreateSurveyEntity(root.transform, "post:first", "post", "subreddit:technology");
                var second = CreateSurveyEntity(root.transform, "post:second", "post", "subreddit:technology");
                first.transform.localPosition = Vector3.forward * 10; second.transform.localPosition = Vector3.forward * 20;
                var targeting = root.AddComponent<ProbeTargeting>(); targeting.Initialize(visibility);
                var scanner = root.AddComponent<SignalScanner>(); scanner.Initialize(modes, visibility, targeting, null);
                scanner.BindRegistry(registry); scanner.AutomaticTuning = true;
                var completions = 0;
                scanner.ScanFinished += (_, clean, practice) => { Assert.That(clean, Is.True); Assert.That(practice, Is.True); completions++; };
                targeting.SelectTarget(first); scanner.BeginScan(); scanner.Tick(.75f);
                targeting.SelectTarget(second); scanner.Tick(.9f);
                Assert.That(scanner.State, Is.EqualTo(SignalScanState.NoSignal)); Assert.That(completions, Is.Zero);
                scanner.BeginScan(); scanner.Tick(.75f); Assert.That(completions, Is.Zero); scanner.Tick(.75f);
                Assert.That(completions, Is.EqualTo(1)); Assert.That(visibility.SurveySession, Is.Null);
                yield return null;
            }
            finally { Object.Destroy(root); }
        }

        [UnityTest]
        public IEnumerator Scan_RevealsConnectedRouteAndDestination()
        {
            var root = new GameObject("signal scanner test root");
            var storageRoot = Path.Combine(Path.GetTempPath(), "clustr-scan-playmode-" + System.Guid.NewGuid().ToString("N"));
            try
            {
                var modes = new GalaxyModeState();
                var visibility = root.AddComponent<GalaxySurveyVisibilityController>();
                visibility.BindRegistry(registry);
                visibility.Initialize(modes, SurveySnapshotKey.Pinned(9, 3), "test-pilot", storageRoot);
                var source = CreateSurveyEntity(root.transform, "subreddit:technology", "subreddit star", "subreddit:technology");
                var remote = CreateSurveyEntity(root.transform, "subreddit:signal-horizon", "subreddit star", "subreddit:signal-horizon");
                var routeRoot = new GameObject("route");
                routeRoot.transform.SetParent(root.transform, false);
                var route = routeRoot.AddComponent<GalaxySurveyRoute>();
                route.Initialize("route:technology-signal-horizon", source.StableId, remote.StableId, registry);
                var targeting = root.AddComponent<ProbeTargeting>();
                visibility.BindTargeting(targeting);
                modes.SetWorldMode(GalaxyWorldMode.Survey);
                Assert.That(visibility.StartAt(source.StableId), Is.True);
                yield return null;
                Assert.That(TryGetActive(remote.StableId, out _), Is.False);

                var eventCount = 0;
                visibility.SurveyTransitionCommitted += _ => eventCount++;
                var scanner = root.AddComponent<SignalScanner>();
                scanner.Initialize(modes, visibility, targeting, new[]
                {
                    new ScanRoute(route.StableId, source.StableId, remote.StableId, 8f)
                });
                scanner.BindRegistry(registry);
                targeting.SelectTarget(source);
                scanner.BeginScan();
                scanner.Tick(SignalScanner.RequiredHoldSeconds);
                yield return null;

                Assert.That(visibility.SurveySession.State.IsKnown((GalaxyEntityId)remote.StableId), Is.True);
                Assert.That(visibility.SurveySession.State.IsIdentified((GalaxyEntityId)remote.StableId), Is.False);
                Assert.That(TryGetActive(remote.StableId, out _), Is.True);
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
                visibility.BindRegistry(registry);
                visibility.Initialize(modes, SurveySnapshotKey.Pinned(9, 3), "test-pilot", storageRoot);
                visibility.BindTargeting(targeting);
                var technology = CreateSurveyEntity(world.transform, "subreddit:technology", "subreddit star",
                    "subreddit:technology");
                var remote = CreateSurveyEntity(world.transform, "subreddit:remote", "subreddit star", "subreddit:remote");
                var comment = CreateSurveyEntity(world.transform, "comment:remote", "discussion satellite", "subreddit:remote");
                var cameraObject = new GameObject("mode-aware travel camera");
                cameraObject.transform.SetParent(probe.transform, false);
                var navigation = probe.AddComponent<GalaxyNavigationController>();
                navigation.BindRegistry(registry);
                navigation.Initialize(targeting, cameraObject.AddComponent<Camera>());
                navigation.BindDiscoveryVisibility(visibility);

                modes.SetWorldMode(GalaxyWorldMode.Survey);
                Assert.That(visibility.StartAt(technology.StableId), Is.True);
                yield return null;
                Assert.That(navigation.CanTravelTo(technology), Is.False); // No authoritative jump session in this visibility fixture.
                Assert.That(navigation.CanApproach(technology), Is.True);
                Assert.That(navigation.CanTravelTo(remote), Is.False);
                Assert.That(navigation.CanTravelTo(comment), Is.False);
                Assert.That(navigation.PlotAutopilot(remote), Is.False);

                modes.SetWorldMode(GalaxyWorldMode.Explore);
                yield return null;
                Assert.That(navigation.CanTravelTo(remote), Is.False);
                Assert.That(navigation.CanApproach(remote), Is.True);
                Assert.That(navigation.PlotAutopilot(remote), Is.True);

                // Place the probe on the deterministic approach anchor; this
                // verifies that an actual completed auto approach does not
                // lock out a freely chosen Mastery mode.
                body.position = GalaxyTravelRules.ArrivalAnchor(remote.transform.position,
                    remote.transform.position + Vector3.forward * 100f,
                    GalaxyTravelRules.ArrivalRadius(remote.SurfaceRadius + (float)ClustrGalaxy.Generation.CelestialLayout.StellarClearance, 0d));
                body.linearVelocity = Vector3.zero;
                yield return new WaitForFixedUpdate();
                Assert.That(navigation.HasAutopilotArrival, Is.True);
                modes.SetFlightMode(FlightControlMode.PilotMastery);
                Assert.That(modes.FlightMode, Is.EqualTo(FlightControlMode.PilotMastery));

                modes.SetWorldMode(GalaxyWorldMode.Survey);
                yield return null;
                Assert.That(visibility.SurveySession.State.IsKnown((GalaxyEntityId)remote.StableId), Is.False);
                Assert.That(remote.gameObject.activeSelf, Is.False);

                var region = new GameObject("community"); region.transform.SetParent(world.transform, false);
                region.AddComponent<GalaxyEntity>().Initialize("community:test", "region", "community", 1, registry);
                modes.SetWorldMode(GalaxyWorldMode.Explore);
                navigation.NavigateTo(GalaxyNavigationLevel.Region, "community:test");
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
        public IEnumerator FastTravelWithoutAuthoritativeSessionCannotTeleportOrClearStoredMomentum()
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
                navigation.BindRegistry(registry);
                navigation.Initialize(targeting, cameraObject.AddComponent<Camera>());

                body.linearVelocity = new Vector3(35f, -2f, 11f);
                body.angularVelocity = new Vector3(0.4f, 0.8f, -0.6f);
                navigation.SetMapOpen(true);
                Assert.That(body.isKinematic, Is.False, "Chart inspection must leave local physics running");
                flight.ModeState.SetFlightMode(FlightControlMode.PilotMastery);
                Assert.That(navigation.TryFastTravel(target), Is.False);
                yield return new WaitForFixedUpdate();

                navigation.SetMapOpen(false);
                Assert.That(body.isKinematic, Is.False);
                Assert.That(body.linearVelocity, Is.EqualTo(new Vector3(35f,-2f,11f)));
                Assert.That(body.angularVelocity, Is.EqualTo(new Vector3(.4f,.8f,-.6f)));
                Assert.That(body.position.magnitude, Is.LessThan(5f), "Rejected travel cannot teleport to the destination");
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
                probe.AddComponent<ObserverProbeController>().Initialize(new GalaxyModeState());
                var targeting = probe.AddComponent<ProbeTargeting>();
                cameraObject.transform.SetParent(probe.transform, false);
                var camera = cameraObject.AddComponent<Camera>();
                var navigation = probe.AddComponent<GalaxyNavigationController>();
                navigation.BindRegistry(registry);
                navigation.Initialize(targeting, camera);

                var region = new GameObject("region"); region.transform.SetParent(probe.transform, false);
                region.AddComponent<GalaxyEntity>().Initialize("community_1", "region", "community", 1, registry);
                var system = new GameObject("system"); system.transform.SetParent(probe.transform, false);
                system.AddComponent<GalaxyEntity>().Initialize("subreddit_2", "system", "subreddit", 1, registry);
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
        public IEnumerator PooledEntity_SameFrameReacquireDoesNotKeepTheDyingVisual()
        {
            var owner = new GameObject("pool visual owner");
            try
            {
                var pool = new GalaxyEntityPool(owner.transform);
                var body = pool.Acquire(PrimitiveType.Capsule, owner.transform);
                body.GetComponent<GalaxyEntity>().Initialize("user_1", "Pilot", "user", 1, registry);
                new GameObject("Catalog Visual").transform.SetParent(body.transform, false);

                pool.Release(body);
                var reused = pool.Acquire(PrimitiveType.Capsule, owner.transform);

                Assert.That(reused, Is.SameAs(body));
                Assert.That(reused.transform.Find("Catalog Visual"), Is.Null);
                yield return null;
            }
            finally
            {
                Object.Destroy(owner);
            }
        }

        [UnityTest]
        public IEnumerator Registry_ReactivatingKnownEntitiesKeepsTheSortedView()
        {
            var owner = new GameObject("registry view owner");
            try
            {
                for (var index = 0; index < 50; index++)
                {
                    var child = new GameObject("entity " + index);
                    child.transform.SetParent(owner.transform, false);
                    child.AddComponent<GalaxyEntity>().Initialize("post_" + index.ToString("D3"), "Post", "post", 1, registry);
                }
                yield return null;
                var view = registry.Entities;
                foreach (var entity in owner.GetComponentsInChildren<GalaxyEntity>())
                {
                    entity.gameObject.SetActive(false);
                    entity.gameObject.SetActive(true);
                }
                Assert.That(registry.Entities, Is.SameAs(view));
                Assert.That(registry.ActiveEntities.Count, Is.EqualTo(50));
            }
            finally
            {
                Object.Destroy(owner);
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
                body.GetComponent<GalaxyEntity>().Initialize("comment_1", "Comment", "comment", 1, registry);
                Assert.That(TryGetActive("comment_1", out _), Is.False);
                body.AddComponent<CommentThreadNode>().Initialize(7);
                body.SetActive(true);
                Assert.That(TryGetActive("comment_1", out _), Is.True);

                pool.Release(body);
                yield return null;

                Assert.That(TryGetActive("comment_1", out _), Is.False);
                var reused = pool.Acquire(PrimitiveType.Sphere, owner.transform);
                Assert.That(reused, Is.SameAs(body));
                reused.GetComponent<GalaxyEntity>().Initialize("post_reused", "Post", "post", 1, registry);
                reused.SetActive(true);
                Assert.That(TryGetActive("post_reused", out _), Is.True);
                Assert.That(reused.GetComponents<CommentThreadNode>(), Has.Length.EqualTo(1));
                Assert.That(reused.GetComponent<CommentThreadNode>().ReplyCount, Is.Zero);
                Assert.That(reused.GetComponent<CommentThreadNode>().DemoModel, Is.Null);
                Assert.That(reused.GetComponent<CommentThreadNode>().isActiveAndEnabled, Is.False);
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
                var snapshot = new SurveySnapshotId(9, 3, "pinned");
                var existing = new SurveyFileRepository(new FileDurableDocumentStore(storageRoot));
                existing.Save(new SurveyState(snapshot, "test-pilot", (GalaxyEntityId)"subreddit:remote",
                    new[] { (GalaxyEntityId)"subreddit:remote", (GalaxyEntityId)"post:remote-only" }));
                var storagePath = Path.Combine(storageRoot, ProfilePaths.Survey("test-pilot", "pinned", 9, 3));
                var before = File.ReadAllText(storagePath);

                var controller = root.AddComponent<GalaxySurveyVisibilityController>();
                controller.BindRegistry(registry);
                controller.Initialize(new GalaxyModeState(), key, "test-pilot", storageRoot);
                CreateSurveyEntity(root.transform, "subreddit:technology", "subreddit star", "subreddit:technology");

                Assert.That(controller.StartAt("subreddit:technology"), Is.False);
                Assert.That(File.ReadAllText(storagePath), Is.EqualTo(before));
                Assert.That(controller.SurveySession, Is.Null);
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
                visibility.BindRegistry(registry);
                visibility.Initialize(modes, SurveySnapshotKey.Demo, "test-pilot", Path.GetTempPath());
                visibility.BindTargeting(targeting);
                cameraObject.transform.SetParent(probe.transform, false);
                var camera = cameraObject.AddComponent<Camera>();
                var navigation = probe.AddComponent<GalaxyNavigationController>();
                navigation.BindRegistry(registry);
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
                entity.Initialize("community-a", "Example Region", "community", 10, registry);
                landmark.AddComponent<LiveEntityMetadata>().Initialize(new SpatialNode
                {
                    id = "community-a", name = "Example Region", type = "community"
                });
                var navigation = probe.AddComponent<GalaxyNavigationController>();
                navigation.BindRegistry(registry);
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

        private GalaxyEntity CreateSurveyEntity(Transform parent, string stableId, string entityType,
            string systemStableId)
        {
            var body = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            body.transform.SetParent(parent, false);
            var entity = body.AddComponent<GalaxyEntity>();
            entity.Initialize(stableId, stableId, entityType, 1, registry);
            body.AddComponent<GalaxySurveyMembership>().Initialize(systemStableId);
            return entity;
        }
    }
}
