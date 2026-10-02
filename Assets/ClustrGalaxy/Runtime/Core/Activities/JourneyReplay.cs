using System;
using System.Collections.Generic;
using System.Numerics;
using ClustrGalaxy.Core.Navigation;
using ClustrGalaxy.Core.Survey;

namespace ClustrGalaxy.Core.Activities
{
    public readonly struct ReplayPosition
    {
        public ReplayPosition(double x, double y, double z)
        {
            if (!Valid(x) || !Valid(y) || !Valid(z)) throw new ArgumentException("Invalid world position.");
            X = x; Y = y; Z = z;
        }
        private static bool Valid(double n) => !double.IsNaN(n) && !double.IsInfinity(n) && Math.Abs(n) <= 1e15;
        public double X { get; } public double Y { get; } public double Z { get; }
        public static ReplayPosition Lerp(ReplayPosition a, ReplayPosition b, double t) =>
            new(a.X + (b.X-a.X)*t, a.Y + (b.Y-a.Y)*t, a.Z + (b.Z-a.Z)*t);
    }
    public readonly struct ReplayPose
    {
        public ReplayPose(double seconds, ReplayPosition position, Quaternion rotation)
        {
            if (double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds < 0 ||
                float.IsNaN(rotation.LengthSquared()) || Math.Abs(rotation.LengthSquared()-1) > .01f)
                throw new ArgumentException("Invalid timed pose.");
            Seconds = seconds; Position = position; Rotation = Quaternion.Normalize(rotation);
        }
        public double Seconds { get; } public ReplayPosition Position { get; } public Quaternion Rotation { get; }
    }
    public sealed class ReplayIdentity
    {
        public ReplayIdentity(SurveySnapshotId snapshot, ActivityWorldMode worldMode, int contentVersion,
            string tuningId, NavigationFlightMode flightMode, string courseId = "", int courseVersion = 0)
        {
            if (snapshot.RevisionId <= 0 || snapshot.CatalogId < 0 || contentVersion < 1 ||
                !Enum.IsDefined(typeof(ActivityWorldMode), worldMode) || !Enum.IsDefined(typeof(NavigationFlightMode), flightMode) ||
                !Token(tuningId, false) || !Token(courseId, true) || (courseId.Length == 0 ? courseVersion != 0 : courseVersion < 1))
                throw new ArgumentException("Invalid replay identity.");
            Snapshot = snapshot; WorldMode = worldMode; ContentVersion = contentVersion; TuningId = tuningId;
            FlightMode = flightMode; CourseId = courseId; CourseVersion = courseVersion;
        }
        public static bool Token(string value, bool empty)
        {
            if (value == null || value.Length > 128 || (!empty && value.Length == 0)) return false;
            foreach (var c in value) if (!(c >= 'a' && c <= 'z') && !(c >= 'A' && c <= 'Z') &&
                !(c >= '0' && c <= '9') && c != '-' && c != '_') return false;
            return true;
        }
        public SurveySnapshotId Snapshot { get; } public ActivityWorldMode WorldMode { get; }
        public int ContentVersion { get; } public string TuningId { get; }
        public NavigationFlightMode FlightMode { get; } public string CourseId { get; } public int CourseVersion { get; }
        public bool IsCompetitiveWith(ReplayIdentity other) => other != null && CourseId.Length > 0 &&
            FlightMode != NavigationFlightMode.Autopilot && Snapshot == other.Snapshot && WorldMode == other.WorldMode &&
            ContentVersion == other.ContentVersion && TuningId == other.TuningId && FlightMode == other.FlightMode &&
            CourseId == other.CourseId && CourseVersion == other.CourseVersion;
    }
    public enum ReplayEventKind { PersonalBookmark, CommittedDiscovery, ActivityCompleted }
    public readonly struct ReplayEvent
    {
        public ReplayEvent(double seconds,ReplayEventKind kind,string label)
        {
            if(!double.IsFinite(seconds)||seconds<0||!Enum.IsDefined(typeof(ReplayEventKind),kind)||string.IsNullOrWhiteSpace(label)||label.Length>64||System.Text.Encoding.UTF8.GetByteCount(label)>128)
                throw new ArgumentException("Invalid replay annotation.");
            foreach(var c in label)if(!char.IsLetterOrDigit(c)&&c!=' '&&c!='.'&&c!=','&&c!='-'&&c!='_'&&c!='('&&c!=')'&&c!='\'')throw new ArgumentException("Replay labels must be plain display text.");
            if(label.IndexOf("www.",StringComparison.OrdinalIgnoreCase)>=0)throw new ArgumentException("Network references are not replay labels.");
            Seconds=seconds;Kind=kind;Label=label;
        }
        public double Seconds {get;}public ReplayEventKind Kind {get;}public string Label {get;}
    }
    /// <summary>Declarative playback only. It has no session, provider, or progression callbacks.</summary>
    public sealed class JourneyReplay
    {
        public const int MaximumSamples = 9000;
        public const int MaximumEvents = 256;
        public const int MaximumSeconds = 900;
        public JourneyReplay(ReplayIdentity identity, IReadOnlyList<ReplayPose> poses, ReplayPosition? courseAnchor = null, IReadOnlyList<ReplayEvent> events = null)
        {
            Identity = identity ?? throw new ArgumentNullException(nameof(identity));
            if(courseAnchor.HasValue&&identity.CourseId.Length==0)throw new ArgumentException("A course frame requires course identity.");
            CourseAnchor=courseAnchor;
            if (poses == null || poses.Count < 1 || poses.Count > MaximumSamples) throw new ArgumentException("Invalid pose count.");
            var copy = new ReplayPose[poses.Count];
            for (var i = 0; i < copy.Length; i++)
            {
                var pose = poses[i];
                copy[i] = new ReplayPose(pose.Seconds, pose.Position, pose.Rotation);
                if ((i == 0 && pose.Seconds != 0) || pose.Seconds > MaximumSeconds ||
                    (i > 0 && pose.Seconds-copy[i-1].Seconds < .099)) throw new ArgumentException("Invalid replay timing.");
            }
            Poses = Array.AsReadOnly(copy);
            var annotationCount=events?.Count??0;if(annotationCount>MaximumEvents)throw new ArgumentException("Too many replay annotations.");var annotations=new ReplayEvent[annotationCount];
            for(var i=0;i<annotations.Length;i++)
            {var e=events[i];annotations[i]=new ReplayEvent(e.Seconds,e.Kind,e.Label);if(e.Seconds>Duration||(i>0&&e.Seconds<annotations[i-1].Seconds))throw new ArgumentException("Annotation outside recorded interval or out of order.");}
            Events=Array.AsReadOnly(annotations);
        }
        public IReadOnlyList<ReplayEvent> Events {get;}
        public ReplayIdentity Identity { get; }
        public IReadOnlyList<ReplayPose> Poses { get; }
        public ReplayPosition? CourseAnchor {get;}
        public JourneyReplay WithCourseAnchor(ReplayPosition anchor)=>new(Identity,Poses,anchor,Events);
        public double Duration => Poses[Poses.Count-1].Seconds;
        public ReplayPose At(double seconds)
        {
            if (double.IsNaN(seconds) || double.IsInfinity(seconds)) throw new ArgumentException("Invalid playback time.");
            if (seconds <= 0) return Poses[0]; if (seconds >= Duration) return Poses[Poses.Count-1];
            var lo = 0; var hi = Poses.Count-1;
            while (hi-lo > 1) { var mid = (lo+hi)/2; if (Poses[mid].Seconds <= seconds) lo = mid; else hi = mid; }
            var a = Poses[lo]; var b = Poses[hi]; var gap = b.Seconds-a.Seconds;
            // Pauses/loading gaps are held, never turned into invented flight paths.
            if (gap > .25) return a;
            var t = (seconds-a.Seconds)/gap;
            return new ReplayPose(seconds,ReplayPosition.Lerp(a.Position,b.Position,t),Quaternion.Slerp(a.Rotation,b.Rotation,(float)t));
        }
    }
    public sealed class JourneyReplayBuffer
    {
        private readonly ReplayPose[] poses = new ReplayPose[JourneyReplay.MaximumSamples];
        private int start, count;
        private readonly ReplayEvent[] events=new ReplayEvent[JourneyReplay.MaximumEvents];private int eventStart,eventCount;
        private double last = -1, nextSample;
        private ReplayPose previous;
        public int Count => count;
        public void Clear() { eventStart=eventCount=0;start = count = 0; last = -1; nextSample = 0; }
        public bool Record(double seconds, ReplayPosition position, Quaternion rotation)
        {
            var pose = new ReplayPose(seconds,position,rotation);
            if (seconds < last) throw new ArgumentException("Replay clock moved backwards.");
            if (last < 0 || seconds-last > .25)
            { Append(pose); nextSample = seconds+.1; previous = pose; last = seconds; return true; }
            if (seconds == last) return false;
            var recorded = false;
            while (nextSample <= seconds+1e-8)
            {
                var t = Math.Max(0,Math.Min(1,(nextSample-last)/(seconds-last)));
                Append(new ReplayPose(nextSample,ReplayPosition.Lerp(previous.Position,position,t),
                    Quaternion.Slerp(previous.Rotation,rotation,(float)t)));
                nextSample += .1; recorded = true;
            }
            previous = pose; last = seconds; return recorded;
        }
        public void RecordEvent(ReplayEvent annotation)
        {
            annotation=new ReplayEvent(annotation.Seconds,annotation.Kind,annotation.Label);
            if(eventCount>0&&annotation.Seconds<events[(eventStart+eventCount-1)%events.Length].Seconds)throw new ArgumentException("Replay event clock moved backwards.");
            if(eventCount==events.Length){eventStart=(eventStart+1)%events.Length;eventCount--;}
            events[(eventStart+eventCount)%events.Length]=annotation;eventCount++;
        }
        private void Append(ReplayPose pose)
        {
            while (count > 0 && pose.Seconds-poses[start].Seconds > JourneyReplay.MaximumSeconds)
            { start = (start+1)%poses.Length; count--; }
            if (count == poses.Length) { start = (start+1)%poses.Length; count--; }
            poses[(start+count)%poses.Length] = pose; count++;
        }
        public JourneyReplay Capture(ReplayIdentity identity)
        {
            if (count == 0) throw new InvalidOperationException("No flight has been recorded yet.");
            var copy = new ReplayPose[count]; var beginning = poses[start].Seconds;
            for (var i = 0; i < count; i++) { var p = poses[(start+i)%poses.Length]; copy[i] = new ReplayPose(p.Seconds-beginning,p.Position,p.Rotation); }
            var annotations=new List<ReplayEvent>();var end=copy[copy.Length-1].Seconds+beginning;
            // Menus pause pose recording; events saved there belong at the last recorded pose.
            for(var i=0;i<eventCount;i++){var e=events[(eventStart+i)%events.Length];if(e.Seconds>=beginning)annotations.Add(new ReplayEvent(Math.Min(e.Seconds,end)-beginning,e.Kind,e.Label));}
            return new JourneyReplay(identity,copy,events:annotations);
        }
    }
}
