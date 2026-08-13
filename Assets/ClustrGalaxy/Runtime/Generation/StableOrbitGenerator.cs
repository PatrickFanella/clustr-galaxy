using System;
using System.Text;

namespace ClustrGalaxy.Generation
{
    public static class StableOrbitGenerator
    {
        public const string Version = "orbit-v1";
        private const double Tau = Math.PI * 2d;

        public static OrbitalElements GeneratePlanet(
            long revisionId,
            long catalogId,
            string parentStableId,
            string entityStableId,
            double semiMajorAxis)
        {
            return Generate(revisionId, catalogId, parentStableId, entityStableId,
                "planet", semiMajorAxis, 0.12d, 12d);
        }

        public static OrbitalElements GenerateMoon(
            long revisionId,
            long catalogId,
            string parentStableId,
            string entityStableId,
            double semiMajorAxis)
        {
            return Generate(revisionId, catalogId, parentStableId, entityStableId,
                "moon", semiMajorAxis, 0.06d, 8d);
        }

        private static OrbitalElements Generate(
            long revisionId,
            long catalogId,
            string parentStableId,
            string entityStableId,
            string role,
            double semiMajorAxis,
            double maximumEccentricity,
            double maximumInclinationDegrees)
        {
            var identity = string.Join("|", Version, revisionId, catalogId,
                parentStableId ?? string.Empty, entityStableId ?? string.Empty, role);

            return new OrbitalElements(
                Math.Max(0d, semiMajorAxis),
                Draw(identity, "eccentricity") * maximumEccentricity,
                Draw(identity, "inclination") * maximumInclinationDegrees * Math.PI / 180d,
                Draw(identity, "ascending-node") * Tau,
                Draw(identity, "periapsis") * Tau,
                Draw(identity, "mean-anomaly") * Tau);
        }

        private static double Draw(string identity, string channel)
        {
            var hash = Fnv1A64(identity + "|" + channel);
            return (hash >> 11) * (1d / 9007199254740992d);
        }

        private static ulong Fnv1A64(string value)
        {
            const ulong offset = 14695981039346656037UL;
            const ulong prime = 1099511628211UL;
            var hash = offset;
            var bytes = Encoding.UTF8.GetBytes(value);
            for (var index = 0; index < bytes.Length; index++)
            {
                hash ^= bytes[index];
                hash *= prime;
            }

            return hash;
        }
    }
}
