using System;
using System.Collections.Generic;

namespace ClustrGalaxy.Gameplay
{
    /// <summary>
    /// JSON-safe, snapshot-bound Survey state. All collections are arrays so
    /// Unity's built-in JSON serializer can persist the exact offline profile.
    /// </summary>
    [Serializable]
    public sealed class SurveyDiscoveryState
    {
        public const int CurrentSchemaVersion = 1;

        public int schema_version = CurrentSchemaVersion;
        public long revision_id;
        public long spatial_catalog_id;
        public string snapshot_namespace = string.Empty;
        public string starting_system_id = string.Empty;
        public string[] known_entity_ids = Array.Empty<string>();
        public string[] known_route_ids = Array.Empty<string>();
        public string[] completed_scan_ids = Array.Empty<string>();
        public string[] constellation_member_ids = Array.Empty<string>();
        public string[] completed_contract_ids = Array.Empty<string>();
        public string[] achievement_ids = Array.Empty<string>();
        public int scanner_level = 1;
        public string last_saved_utc = string.Empty;

        public static SurveyDiscoveryState CreateFresh(SurveySnapshotKey key, string startingSystemId)
        {
            var state = new SurveyDiscoveryState
            {
                revision_id = key.RevisionId,
                spatial_catalog_id = key.CatalogId,
                snapshot_namespace = key.StorageNamespace,
                starting_system_id = startingSystemId ?? string.Empty
            };
            state.known_entity_ids = Normalize(new[] { state.starting_system_id });
            return state;
        }

        public bool BelongsTo(SurveySnapshotKey key) =>
            schema_version == CurrentSchemaVersion &&
            revision_id == key.RevisionId &&
            spatial_catalog_id == key.CatalogId &&
            string.Equals(snapshot_namespace, key.StorageNamespace, StringComparison.Ordinal) &&
            !string.IsNullOrWhiteSpace(starting_system_id);

        public void NormalizeCollections()
        {
            known_entity_ids = Normalize(known_entity_ids);
            known_route_ids = Normalize(known_route_ids);
            completed_scan_ids = Normalize(completed_scan_ids);
            constellation_member_ids = Normalize(constellation_member_ids);
            completed_contract_ids = Normalize(completed_contract_ids);
            achievement_ids = Normalize(achievement_ids);
            scanner_level = Math.Max(1, scanner_level);
        }

        public static string[] Normalize(IEnumerable<string> ids)
        {
            var sorted = new SortedSet<string>(StringComparer.Ordinal);
            if (ids != null)
            {
                foreach (var id in ids)
                {
                    if (!string.IsNullOrWhiteSpace(id))
                    {
                        sorted.Add(id);
                    }
                }
            }

            var result = new string[sorted.Count];
            sorted.CopyTo(result);
            return result;
        }
    }
}
