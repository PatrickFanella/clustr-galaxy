using System;
using System.Collections.Generic;
using ClustrGalaxy.Generation;
using ClustrGalaxy.Gameplay;
using ClustrGalaxy.Interaction;
using ClustrGalaxy.Simulation;
using ClustrGalaxy.World;
using UnityEngine;
using UnityEngine.InputSystem;
using ClustrGalaxy.Core.Domain;
using ClustrGalaxy.Core.Survey;
using CoreNavigation = ClustrGalaxy.Core.Navigation.GalaxyNavigation;
using CoreNavigationPose = ClustrGalaxy.Core.Navigation.NavigationPose;
using CoreNavigationTarget = ClustrGalaxy.Core.Navigation.NavigationTarget;
using CoreNavigationFlightMode = ClustrGalaxy.Core.Navigation.NavigationFlightMode;
using NumericsVector3 = System.Numerics.Vector3;
using NumericsQuaternion = System.Numerics.Quaternion;

namespace ClustrGalaxy.Flight
{
    [RequireComponent(typeof(Rigidbody), typeof(ObserverProbeController))]
    public sealed class GalaxyNavigationController : MonoBehaviour
    {
        private ObserverProbeController flight = null!;
        private ProbeTargeting targeting = null!;
        private Rigidbody body = null!;
        private Camera flightCamera = null!;
        private ClustrGalaxy.UI.CockpitChartStation chartStation;
        private Camera viewCamera => IsMapOpen && chartStation?.ChartCamera != null ? chartStation.ChartCamera : flightCamera;
        private InputAction toggleMap = null!;
        private InputAction back = null!;
        private InputAction mapCursor = null!;
        private InputAction mapSelect = null!;
        private InputAction enterSurvey = null!;
        private InputAction fastTravel = null!;
        private InputAction plotAutopilot = null!;
        private float blend;
        private CoreNavigation navigation = new();
        private ClustrGalaxy.Core.Session.GalaxySession session;
        private GalaxySurveyVisibilityController discoveryVisibility = null!;
        private GalaxyEntityRegistry registry = null!;
        private FloatingOrigin floatingOrigin = null!;
        private Func<GalaxyEntityId, CoreNavigationTarget> resolveTarget;
        private string plottedDestinationId = string.Empty;
        private bool autopilotArrived;
        private bool masteryArrivalReported;

        private const float AutopilotAnchorTolerance = 2.5f;

        public bool IsMapOpen { get; private set; }
        public bool IsChartSettled => IsMapOpen && blend >= 1f;
        public GalaxyNavigationLevel Level => (GalaxyNavigationLevel)navigation.State.Level;
        public string FocusedStableId => navigation.State.FocusedId.Value;
        public Vector2 MapCursorPosition { get; private set; } = new(0.5f, 0.5f);
        public string MapHoveredStableId { get; private set; } = string.Empty;
        public string MapHoverStatus { get; private set; } = "NO SELECTABLE SIGNAL";
        public string TravelStatus { get; private set; } = string.Empty;
        public void ReportTravelStatus(string value) => TravelStatus = value ?? "";
        public string PlottedDestinationId => plottedDestinationId;
        public bool HasAutopilotArrival => autopilotArrived;
        public bool OrbitActive => navigation.Orbit.Active;
        public bool OrbitCaptured => navigation.Orbit.Captured;
        public float OrbitAltitude => navigation.Orbit.Altitude;
        public float OrbitSpeed => navigation.Orbit.CircularSpeed;

        public void InterruptForRecovery()
        {
            session?.Travel?.Abort(session.Travel.State.OperationId, "RECOVERY · TRAVEL ABORTED");
            activities?.Cancel(); ApplyNavigation(new ClustrGalaxy.Core.Navigation.ClearFlightGuidanceIntent());
            flight.ClearAutopilotDesiredVelocity(); TravelStatus = "RECOVERY HOLD";
        }

