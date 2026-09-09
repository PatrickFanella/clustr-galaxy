using System;
using System.IO;
using System.Numerics;
using ClustrGalaxy.Core.Activities;
using ClustrGalaxy.Core.Navigation;
using ClustrGalaxy.Core.Persistence;
using ClustrGalaxy.Core.Survey;
using ClustrGalaxy.Persistence;
using NUnit.Framework;
namespace ClustrGalaxy.Tests
{
    public sealed class FlightGhostTests
    {
        private static readonly ActivityIdentity Identity=new("pilot",new SurveySnapshotId(9,3,"demo"),ActivityWorldMode.Survey);
        private static FlightActivityRun Run(ReplayPosition anchor,bool save=true,NavigationFlightMode mode=NavigationFlightMode.PilotAssist)=>
            new(FlightCourse.Playground(FlightActivityKind.RingRun,anchor),mode,false,"tuning",Identity,save);
        private static FlightRecord Complete(FlightActivityRun run)
        {foreach(var point in run.Course.Points)run.Advance(point,.1,run.Mode);return run.Result();}
        [Test] public void SavingGhostRequiresExplicitOptIn()
        {
            Assert.That(Complete(Run(default,false)).Ghost,Is.Null);
            Assert.That(Complete(Run(default)).Ghost,Is.Not.Null);
            Assert.That(new ActivityPreferences().SaveBestGhosts,Is.False);
        }
        [Test] public void RecordingIncludesStartAtMaximumSupportedStep()
        {
            var run=Run(default);
            foreach(var point in run.Course.Points)run.Advance(point,.25,run.Mode);
            var record=run.Result();
            Assert.That(record.Ghost.Replay.Poses[0].Seconds,Is.Zero);
            Assert.That(Math.Abs(record.Ghost.Replay.Duration-record.Seconds),Is.LessThanOrEqualTo(.1));
        }
        [Test] public void GhostFollowsTranslatedCourseAndLargeLogicalCoordinates()
        {
            var oldRun=Run(new ReplayPosition(1e12,200,300));var ghost=Complete(oldRun).Ghost;
            var next=Run(new ReplayPosition(1e12+10000,900,600));next.AttachGhost(ghost);
            Assert.That(next.BestGhost,Is.SameAs(ghost));var before=ghost.Replay.At(.2);var after=ghost.At(.2,next.Course.Points[0]);
            Assert.That(after.Position.X-before.Position.X,Is.EqualTo(10000).Within(.001));
            Assert.That(after.Position.Y-before.Position.Y,Is.EqualTo(700).Within(.001));
            Assert.That(after.Position.Z-before.Position.Z,Is.EqualTo(300).Within(.001));
        }
        [Test] public void DifferentModeSnapshotAndPreviewCannotAttachCompetitiveGhost()
        {
            var ghost=Complete(Run(default)).Ghost;
            var mastery=Run(default,true,NavigationFlightMode.PilotMastery);mastery.AttachGhost(ghost);Assert.That(mastery.BestGhost,Is.Null);
            var other=new ActivityIdentity("pilot",new SurveySnapshotId(10,3,"demo"),ActivityWorldMode.Survey);
            var run=new FlightActivityRun(FlightCourse.Playground(FlightActivityKind.RingRun,default),NavigationFlightMode.PilotAssist,false,"tuning",other);
            run.AttachGhost(ghost);Assert.That(run.BestGhost,Is.Null);
            var preview=new FlightActivityRun(run.Course,run.Mode,true,"tuning",Identity);preview.AttachGhost(ghost);Assert.That(preview.BestGhost,Is.Null);
        }
        [Test] public void MaximumPoseAndAnnotationPayloadRestoresInsideACompanion()
        {
            var folder=Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);
            try
            {
                var poses=new ReplayPose[9000];for(var i=0;i<poses.Length;i++)poses[i]=new ReplayPose(i*.1,default,Quaternion.Identity);
                var events=new ReplayEvent[256];for(var i=0;i<events.Length;i++)events[i]=new ReplayEvent(i,ReplayEventKind.PersonalBookmark,new string('x',64));
                var identity=new ReplayIdentity(Identity.Snapshot,Identity.Mode,1,"tuning",NavigationFlightMode.PilotAssist,"ring-run",1);
                var replay=new JourneyReplay(identity,poses,default(ReplayPosition),events);
                var record=new FlightRecord("ring-run",1,NavigationFlightMode.PilotAssist,899.9,1,"tuning",new FlightGhost(replay,default));
                var repo=new ActivityFileRepository(new FileDurableDocumentStore(folder));repo.Save(new ActivityState(Identity,flights:new[]{record}));
                var loaded=repo.LoadOrCreate(Identity).Flights[0].Ghost.Replay;Assert.That(loaded.Poses.Count,Is.EqualTo(9000));Assert.That(loaded.Events.Count,Is.EqualTo(256));
            }
            finally{Directory.Delete(folder,true);}
        }
        [Test] public void ScoreAndGhostRoundTripInOneCompanionDocument()
        {
            var folder=Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);
            try
            {
                var repo=new ActivityFileRepository(new FileDurableDocumentStore(folder));var record=Complete(Run(default));
                repo.Save(new ActivityState(Identity,flights:new[]{record},preferences:new ActivityPreferences(saveBestGhosts:true)));
                var loaded=repo.LoadOrCreate(Identity);Assert.That(loaded.Preferences.SaveBestGhosts,Is.True);
                Assert.That(loaded.Flights[0].Seconds,Is.EqualTo(record.Seconds));
                Assert.That(loaded.Flights[0].Ghost.Replay.Poses.Count,Is.EqualTo(record.Ghost.Replay.Poses.Count));
                Assert.That(Directory.GetFiles(folder,"*.clustr-replay",SearchOption.AllDirectories),Is.Empty);
            }
            finally{Directory.Delete(folder,true);}
        }
        [Test] public void MismatchedEmbeddedSnapshotIsRejected()
        {
            var record=Complete(Run(default));var other=new ActivityIdentity("pilot",new SurveySnapshotId(10,3,"demo"),ActivityWorldMode.Survey);
            Assert.Throws<ArgumentException>(()=>new ActivityState(other,flights:new[]{record}));
            Assert.Throws<ArgumentException>(()=>new FlightRecord(record.CourseId,1,record.Mode,50,1,"tuning",record.Ghost));
        }
        private sealed class Store:IDurableDocumentStore
        {
            public string Document;public bool Fail;
            public bool TryRead(string path,out string document){document=Document;return document!=null;}
            public void Write(string path,string document,Func<string,bool> validate)
            {if(!validate(document))throw new InvalidDataException();if(Fail)throw new IOException("interrupted replacement");Document=document;}
            public void Quarantine(string path){throw new InvalidOperationException("No quarantine expected");}
        }
        [Test] public void FailedReplacementKeepsOldScoreAndGhostTogetherUntilRetry()
        {
            var store=new Store();var repo=new ActivityFileRepository(store);
            var initialRun=Run(default);initialRun.Advance(new ReplayPosition(1000,0,0),.1,initialRun.Mode);
            var initial=Complete(initialRun);
            var session=new ActivitySession(repo,Identity,AuthoredActivityCatalog.Definitions,flightEligibility:_=>true);
            session.Apply(new ActivityIntent("first",Identity,ActivityOperation.CompleteFlight,DateTime.UtcNow,flight:initial));
            var oldState=session.State;var oldDocument=store.Document;
            var faster=Complete(Run(new ReplayPosition(1000,0,0)));
            var intent=new ActivityIntent("improvement",Identity,ActivityOperation.CompleteFlight,DateTime.UtcNow,flight:faster);
            store.Fail=true;Assert.That(session.Apply(intent).Committed,Is.False);
            Assert.That(session.State,Is.SameAs(oldState));Assert.That(store.Document,Is.EqualTo(oldDocument));
            store.Fail=false;Assert.That(session.Apply(intent).Committed,Is.True);
            var restored=repo.LoadOrCreate(Identity);Assert.That(restored.Flights[0].Seconds,Is.EqualTo(faster.Seconds));
            Assert.That(restored.Flights[0].Ghost.Anchor.X,Is.EqualTo(1000));
            Assert.That(session.Apply(intent).Changed,Is.False);
        }
        [Test] public void LegacyRecordsWithoutGhostFieldsStillLoadWithSavingOff()
        {
            var store=new Store();var repo=new ActivityFileRepository(store);
            repo.Save(new ActivityState(Identity,flights:new[]{Complete(Run(default,false))}));
            store.Document=System.Text.RegularExpressions.Regex.Replace(store.Document,
                @"\s*""ghost""\s*:\s*null\s*,?", "");
            store.Document=System.Text.RegularExpressions.Regex.Replace(store.Document,
                @"\s*""save_best_ghosts""\s*:\s*false\s*,?", "");
            // These optional fields are last in their objects; remove the preceding trailing comma.
            store.Document=System.Text.RegularExpressions.Regex.Replace(store.Document,@",\s*}","}");
            var loaded=repo.LoadOrCreate(Identity);Assert.That(loaded.Flights[0].Ghost,Is.Null);Assert.That(loaded.Preferences.SaveBestGhosts,Is.False);
        }
        [Test] public void PlaybackDoesNotAllocateDuringSteadyPractice()
        {
            var run=Run(default);var ghost=Complete(run).Ghost;
            for(var i=0;i<100;i++)ghost.At(.2,run.Course.Points[0]);
            var before=GC.GetAllocatedBytesForCurrentThread();
            for(var i=0;i<10000;i++)ghost.At(.2,run.Course.Points[0]);
            Assert.That(GC.GetAllocatedBytesForCurrentThread()-before,Is.Zero);
        }
    }
}
