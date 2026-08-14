using System;
using ClustrGalaxy.Data;
using ClustrGalaxy.Flight;
using ClustrGalaxy.World;
using UnityEngine;
using ClustrGalaxy.Core.Domain;

namespace ClustrGalaxy.Interaction
{
    public sealed class LiveGalaxySelectionController : MonoBehaviour
    {
        private ProbeTargeting targeting = null!;
        private SpatialSceneLoader loader = null!;
        private GalaxyNavigationController navigation = null!;

        public void Initialize(ProbeTargeting probeTargeting, SpatialSceneLoader sceneLoader,
            GalaxyNavigationController navigationController)
        {
            targeting = probeTargeting;
            loader = sceneLoader;
            navigation = navigationController;
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
                switch (target.Kind)
                {
                    case GalaxyEntityKind.Community:
                        navigation.NavigateTo(GalaxyNavigationLevel.Region, metadata.Node.id);
                        loader.LoadCommunity(metadata.Node.id);
                        break;
                    case GalaxyEntityKind.Subreddit:
                        navigation.NavigateTo(GalaxyNavigationLevel.System, metadata.Node.id);
                        loader.LoadSystem(metadata.Node.id);
                        break;
                    case GalaxyEntityKind.Post:
                        navigation.NavigateTo(GalaxyNavigationLevel.Inspect, metadata.Node.id);
                        loader.LoadDiscussion(metadata.Node.id);
                        break;
                    case GalaxyEntityKind.Comment when metadata.Node.metrics.reply_count > 0:
                        navigation.NavigateTo(GalaxyNavigationLevel.Inspect, metadata.Node.id);
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
