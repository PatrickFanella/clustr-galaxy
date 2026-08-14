namespace ClustrGalaxy.Generation
{
    public static class GalaxyScale
    {
        // KeplerOrbit consumes elapsed time multiplied by these values.
        public const float PlanetOrbitTimeScale = GalaxyTuning.OrbitSimulationTimeScale;
        public const float MoonOrbitTimeScale = GalaxyTuning.OrbitSimulationTimeScale;
        public const float ShipOrbitTimeScale = GalaxyTuning.OrbitSimulationTimeScale;

        public const double FirstPlanetAxis = 500d * GalaxyTuning.PlanetOrbitScale;
        public const double PlanetLaneGap = 120d * GalaxyTuning.PlanetOrbitScale;
        public const double TravelerOrbitOffset = 300d * GalaxyTuning.PlanetOrbitScale;
        public const double TravelerLaneGap = 5d * GalaxyTuning.PlanetOrbitScale;

        public const double MoonInitialGap = 35d * GalaxyTuning.MoonOrbitScale;
        public const double MoonVisualGap = 18d * GalaxyTuning.MoonOrbitScale;
        public const double MoonLaneGap = 14d * GalaxyTuning.MoonOrbitScale;
        public const double RingInnerGap = 75d * GalaxyTuning.MoonOrbitScale;

        public const double FirstLiveShipAxis = 420d * GalaxyTuning.SystemDistanceScale;
        public const double LiveShipLaneGap = 85d * GalaxyTuning.SystemDistanceScale;
    }
}
