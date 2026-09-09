using System.Collections.Generic;
using UnityEngine;

namespace ClustrGalaxy.Simulation
{
    public sealed class GravitySource : MonoBehaviour
    {
        private static readonly List<GravitySource> Sources = new();

        private KeplerOrbit orbit;
        private ClustrGalaxy.World.DoubleVector3 logicalPosition;
        public void RefreshStaticPosition()
        {
            if (Definition != null && (orbit == null || !orbit.enabled))
                transform.position = logicalPosition.RelativeTo(ClustrGalaxy.World.FloatingOrigin.Active != null ?
                    ClustrGalaxy.World.FloatingOrigin.Active.LogicalOrigin : ClustrGalaxy.World.DoubleVector3.Zero);
        }
        public ClustrGalaxy.World.DoubleVector3 LogicalPosition => orbit != null ? orbit.LogicalPositionAt(orbit.SimulationTime) : logicalPosition;
        public Vector3 CenterVelocity => orbit != null ? orbit.WorldVelocity : Vector3.zero;
        public double MaximumSpeed { get; private set; }
        public static IReadOnlyList<GravitySource> ActiveSources => Sources;
        public ClustrGalaxy.Core.Navigation.CelestialBodyDefinition Definition { get; private set; }
        public double GravitationalParameter { get; private set; }
        public double SofteningRadius { get; private set; }
        public double MaximumAcceleration { get; private set; }

        public void Initialize(double gravitationalParameter, double softeningRadius, double maximumAcceleration, ClustrGalaxy.World.DoubleVector3? staticLogicalPosition = null)
        {
            GravitationalParameter = gravitationalParameter;
            SofteningRadius = softeningRadius;
            MaximumAcceleration = maximumAcceleration;
            logicalPosition = staticLogicalPosition ?? (ClustrGalaxy.World.FloatingOrigin.Active != null ? ClustrGalaxy.World.FloatingOrigin.Active.LogicalOrigin : ClustrGalaxy.World.DoubleVector3.Zero).Add(transform.position);
            var candidateOrbit = GetComponent<KeplerOrbit>();
            orbit = candidateOrbit != null && candidateOrbit.enabled ? candidateOrbit : null;
            MaximumSpeed = orbit != null ? orbit.MaximumSpeed : 0d;
            var entity = GetComponent<ClustrGalaxy.World.GalaxyEntity>();
            var radius = entity != null ? entity.SurfaceRadius : 0f;
            Definition = radius > 0 ? new ClustrGalaxy.Core.Navigation.CelestialBodyDefinition(
                (ClustrGalaxy.Core.Domain.GalaxyEntityId)entity.StableId, radius, gravitationalParameter,
                radius + (entity.VisualKind == ClustrGalaxy.Core.Domain.GalaxyVisualKind.Star ? ClustrGalaxy.Generation.CelestialLayout.StellarClearance : 0d)) : null;
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
