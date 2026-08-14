using UnityEngine;
using UnityEngine.InputSystem;
using ClustrGalaxy.Generation;
using ClustrGalaxy.Gameplay;
using ClustrGalaxy.Simulation;

namespace ClustrGalaxy.Flight
{
    [RequireComponent(typeof(Rigidbody))]
    public sealed class ObserverProbeController : MonoBehaviour
    {
        [Header("Translation")]
        [SerializeField] private float cruiseSpeed = GalaxyTuning.CruiseSpeed;
        [SerializeField] private float boostMultiplier = GalaxyTuning.BoostMultiplier;
        [SerializeField] private float acceleration = GalaxyTuning.Acceleration;
        [SerializeField] private float brakeAcceleration = GalaxyTuning.BrakeAcceleration;

        [Header("Rotation")]
        [SerializeField] private float pitchRate = 75f;
        [SerializeField] private float yawRate = 90f;
        [SerializeField] private float rollRate = 100f;

        private Rigidbody body = null!;
        private InputAction move = null!;
        private InputAction look = null!;
        private InputAction lift = null!;
        private InputAction roll = null!;
        private InputAction boost = null!;
        private InputAction brake = null!;
        private InputAction gravityMode = null!;
        private InputAction previousFlightMode = null!;
        private InputAction nextFlightMode = null!;
        private Vector3? autopilotDesiredVelocity;

        public float Speed => body == null ? 0f : body.linearVelocity.magnitude;
        public bool GravityModeEnabled { get; private set; }
        public GalaxyModeState ModeState { get; private set; } = null!;

        public void Initialize(GalaxyModeState modeState)
        {
            ModeState = modeState ?? throw new System.ArgumentNullException(nameof(modeState));
        }

        /// <summary>Supplies a bounded desired velocity for Autopilot only.</summary>
        public void SetAutopilotDesiredVelocity(Vector3 desiredVelocity)
        {
            autopilotDesiredVelocity = desiredVelocity;
        }

        public void ClearAutopilotDesiredVelocity()
        {
            autopilotDesiredVelocity = null;
        }

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            body.useGravity = false;
            body.linearDamping = 0f;
            body.angularDamping = 4f;
            body.interpolation = RigidbodyInterpolation.Interpolate;

            move = new InputAction("Move", InputActionType.Value);
            move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w")
                .With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a")
                .With("Right", "<Keyboard>/d");
            move.AddBinding("<Gamepad>/leftStick");

            look = new InputAction("Look", InputActionType.Value);
            look.AddBinding("<Mouse>/delta").WithProcessor("scaleVector2(x=0.03,y=0.03)");
            look.AddBinding("<Gamepad>/rightStick");

            lift = new InputAction("Lift", InputActionType.Value);
            lift.AddCompositeBinding("1DAxis")
                .With("Negative", "<Keyboard>/leftCtrl")
                .With("Positive", "<Keyboard>/space");
            lift.AddCompositeBinding("1DAxis")
                .With("Negative", "<Gamepad>/leftTrigger")
                .With("Positive", "<Gamepad>/rightTrigger");

            roll = new InputAction("Roll", InputActionType.Value);
            roll.AddCompositeBinding("1DAxis")
                .With("Negative", "<Keyboard>/q")
                .With("Positive", "<Keyboard>/e");
            roll.AddCompositeBinding("1DAxis")
                .With("Negative", "<Gamepad>/leftShoulder")
                .With("Positive", "<Gamepad>/rightShoulder");

            boost = new InputAction("Boost", InputActionType.Button, "<Keyboard>/leftShift");
            boost.AddBinding("<Gamepad>/leftStickPress");
            brake = new InputAction("Brake", InputActionType.Button, "<Keyboard>/x");
            brake.AddBinding("<Gamepad>/buttonEast");
            gravityMode = new InputAction("Gravity Mode", InputActionType.Button, "<Keyboard>/g");
            gravityMode.AddBinding("<Gamepad>/buttonNorth");
            gravityMode.performed += _ => GravityModeEnabled = !GravityModeEnabled;

            previousFlightMode = new InputAction("Previous Pilot Mode", InputActionType.Button, "<Keyboard>/[");
            previousFlightMode.AddBinding("<Gamepad>/dpad/left");
            previousFlightMode.performed += _ => EnsureModeState().CycleFlightMode(-1);
            nextFlightMode = new InputAction("Next Pilot Mode", InputActionType.Button, "<Keyboard>/]");
            nextFlightMode.AddBinding("<Gamepad>/dpad/right");
            nextFlightMode.performed += _ => EnsureModeState().CycleFlightMode(1);
        }

        private void OnEnable()
        {
            move?.Enable();
            look?.Enable();
            lift?.Enable();
            roll?.Enable();
            boost?.Enable();
            brake?.Enable();
            gravityMode?.Enable();
            previousFlightMode?.Enable();
            nextFlightMode?.Enable();
        }

        private void OnDisable()
        {
            move?.Disable();
            look?.Disable();
            lift?.Disable();
            roll?.Disable();
            boost?.Disable();
            brake?.Disable();
            gravityMode?.Disable();
            previousFlightMode?.Disable();
            nextFlightMode?.Disable();
        }

        private void FixedUpdate()
        {
            var modes = EnsureModeState();
            var translation = move.ReadValue<Vector2>();
            var vertical = ProbeFlightModel.ApplyDeadzone(lift.ReadValue<float>());
            var localIntent = Vector3.ClampMagnitude(
                new Vector3(translation.x, vertical, translation.y), 1f);
            var speed = ProbeFlightModel.DesiredSpeed(cruiseSpeed, boostMultiplier, boost.IsPressed());
            var desiredInput = modes.FlightMode == FlightControlMode.Autopilot
                ? autopilotDesiredVelocity ?? Vector3.zero
                : transform.TransformDirection(localIntent);
            if (brake.IsPressed())
            {
                body.linearVelocity = ProbeFlightModel.MoveTowardsVelocity(
                    body.linearVelocity, Vector3.zero, brakeAcceleration, Time.fixedDeltaTime);
            }
            else
            {
                body.linearVelocity = GravityModeEnabled
                    ? ProbeFlightModel.VelocityWithMomentumPreserved(
                        modes.FlightMode,
                        body.linearVelocity,
                        desiredInput,
                        speed,
                        acceleration,
                        Time.fixedDeltaTime)
                    : ProbeFlightModel.DesiredVelocity(
                        modes.FlightMode,
                        body.linearVelocity,
                        desiredInput,
                        speed,
                        acceleration,
                        Time.fixedDeltaTime);
            }

            if (GravityModeEnabled)
            {
                body.AddForce(GravitySource.AccelerationAt(body.position), ForceMode.Acceleration);
            }

            var aim = look.ReadValue<Vector2>();
            var rollInput = ProbeFlightModel.ApplyDeadzone(roll.ReadValue<float>());
            var localAngularVelocity = new Vector3(
                -aim.y * pitchRate,
                aim.x * yawRate,
                -rollInput * rollRate) * Mathf.Deg2Rad;
            body.angularVelocity = transform.TransformDirection(localAngularVelocity);
        }

        private void OnDestroy()
        {
            move?.Dispose();
            look?.Dispose();
            lift?.Dispose();
            roll?.Dispose();
            boost?.Dispose();
            brake?.Dispose();
            gravityMode?.Dispose();
            previousFlightMode?.Dispose();
            nextFlightMode?.Dispose();
        }

        private GalaxyModeState EnsureModeState()
        {
            return ModeState ??= new GalaxyModeState();
        }
    }
}
