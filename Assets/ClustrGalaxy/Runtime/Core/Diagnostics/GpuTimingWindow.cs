using System;

namespace ClustrGalaxy.Core.Diagnostics
{
    /// <summary>Preallocated timestamp window; delayed values can fill their own frame, never a different frame.</summary>
    public sealed class GpuTimingWindow
    {
        private readonly ulong[] timestamps;
        public float[] Values { get; }
        public int Count { get; private set; }
        public int ValidCount { get; private set; }
        public bool Overflow { get; private set; }
        public bool Conflict { get; private set; }
        public ulong Start { get; private set; }
        public ulong End { get; private set; }
        public bool ObservedEnd { get; private set; }
        public int ExpectedCount { get; private set; }
        public bool Active { get; private set; }
        public ulong FirstTimestamp => Count == 0 ? 0 : timestamps[0];
        public ulong LastTimestamp => Count == 0 ? 0 : timestamps[Count - 1];
        public bool Complete => End > Start && ObservedEnd && !Overflow && !Conflict && Count == ExpectedCount && ValidCount == ExpectedCount;

        public GpuTimingWindow(int capacity)
        {
            if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
            timestamps = new ulong[capacity]; Values = new float[capacity];
        }
        // Allocate only when preparing a report after measurement; never during capture.
        public ulong[] MissingTimestamps()
        {
            var missing=new ulong[Count-ValidCount];var index=0;
            for(var i=0;i<Count;i++)if(Values[i]<=0)missing[index++]=timestamps[i];
            return missing;
        }
        public void Begin(ulong timestamp)
        {
            Count = ValidCount = ExpectedCount = 0;
            Overflow = Conflict = ObservedEnd = false;
            Start = timestamp; End = 0; Active = true;
        }
        public void Close(ulong timestamp, int expectedCount)
        {
            End = timestamp; ExpectedCount = expectedCount;
            while (Count > 0 && timestamps[Count - 1] >= End)
            {
                ObservedEnd = true;
                if (Values[Count - 1] > 0) ValidCount--;
                Count--;
            }
        }
        public void Record(ulong timestamp, float milliseconds)
        {
            if (!Active || timestamp <= Start) return;
            if (End != 0 && timestamp >= End) { ObservedEnd = true; return; }
            var valid = float.IsFinite(milliseconds) && milliseconds > 0;
            var low = 0; var high = Count - 1; var index = -1;
            while (low <= high)
            {
                var middle = low + (high - low) / 2;
                if (timestamps[middle] == timestamp) { index = middle; break; }
                if (timestamps[middle] < timestamp) low = middle + 1; else high = middle - 1;
            }
            if (index < 0) index = ~low;
            if (index >= 0)
            {
                if (valid && Values[index] == 0) { Values[index] = milliseconds; ValidCount++; }
                else if (valid && Values[index] != milliseconds) Conflict = true;
                return;
            }
            if (Count == timestamps.Length) { Overflow = true; return; }
            index = ~index;
            Array.Copy(timestamps, index, timestamps, index + 1, Count - index);
            Array.Copy(Values, index, Values, index + 1, Count - index);
            timestamps[index] = timestamp;
            Values[index] = valid ? milliseconds : 0;
            Count++;
            if (valid) ValidCount++;
        }
    }
}
