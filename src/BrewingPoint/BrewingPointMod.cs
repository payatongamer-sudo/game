using System;
using System.IO;
using BrewingPoint.Minecraft;
using MelonLoader;
using MelonLoader.Utils;
using S1API.Building;
using S1API.Lifecycle;
using UnityEngine;

[assembly: MelonInfo(typeof(BrewingPoint.BrewingPointMod), "Brewing Point", "0.1.0", "payatongamer")]
[assembly: MelonGame("TVGS", "Schedule I")]

namespace BrewingPoint
{
    /// <summary>
    /// Brewing Point: Minecraft's Brewing Stand and brewing ingredients in Schedule I, read from the
    /// player's own Minecraft: Java Edition. Design lives in sheets/*.json; this is the Schedule I glue.
    /// </summary>
    public sealed class BrewingPointMod : MelonMod
    {
        internal static MelonLogger.Instance Log = null!;
        internal static MinecraftContent? Minecraft;
        private float _nextPoll;

        public override void OnInitializeMelon()
        {
            Log = LoggerInstance;
            Checks.Echo = line => Log.Msg(line);
            Checks.Begin(MelonEnvironment.UserDataDirectory);

            var config = Path.Combine(MelonEnvironment.UserDataDirectory, "BrewingPoint.cfg");
            var candidates = MinecraftLocator.Candidates(Environment.GetCommandLineArgs(), Environment.GetEnvironmentVariable, config);
            try
            {
                Minecraft = MinecraftContent.Load(candidates, out var report);
                if (Minecraft == null)
                {
                    Log.Error("Brewing Point needs your own Minecraft: Java Edition (1.14 or newer, launched once). Looked in:" +
                              Environment.NewLine + report +
                              Environment.NewLine + "Set minecraft_dir=<folder> in UserData/BrewingPoint.cfg if it lives somewhere else.");
                    Checks.Fail("minecraft_found", report);
                    return;
                }
            }
            catch (Exception ex)
            {
                Log.Error("Reading Minecraft failed: " + ex);
                Checks.Fail("minecraft_found", ex.Message);
                return;
            }

            Checks.Ok("minecraft_found", $"{Minecraft.Install.VersionId} at {Minecraft.Install.Root}");
            foreach (var warning in Minecraft.Warnings)
                Log.Warning(warning);
            Checks.Ok("minecraft_content", $"{Minecraft.Names.Count} names, {Minecraft.Textures.Count} textures, {Minecraft.Models.Count} models, {Minecraft.Sounds.Count} sounds, {Minecraft.Warnings.Count} warnings");

            GameLifecycle.OnPreLoad += ContentRegistry.Register;
            GameLifecycle.OnLoadComplete += ContentRegistry.OnLoadComplete;
            BuildEvents.OnBuildableItemInitialized += BrewingStands.OnBuildableInitialized;
        }

        public override void OnUpdate()
        {
            if (Minecraft == null || Time.unscaledTime < _nextPoll)
                return;
            _nextPoll = Time.unscaledTime + 0.25f;
            BrewingStands.Poll();
        }
    }
}
