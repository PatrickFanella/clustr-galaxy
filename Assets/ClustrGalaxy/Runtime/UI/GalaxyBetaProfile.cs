using System;
using System.Collections.Generic;

namespace ClustrGalaxy.UI
{
    [Serializable]
    public sealed class GalaxyBetaProfileReport
    {
        public string frame_trace_file;
        public bool execution_counter_available;
        public string source_sha = string.Empty;
        public string build_id = string.Empty;
        public string scenario = string.Empty;
        public string graphics_api = string.Empty;
        public int target_frame_rate;
        public int max_queued_frames;
        public int v_sync_count;
        public string window_system = string.Empty;
        public string data_mode = string.Empty;
        public string gc_measurement_source = string.Empty;
        public string gpu_memory_measurement_source = string.Empty;
        public string player_memory_measurement_source = string.Empty;
        public int invalid_measured_samples;
        public int gpu_recorded_samples;
        public ulong[] missing_gpu_timestamps;
        public bool gpu_drain_complete;
        public bool timestamp_clock_compatible;
        public ulong capture_start_timestamp;
        public ulong capture_end_timestamp;
        public ulong first_gpu_timestamp;
        public ulong last_gpu_timestamp;
        public ulong gpu_cpu_timer_frequency;
        public bool warm_cache_available;
        public float warm_cache_overview_seconds;
        public string warm_cache_measurement_scope;
        public long cache_usage_bytes = -1;
        public bool moving_flight_verified;
        public float measured_flight_distance;
        public bool smoke_complete;
        public bool active_features_requested;
        public bool active_features_initialized;
        public int feature_frames;
        public int guidance_frames;
        public int ghost_frames;
        public int ghost_in_view_frames;
        public int audio_frames;
        public int celebration_frames;
        public int maximum_audio_voices;
        public int maximum_celebrations;
        public bool cpu_counter_available;
        public bool gc_counter_available;
        public bool memory_counter_available;
        public bool sample_overflow;
        public int sample_count;
        public int valid_gpu_samples;
        public float measured_seconds;
        public int ordinary_flight_samples;
        public int loading_samples;
        public int other_gameplay_samples;
        public float ordinary_flight_seconds;
        public float ordinary_maximum_frame_stall_ms;
        public long ordinary_peak_gc_bytes;
        public int resident_nodes;
        public int resident_routes;
        public bool preflight_passed;
        public string generated_at_utc = string.Empty;
        public string unity_version = string.Empty;
        public string operating_system = string.Empty;
        public string graphics_device = string.Empty;
        public int width;
        public int height;
        public int duration_seconds;
        public long revision_id;
        public long spatial_catalog_id;
        public float p95_cpu_frame_ms;
        public float p95_gpu_frame_ms;
        public float maximum_frame_stall_ms;
        public long peak_gc_allocated_bytes_per_frame;
        public long p95_gc_allocated_bytes_per_frame;
        public long peak_player_memory_bytes;
        public long peak_gpu_memory_bytes;
        public bool gpu_timing_available;
        public bool gpu_memory_available;
        public bool qualified;
        public string[] failures = Array.Empty<string>();
    }

    public readonly struct GalaxyBetaProfileVerdict
    {
        public GalaxyBetaProfileVerdict(bool qualified, string[] failures)
        {
            Qualified = qualified;
            Failures = failures ?? Array.Empty<string>();
        }

        public bool Qualified { get; }
        public string[] Failures { get; }
    }

    public static class GalaxyBetaProfile
    {
        public const float MaximumP95FrameMilliseconds = 16.7f;
        public const float MaximumStallMilliseconds = 50f;
        public const long MaximumPlayerMemoryBytes = 4L * 1024 * 1024 * 1024;
        public const long MaximumGpuMemoryBytes = 6L * 1024 * 1024 * 1024;

