using System;
using ClustrGalaxy.Constellations;
using ClustrGalaxy.Data;
using ClustrGalaxy.Discovery;
using ClustrGalaxy.Flight;
using ClustrGalaxy.Gameplay;
using ClustrGalaxy.Interaction;
using ClustrGalaxy.Progression;
using ClustrGalaxy.Scanning;
using ClustrGalaxy.UI;
using ClustrGalaxy.World;
using UnityEngine;

namespace ClustrGalaxy.Bootstrap
{
    /// <summary>
    /// Composition root for the local/offline galaxy loop. It deliberately
    /// owns service lifetime and injects the same mode state/event bus into all
    /// consumers, rather than allowing gameplay code to recover dependencies
    /// through scene searches or static mutable state.
    /// </summary>
    public sealed class GalaxyGameplayBootstrap : MonoBehaviour
    {
        private ConstellationTracker constellationTracker;
        private PersonalConstellationStore personalConstellations;
        private ConstellationSubmissionOutbox constellationOutbox;
        private SurveyProgression progression;
        private EasterEggTracker easterEggs;

        public GalaxyModeState Modes { get; private set; }
        public SurveyEventBus SurveyEvents { get; private set; }
        public GalaxyEntityRegistry Registry { get; private set; }
        public SurveyDiscoveryStore DiscoveryStore => visibility != null ? visibility.DiscoveryStore : null;

        private GalaxySurveyVisibilityController visibility;
        private ProbeHud hud;
        private InstancedStarfield starfield;

        public void Initialize(
            ObserverProbeController flight,
            ProbeTargeting targeting,
            SpatialSceneLoader loader,
            GalaxyNavigationController navigation,
            GalaxySurveyVisibilityController visibilityController,
            SignalScanner scanner,
            CommentThreadController threadInspection,
            ProbeHud probeHud,
            InstancedStarfield distantStarfield,
            GalaxyEntityRegistry entityRegistry,
            ScanRoute[] demoRoutes)
        {
            if (flight == null) throw new ArgumentNullException(nameof(flight));
            if (targeting == null) throw new ArgumentNullException(nameof(targeting));
            if (visibilityController == null) throw new ArgumentNullException(nameof(visibilityController));
            if (scanner == null) throw new ArgumentNullException(nameof(scanner));
            if (probeHud == null) throw new ArgumentNullException(nameof(probeHud));

            Modes = new GalaxyModeState();
            SurveyEvents = new SurveyEventBus();
            Registry = entityRegistry ?? throw new ArgumentNullException(nameof(entityRegistry));
            visibility = visibilityController;
            hud = probeHud;
            starfield = distantStarfield;

            flight.Initialize(Modes);
            visibility.Initialize(Modes, SpatialSceneLoader.DemoSurveySnapshotKey);
            visibility.BindRegistry(Registry);
            visibility.BindDataLoader(loader);
            targeting.Initialize(visibility);
            visibility.BindTargeting(targeting);
            scanner.Initialize(Modes, visibility, targeting, SurveyEvents, demoRoutes);
            scanner.BindRegistry(Registry);
            scanner.BindDataLoader(loader);
            threadInspection.Initialize(targeting);
            threadInspection.BindNavigation(navigation);
            navigation.Initialize(targeting, Camera.main);
            navigation.BindDiscoveryVisibility(visibility);
            navigation.BindSurveyEvents(SurveyEvents);
            hud.Initialize(flight, targeting, loader, navigation, Modes, visibility, scanner, threadInspection);
            var performance = GetComponent<GalaxyPerformanceMonitor>();
            performance?.Configure(Registry, scanner, visibility);
            SurveyEvents.Published += OnSurveyEvent;
            visibility.SurveyStarted += ConfigureSurveyServices;
        }

        private void OnSurveyEvent(SurveyEvent surveyEvent)
        {
            if (surveyEvent.Type == SurveyEventType.ScanCompleted)
            {
                GetComponent<GalaxyPerformanceMonitor>()?.RecordScanEvent();
            }
        }

        private void ConfigureSurveyServices(SurveyDiscoveryStore store)
        {
            DisposeSurveyServices();
            constellationTracker = new ConstellationTracker(store, SurveyEvents, new[] { ConstellationDefinition.Example },
                ConstellationDefinition.Example.MemberStableIds);
            personalConstellations = PersonalConstellationStore.OpenDefault(store.ProfileId, store.SnapshotKey, SurveyEvents);
            constellationOutbox = new ConstellationSubmissionOutbox(personalConstellations);
            progression = new SurveyProgression(store, SurveyEvents);
            easterEggs = new EasterEggTracker(store, Modes, SurveyEvents, new[] { EasterEggDefinition.SignalAfterimage });
            hud.ConfigureConstellations(constellationTracker, personalConstellations, constellationOutbox);
            hud.ConfigureProgression(progression);
            hud.ConfigureEasterEggs(easterEggs, starfield);
        }

        private void DisposeSurveyServices()
        {
            constellationTracker?.Dispose();
            progression?.Dispose();
            easterEggs?.Dispose();
            constellationTracker = null;
            progression = null;
            easterEggs = null;
        }

        private void OnDestroy()
        {
            if (visibility != null) visibility.SurveyStarted -= ConfigureSurveyServices;
            if (SurveyEvents != null) SurveyEvents.Published -= OnSurveyEvent;
            DisposeSurveyServices();
        }
    }
}
