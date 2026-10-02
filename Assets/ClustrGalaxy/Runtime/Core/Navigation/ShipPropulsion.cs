using System;
using System.Numerics;

namespace ClustrGalaxy.Core.Navigation
{
    public sealed class ShipDefinition
    {
        public const string Version = "ship-v1";
        public static ShipDefinition Reference { get; } = new(100, new Vector3(200, 250, 160), new Vector3(24000), new Vector3(2513, 3142, 2011));
        public ShipDefinition(float mass, Vector3 inertia, Vector3 forceLimits, Vector3 torqueLimits)
        {
            if (!float.IsFinite(mass) || mass <= 0 || !Positive(inertia) || !Positive(forceLimits) || !Positive(torqueLimits))
                throw new ArgumentException("Ship mass, inertia and actuator limits must be positive and finite.");
            Mass = mass; Inertia = inertia; ForceLimits = forceLimits; TorqueLimits = torqueLimits;
        }
        public float Mass { get; }
        public Vector3 Inertia { get; }
        public Vector3 ForceLimits { get; }
        public Vector3 TorqueLimits { get; }
        private static bool Positive(Vector3 value) => Finite(value) && value.X > 0 && value.Y > 0 && value.Z > 0;
        internal static bool Finite(Vector3 value) => float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z);
    }

    public readonly struct FlightCommand
    {
        public FlightCommand(string sessionId, long sequence, NavigationFlightMode mode, Vector3 translation,
            Vector3 angularInput, Vector3 desiredVelocity, bool brake, bool boost)
        {
            if (string.IsNullOrEmpty(sessionId) || sequence <= 0 || !ShipDefinition.Finite(translation) ||
                !ShipDefinition.Finite(angularInput) || !ShipDefinition.Finite(desiredVelocity)) throw new ArgumentException("Invalid flight command.");
            SessionId = sessionId; Sequence = sequence; Mode = mode; Translation = translation;
            AngularInput = angularInput; DesiredVelocity = desiredVelocity; Brake = brake; Boost = boost;
        }
        public string SessionId { get; } public long Sequence { get; }
        public NavigationFlightMode Mode { get; }
        public Vector3 Translation { get; } public Vector3 AngularInput { get; } public Vector3 DesiredVelocity { get; }
        public bool Brake { get; } public bool Boost { get; }
    }

    public readonly struct PropulsionRequest
    {
        public PropulsionRequest(Vector3 force, Vector3 torque) { Force = force; Torque = torque; }
        public Vector3 Force { get; } public Vector3 Torque { get; }
    }

    public sealed class FlightSimulationState
    {
        public FlightSimulationState() { SessionId = Guid.NewGuid().ToString("N"); }
        public string SessionId { get; }
        public ShipDefinition Ship { get; } = ShipDefinition.Reference;
        public FlightCommand Command { get; private set; }
        public FlightTelemetry Telemetry { get; private set; }
        public void SetTelemetry(FlightTelemetry value) => Telemetry = value;
        public bool Recovering { get; private set; }
        public string RecoveryReason { get; private set; } = "";
        public ClustrGalaxy.Core.Domain.GalaxyEntityId RecoveryTarget { get; private set; }
        public bool Accept(FlightCommand command)
        {
            if (command.SessionId != SessionId || command.Sequence <= Command.Sequence || Recovering) return false;
            Command = command; return true;
        }
        public void BeginRecovery(string reason, ClustrGalaxy.Core.Domain.GalaxyEntityId target) { Recovering = true; RecoveryReason = reason; RecoveryTarget = target; }
        public void Resume() { Recovering = false; RecoveryReason = ""; RecoveryTarget = default; }
    }

    public static class ShipPropulsion
    {
        public static PropulsionRequest Allocate(ShipDefinition ship, PropulsionRequest request)
        {
            if (ship == null || !ShipDefinition.Finite(request.Force) || !ShipDefinition.Finite(request.Torque))
                throw new ArgumentException("Invalid propulsion request.");
            return new PropulsionRequest(Limit(request.Force, ship.ForceLimits), Limit(request.Torque, ship.TorqueLimits));
        }
        private static Vector3 Limit(Vector3 request, Vector3 envelope)
        {
            var normalized = request / envelope;
            return request / Math.Max(1f, normalized.Length());
        }
        public static PropulsionRequest Calculate(ShipDefinition ship, FlightCommand command,
            Vector3 velocity, Vector3 angularVelocity, Vector3 gravity, float dt, float cruiseSpeed,
            float acceleration, float braking, float boostMultiplier, Vector3 angularRates, float angularAcceleration)
        {
            if (!float.IsFinite(dt) || dt <= 0) throw new ArgumentOutOfRangeException(nameof(dt));
            var boost = command.Boost ? boostMultiplier : 1f;
            Vector3 requestedAcceleration;
            if (command.Mode == NavigationFlightMode.PilotMastery && !command.Brake)
                requestedAcceleration = command.Translation * acceleration * boost;
            else
            {
                var target = command.Brake ? Vector3.Zero : command.Mode == NavigationFlightMode.Autopilot
                    ? command.DesiredVelocity : command.Translation * cruiseSpeed * boost;
                requestedAcceleration = (target - velocity) / dt - gravity;
                var limit = command.Brake || command.Translation.LengthSquared() < .0001f ? braking : acceleration * boost;
                var length = requestedAcceleration.Length();
                if (length > limit) requestedAcceleration *= limit / length;
            }
            var targetAngular = command.Brake ? Vector3.Zero : command.AngularInput * angularRates;
            var angular = command.Mode == NavigationFlightMode.PilotMastery && !command.Brake
                ? command.AngularInput * angularAcceleration : (targetAngular - angularVelocity) / dt;
            if (angular.Length() > angularAcceleration) angular = Vector3.Normalize(angular) * angularAcceleration;
            return Allocate(ship, new PropulsionRequest(requestedAcceleration * ship.Mass, angular * ship.Inertia));
        }
    }
}
