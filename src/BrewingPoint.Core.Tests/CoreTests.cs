using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using BrewingPoint.Brewing;
using BrewingPoint.Data;
using BrewingPoint.Minecraft;
using Xunit;

namespace BrewingPoint.Core.Tests
{
    public class SheetTests
    {
        [Fact]
        public void EveryIngredientEffectAndShopResolves()
        {
            foreach (var i in Sheets.Ingredients)
            {
                Assert.True(Catalog.EffectById.ContainsKey(i.Effect), i.Id);
                Assert.True(Catalog.ShopById.ContainsKey(i.Shop), i.Id);
            }
            Assert.Equal(10, Sheets.Ingredients.Length);
        }

        [Fact]
        public void RequiredJarEntriesCoverIconsModelsAndLang()
        {
            var req = Catalog.RequiredJarEntries();
            Assert.Contains("assets/minecraft/lang/en_us.json", req);
            Assert.Contains("assets/minecraft/textures/item/nether_wart.png", req);
            Assert.Contains("assets/minecraft/models/block/brewing_stand.json", req);
            Assert.Contains("assets/minecraft/textures/item/brewing_stand.png", req);
            Assert.Equal(1 + 10 + 1 + 7, req.Count);
        }

        [Fact]
        public void ModelReferenceStripsJarPath()
        {
            Assert.Equal("block/brewing_stand_bottle1", Catalog.ModelReference(Catalog.ModelById["brewing_stand_bottle1"]));
        }
    }

    public class BrewRuleTests
    {
        private static readonly string Awkward = Catalog.CustomEffectId("awkward")!;

        [Fact]
        public void EffectIngredientNeedsAwkwardBatchAtTheStand()
        {
            var msg = BrewRules.CannotStart(true, "mc_sugar", new[] { "calming" }, "Sugar");
            Assert.Equal(string.Format(BrewRules.NeedsAwkward, "Sugar"), msg);
            Assert.Null(BrewRules.CannotStart(true, "mc_sugar", new[] { "calming", Awkward }, "Sugar"));
        }

        [Fact]
        public void NetherWartAndFermentedEyeNeedNoBase()
        {
            Assert.Null(BrewRules.CannotStart(true, "mc_nether_wart", new[] { "calming" }, "Nether Wart"));
            Assert.Null(BrewRules.CannotStart(true, "mc_fermented_spider_eye", new[] { "energizing" }, "Fermented Spider Eye"));
        }

        [Fact]
        public void StandRefusesVanillaMixersAndVanillaStationRefusesMinecraftOnes()
        {
            Assert.Equal(BrewRules.StandOnlyMinecraft, BrewRules.CannotStart(true, "cuke", Array.Empty<string>(), "Cuke"));
            Assert.Equal(string.Format(BrewRules.UseTheStand, "Sugar"), BrewRules.CannotStart(false, "mc_sugar", Array.Empty<string>(), "Sugar"));
            Assert.Null(BrewRules.CannotStart(false, "cuke", Array.Empty<string>(), "Cuke"));
            Assert.Null(BrewRules.CannotStart(true, null, Array.Empty<string>(), ""));
        }

        [Fact]
        public void SugarUsesUpTheAwkwardBase()
        {
            // game maths: OG Kush (Calming) + Nether Wart -> [calming, awkward]; + Sugar adds Energizing
            var result = BrewRules.ApplyReactions(new List<string> { "calming", "awkward", "energizing" }, "energizing");
            Assert.Equal(new[] { "calming", "energizing" }, result);
        }

        [Fact]
        public void FermentedSpiderEyeCorruptsLikeMinecraft()
        {
            var result = BrewRules.ApplyReactions(new List<string> { "energizing", "brighteyed", "refreshing", "weakness" }, "weakness");
            Assert.Equal(new[] { "sedating", "sneaky", "toxic", "weakness" }, result);
        }

