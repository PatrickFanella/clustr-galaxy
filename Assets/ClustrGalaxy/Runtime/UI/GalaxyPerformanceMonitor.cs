using System;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using ClustrGalaxy.Core.Diagnostics;
using ClustrGalaxy.Data;
using ClustrGalaxy.Flight;
using ClustrGalaxy.Scanning;
using ClustrGalaxy.Simulation;
using ClustrGalaxy.World;
using Unity.Profiling;
using Unity.Profiling.LowLevel.Unsafe;
using UnityEngine;

namespace ClustrGalaxy.UI
{
    public sealed class GalaxyPerformanceMonitor : MonoBehaviour
    {
        private float[] frameMilliseconds = Array.Empty<float>();
        private float[] cpuMilliseconds = Array.Empty<float>();
        private long[] gcAllocatedBytes = Array.Empty<long>();
        private float[] floatScratch = Array.Empty<float>();
        private long[] longScratch = Array.Empty<long>();
        private byte[] intervals = Array.Empty<byte>();
        [StructLayout(LayoutKind.Sequential)]
        private struct ExecutionSample
        {
            public long ThreadCpuNanoseconds, ProcessCpuNanoseconds, VoluntarySwitches, InvoluntarySwitches, MajorFaults;
        }
        private ExecutionSample[] executionSamples = Array.Empty<ExecutionSample>();
        private double[] memoryQueryMilliseconds = Array.Empty<double>();
        private ulong[] frameTimestamps = Array.Empty<ulong>();
        private int[] collectionCounts = Array.Empty<int>();
        private int previousCollectionCount, fixedStepsSinceUpdate;
        private int[] fixedSteps = Array.Empty<int>();
        private long[] orbitUpdates = Array.Empty<long>();
        private double[] orbitWorkMilliseconds = Array.Empty<double>();
        private long previousOrbitTicks, previousOrbitUpdates;
        private ExecutionSample previousExecution;
        private double previousMemoryQueryMilliseconds;
        public bool ExecutionCountersAvailable { get; private set; }
        public bool CaptureFinished => frozen;
        private readonly FrameTiming[] recentTimings = new FrameTiming[32];
        private GpuTimingWindow gpuWindow;
        private bool prepared, capture, draining, frozen, cpuOverflow;
        private bool nativeAllocations, nativeMemory, nativeResidentMemory;
        private int count, pending = -1, invalidSamples;
        private double capturedSeconds, ordinarySeconds;
        private ulong latestAvailableTimestamp;
#if UNITY_STANDALONE_LINUX && !UNITY_EDITOR
        private ulong previousAllocated;
        [DllImport("clustrtelemetry")] private static extern int clustr_execution_snapshot(out ExecutionSample sample);
        [DllImport("clustrtelemetry")] private static extern int clustr_allocation_available();
        [DllImport("clustrtelemetry")] private static extern ulong clustr_allocated_bytes();
        [DllImport("clustrtelemetry")] private static extern long clustr_gpu_memory_bytes();
        [DllImport("clustrtelemetry")] private static extern long clustr_process_peak_rss_bytes();
#endif
        private ProfilerRecorder allocatedMemory, mainThreadTime, gcAllocated, gfxUsedMemory;
        private GalaxyNavigationController navigation;
        private ProbeHud hud;
        private ObserverProbeController flight;
        private SpatialSceneLoader loader;
        private GalaxyEntityRegistry registry;
        private GalaxySurveyVisibilityController visibility;
        private int scanEvents;

