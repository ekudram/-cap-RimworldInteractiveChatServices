// PassionCommandhandler.cs
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
// Passion command handler
using _CAP__Chat_Interactive.Command.CommandHelpers;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace CAP_ChatInteractive.Commands.ViewerCommands
{
    internal class PassionCommandhandler
    {
        internal static string HandlePassionCommand(ChatMessageWrapper messageWrapper, string[] args)
        {
            try
            {
                var viewer = Viewers.GetViewer(messageWrapper);
                if (viewer == null)
                    return "RICS.PASSION.ViewerNotFound".Translate();

                Verse.Pawn pawn = PawnItemHelper.GetViewerPawn(messageWrapper);

                if (pawn == null)
                    return "RICS.PASSION.NoAssignedPawn".Translate();

                if (pawn.Dead)
                {
                    var deathInfo = GameComponent_PawnAssignmentManager.GetPawnDeathInfo(pawn);

                    string deathDetails = deathInfo.ToString(); // e.g. "Deceased (body remains) — bullet wound caused by Assault Rifle"

                    return "RICS.PASSION.PawnDead".Translate() + "RICS.Return.PawnDeadReason".Translate(deathDetails);
                }

                if (args.Length > 0 && args[0].ToLower() == "list")
                {
                    return ListPawnPassions(pawn);
                }

                if (args.Length == 0)
                    return "RICS.PASSION.Usage".Translate();

                // NEW: Flexible parsing for both orders (!passion <wager> <skill> OR !passion <skill> <wager>)
                (int? wager, SkillDef targetSkill) = ParseWagerAndSkill(args);

                if (wager == null)
                    return "RICS.PASSION.Usage".Translate();

                int finalWager = wager.Value;

                // Values now come from per-command CustomData (migrated from global settings).
                var cmdSettings = CommandSettingsManager.GetSettings("passion");
                int minW = cmdSettings.GetCustom<int>("minPassionWager", 500);
                int maxW = cmdSettings.GetCustom<int>("maxPassionWager", 1000);

                if (finalWager < minW)
                    return "RICS.PASSION.MinWager".Translate(minW);

                if (finalWager > maxW)
                    return "RICS.PASSION.MaxWager".Translate(maxW);

                if (viewer.Coins < finalWager)
                    return "RICS.PASSION.NotEnoughCoins".Translate(viewer.Coins, finalWager);

                var result = PassionSystem.GambleForPassion(pawn, finalWager, viewer, targetSkill);

                if (!result.alreadyCharged)
                {
                    viewer.TakeCoins(finalWager);
                }

                Viewers.SaveViewers();

                return result.message;
            }
            catch (Exception ex)
            {
                Logger.Error($"Error in passion command: {ex.Message}");
                return "RICS.PASSION.GeneralError".Translate();
            }
        }

        private static string ListPawnPassions(Verse.Pawn pawn)
        {
            try
            {
                var passionSkills = new List<string>();

                foreach (var skill in pawn.skills.skills)
                {
                    if (skill?.passion != RimWorld.Passion.None && skill?.def != null)
                    {
                        string passionLevel = GetPassionEmoji(skill.passion);
                        passionSkills.Add($"{skill.def.LabelCap}{passionLevel}");
                    }
                }

                if (!passionSkills.Any())
                    return "RICS.PASSION.ListNoPassions".Translate(pawn.Name.ToString());

                passionSkills.Sort();

                return "RICS.PASSION.ListPassions".Translate(pawn.Name.ToString(), string.Join(", ", passionSkills));
            }
            catch (Exception ex)
            {
                Logger.Error($"Error listing passions: {ex.Message}");
                return "RICS.PASSION.ListError".Translate(pawn.Name.ToString());
            }
        }

        private static string GetPassionEmoji(RimWorld.Passion passion)
        {
            return passion switch
            {
                RimWorld.Passion.Major => " 🔥🔥",
                RimWorld.Passion.Minor => " 🔥",
                _ => ""
            };
        }

        private static bool TryParseSkill(string skillName, out SkillDef skillDef)
        {
            skillDef = DefDatabase<SkillDef>.AllDefs.FirstOrDefault(s =>
                s.defName.Equals(skillName, StringComparison.OrdinalIgnoreCase) ||
                s.LabelCap.ToString().Equals(skillName, StringComparison.OrdinalIgnoreCase));

            return skillDef != null;
        }

        /// <summary>
        /// Parses wager + optional skill from any argument order.
        /// Supports: !passion 5000 melee, !passion melee 5000, !passion 500 (random)
        /// </summary>
        private static (int? wager, SkillDef targetSkill) ParseWagerAndSkill(string[] args)
        {
            int? wager = null;
            SkillDef skill = null;

            foreach (string arg in args)
            {
                if (int.TryParse(arg, out int num) && num > 0)
                {
                    if (wager == null)
                        wager = num;
                }
                else if (TryParseSkill(arg, out SkillDef parsedSkill))
                {
                    if (skill == null)
                        skill = parsedSkill;
                }
            }

            // If only one arg and it's a number → random mode
            if (wager != null && skill == null && args.Length == 1)
                return (wager, null);

            // If we have both → targeted
            if (wager != null && skill != null)
                return (wager, skill);

            return (null, null);
        }
    }
}