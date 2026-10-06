using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using ClustrGalaxy.Core.Data;
using ClustrGalaxy.Core.Domain;

namespace ClustrGalaxy.Core.Navigation
{
    public enum NavigationLevel { Galaxy, Region, System, Inspect }
    public enum NavigationFlightMode { Autopilot, PilotAssist, PilotMastery }

    public readonly struct NavigationPose
    {
        public NavigationPose(Vector3 position, Quaternion rotation) { Position = position; Rotation = rotation; }
        public Vector3 Position { get; }
        public Quaternion Rotation { get; }
    }

    public sealed class NavigationTarget
    {
        public NavigationTarget(GalaxyEntityId id, GalaxyEntityKind kind, string displayName, Vector3 position,
            bool active, bool known, float collisionRadius = 0, float gravitySoftening = 0,
            Vector3 velocity = default, CelestialBodyDefinition body = null)
        { Id = id; Kind = kind; DisplayName = displayName ?? string.Empty; Position = position; Active = active;
            Known = known; CollisionRadius = collisionRadius; GravitySoftening = gravitySoftening; Velocity = velocity; Body = body; }
        public GalaxyEntityId Id { get; }
        public GalaxyEntityKind Kind { get; }
        public string DisplayName { get; }
        public Vector3 Position { get; }
        public bool Active { get; }
        public bool Known { get; }
        public float CollisionRadius { get; }
        public float GravitySoftening { get; }
        public Vector3 Velocity { get; } public CelestialBodyDefinition Body { get; }
    }

    public sealed class GalaxyNavigationState
    {
        internal GalaxyNavigationState(bool mapOpen, bool survey, bool requiresSurveyStart, NavigationLevel level,
            GalaxyEntityId focused, GalaxyEntityId selected, GalaxyEntityId pendingTravel, GalaxyEntityId plotted,
            IReadOnlyList<NavigationFrame> history, string status, bool autopilotArrived, bool masteryReported)
        { IsMapOpen = mapOpen; IsSurvey = survey; RequiresSurveyStart = requiresSurveyStart; Level = level;
            FocusedId = focused; SelectedId = selected; PendingTravelId = pendingTravel; PlottedDestinationId = plotted;
            History = history; Status = status ?? string.Empty; AutopilotArrived = autopilotArrived;
            MasteryArrivalReported = masteryReported; }
        public bool IsMapOpen { get; }
        public bool IsSurvey { get; }
        public bool RequiresSurveyStart { get; }
        public NavigationLevel Level { get; }
        public GalaxyEntityId FocusedId { get; }
        public GalaxyEntityId SelectedId { get; }
        public GalaxyEntityId PendingTravelId { get; }
        public GalaxyEntityId PlottedDestinationId { get; }
        public IReadOnlyList<NavigationFrame> History { get; }
        public string Status { get; }
        public bool AutopilotArrived { get; }
        public bool MasteryArrivalReported { get; }
    }

    public readonly struct NavigationFrame
    {
        public NavigationFrame(NavigationLevel level, GalaxyEntityId id, NavigationPose returnPose, SpatialScopeRequest? scope = null)
        { Level = level; Id = id; ReturnPose = returnPose; Scope = scope; }
        public NavigationLevel Level { get; }
        public GalaxyEntityId Id { get; }
        public NavigationPose ReturnPose { get; }
        public SpatialScopeRequest? Scope { get; }
    }

    public abstract class NavigationIntent { }
    public sealed class SetMapIntent : NavigationIntent
    {
        public SetMapIntent(bool open, NavigationPose pose, Vector3 velocity, Vector3 angularVelocity)
        { Open = open; Pose = pose; Velocity = velocity; AngularVelocity = angularVelocity; }
        public bool Open { get; } public NavigationPose Pose { get; }
        public Vector3 Velocity { get; } public Vector3 AngularVelocity { get; }
    }
    public sealed class SelectNavigationTargetIntent : NavigationIntent
    { public SelectNavigationTargetIntent(GalaxyEntityId id) { Id = id; } public GalaxyEntityId Id { get; } }
    public sealed class InspectNavigationTargetIntent : NavigationIntent
    { public InspectNavigationTargetIntent(NavigationTarget target, NavigationPose pose) { Target = target; Pose = pose; }
        public NavigationTarget Target { get; } public NavigationPose Pose { get; } }
    public sealed class EnterSurveyNavigationIntent : NavigationIntent { }
    public sealed class SetSurveyModeIntent : NavigationIntent
    {
        public SetSurveyModeIntent(bool survey, bool requiresStart)
        { Survey = survey; RequiresStart = survey && requiresStart; }
        public bool Survey { get; }
        public bool RequiresStart { get; }
    }
    public sealed class ChooseSurveyStartIntent : NavigationIntent
    { public ChooseSurveyStartIntent(GalaxyEntityId id) { Id = id; } public GalaxyEntityId Id { get; } }
    public sealed class FastTravelIntent : NavigationIntent
    { public FastTravelIntent(NavigationTarget target) { Target = target; } public NavigationTarget Target { get; } }
    public sealed class PlotApproachIntent : NavigationIntent
    { public PlotApproachIntent(NavigationTarget target, bool mastery) { Target = target; Mastery = mastery; }
        public NavigationTarget Target { get; } public bool Mastery { get; } }
    public sealed class ReturnNavigationIntent : NavigationIntent { }
    public sealed class ClearFlightGuidanceIntent : NavigationIntent { }

