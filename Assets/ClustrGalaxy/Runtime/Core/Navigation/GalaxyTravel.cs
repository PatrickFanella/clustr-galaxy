using System;
using ClustrGalaxy.Core.Domain;
using ClustrGalaxy.Core.Survey;

namespace ClustrGalaxy.Core.Navigation
{
    public enum TravelPhase { LocalFlight, Charging, SystemCruise, Approach, JumpCharging, JumpTransit, Arrival }
    public enum TravelAction { Cruise, Jump, Cancel }

    public sealed class TravelIntent
    {
        public TravelIntent(string sessionId, long operationId, SurveySnapshotId snapshot, TravelAction action,
            GalaxyEntityId target = default, bool automatic = true)
        { SessionId = sessionId; OperationId = operationId; Snapshot = snapshot; Action = action; Target = target; Automatic = automatic; }
        public string SessionId { get; }
        public long OperationId { get; }
        public SurveySnapshotId Snapshot { get; }
        public TravelAction Action { get; }
        public GalaxyEntityId Target { get; }
        public bool Automatic { get; }
        internal bool Same(TravelIntent other) => other != null && SessionId == other.SessionId &&
            OperationId == other.OperationId && Snapshot == other.Snapshot && Action == other.Action &&
            Target == other.Target && Automatic == other.Automatic;
    }

    public sealed class TravelState
    {
        public const string Version = "travel-v1";
        internal TravelState(string sessionId, long operation, SurveySnapshotId snapshot, TravelPhase phase,
            TravelAction action, GalaxyEntityId target, bool automatic)
        { SessionId = sessionId; OperationId = operation; Snapshot = snapshot; Phase = phase; Action = action; Target = target; Automatic = automatic; }
        public string SessionId { get; }
        public long OperationId { get; }
        public SurveySnapshotId Snapshot { get; }
        public TravelPhase Phase { get; }
        public TravelAction Action { get; }
        public GalaxyEntityId Target { get; }
        public bool Automatic { get; }
        public bool Active => Phase != TravelPhase.LocalFlight;
    }

    public sealed class TravelOutcome
    {
        internal TravelOutcome(bool committed, TravelState state, string reason, bool aborted = false)
        { Committed = committed; State = state; Reason = reason ?? ""; Aborted = aborted; }
        public bool Committed { get; }
        public TravelState State { get; }
        public string Reason { get; }
        public bool Aborted { get; }
    }

