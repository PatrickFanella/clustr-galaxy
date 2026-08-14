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
        private Camera viewCamera = null!;
        private InputAction toggleMap = null!;
        private InputAction back = null!;
        private InputAction mapCursor = null!;
        private InputAction mapSelect = null!;
        private InputAction enterSurvey = null!;
        private InputAction fastTravel = null!;
        private InputAction plotAutopilot = null!;
        private float blend;
        private Vector3 storedVelocity;
        private Vector3 storedAngularVelocity;
        private readonly GalaxyNavigationHistory history = new();
        private readonly CoreNavigation navigation = new();
        private GalaxySurveyVisibilityController discoveryVisibility = null!;
        private GalaxyEntityRegistry registry = null!;
        private FloatingOrigin floatingOrigin = null!;
        private string plottedDestinationId = string.Empty;
        private bool autopilotArrived;
        private bool masteryArrivalReported;

        private const float AutopilotAnchorTolerance = 2.5f;

        public bool IsMapOpen { get; private set; }
        public GalaxyNavigationLevel Level => history.Level;
        public string FocusedStableId => history.StableId;
        public Vector2 MapCursorPosition { get; private set; } = new(0.5f, 0.5f);
        public string TravelStatus { get; private set; } = string.Empty;
        public string PlottedDestinationId => plottedDestinationId;
        public bool HasAutopilotArrival => autopilotArrived;
        public event Action<bool> ModeChanged = delegate { };
        public event Action<GalaxyNavigationLevel, string> ViewChanged = delegate { };

        public void Initialize(ProbeTargeting probeTargeting, Camera camera)
        {
            targeting = probeTargeting;
            viewCamera = camera;
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

        private void SyncWorldMode(GalaxyWorldMode mode) => navigation.Apply(
            new ClustrGalaxy.Core.Navigation.SetSurveyModeIntent(mode == GalaxyWorldMode.Survey,
                discoveryVisibility?.RequiresStartingSystemChoice == true));

        public void BindRegistry(GalaxyEntityRegistry entityRegistry) =>
            registry = entityRegistry ?? throw new ArgumentNullException(nameof(entityRegistry));

        private void Awake()
        {
            flight = GetComponent<ObserverProbeController>();
            body = GetComponent<Rigidbody>();
            floatingOrigin = GetComponent<FloatingOrigin>();
            toggleMap = new InputAction("Galaxy Map", InputActionType.Button, "<Keyboard>/m");
            toggleMap.AddBinding("<Gamepad>/select");
            toggleMap.performed += _ => SetMapOpen(!IsMapOpen);
            back = new InputAction("Back", InputActionType.Button, "<Keyboard>/escape");
            back.AddBinding("<Gamepad>/start");
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
            if (IsMapOpen == open)
            {
                return;
            }
            var outcome = navigation.Apply(new ClustrGalaxy.Core.Navigation.SetMapIntent(open, CorePose(),
                ToNumerics(body.linearVelocity), ToNumerics(body.angularVelocity)));
            IsMapOpen = outcome.State.IsMapOpen;
            targeting?.SetAcquisitionEnabled(!open);
            if (open)
            {
                storedVelocity = body.linearVelocity;
                storedAngularVelocity = body.angularVelocity;
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                body.isKinematic = true;
                flight.enabled = false;
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else
            {
                body.isKinematic = false;
                body.linearVelocity = storedVelocity;
                body.angularVelocity = storedAngularVelocity;
                flight.enabled = true;
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
            ModeChanged(open);
        }

        public void NavigateTo(GalaxyNavigationLevel level, string stableId)
        {
            var entity = registry?.FindActiveEntity(stableId);
            if (entity == null || !navigation.Apply(new ClustrGalaxy.Core.Navigation.InspectNavigationTargetIntent(
                    ToTarget(entity), CorePose())).Succeeded) return;
            history.Enter(level, stableId, CapturePose());
            SetMapOpen(true);
            ViewChanged(history.Level, history.StableId);
        }

        public void NavigateBack()
        {
            navigation.Apply(new ClustrGalaxy.Core.Navigation.ReturnNavigationIntent());
            if (!history.TryBack(out var pose))
            {
                SetMapOpen(false);
                return;
            }

            body.position = pose.Position;
            body.rotation = pose.Rotation;
            transform.SetPositionAndRotation(pose.Position, pose.Rotation);
            ViewChanged(history.Level, history.StableId);
        }

        private NavigationPose CapturePose() => new(body.position, body.rotation);

        private void Update()
        {
            if (!IsMapOpen)
            {
                return;
            }

            MapCursorPosition = new Vector2(
                Mathf.Clamp01(MapCursorPosition.x + mapCursor.ReadValue<Vector2>().x * Time.unscaledDeltaTime * 0.65f),
                Mathf.Clamp01(MapCursorPosition.y + mapCursor.ReadValue<Vector2>().y * Time.unscaledDeltaTime * 0.65f));
            if (Mouse.current != null && Mouse.current.delta.ReadValue().sqrMagnitude > 0.1f)
            {
                var mouse = Mouse.current.position.ReadValue();
                MapCursorPosition = new Vector2(mouse.x / Screen.width, mouse.y / Screen.height);
            }
        }

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

            var candidates = new List<GalaxyMapCandidate>();
            foreach (var entity in registry?.ActiveEntities ?? Array.Empty<GalaxyEntity>())
            {
                var metadata = entity.GetComponent<LiveEntityMetadata>();
                if (metadata?.Node == null)
                {
                    continue;
                }
                var viewport = viewCamera.WorldToViewportPoint(entity.transform.position);
                candidates.Add(new GalaxyMapCandidate(entity.StableId, entity.Kind,
                    new Vector2(viewport.x, viewport.y), viewport.z > 0f,
                    discoveryVisibility == null || discoveryVisibility.IsKnown(entity)));
            }

            var worldMode = discoveryVisibility != null ? discoveryVisibility.WorldMode : GalaxyWorldMode.Explore;
            var selectedId = GalaxyMapSelection.NearestId(history.Level, MapCursorPosition, candidates, 0.12f, worldMode);
            var selected = registry?.FindActiveEntity(selectedId);
            if (selected != null)
            {
                targeting.SelectTarget(selected);
            }
        }

        public bool EnterSurvey()
        {
            if (discoveryVisibility == null)
            {
                return false;
            }
            SetMapOpen(true);
            navigation.Apply(new ClustrGalaxy.Core.Navigation.EnterSurveyNavigationIntent());
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
                var viewport = viewCamera.WorldToViewportPoint(system.transform.position);
                candidates.Add(new GalaxyMapCandidate(system.StableId, system.Kind, new Vector2(viewport.x, viewport.y),
                    viewport.z > 0f));
            }
            var selectedId = GalaxyMapSelection.NearestId(GalaxyNavigationLevel.Region, MapCursorPosition,
                candidates, 0.12f, GalaxyWorldMode.Explore);
            var started = !string.IsNullOrWhiteSpace(selectedId) && discoveryVisibility.StartAt(selectedId);
            if (started) navigation.Apply(new ClustrGalaxy.Core.Navigation.ChooseSurveyStartIntent((GalaxyEntityId)selectedId));
            return started;
        }

        public bool CanTravelTo(GalaxyEntity entity)
        {
            if (entity == null)
            {
                return false;
            }

            var mode = discoveryVisibility != null ? discoveryVisibility.WorldMode : GalaxyWorldMode.Explore;
            var known = discoveryVisibility == null || discoveryVisibility.IsKnown(entity);
            return GalaxyTravelRules.IsEligibleDestination(mode, known, entity.Kind);
        }

        public bool TryFastTravel(GalaxyEntity entity)
        {
            if (!CanTravelTo(entity))
            {
                TravelStatus = "FAST TRAVEL UNAVAILABLE — SELECT A KNOWN SYSTEM";
                return false;
            }

            var outcome = navigation.Apply(new ClustrGalaxy.Core.Navigation.FastTravelIntent(ToTarget(entity)));
            TravelStatus = outcome.Succeeded ? outcome.State.Status : outcome.Rejection;
            return outcome.Succeeded;
        }

        public bool PlotAutopilot(GalaxyEntity entity)
        {
            if (!CanTravelTo(entity))
            {
                TravelStatus = "AUTOPILOT UNAVAILABLE — SELECT A KNOWN SYSTEM";
                return false;
            }

            var outcome = navigation.Apply(new ClustrGalaxy.Core.Navigation.PlotApproachIntent(ToTarget(entity), false));
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
            if (!CanTravelTo(entity))
            {
                TravelStatus = "MASTERY DESTINATION UNAVAILABLE — SELECT A KNOWN SYSTEM";
                return false;
            }

            var outcome = navigation.Apply(new ClustrGalaxy.Core.Navigation.PlotApproachIntent(ToTarget(entity), true));
            if (!outcome.Succeeded) { TravelStatus = outcome.Rejection; return false; }
            plottedDestinationId = entity.StableId;
            autopilotArrived = false;
            masteryArrivalReported = false;
            TravelStatus = outcome.State.Status;
            return true;
        }

        private void FixedUpdate()
        {
            var mode = flight.ModeState.FlightMode switch
            {
                FlightControlMode.Autopilot => CoreNavigationFlightMode.Autopilot,
                FlightControlMode.PilotMastery => CoreNavigationFlightMode.PilotMastery,
                _ => CoreNavigationFlightMode.PilotAssist
            };
            var outcome = navigation.AdvancePhysics(CorePose(), ToNumerics(body.linearVelocity), mode,
                id => ToTarget(registry?.FindActiveEntity(id.Value)), GalaxyTuning.BrakeAcceleration,
                AutopilotAnchorTolerance);
            TravelStatus = outcome.State.Status;
            plottedDestinationId = outcome.State.PlottedDestinationId.Value ?? string.Empty;
            autopilotArrived = outcome.State.AutopilotArrived;
            masteryArrivalReported = outcome.State.MasteryArrivalReported;
            if (outcome.Effect.Teleport)
            {
                storedVelocity = Vector3.zero;
                storedAngularVelocity = Vector3.zero;
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
            if (!outcome.Effect.MasteryArrival.IsEmpty && discoveryVisibility?.SurveySession != null)
            {
                var session = discoveryVisibility.SurveySession;
                discoveryVisibility.ApplySurveyIntent(new PilotMasteryArrivalIntent(
                    "mastery:" + outcome.Effect.MasteryArrival.Value, session.State.Snapshot, DateTime.UtcNow,
                    outcome.Effect.MasteryArrival));
            }
        }

        private CoreNavigationPose CorePose() => new(ToNumerics(body.position), new NumericsQuaternion(
            body.rotation.x, body.rotation.y, body.rotation.z, body.rotation.w));
        private CoreNavigationTarget ToTarget(GalaxyEntity entity)
        {
            if (entity == null) return null;
            var collider = entity.GetComponent<Collider>();
            var collisionRadius = collider != null ? collider.bounds.extents.magnitude : 0f;
            var gravity = entity.GetComponent<GravitySource>();
            return new CoreNavigationTarget((GalaxyEntityId)entity.StableId, entity.Kind, entity.DisplayName,
                ToNumerics(entity.transform.position), entity.gameObject.activeInHierarchy,
                discoveryVisibility == null || discoveryVisibility.IsKnown(entity), collisionRadius,
                gravity == null ? 0f : (float)gravity.SofteningRadius);
        }
        private static NumericsVector3 ToNumerics(Vector3 value) => new(value.x, value.y, value.z);
        private static Vector3 ToUnity(NumericsVector3 value) => new(value.X, value.Y, value.Z);

        private void LateUpdate()
        {
            if (viewCamera == null)
            {
                return;
            }
            blend = Mathf.MoveTowards(blend, IsMapOpen ? 1f : 0f, Time.unscaledDeltaTime / 0.8f);
            var eased = blend * blend * (3f - 2f * blend);
            var focused = !string.IsNullOrWhiteSpace(history.StableId) ? registry?.FindActiveEntity(history.StableId) : null;
            var focus = focused != null
                ? focused.transform.position
                : targeting != null && targeting.Target != null
                    ? targeting.Target.transform.position
                : transform.position + transform.forward * 240f;
            var distance = history.Level switch
            {
                GalaxyNavigationLevel.Region => 3200f * GalaxyTuning.RegionDistanceScale,
                GalaxyNavigationLevel.System => 1500f * GalaxyTuning.SystemDistanceScale,
                GalaxyNavigationLevel.Inspect => 180f * GalaxyTuning.MoonOrbitScale,
                _ => 5200f * GalaxyTuning.RegionDistanceScale
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