    public sealed class NavigationEffect
    {
        internal NavigationEffect(bool pause, bool resume, bool teleport, Vector3 position, Vector3 velocity,
            Vector3 angularVelocity, bool rebase, SpatialScopeRequest? load, GalaxyEntityId masteryArrival, Quaternion? rotation = null)
        { Rotation = rotation; PausePhysics = pause; ResumePhysics = resume; Teleport = teleport; Position = position; Velocity = velocity;
            AngularVelocity = angularVelocity; Rebase = rebase; ScopeToLoad = load; MasteryArrival = masteryArrival; }
        public Quaternion? Rotation { get; }
        public bool PausePhysics { get; }
        public bool ResumePhysics { get; }
        public bool Teleport { get; }
        public Vector3 Position { get; }
        public Vector3 Velocity { get; }
        public Vector3 AngularVelocity { get; }
        public bool Rebase { get; }
        public SpatialScopeRequest? ScopeToLoad { get; }
        public GalaxyEntityId MasteryArrival { get; }
        public static NavigationEffect None { get; } = new(false, false, false, default, default, default, false, null, default);
    }

    public sealed class NavigationOutcome
    {
        internal NavigationOutcome(bool succeeded, string rejection, GalaxyNavigationState state, NavigationEffect effect)
        { Succeeded = succeeded; Rejection = rejection ?? string.Empty; State = state; Effect = effect ?? NavigationEffect.None; }
        public bool Succeeded { get; }
        public string Rejection { get; }
        public GalaxyNavigationState State { get; }
        public NavigationEffect Effect { get; }
    }

    public sealed class GalaxyNavigation
    {
        public const float DefaultClearance = 24f;
        public const float AutopilotMaximumSpeed = 92f;
        public const float AutopilotArrivalSpeed = 1.5f;
        private readonly List<NavigationFrame> history = new();
        private GalaxyNavigationState state = NewState();

        public GalaxyNavigationState State => state;
        public OrbitalAutopilot Orbit { get; } = new();

        public NavigationOutcome Apply(NavigationIntent intent)
        {
            if (intent == null) throw new ArgumentNullException(nameof(intent));
            if (intent is FastTravelIntent || intent is PlotApproachIntent || intent is ClearFlightGuidanceIntent ||
                intent is EnterSurveyNavigationIntent || intent is SetSurveyModeIntent || intent is ReturnNavigationIntent)
                Orbit.Cancel();
            switch (intent)
            {
                case SetMapIntent map: return SetMap(map);
                case SelectNavigationTargetIntent select:
                    state = Copy(selected: select.Id); return Success();
                case InspectNavigationTargetIntent inspect: return Inspect(inspect);
                case EnterSurveyNavigationIntent _:
                    state = Copy(mapOpen: true, survey: true, requiresStart: true, status: "CHOOSE A STARTING SYSTEM");
                    return Success();
                case SetSurveyModeIntent mode:
                    state = Copy(survey: mode.Survey, requiresStart: mode.RequiresStart,
                        status: mode.Survey && mode.RequiresStart ? "CHOOSE A STARTING SYSTEM" : state.Status);
                    return Success();
                case ChooseSurveyStartIntent start:
                    state = Copy(survey: true, requiresStart: false, focused: start.Id, status: "SURVEY STARTED");
                    return Success();
                case FastTravelIntent travel: return QueueTravel(travel.Target);
                case PlotApproachIntent approach: return Plot(approach.Target, approach.Mastery);
                case ReturnNavigationIntent _: return Return();
                case ClearFlightGuidanceIntent _:
                    state=Copy(pending:default(GalaxyEntityId),plotted:default(GalaxyEntityId),status:"FREE FLIGHT");return Success();
                default: return Reject("Unsupported navigation intent.");
            }
        }

