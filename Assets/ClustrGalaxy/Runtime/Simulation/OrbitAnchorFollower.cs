using UnityEngine;

namespace ClustrGalaxy.Simulation
{
    public sealed class OrbitAnchorFollower : MonoBehaviour
    {
        private Transform anchor = null!;

        public void Initialize(Transform orbitAnchor)
        {
            anchor = orbitAnchor;
            ApplyPosition();
        }

        private void LateUpdate() => ApplyPosition();

        private void ApplyPosition()
        {
            if (anchor != null)
            {
                transform.position = anchor.position;
            }
        }
    }
}