        public bool StartOrbit(GalaxyEntity entity)
        {
            var gravity = entity != null ? entity.GetComponent<GravitySource>() : null;
            var collider = entity != null ? entity.GetComponent<SphereCollider>() : null;
            if (session?.Travel?.State.Active == true || flight.Simulation.Recovering || gravity == null || collider == null || !entity.gameObject.activeInHierarchy ||
                (discoveryVisibility != null && discoveryVisibility.WorldMode == GalaxyWorldMode.Survey && !discoveryVisibility.IsKnown(entity)))
            { TravelStatus = "ORBIT UNAVAILABLE — SELECT A KNOWN GRAVITY BODY"; return false; }
            var orbit = entity.GetComponent<KeplerOrbit>();
            var maximum = orbit != null ? OrbitStability.MaximumProgradeMoonAxis(
                orbit.Elements.SemiMajorAxis * (1d - orbit.Elements.Eccentricity), gravity.GravitationalParameter, orbit.ParentMu) : double.PositiveInfinity;
            var minimum = Math.Max(entity.SurfaceRadius * 1.6d, (gravity.Definition?.ExclusionRadius ?? entity.SurfaceRadius) + ShipSurfaceSafety.HullRadius * 2d + 2d);
            if (maximum < minimum) { TravelStatus = "ORBIT UNAVAILABLE · NO TIDALLY SAFE BAND"; return false; }
            var centerVelocity = gravity.CenterVelocity;
            var candidate = new ClustrGalaxy.Core.Navigation.OrbitalAutopilot();
            if (!candidate.Begin((GalaxyEntityId)entity.StableId, ToNumerics(body.position - entity.transform.position),
                ToNumerics(body.linearVelocity - centerVelocity), gravity.GravitationalParameter, entity.SurfaceRadius, minimum, maximum, flight.Simulation.Ship.ForceLimits.X / flight.Simulation.Ship.Mass))
            { TravelStatus = "ORBIT UNAVAILABLE — MOVE CLEAR OF THE SURFACE"; return false; }
            activities?.Cancel();
            ApplyNavigation(new ClustrGalaxy.Core.Navigation.ClearFlightGuidanceIntent());
            navigation.Orbit.Begin((GalaxyEntityId)entity.StableId, ToNumerics(body.position - entity.transform.position),
                ToNumerics(body.linearVelocity - centerVelocity), gravity.GravitationalParameter, entity.SurfaceRadius, minimum, maximum,
                flight.Simulation.Ship.ForceLimits.X / flight.Simulation.Ship.Mass);
            RefreshResidency();
            flight.ModeState.SetFlightMode(FlightControlMode.Autopilot);
            TravelStatus = "AUTO ORBIT · CAPTURING";
            return true;
        }

        public void ReleaseOrbit()
        {
            navigation.Orbit.Cancel(); RefreshResidency(); flight.ClearAutopilotDesiredVelocity();
            flight.ModeState.SetFlightMode(FlightControlMode.PilotMastery);
            TravelStatus = "ORBIT RELEASED · INERTIAL FLIGHT";
        }

        private bool GuideOrbit()
        {
            if (!navigation.Orbit.Active) return false;
            var entity = registry?.FindActiveEntity(navigation.Orbit.TargetId.Value);
            if (entity == null || flight.ModeState.FlightMode != FlightControlMode.Autopilot || !flight.GravityModeEnabled ||
                (discoveryVisibility != null && discoveryVisibility.WorldMode == GalaxyWorldMode.Survey && !discoveryVisibility.IsKnown(entity)))
            { navigation.Orbit.Cancel(); RefreshResidency(); flight.ClearAutopilotDesiredVelocity(); return false; }
            var centerVelocity = entity.TryGetComponent<KeplerOrbit>(out var orbit) ? orbit.WorldVelocity : Vector3.zero;
            var velocity = navigation.Orbit.DesiredRelativeVelocity(ToNumerics(body.position - entity.transform.position),
                ToNumerics(body.linearVelocity - centerVelocity));
            if (!navigation.Orbit.Active) { RefreshResidency(); flight.ClearAutopilotDesiredVelocity(); return false; }
            flight.SetAutopilotDesiredVelocity(centerVelocity + ToUnity(velocity), true);
            TravelStatus = navigation.Orbit.Captured ? "AUTO ORBIT · STABLE" : "AUTO ORBIT · CAPTURING";
            return true;
        }
        public event Action<ClustrGalaxy.Core.Data.SpatialScopeRequest> ScopeRequested = delegate { };
        public event Action<bool> ModeChanged = delegate { };
        public event Action<GalaxyNavigationLevel, string> ViewChanged = delegate { };

