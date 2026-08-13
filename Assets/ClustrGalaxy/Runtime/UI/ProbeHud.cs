using ClustrGalaxy.Data;
using ClustrGalaxy.Flight;
using ClustrGalaxy.Interaction;
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

        public void Initialize(
            ObserverProbeController flightController,
            ProbeTargeting targetController,
            SpatialSceneLoader spatialSceneLoader)
        {
            flight = flightController;
            targeting = targetController;
            dataLoader = spatialSceneLoader;
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
            var panel = new Rect(18, 18, 365, targeting?.Target == null ? 232 : 302);
            GUI.DrawTexture(panel, panelTexture);
            GUILayout.BeginArea(new Rect(panel.x + 14, panel.y + 10, panel.width - 28, panel.height - 20));
            GUILayout.Label("CLUSTR // OBSERVER PROBE", titleStyle);
            GUILayout.Label($"SPEED  {flight?.Speed ?? 0f:000.0}", bodyStyle);
            GUILayout.Label($"FLIGHT  {(flight != null && flight.GravityModeEnabled ? "GRAVITY / MOMENTUM" : "ASSISTED CRUISE")}", bodyStyle);
            GUILayout.Label($"DATA  {dataLoader?.StatusText ?? "LOCAL PROTOTYPE"}", bodyStyle);
            if (performance != null)
            {
                GUILayout.Label($"PERF  P95 {performance.P95FrameMilliseconds:0.0} ms  •  MEM {performance.AllocatedMemoryBytes / (1024f * 1024f):0} MB", bodyStyle);
            }
            GUILayout.Space(5);
            GUILayout.Label("WASD / left stick: strafe + thrust\nMouse / right stick: pitch + yaw\nSpace/Ctrl or triggers: vertical\nQ/E or bumpers: roll\nShift/L3: boost  •  X/B: brake  •  F/A: target + inspect\nM/Select: galaxy map  •  G or Y/Triangle: gravity flight", bodyStyle);
            if (targeting?.Target != null)
            {
                GUILayout.Space(8);
                GUILayout.Label($"TARGET  {targeting.Target.DisplayName}", titleStyle);
                GUILayout.Label($"{targeting.Target.EntityType}  •  value {targeting.Target.Value}\n{targeting.Target.StableId}", bodyStyle);
                var thread = targeting.Target.GetComponent<CommentThreadNode>();
                if (thread != null)
                {
                    GUILayout.Label($"THREAD  {thread.ReplyCount} replies expanded", bodyStyle);
                }
            }
            GUILayout.EndArea();

            var center = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            GUI.color = new Color(0.72f, 1f, 0.55f, 0.85f);
            GUI.DrawTexture(new Rect(center.x - 10, center.y - 1, 20, 2), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(center.x - 1, center.y - 10, 2, 20), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

        private void OnDestroy()
        {
            if (panelTexture != null)
            {
                Destroy(panelTexture);
            }
        }
    }
}
