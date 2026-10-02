using System;
using System.Numerics;
using ClustrGalaxy.Core.Navigation;
using ClustrGalaxy.Core.Domain;
using NUnit.Framework;
namespace ClustrGalaxy.Tests
{
    public sealed class FlightTelemetryTests
    {
        private static CelestialBodyDefinition Planet => new((GalaxyEntityId)"post:p",300,360000,300);
        [Test] public void CircularAndEllipticOrbitsHaveCorrectExtrema()
        {
            var circular=FlightTelemetry.Calculate(Planet,new Vector3(600,0,0),new Vector3(0,0,(float)Math.Sqrt(600)),240);
            Assert.That(circular.Periapsis,Is.EqualTo(600).Within(.01));Assert.That(circular.Apoapsis,Is.EqualTo(600).Within(.01));
            Assert.That(circular.Period,Is.EqualTo(2*Math.PI*Math.Sqrt(600)).Within(.01));
            var ellipse=FlightTelemetry.Calculate(Planet,new Vector3(600,0,0),new Vector3(0,0,(float)Math.Sqrt(800)),240);
            Assert.That(ellipse.Periapsis,Is.EqualTo(600).Within(.01));Assert.That(ellipse.Apoapsis,Is.EqualTo(1200).Within(.01));
            Assert.That(ellipse.Impact,Is.False);Assert.That(ellipse.Escaping,Is.False);
        }
        [Test] public void EscapingOutboundDoesNotWarnAboutAPastPeriapsis()
        {
            var outbound=FlightTelemetry.Calculate(Planet,new Vector3(600,0,0),new Vector3(100,0,0),240);
            var inbound=FlightTelemetry.Calculate(Planet,new Vector3(600,0,0),new Vector3(-100,0,0),240);
            Assert.That(outbound.Escaping,Is.True);Assert.That(outbound.Impact,Is.False);
            Assert.That(inbound.Impact,Is.True);Assert.That(inbound.ClosingSpeed,Is.EqualTo(100));
            Assert.That(inbound.StoppingDistance,Is.EqualTo(10000d/480).Within(.0001));
            Assert.That(FlightTelemetry.Calculate(Planet,Vector3.Zero,Vector3.Zero,240).Available,Is.False);
        }
        [Test] public void InvalidOrbitRequestPreservesExistingGuidance()
        {
            var orbit=new OrbitalAutopilot();
            Assert.That(orbit.Begin((GalaxyEntityId)"post:good",new Vector3(600,0,0),Vector3.Zero,360000,300),Is.True);
            Assert.That(orbit.Begin((GalaxyEntityId)"comment:unsafe",new Vector3(30,0,0),Vector3.Zero,400,10,16,15,240),Is.False);
            Assert.That(orbit.TargetId.Value,Is.EqualTo("post:good"));
            Assert.That(orbit.Begin((GalaxyEntityId)"comment:safe",new Vector3(30,0,0),Vector3.Zero,400,10,16,25,240),Is.True);
            Assert.That(orbit.Radius,Is.EqualTo(25));
            Assert.That(orbit.Begin((GalaxyEntityId)"post:heavy",new Vector3(600,0,0),Vector3.Zero,360000000,300,480,900,1),Is.False);
        }
        [Test] public void MovingBodyArrivalMatchesItsVelocity()
        {
            var navigation=new GalaxyNavigation();var velocity=new Vector3(0,0,20);
            var target=new NavigationTarget((GalaxyEntityId)"post:p",GalaxyEntityKind.Post,"Planet",new Vector3(1000,0,0),true,false,100,0,velocity);
            Assert.That(navigation.Apply(new PlotApproachIntent(target,false)).Succeeded,Is.True);
            var result=navigation.AdvancePhysics(new NavigationPose(new Vector3(876,0,0),Quaternion.Identity),velocity,NavigationFlightMode.Autopilot,_=>target,240);
            Assert.That(result.State.AutopilotArrived,Is.True);Assert.That(result.Effect.Velocity,Is.EqualTo(velocity));
        }
    }
}
