using System;

namespace ClustrGalaxy.Generation
{
    /// <summary>Artistic geometry independent of provider metrics and relationships.</summary>
    public static class CelestialLayout
    {
        public const string Version = "celestial-v2";
        public const double StellarClearance = 250d;
        public const int MaximumMoons = 64;
        public static double SatelliteEnvelope(double planetRadius, int moons) =>
            3d * (planetRadius + GalaxyScale.MoonInitialGap) + GalaxyScale.RingInnerGap + Math.Clamp(moons, 0, MaximumMoons) * 80d + 80d;

        public static OrbitalElements Planet(OrbitalElements seed, double radius, int moons,
            double starRadius, double starMu, OrbitalElements previous, double previousEnvelope)
        {
            var envelope = SatelliteEnvelope(radius, moons);
            var planetMu = GalaxyTuning.PlanetSurfaceGravity * radius * radius;
            var hillFraction = .35d * Math.Pow(planetMu / (3d * starMu), 1d / 3d);
            var periapsis = Math.Max(GalaxyScale.FirstPlanetAxis,
                Math.Max(starRadius + StellarClearance + envelope, envelope / hillFraction));
            if (previous.SemiMajorAxis > 0d)
                periapsis = Math.Max(periapsis, previous.SemiMajorAxis * (1d + previous.Eccentricity)
                    + previousEnvelope + envelope + GalaxyScale.PlanetLaneGap);
            // Bound radial excursion, so thousands of lanes grow linearly rather than exponentially.
            var eccentricity = Math.Min(seed.Eccentricity, 500d / (periapsis + 500d));
            return WithShape(seed, periapsis / (1d - eccentricity), eccentricity);
        }

        public static OrbitalElements Moon(OrbitalElements seed, double planetRadius, double radius,
            OrbitalElements previous, double previousRadius)
        {
            var periapsis = previous.SemiMajorAxis <= 0d
                ? 3d * (planetRadius + GalaxyScale.MoonInitialGap) + radius
                : previous.SemiMajorAxis * (1d + previous.Eccentricity) + previousRadius + radius + GalaxyScale.MoonLaneGap;
            var eccentricity = Math.Min(seed.Eccentricity, 5d / (periapsis + 5d));
            return WithShape(seed, periapsis / (1d - eccentricity), eccentricity);
        }

        private static OrbitalElements WithShape(OrbitalElements seed, double axis, double eccentricity) =>
            new(axis, eccentricity, seed.InclinationRadians, seed.LongitudeAscendingNodeRadians,
                seed.ArgumentOfPeriapsisRadians, seed.MeanAnomalyAtEpochRadians);
    }
}
