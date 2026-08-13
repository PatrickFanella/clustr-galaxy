using System;
using ClustrGalaxy.Interaction;
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
        private float blend;
        private Vector3 storedVelocity;
        private Vector3 storedAngularVelocity;

        public bool IsMapOpen { get; private set; }
        public event Action<bool> ModeChanged = delegate { };

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
            back.performed += _ => SetMapOpen(false);
        }

        private void OnEnable()
        {
            toggleMap?.Enable();
            back?.Enable();
        }

        private void OnDisable()
        {
            toggleMap?.Disable();
            back?.Disable();
        }

        public void SetMapOpen(bool open)
        {
            if (IsMapOpen == open)
            {
                return;
            }
            IsMapOpen = open;
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

        private void LateUpdate()
        {
            if (viewCamera == null)
            {
                return;
            }
            blend = Mathf.MoveTowards(blend, IsMapOpen ? 1f : 0f, Time.unscaledDeltaTime / 0.8f);
            var eased = blend * blend * (3f - 2f * blend);
            var focus = targeting != null && targeting.Target != null
                ? targeting.Target.transform.position
                : transform.position + transform.forward * 240f;
            var mapPosition = focus + new Vector3(0f, 1600f, -5200f);
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
        }
    }
}
