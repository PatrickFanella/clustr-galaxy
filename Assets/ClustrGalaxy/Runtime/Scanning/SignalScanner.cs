using System;
using System.Collections.Generic;
using System.Linq;
using ClustrGalaxy.Gameplay;
using ClustrGalaxy.Data;
using ClustrGalaxy.Interaction;
using ClustrGalaxy.World;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ClustrGalaxy.Scanning
{
    /// <summary>
    /// Input and range adapter around <see cref="ScanRules"/>. The only mutation
    /// point is a completed, uninterrupted Survey-mode charge.
    /// </summary>
    public sealed class SignalScanner : MonoBehaviour
    {
        public const float RequiredHoldSeconds = 1.5f;

        [SerializeField] private float maximumScanRange = 3000f;
        private readonly List<ScanRoute> routes = new();
        private ScanRoute[] demoRoutes = Array.Empty<ScanRoute>();
        private InputAction scanAction = null!;
        private GalaxyModeState modes = null!;
        private GalaxySurveyVisibilityController visibility = null!;
        private ProbeTargeting targeting = null!;
        private SurveyEventBus eventBus = null!;
        private SpatialSceneLoader dataLoader = null!;
        private bool holding;
        private float chargeSeconds;

        public SignalScanState State { get; private set; } = SignalScanState.Idle;
        public float Charge01 => Mathf.Clamp01(chargeSeconds / RequiredHoldSeconds);
        public float SignalStrength01 { get; private set; }
        public ScanResult LastResult { get; private set; } = ScanResult.NoSignal;

        private void Awake()
        {
            scanAction = new InputAction("Scan", InputActionType.Button, "<Keyboard>/r");
            scanAction.AddBinding("<Gamepad>/buttonWest");
            scanAction.started += _ => BeginScan();
            scanAction.canceled += _ => CancelScan();
        }

        private void OnEnable() => scanAction?.Enable();
        private void OnDisable()
        {
            scanAction?.Disable();
            CancelScan();
        }

        public void Initialize(GalaxyModeState modeState, GalaxySurveyVisibilityController visibilityController,
            ProbeTargeting targetController, SurveyEventBus sharedEventBus, IEnumerable<ScanRoute> initialRoutes = null)
        {
            modes = modeState ?? throw new ArgumentNullException(nameof(modeState));
            visibility = visibilityController ?? throw new ArgumentNullException(nameof(visibilityController));
            targeting = targetController ?? throw new ArgumentNullException(nameof(targetController));
            eventBus = sharedEventBus ?? throw new ArgumentNullException(nameof(sharedEventBus));
            demoRoutes = initialRoutes?.ToArray() ?? Array.Empty<ScanRoute>();
            SetRoutes(demoRoutes);
        }

        public void SetRoutes(IEnumerable<ScanRoute> scanRoutes)
        {
            routes.Clear();
            if (scanRoutes != null) routes.AddRange(scanRoutes.Where(route => !string.IsNullOrWhiteSpace(route.Id)));
        }

        /// <summary>Uses exact renderer-compatible IDs: source:target:type.</summary>
        public static ScanRoute[] BuildPinnedRoutes(SpatialSceneStore store)
        {
            if (store == null) return Array.Empty<ScanRoute>();
            var catalog = new List<ScanRoute>();
            foreach (var link in store.Links.OrderBy(link => link.source, StringComparer.Ordinal)
                         .ThenBy(link => link.target, StringComparer.Ordinal).ThenBy(link => link.type, StringComparer.Ordinal))
            {
                var id = $"{link.source}:{link.target}:{link.type}";
                catalog.Add(new ScanRoute(id, link.source, link.target, link.weight));
                if (!link.directed)
                {
                    catalog.Add(new ScanRoute(id, link.target, link.source, link.weight));
                }
            }
            return catalog.ToArray();
        }

        public void BindDataLoader(SpatialSceneLoader loader)
        {
            if (dataLoader != null)
            {
                dataLoader.SceneLoaded -= RebuildPinnedRoutes;
                dataLoader.ScopeLoaded -= OnScopeLoaded;
            }
            dataLoader = loader;
            if (dataLoader != null)
            {
                dataLoader.SceneLoaded += RebuildPinnedRoutes;
                dataLoader.ScopeLoaded += OnScopeLoaded;
                RebuildPinnedRoutes();
            }
        }

        public void BeginScan()
        {
            UpdateSignalStrength();
            if (!CanCharge(out var failure))
            {
                State = failure;
                LastResult = failure == SignalScanState.OutOfRange
                    ? new ScanResult(failure, false, Array.Empty<string>(), Array.Empty<string>())
                    : ScanResult.NoSignal;
                holding = false;
                chargeSeconds = 0f;
                return;
            }
            holding = true;
            chargeSeconds = 0f;
            State = SignalScanState.Charging;
        }

        public void CancelScan()
        {
            if (!holding) return;
            holding = false;
            chargeSeconds = 0f;
            State = SignalScanState.Idle;
            UpdateSignalStrength();
        }

        /// <summary>Public for deterministic PlayMode tests; Update supplies Time.deltaTime.</summary>
        public void Tick(float deltaTime)
        {
            UpdateSignalStrength();
            if (!holding) return;
            if (!CanCharge(out var failure))
            {
                holding = false;
                chargeSeconds = 0f;
                State = failure;
                LastResult = failure == SignalScanState.OutOfRange
                    ? new ScanResult(failure, false, Array.Empty<string>(), Array.Empty<string>())
                    : ScanResult.NoSignal;
                return;
            }
            chargeSeconds += Mathf.Max(0f, deltaTime);
            if (chargeSeconds < RequiredHoldSeconds) return;
            holding = false;
            CompleteScan();
        }

        private void Update()
        {
            UpdateSignalStrength();
            Tick(Time.deltaTime);
        }

        private void CompleteScan()
        {
            var target = targeting.Target;
            var store = visibility.DiscoveryStore;
            if (target == null || store == null || !store.IsEntityKnown(target.StableId))
            {
                State = SignalScanState.NoSignal;
                LastResult = ScanResult.NoSignal;
                return;
            }
            if (store.IsScanComplete(target.StableId))
            {
                State = SignalScanState.Complete;
                LastResult = ScanResult.AlreadyScannedResult;
                return;
            }

            var result = ScanRules.Reveal(target.StableId, routes, store.State.scanner_level,
                store.State.known_entity_ids, store.State.known_route_ids, LoadedPostIds(target), IsSystemComplete(target, store));
            store.CompleteScan(target.StableId);
            store.IdentifyEntity(target.StableId);
            foreach (var entityId in result.RevealedEntityIds) store.DiscoverEntity(entityId);
            foreach (var routeId in result.RevealedRouteIds) store.DiscoverRoute(routeId);
            store.Save();
            visibility.ApplyVisibility();
            State = SignalScanState.Complete;
            LastResult = result;
            eventBus.Publish(SurveyEvent.ScanCompleted(store.SnapshotKey, target.StableId, result.RevealedEntityIds,
                result.RevealedRouteIds, DateTime.UtcNow, store.State.scanner_level));
            var membership = target.GetComponent<GalaxySurveyMembership>();
            if (membership != null && target.EntityType == "post planet" &&
                IsSystemComplete(membership.SystemStableId, store))
            {
                eventBus.Publish(SurveyEvent.SystemCompleted(store.SnapshotKey, membership.SystemStableId, DateTime.UtcNow));
            }
        }

        private bool CanCharge(out SignalScanState failure)
        {
            if (modes == null || modes.WorldMode != GalaxyWorldMode.Survey || visibility == null ||
                visibility.DiscoveryStore == null || targeting == null || targeting.Target == null ||
                !visibility.IsKnown(targeting.Target))
            {
                failure = SignalScanState.NoSignal;
                return false;
            }
            if (Vector3.Distance(transform.position, targeting.Target.transform.position) > maximumScanRange)
            {
                failure = SignalScanState.OutOfRange;
                return false;
            }
            failure = SignalScanState.Charging;
            return true;
        }

        public static float SignalStrengthForDistance(float distance, float range)
        {
            if (range <= 0f || float.IsNaN(distance) || float.IsInfinity(distance)) return 0f;
            return Mathf.Clamp01(1f - Mathf.Max(0f, distance) / range);
        }

        private void UpdateSignalStrength()
        {
            SignalStrength01 = targeting?.Target == null
                ? 0f
                : SignalStrengthForDistance(Vector3.Distance(transform.position, targeting.Target.transform.position), maximumScanRange);
        }

        private void RebuildPinnedRoutes()
        {
            if (dataLoader?.Store == null || !dataLoader.IsPinned)
            {
                SetRoutes(demoRoutes);
                return;
            }
            SetRoutes(BuildPinnedRoutes(dataLoader.Store));
        }

        private void OnScopeLoaded(SpatialScene _) => RebuildPinnedRoutes();

        private static IEnumerable<string> LoadedPostIds(GalaxyEntity target)
        {
            if (target == null || target.EntityType != "subreddit star") return Array.Empty<string>();
            return UnityEngine.Object.FindObjectsByType<GalaxyEntity>(FindObjectsInactive.Include)
                .Where(entity => entity.EntityType == "post planet" &&
                    entity.GetComponent<GalaxySurveyMembership>() != null &&
                    string.Equals(entity.GetComponent<GalaxySurveyMembership>().SystemStableId, target.StableId,
                        StringComparison.Ordinal))
                .Select(entity => entity.StableId)
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToArray();
        }

        private static bool IsSystemComplete(GalaxyEntity target, SurveyDiscoveryStore store)
        {
            var membership = target?.GetComponent<GalaxySurveyMembership>();
            return membership != null && IsSystemComplete(membership.SystemStableId, store);
        }

        private static bool IsSystemComplete(string systemStableId, SurveyDiscoveryStore store)
        {
            var children = UnityEngine.Object.FindObjectsByType<GalaxyEntity>(FindObjectsInactive.Include)
                .Where(entity => entity.EntityType == "post planet" && entity.GetComponent<GalaxySurveyMembership>() != null &&
                    string.Equals(entity.GetComponent<GalaxySurveyMembership>().SystemStableId, systemStableId, StringComparison.Ordinal))
                .Select(entity => entity.StableId).OrderBy(id => id, StringComparer.Ordinal).ToArray();
            return children.Length > 0 && children.All(store.IsScanComplete);
        }

        private void OnDestroy()
        {
            if (dataLoader != null)
            {
                dataLoader.SceneLoaded -= RebuildPinnedRoutes;
                dataLoader.ScopeLoaded -= OnScopeLoaded;
            }
            scanAction?.Dispose();
        }
    }
}
