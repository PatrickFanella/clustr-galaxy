using ClustrGalaxy.World;
using UnityEngine;

namespace ClustrGalaxy.Interaction
{
    public sealed class CommentThreadController : MonoBehaviour
    {
        private ProbeTargeting targeting = null!;
        private CommentThreadNode expanded = null!;

        public void Initialize(ProbeTargeting probeTargeting)
        {
            targeting = probeTargeting;
            targeting.TargetChanged += OnTargetChanged;
        }

        private void OnTargetChanged(GalaxyEntity entity)
        {
            if (expanded != null)
            {
                expanded.SetExpanded(false);
            }

            expanded = entity != null ? entity.GetComponent<CommentThreadNode>() : null;
            if (expanded != null)
            {
                expanded.SetExpanded(true);
            }
        }

        private void OnDestroy()
        {
            if (targeting != null)
            {
                targeting.TargetChanged -= OnTargetChanged;
            }
        }
    }
}
