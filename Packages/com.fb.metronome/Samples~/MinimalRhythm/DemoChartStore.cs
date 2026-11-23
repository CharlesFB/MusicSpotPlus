using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using FB.Metronome.Serialization;
using UnityEngine;
namespace FB.Metronome.Samples
{
    /// <summary>示例层的本地存档服务：同目录临时文件、校验、原子替换、上一份备份。</summary>
    public static class DemoChartStore
    {
        public static void Save(string path, string json)
        {
            ChartSession.ReadDocument(json);
            path=Path.GetFullPath(path);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";
            try
            {
                using(var stream=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None))
                {
                    var bytes=new UTF8Encoding(false).GetBytes(json);
                    stream.Write(bytes,0,bytes.Length);stream.Flush(true);
                }
                ChartSession.ReadDocument(File.ReadAllText(temp));
                if(File.Exists(path))
                {
                    // === 损坏主档不能覆盖有效备份；不支持 Replace 时明确失败，不退化为先删后写 ===
                    bool valid=true;
                    try {ChartSession.ReadDocument(File.ReadAllText(path));}
                    catch(ArgumentException){valid=false;}
                    File.Replace(temp,path,valid?path+".bak":null);
                }
                else File.Move(temp,path);
            }
            finally {if(File.Exists(temp))File.Delete(temp);}
        }
        public static ChartDocument Read(string path, out bool recovered)
        {
            recovered=false;
            try {return ChartSession.ReadDocument(File.ReadAllText(path));}
            catch(Exception ex) when(ex is IOException || ex is ArgumentException)
            {
                var backup=ChartSession.ReadDocument(File.ReadAllText(path+".bak"));
                recovered=true;return backup;
            }
        }
        public static string HashFile(string path)
        {
            using(var stream=File.OpenRead(path))using(var sha=SHA256.Create())
                return Hex(sha.ComputeHash(stream));
        }
        public static string HashClip(AudioClip clip)
        {
            var data=new float[clip.samples*clip.channels];
            if(!clip.GetData(data,0))throw new InvalidOperationException("Default music must expose PCM data to identify saved charts");
            using(var sha=SHA256.Create())
            {
                var bytes=new byte[data.Length*sizeof(float)];Buffer.BlockCopy(data,0,bytes,0,bytes.Length);
                byte[] header=Encoding.UTF8.GetBytes(clip.frequency+":"+clip.channels+":");
                sha.TransformBlock(header,0,header.Length,header,0);
                sha.TransformFinalBlock(bytes,0,bytes.Length);return Hex(sha.Hash);
            }
        }
        public static void VerifyIdentity(ChartDocument doc, string hash, float actualDuration)
        {
            if(string.IsNullOrEmpty(doc.audioHash) || string.IsNullOrEmpty(doc.audioId))
                throw new InvalidDataException("Legacy chart has no music identity. Explicitly confirm the selected music first.");
            if(!string.Equals(doc.audioHash,hash,StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Music content differs from the saved chart");
            if(Math.Abs(doc.duration-actualDuration)>.05f)
                throw new InvalidDataException("Music duration differs from the saved chart");
        }
        private static string Hex(byte[] bytes)=>BitConverter.ToString(bytes).Replace("-","").ToLowerInvariant();
    }
}
