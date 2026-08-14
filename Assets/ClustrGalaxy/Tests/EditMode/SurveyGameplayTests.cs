using System;
using System.IO;
using System.Linq;
using ClustrGalaxy.Data;
using ClustrGalaxy.Generation;
using ClustrGalaxy.Flight;
using ClustrGalaxy.Gameplay;
using ClustrGalaxy.Scanning;
using ClustrGalaxy.UI;
using ClustrGalaxy.World;
using ClustrGalaxy.Constellations;
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
        public void AuthoredConstellation_ReportsOneOfSixAndCompletesExactlyOnce()
        {
            using var fixture = SurveyStoreFixture.Create();
            var store = fixture.Open(SurveySnapshotKey.Demo, SurveyFixture.StartingSystemId);
            var bus = new SurveyEventBus();
            using var tracker = new ConstellationTracker(store, bus, new[] { ConstellationDefinition.Example },
                SurveyFixture.ConstellationMembers);

            var first = tracker.RecordScan("post:open-source-ai");
            Assert.That(first.Message, Is.EqualTo("Example Constellation 1/6"));
            foreach (var id in SurveyFixture.ConstellationMembers) tracker.RecordScan(id);
            Assert.That(tracker.IsComplete("example-constellation"), Is.True);
            Assert.That(tracker.CompletionAwards, Is.EqualTo(1));
            Assert.That(tracker.RecordScan("post:open-source-ai").CompletedNow, Is.False);
            Assert.That(tracker.CompletionAwards, Is.EqualTo(1));
        }

        [Test]
        public void AuthoredConstellation_UnresolvedMemberMakesOverlayUnavailableWithoutChangingDenominator()
        {
            using var fixture = SurveyStoreFixture.Create();
            var store = fixture.Open(SurveySnapshotKey.Demo, SurveyFixture.StartingSystemId);
            using var tracker = new ConstellationTracker(store, new SurveyEventBus(),
                new[] { ConstellationDefinition.Example }, SurveyFixture.ConstellationMembers.Take(5));

            var progress = tracker.ProgressOf("example-constellation");
            Assert.That(progress.Available, Is.False);
            Assert.That(progress.Total, Is.EqualTo(6));
            Assert.That(progress.Message, Is.EqualTo("Example Constellation UNAVAILABLE"));
        }

        [Test]
        public void AuthoredConstellation_OnlySharedSnapshotScanEventsAdvanceProgress()
        {
            using var fixture = SurveyStoreFixture.Create();
            var store = fixture.Open(SurveySnapshotKey.Demo, SurveyFixture.StartingSystemId);
            var bus = new SurveyEventBus();
            using var tracker = new ConstellationTracker(store, bus, new[] { ConstellationDefinition.Example }, SurveyFixture.ConstellationMembers);
            bus.Publish(SurveyEvent.ScanCompleted(SurveySnapshotKey.Pinned(9, 3), "post:open-source-ai", Array.Empty<string>(), Array.Empty<string>(), DateTime.UtcNow, 1));
            Assert.That(tracker.ProgressOf("example-constellation").Found, Is.Zero);
            bus.Publish(SurveyEvent.ScanCompleted(SurveySnapshotKey.Demo, "post:open-source-ai", Array.Empty<string>(), Array.Empty<string>(), DateTime.UtcNow, 1));
            Assert.That(tracker.ProgressOf("example-constellation").Found, Is.EqualTo(1));
        }

        [Test]
        public void PersonalConstellations_SaveReloadAndQueueOnlyAfterExplicitConfirmation()
        {
            using var fixture = SurveyStoreFixture.Create();
            var key = SurveySnapshotKey.Demo;
            var members = SurveyFixture.ConstellationMembers.Take(3).ToArray();
            var personal = PersonalConstellationStore.Open(fixture.Root, "test-pilot", key);
            var saved = personal.Save("Portable systems", "A local reason for preserving this pattern.", members, SurveyFixture.ConstellationMembers,
                "personal:portable-systems");
            Assert.That(File.Exists(personal.StoragePath), Is.True);
            var reloaded = PersonalConstellationStore.Open(fixture.Root, "test-pilot", key);
            Assert.That(reloaded.Get(saved.id).member_stable_ids, Is.EqualTo(members.OrderBy(value => value, StringComparer.Ordinal)));
            var outbox = new ConstellationSubmissionOutbox(reloaded);
            Assert.Throws<InvalidOperationException>(() => outbox.QueueForSubmission(saved.id, confirmed: false));
            var queued = outbox.QueueForSubmission(saved.id, confirmed: true);
            Assert.That(queued.state, Is.EqualTo(ConstellationSubmissionState.AwaitingBackend));
            Assert.That(queued.member_stable_ids, Is.EqualTo(members.OrderBy(value => value, StringComparer.Ordinal)));
            Assert.That(outbox.IsQueued(saved.id), Is.True);
            Assert.That(outbox.QueueForSubmission(saved.id, confirmed: true).submission_id, Is.EqualTo(queued.submission_id));
        }

        [Test]
        public void PersonalConstellations_QueuedStatusUsesTheWholeLocalPayloadIdentity()
        {
            using var fixture = SurveyStoreFixture.Create();
            var members = SurveyFixture.ConstellationMembers.Take(3).ToArray();
            var demo = PersonalConstellationStore.Open(fixture.Root, "test-pilot", SurveySnapshotKey.Demo);
            var queuedPersonal = demo.Save("Same words", "Same local note", members, SurveyFixture.ConstellationMembers,
                "personal:demo-queued");
            var demoOutbox = new ConstellationSubmissionOutbox(demo);
            demoOutbox.QueueForSubmission(queuedPersonal.id, confirmed: true);

            var changedMembers = demo.Save("Same words", "Same local note",
                new[] { SurveyFixture.ConstellationMembers[0], SurveyFixture.ConstellationMembers[1], SurveyFixture.ConstellationMembers[3] },
                SurveyFixture.ConstellationMembers, "personal:demo-other-members");
            Assert.That(demoOutbox.IsQueued(changedMembers.id), Is.False);

            var pinned = PersonalConstellationStore.Open(fixture.Root, "test-pilot", SurveySnapshotKey.Pinned(9, 3));
            var otherNamespace = pinned.Save("Same words", "Same local note", members, SurveyFixture.ConstellationMembers,
                "personal:pinned-same-payload");
            Assert.That(new ConstellationSubmissionOutbox(pinned).IsQueued(otherNamespace.id), Is.False);
        }

        [Test]
        public void PersonalConstellations_RejectInvalidLocalContentAndUnknownSurveyMembers()
        {
            using var fixture = SurveyStoreFixture.Create();
            var personal = PersonalConstellationStore.Open(fixture.Root, "test-pilot", SurveySnapshotKey.Demo);
            Assert.Throws<ArgumentException>(() => personal.Save(" ", "reason", SurveyFixture.ConstellationMembers.Take(3), SurveyFixture.ConstellationMembers));
            Assert.Throws<ArgumentException>(() => personal.Save("Valid", "\n", SurveyFixture.ConstellationMembers.Take(3), SurveyFixture.ConstellationMembers));
            Assert.Throws<ArgumentException>(() => personal.Save("Valid", "reason", new[] { "a", "a", "b" }, new[] { "a", "b", "c" }));
            Assert.Throws<ArgumentException>(() => personal.Save("Valid", "reason", new[] { "a", "b", "unknown" }, new[] { "a", "b", "c" }));
        }

        [Test]
        public void ThreadInspection_BuildsOnlyOrderedDescendantsWithinTheDisplayDepth()
        {
            var rootId = "comment:parent";
            var scene = new SpatialScene
            {
                scope = "thread",
                root_id = rootId,
                nodes = new[]
                {
                    CommentNode(rootId, string.Empty, 50, 4),
                    CommentNode("comment:low", rootId, 3, 1),
                    CommentNode("comment:high-b", rootId, 9, 0),
                    CommentNode("comment:high-a", rootId, 9, 0, "author-a", "A useful excerpt."),
                    CommentNode("comment:grandchild", "comment:low", 7, 1),
                    CommentNode("comment:great-grandchild", "comment:grandchild", 5, 1),
                    CommentNode("comment:too-deep", "comment:great-grandchild", 99, 0),
                    CommentNode("comment:unrelated", "comment:other-parent", 100, 0)
                }
            };

            var model = ThreadInspectionModel.Build(scene, maxDepth: 3, maxNodes: 250);

            Assert.That(model.RootId, Is.EqualTo(rootId));
            Assert.That(model.NodeCount, Is.EqualTo(5));
            Assert.That(model.Entries.Select(entry => entry.StableId), Is.EqualTo(new[]
            {
                "comment:high-a", "comment:high-b", "comment:low"
            }));
            Assert.That(model.Entries[0].Author, Is.EqualTo("author-a"));
            Assert.That(model.Entries[0].BodyExcerpt, Is.EqualTo("A useful excerpt."));
            Assert.That(model.Entries[2].Children[0].Children[0].Depth, Is.EqualTo(3));
            Assert.That(model.Entries[2].Children[0].Children[0].HasMore, Is.True);
            Assert.That(model.Truncated, Is.True);
        }

        [Test]
        public void ThreadInspection_EnforcesTheGlobalNodeCapAndRetainsReplyHints()
        {
            var scene = new SpatialScene
            {
                scope = "thread",
                root_id = "comment:parent",
                nodes = new[]
                {
                    CommentNode("comment:parent", string.Empty, 0, 3),
                    CommentNode("comment:first", "comment:parent", 10, 2),
                    CommentNode("comment:child", "comment:first", 5, 1),
                    CommentNode("comment:second", "comment:parent", 9, 0),
                    CommentNode("comment:third", "comment:parent", 8, 0)
                }
            };

            var model = ThreadInspectionModel.Build(scene, maxDepth: 3, maxNodes: 2);

            Assert.That(model.NodeCount, Is.EqualTo(2));
            Assert.That(model.Truncated, Is.True);
            Assert.That(model.Entries[0].StableId, Is.EqualTo("comment:first"));
            Assert.That(model.Entries[0].HasMore, Is.True);
            Assert.That(model.Entries[0].ReplyCount, Is.EqualTo(2));
        }

        [Test]
        public void ThreadInspection_PresentationFlattensDepthFirstAndCapsHudRowsWithoutDroppingModelData()
        {
            var model = ThreadInspectionModel.Build(new SpatialScene
            {
                scope = "thread",
                root_id = "comment:parent",
                nodes = new[]
                {
                    CommentNode("comment:parent", string.Empty, 0, 3),
                    CommentNode("comment:high", "comment:parent", 9, 1,
                        excerpt: "This is intentionally much longer than the HUD excerpt cap."),
                    CommentNode("comment:high-child", "comment:high", 8, 0),
                    CommentNode("comment:middle", "comment:parent", 5, 0),
                    CommentNode("comment:low", "comment:parent", 1, 0)
                }
            });

            var presentation = model.Presentation(maximumRows: 3, maximumExcerptCharacters: 20);

            Assert.That(model.NodeCount, Is.EqualTo(4));
            Assert.That(presentation.TotalNodeCount, Is.EqualTo(4));
            Assert.That(presentation.Rows.Select(row => row.StableId), Is.EqualTo(new[]
            {
                "comment:high", "comment:high-child", "comment:middle"
            }));
            Assert.That(presentation.IsRenderCapped, Is.True);
            Assert.That(presentation.IsPartial, Is.True);
            Assert.That(presentation.Rows[0].Excerpt.Length, Is.LessThanOrEqualTo(20));
        }

        private static SpatialNode CommentNode(string id, string parentId, long score, long replyCount,
            string author = "pilot", string excerpt = "") => new()
        {
            id = id,
            type = "comment",
            parent_id = parentId,
            author_name = author,
            excerpt = excerpt,
            metrics = new SpatialNodeMetrics
            {
                score = score,
                reply_count = replyCount
            }
        };

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

        [Test]
        public void Scan_RevealsStrongestUnknownRouteBeforeWeakerRoutes()
        {
            var routes = new[]
            {
                new ScanRoute("weak", "known", "far", 2f),
                new ScanRoute("strong", "known", "near", 8f)
            };

            var result = ScanRules.Reveal("known", routes, scannerLevel: 1);

            Assert.That(result.RevealedRouteIds, Is.EqualTo(new[] { "strong" }));
            Assert.That(result.RevealedEntityIds, Is.EqualTo(new[] { "near" }));
        }

        [Test]
        public void Scan_RouteDestinationPersistsAsDiscoveredButUnidentified()
        {
            using var fixture = SurveyStoreFixture.Create();
            var store = fixture.Open(SurveySnapshotKey.Pinned(9, 3), SurveyFixture.StartingSystemId);
            store.DiscoverEntity("subreddit:signal-horizon");
            store.Save();

            var restored = fixture.Open(SurveySnapshotKey.Pinned(9, 3), SurveyFixture.StartingSystemId);

            Assert.That(restored.IsEntityKnown("subreddit:signal-horizon"), Is.True);
            Assert.That(restored.IsEntityIdentified("subreddit:signal-horizon"), Is.False);
            Assert.That(GalaxyDiscoveryVisibility.CanRender(GalaxyWorldMode.Survey,
                restored.IsEntityKnown("subreddit:signal-horizon")), Is.True);
            Assert.That(GalaxyDiscoveryVisibility.CanLabel(GalaxyWorldMode.Survey,
                restored.IsEntityIdentified("subreddit:signal-horizon")), Is.False);
        }

        [Test]
        public void ScanRules_OrdersEqualWeightsByOrdinalRouteIdAndUsesScannerLevels()
        {
            var routes = new[]
            {
                new ScanRoute("zeta", "known", "z", 8f),
                new ScanRoute("alpha", "known", "a", 8f),
                new ScanRoute("beta", "known", "b", 7f),
                new ScanRoute("gamma", "known", "c", 6f)
            };
            var result = ScanRules.Reveal("known", routes, scannerLevel: 2);

            Assert.That(result.RevealedRouteIds, Is.EqualTo(new[] { "alpha", "zeta" }));
            Assert.That(ScanRules.Reveal("known", routes, scannerLevel: 3).RevealedRouteIds.Count, Is.EqualTo(4));
        }

        [Test]
        public void ScanRules_PreservesWeightPriorityWhenLexicalRouteIdsConflict()
        {
            var result = ScanRules.Reveal("known", new[]
            {
                new ScanRoute("a-low", "known", "far", 2f),
                new ScanRoute("z-high", "known", "near", 9f)
            }, scannerLevel: 2);

            Assert.That(result.RevealedRouteIds, Is.EqualTo(new[] { "z-high", "a-low" }));
            Assert.That(result.RevealedEntityIds, Is.EqualTo(new[] { "far", "near" }),
                "entity IDs are deliberately returned in ordinal stable-ID order");
        }

        [Test]
        public void ScanRoutes_BuildPinnedCatalogUsesRendererIdsAndUndirectedReverseTraversal()
        {
            var manifest = new SpatialManifest
            {
                revision_id = 9,
                spatial_catalog_id = 3,
                dimensions = 3,
                scene_contract = SpatialSceneContract.Version,
                bounds = new SpatialBounds { min_x = 0f, max_x = 1f, min_y = 0f, max_y = 1f, min_z = 0f, max_z = 1f }
            };
            var store = new SpatialSceneStore(manifest);
            store.Merge(new SpatialScene
            {
                revision_id = 9,
                spatial_catalog_id = 3,
                nodes = new[]
                {
                    new SpatialNode { id = "subreddit:a" }, new SpatialNode { id = "subreddit:b" },
                    new SpatialNode { id = "subreddit:c" }
                },
                links = new[]
                {
                    new SpatialLink { source = "subreddit:a", target = "subreddit:b", type = "route", directed = true, weight = 9 },
                    new SpatialLink { source = "subreddit:b", target = "subreddit:c", type = "activity", directed = false, weight = 4 }
                }
            });

            var routes = SignalScanner.BuildPinnedRoutes(store);

            Assert.That(routes, Has.Some.Matches<ScanRoute>(route => route.Id == "subreddit:a:subreddit:b:route" &&
                route.SourceId == "subreddit:a" && route.TargetId == "subreddit:b"));
            Assert.That(routes, Has.Some.Matches<ScanRoute>(route => route.Id == "subreddit:b:subreddit:c:activity" &&
                route.SourceId == "subreddit:c" && route.TargetId == "subreddit:b"));
            Assert.That(routes.Count(route => route.Id == "subreddit:b:subreddit:c:activity"), Is.EqualTo(2));
            Assert.That(ScanRules.Reveal("subreddit:a", routes, 1).RevealedRouteIds,
                Is.EqualTo(new[] { "subreddit:a:subreddit:b:route" }));
        }

        [Test]
        public void SignalStrength_ClampsAtTargetAndScanRange()
        {
            Assert.That(SignalScanner.SignalStrengthForDistance(0f, 100f), Is.EqualTo(1f));
            Assert.That(SignalScanner.SignalStrengthForDistance(50f, 100f), Is.EqualTo(0.5f));
            Assert.That(SignalScanner.SignalStrengthForDistance(150f, 100f), Is.Zero);
            Assert.That(SignalScanner.SignalStrengthForDistance(1f, 0f), Is.Zero);
        }

        [Test]
        public void Discovery_SchemaOneProfileFailsClosedAsFreshSchemaTwoState()
        {
            using var fixture = SurveyStoreFixture.Create();
            var key = SurveySnapshotKey.Pinned(9, 3);
            var initial = fixture.Open(key, SurveyFixture.StartingSystemId);
            Directory.CreateDirectory(Path.GetDirectoryName(initial.StoragePath));
            File.WriteAllText(initial.StoragePath,
                "{\"schema_version\":1,\"revision_id\":9,\"spatial_catalog_id\":3,\"snapshot_namespace\":\"pinned\",\"starting_system_id\":\"subreddit:technology\",\"known_entity_ids\":[\"subreddit:legacy\"]}");

            var recovered = fixture.Open(key, SurveyFixture.StartingSystemId);

            Assert.That(recovered.State.schema_version, Is.EqualTo(SurveyDiscoveryState.CurrentSchemaVersion));
            Assert.That(recovered.IsEntityKnown("subreddit:legacy"), Is.False);
            Assert.That(recovered.IsEntityIdentified(SurveyFixture.StartingSystemId), Is.True);
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

            public string Root => root;

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
