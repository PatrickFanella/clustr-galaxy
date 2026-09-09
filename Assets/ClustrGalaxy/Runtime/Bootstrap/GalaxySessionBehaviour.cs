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
using ClustrGalaxy.Core.Survey;
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
        private ProbeTargeting boundTargeting;
        private InstancedStarfield starfield;
        private SurveyState observedCosmeticState;
        public void BindStarfield(InstancedStarfield value) => starfield = value;
        private void Update()
        {
            var state = Session?.Survey?.State;
            if (ReferenceEquals(state, observedCosmeticState)) return;
            observedCosmeticState = state;
            starfield?.SetAuthoredAfterimage(state?.StarfieldAfterimageUnlocked == true);
        }

        public GalaxyModeState Modes { get; private set; }
        public GalaxyEntityRegistry Registry { get; private set; }
        public GalaxySession Session { get; private set; }

        public async void Initialize(ObserverProbeController flight, ProbeTargeting targeting, SpatialSceneLoader loader,
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
                    (GalaxyEntityId)route.SourceId, (GalaxyEntityId)route.TargetId, route.Weight, false)));
            var demo = GalaxyPrototypeBootstrap.IsDemo;
            IGalaxySnapshotSource source = demo ? new DeterministicSnapshotSource(manifest, new[] { scene }) :
                new SpatialSceneSnapshotSource(loader.ApiBase, loader.OfflineOnly, storageNamespace:
                    GalaxyLaunchOptions.FixtureApi(Environment.GetCommandLineArgs()) == null ? "pinned" : "fixture");
            Session = new GalaxySession(source,
                new SurveyFileRepository(new FileDurableDocumentStore(Application.persistentDataPath)), GalaxyLaunchOptions.LocalProfileId(Environment.GetCommandLineArgs()),
                new ActivityFileRepository(new FileDurableDocumentStore(Application.persistentDataPath)),
                demoStartingSystem: demo ? (GalaxyEntityId)"subreddit:technology" : default);

            flight.Initialize(Modes);
            flight.BindSimulation(Session.Flight);
            visibility.BindRegistry(Registry);
            visibility.Initialize(Modes, SpatialSceneLoader.DemoSurveySnapshotKey);
            visibility.BindGalaxySession(Session);
            visibility.BindDataLoader(loader);
            targeting.Initialize(visibility);
            boundTargeting = targeting;
            visibility.BindTargeting(targeting);
            scanner.Initialize(Modes, visibility, targeting, demoRoutes);
            scanner.BindRegistry(Registry);
            scanner.BindDataLoader(loader);
            threadInspection.Initialize(targeting);
            threadInspection.BindNavigation(navigation);
            // Establish inspection identity before navigation may synchronously deliver cached data.
            targeting.TargetChanged += SelectionChanged;
            navigation.BindSession(Session);
            if (!TryGetComponent<GalaxySystemCruiseController>(out _)) gameObject.AddComponent<GalaxySystemCruiseController>();
            if (!TryGetComponent<GalaxyJumpController>(out _)) gameObject.AddComponent<GalaxyJumpController>();
            navigation.Initialize(targeting, Camera.main);
            navigation.BindDiscoveryVisibility(visibility);
            // Provider selection can complete from cache in this same call stack.
            // Bind it only after the inspection and session listeners own the new selection.
            GetComponent<LiveGalaxySelectionController>()?.Initialize(targeting, loader, navigation);
            hud.Initialize(flight, targeting, loader, navigation, Modes, visibility, scanner, threadInspection);
            hud.BindSurveyOutcomes(visibility);
            if (Array.Exists(Environment.GetCommandLineArgs(), value => value == "--clustr-ui-smoke"))
                gameObject.AddComponent<GalaxyUiSmokeRunner>();
            if (demo && Array.Exists(Environment.GetCommandLineArgs(), value => value == "--clustr-simulation-journey"))
                gameObject.AddComponent<GalaxySimulationJourneyRunner>();
            if (demo && Array.Exists(Environment.GetCommandLineArgs(), value => value.StartsWith("--clustr-demo-journey=", StringComparison.Ordinal)))
                gameObject.AddComponent<GalaxyDemoJourneyRunner>();
            GetComponent<GalaxyPerformanceMonitor>()?.Configure(Registry, scanner, visibility);
            GetComponent<GalaxyPerformanceMonitor>()?.BindSource(loader);
            if (!demo)
                foreach (var entity in Registry.Entities) entity.gameObject.SetActive(false);
            var assets = loader.gameObject.AddComponent<GalaxyVisualCatalogProvider>();
            await assets.LoadAsync();
            if(this!=null)PresentationAssets=assets.Catalog;
            if (this != null)
            {
                gameObject.AddComponent<ExplorationFeedback>().Initialize(this, scanner, assets);
                gameObject.AddComponent<ExplorationCelebrations>().Initialize(this);
                gameObject.AddComponent<JourneyRecorder>();
                gameObject.AddComponent<FlightActivitiesController>();
                gameObject.AddComponent<CampaignController>().Initialize(assets.Catalog);
                gameObject.AddComponent<ExpeditionController>();
                if(Array.Exists(Environment.GetCommandLineArgs(),value=>value=="--clustr-art-preview"))gameObject.AddComponent<ExplorationArtPreviewRunner>().Initialize(assets.Catalog);
            }
            if (this != null) await loader.BindAndOpenAsync(Session);
        }

        public GalaxyVisualCatalog PresentationAssets {get;private set;}

        private void SelectionChanged(GalaxyEntity entity) => Session?.SetSelection(entity == null ? default : (GalaxyEntityId)entity.StableId);

        private void OnDestroy()
        {
            if (boundTargeting != null) boundTargeting.TargetChanged -= SelectionChanged;
            hud?.BindSurveyOutcomes(null);
            Session?.Dispose();
            Session = null;
            visibility = null;
        }
    }
}
