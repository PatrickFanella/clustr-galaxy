using ClustrGalaxy.Data;
using ClustrGalaxy.Flight;
using ClustrGalaxy.UI;
using ClustrGalaxy.World;
using UnityEngine;

namespace ClustrGalaxy.Interaction
{
    public sealed class CommentThreadController : MonoBehaviour
    {
        private ProbeTargeting targeting = null!;
        private GalaxyNavigationController navigation = null!;
        private string inspectedRootId = string.Empty;

        public ThreadInspectionModel DisplayedModel { get; private set; } = null!;
        public event System.Action<ThreadInspectionModel> ModelChanged = delegate { };

        public void Initialize(ProbeTargeting probeTargeting)
        {
            targeting = probeTargeting;
            targeting.TargetChanged += OnTargetChanged;
        }

        public void BindNavigation(GalaxyNavigationController navigation)
        {
            this.navigation = navigation;
            this.navigation.ViewChanged += OnViewChanged;
        }

        /// <summary>Called by the materializer after the pinned thread scene is cached and merged.</summary>
        public void PresentThread(SpatialScene scene)
        {
            if (scene == null || scene.scope != "thread" ||
                !string.Equals(scene.root_id, inspectedRootId, System.StringComparison.Ordinal))
            {
                return;
            }

            SetModel(ThreadInspectionModel.Build(scene));
        }

        public void ClearInspection()
        {
            inspectedRootId = string.Empty;
            if (DisplayedModel != null)
            {
                DisplayedModel = null!;
                ModelChanged(null!);
            }
        }

        private void OnTargetChanged(GalaxyEntity entity)
        {
            ClearInspection();
            var thread = entity != null ? entity.GetComponent<CommentThreadNode>() : null;
            if (thread != null && thread.isActiveAndEnabled)
            {
                inspectedRootId = entity.StableId;
                if (thread.DemoModel != null)
                {
                    SetModel(thread.DemoModel);
                }
            }
        }

        private void OnViewChanged(GalaxyNavigationLevel level, string stableId)
        {
            if (level != GalaxyNavigationLevel.Inspect ||
                (!string.IsNullOrWhiteSpace(inspectedRootId) &&
                 !string.Equals(inspectedRootId, stableId, System.StringComparison.Ordinal)))
            {
                ClearInspection();
            }
        }

        private void SetModel(ThreadInspectionModel model)
        {
            DisplayedModel = model;
            ModelChanged(model);
        }

        private void OnDestroy()
        {
            if (targeting != null)
            {
                targeting.TargetChanged -= OnTargetChanged;
            }
            if (navigation != null)
            {
                navigation.ViewChanged -= OnViewChanged;
            }
        }
    }
}
