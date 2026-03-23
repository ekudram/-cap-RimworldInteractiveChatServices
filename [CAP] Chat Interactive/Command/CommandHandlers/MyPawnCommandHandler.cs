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
using System.Collections;
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
                    // return "Could not find your viewer data.";
                    return "RICS.MPCH.NoViewerData".Translate();
                }

                var assignmentManager = CAPChatInteractiveMod.GetPawnAssignmentManager();

                // Check if viewer already has a pawn assigned using the new manager
                if (assignmentManager != null)
                {
                    Pawn existingPawn = assignmentManager.GetAssignedPawn(messageWrapper);
                    if (existingPawn == null)
                    {
                        // return "You don't have an active pawn in the colony. Use !pawn to purchase one!";
                        return "RICS.MPCH.NoPawnAssigned".Translate();
                    }
                }

                var pawn = assignmentManager.GetAssignedPawn(messageWrapper);

                // Route to appropriate handler based on subcommand
                switch (subCommand)
                {
                    case "body":
                        return MyPawnCommandHandler_Body.HandleBodyInfo(pawn, args);
                    case "health":
                        return MyPawnCommandHandler_Body.HandlehealthInfo(pawn, args);
                    case "implants":
                        return MyPawnCommandHandler_Body.HandleImplantsInfo(pawn, args);
                    case "gear":
                        return MyPawnCommandHandler_Combat.HandleGearInfo(pawn, args);
                    case "kills":
                    case "killcount":
                        return MyPawnCommandHandler_Combat.HandleKillInfo(pawn, args);
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
                    case "job":
                    case "action":
                        return HandleJobInfo(pawn);
                    case "psycasts":
                        return HandlePsycastsInfo(pawn);
                    default:
                        // return $"Unknown subcommand: {subCommand}. !mypawn [type]: body, health, implants, gear, kills, needs, relations, skills, stats, story, traits, work";
                        return "RICS.MPCH.UnknownSubcommand".Translate(subCommand);
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error in MyPawn command handler: {ex}");
                // return "An error occurred while processing your pawn information.";
                return "RICS.MPCH.ErrorProcessingPawn".Translate();
            }
        }

        public static string StripTags(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            return System.Text.RegularExpressions.Regex.Replace(text, @"<[^>]*>", "");
        }

        private static string HandleNeedsInfo(Pawn pawn, string[] args)
        {
            var report = new StringBuilder();
            // report.AppendLine($"😊 Needs Report: "); //for {pawn.Name}:
            report.AppendLine("RICS.MPCH.NeedsHeader".Translate());

            var needs = pawn.needs?.AllNeeds;
            if (needs == null || needs.Count == 0)
            {
                // return $"{pawn.Name} has no needs tracked.";
                return "RICS.MPCH.NoNeeds".Translate(pawn.LabelShortCap);
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
                // report.AppendLine($"📊 Overall Mood: {moodStatus}");
                report.AppendLine("RICS.MPCH.OverallMood".Translate(moodStatus));
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
            // report.AppendLine($"💕 Relations Report:"); // for {pawn.Name}:
            report.AppendLine("RICS.MPCH.RelationsHeader".Translate());

            // Get the assignment manager
            var assignmentManager = Current.Game?.GetComponent<GameComponent_PawnAssignmentManager>();
            if (assignmentManager == null)
            {
                // return "Relations system not available.";
                return "RICS.MPCH.RelationsUnavailable".Translate();
            }

            // Handle specific viewer relation request
            if (args.Length > 0)
            {
                string targetViewer = args[0];
                var targetPawn = assignmentManager.GetAssignedPawn(targetViewer);
                if (targetPawn == null)
                {
                    // return $"Viewer '{targetViewer}' doesn't have an active pawn.";
                    return "RICS.MPCH.ViewerNoPawn".Translate(targetViewer);
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

            // report.AppendLine($"🤝 Relations between {pawnViewer} and {targetPawnViewer}:");
            report.AppendLine("RICS.MPCH.SpecificRelationHeader".Translate(pawnViewer, targetPawnViewer));

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

            // report.AppendLine($"• Relationship: {directRelation}");
            report.AppendLine("RICS.MPCH.SpecificRelationType".Translate(directRelation));

            // Opinion
            int opinion = pawn.relations.OpinionOf(targetPawn);
            string opinionEmoji = opinion >= 50 ? "😍" : opinion >= 25 ? "😊" : opinion >= 0 ? "🙂" : opinion >= -25 ? "😐" : opinion >= -50 ? "😠" : "😡";
            // report.AppendLine($"• Opinion: {opinion} {opinionEmoji}");
            report.AppendLine("RICS.MPCH.Opinion".Translate(opinion, opinionEmoji));

            // Romance-related info - basic
            if (pawn.relations.DirectRelationExists(PawnRelationDefOf.Lover, targetPawn) ||
                pawn.relations.DirectRelationExists(PawnRelationDefOf.Spouse, targetPawn))
            {
                // report.AppendLine($"• Relationship: Active 💑");
                report.AppendLine("RICS.MPCH.ActiveRelationship".Translate());
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
                // report.AppendLine("👨‍👩‍👧‍👦 Family:");
                report.AppendLine("RICS.MPCH.FamilyHeader".Translate());
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
                // report.AppendLine("🎮 Viewer Friends:");
                report.AppendLine("RICS.MPCH.ViewerFriendsHeader".Translate());
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
                // report.AppendLine("⚔️ Viewer Rivals:");
                report.AppendLine("RICS.MPCH.ViewerRivalsHeader".Translate());
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

            // report.AppendLine($"📊 Social Summary: {family.Count} family, {totalFriends} viewer friends, {totalRivals} viewer rivals");
            report.AppendLine("RICS.MPCH.SocialSummary".Translate(family.Count, totalFriends, totalRivals));    

            if (family.Count == 0 && viewerFriends.Count == 0 && viewerRivals.Count == 0)
            {
                // report.AppendLine("No significant relationships found.");
                report.AppendLine("RICS.MPCH.NoRelations".Translate());
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
            report.AppendLine("RICS.MPCH.SkillsHeader".Translate());

            var skills = pawn.skills?.skills;
            if (skills == null || skills.Count == 0)
            {
                string pawnName = GetDisplayNameForRelations(pawn);
                report.AppendLine("RICS.MPCH.NoSkillsTracked".Translate(pawnName));
                return report.ToString();
            }

            // Specific skill lookup (!mypawn skills shooting / melee / intellectual)
            if (args.Length > 0)
            {
                string search = string.Join(" ", args).ToLower().Replace("_", "").Replace(" ", "");
                var skill = skills.FirstOrDefault(s =>
                    s.def.defName.ToLower().Replace("_", "").Contains(search) ||
                    s.def.label.ToLower().Replace(" ", "").Contains(search));

                if (skill == null)
                {
                    return "RICS.MPCH.SkillNotFound".Translate(string.Join(" ", args));
                }

                string passionEmoji = GetPassionEmoji(skill.passion);
                report.AppendLine($"{passionEmoji}{StripTags(skill.def.LabelCap)}: Level {skill.Level}");

                if (skill.TotallyDisabled)
                {
                    // report.AppendLine("🚫 Disabled");   // backstory / gene / ideology role
                    report.AppendLine("RICS.MPCH.SkillDisabled".Translate());
                }

                if (skill.Level < 21)
                {
                    // report.AppendLine($"Progress: {skill.xpSinceLastLevel}/{skill.XpRequiredForLevelUp}");
                    int xpSinceLastLevel = (int)skill.xpSinceLastLevel;
                    report.AppendLine("RICS.MPCH.SKillProgress".Translate(skill.xpSinceLastLevel, skill.XpRequiredForLevelUp));
                }

                return report.ToString();
            }

            // Full list view — compact + disabled indicator
            foreach (var skill in skills.OrderBy(s => s.def.listOrder))
            {
                if (skill?.def == null) continue;
                string prefix = skill.TotallyDisabled ? "🚫 " : "";
                string passionEmoji = GetPassionEmoji(skill.passion);
                report.AppendLine($"| {prefix}{passionEmoji}{StripTags(skill.def.LabelCap)}: {skill.Level}");
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
            // report.AppendLine($"📊 Stats Overview:");  // for { pawn.Name}:
            report.AppendLine("RICS.MPCH.StatsOverview".Translate()); 

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
            // report.AppendLine("💡 Usage: !mypawn stats <stat1> <stat2> ...");
            report.AppendLine("RICS.MPCH.StatsUsage".Translate());
            // report.AppendLine("Examples: !mypawn stats movespeed shootingaccuracy meleedps");
            report.AppendLine("RICS.MPCH.StatsExample".Translate());
            // reduced for brevity
            //report.AppendLine("Available: movespeed, shootingaccuracy, meleehitchance, meleedps, workspeed, medicaltend, socialimpact, tradeprice, etc.");

            return report.ToString();
        }

        private static string GetSpecificStats(Pawn pawn, string[] args)
        {
            var report = new StringBuilder();
            report.AppendLine("RICS.MPCH.StatsHeader".Translate());

            const int MAX_STATS_TO_SHOW = 5;
            var foundDefs = new HashSet<string>(); // dedup by defName (handles "psychic" + "sensitivity" both matching same stat)
            var notFoundStats = new List<string>();
            int statsShown = 0;

            foreach (var statName in args)
            {
                if (statsShown >= MAX_STATS_TO_SHOW) break;

                var statDef = FindStatDef(statName);
                if (statDef != null)
                {
                    if (!foundDefs.Add(statDef.defName)) continue; // already shown - skip duplicate

                    float value = pawn.GetStatValue(statDef);
                    string formattedValue = FormatStatValue(statDef, value);
                    string description = StripTags(statDef.description) ?? "";

                    // Aggressive truncation for chat (keeps total message <500 chars)
                    if (description.Length > 60)
                    {
                        description = description.Substring(0, 57) + "...";
                    }

                    report.AppendLine($"• {StripTags(statDef.LabelCap)}: {formattedValue}");
                    if (!string.IsNullOrEmpty(description))
                    {
                        report.AppendLine($"  {description}");
                    }

                    statsShown++;
                }
                else
                {
                    notFoundStats.Add(statName);
                }
            }

            if (args.Length > MAX_STATS_TO_SHOW)
            {
                report.AppendLine($"... and {args.Length - MAX_STATS_TO_SHOW} more (max {MAX_STATS_TO_SHOW} shown per message)");
            }

            // Add not found stats at the end
            if (notFoundStats.Count > 0)
            {
                report.AppendLine();
                report.AppendLine("RICS.MPCH.UnknownStats".Translate(string.Join(", ", notFoundStats)));
                report.AppendLine("RICS.MPCH.UseStatsCommand".Translate());
            }

            return report.ToString();
        }

        private static StatDef FindStatDef(string statName)
        {
            if (string.IsNullOrWhiteSpace(statName)) return null;

            string search = statName.ToLower().Trim().Replace("_", "").Replace(" ", "");

            var allStats = DefDatabase<StatDef>.AllDefsListForReading;

            // 1. Exact defName match (highest priority - vanilla best practice)
            var exact = allStats.FirstOrDefault(s =>
                string.Equals(s.defName.Replace("_", ""), search, StringComparison.OrdinalIgnoreCase));
            if (exact != null) return exact;

            // 2. Exact label match (e.g. "psychic sensitivity")
            exact = allStats.FirstOrDefault(s =>
                string.Equals(s.label.Replace(" ", ""), search, StringComparison.OrdinalIgnoreCase));
            if (exact != null) return exact;

            // 3. Smart contains: prefer base stats over Offset/Factor variants (fixes the reported bug)
            //    + shorter defName first (PsychicSensitivity beats PsychicSensitivityOffset)
            return allStats
                .Where(s =>
                {
                    string defClean = s.defName.ToLower().Replace("_", "");
                    string labelClean = s.label.ToLower().Replace(" ", "");
                    return defClean.Contains(search) || labelClean.Contains(search);
                })
                .OrderBy(s => (s.defName.ToLower().EndsWith("offset") || s.defName.ToLower().EndsWith("factor")) &&
                               !search.Contains("offset") && !search.Contains("factor") ? 1 : 0)
                .ThenBy(s => s.defName.Length)   // shorter = more likely the "real" stat
                .ThenBy(s => s.label.Length)
                .FirstOrDefault();
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
            // report.Append($"Age:🧬{pawn.ageTracker.AgeBiologicalYears}/⏳{pawn.ageTracker.AgeChronologicalYears} | ");
            report.Append("RICS.MPCH.BackstoriesAge".Translate(pawn.ageTracker.AgeBiologicalYears, pawn.ageTracker.AgeChronologicalYears));
            // report.AppendLine($"👤 Backstories:");  // for {pawn.Name}:
            report.AppendLine("RICS.MPCH.BackstoriesHeader".Translate());

            // Childhood backstory - truncated to fit in one message
            if (pawn.story?.Childhood != null)
            {
                var childhoodDesc = StripTags(pawn.story.Childhood.FullDescriptionFor(pawn));
                // var truncatedChildhood = TruncateDescription(childhoodDesc, 183); // Limit to 188 chars

                // report.AppendLine($"🎒 Childhood: {StripTags(pawn.story.Childhood.title)}");
                report.AppendLine("RICS.MPCH.Childhood".Translate(StripTags(pawn.story.Childhood.title)));
                // report.AppendLine($"   {truncatedChildhood}");
            }
            else
            {
                // report.AppendLine("🎒 Childhood: No childhood backstory");
                report.AppendLine("RICS.MPCH.ChildhoodNone".Translate());
            }

            report.AppendLine(); // Spacing

            // Adulthood backstory - truncated to fit in one message
            if (pawn.story?.Adulthood != null)
            {
                var adulthoodDesc = StripTags(pawn.story.Adulthood.FullDescriptionFor(pawn));
                // var truncatedAdulthood = TruncateDescription(adulthoodDesc, 183); // Limit to 183 chars

                // report.AppendLine($"🧑 Adulthood: {StripTags(pawn.story.Adulthood.title)}");
                report.AppendLine("RICS.MPCH.Adulthood".Translate(StripTags(pawn.story.Adulthood.title)));
                // report.AppendLine($"   {truncatedAdulthood}");
            }
            else if (pawn.ageTracker.AgeBiologicalYears >= 18)
            {
                // report.AppendLine("🧑 Adulthood: No adulthood backstory");
                report.AppendLine("RICS.MPCH.AdulthoodNone".Translate());
            }
            else
            {
                // report.AppendLine("🧑 Adulthood: Too young for adulthood backstory");
                report.AppendLine("RICS.MPCH.AdulthoodTooYoung".Translate());
            }

            return report.ToString();
        }

        private static string HandleTraitsInfo(Pawn pawn, string[] args)
        {
            var report = new StringBuilder();
            report.AppendLine("RICS.MPCH.TraitsHeader".Translate());

            // Cleaner no-traits handling (original continued into foreach and could NRE if traits == null)
            if (pawn.story?.traits == null || pawn.story.traits.allTraits.Count == 0)
            {
                string pawnName = GetDisplayNameForRelations(pawn);
                report.AppendLine("RICS.MPCH.NoTraits".Translate(pawnName));
                return report.ToString();
            }

            foreach (var trait in pawn.story.traits.allTraits)
            {
                if (trait == null) continue;

                string traitName = StripTags(trait.LabelCap);

                // 🔒 Gene/race locked trait indicator (Biotech only)
                // Verified vanilla behavior: gene-forced traits have trait.sourceGene set (used by Bio tab, trait removal, and surgery UI).
                // We guard with ModsConfig.BiotechActive for perfect compatibility on non-Biotech saves.
                if (ModsConfig.BiotechActive && trait.sourceGene != null)
                {
                    traitName += " 🔒";
                }

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
                // return $"{pawn.Name} is not capable of work.";
                string pawnName = GetDisplayNameForRelations(pawn);
                return "RICS.MPCH.NotCapableOfWork".Translate(pawnName);
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
            // report.AppendLine("💼 Top Work Priorities:");
            report.AppendLine("RICS.MPCH.TopWorkPriorities".Translate());

            // Get work types with priority 1 (highest)
            var topPriorityWork = WorkTypeDefsUtility.WorkTypeDefsInPriorityOrder
                .Where(w => !pawn.WorkTypeIsDisabled(w) && pawn.workSettings.GetPriority(w) == 1)
                .Take(5)
                .ToList();

            if (topPriorityWork.Count > 0)
            {
                // report.AppendLine("🔥 Highest (1):");
                report.AppendLine("RICS.MPCH.HighestPriority".Translate());
                foreach (var workType in topPriorityWork)
                {
                    string label = StripTags(workType.pawnLabel);
                    report.AppendLine($"  • {label}");
                }
            }
            else
            {
                // report.AppendLine("No work set to highest priority");
                report.AppendLine("RICS.MPCH.NoHighestPriority".Translate());
            }

            report.AppendLine();
            // report.AppendLine("💡 Usage: !mypawn work <worktype> [1-4]");
            report.AppendLine("RICS.MPCH.WorkUsage".Translate());
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
                    // results.Add($"❌ Unknown work: {workTypeName}");
                    results.Add("RICS.MPCH.UnknownWork".Translate(workTypeName));
                    continue;
                }

                // Check if work type is disabled for this pawn
                if (pawn.WorkTypeIsDisabled(workType))
                {
                    // results.Add($"❌ {workType.label} disabled");
                    results.Add("RICS.MPCH.WorkDisabled".Translate(workType.label));
                    continue;
                }

                // If next arg exists and is a number 1-4, set priority
                if (i + 1 < args.Length && int.TryParse(args[i + 1], out int newPriority) && newPriority >= 0 && newPriority <= 4)
                {
                    int oldPriority = pawn.workSettings.GetPriority(workType);
                    pawn.workSettings.SetPriority(workType, newPriority);

                    string priorityName = GetPriorityName(newPriority);
                    // results.Add($"✅ {workType.label}: {oldPriority}→{newPriority} ({priorityName})");
                    results.Add("RICS.MPCH.WorkPriorityChanged".Translate(workType.label, oldPriority, newPriority, priorityName));
                    i++; // Skip the priority arg since we used it
                }
                else
                {
                    // Just show current priority
                    int currentPriority = pawn.workSettings.GetPriority(workType);
                    string priorityName = GetPriorityName(currentPriority);
                    // results.Add($"📋 {workType.label}: {currentPriority} ({priorityName})");
                    results.Add("RICS.MPCH.WorkPriorityCurrent".Translate(workType.label, currentPriority, priorityName));
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

        // === Job ===

        private static string HandleJobInfo(Pawn pawn)
        {
            if (pawn?.jobs == null)
            {
                // return "Your pawn is doing nothing.";
                return "RICS.MPCH.PawnJob".Translate("RICS.MPCH.DoingNothing".Translate());
            }

            string currentJob = pawn.jobs.curDriver != null
                ? pawn.jobs.curDriver.GetReport().CapitalizeFirst()
                // : "Doing nothing";
                : "RICS.MPCH.DoingNothing".Translate().ToString();

            string queuedJob = string.Empty;
            if (pawn.jobs.jobQueue != null && pawn.jobs.jobQueue.Count > 0)
            {
                var firstQueueNode = pawn.jobs.jobQueue[0]?.job;
                if (firstQueueNode != null)
                {
                    queuedJob = firstQueueNode.GetReport(pawn).CapitalizeFirst();
                }
            }

            if (!string.IsNullOrEmpty(queuedJob))
            {
                // return $"Your pawn is: {currentJob} | Queued: {queuedJob}";
                return "RICS.MPCH.PawnJobQueued".Translate(currentJob, queuedJob);
            }

            // return $"Your pawn is: {currentJob}";
            return "RICS.MPCH.PawnJob".Translate(currentJob);
        }

        // === Psycasts ===
        // === Psycasts ===

        private static string HandlePsycastsInfo(Pawn pawn)
        {
            if (!ModsConfig.RoyaltyActive)
            {
                Logger.Debug("[MyPawn Psycasts] Royalty DLC not active");
                return "RICS.MPCH.NoRoyalty".Translate();
            }

            if (pawn == null)
            {
                Logger.Warning("[MyPawn Psycasts] Pawn is null");
                return "RICS.MPCH.NoPsycasts".Translate("Unknown");
            }

            if (pawn.abilities == null)
            {
                pawn.abilities = new Pawn_AbilityTracker(pawn);
                Logger.Debug("[MyPawn Psycasts] Forced Pawn_AbilityTracker creation");
            }

            pawn.abilities.Notify_TemporaryAbilitiesChanged();

            var allAbilities = pawn.abilities.AllAbilitiesForReading;

            // Attempt to detect VPE (for logging only — we no longer try to extract)
            bool hasVPEHediff = false;
            if (pawn.health?.hediffSet?.hediffs != null)
            {
                hasVPEHediff = pawn.health.hediffSet.hediffs.Any(h =>
                    h?.def?.defName == "VPE_PsycastAbilityImplant" ||
                    (h != null && h.GetType().Name.Contains("PsycastAbilities")));
            }

            var psycasts = allAbilities
                .Where(a => a != null && a.def != null &&
                            (a.def.IsPsycast ||
                             a is Psycast ||
                             a.def.PsyfocusCost > 0f ||
                             a.def.EntropyGain > 0f ||
                             a.def.AnyCompOverridesPsyfocusCost ||
                             a.def.defName.StartsWith("VPE_") ||  // fallback catch-all for any leaked VPE defs
                             a.def.defName.StartsWith("Ability_VPE_")))
                .GroupBy(a => a.def.level)
                .OrderBy(g => g.Key)
                .ToList();

            int totalPsycasts = psycasts.Sum(g => g.Count());

            if (totalPsycasts == 0)
            {
                if (allAbilities.NullOrEmpty())
                {
                    Logger.Warning("[MyPawn Psycasts] AllAbilitiesForReading empty after init");
                    return "RICS.MPCH.NoPsycastsLazyInit".Translate(pawn.LabelShortCap);
                }

                // User-facing message for VPE
                if (hasVPEHediff)
                {
                    Logger.Debug("[MyPawn Psycasts] VPE hediff detected — psycasts not shown (VPE incompatibility)");
                    return "RICS.MPCH.NoPsycastsVPE".Translate(pawn.LabelShortCap);
                    // Suggested translation: "Your pawn has psycasts from Vanilla Psycasts Expanded, but they are not visible in this command due to mod incompatibility."
                }

                return "RICS.MPCH.NoPsycasts".Translate(pawn.LabelShortCap);
            }

            List<string> levelStrings = new List<string>();
            foreach (var group in psycasts)
            {
                string names = string.Join("• ", group.Select(a => StripTags(a.def.LabelCap.Resolve())));
                levelStrings.Add("RICS.MPCH.PsycastLevelGroup".Translate(group.Key, names));
            }

            string result = string.Join("RICS.MPCH.PsycastSeparator".Translate(), levelStrings);
            Logger.Debug($"[MyPawn Psycasts] Built response with {levelStrings.Count} level groups");
            return result;
        }
    }
}