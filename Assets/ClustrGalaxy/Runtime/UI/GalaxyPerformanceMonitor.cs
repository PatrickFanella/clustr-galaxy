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
        private int cursor;
        private int count;
        private float nextReport;
        private ProfilerRecorder allocatedMemory;
        private ProfilerRecorder mainThreadTime;
        private ProfilerRecorder gcAllocated;
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

        public void Configure(GalaxyEntityRegistry entityRegistry, SignalScanner signalScanner,
            GalaxySurveyVisibilityController visibilityController)
        {
            registry = entityRegistry;
            visibility = visibilityController;
        }

        public void RecordScanEvent() => scanEvents++;

        private void OnEnable()
        {
            allocatedMemory = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "Total Used Memory");
            mainThreadTime = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread", 15);
            gcAllocated = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");
        }

        private void Update()
        {
            frameMilliseconds[cursor] = Time.unscaledDeltaTime * 1000f;
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
        }
    }
}
