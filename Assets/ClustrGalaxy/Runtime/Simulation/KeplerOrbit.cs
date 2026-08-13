using ClustrGalaxy.Generation;
using UnityEngine;

namespace ClustrGalaxy.Simulation
{
    public sealed class KeplerOrbit : MonoBehaviour
    {
        private Transform parentBody = null!;
        private OrbitalElements elements;
        private double parentGravitationalParameter;
        private double timeScale = 1d;

        public OrbitalElements Elements => elements;

        public void Initialize(
            Transform parent,
            OrbitalElements orbitalElements,
            double parentMu,
            double simulationTimeScale = 1d)
        {
            parentBody = parent;
            elements = orbitalElements;
            parentGravitationalParameter = parentMu;
            timeScale = simulationTimeScale;
            ApplyPosition(0d);
        }

        private void FixedUpdate() => ApplyPosition(Time.timeAsDouble * timeScale);

        private void ApplyPosition(double time)
        {
            if (parentBody == null)
            {
                return;
            }

            transform.position = parentBody.position + KeplerOrbitMath.PositionAtTime(
                elements, parentGravitationalParameter, time);
        }
    }
}
