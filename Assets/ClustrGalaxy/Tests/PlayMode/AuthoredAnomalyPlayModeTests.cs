using System.Collections;
using ClustrGalaxy.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace ClustrGalaxy.Tests
{
    public sealed class AuthoredAnomalyPlayModeTests
    {
        [UnityTest] public IEnumerator EmissionCueRestoresWithoutMovingReducedMotionParts()
        {
            var root=GameObject.CreatePrimitive(PrimitiveType.Sphere);root.name="MotionFixture";var visual=root.AddComponent<AuthoredAnomalyVisual>();visual.Initialize(0);var renderer=root.GetComponent<Renderer>();
            visual.Present(1,false,true);var position=root.transform.localPosition;visual.Pulse(1,1);Assert.That(renderer.HasPropertyBlock(),Is.True);
            visual.Present(1.2,true,true);Assert.That(root.transform.localPosition,Is.EqualTo(position));visual.Present(2,true,true);Assert.That(renderer.HasPropertyBlock(),Is.False);
            Object.Destroy(root);yield return null;
        }
        [UnityTest] public IEnumerator ReducedMotionFreezesEveryAnomalyAndOrdinaryAnimationAllocatesNoManagedMemory()
        {
            for(var kind=0;kind<6;kind++)
            {
                var root=new GameObject("Anomaly fixture");var part=new GameObject("MotionPart").transform;part.SetParent(root.transform,false);
                var visual=root.AddComponent<AuthoredAnomalyVisual>();visual.Initialize(kind);
                visual.Present(1,false,true);var position=part.localPosition;var rotation=part.localRotation;var scale=part.localScale;
                visual.Present(10,true,true);Assert.That(part.localPosition,Is.EqualTo(position));Assert.That(part.localRotation,Is.EqualTo(rotation));Assert.That(part.localScale,Is.EqualTo(scale));
                for(var i=0;i<100;i++)visual.Present(i*.02,true,false);
                var bytes=System.GC.GetAllocatedBytesForCurrentThread();for(var i=0;i<1000;i++)visual.Present(i*.02,true,false);
                Assert.That(System.GC.GetAllocatedBytesForCurrentThread()-bytes,Is.Zero);
                Object.Destroy(root);yield return null;
            }
        }
    }
}
