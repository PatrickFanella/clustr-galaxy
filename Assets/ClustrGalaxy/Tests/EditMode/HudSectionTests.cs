using ClustrGalaxy.Core.Presentation;
using ClustrGalaxy.UI;
using NUnit.Framework;
using UnityEngine;
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
        [Test] public void GetOrAddComponentAddsOnceAndReusesTheLiveComponent()
        {
            var owner=new GameObject("lookup owner").transform;
            try
            {
                var added=owner.GetOrAddComponent<BoxCollider>();
                Assert.That(added!=null,Is.True);
                Assert.That(owner.GetOrAddComponent<BoxCollider>(),Is.SameAs(added));
                Assert.That(owner.GetComponents<BoxCollider>().Length,Is.EqualTo(1));
            }
            finally{Object.DestroyImmediate(owner.gameObject);}
        }
    }
}
