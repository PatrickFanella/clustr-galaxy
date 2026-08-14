using System;
using System.Collections.Generic;
using System.Linq;
using ClustrGalaxy.Core.Domain;
using ClustrGalaxy.Core.Presentation;
using ClustrGalaxy.Core.Survey;
using ClustrGalaxy.Data;
using ClustrGalaxy.Flight;
using ClustrGalaxy.Gameplay;
using ClustrGalaxy.Interaction;
using ClustrGalaxy.Scanning;
using ClustrGalaxy.World;
using UnityEngine;

namespace ClustrGalaxy.UI
{
    /// <summary>Thin IMGUI adapter: render a Core presentation and send intents.</summary>
    public sealed class ProbeHud : MonoBehaviour
    {
        private readonly ProbeHudPresenter presenter = new();
        private readonly List<GalaxyEntityId> draftMembers = new();
        private ObserverProbeController flight;
        private ProbeTargeting targeting;
        private SpatialSceneLoader loader;
        private GalaxyNavigationController navigation;
        private GalaxyModeState modes;
        private GalaxySurveyVisibilityController visibility;
        private SignalScanner scanner;
        private CommentThreadController threads;
        private SurveyTransitionOutcome latestOutcome;
        private GUIStyle titleStyle;
        private GUIStyle bodyStyle;
        private Texture2D panelTexture;
        private string draftName = string.Empty;
        private string draftReason = string.Empty;
        private string savedPersonalId = string.Empty;

        public void Initialize(ObserverProbeController flightController, ProbeTargeting targetController,
            SpatialSceneLoader spatialSceneLoader, GalaxyNavigationController navigationController,
            GalaxyModeState galaxyModeState, GalaxySurveyVisibilityController visibilityController,
            SignalScanner signalScanner = null, CommentThreadController commentThreadInspection = null)
        {
            flight = flightController; targeting = targetController; loader = spatialSceneLoader;
            navigation = navigationController; modes = galaxyModeState; visibility = visibilityController;
            scanner = signalScanner; threads = commentThreadInspection;
        }

        public void BindSurveyOutcomes(GalaxySurveyVisibilityController value)
        {
            if (visibility != null) visibility.SurveyTransitionCommitted -= OnSurveyOutcome;
            visibility = value;
            if (visibility != null) visibility.SurveyTransitionCommitted += OnSurveyOutcome;
        }

        private void OnGUI()
        {
            EnsureStyles();
            var presentation = presenter.Present(ReadModel());
            var panel = new Rect(18, 18, 390, Mathf.Min(Screen.height - 36, 900));
            GUI.DrawTexture(panel, panelTexture);
            GUILayout.BeginArea(new Rect(panel.x + 14, panel.y + 10, panel.width - 28, panel.height - 20));
            GUILayout.Label("CLUSTR // OBSERVER PROBE", titleStyle);
            foreach (var line in presentation.Lines) GUILayout.Label(line, bodyStyle);
            foreach (var row in presentation.ThreadRows)
                GUILayout.Label($"{new string(' ', row.Depth * 2)}↳ {row.Author}  ▲{row.Score}  ↩{row.Replies} — {row.Excerpt}", bodyStyle);
            RenderActions(presentation.Actions);
            GUILayout.EndArea();
        }

        private ProbeHudReadModel ReadModel()
        {
            var target = targeting?.Target;
            var state = visibility?.SurveySession?.State;
            var threadRows = threads?.DisplayedModel?.Presentation().Rows.Select(row =>
                new ProbeHudThreadRow(row.Entry.Depth, row.Entry.Author, row.Entry.Score, row.Entry.ReplyCount,
                    row.Excerpt, row.Entry.HasMore)).ToArray() ?? Array.Empty<ProbeHudThreadRow>();
            return new ProbeHudReadModel
            {
                Speed = flight?.Speed ?? 0f,
                FlightMode = modes?.FlightMode.ToString().ToUpperInvariant() ?? "ASSIST",
                Survey = modes?.WorldMode == GalaxyWorldMode.Survey,
                RequiresSurveyStart = visibility?.RequiresStartingSystemChoice == true,
                DataStatus = loader?.StatusText ?? "LOCAL DEMO",
                NavigationLevel = navigation?.Level.ToString().ToUpperInvariant() ?? "FLIGHT",
                NavigationStatus = navigation?.TravelStatus ?? string.Empty,
                TargetName = target?.DisplayName ?? string.Empty,
                TargetKind = target?.Kind.ToString().ToUpperInvariant() ?? string.Empty,
                TargetId = target?.StableId ?? string.Empty,
                TargetKnown = target != null && visibility?.IsKnown(target) == true,
                TargetIdentified = target != null && visibility?.IsIdentified(target) == true,
                TargetNavigable = target != null && navigation?.CanTravelTo(target) == true,
                ScanAvailable = target != null && scanner != null && visibility?.IsKnown(target) == true,
                ScanCharge = scanner?.Charge01 ?? 0f,
                ScannerLevel = state?.ScannerLevel ?? 1,
                Objective = state == null ? string.Empty : $"{state.CompletedContracts.Count}/4 CONTRACTS",
                Notification = latestOutcome?.Notifications.LastOrDefault() ?? string.Empty,
                QueueStatus = latestOutcome?.QueueStatus ?? string.Empty,
                HasSavedPersonalConstellation = !string.IsNullOrWhiteSpace(savedPersonalId),
                AwaitingQueueConfirmation = !string.IsNullOrWhiteSpace(state?.PendingQueueId),
                ThreadRows = threadRows
            };
        }

