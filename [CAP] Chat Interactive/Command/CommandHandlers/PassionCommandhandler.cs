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
        // In PassionCommandhandler.cs - Replace HandlePassionCommand method
        internal static string HandlePassionCommand(ChatMessageWrapper messageWrapper, string[] args)
        {
            try
            {
                // Get the viewer
                var viewer = Viewers.GetViewer(messageWrapper);
                if (viewer == null)
                    return "❌ Viewer not found.";

                // Check if viewer has a pawn assigned
                Verse.Pawn pawn = PawnItemHelper.GetViewerPawn(messageWrapper);

                if (pawn == null)
                    return "❌ You need an assigned pawn to use !passion. Use !join to get in the queue.";

                if (pawn.Dead)
                    return "❌ Your pawn is dead! Wait for resurrection or get a new pawn.";

                // Handle "list" subcommand
                if (args.Length > 0 && args[0].ToLower() == "list")
                {
                    return ListPawnPassions(pawn);
                }

                // Parse arguments for skill targeting
                if (args.Length < 1)
                    return "💡 Usage: !passion <skill> <coins> OR !passion <coins> OR !passion list";

                SkillDef targetSkill = null;
                int wager;

                // Check if first arg is a skill name
                if (args.Length >= 2 && TryParseSkill(args[0], out targetSkill))
                {
                    // Format: !passion <skill> <wager>
                    if (!int.TryParse(args[1], out wager) || wager <= 0)
                        return "💡 Usage: !passion <skill> <coins> - Wager coins to upgrade a specific skill's passion";
                }
                else
                {
                    // Format: !passion <wager> (random skill)
                    if (!int.TryParse(args[0], out wager) || wager <= 0)
                        return "💡 Usage: !passion <coins> - Wager coins to upgrade a random skill's passion";
                }

                // Validate target skill if specified
                if (targetSkill != null)
                {
                    var pawnSkill = pawn.skills.GetSkill(targetSkill);
                    if (pawnSkill == null)
                        return $"❌ Your pawn doesn't have the {targetSkill.LabelCap} skill.";

                    if (pawnSkill.passion == RimWorld.Passion.Major)
                        return $"❌ Your pawn already has major passion in {targetSkill.LabelCap}.";
                }

                // Check if viewer has enough coins
                if (viewer.Coins < wager)
                    return $"❌ You only have {viewer.Coins} coins. You need {wager} coins to wager.";

                // Validate wager limits
                var settings = CAPChatInteractiveMod.Instance.Settings.GlobalSettings;
                if (wager < settings.MinPassionWager)
                    return $"❌ Minimum wager is {settings.MinPassionWager} coins.";

                if (wager > settings.MaxPassionWager)
                    return $"❌ Maximum wager is {settings.MaxPassionWager} coins.";

                // Execute passion gamble
                var result = PassionSystem.GambleForPassion(pawn, wager, viewer, targetSkill);

                // Deduct coins if not already handled in the result
                if (!result.alreadyCharged)
                {
                    viewer.TakeCoins(wager);
                }

                // Save viewer data
                Viewers.SaveViewers();

                return result.message;
            }
            catch (Exception ex)
            {
                Logger.Error($"Error in passion command: {ex.Message}");
                return "❌ An error occurred while processing your passion wager.";
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
                    return $"📝 {pawn.Name} has no passions yet.";

                // Just sort the string list instead of the SkillRecords
                passionSkills.Sort();

                return $"📝 {pawn.Name}'s passions: {string.Join(", ", passionSkills)}";
            }
            catch (Exception ex)
            {
                Logger.Error($"Error listing passions: {ex.Message}");
                return $"❌ Error listing passions for {pawn.Name}";
            }
        }

        // Add this method to match your mypawn command style
        private static string GetPassionEmoji(RimWorld.Passion passion)
        {
            return passion switch
            {
                RimWorld.Passion.Major => " 🔥🔥", // Burning passion
                RimWorld.Passion.Minor => " 🔥",   // Minor passion  
                _ => ""                  // No passion
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