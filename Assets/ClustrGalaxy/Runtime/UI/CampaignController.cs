using System;
using System.Linq;
using ClustrGalaxy.Bootstrap;
using ClustrGalaxy.Core.Activities;
using ClustrGalaxy.Interaction;
using UnityEngine;
using UnityEngine.Events;
namespace ClustrGalaxy.UI
{
    public sealed class CampaignController:MonoBehaviour
    {
        private GalaxySessionBehaviour owner;
        private ProbeHud hud;
        private ActivityState observed;
        private readonly Transform[] anomalies=new Transform[6];
        private Material material;private readonly float[] nextCue=new float[6];private ExplorationFeedback feedback;
        private string status="Demo fiction · look toward nearby bodies for three seconds to notice a clue";
        private void Awake()
        {
            owner=GetComponent<GalaxySessionBehaviour>();hud=GetComponent<ProbeHud>();feedback=GetComponent<ExplorationFeedback>();
            var shader=Shader.Find("Universal Render Pipeline/Lit");if(shader==null)return;
            material=new Material(shader);material.color=new Color(.8f,.65f,1);material.EnableKeyword("_EMISSION");material.SetColor("_EmissionColor",new Color(.4f,.2f,.6f));
            for(var i=0;i<6;i++)
            {
                var go=GameObject.CreatePrimitive(i==3?PrimitiveType.Cube:i==5?PrimitiveType.Cylinder:PrimitiveType.Sphere);
                go.name="Authored anomaly "+i;Destroy(go.GetComponent<Collider>());go.GetComponent<Renderer>().sharedMaterial=material;
                anomalies[i]=go.transform;go.SetActive(false);
            }
        }
        private readonly AuthoredAnomalyVisual[] animations=new AuthoredAnomalyVisual[6];
        public void Initialize(ClustrGalaxy.World.GalaxyVisualCatalog catalog)
        {
            if(catalog?.anomalyPrefabs==null)return;
            for(var i=0;i<6&&i<catalog.anomalyPrefabs.Length;i++)
            {
                if(catalog.anomalyPrefabs[i]==null)continue;
                if(anomalies[i]!=null){anomalies[i].gameObject.SetActive(false);Destroy(anomalies[i].gameObject);}
                var go=Instantiate(catalog.anomalyPrefabs[i]);go.name="Authored anomaly "+i;
                anomalies[i]=go.transform;animations[i]=go.AddComponent<AuthoredAnomalyVisual>();animations[i].Initialize(i);go.SetActive(false);
            }
        }
        private void FixedUpdate()
        {
            var session=owner.Session;var activity=session?.Activities;if(activity==null)return;
            if(!ReferenceEquals(observed,activity.State)){observed=activity.State;SyncReferences();hud.InvalidateExploration();}
            if (hud.InputCaptured || session.Travel?.State.Active == true || session.Flight.Recovering)
            { session.Campaign.ResetObservation(); return; }
            var discovery=session.Campaign;
            for(var i=0;i<CampaignContent.Observations.Count;i++)
            {
                var clue=CampaignContent.Observations[i];var body=owner.Registry.FindEntity(clue.Anchor.Value);
                var visible=activity.State.Identity.Snapshot.StorageNamespace=="demo"&&body!=null&&body.gameObject.activeInHierarchy&&
                    (activity.State.Identity.Mode==ActivityWorldMode.Explore||session.Survey?.State.IsIdentified(clue.Anchor)==true);
                var delta=body==null?Vector3.zero:body.transform.position-transform.position;
                var distance = body == null ? double.NaN : CampaignPlacement.Clearance(delta.magnitude, SafetyRadius(body));
                var anomaly = AnomalyIndex(clue);
                if (body != null && anomaly >= 0)
                {
                    delta = ObservationPoint(clue) - transform.position;
                    // Both proximity to the visible anomaly and clearance from its parent are required.
                    distance = distance < 0 ? double.NaN : delta.magnitude;
                }
                var result=discovery.Advance(i,visible,distance,Vector3.Angle(transform.forward,delta),Time.fixedDeltaTime);
                if(result!=null){status=result.Committed?"Discovered: "+clue.Title+" · Open the campaign notebook":result.Error;hud.InvalidateExploration();}
            }
        }
        public static double SafetyRadius(ClustrGalaxy.World.GalaxyEntity body)
        {
            var gravity = body.GetComponent<ClustrGalaxy.Simulation.GravitySource>();
            return gravity?.Definition?.ExclusionRadius ?? body.SurfaceRadius;
        }
        private static int AnomalyIndex(CampaignObservation clue)
        {
            var index = 0;
            for (var i = 0; i < CampaignContent.Observations.Count; i++)
            {
                var candidate = CampaignContent.Observations[i];
                if (!candidate.ActivityId.StartsWith("anomaly-", StringComparison.Ordinal)) continue;
                if (ReferenceEquals(candidate, clue)) return index;
                index++;
            }
            return -1;
        }
        public Vector3 ObservationPoint(CampaignObservation clue)
        {
            var body = owner.Registry.FindEntity(clue.Anchor.Value);
            if (body == null) throw new InvalidOperationException("Observation anchor unavailable.");
            var index = AnomalyIndex(clue);
            if (index < 0) return body.transform.position;
            var offset = CampaignPlacement.AnomalyOffset(SafetyRadius(body), index, Time.timeAsDouble,
                owner.Session.Activities.State.Preferences.ReducedMotion);
            return body.transform.position + new Vector3(offset.X, offset.Y, offset.Z);
        }
        private void SyncReferences()
        {
            owner.Session.ReleaseActivityEntities("campaign");
            if(observed.FollowedTrail.Length==0)return;
            var record=observed.Find(observed.FollowedTrail);var definition=AuthoredActivityCatalog.Definitions.First(x=>x.Id==record.Id);
            var clue=CampaignContent.Find(record.Id,definition.Observations[record.Observations.Count]);
            // Unavailable/unknown next stops never force provider loads or reveal Survey content.
            try{owner.Session.RetainActivityEntities("campaign",new[]{clue.Anchor});}catch(InvalidOperationException){}
        }
        private void LateUpdate()
        {
            var state=owner.Session?.Activities?.State;var index=0;
            for(var clueIndex=0;clueIndex<CampaignContent.Observations.Count;clueIndex++)
            {
                var clue=CampaignContent.Observations[clueIndex];
                if(!clue.ActivityId.StartsWith("anomaly-",StringComparison.Ordinal))continue;
                var visual=anomalies[index];var body=owner.Registry.FindEntity(clue.Anchor.Value);
                var visible=state?.Identity.Snapshot.StorageNamespace=="demo"&&body!=null&&body.gameObject.activeInHierarchy&&
                    (state.Identity.Mode==ActivityWorldMode.Explore||owner.Session.Survey?.State.IsIdentified(clue.Anchor)==true);
                if(visual!=null)
                {
                    if(visual.gameObject.activeSelf!=visible)visual.gameObject.SetActive(visible);
                    if(visible)
                    {
                        visual.position=ObservationPoint(clue);
                        visual.localScale=Vector3.one*14;visual.rotation=Quaternion.identity;
                        if(!hud.InputCaptured&&(visual.position-transform.position).sqrMagnitude<302500&&Time.unscaledTime>=nextCue[index])
                        {
                            nextCue[index]=Time.unscaledTime+8+index;animations[index]?.Pulse(Time.timeAsDouble,state.Preferences.CelebrationIntensity);
                            if(feedback==null)feedback=GetComponent<ExplorationFeedback>();feedback?.PlayAuthoredAnomaly(clue.ActivityId,visual);
                        }
                        if(animations[index]!=null)animations[index].Present(Time.timeAsDouble,
                            Vector3.Angle(transform.forward,visual.position-transform.position)<25,state.Preferences.ReducedMotion);
                    }
                }
                index++;
            }
        }
        public void AddActions(Action<string,UnityAction> add)
        {
            var activity=owner.Session?.Activities;if(activity==null)return;
            add("OPEN CAMPAIGN NOTEBOOK",()=> this.GetOrAddComponent<CampaignNotebook>().Open());
            if(activity.State.Identity.Snapshot.StorageNamespace!="demo")return;
            add(status,()=>{});
            if(owner.Session.Campaign.Error.Length!=0)add("RETRY CLUE SAVE",()=>{var result=owner.Session.Campaign.Retry();status=result?.Error??"";hud.InvalidateExploration();});
            foreach(var definition in AuthoredActivityCatalog.Definitions)
            {
                var record=activity.State.Find(definition.Id);if(record==null)continue;
                if(definition.Kind==ActivityKind.Trail&&!record.Completed)
                {
                    var id=definition.Id;
                    add("FOLLOW: "+definition.Title,()=>Apply(ActivityOperation.FollowTrail,id));
                }
            }
            if(activity.State.FollowedTrail.Length!=0)add("UNPIN CURIOSITY TRAIL",()=>Apply(ActivityOperation.UnpinTrail));
        }
        private void Apply(ActivityOperation operation,string id="")
        {var a=owner.Session.Activities;var result=a.Apply(new ActivityIntent(Guid.NewGuid().ToString("N"),a.State.Identity,operation,DateTime.UtcNow,id));status=result.Committed?"Trail preference saved":result.Error;hud.InvalidateExploration();}
        private void OnDestroy(){foreach(var item in anomalies)if(item!=null)Destroy(item.gameObject);if(material!=null)Destroy(material);}
    }
}
