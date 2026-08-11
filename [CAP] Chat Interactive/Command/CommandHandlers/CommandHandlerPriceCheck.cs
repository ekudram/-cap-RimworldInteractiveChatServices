// File: CommandHandlerPriceCheck.cs
//
// Copyright (c) Captolamia
// This file is part of CAP Chat Interactive (RICS).
// Licensed under the GNU Affero General Public License v3.0 or later.
// See LICENSE.txt in the project root for full license text.
//
// !pricecheck — store price + brief apparel/weapon stats
using _CAP__Chat_Interactive.Command.CommandHelpers;
using CAP_ChatInteractive.Utilities;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace CAP_ChatInteractive.Commands.CommandHandlers
{
    /// <summary>
    /// Handles !pricecheck (extracted from InventoryCommands for maintainability).
    /// Apparel armor / weapon damage use a temporary Thing so quality + material match in-game tooltips.
    /// </summary>
    public static class CommandHandlerPriceCheck
    {
        private const string ReturnDivider = " | ";

        public static string HandlePriceCheck(ChatMessageWrapper messageWrapper, string[] args)
        {
            if (args == null || args.Length == 0)
                return "RICS.CC.pricecheck.usage".Translate();

            var settings = CAPChatInteractiveMod.Instance?.Settings?.GlobalSettings;
            var currencySymbol = settings?.CurrencyName?.Trim() ?? "¢";

            try
            {
                var parsed = CommandParserUtility.ParseCommandArguments(
                    args,
                    allowQuality: true,
                    allowMaterial: true,
                    allowSide: false,
                    allowQuantity: true);

                if (parsed.HasError)
                    return parsed.Error;

                var storeItem = StoreCommandHelper.GetStoreItemByName(parsed.ItemName);
                if (storeItem == null)
                    return "RICS.CC.pricecheck.notfound".Translate(parsed.ItemName);

                var thingDef = DefDatabase<ThingDef>.GetNamedSilentFail(storeItem.DefName);
                if (thingDef == null)
                    return "RICS.CC.pricecheck.errorthingdef".Translate(parsed.ItemName);

                var researchResult = StoreCommandHelper.HasRequiredResearch(storeItem);
                string researchEmoji = researchResult.Allowed ? "🔬✅" : "🔬🔒";

                var quality = ItemConfigHelper.ParseQuality(parsed.Quality);

                ThingDef material = null;
                if (parsed.Material != null && !parsed.Material.Equals("random", StringComparison.OrdinalIgnoreCase))
                {
                    material = ItemConfigHelper.ParseMaterial(parsed.Material, thingDef);
                    if (material == null)
                        parsed.Material = "";
                }

                if (quality.HasValue && !ItemConfigHelper.IsQualityAllowed(quality))
                    return "RICS.CC.pricecheck.errorquality".Translate(quality.Value.ToString());

                int quantity = Math.Max(1, parsed.Quantity);
                int price = ItemConfigHelper.CalculateFinalPrice(storeItem, quantity, quality, material);

                string quantityStr = quantity > 1 ? $"{quantity} " : "";
                string qualityStr = quality.HasValue
                    ? quality.Value.ToString().ToLower()
                    : (thingDef.HasComp(typeof(CompQuality)) ? "normal" : "");
                string materialStr = material != null ? material.label : "";

                string response = researchEmoji + " " + "RICS.CC.pricecheck.success".Translate(
                    quantityStr, storeItem.CustomName, qualityStr, materialStr, price, currencySymbol);

                // Optional stats (chat multi-part uses | — truncation is command processor)
                string statsSummary = GetItemStatsSummary(thingDef, material, quality);
                if (!string.IsNullOrEmpty(statsSummary))
                    response += ReturnDivider + statsSummary;

                return response;
            }
            catch (Exception ex)
            {
                Logger.Error($"[PriceCheck] Error: {ex}");
                return "RICS.CC.pricecheck.error".Translate();
            }
        }

        /// <summary>
        /// Temp Thing with quality + material so stats match vanilla tooltips.
        /// </summary>
        private static string GetItemStatsSummary(ThingDef thingDef, ThingDef material, QualityCategory? quality)
        {
            Thing tempThing = null;
            try
            {
                ThingDef stuff = material;
                if (thingDef.MadeFromStuff && stuff == null)
                    stuff = GenStuff.DefaultStuffFor(thingDef);

                tempThing = ThingMaker.MakeThing(thingDef, stuff);

                if (quality.HasValue)
                {
                    var compQuality = tempThing.TryGetComp<CompQuality>();
                    compQuality?.SetQuality(quality.Value, ArtGenerationContext.Outsider);
                }

                if (thingDef.IsApparel)
                    return GetApparelArmorSummary(tempThing);

                if (thingDef.IsWeapon)
                    return GetWeaponDamageSummary(tempThing);

                return "";
            }
            catch (Exception ex)
            {
                Logger.Error($"[PriceCheck] Stats generation failed: {ex.Message}");
                return "";
            }
            finally
            {
                tempThing?.Destroy(DestroyMode.Vanish);
            }
        }

        private static string GetApparelArmorSummary(Thing apparel)
        {
            float sharp = apparel.GetStatValue(StatDefOf.ArmorRating_Sharp);
            float blunt = apparel.GetStatValue(StatDefOf.ArmorRating_Blunt);
            float heat = apparel.GetStatValue(StatDefOf.ArmorRating_Heat);

            var armorStats = new List<string>();
            if (sharp >= 0.01f) armorStats.Add($"🗡️{sharp.ToStringPercent()}");
            if (blunt >= 0.01f) armorStats.Add($"🔨{blunt.ToStringPercent()}");
            if (heat >= 0.01f) armorStats.Add($"🔥{heat.ToStringPercent()}");

            return armorStats.Count > 0
                ? "RICS.MPCH.ArmorHeader".Translate() + string.Join(" ", armorStats)
                : "";
        }

        /// <summary>Used by pricecheck and MyPawn weapon report.</summary>
        public static string GetWeaponDamageSummary(Thing weapon)
        {
            if (weapon?.def == null)
                return "";

            if (weapon.def.IsMeleeWeapon)
            {
                float dps = weapon.GetStatValue(StatDefOf.MeleeWeapon_AverageDPS);
                return "RICS.CC.pricecheck.meleeDps".Translate(dps.ToString("F1"));
            }

            if (weapon.def.IsRangedWeapon)
            {
                // 1.6: GetDamageAmount(Thing) / GetArmorPenetration(Thing) match inspect card
                var rangedVerb = weapon.def.Verbs?.FirstOrDefault(v => !v.IsMeleeAttack && v.defaultProjectile != null);
                var projProps = rangedVerb?.defaultProjectile?.projectile;

                if (projProps != null)
                {
                    int damage = projProps.GetDamageAmount(weapon);
                    float ap = projProps.GetArmorPenetration(weapon);
                    if (ap > 0.01f)
                        return "RICS.CC.pricecheck.rangedDamageAp".Translate(damage, ap.ToStringPercent());
                    return "RICS.CC.pricecheck.rangedDamage".Translate(damage);
                }

                return "RICS.CC.pricecheck.rangedGeneric".Translate();
            }

            return "";
        }
    }
}
