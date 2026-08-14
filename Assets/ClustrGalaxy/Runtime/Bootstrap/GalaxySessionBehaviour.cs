using System;
using ClustrGalaxy.Data;
using ClustrGalaxy.Flight;
using ClustrGalaxy.Gameplay;
using ClustrGalaxy.Interaction;
using ClustrGalaxy.Scanning;
using ClustrGalaxy.UI;
using ClustrGalaxy.World;
using UnityEngine;
using System.Linq;
using ClustrGalaxy.Core.Data;
using ClustrGalaxy.Core.Domain;
using ClustrGalaxy.Core.Persistence;
using ClustrGalaxy.Core.Session;
using ClustrGalaxy.Persistence;

namespace ClustrGalaxy.Bootstrap
{
    /// <summary>
    /// The single Unity lifetime owner. Core state is created by the injected
    /// adapters; this component only forwards Unity inputs and owns subscriptions.
    /// </summary>
    public sealed class GalaxySessionBehaviour : MonoBehaviour
    {
        private GalaxySurveyVisibilityController visibility;
        private ProbeHud hud;

        public GalaxyModeState Modes { get; private set; }
        public GalaxyEntityRegistry Registry { get; private set; }
        public GalaxySession Session { get; private set; }

        public void Initialize(ObserverProbeController flight, ProbeTargeting targeting, SpatialSceneLoader loader,
            GalaxyNavigationController navigation, GalaxySurveyVisibilityController visibilityController,
            SignalScanner scanner, CommentThreadController threadInspection, ProbeHud probeHud,
            GalaxyEntityRegistry entityRegistry, ScanRoute[] demoRoutes)
        {
            if (flight == null) throw new ArgumentNullException(nameof(flight));
            if (targeting == null) throw new ArgumentNullException(nameof(targeting));
            if (visibilityController == null) throw new ArgumentNullException(nameof(visibilityController));
            if (scanner == null) throw new ArgumentNullException(nameof(scanner));
            if (probeHud == null) throw new ArgumentNullException(nameof(probeHud));
            Modes = new GalaxyModeState();
            Registry = entityRegistry ?? throw new ArgumentNullException(nameof(entityRegistry));
            visibility = visibilityController;
            hud = probeHud;

            var overviewScope = new SpatialScopeRequest(SpatialScope.Overview);
            var manifest = new GalaxySnapshotManifest(SpatialSceneLoader.DemoSurveySnapshotKey.RevisionId,
                SpatialSceneLoader.DemoSurveySnapshotKey.CatalogId, "spatial-scene-v1", "demo");
            var scene = new GalaxySnapshotScene(manifest.RevisionId, manifest.CatalogId, overviewScope,
                Registry.Entities.Select(entity => new SnapshotNode((GalaxyEntityId)entity.StableId, entity.Kind,
                    entity.DisplayName, entity.Value, entity.transform.position.x, entity.transform.position.y,
                    entity.transform.position.z)),
                Registry.Routes.Select(route => new SnapshotRoute((GalaxyRouteId)route.StableId,
                    (GalaxyEntityId)route.SourceId, (GalaxyEntityId)route.TargetId, 1, false)));
            Session = new GalaxySession(new DeterministicSnapshotSource(manifest, new[] { scene }),
                new SurveyFileRepository(new FileDurableDocumentStore(Application.persistentDataPath)), "offline-pilot");
            var opened = Session.OpenAsync().GetAwaiter().GetResult();
            if (!opened.Succeeded) throw new InvalidOperationException(opened.Error);

            flight.Initialize(Modes);
            visibility.BindRegistry(Registry);
            visibility.Initialize(Modes, SpatialSceneLoader.DemoSurveySnapshotKey);
            visibility.BindGalaxySession(Session);
            visibility.BindDataLoader(loader);
            targeting.Initialize(visibility);
            visibility.BindTargeting(targeting);
            scanner.Initialize(Modes, visibility, targeting, demoRoutes);
            scanner.BindRegistry(Registry);
            scanner.BindDataLoader(loader);
            threadInspection.Initialize(targeting);
            threadInspection.BindNavigation(navigation);
            navigation.Initialize(targeting, Camera.main);
            navigation.BindDiscoveryVisibility(visibility);
            hud.Initialize(flight, targeting, loader, navigation, Modes, visibility, scanner, threadInspection);
            hud.BindSurveyOutcomes(visibility);
            GetComponent<GalaxyPerformanceMonitor>()?.Configure(Registry, scanner, visibility);
        }

        private void OnDestroy()
        {
            hud?.BindSurveyOutcomes(null);
            Session?.Dispose();
            Session = null;
            visibility = null;
        }
    }
}
