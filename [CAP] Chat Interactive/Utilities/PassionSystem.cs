// PassionSystem.cs
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
// Handles passion gambling mechanics
using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace CAP_ChatInteractive
{
    public static class PassionSystem
    {
        public struct PassionResult
        {
            public string message;
            public bool success;
            public bool alreadyCharged;
        }

        public static PassionResult                                                                                                                                           GambleForPassion(Pawn pawn, int wager, Viewer viewer, SkillDef targetSkill = null)
        {
            var result = new PassionResult();
            var rand = new Random();
            double roll = rand.NextDouble() * 100.0;

            // Calculate success chance based on wager - using fully configurable settings
            var settings = CAPChatInteractiveMod.Instance.Settings.GlobalSettings;
            double baseSuccessChance = CalculateSuccessChance(wager);
            double criticalSuccessChance = Math.Min(baseSuccessChance * settings.CriticalSuccessRatio, settings.MaxCriticalSuccessChance);
            double criticalFailChance = Math.Max(settings.CriticalFailBaseChance - (baseSuccessChance * settings.CriticalFailReductionFactor), settings.MinCriticalFailChance);

            // Robust edge-case handling (prevents negative/over-100% probs from bad config)
            criticalSuccessChance = Math.Clamp(criticalSuccessChance, 0.0, 100.0);
            criticalFailChance = Math.Clamp(criticalFailChance, 0.0, 100.0);

            // Get current passions
            var currentPassions = GetPawnPassions(pawn);
            var availableSkills = GetSkillsWithPassionPotential(pawn);

            if (availableSkills.Count == 0)
            {
                result.message = "❌ Your pawn has no skills that can gain passions!";
                result.success = false;
                return result;
            }

            // Handle targeted skill vs random
            if (targetSkill != null)
            {
                return HandleTargetedSkill(pawn, wager, viewer, targetSkill, roll,
                    baseSuccessChance, criticalSuccessChance, criticalFailChance);
            }

            // Critical Success (Upgrade passion or gain new one)
            if (roll < criticalSuccessChance)
            {
                return HandleCriticalSuccess(pawn, wager, viewer, currentPassions, availableSkills);
            }
            // Critical Failure (Lose passion or gain wrong one)
            else if (roll < criticalSuccessChance + criticalFailChance)
            {
                return HandleCriticalFailure(pawn, wager, viewer, currentPassions, availableSkills);
            }
            // Success (Upgrade passion)
            else if (roll < criticalSuccessChance + criticalFailChance + baseSuccessChance)
            {
                return HandleSuccess(pawn, wager, viewer, currentPassions, availableSkills);
            }
            // Failure (No change, lose wager)
            else
            {
                result.message = $"💔 You wagered {wager} coins but failed to improve any passions. Better luck next time!";
                result.success = false;
                result.alreadyCharged = true;
                viewer.TakeCoins(wager);
                return result;
            }
        }

        private static PassionResult HandleTargetedSkill(Pawn pawn, int wager, Viewer viewer,
    SkillDef targetSkill, double roll, double baseSuccessChance,
    double criticalSuccessChance, double criticalFailChance)
        {
            var result = new PassionResult();
            var targetSkillRecord = pawn.skills.GetSkill(targetSkill);

            if (targetSkillRecord == null)
            {
                result.message = $"❌ Your pawn doesn't have the {targetSkill.LabelCap} skill.";
                result.success = false;
                return result;
            }

            // Critical Success
            if (roll < criticalSuccessChance)
            {
                return HandleTargetedCriticalSuccess(pawn, wager, viewer, targetSkillRecord);
            }
            // Critical Failure
            else if (roll < criticalSuccessChance + criticalFailChance)
            {
                return HandleTargetedCriticalFailure(pawn, wager, viewer, targetSkillRecord);
            }
            // Success
            else if (roll < criticalSuccessChance + criticalFailChance + baseSuccessChance)
            {
                return HandleTargetedSuccess(pawn, wager, viewer, targetSkillRecord);
            }
            // Failure
            else
            {
                result.message = $"💔 You wagered {wager} coins but failed to improve {targetSkill.LabelCap} passion.";
                result.success = false;
                result.alreadyCharged = true;
                viewer.TakeCoins(wager);
                return result;
            }
        }

        private static PassionResult HandleTargetedSuccess(Pawn pawn, int wager, Viewer viewer, SkillRecord targetSkillRecord)
        {
            var result = new PassionResult();

            // Check current passion level
            if (targetSkillRecord.passion == Passion.Major)
            {
                result.message = $"✅ SUCCESS! Your pawn already has major passion in {targetSkillRecord.def.LabelCap}. Coins refunded!";
                result.success = true;
                result.alreadyCharged = true;
                return result;
            }
            else if (targetSkillRecord.passion == Passion.Minor)
            {
                // Upgrade minor to major passion
                targetSkillRecord.passion = Passion.Major;
                result.message = $"✅ SUCCESS! {pawn.Name}'s {targetSkillRecord.def.LabelCap} passion upgraded to major!";
                result.success = true;
            }
            else
            {
                // Gain first passion in this skill (none to minor)
                targetSkillRecord.passion = Passion.Minor;
                result.message = $"✅ SUCCESS! {pawn.Name} gained minor passion for {targetSkillRecord.def.LabelCap}!";
                result.success = true;
            }

            result.alreadyCharged = true;
            viewer.TakeCoins(wager);
            return result;
        }

        private static PassionResult HandleTargetedCriticalFailure(Pawn pawn, int wager, Viewer viewer, SkillRecord targetSkillRecord)
        {
            var result = new PassionResult();
            var rand = new Random();

            var settings = CAPChatInteractiveMod.Instance.Settings.GlobalSettings;
            // Configurable chance (default 70%) to affect targeted skill vs random other on targeted crit failure
            if (rand.NextDouble() < settings.TargetedCritFailAffectTargetChance)
            {
                // Affect the targeted skill
                if (targetSkillRecord.passion != Passion.None)
                {
                    // Lose passion in the targeted skill
                    var lostPassionLevel = targetSkillRecord.passion == Passion.Major ? "major" : "minor";
                    targetSkillRecord.passion = Passion.None;
                    result.message = $"💥 CRITICAL FAILURE! {pawn.Name} lost {lostPassionLevel} passion for {targetSkillRecord.def.LabelCap}!";
                }
                else
                {
                    // Targeted skill has no passion - gain a useless passion instead
                    var uselessSkills = GetUselessSkills(pawn);
                    if (uselessSkills.Count > 0)
                    {
                        var wrongSkill = uselessSkills[rand.Next(uselessSkills.Count)];
                        var wrongSkillRecord = pawn.skills.GetSkill(wrongSkill);

                        if (wrongSkillRecord.passion == Passion.None)
                        {
                            wrongSkillRecord.passion = Passion.Minor;
                            result.message = $"💥 CRITICAL FAILURE! {pawn.Name} gained useless minor passion for {wrongSkill.LabelCap}!";
                        }
                        else
                        {
                            result.message = $"💥 CRITICAL FAILURE! The passion gods laughed at your {wager} coin wager!";
                        }
                    }
                    else
                    {
                        result.message = $"💥 CRITICAL FAILURE! Your {wager} coin wager backfired spectacularly!";
                    }
                }
            }
            else
            {
                // Affect a random skill instead of the targeted one
                var allSkills = pawn.skills.skills.Where(s => s.passion != Passion.None).ToList();
                if (allSkills.Count > 0)
                {
                    var randomSkill = allSkills[rand.Next(allSkills.Count)];
                    var lostPassionLevel = randomSkill.passion == Passion.Major ? "major" : "minor";
                    randomSkill.passion = Passion.None;
                    result.message = $"💥 CRITICAL FAILURE! {pawn.Name} lost {lostPassionLevel} passion for {randomSkill.def.LabelCap} instead!";
                }
                else
                {
                    result.message = $"💥 CRITICAL FAILURE! Your {wager} coin wager backfired!";
                }
            }

            result.success = false;
            result.alreadyCharged = true;
            viewer.TakeCoins(wager);
            return result;
        }

        // Helper method for useless skills
        private static List<SkillDef> GetUselessSkills(Pawn pawn)
        {
            var uselessSkillIds = new[] { "Artistic", "Intellectual", "Social", "Animals" };

            // Fix: Add null checks and use ToList() to avoid LINQ issues
            return pawn.skills.skills
                .Where(s => s != null && s.def != null && uselessSkillIds.Contains(s.def.defName) && s.passion == Passion.None)
                .Select(s => s.def)
                .ToList();
        }

        private static PassionResult HandleTargetedCriticalSuccess(Pawn pawn, int wager, Viewer viewer, SkillRecord skill)
        {
            var result = new PassionResult();

            if (skill.passion == Passion.Major)
            {
                result.message = $"🎉 CRITICAL SUCCESS! Your pawn already has major passion in {skill.def.LabelCap}. Coins refunded!";
                result.success = true;
                result.alreadyCharged = true;
                return result;
            }

            skill.passion = skill.passion == Passion.Minor ? Passion.Major : Passion.Minor;
            var newPassionLevel = skill.passion == Passion.Major ? "major" : "minor";

            result.message = $"🎉 CRITICAL SUCCESS! {pawn.Name}'s {skill.def.LabelCap} passion upgraded to {newPassionLevel}!";
            result.success = true;
            result.alreadyCharged = true;
            viewer.TakeCoins(wager);
            return result;
        }


        private static double CalculateSuccessChance(int wager)
        {
            // Base chance scales with wager, but with diminishing returns - now fully configurable
            var settings = CAPChatInteractiveMod.Instance.Settings.GlobalSettings;
            double baseChance = settings.BasePassionSuccessChance;
            double wagerBonus = Math.Min((wager / 100.0) * settings.PassionWagerBonusPer100, settings.MaxPassionWagerBonus);

            return Math.Min(baseChance + wagerBonus, settings.MaxPassionSuccessChance);
        }

        // In PassionSystem.cs - Update the result messages with better emojis

        private static PassionResult HandleCriticalSuccess(Pawn pawn, int wager, Viewer viewer,
            List<SkillRecord> currentPassions, List<SkillDef> availableSkills)
        {
            var result = new PassionResult();
            var rand = new Random();
            var settings = CAPChatInteractiveMod.Instance.Settings.GlobalSettings;
            // Configurable chance (default 50%) to upgrade existing vs gain new on crit success
            if (currentPassions.Count > 0 && rand.NextDouble() < settings.CritSuccessUpgradeVsNewChance)
            {
                // Upgrade existing passion
                var skillToUpgrade = currentPassions[rand.Next(currentPassions.Count)];
                var oldPassion = skillToUpgrade.passion;

                if (oldPassion == Passion.Major)
                {
                    result.message = $"🎉 CRITICAL SUCCESS! Your pawn already has 🔥🔥 passion in {skillToUpgrade.def.LabelCap}. Coins refunded!";
                    result.success = true;
                    result.alreadyCharged = true;
                    return result;
                }

                skillToUpgrade.passion = oldPassion == Passion.Minor ? Passion.Major : Passion.Minor;
                var newPassionLevel = skillToUpgrade.passion == Passion.Major ? "🔥🔥" : "🔥";

                result.message = $"🎉 CRITICAL SUCCESS! {pawn.Name}'s {skillToUpgrade.def.LabelCap} passion upgraded to {newPassionLevel}!";
                result.success = true;
            }
            else
            {
                // Gain new passion in random available skill
                var newSkillDef = availableSkills[rand.Next(availableSkills.Count)];
                var newSkill = pawn.skills.GetSkill(newSkillDef);
                newSkill.passion = Passion.Minor;

                result.message = $"🎉 CRITICAL SUCCESS! {pawn.Name} gained 🔥 passion for {newSkillDef.LabelCap}!";
                result.success = true;
            }

            result.alreadyCharged = true;
            viewer.TakeCoins(wager);
            return result;
        }

        private static PassionResult HandleCriticalFailure(Pawn pawn, int wager, Viewer viewer,
            List<SkillRecord> currentPassions, List<SkillDef> availableSkills)
        {
            var result = new PassionResult();
            var rand = new Random();

            var settings = CAPChatInteractiveMod.Instance.Settings.GlobalSettings;
            // Configurable chance (default 60%) to lose passion vs gain useless on crit failure
            if (currentPassions.Count > 0 && rand.NextDouble() < settings.CritFailLoseVsWrongChance)
            {
                // Lose random passion
                var skillToLose = currentPassions[rand.Next(currentPassions.Count)];
                var lostPassionLevel = skillToLose.passion == Passion.Major ? "🔥🔥" : "🔥";
                skillToLose.passion = Passion.None;

                result.message = $"💥 CRITICAL FAILURE! {pawn.Name} lost {lostPassionLevel} passion for {skillToLose.def.LabelCap}!";
            }
            else
            {
                // Gain passion in useless skill
                var uselessSkills = availableSkills.Where(s => IsUselessSkill(s)).ToList();
                if (uselessSkills.Count == 0)
                    uselessSkills = availableSkills;

                var wrongSkillDef = uselessSkills[rand.Next(uselessSkills.Count)];
                var wrongSkill = pawn.skills.GetSkill(wrongSkillDef);

                if (wrongSkill.passion == Passion.None)
                {
                    wrongSkill.passion = Passion.Minor;
                    result.message = $"💥 CRITICAL FAILURE! {pawn.Name} gained 🔥 passion for useless skill {wrongSkillDef.LabelCap}!";
                }
                else
                {
                    result.message = $"💥 CRITICAL FAILURE! The passion gods laughed at your wager of {wager} coins! 😂";
                }
            }

            result.success = false;
            result.alreadyCharged = true;
            viewer.TakeCoins(wager);
            return result;
        }

        private static PassionResult HandleSuccess(Pawn pawn, int wager, Viewer viewer,
            List<SkillRecord> currentPassions, List<SkillDef> availableSkills)
        {
            var result = new PassionResult();

            if (currentPassions.Count > 0)
            {
                // Upgrade existing minor passion to major
                var minorPassions = currentPassions.Where(p => p.passion == Passion.Minor).ToList();
                if (minorPassions.Count > 0)
                {
                    var skillToUpgrade = minorPassions[new Random().Next(minorPassions.Count)];
                    skillToUpgrade.passion = Passion.Major;

                    result.message = $"✅ SUCCESS! {pawn.Name}'s {skillToUpgrade.def.LabelCap} passion upgraded to 🔥🔥!";
                    result.success = true;
                }
                else
                {
                    result.message = $"✅ SUCCESS! Your pawn already has 🔥🔥 passions in all skills. Coins refunded!";
                    result.success = true;
                    result.alreadyCharged = true;
                    return result;
                }
            }
            else
            {
                // Gain first passion
                var newSkillDef = availableSkills[new Random().Next(availableSkills.Count)];
                var newSkill = pawn.skills.GetSkill(newSkillDef);
                newSkill.passion = Passion.Minor;

                result.message = $"✅ SUCCESS! {pawn.Name} gained 🔥 passion for {newSkillDef.LabelCap}!";
                result.success = true;
            }

            result.alreadyCharged = true;
            viewer.TakeCoins(wager);
            return result;
        }

        private static List<SkillRecord> GetPawnPassions(Pawn pawn)
        {
            return pawn.skills.skills.Where(s => s.passion != Passion.None).ToList();
        }

        private static List<SkillDef> GetSkillsWithPassionPotential(Pawn pawn)
        {
            var allSkills = DefDatabase<SkillDef>.AllDefs.ToList();
            return allSkills.Where(s => pawn.skills.GetSkill(s) != null).ToList();
        }

        private static bool IsUselessSkill(SkillDef skill)
        {
            // Consider artistic, intellectual, social as less immediately useful for survival
            var uselessSkillIds = new[] { "Artistic", "Intellectual", "Social" };
            return uselessSkillIds.Contains(skill.defName);
        }
    }
}