using System;
using System.Numerics;
using ClustrGalaxy.Core.Domain;

namespace ClustrGalaxy.Core.Navigation
{
    public readonly struct SimulationPosition
    {
        public SimulationPosition(double x, double y, double z)
        {
            if (!double.IsFinite(x) || !double.IsFinite(y) || !double.IsFinite(z) || Math.Abs(x) > 1e15 || Math.Abs(y) > 1e15 || Math.Abs(z) > 1e15)
                throw new ArgumentException("Invalid logical position.");
            X = x; Y = y; Z = z;
        }
        public double X { get; } public double Y { get; } public double Z { get; }
        public SimulationPosition Add(Vector3 offset) => new(X + offset.X, Y + offset.Y, Z + offset.Z);
        public Vector3 RelativeTo(SimulationPosition other) => new((float)(X - other.X), (float)(Y - other.Y), (float)(Z - other.Z));
    }
    public readonly struct CruiseTarget
    {
        public CruiseTarget(GalaxyEntityId id, SimulationPosition position, Vector3 velocity, double exclusionRadius)
        {
            if (id.IsEmpty || !ShipDefinition.Finite(velocity) || !double.IsFinite(exclusionRadius) || exclusionRadius <= 0) throw new ArgumentException("Invalid cruise destination.");
            Id = id; Position = position; Velocity = velocity; ExclusionRadius = exclusionRadius;
        }
        public GalaxyEntityId Id { get; } public SimulationPosition Position { get; }
        public Vector3 Velocity { get; } public double ExclusionRadius { get; }
    }
    public readonly struct CruiseContact
    {
        public CruiseContact(SimulationPosition safePosition, Vector3 centerVelocity)
        { Position = safePosition; CenterVelocity = centerVelocity; }
        public SimulationPosition Position { get; } public Vector3 CenterVelocity { get; }
    }
    public interface ICruiseExclusions
    {
        bool Sweep(SimulationPosition from, SimulationPosition to, double seconds, out CruiseContact contact);
    }
    public readonly struct CruiseStep
    {
        internal CruiseStep(SimulationPosition position, Vector3 velocity, bool approach, bool arrived, bool dropped, double clearance, double closing)
        { Position = position; Velocity = velocity; Approach = approach; Arrived = arrived; Dropped = dropped; Clearance = clearance; ClosingSpeed = closing; }
        public SimulationPosition Position { get; } public Vector3 Velocity { get; }
        public bool Approach { get; } public bool Arrived { get; } public bool Dropped { get; }
        public double Clearance { get; } public double ClosingSpeed { get; }
    }
    // This is the explicitly fictional drive, separate from Newtonian local propulsion.
    public sealed class SystemCruise
    {
        public const double StandOff = 300;
        public const float DriveAcceleration = 300;
        private readonly GalaxyEntityId targetId;
        private readonly double maximumSpeed;
        private Vector3 relativeVelocity;
        public SimulationPosition Position { get; private set; }
        public SystemCruise(SimulationPosition start, Vector3 velocity, CruiseTarget target)
        {
            if (!ShipDefinition.Finite(velocity)) throw new ArgumentException("Invalid departure velocity.");
            var distance = start.RelativeTo(target.Position).Length();
            if (distance <= target.ExclusionRadius + 2) throw new ArgumentException("Move clear of the exclusion volume before charging.");
            Position = start; targetId = target.Id; relativeVelocity = velocity - target.Velocity;
            maximumSpeed = Math.Clamp((distance - target.ExclusionRadius - StandOff) / 65d, 120, 4000);
        }
        public CruiseStep Advance(CruiseTarget target, double seconds, bool automatic, Vector3 heading,
            float throttle, bool brake, ICruiseExclusions exclusions)
        {
            if (target.Id != targetId || !double.IsFinite(seconds) || seconds <= 0 || seconds > 1 ||
                !ShipDefinition.Finite(heading) || !float.IsFinite(throttle) || exclusions == null)
                throw new ArgumentException("Invalid cruise step.");
            var offset = target.Position.RelativeTo(Position); var radius = offset.Length();
            var direction = radius > 1e-6 ? offset / radius : Vector3.UnitZ;
            var gap = Math.Max(0, radius - target.ExclusionRadius - StandOff);
            // One-step braking reserve keeps numerical integration outside the handoff boundary.
            var a = DriveAcceleration; var desiredSpeed = Math.Min(maximumSpeed,
                Math.Max(0, Math.Sqrt(2 * a * Math.Max(0, gap - 5) + a * a * seconds * seconds) - a * seconds));
            if (!automatic) desiredSpeed *= Math.Clamp(throttle, 0, 1);
            if (brake) desiredSpeed = 0;
            var desiredDirection = automatic ? direction : heading.LengthSquared() > 1e-8 ? Vector3.Normalize(heading) : direction;
            var desired = desiredDirection * (float)desiredSpeed;
            var change = desired - relativeVelocity; var limit = a * (float)seconds;
            if (change.LengthSquared() > limit * limit) change = Vector3.Normalize(change) * limit;
            relativeVelocity += change;
            var velocity = relativeVelocity + target.Velocity;
            var next = Position.Add(velocity * (float)seconds);
            if (exclusions.Sweep(Position, next, seconds, out var contact))
            {
                Position = contact.Position; relativeVelocity = Vector3.Zero;
                return new CruiseStep(Position, contact.CenterVelocity, false, false, true, 0, 0);
            }
            Position = next;
            var after = target.Position.Add(target.Velocity * (float)seconds).RelativeTo(Position);
            var afterRadius = after.Length();
            var closing = afterRadius > 1e-6 ? Vector3.Dot(relativeVelocity, after / afterRadius) : 0;
            var clearance = afterRadius - target.ExclusionRadius;
            var arrived = clearance > 0 && clearance <= StandOff + 12 && Math.Abs(closing) < 20;
            return new CruiseStep(Position, velocity, gap < 2500, arrived, false, clearance, closing);
        }
    }
}
