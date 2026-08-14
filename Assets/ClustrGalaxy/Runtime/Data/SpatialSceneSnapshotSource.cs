using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ClustrGalaxy.Core.Data;
using ClustrGalaxy.Core.Domain;
using ClustrGalaxy.Core.Persistence;
using UnityEngine;
using UnityEngine.Networking;

namespace ClustrGalaxy.Data
{
    /// <summary>Unity network/cache adapter for the deep pinned snapshot session.</summary>
    public sealed class SpatialSceneSnapshotSource : IGalaxySnapshotSource
    {
        private const string ManifestCache = "ClustrGalaxy/cache/v2/active-manifest.json";
        private readonly string apiBase;
        private readonly bool offlineOnly;
        private readonly IDurableDocumentStore cache;

        public SpatialSceneSnapshotSource(string apiBase, bool offlineOnly, IDurableDocumentStore cache = null)
        {
            this.apiBase = string.IsNullOrWhiteSpace(apiBase) ? throw new ArgumentException("An endpoint root is required.", nameof(apiBase)) : apiBase;
            this.offlineOnly = offlineOnly;
            this.cache = cache ?? new FileDurableDocumentStore(Application.persistentDataPath);
        }

        public async Task<SnapshotSourceResult> OpenAsync(CancellationToken cancellationToken)
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
                cache.Write(ManifestCache, manifestJson, value => TryManifest(value, out _, out _));
                cache.Write(overviewPath, overviewJson,
                    value => TryScene(value, new SpatialScopeRequest(SpatialScope.Overview), manifest, out _));
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
            if (!TryScene(json, request, manifest, out var scene)) return SnapshotSourceResult.Failure("BOUNDED SCOPE REJECTED");
            if (!offline) cache.Write(path, json, value => TryScene(value, request, manifest, out _));
            return SnapshotSourceResult.Success(manifest, scene, offline);
        }

        private string UriFor(SpatialManifest manifest, SpatialScopeRequest request) => request.Scope switch
        {
            SpatialScope.Community => SpatialSceneRequestUris.Community(apiBase, manifest, request.RootId.Value),
            SpatialScope.System => SpatialSceneRequestUris.System(apiBase, manifest, request.RootId.Value),
            SpatialScope.Discussion => SpatialSceneRequestUris.Discussion(apiBase, manifest, request.RootId.Value),
            SpatialScope.Thread => SpatialSceneRequestUris.Thread(apiBase, manifest, request.RootId.Value),
            _ => throw new ArgumentOutOfRangeException(nameof(request))
        };

        private static async Task<string> GetJson(string uri, CancellationToken cancellationToken)
        {
            using var request = UnityWebRequest.Get(uri);
            request.timeout = 12;
            var operation = request.SendWebRequest();
            using var registration = cancellationToken.Register(request.Abort);
            var completion = new TaskCompletionSource<bool>();
            operation.completed += _ => completion.TrySetResult(true);
            if (operation.isDone) completion.TrySetResult(true);
            await completion.Task;
            cancellationToken.ThrowIfCancellationRequested();
            return request.result == UnityWebRequest.Result.Success ? request.downloadHandler.text : string.Empty;
        }

        private static bool TryManifest(string json, out SpatialManifest raw, out GalaxySnapshotManifest manifest)
        {
            raw = null; manifest = null;
            try
            {
                raw = JsonUtility.FromJson<SpatialManifest>(json);
                if (raw == null || !raw.IsCompatible || raw.spatial_catalog_id < 0) return false;
                manifest = new GalaxySnapshotManifest(raw.revision_id, raw.spatial_catalog_id, raw.scene_contract);
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
                var scene = SpatialSceneJson.ParseScene(json);
                if (scene.revision_id != manifest.RevisionId || scene.spatial_catalog_id != manifest.CatalogId) return false;
                var nodes = (scene.nodes ?? Array.Empty<SpatialNode>()).Select(node => new SnapshotNode(
                    (GalaxyEntityId)node.id, GalaxyKinds.ParseEntityKind(node.type), node.name, node.val,
                    node.x, node.y, node.z, string.IsNullOrWhiteSpace(node.parent_id) ? default : (GalaxyEntityId)node.parent_id));
                var routes = (scene.links ?? Array.Empty<SpatialLink>()).Select(link => new SnapshotRoute(
                    (GalaxyRouteId)$"{link.source}:{link.target}:{link.type}", (GalaxyEntityId)link.source,
                    (GalaxyEntityId)link.target, link.weight, link.directed));
                result = new GalaxySnapshotScene(scene.revision_id, scene.spatial_catalog_id, request, nodes, routes, scene.truncated);
                return true;
            }
            catch (Exception) { result = null; return false; }
        }

        private static string CachePath(GalaxySnapshotManifest manifest, SpatialScopeRequest request)
        {
            var key = $"{manifest.RevisionId}:{manifest.CatalogId}:{request}";
            using var sha = SHA256.Create();
            var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(key));
            var name = string.Concat(hash.Take(16).Select(value => value.ToString("x2")));
            return $"ClustrGalaxy/cache/v2/{name}.json";
        }
    }
}
