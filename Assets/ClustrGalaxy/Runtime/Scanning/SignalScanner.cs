using System;
using System.Collections.Generic;
using System.Linq;
using ClustrGalaxy.Gameplay;
using ClustrGalaxy.Data;
using ClustrGalaxy.Interaction;
using ClustrGalaxy.World;
using UnityEngine;
using UnityEngine.InputSystem;
using ClustrGalaxy.Core.Domain;
using ClustrGalaxy.Core.Survey;

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
        private SpatialSceneLoader dataLoader = null!;
        private GalaxyEntityRegistry registry = null!;
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
            ProbeTargeting targetController, IEnumerable<ScanRoute> initialRoutes = null)
        {
            modes = modeState ?? throw new ArgumentNullException(nameof(modeState));
            visibility = visibilityController ?? throw new ArgumentNullException(nameof(visibilityController));
            targeting = targetController ?? throw new ArgumentNullException(nameof(targetController));
            demoRoutes = initialRoutes?.ToArray() ?? Array.Empty<ScanRoute>();
            SetRoutes(demoRoutes);
        }

        public void SetRoutes(IEnumerable<ScanRoute> scanRoutes)
        {
            routes.Clear();
            if (scanRoutes != null) routes.AddRange(scanRoutes.Where(route => !string.IsNullOrWhiteSpace(route.Id)));
        }

        public void BindRegistry(GalaxyEntityRegistry entityRegistry)
        {
            registry = entityRegistry ?? throw new ArgumentNullException(nameof(entityRegistry));
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
                dataLoader.SceneCommitted -= OnSceneCommitted;
            }
            dataLoader = loader;
            if (dataLoader != null)
            {
                dataLoader.SceneCommitted += OnSceneCommitted;
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
            var session = visibility.SurveySession;
            if (target == null || session == null || !visibility.IsKnown(target))
            {
                State = SignalScanState.NoSignal;
                LastResult = ScanResult.NoSignal;
                return;
            }
            if (session.State.IsScanComplete((GalaxyEntityId)target.StableId))
            {
                State = SignalScanState.Complete;
                LastResult = ScanResult.AlreadyScannedResult;
                return;
            }

            CompleteAtomicScan(target, session);
        }

        private void CompleteAtomicScan(GalaxyEntity target, SurveySession session)
        {
            var membership = target.GetComponent<GalaxySurveyMembership>();
            var systemId = membership == null || string.IsNullOrWhiteSpace(membership.SystemStableId)
                ? default(GalaxyEntityId) : (GalaxyEntityId)membership.SystemStableId;
            var completedNow = !systemId.IsEmpty && IsSystemCompleteAfter(target.StableId, systemId, session);
            var intent = new CompleteScanIntent("scan:" + target.StableId, session.State.Snapshot, DateTime.UtcNow,
                (GalaxyEntityId)target.StableId, target.Kind,
                routes.Select(route => new SurveyRoute((GalaxyRouteId)route.Id, (GalaxyEntityId)route.SourceId,
                    (GalaxyEntityId)route.TargetId, route.Weight)),
                LoadedPostIds(target).Select(id => (GalaxyEntityId)id), systemId, completedNow);
            var outcome = visibility.ApplySurveyIntent(intent);
            if (!outcome.Succeeded)
            {
                State = SignalScanState.NoSignal;
                LastResult = ScanResult.NoSignal;
                return;
            }
            State = SignalScanState.Complete;
            LastResult = new ScanResult(State, !outcome.Changed,
                outcome.RevealedEntities.Select(id => id.Value).ToArray(),
                outcome.RevealedRoutes.Select(id => id.Value).ToArray());
        }

        private bool CanCharge(out SignalScanState failure)
        {
            if (modes == null || modes.WorldMode != GalaxyWorldMode.Survey || visibility == null ||
                visibility.SurveySession == null || targeting == null || targeting.Target == null ||
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

        private void OnSceneCommitted(SpatialScene _) => RebuildPinnedRoutes();

        private IEnumerable<string> LoadedPostIds(GalaxyEntity target)
        {
            if (target == null || target.Kind != GalaxyEntityKind.Subreddit) return Array.Empty<string>();
            EnsureRegistry();
            return registry.Entities
                .Where(entity => entity.Kind == GalaxyEntityKind.Post &&
                    entity.GetComponent<GalaxySurveyMembership>() != null &&
                    string.Equals(entity.GetComponent<GalaxySurveyMembership>().SystemStableId, target.StableId,
                        StringComparison.Ordinal))
                .Select(entity => entity.StableId)
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToArray();
        }

        private bool IsSystemCompleteAfter(string targetId, GalaxyEntityId systemId, SurveySession session)
        {
            EnsureRegistry();
            var children = registry.Entities.Where(entity => entity.Kind == GalaxyEntityKind.Post &&
                    entity.GetComponent<GalaxySurveyMembership>() != null &&
                    string.Equals(entity.GetComponent<GalaxySurveyMembership>().SystemStableId, systemId.Value,
                        StringComparison.Ordinal))
                .Select(entity => (GalaxyEntityId)entity.StableId).ToArray();
            return children.Length > 0 && children.All(id => id.Value == targetId || session.State.IsScanComplete(id));
        }

        private void EnsureRegistry()
        {
            if (registry == null)
            {
                registry = visibility?.Registry;
                if (registry == null)
                {
                    throw new InvalidOperationException("Signal scanner requires a survey visibility registry.");
                }
            }
        }

        private void OnDestroy()
        {
            if (dataLoader != null)
            {
                dataLoader.SceneCommitted -= OnSceneCommitted;
            }
            scanAction?.Dispose();
        }
    }
}