        [Fact]
        public void VanillaMixerLeavesNonAwkwardBatchAlone()
        {
            var result = BrewRules.ApplyReactions(new List<string> { "calming", "energizing" }, "energizing");
            Assert.Equal(new[] { "calming", "energizing" }, result);
        }
    }

    public class LocatorAndInstallTests
    {
        private static Dictionary<string, string> JarText(string model) => new Dictionary<string, string>
        {
            ["assets/minecraft/lang/en_us.json"] = "{\"item.minecraft.sugar\":\"Sugar\",\"block.minecraft.nether_wart\":\"Nether Wart\"}",
            ["assets/minecraft/models/block/test_block.json"] = model,
        };

        [Fact]
        public void FindsOfficialLayoutAndReadsLangWithFallbackKeys()
        {
            using var fake = new FakeMinecraft();
            var root = Path.Combine(fake.Dir, ".minecraft");
            fake.WriteJar(Path.Combine(root, "versions", "1.21.4", "1.21.4.jar"), JarText("{}"), new[] { "assets/minecraft/textures/item/sugar.png" });

            using var mc = MinecraftLocator.Open(new[] { fake.Dir }, new[] { "assets/minecraft/textures/item/sugar.png" }, out var report);
            Assert.NotNull(mc);
            Assert.Equal("1.21.4", mc!.VersionId);
            Assert.Equal("Sugar", mc.Translate(new[] { "item.minecraft.sugar" }));
            Assert.Equal("Nether Wart", mc.Translate(new[] { "item.minecraft.nether_wart", "block.minecraft.nether_wart" }));
            Assert.Null(mc.Translate(new[] { "none" }));
            Assert.NotNull(mc.ReadJarEntry("assets/minecraft/textures/item/sugar.png"));
        }

        [Fact]
        public void FindsPrismLayoutFromAnInstanceFolder()
        {
            using var fake = new FakeMinecraft();
            var prism = Path.Combine(fake.Dir, "PrismLauncher");
            fake.WriteJar(Path.Combine(prism, "libraries", "com", "mojang", "minecraft", "1.20.1", "minecraft-1.20.1-client.jar"), JarText("{}"), Array.Empty<string>());
            var instance = Path.Combine(prism, "instances", "Brewing", "minecraft");
            Directory.CreateDirectory(instance);

            var roots = MinecraftLocator.Roots(Path.Combine(prism, "instances", "Brewing")).ToList();
            Assert.Contains(prism, roots);
            using var mc = MinecraftLocator.Open(new[] { instance }, Array.Empty<string>(), out _);
            Assert.Equal("1.20.1", mc!.VersionId);
        }

        [Fact]
        public void SkipsJarsMissingRequiredFilesAndReports()
        {
            using var fake = new FakeMinecraft();
            fake.WriteJar(Path.Combine(fake.Dir, "versions", "old", "old.jar"), JarText("{}"), Array.Empty<string>());
            var mc = MinecraftLocator.Open(new[] { fake.Dir }, new[] { "assets/minecraft/textures/item/sugar.png" }, out var report);
            Assert.Null(mc);
            Assert.Contains("missing 1 file", report);
        }

        [Fact]
        public void CandidatesPreferMeltyArgThenEnvThenConfigThenDefaults()
        {
            using var fake = new FakeMinecraft();
            var cfg = Path.Combine(fake.Dir, "BrewingPoint.cfg");
            File.WriteAllText(cfg, "minecraft_dir=C:/from-config\n");
            var env = new Dictionary<string, string> { [MinecraftLocator.EnvVar] = "C:/from-env", ["APPDATA"] = "C:/AppData" };
            var list = MinecraftLocator.Candidates(new[] { "Schedule I.exe", "--brewingpoint-minecraft=\"C:/from-melty\"" }, k => env.TryGetValue(k, out var v) ? v : null, cfg);
            Assert.Equal("C:/from-melty", list[0]);
            Assert.Equal("C:/from-env", list[1]);
            Assert.Equal("C:/from-config", list[2]);
            Assert.Contains(Path.Combine("C:/AppData", ".minecraft"), list);
        }

