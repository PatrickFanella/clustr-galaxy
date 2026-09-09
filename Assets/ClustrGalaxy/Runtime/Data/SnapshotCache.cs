using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

namespace ClustrGalaxy.Data
{
    public sealed class SnapshotCache
    {
        public const long DefaultMaximumBytes = 10L * 1024L * 1024L * 1024L;

        private readonly string root;
        private readonly string indexPath;
        private readonly long maximumBytes;
        private readonly Action beforeIndexCommit;
        private readonly Dictionary<string, CacheEntry> entries = new(StringComparer.Ordinal);

        public SnapshotCache(string cacheRoot, long maxBytes = DefaultMaximumBytes, Action beforeIndexCommit = null)
        {
            if (string.IsNullOrWhiteSpace(cacheRoot))
            {
                throw new ArgumentException("Cache root is required.", nameof(cacheRoot));
            }
            if (maxBytes < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(maxBytes));
            }

            this.beforeIndexCommit = beforeIndexCommit;
            root = cacheRoot;
            maximumBytes = maxBytes;
            indexPath = Path.Combine(root, "index.json");
            Directory.CreateDirectory(root);
            RejectLinks();
            LoadIndex();
            RecoverUnreferencedFiles();
            EnsureRoom(0);
        }

        public static string DefaultRoot =>
            Path.Combine(Application.persistentDataPath, "ClustrGalaxy", "cache", "v2");

