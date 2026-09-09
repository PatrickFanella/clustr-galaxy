using System;
using ClustrGalaxy.Generation;
using UnityEngine;
using ClustrGalaxy.World;

namespace ClustrGalaxy.Simulation
{
    public static class KeplerOrbitMath
    {
        private const double Tau = Math.PI * 2d;

        /// <summary>Invariant coefficients for the same Kepler equations and operation order.</summary>
        public readonly struct PreparedOrbit
        {
            private readonly bool active;
            private readonly double axis, eccentricity, meanMotion, epoch, minorRatio;
            private readonly double argCos, argSin, inclinationCos, inclinationSin, nodeCos, nodeSin;
            public PreparedOrbit(OrbitalElements elements, double parentMu)
            {
                this = default;
                active = !(elements.SemiMajorAxis <= 0d || parentMu <= 0d);
                if (!active) return;
                axis = elements.SemiMajorAxis;
                eccentricity = Math.Max(0d, Math.Min(0.95d, elements.Eccentricity));
                meanMotion = Math.Sqrt(parentMu / (axis * axis * axis));
                epoch = elements.MeanAnomalyAtEpochRadians;
                minorRatio = Math.Sqrt(1d - eccentricity * eccentricity);
                argCos = Math.Cos(elements.ArgumentOfPeriapsisRadians); argSin = Math.Sin(elements.ArgumentOfPeriapsisRadians);
                inclinationCos = Math.Cos(elements.InclinationRadians); inclinationSin = Math.Sin(elements.InclinationRadians);
                nodeCos = Math.Cos(elements.LongitudeAscendingNodeRadians); nodeSin = Math.Sin(elements.LongitudeAscendingNodeRadians);
            }
            public DoubleVector3 PositionAtTime(double simulationTime)
            {
                if (!active) return DoubleVector3.Zero;
                var anomaly = SolveEccentricAnomaly(WrapRadians(epoch + meanMotion * simulationTime), eccentricity);
                var x = axis * (Math.Cos(anomaly) - eccentricity);
                var z = axis * minorRatio * Math.Sin(anomaly);
                var rotatedX = argCos * x + argSin * z;
                z = -argSin * x + argCos * z;
                var y = -inclinationSin * z;
                z = inclinationCos * z;
                var finalX = nodeCos * rotatedX + nodeSin * z;
                z = -nodeSin * rotatedX + nodeCos * z;
                return new DoubleVector3(finalX, y, z);
            }
        }

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
            var position = PrecisePositionAtTime(elements, parentGravitationalParameter, simulationTime);
            return new Vector3((float)position.X, (float)position.Y, (float)position.Z);
        }

        public static DoubleVector3 PrecisePositionAtTime(OrbitalElements elements, double parentGravitationalParameter, double simulationTime)
        {
            if (elements.SemiMajorAxis <= 0d || parentGravitationalParameter <= 0d) return DoubleVector3.Zero;
            var eccentricity = Math.Max(0d, Math.Min(0.95d, elements.Eccentricity));
            var meanMotion = Math.Sqrt(
                parentGravitationalParameter /
                (elements.SemiMajorAxis * elements.SemiMajorAxis * elements.SemiMajorAxis));
            var meanAnomaly = WrapRadians(
                elements.MeanAnomalyAtEpochRadians + meanMotion * simulationTime);
            var eccentricAnomaly = SolveEccentricAnomaly(meanAnomaly, eccentricity);

            return PrecisePositionAtEccentricAnomaly(elements, eccentricAnomaly);
        }

        // Uniform geometric sampling draws an ellipse without bunching vertices at apoapsis.
        public static Vector3 PositionAtEccentricAnomaly(OrbitalElements elements, double eccentricAnomaly)
        {
            var position = PrecisePositionAtEccentricAnomaly(elements, eccentricAnomaly);
            return new Vector3((float)position.X, (float)position.Y, (float)position.Z);
        }

        private static DoubleVector3 PrecisePositionAtEccentricAnomaly(OrbitalElements elements, double eccentricAnomaly)
        {
            var eccentricity = Math.Max(0d, Math.Min(0.95d, elements.Eccentricity));
            var planeX = elements.SemiMajorAxis * (Math.Cos(eccentricAnomaly) - eccentricity);
            var planeZ = elements.SemiMajorAxis *
                Math.Sqrt(1d - eccentricity * eccentricity) * Math.Sin(eccentricAnomaly);

            RotateY(ref planeX, ref planeZ, elements.ArgumentOfPeriapsisRadians);
            var inclinedY = -Math.Sin(elements.InclinationRadians) * planeZ;
            var inclinedZ = Math.Cos(elements.InclinationRadians) * planeZ;
            RotateY(ref planeX, ref inclinedZ, elements.LongitudeAscendingNodeRadians);

            return new DoubleVector3(planeX, inclinedY, inclinedZ);
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
