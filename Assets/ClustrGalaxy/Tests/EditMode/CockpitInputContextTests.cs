using ClustrGalaxy.Core.Presentation;
using NUnit.Framework;
namespace ClustrGalaxy.Tests
{
    public sealed class CockpitInputContextTests
    {
        [Test] public void NestedViewsCannotResumeFlightUntilEveryOwnerCloses()
        {
            var context = new CockpitInputContext(); context.SetCommand(true);
            var parent = context.AcquireModal(); var child = context.AcquireModal();
            context.SetCompatibilityModal(true); context.SetCompatibilityModal(false);
            Assert.That(context.Paused, Is.True); Assert.That(context.PilotEnabled, Is.False);
            Assert.That(context.ReleaseModal(parent), Is.True); Assert.That(context.Paused, Is.True);
            Assert.That(context.ReleaseModal(parent), Is.False); Assert.That(context.ReleaseModal(999), Is.False);
            context.ReleaseModal(child); Assert.That(context.Paused, Is.False);
            Assert.That(context.NavigationEnabled, Is.False); context.SetCommand(false);
            Assert.That(context.PilotEnabled, Is.True);
        }
        [Test] public void ChartSuppressesPilotButDoesNotPauseSimulation()
        {
            var context = new CockpitInputContext(); context.SetChart(true);
            Assert.That(context.Paused, Is.False);Assert.That(context.PilotEnabled, Is.False);Assert.That(context.NavigationEnabled, Is.True);
            context.SetCommand(true);Assert.That(context.NavigationEnabled, Is.False);
            context.SetCommand(false);context.SetChart(false);Assert.That(context.PilotEnabled, Is.True);
        }
    }
}
