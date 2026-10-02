using ClustrGalaxy.Core.Diagnostics;
using NUnit.Framework;

namespace ClustrGalaxy.Tests
{
    public sealed class GpuTimingWindowTests
    {
        [Test]
        public void DelayedValuesOnlyFillTheirOwnFramesAndExcludeBothWindowEdges()
        {
            var window = new GpuTimingWindow(8); window.Begin(100);
            window.Record(90, 100); window.Record(100, 100);
            window.Record(110, 0); window.Record(120, .8f); window.Record(120, .8f);
            window.Close(200, 2);
            window.Record(110, .7f); window.Record(200, 100); window.Record(210, 100);
            Assert.That(window.Complete, Is.True);
            Assert.That(window.Count, Is.EqualTo(2));
            Assert.That(window.Values[0], Is.EqualTo(.7f));
            Assert.That(window.FirstTimestamp, Is.EqualTo(110));
            Assert.That(window.LastTimestamp, Is.EqualTo(120));
        }
        [Test]
        public void MissingFrameCannotBeReplacedByPostWindowOrDuplicateFrame()
        {
            var window = new GpuTimingWindow(8); window.Begin(100);
            window.Record(110, 1); window.Record(110, 1); window.Close(200, 2);
            window.Record(201, 1);
            Assert.That(window.Complete, Is.False);
            Assert.That(window.ValidCount, Is.EqualTo(1));
        }
        [Test]
        public void InvalidValuesRemainMissingUntilTheSameTimestampBecomesValid()
        {
            var window = new GpuTimingWindow(8); window.Begin(100);
            window.Record(110, float.NaN); window.Record(120, float.PositiveInfinity);
            window.Close(200, 2); window.Record(201, 1);
            Assert.That(window.Complete, Is.False);
            Assert.That(window.MissingTimestamps(),Is.EqualTo(new ulong[]{110,120}));
            window.Record(110, .5f); window.Record(120, .6f);
            Assert.That(window.MissingTimestamps(),Is.Empty);
            Assert.That(window.Complete, Is.True);
        }
        [Test]
        public void ConflictingAndOverflowedSamplesFailClosed()
        {
            var window = new GpuTimingWindow(1); window.Begin(100);
            window.Record(110, 1); window.Record(110, 2); window.Record(120, 1);
            window.Close(200, 1); window.Record(201, 1);
            Assert.That(window.Conflict, Is.True); Assert.That(window.Overflow, Is.True);
            Assert.That(window.Complete, Is.False);
        }
    }
}
