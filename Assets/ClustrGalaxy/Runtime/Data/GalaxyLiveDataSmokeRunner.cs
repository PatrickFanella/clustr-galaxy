using System;
using System.Linq;
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

        public void Initialize(SpatialSceneLoader sceneLoader)
        {
            loader = sceneLoader ?? throw new ArgumentNullException(nameof(sceneLoader));
            loader.SceneLoaded += Advance;
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

        private void Advance()
        {
            if (!loader.IsPinned || loader.Scene == null) return;
            try
            {
                switch (stage)
                {
                    case SmokeStage.Overview:
                        LoadNext("overview", SelectHighestActivity(loader.Scene, "community"),
                            SmokeStage.Community, node => loader.LoadCommunity(node.id));
                        break;
                    case SmokeStage.Community:
                        LoadNext("community", SelectHighestActivity(loader.Scene, "subreddit"),
                            SmokeStage.System, node => loader.LoadSystem(node.id));
                        break;
                    case SmokeStage.System:
                        LoadNext("system", SelectHighestActivity(loader.Scene, "post"),
                            SmokeStage.Discussion, node => loader.LoadDiscussion(node.id));
                        break;
                    case SmokeStage.Discussion:
                        LoadNext("discussion", SelectHighestActivity(loader.Scene, "comment",
                                node => node.metrics != null && node.metrics.reply_count > 0),
                            SmokeStage.Thread, node => loader.LoadThread(node.id));
                        break;
                    case SmokeStage.Thread:
                        stage = SmokeStage.Complete;
                        Debug.Log($"[ClustrGalaxy.LiveSmoke] COMPLETE revision={loader.Manifest.revision_id} " +
                            $"catalog={loader.Manifest.spatial_catalog_id} thread={loader.Scene.root_id} " +
                            $"nodes={loader.Scene.nodes.Length} links={loader.Scene.links.Length}.");
                        break;
                }
            }
            catch (Exception exception)
            {
                stage = SmokeStage.Failed;
                Debug.LogError($"[ClustrGalaxy.LiveSmoke] FAILED stage={stage}: {exception.Message}");
            }
        }

        private void LoadNext(string completedScope, SpatialNode selected, SmokeStage next,
            Action<SpatialNode> request)
        {
            if (selected == null)
            {
                stage = SmokeStage.Failed;
                Debug.LogError($"[ClustrGalaxy.LiveSmoke] FAILED: no eligible node after {completedScope}.");
                return;
            }

            Debug.Log($"[ClustrGalaxy.LiveSmoke] {completedScope} ok; selecting {selected.type} {selected.id}.");
            stage = next;
            request(selected);
        }

        private static long Activity(SpatialNode node)
        {
            if (node.metrics == null) return node.val;
            return Math.Max(node.val, Math.Max(node.metrics.activity_count,
                Math.Max(node.metrics.comment_count, node.metrics.reply_count)));
        }

        private void OnDestroy()
        {
            if (loader != null) loader.SceneLoaded -= Advance;
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
