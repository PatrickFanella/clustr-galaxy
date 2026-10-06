using System;
using System.IO;
using System.Linq;
using ClustrGalaxy.Core.Activities;
using ClustrGalaxy.Core.Survey;
using NUnit.Framework;
namespace ClustrGalaxy.Tests
{
    public sealed class CampaignTests
    {
        private static readonly ActivityIdentity Identity=new("pilot",new SurveySnapshotId(9,3,"demo"),ActivityWorldMode.Explore);
        private sealed class Store:IActivityRepository
        {
            public bool Fail;private readonly MemoryActivityRepository inner=new();
            public ActivityState LoadOrCreate(ActivityIdentity id)=>inner.LoadOrCreate(id);
            public void Save(ActivityState state){if(Fail)throw new IOException("fixture");inner.Save(state);}
        }
        private static ActivitySession Open(IActivityRepository store,ActivityIdentity? id=null)=>new(store,id??Identity,AuthoredActivityCatalog.Definitions);
        [TestCase(120d)] [TestCase(300d)] [TestCase(2250d)] [TestCase(2750d)]
        public void AuthoredAnomaliesRemainOutsideEnlargedSafetySurfaces(double radius)
        {
            for (var index = 0; index < 6; index++)
            for (var second = 0; second < 100; second++)
            {
                var offset = CampaignPlacement.AnomalyOffset(radius, index, second, false);
                Assert.That(CampaignPlacement.Clearance(offset.Length(), radius), Is.EqualTo(160).Within(.001));
                var next = CampaignPlacement.AnomalyOffset(radius, index, second + .02, false);
                Assert.That(System.Numerics.Vector3.Distance(offset, next) / .02, Is.LessThan(15));
            }
            Assert.That(CampaignPlacement.Clearance(radius - 1, radius), Is.LessThan(0));
            Assert.That(CampaignPlacement.Clearance(double.NaN, radius), Is.NaN);
            Assert.That(CampaignPlacement.AnomalyOffset(radius, 1, 0, true),
                Is.EqualTo(CampaignPlacement.AnomalyOffset(radius, 1, 100, true)));
        }
        [Test] public void EntireCampaignHasUniqueReachableTextAndThreeKeepsakes()
        {
            CampaignContent.Validate();Assert.That(CampaignContent.Observations.Count,Is.EqualTo(28));
            Assert.That(CampaignContent.Observations.Select(x=>x.EventId).Distinct().Count(),Is.EqualTo(28));
            Assert.That(AuthoredActivityCatalog.Definitions.Where(x=>x.Kind==ActivityKind.Trail).Select(x=>CampaignContent.Keepsake(x.Id)).Distinct().Count(),Is.EqualTo(3));
        }
        [Test] public void HiddenDistantAndInterruptedObservationDoesNotCommit()
        {
            var activity=Open(new Store());var discovery=new CampaignDiscovery(activity);
            for(var i=0;i<100;i++){discovery.Advance(0,false,10,0,.1);discovery.Advance(0,true,221,0,.1);discovery.Advance(0,true,100,26,.1);}
            Assert.That(activity.State.Records,Is.Empty);
            for(var i=0;i<20;i++)discovery.Advance(0,true,100,0,.1);
            discovery.Advance(0,false,100,0,.1);
            for(var i=0;i<20;i++)discovery.Advance(0,true,100,0,.1);
            Assert.That(activity.State.Records,Is.Empty);
        }
        [Test] public void FailedDiscoveryRetainsRetryAndDoesNotRevealUntilCommitted()
        {
            var store=new Store{Fail=true};var activity=Open(store);var discovery=new CampaignDiscovery(activity);var before=activity.State;
            for(var i=0;i<31;i++)discovery.Advance(0,true,100,0,.1);
            Assert.That(discovery.Error,Is.Not.Empty);Assert.That(activity.State,Is.SameAs(before));
            store.Fail=false;Assert.That(discovery.Retry().Committed,Is.True);
            Assert.That(activity.State.Diary.Single().Text,Does.Contain("A low note repeats"));
            for(var i=0;i<50;i++)discovery.Advance(0,true,100,0,.1);
            Assert.That(activity.State.Diary.Count,Is.EqualTo(1));
        }
        [Test] public void EntireCampaignRestoresWithoutMutatingOtherWorldMode()
        {
            var store=new Store();var activity=Open(store);var discovery=new CampaignDiscovery(activity);
            for(var index=0;index<28;index++)for(var i=0;i<31;i++)discovery.Advance(index,true,100,0,.1);
            foreach(var definition in AuthoredActivityCatalog.Definitions.Where(x=>x.Kind==ActivityKind.Mystery))
            {
                var layout=EvidenceBoard.Default(definition).Layout;
                activity.Apply(new ActivityIntent("board:"+definition.Id,Identity,ActivityOperation.SaveEvidenceBoard,DateTime.UtcNow,definition.Id,
                    evidenceBoard:new EvidenceBoard(definition.Id,layout.Connect(layout.Nodes[0].Id,layout.Nodes[1].Id))));
                Assert.That(activity.Apply(new ActivityIntent("interpret:"+definition.Id,Identity,ActivityOperation.Interpret,DateTime.UtcNow,definition.Id,definition.Interpretations[2],"My reading of the authored evidence.")).Committed,Is.True);
            }
            Assert.That(Open(store).State.Records.Count(x=>x.Completed),Is.EqualTo(11));
            var survey=new ActivityIdentity("pilot",Identity.Snapshot,ActivityWorldMode.Survey);
            Assert.That(Open(store,survey).State.Records,Is.Empty);
        }
        [Test] public void OrdinaryFlightObservationChecksDoNotAllocate()
        {
            var activity=Open(new Store());var discovery=new CampaignDiscovery(activity);
            for(var i=0;i<100;i++)discovery.Advance(0,true,500,0,.02);
            var before=GC.GetAllocatedBytesForCurrentThread();
            for(var i=0;i<10000;i++)discovery.Advance(0,true,500,0,.02);
            Assert.That(GC.GetAllocatedBytesForCurrentThread()-before,Is.Zero);
        }
        [Test] public void LiveSnapshotNeverDiscoversFiction()
        {
            var activity=Open(new Store(),new ActivityIdentity("pilot",new SurveySnapshotId(9,3,"live"),ActivityWorldMode.Explore));
            var discovery=new CampaignDiscovery(activity);for(var i=0;i<50;i++)discovery.Advance(0,true,100,0,.1);
            Assert.That(activity.State.Records,Is.Empty);
        }
    }
}
