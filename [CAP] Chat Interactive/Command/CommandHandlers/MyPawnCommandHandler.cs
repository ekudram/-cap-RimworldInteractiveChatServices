using CAP_ChatInteractive;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Verse;

namespace CAP_ChatInteractive.Commands.CommandHandlers
{
    public static class MyPawnCommandHandler
    {
        public static string HandleMyPawnCommand(ChatMessageWrapper user, string subCommand, string[] args)
        {
            try
            {
                // Get the viewer and their pawn
                var viewer = Viewers.GetViewer(user.Username);
                if (viewer == null)
                {
                    return "Could not find your viewer data.";
                }

                var assignmentManager = CAPChatInteractiveMod.GetPawnAssignmentManager();

                // Check if viewer already has a pawn assigned using the new manager
                if (assignmentManager != null && assignmentManager.HasAssignedPawn(user.Username))
                {
                    Pawn existingPawn = assignmentManager.GetAssignedPawn(user.Username);
                    if (existingPawn == null || existingPawn.Dead)
                    {
                        return "You don't have an active pawn in the colony. Use !pawn to purchase one!";
                    }
                }

                var pawn = assignmentManager.GetAssignedPawn(user.Username);

                // Route to appropriate handler based on subcommand
                switch (subCommand)
                {
                    case "health":
                    case "body":
                        return HandleBodyInfo(pawn, args);
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
                        return HandleStoryInfo(pawn, args);
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

        private static string HandleBodyInfo(Pawn pawn, string[] args)
        {
            if (pawn.health?.hediffSet?.hediffs == null || pawn.health.hediffSet.hediffs.Count == 0)
            {
                return $"{pawn.Name} has no health conditions. 🟢";
            }

            var report = new StringBuilder();
            report.AppendLine($"🏥 Health Report for {pawn.Name}:");

            // Add temperature comfort range
            float minComfy = pawn.GetStatValue(StatDefOf.ComfyTemperatureMin);
            float maxComfy = pawn.GetStatValue(StatDefOf.ComfyTemperatureMax);
            report.AppendLine($"🌡️ Comfort Range: {minComfy.ToStringTemperature()} ~ {maxComfy.ToStringTemperature()}");

            // Get visible health conditions grouped by body part
            var healthConditions = GetVisibleHealthConditions(pawn);

            if (healthConditions.Count == 0)
            {
                report.AppendLine("No visible health issues. ✅");
                return report.ToString();
            }

            report.AppendLine("Health Conditions:");

            foreach (var partGroup in healthConditions)
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

        private static string HandleGearInfo(Pawn pawn, string[] args)
        {
            var report = new StringBuilder();
            report.AppendLine($"🎒 Gear Report for {pawn.Name}:");

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
                    string quality = item.TryGetQuality(out QualityCategory qc) ? $" ({qc})" : "";
                    string hitPoints = item.HitPoints != item.MaxHitPoints ?
                        $" 🩹{((float)item.HitPoints / item.MaxHitPoints).ToStringPercent()}" : "";

                    report.AppendLine($"  • {StripTags(item.LabelCap)}{quality}{hitPoints}");
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
            var armorStats = new List<string>();

            // Simple armor values from RimWorld stats
            float sharpArmor = pawn.GetStatValue(StatDefOf.ArmorRating_Sharp);
            float bluntArmor = pawn.GetStatValue(StatDefOf.ArmorRating_Blunt);
            float heatArmor = pawn.GetStatValue(StatDefOf.ArmorRating_Heat);

            if (sharpArmor > 0.01f)
                armorStats.Add($"🗡️{sharpArmor.ToStringPercent()}");

            if (bluntArmor > 0.01f)
                armorStats.Add($"🔨{bluntArmor.ToStringPercent()}");

            if (heatArmor > 0.01f)
                armorStats.Add($"🔥{heatArmor.ToStringPercent()}");

            if (armorStats.Count > 0)
            {
                return $"🛡️ Armor: {string.Join(" ", armorStats)}\n";
            }

            return "🛡️ Armor: None\n";
        }

        private static string StripTags(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            return System.Text.RegularExpressions.Regex.Replace(text, @"<[^>]*>", "");
        }

        private static string HandleKillInfo(Pawn pawn, string[] args)
        {
            var report = new StringBuilder();
            report.AppendLine($"💀 Kill Report for {pawn.Name}:");

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
            report.AppendLine($"😊 Needs Report for {pawn.Name}:");

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
            report.AppendLine($"💕 Relations Report for {pawn.Name}:");

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
            report.AppendLine($"🤝 Relations between {pawn.Name} and {targetViewer}'s pawn {targetPawn.Name}:");

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
                    string viewerInfo = assignmentManager.IsViewerPawn(relative) ? $" ({assignmentManager.GetUsernameForPawn(relative)})" : "";
                    report.AppendLine($"  • {relative.Name}{viewerInfo}: {relation}");
                }
            }

            // Viewer friends (top 5 by opinion)
            var viewerFriends = assignmentManager.GetAllViewerPawns()
                .Where(p => p != pawn && pawn.relations.OpinionOf(p) > 10)
                .OrderByDescending(p => pawn.relations.OpinionOf(p))
                .Take(5)
                .ToList();

            if (viewerFriends.Count > 0)
            {
                report.AppendLine("🎮 Viewer Friends:");
                foreach (var friend in viewerFriends)
                {
                    int opinion = pawn.relations.OpinionOf(friend);
                    string username = assignmentManager.GetUsernameForPawn(friend);
                    report.AppendLine($"  • {username}'s {friend.Name}: {opinion} 😊");
                }
            }

            // Viewer rivals (top 5 by negative opinion)
            var viewerRivals = assignmentManager.GetAllViewerPawns()
                .Where(p => p != pawn && pawn.relations.OpinionOf(p) < -10)
                .OrderBy(p => pawn.relations.OpinionOf(p))
                .Take(5)
                .ToList();

            if (viewerRivals.Count > 0)
            {
                report.AppendLine("⚔️ Viewer Rivals:");
                foreach (var rival in viewerRivals)
                {
                    int opinion = pawn.relations.OpinionOf(rival);
                    string username = assignmentManager.GetUsernameForPawn(rival);
                    report.AppendLine($"  • {username}'s {rival.Name}: {opinion} 😠");
                }
            }

            // Overall social summary
            int totalFriends = assignmentManager.GetAllViewerPawns().Count(p => p != pawn && pawn.relations.OpinionOf(p) > 10);
            int totalRivals = assignmentManager.GetAllViewerPawns().Count(p => p != pawn && pawn.relations.OpinionOf(p) < -10);

            report.AppendLine($"📊 Social Summary: {totalFriends} viewer friends, {totalRivals} viewer rivals");

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

            return "Relative";
        }

        private static string HandleSkillsInfo(Pawn pawn, string[] args)
        {
            var report = new StringBuilder();
            report.AppendLine($"🎯 Skills Report for {pawn.Name}:");

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
                string levelDescription = GetSkillLevelDescriptionDetailed(skill.Level);

                report.AppendLine($"• {skillName}: {passionEmoji} Level {skill.Level} ({levelDescription})");
            }

            // Add learning summary
            var burningPassions = skills.Count(s => s.passion == Passion.Major);
            var minorPassions = skills.Count(s => s.passion == Passion.Minor);

            if (burningPassions > 0 || minorPassions > 0)
            {
                report.AppendLine($"📚 Passions: {burningPassions} burning, {minorPassions} minor");
            }

            // Top 3 skills
            var topSkills = skills.OrderByDescending(s => s.Level).Take(3);
            if (topSkills.Any(s => s.Level >= 10))
            {
                report.Append("🏆 Best Skills: ");
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
                _ => "⚫"                // No passion
            };
        }

        private static string GetSkillLevelDescription(int level)
        {
            return level switch
            {
                >= 20 => "Legendary 🌟",
                >= 16 => "Master 🎯",
                >= 12 => "Expert 💪",
                >= 8 => "Skilled ✨",
                >= 4 => "Competent 👍",
                >= 1 => "Novice 👶",
                _ => "Ignorant ❓"
            };
        }

        // Alternative more detailed level descriptions if you prefer:
        private static string GetSkillLevelDescriptionDetailed(int level)
        {
            if (level >= 20) return "Legendary 🌟";
            if (level >= 18) return "Master 🎯";
            if (level >= 16) return "Expert 💪";
            if (level >= 14) return "Proficient ✨";
            if (level >= 12) return "Skilled 👍";
            if (level >= 10) return "Adept 👌";
            if (level >= 8) return "Competent ✅";
            if (level >= 6) return "Experienced 📚";
            if (level >= 4) return "Novice 👶";
            if (level >= 2) return "Beginner 🌱";
            if (level >= 1) return "Awkward 🐣";
            return "Ignorant ❓";
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
            report.AppendLine($"📊 Stats Overview for {pawn.Name}:");

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
            report.AppendLine("Available: movespeed, shootingaccuracy, meleehitchance, meleedps, workspeed, medicaltend, socialimpact, tradeprice, etc.");

            return report.ToString();
        }

        private static string GetSpecificStats(Pawn pawn, string[] args)
        {
            var report = new StringBuilder();
            report.AppendLine($"📊 Stats for {pawn.Name}:");

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

        private static string HandleStoryInfo(Pawn pawn, string[] args)
        {
            // If specific subcommand provided, route to appropriate handler
            if (args.Length > 0)
            {
                string subCommand = args[0].ToLower();
                switch (subCommand)
                {
                    case "backstory":
                    case "backstories":
                    case "history":
                        return HandleBackstoriesInfo(pawn, args.Skip(1).ToArray());
                    case "traits":
                        return HandleTraitsInfo(pawn, args.Skip(1).ToArray());
                    case "all":
                        return HandleFullStoryInfo(pawn);
                }
            }

            // Default: show brief overview
            return GetStoryOverview(pawn);
        }

        private static string GetStoryOverview(Pawn pawn)
        {
            var report = new StringBuilder();
            report.AppendLine($"📖 Story Overview for {pawn.Name}:");

            // Backstory count
            int backstoryCount = 0;
            if (pawn.story?.Childhood != null) backstoryCount++;
            if (pawn.story?.Adulthood != null) backstoryCount++;

            report.AppendLine($"• Backstories: {backstoryCount} (use !mypawn story backstory for details)");

            // Traits
            if (pawn.story?.traits != null && pawn.story.traits.allTraits.Count > 0)
            {
                var traitNames = pawn.story.traits.allTraits.Select(t => StripTags(t.LabelCap));
                report.AppendLine($"• Traits: {string.Join(", ", traitNames)}");
                report.AppendLine($"  (use !mypawn story traits for details)");
            }
            else
            {
                report.AppendLine("• Traits: None");
            }

            return report.ToString();
        }

        private static string HandleBackstoriesInfo(Pawn pawn, string[] args)
        {
            var report = new StringBuilder();
            report.AppendLine($"👤 Backstories for {pawn.Name}:");

            // Childhood backstory
            if (pawn.story?.Childhood != null)
            {
                report.AppendLine($"🎒 Childhood: {StripTags(pawn.story.Childhood.title)}");
                report.AppendLine($"   {StripTags(pawn.story.Childhood.FullDescriptionFor(pawn))}");
            }
            else
            {
                report.AppendLine("🎒 Childhood: No childhood backstory");
            }

            report.AppendLine(); // Spacing

            // Adulthood backstory (only if pawn is old enough)
            if (pawn.story?.Adulthood != null)
            {
                report.AppendLine($"🧑 Adulthood: {StripTags(pawn.story.Adulthood.title)}");
                report.AppendLine($"   {StripTags(pawn.story.Adulthood.FullDescriptionFor(pawn))}");
            }
            else if (pawn.ageTracker.AgeBiologicalYears >= 18)
            {
                report.AppendLine("🧑 Adulthood: No adulthood backstory");
            }
            else
            {
                report.AppendLine("🧑 Adulthood: Too young for adulthood backstory");
            }

            // Backstory stats if requested
            if (args.Length > 0 && args[0].ToLower() == "stats")
            {
                report.AppendLine();
                report.AppendLine("📊 Backstory Skill Modifiers:");
                report.Append(GetBackstorySkillModifiers(pawn));
            }

            return report.ToString();
        }

        private static string HandleTraitsInfo(Pawn pawn, string[] args)
        {
            var report = new StringBuilder();
            report.AppendLine($"🎭 Traits for {pawn.Name}:");

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

        private static string HandleFullStoryInfo(Pawn pawn)
        {
            var report = new StringBuilder();
            report.AppendLine($"📚 Complete Story for {pawn.Name}:");

            // Backstories section
            report.AppendLine(HandleBackstoriesInfo(pawn, new string[0]));
            report.AppendLine();

            // Traits section  
            report.AppendLine(HandleTraitsInfo(pawn, new string[0]));

            return report.ToString();
        }

        private static string GetBackstorySkillModifiers(Pawn pawn)
        {
            var modifiers = new StringBuilder();

            // Check childhood modifiers
            if (pawn.story?.Childhood?.workDisables != 0)
            {
                modifiers.AppendLine($"Childhood disables: {pawn.story.Childhood.workDisables}");
            }

            // Check adulthood modifiers  
            if (pawn.story?.Adulthood?.workDisables != 0)
            {
                modifiers.AppendLine($"Adulthood disables: {pawn.story.Adulthood.workDisables}");
            }

            // You could add more detailed skill modifier analysis here if needed

            return modifiers.Length > 0 ? modifiers.ToString() : "No significant skill modifiers from backstories.";
        }

        private static string HandleWorkInfo(Pawn pawn, string[] args)
        {
            // Check if pawn can work
            if (pawn.workSettings?.EverWork == false)
            {
                return $"{pawn.Name} is not capable of work.";
            }

            // Handle priority changes if arguments provided
            if (args.Length > 0)
            {
                return HandleWorkPriorityChanges(pawn, args);
            }

            // Default: show work priority summary
            return GetWorkPrioritySummary(pawn);
        }

        private static string HandleWorkPriorityChanges(Pawn pawn, string[] args)
        {
            var changes = new List<string>();

            foreach (var arg in args)
            {
                // Parse worktype=priority format (e.g., "firefight=1" or "doctor=3")
                var parts = arg.Split('=');
                if (parts.Length != 2)
                {
                    return $"Invalid format: {arg}. Use: worktype=priority (e.g., firefight=1)";
                }

                string workTypeName = parts[0].ToLower().Trim();
                if (!int.TryParse(parts[1], out int newPriority) || newPriority < 0 || newPriority > 4)
                {
                    return $"Invalid priority: {parts[1]}. Must be 0-4.";
                }

                // Find the work type
                var workType = DefDatabase<WorkTypeDef>.AllDefs
                    .FirstOrDefault(w => w.defName.Equals(workTypeName, StringComparison.OrdinalIgnoreCase) ||
                                        w.label.ToLower().Contains(workTypeName));

                if (workType == null)
                {
                    return $"Unknown work type: {workTypeName}. Use !mypawn work to see available types.";
                }

                // Check if work type is disabled for this pawn
                if (pawn.WorkTypeIsDisabled(workType))
                {
                    return $"{workType.label} is disabled for {pawn.Name}.";
                }

                // Change the priority
                int oldPriority = pawn.workSettings.GetPriority(workType);
                pawn.workSettings.SetPriority(workType, newPriority);

                changes.Add($"{workType.label}: {oldPriority}→{newPriority}");
            }

            return $"Work priorities updated: {string.Join(", ", changes)}";
        }

        private static string GetWorkPrioritySummary(Pawn pawn)
        {
            var report = new StringBuilder();
            report.AppendLine($"💼 Work Priorities for {pawn.Name}:");

            // Get all work types in priority order
            var workTypes = WorkTypeDefsUtility.WorkTypeDefsInPriorityOrder
                .Where(w => !pawn.WorkTypeIsDisabled(w))
                .ToList();

            if (workTypes.Count == 0)
            {
                return $"{pawn.Name} has no available work types.";
            }

            // Group by priority level (4=highest, 0=disabled)
            var byPriority = workTypes
                .Select(w => new { WorkType = w, Priority = pawn.workSettings.GetPriority(w) })
                .Where(x => x.Priority > 0) // Only show enabled work
                .GroupBy(x => x.Priority)
                .OrderByDescending(g => g.Key) // Highest priority first
                .ToList();

            foreach (var priorityGroup in byPriority)
            {
                string priorityName = GetPriorityName(priorityGroup.Key);
                var workNames = priorityGroup.Select(x => StripTags(x.WorkType.label)).OrderBy(n => n);

                report.AppendLine($"• {priorityName}: {string.Join(", ", workNames)}");
            }

            // Show disabled work types
            var disabledWork = workTypes
                .Where(w => pawn.workSettings.GetPriority(w) == 0)
                .Select(w => StripTags(w.label))
                .OrderBy(n => n)
                .ToList();

            if (disabledWork.Count > 0)
            {
                report.AppendLine($"• Disabled: {string.Join(", ", disabledWork.Take(5))}");
                if (disabledWork.Count > 5)
                {
                    report.AppendLine($"  ... and {disabledWork.Count - 5} more");
                }
            }

            report.AppendLine();
            report.AppendLine("💡 Usage: !mypawn work worktype=priority (e.g., !mypawn work doctor=3 firefight=1)");
            report.AppendLine("Priorities: 4=🔥, 3=💪, 2=👍, 1=👌, 0=❌");

            return report.ToString();
        }

        private static string GetPriorityName(int priority)
        {
            return priority switch
            {
                4 => "🔥 Highest",
                3 => "💪 High",
                2 => "👍 Medium",
                1 => "👌 Low",
                _ => "❌ Disabled"
            };
        }

        // Helper method to check if pawn is valid and accessible
        private static bool IsPawnValid(Pawn pawn)
        {
            return pawn != null && !pawn.Dead && pawn.Spawned;
        }

        // Helper method to format health percentage with color coding
        private static string FormatHealthPercentage(float percent)
        {
            if (percent >= 0.8f) return $"<color=green>{percent.ToStringPercent()}</color>";
            if (percent >= 0.5f) return $"<color=yellow>{percent.ToStringPercent()}</color>";
            return $"<color=red>{percent.ToStringPercent()}</color>";
        }
    }
}