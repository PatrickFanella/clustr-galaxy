using System;
using System.Numerics;
using ClustrGalaxy.Core.Navigation;
using ClustrGalaxy.Core.Domain;
using ClustrGalaxy.Generation;
using ClustrGalaxy.Simulation;
using NUnit.Framework;

namespace ClustrGalaxy.Tests
{
    public sealed class OrbitalFlightTests
    {
        [Test] public void CircularOrbitCoastsForTenPeriodsWithoutRadialCollapse()
        {
            const double mu=360000, radius=600, step=.02;
            double x=radius,y=0,vx=0,vy=Math.Sqrt(mu/radius),minimum=radius,maximum=radius;
            var period=2*Math.PI*Math.Sqrt(radius*radius*radius/mu);
            var maximumEnergyError=0d;var initialEnergy=-mu/(2*radius);
            for(var i=0;i<10*period/step;i++)
            {
                var r=Math.Sqrt(x*x+y*y);var a=-mu/(r*r*r);
                vx+=x*a*step*.5;vy+=y*a*step*.5;x+=vx*step;y+=vy*step;
                r=Math.Sqrt(x*x+y*y);a=-mu/(r*r*r);vx+=x*a*step*.5;vy+=y*a*step*.5;
                minimum=Math.Min(minimum,r);maximum=Math.Max(maximum,r);
                maximumEnergyError=Math.Max(maximumEnergyError,Math.Abs(((vx*vx+vy*vy)*.5-mu/r-initialEnergy)/initialEnergy));
            }
            Assert.That(maximum-minimum,Is.LessThan(.01));
            Assert.That(maximumEnergyError,Is.LessThan(.000001));
        }
        [Test] public void CaptureConvergesAndReleasedOrbitRemainsBound()
        {
            var orbit=new OrbitalAutopilot();const double mu=360000;var position=new Vector3(1100,0,0);var velocity=Vector3.Zero;
            Assert.That(orbit.Begin((GalaxyEntityId)"planet",position,velocity,mu,300),Is.True);
            const float dt=.02f;
            for(var i=0;i<6000;i++)
            {
                var desired=orbit.DesiredRelativeVelocity(position,velocity);
                var delta=desired-velocity;if(delta.Length()>96*dt)delta=Vector3.Normalize(delta)*96*dt;
                velocity+=delta;
                velocity-=Vector3.Normalize(position)*(float)(mu/position.LengthSquared())*dt;
                position+=velocity*dt;
                Assert.That(position.Length(),Is.GreaterThan(300));
            }
            Assert.That(orbit.Captured,Is.True);Assert.That(position.Length(),Is.EqualTo(900).Within(8));
            orbit.Cancel();var minimum=position.Length();var maximum=minimum;
            for(var i=0;i<30000;i++)
            {velocity-=Vector3.Normalize(position)*(float)(mu/position.LengthSquared())*dt;position+=velocity*dt;minimum=Math.Min(minimum,position.Length());maximum=Math.Max(maximum,position.Length());}
            Assert.That(maximum-minimum,Is.LessThan(20));
        }
        [Test] public void InvalidAndInteriorCaptureRequestsAreRejected()
        {
            var orbit=new OrbitalAutopilot();
            Assert.That(orbit.Begin((GalaxyEntityId)"p",new Vector3(1,0,0),Vector3.Zero,100,10),Is.False);
            Assert.That(orbit.Begin((GalaxyEntityId)"p",new Vector3(100,0,0),Vector3.Zero,double.NaN,10),Is.False);
            Assert.That(orbit.Begin(default,new Vector3(100,0,0),Vector3.Zero,100,10),Is.False);
        }
        [Test] public void AnchorRelativeGuidanceIsUnaffectedByOriginTranslation()
        {
            var a=new OrbitalAutopilot();var b=new OrbitalAutopilot();var p=new Vector3(600,0,0);var v=new Vector3(0,0,20);
            a.Begin((GalaxyEntityId)"p",p,v,360000,300);b.Begin((GalaxyEntityId)"p",p,v,360000,300);
            Assert.That(a.DesiredRelativeVelocity(p,v),Is.EqualTo(b.DesiredRelativeVelocity((p+new Vector3(10000))-new Vector3(10000),v)));
        }
        [Test] public void GravityOutsideSurfaceFollowsInverseSquare()
        {
            var a=GravityMath.Acceleration(new UnityEngine.Vector3(100,0,0),UnityEngine.Vector3.zero,10000,10,12);
            var b=GravityMath.Acceleration(new UnityEngine.Vector3(200,0,0),UnityEngine.Vector3.zero,10000,10,12);
            Assert.That(a.x,Is.EqualTo(-1).Within(.00001));Assert.That(a.magnitude/b.magnitude,Is.EqualTo(4).Within(.00001));
        }
        [Test] public void ExpandedPlanetOrbitClearsTheSolarSurface()
        {
            var orbit=StableOrbitGenerator.GeneratePlanet(9,3,"subreddit:technology","post:open-source-ai",GalaxyScale.FirstPlanetAxis);
            Assert.That(orbit.SemiMajorAxis*(1-orbit.Eccentricity)-300,Is.GreaterThan(GalaxyTuning.SunRadius));
        }
    }
}
