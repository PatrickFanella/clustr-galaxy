using System;
using System.Collections.Generic;
using UnityEngine;

namespace ClustrGalaxy.UI
{
    /// <summary>Separate UI diagnostics, never a release-qualification verdict.</summary>
    [Serializable]
    public sealed class InteractionPerformanceReport
    {
        public string source_sha, build_id, scenario, graphics_api, data_mode;
        public int width, height, sample_count, valid_gpu_samples, ordinary_flight_samples, other_gameplay_samples;
        public double measured_seconds;
        public float cpu_p95_ms, gpu_p95_ms, maximum_frame_ms;
        public long peak_gc_bytes, peak_player_memory_bytes, peak_gpu_memory_bytes;
        public bool cpu_available, gc_available, memory_available, gpu_available, gpu_memory_available;
        public bool gpu_complete, overflow, functional_passed, measurements_passed;
        public int invalid_samples;
        public int interaction_steps, palette_mask, instrument_mask;
        public string gc_source, player_memory_source, gpu_memory_source;
        public string[] failures = Array.Empty<string>();

        public string[] Evaluate()
        {
            var errors = new List<string>();
            if (!functional_passed) errors.Add("UI interaction did not complete");
            if (width != 2560 || height != 1440) errors.Add("Resolution must be 2560x1440");
            if (!double.IsFinite(measured_seconds) || measured_seconds < 30 || sample_count < 1500 || overflow)
                errors.Add("Insufficient or overflowed interaction samples");
            if (ordinary_flight_samples != 0 || other_gameplay_samples != sample_count)
                errors.Add("Interaction window included non-UI intervals");
            if (!cpu_available || !gc_available || !memory_available || invalid_samples != 0)
                errors.Add("Required CPU/GC/memory measurements unavailable or invalid");
            if (!gpu_available || !gpu_complete || valid_gpu_samples != sample_count)
                errors.Add("Complete GPU interaction measurements unavailable");
            if (!float.IsFinite(cpu_p95_ms) || cpu_p95_ms <= 0 || cpu_p95_ms >= 16.7f ||
                !float.IsFinite(gpu_p95_ms) || gpu_p95_ms <= 0 || gpu_p95_ms >= 16.7f ||
                !float.IsFinite(maximum_frame_ms) || maximum_frame_ms <= 0 || maximum_frame_ms > 50)
                errors.Add("Interaction timing limit exceeded or invalid");
            if (peak_gc_bytes < 0) errors.Add("Invalid allocation measurement");
            // UI editing may allocate. Its real peak is reported, not reclassified as flight.
            if (peak_player_memory_bytes <= 0 || peak_player_memory_bytes >= GalaxyBetaProfile.MaximumPlayerMemoryBytes ||
                !gpu_memory_available || peak_gpu_memory_bytes <= 0 || peak_gpu_memory_bytes >= GalaxyBetaProfile.MaximumGpuMemoryBytes)
                errors.Add("Interaction memory unavailable or above limit");
            if (string.IsNullOrWhiteSpace(source_sha) || string.IsNullOrWhiteSpace(build_id)) errors.Add("Artifact identity missing");
            return errors.ToArray();
        }

        public static InteractionPerformanceReport Capture(string scenario, GalaxyPerformanceMonitor monitor, bool succeeded) => new()
        {
            source_sha = Application.version.Contains("+") ? Application.version.Split('+')[1] : Application.version, build_id = Application.buildGUID, scenario = scenario,
            graphics_api = SystemInfo.graphicsDeviceType.ToString(), data_mode = "Deterministic demo; seeded acceptance profile",
            width = Screen.width, height = Screen.height, sample_count = monitor.CollectedSampleCount,
            valid_gpu_samples = monitor.ValidGpuSamples, measured_seconds = monitor.CapturedSeconds,
            ordinary_flight_samples = monitor.OrdinaryFlightSamples, other_gameplay_samples = monitor.OtherGameplaySamples,
            cpu_p95_ms = monitor.P95CpuMilliseconds, gpu_p95_ms = monitor.P95GpuMilliseconds,
            maximum_frame_ms = monitor.MaximumFrameMilliseconds, peak_gc_bytes = monitor.PeakGcAllocatedBytes,
            peak_player_memory_bytes = monitor.PeakAllocatedMemoryBytes, peak_gpu_memory_bytes = monitor.PeakGpuMemoryBytes,
            cpu_available = monitor.CpuCounterAvailable, gc_available = monitor.GcCounterAvailable,
            memory_available = monitor.MemoryCounterAvailable, gpu_available = monitor.GpuTimingAvailable,
            gpu_memory_available = monitor.GpuMemoryAvailable, gpu_complete = monitor.GpuDrainComplete,
            overflow = monitor.SampleOverflow, invalid_samples = monitor.InvalidMeasuredSamples, functional_passed = succeeded,
            gc_source = monitor.GcMeasurementSource, player_memory_source = monitor.PlayerMemoryMeasurementSource,
            gpu_memory_source = monitor.GpuMemoryMeasurementSource
        };
    }
}
