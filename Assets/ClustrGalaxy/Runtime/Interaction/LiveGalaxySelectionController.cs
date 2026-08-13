using System;
using ClustrGalaxy.Data;
using ClustrGalaxy.World;
using UnityEngine;

namespace ClustrGalaxy.Interaction
{
    public sealed class LiveGalaxySelectionController : MonoBehaviour
    {
        private ProbeTargeting targeting = null!;
        private SpatialSceneLoader loader = null!;

        public void Initialize(ProbeTargeting probeTargeting, SpatialSceneLoader sceneLoader)
        {
            targeting = probeTargeting;
            loader = sceneLoader;
            targeting.TargetChanged += InspectTarget;
        }

        private void InspectTarget(GalaxyEntity target)
        {
            var metadata = target != null ? target.GetComponent<LiveEntityMetadata>() : null;
            if (metadata?.Node == null || loader.Manifest == null)
            {
                return;
            }

            try
            {
                switch (metadata.Node.type)
                {
                    case "community":
                        loader.LoadCommunity(metadata.Node.id);
                        break;
                    case "subreddit":
                        loader.LoadSystem(metadata.Node.id);
                        break;
                    case "post":
                        loader.LoadDiscussion(metadata.Node.id);
                        break;
                    case "comment" when metadata.Node.metrics.reply_count > 0:
                        loader.LoadThread(metadata.Node.id);
                        break;
                }
            }
            catch (InvalidOperationException exception)
            {
                Debug.LogWarning($"[ClustrGalaxy.Selection] {exception.Message}");
            }
        }

        private void OnDestroy()
        {
            if (targeting != null)
            {
                targeting.TargetChanged -= InspectTarget;
            }
        }
    }
}
