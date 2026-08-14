using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ClustrGalaxy.Core.Persistence
{
    public interface IDurableDocumentStore
    {
        bool TryRead(string relativePath, out string document);
        void Write(string relativePath, string document, Func<string, bool> validate);
        void Quarantine(string relativePath);
    }

    public sealed class FileDurableDocumentStore : IDurableDocumentStore
    {
        private readonly string root;

        public FileDurableDocumentStore(string rootPath)
        {
            if (string.IsNullOrWhiteSpace(rootPath))
                throw new ArgumentException("A durable-document root is required.", nameof(rootPath));
            root = Path.GetFullPath(rootPath);
        }

        public bool TryRead(string relativePath, out string document)
        {
            var path = Resolve(relativePath);
            if (!File.Exists(path))
            {
                document = string.Empty;
                return false;
            }
            document = File.ReadAllText(path, Encoding.UTF8);
            return true;
        }

        public void Write(string relativePath, string document, Func<string, bool> validate)
        {
            if (document == null) throw new ArgumentNullException(nameof(document));
            if (validate == null) throw new ArgumentNullException(nameof(validate));
            if (!validate(document)) throw new InvalidDataException("Durable document failed pre-write validation.");

            var destination = Resolve(relativePath);
            var directory = Path.GetDirectoryName(destination);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            var temporary = destination + ".tmp";
            var bytes = new UTF8Encoding(false).GetBytes(document);
            using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush(true);
            }
            var written = File.ReadAllText(temporary, Encoding.UTF8);
            if (!validate(written)) throw new InvalidDataException("Durable document failed post-write validation.");
            if (File.Exists(destination)) File.Replace(temporary, destination, null);
            else File.Move(temporary, destination);
        }

        public void Quarantine(string relativePath)
        {
            var path = Resolve(relativePath);
            if (!File.Exists(path)) return;
            var quarantine = path + ".corrupt-" + DateTime.UtcNow.ToString("yyyyMMddHHmmssfff");
            File.Move(path, quarantine);
        }

        private string Resolve(string relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath) || Path.IsPathRooted(relativePath))
                throw new ArgumentException("A relative durable-document path is required.", nameof(relativePath));
            var resolved = Path.GetFullPath(Path.Combine(root, relativePath));
            var prefix = root.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal)
                ? root : root + Path.DirectorySeparatorChar;
            if (!resolved.StartsWith(prefix, StringComparison.Ordinal) && !string.Equals(resolved, root, StringComparison.Ordinal))
                throw new ArgumentException("The durable-document path escapes its root.", nameof(relativePath));
            return resolved;
        }
    }

    public sealed class MemoryDurableDocumentStore : IDurableDocumentStore
    {
        private readonly Dictionary<string, string> documents = new(StringComparer.Ordinal);
        public int WriteCount { get; private set; }
        public IReadOnlyDictionary<string, string> Documents => documents;

        public bool TryRead(string relativePath, out string document) => documents.TryGetValue(relativePath, out document);
        public void Write(string relativePath, string document, Func<string, bool> validate)
        {
            if (validate == null) throw new ArgumentNullException(nameof(validate));
            if (!validate(document)) throw new InvalidDataException("Durable document failed validation.");
            documents[relativePath] = document;
            WriteCount++;
        }
        public void Quarantine(string relativePath)
        {
            if (!documents.TryGetValue(relativePath, out var value)) return;
            documents.Remove(relativePath);
            documents[relativePath + ".corrupt"] = value;
        }
    }

    public static class ProfilePaths
    {
        public const string Version = "v2";
        public static string Survey(string profileId, string snapshotNamespace, long revisionId, long catalogId)
        {
            var profile = Segment(profileId, nameof(profileId));
            var snapshot = Segment(snapshotNamespace, nameof(snapshotNamespace));
            return Path.Combine("ClustrGalaxy", "profiles", Version, profile, snapshot, $"{revisionId}-{catalogId}.json");
        }
        public static string Cache(string key) => Path.Combine("ClustrGalaxy", "cache", Version, Segment(key, nameof(key)) + ".json");
        private static string Segment(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("A storage path segment is required.", parameterName);
            foreach (var character in value)
                if (!(char.IsLetterOrDigit(character) || character == '-' || character == '_'))
                    throw new ArgumentException("Storage path segments may contain only letters, digits, '-' and '_'.", parameterName);
            return value;
        }
    }
}
