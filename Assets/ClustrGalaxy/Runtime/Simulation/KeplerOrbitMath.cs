using System;
using ClustrGalaxy.Generation;
using UnityEngine;

namespace ClustrGalaxy.Simulation
{
    public static class KeplerOrbitMath
    {
        private const double Tau = Math.PI * 2d;

        public static double Period(OrbitalElements elements, double parentGravitationalParameter)
        {
            if (elements.SemiMajorAxis <= 0d || parentGravitationalParameter <= 0d)
            {
                return 0d;
            }

            return Tau * Math.Sqrt(
                elements.SemiMajorAxis * elements.SemiMajorAxis * elements.SemiMajorAxis /
                parentGravitationalParameter);
        }

        public static Vector3 PositionAtTime(
            OrbitalElements elements,
            double parentGravitationalParameter,
            double simulationTime)
        {
            if (elements.SemiMajorAxis <= 0d || parentGravitationalParameter <= 0d)
            {
                return Vector3.zero;
            }

            var eccentricity = Math.Max(0d, Math.Min(0.95d, elements.Eccentricity));
            var meanMotion = Math.Sqrt(
                parentGravitationalParameter /
                (elements.SemiMajorAxis * elements.SemiMajorAxis * elements.SemiMajorAxis));
            var meanAnomaly = WrapRadians(
                elements.MeanAnomalyAtEpochRadians + meanMotion * simulationTime);
            var eccentricAnomaly = SolveEccentricAnomaly(meanAnomaly, eccentricity);

            var planeX = elements.SemiMajorAxis * (Math.Cos(eccentricAnomaly) - eccentricity);
            var planeZ = elements.SemiMajorAxis *
                Math.Sqrt(1d - eccentricity * eccentricity) * Math.Sin(eccentricAnomaly);

            RotateY(ref planeX, ref planeZ, elements.ArgumentOfPeriapsisRadians);
            var inclinedY = -Math.Sin(elements.InclinationRadians) * planeZ;
            var inclinedZ = Math.Cos(elements.InclinationRadians) * planeZ;
            RotateY(ref planeX, ref inclinedZ, elements.LongitudeAscendingNodeRadians);

            return new Vector3((float)planeX, (float)inclinedY, (float)inclinedZ);
        }

        private static double SolveEccentricAnomaly(double meanAnomaly, double eccentricity)
        {
            var eccentricAnomaly = meanAnomaly;
            for (var iteration = 0; iteration < 8; iteration++)
            {
                var correction =
                    (eccentricAnomaly - eccentricity * Math.Sin(eccentricAnomaly) - meanAnomaly) /
                    (1d - eccentricity * Math.Cos(eccentricAnomaly));
                eccentricAnomaly -= correction;
                if (Math.Abs(correction) < 1e-12d)
                {
                    break;
                }
            }

            return eccentricAnomaly;
        }

        private static double WrapRadians(double radians)
        {
            radians %= Tau;
            return radians < 0d ? radians + Tau : radians;
        }

        private static void RotateY(ref double x, ref double z, double radians)
        {
            var cosine = Math.Cos(radians);
            var sine = Math.Sin(radians);
            var rotatedX = cosine * x + sine * z;
            z = -sine * x + cosine * z;
            x = rotatedX;
        }
    }
}
