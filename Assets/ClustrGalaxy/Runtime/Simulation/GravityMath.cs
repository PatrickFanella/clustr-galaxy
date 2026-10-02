using System;
using UnityEngine;

namespace ClustrGalaxy.Simulation
{
    public static class GravityMath
    {
        public static Vector3 Acceleration(
            Vector3 observerPosition,
            Vector3 sourcePosition,
            double gravitationalParameter,
            double softeningRadius,
            double maximumAcceleration)
        {
            if (gravitationalParameter <= 0d || maximumAcceleration <= 0d)
            {
                return Vector3.zero;
            }

            var towardSource = sourcePosition - observerPosition;
            var distanceSquared = (double)towardSource.sqrMagnitude;
            if (distanceSquared <= double.Epsilon)
            {
                return Vector3.zero;
            }

            var softening = Math.Max(0d, softeningRadius);
            var denominator = Math.Pow(
                Math.Max(distanceSquared, softening * softening),
                1.5d);
            var scale = gravitationalParameter / denominator;
            var acceleration = towardSource * (float)scale;
            return Vector3.ClampMagnitude(acceleration, (float)maximumAcceleration);
        }
    }
}
