using System;
using System.Numerics;
using ClustrGalaxy.Core.Domain;

namespace ClustrGalaxy.Core.Navigation
{
    /// <summary>Session-owned, anchor-relative circular-orbit capture guidance.</summary>
    public sealed class OrbitalAutopilot
    {
        public GalaxyEntityId TargetId { get; private set; }
        public bool Active => !TargetId.IsEmpty;
        public float Radius { get; private set; }
        public Vector3 Normal { get; private set; }
        public bool Captured { get; private set; }
        public float Altitude { get; private set; }
        public float CircularSpeed { get; private set; }
        public float RadialSpeed { get; private set; }
        private double mu;
        private float bodyRadius;

        public bool Begin(GalaxyEntityId id, Vector3 relativePosition, Vector3 relativeVelocity,
            double gravitationalParameter, float collisionRadius, double minimumRadius = 0,
            double maximumRadius = double.PositiveInfinity, double availableAcceleration = double.PositiveInfinity)
        {
            if (id.IsEmpty || !Finite(relativePosition) || !Finite(relativeVelocity) ||
                double.IsNaN(gravitationalParameter) || double.IsInfinity(gravitationalParameter) || gravitationalParameter <= 0 ||
                float.IsNaN(collisionRadius) || float.IsInfinity(collisionRadius) || collisionRadius <= 0 ||
                relativePosition.Length() <= collisionRadius) return false;
            var minimum = Math.Max(collisionRadius * 1.6d, minimumRadius);
            var maximum = Math.Min(collisionRadius * 3d, maximumRadius);
            if (!double.IsFinite(minimum) || double.IsNaN(maximum) || maximum < minimum || availableAcceleration <= 0 || double.IsNaN(availableAcceleration)) return false;
            var targetRadius = Math.Clamp(relativePosition.Length(), minimum, maximum);
            if (gravitationalParameter / (targetRadius * targetRadius) > availableAcceleration * .8d) return false;
            TargetId = id; mu = gravitationalParameter; bodyRadius = collisionRadius;
            Radius = (float)targetRadius;
            var normal = Vector3.Cross(relativePosition, relativeVelocity);
            if (normal.LengthSquared() < .001f) normal = Vector3.Cross(relativePosition, Vector3.UnitY);
            if (normal.LengthSquared() < .001f) normal = Vector3.Cross(relativePosition, Vector3.UnitX);
            Normal = Vector3.Normalize(normal); Captured = false;
            return true;
        }

        public Vector3 DesiredRelativeVelocity(Vector3 relativePosition, Vector3 relativeVelocity)
        {
            var radius = relativePosition.Length();
            if (!Active || !Finite(relativePosition) || !Finite(relativeVelocity) || radius <= bodyRadius)
            { Cancel(); return Vector3.Zero; }
            var radial = relativePosition / radius;
            var tangent = Vector3.Cross(Normal, radial);
            if (tangent.LengthSquared() < .001f) { Cancel(); return Vector3.Zero; }
            tangent = Vector3.Normalize(tangent);
            CircularSpeed = (float)Math.Sqrt(mu / radius);
            RadialSpeed = Vector3.Dot(relativeVelocity, radial);
            Altitude = radius - bodyRadius;
            var correction = Math.Clamp((Radius - radius) * .25f, -20f, 20f);
            var planeCorrection = Math.Clamp(-Vector3.Dot(relativePosition, Normal) * .25f, -20f, 20f);
            Captured = Math.Abs(radius - Radius) < Math.Max(2f, Radius * .015f) &&
                Math.Abs(RadialSpeed) < 1f && Math.Abs(Vector3.Dot(relativeVelocity, tangent) - CircularSpeed) < 1f;
            return tangent * CircularSpeed + radial * correction + Normal * planeCorrection;
        }

        public void Cancel() { TargetId = default; Captured = false; }
        private static bool Finite(Vector3 value) => !(float.IsNaN(value.X) || float.IsNaN(value.Y) || float.IsNaN(value.Z) ||
            float.IsInfinity(value.X) || float.IsInfinity(value.Y) || float.IsInfinity(value.Z));
    }
}
