using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using ClustrGalaxy.Core.Data;
using ClustrGalaxy.Core.Domain;
using ClustrGalaxy.Core.Navigation;
using ClustrGalaxy.Core.Presentation;
using ClustrGalaxy.Core.Survey;
using ClustrGalaxy.Core.World;

namespace ClustrGalaxy.Core.Session
{
    public sealed class GalaxySession : IDisposable
    {
        private readonly GalaxySnapshotSession snapshots;
        private readonly ISurveyRepository surveyRepository;
        private readonly string profileId;

        public GalaxySession(IGalaxySnapshotSource snapshotSource, ISurveyRepository surveyRepository, string profileId)
        {
            snapshots = new GalaxySnapshotSession(snapshotSource);
            this.surveyRepository = surveyRepository ?? throw new ArgumentNullException(nameof(surveyRepository));
            if (string.IsNullOrWhiteSpace(profileId)) throw new ArgumentException("A profile ID is required.", nameof(profileId));
            this.profileId = profileId;
            Resident = new ResidentGalaxy();
            Navigation = new GalaxyNavigation();
            Hud = new ProbeHudPresenter();
        }

        public ResidentGalaxy Resident { get; }
        public GalaxyNavigation Navigation { get; }
        public ProbeHudPresenter Hud { get; }
        public SurveySession Survey { get; private set; }
        public GalaxySnapshotSession Snapshots => snapshots;

        public async Task<SnapshotLoadOutcome> OpenAsync(CancellationToken cancellationToken = default)
        {
            var outcome = await snapshots.OpenAsync(cancellationToken);
            if (outcome.Succeeded) ApplyScene(outcome.Scene);
            return outcome;
        }

        public async Task<SnapshotLoadOutcome> LoadAsync(SpatialScopeRequest request,
            CancellationToken cancellationToken = default)
        {
            var outcome = await snapshots.LoadAsync(request, cancellationToken);
            if (outcome.Succeeded) ApplyScene(outcome.Scene);
            return outcome;
        }

        public SurveyTransitionOutcome StartSurvey(GalaxyEntityId startingSystemId,
            IEnumerable<GalaxyEntityId> seedIdentifiedEntities, string intentId, DateTime utc)
        {
            if (snapshots.Manifest == null) throw new InvalidOperationException("A pinned snapshot must be open before Survey starts.");
            var snapshot = snapshots.Manifest.SurveySnapshot;
            Survey ??= new SurveySession(surveyRepository, profileId, snapshot, startingSystemId);
            if (Survey.State.StartingSystemId != startingSystemId)
                throw new InvalidOperationException("The active Survey session has a different starting system.");
            var outcome = Survey.Apply(new StartSurveyIntent(intentId, snapshot, utc, startingSystemId, seedIdentifiedEntities));
            if (outcome.Succeeded) ApplySurveyVisibility(false);
            return outcome;
        }

        public SurveyTransitionOutcome ApplySurvey(SurveyIntent intent)
        {
            if (Survey == null) throw new InvalidOperationException("Survey has not started.");
            var outcome = Survey.Apply(intent);
            if (outcome.Succeeded) ApplySurveyVisibility(false);
            return outcome;
        }

        public NavigationOutcome AdvanceNavigationPhysics(NavigationPose pose, Vector3 velocity,
            NavigationFlightMode flightMode, Func<GalaxyEntityId, NavigationTarget> resolve, float brakeAcceleration,
            DateTime utc)
        {
            var outcome = Navigation.AdvancePhysics(pose, velocity, flightMode, resolve, brakeAcceleration);
            if (!outcome.Effect.MasteryArrival.IsEmpty && Survey != null)
            {
                var id = "mastery:" + outcome.Effect.MasteryArrival.Value + ":" + utc.Ticks;
                ApplySurvey(new PilotMasteryArrivalIntent(id, Survey.State.Snapshot, utc, outcome.Effect.MasteryArrival));
            }
            return outcome;
        }

        public ResidentGalaxyChangeSet ApplySurveyVisibility(bool explore)
        {
            return Resident.ApplySurveyVisibility(explore,
                Survey?.State.KnownEntities ?? Array.Empty<GalaxyEntityId>(),
                Survey?.State.KnownRoutes ?? Array.Empty<GalaxyRouteId>());
        }

        public void Dispose() => snapshots.Dispose();

        private ResidentGalaxyChangeSet ApplyScene(GalaxySnapshotScene scene)
        {
            var system = scene.Scope.Scope == SpatialScope.System ? scene.Scope.RootId : default;
            var entities = scene.Nodes.Select(node => new ResidentEntity(node.Id, node.Kind,
                GalaxyKinds.VisualFor(node.Kind), scene.Scope, node.Name, node.Value, system));
            var routes = scene.Routes.Select(route => new ResidentRoute(route.Id, route.SourceId, route.TargetId, scene.Scope));
            return Resident.ApplyProjection(scene.Scope, entities, routes);
        }
    }
}
