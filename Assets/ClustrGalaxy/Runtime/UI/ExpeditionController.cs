using System;
using ClustrGalaxy.Bootstrap;
using ClustrGalaxy.Core.Activities;
using ClustrGalaxy.Interaction;
using UnityEngine;
using UnityEngine.Events;
namespace ClustrGalaxy.UI
{
    public sealed class ExpeditionController:MonoBehaviour
    {
        private GalaxySessionBehaviour owner;private ProbeHud hud;private ExpeditionRecord observed;private int residentCount=-1;
        private string message="Optional journeys · about fifteen minutes, at your own pace";
        private void Awake(){owner=GetComponent<GalaxySessionBehaviour>();hud=GetComponent<ProbeHud>();}
        private void FixedUpdate()
        {
            var session=owner.Session;var trip=session?.Activities?.State.Expedition;if(session==null)return;
            if(!ReferenceEquals(observed,trip)||residentCount!=session.Resident.LoadedEntities.Count)
            {observed=trip;residentCount=session.Resident.LoadedEntities.Count;session.SyncExpeditionReferences();hud.InvalidateExploration();}
            if(trip?.Status!=ExpeditionStatus.Active||trip.Plan.Mood==ExpeditionMood.PilotPractice||hud.InputCaptured)return;
            var id=trip.Plan.Stops[trip.Current];var body=owner.Registry.FindEntity(id.Value);if(body==null||!body.gameObject.activeInHierarchy)return;
            var radius=body.GetComponent<ClustrGalaxy.Simulation.GravitySource>()?.Definition?.ExclusionRadius??body.SurfaceRadius;
            var clearance=Vector3.Distance(transform.position,body.transform.position)-radius;
            var outcome=session.VisitExpedition(id,clearance);
            if(outcome!=null){message=outcome.Committed?"Stop visited. Continue when you feel ready.":outcome.Error;hud.InvalidateExploration();}
        }
        public void AddActions(Action<string,UnityAction> add)
        {
            var session=owner.Session;var trip=session?.Activities?.State.Expedition;if(session?.Activities==null)return;
            add(message,()=>{});
            if(trip==null||trip.Status==ExpeditionStatus.Completed||trip.Status==ExpeditionStatus.Abandoned)
            {
                add("EXPEDITION: QUIET DRIFT",()=>StartExpedition(ExpeditionMood.QuietDrift));
                add("EXPEDITION: UNEXPECTED CONNECTION",()=>StartExpedition(ExpeditionMood.UnexpectedConnection));
                add("EXPEDITION: PILOT PRACTICE",()=>StartExpedition(ExpeditionMood.PilotPractice));
                return;
            }
            add("JOURNEY: "+trip.Plan.Mood+" · "+trip.Current+" / "+trip.Plan.Count+" · "+trip.Status+(trip.Plan.Shorter?" · SHORTER: limited eligible loaded data":""),()=>{});
            if(session.ExpeditionError.Length!=0)add("RETRY EXPEDITION SAVE",()=>{var result=session.RetryExpedition();message=result?.Error??"";hud.InvalidateExploration();});
            if(trip.Status==ExpeditionStatus.Active)
            {
                add("PAUSE EXPEDITION",()=>Change(ActivityOperation.PauseExpedition));
                if(trip.Plan.Mood==ExpeditionMood.PilotPractice)
                    add("START NEXT PRACTICE CHALLENGE",()=>GetComponent<FlightActivitiesController>().StartPlayground(trip.Current==0?FlightActivityKind.RingRun:trip.Current==1?FlightActivityKind.FigureEight:FlightActivityKind.StableOrbit,false));
                else
                {
                    var target=owner.Registry.FindEntity(trip.Plan.Stops[trip.Current].Value);
                    var visible=target!=null&&target.gameObject.activeInHierarchy&&(session.Activities.State.Identity.Mode==ActivityWorldMode.Explore||session.Survey?.State.IsIdentified(trip.Plan.Stops[trip.Current])==true);
                    if(visible)add("SELECT NEXT SCENIC STOP: "+target.DisplayName,()=>{GetComponent<ProbeTargeting>().SelectTarget(target);hud.SetMenuOpen(false);});
                    else add("NEXT STOP UNAVAILABLE · load its scope or pause this journey",()=>{});
                }
            }
            if(trip.Status==ExpeditionStatus.Paused)add("RESUME EXPEDITION",()=>Change(ActivityOperation.ResumeExpedition));
            if(trip.Status==ExpeditionStatus.ReadyToClose)add("CLOSE JOURNEY AND SAVE JOURNAL CARD",()=>Change(ActivityOperation.CloseExpedition));
            add("ABANDON EXPEDITION",()=>Change(ActivityOperation.AbandonExpedition));
        }
        private void StartExpedition(ExpeditionMood mood)
        {try{var result=owner.Session.StartExpedition(mood);message=result.Committed?"Journey started. Nothing else begins automatically.":result.Error;}catch(Exception e){message=e.Message;}hud.InvalidateExploration();}
        private void Change(ActivityOperation operation)
        {var result=owner.Session.ChangeExpedition(operation);message=result.Committed?"Journey preference saved locally.":result.Error;hud.InvalidateExploration();}
    }
}
