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
        private bool selectingArrival;
        public void SelectArrivalTarget(GalaxyEntity target)
        {
            selectingArrival = true;
            try { targeting.SelectTarget(target); }
            finally { selectingArrival = false; }
        }

        public void Initialize(ProbeTargeting probeTargeting, SpatialSceneLoader sceneLoader,
            GalaxyNavigationController navigationController)
        {
            targeting = probeTargeting;
            loader = sceneLoader;
            navigation = navigationController;
            targeting.TargetChanged += InspectTarget;
            navigation.ScopeRequested += LoadRequestedScope;
        }

        private void InspectTarget(GalaxyEntity target)
        {
            if (selectingArrival) return;
            var metadata = target != null ? target.GetComponent<LiveEntityMetadata>() : null;
            if (metadata?.Node == null || loader.Manifest == null)
            {
                return;
            }

            try
            {
                navigation.NavigateTo(navigation.Level, metadata.Node.id);
            }
            catch (InvalidOperationException exception)
            {
                Debug.LogWarning($"[ClustrGalaxy.Selection] {exception.Message}");
            }
        }

        private void LoadRequestedScope(ClustrGalaxy.Core.Data.SpatialScopeRequest scope) => loader.LoadScope(scope);

        private void OnDestroy()
        {
            if (navigation != null) navigation.ScopeRequested -= LoadRequestedScope;
            if (targeting != null)
            {
                targeting.TargetChanged -= InspectTarget;
            }
        }
    }
}
