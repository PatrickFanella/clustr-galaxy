using System;
using ClustrGalaxy.Data;
using ClustrGalaxy.Gameplay;
using ClustrGalaxy.Interaction;
using UnityEngine;

namespace ClustrGalaxy.World
{
    /// <summary>
    /// The one information boundary between loaded factual graph data and what a
    /// Survey player is allowed to see or operate. It is intentionally pure so
    /// rendering, targeting, labels, and future travel controls share exactly
    /// the same rule.
    /// </summary>
    public static class GalaxyDiscoveryVisibility
    {
        public static bool CanRender(GalaxyWorldMode mode, bool known) =>
            mode == GalaxyWorldMode.Explore || known;

        public static bool CanTarget(GalaxyWorldMode mode, bool known) =>
            mode == GalaxyWorldMode.Explore || known;

        public static bool CanLabel(GalaxyWorldMode mode, bool identified) =>
            mode == GalaxyWorldMode.Explore || identified;

        public static bool CanFastTravel(GalaxyWorldMode mode, bool known) =>
            mode == GalaxyWorldMode.Explore || known;
    }

    public static class SurveyStartingKnowledge
    {
        public static bool CanSeed(string entityType, bool isNavigationAnchor) =>
            isNavigationAnchor || entityType == "subreddit star" || entityType == "post planet";
    }

    /// <summary>
    /// Stable metadata assigned to both fixture and streamed roots. It avoids
    /// deriving Survey knowledge from object names or transform hierarchy.
    /// </summary>
    public sealed class GalaxySurveyMembership : MonoBehaviour
    {
        public string SystemStableId { get; private set; } = string.Empty;
        public bool IsNavigationAnchor { get; private set; }

        public void Initialize(string systemStableId, bool isNavigationAnchor = false)
        {
            SystemStableId = systemStableId ?? string.Empty;
            IsNavigationAnchor = isNavigationAnchor;
        }

        public static void InitializeDescendants(Transform root, string systemStableId)
        {
            if (root == null)
            {
                return;
            }

            var entities = root.GetComponentsInChildren<GalaxyEntity>(includeInactive: true);
            for (var index = 0; index < entities.Length; index++)
            {
                var membership = entities[index].GetComponent<GalaxySurveyMembership>();
                if (membership == null)
                {
                    membership = entities[index].gameObject.AddComponent<GalaxySurveyMembership>();
                }
                membership.Initialize(systemStableId);
            }
        }
    }

    /// <summary>Metadata for route renderers; route IDs stay logical data IDs.</summary>
    public sealed class GalaxySurveyRoute : MonoBehaviour
    {
        public string StableId { get; private set; } = string.Empty;
        public string SourceId { get; private set; } = string.Empty;
        public string TargetId { get; private set; } = string.Empty;

        public void Initialize(string stableId, string sourceId, string targetId)
        {
            StableId = stableId ?? string.Empty;
            SourceId = sourceId ?? string.Empty;
            TargetId = targetId ?? string.Empty;
        }
    }

    /// <summary>
    /// Scene adapter for <see cref="GalaxyDiscoveryVisibility"/>. It keeps
    /// logical nodes in the scene store while deactivating unknown render roots,
    /// which also removes them from GalaxyEntity's targeting registry.
    /// </summary>
    public sealed class GalaxySurveyVisibilityController : MonoBehaviour
    {
        private GalaxyModeState modes = null!;
        private SurveyDiscoveryStore discoveryStore = null!;
        private SurveySnapshotKey snapshotKey;
        private string profileId = SurveyDiscoveryStore.DefaultProfileId;
        private string persistentDataRoot = string.Empty;
        private ProbeTargeting targeting = null!;
        private SpatialSceneLoader dataLoader = null!;
        private GalaxyEntityRegistry registry = null!;

        public SurveyDiscoveryStore DiscoveryStore => discoveryStore;
        public GalaxyEntityRegistry Registry
        {
            get
            {
                EnsureRegistry();
                return registry;
            }
        }
        public SurveySnapshotKey PendingSnapshotKey => snapshotKey;
        public GalaxyWorldMode WorldMode => modes?.WorldMode ?? GalaxyWorldMode.Explore;
        public bool RequiresStartingSystemChoice =>
            modes != null && modes.WorldMode == GalaxyWorldMode.Survey && discoveryStore == null;
        public event Action<SurveyDiscoveryStore> SurveyStarted = delegate { };

