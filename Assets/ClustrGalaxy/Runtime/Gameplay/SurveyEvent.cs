using System;
using System.Collections.Generic;

namespace ClustrGalaxy.Gameplay
{
    public enum SurveyEventType
    {
        ScanCompleted,
        SystemCompleted,
        AuthoredConstellationCompleted,
        PersonalConstellationSaved,
        PilotMasteryArrival
    }

    public sealed class SurveyEvent
    {
        private SurveyEvent(SurveyEventType type, SurveySnapshotKey snapshotKey, string scannedId, IReadOnlyList<string> entities,
            IReadOnlyList<string> routes, DateTime utc, int scannerLevel)
        {
            Type = type;
            SnapshotKey = snapshotKey;
            ScannedId = scannedId ?? string.Empty;
            NewlyRevealedEntityIds = entities ?? Array.Empty<string>();
            NewlyRevealedRouteIds = routes ?? Array.Empty<string>();
            Utc = utc;
            ScannerLevel = scannerLevel;
        }

        public SurveyEventType Type { get; }
        public SurveySnapshotKey SnapshotKey { get; }
        public string ScannedId { get; }
        public IReadOnlyList<string> NewlyRevealedEntityIds { get; }
        public IReadOnlyList<string> NewlyRevealedRouteIds { get; }
        public DateTime Utc { get; }
        public int ScannerLevel { get; }

        public static SurveyEvent ScanCompleted(SurveySnapshotKey snapshotKey, string scannedId,
            IReadOnlyList<string> entities, IReadOnlyList<string> routes, DateTime utc, int scannerLevel) =>
            new(SurveyEventType.ScanCompleted, snapshotKey, scannedId, entities, routes, utc, scannerLevel);

        public static SurveyEvent SystemCompleted(SurveySnapshotKey snapshotKey, string systemId, DateTime utc) =>
            new(SurveyEventType.SystemCompleted, snapshotKey, systemId, Array.Empty<string>(), Array.Empty<string>(), utc, 0);

        public static SurveyEvent AuthoredConstellationCompleted(SurveySnapshotKey snapshotKey, string constellationId, DateTime utc) =>
            new(SurveyEventType.AuthoredConstellationCompleted, snapshotKey, constellationId, Array.Empty<string>(), Array.Empty<string>(), utc, 0);

        public static SurveyEvent PersonalConstellationSaved(SurveySnapshotKey snapshotKey, string constellationId, DateTime utc) =>
            new(SurveyEventType.PersonalConstellationSaved, snapshotKey, constellationId, Array.Empty<string>(), Array.Empty<string>(), utc, 0);

        /// <summary>Future travel code emits this only after a real Mastery-mode arrival/docking check.</summary>
        public static SurveyEvent PilotMasteryArrival(SurveySnapshotKey snapshotKey, string destinationId, DateTime utc) =>
            new(SurveyEventType.PilotMasteryArrival, snapshotKey, destinationId, Array.Empty<string>(), Array.Empty<string>(), utc, 0);
    }
}
