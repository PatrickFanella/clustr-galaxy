using UnityEngine;

namespace ClustrGalaxy.World
{
    public sealed class DeterministicOrbit : MonoBehaviour
    {
        private Transform center = null!;
        private float radius;
        private float radiansPerSecond;
        private float phase;
        private float verticalScale;

        public void Initialize(Transform orbitCenter, float orbitRadius, float periodSeconds, float phaseRadians, float inclination)
        {
            center = orbitCenter;
            radius = orbitRadius;
            radiansPerSecond = Mathf.PI * 2f / Mathf.Max(1f, periodSeconds);
            phase = phaseRadians;
            verticalScale = Mathf.Sin(inclination * Mathf.Deg2Rad);
            ApplyPosition(0f);
        }

        private void Update() => ApplyPosition(Time.timeAsDouble);

        private void ApplyPosition(double time)
        {
            if (center == null)
            {
                return;
            }

            var angle = phase + (float)time * radiansPerSecond;
            transform.position = center.position + new Vector3(
                Mathf.Cos(angle) * radius,
                Mathf.Sin(angle) * radius * verticalScale,
                Mathf.Sin(angle) * radius);
        }
    }
}
