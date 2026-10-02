using System;
using System.Linq;
using System.Collections;
using ClustrGalaxy.Interaction;
using ClustrGalaxy.World;
using UnityEngine;

namespace ClustrGalaxy.Data
{
    /// <summary>
    /// Opt-in release diagnostic for the real bounded hierarchy. It uses the
    /// same loader, cache, store, and renderer callbacks as player interaction.
    /// Normal launches never add this component.
    /// </summary>
    public sealed class GalaxyLiveDataSmokeRunner : MonoBehaviour
    {
        private SpatialSceneLoader loader = null!;
        private SmokeStage stage;
        private ProbeTargeting targeting;
        private GalaxyEntityRegistry registry;
        private float deadline;
        private string connectedSystem;
        public void BindPlayer(ProbeTargeting playerTargeting, GalaxyEntityRegistry entities)
        { targeting = playerTargeting; registry = entities; }
        private void Update()
        {
            if (!Completed && !Failed && Time.realtimeSinceStartup > deadline)
                Fail("hierarchy timeout: " + loader.StatusText);
        }
        public bool Completed => stage == SmokeStage.Complete;
        public bool Failed => stage == SmokeStage.Failed;

        public void Initialize(SpatialSceneLoader sceneLoader)
        {
            loader = sceneLoader ?? throw new ArgumentNullException(nameof(sceneLoader));
            loader.SceneCommitted += Advance;
            deadline = Time.realtimeSinceStartup + 180f;
            Debug.Log("[ClustrGalaxy.LiveSmoke] Waiting for pinned overview.");
        }

        public static SpatialNode SelectHighestActivity(SpatialScene scene, string nodeType,
            Func<SpatialNode, bool> predicate = null)
        {
            if (scene?.nodes == null || string.IsNullOrWhiteSpace(nodeType)) return null;
            return scene.nodes
                .Where(node => node != null && string.Equals(node.type, nodeType, StringComparison.Ordinal) &&
                    (predicate == null || predicate(node)))
                .OrderByDescending(Activity)
                .ThenBy(node => node.id, StringComparer.Ordinal)
                .FirstOrDefault();
        }

        private void Advance(SpatialScene scene)
        {
            if (!loader.IsPinned || loader.Scene == null) return;
            try
            {
                switch (stage)
                {
                    case SmokeStage.Overview:
                        LoadNext("overview", SelectHighestActivity(loader.Scene, "community"),
                            SmokeStage.Community, SelectPlayerTarget);
                        break;
                    case SmokeStage.Community:
                        var home = SelectHighestActivity(loader.Scene, "subreddit");
                        var link = home == null ? null : loader.Scene.links.FirstOrDefault(route => route.source == home.id || !route.directed && route.target == home.id);
                        connectedSystem = link == null ? null : link.source == home.id ? link.target : link.source;
                        LoadNext("community", home, SmokeStage.System, SelectPlayerTarget);
                        break;
                    case SmokeStage.System:
                        var star = registry.FindEntity(loader.Scene.root_id);
                        var gravity = star != null ? star.GetComponent<ClustrGalaxy.Simulation.GravitySource>() : null;
                        if (gravity?.Definition == null || Vector3.Distance(targeting.transform.position, star.transform.position) <= gravity.Definition.ExclusionRadius + ClustrGalaxy.Flight.ShipSurfaceSafety.HullRadius)
                            throw new InvalidOperationException("Live system loaded with the probe inside its stellar exclusion volume.");
                        Debug.Log("[ClustrGalaxy.LiveSmoke] Physical system start is outside stellar exclusion.");
                        LoadNext("system", SelectHighestActivity(loader.Scene, "post"),
                            SmokeStage.Discussion, SelectPlayerTarget);
                        break;
                    case SmokeStage.Discussion:
                        LoadNext("discussion", SelectHighestActivity(loader.Scene, "comment",
                                node => node.metrics != null && node.metrics.reply_count > 0),
                            SmokeStage.Thread, SelectPlayerTarget);
                        break;
                    case SmokeStage.Thread:
                        if (targeting.GetComponent<CommentThreadController>()?.DisplayedModel == null)
                            throw new InvalidOperationException("Thread did not reach the HUD tree.");
                        foreach (var node in loader.Scene.nodes)
                            if (node.id != loader.Scene.root_id && registry.FindEntity(node.id) != null)
                                throw new InvalidOperationException("Thread reply was materialized as a physical entity: " + node.id);
                        stage = SmokeStage.Complete;
                        Debug.Log($"[ClustrGalaxy.LiveSmoke] COMPLETE revision={loader.Manifest.revision_id} " +
                            $"catalog={loader.Manifest.spatial_catalog_id} thread={loader.Scene.root_id} " +
                            $"nodes={loader.Scene.nodes.Length} links={loader.Scene.links.Length}.");
                        if (Array.Exists(Environment.GetCommandLineArgs(), arg => arg == "--clustr-jump-smoke")) StartCoroutine(JumpJourney());
                        else if (!GalaxyLaunchOptions.IsBetaProfile(Environment.GetCommandLineArgs())) Application.Quit(0);
                        break;
                }
            }
            catch (Exception exception)
            {
                Fail($"stage={stage}: {exception.Message}");
            }
        }

