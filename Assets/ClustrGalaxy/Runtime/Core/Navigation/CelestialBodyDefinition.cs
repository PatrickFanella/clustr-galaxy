using System;
using System.Numerics;
using ClustrGalaxy.Core.Domain;

namespace ClustrGalaxy.Core.Navigation
{
    public sealed class CelestialBodyDefinition
    {
        public const string Version = "body-v2";
        public CelestialBodyDefinition(GalaxyEntityId id, double radius, double mu, double exclusionRadius)
        {
            if (!double.IsFinite(radius) || radius <= 0 || !double.IsFinite(mu) || mu <= 0 || !double.IsFinite(exclusionRadius) || exclusionRadius < radius)
                throw new ArgumentException("Invalid celestial body definition.");
            Id = id; Radius = radius; Mu = mu; ExclusionRadius = exclusionRadius;
        }
        public GalaxyEntityId Id { get; } public double Radius { get; } public double Mu { get; } public double ExclusionRadius { get; }
        public bool Stellar => ExclusionRadius > Radius;
    }
    public static class SurfaceIntersection
    {
        public static bool Sweep(Vector3 start, Vector3 end, double radius, out float fraction)
        {
            fraction = 0;
            if (!ShipDefinition.Finite(start) || !ShipDefinition.Finite(end) || !double.IsFinite(radius) || radius <= 0)
                throw new ArgumentException("Invalid surface sweep.");
            double x=start.X,y=start.Y,z=start.Z,dx=(double)end.X-x,dy=(double)end.Y-y,dz=(double)end.Z-z;
            var c=x*x+y*y+z*z-radius*radius;
            if(c<=0) return true;
            var a=dx*dx+dy*dy+dz*dz;
            if(a<1e-18) return false;
            var b=2*(x*dx+y*dy+z*dz);var discriminant=b*b-4*a*c;
            if(discriminant<0) return false;
            var t=(-b-Math.Sqrt(discriminant))/(2*a);
            if(t<0||t>1) return false;
            fraction=(float)t;return true;
        }
    }
}
