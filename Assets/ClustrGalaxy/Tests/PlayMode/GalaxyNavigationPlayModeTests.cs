using System.Collections;
using ClustrGalaxy.Flight;
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
    }
}
