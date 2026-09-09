using ClustrGalaxy.Generation;
using UnityEngine;
using ClustrGalaxy.World;

namespace ClustrGalaxy.Simulation
{
    public sealed class KeplerOrbit : MonoBehaviour
    {
        public static bool CaptureTiming { get; set; }
        public static long AppliedOrbitTicks { get; private set; }
        public static long AppliedOrbitCount { get; private set; }
        private Transform bodyTransform;
        private void Awake() => bodyTransform = transform;
        private Transform parentBody = null!;
        private OrbitalElements elements;
        private KeplerOrbitMath.PreparedOrbit ephemeris;
        private double cachedLocalTime = double.NaN;
        private DoubleVector3 cachedLocalPosition;
        private double parentGravitationalParameter;
        private double timeScale = 1d;
        private KeplerOrbit parentOrbit;
        private DoubleVector3 parentLogicalPosition;
        public double MaximumSpeed => elements.SemiMajorAxis > 0 ?
            System.Math.Sqrt(parentGravitationalParameter / elements.SemiMajorAxis * (1d + elements.Eccentricity) / (1d - elements.Eccentricity)) * timeScale + (parentOrbit != null ? parentOrbit.MaximumSpeed : 0d) : 0d;
        public Transform ParentBody => parentBody;
        public double ParentMu => parentGravitationalParameter;
        public DoubleVector3 LogicalPositionAt(double time)
        {
            if (time != cachedLocalTime)
            {
                cachedLocalPosition = ephemeris.PositionAtTime(time * timeScale);
                cachedLocalTime = time;
            }
            // Cache only the local offset: parent reinitialization and rebasing must remain visible.
            return (parentOrbit != null ? parentOrbit.LogicalPositionAt(time) : parentLogicalPosition).Add(cachedLocalPosition);
        }

        public OrbitalElements Elements => elements;
        public double SimulationTime { get; private set; }
        public Vector3 WorldVelocity
        {
            get
            {
                var time = Time.timeAsDouble * timeScale;
                var before = ephemeris.PositionAtTime(time - .01d * timeScale);
                var after = ephemeris.PositionAtTime(time + .01d * timeScale);
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
            bodyTransform = transform; // Initialize may run while a Survey body is inactive, before Awake.
            parentBody = parent;
            parentOrbit = parent.GetComponent<KeplerOrbit>();
            if (parentOrbit != null && !parentOrbit.enabled) parentOrbit = null;
            var fixedParent = parent.GetComponent<GravitySource>();
            parentLogicalPosition = fixedParent != null ? fixedParent.LogicalPosition : (FloatingOrigin.Active != null ? FloatingOrigin.Active.LogicalOrigin : DoubleVector3.Zero).Add(parent.position);
            elements = orbitalElements;
            parentGravitationalParameter = parentMu;
            timeScale = simulationTimeScale;
            ephemeris = new KeplerOrbitMath.PreparedOrbit(elements, parentGravitationalParameter);
            cachedLocalTime = double.NaN;
            ApplyPosition(Time.timeAsDouble);
        }

        public void RefreshPosition() => ApplyPosition(SimulationTime);
        private void FixedUpdate()
        {
            if (!CaptureTiming) { ApplyPosition(Time.timeAsDouble); return; }
            var started = System.Diagnostics.Stopwatch.GetTimestamp();
            ApplyPosition(Time.timeAsDouble);
            AppliedOrbitTicks += System.Diagnostics.Stopwatch.GetTimestamp() - started;
            AppliedOrbitCount++;
        }

        private void ApplyPosition(double time)
        {
            if (parentBody == null)
            {
                return;
            }

            SimulationTime = time;
            bodyTransform.position = LogicalPositionAt(time).RelativeTo(
                FloatingOrigin.Active != null ? FloatingOrigin.Active.LogicalOrigin : DoubleVector3.Zero);
        }
    }
}