        public void Initialize(GalaxyModeState modeState, SurveyDiscoveryStore store)
        {
            if (modeState == null) throw new ArgumentNullException(nameof(modeState));
            if (store == null) throw new ArgumentNullException(nameof(store));
            Unsubscribe();
            modes = modeState;
            discoveryStore = store;
            snapshotKey = store.SnapshotKey;
            modes.WorldModeChanged += OnWorldModeChanged;
            ApplyVisibility();
        }

        public void Initialize(GalaxyModeState modeState, SurveySnapshotKey key,
            string surveyProfileId = SurveyDiscoveryStore.DefaultProfileId, string dataRoot = null)
        {
            if (modeState == null) throw new ArgumentNullException(nameof(modeState));
            Unsubscribe();
            modes = modeState;
            snapshotKey = key;
            profileId = string.IsNullOrWhiteSpace(surveyProfileId) ? SurveyDiscoveryStore.DefaultProfileId : surveyProfileId;
            persistentDataRoot = dataRoot ?? Application.persistentDataPath;
            discoveryStore = null!;
            modes.WorldModeChanged += OnWorldModeChanged;
            ApplyVisibility();
        }

        public void BindTargeting(ProbeTargeting targetController)
        {
            targeting = targetController;
            targeting?.Initialize(this);
            ApplyVisibility();
        }

        public void BindRegistry(GalaxyEntityRegistry entityRegistry)
        {
            registry = entityRegistry ?? throw new ArgumentNullException(nameof(entityRegistry));
            registry.RefreshFromScene();
            ApplyVisibility();
        }

        public void BindDataLoader(SpatialSceneLoader sceneLoader)
        {
            if (dataLoader != null) dataLoader.SceneLoaded -= UpdatePendingSnapshotKey;
            dataLoader = sceneLoader;
            if (dataLoader != null)
            {
                dataLoader.SceneLoaded += UpdatePendingSnapshotKey;
                UpdatePendingSnapshotKey();
            }
        }

        public void EnterSurvey() => modes?.SetWorldMode(GalaxyWorldMode.Survey);
        public void ReturnToExplore() => modes?.SetWorldMode(GalaxyWorldMode.Explore);

        public static SurveySnapshotKey SelectPendingSnapshotKey(SurveySnapshotKey current,
            bool surveyCommitted, SurveySnapshotKey? validatedPinnedKey) =>
            !surveyCommitted && validatedPinnedKey.HasValue ? validatedPinnedKey.Value : current;

        public bool StartAt(string systemStableId)
        {
            EnsureRegistry();
            registry.RefreshFromScene();
            if (!TryGetLoadedSubreddit(systemStableId, out _))
            {
                return false;
            }

            if (discoveryStore == null)
            {
                var opened = string.IsNullOrWhiteSpace(persistentDataRoot)
                    ? SurveyDiscoveryStore.OpenDefault(profileId, snapshotKey, systemStableId)
                    : SurveyDiscoveryStore.Open(persistentDataRoot, profileId, snapshotKey, systemStableId);
                if (!string.Equals(opened.StartingSystemId, systemStableId, StringComparison.Ordinal))
                {
                    return false;
                }
                discoveryStore = opened;
            }
            else if (!string.Equals(discoveryStore.StartingSystemId, systemStableId, StringComparison.Ordinal))
            {
                return false;
            }

            EnsureRegistry();
            for (var index = 0; index < registry.Entities.Count; index++)
            {
                var entity = registry.Entities[index];
                if (BelongsToSystem(entity, systemStableId) && CanSeedStartingKnowledge(entity))
                {
                    discoveryStore.IdentifyEntity(entity.StableId);
                }
            }
            discoveryStore.Save();
            ApplyVisibility();
            SurveyStarted(discoveryStore);
            return true;
        }

        public bool CanRender(GalaxyEntity entity) => Can(entity, GalaxyDiscoveryVisibility.CanRender);
        public bool CanTarget(GalaxyEntity entity) => Can(entity, GalaxyDiscoveryVisibility.CanTarget);
        public bool CanLabel(GalaxyEntity entity) => entity != null && GalaxyDiscoveryVisibility.CanLabel(
            modes?.WorldMode ?? GalaxyWorldMode.Explore,
            discoveryStore != null && discoveryStore.IsEntityIdentified(entity.StableId));
        public bool CanFastTravel(GalaxyEntity entity) => Can(entity, GalaxyDiscoveryVisibility.CanFastTravel);

        public bool IsKnown(GalaxyEntity entity) => entity != null && discoveryStore != null &&
            discoveryStore.IsEntityKnown(entity.StableId);