        public string GcMeasurementSource => nativeAllocations ? "Mono allocation profiler, all managed threads" : "Unity GC Allocated In Frame";
        public string GpuMemoryMeasurementSource => "Linux DRM requested buffers, unique clients";
        public string PlayerMemoryMeasurementSource => "Linux process high-water RSS (getrusage)";
        public int OrdinaryFlightSamples { get; private set; }
        public int LoadingSamples { get; private set; }
        public int OtherGameplaySamples { get; private set; }
        public float OrdinaryFlightSeconds => (float)ordinarySeconds;
        public double CapturedSeconds => capturedSeconds;
        public float OrdinaryMaximumFrameMilliseconds { get; private set; }
        public long OrdinaryPeakGcBytes { get; private set; }
        public int InvalidMeasuredSamples => invalidSamples + (gpuWindow?.Conflict == true ? 1 : 0);
        public bool SampleOverflow => cpuOverflow || gpuWindow?.Overflow == true;
        public int ValidGpuSamples => gpuWindow?.ValidCount ?? 0;
        public ulong[] MissingGpuTimestamps => gpuWindow?.MissingTimestamps() ?? Array.Empty<ulong>();
        public int RecordedGpuSamples => gpuWindow?.Count ?? 0;
        public ulong FirstGpuTimestamp => gpuWindow?.FirstTimestamp ?? 0;
        public ulong LastGpuTimestamp => gpuWindow?.LastTimestamp ?? 0;
        public ulong CaptureStartTimestamp => gpuWindow?.Start ?? 0;
        public ulong CaptureEndTimestamp => gpuWindow?.End ?? 0;
        public bool GpuDrainComplete => pending < 0 && gpuWindow?.Complete == true;
        public bool TimestampClockCompatible { get; private set; }
        public bool CpuCounterAvailable => mainThreadTime.Valid;
        public bool GcCounterAvailable => nativeAllocations || gcAllocated.Valid;
        public bool MemoryCounterAvailable => nativeResidentMemory;
        public long AllocatedMemoryBytes => allocatedMemory.Valid ? allocatedMemory.LastValue : 0;
        public long MainThreadNanoseconds => mainThreadTime.Valid ? mainThreadTime.LastValue : 0;
        public long SteadyStateGcBytes => gcAllocated.Valid ? gcAllocated.LastValue : 0;
        public float MainThreadMilliseconds => MainThreadNanoseconds / 1000000f;
        public int ActiveEntityCount { get; private set; }
        public int PooledEntityCount { get; private set; }
        public int KnownNodeCount { get; private set; }
        public int VisibleNodeCount { get; private set; }
        public int ScanEventCount => scanEvents;
        public float P95FrameMilliseconds { get; private set; }
        public float P95CpuMilliseconds { get; private set; }
        public float P95GpuMilliseconds { get; private set; }
        public float MaximumFrameMilliseconds { get; private set; }
        public long PeakGcAllocatedBytes { get; private set; }
        public long P95GcAllocatedBytes { get; private set; }
        public long PeakAllocatedMemoryBytes { get; private set; }
        public long PeakGpuMemoryBytes { get; private set; }
        public bool GpuTimingAvailable => ValidGpuSamples > 0;
        public bool GpuMemoryAvailable => nativeMemory && PeakGpuMemoryBytes > 0;
        public int CollectedSampleCount => count;

