using System;
using System.Collections.Generic;
using BrewingPoint.Brewing;
using HarmonyLib;
using Il2CppScheduleOne.Product;
using Il2CppScheduleOne.UI.Stations;

namespace BrewingPoint
{
    /// <summary>
    /// Minecraft's brewing rules on the station screen: the Brewing Stand only takes Minecraft ingredients,
    /// effect ingredients need an Awkward batch (Nether Wart first), and normal Mixing Stations send Minecraft
    /// ingredients to the Brewing Stand. Begin is disabled and the reason shown where the game shows its own.
    /// </summary>
    [HarmonyPatch(typeof(MixingStationInterface))]
    internal static class BrewGatePatches
    {
        internal static string? Problem(MixingStationInterface ui)
        {
            try
            {
                var station = ui.Station;
                if (station == null || station.CurrentMixOperation != null || station.OutputSlot.Quantity > 0)
                    return null;
                var mixer = station.MixerSlot?.ItemInstance;
                if (mixer == null || station.MixerSlot!.Quantity <= 0)
                    return null;
                var effects = new List<string>();
                var product = station.ProductSlot?.ItemInstance?.Definition?.TryCast<ProductDefinition>();
                if (product?.Properties != null)
                    for (int i = 0; i < product.Properties.Count; i++)
                        if (product.Properties[i] != null)
                            effects.Add(product.Properties[i].ID);
                var name = mixer.Definition != null ? mixer.Definition.Name : mixer.ID;
                return BrewRules.CannotStart(BrewingStands.IsBrewingStand(station), mixer.ID, effects, name);
            }
            catch (Exception ex)
            {
                BrewingPointMod.Log.Warning("Brewing rule check failed: " + ex.Message);
                return null;
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(nameof(MixingStationInterface.UpdateInstruction))]
        private static void Instruction(MixingStationInterface __instance)
        {
            var problem = Problem(__instance);
            if (problem == null || __instance.InstructionLabel == null)
                return;
            __instance.InstructionLabel.enabled = true;
            __instance.InstructionLabel.text = problem;
        }

        [HarmonyPostfix]
        [HarmonyPatch(nameof(MixingStationInterface.UpdateBeginButton))]
        private static void BeginButton(MixingStationInterface __instance)
        {
            if (__instance.BeginButton != null && Problem(__instance) != null)
                __instance.BeginButton.interactable = false;
        }

        [HarmonyPrefix]
        [HarmonyPatch(nameof(MixingStationInterface.BeginButtonPressed))]
        private static bool BeginPressed(MixingStationInterface __instance) => Problem(__instance) == null;
    }
}