        [Fact]
        public void ResolvesSoundEventThroughAssetIndexAndDecodesOgg()
        {
            using var fake = new FakeMinecraft();
            var root = Path.Combine(fake.Dir, ".minecraft");
            fake.WriteJar(Path.Combine(root, "versions", "1.21.4", "1.21.4.jar"), JarText("{}"), Array.Empty<string>());
            File.WriteAllText(Path.Combine(root, "versions", "1.21.4", "1.21.4.json"), "{\"assetIndex\":{\"id\":\"19\"}}");
            var tone = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", "tone.ogg"));
            var soundsJson = "{\"test.brew\":{\"sounds\":[\"test/brew1\",{\"name\":\"test/brew2\",\"volume\":0.5},{\"name\":\"test.other\",\"type\":\"event\"}]}," +
                             "\"test.other\":{\"sounds\":[\"test/brew3\"]}}";
            fake.WriteAssets(root, "19", new Dictionary<string, byte[]>
            {
                ["minecraft/sounds.json"] = Encoding.UTF8.GetBytes(soundsJson),
                ["minecraft/sounds/test/brew1.ogg"] = tone,
            });

            using var mc = MinecraftLocator.Open(new[] { root }, Array.Empty<string>(), out _);
            var files = mc!.SoundFiles("test.brew");
            Assert.Equal(new[] { "minecraft/sounds/test/brew1.ogg", "minecraft/sounds/test/brew2.ogg", "minecraft/sounds/test/brew3.ogg" }, files);
            var bytes = mc.ReadAsset(files[0]);
            Assert.NotNull(bytes);
            Assert.Null(mc.ReadAsset(files[1]));

            var pcm = OggPcm.Decode(bytes!);
            Assert.Equal(2, pcm.Channels);
            Assert.Equal(22050, pcm.SampleRate);
            Assert.InRange(pcm.FramesPerChannel, 5000, 6000); // 0.25 s
            Assert.InRange(pcm.Samples.Max(), 0.08f, 0.2f); // ffmpeg sine amplitude is 1/8
        }
    }

    public class MesherTests
    {
        private const string Cube = "{\"textures\":{\"all\":\"block/test\",\"side\":\"#all\"},\"elements\":[{\"from\":[0,0,0],\"to\":[16,16,16]," +
            "\"faces\":{\"north\":{\"texture\":\"#side\"},\"south\":{\"texture\":\"#side\"},\"east\":{\"texture\":\"#side\"},\"west\":{\"texture\":\"#side\"}," +
            "\"up\":{\"texture\":\"#all\",\"rotation\":90},\"down\":{\"uv\":[0,0,8,8],\"texture\":\"#all\"}}}]}";

        private static Func<string, string?> Jar(Dictionary<string, string> files) => p => files.TryGetValue(p, out var t) ? t : null;

        [Fact]
        public void CubeBecomesTwelveOutwardTrianglesOnTheBlockFootprint()
        {
            var model = BlockModelLoader.Load("block/cube", Jar(new Dictionary<string, string> { ["assets/minecraft/models/block/cube.json"] = Cube }));
            var mesh = BlockMesher.Build(model, 1f);
            Assert.Empty(mesh.Problems);
            Assert.Equal(24, mesh.Vertices.Count);
            var tris = mesh.Submeshes["assets/minecraft/textures/block/test.png"];
            Assert.Equal(36, tris.Count);
            Assert.Equal(-0.5f, mesh.Vertices.Min(v => v.X), 3);
            Assert.Equal(0.5f, mesh.Vertices.Max(v => v.Z), 3);
            Assert.Equal(0f, mesh.Vertices.Min(v => v.Y), 3);
            Assert.Equal(1f, mesh.Vertices.Max(v => v.Y), 3);
            var centre = new Vec3(0, 0.5f, 0);
            for (int i = 0; i < tris.Count; i += 3)
            {
                var a = mesh.Vertices[tris[i]];
                var b = mesh.Vertices[tris[i + 1]];
                var c = mesh.Vertices[tris[i + 2]];
                var n = Vec3.Cross(b - a, c - a);
                var outward = new Vec3((a.X + b.X + c.X) / 3 - centre.X, (a.Y + b.Y + c.Y) / 3 - centre.Y, (a.Z + b.Z + c.Z) / 3 - centre.Z);
                Assert.True(Vec3.Dot(n, outward) > 0, $"triangle {i / 3} faces inward");
            }
        }

