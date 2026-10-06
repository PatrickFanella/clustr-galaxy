using System;
using ClustrGalaxy.Bootstrap;
using ClustrGalaxy.Core.Domain;
using ClustrGalaxy.Core.Navigation;
using ClustrGalaxy.Core.Session;
using ClustrGalaxy.Data;
using ClustrGalaxy.Gameplay;
using ClustrGalaxy.Simulation;
using ClustrGalaxy.UI;
using ClustrGalaxy.World;
using UnityEngine;
namespace ClustrGalaxy.Flight
{
    public sealed class GalaxyJumpController : MonoBehaviour
    {
        private GalaxySessionBehaviour owner; private GalaxyNavigationController navigation;
        private ObserverProbeController flight; private Rigidbody body; private ProbeHud hud;
        private long loadingOperation = -1;
        private void Awake()
        { owner=GetComponent<GalaxySessionBehaviour>(); navigation=GetComponent<GalaxyNavigationController>(); flight=GetComponent<ObserverProbeController>(); body=GetComponent<Rigidbody>(); hud=GetComponent<ProbeHud>(); }
        public bool Active => owner.Session?.Travel?.State.Active == true && owner.Session.Travel.State.Action == TravelAction.Jump;
        public bool Begin(GalaxyEntity target)
            => target != null && Begin((GalaxyEntityId)target.StableId);
        public bool Begin(GalaxyEntityId target)
        {
            var session=owner.Session; if(session?.Travel==null || target.IsEmpty) return false;
            var travel=session.Travel;
            var result=travel.Apply(new TravelIntent(travel.State.SessionId,travel.NextOperationId,travel.State.Snapshot,TravelAction.Jump,target));
            if(!result.Committed){navigation.ReportTravelStatus(result.Reason);return false;}
            navigation.SetMapOpen(false); hud.SetMenuOpen(false); flight.ClearAutopilotDesiredVelocity(); return true;
        }
        public void Cancel()
        {
            var travel=owner.Session?.Travel; if(!Active)return;
            travel.Apply(new TravelIntent(travel.State.SessionId,travel.NextOperationId,travel.State.Snapshot,TravelAction.Cancel));
            navigation.ReportTravelStatus(travel.Status); hud.InvalidateExploration();
        }
        private void FixedUpdate()
        {
            if(!Active)return;
            var travel=owner.Session.Travel; travel.AdvanceCharge(Time.fixedDeltaTime);
            if(travel.State.Phase==TravelPhase.JumpTransit && loadingOperation!=travel.State.OperationId)
            { loadingOperation=travel.State.OperationId; Load(owner.Session, loadingOperation); }
        }
        private async void Load(GalaxySession session,long operation)
        {
            try
            {
                var preparation=await session.StageJumpAsync();
                if(this==null || owner.Session!=session || !Active || session.Travel.State.OperationId!=operation)return;
                if(preparation==null){session.Travel.Abort(operation,"JUMP LOAD FAILED · "+session.JumpError+" · RETRY AVAILABLE");return;}
                var destination=owner.Registry.FindEntity(preparation.Target.Value);
                Vector3 arrival, velocity;
                if(preparation.Receipt==null)
                {
                    var source=destination!=null?destination.GetComponent<GravitySource>():null;
                    if(source?.Definition==null)throw new InvalidOperationException("Authored destination has no physical frame.");
                    arrival=source.transform.position+Vector3.up*((float)source.Definition.ExclusionRadius+500);
                    velocity=source.CenterVelocity;
                    if(!ShipSurfaceSafety.IsClear(arrival))throw new InvalidOperationException("Destination arrival volume is occupied.");
                }
                else
                {
                    // Every live layout reserves the central stellar envelope. Arrival is on its polar side.
                    arrival=new DoubleVector3(0,ClustrGalaxy.Generation.GalaxyTuning.SunRadius*1.25+ClustrGalaxy.Generation.CelestialLayout.StellarClearance+500,0)
                        .RelativeTo(FloatingOrigin.Active!=null?FloatingOrigin.Active.LogicalOrigin:DoubleVector3.Zero);
                    velocity=Vector3.zero;
                }
                var outcome=session.CommitJump(preparation,500,0);
                if(!outcome.Succeeded){session.Travel.Abort(operation,"JUMP CANCELLED · DESTINATION RECEIPT EXPIRED");return;}
                var loader=FindAnyObjectByType<SpatialSceneLoader>();
                if(outcome.Scene!=null)loader.PresentCommittedJump(outcome);
                navigation.ArriveAtSystem(preparation.Target.Value);
                var arrivalTarget = owner.Registry.FindActiveEntity(preparation.Target.Value);
                var selection = GetComponent<ClustrGalaxy.Interaction.LiveGalaxySelectionController>();
                if (selection != null) selection.SelectArrivalTarget(arrivalTarget);
                else GetComponent<ClustrGalaxy.Interaction.ProbeTargeting>().SelectTarget(arrivalTarget);
                body.isKinematic=false;body.position=arrival;transform.position=arrival;
                body.linearVelocity=velocity;body.angularVelocity=Vector3.zero;
                flight.ModeState.SetFlightMode(FlightControlMode.PilotMastery);
                GetComponent<FloatingOrigin>()?.Rebase(-arrival);
                Physics.SyncTransforms();
                navigation.ReportTravelStatus("JUMP ARRIVED · LOCAL FLIGHT"); hud.InvalidateExploration();
            }
            catch(Exception error)
            {
                if(this!=null && owner.Session==session)
                { session.Travel.Abort(operation,"JUMP FAILED · "+error.Message); navigation.ReportTravelStatus("JUMP FAILED · "+error.Message);hud.InvalidateExploration(); }
            }
        }
    }
}