        public void BindSession(ClustrGalaxy.Core.Session.GalaxySession owner)
        { session = owner; navigation = owner.Navigation; }
        public void RefreshResidency() => session?.RefreshNavigationReferences();
        private ClustrGalaxy.Core.Navigation.NavigationOutcome ApplyNavigation(ClustrGalaxy.Core.Navigation.NavigationIntent intent)
        {
            var outcome = navigation.Apply(intent);
            RefreshResidency();
            return outcome;
        }

        public void Initialize(ProbeTargeting probeTargeting, Camera camera)
        {
            targeting = probeTargeting;
            flightCamera = camera;
            if (GetComponent<ClustrGalaxy.UI.ProbeHud>() != null)
            {
                chartStation = gameObject.AddComponent<ClustrGalaxy.UI.CockpitChartStation>();
                chartStation.Initialize(camera,this);
            }
        }

        public void BindDiscoveryVisibility(GalaxySurveyVisibilityController visibilityController)
        {
            if (discoveryVisibility != null && flight?.ModeState != null)
                flight.ModeState.WorldModeChanged -= SyncWorldMode;
            discoveryVisibility = visibilityController;
            registry = visibilityController?.Registry;
            if (discoveryVisibility != null && flight?.ModeState != null)
            {
                flight.ModeState.WorldModeChanged += SyncWorldMode;
                SyncWorldMode(flight.ModeState.WorldMode);
            }
        }

        private void SyncWorldMode(GalaxyWorldMode mode) => ApplyNavigation(
            new ClustrGalaxy.Core.Navigation.SetSurveyModeIntent(mode == GalaxyWorldMode.Survey,
                discoveryVisibility?.RequiresStartingSystemChoice == true));

        public void BindRegistry(GalaxyEntityRegistry entityRegistry) =>
            registry = entityRegistry ?? throw new ArgumentNullException(nameof(entityRegistry));

        private void Awake()
        {
            resolveTarget = id => ToTarget(registry?.FindActiveEntity(id.Value), physicsTarget);
            flight = GetComponent<ObserverProbeController>();
            body = GetComponent<Rigidbody>();
            floatingOrigin = GetComponent<FloatingOrigin>();
            toggleMap = new InputAction("Galaxy Map", InputActionType.Button, "<Keyboard>/m");
            toggleMap.AddBinding("<Gamepad>/select");
            toggleMap.performed += _ => SetMapOpen(!IsMapOpen);
            back = new InputAction("Back", InputActionType.Button, "<Keyboard>/escape");
            back.AddBinding("<Gamepad>/buttonEast");
            back.performed += _ => NavigateBack();
            mapCursor = new InputAction("Map Cursor", InputActionType.Value);
            mapCursor.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/upArrow")
                .With("Down", "<Keyboard>/downArrow")
                .With("Left", "<Keyboard>/leftArrow")
                .With("Right", "<Keyboard>/rightArrow");
            mapCursor.AddBinding("<Gamepad>/leftStick");
            mapSelect = new InputAction("Map Select", InputActionType.Button, "<Keyboard>/enter");
            mapSelect.AddBinding("<Gamepad>/buttonSouth");
            mapSelect.performed += _ => SelectAtMapCursor();
            enterSurvey = new InputAction("Enter Survey", InputActionType.Button, "<Keyboard>/v");
            enterSurvey.AddBinding("<Gamepad>/dpad/up");
            enterSurvey.performed += _ => EnterSurvey();
            fastTravel = new InputAction("Fast Travel", InputActionType.Button, "<Keyboard>/t");
            fastTravel.AddBinding("<Gamepad>/dpad/down");
            fastTravel.performed += _ => TryFastTravel(targeting?.Target);
            plotAutopilot = new InputAction("Plot Autopilot", InputActionType.Button, "<Keyboard>/p");
            plotAutopilot.AddBinding("<Gamepad>/rightStickPress");
            plotAutopilot.performed += _ => PlotAutopilot(targeting?.Target);
        }

