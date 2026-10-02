using System;
using System.Numerics;
using ClustrGalaxy.Core.Navigation;
using NUnit.Framework;
namespace ClustrGalaxy.Tests
{
    public sealed class ShipPropulsionTests
    {
        [Test] public void CombinedAxesCannotExceedActuatorEnvelope()
        {
            var ship=ShipDefinition.Reference;
            var result=ShipPropulsion.Allocate(ship,new PropulsionRequest(new Vector3(100000),new Vector3(100000)));
            Assert.That((result.Force/ship.ForceLimits).Length(),Is.EqualTo(1).Within(.00001));
            Assert.That((result.Torque/ship.TorqueLimits).Length(),Is.EqualTo(1).Within(.00001));
            Assert.That(result.Force.X,Is.GreaterThan(0));
        }
        [Test] public void SameForceAndTorqueRespondToMassAndInertia()
        {
            var light=new ShipDefinition(100,new Vector3(100),new Vector3(10000),new Vector3(1000));
            var heavy=new ShipDefinition(200,new Vector3(400),new Vector3(10000),new Vector3(1000));
            var request=new PropulsionRequest(new Vector3(1000,0,0),new Vector3(100,0,0));
            var a=ShipPropulsion.Allocate(light,request);var b=ShipPropulsion.Allocate(heavy,request);
            Assert.That(a.Force.X/light.Mass,Is.EqualTo(2*b.Force.X/heavy.Mass));
            Assert.That(a.Torque.X/light.Inertia.X,Is.EqualTo(4*b.Torque.X/heavy.Inertia.X));
        }
        [Test] public void NeutralMasteryNeverErasesFastLinearOrAngularMomentum()
        {
            var state=new FlightSimulationState();
            var command=new FlightCommand(state.SessionId,1,NavigationFlightMode.PilotMastery,Vector3.Zero,Vector3.Zero,Vector3.Zero,false,false);
            var result=ShipPropulsion.Calculate(state.Ship,command,new Vector3(10000),new Vector3(100),Vector3.Zero,.02f,92,96,240,1.75f,Vector3.One,12);
            Assert.That(result.Force,Is.EqualTo(Vector3.Zero));Assert.That(result.Torque,Is.EqualTo(Vector3.Zero));
        }
        [Test] public void GravityCompensationConsumesAvailableThrust()
        {
            var state=new FlightSimulationState();
            var command=new FlightCommand(state.SessionId,1,NavigationFlightMode.Autopilot,Vector3.Zero,Vector3.Zero,Vector3.Zero,false,false);
            var gravity=new Vector3(0,-300,0);
            var result=ShipPropulsion.Calculate(state.Ship,command,Vector3.Zero,Vector3.Zero,gravity,.02f,92,96,240,1.75f,Vector3.One,12);
            Assert.That(result.Force.Y/state.Ship.Mass+gravity.Y,Is.LessThan(-59));
        }
        [Test] public void CommandsRejectWrongSessionReplayAndRecoveryMutation()
        {
            var state=new FlightSimulationState();
            FlightCommand Command(string id,long sequence)=>new(id,sequence,NavigationFlightMode.PilotAssist,Vector3.Zero,Vector3.Zero,Vector3.Zero,false,false);
            Assert.That(state.Accept(Command("other",1)),Is.False);
            Assert.That(state.Accept(Command(state.SessionId,1)),Is.True);
            Assert.That(state.Accept(Command(state.SessionId,1)),Is.False);
            state.BeginRecovery("impact",(ClustrGalaxy.Core.Domain.GalaxyEntityId)"post:p");
            Assert.That(state.Accept(Command(state.SessionId,2)),Is.False);
            state.Resume();Assert.That(state.Accept(Command(state.SessionId,2)),Is.True);
        }
        [Test] public void SweepDetectsCrossingEvenWhenBothEndpointsAreOutside()
        {
            Assert.That(SurfaceIntersection.Sweep(new Vector3(-1000,0,0),new Vector3(1000,0,0),100,out var fraction),Is.True);
            Assert.That(fraction,Is.EqualTo(.45f).Within(.000001));
            Assert.That(SurfaceIntersection.Sweep(new Vector3(-1000,101,0),new Vector3(1000,101,0),100,out _),Is.False);
            Assert.Throws<ArgumentException>(()=>SurfaceIntersection.Sweep(new Vector3(float.NaN,0,0),Vector3.Zero,100,out _));
        }
    }
}
