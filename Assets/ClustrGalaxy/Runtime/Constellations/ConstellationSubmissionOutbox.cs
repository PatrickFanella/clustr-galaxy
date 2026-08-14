using System;
using System.IO;
using System.Linq;
using System.Text;
using ClustrGalaxy.Gameplay;
using UnityEngine;

namespace ClustrGalaxy.Constellations
{
    public enum ConstellationSubmissionState { AwaitingBackend }

    [Serializable]
    public sealed class ConstellationSubmission
    {
        public string submission_id = string.Empty;
        public long revision_id;
        public long spatial_catalog_id;
        public string snapshot_namespace = string.Empty;
        public string[] member_stable_ids = Array.Empty<string>();
        public string name = string.Empty;
        public string note = string.Empty;
        public string created_utc = string.Empty;
        public ConstellationSubmissionState state = ConstellationSubmissionState.AwaitingBackend;
    }
    [Serializable] internal sealed class ConstellationSubmissionDocument { public ConstellationSubmission[] submissions = Array.Empty<ConstellationSubmission>(); }

    /// <summary>A durable local queue. This class intentionally contains no HTTP or credential code.</summary>
    public sealed class ConstellationSubmissionOutbox
    {
        private readonly PersonalConstellationStore personalStore;
        private readonly string path;
        private ConstellationSubmissionDocument document;
        public string StoragePath => path;
        public ConstellationSubmission[] Submissions => document.submissions ?? Array.Empty<ConstellationSubmission>();

        public ConstellationSubmissionOutbox(PersonalConstellationStore store)
        {
            personalStore = store ?? throw new ArgumentNullException(nameof(store));
            path = Path.Combine(Path.GetDirectoryName(store.StoragePath), "constellation-submissions.json");
            document = TryLoad(path) ?? new ConstellationSubmissionDocument();
            document.submissions ??= Array.Empty<ConstellationSubmission>();
        }

        public ConstellationSubmission QueueForSubmission(string personalId, bool confirmed)
        {
            if (!confirmed) throw new InvalidOperationException("Submission must be explicitly confirmed.");
            var personal = personalStore.Get(personalId) ?? throw new ArgumentException("Personal constellation was not found in this snapshot.", nameof(personalId));
            var duplicate = document.submissions.FirstOrDefault(item => item != null &&
                item.revision_id == personal.revision_id && item.spatial_catalog_id == personal.spatial_catalog_id &&
                string.Equals(item.snapshot_namespace, personal.snapshot_namespace, StringComparison.Ordinal) &&
                item.member_stable_ids != null && item.member_stable_ids.SequenceEqual(personal.member_stable_ids) &&
                string.Equals(item.name, personal.name, StringComparison.Ordinal) && string.Equals(item.note, personal.note, StringComparison.Ordinal));
            if (duplicate != null) return duplicate;
            var item = new ConstellationSubmission
            {
                submission_id = "submission:" + Guid.NewGuid().ToString("N"),
                revision_id = personal.revision_id, spatial_catalog_id = personal.spatial_catalog_id,
                snapshot_namespace = personal.snapshot_namespace, member_stable_ids = personal.member_stable_ids,
                name = personal.name, note = personal.note, created_utc = DateTime.UtcNow.ToString("O"),
                state = ConstellationSubmissionState.AwaitingBackend
            };
            document.submissions = document.submissions.Concat(new[] { item }).OrderBy(entry => entry.submission_id, StringComparer.Ordinal).ToArray();
            Save(); return item;
        }

        public bool IsQueued(string personalId)
        {
            var personal = personalStore.Get(personalId); if (personal == null) return false;
            return document.submissions.Any(item => item != null && item.revision_id == personal.revision_id &&
                item.spatial_catalog_id == personal.spatial_catalog_id &&
                string.Equals(item.snapshot_namespace, personal.snapshot_namespace, StringComparison.Ordinal) &&
                item.member_stable_ids != null && item.member_stable_ids.SequenceEqual(personal.member_stable_ids) &&
                string.Equals(item.name, personal.name, StringComparison.Ordinal) &&
                string.Equals(item.note, personal.note, StringComparison.Ordinal));
        }

        private void Save()
        {
            var directory = Path.GetDirectoryName(path); Directory.CreateDirectory(directory);
            var temporary = path + ".tmp";
            var bytes = new UTF8Encoding(false).GetBytes(JsonUtility.ToJson(document, false));
            using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None)) { stream.Write(bytes, 0, bytes.Length); stream.Flush(true); }
            if (TryLoad(temporary) == null) throw new InvalidDataException("Submission outbox post-write validation failed.");
            if (File.Exists(path)) File.Replace(temporary, path, null); else File.Move(temporary, path);
        }
        private static ConstellationSubmissionDocument TryLoad(string storagePath)
        {
            if (!File.Exists(storagePath)) return null;
            try { return JsonUtility.FromJson<ConstellationSubmissionDocument>(File.ReadAllText(storagePath, Encoding.UTF8)); }
            catch (Exception) { return null; }
        }
    }
}
