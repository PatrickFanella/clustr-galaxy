using System;
using System.Collections.Generic;
using ClustrGalaxy.Interaction;
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
        private float blend;
        private Vector3 storedVelocity;
        private Vector3 storedAngularVelocity;
        private readonly GalaxyNavigationHistory history = new();

        public bool IsMapOpen { get; private set; }
        public GalaxyNavigationLevel Level => history.Level;
        public string FocusedStableId => history.StableId;
        public Vector2 MapCursorPosition { get; private set; } = new(0.5f, 0.5f);
        public event Action<bool> ModeChanged = delegate { };
        public event Action<GalaxyNavigationLevel, string> ViewChanged = delegate { };

        public void Initialize(ProbeTargeting probeTargeting, Camera camera)
        {
            targeting = probeTargeting;
            viewCamera = camera;
        }

        private void Awake()
        {
            flight = GetComponent<ObserverProbeController>();
            body = GetComponent<Rigidbody>();
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
        }

        private void OnEnable()
        {
            toggleMap?.Enable();
            back?.Enable();
            mapCursor?.Enable();
            mapSelect?.Enable();
        }

        private void OnDisable()
        {
            toggleMap?.Disable();
            back?.Disable();
            mapCursor?.Disable();
            mapSelect?.Disable();
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
                    new Vector2(viewport.x, viewport.y), viewport.z > 0f));
            }

            var selectedId = GalaxyMapSelection.NearestId(history.Level, MapCursorPosition, candidates, 0.12f);
            if (GalaxyEntity.TryGet(selectedId, out var selected))
            {
                targeting.SelectTarget(selected);
            }
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
                GalaxyNavigationLevel.Region => 3200f,
                GalaxyNavigationLevel.System => 1500f,
                GalaxyNavigationLevel.Inspect => 180f,
                _ => 5200f
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
        }
    }
}
