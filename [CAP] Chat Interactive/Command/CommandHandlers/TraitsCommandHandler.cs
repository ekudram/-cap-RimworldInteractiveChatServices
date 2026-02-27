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
                    return "RICS.TCH.Lookup.Usage".Translate();
                }

                string traitName = string.Join(" ", args).ToLowerInvariant();
                var buyableTrait = FindBuyableTrait(traitName);

                if (buyableTrait == null)
                {
                    return "RICS.TCH.Lookup.TraitNotFound".Translate(string.Join(" ", args));
                }

                return FormatTraitInfoSimple(buyableTrait);
            }
            catch (Exception ex)
            {
                Logger.Error($"Error in LookupTrait command handler: {ex}");
                return "RICS.TCH.Error".Translate();
            }
        }

        private static string FormatTraitInfoSimple(BuyableTrait buyableTrait)
        {
            var settings = CAPChatInteractiveMod.Instance.Settings.GlobalSettings;
            var currencySymbol = settings.CurrencyName?.Trim() ?? "¢";

            // Base format without description
            string baseFormat = "RICS.TCH.SimpleInfo.Format".Translate(
                buyableTrait.Name,
                buyableTrait.AddPrice,
                buyableTrait.RemovePrice,
                currencySymbol
            );

            if (!string.IsNullOrEmpty(buyableTrait.Description))
            {
                string cleanDescription = Dialog_TraitsEditor.ReplacePawnVariables(buyableTrait.Description);
                string truncatedDesc = TruncateDescription(cleanDescription, 200);

                return "RICS.TCH.SimpleInfo.WithDescription".Translate(
                    buyableTrait.Name,
                    buyableTrait.AddPrice,
                    buyableTrait.RemovePrice,
                    currencySymbol,
                    truncatedDesc
                );
            }

            return baseFormat;
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
                    return "RICS.TCH.Add.Usage".Translate();
                }

                var viewer = Viewers.GetViewer(messageWrapper);
                if (viewer == null)
                {
                    return "RICS.TCH.Add.NoViewerData".Translate();
                }

                var assignmentManager = CAPChatInteractiveMod.GetPawnAssignmentManager();
                Pawn pawn = assignmentManager.GetAssignedPawn(messageWrapper);

                if (pawn == null)
                {
                    return "RICS.TCH.Add.NoPawn".Translate();
                }
                if (pawn.Dead)
                {
                    return "RICS.TCH.Add.PawnDead".Translate();
                }

                string traitName = string.Join(" ", args).ToLowerInvariant();
                var buyableTrait = FindBuyableTrait(traitName);

                if (buyableTrait == null)
                {
                    return "RICS.TCH.Add.TraitNotFound".Translate(string.Join(" ", args));
                }

                if (!buyableTrait.CanAdd)
                {
                    return "RICS.TCH.Add.CannotAdd".Translate(buyableTrait.Name);
                }

                var settings = CAPChatInteractiveMod.Instance.Settings.GlobalSettings;
                int maxTraits = settings?.MaxTraits ?? 4;
                if (pawn.story.traits.allTraits.Count >= maxTraits && !buyableTrait.BypassLimit)
                {
                    return "RICS.TCH.Add.MaxTraitsReached".Translate(maxTraits);
                }

                TraitDef traitDef = DefDatabase<TraitDef>.GetNamedSilentFail(buyableTrait.DefName);
                if (traitDef != null && pawn.story.traits.HasTrait(traitDef))
                {
                    return "RICS.TCH.Add.AlreadyHasTrait".Translate(buyableTrait.Name);
                }

                string conflictCheck = CheckTraitConflicts(pawn, buyableTrait);
                if (!string.IsNullOrEmpty(conflictCheck))
                {
                    return conflictCheck;  // ← assumes CheckTraitConflicts already returns translated string
                }

                int traitCost = buyableTrait.AddPrice;
                var currencySymbol = settings.CurrencyName?.Trim() ?? "¢";

                if (viewer.Coins < traitCost)
                {
                    return "RICS.TCH.Add.NotEnoughCoins".Translate(
                        traitCost,
                        viewer.Coins,
                        currencySymbol
                    );
                }

                if (traitDef == null)
                {
                    return "RICS.TCH.Add.TraitDefMissing".Translate(buyableTrait.Name);
                }

                Trait newTrait = new Trait(traitDef, buyableTrait.Degree, false);
                pawn.story.traits.GainTrait(newTrait);

                viewer.TakeCoins(traitCost);

                return "RICS.TCH.Add.Success".Translate(
                    buyableTrait.Name,
                    pawn.Name.ToString(),
                    traitCost,
                    currencySymbol
                );
            }
            catch (Exception ex)
            {
                Logger.Error($"Error in AddTrait command handler: {ex}");
                return "RICS.TCH.Error".Translate();
            }
        }

        public static string HandleRemoveTraitCommand(ChatMessageWrapper messageWrapper, string[] args)
        {
            try
            {
                if (args.Length == 0)
                {
                    return "RICS.TCH.Remove.Usage".Translate();
                }

                var viewer = Viewers.GetViewer(messageWrapper);
                if (viewer == null)
                {
                    return "RICS.TCH.Remove.NoViewerData".Translate();
                }

                var assignmentManager = CAPChatInteractiveMod.GetPawnAssignmentManager();
                Pawn pawn = assignmentManager.GetAssignedPawn(messageWrapper);

                if (pawn == null)
                {
                    return "RICS.TCH.Remove.NoPawn".Translate();
                }
                if (pawn.Dead)
                {
                    return "RICS.TCH.Remove.PawnDead".Translate();
                }

                string traitName = string.Join(" ", args).ToLowerInvariant();
                var buyableTrait = FindBuyableTrait(traitName);

                if (buyableTrait == null)
                {
                    return "RICS.TCH.Remove.TraitNotFound".Translate(string.Join(" ", args));
                }

                if (!buyableTrait.CanRemove)
                {
                    return "RICS.TCH.Remove.CannotRemove".Translate(buyableTrait.Name);
                }

                TraitDef removeTraitDef = DefDatabase<TraitDef>.GetNamedSilentFail(buyableTrait.DefName);
                var existingTrait = pawn.story.traits.allTraits.FirstOrDefault(t =>
                    t.def.defName == buyableTrait.DefName && t.Degree == buyableTrait.Degree);

                if (existingTrait == null)
                {
                    return "RICS.TCH.Remove.DoesNotHaveTrait".Translate(buyableTrait.Name);
                }

                // NEW CHECK: Prevent removal of forced traits (e.g., from genes)
                if (existingTrait.sourceGene != null || existingTrait.ScenForced)
                {
                    return "RICS.TCH.Remove.ForcedTrait".Translate(buyableTrait.Name);
                }

                int removeCost = buyableTrait.RemovePrice;
                var settings = CAPChatInteractiveMod.Instance.Settings.GlobalSettings;
                var currencySymbol = settings.CurrencyName?.Trim() ?? "¢";

                if (viewer.Coins < removeCost)
                {
                    return "RICS.TCH.Remove.NotEnoughCoins".Translate(
                        removeCost,
                        viewer.Coins,
                        currencySymbol
                    );
                }

                // Remove the trait
                pawn.story.traits.RemoveTrait(existingTrait);

                // Deduct coins
                viewer.TakeCoins(removeCost);

                return "RICS.TCH.Remove.Success".Translate(
                    buyableTrait.Name,
                    pawn.Name.ToString(),
                    removeCost,
                    currencySymbol
                );
            }
            catch (Exception ex)
            {
                Logger.Error($"Error in RemoveTrait command handler: {ex}");
                return "RICS.TCH.Error".Translate();
            }
        }

        public static string HandleReplaceTraitCommand(ChatMessageWrapper messageWrapper, string[] args)
        {
            try
            {
                if (args.Length < 2)
                {
                    return "RICS.TCH.Replace.Usage".Translate();
                }

                var viewer = Viewers.GetViewer(messageWrapper);
                if (viewer == null)
                {
                    return "RICS.TCH.Replace.NoViewerData".Translate();
                }

                var assignmentManager = CAPChatInteractiveMod.GetPawnAssignmentManager();
                Pawn pawn = assignmentManager.GetAssignedPawn(messageWrapper);

                if (pawn == null)
                {
                    return "RICS.TCH.Replace.NoPawn".Translate();
                }
                if (pawn.Dead)
                {
                    return "RICS.TCH.Replace.PawnDead".Translate();
                }

                string oldTraitName = ParseTraitNames(args, out string newTraitName);

                if (string.IsNullOrEmpty(oldTraitName) || string.IsNullOrEmpty(newTraitName))
                {
                    return "RICS.TCH.Replace.ParseError".Translate();
                }

                Logger.Debug($"ReplaceTrait: old='{oldTraitName}', new='{newTraitName}'");

                var oldBuyableTrait = FindBuyableTrait(oldTraitName);
                var newBuyableTrait = FindBuyableTrait(newTraitName);

                if (oldBuyableTrait == null)
                {
                    return "RICS.TCH.Replace.OldTraitNotFound".Translate(oldTraitName);
                }

                if (newBuyableTrait == null)
                {
                    return "RICS.TCH.Replace.NewTraitNotFound".Translate(newTraitName);
                }

                if (!oldBuyableTrait.CanRemove)
                {
                    return "RICS.TCH.Replace.OldCannotRemove".Translate(oldBuyableTrait.Name);
                }

                if (!newBuyableTrait.CanAdd)
                {
                    return "RICS.TCH.Replace.NewCannotAdd".Translate(newBuyableTrait.Name);
                }

                TraitDef oldTraitDef = DefDatabase<TraitDef>.GetNamedSilentFail(oldBuyableTrait.DefName);
                TraitDef newTraitDef = DefDatabase<TraitDef>.GetNamedSilentFail(newBuyableTrait.DefName);

                if (oldTraitDef == null)
                {
                    return "RICS.TCH.Replace.OldTraitDefMissing".Translate(oldBuyableTrait.Name);
                }

                if (newTraitDef == null)
                {
                    return "RICS.TCH.Replace.NewTraitDefMissing".Translate(newBuyableTrait.Name);
                }

                var existingTrait = pawn.story.traits.allTraits.FirstOrDefault(t =>
                    t.def.defName == oldBuyableTrait.DefName && t.Degree == oldBuyableTrait.Degree);

                if (existingTrait == null)
                {
                    return "RICS.TCH.Replace.DoesNotHaveOld".Translate(oldBuyableTrait.Name);
                }

                if (existingTrait.sourceGene != null || existingTrait.ScenForced)
                {
                    return "RICS.TCH.Replace.OldTraitForced".Translate(oldBuyableTrait.Name);
                }

                // Check if pawn already has the new trait (different from old)
                if (oldBuyableTrait.DefName != newBuyableTrait.DefName || oldBuyableTrait.Degree != newBuyableTrait.Degree)
                {
                    if (pawn.story.traits.allTraits.Any(t =>
                        t.def.defName == newBuyableTrait.DefName && t.Degree == newBuyableTrait.Degree))
                    {
                        return "RICS.TCH.Replace.AlreadyHasNew".Translate(newBuyableTrait.Name);
                    }
                }

                // Check conflicts with other existing traits (excluding the one being replaced)
                var otherTraits = pawn.story.traits.allTraits.Where(t => t != existingTrait).ToList();
                foreach (var otherTrait in otherTraits)
                {
                    if (newTraitDef.ConflictsWith(otherTrait) || otherTrait.def.ConflictsWith(newTraitDef))
                    {
                        return "RICS.TCH.Replace.ConflictWithExisting".Translate(
                            newBuyableTrait.Name,
                            otherTrait.Label
                        );
                    }
                }

                int totalCost = oldBuyableTrait.RemovePrice + newBuyableTrait.AddPrice;
                var settings = CAPChatInteractiveMod.Instance.Settings.GlobalSettings;
                var currencySymbol = settings.CurrencyName?.Trim() ?? "¢";

                if (viewer.Coins < totalCost)
                {
                    return "RICS.TCH.Replace.NotEnoughCoins".Translate(
                        totalCost,
                        oldBuyableTrait.Name,
                        newBuyableTrait.Name,
                        currencySymbol,
                        viewer.Coins
                    );
                }

                // Remove old trait
                pawn.story.traits.RemoveTrait(existingTrait);

                // Add new trait
                Trait newTrait = new Trait(newTraitDef, newBuyableTrait.Degree, false);
                pawn.story.traits.GainTrait(newTrait);

                // Deduct coins
                viewer.TakeCoins(totalCost);

                return "RICS.TCH.Replace.Success".Translate(
                    oldBuyableTrait.Name,
                    newBuyableTrait.Name,
                    pawn.Name.ToString(),
                    totalCost,
                    currencySymbol
                );
            }
            catch (Exception ex)
            {
                Logger.Error($"Error in ReplaceTrait command handler: {ex}");
                return "RICS.TCH.Error".Translate();
            }
        }

        public static string HandleSetTraitsCommand(ChatMessageWrapper messageWrapper, string[] args)
        {
            try
            {
                if (args.Length < 1)
                {
                    return "RICS.TCH.Set.Usage".Translate();
                }

                var viewer = Viewers.GetViewer(messageWrapper);
                if (viewer == null)
                {
                    return "RICS.TCH.Set.NoViewerData".Translate();
                }

                var assignmentManager = CAPChatInteractiveMod.GetPawnAssignmentManager();
                Pawn pawn = assignmentManager.GetAssignedPawn(messageWrapper);

                if (pawn == null)
                {
                    return "RICS.TCH.Set.NoPawn".Translate();
                }
                if (pawn.Dead)
                {
                    return "RICS.TCH.Set.PawnDead".Translate();
                }

                // Step 1: Find and validate all requested traits
                var resolvedTraits = new List<BuyableTrait>();
                int bypassCount = 0;

                for (int i = 0; i < args.Length; i++)
                {
                    BuyableTrait trait = TraitsManager.AllBuyableTraits.Values
                        .FirstOrDefault(t =>
                            t.Name.ToLowerInvariant() == args[i].ToLowerInvariant() ||
                            t.DefName.ToLowerInvariant() == args[i].ToLowerInvariant());

                    // Try joining with next word if single word didn't match
                    if (trait == null && i + 1 < args.Length)
                    {
                        string joined = $"{args[i]} {args[i + 1]}".ToLowerInvariant();
                        trait = TraitsManager.AllBuyableTraits.Values
                            .FirstOrDefault(t =>
                                t.Name.ToLowerInvariant() == joined ||
                                t.DefName.ToLowerInvariant() == joined);

                        if (trait != null)
                            i++; // consume second word
                    }

                    if (trait == null)
                    {
                        return "RICS.TCH.Set.TraitNotFound".Translate(args[i]);
                    }

                    if (!trait.CanAdd)
                    {
                        return "RICS.TCH.Set.CannotAdd".Translate(trait.Name);
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
                            return "RICS.TCH.Set.ConflictBetweenRequested".Translate(
                                traitA.Name,
                                traitB.Name
                            );
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
                        .FirstOrDefault(t => t.DefName == existing.def.defName && t.Degree == existing.Degree))
                    .Where(t => t != null)
                    .ToList();

                // Step 4: Check requested traits for conflicts with protected traits
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
                            return "RICS.TCH.Set.ConflictWithProtected".Translate(
                                requestedTrait.Name,
                                protectedTrait.Name
                            );
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
                    return "RICS.TCH.Set.TooManyTraits".Translate(
                        requestedCount,
                        protectedCount,
                        effectiveMax
                    );
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
                var currencySymbol = settings.CurrencyName?.Trim() ?? "¢";

                if (viewer.Coins < totalCost)
                {
                    return "RICS.TCH.Set.NotEnoughCoins".Translate(
                        totalCost,
                        currencySymbol,
                        viewer.Coins
                    );
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
                        return "RICS.TCH.Set.TraitDefMissing".Translate(t.Name);
                    }
                    Trait newTrait = new Trait(newTraitDef, t.Degree, false);
                    pawn.story.traits.GainTrait(newTrait);
                }

                // Deduct coins
                viewer.TakeCoins(totalCost);

                return "RICS.TCH.Set.Success".Translate(
                    string.Join(", ", resolvedTraits.Select(t => t.Name)),
                    totalCost,
                    currencySymbol
                );
            }
            catch (Exception ex)
            {
                Logger.Error($"Error in SetTraits command handler: {ex}");
                return "RICS.TCH.Error".Translate();
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
                    return "RICS.TCH.List.NoTraits".Translate();
                }

                var response = new StringBuilder();
                response.AppendLine("RICS.TCH.List.Header".Translate());

                // Group by mod source for better organization
                var traitsByMod = enabledTraits.GroupBy(t => t.ModSource)
                                              .OrderBy(g => g.Key);

                foreach (var modGroup in traitsByMod)
                {
                    response.AppendLine("\n" + "RICS.TCH.List.ModGroup".Translate(modGroup.Key));

                    var traitList = modGroup.Select(t => t.Name)
                                          .OrderBy(label => label)
                                          .Take(10); // Limit per mod to avoid message spam

                    response.AppendLine(string.Join(", ", traitList));

                    if (modGroup.Count() > 10)
                    {
                        response.AppendLine("RICS.TCH.List.MoreTraits".Translate(modGroup.Count() - 10));
                    }
                }

                response.AppendLine("\n" + "RICS.TCH.List.Footer".Translate());

                return response.ToString();
            }
            catch (Exception ex)
            {
                Logger.Error($"Error in ListTraits command handler: {ex}");
                return "RICS.TCH.Error".Translate();
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
                    return "RICS.TCH.ConflictWithExisting".Translate(
                        newTrait.Name,
                        existingTrait.Label
                    );
                }
            }

            return null;
        }
    }
}
