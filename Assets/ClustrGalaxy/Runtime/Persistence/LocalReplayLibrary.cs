using System;
using System.IO;
using System.Linq;
using ClustrGalaxy.Core.Activities;
namespace ClustrGalaxy.Persistence
{
    /// <summary>Local-only file operations. Import validates the full declarative package before writing.</summary>
    public sealed class LocalReplayLibrary
    {
        private readonly string root;
        public LocalReplayLibrary(string storage){root=Path.GetFullPath(storage??throw new ArgumentNullException(nameof(storage)));}
        public string[] Files()
        {
            var files=new System.Collections.Generic.List<string>();
            foreach(var folder in new[]{"replays","ghosts","imported","exports"})
            {
                var path=Path.Combine(root,folder);if(!Directory.Exists(path)||(File.GetAttributes(path)&FileAttributes.ReparsePoint)!=0)continue;
                foreach(var file in Directory.EnumerateFiles(path))if(file.EndsWith(".clustr-replay",StringComparison.Ordinal)||file.EndsWith(".png",StringComparison.Ordinal))files.Add(file);
            }
            var images=Path.Combine(root,"images");
            if(Directory.Exists(images)&&(File.GetAttributes(images)&FileAttributes.ReparsePoint)==0)
                foreach(var bundle in Directory.EnumerateDirectories(images))
                    if(Guid.TryParseExact(Path.GetFileName(bundle),"N",out _)&&(File.GetAttributes(bundle)&FileAttributes.ReparsePoint)==0&&File.Exists(Path.Combine(bundle,"metadata.json"))&&File.Exists(Path.Combine(bundle,"image.png")))
                        files.Add(Path.Combine(bundle,"image.png"));
            return files.OrderBy(x=>x,StringComparer.Ordinal).ToArray();
        }
        public long BytesFor(string path)
        {
            var bytes=new FileInfo(path).Length;var folder=Path.GetDirectoryName(path);
            if(Path.GetDirectoryName(folder)==Path.Combine(root,"images")&&File.Exists(Path.Combine(folder,"metadata.json")))bytes+=new FileInfo(Path.Combine(folder,"metadata.json")).Length;
            return bytes;
        }
        public JourneyReplay Read(string path)
        {using var stream=File.OpenRead(path);return ReplayPackage.Read(stream);}
        public string Import(string source)
        {
            if(!Path.IsPathRooted(source)||source.IndexOf("://",StringComparison.Ordinal)>=0)throw new InvalidDataException("Choose an absolute local file path.");
            var replay=Read(source);var directory=Path.Combine(root,"imported");Directory.CreateDirectory(directory);
            var destination=Path.Combine(directory,Guid.NewGuid().ToString("N")+".clustr-replay");ReplayPackage.Save(destination,replay);return destination;
        }
        public void Delete(string path)
        {
            var full=Path.GetFullPath(path);
            if(!Files().Contains(full,StringComparer.Ordinal))throw new InvalidOperationException("Only listed local creations can be deleted.");
            File.Delete(full);
            var directory=Path.GetDirectoryName(full);
            if(Path.GetDirectoryName(directory)==Path.Combine(root,"images")&&Guid.TryParseExact(Path.GetFileName(directory),"N",out _))
            {
                File.Delete(Path.Combine(directory,"metadata.json"));
                if(!Directory.EnumerateFileSystemEntries(directory).Any())Directory.Delete(directory);
            }
        }
    }
}
