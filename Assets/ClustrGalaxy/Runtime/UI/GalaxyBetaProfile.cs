using System;
using System.Collections.Generic;

namespace ClustrGalaxy.UI
{
    [Serializable]
    public sealed class GalaxyBetaProfileReport
    {
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

        public static GalaxyBetaProfileVerdict Evaluate(GalaxyBetaProfileReport report)
        {
            if (report == null) throw new ArgumentNullException(nameof(report));
            var failures = new List<string>();
            if (report.p95_cpu_frame_ms > MaximumP95FrameMilliseconds)
                failures.Add($"CPU p95 {report.p95_cpu_frame_ms:0.00} ms exceeds 16.7 ms");
            if (!report.gpu_timing_available)
                failures.Add("GPU frame timing unavailable");
            else if (report.p95_gpu_frame_ms > MaximumP95FrameMilliseconds)
                failures.Add($"GPU p95 {report.p95_gpu_frame_ms:0.00} ms exceeds 16.7 ms");
            if (report.maximum_frame_stall_ms > MaximumStallMilliseconds)
                failures.Add($"Main-frame stall {report.maximum_frame_stall_ms:0.00} ms exceeds 50 ms");
            if (report.p95_gc_allocated_bytes_per_frame > 0)
                failures.Add($"Steady-state GC allocation p95 is {report.p95_gc_allocated_bytes_per_frame} B/frame");
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
