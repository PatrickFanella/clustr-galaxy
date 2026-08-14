using System;
using System.Collections.Generic;
using System.Linq;
using ClustrGalaxy.Gameplay;

namespace ClustrGalaxy.Constellations
{
    [Serializable]
    public sealed class PersonalConstellation
    {
        public string id = string.Empty;
        public long revision_id;
        public long spatial_catalog_id;
        public string snapshot_namespace = string.Empty;
        public string name = string.Empty;
        public string note = string.Empty;
        public string[] member_stable_ids = Array.Empty<string>();
        public string created_utc = string.Empty;

        public SurveySnapshotKey SnapshotKey => new(revision_id, spatial_catalog_id, snapshot_namespace);

        public static PersonalConstellation Create(SurveySnapshotKey snapshot, string name, string note,
            IEnumerable<string> memberStableIds, IEnumerable<string> knownStableIds, string id = null)
        {
            ValidateText(name, 1, 60, nameof(name));
            ValidateText(note, 1, 500, nameof(note));
            var members = NormalizeMembers(memberStableIds);
            if (members.Length < 3 || members.Length > 12)
                throw new ArgumentException("A personal constellation requires 3 to 12 unique stable IDs.", nameof(memberStableIds));
            var known = new HashSet<string>(knownStableIds ?? Array.Empty<string>(), StringComparer.Ordinal);
            if (members.Any(member => !known.Contains(member)))
                throw new ArgumentException("Every personal constellation member must be known in Survey.", nameof(memberStableIds));
            return new PersonalConstellation
            {
                id = string.IsNullOrWhiteSpace(id) ? "personal:" + Guid.NewGuid().ToString("N") : id,
                revision_id = snapshot.RevisionId,
                spatial_catalog_id = snapshot.CatalogId,
                snapshot_namespace = snapshot.StorageNamespace,
                name = name.Trim(), note = note.Trim(), member_stable_ids = members,
                created_utc = DateTime.UtcNow.ToString("O")
            };
        }

        public bool BelongsTo(SurveySnapshotKey snapshot) =>
            revision_id == snapshot.RevisionId && spatial_catalog_id == snapshot.CatalogId &&
            string.Equals(snapshot_namespace, snapshot.StorageNamespace, StringComparison.Ordinal);

        internal static void ValidateText(string value, int minimum, int maximum, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Trim().Length < minimum || value.Trim().Length > maximum ||
                value.Any(char.IsControl))
                throw new ArgumentException($"Value must contain {minimum} to {maximum} non-control characters.", parameterName);
        }

        internal static string[] NormalizeMembers(IEnumerable<string> values)
        {
            var raw = (values ?? Array.Empty<string>()).ToArray();
            if (raw.Any(string.IsNullOrWhiteSpace)) throw new ArgumentException("Stable IDs may not be blank.", nameof(values));
            var normalized = raw.OrderBy(value => value, StringComparer.Ordinal).ToArray();
            if (normalized.Distinct(StringComparer.Ordinal).Count() != normalized.Length)
                throw new ArgumentException("Stable IDs may not be duplicated.", nameof(values));
            return normalized;
        }
    }
}