        /// <summary>Record the failed stage and end a non-profile run with exit code 2.</summary>
        private void Fail(string reason)
        {
            if (Failed) return;
            stage = SmokeStage.Failed;
            Debug.LogError("[ClustrGalaxy.LiveSmoke] FAILED " + reason);
            if (!GalaxyLaunchOptions.IsBetaProfile(Environment.GetCommandLineArgs())) Application.Quit(2);
        }

        private IEnumerator JumpJourney()
        {
            yield return null;
            var navigation = targeting.GetComponent<ClustrGalaxy.Flight.GalaxyNavigationController>();
            var jump = targeting.GetComponent<ClustrGalaxy.Flight.GalaxyJumpController>();
            var beforeScene = loader.Scene; var beforeSystem = loader.Session.CurrentSystemId;
            var beforeProgress = loader.Session.Activities.State;
            if (string.IsNullOrWhiteSpace(connectedSystem)) { JumpFailure("Home system has no published connected system"); yield break; }
            var target = (ClustrGalaxy.Core.Domain.GalaxyEntityId)connectedSystem;
            navigation.SetMapOpen(false);
            if (!jump.Begin(target)) { JumpFailure("No eligible connected system"); yield break; }
            yield return new WaitForSecondsRealtime(.25f); jump.Cancel();
            if (jump.Active || loader.Session.CurrentSystemId != beforeSystem || !ReferenceEquals(beforeScene,loader.Scene))
            { JumpFailure("Cancelled charge replaced the usable scene"); yield break; }
            if (!jump.Begin(target)) { JumpFailure("Retry did not engage"); yield break; }
            var until=Time.realtimeSinceStartup+30;
            while(jump.Active && Time.realtimeSinceStartup<until)yield return null;
            if(jump.Active || navigation.IsMapOpen || targeting.GetComponent<Rigidbody>().isKinematic ||
                !targeting.GetComponent<ClustrGalaxy.Flight.ObserverProbeController>().enabled || loader.Session.CurrentSystemId.Value!=connectedSystem || loader.Scene.root_id!=connectedSystem ||
                !ClustrGalaxy.Flight.ShipSurfaceSafety.IsClear(targeting.transform.position) ||
                !ReferenceEquals(beforeProgress,loader.Session.Activities.State))
            { JumpFailure("Destination, clearance or progression assertion failed"); yield break; }
            // Returning must work even when the first system's render objects were pooled.
            if (!jump.Begin(beforeSystem)) { JumpFailure("Return jump by stable identity did not engage"); yield break; }
            until=Time.realtimeSinceStartup+30;
            while(jump.Active && Time.realtimeSinceStartup<until)yield return null;
            if(jump.Active || navigation.IsMapOpen || targeting.GetComponent<Rigidbody>().isKinematic ||
                !targeting.GetComponent<ClustrGalaxy.Flight.ObserverProbeController>().enabled || loader.Session.CurrentSystemId!=beforeSystem || loader.Scene.root_id!=beforeSystem.Value ||
                !ClustrGalaxy.Flight.ShipSurfaceSafety.IsClear(targeting.transform.position) ||
                !ReferenceEquals(beforeProgress,loader.Session.Activities.State))
            { JumpFailure("Return destination, clearance or progression assertion failed"); yield break; }
            Debug.Log("[ClustrGalaxy.LiveSmoke] JUMP COMPLETE · round trip by stable identity, safe arrivals, unchanged progression");
            Application.Quit(0);
        }
        private void JumpFailure(string reason)
        { stage=SmokeStage.Failed;Debug.LogError("[ClustrGalaxy.LiveSmoke] FAILED jump: "+reason);Application.Quit(2); }

        private void LoadNext(string completedScope, SpatialNode selected, SmokeStage next,
            Action<SpatialNode> request)
        {
            if (selected == null)
            {
                Fail($"no eligible node after {completedScope}.");
                return;
            }

            Debug.Log($"[ClustrGalaxy.LiveSmoke] {completedScope} ok; nodes={loader.Scene.nodes.Length} links={loader.Scene.links.Length}; selecting {selected.type} {selected.id}.");
            stage = next;
            deadline = Time.realtimeSinceStartup + 180f;
            StartCoroutine(SelectAfterPresentation(selected, request));
        }

        private IEnumerator SelectAfterPresentation(SpatialNode selected, Action<SpatialNode> request)
        {
            // Finish the committing frame and all presentation callbacks before another selection.
            yield return null;
            try { request(selected); }
            catch (Exception exception)
            {
                Fail("player selection: " + exception.Message);
            }
        }

        private void SelectPlayerTarget(SpatialNode node)
        {
            var entity = registry?.FindActiveEntity(node.id);
            if (entity == null || targeting == null)
                throw new InvalidOperationException("Required rendered target is unavailable: " + node.id);
            targeting.SelectTarget(entity);
        }

        private static long Activity(SpatialNode node)
        {
            if (node.metrics == null) return node.val;
            return Math.Max(node.val, Math.Max(node.metrics.activity_count,
                Math.Max(node.metrics.comment_count, node.metrics.reply_count)));
        }

        private void OnDestroy()
        {
            if (loader != null) loader.SceneCommitted -= Advance;
        }

        private enum SmokeStage
        {
            Overview,
            Community,
            System,
            Discussion,
            Thread,
            Complete,
            Failed
        }
    }
}