        public void SetPilotInputEnabled(bool value) { if (value) OnEnable(); else OnDisable(); }
        public void SetCockpitInputEnabled(bool enabled, bool chart)
        {
            SetAction(toggleMap, enabled); SetAction(back, enabled && chart);
            SetAction(mapCursor, enabled && chart); SetAction(mapSelect, enabled && chart);
            SetAction(enterSurvey, enabled); SetAction(fastTravel, enabled); SetAction(plotAutopilot, enabled);
        }
        private static void SetAction(InputAction action, bool enabled)
        { if (action == null || action.enabled == enabled) return; if (enabled) action.Enable(); else action.Disable(); }

        private void OnEnable()
        {
            toggleMap?.Enable();
            back?.Enable();
            mapCursor?.Enable();
            mapSelect?.Enable();
            enterSurvey?.Enable();
            fastTravel?.Enable();
            plotAutopilot?.Enable();
        }

        private void OnDisable()
        {
            toggleMap?.Disable();
            back?.Disable();
            mapCursor?.Disable();
            mapSelect?.Disable();
            enterSurvey?.Disable();
            fastTravel?.Disable();
            plotAutopilot?.Disable();
        }

        public void SetMapOpen(bool open)
        {
            if (open && session?.Travel?.State.Active == true)
            { TravelStatus = "CANCEL THE ACTIVE DRIVE BEFORE OPENING THE MAP"; return; }
            if (IsMapOpen == open && navigation.State.IsMapOpen == open)
            {
                return;
            }
            var outcome = ApplyNavigation(new ClustrGalaxy.Core.Navigation.SetMapIntent(open, CorePose(),
                ToNumerics(body.linearVelocity), ToNumerics(body.angularVelocity)));
            IsMapOpen = outcome.State.IsMapOpen;
            // Chart inspection owns input, not the ship's momentum or celestial clock.
            if (GetComponent<ClustrGalaxy.UI.ProbeHud>() == null)
            {
                flight.SetPilotInputEnabled(!open);
                targeting?.SetAcquisitionEnabled(!open);
                Cursor.lockState = open ? CursorLockMode.None : CursorLockMode.Locked; Cursor.visible = open;
            }
            chartStation?.SetVisible(open);
            ModeChanged(open);
        }

        public void NavigateTo(GalaxyNavigationLevel level, string stableId)
        {
            var entity = registry?.FindActiveEntity(stableId);
            if (entity == null) return;
            var outcome = ApplyNavigation(new ClustrGalaxy.Core.Navigation.InspectNavigationTargetIntent(ToTarget(entity), CorePose()));
            if (!outcome.Succeeded) return;
            SetMapOpen(true);
            ViewChanged((GalaxyNavigationLevel)navigation.State.Level, navigation.State.FocusedId.Value);
            if (outcome.Effect.ScopeToLoad.HasValue) ScopeRequested(outcome.Effect.ScopeToLoad.Value);
        }

        public void ArriveAtSystem(string id)
        {
            var entity=registry.FindEntity(id);
            if(entity!=null)ApplyNavigation(new ClustrGalaxy.Core.Navigation.InspectNavigationTargetIntent(ToTarget(entity), CorePose()));
            SetMapOpen(false); ViewChanged(GalaxyNavigationLevel.System,id);
        }
        public void NavigateBack()
        {
            targeting?.SelectTarget(null);
            var outcome = ApplyNavigation(new ClustrGalaxy.Core.Navigation.ReturnNavigationIntent());
            if (!outcome.Effect.Teleport) { SetMapOpen(false); return; }
            ViewChanged(Level, FocusedStableId);
            if (outcome.Effect.ScopeToLoad.HasValue) ScopeRequested(outcome.Effect.ScopeToLoad.Value);
        }

        private void Update()
        {
            if (!IsMapOpen)
            {
                MapHoveredStableId = string.Empty;
                MapHoverStatus = "NO SELECTABLE SIGNAL";
                return;
            }

            MapCursorPosition = new Vector2(
                Mathf.Clamp01(MapCursorPosition.x + mapCursor.ReadValue<Vector2>().x * Time.unscaledDeltaTime * 0.65f),
                Mathf.Clamp01(MapCursorPosition.y + mapCursor.ReadValue<Vector2>().y * Time.unscaledDeltaTime * 0.65f));
            if (Mouse.current != null && Mouse.current.delta.ReadValue().sqrMagnitude > 0.1f)
            {
                var mouse = Mouse.current.position.ReadValue();
                if (chartStation != null)
                { if (chartStation.TryPointer(mouse,out var chartPoint)) MapCursorPosition = chartPoint; }
                else MapCursorPosition = new Vector2(mouse.x / Screen.width, mouse.y / Screen.height);
            }
            RefreshMapHover();
        }

