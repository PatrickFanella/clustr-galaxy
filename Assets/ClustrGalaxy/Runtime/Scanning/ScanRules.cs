using System;
using System.Collections.Generic;
using System.Linq;

namespace ClustrGalaxy.Scanning
{
    /// <summary>Pure, deterministic scanning policy. It has no Unity or persistence dependency.</summary>
    public static class ScanRules
    {
        public static ScanResult Reveal(string scannedId, IEnumerable<ScanRoute> routes, int scannerLevel) =>
            Reveal(scannedId, routes, scannerLevel, Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(), false);

        public static ScanResult Reveal(string scannedId, IEnumerable<ScanRoute> routes, int scannerLevel,
            IEnumerable<string> knownEntities, IEnumerable<string> knownRoutes, IEnumerable<string> loadedChildren,
            bool completedSystem)
        {
            if (string.IsNullOrWhiteSpace(scannedId)) return ScanResult.NoSignal;
            var knownEntitySet = new HashSet<string>(knownEntities ?? Array.Empty<string>(), StringComparer.Ordinal);
            var knownRouteSet = new HashSet<string>(knownRoutes ?? Array.Empty<string>(), StringComparer.Ordinal);
            // Entities are returned in ordinal stable-ID order. Routes preserve
            // published relevance order: descending weight, then stable ID.
            var entities = new SortedSet<string>(StringComparer.Ordinal);
            var routeIds = new List<string>();
            var seenRouteIds = new HashSet<string>(StringComparer.Ordinal);

            foreach (var child in loadedChildren ?? Array.Empty<string>())
            {
                if (!string.IsNullOrWhiteSpace(child) && !knownEntitySet.Contains(child)) entities.Add(child);
            }

            var candidates = (routes ?? Array.Empty<ScanRoute>())
                .Where(route => string.Equals(route.SourceId, scannedId, StringComparison.Ordinal) &&
                                !string.IsNullOrWhiteSpace(route.Id) && !knownRouteSet.Contains(route.Id))
                .OrderByDescending(route => route.Weight)
                .ThenBy(route => route.Id, StringComparer.Ordinal)
                .ToArray();
            var revealLimit = scannerLevel >= 3 ? 4 : scannerLevel >= 2 ? 2 : 1;
            foreach (var route in candidates.GroupBy(route => route.Id, StringComparer.Ordinal)
                         .Select(group => group.First()).Take(revealLimit))
            {
                routeIds.Add(route.Id);
                seenRouteIds.Add(route.Id);
                if (!knownEntitySet.Contains(route.TargetId)) entities.Add(route.TargetId);
            }

            // Completing a system is the only way to expose deliberately weak
            // signals beyond the normal scanner-tier budget.
            if (completedSystem)
            {
                foreach (var route in candidates.GroupBy(route => route.Id, StringComparer.Ordinal)
                             .Select(group => group.First()).Skip(revealLimit).Where(route => route.Weight <= 2f))
                {
                    if (!seenRouteIds.Add(route.Id)) continue;
                    routeIds.Add(route.Id);
                    if (!knownEntitySet.Contains(route.TargetId)) entities.Add(route.TargetId);
                }
            }
            return new ScanResult(SignalScanState.Complete, false, entities.ToArray(), routeIds.ToArray());
        }
    }
}
