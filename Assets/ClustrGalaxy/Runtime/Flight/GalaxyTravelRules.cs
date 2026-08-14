using System;
using ClustrGalaxy.Gameplay;
using UnityEngine;

namespace ClustrGalaxy.Flight
{
    /// <summary>
    /// Pure travel policy shared by the map, HUD and probe controls.  It is
    /// deliberately stricter than targeting: comments are useful inspection
    /// targets but never spatial destinations for a ship.
    /// </summary>
    public static class GalaxyTravelRules
    {
        public const float DefaultClearance = 24f;
        public const float AutopilotMaximumSpeed = 92f;
        public const float AutopilotArrivalSpeed = 1.5f;

        public static bool IsNavigableEntityType(string entityType)
        {
            return string.Equals(entityType, "subreddit", StringComparison.Ordinal) ||
                   string.Equals(entityType, "subreddit star", StringComparison.Ordinal);
        }

        public static bool IsEligibleDestination(GalaxyWorldMode worldMode, bool known, string entityType) =>
            IsNavigableEntityType(entityType) &&
            (worldMode == GalaxyWorldMode.Explore || known);

        public static float ArrivalRadius(float collisionRadius, double softeningRadius, float clearance = DefaultClearance)
        {
            return Mathf.Max(0f, collisionRadius, (float)Math.Max(0d, softeningRadius)) +
                   Mathf.Max(0f, clearance);
        }

        public static Vector3 ArrivalAnchor(Vector3 destination, Vector3 approachOrigin, float arrivalRadius)
        {
            var direction = destination - approachOrigin;
            if (direction.sqrMagnitude < 0.0001f)
            {
                direction = Vector3.back;
            }
            return destination - direction.normalized * Mathf.Max(0f, arrivalRadius);
        }

        public static Vector3 AutopilotDesiredVelocity(Vector3 currentPosition, Vector3 arrivalAnchor,
            Vector3 currentVelocity, float maximumSpeed, float brakeAcceleration, float arrivalRadius)
        {
            var offset = arrivalAnchor - currentPosition;
            var distance = offset.magnitude;
            if (distance <= Mathf.Max(0f, arrivalRadius))
            {
                return Vector3.zero;
            }

            var maximum = Mathf.Max(0f, maximumSpeed);
            var braking = Mathf.Max(0.01f, brakeAcceleration);
            // v^2 = 2ad; retaining a short minimum speed avoids a permanent
            // near-zero approach from float rounding while still braking well
            // before the protected envelope.
            var stoppingSpeed = Mathf.Sqrt(2f * braking * Mathf.Max(0f, distance - arrivalRadius));
            var desiredSpeed = Mathf.Min(maximum, Mathf.Max(AutopilotArrivalSpeed, stoppingSpeed));
            return offset / distance * desiredSpeed;
        }

        public static bool HasArrived(Vector3 currentPosition, Vector3 arrivalAnchor, Vector3 velocity,
            float arrivalRadius, float maximumArrivalSpeed = AutopilotArrivalSpeed)
        {
            return Vector3.Distance(currentPosition, arrivalAnchor) <= Mathf.Max(0f, arrivalRadius) &&
                   velocity.magnitude <= Mathf.Max(0f, maximumArrivalSpeed);
        }
    }
}
