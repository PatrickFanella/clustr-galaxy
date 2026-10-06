using System;
using System.Collections;
using System.IO;
using ClustrGalaxy.Data;
using ClustrGalaxy.Flight;
using ClustrGalaxy.Interaction;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using System.Threading.Tasks;

namespace ClustrGalaxy.UI
{
    /// <summary>Opt-in Linux beta evidence capture; inert during normal play.</summary>
    public sealed class GalaxyBetaProfileRunner : MonoBehaviour
    {
        private SpatialSceneLoader loader = null!;
        private GalaxyPerformanceMonitor monitor = null!;
        private int durationSeconds;
        private float measuredSeconds;
        private bool warmCacheAvailable;
        private float warmCacheSeconds;
        private long cacheUsageBytes = -1;
        private Gamepad controller;
        private InputSettings.BackgroundBehavior previousBackground;
        private float flightDistance;
        private bool movingFlightVerified;
        private ActiveFeatureProfileScenario activeFeatures;
        public string StatusText { get; private set; } = "WAITING";

        public static string ReportPath => Path.Combine(Application.persistentDataPath,
            "ClustrGalaxy", "reports", "latest-beta-profile.json");

        public void Initialize(SpatialSceneLoader sceneLoader, GalaxyPerformanceMonitor performanceMonitor,
            int requestedDurationSeconds)
        {
            loader = sceneLoader ?? throw new ArgumentNullException(nameof(sceneLoader));
            monitor = performanceMonitor ?? throw new ArgumentNullException(nameof(performanceMonitor));
            durationSeconds = Mathf.Clamp(requestedDurationSeconds, 30, 3600);
            Application.targetFrameRate = 120;
            StartCoroutine(Capture());
        }

