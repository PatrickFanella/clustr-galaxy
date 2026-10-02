using System;
using System.Globalization;

namespace ClustrGalaxy.Data
{
    public enum GalaxyDataMode
    {
        LocalDemo,
        DeterministicFixture,
        Connecting,
        LegacyUnpinned,
        LivePinned,
        OfflinePinned,
        Failed
    }

    public static class GalaxyDataStatus
    {
        public static string Pinned(SpatialManifest manifest, bool offline, int landmarkCount)
        {
            var label = $"{(offline ? "OFFLINE" : "LIVE")} REV {manifest.revision_id} / CAT {manifest.spatial_catalog_id}  {landmarkCount} LANDMARKS";
            if (!offline || !TryFormatPublicationTime(manifest.published_at, out var published))
            {
                return label;
            }

            return $"{label}  •  PUBLISHED {published}";
        }

        public static string Legacy(int landmarkCount) =>
            $"DEMO / LEGACY / UNPINNED  {landmarkCount} LANDMARKS";

        public static string Fallback(string reason) =>
            $"LOCAL DEMO FALLBACK  {reason}";

        private static bool TryFormatPublicationTime(string value, out string formatted)
        {
            formatted = string.Empty;
            if (!DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var published))
            {
                return false;
            }

            formatted = published.ToString("yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture);
            return true;
        }
    }
}
