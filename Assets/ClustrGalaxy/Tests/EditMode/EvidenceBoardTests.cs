using System;
using System.IO;
using System.Linq;
using ClustrGalaxy.Core.Activities;
using ClustrGalaxy.Core.Domain;
using ClustrGalaxy.Core.Persistence;
using ClustrGalaxy.Core.Survey;
using ClustrGalaxy.Persistence;
using NUnit.Framework;
namespace ClustrGalaxy.Tests
{
    public sealed class EvidenceBoardTests
    {
        private static readonly ActivityIdentity Id=new("pilot",new SurveySnapshotId(9,3,"demo"),ActivityWorldMode.Explore);
        private static ActivityDefinition Case=>AuthoredActivityCatalog.Definitions.Single(x=>x.Id=="mystery-chorus");
        private sealed class Store:IDurableDocumentStore
        {
            public string Document; public bool Fail;
            public bool TryRead(string path,out string text){text=Document;return text!=null;}
            public void Write(string path,string text,Func<string,bool> valid){if(Fail)throw new IOException("fixture");if(!valid(text))throw new InvalidDataException();Document=text;}
            public void Quarantine(string path)=>throw new InvalidDataException();
        }
        private static ActivitySession Ready(IActivityRepository repository)
        {
            var session=new ActivitySession(repository,Id,AuthoredActivityCatalog.Definitions);
            foreach(var clue in Case.Observations)session.Apply(new ActivityIntent(clue,Id,ActivityOperation.Observe,DateTime.UtcNow,Case.Id,clue));
            return session;
        }
        private static EvidenceBoard Connected()
        {var layout=EvidenceBoard.Default(Case).Layout;return new EvidenceBoard(Case.Id,layout.Connect(layout.Nodes[0].Id,layout.Nodes[1].Id));}
        [Test] public void InterpretationRequiresFiveDiscoveredCardsAndAConnection()
        {
            var session=Ready(new MemoryActivityRepository());
            Assert.That(session.Apply(new ActivityIntent("missing",Id,ActivityOperation.Interpret,DateTime.UtcNow,Case.Id,Case.Interpretations[0])).Committed,Is.False);
            Assert.That(session.Apply(new ActivityIntent("empty",Id,ActivityOperation.Interpret,DateTime.UtcNow,Case.Id,Case.Interpretations[0],evidenceBoard:EvidenceBoard.Default(Case))).Committed,Is.False);
            var unknown=Connected().Layout.Nodes.Select((n,i)=>i==0?new DesignNode((GalaxyEntityId)"unknown",n.X,n.Y):n);
            Assert.Throws<ArgumentException>(()=>new EvidenceBoard(Case.Id,new ConstellationDesign(Case.Id,unknown)).Validate(Case,session.State.Find(Case.Id),false));
            var fresh=new ActivitySession(new MemoryActivityRepository(),Id,AuthoredActivityCatalog.Definitions);
            Assert.That(fresh.Apply(new ActivityIntent("hidden",Id,ActivityOperation.SaveEvidenceBoard,DateTime.UtcNow,Case.Id,evidenceBoard:Connected())).Committed,Is.False);
        }
        [Test] public void BoardAndInterpretationCommitAtomicallyRetryAndRestore()
        {
            var store=new Store();var repo=new ActivityFileRepository(store);var session=Ready(repo);var before=session.State;
            var intent=new ActivityIntent("submit",Id,ActivityOperation.Interpret,DateTime.UtcNow,Case.Id,Case.Interpretations[1],"A personal reading.",evidenceBoard:Connected());
            store.Fail=true;Assert.That(session.Apply(intent).Committed,Is.False);Assert.That(session.State,Is.SameAs(before));
            store.Fail=false;Assert.That(session.Apply(intent).Committed,Is.True);Assert.That(session.Apply(intent).Changed,Is.False);
            var restored=new ActivitySession(repo,Id,AuthoredActivityCatalog.Definitions).State;
            Assert.That(restored.EvidenceBoards.Single().Layout.Edges.Count,Is.EqualTo(1));Assert.That(restored.Find(Case.Id).Completed,Is.True);
            Assert.That(restored.Find(Case.Id).Explanation,Is.EqualTo("A personal reading."));Assert.That(restored.Diary.Count(x=>x.Id=="interpretation:"+Case.Id),Is.EqualTo(1));
        }
        [Test] public void HistoricalCompletionWithoutBoardIsPreservedAndCanBeDecorated()
        {
            var repo=new MemoryActivityRepository();repo.Save(new ActivityState(Id,new[]{new ActivityRecord(Case.Id,Case.ContentVersion,Case.Observations,true,Case.Interpretations[0])}));
            var session=new ActivitySession(repo,Id,AuthoredActivityCatalog.Definitions);
            Assert.That(session.State.Find(Case.Id).Completed,Is.True);Assert.That(session.State.EvidenceBoards,Is.Empty);
            Assert.That(session.Apply(new ActivityIntent("decorate",Id,ActivityOperation.SaveEvidenceBoard,DateTime.UtcNow,Case.Id,evidenceBoard:Connected())).Committed,Is.True);
            Assert.That(session.State.Diary,Is.Empty);Assert.That(session.State.Find(Case.Id).Interpretation,Is.EqualTo(Case.Interpretations[0]));
        }
        [Test] public void ExistingFileWithoutBoardCompanionRestoresHistoricalCompletion()
        {
            var store=new Store();var repo=new ActivityFileRepository(store);
            repo.Save(new ActivityState(Id,new[]{new ActivityRecord(Case.Id,Case.ContentVersion,Case.Observations,true,Case.Interpretations[0])}));
            var start=store.Document.IndexOf("\"evidence_boards\":",StringComparison.Ordinal);
            var end=store.Document.IndexOf("\"records\":",start,StringComparison.Ordinal);
            store.Document=store.Document.Remove(start,end-start);
            var restored=new ActivitySession(repo,Id,AuthoredActivityCatalog.Definitions).State;
            Assert.That(restored.Find(Case.Id).Completed,Is.True);Assert.That(restored.EvidenceBoards,Is.Empty);
        }
        [Test] public void LiveAndOtherSnapshotBoardsCannotMutateTheSession()
        {
            var id=new ActivityIdentity("pilot",new SurveySnapshotId(9,3,"live"),ActivityWorldMode.Explore);
            var session=new ActivitySession(new MemoryActivityRepository(),id,AuthoredActivityCatalog.Definitions);
            Assert.That(session.Apply(new ActivityIntent("fiction",id,ActivityOperation.SaveEvidenceBoard,DateTime.UtcNow,Case.Id,evidenceBoard:Connected())).Committed,Is.False);
            Assert.Throws<InvalidOperationException>(()=>session.Apply(new ActivityIntent("wrong",Id,ActivityOperation.SaveEvidenceBoard,DateTime.UtcNow,Case.Id,evidenceBoard:Connected())));
        }
    }
}
