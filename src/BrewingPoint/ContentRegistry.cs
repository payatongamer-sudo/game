using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BrewingPoint.Brewing;
using BrewingPoint.Data;
using S1API.Items;
using S1API.Items.Ingredient;
using S1API.Products;
using S1API.Properties;
using S1API.Properties.Interfaces;
using S1API.Shops;
using UnityEngine;
using BuildableItemCreator = S1API.Items.Buildable.BuildableItemCreator;

namespace BrewingPoint
{
    /// <summary>
    /// Registers the sheets with Schedule I through S1API: custom effects, the Minecraft ingredients,
    /// the Brewing Stand, the brewing reactions, and the shop listings. Runs identically on every peer.
    /// </summary>
    internal static class ContentRegistry
    {
        private static bool _registered;
        internal static readonly Dictionary<string, PropertyBase> Effects = new Dictionary<string, PropertyBase>(StringComparer.Ordinal);
        internal static readonly Dictionary<string, ItemDefinition> Items = new Dictionary<string, ItemDefinition>(StringComparer.Ordinal);

        /// <summary>GameLifecycle.OnPreLoad. S1API re-applies registered content on later loads, so this runs once.</summary>
        public static void Register()
        {
            if (_registered || BrewingPointMod.Minecraft == null)
                return;
            _registered = true;
            var names = BrewingPointMod.Minecraft.Names;

            try
            {
                foreach (var row in Sheets.Effects)
                    Effects[row.Id] = row.Kind == "vanilla" ? Vanilla(row.S1apiToken) : Custom(row, names[row.Id]);
                Checks.Ok("effects", $"{Effects.Count} effects ({Sheets.Effects.Count(e => e.Kind == "custom")} custom)");
            }
            catch (Exception ex)
            {
                Checks.Fail("effects", ex.Message);
                BrewingPointMod.Log.Error(ex.ToString());
                return;
            }

            int ingredients = 0;
            foreach (var row in Sheets.Ingredients)
            {
                try
                {
                    var builder = MixIngredientItemCreator.CloneFrom(row.CloneFrom)
                        .WithBasicInfo(row.Id, names[row.Id], row.Description, ItemCategory.Ingredient)
                        .WithPricing(row.Price, 0.5f)
                        .WithRequiredRank(null)
                        .WithEffect(Effects[row.Effect]);
                    var icon = UnityAssets.Icon(row.McTexture);
                    if (icon != null)
                        builder = builder.WithIcon(icon);
                    Items[row.Id] = builder.Build();
                    ingredients++;
                }
                catch (Exception ex)
                {
                    Checks.Fail("ingredient_" + row.Id, ex.Message);
                    BrewingPointMod.Log.Error(ex.ToString());
                }
            }
            if (ingredients == Sheets.Ingredients.Length)
                Checks.Ok("ingredients", $"{ingredients} Minecraft ingredients: " + string.Join(", ", Sheets.Ingredients.Select(i => names[i.Id])));

            var stand = Catalog.BrewingStand;
            try
            {
                var template = ItemManager.GetDefinition(stand.CloneFrom);
                var builder = BuildableItemCreator.CloneFrom(stand.CloneFrom)
                    .WithBasicInfo(stand.Id, names[stand.Id], stand.Description, template?.Category ?? ItemCategory.Equipment)
                    .WithPricing(stand.Price, 0.5f)
                    .WithRequiredRank(null);
                var icon = UnityAssets.Icon(stand.McIcon);
                if (icon != null)
                    builder = builder.WithIcon(icon);
                Items[stand.Id] = builder.Build();
                Checks.Ok("brewing_stand", names[stand.Id] + " cloned from " + stand.CloneFrom);
            }
            catch (Exception ex)
            {
                Checks.Fail("brewing_stand", ex.Message);
                BrewingPointMod.Log.Error(ex.ToString());
            }

            try
            {
                foreach (var r in Sheets.Reactions)
                    MixReactions.AddRule(Effects[r.MixerEffect], Effects[r.WhenContains], Effects[r.Result], null, r.Replace);
                Checks.Ok("reactions", $"{Sheets.Reactions.Length} Minecraft brewing rules");
            }
            catch (Exception ex)
            {
                Checks.Fail("reactions", ex.Message);
                BrewingPointMod.Log.Error(ex.ToString());
            }
        }

        /// <summary>GameLifecycle.OnLoadComplete: shops exist now. Listings are client-local, so every peer adds them.</summary>
        public static void OnLoadComplete()
        {
            if (!_registered)
                return;
            int listed = 0;
            var places = new HashSet<string>();
            void List(string itemId, string shopGroup, float price)
            {
                if (!Items.TryGetValue(itemId, out var item))
                    return;
                foreach (var shop in ShopManager.FindShopsByItem(Catalog.ShopById[shopGroup].AnchorItem))
                {
                    if (!shop.HasItem(itemId) && shop.AddItem(item, price))
                        listed++;
                    places.Add(shop.Name);
                }
            }
            try
            {
                foreach (var row in Sheets.Ingredients)
                    List(row.Id, row.Shop, row.Price);
                List(Catalog.BrewingStand.Id, Catalog.BrewingStand.Shop, Catalog.BrewingStand.Price);
                if (places.Count == 0)
                    Checks.Fail("shops", "no shop sells the anchor items " + string.Join(", ", Sheets.Shops.Select(s => s.AnchorItem)));
                else
                    Checks.Ok("shops", $"{listed} new listings at {string.Join(", ", places)}");
            }
            catch (Exception ex)
            {
                Checks.Fail("shops", ex.Message);
                BrewingPointMod.Log.Error(ex.ToString());
            }
            TestMode.OnLoadComplete();
        }

        private static PropertyBase Vanilla(string token)
        {
            var field = typeof(Property).GetField(token, BindingFlags.Public | BindingFlags.Static);
            return field?.GetValue(null) as PropertyBase
                   ?? throw new InvalidOperationException("S1API has no Property." + token);
        }

        private static PropertyBase Custom(EffectsRow row, string name)
        {
            var (pr, pg, pb) = Catalog.Rgb(row.ProductColor);
            var (lr, lg, lb) = Catalog.Rgb(row.LabelColor);
            var builder = EffectCreator.CreateBuilder()
                .WithBasicInfo(row.CustomId, name, $"Minecraft brewing: {name}.")
                .WithTier(row.Tier ?? 1)
                .WithAddictiveness(row.Addictiveness ?? 0f)
                .WithValue(row.ValueChange ?? 0, row.ValueMultiplier ?? 1f)
                .WithColors(new Color(pr, pg, pb), new Color(lr, lg, lb));
            if (row.MixPush == "zero")
                builder = builder.WithMixGeometry(Vector2.zero, 0f);
            return builder.Build();
        }
    }
}
