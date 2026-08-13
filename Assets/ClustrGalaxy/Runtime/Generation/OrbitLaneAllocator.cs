using System;

namespace ClustrGalaxy.Generation
{
    public static class OrbitLaneAllocator
    {
        public static double AllocateAfter(
            OrbitalElements previous,
            double previousVisualRadius,
            double nextEccentricity,
            double nextVisualRadius,
            double minimumGap)
        {
            var boundedEccentricity = Math.Max(0d, Math.Min(0.95d, nextEccentricity));
            var previousApoapsis = previous.SemiMajorAxis * (1d + previous.Eccentricity);
            var bodyClearance = 3d * (
                Math.Max(0d, previousVisualRadius) + Math.Max(0d, nextVisualRadius));
            var clearance = Math.Max(Math.Max(0d, minimumGap), bodyClearance);
            return (previousApoapsis + clearance) / (1d - boundedEccentricity);
        }
    }
}