        public NavigationOutcome AdvancePhysics(NavigationPose pose, Vector3 velocity, NavigationFlightMode mode,
            Func<GalaxyEntityId, NavigationTarget> resolve, float brakeAcceleration, float arrivalTolerance = 2.5f)
        {
            if (resolve == null) throw new ArgumentNullException(nameof(resolve));
            if (!state.PendingTravelId.IsEmpty)
            {
                var target = resolve(state.PendingTravelId);
                if (!CanTravel(target)) { state = Copy(pending: default(GalaxyEntityId), status: "FAST TRAVEL CANCELLED — DESTINATION NO LONGER AVAILABLE"); return Success(); }
                var arrival = ArrivalAnchor(target, pose.Position);
                state = Copy(pending: default(GalaxyEntityId), status: "ARRIVED — " + target.DisplayName);
                return Success(new NavigationEffect(false, false, true, arrival, Vector3.Zero, Vector3.Zero, true, null, default));
            }

            if (state.PlottedDestinationId.IsEmpty) return Success();
            var destination = resolve(state.PlottedDestinationId);
            if (!CanApproach(destination))
            { state = Copy(plotted: default(GalaxyEntityId), status: "ROUTE LOST — DESTINATION NO LONGER AVAILABLE"); return Success(); }
            var anchor = ArrivalAnchor(destination, pose.Position);
            var arrived = Vector3.Distance(pose.Position, anchor) <= Math.Max(0, arrivalTolerance) &&
                (velocity - destination.Velocity).Length() <= AutopilotArrivalSpeed;
            if (mode == NavigationFlightMode.Autopilot)
            {
                var desired = destination.Velocity + DesiredVelocity(pose.Position, anchor, brakeAcceleration, arrivalTolerance);
                state = Copy(autopilotArrived: arrived, status: arrived ? "AUTOPILOT ARRIVED — " + destination.DisplayName : state.Status);
                return Success(new NavigationEffect(false, false, false, default, desired, default, false, null, default));
            }
            if (mode == NavigationFlightMode.PilotMastery && arrived && !state.AutopilotArrived && !state.MasteryArrivalReported)
            {
                state = Copy(masteryReported: true, status: "MASTERY ARRIVAL — " + destination.DisplayName);
                return Success(new NavigationEffect(false, false, false, default, default, default, false, null, destination.Kind == GalaxyEntityKind.Subreddit ? destination.Id : default));
            }
            return Success();
        }

        private NavigationOutcome SetMap(SetMapIntent intent)
        {
            state = Copy(mapOpen: intent.Open);
            // Chart inspection changes presentation only; momentum continues in local physics.
            return Success();
        }

        private NavigationOutcome Inspect(InspectNavigationTargetIntent intent)
        {
            if (intent.Target == null || !intent.Target.Active) return Reject("The selected entity is not resident and active.");
            var next = intent.Target.Kind switch
            {
                GalaxyEntityKind.Community => NavigationLevel.Region,
                GalaxyEntityKind.Subreddit => NavigationLevel.System,
                GalaxyEntityKind.Post => NavigationLevel.Inspect,
                GalaxyEntityKind.Comment => NavigationLevel.Inspect,
                _ => state.Level
            };
            SpatialScopeRequest? load = intent.Target.Kind switch
            {
                GalaxyEntityKind.Community => new SpatialScopeRequest(SpatialScope.Community, intent.Target.Id),
                GalaxyEntityKind.Subreddit => new SpatialScopeRequest(SpatialScope.System, intent.Target.Id),
                GalaxyEntityKind.Post => new SpatialScopeRequest(SpatialScope.Discussion, intent.Target.Id),
                GalaxyEntityKind.Comment => new SpatialScopeRequest(SpatialScope.Thread, intent.Target.Id),
                _ => null
            };
            history.Add(new NavigationFrame(next, intent.Target.Id, intent.Pose, load));
            state = Copy(mapOpen: true, level: next, focused: intent.Target.Id, selected: intent.Target.Id, historyValue: history.ToArray());
            return Success(new NavigationEffect(false, false, false, default, default, default, false, load, default));
        }

