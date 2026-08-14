using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace ClustrGalaxy.Gameplay
{
    /// <summary>
    /// One explicit Survey session for one player profile and immutable data
    /// snapshot. Opening or reading never writes; callers must deliberately
    /// mutate and save Survey progress.
    /// </summary>
    public sealed class SurveyDiscoveryStore
    {
        public const string DefaultProfileId = "default";
        public const string RelativeRoot = "ClustrGalaxy/profiles/v1";

        private readonly string storagePath;

        private SurveyDiscoveryStore(string profileId, SurveySnapshotKey snapshotKey, string storagePath,
            SurveyDiscoveryState state)
        {
            ProfileId = profileId;
            SnapshotKey = snapshotKey;
            this.storagePath = storagePath;
            State = state;
        }

        public string ProfileId { get; }
        public SurveySnapshotKey SnapshotKey { get; }
        public SurveyDiscoveryState State { get; }
        public string StoragePath => storagePath;
        public string StartingSystemId => State.starting_system_id;

        public static SurveyDiscoveryStore OpenDefault(string profileId, SurveySnapshotKey snapshotKey,
            string startingSystemId) => Open(Application.persistentDataPath, profileId, snapshotKey, startingSystemId);

        public static SurveyDiscoveryStore Open(string persistentDataPath, string profileId,
            SurveySnapshotKey snapshotKey, string startingSystemId)
        {
            if (string.IsNullOrWhiteSpace(persistentDataPath))
            {
                throw new ArgumentException("A persistent-data root is required.", nameof(persistentDataPath));
            }
            if (string.IsNullOrWhiteSpace(startingSystemId))
            {
                throw new ArgumentException("A starting system is required.", nameof(startingSystemId));
            }

            var safeProfileId = RequirePathSegment(profileId, nameof(profileId));
            var safeNamespace = RequirePathSegment(snapshotKey.StorageNamespace, nameof(snapshotKey));
            var profilePath = Path.Combine(persistentDataPath, "ClustrGalaxy", "profiles", "v1", safeProfileId);
            if (!string.Equals(safeNamespace, SurveySnapshotKey.PinnedNamespace, StringComparison.Ordinal))
            {
                profilePath = Path.Combine(profilePath, safeNamespace);
            }

            var storagePath = Path.Combine(profilePath, $"{snapshotKey.RevisionId}-{snapshotKey.CatalogId}.json");
            var state = TryLoad(storagePath, snapshotKey) ??
                SurveyDiscoveryState.CreateFresh(snapshotKey, startingSystemId);
            state.NormalizeCollections();
            return new SurveyDiscoveryStore(safeProfileId, snapshotKey, storagePath, state);
        }

        public bool IsEntityKnown(string stableId) => Contains(State.known_entity_ids, stableId);
        public bool IsRouteKnown(string stableId) => Contains(State.known_route_ids, stableId);
        public bool IsScanComplete(string stableId) => Contains(State.completed_scan_ids, stableId);
        public bool IsConstellationMemberRecorded(string stableId) => Contains(State.constellation_member_ids, stableId);
        public bool IsContractComplete(string stableId) => Contains(State.completed_contract_ids, stableId);
        public bool IsAchievementUnlocked(string stableId) => Contains(State.achievement_ids, stableId);

        public bool DiscoverEntity(string stableId) => Add(ref State.known_entity_ids, stableId);
        public bool DiscoverRoute(string stableId) => Add(ref State.known_route_ids, stableId);
        public bool CompleteScan(string stableId) => Add(ref State.completed_scan_ids, stableId);
        public bool RecordConstellationMember(string stableId) => Add(ref State.constellation_member_ids, stableId);
        public bool CompleteContract(string stableId) => Add(ref State.completed_contract_ids, stableId);
        public bool UnlockAchievement(string stableId) => Add(ref State.achievement_ids, stableId);

        public bool SetScannerLevel(int scannerLevel)
        {
            var normalized = Math.Max(1, scannerLevel);
            if (State.scanner_level == normalized)
            {
                return false;
            }

            State.scanner_level = normalized;
            return true;
        }

        public void Save()
        {
            State.NormalizeCollections();
            State.last_saved_utc = DateTime.UtcNow.ToString("O");
            var directory = Path.GetDirectoryName(storagePath);
            Directory.CreateDirectory(directory);

            var json = JsonUtility.ToJson(State, prettyPrint: false);
            var temporaryPath = storagePath + ".tmp";
            WriteAndFlush(temporaryPath, json);

            if (!TryLoad(temporaryPath, SnapshotKey, out _))
            {
                throw new InvalidDataException("Survey state failed post-write validation.");
            }

            if (File.Exists(storagePath))
            {
                File.Replace(temporaryPath, storagePath, null);
            }
            else
            {
                File.Move(temporaryPath, storagePath);
            }
        }

        private static SurveyDiscoveryState TryLoad(string path, SurveySnapshotKey key)
        {
            return TryLoad(path, key, out var state) ? state : null;
        }

        private static bool TryLoad(string path, SurveySnapshotKey key, out SurveyDiscoveryState state)
        {
            state = null;
            if (!File.Exists(path))
            {
                return false;
            }

            try
            {
                var json = File.ReadAllText(path, Encoding.UTF8);
                state = JsonUtility.FromJson<SurveyDiscoveryState>(json);
                if (state == null || !state.BelongsTo(key))
                {
                    state = null;
                    return false;
                }

                state.NormalizeCollections();
                return true;
            }
            catch (Exception)
            {
                // Leave bad evidence in place. A later support tool can inspect or
                // quarantine it; silently deleting it would make recovery harder.
                state = null;
                return false;
            }
        }

        private static void WriteAndFlush(string path, string content)
        {
            var bytes = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(content);
            using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
            stream.Write(bytes, 0, bytes.Length);
            stream.Flush(flushToDisk: true);
        }

        private static bool Add(ref string[] ids, string stableId)
        {
            if (string.IsNullOrWhiteSpace(stableId) || Contains(ids, stableId))
            {
                return false;
            }

            var expanded = new string[(ids?.Length ?? 0) + 1];
            if (ids != null)
            {
                Array.Copy(ids, expanded, ids.Length);
            }
            expanded[expanded.Length - 1] = stableId;
            ids = SurveyDiscoveryState.Normalize(expanded);
            return true;
        }

        private static bool Contains(string[] ids, string stableId)
        {
            if (string.IsNullOrWhiteSpace(stableId) || ids == null)
            {
                return false;
            }

            return Array.BinarySearch(ids, stableId, StringComparer.Ordinal) >= 0;
        }

        private static string RequirePathSegment(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("A storage path segment is required.", parameterName);
            }
            foreach (var character in value)
            {
                if (!(char.IsLetterOrDigit(character) || character == '-' || character == '_'))
                {
                    throw new ArgumentException("Storage path segments may only contain letters, digits, '-' or '_'.",
                        parameterName);
                }
            }

            return value;
        }
    }
}
