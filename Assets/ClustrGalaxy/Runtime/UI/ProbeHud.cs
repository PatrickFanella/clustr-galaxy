using ClustrGalaxy.Data;
using ClustrGalaxy.Flight;
using ClustrGalaxy.Gameplay;
using ClustrGalaxy.Interaction;
using ClustrGalaxy.Scanning;
using ClustrGalaxy.World;
using UnityEngine;

namespace ClustrGalaxy.UI
{
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

        public void Initialize(
            ObserverProbeController flightController,
            ProbeTargeting targetController,
            SpatialSceneLoader spatialSceneLoader,
            GalaxyNavigationController navigationController,
            GalaxyModeState galaxyModeState,
            GalaxySurveyVisibilityController visibilityController,
            SignalScanner signalScanner = null,
            CommentThreadController commentThreadInspection = null)
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
        }

        private void EnsureStyles()
        {
            if (titleStyle != null)
            {
                return;
            }

            panelTexture = new Texture2D(1, 1);
            panelTexture.SetPixel(0, 0, new Color(0.015f, 0.03f, 0.045f, 0.88f));
            panelTexture.Apply();
            titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 18,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.72f, 1f, 0.55f) }
            };
            bodyStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                wordWrap = true,
                normal = { textColor = new Color(0.78f, 0.9f, 0.94f) }
            };
        }

        private void OnGUI()
        {
            EnsureStyles();
            var panel = new Rect(18, 18, 365, targeting?.Target == null ? 330 : 680);
            GUI.DrawTexture(panel, panelTexture);
            GUILayout.BeginArea(new Rect(panel.x + 14, panel.y + 10, panel.width - 28, panel.height - 20));
            GUILayout.Label("CLUSTR // OBSERVER PROBE", titleStyle);
            GUILayout.Label($"SPEED  {flight?.Speed ?? 0f:000.0}", bodyStyle);
            GUILayout.Label($"FLIGHT  {(flight != null && flight.GravityModeEnabled ? "GRAVITY / MOMENTUM" : "ASSISTED CRUISE")}", bodyStyle);
            GUILayout.Label($"WORLD  {FormatWorldMode(modeState?.WorldMode ?? GalaxyWorldMode.Explore)}", bodyStyle);
            GUILayout.Label($"PILOT  {FormatFlightMode(modeState?.FlightMode ?? FlightControlMode.PilotAssist)}", bodyStyle);
            GUILayout.Label($"DATA  {dataLoader?.StatusText ?? "LOCAL DEMO"}", bodyStyle);
            GUILayout.Label($"VIEW  {navigation?.Level.ToString().ToUpperInvariant() ?? "FLIGHT"}", bodyStyle);
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
            if (performance != null)
            {
                GUILayout.Label($"PERF  P95 {performance.P95FrameMilliseconds:0.0} ms  •  MEM {performance.AllocatedMemoryBytes / (1024f * 1024f):0} MB", bodyStyle);
            }
            GUILayout.Space(5);
            GUILayout.Label("WASD / left stick: strafe + thrust\nMouse / right stick: pitch + yaw\nSpace/Ctrl or triggers: vertical\nQ/E or bumpers: roll\n[/] or d-pad: pilot mode  •  Shift/L3: boost  •  X/B: brake\nF/A: target + inspect  •  R/X: hold to scan  •  M/Select: galaxy map  •  V/d-pad up: Survey map  •  Enter/A: confirm Survey start\nG or Y/Triangle: gravity flight", bodyStyle);
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
                RenderThreadInspection(threadInspection?.DisplayedModel);
            }
            GUILayout.EndArea();

            var center = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            GUI.color = new Color(0.72f, 1f, 0.55f, 0.85f);
            GUI.DrawTexture(new Rect(center.x - 10, center.y - 1, 20, 2), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(center.x - 1, center.y - 10, 2, 20), Texture2D.whiteTexture);
            GUI.color = Color.white;

            if (navigation != null && navigation.IsMapOpen)
            {
                var cursor = navigation.MapCursorPosition;
                var point = new Vector2(cursor.x * Screen.width, (1f - cursor.y) * Screen.height);
                GUI.color = new Color(1f, 0.8f, 0.2f, 0.95f);
                GUI.DrawTexture(new Rect(point.x - 14, point.y - 2, 28, 4), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(point.x - 2, point.y - 14, 4, 28), Texture2D.whiteTexture);
                GUI.color = Color.white;
            }
        }

        private void OnDestroy()
        {
            if (threadInspection != null)
            {
                threadInspection.ModelChanged -= OnThreadModelChanged;
            }
            if (panelTexture != null)
            {
                Destroy(panelTexture);
            }
        }

        private void RenderThreadInspection(ThreadInspectionModel model)
        {
            if (model != observedThreadModel)
            {
                observedThreadModel = model;
                threadScrollPosition = Vector2.zero;
            }
            if (model == null || model.NodeCount == 0)
            {
                return;
            }

            var presentation = model.Presentation();
            GUILayout.Space(6);
            var partial = presentation.IsPartial ? "  •  PARTIAL" : string.Empty;
            GUILayout.Label($"THREAD  SHOWING {presentation.Rows.Count} OF {presentation.TotalNodeCount}{partial}", titleStyle);
            threadScrollPosition = GUILayout.BeginScrollView(threadScrollPosition, GUILayout.Height(225f));
            foreach (var row in presentation.Rows)
            {
                RenderThreadRow(row);
            }
            GUILayout.EndScrollView();
        }

        private void RenderThreadRow(ThreadInspectionPresentationRow row)
        {
            var entry = row.Entry;
            var indent = new string(' ', Mathf.Max(0, entry.Depth - 1) * 2);
            var author = string.IsNullOrWhiteSpace(entry.Author) ? "unknown" : entry.Author;
            var suffix = entry.HasMore ? "  •  MORE" : string.Empty;
            var excerpt = string.IsNullOrWhiteSpace(row.Excerpt) ? string.Empty : $" — {row.Excerpt}";
            GUILayout.Label($"{indent}↳ {author}  ▲{entry.Score}  ↩{entry.ReplyCount}{suffix}{excerpt}", bodyStyle);
        }

        private void OnThreadModelChanged(ThreadInspectionModel model)
        {
            observedThreadModel = model;
            threadScrollPosition = Vector2.zero;
        }

        private static string FormatWorldMode(GalaxyWorldMode mode)
        {
            return mode == GalaxyWorldMode.Survey ? "SURVEY" : "EXPLORE";
        }

        private static string FormatFlightMode(FlightControlMode mode)
        {
            return mode switch
            {
                FlightControlMode.Autopilot => "AUTO",
                FlightControlMode.PilotMastery => "MASTERY",
                _ => "ASSIST"
            };
        }

        private static string FormatSignalState(SignalScanState state) => state switch
        {
            SignalScanState.Charging => "CHARGING",
            SignalScanState.OutOfRange => "OUT OF RANGE",
            SignalScanState.Complete => "COMPLETE",
            SignalScanState.NoSignal => "NO SIGNAL",
            _ => "READY"
        };
    }
}
