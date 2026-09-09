using System;
using System.IO;
using System.Numerics;
using ClustrGalaxy.Core.Activities;
using ClustrGalaxy.Core.Navigation;
using ClustrGalaxy.Core.Survey;
using ClustrGalaxy.Persistence;
using NUnit.Framework;
namespace ClustrGalaxy.Tests
{
    public sealed class LocalReplayLibraryTests
    {
        private string root;
        [SetUp] public void Setup(){root=Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);}
        [TearDown] public void Cleanup(){Directory.Delete(root,true);}
        private JourneyReplay Replay()=>new(new ReplayIdentity(new SurveySnapshotId(9,3,"demo"),ActivityWorldMode.Explore,1,"tuning",NavigationFlightMode.PilotAssist,"ring-run",1),new[]{new ReplayPose(0,default,Quaternion.Identity),new ReplayPose(.1,new ReplayPosition(1,0,0),Quaternion.Identity)});
        [Test] public void ImportIntoSeparateStoragePreservesSourceAndReplayIdentity()
        {
            var source=Path.Combine(root,"source.clustr-replay");ReplayPackage.Save(source,Replay());var original=File.ReadAllBytes(source);
            var library=new LocalReplayLibrary(Path.Combine(root,"other-profile"));var imported=library.Import(source);
            Assert.That(File.ReadAllBytes(source),Is.EqualTo(original));Assert.That(library.Read(imported).Identity.Snapshot,Is.EqualTo(Replay().Identity.Snapshot));
            Assert.That(library.Files(),Is.EqualTo(new[]{imported}));
        }
        [Test] public void MalformedOrNetworkImportDoesNotWriteDestination()
        {
            var source=Path.Combine(root,"bad.clustr-replay");File.WriteAllBytes(source,new byte[]{1,2,3});var destination=Path.Combine(root,"library");var library=new LocalReplayLibrary(destination);
            Assert.Throws<InvalidDataException>(()=>library.Import(source));Assert.Throws<InvalidDataException>(()=>library.Import("https://example.invalid/replay"));Assert.That(Directory.Exists(destination),Is.False);
        }
        [Test] public void DeletionCannotTouchV1OrUnlistedFiles()
        {
            var v1=Path.Combine(root,"v1");Directory.CreateDirectory(v1);var sentinel=Path.Combine(v1,"sentinel.png");File.WriteAllText(sentinel,"do not touch");
            var library=new LocalReplayLibrary(root);Assert.Throws<InvalidOperationException>(()=>library.Delete(sentinel));Assert.That(File.ReadAllText(sentinel),Is.EqualTo("do not touch"));
            var imported=library.Import(SaveSource());library.Delete(imported);Assert.That(File.Exists(imported),Is.False);
        }
        private string SaveSource(){var source=Path.Combine(root,"source.clustr-replay");ReplayPackage.Save(source,Replay());return source;}
    }
}
