using System.Collections.Generic;
using UnityEngine;

namespace ClustrGalaxy.Simulation
{
    public sealed class GravitySource : MonoBehaviour
    {
        private static readonly List<GravitySource> Sources = new();

        public double GravitationalParameter { get; private set; }
        public double SofteningRadius { get; private set; }
        public double MaximumAcceleration { get; private set; }

        public void Initialize(double gravitationalParameter, double softeningRadius, double maximumAcceleration)
        {
            GravitationalParameter = gravitationalParameter;
            SofteningRadius = softeningRadius;
            MaximumAcceleration = maximumAcceleration;
        }

        public static Vector3 AccelerationAt(Vector3 position, float totalCap = 16f)
        {
            var acceleration = Vector3.zero;
            for (var index = 0; index < Sources.Count; index++)
            {
                var source = Sources[index];
                if (source == null || !source.isActiveAndEnabled)
                {
                    continue;
                }

                acceleration += GravityMath.Acceleration(
                    position,
                    source.transform.position,
                    source.GravitationalParameter,
                    source.SofteningRadius,
                    source.MaximumAcceleration);
            }

            return Vector3.ClampMagnitude(acceleration, totalCap);
        }

        private void OnEnable()
        {
            if (!Sources.Contains(this))
            {
                Sources.Add(this);
            }
        }

        private void OnDisable() => Sources.Remove(this);
    }
}