        public void ConfirmChartSelection() => SelectAtMapCursor();
        private void SelectAtMapCursor()
        {
            if (!IsMapOpen || viewCamera == null)
            {
                return;
            }

            if (discoveryVisibility != null && discoveryVisibility.RequiresStartingSystemChoice)
            {
                ConfirmSurveyStartingSystem();
                return;
            }

            var worldMode = discoveryVisibility != null ? discoveryVisibility.WorldMode : GalaxyWorldMode.Explore;
            var selectedId = GalaxyMapSelection.NearestId((GalaxyNavigationLevel)navigation.State.Level, MapCursorPosition,
                BuildMapCandidates(), 0.12f, worldMode);
            if (TryGetActive(selectedId, out var selected))
            {
                targeting.SelectTarget(selected);
            }
        }

        private bool TryGetActive(string id, out GalaxyEntity entity)
        {
            entity = registry?.FindActiveEntity(id);
            return entity != null;
        }

        private void RefreshMapHover()
        {
            if (viewCamera == null)
            {
                MapHoveredStableId = string.Empty;
                MapHoverStatus = "NO SELECTABLE SIGNAL";
                return;
            }

            var worldMode = discoveryVisibility != null ? discoveryVisibility.WorldMode : GalaxyWorldMode.Explore;
            var nearest = GalaxyMapSelection.Nearest((GalaxyNavigationLevel)navigation.State.Level, MapCursorPosition,
                BuildMapCandidates(), 0.12f, worldMode);
            if (!nearest.HasValue || !TryGetActive(nearest.Value.StableId, out var entity))
            {
                MapHoveredStableId = string.Empty;
                MapHoverStatus = "NO SELECTABLE SIGNAL";
                return;
            }

            var changedTarget = MapHoveredStableId != entity.StableId;
            MapHoveredStableId = entity.StableId;
            var canLabel = discoveryVisibility == null || discoveryVisibility.CanLabel(entity);
            if (!changedTarget && hoverEntity == entity && hoverName == entity.DisplayName && hoverLabelled == canLabel) return;
            hoverEntity = entity; hoverName = entity.DisplayName; hoverLabelled = canLabel;
            MapHoverStatus = canLabel
                ? $"{entity.DisplayName}  •  {entity.Kind.ToString().ToUpperInvariant()}"
                : "UNIDENTIFIED SIGNAL";
        }

        public delegate bool ChartPositionResolver(string stableId, out Vector3 position);
        /// <summary>Region-chart position for entities the current physical system has borrowed.</summary>
        public ChartPositionResolver ChartPositions { get; set; }
        private Vector3 MapPosition(GalaxyEntity entity) =>
            ChartPositions != null && ChartPositions(entity.StableId, out var chart) ? chart : entity.transform.position;

        private readonly List<GalaxyMapCandidate> mapCandidates = new();
        private GalaxyEntity hoverEntity;
        private string hoverName;
        private bool hoverLabelled;

        private List<GalaxyMapCandidate> BuildMapCandidates()
        {
            var candidates = mapCandidates;
            candidates.Clear();
            var entities = registry?.ActiveEntities ?? Array.Empty<GalaxyEntity>();
            for (var index = 0; index < entities.Count; index++)
            {
                var entity = entities[index];
                var viewport = viewCamera.WorldToViewportPoint(MapPosition(entity));
                candidates.Add(new GalaxyMapCandidate(entity.StableId, entity.Kind,
                    new Vector2(viewport.x, viewport.y), viewport.z > 0f,
                    discoveryVisibility == null || discoveryVisibility.IsKnown(entity)));
            }
            return candidates;
        }

