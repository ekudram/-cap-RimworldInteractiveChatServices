// MyPawnCommandHandler.cs
// Copyright (c) Captolamia
// This file is part of CAP Chat Interactive.
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
//
// Handles the !mypawn command and its subcommands to provide detailed information about the viewer's assigned pawn.


using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using Verse;

namespace CAP_ChatInteractive.Commands.CommandHandlers
{
    public static class MyPawnCommandHandler
    {
        public static string HandleMyPawnCommand(ChatMessageWrapper messageWrapper, string subCommand, string[] args)
        {
            try
            {
                // Get the viewer and their pawn
                var viewer = Viewers.GetViewer(messageWrapper);
                if (viewer == null)
                {
                    return "Could not find your viewer data.";
                }

                var assignmentManager = CAPChatInteractiveMod.GetPawnAssignmentManager();

                // Check if viewer already has a pawn assigned using the new manager
                if (assignmentManager != null)
                {
                    Pawn existingPawn = assignmentManager.GetAssignedPawn(messageWrapper);
                    if (existingPawn == null)
                    {
                        return "You don't have an active pawn in the colony. Use !pawn to purchase one!";
                    }
                }

                var pawn = assignmentManager.GetAssignedPawn(messageWrapper);

                // Route to appropriate handler based on subcommand
                switch (subCommand)
                {
                    case "body":
                        return HandleBodyInfo(pawn, args);
                    case "health":
                        return HandlehealthInfo(pawn, args);
                    case "gear":
                        return HandleGearInfo(pawn, args);
                    case "kills":
                    case "killcount":
                        return HandleKillInfo(pawn, args);
                    case "needs":
                        return HandleNeedsInfo(pawn, args);
                    case "relations":
                        return HandleRelationsInfo(pawn, viewer, args);
                    case "skills":
                        return HandleSkillsInfo(pawn, args);
                    case "stats":
                        return HandleStatsInfo(pawn, args);
                    case "story":
                        return HandleBackstoriesInfo(pawn, args);
                    case "traits":
                        return HandleTraitsInfo(pawn, args);
                    case "work":
                        return HandleWorkInfo(pawn, args);
                    default:
                        return $"Unknown subcommand: {subCommand}. Use !mypawn for available options.";
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error in MyPawn command handler: {ex}");
                return "An error occurred while processing your pawn information.";
            }
        }
        // === health ===
        // Viewer requested feature
        private static string HandlehealthInfo(Pawn pawn, string[] args)
        {
            var report = new StringBuilder();
            report.AppendLine("❤️ Health:");

            try
            {
                // Core health capacities - only the most important ones
                var capacities = new[]
                {
            PawnCapacityDefOf.Consciousness,
            PawnCapacityDefOf.Sight,
            PawnCapacityDefOf.Hearing,
            PawnCapacityDefOf.Moving,
            PawnCapacityDefOf.Manipulation,
            PawnCapacityDefOf.Talking,
            PawnCapacityDefOf.Breathing,
            PawnCapacityDefOf.BloodFiltration,
            PawnCapacityDefOf.BloodPumping,
        };

                foreach (var capacity in capacities)
                {
                    if (capacity == null) continue;

                    var capacityValue = pawn.health.capacities.GetLevel(capacity);
                    // string status = GetCapacityStatus(capacityValue);
                    // string emoji = GetCapacityEmoji(capacityValue);
                    report.AppendLine($"• {capacity.LabelCap}:  ({capacityValue.ToStringPercent()})");
                    //report.AppendLine($"• {capacity.LabelCap}: {emoji} {status} ({capacityValue.ToStringPercent()})");
                    // alt
                    // report.AppendLine($"• {capacity.LabelCap}: {emoji} ({capacityValue.ToStringPercent()})");
                }

                // Add pain
                float pain = pawn.health.hediffSet.PainTotal;
                string painStatus = GetPainStatus(pain);
                string painEmoji = GetPainEmoji(pain);
                report.AppendLine($"• Pain: {painEmoji} {painStatus} ({pain.ToStringPercent()})");

            }
            catch (Exception ex)
            {
                Logger.Error($"Error in health info: {ex}");
                return "Error retrieving health information.";
            }

            return report.ToString();
        }

        private static string GetCapacityStatus(float level)
        {
            return level switch
            {
                >= 0.95f => "Perfect",
                >= 0.85f => "Excellent",
                >= 0.70f => "Good",
                >= 0.50f => "Impaired",
                >= 0.30f => "Poor",
                >= 0.10f => "Very Poor",
                > 0f => "Critical",
                _ => "None"
            };
        }

        private static string GetCapacityEmoji(float level)
        {
            return level switch
            {
                >= 0.85f => "🟢",
                >= 0.60f => "🟡",
                >= 0.30f => "🟠",
                > 0f => "🔴",
                _ => "⚫"
            };
        }

        private static string GetPainStatus(float painLevel)
        {
            return painLevel switch
            {
                >= 0.80f => "Extreme",
                >= 0.60f => "Severe",
                >= 0.40f => "Moderate",
                >= 0.20f => "Minor",
                >= 0.05f => "Negligible",
                _ => "None"
            };
        }

        private static string GetPainEmoji(float painLevel)
        {
            return painLevel switch
            {
                >= 0.60f => "😫",
                >= 0.40f => "😣",
                >= 0.20f => "😐",
                >= 0.05f => "🙂",
                _ => "😊"
            };
        }
        // === Body ===
        private static string HandleBodyInfo(Pawn pawn, string[] args)
        {
            if (pawn.health?.hediffSet?.hediffs == null || pawn.health.hediffSet.hediffs.Count == 0)
            {
                return $"{pawn.Name} has no health conditions. 🟢";
            }

            var report = new StringBuilder();
            report.AppendLine($"🏥 Health Report: "); // for {pawn.Name}:

            // Add temperature comfort range
            float minComfy = pawn.GetStatValue(StatDefOf.ComfyTemperatureMin);
            float maxComfy = pawn.GetStatValue(StatDefOf.ComfyTemperatureMax);
            report.AppendLine($"🌡️ Comfort Range: {minComfy.ToStringTemperature()} ~ {maxComfy.ToStringTemperature()}");

            // Get visible health conditions grouped by body part
            var healthConditions = GetVisibleHealthConditions(pawn);

            int maxConditions = 8;
            var limitedConditions = healthConditions.Take(maxConditions).ToList();
            bool hasMore = healthConditions.Count > maxConditions;

            if (healthConditions.Count == 0)
            {
                report.AppendLine("No visible health issues. ✅");
                return report.ToString();
            }

            report.AppendLine("Health Conditions:");

            foreach (var partGroup in limitedConditions)
            {
                string partName = partGroup.Key?.LabelCap ?? "Whole Body";
                report.Append($"• {partName}: ");

                var conditions = new List<string>();

                foreach (var hediff in partGroup)
                {
                    string condition = GetHediffDisplay(hediff);
                    if (!string.IsNullOrEmpty(condition))
                    {
                        conditions.Add(condition);
                    }
                }

                if (conditions.Count > 0)
                {
                    report.Append(string.Join(", ", conditions));
                    report.AppendLine();
                }
            }

            if (hasMore)
            {
                report.AppendLine($"... and {healthConditions.Count - maxConditions} more conditions");
            }

            // Add summary
            int totalConditions = healthConditions.Sum(g => g.Count());
            string severity = GetOverallHealthSeverity(pawn);
            report.AppendLine($"📊 Summary: {totalConditions} condition(s) - {severity}");

            return report.ToString();
        }

        private static List<IGrouping<BodyPartRecord, Hediff>> GetVisibleHealthConditions(Pawn pawn)
        {
            var visibleHediffs = pawn.health.hediffSet.hediffs
                .Where(h => h.Visible)
                .ToList();

            // Include missing parts
            var missingParts = pawn.health.hediffSet.GetMissingPartsCommonAncestors();
            visibleHediffs.AddRange(missingParts);

            return visibleHediffs
                .GroupBy(h => h.Part)
                .OrderByDescending(g => g.Key?.height ?? 0f)
                .ThenByDescending(g => g.Key?.coverageAbsWithChildren ?? 0f)
                .ToList();
        }

        private static string GetHediffDisplay(Hediff hediff)
        {
            if (hediff == null) return string.Empty;

            string display = System.Text.RegularExpressions.Regex.Replace(hediff.LabelCap, @"<[^>]*>", "");

            // Add emoji indicators
            if (hediff is Hediff_MissingPart)
            {
                return $"🦵 {display}"; // Missing limb
            }

            if (hediff.Bleeding)
            {
                return $"🩸 {display}"; // Bleeding
            }

            if (hediff.IsTended())
            {
                return $"🩹 {display}"; // Tended wound
            }

            // Add severity indicators
            if (hediff.Severity > 0.7f)
            {
                return $"🔴 {display}"; // Severe
            }
            else if (hediff.Severity > 0.3f)
            {
                return $"🟡 {display}"; // Moderate
            }
            else
            {
                return $"🟢 {display}"; // Mild
            }
        }

        private static string GetOverallHealthSeverity(Pawn pawn)
        {
            float healthPercent = pawn.health.summaryHealth.SummaryHealthPercent;

            if (healthPercent >= 0.9f) return "Excellent 🟢";
            if (healthPercent >= 0.7f) return "Good 🟢";
            if (healthPercent >= 0.5f) return "Fair 🟡";
            if (healthPercent >= 0.3f) return "Poor 🟠";
            return "Critical 🔴";
        }
        // === Gear ===
        private static string HandleGearInfo(Pawn pawn, string[] args)
        {
            var report = new StringBuilder();
            report.AppendLine($"🎒 Gear Report: ");// for {pawn.Name}:

            // Weapons - check for Simple Sidearms first
            var weapons = GetWeaponsList(pawn);
            if (weapons.Count > 0)
            {
                report.Append("⚔️ Weapons: ");
                report.AppendLine(string.Join(", ", weapons));
            }
            else
            {
                report.AppendLine("⚔️ Weapons: None");
            }

            // Apparel - list everything worn
            var apparel = pawn.apparel?.WornApparel;
            if (apparel != null && apparel.Count > 0)
            {
                report.AppendLine("👕 Apparel:");
                foreach (var item in apparel)
                {
                    // Get base name without quality
                    string baseName = StripTags(item.def.LabelCap);

                    // Add quality if it exists
                    string quality = item.TryGetQuality(out QualityCategory qc) ? $" ({qc})" : "";

                    // Add hit points if damaged
                    string hitPoints = item.HitPoints != item.MaxHitPoints ?
                        $" {((float)item.HitPoints / item.MaxHitPoints).ToStringPercent()}" : "";

                    report.AppendLine($"  • {baseName}{quality}{hitPoints}");
                }
            }
            else
            {
                report.AppendLine("👕 Apparel: None");
            }

            // Inventory items - show all notable items
            var inventory = pawn.inventory?.innerContainer;
            if (inventory != null && inventory.Count > 0)
            {
                var notableItems = inventory.Where(item =>
                     item.def.IsMedicine ||
                    item.def.IsDrug ||
                    item.def.IsIngestible 
                );

                if (notableItems.Any())
                {
                    report.AppendLine("🎒 Inventory:");
                    foreach (var item in notableItems)
                    {
                        string stackInfo = item.stackCount > 1 ? $" x{item.stackCount}" : "";
                        report.AppendLine($"  • {StripTags(item.LabelCap)}{stackInfo}");
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
                    // Use reflection or direct call if you have access to SimpleSidearms API
                    var sidearms = GetSidearmsViaReflection(pawn);
                    if (sidearms != null && sidearms.Count > 0)
                    {
                        weapons.AddRange(sidearms.Select(weapon => StripTags(weapon.LabelCap)));
                        return weapons;
                    }
                }
                catch (Exception ex)
                {
                    Logger.Warning($"Failed to get SimpleSidearms data: {ex.Message}");
                    // Fall through to default equipment
                }
            }

            // Fallback: standard equipment
            var equipment = pawn.equipment?.AllEquipmentListForReading;
            if (equipment != null && equipment.Count > 0)
            {
                weapons.AddRange(equipment.Select(e => StripTags(e.LabelCap)));
            }

            return weapons;
        }

        private static List<Thing> GetSidearmsViaReflection(Pawn pawn)
        {
            try
            {
                // Simple Sidearms integration via reflection
                var sidearmsComp = pawn.TryGetComp<CompEquippable>();
                if (sidearmsComp != null)
                {
                    // Alternative: Check for Simple Sidearms comp
                    var sidearmsField = pawn.GetType().GetField("sidearms", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                    if (sidearmsField != null)
                    {
                        var sidearmsList = sidearmsField.GetValue(pawn) as List<Thing>;
                        return sidearmsList ?? new List<Thing>();
                    }
                }

                // Try the static method approach that the old code used
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
            catch (Exception ex)
            {
                Logger.Warning($"Reflection failed for SimpleSidearms: {ex.Message}");
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

                Logger.Debug($"Calculated armor: Sharp={sharpArmor:P0}, Blunt={bluntArmor:P0}, Heat={heatArmor:P0}");

                var armorStats = new List<string>();

                if (sharpArmor >= 0.01f)
                    armorStats.Add($"🗡️{sharpArmor.ToStringPercent()}");

                if (bluntArmor >= 0.01f)
                    armorStats.Add($"🔨{bluntArmor.ToStringPercent()}");

                if (heatArmor >= 0.01f)
                    armorStats.Add($"🔥{heatArmor.ToStringPercent()}");

                if (armorStats.Count > 0)
                {
                    return $"🛡️ Armor: {string.Join(" ", armorStats)}\n";
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error calculating armor: {ex}");
            }

            return "🛡️ Armor: None\n";
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

        private static string StripTags(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            return System.Text.RegularExpressions.Regex.Replace(text, @"<[^>]*>", "");
        }

        private static string HandleKillInfo(Pawn pawn, string[] args)
        {
            var report = new StringBuilder();
            report.AppendLine($"💀 Kill Report:"); //  for {pawn.Name}:

            // Get kill counts from pawn records - cast float to int
            int humanlikeKills = (int)pawn.records.GetValue(RecordDefOf.KillsHumanlikes);
            int animalKills = (int)pawn.records.GetValue(RecordDefOf.KillsAnimals);
            int mechanoidKills = (int)pawn.records.GetValue(RecordDefOf.KillsMechanoids);
            int totalKills = humanlikeKills + animalKills + mechanoidKills;

            // Total kills
            report.AppendLine($"Total Kills: {totalKills}");

            // Breakdown by type
            if (humanlikeKills > 0)
                report.AppendLine($"• Humans: {humanlikeKills}");
            if (animalKills > 0)
                report.AppendLine($"• Animals: {animalKills}");
            if (mechanoidKills > 0)
                report.AppendLine($"• Mechanoids: {mechanoidKills}");

            // Check for other kill records that might exist
            var killRecords = DefDatabase<RecordDef>.AllDefs
                .Where(r => r.defName.Contains("Kill") || r.defName.Contains("kill"))
                .Where(r => pawn.records.GetValue(r) > 0)
                .ToList();

            // Add any additional kill types found - cast float to int
            foreach (var record in killRecords)
            {
                if (record != RecordDefOf.KillsHumanlikes &&
                    record != RecordDefOf.KillsAnimals &&
                    record != RecordDefOf.KillsMechanoids)
                {
                    int count = (int)pawn.records.GetValue(record);
                    string recordName = record.LabelCap.ToLower().Replace("kills", "").Trim();
                    report.AppendLine($"• {recordName}: {count}");
                }
            }

            // Check for most recent kill if any kills exist
            if (totalKills > 0)
            {
                // Get damage dealt records - cast float to int
                int damageDealt = (int)pawn.records.GetValue(RecordDefOf.DamageDealt);
                if (damageDealt > 0)
                {
                    report.AppendLine($"Damage Dealt: {damageDealt}");
                }

                // Add some flavor text based on kill count
                if (totalKills >= 100)
                    report.AppendLine("🏆 Legendary Slayer!");
                else if (totalKills >= 50)
                    report.AppendLine("⚔️ Veteran Warrior");
                else if (totalKills >= 10)
                    report.AppendLine("🔪 Experienced Fighter");
                else if (totalKills > 0)
                    report.AppendLine("🎯 Getting Started");
            }
            else
            {
                report.AppendLine("No kills recorded yet. Go get 'em!");
            }

            return report.ToString();
        }

        private static string HandleNeedsInfo(Pawn pawn, string[] args)
        {
            var report = new StringBuilder();
            report.AppendLine($"😊 Needs Report: "); //for {pawn.Name}:

            var needs = pawn.needs?.AllNeeds;
            if (needs == null || needs.Count == 0)
            {
                return $"{pawn.Name} has no needs tracked.";
            }

            foreach (var need in needs.Where(n => n != null && n.def != null))
            {
                if (!need.ShowOnNeedList) continue;

                string needName = StripTags(need.def.LabelCap);
                string needStatus = GetNeedStatus(need);

                report.AppendLine($"• {needName}: {needStatus}");
            }

            // Add mood summary if available
            var moodNeed = pawn.needs?.mood;
            if (moodNeed != null)
            {
                string moodStatus = GetMoodStatus(moodNeed.CurLevel);
                report.AppendLine($"📊 Overall Mood: {moodStatus}");
            }

            return report.ToString();
        }

        private static string GetNeedStatus(Need need)
        {
            if (need == null) return "Unknown";

            float curLevel = need.CurLevel;
            float maxLevel = need.MaxLevel;

            // Get percentage for display
            float percent = curLevel / maxLevel;

            // Determine status and emoji based on need type and level
            string status = GetNeedLevelStatus(need.def.defName, percent, curLevel);
            string emoji = GetNeedEmoji(need.def.defName, percent);

            return $"{emoji} {status} ({curLevel.ToString("F1")}/{maxLevel.ToString("F1")})";
        }

        private static string GetNeedLevelStatus(string needDefName, float percent, float curLevel)
        {
            return percent switch
            {
                >= 0.9f => "Excellent",
                >= 0.7f => "Good",
                >= 0.5f => "Okay",
                >= 0.3f => "Low",
                >= 0.1f => "Very Low",
                _ => "Critical"
            };
        }

        private static string GetNeedEmoji(string needDefName, float percent)
        {
            // Default emoji based on level
            string levelEmoji = percent switch
            {
                >= 0.7f => "🟢",
                >= 0.4f => "🟡",
                >= 0.2f => "🟠",
                _ => "🔴"
            };

            // Specific emojis for common needs
            return needDefName.ToLower() switch
            {
                "food" or "hunger" => percent >= 0.3f ? "🍽️" : "🍴",
                "rest" => percent >= 0.3f ? "😴" : "💤",
                "joy" => percent >= 0.3f ? "😄" : "😞",
                "mood" => percent >= 0.7f ? "😊" : percent >= 0.4f ? "😐" : "😠",
                "beauty" => percent >= 0.5f ? "🎨" : "🏚️",
                "comfort" => percent >= 0.5f ? "🛋️" : "🪑",
                "outdoors" => percent >= 0.5f ? "🌳" : "🏠",
                "room" => percent >= 0.5f ? "🏠" : "⛺",
                _ => levelEmoji
            };
        }

        private static string GetMoodStatus(float moodLevel)
        {
            return moodLevel switch
            {
                >= 0.9f => "Ecstatic 😁",
                >= 0.8f => "Very Happy 😊",
                >= 0.6f => "Content 🙂",
                >= 0.4f => "Neutral 😐",
                >= 0.2f => "Stressed 😟",
                >= 0.1f => "Upset 😠",
                _ => "Breaking 😭"
            };
        }

        private static string HandleRelationsInfo(Pawn pawn, Viewer viewer, string[] args)
        {
            var report = new StringBuilder();
            report.AppendLine($"💕 Relations Report:"); // for {pawn.Name}:

            // Get the assignment manager
            var assignmentManager = Current.Game?.GetComponent<GameComponent_PawnAssignmentManager>();
            if (assignmentManager == null)
            {
                return "Relations system not available.";
            }

            // Handle specific viewer relation request
            if (args.Length > 0)
            {
                string targetViewer = args[0];
                var targetPawn = assignmentManager.GetAssignedPawn(targetViewer);
                if (targetPawn == null)
                {
                    return $"Viewer '{targetViewer}' doesn't have an active pawn.";
                }

                return GetSpecificRelationInfo(pawn, targetPawn, targetViewer, assignmentManager);
            }

            // General relations overview
            return GetRelationsOverview(pawn, assignmentManager);
        }

        private static string GetSpecificRelationInfo(Pawn pawn, Pawn targetPawn, string targetViewer, GameComponent_PawnAssignmentManager assignmentManager)
        {
            var report = new StringBuilder();
            string pawnViewer = GetViewerNameFromPawn(pawn);
            string targetPawnViewer = GetViewerNameFromPawn(targetPawn);

            report.AppendLine($"🤝 Relations between {pawnViewer} and {targetPawnViewer}:");

            // Get direct relation
            var directRelation = pawn.relations.DirectRelationExists(PawnRelationDefOf.Spouse, targetPawn) ? "Spouse 💍" :
                                pawn.relations.DirectRelationExists(PawnRelationDefOf.Lover, targetPawn) ? "Lover ❤️" :
                                pawn.relations.DirectRelationExists(PawnRelationDefOf.Fiance, targetPawn) ? "Fiancé 💑" :
                                pawn.relations.DirectRelationExists(PawnRelationDefOf.ExLover, targetPawn) ? "Ex-Lover 💔" :
                                pawn.relations.DirectRelationExists(PawnRelationDefOf.ExSpouse, targetPawn) ? "Ex-Spouse 💔" :
                                pawn.relations.DirectRelationExists(PawnRelationDefOf.Child, targetPawn) ? "Child 👶" :
                                pawn.relations.DirectRelationExists(PawnRelationDefOf.Parent, targetPawn) ? "Parent 👨‍👦" :
                                pawn.relations.DirectRelationExists(PawnRelationDefOf.Sibling, targetPawn) ? "Sibling 👫" :
                                "No direct relation";

            report.AppendLine($"• Relationship: {directRelation}");

            // Opinion
            int opinion = pawn.relations.OpinionOf(targetPawn);
            string opinionEmoji = opinion >= 50 ? "😍" : opinion >= 25 ? "😊" : opinion >= 0 ? "🙂" : opinion >= -25 ? "😐" : opinion >= -50 ? "😠" : "😡";
            report.AppendLine($"• Opinion: {opinion} {opinionEmoji}");

            // Romance-related info - basic
            if (pawn.relations.DirectRelationExists(PawnRelationDefOf.Lover, targetPawn) ||
                pawn.relations.DirectRelationExists(PawnRelationDefOf.Spouse, targetPawn))
            {
                report.AppendLine($"• Relationship: Active 💑");
            }

            return report.ToString();
        }

        private static string GetRelationsOverview(Pawn pawn, GameComponent_PawnAssignmentManager assignmentManager)
        {
            var report = new StringBuilder();

            // Family relations (always show all)
            var family = pawn.relations.RelatedPawns
                .Where(p => p.relations.DirectRelationExists(PawnRelationDefOf.Spouse, pawn) ||
                           p.relations.DirectRelationExists(PawnRelationDefOf.Lover, pawn) ||
                           p.relations.DirectRelationExists(PawnRelationDefOf.Child, pawn) ||
                           p.relations.DirectRelationExists(PawnRelationDefOf.Parent, pawn) ||
                           p.relations.DirectRelationExists(PawnRelationDefOf.Sibling, pawn))
                .ToList();

            if (family.Count > 0)
            {
                report.AppendLine("👨‍👩‍👧‍👦 Family:");
                foreach (var relative in family)
                {
                    string relation = GetFamilyRelation(pawn, relative);
                    string viewerName = GetViewerNameFromPawn(relative);
                    report.AppendLine($"  • {viewerName}: {relation}");
                }
            }

            // Viewer friends (top 5 by opinion) - EXCLUDE family members
            var viewerFriends = assignmentManager.GetAllViewerPawns()
                .Where(p => p != pawn &&
                       pawn.relations.OpinionOf(p) > 10 &&
                       !family.Contains(p)) // Exclude family members
                .OrderByDescending(p => pawn.relations.OpinionOf(p))
                .Take(5)
                .ToList();

            if (viewerFriends.Count > 0)
            {
                report.AppendLine("🎮 Viewer Friends:");
                foreach (var friend in viewerFriends)
                {
                    int opinion = pawn.relations.OpinionOf(friend);
                    string friendViewerName = GetViewerNameFromPawn(friend);
                    report.AppendLine($"  • {friendViewerName}: +{opinion} 😊");
                }
            }

            // Viewer rivals (top 5 by negative opinion) - EXCLUDE family members
            var viewerRivals = assignmentManager.GetAllViewerPawns()
                .Where(p => p != pawn &&
                       pawn.relations.OpinionOf(p) < -10 &&
                       !family.Contains(p)) // Exclude family members
                .OrderBy(p => pawn.relations.OpinionOf(p))
                .Take(5)
                .ToList();

            if (viewerRivals.Count > 0)
            {
                report.AppendLine("⚔️ Viewer Rivals:");
                foreach (var rival in viewerRivals)
                {
                    int opinion = pawn.relations.OpinionOf(rival);
                    string rivalViewerName = GetViewerNameFromPawn(rival);
                    report.AppendLine($"  • {rivalViewerName}: {opinion} 😠");
                }
            }

            // Overall social summary - EXCLUDE family members from counts
            int totalFriends = assignmentManager.GetAllViewerPawns()
                .Count(p => p != pawn && pawn.relations.OpinionOf(p) > 10 && !family.Contains(p));
            int totalRivals = assignmentManager.GetAllViewerPawns()
                .Count(p => p != pawn && pawn.relations.OpinionOf(p) < -10 && !family.Contains(p));

            report.AppendLine($"📊 Social Summary: {family.Count} family, {totalFriends} viewer friends, {totalRivals} viewer rivals");

            if (family.Count == 0 && viewerFriends.Count == 0 && viewerRivals.Count == 0)
            {
                report.AppendLine("No significant relationships found.");
            }

            return report.ToString();
        }

        private static string GetFamilyRelation(Pawn pawn, Pawn relative)
        {
            if (pawn.relations.DirectRelationExists(PawnRelationDefOf.Spouse, relative)) return "Spouse 💍";
            if (pawn.relations.DirectRelationExists(PawnRelationDefOf.Lover, relative)) return "Lover ❤️";
            if (pawn.relations.DirectRelationExists(PawnRelationDefOf.Fiance, relative)) return "Fiancé 💑";
            if (pawn.relations.DirectRelationExists(PawnRelationDefOf.Child, relative)) return "Child 👶";
            if (pawn.relations.DirectRelationExists(PawnRelationDefOf.Parent, relative)) return "Parent 👨‍👦";
            if (pawn.relations.DirectRelationExists(PawnRelationDefOf.Sibling, relative)) return "Sibling 👫";
            if (pawn.relations.DirectRelationExists(PawnRelationDefOf.ExSpouse, relative)) return "Ex-Spouse 💔";
            if (pawn.relations.DirectRelationExists(PawnRelationDefOf.ExLover, relative)) return "Ex-Lover 💔";

            // Check for indirect relations if no direct relation found
            if (pawn.relations.FamilyByBlood.Contains(relative)) return "Blood Relative 👨‍👩‍👧‍👦";
            if (pawn.GetRelations(relative).Any()) return "Relative";

            return "Relation"; // Fallback
        }

        private static string GetViewerNameFromPawn(Pawn pawn)
        {
            if (pawn?.Name is NameTriple nameTriple)
            {
                // Prefer the Nick (username) if it's not empty
                if (!string.IsNullOrEmpty(nameTriple.Nick))
                    return nameTriple.Nick;

                // Fallback to first name if Nick is empty
                return nameTriple.First;
            }
            return pawn?.Name?.ToString() ?? "Unknown";
        }

        private static string GetDisplayNameForRelations(Pawn pawn, GameComponent_PawnAssignmentManager assignmentManager = null)
        {
            if (pawn?.Name is NameTriple nameTriple)
            {
                // If this is a viewer pawn, always use the Nick (username)
                if (assignmentManager?.IsViewerPawn(pawn) == true)
                    return nameTriple.Nick;

                // For non-viewer pawns, use First name (more natural for family relationships)
                return nameTriple.First;
            }
            return pawn?.Name?.ToString() ?? "Unknown";
        }

        private static string HandleSkillsInfo(Pawn pawn, string[] args)
        {
            var report = new StringBuilder();
            report.AppendLine($"🎯 Skills Report: "); // for {pawn.Name}:

            var skills = pawn.skills?.skills;
            if (skills == null || skills.Count == 0)
            {
                return $"{pawn.Name} has no skills tracked.";
            }

            // Group skills by their display order (matching RimWorld's UI)
            var orderedSkills = skills
                .OrderBy(s => s.def.listOrder)
                .ToList();

            foreach (var skill in orderedSkills)
            {
                if (skill == null || skill.def == null) continue;

                string skillName = StripTags(skill.def.LabelCap);
                string passionEmoji = GetPassionEmoji(skill.passion);
                // Remove extra emote
                // string levelDescription = GetSkillLevelDescriptionDetailed(skill.Level);
                // report.AppendLine($"• {passionEmoji}{skillName}: {skill.Level} {levelDescription}");
                report.AppendLine($"• {passionEmoji}{skillName}: {skill.Level} ");
            }

            //// Add learning summary
            //var burningPassions = skills.Count(s => s.passion == Passion.Major);
            //var minorPassions = skills.Count(s => s.passion == Passion.Minor);

            //if (burningPassions > 0 || minorPassions > 0)
            //{
            //    report.AppendLine($"📚 Passions: {burningPassions} 🔥🔥, {minorPassions} 🔥");
            //}

            // Top 3 skills
            var topSkills = skills.OrderByDescending(s => s.Level).Take(3);
            if (topSkills.Any(s => s.Level >= 10))
            {
                report.Append("🏆 Top Skills: ");
                var topSkillNames = topSkills.Select(s => $"{StripTags(s.def.LabelCap)} ({s.Level})");
                report.AppendLine(string.Join(", ", topSkillNames));
            }

            return report.ToString();
        }

        private static string GetPassionEmoji(Passion passion)
        {
            return passion switch
            {
                Passion.Major => "🔥🔥", // Burning passion
                Passion.Minor => "🔥",   // Minor passion  
                _ => ""                // No passion
            };
        }

        private static string GetSkillLevelDescriptionDetailed(int level)
        {
            //if (level >= 20) return "Legendary 🌟";
            //if (level >= 18) return "Master 🎯";
            //if (level >= 16) return "Expert 💪";
            //if (level >= 14) return "Proficient ✨";
            //if (level >= 12) return "Skilled 👍";
            //if (level >= 10) return "Adept 👌";
            //if (level >= 8) return "Competent ✅";
            //if (level >= 6) return "Experienced 📚";
            //if (level >= 4) return "Novice 👶";
            //if (level >= 2) return "Beginner 🌱";
            //if (level >= 1) return "Awkward 🐣";
            //return "Ignorant ❓";

            if (level >= 20) return "🌟";
            if (level >= 18) return "🎯";
            if (level >= 16) return "💪";
            if (level >= 14) return "✨";
            if (level >= 12) return "👍";
            if (level >= 10) return "👌";
            if (level >= 8) return "✅";
            if (level >= 6) return "📚";
            if (level >= 4) return "👶";
            if (level >= 2) return "🌱";
            if (level >= 1) return "🐣";
            return "";
        }

        private static string HandleStatsInfo(Pawn pawn, string[] args)
        {
            if (args.Length == 0)
            {
                return GetStatsOverview(pawn);
            }

            return GetSpecificStats(pawn, args);
        }

        private static string GetStatsOverview(Pawn pawn)
        {
            var report = new StringBuilder();
            report.AppendLine($"📊 Stats Overview:");  // for { pawn.Name}:

            // Show a few key stats as examples
            var keyStats = new[]
            {
                StatDefOf.MoveSpeed,
                StatDefOf.ShootingAccuracyPawn,
                StatDefOf.MeleeHitChance,
                StatDefOf.MeleeDPS,
                StatDefOf.WorkSpeedGlobal,
                StatDefOf.MedicalTendQuality,
                StatDefOf.SocialImpact,
                StatDefOf.TradePriceImprovement
            };

            foreach (var statDef in keyStats)
            {
                if (statDef == null) continue;

                float value = pawn.GetStatValue(statDef);
                string formattedValue = FormatStatValue(statDef, value);

                report.AppendLine($"• {StripTags(statDef.LabelCap)}: {formattedValue}");
            }

            report.AppendLine();
            report.AppendLine("💡 Usage: !mypawn stats <stat1> <stat2> ...");
            report.AppendLine("Examples: !mypawn stats movespeed shootingaccuracy meleedps");
            // reduced for brevity
            //report.AppendLine("Available: movespeed, shootingaccuracy, meleehitchance, meleedps, workspeed, medicaltend, socialimpact, tradeprice, etc.");

            return report.ToString();
        }

        private static string GetSpecificStats(Pawn pawn, string[] args)
        {
            var report = new StringBuilder();
            report.AppendLine($"📊 Stats: "); // for {pawn.Name}:

            var foundStats = new List<string>();
            var notFoundStats = new List<string>();

            foreach (var statName in args)
            {
                var statDef = FindStatDef(statName);
                if (statDef != null)
                {
                    float value = pawn.GetStatValue(statDef);
                    string formattedValue = FormatStatValue(statDef, value);
                    string description = StripTags(statDef.description) ?? "";

                    // Truncate long descriptions
                    if (description.Length > 80)
                    {
                        description = description.Substring(0, 77) + "...";
                    }

                    report.AppendLine($"• {StripTags(statDef.LabelCap)}: {formattedValue}");
                    if (!string.IsNullOrEmpty(description))
                    {
                        report.AppendLine($"  {description}");
                    }

                    foundStats.Add(statName);
                }
                else
                {
                    notFoundStats.Add(statName);
                }
            }

            // Add not found stats at the end
            if (notFoundStats.Count > 0)
            {
                report.AppendLine();
                report.AppendLine($"❌ Unknown stats: {string.Join(", ", notFoundStats)}");
                report.AppendLine("Use !mypawn stats to see available stats.");
            }

            return report.ToString();
        }

        private static StatDef FindStatDef(string statName)
        {
            string searchName = statName.ToLower().Replace("_", "").Replace(" ", "");

            return DefDatabase<StatDef>.AllDefs
                .FirstOrDefault(stat =>
                    stat.defName.ToLower().Replace("_", "").Contains(searchName) ||
                    stat.label.ToLower().Replace(" ", "").Contains(searchName));
        }

        private static string FormatStatValue(StatDef statDef, float value)
        {
            if (statDef == null) return "N/A";

            try
            {
                // Let RimWorld handle the formatting - it knows best how to display each stat
                return statDef.ValueToString(value, statDef.toStringNumberSense);
            }
            catch (Exception ex)
            {
                Logger.Warning($"Error formatting stat {statDef.defName}: {ex.Message}");
                return value.ToString("F1"); // Simple fallback
            }
        }

        private static string HandleBackstoriesInfo(Pawn pawn, string[] args)
        {
            var report = new StringBuilder();
            report.AppendLine($"👤 Backstories:");  // for {pawn.Name}:

            // Childhood backstory - truncated to fit in one message
            if (pawn.story?.Childhood != null)
            {
                var childhoodDesc = StripTags(pawn.story.Childhood.FullDescriptionFor(pawn));
                // var truncatedChildhood = TruncateDescription(childhoodDesc, 183); // Limit to 188 chars

                report.AppendLine($"🎒 Childhood: {StripTags(pawn.story.Childhood.title)}");
                // report.AppendLine($"   {truncatedChildhood}");
            }
            else
            {
                report.AppendLine("🎒 Childhood: No childhood backstory");
            }

            report.AppendLine(); // Spacing

            // Adulthood backstory - truncated to fit in one message
            if (pawn.story?.Adulthood != null)
            {
                var adulthoodDesc = StripTags(pawn.story.Adulthood.FullDescriptionFor(pawn));
                // var truncatedAdulthood = TruncateDescription(adulthoodDesc, 183); // Limit to 183 chars

                report.AppendLine($"🧑 Adulthood: {StripTags(pawn.story.Adulthood.title)}");
                // report.AppendLine($"   {truncatedAdulthood}");
            }
            else if (pawn.ageTracker.AgeBiologicalYears >= 18)
            {
                report.AppendLine("🧑 Adulthood: No adulthood backstory");
            }
            else
            {
                report.AppendLine("🧑 Adulthood: Too young for adulthood backstory");
            }

            return report.ToString();
        }

        private static string HandleTraitsInfo(Pawn pawn, string[] args)
        {
            var report = new StringBuilder();
            report.AppendLine($"🎭 Traits:"); // for { pawn.Name}

            if (pawn.story?.traits == null || pawn.story.traits.allTraits.Count == 0)
            {
                return $"{pawn.Name} has no traits.";
            }

            foreach (var trait in pawn.story.traits.allTraits)
            {
                if (trait == null) continue;

                string traitName = StripTags(trait.LabelCap);
                string traitDesc = StripTags(trait.def.description);

                report.AppendLine($"• {traitName}");
                report.AppendLine($"  {traitDesc}");

                // Add spacing between traits
                if (trait != pawn.story.traits.allTraits.Last())
                {
                    report.AppendLine();
                }
            }

            return report.ToString();
        }

        // === Work ===
        private static string HandleWorkInfo(Pawn pawn, string[] args)
        {
            // Check if pawn can work
            if (pawn.workSettings == null || !pawn.workSettings.EverWork)
            {
                return $"{pawn.Name} is not capable of work.";
            }

            // Ensure work settings are initialized using RimWorld's proper method
            if (!pawn.workSettings.Initialized)
            {
                pawn.workSettings.EnableAndInitialize();
            }

            // If no args, show top 5 highest priority work types
            if (args.Length == 0)
            {
                return GetTopWorkPriorities(pawn);
            }

            // Handle individual work type lookups and changes
            return HandleIndividualWorkCommands(pawn, args);
        }

        private static string GetTopWorkPriorities(Pawn pawn)
        {
            var report = new StringBuilder();
            report.AppendLine("💼 Top Work Priorities:");

            // Get work types with priority 1 (highest)
            var topPriorityWork = WorkTypeDefsUtility.WorkTypeDefsInPriorityOrder
                .Where(w => !pawn.WorkTypeIsDisabled(w) && pawn.workSettings.GetPriority(w) == 1)
                .Take(5)
                .ToList();

            if (topPriorityWork.Count > 0)
            {
                report.AppendLine("🔥 Highest (1):");
                foreach (var workType in topPriorityWork)
                {
                    string label = StripTags(workType.pawnLabel);
                    report.AppendLine($"  • {label}");
                }
            }
            else
            {
                report.AppendLine("No work set to highest priority");
            }

            report.AppendLine();
            report.AppendLine("💡 Usage: !mypawn work <worktype> [1-4]");
            // report.AppendLine("Examples: !mypawn work doctor | !mypawn work firefight 1 | !mypawn work growing 2 cleaning 1");

            return report.ToString();
        }

        private static string HandleIndividualWorkCommands(Pawn pawn, string[] args)
        {
            var results = new List<string>();

            // Process args in pairs (worktype, priority) or single (worktype lookup)
            for (int i = 0; i < args.Length; i++)
            {
                string workTypeName = args[i];

                // Find the work type
                var workType = FindWorkType(workTypeName);
                if (workType == null)
                {
                    results.Add($"❌ Unknown work: {workTypeName}");
                    continue;
                }

                // Check if work type is disabled for this pawn
                if (pawn.WorkTypeIsDisabled(workType))
                {
                    results.Add($"❌ {workType.label} disabled");
                    continue;
                }

                // If next arg exists and is a number 1-4, set priority
                if (i + 1 < args.Length && int.TryParse(args[i + 1], out int newPriority) && newPriority >= 0 && newPriority <= 4)
                {
                    int oldPriority = pawn.workSettings.GetPriority(workType);
                    pawn.workSettings.SetPriority(workType, newPriority);

                    string priorityName = GetPriorityName(newPriority);
                    results.Add($"✅ {workType.label}: {oldPriority}→{newPriority} ({priorityName})");
                    i++; // Skip the priority arg since we used it
                }
                else
                {
                    // Just show current priority
                    int currentPriority = pawn.workSettings.GetPriority(workType);
                    string priorityName = GetPriorityName(currentPriority);
                    results.Add($"📋 {workType.label}: {currentPriority} ({priorityName})");
                }
            }

            return string.Join(" | ", results);
        }

        private static WorkTypeDef FindWorkType(string workTypeName)
        {
            string searchName = workTypeName.ToLower().Replace("_", "").Replace(" ", "");

            return DefDatabase<WorkTypeDef>.AllDefs
                .FirstOrDefault(w => w != null &&
                    (!string.IsNullOrEmpty(w.defName) && w.defName.ToLower().Replace("_", "").Contains(searchName)) ||
                    (!string.IsNullOrEmpty(w.label) && w.label.ToLower().Replace(" ", "").Contains(searchName)));
        }

        private static string GetPriorityName(int priority)
        {
            return priority switch
            {
                1 => "🔥 Highest",
                2 => "💪 High",
                3 => "👍 Medium",
                4 => "👌 Low",
                _ => "❌ Disabled"
            };
        }
    }
}