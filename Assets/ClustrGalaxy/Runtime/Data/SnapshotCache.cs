using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace ClustrGalaxy.Data
{
    public sealed class SnapshotCache
    {
        public const long DefaultMaximumBytes = 10L * 1024L * 1024L * 1024L;

        private readonly string root;
        private readonly string indexPath;
        private readonly long maximumBytes;
        private readonly Dictionary<string, CacheEntry> entries = new(StringComparer.Ordinal);

        public SnapshotCache(string cacheRoot, long maxBytes = DefaultMaximumBytes)
        {
            if (string.IsNullOrWhiteSpace(cacheRoot))
            {
                throw new ArgumentException("Cache root is required.", nameof(cacheRoot));
            }
            if (maxBytes < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(maxBytes));
            }

            root = cacheRoot;
            maximumBytes = maxBytes;
            indexPath = Path.Combine(root, "index.json");
            Directory.CreateDirectory(root);
            LoadIndex();
        }

        public static string DefaultRoot =>
            Path.Combine(Application.persistentDataPath, "ClustrGalaxy", "cache", "v1");

        public bool Contains(string key) => entries.ContainsKey(key);

        public string PathForKey(string key) => Path.Combine(root, FileNameForKey(key));

        public void Write(string key, string json, bool pinnedBase)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("Cache key is required.", nameof(key));
            }
            json ??= string.Empty;
            var bytes = Encoding.UTF8.GetBytes(json);
            var path = PathForKey(key);
            AtomicWrite(path, bytes);

            entries[key] = new CacheEntry
            {
                key = key,
                file_name = Path.GetFileName(path),
                checksum = Hash(bytes),
                size = bytes.LongLength,
                last_access_utc_ticks = DateTime.UtcNow.Ticks,
                pinned_base = pinnedBase
            };
            EvictToBudget();
            SaveIndex();
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
                RemoveEntry(key, path);
                return false;
            }

            entry.last_access_utc_ticks = DateTime.UtcNow.Ticks;
            entries[key] = entry;
            SaveIndex();
            json = Encoding.UTF8.GetString(bytes);
            return true;
        }

        private void EvictToBudget()
        {
            long total = 0;
            foreach (var entry in entries.Values)
            {
                total += entry.size;
            }

            while (total > maximumBytes)
            {
                CacheEntry oldest = null;
                foreach (var candidate in entries.Values)
                {
                    if (candidate.pinned_base)
                    {
                        continue;
                    }
                    if (oldest == null || candidate.last_access_utc_ticks < oldest.last_access_utc_ticks)
                    {
                        oldest = candidate;
                    }
                }
                if (oldest == null)
                {
                    break;
                }

                total -= oldest.size;
                entries.Remove(oldest.key);
                var path = Path.Combine(root, oldest.file_name);
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
        }

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
                    if (entry != null && !string.IsNullOrWhiteSpace(entry.key))
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

        private void SaveIndex()
        {
            var values = new CacheEntry[entries.Count];
            entries.Values.CopyTo(values, 0);
            Array.Sort(values, (left, right) => string.CompareOrdinal(left.key, right.key));
            AtomicWrite(indexPath, Encoding.UTF8.GetBytes(JsonUtility.ToJson(new CacheIndex { entries = values })));
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
            File.WriteAllBytes(temporary, bytes);
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
