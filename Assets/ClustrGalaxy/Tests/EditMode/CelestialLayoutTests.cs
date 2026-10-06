using System;
using ClustrGalaxy.Generation;
using NUnit.Framework;

namespace ClustrGalaxy.Tests.EditMode
{
    public sealed class CelestialLayoutTests
    {
        [TestCase(300d, 0d)]
        [TestCase(300d, .95d)]
        [TestCase(80000000d, .4d)]
        [TestCase(80000000d, 1.2d)]
        [TestCase(300d, -.2d)]
        public void PreparedEphemerisMatchesReferenceEquations(double axis, double eccentricity)
        {
            var elements = new OrbitalElements(axis, eccentricity, .7, -2.1, 1.3, -.5);
            var prepared = new ClustrGalaxy.Simulation.KeplerOrbitMath.PreparedOrbit(elements, 24000000d);
            var times = new[] { -1e9, -10d, 0d, .02d, 1d, 1e4, 1e9 };
            foreach (var time in times)
            {
                var expected = ClustrGalaxy.Simulation.KeplerOrbitMath.PrecisePositionAtTime(elements, 24000000d, time);
                var actual = prepared.PositionAtTime(time);
                Assert.That(actual.X, Is.EqualTo(expected.X));
                Assert.That(actual.Y, Is.EqualTo(expected.Y));
                Assert.That(actual.Z, Is.EqualTo(expected.Z));
            }
        }
        [TestCase(0d, 1d)]
        [TestCase(-1d, 1d)]
        [TestCase(1d, 0d)]
        [TestCase(1d, -1d)]
        public void PreparedInactiveOrbitsPreserveZeroFallback(double axis, double mu)
        {
            var elements = new OrbitalElements(axis, .2, 1, 2, 3, 4);
            Assert.That(new ClustrGalaxy.Simulation.KeplerOrbitMath.PreparedOrbit(elements, mu).PositionAtTime(10),
                Is.EqualTo(ClustrGalaxy.World.DoubleVector3.Zero));
            Assert.That(default(ClustrGalaxy.Simulation.KeplerOrbitMath.PreparedOrbit).PositionAtTime(double.NaN),
                Is.EqualTo(ClustrGalaxy.World.DoubleVector3.Zero));
        }

        [Test]
        public void DistantOrbitRetainsSubmeterMotionBeforeLocalConversion()
        {
            var orbit = new OrbitalElements(80000000, 0, 0, 0, 0, .5);
            var before = ClustrGalaxy.Simulation.KeplerOrbitMath.PrecisePositionAtTime(orbit, 24000000, 1);
            var after = ClustrGalaxy.Simulation.KeplerOrbitMath.PrecisePositionAtTime(orbit, 24000000, 1.02);
            Assert.That(after.RelativeTo(before).magnitude, Is.EqualTo(Math.Sqrt(24000000d / 80000000d) * .02).Within(.000001));
        }

        [Test]
        public void PlanetApproachDoesNotEnableSystemFastTravelOrUnknownSurveyTargets()
        {
            var navigation = new ClustrGalaxy.Core.Navigation.GalaxyNavigation();
            var target = new ClustrGalaxy.Core.Navigation.NavigationTarget((ClustrGalaxy.Core.Domain.GalaxyEntityId)"post:p",
                ClustrGalaxy.Core.Domain.GalaxyEntityKind.Post, "Planet", new System.Numerics.Vector3(1000, 0, 0), true, false, 120);
            Assert.That(navigation.Apply(new ClustrGalaxy.Core.Navigation.PlotApproachIntent(target, false)).Succeeded, Is.True);
            Assert.That(navigation.Apply(new ClustrGalaxy.Core.Navigation.FastTravelIntent(target)).Succeeded, Is.False);
            navigation.Apply(new ClustrGalaxy.Core.Navigation.SetSurveyModeIntent(true, false));
            Assert.That(navigation.Apply(new ClustrGalaxy.Core.Navigation.PlotApproachIntent(target, false)).Succeeded, Is.False);
        }

        [Test]
        public void MaximumSystemHasDisjointEnvelopesAndBoundedExtent()
        {
            var previous = default(OrbitalElements); var envelope = 0d;
            const double starRadius = 2500, starMu = 6 * starRadius * starRadius;
            for (var i = 0; i < 5000; i++)
            {
                var radius = 120 + i % 181;
                var seed = StableOrbitGenerator.GeneratePlanet(1, 2, "system", "planet:" + i, 1);
                var orbit = CelestialLayout.Planet(seed, radius, 64, starRadius, starMu, previous, envelope);
                var nextEnvelope = CelestialLayout.SatelliteEnvelope(radius, 64);
                var periapsis = orbit.SemiMajorAxis * (1 - orbit.Eccentricity);
                Assert.That(periapsis - nextEnvelope, Is.GreaterThan(starRadius + CelestialLayout.StellarClearance - .001));
                if (i > 0) Assert.That(periapsis - nextEnvelope,
                    Is.GreaterThanOrEqualTo(previous.SemiMajorAxis * (1 + previous.Eccentricity) + envelope + GalaxyScale.PlanetLaneGap - .001));
                Assert.That(OrbitStability.MaximumProgradeMoonAxis(periapsis, 4 * radius * radius, starMu), Is.GreaterThanOrEqualTo(nextEnvelope - .001));
                previous = orbit; envelope = nextEnvelope;
            }
            Assert.That(previous.SemiMajorAxis, Is.LessThan(100000000d));
        }

        [Test]
        public void AllReservedMoonsFitWithoutCrossing()
        {
            var previous = default(OrbitalElements); var previousRadius = 0d;
            for (var i = 0; i < 64; i++)
            {
                var seed = StableOrbitGenerator.GenerateMoon(1, 2, "planet", "moon:" + i, 1);
                var orbit = CelestialLayout.Moon(seed, 300, 15, previous, previousRadius);
                var inner = orbit.SemiMajorAxis * (1 - orbit.Eccentricity) - 15;
                Assert.That(inner, Is.GreaterThan(300));
                if (i > 0) Assert.That(inner, Is.GreaterThan(previous.SemiMajorAxis * (1 + previous.Eccentricity) + previousRadius));
                previous = orbit; previousRadius = 15;
            }
            Assert.That(previous.SemiMajorAxis * (1 + previous.Eccentricity) + previousRadius,
                Is.LessThan(CelestialLayout.SatelliteEnvelope(300, 64)));
        }
    }
}