        private IEnumerator Capture()
        {
            Debug.Log($"[ClustrGalaxy.Profile] Waiting for data, then sampling {durationSeconds}s at " +
                      $"{Screen.width}x{Screen.height}.");
            var smoke = loader.GetComponent<GalaxyLiveDataSmokeRunner>();
            var waitDeadline = Time.realtimeSinceStartup + 180f;
            while (smoke != null && !smoke.Completed && !smoke.Failed && Time.realtimeSinceStartup < waitDeadline)
                yield return null;
            if (smoke == null || !smoke.Completed)
            {
                var failedReport = BuildReport();
                failedReport.failures = new[] { "Required hierarchy smoke did not complete; no performance samples collected." };
                var written = WriteReport(failedReport, out _);
                Debug.LogError("[ClustrGalaxy.Profile] Required hierarchy smoke did not complete.");
#if !UNITY_EDITOR
                Application.Quit(written ? 2 : 3);
#endif
                yield break;
            }
            GetComponent<CommentThreadController>()?.ClearInspection();
            GetComponent<ProbeTargeting>()?.SelectTarget(null);
            GetComponent<GalaxyNavigationController>()?.SetMapOpen(false);
            var cacheCheck = CheckWarmCache();
            while (!cacheCheck.IsCompleted) yield return null;
            previousBackground = InputSystem.settings.backgroundBehavior;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            controller = InputSystem.AddDevice<Gamepad>();
            InputSystem.QueueStateEvent(controller, new GamepadState { rightTrigger = .4f });
            if (Array.Exists(Environment.GetCommandLineArgs(), value => value == "--clustr-profile-active-features"))
            {
                activeFeatures = gameObject.AddComponent<ActiveFeatureProfileScenario>();
                try { activeFeatures.Initialize(durationSeconds); }
                catch (Exception exception)
                {
                    var failedReport = BuildReport();
                    activeFeatures.CopyTo(failedReport);
                    failedReport.failures = new[] { "Active-feature initialization failed: " + exception.Message };
                    var written = WriteReport(failedReport, out _);
                    Debug.LogError("[ClustrGalaxy.Profile] " + failedReport.failures[0]);
#if !UNITY_EDITOR
                    Application.Quit(written ? 2 : 3);
#endif
                    yield break;
                }
                InputSystem.QueueStateEvent(controller, new GamepadState { rightTrigger = .4f, leftStick = Vector2.right });
            }
            monitor.PrepareCapture(durationSeconds);
            StatusText = "WARMING UP";
            yield return new WaitForSecondsRealtime(10f);
            monitor.BeginCapture();
            StatusText = "SAMPLING";
            var started = Time.realtimeSinceStartup;
            var previousPosition = transform.position;
            var previousLeg = -1;
            while ((Time.realtimeSinceStartup - started < durationSeconds || monitor.CapturedSeconds < durationSeconds) && !monitor.SampleOverflow)
            {
                var leg = (int)((Time.realtimeSinceStartup - started) / 5f);
                if (activeFeatures == null && leg != previousLeg)
                {
                    InputSystem.QueueStateEvent(controller, new GamepadState { rightTrigger = .4f, leftStick = new Vector2(leg % 2 == 0 ? .2f : -.2f, 0) });
                    previousLeg = leg;
                }
                activeFeatures?.Sample();
                flightDistance += Vector3.Distance(transform.position, previousPosition);
                previousPosition = transform.position;
                yield return null;
            }
            movingFlightVerified = flightDistance >= durationSeconds;
            measuredSeconds = Time.realtimeSinceStartup - started;
            monitor.EndCapture();
            InputSystem.QueueStateEvent(controller, new GamepadState());
            var drainDeadline = Time.realtimeSinceStartup + 2f;
            while (!monitor.GpuDrainComplete && Time.realtimeSinceStartup < drainDeadline) yield return null;
            monitor.FinishCapture();
            var report = BuildReport();
            activeFeatures?.CopyTo(report);
            var preflight = durationSeconds < 600;
            var verdict = GalaxyBetaProfile.Evaluate(report, preflight);
            report.qualified = !preflight && verdict.Qualified;
            report.preflight_passed = preflight && verdict.Qualified;
            report.failures = verdict.Failures;
            if (!WriteReport(report, out var path))
            {
#if !UNITY_EDITOR
                Application.Quit(3);
#endif
                yield break;
            }
            if (verdict.Qualified)
            {
                StatusText = preflight ? "PREFLIGHT PASSED — RELEASE UNQUALIFIED" : "QUALIFIED";
                Debug.Log($"[ClustrGalaxy.Profile] {StatusText} report={path}");
            }
            else
            {
                StatusText = "NOT QUALIFIED";
                Debug.LogWarning($"[ClustrGalaxy.Profile] NOT QUALIFIED: {string.Join("; ", verdict.Failures)} report={path}");
            }
#if !UNITY_EDITOR
            Application.Quit(verdict.Qualified ? 0 : 2);
#endif
        }

        private async Task CheckWarmCache()
        {
            try
            {
                var started = Time.realtimeSinceStartup;
                var source = new SpatialSceneSnapshotSource(loader.ApiBase, true, storageNamespace:
                    loader.DataMode == GalaxyDataMode.DeterministicFixture ? "fixture" : "pinned");
                var outcome = await source.OpenAsync(default);
                warmCacheSeconds = Time.realtimeSinceStartup - started;
                warmCacheAvailable = outcome.Succeeded && outcome.Manifest.SurveySnapshot == loader.Session.Snapshots.Manifest.SurveySnapshot;
                cacheUsageBytes = source.CacheUsageBytes;
            }
            catch (Exception exception)
            {
                warmCacheAvailable = false;
                Debug.LogWarning("[ClustrGalaxy.Profile] Warm-cache check failed: " + exception.Message);
            }
        }

        private void OnDestroy()
        {
            if (controller == null) return;
            InputSystem.RemoveDevice(controller);
            InputSystem.settings.backgroundBehavior = previousBackground;
        }

