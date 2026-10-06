using System;
using System.IO;
using ClustrGalaxy.Core.Diagnostics;
using NUnit.Framework;

namespace ClustrGalaxy.Tests
{
    public sealed class QualificationReportStoreTests
    {
        private string directory;
        [SetUp] public void SetUp() => directory = Path.Combine(Path.GetTempPath(), "clustr-reports-" + Guid.NewGuid().ToString("N"));
        [TearDown] public void TearDown()
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
            if (File.Exists(directory)) File.Delete(directory);
        }
        [Test] public void LaterReportPreservesFailedRunAndReplacesOnlyPointer()
        {
            var failed = QualificationReportStore.Write(directory, "{\"qualified\":false}");
            var passed = QualificationReportStore.Write(directory, "{\"qualified\":true}");
            Assert.That(File.ReadAllText(failed), Is.EqualTo("{\"qualified\":false}"));
            Assert.That(File.ReadAllText(passed), Is.EqualTo("{\"qualified\":true}"));
            Assert.That(File.ReadAllText(Path.Combine(directory, "latest-report-path.txt")), Is.EqualTo(passed));
            Assert.That(Directory.GetFiles(directory, "*.tmp"), Is.Empty);
        }
        [Test] public void UnwritableReportDestinationPropagatesFailure()
        {
            File.WriteAllText(directory, "sentinel");
            Assert.Throws<IOException>(() => QualificationReportStore.Write(directory, "{}"));
            Assert.That(File.ReadAllText(directory), Is.EqualTo("sentinel"));
        }
        [Test] public void PointerFailureStillPreservesDurableReportAndPropagatesFailure()
        {
            Directory.CreateDirectory(Path.Combine(directory, "latest-report-path.txt"));
            Assert.Throws<IOException>(() => QualificationReportStore.Write(directory, "{\"qualified\":false}"));
            var reports = Directory.GetFiles(directory, "*.json");
            Assert.That(reports, Has.Length.EqualTo(1));
            Assert.That(File.ReadAllText(reports[0]), Is.EqualTo("{\"qualified\":false}"));
            Assert.That(Directory.GetFiles(directory, "*.tmp"), Is.Empty);
        }
    }
}
