using System;
using System.Numerics;
using ClustrGalaxy.Core.Domain;
using ClustrGalaxy.Core.Navigation;
using NUnit.Framework;
namespace ClustrGalaxy.Tests
{
    public sealed class SystemCruiseTests
    {
        private sealed class Clear : ICruiseExclusions
        { public bool Sweep(SimulationPosition a, SimulationPosition b, double dt, out CruiseContact contact) { contact = default; return false; } }
        private sealed class Blocked : ICruiseExclusions
        {
            public bool Sweep(SimulationPosition a, SimulationPosition b, double dt, out CruiseContact contact)
            { contact = new CruiseContact(new SimulationPosition(100, 200, 300), new Vector3(2, 3, 4)); return true; }
        }
        [TestCase(10000)] [TestCase(25000)] [TestCase(50000)]
        public void NearbyMovingPlanetJourneyTakesOneToTwoMinutesAndHandsBackSafely(double distance)
        {
            var position = new SimulationPosition(80000000, 0, 0); var velocity = new Vector3(0, 0, 15);
            var target = new CruiseTarget((GalaxyEntityId)"post:planet", position, velocity, 300);
            var drive = new SystemCruise(new SimulationPosition(80000000 + distance, 0, 0), velocity, target);
            var exclusions = new Clear(); double elapsed = GalaxyTravel.ChargeSeconds; CruiseStep result = default;
            for (var i = 0; i < 6000; i++)
            {
                result = drive.Advance(target, .02, true, Vector3.UnitX, 1, false, exclusions); elapsed += .02;
                Assert.That(result.Clearance, Is.GreaterThan(0));
                position = position.Add(velocity * .02f); target = new CruiseTarget(target.Id, position, velocity, 300);
                if (result.Arrived) break;
            }
            Assert.That(result.Arrived, Is.True); Assert.That(elapsed, Is.InRange(60, 120));
            Assert.That(Math.Abs(result.ClosingSpeed), Is.LessThan(20)); Assert.That(result.Clearance, Is.GreaterThan(300));
        }
        [Test] public void SweptExclusionProducesControlledDropAndMatchesBodyVelocity()
        {
            var target = new CruiseTarget((GalaxyEntityId)"post:planet", default, Vector3.Zero, 300);
            var drive = new SystemCruise(new SimulationPosition(10000, 0, 0), new Vector3(-5000, 0, 0), target);
            var step = drive.Advance(target, .02, false, -Vector3.UnitX, 1, false, new Blocked());
            Assert.That(step.Dropped, Is.True); Assert.That(step.Arrived, Is.False);
            Assert.That(step.Position.X, Is.EqualTo(100)); Assert.That(step.Velocity, Is.EqualTo(new Vector3(2, 3, 4)));
        }
        [Test] public void MalformedStepsAndWrongTargetsCannotMoveTheDrive()
        {
            var target = new CruiseTarget((GalaxyEntityId)"post:planet", default, Vector3.Zero, 300);
            var drive = new SystemCruise(new SimulationPosition(10000, 0, 0), Vector3.Zero, target);
            Assert.Throws<ArgumentException>(() => drive.Advance(target, double.NaN, true, Vector3.UnitX, 1, false, new Clear()));
            Assert.Throws<ArgumentException>(() => drive.Advance(new CruiseTarget((GalaxyEntityId)"post:other", default, Vector3.Zero, 300), .02, true, Vector3.UnitX, 1, false, new Clear()));
            Assert.That(drive.Position.X, Is.EqualTo(10000));
        }
    }
}
