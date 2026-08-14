using UnityEngine;
using ClustrGalaxy.Generation;
using ClustrGalaxy.Gameplay;

namespace ClustrGalaxy.Flight
{
    public static class ProbeFlightModel
    {
        public static float DesiredSpeed(float cruiseSpeed, float boostMultiplier, bool boost) =>
            Mathf.Max(0f, cruiseSpeed) * (boost ? Mathf.Max(1f, boostMultiplier) : 1f);

        public static float DesiredSpeed(bool boost) =>
            DesiredSpeed(GalaxyTuning.CruiseSpeed, GalaxyTuning.BoostMultiplier, boost);

        public static Vector3 MoveTowardsVelocity(
            Vector3 currentVelocity,
            Vector3 desiredVelocity,
            float acceleration,
            float deltaTime)
        {
            return Vector3.MoveTowards(
                currentVelocity,
                desiredVelocity,
                Mathf.Max(0f, acceleration) * Mathf.Max(0f, deltaTime));
        }

        /// <summary>
        /// Calculates the player's velocity before gravity is applied. Autopilot
        /// accepts a supplied desired velocity (or holds safely when none is
        /// supplied), Assist targets a capped velocity, and Mastery adds thrust
        /// without replacing existing momentum.
        /// </summary>
        public static Vector3 DesiredVelocity(
            FlightControlMode mode,
            Vector3 currentVelocity,
            Vector3 intent,
            float forwardSpeed,
            float deltaTime)
        {
            return DesiredVelocity(
                mode,
                currentVelocity,
                intent,
                forwardSpeed,
                GalaxyTuning.Acceleration,
                deltaTime);
        }

        /// <summary>
        /// Calculates velocity after pilot or autopilot input while gravity
        /// momentum is enabled. Unlike the normal assisted target-velocity
        /// behaviour, this adds bounded thrust to the existing velocity, so a
        /// gravity assist cannot be cancelled on the next physics tick.
        /// </summary>
        public static Vector3 VelocityWithMomentumPreserved(
            FlightControlMode mode,
            Vector3 currentVelocity,
            Vector3 intent,
            float forwardSpeed,
            float acceleration,
            float deltaTime)
        {
            var thrust = Vector3.zero;
            if (intent.sqrMagnitude > 0.0001f)
            {
                // Autopilot supplies a desired velocity while manual modes
                // supply a normalized control vector. Both become thrust here
                // rather than a replacement velocity.
                thrust = mode == FlightControlMode.Autopilot
                    ? intent.normalized
                    : Vector3.ClampMagnitude(intent, 1f);
            }

            var velocityDelta = thrust * Mathf.Max(0f, acceleration) * Mathf.Max(0f, deltaTime);
            var speed = Mathf.Max(0f, forwardSpeed);
            if (speed > 0f && currentVelocity.sqrMagnitude >= speed * speed &&
                Vector3.Dot(velocityDelta, currentVelocity) > 0f)
            {
                return currentVelocity;
            }

            return currentVelocity + velocityDelta;
        }

        public static Vector3 DesiredVelocity(
            FlightControlMode mode,
            Vector3 currentVelocity,
            Vector3 intent,
            float forwardSpeed,
            float acceleration,
            float deltaTime)
        {
            var speed = Mathf.Max(0f, forwardSpeed);
            var rate = Mathf.Max(0f, acceleration);
            var step = rate * Mathf.Max(0f, deltaTime);

            switch (mode)
            {
                case FlightControlMode.Autopilot:
                    // Intent is an explicitly supplied desired velocity, rather
                    // than player thrust. No target means a safe damping hold.
                    return intent.sqrMagnitude > 0.0001f
                        ? MoveTowardsVelocity(currentVelocity, Vector3.ClampMagnitude(intent, speed), rate, deltaTime)
                        : MoveTowardsVelocity(currentVelocity, Vector3.zero, rate, deltaTime);

                case FlightControlMode.PilotMastery:
                    // Do not use velocity targeting here: mastery preserves the
                    // velocity already accumulated by the pilot and gravity.
                    var masteryAcceleration = Vector3.ClampMagnitude(intent, 1f) * step;
                    if (speed > 0f && currentVelocity.sqrMagnitude >= speed * speed &&
                        Vector3.Dot(masteryAcceleration, currentVelocity) > 0f)
                    {
                        return currentVelocity;
                    }

                    return currentVelocity + masteryAcceleration;

                default:
                    return MoveTowardsVelocity(
                        currentVelocity,
                        Vector3.ClampMagnitude(intent, 1f) * speed,
                        rate,
                        deltaTime);
            }
        }

        public static float ApplyDeadzone(float value, float deadzone = 0.12f)
        {
            var magnitude = Mathf.Abs(value);
            if (magnitude <= deadzone)
            {
                return 0f;
            }

            return Mathf.Sign(value) * Mathf.InverseLerp(deadzone, 1f, magnitude);
        }
    }
}
