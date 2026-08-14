using ClustrGalaxy.Generation;
using ClustrGalaxy.Flight;
using ClustrGalaxy.Gameplay;
using ClustrGalaxy.World;
using NUnit.Framework;

namespace ClustrGalaxy.Tests
{
    /// <summary>
    /// Stable, deterministic content shared by the Survey gameplay test plan.
    /// It deliberately mirrors IDs emitted by <c>GalaxyPrototypeBootstrap</c>, so
    /// future Survey tests never depend on live graph data.
    /// </summary>
    internal static class SurveyFixture
    {
        public const long RevisionId = 9;
        public const long CatalogId = 3;
        public const string StartingSystemId = "subreddit:technology";

        public static readonly string[] ConstellationMembers =
        {
            "subreddit:technology",
            "post:open-source-ai",
            "post:linux-handhelds",
            "post:right-to-repair",
            "post:privacy-tools",
            "post:retro-computing"
        };

        public static readonly SurveyRoute[] Routes =
        {
            new SurveyRoute("subreddit:technology", "post:open-source-ai"),
            new SurveyRoute("subreddit:technology", "post:linux-handhelds")
        };

        public static readonly SurveyThreadNode CommentThread = new SurveyThreadNode(
            "comment:open-source-ai-1",
            new SurveyThreadNode(
                "comment:open-source-ai-1-reply-1",
                new SurveyThreadNode("comment:open-source-ai-1-reply-3")));

        internal readonly struct SurveyRoute
        {
            public SurveyRoute(string sourceId, string destinationId)
            {
                SourceId = sourceId;
                DestinationId = destinationId;
            }

            public string SourceId { get; }
            public string DestinationId { get; }
        }

        internal sealed class SurveyThreadNode
        {
            public SurveyThreadNode(string stableId, SurveyThreadNode child = null)
            {
                StableId = stableId;
                Child = child;
            }

            public string StableId { get; }
            public SurveyThreadNode Child { get; }
        }
    }

    public sealed class SurveyGameplayTests
    {
        [Test]
        public void Modes_AreOrthogonalAndFreelyChangeable()
        {
            var modes = new GalaxyModeState();

            modes.SetWorldMode(GalaxyWorldMode.Survey);
            modes.SetFlightMode(FlightControlMode.PilotMastery);

            Assert.That(modes.WorldMode, Is.EqualTo(GalaxyWorldMode.Survey));
            Assert.That(modes.FlightMode, Is.EqualTo(FlightControlMode.PilotMastery));

            modes.SetFlightMode(FlightControlMode.Autopilot);

            Assert.That(modes.WorldMode, Is.EqualTo(GalaxyWorldMode.Survey));
        }

        [Test]
        public void Modes_DuplicateAssignmentsDoNotRaiseEvents()
        {
            var modes = new GalaxyModeState();
            var changed = 0;
            modes.Changed += () => changed++;

            modes.SetWorldMode(GalaxyWorldMode.Explore);
            modes.SetFlightMode(FlightControlMode.PilotAssist);
            Assert.That(changed, Is.Zero);

            modes.SetWorldMode(GalaxyWorldMode.Survey);
            modes.SetFlightMode(FlightControlMode.PilotMastery);
            Assert.That(changed, Is.EqualTo(2));
        }

        [Test]
        public void Modes_CyclingFlightDoesNotChangeWorldMode()
        {
            var modes = new GalaxyModeState();
            modes.SetWorldMode(GalaxyWorldMode.Survey);

            modes.CycleFlightMode(1);
            Assert.That(modes.FlightMode, Is.EqualTo(FlightControlMode.PilotMastery));
            Assert.That(modes.WorldMode, Is.EqualTo(GalaxyWorldMode.Survey));

            modes.CycleFlightMode(1);
            Assert.That(modes.FlightMode, Is.EqualTo(FlightControlMode.Autopilot));
            Assert.That(modes.WorldMode, Is.EqualTo(GalaxyWorldMode.Survey));
        }

        [Test]
        public void FlightModes_KeepTheirDistinctVelocitySemantics()
        {
            var current = new UnityEngine.Vector3(5f, 0f, 0f);
            var autopilotHold = ProbeFlightModel.DesiredVelocity(
                FlightControlMode.Autopilot, current, UnityEngine.Vector3.zero, 100f, 10f, 1f);
            var assist = ProbeFlightModel.DesiredVelocity(
                FlightControlMode.PilotAssist, current, UnityEngine.Vector3.forward, 100f, 10f, 1f);
            var mastery = ProbeFlightModel.DesiredVelocity(
                FlightControlMode.PilotMastery, current, UnityEngine.Vector3.forward, 100f, 10f, 1f);
            var autopilotTarget = ProbeFlightModel.DesiredVelocity(
                FlightControlMode.Autopilot, current, UnityEngine.Vector3.forward * 80f, 100f, 10f, 1f);

            Assert.That(autopilotHold.x, Is.LessThan(current.x));
            Assert.That(autopilotTarget.z, Is.GreaterThan(0f));
            Assert.That(assist.x, Is.LessThan(current.x));
            Assert.That(assist.z, Is.GreaterThan(0f));
            Assert.That(mastery.x, Is.EqualTo(current.x));
            Assert.That(mastery.z, Is.EqualTo(10f));
        }