        public bool IsIdentified(GalaxyEntity entity) => entity != null && discoveryStore != null &&
            discoveryStore.IsEntityIdentified(entity.StableId);

        public GalaxyEntity[] LoadedStartingSystems()
        {
            var systems = new System.Collections.Generic.List<GalaxyEntity>();
            EnsureRegistry();
            for (var index = 0; index < registry.Entities.Count; index++)
            {
                if (IsSubreddit(registry.Entities[index]))
                {
                    systems.Add(registry.Entities[index]);
                }
            }
            systems.Sort((left, right) => string.CompareOrdinal(left.StableId, right.StableId));
            return systems.ToArray();
        }

        public void ApplyVisibility()
        {
            EnsureRegistry();
            registry.RefreshFromScene();
            for (var index = 0; index < registry.Entities.Count; index++)
            {
                var entity = registry.Entities[index];
                if (string.IsNullOrWhiteSpace(entity.StableId))
                {
                    continue;
                }

                var visible = CanRender(entity);
                if (entity.gameObject.activeSelf != visible)
                {
                    entity.gameObject.SetActive(visible);
                }
            }

            if (targeting != null && targeting.Target != null && !CanTarget(targeting.Target))
            {
                targeting.SelectTarget(null);
            }

            for (var index = 0; index < registry.Routes.Count; index++)
            {
                var route = registry.Routes[index];
                var known = discoveryStore != null && discoveryStore.IsRouteKnown(route.StableId);
                var visible = GalaxyDiscoveryVisibility.CanRender(modes?.WorldMode ?? GalaxyWorldMode.Explore, known);
                if (route.gameObject.activeSelf != visible)
                {
                    route.gameObject.SetActive(visible);
                }
            }
        }

        private bool Can(GalaxyEntity entity, Func<GalaxyWorldMode, bool, bool> gate) =>
            entity != null && gate(modes?.WorldMode ?? GalaxyWorldMode.Explore, IsKnown(entity));

        private static bool BelongsToSystem(GalaxyEntity entity, string systemStableId)
        {
            var membership = entity.GetComponent<GalaxySurveyMembership>();
            return string.Equals(entity.StableId, systemStableId, StringComparison.Ordinal) ||
                membership != null && string.Equals(membership.SystemStableId, systemStableId, StringComparison.Ordinal);
        }

        private static bool CanSeedStartingKnowledge(GalaxyEntity entity)
        {
            var membership = entity.GetComponent<GalaxySurveyMembership>();
            return SurveyStartingKnowledge.CanSeed(entity.EntityType, membership != null && membership.IsNavigationAnchor);
        }

        private void UpdatePendingSnapshotKey()
        {
            snapshotKey = SelectPendingSnapshotKey(snapshotKey, discoveryStore != null,
                dataLoader != null ? dataLoader.PinnedSurveySnapshotKey : null);
        }

        private bool TryGetLoadedSubreddit(string stableId, out GalaxyEntity entity)
        {
            entity = null;
            if (string.IsNullOrWhiteSpace(stableId))
            {
                return false;
            }

            EnsureRegistry();
            var candidate = registry.FindEntity(stableId);
            if (candidate == null)
            {
                return false;
            }

            if (!IsSubreddit(candidate))
            {
                return false;
            }

            entity = candidate;
            return true;
        }

        private static bool IsSubreddit(GalaxyEntity candidate)
        {
            var metadata = candidate.GetComponent<LiveEntityMetadata>();
            var isSubreddit = metadata?.Node != null
                ? metadata.Node.type == "subreddit"
                : candidate.EntityType == "subreddit star";
            return isSubreddit;
        }

        private void OnWorldModeChanged(GalaxyWorldMode _) => ApplyVisibility();

        private void EnsureRegistry()
        {
            if (registry == null)
            {
                // Compatibility for isolated PlayMode fixtures. Runtime setup
                // always injects the shared registry through the composition root.
                registry = GetComponent<GalaxyEntityRegistry>();
                if (registry == null) registry = gameObject.AddComponent<GalaxyEntityRegistry>();
                registry.RefreshFromScene();
            }
        }

        private void Unsubscribe()
        {
            if (modes != null)
            {
                modes.WorldModeChanged -= OnWorldModeChanged;
            }
        }

        private void OnDestroy()
        {
            Unsubscribe();
            if (dataLoader != null) dataLoader.SceneLoaded -= UpdatePendingSnapshotKey;
        }
    }
}
