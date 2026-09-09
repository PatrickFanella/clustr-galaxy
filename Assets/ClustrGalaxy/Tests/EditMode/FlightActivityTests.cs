using System;
using System.IO;
using System.Linq;
using ClustrGalaxy.Core.Activities;
using ClustrGalaxy.Core.Navigation;
using ClustrGalaxy.Core.Persistence;
using ClustrGalaxy.Core.Survey;
using ClustrGalaxy.Persistence;
using NUnit.Framework;

namespace ClustrGalaxy.Tests
{
    public sealed class FlightActivityTests
    {
        private static readonly ActivityIdentity Identity=new("pilot",new SurveySnapshotId(9,3,"demo"),ActivityWorldMode.Explore);
        [Test] public void ThreeScenicProfilesKeepEndpointsAndLogicalPrecision()
        {
            var a=new ReplayPosition(1e12,200,300);var b=new ReplayPosition(1e12+2000,600,1400);
            var courses=Enumerable.Range(0,3).Select(i=>FlightCourse.Scenic(i,a,b)).ToArray();
            foreach(var course in courses)
            {Assert.That(FlightCourse.Distance(course.Points[0],a),Is.LessThan(.001));Assert.That(FlightCourse.Distance(course.Points[32],b),Is.LessThan(.001));Assert.That(course.Points.Count,Is.EqualTo(33));}
            Assert.That(FlightCourse.Distance(courses[0].Points[8],courses[1].Points[8]),Is.GreaterThan(10));
            Assert.Throws<ArgumentException>(()=>FlightCourse.Scenic(3,a,b));
        }
        [Test] public void LongScenicSegmentsRemainActiveInsideCorridorButLeavingEndsGuidance()
        {
            var course=FlightCourse.Scenic(0,new ReplayPosition(0,0,0),new ReplayPosition(160000,0,0));
            var run=new FlightActivityRun(course,NavigationFlightMode.PilotAssist,false,"tuning");
            run.Advance(course.Points[0],.02,run.Mode);
            run.Advance(course.Points[0],.02,run.Mode);
            Assert.That(run.Active,Is.True,"The next checkpoint is far away, but the ship is on the path.");
            var a=course.Points[0];var b=course.Points[1];
            run.Advance(new ReplayPosition((a.X+b.X)/2,(a.Y+b.Y)/2,(a.Z+b.Z)/2),.02,run.Mode);
            Assert.That(run.Active,Is.True);
            run.Advance(new ReplayPosition(a.X,a.Y+10000,a.Z),.02,run.Mode);
            Assert.That(run.Cancelled,Is.True);
        }
        [Test] public void RingHasEightGatesAndMissedGateDoesNotForceRestart()
        {
            var course=FlightCourse.Playground(FlightActivityKind.RingRun,default);
            var run=new FlightActivityRun(course,NavigationFlightMode.PilotAssist,false,"tuning");
            run.Advance(new ReplayPosition(1000,1000,1000),.02,run.Mode);
            Assert.That(run.Active,Is.True);Assert.That(run.Checkpoint,Is.Zero);
            foreach(var p in course.Points)run.Advance(p,.02,run.Mode);
            Assert.That(run.Completed,Is.True);Assert.That(run.Result().Mode,Is.EqualTo(NavigationFlightMode.PilotAssist));
        }
        [Test] public void PreviewNeverProducesARecordAndModeChangesCancel()
        {
            var course=FlightCourse.Playground(FlightActivityKind.FigureEight,default);
            var preview=new FlightActivityRun(course,NavigationFlightMode.Autopilot,true,"tuning");
            foreach(var p in course.Points)preview.Advance(p,.02,preview.Mode);
            Assert.That(preview.Completed,Is.True);Assert.Throws<InvalidOperationException>(()=>preview.Result());
            var run=new FlightActivityRun(course,NavigationFlightMode.PilotAssist,false,"tuning");
            run.Advance(course.Points[0],.02,NavigationFlightMode.PilotMastery);Assert.That(run.Cancelled,Is.True);
        }
        [Test] public void StableOrbitRequiresTwentyContinuousMovingSeconds()
        {
            var run=new FlightActivityRun(FlightCourse.Playground(FlightActivityKind.StableOrbit,default),NavigationFlightMode.PilotMastery,false,"tuning");
            for(var i=0;i<1100;i++)
            {var a=i*.02*.2;run.Advance(new ReplayPosition(Math.Sin(a)*240,0,Math.Cos(a)*240),.02,run.Mode);if(i==900)Assert.That(run.Completed,Is.False);}
            Assert.That(run.Completed,Is.True);Assert.That(run.Result().Seconds,Is.GreaterThanOrEqualTo(20));
            var stationary=new FlightActivityRun(run.Course,run.Mode,false,"tuning");
            for(var i=0;i<1100;i++)stationary.Advance(run.Course.Points[0],.02,run.Mode);
            Assert.That(stationary.Completed,Is.False);
        }
        [Test] public void FlightRecordsAreDurableSeparateByModeAndDoNotChangeSurveyFile()
        {
            var directory=Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);
            try
            {
                var store=new FileDurableDocumentStore(directory);var repo=new ActivityFileRepository(store);
                var session=new ActivitySession(repo,Identity,AuthoredActivityCatalog.Definitions,flightEligibility:_=>true);
                foreach(var mode in new[]{NavigationFlightMode.PilotAssist,NavigationFlightMode.PilotMastery})
                {
                    var record=new FlightRecord("ring-run",1,mode,50,.9,"tuning");
                    var intent=new ActivityIntent(mode.ToString(),Identity,ActivityOperation.CompleteFlight,DateTime.UtcNow,flight:record);
                    Assert.That(session.Apply(intent).Committed,Is.True);Assert.That(session.Apply(intent).Changed,Is.False);
                }
                var loaded=repo.LoadOrCreate(Identity);Assert.That(loaded.Flights.Count,Is.EqualTo(2));Assert.That(loaded.Diary.Count,Is.EqualTo(2));
                Assert.That(Directory.GetFiles(directory,"*",SearchOption.AllDirectories).Count(x=>x.EndsWith(".json")),Is.EqualTo(1));
                Assert.That(loaded.Flights.All(x=>Math.Abs(x.Accuracy-.9)<1e-9),Is.True);
            }
            finally{Directory.Delete(directory,true);}
        }
        private sealed class FailingRepository:IActivityRepository
        {
            public bool Fail=true;private readonly MemoryActivityRepository inner=new();
            public ActivityState LoadOrCreate(ActivityIdentity identity)=>inner.LoadOrCreate(identity);
            public void Save(ActivityState state){if(Fail)throw new IOException("full disk");inner.Save(state);}
        }
        [Test] public void FailedFlightSaveCanRetryWithoutConsumingIntentOrPersonalBest()
        {
            var repo=new FailingRepository();var session=new ActivitySession(repo,Identity,AuthoredActivityCatalog.Definitions,flightEligibility:_=>true);
            var intent=new ActivityIntent("run",Identity,ActivityOperation.CompleteFlight,DateTime.UtcNow,
                flight:new FlightRecord("ring-run",1,NavigationFlightMode.PilotAssist,50,.9,"tuning"));
            var before=session.State;Assert.That(session.Apply(intent).Committed,Is.False);Assert.That(session.State,Is.SameAs(before));
            repo.Fail=false;Assert.That(session.Apply(intent).Committed,Is.True);Assert.That(session.Apply(intent).Changed,Is.False);
            Assert.That(session.State.Flights.Count,Is.EqualTo(1));Assert.That(session.State.Diary.Count,Is.EqualTo(1));
        }
        [Test] public void StartingCourseCanClearPendingNavigationWithoutTeleportingLater()
        {
            var nav=new GalaxyNavigation();nav.Apply(new PlotApproachIntent(new NavigationTarget(
                (ClustrGalaxy.Core.Domain.GalaxyEntityId)"subreddit:test",ClustrGalaxy.Core.Domain.GalaxyEntityKind.Subreddit,"test",default,true,true),false));
            nav.Apply(new ClearFlightGuidanceIntent());Assert.That(nav.State.PlottedDestinationId.IsEmpty,Is.True);
            Assert.That(nav.State.PendingTravelId.IsEmpty,Is.True);
        }
        [Test] public void UnauthorisedResultsAndNonFiniteRecordsAreRejected()
        {
            var session=new ActivitySession(new MemoryActivityRepository(),Identity,AuthoredActivityCatalog.Definitions);
            var intent=new ActivityIntent("forged",Identity,ActivityOperation.CompleteFlight,DateTime.UtcNow,
                flight:new FlightRecord("ring-run",1,NavigationFlightMode.PilotAssist,20,1,"tuning"));
            Assert.That(session.Apply(intent).Committed,Is.False);Assert.That(session.State.Flights,Is.Empty);
            Assert.Throws<ArgumentException>(()=>new FlightRecord("ring-run",1,NavigationFlightMode.PilotAssist,double.NaN,1,"tuning"));
        }
    }
}
