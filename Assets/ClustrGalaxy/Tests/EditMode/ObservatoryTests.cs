using System;
using System.IO;
using System.Linq;
using ClustrGalaxy.Core.Activities;
using ClustrGalaxy.Core.Persistence;
using ClustrGalaxy.Core.Survey;
using ClustrGalaxy.Persistence;
using NUnit.Framework;
namespace ClustrGalaxy.Tests
{
    public sealed class ObservatoryTests
    {
        private static readonly ActivityIdentity Id=new("pilot",new SurveySnapshotId(9,3,"demo"),ActivityWorldMode.Explore);
        private sealed class Store:IDurableDocumentStore
        {
            public string Document;public bool Fail;
            public bool TryRead(string p,out string d){d=Document;return d!=null;}
            public void Write(string p,string d,Func<string,bool> valid){if(Fail)throw new IOException("fixture");if(!valid(d))throw new InvalidDataException();Document=d;}
            public void Quarantine(string p){throw new InvalidDataException();}
        }
        private static ActivityIntent Note(string id)=>new(id,Id,ActivityOperation.Bookmark,DateTime.UtcNow,value:"A quiet moment "+id);
        [Test] public void SixSlotsAreBoundedAndUnknownItemsCannotBePlaced()
        {
            Assert.Throws<ArgumentException>(()=>new ObservatoryDisplay(6,ObservatoryItemKind.Journal,"note"));
            var session=new ActivitySession(new MemoryActivityRepository(),Id,AuthoredActivityCatalog.Definitions);
            Assert.That(session.Apply(new ActivityIntent("bad",Id,ActivityOperation.PlaceDisplay,DateTime.UtcNow,display:new ObservatoryDisplay(0,ObservatoryItemKind.Keepsake,"trail-harmonic"))).Committed,Is.False);
        }
        [Test] public void FailedPlacementRetriesAndRestoresCanonicalCaption()
        {
            var store=new Store();var repo=new ActivityFileRepository(store);var session=new ActivitySession(repo,Id,AuthoredActivityCatalog.Definitions);session.Apply(Note("moment"));
            var before=session.State;var intent=new ActivityIntent("place",Id,ActivityOperation.PlaceDisplay,DateTime.UtcNow,display:new ObservatoryDisplay(5,ObservatoryItemKind.Journal,"moment","forged caption"));
            store.Fail=true;Assert.That(session.Apply(intent).Committed,Is.False);Assert.That(session.State,Is.SameAs(before));
            store.Fail=false;Assert.That(session.Apply(intent).Committed,Is.True);Assert.That(session.Apply(intent).Changed,Is.False);
            var restored=repo.LoadOrCreate(Id);Assert.That(restored.Displays.Single().Slot,Is.EqualTo(5));Assert.That(restored.Displays.Single().Caption,Is.EqualTo("A quiet moment moment"));
        }
        [Test] public void ClearIsDurableRetryableAndRetainsTheOriginalItem()
        {
            var store=new Store();var repo=new ActivityFileRepository(store);var session=new ActivitySession(repo,Id,AuthoredActivityCatalog.Definitions);session.Apply(Note("moment"));
            session.Apply(new ActivityIntent("place",Id,ActivityOperation.PlaceDisplay,DateTime.UtcNow,display:new ObservatoryDisplay(2,ObservatoryItemKind.Journal,"moment")));
            var before=session.State;var clear=new ActivityIntent("clear",Id,ActivityOperation.ClearDisplay,DateTime.UtcNow,value:"2");
            store.Fail=true;Assert.That(session.Apply(clear).Committed,Is.False);Assert.That(session.State,Is.SameAs(before));
            store.Fail=false;Assert.That(session.Apply(clear).Committed,Is.True);Assert.That(session.Apply(clear).Changed,Is.False);
            var restored=repo.LoadOrCreate(Id);Assert.That(restored.Displays,Is.Empty);Assert.That(restored.Diary.Single().Id,Is.EqualTo("moment"));
            Assert.That(session.Apply(new ActivityIntent("invalid",Id,ActivityOperation.ClearDisplay,DateTime.UtcNow,value:"6")).Committed,Is.False);
        }
        [Test] public void ChosenJournalMomentSurvivesAutomaticDiaryEviction()
        {
            var session=new ActivitySession(new MemoryActivityRepository(),Id,AuthoredActivityCatalog.Definitions);session.Apply(Note("moment"));
            session.Apply(new ActivityIntent("place",Id,ActivityOperation.PlaceDisplay,DateTime.UtcNow,display:new ObservatoryDisplay(0,ObservatoryItemKind.Journal,"moment")));
            for(var i=0;i<205;i++)session.Apply(Note("later"+i));
            Assert.That(session.State.Diary.Count,Is.EqualTo(200));Assert.That(session.State.Diary.Any(x=>x.Id=="moment"),Is.False);
            Assert.That(session.State.Displays.Single().Caption,Is.EqualTo("A quiet moment moment"));
        }
    }
}
