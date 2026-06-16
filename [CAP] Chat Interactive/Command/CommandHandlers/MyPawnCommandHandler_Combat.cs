// MyPawnCommandHandler_Combat.cs
// Copyright (c) Captolamia
// This file is part of RICS (Rimworld Interactive Chat Services).
// 
// CAP Chat Interactive is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License as published
// by the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// 
// CAP Chat Interactive is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
// GNU Affero General Public License for more details.
// 
// You should have received a copy of the GNU Affero General Public License
// along with CAP Chat Interactive. If not, see <https://www.gnu.org/licenses/>.
// Handles !mypawn gear and !mypawn kills/killcount subcommands.
// Extracted from main handler for readability and maintainability.

using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using Verse;
using Verse.Noise;

namespace CAP_ChatInteractive.Commands.CommandHandlers
{
    public static class MyPawnCommandHandler_Combat
    {
        // === Gear ===
        /// <summary>
        /// Provides a comprehensive overview of the pawn's current gear and combat-related stats, including:
        /// - Weapons (primary and sidearms)
        /// - Apparel
        /// - Inventory items
        /// - Armor stats
        /// </summary>
        /// <param name="pawn">The pawn whose gear information is being requested.</param>
        /// <param name="args">Additional arguments for the command.</param>
        /// <returns>A formatted string containing the pawn's gear and combat-related stats.</returns>
        public static string HandleGearInfo(Pawn pawn, string[] args)
        {
            var report = new StringBuilder();
            report.AppendLine("RICS.MPCH.GearHeader".Translate());

            // Weapons - check for Simple Sidearms first (unchanged)
            var weapons = GetWeaponsList(pawn);
            if (weapons.Count > 0)
            {
                report.AppendLine("RICS.MPCH.WeaponsHeader".Translate());
                report.AppendLine(string.Join(", ", weapons));
            }

            // === APPAREL SECTION (conditional) ===
            // General path (no args): simple list only — NO materials/layers.
            // This prevents the report from becoming too long/spammy and exceeding chat limits.
            // Filtered path (args present): show ONLY apparel covering the requested body part,
            // including material (if any) and layer. Mirrors the existing !mypawn body <part> UX.
            string bodyPartFilter = args != null && args.Length > 0
                ? string.Join(" ", args).ToLowerInvariant().Trim()
                : null;

            if (!string.IsNullOrEmpty(bodyPartFilter))
            {
                // Specific body part filter provided → detailed coverage view
                BodyPartRecord targetPart = null;
                if (pawn.RaceProps?.body?.AllParts != null)
                {
                    targetPart = pawn.RaceProps.body.AllParts.FirstOrDefault(p =>
                        (p.def?.label?.ToLower().Contains(bodyPartFilter) ?? false) ||
                        (p.def?.defName?.ToLower().Contains(bodyPartFilter) ?? false));
                }

                if (targetPart == null)
                {
                    // Reuse existing translation key from the body handler for consistency
                    return "RICS.MPCH.BodyPartNotFound".Translate(bodyPartFilter);
                }

                report.AppendLine();
                report.AppendLine("RICS.MPCH.GearPartHeader".Translate(targetPart.LabelCap));

                var coveringApparel = pawn.apparel?.WornApparel?
                    .Where(a => a != null && a.def?.apparel != null && a.def.apparel.CoversBodyPart(targetPart))
                    .ToList() ?? new List<Apparel>();

                if (coveringApparel.Count == 0)
                {
                    report.AppendLine("RICS.MPCH.NoApparelCoveringPart".Translate(targetPart.LabelCap));
                }
                else
                {
                    foreach (var item in coveringApparel)
                    {
                        string baseName = MyPawnCommandHandler.StripTags(item.def.LabelCap);
                        string quality = item.TryGetQuality(out QualityCategory qc) ? $" ({qc})" : "";
                        string hitPoints = item.HitPoints != item.MaxHitPoints
                            ? $" {((float)item.HitPoints / item.MaxHitPoints).ToStringPercent()}"
                            : "";

                        // Material info — only when relevant.
                        // Uses vanilla Thing.Stuff + MadeFromStuff. Many modded/unique items have no Stuff.
                        string materialInfo = "";
                        if (item.Stuff != null)
                        {
                            materialInfo = $" ({MyPawnCommandHandler.StripTags(item.Stuff.LabelCap)})";
                        }
                        else if (item.def.MadeFromStuff)
                        {
                            materialInfo = " (unknown material)";
                        }

                        // Layer info — last entry in the layers list is the primary/outermost.
                        // Works for vanilla (OnSkin/Middle/Shell) and modded layers (Utility, Belt, Overhead, etc.)
                        // without needing to research every possible ApparelLayerDef.
                        string layerInfo = "";
                        var appLayers = item.def.apparel?.layers;
                        if (appLayers != null && appLayers.Count > 0)
                        {
                            var primaryLayer = appLayers[appLayers.Count - 1];
                            layerInfo = $" [{primaryLayer.LabelCap} Layer]";
                        }

                        report.AppendLine($"  • {baseName}{quality}{hitPoints}{materialInfo}{layerInfo}");
                    }
                }
            }
            else
            {
                // General path — keep the original simple list (no materials, no layers)
                // so the default !mypawn gear output stays short and readable.
                var apparel = pawn.apparel?.WornApparel;
                if (apparel != null && apparel.Count > 0)
                {
                    report.AppendLine("RICS.MPCH.ApparelHeader".Translate());
                    foreach (var item in apparel)
                    {
                        string baseName = MyPawnCommandHandler.StripTags(item.def.LabelCap);
                        string quality = item.TryGetQuality(out QualityCategory qc) ? $" ({qc})" : "";
                        string hitPoints = item.HitPoints != item.MaxHitPoints
                            ? $" {((float)item.HitPoints / item.MaxHitPoints).ToStringPercent()}"
                            : "";
                        report.AppendLine($"  • {baseName}{quality}{hitPoints}");
                        // NOTE: Materials/layers are intentionally omitted in the general view.
                        // Viewers should use !mypawn gear <bodypart> (torso/head/left arm/etc.) for details.
                    }
                }
            }

            // Inventory items — unchanged (single-pass filter to avoid duplicate medicine reporting)
            var inventory = pawn.inventory?.innerContainer;
            if (inventory != null && inventory.Count > 0)
            {
                var notableItems = inventory.Where(item =>
                    item != null && item.def != null && item.stackCount > 0 &&
                    (item.def.IsMedicine ||
                     item.def.IsDrug ||
                     (item.def.IsIngestible && !item.def.IsMedicine && !item.def.IsDrug)))
                    .ToList();

                if (notableItems.Any())
                {
                    report.AppendLine("RICS.MPCH.InventoryHeader".Translate());
                    foreach (var item in notableItems)
                    {
                        string stackInfo = item.stackCount > 1 ? $" x{item.stackCount}" : "";
                        report.AppendLine($"  • {MyPawnCommandHandler.StripTags(item.LabelCap)}{stackInfo}");
                    }
                }
            }

            // Armor stats from RimWorld (no complex calculations) — shown in both paths
            report.Append(GetArmorSummary(pawn));

            return report.ToString();
        }

