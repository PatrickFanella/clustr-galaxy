using UnityEngine;
using ClustrGalaxy.Generation;

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
