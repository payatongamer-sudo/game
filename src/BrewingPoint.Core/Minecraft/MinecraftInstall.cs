using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;

namespace BrewingPoint.Minecraft
{
    /// <summary>
    /// Read-only view of one Minecraft install on the player's PC: the client jar (textures, models,
    /// en_us lang) and the hashed asset store (sounds). Nothing is copied out; bytes are read on demand.
    /// </summary>
    public sealed class MinecraftInstall : IDisposable
    {
        private readonly ZipArchive _jar;
        private Dictionary<string, string>? _lang;
        private Dictionary<string, string>? _assetHashes;
        private JsonDocument? _sounds;

        public string Root { get; }
        public string JarPath { get; }
        public string VersionId { get; }

        public MinecraftInstall(string root, string jarPath)
        {
            Root = root;
            JarPath = jarPath;
            VersionId = VersionFromJar(jarPath);
            _jar = ZipFile.OpenRead(jarPath);
        }

        private static string VersionFromJar(string jarPath)
        {
            var name = Path.GetFileNameWithoutExtension(jarPath);
            if (name.StartsWith("minecraft-", StringComparison.Ordinal) && name.EndsWith("-client", StringComparison.Ordinal))
                return name.Substring("minecraft-".Length, name.Length - "minecraft-".Length - "-client".Length);
            return name;
        }

        public bool HasJarEntry(string path) => _jar.GetEntry(path) != null;

        public byte[]? ReadJarEntry(string path)
        {
            var entry = _jar.GetEntry(path);
            if (entry == null)
                return null;
            using var stream = entry.Open();
            using var memory = new MemoryStream();
            stream.CopyTo(memory);
            return memory.ToArray();
        }

        public string? ReadJarText(string path)
        {
            var bytes = ReadJarEntry(path);
            return bytes == null ? null : System.Text.Encoding.UTF8.GetString(bytes);
        }

        /// <summary>First key that exists in the player's en_us.json, or null.</summary>
        public string? Translate(IEnumerable<string> keys)
        {
            _lang ??= LoadLang();
            foreach (var key in keys)
                if (key != "none" && _lang.TryGetValue(key, out var text) && !string.IsNullOrWhiteSpace(text))
                    return text;
            return null;
        }

        private Dictionary<string, string> LoadLang()
        {
            var json = ReadJarText("assets/minecraft/lang/en_us.json");
            var map = new Dictionary<string, string>(StringComparer.Ordinal);
            if (json == null)
                return map;
            using var doc = JsonDocument.Parse(json);
            foreach (var p in doc.RootElement.EnumerateObject())
                if (p.Value.ValueKind == JsonValueKind.String)
                    map[p.Name] = p.Value.GetString()!;
            return map;
        }

        // ---- sounds: sounds.json event -> sound name -> asset index hash -> assets/objects/xx/hash ----

        /// <summary>Asset index files, preferring the one this version names, then newest first.</summary>
        private IEnumerable<string> AssetIndexFiles()
        {
            var indexes = Path.Combine(Root, "assets", "indexes");
            if (!Directory.Exists(indexes))
                yield break;
            var named = AssetIndexIdFromVersionJson();
            if (named != null)
            {
                var path = Path.Combine(indexes, named + ".json");
                if (File.Exists(path))
                    yield return path;
            }
            foreach (var f in new DirectoryInfo(indexes).GetFiles("*.json").OrderByDescending(f => f.LastWriteTimeUtc))
                yield return f.FullName;
        }

        private string? AssetIndexIdFromVersionJson()
        {
            var versionJson = Path.Combine(Path.GetDirectoryName(JarPath)!, VersionId + ".json");
            if (!File.Exists(versionJson))
                return null;
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(versionJson));
                if (doc.RootElement.TryGetProperty("assetIndex", out var ai) && ai.TryGetProperty("id", out var id))
                    return id.GetString();
            }
            catch (JsonException)
            {
            }
            return null;
        }

        private Dictionary<string, string> AssetHashes()
        {
            if (_assetHashes != null)
                return _assetHashes;
            foreach (var file in AssetIndexFiles())
            {
                try
                {
                    using var doc = JsonDocument.Parse(File.ReadAllText(file));
                    if (!doc.RootElement.TryGetProperty("objects", out var objects))
                        continue;
                    var map = new Dictionary<string, string>(StringComparer.Ordinal);
                    foreach (var p in objects.EnumerateObject())
                        if (p.Value.TryGetProperty("hash", out var h))
                            map[p.Name] = h.GetString()!;
                    if (map.ContainsKey("minecraft/sounds.json"))
                        return _assetHashes = map;
                }
                catch (JsonException)
                {
                }
            }
            return _assetHashes = new Dictionary<string, string>();
        }

        /// <summary>Bytes of an asset from the hashed store (e.g. "minecraft/sounds.json"), or null.</summary>
        public byte[]? ReadAsset(string name)
        {
            if (!AssetHashes().TryGetValue(name, out var hash) || hash.Length < 2)
                return null;
            var path = Path.Combine(Root, "assets", "objects", hash.Substring(0, 2), hash);
            return File.Exists(path) ? File.ReadAllBytes(path) : null;
        }

        /// <summary>The .ogg file names a sound event plays (e.g. block.brewing_stand.brew -> minecraft/sounds/block/brewing_stand/brew1.ogg).</summary>
        public List<string> SoundFiles(string soundEvent)
        {
            var result = new List<string>();
            if (_sounds == null)
            {
                var bytes = ReadAsset("minecraft/sounds.json");
                if (bytes == null)
                    return result;
                _sounds = JsonDocument.Parse(bytes);
            }
            if (!_sounds.RootElement.TryGetProperty(soundEvent, out var ev) || !ev.TryGetProperty("sounds", out var sounds))
                return result;
            foreach (var s in sounds.EnumerateArray())
            {
                string? name = null;
                bool isEvent = false;
                if (s.ValueKind == JsonValueKind.String)
                    name = s.GetString();
                else if (s.ValueKind == JsonValueKind.Object && s.TryGetProperty("name", out var n))
                {
                    name = n.GetString();
                    isEvent = s.TryGetProperty("type", out var t) && t.GetString() == "event";
                }
                if (string.IsNullOrEmpty(name))
                    continue;
                if (isEvent)
                {
                    result.AddRange(SoundFiles(name!));
                    continue;
                }
                var ns = "minecraft";
                var colon = name!.IndexOf(':');
                if (colon >= 0)
                {
                    ns = name.Substring(0, colon);
                    name = name.Substring(colon + 1);
                }
                result.Add($"{ns}/sounds/{name}.ogg");
            }
            return result;
        }

        public void Dispose()
        {
            _sounds?.Dispose();
            _jar.Dispose();
        }
    }
}
