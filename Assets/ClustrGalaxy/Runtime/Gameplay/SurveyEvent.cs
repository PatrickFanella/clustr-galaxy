using System;
using System.Collections.Generic;

namespace ClustrGalaxy.Gameplay
{
    public sealed class SurveyEvent
    {
        private SurveyEvent(SurveySnapshotKey snapshotKey, string scannedId, IReadOnlyList<string> entities,
            IReadOnlyList<string> routes, DateTime utc, int scannerLevel)
        {
            SnapshotKey = snapshotKey;
            ScannedId = scannedId ?? string.Empty;
            NewlyRevealedEntityIds = entities ?? Array.Empty<string>();
            NewlyRevealedRouteIds = routes ?? Array.Empty<string>();
            Utc = utc;
            ScannerLevel = scannerLevel;
        }

        public SurveySnapshotKey SnapshotKey { get; }
        public string ScannedId { get; }
        public IReadOnlyList<string> NewlyRevealedEntityIds { get; }
        public IReadOnlyList<string> NewlyRevealedRouteIds { get; }
        public DateTime Utc { get; }
        public int ScannerLevel { get; }

        public static SurveyEvent ScanCompleted(SurveySnapshotKey snapshotKey, string scannedId,
            IReadOnlyList<string> entities, IReadOnlyList<string> routes, DateTime utc, int scannerLevel) =>
            new(snapshotKey, scannedId, entities, routes, utc, scannerLevel);
    }
}
