using System.Collections;
using System.IO;
using ClustrGalaxy.Flight;
using ClustrGalaxy.Gameplay;
using ClustrGalaxy.Interaction;
using ClustrGalaxy.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ClustrGalaxy.Tests
{
    public sealed class GalaxyNavigationPlayModeTests
    {
        [UnityTest]
        public IEnumerator SurveyStart_HidesEverythingOutsideTheStartingSystem()
        {
            var root = new GameObject("survey visibility test root");
            var storageRoot = Path.Combine(Path.GetTempPath(), "clustr-survey-playmode-" + System.Guid.NewGuid().ToString("N"));
            try
            {
                var modes = new GalaxyModeState();
                var store = SurveyDiscoveryStore.Open(storageRoot, "test-pilot", SurveySnapshotKey.Pinned(9, 3),
                    "subreddit:technology");
                var controller = root.AddComponent<GalaxySurveyVisibilityController>();
                controller.Initialize(modes, store);

                var technology = CreateSurveyEntity(root.transform, "subreddit:technology", "subreddit star",
                    "subreddit:technology");
                var localPost = CreateSurveyEntity(root.transform, "post:open-source-ai", "post planet",
                    "subreddit:technology");
                var localUser = CreateSurveyEntity(root.transform, "user:traveler-00", "user ship",
                    "subreddit:technology");
                var localComment = CreateSurveyEntity(root.transform, "comment:open-source-ai-1", "discussion satellite",
                    "subreddit:technology");
                var localReply = CreateSurveyEntity(root.transform, "comment:open-source-ai-1-reply-1", "reply depth 1",
                    "subreddit:technology");
                var remote = CreateSurveyEntity(root.transform, "subreddit:remote", "subreddit star",
                    "subreddit:remote");

                modes.SetWorldMode(GalaxyWorldMode.Survey);
                Assert.That(controller.StartAt(technology.StableId), Is.True);
                yield return null;

                Assert.That(GalaxyEntity.TryGet(technology.StableId, out _), Is.True);
                Assert.That(GalaxyEntity.TryGet(localPost.StableId, out _), Is.True);
                Assert.That(store.IsEntityKnown(localUser.StableId), Is.False);
                Assert.That(store.IsEntityKnown(localComment.StableId), Is.False);
                Assert.That(store.IsEntityKnown(localReply.StableId), Is.False);
                Assert.That(localUser.gameObject.activeSelf, Is.False);
                Assert.That(localComment.gameObject.activeSelf, Is.False);
                Assert.That(localReply.gameObject.activeSelf, Is.False);
                Assert.That(remote.gameObject.activeSelf, Is.False);
                Assert.That(GalaxyEntity.TryGet(remote.StableId, out _), Is.False);
                Assert.That(store.IsEntityKnown(remote.StableId), Is.False);
                Assert.That(File.Exists(store.StoragePath), Is.True);

                modes.SetWorldMode(GalaxyWorldMode.Explore);
                yield return null;
                Assert.That(remote.gameObject.activeSelf, Is.True);
                Assert.That(store.IsEntityKnown(remote.StableId), Is.False);

                modes.SetWorldMode(GalaxyWorldMode.Survey);
                yield return null;
                Assert.That(remote.gameObject.activeSelf, Is.False);
                Assert.That(store.IsEntityKnown(remote.StableId), Is.False);
            }
            finally
            {
                Object.Destroy(root);
                if (Directory.Exists(storageRoot)) Directory.Delete(storageRoot, recursive: true);
            }
        }

        [UnityTest]
        [Ignore("Enabled by the implementing task")]
        public IEnumerator Scan_RevealsConnectedRouteAndDestination()
        {
            yield break;
        }

        [UnityTest]
        [Ignore("Enabled by the implementing task")]
        public IEnumerator ModeSwitch_PreservesIndependentSurveyProgress()
        {
            yield break;
        }

        [UnityTest]
        public IEnumerator HierarchicalBack_RestoresTheExactRuntimeProbePose()
        {
            var probe = new GameObject("navigation test probe");
            var cameraObject = new GameObject("navigation test camera");
            try
            {
                var body = probe.AddComponent<Rigidbody>();
                probe.AddComponent<ObserverProbeController>();
                var targeting = probe.AddComponent<ProbeTargeting>();
                cameraObject.transform.SetParent(probe.transform, false);
                var camera = cameraObject.AddComponent<Camera>();
                var navigation = probe.AddComponent<GalaxyNavigationController>();
                navigation.Initialize(targeting, camera);

                body.position = new Vector3(10f, 20f, 30f);
                body.rotation = Quaternion.Euler(0f, 15f, 0f);
                navigation.NavigateTo(GalaxyNavigationLevel.Region, "community_1");
                body.position = new Vector3(110f, 120f, 130f);
                body.rotation = Quaternion.Euler(0f, 45f, 0f);
                navigation.NavigateTo(GalaxyNavigationLevel.System, "subreddit_2");

                navigation.NavigateBack();
                yield return new WaitForFixedUpdate();

                Assert.That(navigation.Level, Is.EqualTo(GalaxyNavigationLevel.Region));
                Assert.That(body.position, Is.EqualTo(new Vector3(110f, 120f, 130f)));
                Assert.That(Quaternion.Angle(body.rotation, Quaternion.Euler(0f, 45f, 0f)), Is.LessThan(0.001f));
            }
            finally
            {
                Object.Destroy(probe);
            }
        }

        [UnityTest]
        public IEnumerator PooledEntity_DoesNotRemainTargetableAfterRelease()
        {
            var owner = new GameObject("pool play mode owner");
            try
            {
                var pool = new GalaxyEntityPool(owner.transform);
                var body = pool.Acquire(PrimitiveType.Sphere, owner.transform);
                body.GetComponent<GalaxyEntity>().Initialize("comment_1", "Comment", "comment", 1);
                Assert.That(GalaxyEntity.TryGet("comment_1", out _), Is.True);

                pool.Release(body);
                yield return null;

                Assert.That(GalaxyEntity.TryGet("comment_1", out _), Is.False);
            }
            finally
            {
                Object.Destroy(owner);
            }
        }

        [UnityTest]
        public IEnumerator SurveyStart_RejectsExistingProfileWithDifferentStartingSystemWithoutMutation()
        {
            var root = new GameObject("survey mismatch test root");
            var storageRoot = Path.Combine(Path.GetTempPath(), "clustr-survey-mismatch-" + System.Guid.NewGuid().ToString("N"));
            try
            {
                var key = SurveySnapshotKey.Pinned(9, 3);
                var existing = SurveyDiscoveryStore.Open(storageRoot, "test-pilot", key, "subreddit:remote");
                existing.DiscoverEntity("post:remote-only");
                existing.Save();
                var before = File.ReadAllText(existing.StoragePath);

                var controller = root.AddComponent<GalaxySurveyVisibilityController>();
                controller.Initialize(new GalaxyModeState(), key, "test-pilot", storageRoot);
                CreateSurveyEntity(root.transform, "subreddit:technology", "subreddit star", "subreddit:technology");

                Assert.That(controller.StartAt("subreddit:technology"), Is.False);
                Assert.That(File.ReadAllText(existing.StoragePath), Is.EqualTo(before));
                Assert.That(controller.DiscoveryStore, Is.Null);
                yield return null;
            }
            finally
            {
                Object.Destroy(root);
                if (Directory.Exists(storageRoot)) Directory.Delete(storageRoot, recursive: true);
            }
        }

        [UnityTest]
        public IEnumerator SurveyMapCommand_EntersSurveyWithoutChoosingAStartingSystem()
        {
            var probe = new GameObject("survey map command probe");
            var cameraObject = new GameObject("survey map command camera");
            try
            {
                probe.AddComponent<Rigidbody>();
                var flight = probe.AddComponent<ObserverProbeController>();
                var modes = new GalaxyModeState();
                flight.Initialize(modes);
                var targeting = probe.AddComponent<ProbeTargeting>();
                var visibility = probe.AddComponent<GalaxySurveyVisibilityController>();
                visibility.Initialize(modes, SurveySnapshotKey.Demo, "test-pilot", Path.GetTempPath());
                visibility.BindTargeting(targeting);
                cameraObject.transform.SetParent(probe.transform, false);
                var camera = cameraObject.AddComponent<Camera>();
                var navigation = probe.AddComponent<GalaxyNavigationController>();
                navigation.Initialize(targeting, camera);
                navigation.BindDiscoveryVisibility(visibility);

                Assert.That(navigation.EnterSurvey(), Is.True);
                Assert.That(modes.WorldMode, Is.EqualTo(GalaxyWorldMode.Survey));
                Assert.That(navigation.IsMapOpen, Is.True);
                Assert.That(visibility.RequiresStartingSystemChoice, Is.True);
                yield return null;
            }
            finally
            {
                Object.Destroy(probe);
                Object.Destroy(cameraObject);
            }
        }

        private static GalaxyEntity CreateSurveyEntity(Transform parent, string stableId, string entityType,
            string systemStableId)
        {
            var body = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            body.transform.SetParent(parent, false);
            var entity = body.AddComponent<GalaxyEntity>();
            entity.Initialize(stableId, stableId, entityType, 1);
            body.AddComponent<GalaxySurveyMembership>().Initialize(systemStableId);
            return entity;
        }
    }
}