        public bool EnterSurvey()
        {
            if (discoveryVisibility == null)
            {
                return false;
            }
            SetMapOpen(true);
            ApplyNavigation(new ClustrGalaxy.Core.Navigation.EnterSurveyNavigationIntent());
            discoveryVisibility.EnterSurvey();
            return true;
        }

        public bool ConfirmSurveyStartingSystem()
        {
            if (discoveryVisibility == null || viewCamera == null || !discoveryVisibility.RequiresStartingSystemChoice)
            {
                return false;
            }

            var candidates = new List<GalaxyMapCandidate>();
            foreach (var system in discoveryVisibility.LoadedStartingSystems())
            {
                var viewport = viewCamera.WorldToViewportPoint(MapPosition(system));
                candidates.Add(new GalaxyMapCandidate(system.StableId, system.Kind, new Vector2(viewport.x, viewport.y),
                    viewport.z > 0f));
            }
            var selectedId = GalaxyMapSelection.NearestId(GalaxyNavigationLevel.Region, MapCursorPosition,
                candidates, 0.12f, GalaxyWorldMode.Explore);
            var started = !string.IsNullOrWhiteSpace(selectedId) && discoveryVisibility.StartAt(selectedId);
            return started;
        }

        public bool CanTravelTo(GalaxyEntity entity)
        {
            if (entity == null || flight?.Simulation.Recovering == true || session?.Travel?.State.Active == true)
            {
                return false;
            }

            var mode = discoveryVisibility != null ? discoveryVisibility.WorldMode : GalaxyWorldMode.Explore;
            var known = discoveryVisibility == null || discoveryVisibility.IsKnown(entity);
            return GalaxyTravelRules.IsEligibleDestination(mode, known, entity.Kind) && session?.JumpEligibility((GalaxyEntityId)entity.StableId) == "";
        }

        public bool TryFastTravel(GalaxyEntity entity)
        {
            if (!CanTravelTo(entity))
            {
                TravelStatus = "FAST TRAVEL UNAVAILABLE — SELECT A KNOWN SYSTEM";
                return false;
            }

            return GetComponent<GalaxyJumpController>()?.Begin(entity) == true;
        }

        public bool CanApproach(GalaxyEntity entity) => session?.Travel?.State.Active != true && flight?.Simulation.Recovering != true && entity != null && entity.gameObject.activeInHierarchy &&
            (GalaxyKinds.IsNavigable(entity.Kind) || entity.Kind == GalaxyEntityKind.Post || entity.GetComponent<GravitySource>()?.Definition != null) &&
            (discoveryVisibility == null || discoveryVisibility.WorldMode == GalaxyWorldMode.Explore || discoveryVisibility.IsKnown(entity));

        public bool PlotAutopilot(GalaxyEntity entity)
        {
            if (!CanApproach(entity))
            {
                TravelStatus = "AUTOPILOT UNAVAILABLE — SELECT A KNOWN SYSTEM";
                return false;
            }

            activities?.Cancel();
            var outcome = ApplyNavigation(new ClustrGalaxy.Core.Navigation.PlotApproachIntent(ToTarget(entity), false));
            if (!outcome.Succeeded) { TravelStatus = outcome.Rejection; return false; }
            plottedDestinationId = entity.StableId;
            autopilotArrived = false;
            masteryArrivalReported = false;
            flight.ModeState.SetFlightMode(FlightControlMode.Autopilot);
            TravelStatus = outcome.State.Status;
            return true;
        }

        /// <summary>Tracks an eligible system while the pilot flies manually.</summary>
        public bool TrackMasteryDestination(GalaxyEntity entity)
        {
            if (!CanApproach(entity))
            {
                TravelStatus = "MASTERY DESTINATION UNAVAILABLE — SELECT A KNOWN SYSTEM";
                return false;
            }

            activities?.Cancel();
            var outcome = ApplyNavigation(new ClustrGalaxy.Core.Navigation.PlotApproachIntent(ToTarget(entity), true));
            if (!outcome.Succeeded) { TravelStatus = outcome.Rejection; return false; }
            plottedDestinationId = entity.StableId;
            autopilotArrived = false;
            masteryArrivalReported = false;
            TravelStatus = outcome.State.Status;
            return true;
        }