        private static List<string> GetWeaponsList(Pawn pawn)
        {
            var weapons = new List<string>();

            // Check for Simple Sidearms mod
            if (ModLister.GetActiveModWithIdentifier("PeteTimesSix.SimpleSidearms") != null)
            {
                try
                {
                    var sidearms = GetSidearmsViaReflection(pawn);
                    if (sidearms != null && sidearms.Count > 0)
                    {
                        // Enhanced display: include material where available + mark first melee sidearm
                        int meleeCount = 0;
                        foreach (var sidearm in sidearms)
                        {
                            if (sidearm == null) continue;

                            string label = MyPawnCommandHandler.StripTags(sidearm.LabelCap);
                            string material = GetWeaponMaterialString(sidearm);

                            string entry = !string.IsNullOrEmpty(material)
                                ? $"{label} ({material})"
                                : label;

                            // Highlight first melee sidearm
                            if (meleeCount == 0 && sidearm.def.IsMeleeWeapon)
                            {
                                entry = $"★ {entry} [Primary Sidearm]";
                                meleeCount++;
                            }
                            else if (sidearm.def.IsMeleeWeapon)
                            {
                                meleeCount++;
                            }

                            weapons.Add(entry);
                        }

                        if (weapons.Count > 0)
                            return weapons;
                    }
                }
                catch (Exception ex)
                {
                    Logger.Warning($"Failed to get SimpleSidearms data: {ex.Message}");
                }
            }

            // Fallback: standard equipment
            var equipment = pawn.equipment?.AllEquipmentListForReading;
            if (equipment != null && equipment.Count > 0)
            {
                weapons.AddRange(equipment.Select(e =>
                {
                    string label = MyPawnCommandHandler.StripTags(e.LabelCap);
                    string material = GetWeaponMaterialString(e);
                    return !string.IsNullOrEmpty(material) ? $"{label} ({material})" : label;
                }));
            }

            return weapons;
        }