        [Fact]
        public void NorthFaceLandsOnUnityPositiveZAndKeepsTextureUpright()
        {
            var model = BlockModelLoader.Load("block/cube", Jar(new Dictionary<string, string> { ["assets/minecraft/models/block/cube.json"] = Cube }));
            var mesh = BlockMesher.Build(model, 1f);
            // first face written is north: Minecraft z=0 (north) -> Unity z=+0.5
            for (int i = 0; i < 4; i++)
                Assert.Equal(0.5f, mesh.Vertices[i].Z, 3);
            Assert.Equal(0f, mesh.Normals[0].X, 3);
            Assert.Equal(1f, mesh.Normals[0].Z, 3);
            // top-left corner: highest vertex has v=1 (texture top)
            Assert.Equal(1f, mesh.Vertices[0].Y, 3);
            Assert.Equal(1f, mesh.Uvs[0].V, 3);
        }

        [Fact]
        public void ParentTexturesMergeAndChildOverrides()
        {
            var files = new Dictionary<string, string>
            {
                ["assets/minecraft/models/block/parent.json"] = "{\"textures\":{\"a\":\"block/p\",\"b\":\"block/q\"},\"elements\":[{\"from\":[0,0,8],\"to\":[16,16,8],\"faces\":{\"north\":{\"texture\":\"#a\"},\"south\":{\"texture\":\"#b\"}}}]}",
                ["assets/minecraft/models/block/child.json"] = "{\"parent\":\"minecraft:block/parent\",\"textures\":{\"a\":\"minecraft:block/c\"}}",
            };
            var model = BlockModelLoader.Load("block/child", Jar(files));
            var mesh = BlockMesher.Build(model, 2f);
            Assert.True(mesh.Submeshes.ContainsKey("assets/minecraft/textures/block/c.png"));
            Assert.True(mesh.Submeshes.ContainsKey("assets/minecraft/textures/block/q.png"));
            Assert.Equal(2f, mesh.Vertices.Max(v => v.Y), 3);
        }

        [Fact]
        public void ElementRotationTurnsAPlaneAboutItsOrigin()
        {
            var json = "{\"textures\":{\"t\":\"block/t\"},\"elements\":[{\"from\":[0,0,8],\"to\":[16,16,8],\"rotation\":{\"origin\":[8,8,8],\"axis\":\"y\",\"angle\":45}," +
                       "\"faces\":{\"north\":{\"texture\":\"#t\"}}}]}";
            var model = BlockModelLoader.Load("block/r", Jar(new Dictionary<string, string> { ["assets/minecraft/models/block/r.json"] = json }));
            var mesh = BlockMesher.Build(model, 1f);
            // a 16-wide plane through the centre rotated 45 degrees spans +-0.3536 on both x and z
            Assert.Equal(0.3536f, mesh.Vertices.Max(v => v.X), 3);
            Assert.Equal(0.3536f, mesh.Vertices.Max(v => v.Z), 3);
            Assert.Empty(mesh.Problems);
        }

