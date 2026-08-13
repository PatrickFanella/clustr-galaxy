using System;
using Unity.Profiling;
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

        public float P95FrameMilliseconds { get; private set; }
        public long AllocatedMemoryBytes => allocatedMemory.Valid ? allocatedMemory.LastValue : 0L;
        public long MainThreadNanoseconds => mainThreadTime.Valid ? mainThreadTime.LastValue : 0L;

        private void OnEnable()
        {
            allocatedMemory = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "Total Used Memory");
            mainThreadTime = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread", 15);
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
        }

        private void OnDisable()
        {
            allocatedMemory.Dispose();
            mainThreadTime.Dispose();
        }
    }
}
