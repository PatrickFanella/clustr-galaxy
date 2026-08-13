using System;
using System.Collections;
using UnityEngine;

namespace ClustrGalaxy.Data
{
    public sealed class SpatialSceneLoader : MonoBehaviour
    {
        private SpatialSceneClient client = null!;
        private SnapshotCache cache = null!;
        private string apiBase = string.Empty;
        private bool offlineOnly;
        private bool started;

        public SpatialManifest Manifest { get; private set; } = null!;
        public SpatialScene Scene { get; private set; } = null!;
        public SpatialSceneStore Store { get; private set; } = null!;
        public string StatusText { get; private set; } = "LOCAL DEMO";
        public GalaxyDataMode DataMode { get; private set; } = GalaxyDataMode.LocalDemo;
        public bool IsOffline { get; private set; }
        public bool IsPinned => Manifest != null && Scene != null && Scene.BelongsTo(Manifest);
        public event Action SceneLoaded = delegate { };
        public event Action<SpatialScene> ScopeLoaded = delegate { };

        public void Initialize(string requestedApiBase, bool startOfflineOnly = false)
        {
            if (started)
            {
                return;
            }

            started = true;
            apiBase = requestedApiBase;
            offlineOnly = startOfflineOnly;
            client = new SpatialSceneClient();
            cache = new SnapshotCache(SnapshotCache.DefaultRoot);
            StartCoroutine(LoadOverview());
        }

        public void RefreshGalaxy()
        {
            if (!started)
            {
                return;
            }
            StopAllCoroutines();
            client.Cancel();
            Manifest = null!;
            Scene = null!;
            Store = null!;
            IsOffline = false;
            DataMode = GalaxyDataMode.Connecting;
            StartCoroutine(LoadOverview());
        }

        public void LoadCommunity(string communityId) =>
            BeginScopeLoad(SpatialSceneRequestUris.Community(apiBase, RequireManifest(), communityId),
                ScopeCacheKey("community", communityId), $"LOADING REGION {communityId}");

        public void LoadSystem(string subredditId) =>
            BeginScopeLoad(SpatialSceneRequestUris.System(apiBase, RequireManifest(), subredditId),
                ScopeCacheKey("system", subredditId), $"LOADING SYSTEM {subredditId}");

        public void LoadDiscussion(string postId) =>
            BeginScopeLoad(SpatialSceneRequestUris.Discussion(apiBase, RequireManifest(), postId),
                ScopeCacheKey("discussion", postId), $"LOADING DISCUSSION {postId}");

        public void LoadThread(string parentCommentId) =>
            BeginScopeLoad(SpatialSceneRequestUris.Thread(apiBase, RequireManifest(), parentCommentId),
                ScopeCacheKey("thread", parentCommentId), $"LOADING THREAD {parentCommentId}");

