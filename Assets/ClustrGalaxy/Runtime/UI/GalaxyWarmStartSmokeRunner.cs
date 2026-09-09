using System;
using System.Collections;
using System.IO;
using ClustrGalaxy.Bootstrap;
using ClustrGalaxy.Data;
using ClustrGalaxy.Flight;
using UnityEngine;

namespace ClustrGalaxy.UI
{
    /// <summary>Opt-in offline startup evidence. The parent measures process-to-rendered readiness.</summary>
    public sealed class GalaxyWarmStartSmokeRunner : MonoBehaviour
    {
        private SpatialSceneLoader loader;
        public void Initialize(SpatialSceneLoader value) => loader = value;
        [Serializable] private sealed class Result
        {
            public bool passed, offline, chart_visible;
            public string source_sha, build_id, data_mode, error;
            public long revision_id, catalog_id;
            public int width, height, resident_nodes;
            public float first_frame_seconds, scene_ready_seconds, engine_elapsed_seconds;
        }

        private IEnumerator Start()
        {
            var owner = GetComponent<GalaxySessionBehaviour>();
            var navigation = GetComponent<GalaxyNavigationController>();
            var result = new Result { source_sha = Application.version.Contains("+") ? Application.version.Split('+')[1] : "",
                build_id = Application.buildGUID, width = Screen.width, height = Screen.height,
                first_frame_seconds = Time.realtimeSinceStartup };
            if (GalaxyPrototypeBootstrap.IsDemo || loader == null || !loader.OfflineOnly)
            { Finish(result, "Cached startup requires an explicit offline, non-demo launch."); yield break; }
            var deadline = Time.realtimeSinceStartup + 15f;
            while (!loader.IsPinned && loader.DataMode != GalaxyDataMode.Failed && Time.realtimeSinceStartup < deadline)
                yield return null;
            if (!loader.IsPinned || !loader.IsOffline || owner?.Session == null ||
                owner.Registry.Entities.Count == 0 || navigation == null || !navigation.IsMapOpen ||
                loader.Scene == null || !string.IsNullOrEmpty(loader.Scene.scope) && loader.Scene.scope != "overview")
            { Finish(result, "Verified cached overview did not reach the visible chart: " + loader.StatusText); yield break; }
            result.scene_ready_seconds = Time.realtimeSinceStartup;
            while (!VisibleOverview(owner) && Time.realtimeSinceStartup < deadline) yield return null;
            if (!VisibleOverview(owner))
            { Finish(result, "The cached overview has no visible chart geometry or HUD."); yield break; }
            // Let both the world chart and retained HUD submit complete frames.
            yield return new WaitForEndOfFrame();
            yield return new WaitForEndOfFrame();
            result.offline = true; result.chart_visible = navigation.IsMapOpen;
            result.data_mode = loader.DataMode.ToString(); result.revision_id = loader.Manifest.revision_id;
            result.catalog_id = loader.Manifest.spatial_catalog_id; result.resident_nodes = owner.Registry.Entities.Count;
            Texture2D screenshot = null;
            try
            {
                screenshot = ScreenCapture.CaptureScreenshotAsTexture();
                var folder = Path.Combine(Application.persistentDataPath, "ClustrGalaxy", "warm-start");
                Directory.CreateDirectory(folder);
                File.WriteAllBytes(Path.Combine(folder, "overview.png"), screenshot.EncodeToPNG());
                result.passed = result.chart_visible && screenshot.width == result.width && screenshot.height == result.height;
            }
            catch (Exception exception) { result.error = exception.Message; }
            finally { if (screenshot != null) Destroy(screenshot); }
            Finish(result, result.passed ? null : result.error ?? "Rendered overview unavailable.");
        }

        private static bool VisibleOverview(GalaxySessionBehaviour owner)
        {
            var chart = owner.GetComponent<CockpitChartStation>();
            var hud = owner.GetComponent<ProbeHud>();
            var canvas = hud != null ? hud.GetComponentInChildren<Canvas>() : null;
            if (canvas == null || !canvas.isActiveAndEnabled || chart?.ChartCamera == null ||
                !chart.ChartCamera.isActiveAndEnabled || chart.ChartCamera.targetTexture == null ||
                !chart.ChartCamera.targetTexture.IsCreated()) return false;
            var planes = GeometryUtility.CalculateFrustumPlanes(chart.ChartCamera);
            foreach (var entity in owner.Registry.Entities)
                if (entity.gameObject.activeInHierarchy && entity.TryGetComponent<Renderer>(out var renderer) &&
                    renderer.enabled && GeometryUtility.TestPlanesAABB(planes, renderer.bounds)) return true;
            return false;
        }

        private static void Finish(Result result, string error)
        {
            result.error = error;
            result.engine_elapsed_seconds = Time.realtimeSinceStartup;
            try
            {
                var folder = Path.Combine(Application.persistentDataPath, "ClustrGalaxy", "warm-start");
                Directory.CreateDirectory(folder);
                File.WriteAllText(Path.Combine(folder, "summary.json"), JsonUtility.ToJson(result, true));
            }
            catch (Exception exception)
            { Debug.LogError("[ClustrGalaxy.WarmStart] Report write failed: " + exception.Message); Application.Quit(3); return; }
            if (result.passed) Debug.Log("[ClustrGalaxy.WarmStart] READY");
            else Debug.LogError("[ClustrGalaxy.WarmStart] FAILED: " + error);
            Application.Quit(result.passed ? 0 : 2);
        }
    }
}
