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
    public sealed class PermanentBookmarkTests
    {
        private static readonly ActivityIdentity Id=new("pilot",new SurveySnapshotId(9,3,"demo"),ActivityWorldMode.Explore);
        private sealed class Store:IDurableDocumentStore
        {
            public string Document;public bool Fail;
            public bool TryRead(string p,out string d){d=Document;return d!=null;}
            public void Write(string p,string d,Func<string,bool> valid){if(Fail)throw new IOException("fixture");if(!valid(d))throw new InvalidDataException();Document=d;}
            public void Quarantine(string p){throw new InvalidDataException();}
        }
        private static ActivityIntent Intent(string id,ActivityOperation operation,string text)=>new(id,Id,operation,DateTime.UtcNow,value:text);
        [Test] public void PermanentBookmarksSurviveAutomaticEvictionAndFileRestart()
        {
            var store=new Store();var repo=new ActivityFileRepository(store);var session=new ActivitySession(repo,Id,AuthoredActivityCatalog.Definitions);
            session.Apply(Intent("saved",ActivityOperation.SaveBookmark,"Keep me"));
            for(var i=0;i<205;i++)session.Apply(Intent("auto"+i,ActivityOperation.Bookmark,"Automatic "+i));
            var restored=repo.LoadOrCreate(Id);Assert.That(restored.Diary.Count,Is.EqualTo(201));Assert.That(restored.Diary.Count(x=>!x.Retained),Is.EqualTo(200));Assert.That(restored.Diary.Single(x=>x.Id=="saved").Retained,Is.True);
        }
        [Test] public void SaveAndRemovalAreRetryableAndIdempotentWithoutLosingTheOriginalOnFailure()
        {
            var store=new Store();var session=new ActivitySession(new ActivityFileRepository(store),Id,AuthoredActivityCatalog.Definitions);
            var save=Intent("saved",ActivityOperation.SaveBookmark,"Keep me");var before=session.State;store.Fail=true;
            Assert.That(session.Apply(save).Committed,Is.False);Assert.That(session.State,Is.SameAs(before));store.Fail=false;Assert.That(session.Apply(save).Committed,Is.True);Assert.That(session.Apply(save).Changed,Is.False);
            before=session.State;var remove=Intent("remove",ActivityOperation.DeleteBookmark,"saved");store.Fail=true;Assert.That(session.Apply(remove).Committed,Is.False);Assert.That(session.State,Is.SameAs(before));
            store.Fail=false;Assert.That(session.Apply(remove).Committed,Is.True);Assert.That(session.Apply(remove).Changed,Is.False);Assert.That(session.State.Diary,Is.Empty);
            Assert.That(session.Apply(save).Changed,Is.False);Assert.That(session.State.Diary,Is.Empty);
        }
        [Test] public void KeepingAnAutomaticMomentRetainsItsIdentityTextAndTime()
        {
            var session=new ActivitySession(new MemoryActivityRepository(),Id,AuthoredActivityCatalog.Definitions);session.Apply(Intent("auto",ActivityOperation.Bookmark,"Automatic"));var original=session.State.Diary.Single();
            Assert.That(session.Apply(Intent("reject",ActivityOperation.DeleteBookmark,"auto")).Committed,Is.False);
            Assert.That(session.Apply(Intent("keep",ActivityOperation.RetainDiaryEntry,"auto")).Committed,Is.True);var saved=session.State.Diary.Single();Assert.That(saved.Retained,Is.True);Assert.That(saved.Utc,Is.EqualTo(original.Utc));Assert.That(saved.Id,Is.EqualTo(original.Id));Assert.That(saved.Text,Is.EqualTo(original.Text));
        }
        [Test] public void LegacyExplicitBookmarksRestoreWithoutChangingAutomaticMoments()
        {
            var store=new Store();var repo=new ActivityFileRepository(store);var session=new ActivitySession(repo,Id,AuthoredActivityCatalog.Definitions);session.Apply(Intent("bookmark:old",ActivityOperation.Bookmark,"Old chosen note"));session.Apply(Intent("survey:old",ActivityOperation.Bookmark,"Old automatic note"));
            store.Document=store.Document.Replace(",\"retained\":false","");var restored=repo.LoadOrCreate(Id);
            Assert.That(restored.Diary.Single(x=>x.Id=="bookmark:old").Retained,Is.True);Assert.That(restored.Diary.Single(x=>x.Id=="survey:old").Retained,Is.False);
        }
    }
}
