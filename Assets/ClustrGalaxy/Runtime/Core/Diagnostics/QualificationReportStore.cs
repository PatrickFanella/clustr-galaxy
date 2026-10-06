using System;
using System.IO;
using System.Text;

namespace ClustrGalaxy.Core.Diagnostics
{
    /// <summary>Immutable reports plus an atomically replaced latest-report pointer.</summary>
    public static class QualificationReportStore
    {
        public static string Write(string directory, string json)
        {
            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ") + "-" + Guid.NewGuid().ToString("N") + ".json");
            var bytes = new UTF8Encoding(false).GetBytes(json);
            using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read))
            {
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush(true);
            }
            var pointer = Path.Combine(directory, "latest-report-path.txt");
            var temporary = pointer + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllText(temporary, path);
                if (File.Exists(pointer)) File.Replace(temporary, pointer, null);
                else File.Move(temporary, pointer);
            }
            finally
            {
                if (File.Exists(temporary)) File.Delete(temporary);
            }
            return path;
        }
    }
}
