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
        private ObserverProbeController flight = null!;
        private ProbeTargeting targeting = null!;
        private SpatialSceneLoader dataLoader = null!;
        private GUIStyle titleStyle = null!;
        private GUIStyle bodyStyle = null!;
        private Texture2D panelTexture = null!;
        private GalaxyPerformanceMonitor performance = null!;
        private GalaxyNavigationController navigation = null!;
        private GalaxyModeState modeState = null!;
        private GalaxySurveyVisibilityController discoveryVisibility = null!;
        private SignalScanner scanner = null!;
        private CommentThreadController threadInspection = null!;
        private Vector2 threadScrollPosition;
        private ThreadInspectionModel observedThreadModel = null!;
        private ConstellationTracker constellationTracker;
        private PersonalConstellationStore personalConstellations;
        private ConstellationSubmissionOutbox constellationOutbox;
        private PersonalConstellationDraft personalConstellationDraft;
        private SurveyProgression progression;
        private EasterEggTracker easterEggTracker;
        private InstancedStarfield starfield;
        private EasterEggDiscovery latestEasterEgg;
        private bool hasEasterEggTransmission;
        private string progressionNotification = string.Empty;
        private bool developerPerformanceVisible;
        private bool constellationAuthoringExpanded;
        private GalaxyBetaProfileRunner betaProfile;
        private Vector2 constellationMemberScrollPosition;

        public void Initialize(ObserverProbeController flightController, ProbeTargeting targetController,
            SpatialSceneLoader spatialSceneLoader, GalaxyNavigationController navigationController,
            GalaxyModeState galaxyModeState, GalaxySurveyVisibilityController visibilityController,
            SignalScanner signalScanner = null, CommentThreadController commentThreadInspection = null)
        {
            flight = flightController;
            targeting = targetController;
            dataLoader = spatialSceneLoader;
            navigation = navigationController;
            modeState = galaxyModeState;
            discoveryVisibility = visibilityController;
            scanner = signalScanner;
            if (threadInspection != null)
            {
                threadInspection.ModelChanged -= OnThreadModelChanged;
            }
            threadInspection = commentThreadInspection;
            if (threadInspection != null)
            {
                threadInspection.ModelChanged += OnThreadModelChanged;
            }
            performance = GetComponent<GalaxyPerformanceMonitor>();
            betaProfile = GetComponent<GalaxyBetaProfileRunner>();
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
            GUILayout.Label($"SPEED  {flight?.Speed ?? 0f:000.0}", bodyStyle);
            GUILayout.Label($"FLIGHT  {(flight != null && flight.GravityModeEnabled ? "GRAVITY / MOMENTUM" : "ASSISTED CRUISE")}", bodyStyle);
            GUILayout.Label($"WORLD  {FormatWorldMode(modeState?.WorldMode ?? GalaxyWorldMode.Explore)}", bodyStyle);
            GUILayout.Label($"PILOT  {FormatFlightMode(modeState?.FlightMode ?? FlightControlMode.PilotAssist)}", bodyStyle);
            GUILayout.Label($"DATA  {dataLoader?.StatusText ?? "LOCAL DEMO"}", bodyStyle);
            GUILayout.Label($"VIEW  {navigation?.Level.ToString().ToUpperInvariant() ?? "FLIGHT"}", bodyStyle);
            if (navigation != null && navigation.IsMapOpen)
            {
                GUILayout.Label($"MAP CURSOR  {navigation.MapHoverStatus}", bodyStyle);
            }
            if (modeState != null && modeState.WorldMode == GalaxyWorldMode.Survey && scanner != null)
            {
                GUILayout.Label($"SIGNAL  {FormatSignalState(scanner.State)}  CHARGE {scanner.Charge01 * 100f:000}%  STR {scanner.SignalStrength01 * 100f:000}%", bodyStyle);
                if (scanner.LastResult.AlreadyScanned)
                {
                    GUILayout.Label("ALREADY SURVEYED", titleStyle);
                }
                else if (scanner.LastResult.HasRevelations)
                {
                    GUILayout.Label($"REVEALED  {scanner.LastResult.RevealedEntityIds.Count} signal(s)  •  {scanner.LastResult.RevealedRouteIds.Count} route(s)", bodyStyle);
                }
            }
            RenderConstellationStatus();
            RenderPersonalConstellationAuthoring();
            RenderAuthoredDiscoveryTransmission();
            RenderSurveyObjectives();
            if (performance != null)
            {
                GUILayout.Label($"PERF  P95 {performance.P95FrameMilliseconds:0.0} ms  •  MEM {performance.AllocatedMemoryBytes / (1024f * 1024f):0} MB", bodyStyle);
                if (developerPerformanceVisible)
                {
                    GUILayout.Label($"DEV PERF  ACTIVE {performance.ActiveEntityCount}  •  POOLED {performance.PooledEntityCount}  •  VISIBLE {performance.VisibleNodeCount}  •  KNOWN {performance.KnownNodeCount}\nSCAN EVENTS {performance.ScanEventCount}  •  GC {performance.SteadyStateGcBytes} B/frame  •  MAIN {performance.MainThreadMilliseconds:0.00} ms", bodyStyle);
                }
            }
            if (betaProfile != null)
            {
                GUILayout.Label($"BETA PROFILE  {betaProfile.StatusText}", bodyStyle);
            }
            GUILayout.Space(5);
            GUILayout.Label("WASD / left stick: strafe + thrust\nMouse / right stick: pitch + yaw\nSpace/Ctrl or triggers: vertical\nQ/E or bumpers: roll\n[/] or d-pad: pilot mode  •  Shift/L3: boost  •  X/B: brake\nF/A: target + inspect  •  R/X: hold to scan  •  M/Select: galaxy map  •  V/d-pad up: Survey map  •  Enter/A: confirm Survey start\nT/d-pad down: fast travel  •  P/R3: plot autopilot\nG or Y/Triangle: gravity flight", bodyStyle);
            if (!string.IsNullOrWhiteSpace(navigation?.TravelStatus))
            {
                GUILayout.Label($"TRAVEL  {navigation.TravelStatus}", bodyStyle);
            }
            GUILayout.Space(5);
            if (modeState != null && modeState.WorldMode == GalaxyWorldMode.Explore)
            {
                if (GUILayout.Button("ENTER SURVEY // CHOOSE STARTING SYSTEM"))
                {
                    discoveryVisibility?.EnterSurvey();
                }
            }
            else if (modeState != null && modeState.WorldMode == GalaxyWorldMode.Survey && discoveryVisibility != null &&
                     discoveryVisibility.RequiresStartingSystemChoice)
            {
                GUILayout.Label("SURVEY START // choose one loaded subreddit", titleStyle);
                foreach (var system in discoveryVisibility.LoadedStartingSystems())
                {
                    if (GUILayout.Button(system.DisplayName))
                    {
                        discoveryVisibility.StartAt(system.StableId);
                    }
                }
            }
            else if (modeState != null && modeState.WorldMode == GalaxyWorldMode.Survey)
            {
                if (GUILayout.Button("RETURN TO EXPLORE"))
                {
                    discoveryVisibility?.ReturnToExplore();
                }
            }
            if (targeting?.Target != null)
            {
                GUILayout.Space(8);
                if (discoveryVisibility == null || discoveryVisibility.CanLabel(targeting.Target))
                {
                    GUILayout.Label($"TARGET  {targeting.Target.DisplayName}", titleStyle);
                    GUILayout.Label($"{targeting.Target.EntityType}  •  value {targeting.Target.Value}\n{targeting.Target.StableId}", bodyStyle);
                }
                else
                {
                    GUILayout.Label("TARGET  UNIDENTIFIED SIGNAL", titleStyle);
                    GUILayout.Label("Scan to identify this destination.", bodyStyle);
                }
                if (navigation != null)
                {
                    if (navigation.CanTravelTo(targeting.Target))
                    {
                        if (GUILayout.Button("FAST TRAVEL TO SYSTEM"))
                        {
                            navigation.TryFastTravel(targeting.Target);
                        }
                        if (GUILayout.Button("PLOT AUTOPILOT APPROACH"))
                        {
                            navigation.PlotAutopilot(targeting.Target);
                        }
                        if (modeState != null && modeState.FlightMode == FlightControlMode.PilotMastery &&
                            GUILayout.Button("TRACK MASTERY APPROACH"))
                        {
                            navigation.TrackMasteryDestination(targeting.Target);
                        }
                    }
                    else
                    {
                        GUILayout.Label("TRAVEL  INSPECT TARGETS AND UNKNOWN SIGNALS ARE NOT DESTINATIONS", bodyStyle);
                    }
                }
                RenderThreadInspection(threadInspection?.DisplayedModel);
            }
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
