using System;
using System.Collections.Generic;
using System.Linq;
using BrewingPoint.Data;

namespace BrewingPoint.Brewing
{
    /// <summary>
    /// Minecraft's brewing-stand rules, decided before a brew starts (the mix maths itself is the game's,
    /// plus the reactions sheet). Pure: the mod passes in what's in the station's slots.
    /// </summary>
    public static class BrewRules
    {
        public const string NeedsAwkward = "Brew Nether Wart in first: {0} needs an Awkward batch.";
        public const string StandOnlyMinecraft = "The Brewing Stand only takes Minecraft brewing ingredients.";
        public const string UseTheStand = "{0} is brewed at a Brewing Stand (Dan's Hardware).";

        /// <summary>
        /// Why this brew can't start, or null if it can.
        /// </summary>
        /// <param name="isBrewingStand">The station is the Minecraft Brewing Stand.</param>
        /// <param name="ingredientId">Item in the mixer slot (null/empty = nothing loaded yet).</param>
        /// <param name="productEffectIds">Effect IDs already on the product in the product slot.</param>
        /// <param name="ingredientName">Display name for messages.</param>
        public static string? CannotStart(bool isBrewingStand, string? ingredientId, IEnumerable<string> productEffectIds, string ingredientName)
        {
            if (string.IsNullOrEmpty(ingredientId))
                return null;
            var stand = Catalog.BrewingStand;
            Catalog.IngredientById.TryGetValue(ingredientId!, out var ingredient);

            if (!isBrewingStand)
            {
                if (ingredient != null && stand.VanillaStationsRejectRoles.Contains(ingredient.Role))
                    return string.Format(UseTheStand, ingredientName);
                return null;
            }

            if (ingredient == null || !stand.AcceptsRoles.Contains(ingredient.Role))
                return StandOnlyMinecraft;

            if (stand.NeedsAwkwardRoles.Contains(ingredient.Role))
            {
                var awkwardId = Catalog.CustomEffectId("awkward");
                if (!productEffectIds.Any(e => string.Equals(e, awkwardId, StringComparison.OrdinalIgnoreCase)))
                    return string.Format(NeedsAwkward, ingredientName);
            }
            return null;
        }

        /// <summary>
        /// Plays the reactions sheet on an effect list the way S1API MixReactions does after the game's own
        /// maths: for the mixer's effect, each matching rule replaces (or adds) its result, without duplicates,
        /// up to 8 effects. Used by tests and the mod's log to explain a brew; the game applies the real thing.
        /// </summary>
        public static List<string> ApplyReactions(List<string> gameResult, string mixerEffect)
        {
            var working = new List<string>(gameResult);
            foreach (var rule in Sheets.Reactions)
            {
                if (rule.MixerEffect != mixerEffect)
                    continue;
                int at = working.IndexOf(rule.WhenContains);
                if (at < 0)
                    continue;
                if (rule.Replace)
                {
                    working[at] = rule.Result;
                    int first = working.IndexOf(rule.Result);
                    for (int i = working.Count - 1; i > first; i--)
                        if (working[i] == rule.Result)
                            working.RemoveAt(i);
                }
                else if (working.Count < 8 && !working.Contains(rule.Result))
                {
                    working.Add(rule.Result);
                }
            }
            return working;
        }
    }
}
