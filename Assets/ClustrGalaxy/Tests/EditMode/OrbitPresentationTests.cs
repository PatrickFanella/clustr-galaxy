using System;
using NUnit.Framework;
using ClustrGalaxy.Generation;
using ClustrGalaxy.Simulation;
using UnityEngine;
namespace ClustrGalaxy.Tests
{
    public sealed class OrbitPresentationTests
    {
        [TestCase(0d)] [TestCase(.6d)] [TestCase(.95d)]
        public void GeometricGuideStaysOnEllipseAndHasNoCoarseClosingEdge(double eccentricity)
        {
            const double axis=1500;var elements=new OrbitalElements(axis,eccentricity,0,0,0,0);
            var previous=KeplerOrbitMath.PositionAtEccentricAnomaly(elements,0);
            var f1=Vector3.zero;var f2=new Vector3((float)(-2*axis*eccentricity),0,0);
            for(var i=1;i<=512;i++)
            {
                var point=KeplerOrbitMath.PositionAtEccentricAnomaly(elements,Math.PI*2*i/512);
                Assert.That(Vector3.Distance(point,f1)+Vector3.Distance(point,f2),Is.EqualTo(axis*2).Within(.01));
                Assert.That(Vector3.Distance(previous,point),Is.LessThan(axis*.013));previous=point;
            }
        }
    }
}
