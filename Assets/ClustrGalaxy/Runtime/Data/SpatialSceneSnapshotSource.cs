using System;
using System.Linq;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ClustrGalaxy.Core.Data;
using ClustrGalaxy.Core.Domain;
using ClustrGalaxy.Core.Persistence;
using UnityEngine;
using UnityEngine.Networking;
using Newtonsoft.Json.Linq;

namespace ClustrGalaxy.Data
{
    public interface ISpatialJsonTransport
    {
        Task<string> GetAsync(string uri, CancellationToken cancellationToken);
    }

    /// <summary>Unity network/cache adapter for the deep pinned snapshot session.</summary>
    public sealed class SpatialSceneSnapshotSource : IGalaxySnapshotSource
    {
        private const string ManifestCache = "ClustrGalaxy/cache/v2/active-manifest.json";
        private readonly string apiBase;
        private readonly string storageNamespace;
        private readonly bool offlineOnly;
        private readonly IDurableDocumentStore cache;
        private readonly ISpatialJsonTransport transport;

        public SpatialSceneSnapshotSource(string apiBase, bool offlineOnly, IDurableDocumentStore cache = null, ISpatialJsonTransport transport = null, string storageNamespace = "pinned")
        {
            if (storageNamespace != "pinned" && storageNamespace != "fixture") throw new ArgumentException("Unsupported source namespace.");
            this.storageNamespace = storageNamespace;
            this.transport = transport;
            this.apiBase = string.IsNullOrWhiteSpace(apiBase) ? throw new ArgumentException("An endpoint root is required.", nameof(apiBase)) : apiBase;
            this.offlineOnly = offlineOnly;
            this.cache = cache ?? new SnapshotCacheDocuments(storageNamespace);
        }

        public long CacheUsageBytes => (cache as SnapshotCacheDocuments)?.UsedBytes ?? -1;

        public async Task<SnapshotSourceResult> OpenAsync(CancellationToken cancellationToken)
        {
            var result = await OpenInternalAsync(cancellationToken);
            if (result.Succeeded || offlineOnly) return result;
            cancellationToken.ThrowIfCancellationRequested();
            return await new SpatialSceneSnapshotSource(apiBase, true, cache, transport, storageNamespace).OpenInternalAsync(cancellationToken);
        }

        private async Task<SnapshotSourceResult> OpenInternalAsync(CancellationToken cancellationToken)
        {
            string manifestJson;
            var offline = offlineOnly;
            if (offlineOnly)
            {
                if (!cache.TryRead(ManifestCache, out manifestJson)) return SnapshotSourceResult.Failure("OFFLINE CACHE UNAVAILABLE");
            }
            else
            {
                manifestJson = await GetJson(SpatialSceneRequestUris.Manifest(apiBase), cancellationToken);
                if (string.IsNullOrWhiteSpace(manifestJson))
                {
                    if (!cache.TryRead(ManifestCache, out manifestJson)) return SnapshotSourceResult.Failure("MANIFEST UNAVAILABLE");
                    offline = true;
                }
            }

            if (!TryManifest(manifestJson, out var rawManifest, out var manifest))
                return SnapshotSourceResult.Failure("INCOMPATIBLE MANIFEST");
            var overviewPath = CachePath(manifest, new SpatialScopeRequest(SpatialScope.Overview));
            string overviewJson;
            if (offline)
            {
                if (!cache.TryRead(overviewPath, out overviewJson)) return SnapshotSourceResult.Failure("OFFLINE OVERVIEW UNAVAILABLE");
            }
            else
            {
                overviewJson = await GetJson(SpatialSceneRequestUris.Overview(apiBase, rawManifest), cancellationToken);
                if (string.IsNullOrWhiteSpace(overviewJson))
                {
                    if (!cache.TryRead(overviewPath, out overviewJson)) return SnapshotSourceResult.Failure("OVERVIEW UNAVAILABLE");
                    offline = true;
                }
            }
            if (!TryScene(overviewJson, new SpatialScopeRequest(SpatialScope.Overview), manifest, out var scene))
                return SnapshotSourceResult.Failure("OVERVIEW REJECTED");
            if (!offline)
            {
                cancellationToken.ThrowIfCancellationRequested();
                (cache as SnapshotCacheDocuments)?.PrepareBase(overviewPath);
                cache.Write(overviewPath, overviewJson,
                    value => TryScene(value, new SpatialScopeRequest(SpatialScope.Overview), manifest, out _));
                cache.Write(ManifestCache, manifestJson, value => TryManifest(value, out _, out _));
                (cache as SnapshotCacheDocuments)?.CommitBase(overviewPath);
            }
            return SnapshotSourceResult.Success(manifest, scene, offline);
        }