        private ClustrGalaxy.UI.FlightActivitiesController activities;
        public void BindActivities(ClustrGalaxy.UI.FlightActivitiesController value)=>activities=value;
        private float nextTelemetry;
        private void UpdateTelemetry()
        {
            if (Time.unscaledTime < nextTelemetry) return;
            nextTelemetry = Time.unscaledTime + .2f;
            var source = targeting?.Target != null ? targeting.Target.GetComponent<GravitySource>() : null;
            if (source?.Definition == null)
            {
                var strongest = 0d; var sources = GravitySource.ActiveSources;
                for (var i = 0; i < sources.Count; i++)
                {
                    var candidate = sources[i]; if (candidate?.Definition == null) continue;
                    var strength = candidate.GravitationalParameter / Math.Max(1d, (candidate.transform.position - body.position).sqrMagnitude);
                    if (strength > strongest) { strongest = strength; source = candidate; }
                }
            }
            flight.Simulation.SetTelemetry(source?.Definition == null ? default : ClustrGalaxy.Core.Navigation.FlightTelemetry.Calculate(
                source.Definition, ToNumerics(body.position - source.transform.position), ToNumerics(body.linearVelocity - source.CenterVelocity), GalaxyTuning.BrakeAcceleration));
        }
        private void FixedUpdate()
        {
            if (session?.Travel?.State.Active == true)
            { flight.Simulation.SetTelemetry(default); TravelStatus = session.Travel.Status; return; }
            UpdateTelemetry();
            if (flight.Simulation.Recovering) { TravelStatus = flight.Simulation.RecoveryReason; return; }
            if (GuideOrbit()) return;
            if(activities!=null&&activities.GuidePhysics())return;
            var mode = flight.ModeState.FlightMode switch
            {
                FlightControlMode.Autopilot => CoreNavigationFlightMode.Autopilot,
                FlightControlMode.PilotMastery => CoreNavigationFlightMode.PilotMastery,
                _ => CoreNavigationFlightMode.PilotAssist
            };
            var outcome = navigation.AdvancePhysics(CorePose(), ToNumerics(body.linearVelocity), mode,
                resolveTarget, GalaxyTuning.BrakeAcceleration,
                AutopilotAnchorTolerance);
            TravelStatus = outcome.State.Status;
            plottedDestinationId = outcome.State.PlottedDestinationId.Value ?? string.Empty;
            autopilotArrived = outcome.State.AutopilotArrived;
            masteryArrivalReported = outcome.State.MasteryArrivalReported;
            if (outcome.Effect.Teleport)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                var position = ToUnity(outcome.Effect.Position);
                body.position = position;
                transform.position = position;
                if (outcome.Effect.Rebase) floatingOrigin?.Rebase(-position);
                Physics.SyncTransforms();
            }
            if (!outcome.State.PlottedDestinationId.IsEmpty && mode == CoreNavigationFlightMode.Autopilot)
                flight.SetAutopilotDesiredVelocity(ToUnity(outcome.Effect.Velocity));
            else
                flight.ClearAutopilotDesiredVelocity();
            if (!outcome.Effect.MasteryArrival.IsEmpty && discoveryVisibility?.WorldMode == GalaxyWorldMode.Survey && discoveryVisibility.SurveySession != null)
            {
                var session = discoveryVisibility.SurveySession;
                discoveryVisibility.ApplySurveyIntent(new PilotMasteryArrivalIntent(
                    "mastery:" + outcome.Effect.MasteryArrival.Value, session.State.Snapshot, DateTime.UtcNow,
                    outcome.Effect.MasteryArrival));
            }
        }

