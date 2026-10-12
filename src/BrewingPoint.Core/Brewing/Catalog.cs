using System;
using System.Collections.Generic;
using System.Linq;
using BrewingPoint.Data;

namespace BrewingPoint.Brewing
{
    /// <summary>Lookups over the generated sheets.</summary>
    public static class Catalog
    {
        public static readonly Dictionary<string, IngredientsRow> IngredientById =
            Sheets.Ingredients.ToDictionary(r => r.Id, StringComparer.Ordinal);

        public static readonly Dictionary<string, EffectsRow> EffectById =
            Sheets.Effects.ToDictionary(r => r.Id, StringComparer.Ordinal);

        public static readonly Dictionary<string, ModelsRow> ModelById =
            Sheets.Models.ToDictionary(r => r.Id, StringComparer.Ordinal);

        public static readonly Dictionary<string, SoundsRow> SoundById =
            Sheets.Sounds.ToDictionary(r => r.Id, StringComparer.Ordinal);

        public static readonly Dictionary<string, ShopsRow> ShopById =
            Sheets.Shops.ToDictionary(r => r.Id, StringComparer.Ordinal);

        public static StationsRow BrewingStand => Sheets.Stations[0];

        /// <summary>The game-side effect ID an effects row stands for (custom id, or null for vanilla: resolved through S1API).</summary>
        public static string? CustomEffectId(string effectId) =>
            EffectById[effectId].Kind == "custom" ? EffectById[effectId].CustomId : null;

        /// <summary>Every file the mashup reads from the player's Minecraft jar; all must exist for it to load.</summary>
        public static IReadOnlyCollection<string> RequiredJarEntries()
        {
            var set = new SortedSet<string>(StringComparer.Ordinal) { "assets/minecraft/lang/en_us.json" };
            foreach (var i in Sheets.Ingredients)
                set.Add(i.McTexture);
            foreach (var s in Sheets.Stations)
                set.Add(s.McIcon);
            foreach (var m in Sheets.Models)
                set.Add(m.JarPath);
            return set;
        }

        /// <summary>Model id ("brewing_stand") -> Minecraft model reference ("block/brewing_stand").</summary>
        public static string ModelReference(ModelsRow row)
        {
            const string prefix = "assets/minecraft/models/";
            var path = row.JarPath;
            if (path.StartsWith(prefix, StringComparison.Ordinal))
                path = path.Substring(prefix.Length);
            return path.EndsWith(".json", StringComparison.Ordinal) ? path.Substring(0, path.Length - 5) : path;
        }

        /// <summary>#RRGGBB -> 0..1 RGB.</summary>
        public static (float r, float g, float b) Rgb(string hex)
        {
            int v = Convert.ToInt32(hex.TrimStart('#'), 16);
            return (((v >> 16) & 0xFF) / 255f, ((v >> 8) & 0xFF) / 255f, (v & 0xFF) / 255f);
        }
    }
}
