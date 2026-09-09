using System;
using System.Threading.Tasks;
using ClustrGalaxy.Core.Data;
using ClustrGalaxy.Core.Domain;
using ClustrGalaxy.Core.Session;
using ClustrGalaxy.Gameplay;
using UnityEngine;

namespace ClustrGalaxy.Data
{
    /// <summary>Unity presentation adapter; the core session owns loading and snapshot identity.</summary>
    public sealed class SpatialSceneLoader : MonoBehaviour
    {
        public string ApiBase { get; private set; }
        public bool OfflineOnly { get; private set; }
        public GalaxySession Session { get; private set; }
        public SpatialManifest Manifest { get; private set; }
        public SpatialScene Scene { get; private set; }
        public SpatialSceneStore Store { get; private set; }
        public string StatusText { get; private set; } = "CONNECTING TO CLUSTR";
        public GalaxyDataMode DataMode { get; private set; } = GalaxyDataMode.Connecting;
        public bool IsOffline { get; private set; }
        public bool IsLoading { get; private set; }
        public bool IsPinned => Manifest != null && Scene != null && Scene.BelongsTo(Manifest);
        public SurveySnapshotKey? PinnedSurveySnapshotKey { get; private set; }
        public static SurveySnapshotKey DemoSurveySnapshotKey => SurveySnapshotKey.Demo;
        public event Action<SpatialScene> SceneCommitted = delegate { };
        public void Initialize(string apiBase, bool offlineOnly = false)
        { ApiBase = apiBase; OfflineOnly = offlineOnly; }
        public async Task<bool> BindAndOpenAsync(GalaxySession session)
        {
            Session = session;
            IsLoading = true;
            var outcome = await session.OpenAsync();
            IsLoading = false;
            if (this == null || Session != session) return false;
            if (!outcome.Succeeded) { Fail(outcome.Error); return false; }
            if (outcome.Manifest.StorageNamespace == "demo")
            { DataMode = GalaxyDataMode.LocalDemo; StatusText = "DETERMINISTIC DEMO — AUTHORED LOCAL DATA"; return true; }
            Manifest = JsonUtility.FromJson<SpatialManifest>(outcome.Manifest.SourceJson);
            Store = new SpatialSceneStore(Manifest);
            PinnedSurveySnapshotKey = new SurveySnapshotKey(Manifest.revision_id, Manifest.spatial_catalog_id, outcome.Manifest.StorageNamespace);
            Commit(outcome);
            return true;
        }
        public void RefreshGalaxy() => StatusText = "START A NEW SESSION TO CHANGE SNAPSHOT";
        public void LoadScope(SpatialScopeRequest request) => Load(request.Scope, request.RootId.Value);
        public void LoadCommunity(string id) => Load(SpatialScope.Community, id);
        public void LoadSystem(string id) => Load(SpatialScope.System, id);
        public void LoadDiscussion(string id) => Load(SpatialScope.Discussion, id);
        public void LoadThread(string id) => Load(SpatialScope.Thread, id);
        private async void Load(SpatialScope scope, string id)
        {
            if (Session == null || !IsPinned) return;
            var owner = Session;
            StatusText = "LOADING " + scope.ToString().ToUpperInvariant();
            IsLoading = true;
            var outcome = await owner.LoadAsync(new SpatialScopeRequest(scope, (GalaxyEntityId)id));
            if (!outcome.IsStale) IsLoading = false;
            if (this == null || Session != owner || outcome.IsStale) return;
            if (!outcome.Succeeded) { StatusText = outcome.Error + " / CURRENT VIEW RETAINED"; return; }
            Commit(outcome);
        }
        public void PresentCommittedJump(SnapshotLoadOutcome outcome)
        {
            if (outcome == null || !outcome.Succeeded || outcome.Scene == null ||
                !Session.Snapshots.Scenes.TryGetValue(outcome.Scene.Scope, out var scene) || !ReferenceEquals(scene, outcome.Scene))
                throw new InvalidOperationException("Only an authoritative committed scene may be presented.");
            Commit(outcome);
        }
        private void Commit(SnapshotLoadOutcome outcome)
        {
            Scene = SpatialSceneJson.ParseScene(outcome.Scene.SourceJson);
            // Derived read adapter only; the session owns which scopes remain resident.
            Store = new SpatialSceneStore(Manifest);
            foreach (var projection in Session.Snapshots.Scenes.Values)
                if (!string.IsNullOrEmpty(projection.SourceJson)) Store.Merge(SpatialSceneJson.ParseScene(projection.SourceJson));
            IsOffline = outcome.Mode == SnapshotDataMode.OfflinePinned;
            DataMode = outcome.Manifest.StorageNamespace == "fixture" ? GalaxyDataMode.DeterministicFixture : IsOffline ? GalaxyDataMode.OfflinePinned : GalaxyDataMode.LivePinned;
            var suffix = string.IsNullOrEmpty(Scene.scope) ? "OVERVIEW" : Scene.scope.ToUpperInvariant();
            StatusText = (DataMode == GalaxyDataMode.DeterministicFixture ? (IsOffline ? "OFFLINE DETERMINISTIC FIXTURE " : "DETERMINISTIC FIXTURE ") : IsOffline ? "OFFLINE " : "LIVE ") + suffix + "  " + Scene.nodes.Length + " OBJECTS";
            if (!string.IsNullOrEmpty(Scene.scope))
            {
                var limits = SpatialScopePaging.For(Scene.scope);
                StatusText += SpatialScopePaging.StatusSuffix(SpatialScopePaging.CompletionFor(Scene, limits, Scene.nodes.Length, Scene.links.Length));
            }
            if (Scene.scope == "system" && Session.CurrentSystemId.Value != Scene.root_id)
                StatusText += " · DATA INSPECTION ONLY · JUMP TO ENTER";
            SceneCommitted(Scene);
        }
        private void Fail(string reason) { DataMode = GalaxyDataMode.Failed; StatusText = reason; }
        private void OnDestroy() => Session?.Snapshots.CancelPending();
    }
}
