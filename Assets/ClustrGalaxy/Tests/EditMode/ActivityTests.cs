using System;
using System.IO;
using System.Linq;
using ClustrGalaxy.Core.Activities;
using ClustrGalaxy.Core.Persistence;
using ClustrGalaxy.Core.Domain;
using ClustrGalaxy.Core.Survey;
using ClustrGalaxy.Persistence;
using NUnit.Framework;

namespace ClustrGalaxy.Tests
{
    public sealed class ActivityTests
    {
        private static readonly ActivityIdentity Identity = new("pilot", new SurveySnapshotId(9, 3, "demo"), ActivityWorldMode.Survey);
        private static ActivityIntent Intent(string id, ActivityOperation operation, string activity = "", string value = "",
            ActivityIdentity? identity = null) => new(id, identity ?? Identity, operation, new DateTime(2026, 9, 6, 0, 0, 0, DateTimeKind.Utc), activity, value);
        private static ActivitySession Open(IActivityRepository repository, ActivityIdentity? identity = null) => new(repository, identity ?? Identity, AuthoredActivityCatalog.Definitions);
        private sealed class FailingRepository : IActivityRepository
        {
            public bool Fail = true;
            private readonly MemoryActivityRepository inner = new();
            public ActivityState LoadOrCreate(ActivityIdentity identity) => inner.LoadOrCreate(identity);
            public void Save(ActivityState state) { if (Fail) throw new IOException("fixture full disk"); inner.Save(state); }
        }
        [Test] public void SaveFailurePreservesStateAndAllowsSameIntentRetry()
        {
            var repository = new FailingRepository(); var session = Open(repository); var before = session.State;
            var intent = Intent("scan-1", ActivityOperation.Observe, "trail-harmonic", "harmonic-1");
            var events = 0; session.Outcome += _ => events++;
            Assert.That(session.Apply(intent).Committed, Is.False);
            Assert.That(session.State, Is.SameAs(before)); Assert.That(events, Is.EqualTo(1));
            repository.Fail = false;
            Assert.That(session.Apply(intent).Committed, Is.True);
            Assert.That(session.State.Diary.Count, Is.EqualTo(1));
            Assert.That(session.Apply(intent).Changed, Is.False);
            Assert.That(session.State.Diary.Count, Is.EqualTo(1)); Assert.That(events, Is.EqualTo(3));
        }
        [Test] public void OutcomeListenersCannotMutateNestedState()
        {
            var session = Open(new MemoryActivityRepository());
            session.Outcome += _ => Assert.Throws<InvalidOperationException>(() => session.Apply(Intent("nested", ActivityOperation.Bookmark, value: "Nested")));
            Assert.That(session.Apply(Intent("outer", ActivityOperation.Bookmark, value: "A quiet moment")).Committed, Is.True);
            Assert.That(session.State.Diary.Count, Is.EqualTo(1));
        }
        [Test] public void RejectWrongProfileSnapshotAndWorldMode()
        {
            var session = Open(new MemoryActivityRepository());
            var wrong = new[] { new ActivityIdentity("other", Identity.Snapshot, Identity.Mode),
                new ActivityIdentity("pilot", new SurveySnapshotId(10,3,"demo"), Identity.Mode),
                new ActivityIdentity("pilot", Identity.Snapshot, ActivityWorldMode.Explore) };
            foreach (var identity in wrong)
                Assert.Throws<InvalidOperationException>(() => session.Apply(Intent("id", ActivityOperation.Bookmark, value: "note", identity: identity)));
            Assert.That(session.State.Diary, Is.Empty);
        }
        [Test] public void TrailsRequireOrderedCluesAndUnpinOnCompletion()
        {
            var session = Open(new MemoryActivityRepository());
            Assert.That(session.Apply(Intent("late", ActivityOperation.Observe, "trail-harmonic", "harmonic-2")).Committed, Is.False);
            Assert.That(session.Apply(Intent("follow-early", ActivityOperation.FollowTrail, "trail-harmonic")).Committed, Is.False);
            session.Apply(Intent("1", ActivityOperation.Observe, "trail-harmonic", "harmonic-1"));
            session.Apply(Intent("follow", ActivityOperation.FollowTrail, "trail-harmonic"));
            Assert.That(session.State.FollowedTrail, Is.EqualTo("trail-harmonic"));
            for (var i = 2; i <= 4; i++) session.Apply(Intent(i.ToString(), ActivityOperation.Observe, "trail-harmonic", "harmonic-" + i));
            Assert.That(session.State.Find("trail-harmonic").Completed, Is.True);
            Assert.That(session.State.FollowedTrail, Is.Empty);
        }
        [TestCase("A shared greeting")]
        [TestCase("A navigation song")]
        [TestCase("A beautiful coincidence")]
        public void MysteryAcceptsEverySupportedInterpretationAfterAllEvidence(string interpretation)
        {
            var session = Open(new MemoryActivityRepository());
            Assert.That(session.Apply(Intent("early", ActivityOperation.Interpret, "mystery-chorus", interpretation)).Committed, Is.False);
            for (var i = 5; i >= 1; i--) session.Apply(Intent(i.ToString(), ActivityOperation.Observe, "mystery-chorus", "chorus-" + i));
            Assert.That(session.State.Find("mystery-chorus").Completed, Is.False);
            var content = AuthoredActivityCatalog.Definitions.Single(x=>x.Id=="mystery-chorus");
            var layout = EvidenceBoard.Default(content).Layout;
            session.Apply(new ActivityIntent("board", session.State.Identity, ActivityOperation.SaveEvidenceBoard, DateTime.UtcNow,
                content.Id, evidenceBoard:new EvidenceBoard(content.Id,layout.Connect(layout.Nodes[0].Id,layout.Nodes[1].Id))));
            Assert.That(session.Apply(Intent("answer", ActivityOperation.Interpret, "mystery-chorus", interpretation)).Committed, Is.True);
            Assert.That(session.State.Find("mystery-chorus").Completed, Is.True);
        }
        [Test] public void LiveActivitiesDoNotAcceptAuthoredStories()
        {
            var identity = new ActivityIdentity("pilot", new SurveySnapshotId(9,3,"pinned"), ActivityWorldMode.Explore);
            var session = Open(new MemoryActivityRepository(), identity);
            Assert.That(session.Apply(Intent("story", ActivityOperation.Observe, "anomaly-echo", "echo", identity)).Committed, Is.False);
            Assert.That(session.Apply(Intent("note", ActivityOperation.Bookmark, value: "My interpretation", identity: identity)).Committed, Is.True);
        }
        [Test] public void DiaryRetainsLatestTwoHundredWithoutLosingIntentDeduplication()
        {
            var session = Open(new MemoryActivityRepository());
            for (var i = 0; i < 205; i++) session.Apply(Intent(i.ToString(), ActivityOperation.Bookmark, value: "Moment " + i));
            Assert.That(session.State.Diary.Count, Is.EqualTo(200));
            Assert.That(session.State.Diary[0].Id, Is.EqualTo("5"));
            Assert.That(session.Apply(Intent("0", ActivityOperation.Bookmark, value: "Moment 0")).Changed, Is.False);
        }
        [Test] public void CompanionRoundTripPreservesExistingSurveyAndV1Sentinels()
        {
            var store = new MemoryDurableDocumentStore();
            var surveyPath = ProfilePaths.Survey("pilot", "demo", 9,3);
            store.Write(surveyPath, "existing-v2-survey", _ => true);
            store.Write("ClustrGalaxy/profiles/v1/sentinel.json", "v1 untouched", _ => true);
            var repository = new ActivityFileRepository(store); var session = Open(repository);
            session.Apply(Intent("observe", ActivityOperation.Observe, "trail-beacon", "beacon-1"));
            session.Apply(Intent("follow", ActivityOperation.FollowTrail, "trail-beacon"));
            var restarted = Open(new ActivityFileRepository(store));
            Assert.That(restarted.State.FollowedTrail, Is.EqualTo("trail-beacon"));
            Assert.That(restarted.State.Diary.Count, Is.EqualTo(1));
            Assert.That(restarted.Apply(Intent("observe", ActivityOperation.Observe, "trail-beacon", "beacon-1")).Changed, Is.False);
            Assert.That(store.Documents[surveyPath], Is.EqualTo("existing-v2-survey"));
            Assert.That(store.Documents["ClustrGalaxy/profiles/v1/sentinel.json"], Is.EqualTo("v1 untouched"));
            var explore = new ActivityIdentity("pilot", Identity.Snapshot, ActivityWorldMode.Explore);
            Assert.That(Open(repository, explore).State.Diary, Is.Empty);
        }
        [Test] public void CorruptCompanionIsPreservedAndDoesNotResetProgress()
        {
            var store = new MemoryDurableDocumentStore(); var path = ActivityFileRepository.PathFor(Identity);
            store.Write(path, "{broken", _ => true);
            Assert.Throws<InvalidDataException>(() => Open(new ActivityFileRepository(store)));
            Assert.That(store.Documents[path], Is.EqualTo("{broken"));
        }
        [Test] public void CelebrationSlotsBoundConcurrencyAndNeverReplayAcceptedEvents()
        {
            var slots = new CelebrationSlots();
            Assert.That(slots.TryStart("a",0), Is.EqualTo(0));
            Assert.That(slots.TryStart("b",0), Is.EqualTo(1));
            Assert.That(slots.TryStart("c",0), Is.EqualTo(2));
            Assert.That(slots.TryStart("d",0), Is.EqualTo(-1));
            Assert.That(slots.TryStart("a",3), Is.EqualTo(-1));
            Assert.That(slots.TryStart("d",3), Is.EqualTo(0));
            Assert.Throws<ArgumentException>(() => slots.TryStart("invalid",double.NaN));
        }
        [Test] public void PooledCelebrationWorkloadDoesNotAllocateWhileStartingEffects()
        {
            var slots = new CelebrationSlots();
            var ids = new string[600]; for (var i=0;i<ids.Length;i++) ids[i]="effect-"+i;
            slots.TryStart("warmup",0); slots.Remaining(0,0);
            var before=GC.GetAllocatedBytesForCurrentThread(); var accepted=0;
            for(var i=0;i<ids.Length;i++) if(slots.TryStart(ids[i],3+(i/3)*3)>=0)accepted++;
            var allocated=GC.GetAllocatedBytesForCurrentThread()-before;
            Assert.That(accepted,Is.EqualTo(600)); Assert.That(allocated,Is.Zero);
            Assert.That(slots.TryStart(ids[0],1000),Is.EqualTo(-1));
        }
        [Test] public void ConstellationDesignValidatesMembershipEdgesAndImmutableEdits()
        {
            var ids = new[] { (GalaxyEntityId)"post:a", (GalaxyEntityId)"post:b", (GalaxyEntityId)"post:c" };
            var original = ConstellationDesign.Default("personal",ids);
            var edited = original.Move(ids[0],.2f,.3f).Connect(ids[0],ids[1]).Appearance(5,2);
            Assert.That(original.Edges, Is.Empty); Assert.That(edited.Edges.Count, Is.EqualTo(1));
            Assert.That(edited.Connect(ids[1],ids[0]).Edges, Is.Empty);
            Assert.Throws<ArgumentException>(() => edited.Move(ids[0],float.NaN,0));
            Assert.Throws<ArgumentException>(() => edited.Connect(ids[0],(GalaxyEntityId)"post:unknown"));
            Assert.Throws<ArgumentException>(() => new ConstellationDesign("personal",edited.Nodes,
                new[] { new DesignEdge(ids[0],ids[1]),new DesignEdge(ids[1],ids[0]) }));
        }
        [Test] public void ConstellationDesignPersistenceRequiresAuthoritativeEligibility()
        {
            var repository = new ActivityFileRepository(new MemoryDurableDocumentStore());
            var design = ConstellationDesign.Default("personal", new[] { (GalaxyEntityId)"post:a", (GalaxyEntityId)"post:b", (GalaxyEntityId)"post:c" });
            var allowed = false;
            var session = new ActivitySession(repository,Identity,AuthoredActivityCatalog.Definitions,_ => allowed);
            var intent = new ActivityIntent("design",Identity,ActivityOperation.SaveDesign,DateTime.UtcNow,design:design);
            Assert.That(session.Apply(intent).Committed, Is.False); allowed = true;
            Assert.That(session.Apply(intent).Committed, Is.True);
            var restored = Open(repository).State.Designs.Single();
            Assert.That(restored.Nodes.Select(n => n.Id), Is.EqualTo(design.Nodes.Select(n => n.Id)));
            Assert.That(restored.PersonalId, Is.EqualTo("personal"));
        }
        [Test] public void PreferencesRoundTripWithoutChangingSurveyOrActivityProgress()
        {
            var store = new MemoryDurableDocumentStore(); var repository = new ActivityFileRepository(store); var session = Open(repository);
            var preferences = new ActivityPreferences(true, true, true, .2f, 120, .1f);
            Assert.That(session.Apply(new ActivityIntent("prefs", Identity, ActivityOperation.SetPreferences,
                DateTime.UtcNow, preferences: preferences)).Committed, Is.True);
            var restored = Open(repository).State;
            Assert.That(restored.Preferences.AutomaticTuning, Is.True);
            Assert.That(restored.Preferences.MusicMuted, Is.True);
            Assert.That(restored.Preferences.ReducedMotion, Is.True);
            Assert.That(restored.Preferences.Tempo, Is.EqualTo(120));
            Assert.That(restored.Records, Is.Empty); Assert.That(restored.Diary, Is.Empty);
            Assert.Throws<ArgumentException>(() => new ActivityPreferences(musicVolume: float.NaN));
        }
        [Test] public void MusicalIdentityUsesOnlyPentatonicIntervalsAndIsStable()
        {
            var allowed = new[] { 0,2,4,7,9,12,14,16,19,21 };
            for (var i = 0; i < 100; i++)
            {
                var id = "post:" + i; var note = DiscoveryMusic.SemitoneFor(id);
                Assert.That(allowed.Contains(note), Is.True);
                Assert.That(DiscoveryMusic.SemitoneFor(new string(id.ToCharArray())), Is.EqualTo(note));
            }
        }
        [Test] public void StateDoesNotExposeCallerOwnedMutableArrays()
        {
            var observations = new[] { "echo" }; var record = new ActivityRecord("anomaly-echo", 1, observations, true);
            observations[0] = "changed";
            Assert.That(record.Observations[0], Is.EqualTo("echo"));
            Assert.Throws<NotSupportedException>(() => ((System.Collections.Generic.IList<string>)record.Observations)[0] = "changed");
        }
        [Test] public void CatalogContainsExactlyAcceptedStoryInventory()
        {
            Assert.That(AuthoredActivityCatalog.Definitions.Count(x => x.Kind == ActivityKind.Trail && x.Observations.Count == 4), Is.EqualTo(3));
            Assert.That(AuthoredActivityCatalog.Definitions.Count(x => x.Kind == ActivityKind.Anomaly), Is.EqualTo(6));
            Assert.That(AuthoredActivityCatalog.Definitions.Count(x => x.Kind == ActivityKind.Mystery && x.Observations.Count == 5 && x.Interpretations.Count == 3), Is.EqualTo(2));
        }
        [Test] public void TuningUsesCumulativeAlignmentWithoutChangingScanDuration()
        {
            var tuning = new SignalTuning(); tuning.Advance(.4f,5,false); tuning.Advance(.5f,6,false);
            Assert.That(tuning.Clean, Is.False); tuning.Advance(.35f,4,false); Assert.That(tuning.Clean, Is.True);
            Assert.That(SignalTuning.RequiredHoldSeconds, Is.EqualTo(1.5f));
            tuning.Reset(); tuning.Advance(float.PositiveInfinity,0,true); Assert.That(tuning.Clean, Is.False);
            tuning.Advance(.75f,90,true); Assert.That(tuning.Clean, Is.True);
        }
    }
}
