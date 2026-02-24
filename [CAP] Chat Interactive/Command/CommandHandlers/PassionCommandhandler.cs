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
                    return "RICS.PASSION.PawnDead".Translate();

                if (args.Length > 0 && args[0].ToLower() == "list")
                {
                    return ListPawnPassions(pawn);
                }

                if (args.Length < 1)
                    return "RICS.PASSION.Usage".Translate();

                SkillDef targetSkill = null;
                int wager;

                if (args.Length >= 2 && TryParseSkill(args[0], out targetSkill))
                {
                    if (!int.TryParse(args[1], out wager) || wager <= 0)
                        return "RICS.PASSION.UsageTargeted".Translate();
                }
                else
                {
                    if (!int.TryParse(args[0], out wager) || wager <= 0)
                        return "RICS.PASSION.UsageRandom".Translate();
                }

                if (targetSkill != null)
                {
                    var pawnSkill = pawn.skills.GetSkill(targetSkill);
                    if (pawnSkill == null)
                        return "RICS.PASSION.NoSkill".Translate(targetSkill.LabelCap);

                    if (pawnSkill.passion == RimWorld.Passion.Major)
                        return "RICS.PASSION.AlreadyMajorTargeted".Translate(targetSkill.LabelCap);
                }

                if (viewer.Coins < wager)
                    return "RICS.PASSION.NotEnoughCoins".Translate(viewer.Coins, wager);

                var settings = CAPChatInteractiveMod.Instance.Settings.GlobalSettings;
                if (wager < settings.MinPassionWager)
                    return "RICS.PASSION.MinWager".Translate(settings.MinPassionWager);

                if (wager > settings.MaxPassionWager)
                    return "RICS.PASSION.MaxWager".Translate(settings.MaxPassionWager);

                var result = PassionSystem.GambleForPassion(pawn, wager, viewer, targetSkill);

                if (!result.alreadyCharged)
                {
                    viewer.TakeCoins(wager);
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
    }
}