using System;

namespace ClustrGalaxy.Generation
{
    public static class OrbitStability
    {
        public static double MaximumProgradeMoonAxis(
            double planetSemiMajorAxis,
            double planetGravitationalParameter,
            double starGravitationalParameter)
        {
            if (planetSemiMajorAxis <= 0d ||
                planetGravitationalParameter <= 0d ||
                starGravitationalParameter <= 0d)
            {
                return 0d;
            }

            var hillRadius = planetSemiMajorAxis * Math.Pow(
                planetGravitationalParameter / (3d * starGravitationalParameter),
                1d / 3d);
            return hillRadius * 0.35d;
        }

        public static double MinimumVisualMoonAxis(
            double planetVisualRadius,
            double moonVisualRadius,
            double moonEccentricity,
            double visualGap)
        {
            var clearance = Math.Max(0d, planetVisualRadius) +
                Math.Max(0d, moonVisualRadius) + Math.Max(0d, visualGap);
            var eccentricity = Math.Max(0d, Math.Min(0.95d, moonEccentricity));
            return clearance / (1d - eccentricity);
        }
    }
}
