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

        public static bool CanLabel(GalaxyWorldMode mode, bool known) =>
            mode == GalaxyWorldMode.Explore || known;

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

        public SurveyDiscoveryStore DiscoveryStore => discoveryStore;
        public SurveySnapshotKey PendingSnapshotKey => snapshotKey;
        public GalaxyWorldMode WorldMode => modes?.WorldMode ?? GalaxyWorldMode.Explore;
        public bool RequiresStartingSystemChoice =>
            modes != null && modes.WorldMode == GalaxyWorldMode.Survey && discoveryStore == null;

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

            var entities = UnityEngine.Object.FindObjectsByType<GalaxyEntity>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (var index = 0; index < entities.Length; index++)
            {
                var entity = entities[index];
                if (BelongsToSystem(entity, systemStableId) && CanSeedStartingKnowledge(entity))
                {
                    discoveryStore.DiscoverEntity(entity.StableId);
                }
            }
            discoveryStore.Save();
            ApplyVisibility();
            return true;
        }

        public bool CanRender(GalaxyEntity entity) => Can(entity, GalaxyDiscoveryVisibility.CanRender);
        public bool CanTarget(GalaxyEntity entity) => Can(entity, GalaxyDiscoveryVisibility.CanTarget);
        public bool CanLabel(GalaxyEntity entity) => Can(entity, GalaxyDiscoveryVisibility.CanLabel);
        public bool CanFastTravel(GalaxyEntity entity) => Can(entity, GalaxyDiscoveryVisibility.CanFastTravel);

        public bool IsKnown(GalaxyEntity entity) => entity != null && discoveryStore != null &&
            discoveryStore.IsEntityKnown(entity.StableId);

        public GalaxyEntity[] LoadedStartingSystems()
        {
            var candidates = UnityEngine.Object.FindObjectsByType<GalaxyEntity>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var systems = new System.Collections.Generic.List<GalaxyEntity>();
            for (var index = 0; index < candidates.Length; index++)
            {
                if (IsSubreddit(candidates[index]))
                {
                    systems.Add(candidates[index]);
                }
            }
            systems.Sort((left, right) => string.CompareOrdinal(left.StableId, right.StableId));
            return systems.ToArray();
        }

        public void ApplyVisibility()
        {
            var entities = UnityEngine.Object.FindObjectsByType<GalaxyEntity>(FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            for (var index = 0; index < entities.Length; index++)
            {
                var entity = entities[index];
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

            var routes = UnityEngine.Object.FindObjectsByType<GalaxySurveyRoute>(FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            for (var index = 0; index < routes.Length; index++)
            {
                var route = routes[index];
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

        private static bool TryGetLoadedSubreddit(string stableId, out GalaxyEntity entity)
        {
            entity = null;
            if (string.IsNullOrWhiteSpace(stableId))
            {
                return false;
            }

            var candidates = UnityEngine.Object.FindObjectsByType<GalaxyEntity>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            GalaxyEntity candidate = null;
            for (var index = 0; index < candidates.Length; index++)
            {
                if (string.Equals(candidates[index].StableId, stableId, StringComparison.Ordinal))
                {
                    candidate = candidates[index];
                    break;
                }
            }
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
