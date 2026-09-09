using System;
using System.Collections.Generic;
using System.Numerics;
using ClustrGalaxy.Core.Navigation;

namespace ClustrGalaxy.Core.Activities
{
    public enum FlightActivityKind { Scenic, RingRun, FigureEight, StableOrbit }
    public sealed class FlightCourse
    {
        public FlightCourse(string id, string title, FlightActivityKind kind, IReadOnlyList<ReplayPosition> points, double radius)
        {
            if (!ReplayIdentity.Token(id,false) || string.IsNullOrWhiteSpace(title) || !Enum.IsDefined(typeof(FlightActivityKind),kind) ||
                points == null || points.Count < 2 || points.Count > 128 || double.IsNaN(radius) || double.IsInfinity(radius) || radius <= 0)
                throw new ArgumentException("Invalid flight course.");
            var copy = new ReplayPosition[points.Count]; for (var i=0;i<copy.Length;i++) copy[i]=points[i];
            Id=id; Title=title; Kind=kind; Points=Array.AsReadOnly(copy); Radius=radius;
        }
        public string Id { get; } public string Title { get; } public FlightActivityKind Kind { get; }
        public const int CurrentVersion = 2;
        public int Version => CurrentVersion; public IReadOnlyList<ReplayPosition> Points { get; } public double Radius { get; }
        public static double Distance(ReplayPosition a, ReplayPosition b) => Math.Sqrt((a.X-b.X)*(a.X-b.X)+(a.Y-b.Y)*(a.Y-b.Y)+(a.Z-b.Z)*(a.Z-b.Z));
        public static FlightCourse Scenic(int profile, ReplayPosition start, ReplayPosition end)
        {
            if (profile<0 || profile>2 || Distance(start,end)<100) throw new ArgumentException("Scenic route endpoints are too close or profile is invalid.");
            var dx=end.X-start.X;var dy=end.Y-start.Y;var dz=end.Z-start.Z;var length=Distance(start,end);
            var direction=Vector3.Normalize(new Vector3((float)(dx/length),(float)(dy/length),(float)(dz/length)));
            var side=Vector3.Normalize(Vector3.Cross(direction,Math.Abs(direction.Y)>.9f?Vector3.UnitX:Vector3.UnitY));
            var up=Vector3.Cross(side,direction);var points=new ReplayPosition[33];
            for(var i=0;i<points.Length;i++)
            {
                var t=i/32d;var envelope=Math.Sin(Math.PI*t);var amplitude=Math.Min(length*.12,700);
                var lateral=amplitude*envelope*(profile==0?1:Math.Sin(t*Math.PI*(profile==1?4:2)));
                var vertical=profile==2?amplitude*.4*envelope:profile==1?amplitude*.3*Math.Sin(t*Math.PI*2):0;
                points[i]=new ReplayPosition(start.X+dx*t+side.X*lateral+up.X*vertical,start.Y+dy*t+side.Y*lateral+up.Y*vertical,start.Z+dz*t+side.Z*lateral+up.Z*vertical);
            }
            var names=new[]{"Broad current","Winding passage","Edge-skimming ribbon"};
            return new FlightCourse("scenic-"+profile,names[profile],FlightActivityKind.Scenic,points,Math.Max(35,Math.Min(180,length/32*.7)));
        }
        public static FlightCourse Playground(FlightActivityKind kind, ReplayPosition anchor)
        {
            if(kind==FlightActivityKind.Scenic) throw new ArgumentException("Choose a playground.");
            var count=kind==FlightActivityKind.RingRun?8:kind==FlightActivityKind.FigureEight?16:32;
            var points=new ReplayPosition[count];
            for(var i=0;i<count;i++)
            {
                var a=2*Math.PI*i/count;
                points[i]=new ReplayPosition(anchor.X+Math.Sin(a)*(kind==FlightActivityKind.FigureEight?350:240),anchor.Y,
                    anchor.Z+(kind==FlightActivityKind.FigureEight?Math.Sin(2*a)*190:Math.Cos(a)*240));
            }
            return new FlightCourse(kind==FlightActivityKind.RingRun?"ring-run":kind==FlightActivityKind.FigureEight?"figure-eight":"stable-orbit",
                kind==FlightActivityKind.RingRun?"Eight-gate ring run":kind==FlightActivityKind.FigureEight?"Two-body figure eight":"Stable orbit",kind,points,45);
        }
    }
    public sealed class FlightRecord
    {
        public FlightRecord(string courseId,int version,NavigationFlightMode mode,double seconds,double accuracy,string tuningId,FlightGhost ghost=null)
        {
            if(!ReplayIdentity.Token(courseId,false)||version<1||mode==NavigationFlightMode.Autopilot||!Enum.IsDefined(typeof(NavigationFlightMode),mode)||
                double.IsNaN(seconds)||double.IsInfinity(seconds)||seconds<=0||seconds>86400||double.IsNaN(accuracy)||accuracy<0||accuracy>1||!ReplayIdentity.Token(tuningId,false))
                throw new ArgumentException("Invalid flight record.");
            if(ghost!=null&&(ghost.Replay.Identity.CourseId!=courseId||ghost.Replay.Identity.CourseVersion!=version||
                ghost.Replay.Identity.FlightMode!=mode||ghost.Replay.Identity.TuningId!=tuningId||seconds>900||
                Math.Abs(ghost.Replay.Duration-seconds)>.15))throw new ArgumentException("Ghost does not match the full record.");
            Ghost=ghost;
            CourseId=courseId;Version=version;Mode=mode;Seconds=seconds;Accuracy=accuracy;TuningId=tuningId;
        }
        public FlightGhost Ghost {get;}
        public string CourseId {get;} public int Version {get;} public NavigationFlightMode Mode {get;}
        public double Seconds {get;} public double Accuracy {get;} public string TuningId {get;}
        public string Key => CourseId+":"+Version+":"+Mode+":"+TuningId;
        public bool Improves(FlightRecord previous) => previous==null || Accuracy>previous.Accuracy+.0001 ||
            (Math.Abs(Accuracy-previous.Accuracy)<.0001 && Seconds<previous.Seconds);
    }
    /// <summary>Session-owned transient course evaluator. It never grants Survey awards.</summary>
    public sealed class FlightActivityRun
    {
        private double validSeconds, stableSeconds, gateAccuracy;
        public JourneyReplayBuffer Recording {get;}=new();
        private ReplayPosition previous;
        private bool hasPrevious;
        public FlightActivityRun(FlightCourse course,NavigationFlightMode mode,bool preview,string tuningId,ActivityIdentity? identity=null,bool saveBestGhost=false)
        {
            Course=course??throw new ArgumentNullException(nameof(course));
            if(!Enum.IsDefined(typeof(NavigationFlightMode),mode)||!ReplayIdentity.Token(tuningId,false))throw new ArgumentException("Invalid flight mode/tuning.");
            Mode=mode;Preview=preview||mode==NavigationFlightMode.Autopilot;TuningId=tuningId;Identity=identity;SaveBestGhost=saveBestGhost;
            Id=Guid.NewGuid().ToString("N");
        }
        public bool SaveBestGhost {get;}
        public FlightGhost BestGhost {get;private set;}
        public void AttachGhost(FlightGhost ghost){BestGhost=ghost?.Compatible(this)==true?ghost:null;}
        public ActivityIdentity? Identity {get;}
        public string Id {get;} public FlightCourse Course {get;} public NavigationFlightMode Mode {get;} public bool Preview {get;} public string TuningId {get;}
        public int Checkpoint {get;private set;} public double Elapsed {get;private set;}
        public double StableSeconds=>stableSeconds; public bool Completed {get;private set;} public bool Cancelled {get;private set;}
        public bool Active=>!Completed&&!Cancelled;
        public ReplayPosition Target=>Course.Points[Math.Min(Checkpoint,Course.Points.Count-1)];
        public void Cancel(){Cancelled=true;}
        public void Advance(ReplayPosition position,double delta,NavigationFlightMode mode,Quaternion? rotation=null)
        {
            if(double.IsNaN(delta)||double.IsInfinity(delta)||delta<=0||delta>.25)throw new ArgumentException("Invalid flight sample interval.");
            if(!Active)return;if(mode!=Mode){Cancel();return;}
            if(!hasPrevious)Recording.Record(0,position,rotation??Quaternion.Identity);
            Elapsed+=delta;
            Recording.Record(Elapsed,position,rotation??Quaternion.Identity);
            var distance=FlightCourse.Distance(position,Target);
            if(Course.Kind==FlightActivityKind.StableOrbit&&!Preview)
            {
                // Ring centre is stable and authored, independent of floating-origin presentation.
                var first=Course.Points[0];var centre=new ReplayPosition(first.X,first.Y,first.Z-240);
                var radial=FlightCourse.Distance(position,centre);
                var speed=hasPrevious?FlightCourse.Distance(previous,position)/delta:0;
                var radialMotion=hasPrevious&&speed>0?Math.Abs((position.X-previous.X)*(position.X-centre.X)+(position.Z-previous.Z)*(position.Z-centre.Z))/(delta*speed*Math.Max(1,radial)):1;
                var stable=radialMotion<.5 && Math.Abs(radial-240)<=45 && Math.Abs(position.Y-centre.Y)<=45 && speed>=5 && speed<=110;
                if(stable){stableSeconds+=delta;validSeconds+=delta;}else stableSeconds=0;
                if(stableSeconds>=20)Completed=true;
            }
            else
            {
                if(distance<=Course.Radius){validSeconds+=delta;gateAccuracy+=1-distance/Course.Radius;Checkpoint++;if(Checkpoint>=Course.Points.Count)Completed=true;}
                // Missing a playground gate keeps the next gate selected. Scenic guidance ends gently on departure.
                if(Course.Kind==FlightActivityKind.Scenic&&hasPrevious&&Checkpoint>0&&!Completed)
                {
                    var a=Course.Points[Checkpoint-1]; var b=Target;
                    var dx=b.X-a.X;var dy=b.Y-a.Y;var dz=b.Z-a.Z;
                    var lengthSquared=dx*dx+dy*dy+dz*dz;
                    var t=lengthSquared>0?Math.Max(0,Math.Min(1,((position.X-a.X)*dx+(position.Y-a.Y)*dy+(position.Z-a.Z)*dz)/lengthSquared)):0;
                    var nearest=new ReplayPosition(a.X+t*dx,a.Y+t*dy,a.Z+t*dz);
                    if(FlightCourse.Distance(position,nearest)>Course.Radius*8)Cancel();
                }
            }
            previous=position;hasPrevious=true;
        }
        public FlightRecord Result()
        {
            if(!Completed||Preview||Course.Kind==FlightActivityKind.Scenic)throw new InvalidOperationException("No scored result.");
            FlightGhost ghost=null;
            if(SaveBestGhost&&Identity.HasValue&&Elapsed<=900)
            {
                var id=Identity.Value;
                ghost=new FlightGhost(Recording.Capture(new ReplayIdentity(id.Snapshot,id.Mode,1,TuningId,Mode,Course.Id,Course.Version)),Course.Points[0]);
            }
            return new FlightRecord(Course.Id,Course.Version,Mode,Elapsed,
                Course.Kind==FlightActivityKind.StableOrbit?Math.Min(1,validSeconds/Elapsed):gateAccuracy/Course.Points.Count,TuningId,ghost);
        }
    }
}