        private NavigationOutcome QueueTravel(NavigationTarget target)
        {
            if (!CanTravel(target)) return Reject("FAST TRAVEL UNAVAILABLE — SELECT A KNOWN SYSTEM");
            state = Copy(pending: target.Id, status: "FAST TRAVEL ARMED — EXECUTING ON PHYSICS TICK");
            return Success();
        }

        private NavigationOutcome Plot(NavigationTarget target, bool mastery)
        {
            if (!CanApproach(target)) return Reject("APPROACH UNAVAILABLE — SELECT A KNOWN BODY");
            state = Copy(plotted: target.Id, autopilotArrived: false, masteryReported: false,
                status: mastery ? "MASTERY APPROACH SET — " + target.DisplayName : "AUTOPILOT PLOTTED — " + target.DisplayName);
            return Success();
        }

        private NavigationOutcome Return()
        {
            if (history.Count == 0) { state = Copy(mapOpen: false); return Success(); }
            var frame = history[history.Count - 1]; history.RemoveAt(history.Count - 1);
            var previous = history.Count == 0 ? default : history[history.Count - 1];
            state = Copy(level: history.Count == 0 ? NavigationLevel.Galaxy : previous.Level,
                focused: history.Count == 0 ? default : previous.Id, selected: default(GalaxyEntityId), historyValue: history.ToArray());
            return Success(new NavigationEffect(false, false, true, frame.ReturnPose.Position, default, default, false, history.Count == 0 ? null : previous.Scope, default, frame.ReturnPose.Rotation));
        }

        private bool CanApproach(NavigationTarget target) => target != null && target.Active &&
            (GalaxyKinds.IsNavigable(target.Kind) || target.Kind == GalaxyEntityKind.Post || target.Body != null) && (!state.IsSurvey || target.Known);
        private bool CanTravel(NavigationTarget target) => target != null && target.Active &&
            GalaxyKinds.IsNavigable(target.Kind) && (!state.IsSurvey || target.Known);
        private static Vector3 ArrivalAnchor(NavigationTarget target, Vector3 origin)
        {
            var radius = Math.Max(Math.Max(0, target.CollisionRadius), Math.Max(0, target.GravitySoftening)) + DefaultClearance;
            var direction = target.Position - origin;
            if (direction.LengthSquared() < 0.0001f) direction = -Vector3.UnitZ;
            return target.Position - Vector3.Normalize(direction) * radius;
        }
        private static Vector3 DesiredVelocity(Vector3 current, Vector3 arrival, float brakeAcceleration, float tolerance)
        {
            var offset = arrival - current; var distance = offset.Length();
            if (distance <= Math.Max(0, tolerance) || distance <= 0.0001f) return Vector3.Zero;
            var stopping = (float)Math.Sqrt(2f * Math.Max(0.01f, brakeAcceleration) * Math.Max(0, distance - tolerance));
            var speed = Math.Min(AutopilotMaximumSpeed, Math.Max(AutopilotArrivalSpeed, stopping));
            return offset / distance * speed;
        }

        private NavigationOutcome unchangedOutcome;
        private NavigationOutcome Success(NavigationEffect effect = null)
        {
            if (effect != null) return new NavigationOutcome(true, "", state, effect);
            if (unchangedOutcome == null || !ReferenceEquals(unchangedOutcome.State, state))
                unchangedOutcome = new NavigationOutcome(true, "", state, null);
            return unchangedOutcome;
        }
        private NavigationOutcome Reject(string reason) => new(false, reason, state, NavigationEffect.None);
        private static GalaxyNavigationState NewState() => new(false, false, false, NavigationLevel.Galaxy,
            default, default, default, default, Array.Empty<NavigationFrame>(), "", false, false);
        private GalaxyNavigationState Copy(bool? mapOpen = null, bool? survey = null, bool? requiresStart = null,
            NavigationLevel? level = null, GalaxyEntityId? focused = null, GalaxyEntityId? selected = null,
            GalaxyEntityId? pending = null, GalaxyEntityId? plotted = null, IReadOnlyList<NavigationFrame> historyValue = null,
            string status = null, bool? autopilotArrived = null, bool? masteryReported = null) =>
            new(mapOpen ?? state.IsMapOpen, survey ?? state.IsSurvey, requiresStart ?? state.RequiresSurveyStart,
                level ?? state.Level, focused ?? state.FocusedId, selected ?? state.SelectedId,
                pending ?? state.PendingTravelId, plotted ?? state.PlottedDestinationId,
                historyValue ?? state.History, status ?? state.Status, autopilotArrived ?? state.AutopilotArrived,
                masteryReported ?? state.MasteryArrivalReported);
    }
}
