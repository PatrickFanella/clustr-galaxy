using System.Collections;
using ClustrGalaxy.Flight;
using ClustrGalaxy.Gameplay;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
namespace ClustrGalaxy.Tests
{
    public sealed class ShipControlPlayModeTests
    {
        [UnityTest] public IEnumerator GravityAccumulatesInInertialFlightAndBrakeArrestsDrift()
        {
            var source=new GameObject("Test gravity body");var ship=new GameObject("Test falling ship");var pad=InputSystem.AddDevice<Gamepad>();
            var background=InputSystem.settings.backgroundBehavior;InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            try
            {
                source.AddComponent<ClustrGalaxy.Simulation.GravitySource>().Initialize(10000,1,12);
                ship.transform.position=new Vector3(100,0,0);
                var controller=ship.AddComponent<ObserverProbeController>();var modes = new GalaxyModeState();modes.SetFlightMode(FlightControlMode.PilotMastery);controller.Initialize(modes);var body=ship.GetComponent<Rigidbody>();
                Assert.That(controller.GravityModeEnabled,Is.True);
                for(var i=0;i<50;i++)yield return new WaitForFixedUpdate();
                Assert.That(body.linearVelocity.x,Is.LessThan(-.8f));Assert.That(body.position.x,Is.LessThan(99.7f));
                InputSystem.QueueStateEvent(pad,new GamepadState().WithButton(GamepadButton.East));yield return null;
                for(var i=0;i<5;i++)yield return new WaitForFixedUpdate();
                Assert.That(body.linearVelocity.magnitude,Is.LessThan(.1f));
            }
            finally{InputSystem.RemoveDevice(pad);InputSystem.settings.backgroundBehavior=background;Object.Destroy(ship);Object.Destroy(source);}
        }
        [UnityTest] public IEnumerator ShipStickRotatesWithoutThrustAndTriggersDriveTheShip()
        {
            var root=new GameObject("Ship control binding test");var pad=InputSystem.AddDevice<Gamepad>();
            var background=InputSystem.settings.backgroundBehavior;InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            try
            {
                var controller=root.AddComponent<ObserverProbeController>();controller.Initialize(new GalaxyModeState());var body=root.GetComponent<Rigidbody>();
                InputSystem.QueueStateEvent(pad,new GamepadState{rightStick=Vector2.up});yield return null;
                yield return new WaitForFixedUpdate();yield return new WaitForFixedUpdate();
                Assert.That(body.linearVelocity.magnitude,Is.LessThan(.001f));Assert.That(root.transform.InverseTransformDirection(body.angularVelocity).x,Is.LessThan(-.01f));
                body.linearVelocity=Vector3.zero;body.angularVelocity=Vector3.zero;
                InputSystem.QueueStateEvent(pad,new GamepadState{leftStick=Vector2.up});yield return null;
                yield return new WaitForFixedUpdate();yield return new WaitForFixedUpdate();
                Assert.That(Vector3.Dot(body.linearVelocity,root.transform.up),Is.GreaterThan(.01f));
                body.linearVelocity=Vector3.zero;body.angularVelocity=Vector3.zero;
                InputSystem.QueueStateEvent(pad,new GamepadState{leftStick=Vector2.right});yield return null;
                yield return new WaitForFixedUpdate();yield return new WaitForFixedUpdate();
                Assert.That(root.transform.InverseTransformDirection(body.angularVelocity).y,Is.GreaterThan(.01f));
                Assert.That(body.linearVelocity.magnitude,Is.LessThan(.001f));
                InputSystem.QueueStateEvent(pad,new GamepadState{rightStick=Vector2.right});yield return null;
                yield return new WaitForFixedUpdate();yield return new WaitForFixedUpdate();
                Assert.That(root.transform.InverseTransformDirection(body.angularVelocity).z,Is.LessThan(-.01f));
                InputSystem.QueueStateEvent(pad,new GamepadState{rightTrigger=1});yield return null;
                yield return new WaitForFixedUpdate();yield return new WaitForFixedUpdate();
                Assert.That(Vector3.Dot(body.linearVelocity,root.transform.forward),Is.GreaterThan(.01f));
                InputSystem.QueueStateEvent(pad,new GamepadState());yield return null;
                body.linearVelocity=root.transform.forward*ClustrGalaxy.Generation.GalaxyTuning.CruiseSpeed;
                for(var i=0;i<25;i++)yield return new WaitForFixedUpdate();
                Assert.That(body.linearVelocity.magnitude,Is.LessThan(.001f),"Assist should stop from cruise within half a second after releasing thrust.");
                controller.enabled=false;body.linearVelocity=Vector3.zero;body.angularVelocity=Vector3.zero;
                yield return new WaitForFixedUpdate();yield return new WaitForFixedUpdate();
                Assert.That(body.linearVelocity,Is.EqualTo(Vector3.zero));
            }
            finally{InputSystem.RemoveDevice(pad);InputSystem.settings.backgroundBehavior=background;Object.Destroy(root);}
        }
    }
}
