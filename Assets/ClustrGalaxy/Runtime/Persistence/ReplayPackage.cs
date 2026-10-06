using System;
using System.IO;
using System.Numerics;
using System.Text;
using ClustrGalaxy.Core.Activities;
using ClustrGalaxy.Core.Navigation;
using ClustrGalaxy.Core.Survey;

namespace ClustrGalaxy.Persistence
{
    /// <summary>Fixed declarative binary schema: no object types, commands, assets, URLs, or provider callbacks.</summary>
    public static class ReplayPackage
    {
        public const long MaximumBytes = 64L*1024*1024;
        private const uint Magic = 0x52544743; // CGTR little endian
        private const int Version = 3;
        private static readonly Encoding Utf8 = new UTF8Encoding(false,true);
        public static void Write(Stream stream, JourneyReplay replay)
        {
            using var writer = new BinaryWriter(stream,Utf8,true);
            writer.Write(Magic); writer.Write(Version);
            var id = replay.Identity; writer.Write(id.Snapshot.RevisionId); writer.Write(id.Snapshot.CatalogId);
            Text(writer,id.Snapshot.StorageNamespace); writer.Write((int)id.WorldMode); writer.Write(id.ContentVersion);
            Text(writer,id.TuningId); writer.Write((int)id.FlightMode); Text(writer,id.CourseId); writer.Write(id.CourseVersion);
            writer.Write(replay.CourseAnchor.HasValue);
            if(replay.CourseAnchor.HasValue){var anchor=replay.CourseAnchor.Value;writer.Write(anchor.X);writer.Write(anchor.Y);writer.Write(anchor.Z);}
            writer.Write(replay.Poses.Count);
            foreach (var p in replay.Poses)
            {
                writer.Write(p.Seconds); writer.Write(p.Position.X); writer.Write(p.Position.Y); writer.Write(p.Position.Z);
                writer.Write(p.Rotation.X); writer.Write(p.Rotation.Y); writer.Write(p.Rotation.Z); writer.Write(p.Rotation.W);
            }
            writer.Write(replay.Events.Count);
            foreach(var e in replay.Events){writer.Write(e.Seconds);writer.Write((int)e.Kind);Text(writer,e.Label);}
        }
        public static JourneyReplay Read(Stream stream)
        {
            if (stream == null || !stream.CanSeek || stream.Length-stream.Position > MaximumBytes)
                throw new InvalidDataException("Replay exceeds 64 MiB or cannot be bounded.");
            try
            {
                using var reader = new BinaryReader(stream,Utf8,true);
                if(reader.ReadUInt32()!=Magic)throw new InvalidDataException("Unsupported replay format.");
                var format=reader.ReadInt32();if(format!=1&&format!=2&&format!=Version)throw new InvalidDataException("Unsupported replay format/version.");
                var revision = reader.ReadInt64(); var catalog = reader.ReadInt64(); var space = Text(reader);
                if (!ReplayIdentity.Token(space,false)) throw new InvalidDataException("Invalid snapshot namespace.");
                var snapshot = new SurveySnapshotId(revision,catalog,space);
                var mode = (ActivityWorldMode)reader.ReadInt32(); var content = reader.ReadInt32(); var tuning = Text(reader);
                var flight = (NavigationFlightMode)reader.ReadInt32(); var course = Text(reader); var version = reader.ReadInt32();
                var identity = new ReplayIdentity(snapshot,mode,content,tuning,flight,course,version);
                ReplayPosition? anchor=null;
                if(format>=2)
                {
                    var flag=reader.ReadByte();if(flag>1)throw new InvalidDataException("Invalid replay frame flag.");
                    if(flag==1)anchor=new ReplayPosition(reader.ReadDouble(),reader.ReadDouble(),reader.ReadDouble());
                }
                var count = reader.ReadInt32();
                if (count < 1 || count > JourneyReplay.MaximumSamples || (format<3?stream.Length-stream.Position != count*48L:stream.Length-stream.Position < count*48L+4))
                    throw new InvalidDataException("Invalid replay sample count, truncated poses, or unsupported trailing content.");
                var poses = new ReplayPose[count];
                for (var i = 0; i < count; i++) poses[i] = new ReplayPose(reader.ReadDouble(),
                    new ReplayPosition(reader.ReadDouble(),reader.ReadDouble(),reader.ReadDouble()),
                    new Quaternion(reader.ReadSingle(),reader.ReadSingle(),reader.ReadSingle(),reader.ReadSingle()));
                var events=Array.Empty<ReplayEvent>();
                if(format>=3)
                {
                    var eventCount=reader.ReadInt32();if(eventCount<0||eventCount>JourneyReplay.MaximumEvents)throw new InvalidDataException("Invalid replay annotation count.");
                    events=new ReplayEvent[eventCount];for(var i=0;i<eventCount;i++)events[i]=new ReplayEvent(reader.ReadDouble(),(ReplayEventKind)reader.ReadInt32(),Text(reader));
                }
                if(stream.Position!=stream.Length)throw new InvalidDataException("Unsupported trailing replay content.");
                return new JourneyReplay(identity,poses,anchor,events);
            }
            catch (Exception exception) when (exception is ArgumentException || exception is EndOfStreamException || exception is OverflowException)
            { throw new InvalidDataException("Malformed replay package.",exception); }
        }
        public static void Save(string path, JourneyReplay replay)
        {
            var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using (var file = new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None))
                { Write(file,replay); file.Flush(true); }
                File.Move(temporary,path);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
        private static void Text(BinaryWriter writer,string value)
        { var bytes = Utf8.GetBytes(value); if (bytes.Length > 128) throw new InvalidDataException("Replay text too long."); writer.Write((ushort)bytes.Length); writer.Write(bytes); }
        private static string Text(BinaryReader reader)
        {
            var length = reader.ReadUInt16(); if (length > 128) throw new InvalidDataException("Replay text too long.");
            var bytes = reader.ReadBytes(length); if (bytes.Length != length) throw new EndOfStreamException(); return Utf8.GetString(bytes);
        }
    }
}
