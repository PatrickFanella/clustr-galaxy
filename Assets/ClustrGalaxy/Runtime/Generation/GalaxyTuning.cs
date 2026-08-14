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
        public const float PlanetOrbitScale = 3f;
        public const float MoonOrbitScale = 2.25f;

        public const float OrbitPeriodScale = 10f;

        // KeplerOrbit multiplies elapsed time by this value, so the reciprocal
        // is required to make a ten-times-longer visible period.
        public const float OrbitSimulationTimeScale = 1f / OrbitPeriodScale;

        public const float CruiseSpeed = 92f;
        public const float BoostMultiplier = 1.75f;
        public const float Acceleration = 48f;
        public const float BrakeAcceleration = 120f;

        public static float EstimatedTravelSeconds(float distance) =>
            distance / CruiseSpeed;
    }
}
