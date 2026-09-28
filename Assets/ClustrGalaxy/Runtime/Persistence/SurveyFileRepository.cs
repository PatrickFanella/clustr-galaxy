using System;
using System.Globalization;
using System.Linq;
using System.Reflection;
using ClustrGalaxy.Core.Domain;
using ClustrGalaxy.Core.Persistence;
using ClustrGalaxy.Core.Survey;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace ClustrGalaxy.Persistence
{
    [Serializable]
    internal sealed class SurveyStateDocument
    {
        // Zero until read: a document without schema_version must not look current.
        public int schema_version;
        public string profile_id = string.Empty;
        public long revision_id;
        public long catalog_id;
        public string snapshot_namespace = string.Empty;
        public string starting_system_id = string.Empty;
        public string[] known_entities = Array.Empty<string>();
        public string[] identified_entities = Array.Empty<string>();
        public string[] known_routes = Array.Empty<string>();
        public string[] completed_scans = Array.Empty<string>();
        public string[] completed_constellations = Array.Empty<string>();
        public string[] discoveries = Array.Empty<string>();
        public string[] completed_contracts = Array.Empty<string>();
        public string[] achievements = Array.Empty<string>();
        public string[] processed_intents = Array.Empty<string>();
        public PersonalConstellationDocument[] personal_constellations = Array.Empty<PersonalConstellationDocument>();
        public string[] queued_personal_constellations = Array.Empty<string>();
        public string pending_queue_id = string.Empty;
        public int scanned_post_count;
        public int revealed_route_count;
        public int completed_system_count;
        public int completed_authored_constellation_count;
        public int scanner_level = 1;
        public int authored_sequence_progress;
        public string saved_utc = string.Empty;
    }

    [Serializable]
    internal sealed class PersonalConstellationDocument
    {
        public string id = string.Empty;
        public string name = string.Empty;
        public string reason = string.Empty;
        public string[] members = Array.Empty<string>();
        public string created_utc = string.Empty;
    }

    public sealed class SurveyFileRepository : ISurveyRepository
    {
        private readonly IDurableDocumentStore documents;

        public SurveyFileRepository(IDurableDocumentStore documentStore)
        {
            documents = documentStore ?? throw new ArgumentNullException(nameof(documentStore));
        }

        public static SurveyFileRepository OpenDefault() => new(
            new FileDurableDocumentStore(Application.persistentDataPath));

        public SurveyState LoadOrCreate(string profileId, SurveySnapshotId snapshot, GalaxyEntityId startingSystemId)
        {
            var path = PathFor(profileId, snapshot);
            if (!documents.TryRead(path, out var json)) return Fresh(profileId, snapshot, startingSystemId);
            if (!TryParse(json, profileId, snapshot, out var state))
            {
                documents.Quarantine(path);
                return Fresh(profileId, snapshot, startingSystemId);
            }
            if (state.StartingSystemId != startingSystemId)
                throw new InvalidOperationException("The v2 Survey profile is already bound to a different starting system.");
            return state;
        }

        public void Save(SurveyState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            var document = ToDocument(state);
            document.schema_version = SurveyState.SchemaVersion;
            var json = JsonUtility.ToJson(document, false);
            documents.Write(PathFor(state.ProfileId, state.Snapshot), json,
                candidate => TryParse(candidate, state.ProfileId, state.Snapshot, out _));
        }

        private static SurveyState Fresh(string profileId, SurveySnapshotId snapshot, GalaxyEntityId startingSystemId) =>
            new(snapshot, profileId, startingSystemId, new[] { startingSystemId }, new[] { startingSystemId });

        private static string PathFor(string profileId, SurveySnapshotId snapshot) =>
            ProfilePaths.Survey(profileId, snapshot.StorageNamespace, snapshot.RevisionId, snapshot.CatalogId);

        private static bool TryParse(string json, string profileId, SurveySnapshotId snapshot, out SurveyState state)
        {
            state = null;
            try
            {
                // JsonUtility keeps defaults for absent keys; a v2 document must contain every field.
                if (!HasEveryField(JObject.Parse(json), typeof(SurveyStateDocument))) return false;
                var value = JsonUtility.FromJson<SurveyStateDocument>(json);
                if (value == null || value.schema_version != SurveyState.SchemaVersion ||
                    !string.Equals(value.profile_id, profileId, StringComparison.Ordinal) ||
                    value.revision_id != snapshot.RevisionId || value.catalog_id != snapshot.CatalogId ||
                    !string.Equals(value.snapshot_namespace, snapshot.StorageNamespace, StringComparison.Ordinal) ||
                    string.IsNullOrWhiteSpace(value.starting_system_id)) return false;
                var personal = (value.personal_constellations ?? Array.Empty<PersonalConstellationDocument>())
                    .Select(item => new PersonalConstellationRecord(item.id, item.name, item.reason,
                        (item.members ?? Array.Empty<string>()).Select(member => (GalaxyEntityId)member),
                        ParseUtc(item.created_utc))).ToArray();
                DateTime? saved = string.IsNullOrWhiteSpace(value.saved_utc) ? null : ParseUtc(value.saved_utc);
                state = new SurveyState(snapshot, profileId, (GalaxyEntityId)value.starting_system_id,
                    StringsToEntities(value.known_entities), StringsToEntities(value.identified_entities),
                    StringsToRoutes(value.known_routes), StringsToEntities(value.completed_scans),
                    value.completed_constellations, value.discoveries, value.completed_contracts, value.achievements,
                    value.processed_intents, personal, value.queued_personal_constellations, value.pending_queue_id,
                    value.scanned_post_count, value.revealed_route_count, value.completed_system_count,
                    value.completed_authored_constellation_count, value.scanner_level, value.authored_sequence_progress, saved);
                return true;
            }
            catch (Exception) { state = null; return false; }
        }

        private static DateTime ParseUtc(string value) =>
            DateTime.ParseExact(value, "O", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

        private static bool HasEveryField(JObject document, Type type)
        {
            foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
                if (document[field.Name] == null) return false;
            if (type == typeof(SurveyStateDocument) && document["personal_constellations"] is JArray items)
                foreach (var item in items)
                    if (item is not JObject member || !HasEveryField(member, typeof(PersonalConstellationDocument))) return false;
            return true;
        }

        private static SurveyStateDocument ToDocument(SurveyState state) => new()
        {
            profile_id = state.ProfileId,
            revision_id = state.Snapshot.RevisionId,
            catalog_id = state.Snapshot.CatalogId,
            snapshot_namespace = state.Snapshot.StorageNamespace,
            starting_system_id = state.StartingSystemId.Value,
            known_entities = state.KnownEntities.Select(value => value.Value).ToArray(),
            identified_entities = state.IdentifiedEntities.Select(value => value.Value).ToArray(),
            known_routes = state.KnownRoutes.Select(value => value.Value).ToArray(),
            completed_scans = state.CompletedScans.Select(value => value.Value).ToArray(),
            completed_constellations = state.CompletedConstellations.ToArray(),
            discoveries = state.Discoveries.ToArray(),
            completed_contracts = state.CompletedContracts.ToArray(),
            achievements = state.Achievements.ToArray(),
            processed_intents = state.ProcessedIntentIds.ToArray(),
            personal_constellations = state.PersonalConstellations.Select(value => new PersonalConstellationDocument
            {
                id = value.Id, name = value.Name, reason = value.Reason,
                members = value.Members.Select(member => member.Value).ToArray(), created_utc = value.CreatedUtc.ToString("O")
            }).ToArray(),
            queued_personal_constellations = state.QueuedPersonalConstellationIds.ToArray(),
            pending_queue_id = state.PendingQueueId,
            scanned_post_count = state.ScannedPostCount,
            revealed_route_count = state.RevealedRouteCount,
            completed_system_count = state.CompletedSystemCount,
            completed_authored_constellation_count = state.CompletedAuthoredConstellationCount,
            scanner_level = state.ScannerLevel,
            authored_sequence_progress = state.AuthoredSequenceProgress,
            saved_utc = state.SavedUtc?.ToString("O") ?? string.Empty
        };

        private static GalaxyEntityId[] StringsToEntities(string[] values) =>
            (values ?? Array.Empty<string>()).Select(value => (GalaxyEntityId)value).ToArray();
        private static GalaxyRouteId[] StringsToRoutes(string[] values) =>
            (values ?? Array.Empty<string>()).Select(value => (GalaxyRouteId)value).ToArray();
    }
}
