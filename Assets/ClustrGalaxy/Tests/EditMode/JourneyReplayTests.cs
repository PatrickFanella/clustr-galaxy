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
    public sealed class JourneyReplayTests
    {
        private static ReplayIdentity Identity(string course = "ring", int version = 1, string tuning = "flight-v1",
            NavigationFlightMode flight = NavigationFlightMode.PilotAssist, long revision = 9) =>
            new(new SurveySnapshotId(revision,3,"demo"),ActivityWorldMode.Survey,1,tuning,flight,course,version);
        private static ReplayPose Pose(double t, double x = 0) => new(t,new ReplayPosition(x,0,0),Quaternion.Identity);
        private static JourneyReplay Replay() => new(Identity(),new[] { Pose(0,1e12),Pose(.1,1e12+10) });
        [Test] public void RecordsExactlyTenHzFromIrregularRenderFramesWithoutAllocating()
        {
            var buffer = new JourneyReplayBuffer(); buffer.Record(0,default,Quaternion.Identity);
            for (var i = 1; i <= 100; i++) buffer.Record(i/144d,new ReplayPosition(i/144d,0,0),Quaternion.Identity);
            buffer.Clear();
            var before = GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i <= 14400; i++) buffer.Record(i/144d,new ReplayPosition(i/144d,0,0),Quaternion.Identity);
            var bytes = GC.GetAllocatedBytesForCurrentThread()-before;
            Assert.That(bytes,Is.Zero); Assert.That(buffer.Count,Is.EqualTo(1001));
            var replay = buffer.Capture(Identity());
            for (var i = 0; i < replay.Poses.Count; i++)
            { Assert.That(replay.Poses[i].Seconds,Is.EqualTo(i*.1).Within(1e-7)); Assert.That(replay.Poses[i].Position.X,Is.EqualTo(i*.1).Within(1e-7)); }
        }
        [Test] public void RollingBufferCapsSamplesAndDurationAndCaptureIsImmutable()
        {
            var buffer = new JourneyReplayBuffer();
            for (var i = 0; i < 10000; i++) buffer.Record(i*.1,new ReplayPosition(i,0,0),Quaternion.Identity);
            Assert.That(buffer.Count,Is.EqualTo(9000)); var saved = buffer.Capture(Identity());
            Assert.That(saved.Poses[0].Position.X,Is.EqualTo(1000).Within(.001));
            Assert.That(saved.Duration,Is.EqualTo(899.9).Within(1e-6));
            buffer.Clear(); Assert.That(saved.Poses.Count,Is.EqualTo(9000));
            Assert.Throws<InvalidOperationException>(() => buffer.Capture(Identity()));
        }
        [Test] public void GapsAreHeldAndOldHistoryExpiresWithoutInventingSamples()
        {
            var buffer = new JourneyReplayBuffer(); buffer.Record(0,default,Quaternion.Identity);
            buffer.Record(1,new ReplayPosition(100,0,0),Quaternion.Identity);
            Assert.That(buffer.Capture(Identity()).At(.5).Position.X,Is.Zero);
            buffer.Record(1000,new ReplayPosition(200,0,0),Quaternion.Identity);
            Assert.That(buffer.Count,Is.EqualTo(1));
            Assert.Throws<ArgumentException>(() => buffer.Record(999,default,Quaternion.Identity));
        }
        [Test] public void InterpolationPreservesLogicalCoordinatesAcrossOriginShifts()
        {
            // Both poses reconstruct origin + local coordinates before recording.
            var replay = Replay(); Assert.That(replay.At(.05).Position.X,Is.EqualTo(1e12+5));
            Assert.That(replay.At(-1).Seconds,Is.Zero); Assert.That(replay.At(100).Seconds,Is.EqualTo(.1));
            Assert.Throws<ArgumentException>(() => replay.At(double.NaN));
        }
        [Test] public void GhostCompatibilityRequiresAllIdentitiesAndSeparateFlightModes()
        {
            var id = Identity(); Assert.That(id.IsCompetitiveWith(Identity()),Is.True);
            Assert.That(id.IsCompetitiveWith(Identity(version:2)),Is.False);
            Assert.That(id.IsCompetitiveWith(Identity(tuning:"flight-v2")),Is.False);
            Assert.That(id.IsCompetitiveWith(Identity(flight:NavigationFlightMode.PilotMastery)),Is.False);
            Assert.That(id.IsCompetitiveWith(Identity(revision:10)),Is.False);
            Assert.That(Identity("",0).IsCompetitiveWith(Identity("",0)),Is.False);
            Assert.That(Identity(flight:NavigationFlightMode.Autopilot).IsCompetitiveWith(Identity(flight:NavigationFlightMode.Autopilot)),Is.False);
        }
        [Test] public void ReplayPackageRoundTripsWithoutAnyProgressionService()
        {
            using var stream = new MemoryStream(); ReplayPackage.Write(stream,Replay()); stream.Position = 0;
            var loaded = ReplayPackage.Read(stream); Assert.That(loaded.Poses.Count,Is.EqualTo(2));
            Assert.That(loaded.At(.05).Position.X,Is.EqualTo(1e12+5)); Assert.That(loaded.Identity.IsCompetitiveWith(Identity()),Is.True);
        }
        [Test] public void AnnotationRoundTripKeepsDeclarativeLabelsAndCourseFrames()
        {
            var source=Replay();var replay=new JourneyReplay(source.Identity,source.Poses,events:new[]{new ReplayEvent(.1,ReplayEventKind.PersonalBookmark,"A quiet moment")});
            using var stream=new MemoryStream();ReplayPackage.Write(stream,replay.WithCourseAnchor(default));stream.Position=0;var restored=ReplayPackage.Read(stream);
            Assert.That(restored.Events.Count,Is.EqualTo(1));Assert.That(restored.Events[0].Label,Is.EqualTo("A quiet moment"));Assert.That(restored.Events[0].Kind,Is.EqualTo(ReplayEventKind.PersonalBookmark));
        }
        [Test] public void AnnotationLimitsRejectUnsupportedKindsTextTimingAndCounts()
        {
            foreach(var text in new[]{"https://example.com","/tmp/asset","www.example.com","<script>","line\nfeed",new string('x',65)})Assert.Throws<ArgumentException>(()=>new ReplayEvent(0,ReplayEventKind.PersonalBookmark,text));
            Assert.Throws<ArgumentException>(()=>new ReplayEvent(0,(ReplayEventKind)99,"Moment"));Assert.Throws<ArgumentException>(()=>new ReplayEvent(double.NaN,ReplayEventKind.PersonalBookmark,"Moment"));
            Assert.Throws<ArgumentException>(()=>new JourneyReplay(Identity(),new[]{Pose(0)},events:new[]{new ReplayEvent(1,ReplayEventKind.PersonalBookmark,"Future")}));
            using var stream=new MemoryStream();ReplayPackage.Write(stream,Replay());var bytes=stream.ToArray();Array.Copy(BitConverter.GetBytes(257),0,bytes,bytes.Length-4,4);
            Assert.Throws<InvalidDataException>(()=>ReplayPackage.Read(new MemoryStream(bytes)));
        }
        [Test] public void AnnotationRingIsBoundedAndNormalizesTheCapturedWindow()
        {
            var buffer=new JourneyReplayBuffer();buffer.Record(0,default,Quaternion.Identity);
            for(var i=0;i<300;i++){buffer.Record(i*.1,default,Quaternion.Identity);buffer.RecordEvent(new ReplayEvent(i*.1,ReplayEventKind.PersonalBookmark,"Moment"));}
            var saved=buffer.Capture(Identity());Assert.That(saved.Events.Count,Is.EqualTo(256));Assert.That(saved.Events[0].Seconds,Is.EqualTo(4.4).Within(.001));
            buffer.Record(1000,default,Quaternion.Identity);Assert.That(buffer.Capture(Identity()).Events,Is.Empty);
        }
        [Test] public void BookmarkSavedWhileRecordingIsPausedIsKeptAtTheLastPose()
        {
            var buffer=new JourneyReplayBuffer();
            for(var i=0;i<=10;i++)buffer.Record(i*.1,default,Quaternion.Identity);
            buffer.RecordEvent(new ReplayEvent(3,ReplayEventKind.PersonalBookmark,"Saved from menu"));
            var saved=buffer.Capture(Identity());
            Assert.That(saved.Events.Count,Is.EqualTo(1));
            Assert.That(saved.Events[0].Seconds,Is.EqualTo(saved.Poses[saved.Poses.Count-1].Seconds).Within(1e-9));
        }
        [Test] public void ExplicitCourseFrameRoundTripsAndLegacyPackagesStayStandalone()
        {
            var original=Replay();using var stream=new MemoryStream();ReplayPackage.Write(stream,original.WithCourseAnchor(new ReplayPosition(1e12,4,9)));
            stream.Position=0;var restored=ReplayPackage.Read(stream);Assert.That(restored.CourseAnchor.Value.X,Is.EqualTo(1e12));
            using var plain=new MemoryStream();ReplayPackage.Write(plain,original);var modern=plain.ToArray();Array.Resize(ref modern,modern.Length-4);modern[4]=2;Assert.That(ReplayPackage.Read(new MemoryStream(modern)).Events,Is.Empty);
            var frameOffset=modern.Length-original.Poses.Count*48-5;var old=new byte[modern.Length-1];
            Array.Copy(modern,0,old,0,frameOffset);Array.Copy(modern,frameOffset+1,old,frameOffset,modern.Length-frameOffset-1);old[4]=1;
            var legacy=ReplayPackage.Read(new MemoryStream(old));Assert.That(legacy.CourseAnchor.HasValue,Is.False);Assert.That(legacy.Poses.Count,Is.EqualTo(original.Poses.Count));
            modern[frameOffset]=2;Assert.Throws<InvalidDataException>(()=>ReplayPackage.Read(new MemoryStream(modern)));
        }
        [Test] public void RejectsUnknownVersionTrailingContentTruncationAndInvalidNumbers()
        {
            using var stream = new MemoryStream(); ReplayPackage.Write(stream,Replay()); var valid = stream.ToArray();
            var version = (byte[])valid.Clone(); version[4] = 99;
            var trailing = new byte[valid.Length+1]; Array.Copy(valid,trailing,valid.Length);
            var truncated = new byte[valid.Length-1]; Array.Copy(valid,truncated,truncated.Length);
            var invalid = (byte[])valid.Clone(); Array.Copy(BitConverter.GetBytes(double.NaN),0,invalid,invalid.Length-48,8);
            foreach (var bytes in new[] { version,trailing,truncated,invalid,new byte[0] })
                Assert.Throws<InvalidDataException>(() => ReplayPackage.Read(new MemoryStream(bytes)));
        }
        [Test] public void RejectsOversizedPackagesAndSampleCountBeforeAllocatingPoses()
        {
            using var stream = new MemoryStream(); ReplayPackage.Write(stream,Replay()); var bytes = stream.ToArray();
            Array.Copy(BitConverter.GetBytes(9001),0,bytes,bytes.Length-96-8,4);
            Assert.Throws<InvalidDataException>(() => ReplayPackage.Read(new MemoryStream(bytes)));
            using var huge = new MemoryStream(); huge.SetLength(ReplayPackage.MaximumBytes+1);
            Assert.Throws<InvalidDataException>(() => ReplayPackage.Read(huge));
        }
        [Test] public void RejectsAssetPathsUrlsInvalidPosesAndHighFrequencyImports()
        {
            foreach (var value in new[] { "https://host", "../asset", "/tmp/asset", "script()", "file:asset" })
                Assert.Throws<ArgumentException>(() => Identity(tuning:value));
            Assert.Throws<ArgumentException>(() => new ReplayPosition(double.PositiveInfinity,0,0));
            Assert.Throws<ArgumentException>(() => new ReplayPose(0,default,default));
            Assert.Throws<ArgumentException>(() => new JourneyReplay(Identity(),new[] { Pose(0),Pose(.01) }));
            Assert.Throws<ArgumentException>(() => new JourneyReplay(Identity(),new[] { Pose(0),Pose(901) }));
        }
        [Test] public void ExplicitSaveIsDurableAndNeverOverwritesAnExistingReplay()
        {
            var folder = Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString("N")); Directory.CreateDirectory(folder);
            try
            {
                var path = Path.Combine(folder,"journey.clustr-replay"); ReplayPackage.Save(path,Replay()); var before = File.ReadAllBytes(path);
                Assert.Throws<IOException>(() => ReplayPackage.Save(path,Replay()));
                Assert.That(File.ReadAllBytes(path),Is.EqualTo(before)); Assert.That(Directory.GetFiles(folder).Length,Is.EqualTo(1));
                using var file = File.OpenRead(path); Assert.That(ReplayPackage.Read(file).Duration,Is.EqualTo(.1));
            }
            finally { Directory.Delete(folder,true); }
        }
    }
}
