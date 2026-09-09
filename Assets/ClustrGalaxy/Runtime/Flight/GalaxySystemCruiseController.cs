using System;
using ClustrGalaxy.Bootstrap;
using ClustrGalaxy.Core.Domain;
using ClustrGalaxy.Core.Navigation;
using ClustrGalaxy.Gameplay;
using ClustrGalaxy.Simulation;
using ClustrGalaxy.UI;
using ClustrGalaxy.World;
using UnityEngine;
using NVector = System.Numerics.Vector3;

namespace ClustrGalaxy.Flight
{
    [DefaultExecutionOrder(200)]
    public sealed class GalaxySystemCruiseController : MonoBehaviour, ICruiseExclusions
    {
        private GalaxySessionBehaviour owner;
        private ObserverProbeController flight;
        private GalaxyNavigationController navigation;
        private Rigidbody body;
        private ProbeHud hud;
        private GalaxyTravel travel;
        private SystemCruise drive;
        private Vector3 handoffVelocity;
        private bool controlling;
        public float Throttle { get; private set; } = .5f;
        public bool Active => travel?.State.Active == true && travel.State.Action == TravelAction.Cruise;
        private void Awake()
        {
            owner = GetComponent<GalaxySessionBehaviour>(); flight = GetComponent<ObserverProbeController>();
            navigation = GetComponent<GalaxyNavigationController>(); body = GetComponent<Rigidbody>(); hud = GetComponent<ProbeHud>();
        }
        private bool Bind()
        {
            var current = owner?.Session?.Travel;
            if (ReferenceEquals(current, travel)) return travel != null;
            if (travel != null) travel.Changed -= Changed;
            travel = current; if (travel != null) travel.Changed += Changed;
            return travel != null;
        }
        public bool Begin(GalaxyEntity entity, bool automatic)
        {
            if (!Bind() || entity == null || !navigation.CanApproach(entity) || navigation.IsMapOpen) return false;
            var source = entity.GetComponent<GravitySource>(); if (source?.Definition == null) return false;
            try { _ = new SystemCruise(Logical(body.position), N(body.linearVelocity), Target(source)); }
            catch (ArgumentException error) { navigation.ReportTravelStatus(error.Message); return false; }
            var outcome = travel.Apply(new TravelIntent(travel.State.SessionId, travel.NextOperationId, travel.State.Snapshot,
                TravelAction.Cruise, (GalaxyEntityId)entity.StableId, automatic));
            if (!outcome.Committed) { navigation.ReportTravelStatus(outcome.Reason); return false; }
            Throttle = .5f; drive = null; flight.ClearAutopilotDesiredVelocity(); hud?.SetMenuOpen(false);
            return true;
        }
        public void Cancel()
        {
            if (!Bind() || !Active) return;
            travel.Apply(new TravelIntent(travel.State.SessionId, travel.NextOperationId, travel.State.Snapshot, TravelAction.Cancel));
        }
        private void Changed(TravelOutcome outcome)
        {
            if (outcome.State.Phase == TravelPhase.LocalFlight && controlling)
            {
                var target = driveTarget;
                if (outcome.Aborted) handoffVelocity = target != null ? target.CenterVelocity : Vector3.zero;
                if (!flight.Simulation.Recovering)
                {
                    body.isKinematic = false; body.linearVelocity = handoffVelocity; body.angularVelocity = Vector3.zero;
                    flight.ModeState.SetFlightMode(FlightControlMode.PilotMastery);
                }
                flight.DriveVelocity = null; flight.ClearAutopilotDesiredVelocity(); drive = null; controlling = false;
            }
            navigation.ReportTravelStatus(outcome.Reason); hud?.InvalidateExploration();
        }
        private GravitySource driveTarget;
        private void FixedUpdate()
        {
            if (!Bind() || !Active) return;
            var entity = owner.Registry.FindActiveEntity(travel.State.Target.Value);
            var source = entity != null ? entity.GetComponent<GravitySource>() : null;
            if (source?.Definition == null || flight.Simulation.Recovering)
            { travel.Abort(travel.State.OperationId, "DESTINATION UNAVAILABLE · DRIVE ABORTED"); return; }
            driveTarget = source;
            travel.AdvanceCharge(Time.fixedDeltaTime);
            if (travel.State.Phase != TravelPhase.SystemCruise && travel.State.Phase != TravelPhase.Approach) return;
            var target = Target(source);
            if (!controlling)
            {
                try { drive = new SystemCruise(Logical(body.position), N(body.linearVelocity), target); }
                catch (ArgumentException error) { travel.Abort(travel.State.OperationId, error.Message); return; }
                body.isKinematic = true; controlling = true;
            }
            var dt = Time.fixedDeltaTime;
            if (!travel.State.Automatic)
            {
                Throttle = Mathf.Clamp01(Throttle + flight.PilotTranslation.z * dt * .5f);
                if (flight.PilotBrake) Throttle = 0;
                body.MoveRotation(body.rotation * Quaternion.Euler(Vector3.Scale(flight.PilotRotation, flight.AngularRateLimits) * dt));
            }
            else
            {
                var offset = entity.transform.position - body.position;
                if (offset.sqrMagnitude > 1) body.MoveRotation(Quaternion.RotateTowards(body.rotation,
                    Quaternion.LookRotation(offset, transform.up), 60 * dt));
            }
            var heading = transform.forward + transform.right * flight.PilotTranslation.x * .1f + transform.up * flight.PilotTranslation.y * .1f;
            var result = drive.Advance(target, dt, travel.State.Automatic, N(heading), Throttle, flight.PilotBrake, this);
            body.position = Local(result.Position); transform.position = body.position;
            flight.DriveVelocity = handoffVelocity = U(result.Velocity);
            if (result.Dropped)
            {
                // This explicit fictional-drive drop preserves the safe swept contact result.
                var velocity = handoffVelocity;
                travel.Abort(travel.State.OperationId, "EXCLUSION AHEAD · CONTROLLED DRIVE DROP");
                if (!flight.Simulation.Recovering) body.linearVelocity = velocity;
                return;
            }
            if (result.Approach && travel.State.Phase == TravelPhase.SystemCruise) travel.BeginApproach(travel.State.OperationId);
            if (result.Arrived)
            {
                var operation = travel.State.OperationId;
                if (travel.PrepareArrival(travel.State.SessionId, operation, travel.State.Snapshot, result.Clearance, result.ClosingSpeed).Committed)
                    travel.FinishArrival(operation);
            }
        }
        public bool Sweep(SimulationPosition from, SimulationPosition to, double seconds, out CruiseContact contact)
        {
            GravitySource hit = null; var nearest = 2f; NVector normal = default;
            var sources = GravitySource.ActiveSources;
            for (var i = 0; i < sources.Count; i++)
            {
                var source = sources[i]; if (source?.Definition == null) continue;
                var center = Logical(source.transform.position); var start = from.RelativeTo(center);
                var radius = source.Definition.ExclusionRadius + ShipSurfaceSafety.HullRadius;
                if (!SurfaceIntersection.Sweep(start, to.RelativeTo(center), radius + source.MaximumSpeed * seconds, out _)) continue;
                var end = to.RelativeTo(center.Add(N(source.CenterVelocity) * (float)seconds));
                if (!SurfaceIntersection.Sweep(start, end, radius, out var fraction) || fraction >= nearest) continue;
                nearest = fraction; hit = source; normal = NVector.Lerp(start, end, fraction);
            }
            if (hit == null) { contact = default; return false; }
            normal = normal.LengthSquared() > 1e-8 ? NVector.Normalize(normal) : NVector.UnitY;
            var position = Logical(hit.transform.position).Add(normal * (float)(hit.Definition.ExclusionRadius + SystemCruise.StandOff));
            contact = new CruiseContact(position, N(hit.CenterVelocity)); return true;
        }
        private static CruiseTarget Target(GravitySource source) => new(source.Definition.Id, Logical(source.transform.position), N(source.CenterVelocity), source.Definition.ExclusionRadius);
        private static SimulationPosition Logical(Vector3 value)
        {
            var origin = FloatingOrigin.Active != null ? FloatingOrigin.Active.LogicalOrigin : DoubleVector3.Zero;
            return new SimulationPosition(origin.X + value.x, origin.Y + value.y, origin.Z + value.z);
        }
        private static Vector3 Local(SimulationPosition value)
        {
            var origin = FloatingOrigin.Active != null ? FloatingOrigin.Active.LogicalOrigin : DoubleVector3.Zero;
            return new Vector3((float)(value.X - origin.X), (float)(value.Y - origin.Y), (float)(value.Z - origin.Z));
        }
        private static NVector N(Vector3 value) => new(value.x, value.y, value.z);
        private static Vector3 U(NVector value) => new(value.X, value.Y, value.Z);
        private void OnDestroy()
        { if (travel != null) { travel.Abort(travel.State.OperationId, "DRIVE DISCONNECTED"); travel.Changed -= Changed; } }
    }
}
