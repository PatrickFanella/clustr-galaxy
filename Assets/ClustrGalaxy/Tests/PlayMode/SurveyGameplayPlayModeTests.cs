using System;
using System.Collections;
using System.IO;
using System.Linq;
using ClustrGalaxy.Constellations;
using ClustrGalaxy.Data;
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
    /// <summary>Offline, composition-level regression for the core Survey loop.</summary>
    public sealed class SurveyGameplayPlayModeTests
    {
        [UnityTest]
        public IEnumerator OfflineDemoJourney_SurveyScanThreadModesAndReloadRemainCoherent()
        {
            var root = new GameObject("offline survey journey");
            var storageRoot = Path.Combine(Path.GetTempPath(), "clustr-offline-journey-" + Guid.NewGuid().ToString("N"));
            try
            {
                var modes = new GalaxyModeState();
                var events = new SurveyEventBus();
                var visibility = root.AddComponent<GalaxySurveyVisibilityController>();
                var registry = root.AddComponent<GalaxyEntityRegistry>();
                var technology = CreateEntity(root.transform, "subreddit:technology", "subreddit star", "subreddit:technology");
                var post = CreateEntity(root.transform, "post:open-source-ai", "post planet", "subreddit:technology");
                var destination = CreateEntity(root.transform, "subreddit:signal-horizon", "subreddit star", "subreddit:signal-horizon");
                var parent = CreateEntity(root.transform, "comment:open-source-ai-parent", "discussion satellite", "subreddit:technology");
                parent.gameObject.AddComponent<CommentThreadNode>().Initialize(1);
                var routeRoot = new GameObject("offline survey route");
                routeRoot.transform.SetParent(root.transform, false);
                routeRoot.AddComponent<GalaxySurveyRoute>().Initialize("route:technology-signal-horizon", technology.StableId, destination.StableId);
                registry.RefreshFromScene();

                var store = SurveyDiscoveryStore.Open(storageRoot, "offline-pilot", SurveySnapshotKey.Pinned(9, 3), technology.StableId);
                visibility.Initialize(modes, store);
                visibility.BindRegistry(registry);
                var targeting = root.AddComponent<ProbeTargeting>();
                visibility.BindTargeting(targeting);
                var scanner = root.AddComponent<SignalScanner>();
                scanner.Initialize(modes, visibility, targeting, events, new[]
                {
                    new ScanRoute("route:technology-signal-horizon", technology.StableId, destination.StableId, 8f)
                });
                scanner.BindRegistry(registry);
                var thread = root.AddComponent<CommentThreadController>();
                thread.Initialize(targeting);
                using var constellation = new ConstellationTracker(store, events, new[] { ConstellationDefinition.Example },
                    ConstellationDefinition.Example.MemberStableIds);

                Assert.That(modes.WorldMode, Is.EqualTo(GalaxyWorldMode.Explore));
                modes.SetWorldMode(GalaxyWorldMode.Survey);
                Assert.That(visibility.StartAt(technology.StableId), Is.True);
                yield return null;
                Assert.That(visibility.IsKnown(technology), Is.True);
                Assert.That(visibility.IsKnown(destination), Is.False);

                targeting.SelectTarget(technology);
                scanner.BeginScan();
                scanner.Tick(SignalScanner.RequiredHoldSeconds);
                yield return null;
                Assert.That(store.IsRouteKnown("route:technology-signal-horizon"), Is.True);
                Assert.That(store.IsEntityKnown(destination.StableId), Is.True);
                Assert.That(constellation.ProgressOf("example-constellation").Message, Is.EqualTo("Example Constellation 1/6"));

                modes.SetWorldMode(GalaxyWorldMode.Explore);
                yield return null;
                var beforeThread = GalaxyEntity.ActiveEntities.Count();
                targeting.SelectTarget(parent);
                thread.PresentThread(new SpatialScene
                {
                    scope = "thread", root_id = parent.StableId,
                    nodes = new[]
                    {
                        new SpatialNode { id = parent.StableId, type = "comment" },
                        new SpatialNode { id = "comment:reply-hud-only", type = "comment", parent_id = parent.StableId,
                            author_name = "offline", metrics = new SpatialNodeMetrics { score = 1, reply_count = 0 } }
                    }
                });
                Assert.That(thread.DisplayedModel, Is.Not.Null);
                Assert.That(thread.DisplayedModel.NodeCount, Is.EqualTo(1));
                Assert.That(GalaxyEntity.ActiveEntities.Count(), Is.EqualTo(beforeThread));

                modes.SetFlightMode(FlightControlMode.Autopilot);
                modes.SetFlightMode(FlightControlMode.PilotAssist);
                modes.SetFlightMode(FlightControlMode.PilotMastery);
                Assert.That(modes.FlightMode, Is.EqualTo(FlightControlMode.PilotMastery));
                store.Save();

                var reloaded = SurveyDiscoveryStore.Open(storageRoot, "offline-pilot", SurveySnapshotKey.Pinned(9, 3), technology.StableId);
                Assert.That(reloaded.IsEntityKnown(destination.StableId), Is.True);
                visibility.Initialize(modes, reloaded);
                visibility.BindRegistry(registry);
                modes.SetWorldMode(GalaxyWorldMode.Explore);
                Assert.That(visibility.CanRender(destination), Is.True);
                modes.SetWorldMode(GalaxyWorldMode.Survey);
                Assert.That(reloaded.IsRouteKnown("route:technology-signal-horizon"), Is.True);
                Assert.That(visibility.IsKnown(destination), Is.True);
                Assert.That(visibility.IsKnown(post), Is.True);
            }
            finally
            {
                UnityEngine.Object.Destroy(root);
                if (Directory.Exists(storageRoot)) Directory.Delete(storageRoot, recursive: true);
            }
        }

        private static GalaxyEntity CreateEntity(Transform parent, string stableId, string type, string systemId)
        {
            var entityRoot = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            entityRoot.transform.SetParent(parent, false);
            var entity = entityRoot.AddComponent<GalaxyEntity>();
            entity.Initialize(stableId, stableId, type, 1);
            entityRoot.AddComponent<GalaxySurveyMembership>().Initialize(systemId);
            return entity;
        }
    }
}