        private CoreNavigationPose CorePose() => new(ToNumerics(body.position), new NumericsQuaternion(
            body.rotation.x, body.rotation.y, body.rotation.z, body.rotation.w));
        // One reusable target for the per-tick resolver; intents always receive a fresh target.
        private readonly CoreNavigationTarget physicsTarget = new(default, default, string.Empty, default, false, false);
        private CoreNavigationTarget ToTarget(GalaxyEntity entity, CoreNavigationTarget reuse = null)
        {
            if (entity == null) return null;
            var collider = entity.GetComponent<Collider>();
            var collisionRadius = entity.SurfaceRadius > 0 ? entity.SurfaceRadius : collider != null ? collider.bounds.extents.magnitude : 0f;
            if (entity.VisualKind == GalaxyVisualKind.Star) collisionRadius += (float)ClustrGalaxy.Generation.CelestialLayout.StellarClearance;
            var gravity = entity.GetComponent<GravitySource>();
            return (reuse ?? new CoreNavigationTarget(default, default, string.Empty, default, false, false)).Refresh(
                (GalaxyEntityId)entity.StableId, entity.Kind, entity.DisplayName,
                ToNumerics(entity.transform.position), entity.gameObject.activeInHierarchy,
                discoveryVisibility == null || discoveryVisibility.IsKnown(entity), collisionRadius,
                gravity == null ? 0f : (float)gravity.SofteningRadius,
                ToNumerics(gravity != null ? gravity.CenterVelocity : entity.TryGetComponent<KeplerOrbit>(out var orbit) ? orbit.WorldVelocity : Vector3.zero), gravity?.Definition,
                discoveryVisibility == null || discoveryVisibility.CanLabel(entity));
        }
        private static NumericsVector3 ToNumerics(Vector3 value) => new(value.x, value.y, value.z);
        private static Vector3 ToUnity(NumericsVector3 value) => new(value.X, value.Y, value.Z);

        private void LateUpdate()
        {
            if (viewCamera == null)
            {
                return;
            }
            if (chartStation != null && !IsMapOpen) return;
            blend = Mathf.MoveTowards(blend, IsMapOpen ? 1f : 0f, Time.unscaledDeltaTime / 0.8f);
            var eased = blend * blend * (3f - 2f * blend);
            GalaxyEntity focused = null;
            var hasFocusedEntity = !string.IsNullOrWhiteSpace(navigation.State.FocusedId.Value) &&
                TryGetActive(navigation.State.FocusedId.Value, out focused);
            var hasTarget = targeting != null && targeting.Target != null;
            var focus = ResolveMapFocus((GalaxyNavigationLevel)navigation.State.Level,
                hasFocusedEntity, hasFocusedEntity ? MapPosition(focused) : Vector3.zero,
                hasTarget, hasTarget ? MapPosition(targeting.Target) : Vector3.zero,
                transform.position, transform.forward);
            var distance = (GalaxyNavigationLevel)navigation.State.Level switch
            {
                GalaxyNavigationLevel.Region => 3200f * GalaxyTuning.RegionDistanceScale,
                GalaxyNavigationLevel.System => 1500f * GalaxyTuning.SystemDistanceScale,
                GalaxyNavigationLevel.Inspect => 180f * GalaxyTuning.MoonOrbitScale,
                _ => ClustrGalaxy.Data.GalacticLandmarkLayout.OverviewCameraDistance(58f, viewCamera.aspect)
            };
            var mapPosition = focus + new Vector3(0f, distance * 0.3f, -distance);
            var mapRotation = Quaternion.LookRotation(focus - mapPosition, Vector3.up);
            var flightPosition = transform.position;
            var flightRotation = transform.rotation;
            viewCamera.transform.SetPositionAndRotation(
                Vector3.Lerp(flightPosition, mapPosition, eased),
                Quaternion.Slerp(flightRotation, mapRotation, eased));
            viewCamera.fieldOfView = Mathf.Lerp(72f, 58f, eased);
        }

        public static Vector3 ResolveMapFocus(GalaxyNavigationLevel level,
            bool hasFocusedEntity, Vector3 focusedPosition,
            bool hasTarget, Vector3 targetPosition,
            Vector3 probePosition, Vector3 probeForward)
        {
            if (hasFocusedEntity) return focusedPosition;
            if (hasTarget) return targetPosition;
            return level == GalaxyNavigationLevel.Galaxy
                ? Vector3.zero
                : probePosition + probeForward * 240f;
        }

        private void OnDestroy()
        {
            if (discoveryVisibility != null && flight?.ModeState != null)
                flight.ModeState.WorldModeChanged -= SyncWorldMode;
            toggleMap?.Dispose();
            back?.Dispose();
            mapCursor?.Dispose();
            mapSelect?.Dispose();
            enterSurvey?.Dispose();
            fastTravel?.Dispose();
            plotAutopilot?.Dispose();
        }
    }
}
