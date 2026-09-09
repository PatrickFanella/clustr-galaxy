using ClustrGalaxy.Core.Presentation;
using NUnit.Framework;
namespace ClustrGalaxy.Tests
{
    public sealed class HudSectionTests
    {
        [Test] public void SectionsKeepGlobalExitControlsAndSeparateCreationFromFlight()
        {
            foreach(var selected in ProbeHudSections.Selectable)Assert.That(ProbeHudSections.Visible(ProbeHudSection.Common,selected),Is.True);
            Assert.That(ProbeHudSections.For(ProbeHudAction.ConfirmLocalQueue),Is.EqualTo(ProbeHudSection.Creations));
            Assert.That(ProbeHudSections.For(ProbeHudAction.ChooseStartingSystem),Is.EqualTo(ProbeHudSection.Selection));
            Assert.That(ProbeHudSections.Visible(ProbeHudSection.Activities,ProbeHudSection.Creations),Is.False);
            foreach(var row in ProbeHudSections.Selectable)Assert.That(ProbeHudSections.Visible(row,ProbeHudSection.All),Is.True);
        }
    }
}