        private IEnumerator LoadOverview()
        {
            StatusText = "CONNECTING TO CLUSTR";
            DataMode = GalaxyDataMode.Connecting;
            string manifestJson = null!;
            SpatialRequestFailure? manifestFailure = null;
            if (offlineOnly)
            {
                if (!cache.TryRead("active-manifest", out manifestJson))
                {
                    IsOffline = true;
                    Fail("OFFLINE CACHE UNAVAILABLE");
                    yield break;
                }

                IsOffline = true;
            }
            else
            {
                yield return client.GetJson(
                    SpatialSceneRequestUris.Manifest(apiBase),
                    json => manifestJson = json,
                    failure => manifestFailure = failure);
            }

            var liveManifest = !string.IsNullOrWhiteSpace(manifestJson);
            if (!liveManifest && manifestFailure?.ResponseCode != 404)
            {
                if (cache.TryRead("active-manifest", out var cachedManifest))
                {
                    manifestJson = cachedManifest;
                    IsOffline = true;
                }
                else
                {
                    Fail($"MANIFEST {manifestFailure}");
                    yield break;
                }
            }

            if (!string.IsNullOrWhiteSpace(manifestJson))
            {
                try
                {
                    Manifest = JsonUtility.FromJson<SpatialManifest>(manifestJson);
                }
                catch (Exception exception)
                {
                    Fail($"MANIFEST JSON: {exception.Message}");
                    yield break;
                }
                if (Manifest == null || !Manifest.IsCompatible)
                {
                    Fail("INCOMPATIBLE MANIFEST");
                    yield break;
                }
            }

            StatusText = Manifest == null ? "LOADING LEGACY OVERVIEW" : $"LOADING REVISION {Manifest.revision_id}";
            var overviewUri = Manifest == null
                ? SpatialSceneRequestUris.LegacyOverview(apiBase)
                : SpatialSceneRequestUris.Overview(apiBase, Manifest);
            var overviewKey = Manifest == null ? string.Empty : ScopeCacheKey("overview", "root");
            string overviewJson = null!;
            SpatialRequestFailure? overviewFailure = null;
            if (offlineOnly)
            {
                cache.TryRead(overviewKey, out overviewJson);
            }
            else
            {
                yield return client.GetJson(overviewUri, json => overviewJson = json, failure => overviewFailure = failure);
            }

            if (string.IsNullOrWhiteSpace(overviewJson) && Manifest != null && cache.TryRead(overviewKey, out var cachedOverview))
            {
                overviewJson = cachedOverview;
                IsOffline = true;
            }
            if (string.IsNullOrWhiteSpace(overviewJson))
            {
                Fail($"OVERVIEW {overviewFailure}");
                yield break;
            }

            try
            {
                Scene = SpatialSceneJson.ParseScene(overviewJson);
            }
            catch (Exception exception)
            {
                Fail($"OVERVIEW JSON: {exception.Message}");
                yield break;
            }

            if (Manifest != null && !Scene.BelongsTo(Manifest))
            {
                Fail("MIXED REVISION OR CATALOG");
                yield break;
            }

            if (Manifest != null)
            {
                Store = new SpatialSceneStore(Manifest);
                Store.Merge(Scene);
                cache.Write(overviewKey, overviewJson, pinnedBase: true);
                if (liveManifest)
                {
                    cache.Write("active-manifest", manifestJson, pinnedBase: true);
                }
            }

            var nodeCount = Scene.nodes?.Length ?? 0;
            var linkCount = Scene.links?.Length ?? 0;
            if (Manifest == null)
            {
                DataMode = GalaxyDataMode.LegacyUnpinned;
                StatusText = GalaxyDataStatus.Legacy(nodeCount);
                Debug.Log($"[ClustrGalaxy.Data] Loaded public legacy overview: {nodeCount} nodes, {linkCount} links; no revision manifest is deployed.");
            }
            else
            {
                DataMode = IsOffline ? GalaxyDataMode.OfflinePinned : GalaxyDataMode.LivePinned;
                var mode = IsOffline ? "OFFLINE" : "LIVE";
                StatusText = GalaxyDataStatus.Pinned(Manifest, IsOffline, nodeCount);
                Debug.Log($"[ClustrGalaxy.Data] Loaded {mode.ToLowerInvariant()} pinned overview revision {Manifest.revision_id}, catalog {Manifest.spatial_catalog_id}: {nodeCount} nodes, {linkCount} links.");
            }
            SceneLoaded();
        }

        private void BeginScopeLoad(string uri, string cacheKey, string status)
        {
            if (Manifest == null || Store == null)
            {
                throw new InvalidOperationException("A pinned overview must load before requesting a bounded scope.");
            }
            client.Cancel();
            StatusText = status;
            StartCoroutine(LoadScope(uri, cacheKey));
        }

        private IEnumerator LoadScope(string uri, string cacheKey)
        {
            string json = null!;
            SpatialRequestFailure? failure = null;
            yield return client.GetJson(uri, value => json = value, value => failure = value);

            var offlineResult = false;
            if (string.IsNullOrWhiteSpace(json) && cache.TryRead(cacheKey, out var cached))
            {
                json = cached;
                offlineResult = true;
            }
            if (string.IsNullOrWhiteSpace(json))
            {
                StatusText = $"SCOPE UNAVAILABLE  {failure}";
                Debug.LogWarning($"[ClustrGalaxy.Data] Bounded scope unavailable: {failure}");
                yield break;
            }

            SpatialScene loaded;
            try
            {
                loaded = SpatialSceneJson.ParseScene(json);
                Store.Merge(loaded);
            }
            catch (Exception exception)
            {
                StatusText = $"SCOPE REJECTED  {exception.Message}";
                Debug.LogWarning($"[ClustrGalaxy.Data] Rejected bounded scope: {exception.Message}");
                yield break;
            }

            if (!offlineResult)
            {
                cache.Write(cacheKey, json, pinnedBase: false);
            }
            Scene = loaded;
            IsOffline = offlineResult;
            DataMode = offlineResult ? GalaxyDataMode.OfflinePinned : GalaxyDataMode.LivePinned;
            var suffix = loaded.truncated ? " / MORE AVAILABLE" : string.Empty;
            StatusText = $"{(offlineResult ? "OFFLINE" : "LIVE")} {loaded.scope.ToUpperInvariant()}  {loaded.nodes.Length} OBJECTS{suffix}";
            ScopeLoaded(loaded);
            SceneLoaded();
        }

        private SpatialManifest RequireManifest() =>
            Manifest ?? throw new InvalidOperationException("The public endpoint is in legacy demo mode; bounded live requests require a pinned manifest.");

        private string ScopeCacheKey(string scope, string rootId) =>
            $"{Manifest.revision_id}:{Manifest.spatial_catalog_id}:{scope}:{rootId}";

        private void Fail(string reason)
        {
            DataMode = GalaxyDataMode.Failed;
            StatusText = GalaxyDataStatus.Fallback(reason);
            Debug.LogWarning($"[ClustrGalaxy.Data] {reason}; keeping deterministic local prototype.");
        }

        private void OnDestroy()
        {
            client?.Dispose();
        }
    }
}
