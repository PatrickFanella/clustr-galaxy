using System;
using System.IO;
using ClustrGalaxy.Data;
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

        [Test]
        public void Discovery_RoundTripsAProfileWithinItsPinnedSnapshot()
        {
            using var fixture = SurveyStoreFixture.Create();
            var key = SurveySnapshotKey.Pinned(SurveyFixture.RevisionId, SurveyFixture.CatalogId);
            var first = fixture.Open(key, SurveyFixture.StartingSystemId);

            first.DiscoverEntity("post:open-source-ai");
            first.DiscoverRoute("route:technology-open-source-ai");
            first.Save();

            var restored = fixture.Open(key, SurveyFixture.StartingSystemId);
            Assert.That(restored.IsEntityKnown("post:open-source-ai"), Is.True);
            Assert.That(restored.IsRouteKnown("route:technology-open-source-ai"), Is.True);
            Assert.That(restored.StartingSystemId, Is.EqualTo(SurveyFixture.StartingSystemId));
            Assert.That(restored.SnapshotKey, Is.EqualTo(key));
        }

        [Test]
        public void Discovery_DeduplicatesMutationsAndWritesOrdinallySortedJson()
        {
            using var fixture = SurveyStoreFixture.Create();
            var store = fixture.Open(SurveySnapshotKey.Pinned(9, 3), SurveyFixture.StartingSystemId);

            Assert.That(store.DiscoverEntity("post:zeta"), Is.True);
            Assert.That(store.DiscoverEntity("post:alpha"), Is.True);
            Assert.That(store.DiscoverEntity("post:zeta"), Is.False);
            Assert.That(store.CompleteScan("scan:zeta"), Is.True);
            Assert.That(store.CompleteScan("scan:alpha"), Is.True);
            Assert.That(store.CompleteScan("scan:zeta"), Is.False);
            store.Save();

            var json = File.ReadAllText(store.StoragePath);
            var expectedEntities = new[]
            {
                "post:alpha", "post:zeta", SurveyFixture.StartingSystemId
            };
            Array.Sort(expectedEntities, StringComparer.Ordinal);
            Assert.That(store.State.known_entity_ids, Is.EqualTo(expectedEntities));
            Assert.That(store.State.completed_scan_ids, Is.EqualTo(new[] { "scan:alpha", "scan:zeta" }));
            Assert.That(json.IndexOf("post:alpha", StringComparison.Ordinal),
                Is.LessThan(json.IndexOf("post:zeta", StringComparison.Ordinal)));
            Assert.That(json.IndexOf("scan:alpha", StringComparison.Ordinal),
                Is.LessThan(json.IndexOf("scan:zeta", StringComparison.Ordinal)));
        }

        [Test]
        public void Discovery_CorruptFileReturnsFreshStateAndPreservesEvidence()
        {
            using var fixture = SurveyStoreFixture.Create();
            var key = SurveySnapshotKey.Pinned(9, 3);
            var original = fixture.Open(key, SurveyFixture.StartingSystemId);
            Directory.CreateDirectory(Path.GetDirectoryName(original.StoragePath));
            const string corruptJson = "{ truncated";
            File.WriteAllText(original.StoragePath, corruptJson);

            var recovered = fixture.Open(key, SurveyFixture.StartingSystemId);

            Assert.That(recovered.IsEntityKnown(SurveyFixture.StartingSystemId), Is.True);
            Assert.That(recovered.IsEntityKnown("post:open-source-ai"), Is.False);
            Assert.That(File.ReadAllText(original.StoragePath), Is.EqualTo(corruptJson));
        }

        [Test]
        public void Discovery_IsolatedByProfileSnapshotAndExplicitDemoNamespace()
        {
            using var fixture = SurveyStoreFixture.Create();
            var pinned = SurveySnapshotKey.Pinned(9, 3);
            var demo = SurveySnapshotKey.Demo;
            var first = fixture.Open(pinned, SurveyFixture.StartingSystemId);
            first.DiscoverEntity("post:open-source-ai");
            first.Save();

            Assert.That(fixture.Open(SurveySnapshotKey.Pinned(10, 4), SurveyFixture.StartingSystemId)
                .IsEntityKnown("post:open-source-ai"), Is.False);
            Assert.That(fixture.Open(demo, SurveyFixture.StartingSystemId)
                .IsEntityKnown("post:open-source-ai"), Is.False);
            Assert.That(fixture.OpenAs("second-pilot", pinned, SurveyFixture.StartingSystemId)
                .IsEntityKnown("post:open-source-ai"), Is.False);
            Assert.That(first.StoragePath, Does.Not.EqualTo(fixture.Open(demo, SurveyFixture.StartingSystemId).StoragePath));
            Assert.That(SpatialSceneLoader.DemoSurveySnapshotKey, Is.EqualTo(demo));
        }

        [Test]
        public void Discovery_OpenOrExploreDoesNotWriteWithoutAnExplicitSave()
        {
            using var fixture = SurveyStoreFixture.Create();
            var store = fixture.Open(SurveySnapshotKey.Pinned(9, 3), SurveyFixture.StartingSystemId);
            var modes = new GalaxyModeState();

            modes.SetWorldMode(GalaxyWorldMode.Explore);

            Assert.That(File.Exists(store.StoragePath), Is.False);
            Assert.That(store.IsEntityKnown(SurveyFixture.StartingSystemId), Is.True);
        }

        [Test]
        public void Discovery_PersistsEverySurveyProgressCollection()
        {
            using var fixture = SurveyStoreFixture.Create();
            var store = fixture.Open(SurveySnapshotKey.Pinned(9, 3), SurveyFixture.StartingSystemId);
            store.DiscoverRoute("route:technology-open-source-ai");
            store.CompleteScan("subreddit:technology");
            store.RecordConstellationMember("post:open-source-ai");
            store.CompleteContract("contract:first-signal");
            store.UnlockAchievement("achievement:surveyor");
            store.SetScannerLevel(2);
            store.Save();

            var restored = fixture.Open(SurveySnapshotKey.Pinned(9, 3), SurveyFixture.StartingSystemId);
            Assert.That(restored.IsRouteKnown("route:technology-open-source-ai"), Is.True);
            Assert.That(restored.IsScanComplete("subreddit:technology"), Is.True);
            Assert.That(restored.IsConstellationMemberRecorded("post:open-source-ai"), Is.True);
            Assert.That(restored.IsContractComplete("contract:first-signal"), Is.True);
            Assert.That(restored.IsAchievementUnlocked("achievement:surveyor"), Is.True);
            Assert.That(restored.State.scanner_level, Is.EqualTo(2));
            Assert.That(restored.State.last_saved_utc, Is.Not.Empty);
        }

        [TestCase(GalaxyWorldMode.Explore, false, true)]
        [TestCase(GalaxyWorldMode.Survey, false, false)]
        [TestCase(GalaxyWorldMode.Survey, true, true)]
        public void DiscoveryVisibility_UsesWorldModeAndKnownState(
            GalaxyWorldMode mode, bool known, bool expected)
        {
            Assert.That(GalaxyDiscoveryVisibility.CanRender(mode, known), Is.EqualTo(expected));
            Assert.That(GalaxyDiscoveryVisibility.CanTarget(mode, known), Is.EqualTo(expected));
            Assert.That(GalaxyDiscoveryVisibility.CanLabel(mode, known), Is.EqualTo(expected));
            Assert.That(GalaxyDiscoveryVisibility.CanFastTravel(mode, known), Is.EqualTo(expected));
        }

        [Test]
        public void SurveyStart_UsesValidatedPinnedKeyOnlyBeforeCommit()
        {
            var demo = SurveySnapshotKey.Demo;
            var pinned = SurveySnapshotKey.Pinned(18, 4);

            Assert.That(GalaxySurveyVisibilityController.SelectPendingSnapshotKey(demo, false, pinned), Is.EqualTo(pinned));
            Assert.That(GalaxySurveyVisibilityController.SelectPendingSnapshotKey(demo, true, pinned), Is.EqualTo(demo));
            Assert.That(GalaxySurveyVisibilityController.SelectPendingSnapshotKey(demo, false, null), Is.EqualTo(demo));
        }

        [TestCase("subreddit star", false, true)]
        [TestCase("post planet", false, true)]
        [TestCase("user ship", false, false)]
        [TestCase("discussion satellite", false, false)]
        [TestCase("comment ring particle", false, false)]
        [TestCase("reply depth 1", false, false)]
        [TestCase("user ship", true, true)]
        public void SurveyStart_SeedsOnlyExplicitKnowledgeKinds(string entityType, bool anchor, bool expected)
        {
            Assert.That(SurveyStartingKnowledge.CanSeed(entityType, anchor), Is.EqualTo(expected));
        }

        private sealed class SurveyStoreFixture : IDisposable
        {
            private readonly string root;

            private SurveyStoreFixture(string root)
            {
                this.root = root;
            }

            public static SurveyStoreFixture Create() => new SurveyStoreFixture(
                Path.Combine(Path.GetTempPath(), "clustr-survey-test-" + Guid.NewGuid().ToString("N")));

            public SurveyDiscoveryStore Open(SurveySnapshotKey key, string startingSystemId) =>
                SurveyDiscoveryStore.Open(root, "test-pilot", key, startingSystemId);

            public SurveyDiscoveryStore OpenAs(string profileId, SurveySnapshotKey key, string startingSystemId) =>
                SurveyDiscoveryStore.Open(root, profileId, key, startingSystemId);

            public void Dispose()
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, recursive: true);
                }
            }
        }

    }
}
