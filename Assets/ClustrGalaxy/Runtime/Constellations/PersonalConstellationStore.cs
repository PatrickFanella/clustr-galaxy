using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using ClustrGalaxy.Gameplay;
using UnityEngine;

namespace ClustrGalaxy.Constellations
{
    [Serializable] internal sealed class PersonalConstellationDocument { public PersonalConstellation[] constellations = Array.Empty<PersonalConstellation>(); }

    /// <summary>Profile-local authored overlays, deliberately separated from factual Survey state.</summary>
    public sealed class PersonalConstellationStore
    {
        private readonly string path;
        private readonly SurveySnapshotKey snapshot;
        private readonly SurveyEventBus eventBus;
        private PersonalConstellationDocument document;

        private PersonalConstellationStore(string storagePath, SurveySnapshotKey snapshotKey, PersonalConstellationDocument loaded,
            SurveyEventBus sharedEventBus)
        { path = storagePath; snapshot = snapshotKey; document = loaded; eventBus = sharedEventBus; Normalize(); }

        public string StoragePath => path;
        public SurveySnapshotKey SnapshotKey => snapshot;
        public IReadOnlyList<PersonalConstellation> Constellations => document.constellations.Where(item => item != null && item.BelongsTo(snapshot)).ToArray();

        public static PersonalConstellationStore OpenDefault(string profileId, SurveySnapshotKey snapshotKey,
            SurveyEventBus sharedEventBus = null) => Open(Application.persistentDataPath, profileId, snapshotKey, sharedEventBus);

        public static PersonalConstellationStore Open(string persistentDataPath, string profileId, SurveySnapshotKey snapshotKey,
            SurveyEventBus sharedEventBus = null)
        {
            if (string.IsNullOrWhiteSpace(persistentDataPath)) throw new ArgumentException("A persistent-data root is required.", nameof(persistentDataPath));
            var safeProfile = RequirePathSegment(profileId);
            var storagePath = Path.Combine(persistentDataPath, "ClustrGalaxy", "profiles", "v1", safeProfile, "constellations.json");
            var loaded = TryLoad(storagePath) ?? new PersonalConstellationDocument();
            return new PersonalConstellationStore(storagePath, snapshotKey, loaded, sharedEventBus);
        }

        public PersonalConstellation Save(string name, string note, IEnumerable<string> memberStableIds,
            IEnumerable<string> knownStableIds, string id = null)
        {
            var constellation = PersonalConstellation.Create(snapshot, name, note, memberStableIds, knownStableIds, id);
            if (document.constellations.Any(item => item != null && string.Equals(item.id, constellation.id, StringComparison.Ordinal)))
                throw new ArgumentException("A personal constellation with this ID already exists.", nameof(id));
            document.constellations = document.constellations.Concat(new[] { constellation }).ToArray();
            Normalize(); SaveDocument();
            eventBus?.Publish(SurveyEvent.PersonalConstellationSaved(snapshot, constellation.id, DateTime.UtcNow));
            return constellation;
        }

        public PersonalConstellation Get(string id) => Constellations.FirstOrDefault(item => string.Equals(item.id, id, StringComparison.Ordinal));

        private void Normalize()
        {
            document ??= new PersonalConstellationDocument();
            document.constellations ??= Array.Empty<PersonalConstellation>();
            document.constellations = document.constellations.Where(item => item != null && !string.IsNullOrWhiteSpace(item.id))
                .OrderBy(item => item.id, StringComparer.Ordinal).ToArray();
        }

        private void SaveDocument()
        {
            var directory = Path.GetDirectoryName(path); Directory.CreateDirectory(directory);
            var temporary = path + ".tmp";
            WriteAndFlush(temporary, JsonUtility.ToJson(document, false));
            if (TryLoad(temporary) == null) throw new InvalidDataException("Personal constellation post-write validation failed.");
            if (File.Exists(path)) File.Replace(temporary, path, null); else File.Move(temporary, path);
        }

        private static PersonalConstellationDocument TryLoad(string storagePath)
        {
            if (!File.Exists(storagePath)) return null;
            try { return JsonUtility.FromJson<PersonalConstellationDocument>(File.ReadAllText(storagePath, Encoding.UTF8)); }
            catch (Exception) { return null; }
        }

        private static void WriteAndFlush(string destination, string json)
        {
            var bytes = new UTF8Encoding(false).GetBytes(json);
            using var stream = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.None);
            stream.Write(bytes, 0, bytes.Length); stream.Flush(true);
        }
        private static string RequirePathSegment(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Any(c => !(char.IsLetterOrDigit(c) || c == '-' || c == '_')))
                throw new ArgumentException("Profile IDs may only contain letters, digits, '-' or '_'.", nameof(value));
            return value;
        }
    }
}
