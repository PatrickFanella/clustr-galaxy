using System;
using System.IO;
using System.Linq;
using ClustrGalaxy.Core.Domain;
using ClustrGalaxy.Core.Persistence;
using ClustrGalaxy.Core.Survey;
using ClustrGalaxy.Persistence;
using NUnit.Framework;

namespace ClustrGalaxy.Tests
{
    public sealed class SurveyJourneyTests
    {
        [Test]
        public void DemoJourneyPersistsContractsConstellationDiscoveryAndConfirmedLocalQueue()
        {
            var root = Path.Combine(Path.GetTempPath(), "galaxy-journey-" + Guid.NewGuid().ToString("N"));
            try
            {
                var snapshot = new SurveySnapshotId(9,3,"demo");
                var repository = new SurveyFileRepository(new FileDurableDocumentStore(root));
                var ids = new[] { "subreddit:technology", "post:retro-computing", "post:open-source-ai",
                    "post:linux-handhelds", "post:right-to-repair", "post:privacy-tools" }.Select(x => (GalaxyEntityId)x).ToArray();
                var session = new SurveySession(repository,"pilot",snapshot,ids[0]);
                Assert.That(session.Apply(new StartSurveyIntent("start",snapshot,DateTime.UtcNow,ids[0],ids)).Succeeded,Is.True);
                var routes = new[]
                {
                    new SurveyRoute((GalaxyRouteId)"route:technology-signal-horizon", ids[0], (GalaxyEntityId)"subreddit:signal-horizon", 8),
                    new SurveyRoute((GalaxyRouteId)"route:technology-weak-archive", ids[0], (GalaxyEntityId)"subreddit:weak-archive", 2)
                };
                for (var i=0;i<ids.Length;i++)
                {
                    var scan = new CompleteScanIntent("scan:"+i,snapshot,DateTime.UtcNow,ids[i],
                        i==0?GalaxyEntityKind.Subreddit:GalaxyEntityKind.Post,routes,Array.Empty<GalaxyEntityId>(),ids[0],i==ids.Length-1);
                    Assert.That(session.Apply(scan).Succeeded,Is.True);
                    Assert.That(session.Apply(scan).Changed,Is.False);
                    if (i == 0)
                    {
                        Assert.That(session.State.KnownRoutes, Is.EquivalentTo(new[] { routes[0].Id }));
                        Assert.That(session.State.IsIdentified(routes[0].TargetId), Is.False);
                        Assert.That(session.State.IsKnown(routes[1].TargetId), Is.False);
                    }
                }
                Assert.That(session.State.KnownRoutes, Is.EquivalentTo(routes.Select(route => route.Id)));
                Assert.That(session.State.CompletedContracts.Count,Is.EqualTo(4));
                Assert.That(session.State.ScannerLevel,Is.EqualTo(3));
                Assert.That(session.State.CompletedConstellations.Count,Is.EqualTo(1));
                Assert.That(session.State.Discoveries,Does.Contain("easter-egg:signal-afterimage"));
                var saved=session.Apply(new SavePersonalConstellationIntent("save",snapshot,DateTime.UtcNow,"Local arc","Related discussions",ids.Take(3)));
                Assert.That(saved.Succeeded,Is.True);
                Assert.That(session.State.QueuedPersonalConstellationIds,Is.Empty);
                session.Apply(new RequestLocalQueueIntent("request",snapshot,DateTime.UtcNow,saved.PersonalConstellationId));
                Assert.That(session.State.QueuedPersonalConstellationIds,Is.Empty);
                Assert.That(session.Apply(new ConfirmLocalQueueIntent("confirm",snapshot,DateTime.UtcNow)).QueueStatus,
                    Is.EqualTo(SurveySession.QueuedLocallyStatus));
                var restored=new SurveySession(repository,"pilot",snapshot,ids[0]);
                Assert.That(restored.State.StarfieldAfterimageUnlocked, Is.True);
                Assert.That(restored.State.CompletedContracts,Is.EquivalentTo(session.State.CompletedContracts));
                Assert.That(restored.State.Achievements,Is.EquivalentTo(session.State.Achievements));
                Assert.That(restored.State.Discoveries,Is.EquivalentTo(session.State.Discoveries));
                Assert.That(restored.State.QueuedPersonalConstellationIds,Does.Contain(saved.PersonalConstellationId));
                Assert.That(restored.State.PersonalConstellations.Single().Reason,Is.EqualTo("Related discussions"));
            }
            finally { if(Directory.Exists(root))Directory.Delete(root,true); }
        }
    }
}
