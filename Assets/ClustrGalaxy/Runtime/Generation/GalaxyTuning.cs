namespace ClustrGalaxy.Generation
{
    /// <summary>
    /// Beta-wide presentation and observer-flight constants. Logical spacing is
    /// intentionally separate from visual body radius and gravity parameters.
    /// </summary>
    public static class GalaxyTuning
    {
        public const float RegionDistanceScale = 5f;
        public const float SystemDistanceScale = 4f;
        public const float PlanetOrbitScale = 18f;
        public const float MoonOrbitScale = 2.25f;

        public const float PlanetRadiusScale = 30f;
        public const float SunRadius = 2000f;
        public const float PlanetSurfaceGravity = 4f;
        public const float SunSurfaceGravity = 6f;

        public const float OrbitPeriodScale = 1f;

        // KeplerOrbit multiplies elapsed time by this value, so the reciprocal
        // keeps the configured orbital and ship gravity clocks consistent.
        public const float OrbitSimulationTimeScale = 1f / OrbitPeriodScale;

        public const float CruiseSpeed = 92f;
        public const float BoostMultiplier = 1.75f;
        public const float Acceleration = 96f;
        public const float BrakeAcceleration = 240f;

        public static float EstimatedTravelSeconds(float distance) =>
            distance / CruiseSpeed;
    }
}
