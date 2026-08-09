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
// !passion [list] | !passion <wager> [skill] — gamble for skill passion
using _CAP__Chat_Interactive.Command.CommandHelpers;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace CAP_ChatInteractive.Commands.ViewerCommands
{
    internal static class PassionCommandhandler
    {
        private const string ReturnDivider = " | ";

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

                if (pawn.Destroyed || pawn.Dead)
                {
                    var deathInfo = GameComponent_PawnAssignmentManager.GetPawnDeathInfo(pawn);
                    return "RICS.PASSION.PawnDead".Translate()
                           + ReturnDivider
                           + "RICS.Return.PawnDeadReason".Translate(deathInfo.ToString());
                }

                args = args ?? Array.Empty<string>();

                if (args.Length > 0 && args[0].Equals("list", StringComparison.OrdinalIgnoreCase))
                    return ListPawnPassions(pawn);

                if (args.Length == 0)
                    return "RICS.PASSION.Usage".Translate();

                // !passion <wager> <skill> OR !passion <skill> <wager> OR !passion <wager>
                (int? wager, SkillDef targetSkill) = ParseWagerAndSkill(args);
                if (wager == null)
                    return "RICS.PASSION.Usage".Translate();

                int finalWager = wager.Value;

                var cmdSettings = CommandSettingsManager.GetSettings("passion");
                int minW = cmdSettings.GetCustom("minPassionWager", 500);
                int maxW = cmdSettings.GetCustom("maxPassionWager", 1000);

                if (finalWager < minW)
                    return "RICS.PASSION.MinWager".Translate(minW);

                if (finalWager > maxW)
                    return "RICS.PASSION.MaxWager".Translate(maxW);

                if (viewer.Coins < finalWager)
                    return "RICS.PASSION.NotEnoughCoins".Translate(viewer.Coins, finalWager);

                var result = PassionSystem.GambleForPassion(pawn, finalWager, viewer, targetSkill);

                if (!result.alreadyCharged)
                    viewer.TakeCoins(finalWager);

                Viewers.SaveViewers();
                return result.message;
            }
            catch (Exception ex)
            {
                Logger.Error($"[Passion] Error in passion command: {ex}");
                return "RICS.PASSION.GeneralError".Translate();
            }
        }

        private static string ListPawnPassions(Verse.Pawn pawn)
        {
            try
            {
                if (pawn?.skills?.skills == null)
                    return "RICS.PASSION.ListNoPassions".Translate(pawn?.Name?.ToString() ?? "pawn");

                var passionSkills = new List<string>();
                foreach (var skill in pawn.skills.skills)
                {
                    if (skill?.passion != RimWorld.Passion.None && skill.def != null)
                        passionSkills.Add($"{skill.def.LabelCap}{GetPassionEmoji(skill.passion)}");
                }

                if (!passionSkills.Any())
                    return "RICS.PASSION.ListNoPassions".Translate(pawn.Name.ToString());

                passionSkills.Sort();
                return "RICS.PASSION.ListPassions".Translate(pawn.Name.ToString(), string.Join(", ", passionSkills));
            }
            catch (Exception ex)
            {
                Logger.Error($"[Passion] Error listing passions: {ex}");
                return "RICS.PASSION.ListError".Translate(pawn?.Name?.ToString() ?? "pawn");
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
        /// Supports: !passion 5000 melee, !passion melee 5000, !passion 500 (random skill).
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

            if (wager != null && skill == null && args.Length == 1)
                return (wager, null);

            if (wager != null && skill != null)
                return (wager, skill);

            // wager only with extra unrecognized tokens → treat as random (usage if no wager)
            if (wager != null)
                return (wager, skill);

            return (null, null);
        }
    }
}
