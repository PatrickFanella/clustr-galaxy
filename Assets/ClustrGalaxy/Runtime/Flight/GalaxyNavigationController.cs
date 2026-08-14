using System;
using System.Collections.Generic;
using ClustrGalaxy.Generation;
using ClustrGalaxy.Gameplay;
using ClustrGalaxy.Interaction;
using ClustrGalaxy.Simulation;
using ClustrGalaxy.World;
using UnityEngine;
using UnityEngine.InputSystem;

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
        private GalaxySurveyVisibilityController discoveryVisibility = null!;
        private FloatingOrigin floatingOrigin = null!;
        private SurveyEventBus surveyEvents;
        private string pendingFastTravelId = string.Empty;
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
            discoveryVisibility = visibilityController;
        }

        public void BindSurveyEvents(SurveyEventBus sharedEventBus)
        {
            surveyEvents = sharedEventBus;
        }

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
            IsMapOpen = open;
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
            history.Enter(level, stableId, CapturePose());
            SetMapOpen(true);
            ViewChanged(history.Level, history.StableId);
        }

        public void NavigateBack()
        {
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
            foreach (var entity in GalaxyEntity.ActiveEntities)
            {
                var metadata = entity.GetComponent<LiveEntityMetadata>();
                if (metadata?.Node == null)
                {
                    continue;
                }
                var viewport = viewCamera.WorldToViewportPoint(entity.transform.position);
                candidates.Add(new GalaxyMapCandidate(entity.StableId, metadata.Node.type,
                    new Vector2(viewport.x, viewport.y), viewport.z > 0f,
                    discoveryVisibility == null || discoveryVisibility.IsKnown(entity)));
            }

            var worldMode = discoveryVisibility != null ? discoveryVisibility.WorldMode : GalaxyWorldMode.Explore;
            var selectedId = GalaxyMapSelection.NearestId(history.Level, MapCursorPosition, candidates, 0.12f, worldMode);
            if (GalaxyEntity.TryGet(selectedId, out var selected))
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
                candidates.Add(new GalaxyMapCandidate(system.StableId, "subreddit", new Vector2(viewport.x, viewport.y),
                    viewport.z > 0f));
            }
            var selectedId = GalaxyMapSelection.NearestId(GalaxyNavigationLevel.Region, MapCursorPosition,
                candidates, 0.12f, GalaxyWorldMode.Explore);
            return !string.IsNullOrWhiteSpace(selectedId) && discoveryVisibility.StartAt(selectedId);
        }

        public bool CanTravelTo(GalaxyEntity entity)
        {
            if (entity == null)
            {
                return false;
            }

            var metadata = entity.GetComponent<LiveEntityMetadata>();
            var entityType = metadata?.Node?.type ?? entity.EntityType;
            var mode = discoveryVisibility != null ? discoveryVisibility.WorldMode : GalaxyWorldMode.Explore;
            var known = discoveryVisibility == null || discoveryVisibility.IsKnown(entity);
            return GalaxyTravelRules.IsEligibleDestination(mode, known, entityType);
        }

        public bool TryFastTravel(GalaxyEntity entity)
        {
            if (!CanTravelTo(entity))
            {
                TravelStatus = "FAST TRAVEL UNAVAILABLE — SELECT A KNOWN SYSTEM";
                return false;
            }

            pendingFastTravelId = entity.StableId;
            TravelStatus = "FAST TRAVEL ARMED — EXECUTING ON PHYSICS TICK";
            return true;
        }

        public bool PlotAutopilot(GalaxyEntity entity)
        {
            if (!CanTravelTo(entity))
            {
                TravelStatus = "AUTOPILOT UNAVAILABLE — SELECT A KNOWN SYSTEM";
                return false;
            }

            plottedDestinationId = entity.StableId;
            autopilotArrived = false;
            masteryArrivalReported = false;
            flight.ModeState.SetFlightMode(FlightControlMode.Autopilot);
            TravelStatus = $"AUTOPILOT PLOTTED — {entity.DisplayName}";
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

            plottedDestinationId = entity.StableId;
            autopilotArrived = false;
            masteryArrivalReported = false;
            TravelStatus = $"MASTERY APPROACH SET — {entity.DisplayName}";
            return true;
        }

        private void FixedUpdate()
        {
            if (!string.IsNullOrWhiteSpace(pendingFastTravelId))
            {
                var destinationId = pendingFastTravelId;
                pendingFastTravelId = string.Empty;
                if (GalaxyEntity.TryGet(destinationId, out var destination) && CanTravelTo(destination))
                {
                    ExecuteFastTravel(destination);
                }
                else
                {
                    TravelStatus = "FAST TRAVEL CANCELLED — DESTINATION NO LONGER AVAILABLE";
                }
            }

            UpdatePlottedApproach();
        }

        private void ExecuteFastTravel(GalaxyEntity destination)
        {
            var arrival = ArrivalAnchor(destination, body.position);
            // The map pauses the rigidbody by retaining its pre-map velocity.
            // A jump is a hard docking transaction, so clear both the live and
            // retained state before the map can ever restore old momentum.
            storedVelocity = Vector3.zero;
            storedAngularVelocity = Vector3.zero;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.position = arrival;
            transform.position = arrival;

            // Rebase in the same physics transaction. This keeps the probe at
            // a precise local origin while FloatingOrigin preserves the global
            // double-precision coordinate of every scene root and orbit.
            if (floatingOrigin != null)
            {
                floatingOrigin.Rebase(-arrival);
            }
            Physics.SyncTransforms();
            TravelStatus = $"ARRIVED — {destination.DisplayName}";
        }

        private void UpdatePlottedApproach()
        {
            if (string.IsNullOrWhiteSpace(plottedDestinationId))
            {
                return;
            }
            if (!GalaxyEntity.TryGet(plottedDestinationId, out var destination) || !CanTravelTo(destination))
            {
                flight.ClearAutopilotDesiredVelocity();
                TravelStatus = "ROUTE LOST — DESTINATION NO LONGER AVAILABLE";
                plottedDestinationId = string.Empty;
                return;
            }

            var anchor = ArrivalAnchor(destination, body.position);
            var arrived = GalaxyTravelRules.HasArrived(body.position, anchor, body.linearVelocity,
                AutopilotAnchorTolerance);
            if (flight.ModeState.FlightMode == FlightControlMode.Autopilot)
            {
                flight.SetAutopilotDesiredVelocity(GalaxyTravelRules.AutopilotDesiredVelocity(
                    body.position, anchor, body.linearVelocity, GalaxyTravelRules.AutopilotMaximumSpeed,
                    GalaxyTuning.BrakeAcceleration, AutopilotAnchorTolerance));
                if (arrived)
                {
                    flight.ClearAutopilotDesiredVelocity();
                    autopilotArrived = true;
                    TravelStatus = $"AUTOPILOT ARRIVED — {destination.DisplayName}";
                }
                return;
            }

            flight.ClearAutopilotDesiredVelocity();
            // A prior automatic arrival is deliberately not reclassified as a
            // Pilot Mastery arrival when the player switches modes afterwards.
            if (!autopilotArrived && !masteryArrivalReported &&
                flight.ModeState.FlightMode == FlightControlMode.PilotMastery && arrived)
            {
                masteryArrivalReported = true;
                TravelStatus = $"MASTERY ARRIVAL — {destination.DisplayName}";
                var store = discoveryVisibility?.DiscoveryStore;
                if (store != null)
                {
                    surveyEvents?.Publish(SurveyEvent.PilotMasteryArrival(store.SnapshotKey,
                        destination.StableId, DateTime.UtcNow));
                }
            }
        }

        private static Vector3 ArrivalAnchor(GalaxyEntity destination, Vector3 approachOrigin)
        {
            var collider = destination.GetComponent<Collider>();
            var collisionRadius = collider != null ? collider.bounds.extents.magnitude : 0f;
            var gravity = destination.GetComponent<GravitySource>();
            var softening = gravity != null ? gravity.SofteningRadius : 0d;
            var radius = GalaxyTravelRules.ArrivalRadius(collisionRadius, softening);
            return GalaxyTravelRules.ArrivalAnchor(destination.transform.position, approachOrigin, radius);
        }

        private void LateUpdate()
        {
            if (viewCamera == null)
            {
                return;
            }
            blend = Mathf.MoveTowards(blend, IsMapOpen ? 1f : 0f, Time.unscaledDeltaTime / 0.8f);
            var eased = blend * blend * (3f - 2f * blend);
            var focus = !string.IsNullOrWhiteSpace(history.StableId) && GalaxyEntity.TryGet(history.StableId, out var focused)
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
