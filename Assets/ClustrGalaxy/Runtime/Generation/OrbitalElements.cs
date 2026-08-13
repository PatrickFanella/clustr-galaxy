namespace ClustrGalaxy.Generation
{
    public readonly struct OrbitalElements
    {
        public OrbitalElements(
            double semiMajorAxis,
            double eccentricity,
            double inclinationRadians,
            double longitudeAscendingNodeRadians,
            double argumentOfPeriapsisRadians,
            double meanAnomalyAtEpochRadians)
        {
            SemiMajorAxis = semiMajorAxis;
            Eccentricity = eccentricity;
            InclinationRadians = inclinationRadians;
            LongitudeAscendingNodeRadians = longitudeAscendingNodeRadians;
            ArgumentOfPeriapsisRadians = argumentOfPeriapsisRadians;
            MeanAnomalyAtEpochRadians = meanAnomalyAtEpochRadians;
        }

        public double SemiMajorAxis { get; }
        public double Eccentricity { get; }
        public double InclinationRadians { get; }
        public double LongitudeAscendingNodeRadians { get; }
        public double ArgumentOfPeriapsisRadians { get; }
        public double MeanAnomalyAtEpochRadians { get; }
    }
}
