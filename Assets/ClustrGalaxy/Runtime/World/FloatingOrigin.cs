using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ClustrGalaxy.World
{
    [DefaultExecutionOrder(1000)]
    public sealed class FloatingOrigin : MonoBehaviour
    {
        [SerializeField] private float rebaseThreshold = 8000f;

        public DoubleVector3 LogicalOrigin { get; private set; } = DoubleVector3.Zero;
        public event Action<Vector3, DoubleVector3> Rebased = delegate { };

        private void FixedUpdate()
        {
            var shift = FloatingOriginMath.RequiredShift(transform.position, rebaseThreshold);
            if (shift != Vector3.zero)
            {
                Rebase(shift);
            }
        }

        public void Rebase(Vector3 shift)
        {
            if (shift == Vector3.zero)
            {
                return;
            }

            var roots = SceneManager.GetActiveScene().GetRootGameObjects();
            foreach (var root in roots)
            {
                var rigidbody = root.GetComponent<Rigidbody>();
                if (rigidbody != null)
                {
                    rigidbody.position += shift;
                }
                else
                {
                    root.transform.position += shift;
                }
            }

            ShiftWorldSpaceEffects(roots, shift);
            LogicalOrigin = LogicalOrigin.Add(-shift);
            Physics.SyncTransforms();
            Rebased(shift, LogicalOrigin);
        }

        private static void ShiftWorldSpaceEffects(GameObject[] roots, Vector3 shift)
        {
            foreach (var root in roots)
            {
                foreach (var line in root.GetComponentsInChildren<LineRenderer>(true))
                {
                    if (!line.useWorldSpace) continue;
                    var positions = new Vector3[line.positionCount];
                    line.GetPositions(positions);
                    for (var index = 0; index < positions.Length; index++) positions[index] += shift;
                    line.SetPositions(positions);
                }
                foreach (var trail in root.GetComponentsInChildren<TrailRenderer>(true))
                {
                    var positions = new Vector3[trail.positionCount];
                    trail.GetPositions(positions);
                    for (var index = 0; index < positions.Length; index++) positions[index] += shift;
                    trail.SetPositions(positions);
                }
                foreach (var system in root.GetComponentsInChildren<ParticleSystem>(true))
                {
                    if (system.main.simulationSpace != ParticleSystemSimulationSpace.World) continue;
                    var particles = new ParticleSystem.Particle[system.particleCount];
                    var count = system.GetParticles(particles);
                    for (var index = 0; index < count; index++) particles[index].position += shift;
                    system.SetParticles(particles, count);
                }
            }
        }
    }
}
