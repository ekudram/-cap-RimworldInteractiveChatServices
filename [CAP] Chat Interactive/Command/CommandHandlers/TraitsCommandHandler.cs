// TraitsCommandHandler.cs
// Copyright (c) Captolamia. All rights reserved.
// Licensed under the AGPLv3 License. See LICENSE file in the project root for full license information.
//
// Handles trait-related commands: !trait, !addtrait, !removetrait, !traits
using CAP_ChatInteractive;
using CAP_ChatInteractive.Traits;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Verse;

namespace CAP_ChatInteractive.Commands.CommandHandlers
{
    public static class TraitsCommandHandler
    {
        public static string HandleLookupTraitCommand(ChatMessageWrapper user, string[] args)
        {
            try
            {
                if (args.Length == 0)
                {
                    return "Usage: !trait <trait_name> - Look up information about a specific trait. Can also use !lookup trait to search for a trait";
                }

                string traitName = string.Join(" ", args).ToLower();
                var buyableTrait = FindBuyableTrait(traitName);

                if (buyableTrait == null)
                {
                    return $"Trait '{string.Join(" ", args)}' not found. Use !traits to see available traits.";
                }

                return FormatTraitInfoSimple(buyableTrait);
            }
            catch (Exception ex)
            {
                Logger.Error($"Error in LookupTrait command handler: {ex}");
                return "An error occurred while looking up the trait.";
            }
        }

        private static string FormatTraitInfoSimple(BuyableTrait buyableTrait)
        {
            var sb = new StringBuilder();
            sb.Append($"📖 {buyableTrait.Name}: ");
            sb.Append($"Add - {buyableTrait.AddPrice} coins, ");
            sb.Append($"Remove - {buyableTrait.RemovePrice} coins");

            // Add truncated description if available
            if (!string.IsNullOrEmpty(buyableTrait.Description))
            {
                string cleanDescription = Dialog_TraitsEditor.ReplacePawnVariables(buyableTrait.Description);
                string truncatedDesc = TruncateDescription(cleanDescription, 200); // Leave room for the rest
                sb.Append($" | {truncatedDesc}");
            }

            return sb.ToString();
        }

        private static string TruncateDescription(string description, int maxLength)
        {
            if (string.IsNullOrEmpty(description) || description.Length <= maxLength)
                return description;

            // Find the last space before maxLength to avoid breaking words
            int lastSpace = description.LastIndexOf(' ', maxLength - 3);
            if (lastSpace > 0)
            {
                return description.Substring(0, lastSpace) + "...";
            }

            return description.Substring(0, maxLength - 3) + "...";
        }

        public static string HandleAddTraitCommand(ChatMessageWrapper user, string[] args)
        {
            try
            {
                if (args.Length == 0)
                {
                    return "Usage: !addtrait <trait_name> - Add a trait to your pawn.";
                }

                // Get the viewer and their pawn
                var viewer = Viewers.GetViewer(user);
                if (viewer == null)
                {
                    return "Could not find your viewer data.";
                }

                var assignmentManager = CAPChatInteractiveMod.GetPawnAssignmentManager();
                Pawn pawn = assignmentManager.GetAssignedPawn(user);

                if (pawn == null || pawn.Dead)
                {
                    return "You don't have an active pawn in the colony. Use !pawn to purchase one!";
                }

                string traitName = string.Join(" ", args).ToLower();
                var buyableTrait = FindBuyableTrait(traitName);

                if (buyableTrait == null)
                {
                    return $"Trait '{string.Join(" ", args)}' not found. Use !traits to see available traits.";
                }

                // Check if trait can be added
                if (!buyableTrait.CanAdd)
                {
                    return $"The trait '{buyableTrait.Name}' cannot be added to pawns.";
                }

                // Check if pawn already has max traits (unless trait bypasses limit)
                var settings = CAPChatInteractiveMod.Instance.Settings.GlobalSettings;
                int maxTraits = settings?.MaxTraits ?? 4;
                if (pawn.story.traits.allTraits.Count >= maxTraits && !buyableTrait.BypassLimit)
                {
                    return $"Your pawn already has the maximum of {maxTraits} traits. Use !removetrait to remove one first.";
                }

                // Check if pawn already has this trait - get the TraitDef first
                TraitDef traitDef = DefDatabase<TraitDef>.GetNamedSilentFail(buyableTrait.DefName);
                if (traitDef != null && pawn.story.traits.HasTrait(traitDef))
                {
                    return $"Your pawn already has the trait '{buyableTrait.Name}'.";
                }

                // Check for conflicts with existing traits
                string conflictCheck = CheckTraitConflicts(pawn, buyableTrait);
                if (!string.IsNullOrEmpty(conflictCheck))
                {
                    return conflictCheck;
                }

                // Check viewer's coins
                int traitCost = buyableTrait.AddPrice;
                if (viewer.Coins < traitCost)
                {
                    return $"You need {traitCost} coins to add this trait, but you only have {viewer.Coins} coins.";
                }

                // Add the trait - get the TraitDef from DefName
                if (traitDef == null)
                {
                    return $"Error: Trait definition for '{buyableTrait.Name}' not found.";
                }

                Trait newTrait = new Trait(traitDef, buyableTrait.Degree, false);
                pawn.story.traits.GainTrait(newTrait);

                // Apply any skill adjustments from the trait
                ApplyTraitSkillEffects(pawn, buyableTrait);

                // Deduct coins
                viewer.TakeCoins(traitCost);

                return $"✅ Added trait '{buyableTrait.Name}' to {pawn.Name} for {traitCost} coins!";
            }
            catch (Exception ex)
            {
                Logger.Error($"Error in AddTrait command handler: {ex}");
                return "An error occurred while adding the trait.";
            }
        }

