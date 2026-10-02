using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ClustrGalaxy.Core.Domain;
using ClustrGalaxy.Core.Survey;

namespace ClustrGalaxy.Core.Data
{
    public enum SnapshotDataMode { Connecting, LivePinned, OfflinePinned, LegacyUnpinned, DeterministicDemo, DeterministicFixture, Failed }

    public sealed class GalaxySnapshotManifest
    {
        public GalaxySnapshotManifest(long revisionId, long catalogId, string contractVersion, string storageNamespace = "pinned", string sourceJson = "")
        {
            SourceJson = sourceJson;
            RevisionId = revisionId; CatalogId = catalogId; ContractVersion = contractVersion ?? string.Empty;
            StorageNamespace = string.IsNullOrWhiteSpace(storageNamespace) ? "pinned" : storageNamespace;
        }
        public long RevisionId { get; }
        public long CatalogId { get; }
        public string SourceJson { get; }
        public string ContractVersion { get; }
        public string StorageNamespace { get; }
        public bool IsCompatible => RevisionId > 0 && CatalogId >= 0 && ContractVersion == "spatial-scene-v1";
        public SurveySnapshotId SurveySnapshot => new(RevisionId, CatalogId, StorageNamespace);
    }

    public sealed class SnapshotNode
    {
        public SnapshotNode(GalaxyEntityId id, GalaxyEntityKind kind, string name, long value,
            float x = 0, float y = 0, float z = 0, GalaxyEntityId parentId = default)
        { Id = id; Kind = kind; Name = name ?? string.Empty; Value = value; X = x; Y = y; Z = z; ParentId = parentId; }
        public GalaxyEntityId Id { get; }
        public GalaxyEntityKind Kind { get; }
        public string Name { get; }
        public long Value { get; }
        public float X { get; }
        public float Y { get; }
        public float Z { get; }
        public GalaxyEntityId ParentId { get; }
    }

    public sealed class SnapshotRoute
    {
        public SnapshotRoute(GalaxyRouteId id, GalaxyEntityId sourceId, GalaxyEntityId targetId, long weight, bool directed)
        { Id = id; SourceId = sourceId; TargetId = targetId; Weight = weight; Directed = directed; }
        public GalaxyRouteId Id { get; }
        public GalaxyEntityId SourceId { get; }
        public GalaxyEntityId TargetId { get; }
        public long Weight { get; }
        public bool Directed { get; }
    }

    public sealed class GalaxySnapshotScene
    {
        public GalaxySnapshotScene(long revisionId, long catalogId, SpatialScopeRequest scope,
            IEnumerable<SnapshotNode> nodes, IEnumerable<SnapshotRoute> routes, bool truncated = false, string sourceJson = "", string completion = "")
        {
            RevisionId = revisionId; CatalogId = catalogId; Scope = scope;
            Nodes = (nodes ?? Array.Empty<SnapshotNode>()).Where(value => value != null).ToArray();
            Routes = (routes ?? Array.Empty<SnapshotRoute>()).Where(value => value != null).ToArray();
            Truncated = truncated; SourceJson = sourceJson; Completion = completion;
        }
        public long RevisionId { get; }
        public long CatalogId { get; }
        public SpatialScopeRequest Scope { get; }
        public IReadOnlyList<SnapshotNode> Nodes { get; }
        public IReadOnlyList<SnapshotRoute> Routes { get; }
        public bool Truncated { get; }
        public string SourceJson { get; }
        public string Completion { get; }
        public bool BelongsTo(GalaxySnapshotManifest manifest) => manifest != null &&
            RevisionId == manifest.RevisionId && CatalogId == manifest.CatalogId;
    }

    public sealed class SnapshotSourceResult
    {
        private SnapshotSourceResult(bool succeeded, bool offline, GalaxySnapshotManifest manifest,
            GalaxySnapshotScene scene, string error)
        { Succeeded = succeeded; Offline = offline; Manifest = manifest; Scene = scene; Error = error ?? string.Empty; }
        public bool Succeeded { get; }
        public bool Offline { get; }
        public GalaxySnapshotManifest Manifest { get; }
        public GalaxySnapshotScene Scene { get; }
        public string Error { get; }
        public static SnapshotSourceResult Success(GalaxySnapshotManifest manifest, GalaxySnapshotScene scene, bool offline = false) =>
            new(true, offline, manifest, scene, string.Empty);
        public static SnapshotSourceResult Failure(string error) => new(false, false, null, null, error);
    }

    public interface IGalaxySnapshotSource
    {
        Task<SnapshotSourceResult> OpenAsync(CancellationToken cancellationToken);
        Task<SnapshotSourceResult> LoadAsync(GalaxySnapshotManifest manifest, SpatialScopeRequest request,
            CancellationToken cancellationToken);
    }

