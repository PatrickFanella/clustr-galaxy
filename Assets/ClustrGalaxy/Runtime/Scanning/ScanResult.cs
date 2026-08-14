using System;
using System.Collections.Generic;

namespace ClustrGalaxy.Scanning
{
    public readonly struct ScanRoute
    {
        public ScanRoute(string id, string sourceId, string targetId, float weight)
        {
            Id = id ?? string.Empty;
            SourceId = sourceId ?? string.Empty;
            TargetId = targetId ?? string.Empty;
            Weight = weight;
        }

        public string Id { get; }
        public string SourceId { get; }
        public string TargetId { get; }
        public float Weight { get; }
    }

    public enum SignalScanState { Idle, Charging, Complete, OutOfRange, NoSignal }

    public sealed class ScanResult
    {
        public static readonly ScanResult NoSignal = new(SignalScanState.NoSignal, false, Array.Empty<string>(), Array.Empty<string>());
        public static readonly ScanResult AlreadyScannedResult = new(SignalScanState.Complete, true, Array.Empty<string>(), Array.Empty<string>());

        public ScanResult(SignalScanState state, bool alreadyScanned, IReadOnlyList<string> revealedEntityIds,
            IReadOnlyList<string> revealedRouteIds)
        {
            State = state;
            AlreadyScanned = alreadyScanned;
            RevealedEntityIds = revealedEntityIds ?? Array.Empty<string>();
            RevealedRouteIds = revealedRouteIds ?? Array.Empty<string>();
        }

        public SignalScanState State { get; }
        public bool AlreadyScanned { get; }
        public IReadOnlyList<string> RevealedEntityIds { get; }
        public IReadOnlyList<string> RevealedRouteIds { get; }
        public bool HasRevelations => RevealedEntityIds.Count > 0 || RevealedRouteIds.Count > 0;
    }
}
