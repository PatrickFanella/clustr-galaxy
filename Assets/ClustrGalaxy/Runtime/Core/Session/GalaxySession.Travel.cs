using System;
using System.Linq;
using System.Collections.Generic;
using ClustrGalaxy.Core.World;
using System.Threading;
using System.Threading.Tasks;
using ClustrGalaxy.Core.Data;
using ClustrGalaxy.Core.Domain;
using ClustrGalaxy.Core.Navigation;
using ClustrGalaxy.Core.Survey;
namespace ClustrGalaxy.Core.Session
{
    public sealed class JumpPreparation
    {
        internal JumpPreparation(string session, long operation, SurveySnapshotId snapshot, GalaxyEntityId target, SnapshotLoadOutcome receipt)
        { SessionId=session; OperationId=operation; Snapshot=snapshot; Target=target; Receipt=receipt; }
        public string SessionId {get;}
        public long OperationId {get;}
        public SurveySnapshotId Snapshot {get;}
        public GalaxyEntityId Target {get;}
        public SnapshotLoadOutcome Receipt {get;}
    }
    public sealed partial class GalaxySession
    {
        private JumpPreparation preparedJump;
        private bool committingJump;
        public string JumpError { get; private set; } = "";
        public string JumpEligibility(GalaxyEntityId target) => Travel == null ? "No active session." :
            TravelEligibility(new TravelIntent(Travel.State.SessionId, Travel.NextOperationId, Travel.State.Snapshot, TravelAction.Jump, target));
        // Called when the command presentation changes, never from the flight update loop.
        // Destinations belong to the resident data graph; a rendered star is not required.
        public IReadOnlyList<ResidentEntity> JumpDestinations()
        {
            var destinations = new Dictionary<GalaxyEntityId, ResidentEntity>();
            if (Travel == null || Flight.Recovering || CurrentSystemId.IsEmpty) return Array.Empty<ResidentEntity>();
            var residentRoutes = new HashSet<GalaxyRouteId>();
            foreach (var route in Resident.LoadedRoutes) residentRoutes.Add(route.Id);
            foreach (var scene in snapshots.Scenes.Values)
                foreach (var route in scene.Routes)
                {
                    if (!residentRoutes.Contains(route.Id) || (!explore && Survey?.State.KnownRoutes.Contains(route.Id) != true)) continue;
                    var target = route.SourceId == CurrentSystemId ? route.TargetId :
                        !route.Directed && route.TargetId == CurrentSystemId ? route.SourceId : default;
                    if (target.IsEmpty || target == CurrentSystemId ||
                        !Resident.TryFindLoaded(target, out var entity) || entity.Kind != GalaxyEntityKind.Subreddit ||
                        (!explore && Survey?.State.IsKnown(target) != true)) continue;
                    destinations[target] = entity;
                }
            return destinations.Values.OrderBy(value => value.Id).ToArray();
        }
        public string JumpDestinationLabel(ResidentEntity destination) =>
            explore || Survey?.State.IsIdentified(destination.Id) == true ? destination.DisplayName : "Unidentified connected system";

        public async Task<JumpPreparation> StageJumpAsync(CancellationToken token = default)
        {
            var state = Travel?.State;
            if (state == null || state.Phase != TravelPhase.JumpTransit || committingJump) return null;
            preparedJump = null; JumpError = "";
            SnapshotLoadOutcome receipt = null;
            if (snapshots.Mode != SnapshotDataMode.DeterministicDemo)
                receipt = await snapshots.StageAsync(new SpatialScopeRequest(SpatialScope.System, state.Target), token);
            if (token.IsCancellationRequested || !Travel.Matches(state.SessionId, state.OperationId, state.Snapshot) ||
                Travel.State.Phase != TravelPhase.JumpTransit) return null;
            if (receipt != null && (!receipt.Succeeded || receipt.IsStale))
            { JumpError = receipt.Error.Length == 0 ? "Destination loading was superseded." : receipt.Error; return null; }
            if (receipt != null && !receipt.Scene.Nodes.Any(node => node.Id == state.Target && node.Kind == GalaxyEntityKind.Subreddit))
            { JumpError = "The destination system response is missing its star."; snapshots.CancelPending(); return null; }
            return preparedJump = new JumpPreparation(state.SessionId, state.OperationId, state.Snapshot, state.Target, receipt);
        }
        // Only the issued preparation can authorize arrival. Physical adapters must supply a checked safe handoff.
        public SnapshotLoadOutcome CommitJump(JumpPreparation preparation, double clearance, double closingSpeed)
        {
            if (committingJump || preparation == null || !ReferenceEquals(preparation, preparedJump) ||
                !Travel.Matches(preparation.SessionId, preparation.OperationId, preparation.Snapshot) ||
                Travel.State.Phase != TravelPhase.JumpTransit || !double.IsFinite(clearance) || clearance <= 0 ||
                !double.IsFinite(closingSpeed) || Math.Abs(closingSpeed) >= 20 || JumpEligibility(preparation.Target).Length != 0)
                return new SnapshotLoadOutcome(0,false,true,snapshots.Mode,snapshots.Manifest,null,"JUMP REJECTED","Jump arrival is stale, unsafe or ineligible.");
            committingJump = true;
            try
            {
                var committed = preparation.Receipt == null ? new SnapshotLoadOutcome(0,true,false,snapshots.Mode,snapshots.Manifest,null,"AUTHORED DESTINATION VALIDATED","") : snapshots.CommitStaged(preparation.Receipt);
                if (!committed.Succeeded) return committed;
                if (committed.Scene != null) Travel.ProtectMutation(() => ApplyScene(committed.Scene));
                var arrival = Travel.PrepareArrival(preparation.SessionId,preparation.OperationId,preparation.Snapshot,clearance,closingSpeed);
                if (!arrival.Committed) throw new InvalidOperationException(arrival.Reason);
                Travel.FinishArrival(preparation.OperationId);
                ReplayBuffer.Clear();
                return committed;
            }
            finally { committingJump = false; preparedJump = null; }
        }
    }
}
