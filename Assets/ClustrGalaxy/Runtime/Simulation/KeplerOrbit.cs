using ClustrGalaxy.Generation;
using UnityEngine;
using ClustrGalaxy.World;

namespace ClustrGalaxy.Simulation
{
    public sealed class KeplerOrbit : MonoBehaviour
    {
        private Transform parentBody = null!;
        private OrbitalElements elements;
        private double parentGravitationalParameter;
        private double timeScale = 1d;
        private KeplerOrbit parentOrbit;
        private DoubleVector3 parentLogicalPosition;
        public double MaximumSpeed => elements.SemiMajorAxis > 0 ?
            System.Math.Sqrt(parentGravitationalParameter / elements.SemiMajorAxis * (1d + elements.Eccentricity) / (1d - elements.Eccentricity)) * timeScale + (parentOrbit != null ? parentOrbit.MaximumSpeed : 0d) : 0d;
        public Transform ParentBody => parentBody;
        public double ParentMu => parentGravitationalParameter;
        public DoubleVector3 LogicalPositionAt(double time) =>
            (parentOrbit != null ? parentOrbit.LogicalPositionAt(time) : parentLogicalPosition)
                .Add(KeplerOrbitMath.PrecisePositionAtTime(elements, parentGravitationalParameter, time * timeScale));

        public OrbitalElements Elements => elements;
        public double SimulationTime { get; private set; }
        public Vector3 WorldVelocity
        {
            get
            {
                var time = Time.timeAsDouble * timeScale;
                var before = KeplerOrbitMath.PrecisePositionAtTime(elements, parentGravitationalParameter, time - .01d * timeScale);
                var after = KeplerOrbitMath.PrecisePositionAtTime(elements, parentGravitationalParameter, time + .01d * timeScale);
                var velocity = after.RelativeTo(before) / .02f;
                return velocity + (parentOrbit != null ? parentOrbit.WorldVelocity : Vector3.zero);
            }
        }

        public void Initialize(
            Transform parent,
            OrbitalElements orbitalElements,
            double parentMu,
            double simulationTimeScale = 1d)
        {
            parentBody = parent;
            parentOrbit = parent.GetComponent<KeplerOrbit>();
            if (parentOrbit != null && !parentOrbit.enabled) parentOrbit = null;
            var fixedParent = parent.GetComponent<GravitySource>();
            parentLogicalPosition = fixedParent != null ? fixedParent.LogicalPosition : (FloatingOrigin.Active != null ? FloatingOrigin.Active.LogicalOrigin : DoubleVector3.Zero).Add(parent.position);
            elements = orbitalElements;
            parentGravitationalParameter = parentMu;
            timeScale = simulationTimeScale;
            ApplyPosition(Time.timeAsDouble);
        }

        public void RefreshPosition() => ApplyPosition(SimulationTime);
        private void FixedUpdate() => ApplyPosition(Time.timeAsDouble);

        private void ApplyPosition(double time)
        {
            if (parentBody == null)
            {
                return;
            }

            SimulationTime = time;
            transform.position = LogicalPositionAt(time).RelativeTo(
                FloatingOrigin.Active != null ? FloatingOrigin.Active.LogicalOrigin : DoubleVector3.Zero);
        }
    }
}