        public static string HandleRemoveTraitCommand(ChatMessageWrapper user, string[] args)
        {
            try
            {
                if (args.Length == 0)
                {
                    return "Usage: !removetrait <trait_name> - Remove a trait from your pawn.";
                }

                // Get the viewer and their pawn
                var viewer = Viewers.GetViewer(user);
                if (viewer == null)
                {
                    return "Could not find your viewer data.";
                }

                var assignmentManager = CAPChatInteractiveMod.GetPawnAssignmentManager();
                Pawn pawn = assignmentManager.GetAssignedPawn(user);

                if (pawn == null || pawn.Dead)
                {
                    return "You don't have an active pawn in the colony. Use !pawn to purchase one!";
                }

                string traitName = string.Join(" ", args).ToLower();
                var buyableTrait = FindBuyableTrait(traitName);

                if (buyableTrait == null)
                {
                    return $"Trait '{string.Join(" ", args)}' not found. Use !traits to see available traits.";
                }

                // Check if trait can be removed
                if (!buyableTrait.CanRemove)
                {
                    return $"The trait '{buyableTrait.Name}' cannot be removed from pawns.";
                }
                // Check if pawn has this trait - get the TraitDef first  
                TraitDef removeTraitDef = DefDatabase<TraitDef>.GetNamedSilentFail(buyableTrait.DefName);
                var existingTrait = pawn.story.traits.allTraits.FirstOrDefault(t =>
                    t.def.defName == buyableTrait.DefName && t.Degree == buyableTrait.Degree);
                if (existingTrait == null)
                {
                    return $"Your pawn does not have the trait '{buyableTrait.Name}'.";
                }

                // Check viewer's coins
                int removeCost = buyableTrait.RemovePrice;
                if (viewer.Coins < removeCost)
                {
                    return $"You need {removeCost} coins to remove this trait, but you only have {viewer.Coins} coins.";
                }

                // Remove the trait
                pawn.story.traits.RemoveTrait(existingTrait);

                // Remove any skill adjustments from the trait
                RemoveTraitSkillEffects(pawn, buyableTrait);

                // Deduct coins
                viewer.TakeCoins(removeCost);

                return $"✅ Removed trait '{buyableTrait.Name}' from {pawn.Name} for {removeCost} coins!";
            }
            catch (Exception ex)
            {
                Logger.Error($"Error in RemoveTrait command handler: {ex}");
                return "An error occurred while removing the trait.";
            }
        }

        public static string HandleListTraitsCommand(ChatMessageWrapper user, string[] args)
        {
            try
            {
                var enabledTraits = TraitsManager.GetEnabledTraits().ToList();

                if (!enabledTraits.Any())
                {
                    return "No traits are currently available.";
                }

                var response = new StringBuilder();
                response.AppendLine("📋 Available Traits:");

                // Group by mod source for better organization
                var traitsByMod = enabledTraits.GroupBy(t => t.ModSource)
                                              .OrderBy(g => g.Key);

                foreach (var modGroup in traitsByMod)
                {
                    response.AppendLine($"\n📁 {modGroup.Key}:");

                    var traitList = modGroup.Select(t => t.Name)
                                          .OrderBy(label => label)
                                          .Take(10); // Limit per mod to avoid message spam

                    response.AppendLine(string.Join(", ", traitList));

                    if (modGroup.Count() > 10)
                    {
                        response.AppendLine($"... and {modGroup.Count() - 10} more traits");
                    }
                }

                response.AppendLine($"\n💡 Use !trait <name> for details, !addtrait <name> to add, !removetrait <name> to remove");

                return response.ToString();
            }
            catch (Exception ex)
            {
                Logger.Error($"Error in ListTraits command handler: {ex}");
                return "An error occurred while listing traits.";
            }
        }

        private static BuyableTrait FindBuyableTrait(string searchTerm)
        {
            return TraitsManager.AllBuyableTraits.Values
                .FirstOrDefault(trait =>
                    trait.Name.ToLower().Contains(searchTerm) ||
                    trait.DefName.ToLower().Contains(searchTerm));
        }

        private static string CheckTraitConflicts(Pawn pawn, BuyableTrait newTrait)
        {
            // Get the TraitDef from the DefName
            TraitDef newTraitDef = DefDatabase<TraitDef>.GetNamedSilentFail(newTrait.DefName);
            if (newTraitDef == null) return null;

            foreach (var existingTrait in pawn.story.traits.allTraits)
            {
                if (newTraitDef.ConflictsWith(existingTrait) || existingTrait.def.ConflictsWith(newTraitDef))
                {
                    return $"❌ {newTrait.Name} conflicts with your pawn's existing trait {existingTrait.Label}.";
                }
            }
            return null;
        }

        private static void ApplyTraitSkillEffects(Pawn pawn, BuyableTrait buyableTrait)
        {
            // RimWorld automatically applies skill gains when traits are added
            // No need for manual skill adjustment - the trait system handles this
            Logger.Debug($"Trait {buyableTrait.Name} added to {pawn.Name} - skill effects applied automatically by RimWorld");
        }

        private static void RemoveTraitSkillEffects(Pawn pawn, BuyableTrait buyableTrait)
        {
            // RimWorld automatically removes skill effects when traits are removed
            // No need for manual skill adjustment - the trait system handles this
            Logger.Debug($"Trait {buyableTrait.Name} removed from {pawn.Name} - skill effects removed automatically by RimWorld");
        }
    }
}
