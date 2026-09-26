using System;
using System.Linq;
using System.IO;
using ClustrGalaxy.Persistence;
using ClustrGalaxy.Bootstrap;
using ClustrGalaxy.Core.Activities;
using ClustrGalaxy.Core.Domain;
using ClustrGalaxy.Core.Navigation;
using ClustrGalaxy.Flight;
using ClustrGalaxy.Simulation;
using ClustrGalaxy.Gameplay;
using ClustrGalaxy.World;
using UnityEngine;
using UnityEngine.UI;

namespace ClustrGalaxy.UI
{
    public sealed class FlightActivitiesController : MonoBehaviour
    {
        private GalaxySessionBehaviour owner;
        private ObserverProbeController flight;
        private FloatingOrigin origin;
        private ProbeHud hud;
        private LineRenderer path,gate,ghost;
        private bool showGhost=true,initialGravity;
        public bool GhostVisible=>showGhost;
        public bool GhostDisplayed=>ghost!=null&&ghost.gameObject.activeInHierarchy;
        public void ToggleGhost(){showGhost=!showGhost;hud.InvalidateExploration();}
        private Material material,ghostMaterial;
        private float initialFixedStep;
        private readonly Transform[] trainingBodies=new Transform[2];
        public Vector3 GhostPosition {get;private set;}
        private GameObject timerPanel;
        private Text timerTitle;
        private readonly Text[] timerDigits=new Text[6];
        private static readonly string[] Digits={"0","1","2","3","4","5","6","7","8","9"};
        private int lastTenths=-1;
        private GameObject visuals;
        private FlightActivityRun observed;
        private int observedCheckpoint=-1;
        private bool observedActive;
        public string LastGhostPath {get;private set;}
        public void SaveGhost()
        {
            try
            {
                var run=owner.Session.FlightActivity;
                if(run==null||!run.Completed||run.Preview)throw new InvalidOperationException("Complete a manual course before saving its recording.");
                var snapshot=run.Identity??throw new InvalidOperationException("Recording identity unavailable.");
                var replay=run.Recording.Capture(new ReplayIdentity(snapshot.Snapshot,snapshot.Mode,1,run.TuningId,run.Mode,run.Course.Id,run.Course.Version));
                if(run.Elapsed<=JourneyReplay.MaximumSeconds)replay=replay.WithCourseAnchor(run.Course.Points[0]);
                var folder=Path.Combine(Application.persistentDataPath,"ClustrGalaxy","ghosts");Directory.CreateDirectory(folder);
                LastGhostPath=Path.Combine(folder,DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ", System.Globalization.CultureInfo.InvariantCulture)+"-"+Guid.NewGuid().ToString("N")+".clustr-replay");
                ReplayPackage.Save(LastGhostPath,replay);Status="Flight recording saved locally";
            }
            catch(Exception e){Status="Could not save flight recording. Retry: "+e.Message;}
            hud.InvalidateExploration();
        }
        public void ViewGhost()
        {
            try
            {
                var folder=Path.Combine(Application.persistentDataPath,"ClustrGalaxy","ghosts");
                var file=Directory.Exists(folder)?Directory.GetFiles(folder,"*.clustr-replay").OrderBy(x=>x,StringComparer.Ordinal).LastOrDefault():null;
                if(file==null)throw new InvalidOperationException("No saved flight recording yet.");
                using var stream=File.OpenRead(file);var replay=ReplayPackage.Read(stream);
                (GetComponent<JourneyReplayViewer>()??gameObject.AddComponent<JourneyReplayViewer>()).Open(replay);
            }
            catch(Exception e){Status=e.Message;hud.InvalidateExploration();}
        }
        public string Status {get;private set;}="Optional flight activities · no penalty for leaving";
        private void Awake()
        {
            owner=GetComponent<GalaxySessionBehaviour>();flight=GetComponent<ObserverProbeController>();origin=GetComponent<FloatingOrigin>();hud=GetComponent<ProbeHud>();
            GetComponent<GalaxyNavigationController>().BindActivities(this);
            timerPanel=new GameObject("Flight timer",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler));timerPanel.transform.SetParent(transform,false);
            timerPanel.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;timerPanel.GetComponent<Canvas>().sortingOrder=4;
            var scale=timerPanel.GetComponent<CanvasScaler>();scale.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scale.referenceResolution=new Vector2(1280,720);scale.matchWidthOrHeight=1;
            var title=timerTitle=ConstellationDiagram.MakeText(timerPanel.transform,"FLIGHT SECONDS",16);title.rectTransform.anchorMin=title.rectTransform.anchorMax=new Vector2(1,1);title.rectTransform.anchoredPosition=new Vector2(-240,-30);title.rectTransform.sizeDelta=new Vector2(430,30);
            for(var i=0;i<6;i++)
            {var label=timerDigits[i]=ConstellationDiagram.MakeText(timerPanel.transform,i==4?".":"0",24);label.rectTransform.anchorMin=label.rectTransform.anchorMax=new Vector2(1,1);label.rectTransform.anchoredPosition=new Vector2(-260+i*20,-60);label.rectTransform.sizeDelta=new Vector2(20,35);}
            timerPanel.SetActive(false);
            var shader=Shader.Find("Universal Render Pipeline/Lit")??Shader.Find("Standard")??Shader.Find("Sprites/Default");
            if(shader==null)return;
            material=new Material(shader);material.color=new Color(.3f,.85f,.9f);material.EnableKeyword("_EMISSION");
            if(material.HasProperty("_EmissionColor"))material.SetColor("_EmissionColor",new Color(.1f,.4f,.45f));
            visuals=new GameObject("Optional authored flight course");
            path=MakeLine("Course",2);gate=MakeLine("Next gate",4);gate.loop=true;gate.positionCount=32;
            ghost=MakeLine("Personal best — local recording",3);ghost.positionCount=9;ghost.startColor=ghost.endColor=new Color(1,.75f,.25f);
            ghostMaterial=new Material(material);ghostMaterial.color=new Color(1,.75f,.25f);
            if(ghostMaterial.HasProperty("_EmissionColor"))ghostMaterial.SetColor("_EmissionColor",new Color(.7f,.4f,.05f));ghost.sharedMaterial=ghostMaterial;
            ghost.gameObject.SetActive(false);
            for(var i=0;i<trainingBodies.Length;i++)
            {
                var body=GameObject.CreatePrimitive(PrimitiveType.Sphere);body.name="Authored training body "+(i+1);body.SetActive(false);
                Destroy(body.GetComponent<Collider>());body.transform.SetParent(visuals.transform,false);body.transform.localScale=Vector3.one*60;
                body.GetComponent<Renderer>().sharedMaterial=material;trainingBodies[i]=body.transform;
            }
        }
        private LineRenderer MakeLine(string name,float width)
        {
            var go=new GameObject(name);go.transform.SetParent(visuals.transform,false);var line=go.AddComponent<LineRenderer>();
            line.sharedMaterial=material;line.useWorldSpace=false;line.startWidth=line.endWidth=width;return line;
        }
        private ReplayPosition Logical(Vector3 p)
        {var o=origin==null?DoubleVector3.Zero:origin.LogicalOrigin;return new ReplayPosition(o.X+p.x,o.Y+p.y,o.Z+p.z);}
        private Vector3 Local(ReplayPosition p)
        {var o=origin==null?DoubleVector3.Zero:origin.LogicalOrigin;return new Vector3((float)(p.X-o.X),(float)(p.Y-o.Y),(float)(p.Z-o.Z));}
        public void StartPlayground(FlightActivityKind kind,bool preview)
        {
            try
            {
                FlightCourse course = null;
                for (var attempt = 0; attempt < 32; attempt++)
                {
                    var anchor = Logical(transform.position + transform.forward * 350 + Vector3.up * (attempt * 500));
                    var candidate = FlightCourse.Playground(kind, anchor);
                    if (CourseIsClear(candidate)) { course = candidate; break; }
                }
                if (course == null) throw new InvalidOperationException("No safe practice space here. Move away from nearby bodies and retry.");
                StartCourse(course,preview,Array.Empty<GalaxyEntityId>());
            }
            catch(Exception e){Status=e.Message;hud.InvalidateExploration();}
        }
        public void StartScenic(GalaxyRouteId id,int profile)
        {
            try
            {
                var route=owner.Session.Resident.LoadedRoutes.FirstOrDefault(x=>x.Id==id);
                if(route==null)throw new InvalidOperationException("Route is no longer resident.");
                var a=owner.Registry.FindEntity(route.SourceId.Value);var b=owner.Registry.FindEntity(route.TargetId.Value);
                if(a==null||b==null)throw new InvalidOperationException("Both route endpoints must be resident.");
                // Offset presentation lanes away from physical body centres; source endpoints/weights stay untouched.
                FlightCourse course = null;
                var aHeight = (float)(a.GetComponent<GravitySource>()?.Definition?.ExclusionRadius ?? a.SurfaceRadius) + 600;
                var bHeight = (float)(b.GetComponent<GravitySource>()?.Definition?.ExclusionRadius ?? b.SurfaceRadius) + 600;
                for (var attempt = 0; attempt < 32; attempt++)
                {
                    var candidate = FlightCourse.Scenic(profile, Logical(a.transform.position + Vector3.up * (aHeight + attempt * 500)),
                        Logical(b.transform.position + Vector3.up * (bHeight + attempt * 500)));
                    if (CourseIsClear(candidate)) { course = candidate; break; }
                }
                if (course == null) throw new InvalidOperationException("This route has no clear scenic corridor right now. Try another route.");
                StartCourse(course,false,new[]{route.SourceId,route.TargetId},id);
            }
            catch(Exception e){Status=e.Message;hud.InvalidateExploration();}
        }
        private bool CourseIsClear(FlightCourse course)
        {
            var sources = GravitySource.ActiveSources;
            for (var i = 0; i < course.Points.Count; i++)
            {
                var from = Local(course.Points[i]); var to = Local(course.Points[(i + 1) % course.Points.Count]);
                // Scenic paths are open; playgrounds close into a loop.
                if (i == course.Points.Count - 1 && course.Kind == FlightActivityKind.Scenic) to = from;
                for (var n = 0; n < sources.Count; n++)
                {
                    var source = sources[n]; if (source?.Definition == null) continue;
                    var centre = source.transform.position; var start = from - centre; var end = to - centre;
                    if (SurfaceIntersection.Sweep(new System.Numerics.Vector3(start.x,start.y,start.z),
                        new System.Numerics.Vector3(end.x,end.y,end.z), source.Definition.ExclusionRadius + course.Radius + 100, out _)) return false;
                }
            }
            return true;
        }
        private void StartCourse(FlightCourse course,bool preview,GalaxyEntityId[] anchors,GalaxyRouteId route=default)
        {
            if (flight.Simulation.Recovering || owner.Session.Travel.State.Active)
                throw new InvalidOperationException("Resume local flight before starting an activity.");
            if(preview)owner.Modes.SetFlightMode(FlightControlMode.Autopilot);
            owner.Session.StartFlightActivity(course,(NavigationFlightMode)(int)owner.Modes.FlightMode,preview,flight.ReplayTuningIdentity(),anchors,route);
            initialGravity=flight.GravityModeEnabled;initialFixedStep=Time.fixedDeltaTime;
            Status=(owner.Session.FlightActivity.Preview?"Unscored preview · ":"Started · ")+course.Title;hud.InvalidateExploration();
        }
        public void Cancel(){owner.Session.CancelFlightActivity();flight.ClearAutopilotDesiredVelocity();Status="Guidance ended · free flight";hud.InvalidateExploration();}
        public void RetrySave(){var outcome=owner.Session.CommitFlightActivity();if(outcome?.Committed==true)Celebrate();Status=owner.Session.FlightError.Length==0?"Flight record saved locally":owner.Session.FlightError;hud.InvalidateExploration();}
        public bool GuidePhysics()
        {
            var run=owner.Session?.FlightActivity;if(run==null||!run.Active)return false;
            if(initialGravity!=flight.GravityModeEnabled||initialFixedStep!=Time.fixedDeltaTime){Cancel();Status="Flight settings changed · start again for a comparable record";return true;}
            if (flight.Simulation.Recovering || owner.Session.Travel.State.Active) { Cancel(); return true; }
            if (!ShipSurfaceSafety.IsClear(Local(run.Target))) { Cancel(); Status = "A body has entered the course · guidance ended safely"; return true; }
            var mode=(NavigationFlightMode)(int)owner.Modes.FlightMode;
            var q=transform.rotation;
            owner.Session.AdvanceFlightActivity(Logical(transform.position),Time.fixedDeltaTime,mode,new System.Numerics.Quaternion(q.x,q.y,q.z,q.w));
            if(run.Active&&mode==NavigationFlightMode.Autopilot)
            {
                var delta=Local(run.Target)-transform.position;var distance=delta.magnitude;
                flight.SetAutopilotDesiredVelocity(distance>.01f?delta/distance*Mathf.Min(80,Mathf.Max(12,distance*1.5f)):Vector3.zero);
            }
            else flight.ClearAutopilotDesiredVelocity();
            return true;
        }
        private void LateUpdate()
        {
            var run=owner.Session?.FlightActivity;
            if(!ReferenceEquals(observed,run)||observedCheckpoint!=(run?.Checkpoint??-1)||observedActive!=(run?.Active??false))
            {
                observed=run;observedCheckpoint=run?.Checkpoint??-1;observedActive=run?.Active??false;
                timerTitle.text=run?.Course.Kind==FlightActivityKind.StableOrbit&&!run.Preview?"STABLE ORBIT SECONDS · REACH 20":"FLIGHT SECONDS";
                if(run!=null)Status=run.Course.Title+" · "+(run.Cancelled?"Guidance ended":run.Completed?(run.Preview?"Preview complete":"Complete · "+run.Elapsed.ToString("F1")+"s"):
                    run.Course.Kind==FlightActivityKind.StableOrbit&&!run.Preview?"Stay on the ring, keep moving for 20 continuous seconds":"Next gate "+(run.Checkpoint+1)+" / "+run.Course.Points.Count)+
                    (owner.Session.FlightError.Length==0?"":" · "+owner.Session.FlightError);
                if(run?.Completed==true&&!run.Preview&&owner.Session.FlightError.Length==0)Celebrate();
                hud.InvalidateExploration();
                if(path!=null)path.positionCount=run?.Course.Points.Count??0;
            }
            var timerVisible=run!=null&&!hud.InputCaptured;
            if(timerPanel.activeSelf!=timerVisible)timerPanel.SetActive(timerVisible);
            if(run!=null)
            {
                var tenths=(int)Math.Min(99999,(run.Course.Kind==FlightActivityKind.StableOrbit&&!run.Preview?run.StableSeconds:run.Elapsed)*10);
                if(tenths!=lastTenths){lastTenths=tenths;for(var i=5;i>=0;i--){if(i==4)continue;var digit=tenths%10;tenths/=10;if(timerDigits[i].text!=Digits[digit])timerDigits[i].text=Digits[digit];}}
            }
            if(visuals==null)return;
            if(visuals.activeSelf!=(run?.Active==true))visuals.SetActive(run?.Active==true);
            if(run==null||!run.Active)return;
            visuals.transform.position=Vector3.zero;
            var figure=run.Course.Kind==FlightActivityKind.FigureEight;var orbit=run.Course.Kind==FlightActivityKind.StableOrbit;
            for(var i=0;i<trainingBodies.Length;i++)
            {
                var active=figure||(orbit&&i==0);if(trainingBodies[i].gameObject.activeSelf!=active)trainingBodies[i].gameObject.SetActive(active);
                if(active)trainingBodies[i].position=Local(run.Course.Points[0])+(figure?new Vector3(i==0?-175:175,0,0):new Vector3(0,0,-240));
            }
            var show=showGhost&&run.BestGhost!=null&&run.Elapsed<=run.BestGhost.Replay.Duration&&owner.Session.Activities.State.Preferences.ReducedMotion==false;
            if(ghost.gameObject.activeSelf!=show)ghost.gameObject.SetActive(show);
            if(show)
            {
                var pose=run.BestGhost.At(run.Elapsed,run.Course.Points[0]);var centre=GhostPosition=Local(pose.Position);
                var q=pose.Rotation;var rotation=new Quaternion(q.X,q.Y,q.Z,q.W);
                for(var i=0;i<9;i++)
                {
                    var point=(i%4) switch{0=>new Vector3(0,0,12),1=>new Vector3(-8,0,-8),2=>new Vector3(0,7,-4),_=>new Vector3(8,0,-8)};
                    ghost.SetPosition(i,centre+rotation*point);
                }
            }
            for(var i=0;i<run.Course.Points.Count;i++)path.SetPosition(i,Local(run.Course.Points[i]));
            var target=Local(run.Target);var r=(float)run.Course.Radius;
            for(var i=0;i<32;i++){var a=i*Mathf.PI/16;gate.SetPosition(i,target+new Vector3(Mathf.Cos(a)*r,Mathf.Sin(a)*r,0));}
        }
        private void Celebrate()
        {
            var run=owner.Session.FlightActivity;if(run==null||run.Course.Kind==FlightActivityKind.Scenic)return;
            GetComponent<ExplorationCelebrations>()?.Show(new CelebrationCue("flight:"+run.Id,CelebrationKind.FlightRibbon,Array.Empty<GalaxyEntityId>()));
        }
        private void OnDestroy(){if(visuals!=null)Destroy(visuals);if(material!=null)Destroy(material);if(ghostMaterial!=null)Destroy(ghostMaterial);}
    }
}
