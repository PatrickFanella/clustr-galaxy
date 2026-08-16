using System;
using Unity.Profiling;
using ClustrGalaxy.Scanning;
using ClustrGalaxy.World;
using UnityEngine;

namespace ClustrGalaxy.UI
{
    public sealed class GalaxyPerformanceMonitor : MonoBehaviour
    {
        private const int SampleCount = 600;
        private readonly float[] frameMilliseconds = new float[SampleCount];
        private readonly float[] cpuMilliseconds = new float[SampleCount];
        private readonly float[] gpuMilliseconds = new float[SampleCount];
        private readonly long[] gcAllocatedBytes = new long[SampleCount];
        private readonly FrameTiming[] latestTiming = new FrameTiming[1];
        private int cursor;
        private int count;
        private float nextReport;
        private ProfilerRecorder allocatedMemory;
        private ProfilerRecorder mainThreadTime;
        private ProfilerRecorder gcAllocated;
        private ProfilerRecorder gfxUsedMemory;
        private GalaxyEntityRegistry registry;
        private GalaxySurveyVisibilityController visibility;
        private int scanEvents;

        public float P95FrameMilliseconds { get; private set; }
        public long AllocatedMemoryBytes => allocatedMemory.Valid ? allocatedMemory.LastValue : 0L;
        public long MainThreadNanoseconds => mainThreadTime.Valid ? mainThreadTime.LastValue : 0L;
        public long SteadyStateGcBytes => gcAllocated.Valid ? gcAllocated.LastValue : 0L;
        public int ActiveEntityCount { get; private set; }
        public int PooledEntityCount { get; private set; }
        public int KnownNodeCount { get; private set; }
        public int VisibleNodeCount { get; private set; }
        public int ScanEventCount => scanEvents;
        public float MainThreadMilliseconds => MainThreadNanoseconds / 1000000f;
        public float P95CpuMilliseconds { get; private set; }
        public float P95GpuMilliseconds { get; private set; }
        public float MaximumFrameMilliseconds { get; private set; }
        public long PeakGcAllocatedBytes { get; private set; }
        public long P95GcAllocatedBytes { get; private set; }
        public long PeakAllocatedMemoryBytes { get; private set; }
        public long PeakGpuMemoryBytes { get; private set; }
        public bool GpuTimingAvailable { get; private set; }
        public bool GpuMemoryAvailable => gfxUsedMemory.Valid && PeakGpuMemoryBytes > 0;
        public int CollectedSampleCount => count;

        public void Configure(GalaxyEntityRegistry entityRegistry, SignalScanner signalScanner,
            GalaxySurveyVisibilityController visibilityController)
        {
            registry = entityRegistry;
            visibility = visibilityController;
        }

        public void RecordScanEvent() => scanEvents++;

        public void ResetSamplingWindow()
        {
            cursor = 0;
            count = 0;
            P95FrameMilliseconds = 0f;
            P95CpuMilliseconds = 0f;
            P95GpuMilliseconds = 0f;
            MaximumFrameMilliseconds = 0f;
            PeakGcAllocatedBytes = 0L;
            P95GcAllocatedBytes = 0L;
            PeakAllocatedMemoryBytes = 0L;
            PeakGpuMemoryBytes = 0L;
            GpuTimingAvailable = false;
            Array.Clear(frameMilliseconds, 0, frameMilliseconds.Length);
            Array.Clear(cpuMilliseconds, 0, cpuMilliseconds.Length);
            Array.Clear(gpuMilliseconds, 0, gpuMilliseconds.Length);
            Array.Clear(gcAllocatedBytes, 0, gcAllocatedBytes.Length);
        }

