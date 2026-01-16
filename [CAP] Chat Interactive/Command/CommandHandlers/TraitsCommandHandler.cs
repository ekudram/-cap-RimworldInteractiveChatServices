// TraitsCommandHandler.cs
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
        public static string HandleLookupTraitCommand(ChatMessageWrapper messageWrapper, string[] args)
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

        public static string HandleAddTraitCommand(ChatMessageWrapper messageWrapper, string[] args)
        {
            try
            {
                if (args.Length == 0)
                {
                    return "Usage: !addtrait [trait_name] - Add a trait to your pawn.";
                }

                // Get the viewer and their pawn
                var viewer = Viewers.GetViewer(messageWrapper);
                if (viewer == null)
                {
                    return "Could not find your viewer data.";
                }

                var assignmentManager = CAPChatInteractiveMod.GetPawnAssignmentManager();
                Pawn pawn = assignmentManager.GetAssignedPawn(messageWrapper);

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

        public static string HandleRemoveTraitCommand(ChatMessageWrapper messageWrapper, string[] args)
        {
            try
            {
                if (args.Length == 0)
                {
                    return "Usage: !removetrait <trait_name> - Remove a trait from your pawn.";
                }

                // Get the viewer and their pawn
                var viewer = Viewers.GetViewer(messageWrapper);
                if (viewer == null)
                {
                    return "Could not find your viewer data.";
                }

                var assignmentManager = CAPChatInteractiveMod.GetPawnAssignmentManager();
                Pawn pawn = assignmentManager.GetAssignedPawn(messageWrapper);

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

                // NEW CHECK: Prevent removal of forced traits (e.g., from genes)
                if (existingTrait.sourceGene != null || existingTrait.ScenForced)
                {
                    return $"❌ The trait '{buyableTrait.Name}' is forced (e.g., from genes) and cannot be removed.";
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

        public static string HandleReplaceTraitCommand(ChatMessageWrapper messageWrapper, string[] args)
        {
            try
            {
                if (args.Length < 2)
                {
                    return "Usage: !replacetrait <old_trait_name> <new_trait_name> - Replace one trait with another on your pawn.";
                }

                // Get the viewer and their pawn
                var viewer = Viewers.GetViewer(messageWrapper);
                if (viewer == null)
                {
                    return "Could not find your viewer data.";
                }

                var assignmentManager = CAPChatInteractiveMod.GetPawnAssignmentManager();
                Pawn pawn = assignmentManager.GetAssignedPawn(messageWrapper);

                if (pawn == null || pawn.Dead)
                {
                    return "You don't have an active pawn in the colony. Use !pawn to purchase one!";
                }

                // SIMPLE PARSING: First arg = old trait, rest = new trait
                //string oldTraitName = args[0].ToLower();
                //string newTraitName = string.Join(" ", args.Skip(1)).ToLower();

                string oldTraitName = ParseTraitNames(args, out string newTraitName);

                if (string.IsNullOrEmpty(oldTraitName) || string.IsNullOrEmpty(newTraitName))
                {
                    return "Could not parse trait names. Try: !replacetrait \"old trait\" \"new trait\"";
                }

                Logger.Debug($"ReplaceTrait: old='{oldTraitName}', new='{newTraitName}'");

                // Find the traits in the database
                var oldBuyableTrait = FindBuyableTrait(oldTraitName);
                var newBuyableTrait = FindBuyableTrait(newTraitName);

                if (oldBuyableTrait == null)
                {
                    return $"Trait '{oldTraitName}' not found. Use !traits to see available traits.";
                }

                if (newBuyableTrait == null)
                {
                    return $"Trait '{newTraitName}' not found. Use !traits to see available traits.";
                }

                // Check if old trait can be removed
                if (!oldBuyableTrait.CanRemove)
                {
                    return $"The trait '{oldBuyableTrait.Name}' cannot be removed from pawns.";
                }

                // Check if new trait can be added
                if (!newBuyableTrait.CanAdd)
                {
                    return $"The trait '{newBuyableTrait.Name}' cannot be added to pawns.";
                }

                // Get the TraitDefs
                TraitDef oldTraitDef = DefDatabase<TraitDef>.GetNamedSilentFail(oldBuyableTrait.DefName);
                TraitDef newTraitDef = DefDatabase<TraitDef>.GetNamedSilentFail(newBuyableTrait.DefName);

                if (oldTraitDef == null)
                {
                    return $"Error: Trait definition for '{oldBuyableTrait.Name}' not found.";
                }

                if (newTraitDef == null)
                {
                    return $"Error: Trait definition for '{newBuyableTrait.Name}' not found.";
                }

                // Check if pawn has the old trait
                var existingTrait = pawn.story.traits.allTraits.FirstOrDefault(t =>
                    t.def.defName == oldBuyableTrait.DefName && t.Degree == oldBuyableTrait.Degree);

                if (existingTrait == null)
                {
                    return $"Your pawn does not have the trait '{oldBuyableTrait.Name}'.";
                }

                // NEW CHECK: Prevent removal of forced traits (e.g., from genes)
                if (existingTrait.sourceGene != null || existingTrait.ScenForced)
                {
                    return $"❌ The trait '{oldBuyableTrait.Name}' is forced (e.g., from genes) and cannot be replaced.";
                }

                // Check if pawn already has the new trait (different from old trait)
                if (oldBuyableTrait.DefName != newBuyableTrait.DefName || oldBuyableTrait.Degree != newBuyableTrait.Degree)
                {
                    if (pawn.story.traits.allTraits.Any(t =>
                        t.def.defName == newBuyableTrait.DefName && t.Degree == newBuyableTrait.Degree))
                    {
                        return $"Your pawn already has the trait '{newBuyableTrait.Name}'.";
                    }
                }

                // Check for conflicts with other existing traits (excluding the one being replaced)
                var otherTraits = pawn.story.traits.allTraits.Where(t => t != existingTrait).ToList();
                foreach (var otherTrait in otherTraits)
                {
                    if (newTraitDef.ConflictsWith(otherTrait) || otherTrait.def.ConflictsWith(newTraitDef))
                    {
                        return $"❌ {newBuyableTrait.Name} conflicts with your pawn's existing trait {otherTrait.Label}.";
                    }
                }

                // Calculate total cost
                int totalCost = oldBuyableTrait.RemovePrice + newBuyableTrait.AddPrice;

                // Check viewer's coins
                if (viewer.Coins < totalCost)
                {
                    return $"You need {totalCost} coins to replace {oldBuyableTrait.Name} with {newBuyableTrait.Name}, but you only have {viewer.Coins} coins.";
                }

                // Remove old trait
                pawn.story.traits.RemoveTrait(existingTrait);

                // Add new trait
                Trait newTrait = new Trait(newTraitDef, newBuyableTrait.Degree, false);
                pawn.story.traits.GainTrait(newTrait);

                // Deduct coins
                viewer.TakeCoins(totalCost);

                return $"✅ Replaced trait '{oldBuyableTrait.Name}' with '{newBuyableTrait.Name}' on {pawn.Name} for {totalCost} coins!";
            }
            catch (Exception ex)
            {
                Logger.Error($"Error in ReplaceTrait command handler: {ex}");
                return "An error occurred while replacing the trait.";
            }
        }

        public static string HandleSetTraitsCommand(ChatMessageWrapper messageWrapper, string[] args)
        {
            try
            {
                if (args.Length < 1)
                {
                    return "Usage: !settraits <trait_name> <trait_name>. Add and replace traits in bulk.";
                }

                var viewer = Viewers.GetViewer(messageWrapper);
                if (viewer == null)
                {
                    return "Could not find your viewer data.";
                }

                var assignmentManager = CAPChatInteractiveMod.GetPawnAssignmentManager();
                Pawn pawn = assignmentManager.GetAssignedPawn(messageWrapper);

                if (pawn == null || pawn.Dead)
                {
                    return "You don't have an active pawn in the colony. Use !pawn to purchase one!";
                }

                // Step 1: Find and validate all requested traits
                var resolvedTraits = new List<BuyableTrait>();
                int bypassCount = 0;

                for (int i = 0; i < args.Length; i++)
                {
                    BuyableTrait trait = TraitsManager.AllBuyableTraits.Values
                        .FirstOrDefault(t =>
                            t.Name.ToLower() == args[i].ToLower() ||
                            t.DefName.ToLower() == args[i].ToLower());

                    // Try joining with next word if single word didn't match
                    if (trait == null && i + 1 < args.Length)
                    {
                        string joined = $"{args[i]} {args[i + 1]}".ToLower();
                        trait = TraitsManager.AllBuyableTraits.Values
                            .FirstOrDefault(t =>
                                t.Name.ToLower() == joined ||
                                t.DefName.ToLower() == joined);

                        if (trait != null)
                            i++; // consume second word
                    }

                    if (trait == null)
                    {
                        return $"Trait '{args[i]}' not found. Use !traits to see available traits.";
                    }

                    if (!trait.CanAdd)
                    {
                        return $"The trait '{trait.Name}' cannot be added to pawns.";
                    }

                    if (trait.BypassLimit == true)
                        bypassCount++;

                    resolvedTraits.Add(trait);
                }

                // Step 2: Check for conflicts within requested traits (bidirectional)
                for (int i = 0; i < resolvedTraits.Count; i++)
                {
                    var traitA = resolvedTraits[i];
                    var traitDefA = DefDatabase<TraitDef>.GetNamedSilentFail(traitA.DefName);

                    for (int j = i + 1; j < resolvedTraits.Count; j++)
                    {
                        var traitB = resolvedTraits[j];
                        var traitDefB = DefDatabase<TraitDef>.GetNamedSilentFail(traitB.DefName);

                        // Check both directions
                        if (traitA.Conflicts.Any(c => c.Equals(traitB.Name, StringComparison.OrdinalIgnoreCase)) || traitB.Conflicts.Any(c => c.Equals(traitA.Name, StringComparison.OrdinalIgnoreCase)) ||
                            (traitDefA != null && traitDefB != null && traitDefA.ConflictsWith(traitDefB)) ||
                            (traitDefA.defName == traitDefB.defName))
                        {
                            return $"❌ {traitA.Name} conflicts with {traitB.Name}.";
                        }
                    }
                }

                // Step 3: Identify forced and unremovable traits from existing traits
                var forcedList = pawn.story.traits.allTraits
                    .Where(t => t.ScenForced || t.sourceGene != null)
                    .ToList();

                var unremovableList = pawn.story.traits.allTraits
                    .Where(existing =>
                    {
                        var buyable = TraitsManager.AllBuyableTraits.Values
                            .FirstOrDefault(t => t.DefName == existing.def.defName && t.Degree == existing.Degree);
                        return buyable != null && !buyable.CanRemove && !forcedList.Contains(existing);
                    })
                    .ToList();

                var protectedTraits = forcedList.Concat(unremovableList)
                    .Select(existing => TraitsManager.AllBuyableTraits.Values
                        .FirstOrDefault(bt => bt.DefName == existing.def.defName && bt.Degree == existing.Degree))
                    .Where(bt => bt != null)
                    .ToList();

                // Step 4: Check for conflicts with protected traits
                foreach (var requestedTrait in resolvedTraits)
                {
                    var traitDefA = DefDatabase<TraitDef>.GetNamedSilentFail(requestedTrait.DefName);
                    foreach (var protectedTrait in protectedTraits)
                    {
                        var traitDefB = DefDatabase<TraitDef>.GetNamedSilentFail(protectedTrait.DefName);
                        if (requestedTrait.Conflicts.Any(c => c.Equals(protectedTrait.Name, StringComparison.OrdinalIgnoreCase)) ||
                            protectedTrait.Conflicts.Any(c => c.Equals(requestedTrait.Name, StringComparison.OrdinalIgnoreCase)) ||
                            (traitDefA != null && traitDefB != null && traitDefA.ConflictsWith(traitDefB)) ||
                            (traitDefA.defName == traitDefB.defName))
                        {
                            return $"❌ {requestedTrait.Name} conflicts with protected trait {protectedTrait.Name}.";
                        }
                    }
                }

                // Step 5: Calculate effective max traits (accounting for protected traits)
                var settings = CAPChatInteractiveMod.Instance.Settings.GlobalSettings;
                int maxTraits = settings?.MaxTraits ?? 4;
                int protectedCount = forcedList.Count + unremovableList.Count;
                int effectiveMax = maxTraits - protectedCount;

                int requestedCount = resolvedTraits.Count - bypassCount;

                if (requestedCount > effectiveMax)
                {
                    return $"Too many traits ({requestedCount}). With {protectedCount} protected traits, your max is {effectiveMax}.";
                }

                // Step 6: Remove overlaps (traits already on pawn that are also requested)
                var existingTraits = pawn.story.traits.allTraits
                    .Where(existing => !resolvedTraits.Any(rt =>
                        rt.DefName == existing.def.defName && rt.Degree == existing.Degree))
                    .ToList();

                resolvedTraits = resolvedTraits
                    .Where(rt => !pawn.story.traits.allTraits.Any(et =>
                        et.def.defName == rt.DefName && et.Degree == rt.Degree))
                    .ToList();

                // Step 7: Determine which traits to remove (only removable ones)
                var removableTraits = existingTraits
                    .Except(forcedList)
                    .Except(unremovableList)
                    .ToList();

                // Step 8: Calculate cost
                int totalCost = 0;

                // Cost to remove traits that will be removed
                foreach (var t in removableTraits)
                {
                    BuyableTrait bT = TraitsManager.AllBuyableTraits.Values
                        .FirstOrDefault(bt => bt.DefName == t.def.defName && bt.Degree == t.Degree);
                    if (bT != null)
                        totalCost += bT.RemovePrice;
                }

                // Cost to add new traits
                foreach (var t in resolvedTraits)
                {
                    totalCost += t.AddPrice;
                }

                // Step 9: Check if user can afford
                if (viewer.Coins < totalCost)
                {
                    return $"You need {totalCost} coins, but you only have {viewer.Coins} coins.";
                }

                // Step 10: Apply changes
                // Remove all removable traits
                foreach (var t in removableTraits)
                {
                    pawn.story.traits.RemoveTrait(t);
                }

                // Add new traits
                foreach (var t in resolvedTraits)
                {
                    TraitDef newTraitDef = DefDatabase<TraitDef>.GetNamedSilentFail(t.DefName);
                    if (newTraitDef == null)
                    {
                        return $"Error: Trait definition for '{t.Name}' not found.";
                    }
                    Trait newTrait = new Trait(newTraitDef, t.Degree, false);
                    pawn.story.traits.GainTrait(newTrait);
                }

                // Deduct coins
                viewer.TakeCoins(totalCost);

                return $"✅ Set new traits: {string.Join(", ", resolvedTraits.Select(t => t.Name))} for {totalCost} coins";
            }
            catch (Exception ex)
            {
                Logger.Error($"Error in SetTraits command handler: {ex}");
                return "An error occurred while setting traits.";
            }
        }

        // Helper method to parse trait names (add this as a private static method)
        private static string ParseTraitNames(string[] args, out string newTraitName)
        {
            newTraitName = null;

            if (args.Length == 2)
            {
                // Simple case: !replacetrait greedy jogger
                newTraitName = args[1].ToLower();
                return args[0].ToLower();
            }

            // Complex case with multi-word trait names
            // Strategy: Find the split point by checking all possible combinations
            for (int splitPoint = 1; splitPoint < args.Length; splitPoint++)
            {
                string potentialOldTrait = string.Join(" ", args.Take(splitPoint));
                string potentialNewTrait = string.Join(" ", args.Skip(splitPoint));

                // Check if both are valid traits
                var oldTrait = FindBuyableTrait(potentialOldTrait);
                var newTrait = FindBuyableTrait(potentialNewTrait);

                if (oldTrait != null && newTrait != null)
                {
                    newTraitName = potentialNewTrait.ToLower();
                    return potentialOldTrait.ToLower();
                }
            }

            // Fallback: If we can't find a clear split, assume first word is old trait, rest is new trait
            if (args.Length > 1)
            {
                string potentialOldTrait = args[0];
                string potentialNewTrait = string.Join(" ", args.Skip(1));

                var oldTrait = FindBuyableTrait(potentialOldTrait);
                var newTrait = FindBuyableTrait(potentialNewTrait);

                if (oldTrait != null)
                {
                    newTraitName = potentialNewTrait.ToLower();
                    return potentialOldTrait.ToLower();
                }
            }

            return null;
        }

        public static string HandleListTraitsCommand(ChatMessageWrapper messageWrapper, string[] args)
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