        private bool WriteReport(GalaxyBetaProfileReport report, out string path)
        {
            path = null;
            try
            {
                var directory = Path.GetDirectoryName(ReportPath);
                Directory.CreateDirectory(directory);
                // Initialization failures still need a report, before capture storage exists.
                if (monitor.CaptureFinished)
                {
                    var frameTrace = "frames-" + DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffffffZ") + "-" + Guid.NewGuid().ToString("N") + ".csv";
                    monitor.WriteFrameEvidence(Path.Combine(directory, frameTrace));
                    report.frame_trace_file = frameTrace;
                    report.execution_counter_available = monitor.ExecutionCountersAvailable;
                }
                path = ClustrGalaxy.Core.Diagnostics.QualificationReportStore.Write(
                    Path.GetDirectoryName(ReportPath), JsonUtility.ToJson(report, prettyPrint: true));
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogError("[ClustrGalaxy.Profile] Report write failed: " + exception.Message);
                return false;
            }
        }

        private GalaxyBetaProfileReport BuildReport() => new()
        {
            source_sha = Application.version.Contains("+") ? Application.version.Split('+')[1] : string.Empty,
            build_id = Application.buildGUID,
            graphics_api = SystemInfo.graphicsDeviceType.ToString(),
            target_frame_rate = Application.targetFrameRate,
            max_queued_frames = QualitySettings.maxQueuedFrames,
            v_sync_count = QualitySettings.vSyncCount,
            window_system = Array.Exists(Environment.GetCommandLineArgs(), value => value == "-force-wayland") ? "wayland" : "x11",
            scenario = (loader.DataMode == GalaxyDataMode.DeterministicFixture ? "fixture-hierarchy" : "live-hierarchy") + (activeFeatures == null ? "" : "-active-features"),
            data_mode = loader.DataMode.ToString(),
            gc_measurement_source = monitor.GcMeasurementSource,
            gpu_memory_measurement_source = monitor.GpuMemoryMeasurementSource,
            player_memory_measurement_source = monitor.PlayerMemoryMeasurementSource,
            invalid_measured_samples = monitor.InvalidMeasuredSamples,
            gpu_recorded_samples = monitor.RecordedGpuSamples,
            missing_gpu_timestamps = monitor.MissingGpuTimestamps,
            gpu_drain_complete = monitor.GpuDrainComplete,
            timestamp_clock_compatible = monitor.TimestampClockCompatible,
            capture_start_timestamp = monitor.CaptureStartTimestamp,
            capture_end_timestamp = monitor.CaptureEndTimestamp,
            first_gpu_timestamp = monitor.FirstGpuTimestamp,
            last_gpu_timestamp = monitor.LastGpuTimestamp,
            gpu_cpu_timer_frequency = FrameTimingManager.GetCpuTimerFrequency(),
            warm_cache_available = warmCacheAvailable,
            warm_cache_overview_seconds = warmCacheSeconds,
            warm_cache_measurement_scope = "verified cached-source open only; process-to-rendered startup is a separate warm-start gate",
            cache_usage_bytes = cacheUsageBytes,
            moving_flight_verified = movingFlightVerified,
            measured_flight_distance = flightDistance,
            smoke_complete = loader.GetComponent<GalaxyLiveDataSmokeRunner>()?.Completed == true,
            measured_seconds = measuredSeconds,
            ordinary_flight_samples = monitor.OrdinaryFlightSamples,
            loading_samples = monitor.LoadingSamples,
            other_gameplay_samples = monitor.OtherGameplaySamples,
            ordinary_flight_seconds = monitor.OrdinaryFlightSeconds,
            ordinary_maximum_frame_stall_ms = monitor.OrdinaryMaximumFrameMilliseconds,
            ordinary_peak_gc_bytes = monitor.OrdinaryPeakGcBytes,
            resident_nodes = loader.Session?.Resident.LoadedEntities.Count ?? 0,
            resident_routes = loader.Session?.Resident.LoadedRoutes.Count ?? 0,
            sample_count = monitor.CollectedSampleCount,
            valid_gpu_samples = monitor.ValidGpuSamples,
            sample_overflow = monitor.SampleOverflow,
            cpu_counter_available = monitor.CpuCounterAvailable,
            gc_counter_available = monitor.GcCounterAvailable,
            memory_counter_available = monitor.MemoryCounterAvailable,
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