        public void BindSource(SpatialSceneLoader source) => loader = source;
        public void Configure(GalaxyEntityRegistry entityRegistry, SignalScanner scanner, GalaxySurveyVisibilityController controller)
        {
            registry = entityRegistry; visibility = controller;
            navigation = GetComponent<GalaxyNavigationController>();
            hud = GetComponent<ProbeHud>(); flight = GetComponent<ObserverProbeController>();
        }
        public void RecordScanEvent() => scanEvents++;
        public void PrepareCapture(int seconds)
        {
            var capacity = Math.Clamp(seconds, 30, 3600) * 500;
            frameMilliseconds = new float[capacity]; cpuMilliseconds = new float[capacity];
            gcAllocatedBytes = new long[capacity]; intervals = new byte[capacity];
            floatScratch = new float[capacity]; longScratch = new long[capacity];
            executionSamples = new ExecutionSample[capacity];
            collectionCounts = new int[capacity];
            fixedSteps = new int[capacity]; orbitUpdates = new long[capacity]; orbitWorkMilliseconds = new double[capacity];
            KeplerOrbit.CaptureTiming = true;
            memoryQueryMilliseconds = new double[capacity]; frameTimestamps = new ulong[capacity];
            gpuWindow = new GpuTimingWindow(capacity);
            ResetSamplingWindow(); prepared = true;
        }
        public void ResetSamplingWindow()
        {
            count = invalidSamples = 0; pending = -1;
            capture = draining = frozen = cpuOverflow = false;
            capturedSeconds = ordinarySeconds = 0;
            OrdinaryFlightSamples = LoadingSamples = OtherGameplaySamples = 0;
            OrdinaryMaximumFrameMilliseconds = MaximumFrameMilliseconds = 0;
            OrdinaryPeakGcBytes = PeakGcAllocatedBytes = P95GcAllocatedBytes = 0;
            PeakAllocatedMemoryBytes = PeakGpuMemoryBytes = 0;
            P95FrameMilliseconds = P95CpuMilliseconds = P95GpuMilliseconds = 0;
            Array.Clear(frameMilliseconds, 0, frameMilliseconds.Length);
            Array.Clear(cpuMilliseconds, 0, cpuMilliseconds.Length);
            Array.Clear(gcAllocatedBytes, 0, gcAllocatedBytes.Length);
        }
        public void BeginCapture()
        {
            if (!prepared) throw new InvalidOperationException("Prepare capture storage before warm-up.");
            ResetSamplingWindow();
            var start = (ulong)ProfilerUnsafeUtility.Timestamp;
            var frequency = FrameTimingManager.GetCpuTimerFrequency();
            var ratio = ProfilerUnsafeUtility.TimestampToNanosecondsConversionRatio;
            TimestampClockCompatible = frequency > 0 && ratio.Denominator > 0 &&
                Math.Abs(frequency * (double)ratio.Numerator / ratio.Denominator - 1e9) < 1e6 &&
                latestAvailableTimestamp > 0 && start > latestAvailableTimestamp &&
                (start - latestAvailableTimestamp) / (double)frequency < 1;
            gpuWindow.Begin(start); capture = true;
        }
        public void EndCapture()
        {
            capture = false; draining = true;
            gpuWindow.Close((ulong)ProfilerUnsafeUtility.Timestamp, count);
        }
        public void FinishCapture()
        {
            draining = false; frozen = true; KeplerOrbit.CaptureTiming = false;
            P95FrameMilliseconds = Percentile(frameMilliseconds, count);
            P95CpuMilliseconds = Percentile(cpuMilliseconds, count);
            P95GpuMilliseconds = Percentile(gpuWindow.Values, gpuWindow.Count);
            Array.Copy(gcAllocatedBytes, longScratch, count);
            Array.Sort(longScratch, 0, count);
            P95GcAllocatedBytes = count == 0 ? 0 : longScratch[Math.Clamp((int)Math.Ceiling(count * .95) - 1, 0, count - 1)];
            if (registry == null) return;
            ActiveEntityCount = PooledEntityCount = VisibleNodeCount = 0;
            foreach (var entity in registry.Entities)
            {
                if (entity == null) continue;
                if (entity.gameObject.activeInHierarchy) VisibleNodeCount++;
                if (entity.gameObject.activeSelf) ActiveEntityCount++; else PooledEntityCount++;
            }
            KnownNodeCount = visibility?.SurveySession?.State.KnownEntities.Count ?? registry.Entities.Count;
        }
        private float Percentile(float[] values, int length)
        {
            if (length == 0) return 0;
            Array.Copy(values, floatScratch, length);
            Array.Sort(floatScratch, 0, length);
            return floatScratch[Math.Clamp((int)Math.Ceiling(length * .95) - 1, 0, length - 1)];
        }
        private void OnEnable()
        {
#if UNITY_STANDALONE_LINUX && !UNITY_EDITOR
            if (GalaxyLaunchOptions.IsBetaProfile(Environment.GetCommandLineArgs()) || Array.Exists(Environment.GetCommandLineArgs(), value => value == "--clustr-ui-performance"))
            {
                try
                {
                    if (clustr_allocation_available() != 0)
                    {
                        var before = clustr_allocated_bytes();
                        var probe = new byte[4096]; GC.KeepAlive(probe);
                        nativeAllocations = clustr_allocated_bytes() >= before + 4096;
                    }
                    ExecutionCountersAvailable = clustr_execution_snapshot(out previousExecution) != 0;
                    previousAllocated = clustr_allocated_bytes();
                    nativeMemory = clustr_gpu_memory_bytes() > 0;
                    nativeResidentMemory = clustr_process_peak_rss_bytes() > 0;
                    Debug.Log("[ClustrGalaxy.Telemetry] allocation_probe=" + nativeAllocations + " drm_memory=" + nativeMemory + " process_rss=" + nativeResidentMemory);
                }
                catch (Exception exception) { Debug.LogWarning("[ClustrGalaxy.Telemetry] Native measurements unavailable: " + exception.Message); }
            }
#endif
            allocatedMemory = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "Total Used Memory");
            mainThreadTime = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread", 15);
            gcAllocated = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");
            gfxUsedMemory = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "Gfx Used Memory");
        }
        private void FixedUpdate()
        {
            if (prepared && !frozen) fixedStepsSinceUpdate++;
        }
        private void Update()
        {
            if (!prepared || frozen) return;
            long allocation = gcAllocated.Valid ? gcAllocated.LastValue : -1;
            long residentBytes = -1, graphicsBytes = -1;
            var currentCollectionCount = GC.CollectionCount(0);
            var currentOrbitTicks = KeplerOrbit.AppliedOrbitTicks;
            var currentOrbitUpdates = KeplerOrbit.AppliedOrbitCount;
            var executionDelta = default(ExecutionSample);
            var currentMemoryQueryMilliseconds = 0d;
#if UNITY_STANDALONE_LINUX && !UNITY_EDITOR
            if (ExecutionCountersAvailable)
            {
                ExecutionCountersAvailable = clustr_execution_snapshot(out var currentExecution) != 0;
                if (ExecutionCountersAvailable)
                {
                    executionDelta = new ExecutionSample {
                        ThreadCpuNanoseconds = currentExecution.ThreadCpuNanoseconds - previousExecution.ThreadCpuNanoseconds,
                        ProcessCpuNanoseconds = currentExecution.ProcessCpuNanoseconds - previousExecution.ProcessCpuNanoseconds,
                        VoluntarySwitches = currentExecution.VoluntarySwitches - previousExecution.VoluntarySwitches,
                        InvoluntarySwitches = currentExecution.InvoluntarySwitches - previousExecution.InvoluntarySwitches,
                        MajorFaults = currentExecution.MajorFaults - previousExecution.MajorFaults };
                    previousExecution = currentExecution;
                }
            }
            if (nativeAllocations)
            {
                var current = clustr_allocated_bytes();
                allocation = current >= previousAllocated ? (long)(current - previousAllocated) : -1;
                previousAllocated = current;
            }
            if (nativeResidentMemory)
            {
                residentBytes = clustr_process_peak_rss_bytes();
                if (residentBytes <= 0) nativeResidentMemory = false;
            }
            if (nativeMemory)
            {
                var queryStarted = ProfilerUnsafeUtility.Timestamp;
                graphicsBytes = clustr_gpu_memory_bytes();
                var conversion = ProfilerUnsafeUtility.TimestampToNanosecondsConversionRatio;
                currentMemoryQueryMilliseconds = (ProfilerUnsafeUtility.Timestamp - queryStarted) *
                    (double)conversion.Numerator / conversion.Denominator / 1000000d;
                if (graphicsBytes < 0) nativeMemory = false;
            }
#endif
            // Delta time, Main Thread and allocation deltas describe the preceding
            // frame. Finish its slot before opening the current frame's slot.
            if (pending >= 0)
            {
                fixedSteps[pending] = fixedStepsSinceUpdate;
                orbitUpdates[pending] = currentOrbitUpdates - previousOrbitUpdates;
                orbitWorkMilliseconds[pending] = (currentOrbitTicks - previousOrbitTicks) * 1000d / System.Diagnostics.Stopwatch.Frequency;
                collectionCounts[pending] = currentCollectionCount - previousCollectionCount;
                executionSamples[pending] = executionDelta;
                memoryQueryMilliseconds[pending] = previousMemoryQueryMilliseconds;
                CompleteCpuFrame(pending, Time.unscaledDeltaTime, MainThreadMilliseconds, allocation, residentBytes, graphicsBytes);
                pending = -1;
            }
            fixedStepsSinceUpdate = 0;
            previousOrbitTicks = currentOrbitTicks; previousOrbitUpdates = currentOrbitUpdates;
            previousCollectionCount = currentCollectionCount;
            previousMemoryQueryMilliseconds = currentMemoryQueryMilliseconds;
            if (capture)
            {
                if (count == frameMilliseconds.Length) cpuOverflow = true;
                else
                {
                    pending = count++;
                    frameTimestamps[pending] = (ulong)ProfilerUnsafeUtility.Timestamp;
                    var loading = loader?.IsLoading == true;
                    var ordinary = !loading && navigation != null && !navigation.IsMapOpen && hud?.InputCaptured != true && flight != null && flight.enabled;
                    intervals[pending] = loading ? (byte)1 : ordinary ? (byte)2 : (byte)3;
                }
            }
            if (capture || draining || !gpuWindow.Active)
            {
                FrameTimingManager.CaptureFrameTimings();
                var available = FrameTimingManager.GetLatestTimings((uint)recentTimings.Length, recentTimings);
                for (var i = (int)available - 1; i >= 0; i--)
                {
                    var timing = recentTimings[i];
                    latestAvailableTimestamp = Math.Max(latestAvailableTimestamp, timing.frameStartTimestamp);
                    gpuWindow.Record(timing.frameStartTimestamp, (float)timing.gpuFrameTime);
                }
            }
        }
        private void CompleteCpuFrame(int index, float seconds, float cpuMs, long allocation, long residentBytes, long graphicsBytes)
        {
            var frameMs = seconds * 1000f;
            frameMilliseconds[index] = frameMs; cpuMilliseconds[index] = cpuMs; gcAllocatedBytes[index] = allocation;
            capturedSeconds += seconds;
            if (!float.IsFinite(cpuMs) || cpuMs <= 0 || !float.IsFinite(frameMs) || frameMs <= 0 ||
                allocation < 0 || residentBytes <= 0 || graphicsBytes < 0) invalidSamples++;
            MaximumFrameMilliseconds = Mathf.Max(MaximumFrameMilliseconds, frameMs);
            PeakGcAllocatedBytes = Math.Max(PeakGcAllocatedBytes, allocation);
            PeakAllocatedMemoryBytes = Math.Max(PeakAllocatedMemoryBytes, residentBytes);
            PeakGpuMemoryBytes = Math.Max(PeakGpuMemoryBytes, graphicsBytes);
            if (intervals[index] == 1) LoadingSamples++;
            else if (intervals[index] == 2)
            {
                OrdinaryFlightSamples++; ordinarySeconds += seconds;
                OrdinaryMaximumFrameMilliseconds = Mathf.Max(OrdinaryMaximumFrameMilliseconds, frameMs);
                OrdinaryPeakGcBytes = Math.Max(OrdinaryPeakGcBytes, allocation);
            }
            else OtherGameplaySamples++;
        }
        // Preserve raw order and write only after the measured window has been frozen.
        // Execution counters describe Update-to-Update intervals, not profiler marker durations.
        public void WriteFrameEvidence(string path)
        {
            if (!frozen) throw new InvalidOperationException("Finish capture before exporting frame evidence.");
            using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
            using var writer = new StreamWriter(stream);
            writer.WriteLine("sample,timestamp,interval,frame_ms,main_thread_marker_ms,gc_bytes,gpu_memory_query_ms,execution_available,update_thread_cpu_ms,update_process_cpu_ms,voluntary_switches,involuntary_switches,major_faults,gc_collections,update_interval_fixed_steps,orbit_updates,orbit_work_ms");
            for (var i = 0; i < count; i++)
            {
                var execution = executionSamples[i];
                writer.WriteLine(string.Join(",", i.ToString(CultureInfo.InvariantCulture), frameTimestamps[i].ToString(CultureInfo.InvariantCulture),
                    intervals[i].ToString(CultureInfo.InvariantCulture), frameMilliseconds[i].ToString("R", CultureInfo.InvariantCulture),
                    cpuMilliseconds[i].ToString("R", CultureInfo.InvariantCulture), gcAllocatedBytes[i].ToString(CultureInfo.InvariantCulture),
                    memoryQueryMilliseconds[i].ToString("R", CultureInfo.InvariantCulture), ExecutionCountersAvailable ? "1" : "0",
                    (execution.ThreadCpuNanoseconds / 1000000d).ToString("R", CultureInfo.InvariantCulture),
                    (execution.ProcessCpuNanoseconds / 1000000d).ToString("R", CultureInfo.InvariantCulture),
                    execution.VoluntarySwitches.ToString(CultureInfo.InvariantCulture), execution.InvoluntarySwitches.ToString(CultureInfo.InvariantCulture),
                    execution.MajorFaults.ToString(CultureInfo.InvariantCulture), collectionCounts[i].ToString(CultureInfo.InvariantCulture), fixedSteps[i].ToString(CultureInfo.InvariantCulture),
                    orbitUpdates[i].ToString(CultureInfo.InvariantCulture), orbitWorkMilliseconds[i].ToString("R", CultureInfo.InvariantCulture)));
            }
            writer.Flush(); stream.Flush(true);
        }
        private void OnDisable()
        {
            KeplerOrbit.CaptureTiming = false;
            allocatedMemory.Dispose(); mainThreadTime.Dispose(); gcAllocated.Dispose(); gfxUsedMemory.Dispose();
        }
    }
}
