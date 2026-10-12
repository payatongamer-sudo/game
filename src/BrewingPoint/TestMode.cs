using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BrewingPoint.Brewing;
using BrewingPoint.Data;
using Il2CppScheduleOne;
using Il2CppScheduleOne.Effects;
using Il2CppScheduleOne.Product;
using MelonLoader.Utils;
using S1API.Console;

namespace BrewingPoint
{
    /// <summary>
    /// After every load: run Minecraft's brew order through the game's own mixing maths (with the reactions
    /// applied) and log the result as checks. In test mode (--brewingpoint-test, BREWINGPOINT_TEST=1 or
    /// UserData/BrewingPoint.test) also hand the player a Brewing Stand, the ingredients, OG Kush and cash,
    /// so a test run can place the stand and brew straight away.
    /// </summary>
    internal static class TestMode
    {
        public static bool On =>
            Environment.GetCommandLineArgs().Any(a => string.Equals(a, "--brewingpoint-test", StringComparison.OrdinalIgnoreCase)) ||
            Environment.GetEnvironmentVariable("BREWINGPOINT_TEST") == "1" ||
            File.Exists(Path.Combine(MelonEnvironment.UserDataDirectory, "BrewingPoint.test"));

        public static void OnLoadComplete()
        {
            CheckBrewOrder();
            if (!On)
                return;
            try
            {
                ConsoleHelper.RunCashCommand(2000);
                ConsoleHelper.AddItemToInventory(Catalog.BrewingStand.Id, 1);
                ConsoleHelper.AddItemToInventory("ogkush", 6);
                foreach (var id in new[] { "mc_nether_wart", "mc_sugar", "mc_fermented_spider_eye", "mc_glowstone_dust" })
                    ConsoleHelper.AddItemToInventory(id, 6);
                Checks.Ok("test_mode", "gave a Brewing Stand, OG Kush x6, Nether Wart, Sugar, Fermented Spider Eye, Glowstone Dust x6 and $2000");
            }
            catch (Exception ex)
            {
                Checks.Fail("test_mode", ex.Message);
            }
        }

        /// <summary>OG Kush + Nether Wart + Sugar, then + Fermented Spider Eye, through EffectMixCalculator.</summary>
        private static void CheckBrewOrder()
        {
            try
            {
                var kush = Registry.GetItem("ogkush")?.TryCast<ProductDefinition>();
                Effect? EffectOf(string itemId) => Registry.GetItem(itemId)?.TryCast<PropertyItemDefinition>()?.Properties?[0];
                var wart = EffectOf("mc_nether_wart");
                var sugar = EffectOf("mc_sugar");
                var eye = EffectOf("mc_fermented_spider_eye");
                if (kush == null || wart == null || sugar == null || eye == null)
                {
                    Checks.Fail("brew_order", "OG Kush or a Minecraft ingredient is missing from the registry");
                    return;
                }
                var awkward = EffectMixCalculator.MixProperties(kush.Properties, wart, kush.DrugType);
                var swift = EffectMixCalculator.MixProperties(awkward, sugar, kush.DrugType);
                var corrupted = EffectMixCalculator.MixProperties(swift, eye, kush.DrugType);
                string Ids(Il2CppSystem.Collections.Generic.List<Effect> list)
                {
                    var ids = new List<string>();
                    for (int i = 0; i < list.Count; i++)
                        ids.Add(list[i].ID);
                    return "[" + string.Join(", ", ids) + "]";
                }
                bool Has(Il2CppSystem.Collections.Generic.List<Effect> list, string id)
                {
                    for (int i = 0; i < list.Count; i++)
                        if (string.Equals(list[i].ID, id, StringComparison.OrdinalIgnoreCase))
                            return true;
                    return false;
                }
                var awkwardId = Catalog.CustomEffectId("awkward")!;
                var weaknessId = Catalog.CustomEffectId("weakness")!;
                var energizingId = ContentRegistry.Effects["energizing"].ID;
                bool ok = Has(awkward, awkwardId) && !Has(swift, awkwardId) && Has(swift, energizingId)
                          && Has(corrupted, weaknessId) && !Has(corrupted, energizingId);
                var detail = $"OG Kush {Ids(kush.Properties)} + Nether Wart = {Ids(awkward)}; + Sugar = {Ids(swift)}; + Fermented Spider Eye = {Ids(corrupted)}";
                if (ok)
                    Checks.Ok("brew_order", detail);
                else
                    Checks.Fail("brew_order", detail);
            }
            catch (Exception ex)
            {
                Checks.Fail("brew_order", ex.Message);
            }
        }
    }
}
