using System;
using System.Collections;
using System.IO;
using ClustrGalaxy.Data;
using UnityEngine;

namespace ClustrGalaxy.UI
{
    /// <summary>Opt-in Linux beta evidence capture; inert during normal play.</summary>
    public sealed class GalaxyBetaProfileRunner : MonoBehaviour
    {
        private SpatialSceneLoader loader = null!;
        private GalaxyPerformanceMonitor monitor = null!;
        private int durationSeconds;
        public string StatusText { get; private set; } = "WAITING";

        public static string ReportPath => Path.Combine(Application.persistentDataPath,
            "ClustrGalaxy", "reports", "latest-beta-profile.json");

        public void Initialize(SpatialSceneLoader sceneLoader, GalaxyPerformanceMonitor performanceMonitor,
            int requestedDurationSeconds)
        {
            loader = sceneLoader ?? throw new ArgumentNullException(nameof(sceneLoader));
            monitor = performanceMonitor ?? throw new ArgumentNullException(nameof(performanceMonitor));
            durationSeconds = Mathf.Clamp(requestedDurationSeconds, 30, 3600);
            StartCoroutine(Capture());
        }

        private IEnumerator Capture()
        {
            Debug.Log($"[ClustrGalaxy.Profile] Waiting for data, then sampling {durationSeconds}s at " +
                      $"{Screen.width}x{Screen.height}.");
            var waitDeadline = Time.realtimeSinceStartup + 30f;
            while (loader.DataMode == GalaxyDataMode.Connecting && Time.realtimeSinceStartup < waitDeadline)
                yield return null;

            StatusText = "WARMING UP";
            yield return new WaitForSecondsRealtime(10f);
            monitor.ResetSamplingWindow();
            StatusText = $"SAMPLING {durationSeconds}s";
            var started = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - started < durationSeconds)
                yield return null;

            var report = BuildReport();
            var verdict = GalaxyBetaProfile.Evaluate(report);
            report.qualified = verdict.Qualified;
            report.failures = verdict.Failures;
            var path = ReportPath;
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var temporary = path + ".tmp";
            File.WriteAllText(temporary, JsonUtility.ToJson(report, prettyPrint: true));
            if (File.Exists(path)) File.Delete(path);
            File.Move(temporary, path);
            if (verdict.Qualified)
            {
                StatusText = "QUALIFIED";
                Debug.Log($"[ClustrGalaxy.Profile] QUALIFIED report={path}");
            }
            else
            {
                StatusText = "NOT QUALIFIED";
                Debug.LogWarning($"[ClustrGalaxy.Profile] NOT QUALIFIED: {string.Join("; ", verdict.Failures)} report={path}");
            }
#if !UNITY_EDITOR
            Application.Quit(0);
#endif
        }

        private GalaxyBetaProfileReport BuildReport() => new()
        {
            generated_at_utc = DateTime.UtcNow.ToString("O"),
            unity_version = Application.unityVersion,
            operating_system = SystemInfo.operatingSystem,
            graphics_device = SystemInfo.graphicsDeviceName,
            width = Screen.width,
            height = Screen.height,
            duration_seconds = durationSeconds,
            revision_id = loader.Manifest?.revision_id ?? 0,
            spatial_catalog_id = loader.Manifest?.spatial_catalog_id ?? 0,
            p95_cpu_frame_ms = monitor.P95CpuMilliseconds,
            p95_gpu_frame_ms = monitor.P95GpuMilliseconds,
            maximum_frame_stall_ms = monitor.MaximumFrameMilliseconds,
            peak_gc_allocated_bytes_per_frame = monitor.PeakGcAllocatedBytes,
            p95_gc_allocated_bytes_per_frame = monitor.P95GcAllocatedBytes,
            peak_player_memory_bytes = monitor.PeakAllocatedMemoryBytes,
            peak_gpu_memory_bytes = monitor.PeakGpuMemoryBytes,
            gpu_timing_available = monitor.GpuTimingAvailable,
            gpu_memory_available = monitor.GpuMemoryAvailable
        };
    }
}
