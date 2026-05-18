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

        // In MyPawnCommandHandler_Combat.cs, replace the entire HandleWeaponInfo() with this improved version:
        // In MyPawnCommandHandler_Combat.cs, inside MyPawnCommandHandler_Combat.HandleWeaponInfo()

        public static string HandleWeaponInfo(Pawn pawn)
        {
            var report = new StringBuilder();
            report.AppendLine("RICS.MPCH.WeaponHeader".Translate());

            var weapon = pawn.equipment?.Primary;
            if (weapon == null)
            {
                report.AppendLine("RICS.MPCH.NoWeaponEquipped".Translate());
                return report.ToString();
            }

            string name = MyPawnCommandHandler.StripTags(weapon.LabelCap);
            report.AppendLine($"• {name}");

            // Core stats (damage / DPS / AP)
            string stats = CommandHandlerPriceCheck.GetWeaponDamageSummary(weapon);
            if (!string.IsNullOrEmpty(stats))
            {
                report.AppendLine(stats);
            }

            // Unique + Persona traits
            var uniqueTraits = GetUniqueWeaponTraits(weapon);
            if (uniqueTraits.Count > 0)
            {
                report.AppendLine("RICS.MPCH.WeaponTraits".Translate());
                report.AppendLine(string.Join(", ", uniqueTraits));
            }

            return report.ToString();
        }

        // In MyPawnCommandHandler_Combat.cs
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