    // State changes are authoritative; no position, provider request, or progression is hidden here.
    public sealed class GalaxyTravel
    {
        public const double ChargeSeconds = 3;
        private readonly Func<TravelIntent, string> validate;
        private readonly Func<long, bool> jumpReady;
        private TravelIntent lastIntent;
        private TravelOutcome lastIntentOutcome;
        private TravelOutcome current;
        private double charge;
        private bool publishing;
        public GalaxyTravel(string sessionId, SurveySnapshotId snapshot, Func<TravelIntent, string> eligibility, Func<long, bool> jumpArrivalReady = null)
        {
            if (string.IsNullOrWhiteSpace(sessionId) || snapshot.RevisionId <= 0) throw new ArgumentException("A frozen session is required.");
            jumpReady = jumpArrivalReady;
            validate = eligibility ?? throw new ArgumentNullException(nameof(eligibility));
            State = new TravelState(sessionId, 0, snapshot, TravelPhase.LocalFlight, TravelAction.Cruise, default, true);
            current = new TravelOutcome(true, State, "LOCAL FLIGHT");
        }
        public TravelState State { get; private set; }
        public string Status => current.Reason;
        public long NextOperationId => State.OperationId + 1;
        public double ChargeRemaining => State.Phase == TravelPhase.Charging || State.Phase == TravelPhase.JumpCharging ? Math.Max(0, ChargeSeconds - charge) : 0;
        public event Action<TravelOutcome> Changed = delegate { };
        public TravelOutcome Apply(TravelIntent intent)
        {
            if (publishing) return Reject("Travel listeners cannot change travel during an outcome.");
            if (intent == null || intent.SessionId != State.SessionId || intent.Snapshot != State.Snapshot)
                return Reject("The travel command belongs to another session or snapshot.");
            if (lastIntent != null && intent.OperationId == lastIntent.OperationId && lastIntent.Same(intent)) return lastIntentOutcome;
            if (intent.OperationId != NextOperationId) return Reject("The travel command is stale.");
            if (!Enum.IsDefined(typeof(TravelAction), intent.Action)) return Reject("Unsupported travel command.");
            if (intent.Action == TravelAction.Cancel && !State.Active) return Reject("No drive is active.");
            if (intent.Action != TravelAction.Cancel)
            {
                if (State.Active) return Reject("Cancel the active drive before starting another journey.");
                var error = validate(intent); if (!string.IsNullOrEmpty(error)) return Reject(error);
            }
            charge = 0;
            lastIntent = intent;
            var phase = intent.Action == TravelAction.Cancel ? TravelPhase.LocalFlight :
                intent.Action == TravelAction.Jump ? TravelPhase.JumpCharging : TravelPhase.Charging;
            // Cache before publishing so duplicate input never replays an arrival notification.
            State = new TravelState(State.SessionId, intent.OperationId, State.Snapshot, phase, intent.Action,
                intent.Action == TravelAction.Cancel ? default : intent.Target, intent.Automatic);
            lastIntentOutcome = current = new TravelOutcome(true, State,
                phase == TravelPhase.LocalFlight ? "TRAVEL ABORTED · LOCAL FLIGHT" : "DRIVE CHARGING", intent.Action == TravelAction.Cancel);
            Publish(current); return current;
        }
        public TravelOutcome AdvanceCharge(double seconds)
        {
            if (publishing || !double.IsFinite(seconds) || seconds < 0) return Reject("Invalid travel clock step.");
            if (State.Phase != TravelPhase.Charging && State.Phase != TravelPhase.JumpCharging) return current;
            charge += seconds;
            if (charge < ChargeSeconds) return current;
            return Transition(State.Phase == TravelPhase.Charging ? TravelPhase.SystemCruise : TravelPhase.JumpTransit,
                State.Phase == TravelPhase.Charging ? "SYSTEM CRUISE · FICTIONAL DRIVE" : "JUMP TRANSIT · VALIDATING DESTINATION");
        }
        public bool Matches(string session, long operation, SurveySnapshotId snapshot) =>
            State.Active && session == State.SessionId && operation == State.OperationId && snapshot == State.Snapshot;
        public TravelOutcome BeginApproach(long operation) => !publishing && operation == State.OperationId && State.Phase == TravelPhase.SystemCruise ?
            Transition(TravelPhase.Approach, "APPROACH · DRIVE BRAKING") : Reject("Approach is no longer current.");
        public TravelOutcome PrepareArrival(string session, long operation, SurveySnapshotId snapshot, double clearance, double closingSpeed)
        {
            if (publishing || !Matches(session, operation, snapshot) ||
                (State.Phase != TravelPhase.JumpTransit && State.Phase != TravelPhase.Approach)) return Reject("Arrival is stale or was cancelled.");
            if (!double.IsFinite(clearance) || clearance <= 0 || !double.IsFinite(closingSpeed) || Math.Abs(closingSpeed) >= 20)
                return Reject("Arrival requires safe clearance and less than 20 m/s relative closing speed.");
            if (State.Action == TravelAction.Jump && jumpReady != null && !jumpReady(operation))
                return Reject("A current validated destination receipt is required for jump arrival.");
            return Transition(TravelPhase.Arrival, "ARRIVAL · RETURNING TO LOCAL PHYSICS");
        }
        public TravelOutcome FinishArrival(long operation) => !publishing && operation == State.OperationId && State.Phase == TravelPhase.Arrival ?
            Transition(TravelPhase.LocalFlight, "ARRIVED · LOCAL FLIGHT") : Reject("Arrival has already completed or was cancelled.");
        public TravelOutcome Abort(long operation, string reason) => !publishing && operation == State.OperationId && State.Active ?
            Transition(TravelPhase.LocalFlight, reason, true) : Reject("No matching travel to abort.");
        private TravelOutcome Transition(TravelPhase phase, string reason, bool aborted = false)
        {
            State = new TravelState(State.SessionId, State.OperationId, State.Snapshot, phase, State.Action,
                phase == TravelPhase.LocalFlight ? default : State.Target, State.Automatic);
            current = new TravelOutcome(true, State, reason, aborted); Publish(current); return current;
        }
        internal void ProtectMutation(Action mutation)
        {
            if (publishing) throw new InvalidOperationException("Nested travel mutation.");
            publishing=true; try { mutation(); } finally { publishing=false; }
        }
        private void Publish(TravelOutcome outcome)
        { publishing = true; try { Changed(outcome); } finally { publishing = false; } }
        private TravelOutcome Reject(string reason) => new(false, State, reason);
    }
}
