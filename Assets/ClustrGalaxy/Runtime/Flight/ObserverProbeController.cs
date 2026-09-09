using UnityEngine;
using UnityEngine.InputSystem;
using ClustrGalaxy.Generation;
using ClustrGalaxy.Gameplay;
using ClustrGalaxy.Simulation;
using ClustrGalaxy.Core.Navigation;

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
        [SerializeField] private float angularAcceleration = 720f;
        private InputAction attitude = null!;
        private InputAction yawLift = null!;
        private InputAction thrust = null!;
        private InputAction strafe = null!;
        private InputAction move = null!;
        private InputAction look = null!;
        private InputAction lift = null!;
        private InputAction roll = null!;
        private InputAction boost = null!;
        private InputAction brake = null!;
        private InputAction previousFlightMode = null!;
        private InputAction nextFlightMode = null!;
        private Vector3? autopilotDesiredVelocity;
        private FlightSimulationState simulation = new();
        private long commandSequence;
        public FlightSimulationState Simulation => simulation;
        public Vector3 AppliedAcceleration { get; private set; }
        public void BindSimulation(FlightSimulationState value)
        {
            simulation = value ?? throw new System.ArgumentNullException(nameof(value)); commandSequence = 0;
            ConfigureBody();
        }
        private void ConfigureBody()
        {
            body.mass = simulation.Ship.Mass; body.centerOfMass = Vector3.zero;
            body.inertiaTensorRotation = Quaternion.identity;
            body.inertiaTensor = Unity(simulation.Ship.Inertia);
            body.maxAngularVelocity = 1000f;
        }
        private static System.Numerics.Vector3 Numerics(Vector3 v) => new(v.x, v.y, v.z);
        private static Vector3 Unity(System.Numerics.Vector3 v) => new(v.X, v.Y, v.Z);
        public float LocalGravityAcceleration { get; private set; }

        public string ReplayTuningIdentity()
        {
            // Explicit model version plus effective serialized controls and physics step; not locale-dependent.
            var values=new[]{cruiseSpeed,boostMultiplier,acceleration,brakeAcceleration,pitchRate,yawRate,rollRate,angularAcceleration,Time.fixedDeltaTime,GalaxyTuning.SunRadius,GalaxyTuning.SunSurfaceGravity,GalaxyTuning.PlanetSurfaceGravity,GalaxyTuning.PlanetOrbitScale,GalaxyTuning.PlanetRadiusScale,GalaxyTuning.OrbitSimulationTimeScale,GravityModeEnabled?1f:0f};
            using var hash=System.Security.Cryptography.SHA256.Create();
            var bytes=new byte[values.Length*4];for(var i=0;i<values.Length;i++)
            {var part=System.BitConverter.GetBytes(values[i]);if(!System.BitConverter.IsLittleEndian)System.Array.Reverse(part);System.Array.Copy(part,0,bytes,i*4,4);}
            return "flight-v7-"+System.BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-","").ToLowerInvariant();
        }
        public Vector3? DriveVelocity { get; internal set; }
        public Vector3 PilotTranslation { get; private set; }
        public Vector3 PilotRotation { get; private set; }
        public bool PilotBrake { get; private set; }
        public Vector3 AngularRateLimits => new(pitchRate, yawRate, rollRate);
        public float Speed => DriveVelocity?.magnitude ?? (body == null ? 0f : body.linearVelocity.magnitude);
        public bool GravityModeEnabled => true;
        public GalaxyModeState ModeState { get; private set; } = null!;

        public void Initialize(GalaxyModeState modeState)
        {
            ModeState = modeState ?? throw new System.ArgumentNullException(nameof(modeState));
        }

        /// <summary>Supplies a bounded desired velocity for Autopilot only.</summary>
        public void SetAutopilotDesiredVelocity(Vector3 desiredVelocity, bool orbit = false)
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
            body.angularDamping = 0f;
            if (GetComponent<ShipSurfaceSafety>() == null) gameObject.AddComponent<ShipSurfaceSafety>();
            ConfigureBody();
            body.interpolation = RigidbodyInterpolation.Interpolate;

            move = new InputAction("Move", InputActionType.Value);
            move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w")
                .With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a")
                .With("Right", "<Keyboard>/d");
            attitude=new InputAction("Ship pitch and roll",InputActionType.Value,"<Gamepad>/rightStick");
            yawLift=new InputAction("Ship yaw and vertical thrust",InputActionType.Value,"<Gamepad>/leftStick");
            thrust=new InputAction("Forward and reverse thrust",InputActionType.Value);
            thrust.AddCompositeBinding("1DAxis").With("Positive","<Gamepad>/rightTrigger").With("Negative","<Gamepad>/leftTrigger");
            strafe=new InputAction("Sideways thrust",InputActionType.Value);
            strafe.AddCompositeBinding("1DAxis").With("Positive","<Gamepad>/rightShoulder").With("Negative","<Gamepad>/leftShoulder");

            look = new InputAction("Look", InputActionType.Value);
            look.AddBinding("<Mouse>/delta").WithProcessor("scaleVector2(x=0.03,y=0.03)");


            lift = new InputAction("Lift", InputActionType.Value);
            lift.AddCompositeBinding("1DAxis")
                .With("Negative", "<Keyboard>/leftCtrl")
                .With("Positive", "<Keyboard>/space");


            roll = new InputAction("Roll", InputActionType.Value);
            roll.AddCompositeBinding("1DAxis")
                .With("Negative", "<Keyboard>/q")
                .With("Positive", "<Keyboard>/e");


            boost = new InputAction("Boost", InputActionType.Button, "<Keyboard>/leftShift");
            boost.AddBinding("<Gamepad>/leftStickPress");
            brake = new InputAction("Brake", InputActionType.Button, "<Keyboard>/x");
            brake.AddBinding("<Gamepad>/buttonEast");

            previousFlightMode = new InputAction("Previous Pilot Mode", InputActionType.Button, "<Keyboard>/[");
            previousFlightMode.AddBinding("<Gamepad>/dpad/left");
            previousFlightMode.performed += _ => EnsureModeState().CycleFlightMode(-1);
            nextFlightMode = new InputAction("Next Pilot Mode", InputActionType.Button, "<Keyboard>/]");
            nextFlightMode.AddBinding("<Gamepad>/dpad/right");
            nextFlightMode.performed += _ => EnsureModeState().CycleFlightMode(1);
        }

        public void SetPilotInputEnabled(bool value) { if (value) OnEnable(); else OnDisable(); }

        private void OnEnable()
        {
            attitude?.Enable();yawLift?.Enable();thrust?.Enable();strafe?.Enable();
            move?.Enable();
            look?.Enable();
            lift?.Enable();
            roll?.Enable();
            boost?.Enable();
            brake?.Enable();
            previousFlightMode?.Enable();
            nextFlightMode?.Enable();
        }

        private void OnDisable()
        {
            attitude?.Disable();yawLift?.Disable();thrust?.Disable();strafe?.Disable();
            move?.Disable();
            look?.Disable();
            lift?.Disable();
            roll?.Disable();
            boost?.Disable();
            brake?.Disable();
            previousFlightMode?.Disable();
            nextFlightMode?.Disable();
        }

        private void FixedUpdate()
        {
            var modes = EnsureModeState();
            var translation = move.ReadValue<Vector2>();
            var vertical = ProbeFlightModel.ApplyDeadzone(lift.ReadValue<float>());
            var stick=attitude.ReadValue<Vector2>();var secondary=yawLift.ReadValue<Vector2>();
            var thrustAxis=ProbeFlightModel.ApplyDeadzone(thrust.ReadValue<float>());
            var strafeAxis=ProbeFlightModel.ApplyDeadzone(strafe.ReadValue<float>());
            var ship=ClustrGalaxy.Core.Navigation.ShipControlInput.FromGamepad(new System.Numerics.Vector2(stick.x,stick.y),
                new System.Numerics.Vector2(secondary.x,secondary.y),Mathf.Max(0,thrustAxis),Mathf.Max(0,-thrustAxis),Mathf.Max(0,strafeAxis),Mathf.Max(0,-strafeAxis));
            var localIntent = Vector3.ClampMagnitude(new Vector3(translation.x+ship.Translation.X,vertical+ship.Translation.Y,translation.y+ship.Translation.Z),1f);
            var aim = look.ReadValue<Vector2>();
            var rollInput = ProbeFlightModel.ApplyDeadzone(roll.ReadValue<float>());
            var angularInput = new Vector3(Mathf.Clamp(-aim.y + ship.Rotation.X, -1, 1),
                Mathf.Clamp(aim.x + ship.Rotation.Y, -1, 1), Mathf.Clamp(-rollInput + ship.Rotation.Z, -1, 1));
            PilotTranslation = localIntent; PilotRotation = angularInput; PilotBrake = brake.IsPressed();
            if (simulation.Recovering || body.isKinematic) { AppliedAcceleration = Vector3.zero; return; }
            var gravity = GravitySource.AccelerationAt(body.position);
            LocalGravityAcceleration = gravity.magnitude;
            var mode = modes.FlightMode == FlightControlMode.PilotMastery ? NavigationFlightMode.PilotMastery :
                modes.FlightMode == FlightControlMode.Autopilot ? NavigationFlightMode.Autopilot : NavigationFlightMode.PilotAssist;
            var command = new FlightCommand(simulation.SessionId, ++commandSequence, mode, Numerics(localIntent),
                Numerics(angularInput), Numerics(transform.InverseTransformDirection(autopilotDesiredVelocity ?? Vector3.zero)), brake.IsPressed(), boost.IsPressed());
            if (!simulation.Accept(command)) return;
            var propulsion = ShipPropulsion.Calculate(simulation.Ship, command,
                Numerics(transform.InverseTransformDirection(body.linearVelocity)),
                Numerics(transform.InverseTransformDirection(body.angularVelocity)),
                Numerics(transform.InverseTransformDirection(gravity)), Time.fixedDeltaTime, cruiseSpeed,
                acceleration, brakeAcceleration, boostMultiplier, Numerics(new Vector3(pitchRate, yawRate, rollRate) * Mathf.Deg2Rad), angularAcceleration * Mathf.Deg2Rad);
            var force = transform.TransformDirection(Unity(propulsion.Force));
            AppliedAcceleration = force / body.mass + gravity;
            body.AddForce(force + gravity * body.mass, ForceMode.Force);
            body.AddTorque(transform.TransformDirection(Unity(propulsion.Torque)), ForceMode.Force);
        }

        private void OnDestroy()
        {
            attitude?.Dispose();yawLift?.Dispose();thrust?.Dispose();strafe?.Dispose();
            move?.Dispose();
            look?.Dispose();
            lift?.Dispose();
            roll?.Dispose();
            boost?.Dispose();
            brake?.Dispose();
            previousFlightMode?.Dispose();
            nextFlightMode?.Dispose();
        }

        private GalaxyModeState EnsureModeState()
        {
            return ModeState ??= new GalaxyModeState();
        }
    }
}