        [Fact]
        public void MissingModelThrowsAndUnresolvedTextureIsReported()
        {
            Assert.Throws<InvalidOperationException>(() => BlockModelLoader.Load("block/nope", _ => null));
            var json = "{\"elements\":[{\"from\":[0,0,0],\"to\":[1,1,1],\"faces\":{\"up\":{\"texture\":\"#missing\"}}}]}";
            var model = BlockModelLoader.Load("block/x", Jar(new Dictionary<string, string> { ["assets/minecraft/models/block/x.json"] = json }));
            var mesh = BlockMesher.Build(model, 1f);
            Assert.Single(mesh.Problems);
        }
    }
}

namespace BrewingPoint.Core.Tests
{
    public class ContentTests
    {
        [Fact]
        public void LoadsNamesIconsModelsAndSoundsFromAFullInstall()
        {
            using var fake = new FakeMinecraft();
            var root = Path.Combine(fake.Dir, ".minecraft");
            var text = new Dictionary<string, string>
            {
                ["assets/minecraft/lang/en_us.json"] = "{\"item.minecraft.sugar\":\"Sugar (test)\",\"block.minecraft.brewing_stand\":\"Stand (test)\",\"effect.minecraft.weakness\":\"Weak (test)\"}",
            };
            // made-up two-element model shared by every stand part
            const string model = "{\"textures\":{\"s\":\"block/test_stand\",\"b\":\"block/test_base\"},\"elements\":[" +
                "{\"from\":[7,0,7],\"to\":[9,14,9],\"faces\":{\"north\":{\"texture\":\"#s\"},\"up\":{\"texture\":\"#s\"}}}," +
                "{\"from\":[2,0,2],\"to\":[14,2,14],\"faces\":{\"up\":{\"texture\":\"#b\"}}}]}";
            foreach (var m in Sheets.Models)
                text[m.JarPath] = model;
            var pngs = Sheets.Ingredients.Select(i => i.McTexture).Concat(new[]
            {
                Catalog.BrewingStand.McIcon, "assets/minecraft/textures/block/test_stand.png", "assets/minecraft/textures/block/test_base.png",
            });
            fake.WriteJar(Path.Combine(root, "versions", "1.21.4", "1.21.4.jar"), text, pngs);
            var tone = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", "tone.ogg"));
            fake.WriteAssets(root, "19", new Dictionary<string, byte[]>
            {
                ["minecraft/sounds.json"] = Encoding.UTF8.GetBytes("{\"block.brewing_stand.brew\":{\"sounds\":[\"block/brewing_stand/brew1\"]}}"),
                ["minecraft/sounds/block/brewing_stand/brew1.ogg"] = tone,
            });

            using var content = MinecraftContent.Load(new[] { root }, out var report);
            Assert.NotNull(content);
            Assert.Equal("Sugar (test)", content!.Names["mc_sugar"]);
            Assert.Equal("Stand (test)", content.Names[Catalog.BrewingStand.Id]);
            Assert.Equal("Weak (test)", content.Names["weakness"]);
            Assert.Equal("Awkward", content.Names["awkward"]);
            Assert.Equal("Rabbit Foot", content.Names["mc_rabbit_foot"]); // not in the test lang: id fallback
            Assert.Equal(7, content.Models.Count);
            Assert.All(content.Models.Values, m => Assert.Equal(2, m.Submeshes.Count));
            Assert.True(content.Textures.ContainsKey("assets/minecraft/textures/block/test_base.png"));
            Assert.Single(content.Sounds["brew"]);
            Assert.Empty(content.Warnings);
            Assert.Equal((1, 1), MinecraftContent.PngSize(FakeMinecraft.TinyPng));
        }

        [Fact]
        public void MissingMinecraftGivesAReportNotAnException()
        {
            using var fake = new FakeMinecraft();
            Assert.Null(MinecraftContent.Load(new[] { fake.Dir }, out var report));
            Assert.Contains("no Minecraft client jar", report);
        }
    }
}
