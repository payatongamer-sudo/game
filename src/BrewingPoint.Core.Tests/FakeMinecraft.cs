using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

namespace BrewingPoint.Core.Tests
{
    /// <summary>
    /// Builds a throwaway folder shaped like a Minecraft install, filled with made-up test data
    /// (own JSON, a 1x1 PNG, a generated sine tone). It only mimics the layout; it holds no Minecraft content.
    /// </summary>
    internal sealed class FakeMinecraft : IDisposable
    {
        public string Dir { get; } = Path.Combine(Path.GetTempPath(), "bp-test-" + Guid.NewGuid().ToString("N"));
        public static readonly byte[] TinyPng = Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");

        public FakeMinecraft() => Directory.CreateDirectory(Dir);

        public string WriteJar(string jarPath, IDictionary<string, string> textEntries, IEnumerable<string> pngEntries)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(jarPath)!);
            using var zip = ZipFile.Open(jarPath, ZipArchiveMode.Create);
            foreach (var kv in textEntries)
            {
                using var w = new StreamWriter(zip.CreateEntry(kv.Key).Open(), new UTF8Encoding(false));
                w.Write(kv.Value);
            }
            foreach (var p in pngEntries)
            {
                using var s = zip.CreateEntry(p).Open();
                s.Write(TinyPng, 0, TinyPng.Length);
            }
            return jarPath;
        }

        /// <summary>Writes assets/indexes/{id}.json and the hashed objects for the given assets.</summary>
        public void WriteAssets(string root, string indexId, IDictionary<string, byte[]> assets)
        {
            var objects = new StringBuilder();
            foreach (var kv in assets)
            {
                var hash = Convert.ToHexString(SHA1.HashData(kv.Value)).ToLowerInvariant();
                var dir = Path.Combine(root, "assets", "objects", hash.Substring(0, 2));
                Directory.CreateDirectory(dir);
                File.WriteAllBytes(Path.Combine(dir, hash), kv.Value);
                if (objects.Length > 0)
                    objects.Append(',');
                objects.Append($"\"{kv.Key}\":{{\"hash\":\"{hash}\",\"size\":{kv.Value.Length}}}");
            }
            var indexes = Path.Combine(root, "assets", "indexes");
            Directory.CreateDirectory(indexes);
            File.WriteAllText(Path.Combine(indexes, indexId + ".json"), "{\"objects\":{" + objects + "}}");
        }

        public void Dispose()
        {
            try { Directory.Delete(Dir, true); } catch (IOException) { }
        }
    }
}