        public async Task<SnapshotSourceResult> LoadAsync(GalaxySnapshotManifest manifest, SpatialScopeRequest request,
            CancellationToken cancellationToken)
        {
            var rawManifest = new SpatialManifest
            {
                revision_id = manifest.RevisionId, spatial_catalog_id = manifest.CatalogId,
                scene_contract = manifest.ContractVersion, dimensions = 3,
                bounds = new SpatialBounds { min_x = 0, max_x = 0, min_y = 0, max_y = 0, min_z = 0, max_z = 0 }
            };
            var path = CachePath(manifest, request);
            string json = string.Empty;
            var offline = offlineOnly;
            if (!offlineOnly) json = await GetJson(UriFor(rawManifest, request), cancellationToken);
            if (string.IsNullOrWhiteSpace(json))
            {
                if (!cache.TryRead(path, out json)) return SnapshotSourceResult.Failure("BOUNDED SCOPE UNAVAILABLE");
                offline = true;
            }
            if (offline)
            {
                if (!TryScene(json, request, manifest, out var cachedScene)) return SnapshotSourceResult.Failure("BOUNDED CACHE REJECTED");
                return SnapshotSourceResult.Success(manifest, cachedScene, true);
            }
            var scope = request.Scope.ToString().ToLowerInvariant();
            var limits = SpatialScopePaging.For(scope);
            var pages = new List<SpatialScene>();
            var cursors = new HashSet<string>(StringComparer.Ordinal);
            var identities = new Dictionary<string, string>(StringComparer.Ordinal);
            var routeIdentities = new Dictionary<(string, string, string), string>();
            var nodes = 0; var links = 0;
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!TryScene(json, request, manifest, out _)) return SnapshotSourceResult.Failure("BOUNDED SCOPE REJECTED");
                var page = SpatialSceneJson.ParseScene(json);
                if (!SpatialScopePaging.CanAccept(limits, nodes, links, page))
                    return SnapshotSourceResult.Failure("CLIENT CEILING EXCEEDED");
                foreach (var node in page.nodes)
                {
                    var value = JsonUtility.ToJson(node);
                    if (identities.TryGetValue(node.id, out var previous) && previous != value)
                        return SnapshotSourceResult.Failure("CONFLICTING DUPLICATE ENTITY");
                    identities[node.id] = value;
                }
                foreach (var link in page.links)
                {
                    var key = (link.source, link.target, link.type);
                    var value = JsonUtility.ToJson(link);
                    if (routeIdentities.TryGetValue(key, out var previous) && previous != value)
                        return SnapshotSourceResult.Failure("CONFLICTING DUPLICATE ROUTE");
                    routeIdentities[key] = value;
                }
                pages.Add(page); nodes += page.nodes.Length; links += page.links.Length;
                if (string.IsNullOrWhiteSpace(page.next_cursor) ||
                    SpatialScopePaging.CompletionFor(page, limits, nodes, links) != SpatialScopeCompletion.Complete) break;
                if (!cursors.Add(page.next_cursor)) return SnapshotSourceResult.Failure("REPEATED CONTINUATION CURSOR");
                json = await GetJson(SpatialScopePaging.WithCursor(UriFor(rawManifest, request), page.next_cursor), cancellationToken);
                if (string.IsNullOrWhiteSpace(json)) return SnapshotSourceResult.Failure("CONTINUATION UNAVAILABLE / CURRENT VIEW RETAINED");
            }
            var aggregate = SpatialScopePaging.Aggregate(rawManifest, scope, request.RootId.Value, pages);
            json = JsonUtility.ToJson(aggregate);
            if (!TryScene(json, request, manifest, out var scene)) return SnapshotSourceResult.Failure("AGGREGATE REJECTED");
            cancellationToken.ThrowIfCancellationRequested();
            cache.Write(path, json, value => TryScene(value, request, manifest, out _));
            return SnapshotSourceResult.Success(manifest, scene, false);
        }

        private string UriFor(SpatialManifest manifest, SpatialScopeRequest request) => request.Scope switch
        {
            SpatialScope.Community => SpatialSceneRequestUris.Community(apiBase, manifest, request.RootId.Value),
            SpatialScope.System => SpatialSceneRequestUris.System(apiBase, manifest, request.RootId.Value),
            SpatialScope.Discussion => SpatialSceneRequestUris.Discussion(apiBase, manifest, request.RootId.Value),
            SpatialScope.Thread => SpatialSceneRequestUris.Thread(apiBase, manifest, request.RootId.Value),
            _ => throw new ArgumentOutOfRangeException(nameof(request))
        };

        private async Task<string> GetJson(string uri, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (transport != null) return await transport.GetAsync(uri, cancellationToken);
            for (var attempt = 0; attempt < 3; attempt++)
            {
                using var request = UnityWebRequest.Get(uri);
                request.timeout = 12;
                request.SetRequestHeader("Accept", "application/json");
                var operation = request.SendWebRequest();
                using var registration = cancellationToken.Register(request.Abort);
                var completion = new TaskCompletionSource<bool>();
                operation.completed += _ => completion.TrySetResult(true);
                if (operation.isDone) completion.TrySetResult(true);
                await completion.Task;
                cancellationToken.ThrowIfCancellationRequested();
                if (request.result == UnityWebRequest.Result.Success) return request.downloadHandler.text;
                var transient = request.responseCode == 0 || request.responseCode == 408 || request.responseCode == 429 || request.responseCode >= 500;
                if (!transient || attempt == 2) return string.Empty;
                await Task.Delay(500 * (1 << attempt), cancellationToken);
            }
            return string.Empty;
        }

        private bool TryManifest(string json, out SpatialManifest raw, out GalaxySnapshotManifest manifest)
        {
            raw = null; manifest = null;
            try
            {
                var document = ParseDocument(json);
                if (!HasIdentity(document) || document["dimensions"]?.Type != JTokenType.Integer || document["scene_contract"]?.Type != JTokenType.String) return false;
                raw = JsonUtility.FromJson<SpatialManifest>(json);
                if (raw == null || !raw.IsCompatible || raw.spatial_catalog_id < 0) return false;
                manifest = new GalaxySnapshotManifest(raw.revision_id, raw.spatial_catalog_id, raw.scene_contract, storageNamespace, sourceJson: json);
                return manifest.IsCompatible;
            }
            catch (Exception) { raw = null; manifest = null; return false; }
        }

        private static bool TryScene(string json, SpatialScopeRequest request, GalaxySnapshotManifest manifest,
            out GalaxySnapshotScene result)
        {
            result = null;
            try
            {
                var document = ParseDocument(json);
                if (!HasIdentity(document) || document["nodes"] is not JArray rawNodes || document["links"] is not JArray rawLinks) return false;
                var identities = new Dictionary<string, JToken>(StringComparer.Ordinal);
                foreach (var rawNode in rawNodes)
                {
                    if (rawNode is not JObject node || !HasString(node, "id") || !HasString(node, "type")) return false;
                    var id = (string)node["id"];
                    if (id != id.Trim() || identities.TryGetValue(id, out var previous) && !JToken.DeepEquals(previous,node)) return false;
                    identities[id] = node;
                    if (node["metrics"] != null && node["metrics"] is not JObject) return false;
                }
                var routeIdentities = new Dictionary<(string, string, string), JObject>();
                foreach (var rawLink in rawLinks)
                {
                    if (rawLink is not JObject link || !HasString(link,"source") || !HasString(link,"target")) return false;
                    var key = ((string)link["source"], (string)link["target"], (string)link["type"]);
                    if (routeIdentities.TryGetValue(key, out var previous) && !JToken.DeepEquals(previous, link)) return false;
                    routeIdentities[key] = link;
                }
                var scene = SpatialSceneJson.ParseScene(json);
                foreach (var node in scene.nodes)
                    if (!float.IsFinite(node.x) || !float.IsFinite(node.y) || !float.IsFinite(node.z) || node.val < 0 || node.metrics.depth > 3 || node.metrics.depth < 0) return false;
                foreach (var link in scene.links) if (link.weight < 0) return false;
                if (scene.revision_id != manifest.RevisionId || scene.spatial_catalog_id != manifest.CatalogId) return false;
                // Community payloads omit scope/root metadata in spatial-scene-v1.
                // Bind those optional fields to the pinned request; entity expansions publish both.
                if (request.Scope != SpatialScope.Overview && request.Scope != SpatialScope.Community &&
                    (string.IsNullOrWhiteSpace(scene.scope) || string.IsNullOrWhiteSpace(scene.root_id))) return false;
                if (request.Scope != SpatialScope.Overview)
                    SpatialSceneScopeContract.ApplyOrValidate(scene, request.Scope.ToString().ToLowerInvariant(), request.RootId.Value);
                var nodes = (scene.nodes ?? Array.Empty<SpatialNode>()).GroupBy(node => node.id).Select(group => group.First()).Select(node => new SnapshotNode(
                    (GalaxyEntityId)node.id, GalaxyKinds.ParseEntityKind(node.type), node.name, node.val,
                    node.x, node.y, node.z, string.IsNullOrWhiteSpace(node.parent_id) ? default : (GalaxyEntityId)node.parent_id));
                var routes = (scene.links ?? Array.Empty<SpatialLink>()).GroupBy(link => (link.source, link.target, link.type)).Select(group => group.First()).Select(link => new SnapshotRoute(
                    (GalaxyRouteId)$"{link.source}:{link.target}:{link.type}", (GalaxyEntityId)link.source,
                    (GalaxyEntityId)link.target, link.weight, link.directed));
                result = new GalaxySnapshotScene(scene.revision_id, scene.spatial_catalog_id, request, nodes, routes, scene.truncated, json);
                return true;
            }
            catch (Exception) { result = null; return false; }
        }

        private static JObject ParseDocument(string json) => JObject.Parse(json, new JsonLoadSettings
        { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
        private static bool HasIdentity(JObject document) =>
            document["revision_id"]?.Type == JTokenType.Integer && document["spatial_catalog_id"]?.Type == JTokenType.Integer;
        private static bool HasString(JObject document, string name) => document[name]?.Type == JTokenType.String &&
            !string.IsNullOrWhiteSpace((string)document[name]);

        private sealed class SnapshotCacheDocuments : IDurableDocumentStore
        {
            private readonly SnapshotCache storage;
            public SnapshotCacheDocuments(string storageNamespace) => storage = new SnapshotCache(storageNamespace == "pinned"
                ? SnapshotCache.DefaultRoot : System.IO.Path.Combine(Application.persistentDataPath,"ClustrGalaxy","fixture-cache","v2"));
            public long UsedBytes => storage.UsedBytes;
            private string pendingBase;
            public void PrepareBase(string path) => pendingBase = path;
            public void CommitBase(string path) => storage.ProtectOnly(ManifestCache, path);
            public bool TryRead(string path, out string value) => storage.TryRead(path, out value);
            public void Write(string path, string value, Func<string, bool> validate)
            {
                if (!validate(value)) throw new System.IO.InvalidDataException("Snapshot cache validation failed.");
                storage.Write(path, value, path == ManifestCache || path == pendingBase);
            }
            public void Quarantine(string path) => storage.Quarantine(path);
        }

        private static string CachePath(GalaxySnapshotManifest manifest, SpatialScopeRequest request)
        {
            var key = $"{manifest.RevisionId}:{manifest.CatalogId}:{request}";
            if (manifest.StorageNamespace != "pinned") key = manifest.StorageNamespace + ":" + key;
            using var sha = SHA256.Create();
            var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(key));
            var name = string.Concat(hash.Take(16).Select(value => value.ToString("x2")));
            return $"ClustrGalaxy/cache/v2/{name}.json";
        }
    }
}