        private void RenderActions(IReadOnlyList<ProbeHudAction> actions)
        {
            foreach (var action in actions)
            {
                if (action == ProbeHudAction.ChooseStartingSystem)
                {
                    foreach (var system in visibility.LoadedStartingSystems())
                        if (GUILayout.Button("START AT " + system.DisplayName)) visibility.StartAt(system.StableId);
                    continue;
                }
                if (!GUILayout.Button(ActionLabel(action))) continue;
                Dispatch(action);
            }
            if (modes?.WorldMode == GalaxyWorldMode.Survey && visibility?.SurveySession != null)
                RenderDraftFields();
        }

        private void Dispatch(ProbeHudAction action)
        {
            var target = targeting?.Target;
            switch (action)
            {
                case ProbeHudAction.EnterSurvey: visibility?.EnterSurvey(); break;
                case ProbeHudAction.ReturnToExplore: visibility?.ReturnToExplore(); break;
                case ProbeHudAction.Scan: scanner?.BeginScan(); break;
                case ProbeHudAction.FastTravel: navigation?.TryFastTravel(target); break;
                case ProbeHudAction.PlotAutopilot: navigation?.PlotAutopilot(target); break;
                case ProbeHudAction.TrackMastery: navigation?.TrackMasteryDestination(target); break;
                case ProbeHudAction.AddConstellationMember:
                    if (target != null && draftMembers.Count < 12 && !draftMembers.Contains((GalaxyEntityId)target.StableId))
                        draftMembers.Add((GalaxyEntityId)target.StableId);
                    break;
                case ProbeHudAction.SavePersonalConstellation: SaveDraft(); break;
                case ProbeHudAction.RequestLocalQueue: Apply(new RequestLocalQueueIntent(IntentId("queue-request"),
                    visibility.SurveySession.State.Snapshot, DateTime.UtcNow, savedPersonalId)); break;
                case ProbeHudAction.ConfirmLocalQueue: Apply(new ConfirmLocalQueueIntent(IntentId("queue-confirm"),
                    visibility.SurveySession.State.Snapshot, DateTime.UtcNow)); break;
                case ProbeHudAction.CancelLocalQueue: Apply(new CancelLocalQueueIntent(IntentId("queue-cancel"),
                    visibility.SurveySession.State.Snapshot, DateTime.UtcNow)); break;
            }
        }

        private void RenderDraftFields()
        {
            GUILayout.Label($"PERSONAL CONSTELLATION  {draftMembers.Count}/12", titleStyle);
            foreach (var member in draftMembers.ToArray())
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(member.Value, bodyStyle);
                if (GUILayout.Button("REMOVE", GUILayout.Width(72))) draftMembers.Remove(member);
                GUILayout.EndHorizontal();
            }
            GUILayout.Label("NAME", bodyStyle);
            draftName = GUILayout.TextField(draftName, 60);
            GUILayout.Label("REASON", bodyStyle);
            draftReason = GUILayout.TextArea(draftReason, 500, GUILayout.Height(45));
        }

        private void SaveDraft()
        {
            var session = visibility?.SurveySession;
            if (session == null) return;
            var outcome = Apply(new SavePersonalConstellationIntent(IntentId("personal-save"), session.State.Snapshot,
                DateTime.UtcNow, draftName, draftReason, draftMembers));
            if (outcome?.Succeeded == true) savedPersonalId = outcome.PersonalConstellationId;
        }

        private SurveyTransitionOutcome Apply(SurveyIntent intent) => visibility?.ApplySurveyIntent(intent);
        private static string IntentId(string prefix) => prefix + ":" + Guid.NewGuid().ToString("N");
        private static string ActionLabel(ProbeHudAction action) => action.ToString()
            .Replace("PersonalConstellation", " Personal Constellation").Replace("LocalQueue", " Local Queue")
            .Replace("Survey", " Survey").Replace("Autopilot", " Autopilot").ToUpperInvariant();

        private void OnSurveyOutcome(SurveyTransitionOutcome outcome)
        {
            latestOutcome = outcome;
            if (outcome.RevealedEntities.Count > 0 || outcome.RevealedRoutes.Count > 0)
                GetComponent<GalaxyPerformanceMonitor>()?.RecordScanEvent();
        }

        private void EnsureStyles()
        {
            if (titleStyle != null) return;
            panelTexture = new Texture2D(1, 1);
            panelTexture.SetPixel(0, 0, new Color(0.015f, 0.03f, 0.045f, 0.88f));
            panelTexture.Apply();
            titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.72f, 1f, 0.55f) } };
            bodyStyle = new GUIStyle(GUI.skin.label) { fontSize = 13, wordWrap = true,
                normal = { textColor = new Color(0.78f, 0.9f, 0.94f) } };
        }

        private void OnDestroy()
        {
            if (visibility != null) visibility.SurveyTransitionCommitted -= OnSurveyOutcome;
            if (panelTexture != null) Destroy(panelTexture);
        }
    }
}
