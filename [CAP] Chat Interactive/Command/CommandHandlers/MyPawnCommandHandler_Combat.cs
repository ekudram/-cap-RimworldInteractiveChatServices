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

namespace CAP_ChatInteractive.Commands.CommandHandlers
{
    public static class MyPawnCommandHandler_Combat
    {
        // === Gear ===
        // === Gear ===
        public static string HandleGearInfo(Pawn pawn, string[] args)
        {
            var report = new StringBuilder();
            report.AppendLine("RICS.MPCH.GearHeader".Translate());

            // Weapons - check for Simple Sidearms first
            var weapons = GetWeaponsList(pawn);
            if (weapons.Count > 0)
            {
                report.AppendLine("RICS.MPCH.WeaponsHeader".Translate());
                report.AppendLine(string.Join(", ", weapons));
            }

            // Apparel - list everything worn
            var apparel = pawn.apparel?.WornApparel;
            if (apparel != null && apparel.Count > 0)
            {
                report.AppendLine("RICS.MPCH.ApparelHeader".Translate());
                foreach (var item in apparel)
                {
                    string baseName = MyPawnCommandHandler.StripTags(item.def.LabelCap);
                    string quality = item.TryGetQuality(out QualityCategory qc) ? $" ({qc})" : "";
                    string hitPoints = item.HitPoints != item.MaxHitPoints ?
                        $" {((float)item.HitPoints / item.MaxHitPoints).ToStringPercent()}" : "";
                    report.AppendLine($"  • {baseName}{quality}{hitPoints}");
                }
            }

            // Inventory items - show all notable items (fixed duplicate medicine reporting)
            // Vanilla innerContainer holds unique Thing stacks; we now use a single-pass filter
            // to avoid double-matching via IsMedicine + IsIngestible on the same stack.
            var inventory = pawn.inventory?.innerContainer;
            if (inventory != null && inventory.Count > 0)
            {
                // Distinct by reference (each stack is a unique Thing) + clear non-overlapping conditions
                var notableItems = inventory.Where(item =>
                    item != null && item.def != null && item.stackCount > 0 &&
                    (item.def.IsMedicine ||
                     item.def.IsDrug ||
                     (item.def.IsIngestible && !item.def.IsMedicine && !item.def.IsDrug)))
                    .ToList();  // materialize once for safety

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

            // Armor stats from RimWorld (no complex calculations)
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
                        weapons.AddRange(sidearms.Select(weapon => MyPawnCommandHandler.StripTags(weapon.LabelCap)));
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
                weapons.AddRange(equipment.Select(e => MyPawnCommandHandler.StripTags(e.LabelCap)));
            }

            return weapons;
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
            try
            {
                float sharpArmor = CalculateArmorRating(pawn, StatDefOf.ArmorRating_Sharp);
                float bluntArmor = CalculateArmorRating(pawn, StatDefOf.ArmorRating_Blunt);
                float heatArmor = CalculateArmorRating(pawn, StatDefOf.ArmorRating_Heat);

                var armorStats = new List<string>();

                if (sharpArmor >= 0.01f)
                    armorStats.Add($"🗡️{sharpArmor.ToStringPercent()}");

                if (bluntArmor >= 0.01f)
                    armorStats.Add($"🔨{bluntArmor.ToStringPercent()}");

                if (heatArmor >= 0.01f)
                    armorStats.Add($"🔥{heatArmor.ToStringPercent()}");

                if (armorStats.Count > 0)
                {
                    return "RICS.MPCH.ArmorHeader".Translate() + $" {string.Join(" ", armorStats)}\n";
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error calculating armor: {ex}");
            }
            return "RICS.MPCH.ArmorNone".Translate() + "\n";
        }

        private static float CalculateArmorRating(Pawn pawn, StatDef stat)
        {
            if (pawn.apparel?.WornApparel == null || !pawn.apparel.WornApparel.Any())
                return 0f;

            var rating = 0f;
            float baseValue = Mathf.Clamp01(pawn.GetStatValue(stat) / 2f);
            var parts = pawn.RaceProps.body.AllParts;
            var apparel = pawn.apparel.WornApparel;

            foreach (var part in parts)
            {
                float cache = 1f - baseValue;

                if (apparel != null && apparel.Any())
                {
                    cache = apparel.Where(a => a.def.apparel?.CoversBodyPart(part) ?? false)
                       .Select(a => Mathf.Clamp01(a.GetStatValue(stat) / 2f))
                       .Aggregate(cache, (current, v) => current * (1f - v));
                }

                rating += part.coverageAbs * (1f - cache);
            }

            return Mathf.Clamp(rating * 2f, 0f, 2f);
        }

        // === Kills ===
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
