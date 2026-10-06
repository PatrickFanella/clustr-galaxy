using System;
using System.IO;
using ClustrGalaxy.Bootstrap;
using ClustrGalaxy.Core.Activities;
using ClustrGalaxy.Core.Navigation;
using ClustrGalaxy.Persistence;
using ClustrGalaxy.World;
using UnityEngine;

namespace ClustrGalaxy.UI
{
    /// <summary>Copies presentation poses into the session-owned bounded recorder. Saving is explicit.</summary>
    public sealed class JourneyRecorder : MonoBehaviour
    {
        private GalaxySessionBehaviour owner;
        private FloatingOrigin origin;
        private ProbeHud hud;
        private ActivityIdentity? observed;
        private NavigationFlightMode flightMode;
        private bool gravityMode; private string tuningId;
        private ClustrGalaxy.Flight.ObserverProbeController flight;
        private double started;private ActivitySession subscribed;private ActivityState observedState;
        public string Status { get; private set; } = "Rolling flight diary · up to 15 minutes · local only";
        public string LastSavedPath { get; private set; }
        private void Awake() { owner = GetComponent<GalaxySessionBehaviour>(); origin = GetComponent<FloatingOrigin>(); hud = GetComponent<ProbeHud>(); flight=GetComponent<ClustrGalaxy.Flight.ObserverProbeController>(); }
        private void LateUpdate()
        {
            var activity = owner?.Session?.Activities; if (activity == null) return;
            var identity = activity.State.Identity; var mode = (NavigationFlightMode)(int)owner.Modes.FlightMode;
            if (!observed.HasValue || !observed.Value.Equals(identity) || flightMode != mode || gravityMode != flight.GravityModeEnabled)
            { if(subscribed!=null)subscribed.Outcome-=RecordOutcome;subscribed=activity;observedState=activity.State;subscribed.Outcome+=RecordOutcome;owner.Session.ReplayBuffer.Clear(); observed = identity; flightMode = mode; gravityMode=flight.GravityModeEnabled; tuningId=flight.ReplayTuningIdentity(); started = Time.unscaledTimeAsDouble; }
            if (hud != null && hud.InputCaptured) return;
            var local = transform.position; var logical = origin == null ? DoubleVector3.Zero : origin.LogicalOrigin; var q = transform.rotation;
            owner.Session.ReplayBuffer.Record(Time.unscaledTimeAsDouble-started,
                new ReplayPosition(logical.X+local.x,logical.Y+local.y,logical.Z+local.z),new System.Numerics.Quaternion(q.x,q.y,q.z,q.w));
        }
        private void RecordOutcome(ActivityOutcome outcome)
        {
            if(!outcome.Committed||!outcome.Changed||!observed.HasValue||!outcome.Identity.Equals(observed.Value))return;
            var previous=observedState;observedState=outcome.State;if(previous==null)return;
            var seconds=Time.unscaledTimeAsDouble-started;
            foreach(var entry in outcome.State.Diary)
            {
                var existed=false;foreach(var old in previous.Diary)if(old.Id==entry.Id){existed=true;break;}if(existed)continue;
                if(entry.Retained)owner.Session.ReplayBuffer.RecordEvent(new ReplayEvent(seconds,ReplayEventKind.PersonalBookmark,"Saved personal bookmark"));
                else if(entry.Id.StartsWith("survey:clean-signal:",StringComparison.Ordinal))owner.Session.ReplayBuffer.RecordEvent(new ReplayEvent(seconds,ReplayEventKind.CommittedDiscovery,"Clean signal discovery"));
            }
            foreach(var record in outcome.State.Records)if(record.Completed&&previous.Find(record.Id)?.Completed!=true)
                owner.Session.ReplayBuffer.RecordEvent(new ReplayEvent(seconds,ReplayEventKind.ActivityCompleted,"Completed authored activity"));
        }
        private void OnDestroy(){if(subscribed!=null)subscribed.Outcome-=RecordOutcome;}
        public void Save()
        {
            try
            {
                if (!observed.HasValue) throw new InvalidOperationException("No active flight recording.");
                if(!observed.Value.Equals(owner.Session.Activities.State.Identity)||flightMode!=(NavigationFlightMode)(int)owner.Modes.FlightMode||gravityMode!=flight.GravityModeEnabled)
                    throw new InvalidOperationException("Flight context changed; start a new recording before saving.");
                var identity = new ReplayIdentity(observed.Value.Snapshot,observed.Value.Mode,1,tuningId,flightMode);
                var replay = owner.Session.ReplayBuffer.Capture(identity);
                var folder = Path.Combine(Application.persistentDataPath,"ClustrGalaxy","replays"); Directory.CreateDirectory(folder);
                var path = Path.Combine(folder,DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ")+"-"+Guid.NewGuid().ToString("N")+".clustr-replay");
                ReplayPackage.Save(path,replay); LastSavedPath = path;
                Status = "Saved locally · " + replay.Duration.ToString("F1") + " seconds · " + new FileInfo(path).Length + " bytes";
            }
            catch (Exception exception) { Status = "Replay save failed. Retry: " + exception.Message; }
            hud?.InvalidateExploration();
        }
    }
}
