using System;
using System.Collections.Generic;
using System.Linq;
using BrewingPoint.Brewing;
using BrewingPoint.Data;

namespace BrewingPoint.Minecraft
{
    /// <summary>
    /// Everything Brewing Point reads from the player's own Minecraft, gathered once at start (no Unity types):
    /// display names, icon PNG bytes, the Brewing Stand meshes and the brew sound's PCM. The mod turns these into
    /// Unity textures, sprites, meshes and audio clips in memory; nothing is written to disk or shipped.
    /// </summary>
    public sealed class MinecraftContent : IDisposable
    {
        public MinecraftInstall Install { get; }
        public Dictionary<string, string> Names { get; } = new Dictionary<string, string>(StringComparer.Ordinal);
        public Dictionary<string, byte[]> Textures { get; } = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        public Dictionary<string, MeshData> Models { get; } = new Dictionary<string, MeshData>(StringComparer.Ordinal);
        public Dictionary<string, List<OggPcm>> Sounds { get; } = new Dictionary<string, List<OggPcm>>(StringComparer.Ordinal);
        public List<string> Warnings { get; } = new List<string>();

        private MinecraftContent(MinecraftInstall install) => Install = install;

        public static MinecraftContent? Load(IEnumerable<string> candidates, out string report)
        {
            var install = MinecraftLocator.Open(candidates, Catalog.RequiredJarEntries(), out report);
            if (install == null)
                return null;
            var content = new MinecraftContent(install);
            try
            {
                content.Read();
            }
            catch
            {
                content.Dispose();
                throw;
            }
            return content;
        }

        private void Read()
        {
            foreach (var i in Sheets.Ingredients)
            {
                Names[i.Id] = Install.Translate(i.McLangKeys) ?? Pretty(i.McItem);
                AddTexture(i.McTexture);
            }
            var stand = Catalog.BrewingStand;
            Names[stand.Id] = Install.Translate(stand.McLangKeys) ?? "Brewing Stand";
            AddTexture(stand.McIcon);
            foreach (var e in Sheets.Effects)
            {
                if (e.Kind != "custom")
                    continue;
                Names[e.Id] = (e.McLangKey != "none" ? Install.Translate(new[] { e.McLangKey }) : null) ?? e.Name;
            }

            foreach (var m in Sheets.Models)
            {
                var model = BlockModelLoader.Load(Catalog.ModelReference(m), Install.ReadJarText);
                foreach (var tex in model.Elements.SelectMany(el => el.Faces).Select(f => model.TexturePath(f.Texture)).Where(t => t != null).Distinct())
                    AddTexture(tex!);
                var mesh = BlockMesher.Build(model, stand.BlockScale, FramesOf);
                Warnings.AddRange(mesh.Problems);
                Models[m.Id] = mesh;
            }

            foreach (var s in Sheets.Sounds)
            {
                var clips = new List<OggPcm>();
                foreach (var file in Install.SoundFiles(s.McEvent))
                {
                    var bytes = Install.ReadAsset(file);
                    if (bytes == null)
                    {
                        Warnings.Add($"sound {file} not in the asset store (launch that Minecraft version once to download it)");
                        continue;
                    }
                    try
                    {
                        clips.Add(OggPcm.Decode(bytes));
                    }
                    catch (Exception ex)
                    {
                        Warnings.Add($"sound {file} unreadable: {ex.Message}");
                    }
                }
                if (clips.Count == 0)
                    Warnings.Add($"no playable sound for {s.McEvent}");
                Sounds[s.Id] = clips;
            }
        }

        private void AddTexture(string jarPath)
        {
            if (Textures.ContainsKey(jarPath))
                return;
            var bytes = Install.ReadJarEntry(jarPath);
            if (bytes == null)
                Warnings.Add($"texture {jarPath} missing from the Minecraft jar");
            else
                Textures[jarPath] = bytes;
        }

        /// <summary>Animated Minecraft textures are vertical strips of square frames; the first frame is used.</summary>
        public int FramesOf(string jarPath)
        {
            if (!Textures.TryGetValue(jarPath, out var png) && (png = Install.ReadJarEntry(jarPath)!) == null)
                return 1;
            var (w, h) = PngSize(png);
            return w > 0 && h > w && h % w == 0 ? h / w : 1;
        }

        /// <summary>Width and height from a PNG's IHDR chunk (0,0 if not a PNG).</summary>
        public static (int width, int height) PngSize(byte[] png)
        {
            if (png.Length < 24 || png[0] != 0x89 || png[1] != (byte)'P' || png[12] != (byte)'I' || png[13] != (byte)'H')
                return (0, 0);
            int w = (png[16] << 24) | (png[17] << 16) | (png[18] << 8) | png[19];
            int h = (png[20] << 24) | (png[21] << 16) | (png[22] << 8) | png[23];
            return (w, h);
        }

        private static string Pretty(string mcItem) =>
            string.Join(" ", mcItem.Split('_').Select(w => w.Length == 0 ? w : char.ToUpperInvariant(w[0]) + w.Substring(1)));

        public void Dispose() => Install.Dispose();
    }
}
