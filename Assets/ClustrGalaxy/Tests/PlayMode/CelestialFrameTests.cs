using System.Collections;
using ClustrGalaxy.Generation;
using ClustrGalaxy.Simulation;
using ClustrGalaxy.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace ClustrGalaxy.Tests
{
    public sealed class CelestialFrameTests
    {
        [Test] public void CachedLocalEphemerisTracksParentChangesAndPooledReinitialization()
        {
            var root = new GameObject("Cache root"); var parent = new GameObject("Cache parent"); var moon = new GameObject("Cache moon");
            try
            {
                var parentOrbit = parent.AddComponent<KeplerOrbit>();
                var childOrbit = moon.AddComponent<KeplerOrbit>();
                var parentElements = new OrbitalElements(1000, .2, .3, .4, .5, .6);
                var childElements = new OrbitalElements(20, .1, .2, .3, .4, .5);
                parentOrbit.Initialize(root.transform, parentElements, 1000000);
                childOrbit.Initialize(parent.transform, childElements, 1000);
                const double time = 100;
                var original = childOrbit.LogicalPositionAt(time);
                Assert.That(childOrbit.LogicalPositionAt(time), Is.EqualTo(original));
                childOrbit.LogicalPositionAt(time + 50);
                Assert.That(childOrbit.LogicalPositionAt(time), Is.EqualTo(original));
                parentOrbit.Initialize(root.transform, new OrbitalElements(2000, .3, .4, .5, .6, .7), 2000000);
                var expected = parentOrbit.LogicalPositionAt(time).Add(KeplerOrbitMath.PrecisePositionAtTime(childElements, 1000, time));
                Assert.That(childOrbit.LogicalPositionAt(time), Is.EqualTo(expected));
                Assert.That(childOrbit.LogicalPositionAt(time), Is.Not.EqualTo(original));
                childElements = new OrbitalElements(40, .2, .3, .4, .5, .6);
                childOrbit.Initialize(parent.transform, childElements, 2000);
                expected = parentOrbit.LogicalPositionAt(time).Add(KeplerOrbitMath.PrecisePositionAtTime(childElements, 2000, time));
                Assert.That(childOrbit.LogicalPositionAt(time), Is.EqualTo(expected));
            }
            finally { Object.DestroyImmediate(moon); Object.DestroyImmediate(parent); Object.DestroyImmediate(root); }
        }
        [Test] public void ReusedStaticBodyDoesNotInheritADisabledOrbitVelocity()
        {
            var parent = new GameObject("Old orbit center"); var body = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            try
            {
                var orbit = body.AddComponent<KeplerOrbit>(); orbit.Initialize(parent.transform, new OrbitalElements(100, 0, 0, 0, 0, 0), 1000000); orbit.enabled = false;
                body.AddComponent<GalaxyEntity>().Initialize("subreddit:reused", "Reused star", "subreddit star", 1);
                var source = body.AddComponent<GravitySource>(); source.Initialize(1000, .1, 12, new DoubleVector3(500.125, 0, 0));
                source.RefreshStaticPosition();
                Assert.That(source.CenterVelocity, Is.EqualTo(Vector3.zero)); Assert.That(source.MaximumSpeed, Is.Zero);
                Assert.That(source.LogicalPosition.X, Is.EqualTo(500.125));
                var expected = new DoubleVector3(500.125, 0, 0).RelativeTo(FloatingOrigin.Active != null ? FloatingOrigin.Active.LogicalOrigin : DoubleVector3.Zero);
                Assert.That(body.transform.position, Is.EqualTo(expected));
            }
            finally { Object.DestroyImmediate(body); Object.DestroyImmediate(parent); }
        }
        [UnityTest] public IEnumerator InterpolatedShipJumpAndRebasePreserveDestinationClearance()
        {
            var root = new GameObject("Jump frame"); var ship = new GameObject("Jump observer");
            try
            {
                root.AddComponent<CelestialFrame>();
                var star = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                star.transform.SetParent(root.transform, false); star.transform.position = new Vector3(60000, 18000, -50000);
                star.AddComponent<GalaxyEntity>().Initialize("subreddit:jump-frame", "Jump", "subreddit star", 1);
                star.AddComponent<GravitySource>().Initialize(1000000, .1, 12);
                var body = ship.AddComponent<Rigidbody>(); body.useGravity = false;
                body.interpolation = RigidbodyInterpolation.Interpolate;
                var origin = ship.AddComponent<FloatingOrigin>();
                yield return new WaitForFixedUpdate(); yield return null;
                var arrival = star.transform.position + Vector3.up * 2750;
                body.position = arrival; ship.transform.position = arrival;
                origin.Rebase(-arrival); Physics.SyncTransforms();
                for (var i = 0; i < 4; i++) { yield return new WaitForFixedUpdate(); yield return null; }
                Assert.That(Vector3.Distance(body.position, star.transform.position), Is.EqualTo(2750).Within(.1),
                    "ship=" + body.position + " star=" + star.transform.position + " origin=" + origin.LogicalOrigin);
            }
            finally { Object.Destroy(ship); Object.Destroy(root); }
        }
        [UnityTest] public IEnumerator DistantNestedFramesPreserveNearBodyPrecisionAndVelocity()
        {
            var root = new GameObject("Precision frame");
            var ship = new GameObject("Precision observer");
            try
            {
                root.AddComponent<CelestialFrame>();
                var group = new GameObject("Nested scope"); group.transform.SetParent(root.transform, false);
                group.AddComponent<CelestialFrame>();
                var star = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                star.transform.SetParent(group.transform, false); star.transform.position = new Vector3(80000000, 0, 0);
                star.AddComponent<GalaxyEntity>().Initialize("subreddit:precision", "Precision", "subreddit star", 1);
                star.AddComponent<GravitySource>().Initialize(1000000, .1, 12, new DoubleVector3(80000000.125, 0, 0));
                var planet = new GameObject("Near orbit"); planet.transform.SetParent(group.transform, false);
                var orbit = planet.AddComponent<KeplerOrbit>();
                orbit.Initialize(star.transform, new OrbitalElements(100, 0, 0, 0, 0, 0), 1000000);
                ship.transform.position = new Vector3(80000120, 0, 0);
                var body = ship.AddComponent<Rigidbody>(); body.useGravity = false;
                body.linearVelocity = new Vector3(1.25f, -.5f, .75f);
                var origin = ship.AddComponent<FloatingOrigin>();
                origin.Rebase(new Vector3(-80000000, 0, 0));
                for (var i = 0; i < 10; i++)
                {
                    yield return new WaitForFixedUpdate();
                    yield return null; // Rebase between physics ticks must not advance the ephemeris.
                    var velocity = body.linearVelocity;
                    var previousPosition = planet.transform.position;
                    origin.Rebase(new Vector3(.125f, -.25f, .0625f));
                    Assert.That(Vector3.Distance(planet.transform.position, previousPosition + new Vector3(.125f, -.25f, .0625f)), Is.LessThan(.0001f));
                    Assert.That(root.transform.position, Is.EqualTo(Vector3.zero));
                    Assert.That(group.transform.position, Is.EqualTo(Vector3.zero));
                    var expected = orbit.LogicalPositionAt(orbit.SimulationTime).RelativeTo(origin.LogicalOrigin);
                    Assert.That(Vector3.Distance(planet.transform.position, expected), Is.LessThan(.0001f));
                    Assert.That(Vector3.Distance(body.linearVelocity, velocity), Is.LessThan(.00001f));
                    var starExpected = new DoubleVector3(80000000.125, 0, 0).RelativeTo(origin.LogicalOrigin);
                    Assert.That(Vector3.Distance(star.transform.position, starExpected), Is.LessThan(.0001f));
                }
            }
            finally { Object.Destroy(ship); Object.Destroy(root); }
        }
    }
}
