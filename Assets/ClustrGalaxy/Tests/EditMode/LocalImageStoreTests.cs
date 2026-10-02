using System;
using System.IO;
using System.Linq;
using ClustrGalaxy.Core.Activities;
using ClustrGalaxy.Core.Survey;
using ClustrGalaxy.Persistence;
using NUnit.Framework;
namespace ClustrGalaxy.Tests
{
    public sealed class LocalImageStoreTests
    {
        private string root;
        private static readonly ActivityIdentity Id=new("pilot",new SurveySnapshotId(9,3,"demo"),ActivityWorldMode.Explore);
        [SetUp] public void Setup(){root=Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);}
        [TearDown] public void Cleanup(){Directory.Delete(root,true);}
        private static byte[] Header(int width,int height)
        {
            var data=new byte[24];new byte[]{137,80,78,71,13,10,26,10}.CopyTo(data,0);data[12]=73;data[13]=72;data[14]=68;data[15]=82;
            for(var i=0;i<4;i++){data[16+i]=(byte)(width>>(24-i*8));data[20+i]=(byte)(height>>(24-i*8));}return data;
        }
        [Test] public void ExplicitImageAndMetadataAppearTogetherAndDeletionPreservesOtherFiles()
        {
            var store=new LocalImageStore(root);var path=store.Save(Header(1920,1080),Id,DateTime.UtcNow);var library=new LocalReplayLibrary(root);
            Assert.That(library.Files(),Is.EqualTo(new[]{path}));Assert.That(LocalImageStore.Dimensions(File.ReadAllBytes(path)),Is.EqualTo((1920,1080)));
            var metadata=Path.Combine(Path.GetDirectoryName(path),"metadata.json");Assert.That(File.ReadAllText(metadata),Does.Contain("\"profile_id\":\"pilot\""));
            Assert.That(library.BytesFor(path),Is.EqualTo(new FileInfo(path).Length+new FileInfo(metadata).Length));
            var unrelated=Path.Combine(Path.GetDirectoryName(path),"user-note.txt");File.WriteAllText(unrelated,"retain");library.Delete(path);
            Assert.That(File.Exists(path),Is.False);Assert.That(File.Exists(metadata),Is.False);Assert.That(File.ReadAllText(unrelated),Is.EqualTo("retain"));
        }
        [Test] public void FailedMetadataWriteNeverPublishesAnImageAndRetrySucceeds()
        {
            var store=new LocalImageStore(root,(_,_)=>throw new IOException("fixture"));
            Assert.Throws<IOException>(()=>store.Save(Header(1280,720),Id,DateTime.UtcNow));Assert.That(new LocalReplayLibrary(root).Files(),Is.Empty);
            Assert.That(Directory.EnumerateFileSystemEntries(Path.Combine(root,"images")),Is.Empty);
            Assert.That(File.Exists(new LocalImageStore(root).Save(Header(1280,720),Id,DateTime.UtcNow)),Is.True);
        }
        [Test] public void PendingDirectoriesAreInvisibleAndInvalidDimensionsDoNotCreateStorage()
        {
            var root2=Path.Combine(root,"invalid");Assert.Throws<InvalidDataException>(()=>new LocalImageStore(root2).Save(Header(9000,10),Id,DateTime.UtcNow));Assert.That(Directory.Exists(root2),Is.False);
            var pending=Path.Combine(root,"images",".pending-test");Directory.CreateDirectory(pending);File.WriteAllBytes(Path.Combine(pending,"image.png"),Header(1,1));File.WriteAllText(Path.Combine(pending,"metadata.json"),"{}");
            Assert.That(new LocalReplayLibrary(root).Files(),Is.Empty);Assert.Throws<InvalidDataException>(()=>LocalImageStore.Dimensions(new byte[24]));
        }
    }
}