        private void OnEnable()
        {
            allocatedMemory = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "Total Used Memory");
            mainThreadTime = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread", 15);
            gcAllocated = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");
            gfxUsedMemory = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "Gfx Used Memory");
        }

        private void Update()
        {
            var frameMs = Time.unscaledDeltaTime * 1000f;
            frameMilliseconds[cursor] = frameMs;
            var cpuMs = MainThreadMilliseconds > 0f ? MainThreadMilliseconds : frameMs;
            cpuMilliseconds[cursor] = cpuMs;
            FrameTimingManager.CaptureFrameTimings();
            if (FrameTimingManager.GetLatestTimings(1, latestTiming) > 0 && latestTiming[0].gpuFrameTime > 0d)
            {
                gpuMilliseconds[cursor] = (float)latestTiming[0].gpuFrameTime;
                GpuTimingAvailable = true;
            }
            MaximumFrameMilliseconds = Mathf.Max(MaximumFrameMilliseconds, frameMs);
            if (gcAllocated.Valid) PeakGcAllocatedBytes = Math.Max(PeakGcAllocatedBytes, gcAllocated.LastValue);
            gcAllocatedBytes[cursor] = gcAllocated.Valid ? gcAllocated.LastValue : 0L;
            if (allocatedMemory.Valid) PeakAllocatedMemoryBytes = Math.Max(PeakAllocatedMemoryBytes, allocatedMemory.LastValue);
            if (gfxUsedMemory.Valid) PeakGpuMemoryBytes = Math.Max(PeakGpuMemoryBytes, gfxUsedMemory.LastValue);
            cursor = (cursor + 1) % frameMilliseconds.Length;
            count = Math.Min(count + 1, frameMilliseconds.Length);
            if (Time.unscaledTime < nextReport || count < 60)
            {
                return;
            }
            nextReport = Time.unscaledTime + 2f;
            var sorted = new float[count];
            Array.Copy(frameMilliseconds, sorted, count);
            Array.Sort(sorted);
            P95FrameMilliseconds = sorted[Mathf.Clamp(Mathf.CeilToInt(count * 0.95f) - 1, 0, count - 1)];
            P95CpuMilliseconds = Percentile(cpuMilliseconds, count, includeZeros: false);
            P95GpuMilliseconds = Percentile(gpuMilliseconds, count, includeZeros: false);
            P95GcAllocatedBytes = Percentile(gcAllocatedBytes, count);
            if (registry != null)
            {
                ActiveEntityCount = 0;
                PooledEntityCount = 0;
                VisibleNodeCount = 0;
                for (var index = 0; index < registry.Entities.Count; index++)
                {
                    var entity = registry.Entities[index];
                    if (entity == null) continue;
                    if (entity.gameObject.activeInHierarchy) VisibleNodeCount++;
                    if (entity.gameObject.activeSelf) ActiveEntityCount++;
                    else PooledEntityCount++;
                }
                KnownNodeCount = visibility?.SurveySession?.State.KnownEntities.Count ?? registry.Entities.Count;
            }
        }

        private void OnDisable()
        {
            allocatedMemory.Dispose();
            mainThreadTime.Dispose();
            gcAllocated.Dispose();
            gfxUsedMemory.Dispose();
        }

        private static float Percentile(float[] values, int valueCount, bool includeZeros)
        {
            var samples = new float[valueCount];
            var sampleCount = 0;
            for (var index = 0; index < valueCount; index++)
            {
                if (!includeZeros && values[index] <= 0f) continue;
                samples[sampleCount++] = values[index];
            }
            if (sampleCount == 0) return 0f;
            Array.Sort(samples, 0, sampleCount);
            return samples[Mathf.Clamp(Mathf.CeilToInt(sampleCount * 0.95f) - 1, 0, sampleCount - 1)];
        }

        private static long Percentile(long[] values, int valueCount)
        {
            if (valueCount == 0) return 0L;
            var samples = new long[valueCount];
            Array.Copy(values, samples, valueCount);
            Array.Sort(samples);
            return samples[Mathf.Clamp(Mathf.CeilToInt(valueCount * 0.95f) - 1, 0, valueCount - 1)];
        }
    }
}
