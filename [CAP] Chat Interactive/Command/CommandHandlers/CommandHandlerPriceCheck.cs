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
    /// Handles !pricecheck command (extracted from InventoryCommands.cs for maintainability).
    /// Adds brief base stats for apparel (armor) and weapons (damage/DPS) using a temporary Thing
    /// so quality + material multipliers are applied exactly as RimWorld does in-game.
    /// </summary>
    public static class CommandHandlerPriceCheck
    {
        public static string HandlePriceCheck(ChatMessageWrapper messageWrapper, string[] args)
        {
            if (args.Length == 0)
            {
                return "RICS.CC.pricecheck.usage".Translate();
            }

            var settings = CAPChatInteractiveMod.Instance?.Settings?.GlobalSettings;
            var currencySymbol = settings?.CurrencyName?.Trim() ?? "¢";

            try
            {
                var parsed = CommandParserUtility.ParseCommandArguments(
                    args,
                    allowQuality: true,
                    allowMaterial: true,
                    allowSide: false,
                    allowQuantity: true
                );

                if (parsed.HasError)
                {
                    return $"❌ {parsed.Error}";
                }

                var storeItem = StoreCommandHelper.GetStoreItemByName(parsed.ItemName);
                if (storeItem == null)
                {
                    return "RICS.CC.pricecheck.notfound".Translate(parsed.ItemName);
                }

                var thingDef = DefDatabase<ThingDef>.GetNamedSilentFail(storeItem.DefName);
                if (thingDef == null)
                {
                    return "RICS.CC.pricecheck.errorthingdef".Translate(parsed.ItemName);
                }

                var quality = ItemConfigHelper.ParseQuality(parsed.Quality);

                ThingDef material = null;
                if (parsed.Material != null && !parsed.Material.Equals("random", StringComparison.OrdinalIgnoreCase))
                {
                    material = ItemConfigHelper.ParseMaterial(parsed.Material, thingDef);
                    if (material == null)
                    {
                        parsed.Material = "";
                    }
                }

                if (quality.HasValue && !ItemConfigHelper.IsQualityAllowed(quality))
                {
                    return "RICS.CC.pricecheck.errorquality".Translate(quality.Value.ToString());
                }

                int price = ItemConfigHelper.CalculateFinalPrice(
                    storeItem,
                    parsed.Quantity,
                    quality,
                    material
                );

                // Build core response (unchanged translation)
                string quantityStr = parsed.Quantity > 1 ? $"{parsed.Quantity}x " : "";
                string qualityStr = quality.HasValue
                    ? quality.Value.ToString().ToLower()
                    : (thingDef.HasComp(typeof(CompQuality)) ? "normal" : "");
                string materialStr = material != null ? material.label : "";

                string response = "RICS.CC.pricecheck.success".Translate(quantityStr, storeItem.CustomName, qualityStr, materialStr, price, currencySymbol);

                // NEW: Append base stats (apparel armor or weapon damage)
                string statsSummary = GetItemStatsSummary(thingDef, material, quality);
                if (!string.IsNullOrEmpty(statsSummary))
                {
                    response += "\n" + statsSummary;
                }

                return response;
            }
            catch (Exception ex)
            {
                Logger.Error($"Error in pricecheck command: {ex}");
                return $"❌ Error calculating price: {ex.Message}";
            }
        }

        /// <summary>
        /// Creates a temporary Thing (quality + material applied) and returns brief stats.
        /// WHY: Direct ThingDef.GetStatValueAbstract ignores multipliers; this is the exact vanilla method used by apparel/weapon tooltips.
        /// </summary>
        private static string GetItemStatsSummary(ThingDef thingDef, ThingDef material, QualityCategory? quality)
        {
            Thing tempThing = null;
            try
            {
                // Auto-material for stuffable items (vanilla GenStuff behavior)
                ThingDef stuff = material;
                if (thingDef.MadeFromStuff && stuff == null)
                {
                    stuff = GenStuff.DefaultStuffFor(thingDef);
                }

                tempThing = ThingMaker.MakeThing(thingDef, stuff);

                if (quality.HasValue)
                {
                    var compQuality = tempThing.TryGetComp<CompQuality>();
                    if (compQuality != null)
                    {
                        compQuality.SetQuality(quality.Value, ArtGenerationContext.Outsider);
                    }
                }

                if (thingDef.IsApparel)
                {
                    return GetApparelArmorSummary(tempThing);
                }

                if (thingDef.IsWeapon)
                {
                    return GetWeaponDamageSummary(tempThing);
                }

                return "";
            }
            catch (Exception ex)
            {
                Logger.Error($"PriceCheck stats generation failed: {ex.Message}");
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
                ? "RICS.MPCH.ArmorHeader".Translate() + $" {string.Join(" ", armorStats)}"
                : "";
        }

        private static string GetWeaponDamageSummary(Thing weapon)
        {
            if (weapon.def.IsMeleeWeapon)
            {
                float dps = weapon.GetStatValue(StatDefOf.MeleeWeapon_AverageDPS);
                return $"⚔️ Melee DPS: {dps:F1}";
            }

            if (weapon.def.IsRangedWeapon)
            {
                // RimWorld 1.6 ProjectileProperties does NOT expose damageAmountBase / armorPenetrationBase as fields.
                // It provides GetDamageAmount(Thing) + GetArmorPenetration(Thing) — exactly what the in-game weapon inspect card uses.
                // We pass our temporary weapon (quality + material already applied) so values match the tooltip 100%.
                var rangedVerb = weapon.def.Verbs?.FirstOrDefault(v => !v.IsMeleeAttack && v.defaultProjectile != null);
                var projProps = rangedVerb?.defaultProjectile?.projectile;

                if (projProps != null)
                {
                    int damage = projProps.GetDamageAmount(weapon);
                    float ap = projProps.GetArmorPenetration(weapon);

                    string text = $"🔫 Damage: {damage}";
                    if (ap > 0.01f)
                    {
                        text += $" | AP: {ap.ToStringPercent()}";
                    }
                    return text;
                }
                return "🔫 Ranged weapon";
            }

            return "";
        }
    }
}
