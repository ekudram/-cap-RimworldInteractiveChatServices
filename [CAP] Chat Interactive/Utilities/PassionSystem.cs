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

        public static PassionResult GambleForPassion(Pawn pawn, int wager, Viewer viewer, SkillDef targetSkill = null)
        {
            var result = new PassionResult();
            var rand = new Random();
            double roll = rand.NextDouble() * 100.0;

            var settings = CAPChatInteractiveMod.Instance.Settings.GlobalSettings;
            double baseSuccessChance = CalculateSuccessChance(wager);
            double criticalSuccessChance = Math.Min(baseSuccessChance * settings.CriticalSuccessRatio, settings.MaxCriticalSuccessChance);
            double criticalFailChance = Math.Max(settings.CriticalFailBaseChance - (baseSuccessChance * settings.CriticalFailReductionFactor), settings.MinCriticalFailChance);

            criticalSuccessChance = Math.Clamp(criticalSuccessChance, 0.0, 100.0);
            criticalFailChance = Math.Clamp(criticalFailChance, 0.0, 100.0);

            var currentPassions = GetPawnPassions(pawn);
            var availableSkills = GetSkillsWithPassionPotential(pawn);

            if (availableSkills.Count == 0)
            {
                result.message = "RICS.PASSION.NoSkillsForPassion".Translate();
                result.success = false;
                return result;
            }

            if (targetSkill != null)
            {
                return HandleTargetedSkill(pawn, wager, viewer, targetSkill, roll,
                    baseSuccessChance, criticalSuccessChance, criticalFailChance);
            }

            if (roll < criticalSuccessChance)
            {
                return HandleCriticalSuccess(pawn, wager, viewer, currentPassions, availableSkills);
            }
            else if (roll < criticalSuccessChance + criticalFailChance)
            {
                return HandleCriticalFailure(pawn, wager, viewer, currentPassions, availableSkills);
            }
            else if (roll < criticalSuccessChance + criticalFailChance + baseSuccessChance)
            {
                return HandleSuccess(pawn, wager, viewer, currentPassions, availableSkills);
            }
            else
            {
                result.message = "RICS.PASSION.GeneralFailure".Translate(wager);
                result.success = false;
                result.alreadyCharged = true;
                viewer.TakeCoins(wager);
                return result;
            }
        }

        // ... (HandleTargetedSkill, HandleTargetedSuccess, HandleTargetedCriticalSuccess, HandleTargetedCriticalFailure remain the same structure but with translations - full code below for completeness)

        private static PassionResult HandleTargetedSkill(Pawn pawn, int wager, Viewer viewer,
            SkillDef targetSkill, double roll, double baseSuccessChance,
            double criticalSuccessChance, double criticalFailChance)
        {
            var result = new PassionResult();
            var targetSkillRecord = pawn.skills.GetSkill(targetSkill);

            if (targetSkillRecord == null)
            {
                result.message = "RICS.PASSION.NoSkill".Translate(targetSkill.LabelCap);
                result.success = false;
                return result;
            }

            if (roll < criticalSuccessChance)
            {
                return HandleTargetedCriticalSuccess(pawn, wager, viewer, targetSkillRecord);
            }
            else if (roll < criticalSuccessChance + criticalFailChance)
            {
                return HandleTargetedCriticalFailure(pawn, wager, viewer, targetSkillRecord);
            }
            else if (roll < criticalSuccessChance + criticalFailChance + baseSuccessChance)
            {
                return HandleTargetedSuccess(pawn, wager, viewer, targetSkillRecord);
            }
            else
            {
                result.message = "RICS.PASSION.GeneralFailure".Translate(wager); // reuse general failure for consistency
                result.success = false;
                result.alreadyCharged = true;
                viewer.TakeCoins(wager);
                return result;
            }
        }

        private static PassionResult HandleTargetedSuccess(Pawn pawn, int wager, Viewer viewer, SkillRecord targetSkillRecord)
        {
            var result = new PassionResult();

            if (targetSkillRecord.passion == Passion.Major)
            {
                result.message = "RICS.PASSION.TargetedAlreadyMajorSuccess".Translate(targetSkillRecord.def.LabelCap);
                result.success = true;
                result.alreadyCharged = true;
                return result;
            }
            else if (targetSkillRecord.passion == Passion.Minor)
            {
                targetSkillRecord.passion = Passion.Major;
                result.message = "RICS.PASSION.TargetedUpgradeToMajor".Translate(pawn.Name.ToString(), targetSkillRecord.def.LabelCap);
                result.success = true;
            }
            else
            {
                targetSkillRecord.passion = Passion.Minor;
                result.message = "RICS.PASSION.TargetedGainMinor".Translate(pawn.Name.ToString(), targetSkillRecord.def.LabelCap);
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

            if (rand.NextDouble() < settings.TargetedCritFailAffectTargetChance)
            {
                if (targetSkillRecord.passion != Passion.None)
                {
                    var lostPassionLevel = targetSkillRecord.passion == Passion.Major ? "major" : "minor";
                    targetSkillRecord.passion = Passion.None;
                    result.message = "RICS.PASSION.TargetedCritFailLose".Translate(pawn.Name.ToString(), lostPassionLevel, targetSkillRecord.def.LabelCap);
                }
                else
                {
                    var uselessSkills = GetUselessSkills(pawn);
                    if (uselessSkills.Count > 0)
                    {
                        var wrongSkill = uselessSkills[rand.Next(uselessSkills.Count)];
                        var wrongSkillRecord = pawn.skills.GetSkill(wrongSkill);

                        if (wrongSkillRecord.passion == Passion.None)
                        {
                            wrongSkillRecord.passion = Passion.Minor;
                            result.message = "RICS.PASSION.TargetedCritFailGainUseless".Translate(pawn.Name.ToString(), wrongSkill.LabelCap);
                        }
                        else
                        {
                            result.message = "RICS.PASSION.TargetedCritFailLaugh".Translate(wager);
                        }
                    }
                    else
                    {
                        result.message = "RICS.PASSION.TargetedCritFailBackfire".Translate(wager);
                    }
                }
            }
            else
            {
                var allSkills = pawn.skills.skills.Where(s => s.passion != Passion.None).ToList();
                if (allSkills.Count > 0)
                {
                    var randomSkill = allSkills[rand.Next(allSkills.Count)];
                    var lostPassionLevel = randomSkill.passion == Passion.Major ? "major" : "minor";
                    randomSkill.passion = Passion.None;
                    result.message = "RICS.PASSION.TargetedCritFailRandomLose".Translate(pawn.Name.ToString(), lostPassionLevel, randomSkill.def.LabelCap);
                }
                else
                {
                    result.message = "RICS.PASSION.TargetedCritFailRandomBackfire".Translate(wager);
                }
            }

            result.success = false;
            result.alreadyCharged = true;
            viewer.TakeCoins(wager);
            return result;
        }

        private static PassionResult HandleTargetedCriticalSuccess(Pawn pawn, int wager, Viewer viewer, SkillRecord skill)
        {
            var result = new PassionResult();

            if (skill.passion == Passion.Major)
            {
                result.message = "RICS.PASSION.TargetedCritSuccessAlreadyMajor".Translate(skill.def.LabelCap);
                result.success = true;
                result.alreadyCharged = true;
                return result;
            }

            skill.passion = skill.passion == Passion.Minor ? Passion.Major : Passion.Minor;
            var newPassionLevel = skill.passion == Passion.Major ? "major" : "minor";
            string pawnName = 
            result.message = "RICS.PASSION.TargetedCritSuccessUpgrade".Translate(pawn.Name.ToString(), skill.def.LabelCap, newPassionLevel);
            result.success = true;
            result.alreadyCharged = true;
            viewer.TakeCoins(wager);
            return result;
        }

        private static double CalculateSuccessChance(int wager)
        {
            var settings = CAPChatInteractiveMod.Instance.Settings.GlobalSettings;
            double baseChance = settings.BasePassionSuccessChance;
            double wagerBonus = Math.Min((wager / 100.0) * settings.PassionWagerBonusPer100, settings.MaxPassionWagerBonus);

            return Math.Min(baseChance + wagerBonus, settings.MaxPassionSuccessChance);
        }

        private static PassionResult HandleCriticalSuccess(Pawn pawn, int wager, Viewer viewer,
            List<SkillRecord> currentPassions, List<SkillDef> availableSkills)
        {
            var result = new PassionResult();
            var rand = new Random();
            var settings = CAPChatInteractiveMod.Instance.Settings.GlobalSettings;

            if (currentPassions.Count > 0 && rand.NextDouble() < settings.CritSuccessUpgradeVsNewChance)
            {
                var skillToUpgrade = currentPassions[rand.Next(currentPassions.Count)];
                var oldPassion = skillToUpgrade.passion;

                if (oldPassion == Passion.Major)
                {
                    result.message = "RICS.PASSION.CritSuccessAlreadyMajor".Translate(skillToUpgrade.def.LabelCap);
                    result.success = true;
                    result.alreadyCharged = true;
                    return result;
                }

                skillToUpgrade.passion = oldPassion == Passion.Minor ? Passion.Major : Passion.Minor;
                var newPassionLevel = skillToUpgrade.passion == Passion.Major ? "🔥🔥" : "🔥";

                result.message = "RICS.PASSION.CritSuccessUpgrade".Translate(pawn.Name.ToString(), skillToUpgrade.def.LabelCap, newPassionLevel);
                result.success = true;
            }
            else
            {
                var newSkillDef = availableSkills[rand.Next(availableSkills.Count)];
                var newSkill = pawn.skills.GetSkill(newSkillDef);
                newSkill.passion = Passion.Minor;

                result.message = "RICS.PASSION.CritSuccessGainNew".Translate(pawn.Name.ToString(), newSkillDef.LabelCap);
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

            if (currentPassions.Count > 0 && rand.NextDouble() < settings.CritFailLoseVsWrongChance)
            {
                var skillToLose = currentPassions[rand.Next(currentPassions.Count)];
                var lostPassionLevel = skillToLose.passion == Passion.Major ? "🔥🔥" : "🔥";
                skillToLose.passion = Passion.None;

                result.message = "RICS.PASSION.CritFailLose".Translate(pawn.Name.ToString(), lostPassionLevel, skillToLose.def.LabelCap);
            }
            else
            {
                var uselessSkills = availableSkills.Where(s => IsUselessSkill(s)).ToList();
                if (uselessSkills.Count == 0)
                    uselessSkills = availableSkills;

                var wrongSkillDef = uselessSkills[rand.Next(uselessSkills.Count)];
                var wrongSkill = pawn.skills.GetSkill(wrongSkillDef);

                if (wrongSkill.passion == Passion.None)
                {
                    wrongSkill.passion = Passion.Minor;
                    result.message = "RICS.PASSION.CritFailGainUseless".Translate(pawn.Name.ToString(), wrongSkillDef.LabelCap);
                }
                else
                {
                    result.message = "RICS.PASSION.CritFailLaugh".Translate(wager);
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
                var minorPassions = currentPassions.Where(p => p.passion == Passion.Minor).ToList();
                if (minorPassions.Count > 0)
                {
                    var skillToUpgrade = minorPassions[new Random().Next(minorPassions.Count)];
                    skillToUpgrade.passion = Passion.Major;

                    result.message = "RICS.PASSION.SuccessUpgrade".Translate(pawn.Name.ToString(), skillToUpgrade.def.LabelCap);
                    result.success = true;
                }
                else
                {
                    result.message = "RICS.PASSION.SuccessAllMajor".Translate();
                    result.success = true;
                    result.alreadyCharged = true;
                    return result;
                }
            }
            else
            {
                var newSkillDef = availableSkills[new Random().Next(availableSkills.Count)];
                var newSkill = pawn.skills.GetSkill(newSkillDef);
                newSkill.passion = Passion.Minor;
                
                result.message = "RICS.PASSION.SuccessGainFirst".Translate(pawn.Name.ToString(), newSkillDef.LabelCap);
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
            var uselessSkillIds = new[] { "Artistic", "Intellectual", "Social" };
            return uselessSkillIds.Contains(skill.defName);
        }

        private static List<SkillDef> GetUselessSkills(Pawn pawn)
        {
            var uselessSkillIds = new[] { "Artistic", "Intellectual", "Social", "Animals" };

            return pawn.skills.skills
                .Where(s => s != null && s.def != null && uselessSkillIds.Contains(s.def.defName) && s.passion == Passion.None)
                .Select(s => s.def)
                .ToList();
        }
    }
}