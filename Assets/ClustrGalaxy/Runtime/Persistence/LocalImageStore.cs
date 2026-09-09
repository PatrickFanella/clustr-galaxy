using System;
using System.IO;
using System.Security.Cryptography;
using ClustrGalaxy.Core.Activities;
using Newtonsoft.Json.Linq;
namespace ClustrGalaxy.Persistence
{
    /// <summary>Explicit images commit as complete directories; no diary/progression writes.</summary>
    public sealed class LocalImageStore
    {
        public const int MaximumBytes=64*1024*1024;
        private readonly string root; private readonly Action<string,string> metadataWriter;
        public LocalImageStore(string storage,Action<string,string> writeMetadata=null)
        {root=Path.Combine(Path.GetFullPath(storage),"images");metadataWriter=writeMetadata??WriteText;}
        public static (int Width,int Height) Dimensions(byte[] bytes)
        {
            byte[] signature={137,80,78,71,13,10,26,10};
            if(bytes==null||bytes.Length<24||bytes.Length>MaximumBytes)throw new InvalidDataException("Image exceeds its bounds.");
            for(var i=0;i<8;i++)if(bytes[i]!=signature[i])throw new InvalidDataException("Choose a PNG image.");
            if(bytes[12]!=73||bytes[13]!=72||bytes[14]!=68||bytes[15]!=82)throw new InvalidDataException("Missing PNG image header.");
            var width=Big(bytes,16);var height=Big(bytes,20);
            if(width<1||height<1||width>8192||height>8192||(long)width*height>33554432)throw new InvalidDataException("Image dimensions exceed the supported limit.");
            return(width,height);
        }
        private static int Big(byte[] bytes,int i)=>unchecked((bytes[i]<<24)|(bytes[i+1]<<16)|(bytes[i+2]<<8)|bytes[i+3]);
        public static byte[] ReadPng(string path)
        {
            using var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read);
            if(stream.Length>MaximumBytes)throw new InvalidDataException("Image exceeds 64 MiB.");
            var bytes=new byte[checked((int)stream.Length)];var offset=0;
            while(offset<bytes.Length){var count=stream.Read(bytes,offset,bytes.Length-offset);if(count==0)throw new InvalidDataException("Image changed while reading.");offset+=count;}
            if(stream.ReadByte()!=-1)throw new InvalidDataException("Image changed while reading.");
            Dimensions(bytes);return bytes;
        }
        public string Save(byte[] png,ActivityIdentity identity,DateTime utc)
        {
            var size=Dimensions(png);if(utc.Kind!=DateTimeKind.Utc)throw new ArgumentException("UTC capture time required.");
            Directory.CreateDirectory(root);var id=Guid.NewGuid().ToString("N");var pending=Path.Combine(root,".pending-"+id);var final=Path.Combine(root,id);
            Directory.CreateDirectory(pending);
            try
            {
                using(var stream=new FileStream(Path.Combine(pending,"image.png"),FileMode.CreateNew,FileAccess.Write,FileShare.None)){stream.Write(png,0,png.Length);stream.Flush(true);}
                using var hash=SHA256.Create();
                var metadata=new JObject{["schema_version"]=1,["profile_id"]=identity.ProfileId,["snapshot_namespace"]=identity.Snapshot.StorageNamespace,
                    ["revision_id"]=identity.Snapshot.RevisionId,["catalog_id"]=identity.Snapshot.CatalogId,["world_mode"]=identity.Mode.ToString(),
                    ["captured_utc"]=utc.ToString("O"),["width"]=size.Width,["height"]=size.Height,["sha256"]=BitConverter.ToString(hash.ComputeHash(png)).Replace("-","").ToLowerInvariant()};
                metadataWriter(Path.Combine(pending,"metadata.json"),metadata.ToString(Newtonsoft.Json.Formatting.None));
                Directory.Move(pending,final);return Path.Combine(final,"image.png");
            }
            finally{if(Directory.Exists(pending))Directory.Delete(pending,true);}
        }
        private static void WriteText(string path,string text)
        {using var stream=new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.None);var bytes=System.Text.Encoding.UTF8.GetBytes(text);stream.Write(bytes,0,bytes.Length);stream.Flush(true);}
    }
}
