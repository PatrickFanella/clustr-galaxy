using System;
using System.Linq;
using ClustrGalaxy.Core.Activities;
using ClustrGalaxy.Core.Domain;
using ClustrGalaxy.Core.World;
using ClustrGalaxy.Core.Survey;
using NUnit.Framework;
namespace ClustrGalaxy.Tests
{
    public sealed class ExpeditionTests
    {
        private static readonly ActivityIdentity Id=new("pilot",new SurveySnapshotId(9,3,"demo"),ActivityWorldMode.Explore);
        private static ActivityIntent Command(string id,ActivityOperation operation,ExpeditionPlan plan=null,string target="")=>new(id,Id,operation,DateTime.UtcNow,value:target,expedition:plan);
        [Test] public void QuietSelectionIsDeterministicBoundedAndHonestAboutShorterData()
        {
            var ids=new[]{(GalaxyEntityId)"b",(GalaxyEntityId)"a"};
            var plan=ExpeditionPlan.Select(ExpeditionMood.QuietDrift,ids,Array.Empty<ResidentRoute>());
            Assert.That(plan.Stops.Select(x=>x.Value),Is.EqualTo(new[]{"a","b"}));Assert.That(plan.Shorter,Is.True);
            Assert.Throws<InvalidOperationException>(()=>ExpeditionPlan.Select(ExpeditionMood.QuietDrift,Array.Empty<GalaxyEntityId>(),Array.Empty<ResidentRoute>()));
        }
        [Test] public void ConnectionsNeverInventEdgesOrVisitIneligibleNodes()
        {
            var ids=new[]{(GalaxyEntityId)"a",(GalaxyEntityId)"b",(GalaxyEntityId)"c"};
            var routes=new[]{new ResidentRoute((GalaxyRouteId)"ab",ids[0],ids[1],default),new ResidentRoute((GalaxyRouteId)"hidden",ids[1],(GalaxyEntityId)"hidden",default)};
            var plan=ExpeditionPlan.Select(ExpeditionMood.UnexpectedConnection,ids,routes);
            Assert.That(plan.Stops.Select(x=>x.Value),Is.EqualTo(new[]{"a","b"}));Assert.That(plan.Shorter,Is.True);
            Assert.Throws<InvalidOperationException>(()=>ExpeditionPlan.Select(ExpeditionMood.UnexpectedConnection,ids,Array.Empty<ResidentRoute>()));
        }
        [Test] public void PausedJourneyRestoresAndRequiresExplicitClosingCard()
        {
            var repo=new MemoryActivityRepository();var plan=ExpeditionPlan.Select(ExpeditionMood.QuietDrift,new[]{(GalaxyEntityId)"a"},Array.Empty<ResidentRoute>());
            ActivitySession Open()=>new(repo,Id,AuthoredActivityCatalog.Definitions,expeditionEligibility:x=>ReferenceEquals(x,plan),visitEligibility:_=>true);
            var session=Open();Assert.That(session.Apply(Command("start",ActivityOperation.StartExpedition,plan)).Committed,Is.True);
            session.Apply(Command("pause",ActivityOperation.PauseExpedition));session=Open();Assert.That(session.State.Expedition.Status,Is.EqualTo(ExpeditionStatus.Paused));
            Assert.That(session.Apply(Command("premature",ActivityOperation.VisitExpeditionStop,target:"a")).Committed,Is.False);
            session.Apply(Command("resume",ActivityOperation.ResumeExpedition));session.Apply(Command("visit",ActivityOperation.VisitExpeditionStop,target:"a"));
            Assert.That(session.State.Expedition.Status,Is.EqualTo(ExpeditionStatus.ReadyToClose));Assert.That(session.State.Diary,Is.Empty);
            session.Apply(Command("close",ActivityOperation.CloseExpedition));Assert.That(Open().State.Expedition.Status,Is.EqualTo(ExpeditionStatus.Completed));Assert.That(session.State.Diary.Count,Is.EqualTo(1));
            Assert.That(session.Apply(Command("close",ActivityOperation.CloseExpedition)).Changed,Is.False);
        }
        [Test] public void PilotPracticeAdvancesInOrderWithCommittedManualRecordsOnly()
        {
            var plan=ExpeditionPlan.Select(ExpeditionMood.PilotPractice,Array.Empty<GalaxyEntityId>(),Array.Empty<ResidentRoute>());
            var session=new ActivitySession(new MemoryActivityRepository(),Id,AuthoredActivityCatalog.Definitions,flightEligibility:_=>true,expeditionEligibility:_=>true);
            session.Apply(Command("start",ActivityOperation.StartExpedition,plan));
            foreach(var course in new[]{"ring-run","figure-eight","stable-orbit"})
                session.Apply(new ActivityIntent(course,Id,ActivityOperation.CompleteFlight,DateTime.UtcNow,flight:new FlightRecord(course,1,ClustrGalaxy.Core.Navigation.NavigationFlightMode.PilotAssist,30,1,"tuning")));
            Assert.That(session.State.Expedition.Status,Is.EqualTo(ExpeditionStatus.ReadyToClose));
        }
    }
}