        [Test]
        public void GravityFlight_AssistAndMasteryPreserveLateralMomentumWhileApplyingThrust()
        {
            var current = new UnityEngine.Vector3(8f, 0f, 0f);
            var intent = UnityEngine.Vector3.forward;

            var assist = ProbeFlightModel.VelocityWithMomentumPreserved(
                FlightControlMode.PilotAssist, current, intent, 100f, 10f, 1f);
            var mastery = ProbeFlightModel.VelocityWithMomentumPreserved(
                FlightControlMode.PilotMastery, current, intent, 100f, 10f, 1f);

            Assert.That(assist.x, Is.EqualTo(current.x));
            Assert.That(assist.z, Is.EqualTo(10f));
            Assert.That(mastery.x, Is.EqualTo(current.x));
            Assert.That(mastery.z, Is.EqualTo(10f));
        }

        [Test]
        public void GravityFlight_NoInputDoesNotDampExistingVelocityBeforeGravityIsApplied()
        {
            var current = new UnityEngine.Vector3(8f, 3f, -4f);

            var assist = ProbeFlightModel.VelocityWithMomentumPreserved(
                FlightControlMode.PilotAssist, current, UnityEngine.Vector3.zero, 100f, 10f, 1f);
            var mastery = ProbeFlightModel.VelocityWithMomentumPreserved(
                FlightControlMode.PilotMastery, current, UnityEngine.Vector3.zero, 100f, 10f, 1f);

            Assert.That(assist, Is.EqualTo(current));
            Assert.That(mastery, Is.EqualTo(current));
        }

        [Test]
        public void BetaTuning_ExpandsSpaceAndSlowsTheProbe()
        {
            Assert.That(GalaxyTuning.RegionDistanceScale, Is.EqualTo(5f));
            Assert.That(GalaxyTuning.SystemDistanceScale, Is.EqualTo(4f));
            Assert.That(GalaxyTuning.PlanetOrbitScale, Is.EqualTo(3f));
            Assert.That(GalaxyTuning.MoonOrbitScale, Is.InRange(2f, 2.5f));
            Assert.That(GalaxyTuning.OrbitSimulationTimeScale, Is.EqualTo(0.1f));
            Assert.That(GalaxyTuning.OrbitPeriodScale * GalaxyTuning.OrbitSimulationTimeScale,
                Is.EqualTo(1f));
            Assert.That(GalaxyScale.PlanetOrbitTimeScale, Is.EqualTo(0.1f));
            Assert.That(GalaxyScale.FirstPlanetAxis, Is.EqualTo(1500d));
            Assert.That(GalaxyScale.PlanetLaneGap, Is.EqualTo(360d));
            Assert.That(GalaxyTuning.CruiseSpeed, Is.InRange(78f, 104f));
            Assert.That(GalaxyTuning.Acceleration, Is.InRange(38f, 57f));
            Assert.That(GalaxyTuning.BrakeAcceleration, Is.EqualTo(120f));
            Assert.That(GalaxyTuning.BoostMultiplier, Is.EqualTo(1.75f));
        }

        [Test]
        public void BetaTuning_KeepsNeighborPlanetTravelReadable()
        {
            var seconds = GalaxyTuning.EstimatedTravelSeconds(3000f);

            Assert.That(seconds, Is.InRange(20f, 45f));
        }

        [Test]
        public void BetaTuning_UsesTheSameCruiseAndBoostForFlight()
        {
            Assert.That(ProbeFlightModel.DesiredSpeed(false), Is.EqualTo(GalaxyTuning.CruiseSpeed));
            Assert.That(ProbeFlightModel.DesiredSpeed(true),
                Is.EqualTo(GalaxyTuning.CruiseSpeed * GalaxyTuning.BoostMultiplier));
        }

        [Test]
        public void BetaTuning_ScalesTheCompleteRegionSystemPosition()
        {
            var stableId = "subreddit:technology";
            var basePosition = LiveGalaxyMaterializer.ProjectRegionPosition(stableId, 1f);
            var scaledPosition = LiveGalaxyMaterializer.ProjectRegionPosition(
                stableId, GalaxyTuning.SystemDistanceScale);

            Assert.That(scaledPosition, Is.EqualTo(basePosition * GalaxyTuning.SystemDistanceScale));
        }
    }
}