        private static string GetWeaponMaterialString(Thing weapon)
        {
            if (weapon == null) return null;

            if (weapon.Stuff != null)
            {
                return MyPawnCommandHandler.StripTags(weapon.Stuff.LabelCap);
            }

            if (weapon.def.MadeFromStuff)
            {
                return "RICS.MPCH.WeaponMaterialUnknown".Translate();
            }

            return null;
        }

        private static List<Thing> GetSidearmsViaReflection(Pawn pawn)
        {
            try
            {
                var simpleSidearmsType = Type.GetType("SimpleSidearms.SimpleSidearms, SimpleSidearms");
                if (simpleSidearmsType != null)
                {
                    var getSidearmsMethod = simpleSidearmsType.GetMethod("GetSidearms", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
                    if (getSidearmsMethod != null)
                    {
                        var result = getSidearmsMethod.Invoke(null, new object[] { pawn }) as IEnumerable<Thing>;
                        return result?.ToList() ?? new List<Thing>();
                    }
                }
            }
            catch (Exception)
            {
                // Silent fail for optional integration
            }
            return new List<Thing>();
        }

        private static string GetArmorSummary(Pawn pawn)
        {
            if (pawn?.apparel?.WornApparel == null || !pawn.apparel.WornApparel.Any())
                return "RICS.MPCH.ArmorNone".Translate() + "\n";

            try
            {
                float sharp = pawn.GetStatValue(StatDefOf.ArmorRating_Sharp);
                float blunt = pawn.GetStatValue(StatDefOf.ArmorRating_Blunt);
                float heat = pawn.GetStatValue(StatDefOf.ArmorRating_Heat);

                var parts = new List<string>();
                if (sharp >= 0.01f) parts.Add($"🗡️{sharp.ToStringPercent()}");
                if (blunt >= 0.01f) parts.Add($"🔨{blunt.ToStringPercent()}");
                if (heat >= 0.01f) parts.Add($"🔥{heat.ToStringPercent()}");

                return "RICS.MPCH.ArmorHeader".Translate() + string.Join(" ", parts) + "\n";
            }
            catch (Exception ex)
            {
                Logger.Error($"Error calculating armor: {ex}");
                return "RICS.MPCH.ArmorNone".Translate() + "\n";
            }
        }

        // === Weapon Details ===
        /// <summary>
        /// Provides a detailed overview of the pawn's equipped weapons, including:
        /// - Primary weapon
        /// - Sidearms (if Simple Sidearms mod is active)
        /// - Material / Stuff
        /// </summary>
        /// <param name="pawn">The pawn whose weapon information is being retrieved.</param>
        /// <returns>A string containing the detailed weapon information.</returns>

        public static string HandleWeaponInfo(Pawn pawn)
        {
            var report = new StringBuilder();
            report.AppendLine("RICS.MPCH.WeaponHeader".Translate());

            var primary = pawn.equipment?.Primary;

            // === Simple Sidearms Integration ===
            bool hasSimpleSidearms = ModLister.GetActiveModWithIdentifier("PeteTimesSix.SimpleSidearms") != null;
            List<Thing> sidearms = null;

            if (hasSimpleSidearms)
            {
                try
                {
                    sidearms = GetSidearmsViaReflection(pawn);
                }
                catch (Exception ex)
                {
                    Logger.Warning($"Failed to get SimpleSidearms data for weapon info: {ex.Message}");
                }
            }

            // Collect weapons to display: Primary + first melee sidearm + first ranged sidearm
            var weaponsToShow = new List<Thing>();

            if (primary != null)
                weaponsToShow.Add(primary);

            if (sidearms != null && sidearms.Count > 0)
            {
                Thing firstMelee = null;
                Thing firstRanged = null;

                foreach (var sidearm in sidearms)
                {
                    if (sidearm == null) continue;

                    if (firstMelee == null && sidearm.def.IsMeleeWeapon)
                        firstMelee = sidearm;
                    else if (firstRanged == null && sidearm.def.IsRangedWeapon)
                        firstRanged = sidearm;

                    if (firstMelee != null && firstRanged != null)
                        break;
                }

                if (firstMelee != null)
                    weaponsToShow.Add(firstMelee);
                if (firstRanged != null)
                    weaponsToShow.Add(firstRanged);
            }

            if (weaponsToShow.Count == 0)
            {
                report.AppendLine("RICS.MPCH.NoWeaponEquipped".Translate());
                return report.ToString();
            }

            // Display each weapon with material
            foreach (var weapon in weaponsToShow)
            {
                string name = MyPawnCommandHandler.StripTags(weapon.LabelCap);
                string prefix = weapon == primary ? "• " : "  ↳ ";

                report.AppendLine($"{prefix}{name}");

                // Material display -- Material shows already in the label. No need to repeat and clutter the output.
                //if (weapon.Stuff != null)
                //{
                //    string materialName = MyPawnCommandHandler.StripTags(weapon.Stuff.LabelCap);
                //    report.AppendLine("  " + "RICS.MPCH.WeaponMaterial".Translate(materialName));
                //}
                //else if (weapon.def.MadeFromStuff)
                //{
                //    report.AppendLine("  " + "RICS.MPCH.WeaponMaterialUnknown".Translate());
                //}

                // Only show detailed stats/traits for the primary weapon
                if (weapon == primary)
                {
                    string stats = CommandHandlerPriceCheck.GetWeaponDamageSummary(weapon);
                    if (!string.IsNullOrEmpty(stats))
                    {
                        report.AppendLine("  " + stats);
                    }

                    var uniqueTraits = GetUniqueWeaponTraits(weapon);
                    if (uniqueTraits.Count > 0)
                    {
                        report.AppendLine("  " + "RICS.MPCH.WeaponTraits".Translate());
                        report.AppendLine("    " + string.Join(", ", uniqueTraits));
                    }
                }
            }

            return report.ToString();
        }

        private static List<string> GetUniqueWeaponTraits(Thing weapon)
        {
            var traits = new List<string>();
            if (weapon == null) return traits;

            // 1. Odyssey Unique Weapons + Mods using CompUniqueWeapon
            var uniqueComp = weapon.TryGetComp<CompUniqueWeapon>();
            if (uniqueComp != null)
            {
                var traitList = uniqueComp.TraitsListForReading;
                if (traitList != null)
                {
                    foreach (var trait in traitList)
                    {
                        if (trait != null)
                            traits.Add(trait.LabelCap);
                    }
                }
            }

            // 2. Persona / Bladelink Weapons (Royalty + mods)
            var bladelinkComp = weapon.TryGetComp<CompBladelinkWeapon>();
            if (bladelinkComp != null)
            {
                var traitList = bladelinkComp.TraitsListForReading;
                if (traitList != null)
                {
                    foreach (var trait in traitList)
                    {
                        if (trait != null && !traits.Contains(trait.LabelCap)) // avoid duplicates
                            traits.Add(trait.LabelCap);
                    }
                }
            }

            return traits;
        }

        // === Kills ===
        /// <summary>
        /// Provides a summary of the pawn's combat history based on their kill records, including:
        /// </summary>
        /// <param name="pawn">The pawn whose kill information is being retrieved.</param>
        /// <param name="args">Additional arguments for the command (not used).</param>
        /// <returns>A string containing the pawn's combat history summary.</returns>
        public static string HandleKillInfo(Pawn pawn, string[] args)
        {
            var report = new StringBuilder();
            report.AppendLine("RICS.MPCH.KillHeader".Translate());

            int humanlikeKills = (int)pawn.records.GetValue(RecordDefOf.KillsHumanlikes);
            int animalKills = (int)pawn.records.GetValue(RecordDefOf.KillsAnimals);
            int mechanoidKills = (int)pawn.records.GetValue(RecordDefOf.KillsMechanoids);
            int totalKills = humanlikeKills + animalKills + mechanoidKills;

            report.AppendLine("RICS.MPCH.TotalKills".Translate(totalKills));

            if (humanlikeKills > 0)
                report.AppendLine("RICS.MPCH.KillsHumans".Translate(humanlikeKills));
            if (animalKills > 0)
                report.AppendLine("RICS.MPCH.KillsAnimals".Translate(animalKills));
            if (mechanoidKills > 0)
                report.AppendLine("RICS.MPCH.KillsMechanoids".Translate(mechanoidKills));

            if (totalKills > 0)
            {
                int damageDealt = (int)pawn.records.GetValue(RecordDefOf.DamageDealt);
                if (damageDealt > 0)
                    report.AppendLine("RICS.MPCH.DamageDealt".Translate(damageDealt));

                if (totalKills >= 100)
                    report.AppendLine("RICS.MPCH.LegendarySlayer".Translate());
                else if (totalKills >= 50)
                    report.AppendLine("RICS.MPCH.VeteranWarrior".Translate());
                else if (totalKills >= 10)
                    report.AppendLine("RICS.MPCH.ExperiencedFighter".Translate());
                else if (totalKills > 0)
                    report.AppendLine("RICS.MPCH.GettingStarted".Translate());
            }
            else
            {
                report.AppendLine("RICS.MPCH.NoKills".Translate());
            }

            return report.ToString();
        }
    }
}