        public long UsedBytes => Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories).Sum(path => new FileInfo(path).Length);
        private static bool IsDocumentName(string name) => Regex.IsMatch(name,
            @"\A[0-9a-f]{64}(?:-[0-9a-f]{64})?\.json\z");
        private void RejectLinks()
        {
            if ((File.GetAttributes(root) & FileAttributes.ReparsePoint) != 0 ||
                Directory.EnumerateFileSystemEntries(root, "*", SearchOption.AllDirectories).Any(path => (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0))
                throw new IOException("Cache links are not accepted.");
        }
        private void RecoverUnreferencedFiles()
        {
            var referenced = new HashSet<string>(entries.Values.Select(entry => entry.file_name), StringComparer.Ordinal);
            foreach (var path in Directory.EnumerateFiles(root))
            {
                var name = Path.GetFileName(path);
                var temporary = name.IndexOf(".tmp-", StringComparison.Ordinal);
                if (IsDocumentName(name) && !referenced.Contains(name) ||
                    temporary > 0 && (name.Substring(0,temporary) == "index.json" || IsDocumentName(name.Substring(0,temporary))))
                    File.Delete(path);
            }
        }
        private bool EvictOne(string preserveKey)
        {
            var corrupt = Directory.EnumerateFiles(root)
                .Where(path => Path.GetFileName(path).Contains(".corrupt-", StringComparison.Ordinal))
                .OrderBy(File.GetLastWriteTimeUtc).FirstOrDefault();
            if (corrupt != null) { File.Delete(corrupt); return true; }
            var oldest = entries.Values.Where(entry => !entry.pinned_base && entry.key != preserveKey)
                .OrderBy(entry => entry.last_access_utc_ticks).FirstOrDefault();
            if (oldest == null) return false;
            var path = Path.Combine(root,oldest.file_name);
            if (File.Exists(path)) File.Delete(path);
            entries.Remove(oldest.key);
            return true;
        }
        private void EnsureRoom(long additionalBytes, string preserveKey = null)
        {
            RejectLinks();
            while (UsedBytes + additionalBytes > maximumBytes)
                if (!EvictOne(preserveKey)) throw new IOException("Cache cannot reserve replacement bytes without evicting the active base.");
        }
        private byte[] IndexBytes() => Encoding.UTF8.GetBytes(JsonUtility.ToJson(new CacheIndex {
            entries = entries.Values.OrderBy(entry => entry.key, StringComparer.Ordinal).ToArray() }));

        public bool Contains(string key) => entries.ContainsKey(key);

        public string PathForKey(string key) => Path.Combine(root, entries.TryGetValue(key, out var entry) ? entry.file_name : FileNameForKey(key));

        public void Write(string key, string json, bool pinnedBase)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("Cache key is required.", nameof(key));
            }
            json ??= string.Empty;
            var bytes = Encoding.UTF8.GetBytes(json);
            var checksum = Hash(bytes);
            var path = Path.Combine(root, FileNameForKey(key).Replace(".json", "-" + checksum + ".json"));
            if (File.Exists(path) && Hash(File.ReadAllBytes(path)) != checksum)
                File.Move(path,path + ".corrupt-" + Guid.NewGuid().ToString("N"));
            var candidate = new CacheEntry
            {
                key = key,
                file_name = Path.GetFileName(path),
                checksum = checksum,
                size = bytes.LongLength,
                last_access_utc_ticks = DateTime.UtcNow.Ticks,
                pinned_base = pinnedBase
            };
            // Reserve the exact candidate index as eviction changes its size.
            RejectLinks();
            while (true)
            {
                entries.TryGetValue(key,out var old);
                entries[key] = candidate;
                var indexSize = IndexBytes().LongLength;
                if (old == null) entries.Remove(key); else entries[key] = old;
                if (UsedBytes + (File.Exists(path) ? 0 : bytes.LongLength) + indexSize <= maximumBytes) break;
                if (!EvictOne(key)) throw new IOException("Cache cannot reserve replacement bytes without evicting the active base.");
            }
            SaveIndex();
            var previous = new Dictionary<string, CacheEntry>(entries, StringComparer.Ordinal);
            var created = !File.Exists(path);
            if (created) AtomicWrite(path, bytes);
            entries[key] = candidate;
            try
            {
                EvictToBudget();
                beforeIndexCommit?.Invoke();
                SaveIndex(key);
            }
            catch
            {
                entries.Clear();
                foreach (var pair in previous) entries.Add(pair.Key, pair.Value);
                if (created && File.Exists(path)) File.Delete(path);
                throw;
            }
            DeleteUnreferenced(previous);
        }

        private void DeleteUnreferenced(Dictionary<string, CacheEntry> previous)
        {
            var referenced = new HashSet<string>(StringComparer.Ordinal);
            foreach (var value in entries.Values) referenced.Add(value.file_name);
            foreach (var old in previous.Values)
            {
                if (referenced.Contains(old.file_name)) continue;
                var oldPath = Path.Combine(root, old.file_name);
                if (File.Exists(oldPath)) File.Delete(oldPath);
            }
        }

        public bool TryRead(string key, out string json)
        {
            json = string.Empty;
            if (!entries.TryGetValue(key, out var entry))
            {
                return false;
            }

            var path = Path.Combine(root, entry.file_name);
            if (!File.Exists(path))
            {
                RemoveEntry(key, path);
                return false;
            }

            var bytes = File.ReadAllBytes(path);
            if (!string.Equals(Hash(bytes), entry.checksum, StringComparison.Ordinal))
            {
                Quarantine(key);
                return false;
            }

            entry.last_access_utc_ticks = DateTime.UtcNow.Ticks;
            entries[key] = entry;
            SaveIndex();
            json = Encoding.UTF8.GetString(bytes);
            return true;
        }

        public void ProtectOnly(params string[] keys)
        {
            var protectedKeys = new HashSet<string>(keys, StringComparer.Ordinal);
            foreach (var value in entries.Values) value.pinned_base = protectedKeys.Contains(value.key);
            EvictToBudget(); SaveIndex();
        }

        public void Quarantine(string key)
        {
            if (!entries.TryGetValue(key, out var entry)) return;
            var path = Path.Combine(root, entry.file_name);
            if (File.Exists(path)) File.Move(path, path + ".corrupt-" + Guid.NewGuid().ToString("N"));
            entries.Remove(key);
            var quarantine = Directory.EnumerateFiles(root).Where(file => Path.GetFileName(file).Contains(".corrupt-", StringComparison.Ordinal))
                .OrderBy(File.GetLastWriteTimeUtc).ToArray();
            long retained = quarantine.Sum(file => new FileInfo(file).Length);
            foreach (var file in quarantine)
            {
                if (retained <= Math.Min(64L * 1024 * 1024, maximumBytes)) break;
                retained -= new FileInfo(file).Length; File.Delete(file);
            }
            SaveIndex();
        }

        private void EvictToBudget() => EnsureRoom(0);

        private void LoadIndex()
        {
            if (!File.Exists(indexPath))
            {
                return;
            }
            try
            {
                var index = JsonUtility.FromJson<CacheIndex>(File.ReadAllText(indexPath));
                foreach (var entry in index?.entries ?? Array.Empty<CacheEntry>())
                {
                    if (entry != null && !string.IsNullOrWhiteSpace(entry.key) && (entry.file_name == FileNameForKey(entry.key) || entry.file_name == FileNameForKey(entry.key).Replace(".json", "-" + entry.checksum + ".json")) &&
                        entry.checksum != null && Regex.IsMatch(entry.checksum, @"\A[0-9a-f]{64}\z") && entry.size >= 0)
                    {
                        entries[entry.key] = entry;
                    }
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[ClustrGalaxy.Data] Ignoring corrupt snapshot cache index: {exception.Message}");
                entries.Clear();
            }
        }

        private void SaveIndex(string preserveKey = null)
        {
            var bytes = IndexBytes();
            EnsureRoom(bytes.LongLength, preserveKey);
            bytes = IndexBytes();
            AtomicWrite(indexPath, bytes);
        }

        private void RemoveEntry(string key, string path)
        {
            entries.Remove(key);
            if (File.Exists(path))
            {
                File.Delete(path);
            }
            SaveIndex();
        }

        private static string FileNameForKey(string key) => Hash(Encoding.UTF8.GetBytes(key)) + ".json";

        private static string Hash(byte[] bytes)
        {
            using var sha = SHA256.Create();
            var digest = sha.ComputeHash(bytes);
            var builder = new StringBuilder(digest.Length * 2);
            foreach (var value in digest)
            {
                builder.Append(value.ToString("x2"));
            }
            return builder.ToString();
        }

        private static void AtomicWrite(string destination, byte[] bytes)
        {
            var temporary = destination + ".tmp-" + Guid.NewGuid().ToString("N");
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            { stream.Write(bytes, 0, bytes.Length); stream.Flush(true); }
            if (File.Exists(destination))
            {
                File.Replace(temporary, destination, null);
            }
            else
            {
                File.Move(temporary, destination);
            }
        }

        [Serializable]
        private sealed class CacheIndex
        {
            public CacheEntry[] entries = Array.Empty<CacheEntry>();
        }

        [Serializable]
        private sealed class CacheEntry
        {
            public string key = string.Empty;
            public string file_name = string.Empty;
            public string checksum = string.Empty;
            public long size;
            public long last_access_utc_ticks;
            public bool pinned_base;
        }
    }
}