    public sealed class DeterministicSnapshotSource : IGalaxySnapshotSource
    {
        private readonly GalaxySnapshotManifest manifest;
        private readonly Dictionary<SpatialScopeRequest, GalaxySnapshotScene> scenes;
        public DeterministicSnapshotSource(GalaxySnapshotManifest manifest, IEnumerable<GalaxySnapshotScene> scenes)
        {
            this.manifest = manifest ?? throw new ArgumentNullException(nameof(manifest));
            this.scenes = (scenes ?? Array.Empty<GalaxySnapshotScene>()).ToDictionary(value => value.Scope);
        }
        public Task<SnapshotSourceResult> OpenAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var overview = scenes.Values.FirstOrDefault(value => value.Scope.Scope == SpatialScope.Overview);
            return Task.FromResult(overview == null ? SnapshotSourceResult.Failure("Deterministic overview unavailable.") :
                SnapshotSourceResult.Success(manifest, overview, true));
        }
        public Task<SnapshotSourceResult> LoadAsync(GalaxySnapshotManifest expected, SpatialScopeRequest request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(expected.RevisionId == manifest.RevisionId && expected.CatalogId == manifest.CatalogId &&
                scenes.TryGetValue(request, out var scene)
                ? SnapshotSourceResult.Success(manifest, scene, true)
                : SnapshotSourceResult.Failure("Deterministic scope unavailable."));
        }
    }

    public sealed class FallbackSnapshotSource : IGalaxySnapshotSource
    {
        private readonly IGalaxySnapshotSource primary;
        private readonly IGalaxySnapshotSource fallback;
        private IGalaxySnapshotSource active;
        public FallbackSnapshotSource(IGalaxySnapshotSource primary, IGalaxySnapshotSource fallback)
        { this.primary = primary ?? throw new ArgumentNullException(nameof(primary)); this.fallback = fallback ?? throw new ArgumentNullException(nameof(fallback)); }
        public async Task<SnapshotSourceResult> OpenAsync(CancellationToken cancellationToken)
        {
            var result = await primary.OpenAsync(cancellationToken);
            active = result.Succeeded ? primary : fallback;
            return result.Succeeded ? result : await fallback.OpenAsync(cancellationToken);
        }
        public Task<SnapshotSourceResult> LoadAsync(GalaxySnapshotManifest manifest, SpatialScopeRequest request,
            CancellationToken cancellationToken) => (active ?? primary).LoadAsync(manifest, request, cancellationToken);
    }

    public sealed class SnapshotLoadOutcome
    {
        internal SnapshotLoadOutcome(long operationId, bool succeeded, bool stale, SnapshotDataMode mode,
            GalaxySnapshotManifest manifest, GalaxySnapshotScene scene, string status, string error)
        { OperationId = operationId; Succeeded = succeeded; IsStale = stale; Mode = mode; Manifest = manifest;
            Scene = scene; Status = status ?? string.Empty; Error = error ?? string.Empty; }
        public long OperationId { get; }
        public bool Succeeded { get; }
        public bool IsStale { get; }
        public SnapshotDataMode Mode { get; }
        public GalaxySnapshotManifest Manifest { get; }
        public GalaxySnapshotScene Scene { get; }
        public string Status { get; }
        public string Error { get; }
    }

    public sealed class GalaxySnapshotSession : IDisposable
    {
        private readonly IGalaxySnapshotSource source;
        private readonly Dictionary<SpatialScopeRequest, GalaxySnapshotScene> scenes = new();
        private CancellationTokenSource pending = new();
        private long operationId;
        private bool disposed;
        private SnapshotLoadOutcome staged;

        public GalaxySnapshotSession(IGalaxySnapshotSource sourceAdapter)
        { source = sourceAdapter ?? throw new ArgumentNullException(nameof(sourceAdapter)); }
        public GalaxySnapshotManifest Manifest { get; private set; }
        public SnapshotDataMode Mode { get; private set; } = SnapshotDataMode.Connecting;
        public IReadOnlyDictionary<SpatialScopeRequest, GalaxySnapshotScene> Scenes => scenes;

        public async Task<SnapshotLoadOutcome> OpenAsync(CancellationToken cancellationToken = default)
        {
            if (Manifest != null) return Failure(operationId, "Snapshot identity is frozen; create a new session to refresh.");
            var operation = Begin(cancellationToken, out var token);
            SnapshotSourceResult result;
            try { result = await source.OpenAsync(token); }
            catch (OperationCanceledException)
            { return operation != operationId ? Stale(operation) : Failure(operation, "Snapshot open was cancelled."); }
            catch (Exception exception)
            { return operation != operationId ? Stale(operation) : Failure(operation, exception.Message); }
            if (operation != operationId || token.IsCancellationRequested) return Stale(operation);
            if (result == null || !result.Succeeded || result.Manifest == null || result.Scene == null)
                return Failure(operation, result?.Error ?? "Missing snapshot response.");
            if (!result.Manifest.IsCompatible || !result.Scene.BelongsTo(result.Manifest) ||
                result.Scene.Scope.Scope != SpatialScope.Overview)
                return Failure(operation, "Snapshot overview failed compatibility or identity validation.");
            Manifest = result.Manifest;
            scenes.Clear(); scenes[result.Scene.Scope] = result.Scene;
            Mode = Manifest.StorageNamespace == "demo" ? SnapshotDataMode.DeterministicDemo : result.Offline ? SnapshotDataMode.OfflinePinned : Manifest.StorageNamespace == "fixture" ? SnapshotDataMode.DeterministicFixture : SnapshotDataMode.LivePinned;
            return Success(operation, result.Scene);
        }

        public async Task<SnapshotLoadOutcome> LoadAsync(SpatialScopeRequest request,
            CancellationToken cancellationToken = default)
        {
            var candidate = await StageAsync(request, cancellationToken);
            return candidate.Succeeded ? CommitStaged(candidate) : candidate;
        }

        // Loading for travel validates a destination without replacing the current usable view.
        // Only the exact, still-current receipt can become an active scene.
        public async Task<SnapshotLoadOutcome> StageAsync(SpatialScopeRequest request,
            CancellationToken cancellationToken = default)
        {
            if (request.Scope == SpatialScope.Overview) throw new ArgumentException("Use OpenAsync for an overview.", nameof(request));
            if (Manifest == null) return Failure(operationId, "Open a pinned snapshot before loading a bounded scope.");
            var operation = Begin(cancellationToken, out var token);
            SnapshotSourceResult result;
            try { result = await source.LoadAsync(Manifest, request, token); }
            catch (OperationCanceledException)
            { return operation != operationId ? Stale(operation) : Failure(operation, "Snapshot load was cancelled."); }
            catch (Exception exception)
            { return operation != operationId ? Stale(operation) : Failure(operation, exception.Message); }
            if (operation != operationId || token.IsCancellationRequested) return Stale(operation);
            if (result == null || !result.Succeeded || result.Scene == null) return Failure(operation, result?.Error ?? "Missing snapshot response.");
            if (result.Manifest == null || result.Manifest.RevisionId != Manifest.RevisionId ||
                result.Manifest.CatalogId != Manifest.CatalogId || result.Manifest.ContractVersion != Manifest.ContractVersion ||
                result.Manifest.StorageNamespace != Manifest.StorageNamespace ||
                !result.Scene.BelongsTo(Manifest) || !result.Scene.Scope.Equals(request))
                return Failure(operation, "Bounded scope failed snapshot identity validation.");
            var mode = Manifest.StorageNamespace == "demo" ? SnapshotDataMode.DeterministicDemo :
                result.Offline ? SnapshotDataMode.OfflinePinned :
                Manifest.StorageNamespace == "fixture" ? SnapshotDataMode.DeterministicFixture : SnapshotDataMode.LivePinned;
            staged = new SnapshotLoadOutcome(operation, true, false, mode, Manifest, result.Scene,
                "DESTINATION VALIDATED — AWAITING ARRIVAL", "");
            return staged;
        }

        public SnapshotLoadOutcome CommitStaged(SnapshotLoadOutcome candidate)
        {
            if (disposed || candidate == null || !ReferenceEquals(candidate, staged) ||
                candidate.OperationId != operationId || pending.IsCancellationRequested)
                return Stale(candidate?.OperationId ?? operationId);
            staged = null;
            scenes[candidate.Scene.Scope] = candidate.Scene;
            Mode = candidate.Mode;
            return Success(candidate.OperationId, candidate.Scene);
        }

        public void ReleaseScene(SpatialScopeRequest scope)
        { if (scope.Scope != SpatialScope.Overview) scenes.Remove(scope); }

        public void CancelPending() { if (disposed) return; pending.Cancel(); staged = null; operationId++; }
        public void Dispose() { if (disposed) return; CancelPending(); disposed = true; pending.Dispose(); }

        private long Begin(CancellationToken caller, out CancellationToken token)
        {
            if (disposed) throw new ObjectDisposedException(nameof(GalaxySnapshotSession));
            staged = null; pending.Cancel(); pending.Dispose(); pending = CancellationTokenSource.CreateLinkedTokenSource(caller);
            token = pending.Token;
            return ++operationId;
        }
        private SnapshotLoadOutcome Success(long operation, GalaxySnapshotScene scene) => new(operation, true, false,
            Mode, Manifest, scene, Mode == SnapshotDataMode.DeterministicDemo ? "AUTHORED LOCAL DEMO" : Mode == SnapshotDataMode.OfflinePinned ? "OFFLINE PINNED" : "LIVE PINNED", "");
        private SnapshotLoadOutcome Failure(long operation, string error)
        { return new(operation, false, false, SnapshotDataMode.Failed, Manifest, null, "SNAPSHOT UNAVAILABLE", error); }
        private SnapshotLoadOutcome Stale(long operation) => new(operation, false, true, Mode, Manifest, null, "STALE RESULT", "");
    }
}
