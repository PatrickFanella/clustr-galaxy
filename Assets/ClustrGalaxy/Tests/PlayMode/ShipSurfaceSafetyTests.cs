using System.Collections;
using ClustrGalaxy.Flight;
using ClustrGalaxy.Gameplay;
using ClustrGalaxy.Simulation;
using ClustrGalaxy.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace ClustrGalaxy.Tests
{
    public sealed class ShipSurfaceSafetyTests
    {
        [UnityTest] public IEnumerator HighSpeedCrossingRecoversOnceAndRequiresResume()
        {
            var planet=GameObject.CreatePrimitive(PrimitiveType.Sphere);var ship=new GameObject("Impact test ship");
            try
            {
                planet.transform.localScale=Vector3.one*200;
                planet.AddComponent<GalaxyEntity>().Initialize("post:impact","Impact planet","post planet",1);
                planet.AddComponent<GravitySource>().Initialize(40000,10,6);
                ship.transform.position=new Vector3(-300,0,0);
                var controller=ship.AddComponent<ObserverProbeController>();var modes=new GalaxyModeState();modes.SetFlightMode(FlightControlMode.PilotMastery);controller.Initialize(modes);
                var body=ship.GetComponent<Rigidbody>();body.linearVelocity=new Vector3(50000,0,0);
                Physics.SyncTransforms();yield return new WaitForFixedUpdate();yield return new WaitForFixedUpdate();
                var safety=ship.GetComponent<ShipSurfaceSafety>();
                Assert.That(controller.Simulation.Recovering,Is.True);Assert.That(body.isKinematic,Is.True);
                Assert.That(body.position.magnitude,Is.GreaterThan(500));Assert.That(safety.RecoveryCount,Is.EqualTo(1));
                for(var i=0;i<10;i++)yield return new WaitForFixedUpdate();
                Assert.That(safety.RecoveryCount,Is.EqualTo(1));Assert.That(safety.Resume(),Is.True);
                Assert.That(body.isKinematic,Is.False);Assert.That(body.linearVelocity.magnitude,Is.LessThan(.01));
            }
            finally{Object.Destroy(ship);Object.Destroy(planet);}
        }
        [UnityTest] public IEnumerator StellarExclusionRecoversBeforeSlowSurfaceContact()
        {
            var star=GameObject.CreatePrimitive(PrimitiveType.Sphere);var ship=new GameObject("Stellar safety test");
            try
            {
                star.transform.localScale=Vector3.one*200;
                star.AddComponent<GalaxyEntity>().Initialize("subreddit:star","Star","subreddit star",1);
                star.AddComponent<GravitySource>().Initialize(60000,10,12);
                ship.transform.position=new Vector3(352.01f,0,0);
                var controller=ship.AddComponent<ObserverProbeController>();var modes=new GalaxyModeState();modes.SetFlightMode(FlightControlMode.PilotMastery);controller.Initialize(modes);
                ship.GetComponent<Rigidbody>().linearVelocity=new Vector3(-1,0,0);
                Physics.SyncTransforms();for(var i=0;i<4;i++)yield return new WaitForFixedUpdate();
                Assert.That(controller.Simulation.Recovering,Is.True);
                Assert.That(ship.GetComponent<Rigidbody>().position.magnitude,Is.GreaterThan(800));
            }
            finally{Object.Destroy(ship);Object.Destroy(star);}
        }
        [UnityTest] public IEnumerator SlowPlanetContactDoesNotTriggerRecovery()
        {
            var planet=GameObject.CreatePrimitive(PrimitiveType.Sphere);var ship=new GameObject("Contact test ship");
            try
            {
                planet.transform.localScale=Vector3.one*200;
                planet.AddComponent<GalaxyEntity>().Initialize("post:contact","Contact planet","post planet",1);
                planet.AddComponent<GravitySource>().Initialize(40000,10,6);
                ship.transform.position=new Vector3(108,0,0);
                var controller=ship.AddComponent<ObserverProbeController>();var modes=new GalaxyModeState();modes.SetFlightMode(FlightControlMode.PilotMastery);controller.Initialize(modes);
                var body=ship.GetComponent<Rigidbody>();body.linearVelocity=new Vector3(-10,0,0);
                Physics.SyncTransforms();for(var i=0;i<60;i++)yield return new WaitForFixedUpdate();
                Assert.That(ship.GetComponent<ShipSurfaceSafety>().RecoveryCount,Is.Zero);
                Assert.That(body.position.magnitude,Is.GreaterThan(100));
            }
            finally{Object.Destroy(ship);Object.Destroy(planet);}
        }
    }
}