        public static GalaxyBetaProfileVerdict Evaluate(GalaxyBetaProfileReport report, bool preflight = false)
        {
            if (report == null) throw new ArgumentNullException(nameof(report));
            var failures = new List<string>();
            if (report.active_features_requested)
            {
                if (!report.active_features_initialized || report.feature_frames < (preflight ? 1500 : 30000) ||
                    report.guidance_frames != report.feature_frames || report.ghost_frames != report.feature_frames ||
                    report.ghost_in_view_frames < 100 || report.ghost_in_view_frames > report.feature_frames ||
                    report.audio_frames < 100 || report.audio_frames > report.feature_frames ||
                    report.celebration_frames < 100 || report.celebration_frames > report.feature_frames ||
                    report.maximum_audio_voices < 1 || report.maximum_audio_voices > 8 || report.maximum_celebrations != 3)
                    failures.Add("Required active-feature workload unavailable or incomplete");
            }
            if (report.width != 2560 || report.height != 1440) failures.Add("Resolution must be 2560x1440");
            if (!float.IsFinite(report.measured_seconds) || report.measured_seconds < (preflight ? 30 : 600)) failures.Add("Insufficient measured duration");
            if (!float.IsFinite(report.ordinary_flight_seconds) || !float.IsFinite(report.ordinary_maximum_frame_stall_ms) ||
                report.ordinary_maximum_frame_stall_ms < 0 || report.ordinary_peak_gc_bytes < 0 ||
                report.ordinary_flight_samples < 0 || report.loading_samples < 0 || report.other_gameplay_samples < 0)
                failures.Add("Invalid ordinary-flight measurements");
            if (report.ordinary_flight_samples < (preflight ? 1500 : 30000) || report.ordinary_flight_seconds < (preflight ? 30 : 600))
                failures.Add("Insufficient ordinary-flight measurements");
            if (report.ordinary_flight_samples + report.loading_samples + report.other_gameplay_samples != report.sample_count)
                failures.Add("Measurement intervals do not cover every frame");
            if (report.invalid_measured_samples != 0) failures.Add("Invalid measured frame counters");
            if (!report.moving_flight_verified || !float.IsFinite(report.measured_flight_distance) || report.measured_flight_distance <= 0)
                failures.Add("Moving-flight scenario did not execute");
            if (!report.warm_cache_available || !float.IsFinite(report.warm_cache_overview_seconds) ||
                report.warm_cache_overview_seconds < 0 || report.warm_cache_overview_seconds > 3)
                failures.Add("Verified warm cached overview unavailable within three seconds");
            if (report.cache_usage_bytes < 0 || report.cache_usage_bytes > 10L * 1024 * 1024 * 1024)
                failures.Add("Cache usage unavailable or above 10 GiB");
            if (!report.smoke_complete) failures.Add("Required scenario smoke did not complete");
            if (string.IsNullOrWhiteSpace(report.source_sha) || report.source_sha == "unrecorded" || report.source_sha.EndsWith("-working") || string.IsNullOrWhiteSpace(report.build_id)) failures.Add("Build provenance unavailable");
            if (report.sample_count < (preflight ? 1500 : 30000) || report.sample_overflow) failures.Add("Insufficient or overflowed samples");
            if (!report.cpu_counter_available || !report.gc_counter_available || !report.memory_counter_available) failures.Add("Required CPU/GC/memory counters unavailable");
            if (!report.gpu_drain_complete || !report.timestamp_clock_compatible || report.gpu_recorded_samples != report.sample_count ||
                report.gpu_cpu_timer_frequency == 0 || report.capture_end_timestamp <= report.capture_start_timestamp ||
                report.first_gpu_timestamp <= report.capture_start_timestamp || report.last_gpu_timestamp >= report.capture_end_timestamp ||
                report.last_gpu_timestamp <= report.first_gpu_timestamp)
                failures.Add("GPU capture window unavailable or incomplete");
            else if ((report.capture_end_timestamp - report.capture_start_timestamp) / (double)report.gpu_cpu_timer_frequency < (preflight ? 30 : 600))
                failures.Add("Insufficient GPU capture window duration");
            if (report.valid_gpu_samples != report.sample_count) failures.Add("Missing or invalid GPU timing samples");
            if (!float.IsFinite(report.p95_cpu_frame_ms) || report.p95_cpu_frame_ms <= 0 ||
                !float.IsFinite(report.p95_gpu_frame_ms) || report.p95_gpu_frame_ms <= 0 ||
                !float.IsFinite(report.maximum_frame_stall_ms)) failures.Add("Invalid timing samples");
            if (report.peak_player_memory_bytes <= 0 || report.peak_gpu_memory_bytes <= 0) failures.Add("Invalid memory measurements");
            if (report.p95_cpu_frame_ms >= MaximumP95FrameMilliseconds)
                failures.Add($"CPU p95 {report.p95_cpu_frame_ms:0.00} ms exceeds 16.7 ms");
            if (!report.gpu_timing_available)
                failures.Add("GPU frame timing unavailable");
            else if (report.p95_gpu_frame_ms >= MaximumP95FrameMilliseconds)
                failures.Add($"GPU p95 {report.p95_gpu_frame_ms:0.00} ms exceeds 16.7 ms");
            if (report.ordinary_maximum_frame_stall_ms > MaximumStallMilliseconds)
                failures.Add($"Ordinary-flight stall {report.ordinary_maximum_frame_stall_ms:0.00} ms exceeds 50 ms");
            if (report.ordinary_peak_gc_bytes > 0)
                failures.Add($"Ordinary-flight GC allocation peak is {report.ordinary_peak_gc_bytes} B/frame");
            if (report.peak_player_memory_bytes >= MaximumPlayerMemoryBytes)
                failures.Add("Player memory reached or exceeded 4 GiB");
            if (!report.gpu_memory_available)
                failures.Add("GPU memory usage unavailable");
            else if (report.peak_gpu_memory_bytes >= MaximumGpuMemoryBytes)
                failures.Add("GPU memory reached or exceeded 6 GiB");
            return new GalaxyBetaProfileVerdict(failures.Count == 0, failures.ToArray());
        }
    }
}
