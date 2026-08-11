// File: TraitsCommandHandler.cs
//
// Copyright (c) Captolamia
// This file is part of CAP Chat Interactive (RICS).
// Licensed under the GNU Affero General Public License v3.0 or later.
// See LICENSE.txt in the project root for full license text.
//
//
// !trait / !addtrait / !removetrait / !replacetrait / !settraits / !traits
using _CAP__Chat_Interactive.Command.CommandHelpers;
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
        private const string ReturnDivider = " | ";

        public static string HandleLookupTraitCommand(ChatMessageWrapper messageWrapper, string[] args)
        {
            try
            {
                args = args ?? Array.Empty<string>();
                if (args.Length == 0)
                    return "RICS.TCH.Lookup.Usage".Translate();

                string traitName = string.Join(" ", args).ToLowerInvariant();
                var buyableTrait = FindBuyableTrait(traitName);
                if (buyableTrait == null)
                    return "RICS.TCH.Lookup.TraitNotFound".Translate(string.Join(" ", args));

                return FormatTraitInfoSimple(buyableTrait);
            }
            catch (Exception ex)
            {
                Logger.Error($"[Traits] Error in LookupTrait: {ex}");
                return "RICS.TCH.Error".Translate();
            }
        }

        private static string FormatTraitInfoSimple(BuyableTrait buyableTrait)
        {
            var settings = CAPChatInteractiveMod.Instance?.Settings?.GlobalSettings;
            string currencySymbol = settings?.CurrencyName?.Trim() ?? "¢";

            if (!string.IsNullOrEmpty(buyableTrait.Description))
            {
                string cleanDescription = Dialog_TraitsEditor.ReplacePawnVariables(buyableTrait.Description);
                return "RICS.TCH.SimpleInfo.WithDescription".Translate(
                    buyableTrait.Name,
                    buyableTrait.AddPrice,
                    buyableTrait.RemovePrice,
                    currencySymbol,
                    cleanDescription);
            }

            return "RICS.TCH.SimpleInfo.Format".Translate(
                buyableTrait.Name,
                buyableTrait.AddPrice,
                buyableTrait.RemovePrice,
                currencySymbol);
        }

        public static string HandleAddTraitCommand(ChatMessageWrapper messageWrapper, string[] args)
        {
            try
            {
                args = args ?? Array.Empty<string>();
                if (args.Length == 0)
                    return "RICS.TCH.Add.Usage".Translate();

                var viewer = Viewers.GetViewer(messageWrapper);
                if (viewer == null)
                    return "RICS.TCH.Add.NoViewerData".Translate();

                Verse.Pawn pawn = PawnItemHelper.GetViewerPawn(messageWrapper);
                string pawnError = ValidateLivingViewerPawn(pawn);
                if (pawnError != null)
                    return pawnError;

                string traitName = string.Join(" ", args).ToLowerInvariant();
                var buyableTrait = FindBuyableTrait(traitName);
                if (buyableTrait == null)
                    return "RICS.TCH.Add.TraitNotFound".Translate(string.Join(" ", args));

                if (!buyableTrait.CanAdd)
                    return "RICS.TCH.Add.CannotAdd".Translate(buyableTrait.Name);

                var settings = CAPChatInteractiveMod.Instance?.Settings?.GlobalSettings;
                int maxTraits = settings?.MaxTraits ?? 4;

                int effectiveCount = GetEffectiveTraitCount(pawn);
                if (effectiveCount >= maxTraits && !buyableTrait.BypassLimit)
                    return "RICS.TCH.Add.MaxTraitsReached".Translate(maxTraits);

                TraitDef traitDef = DefDatabase<TraitDef>.GetNamedSilentFail(buyableTrait.DefName);
                if (traitDef != null && pawn.story?.traits != null && pawn.story.traits.HasTrait(traitDef))
                    return "RICS.TCH.Add.AlreadyHasTrait".Translate(buyableTrait.Name);

                string conflictCheck = CheckTraitConflicts(pawn, buyableTrait);
                if (!string.IsNullOrEmpty(conflictCheck))
                    return conflictCheck;

                if (traitDef == null)
                    return "RICS.TCH.Add.TraitDefMissing".Translate(buyableTrait.Name);

                int traitCost = buyableTrait.AddPrice;
                string currencySymbol = settings?.CurrencyName?.Trim() ?? "¢";

                if (viewer.Coins < traitCost)
                {
                    return "RICS.TCH.Add.NotEnoughCoins".Translate(
                        traitCost,
                        viewer.Coins,
                        currencySymbol);
                }

                if (pawn.story?.traits == null)
                    return "RICS.TCH.Error.add".Translate();

                Trait newTrait = new Trait(traitDef, buyableTrait.Degree, false);
                pawn.story.traits.GainTrait(newTrait);
                viewer.TakeCoins(traitCost);

                return "RICS.TCH.Add.Success".Translate(
                    buyableTrait.Name,
                    pawn.Name.ToString(),
                    traitCost,
                    currencySymbol);
            }
            catch (Exception ex)
            {
                Logger.Error($"[Traits] Error in AddTrait: {ex}");
                return "RICS.TCH.Error.add".Translate();
            }
        }

        public static string HandleRemoveTraitCommand(ChatMessageWrapper messageWrapper, string[] args)
        {
            try
            {
                args = args ?? Array.Empty<string>();
                if (args.Length == 0)
                    return "RICS.TCH.Remove.Usage".Translate();

                var viewer = Viewers.GetViewer(messageWrapper);
                if (viewer == null)
                    return "RICS.TCH.Add.NoViewerData".Translate();

                Verse.Pawn pawn = PawnItemHelper.GetViewerPawn(messageWrapper);
                string pawnError = ValidateLivingViewerPawn(pawn);
                if (pawnError != null)
                    return pawnError;

                if (pawn.story?.traits?.allTraits == null)
                    return "RICS.TCH.Errorremove".Translate();

                string traitName = string.Join(" ", args).ToLowerInvariant();
                var buyableTrait = FindBuyableTrait(traitName);
                if (buyableTrait == null)
                    return "RICS.TCH.Remove.TraitNotFound".Translate(string.Join(" ", args));

                if (!buyableTrait.CanRemove)
                    return "RICS.TCH.Remove.CannotRemove".Translate(buyableTrait.Name);

                var existingTrait = pawn.story.traits.allTraits.FirstOrDefault(t =>
                    t.def.defName == buyableTrait.DefName && t.Degree == buyableTrait.Degree);

                if (existingTrait == null)
                    return "RICS.TCH.Remove.DoesNotHaveTrait".Translate(buyableTrait.Name);

                if (existingTrait.sourceGene != null || existingTrait.ScenForced)
                    return "RICS.TCH.Remove.ForcedTrait".Translate(buyableTrait.Name);

                int removeCost = buyableTrait.RemovePrice;
                string currencySymbol = CAPChatInteractiveMod.Instance?.Settings?.GlobalSettings?.CurrencyName?.Trim() ?? "¢";

                if (viewer.Coins < removeCost)
                {
                    return "RICS.TCH.Remove.NotEnoughCoins".Translate(
                        removeCost,
                        viewer.Coins,
                        currencySymbol);
                }

                pawn.story.traits.RemoveTrait(existingTrait);
                viewer.TakeCoins(removeCost);

                return "RICS.TCH.Remove.Success".Translate(
                    buyableTrait.Name,
                    pawn.Name.ToString(),
                    removeCost,
                    currencySymbol);
            }
            catch (Exception ex)
            {
                Logger.Error($"[Traits] Error in RemoveTrait: {ex}");
                return "RICS.TCH.Errorremove".Translate();
            }
        }

        public static string HandleReplaceTraitCommand(ChatMessageWrapper messageWrapper, string[] args)
        {
            try
            {
                args = args ?? Array.Empty<string>();
                if (args.Length < 2)
                    return "RICS.TCH.Replace.Usage".Translate();

                var viewer = Viewers.GetViewer(messageWrapper);
                if (viewer == null)
                    return "RICS.TCH.Add.NoViewerData".Translate();

                Verse.Pawn pawn = PawnItemHelper.GetViewerPawn(messageWrapper);
                string pawnError = ValidateLivingViewerPawn(pawn);
                if (pawnError != null)
                    return pawnError;

                if (pawn.story?.traits?.allTraits == null)
                    return "RICS.TCH.Error.Replace".Translate();

                string oldTraitName = ParseTraitNames(args, out string newTraitName);
                if (string.IsNullOrEmpty(oldTraitName) || string.IsNullOrEmpty(newTraitName))
                    return "RICS.TCH.Replace.ParseError".Translate();

                var oldBuyableTrait = FindBuyableTrait(oldTraitName);
                var newBuyableTrait = FindBuyableTrait(newTraitName);

                if (oldBuyableTrait == null)
                    return "RICS.TCH.Replace.OldTraitNotFound".Translate(oldTraitName);

                if (newBuyableTrait == null)
                    return "RICS.TCH.Replace.NewTraitNotFound".Translate(newTraitName);

                // Anti-exploit: cannot replace BypassLimit traits into a normal slot
                if (oldBuyableTrait.BypassLimit)
                    return "RICS.TCH.Replace.OldCannotRemovebypass".Translate(oldBuyableTrait.Name);

                if (!oldBuyableTrait.CanRemove)
                    return "RICS.TCH.Replace.OldCannotRemove".Translate(oldBuyableTrait.Name);

                if (!newBuyableTrait.CanAdd)
                    return "RICS.TCH.Replace.NewCannotAdd".Translate(newBuyableTrait.Name);

                var settings = CAPChatInteractiveMod.Instance?.Settings?.GlobalSettings;
                string currencySymbol = settings?.CurrencyName?.Trim() ?? "¢";

                TraitDef oldTraitDef = DefDatabase<TraitDef>.GetNamedSilentFail(oldBuyableTrait.DefName);
                TraitDef newTraitDef = DefDatabase<TraitDef>.GetNamedSilentFail(newBuyableTrait.DefName);

                if (oldTraitDef == null)
                    return "RICS.TCH.Replace.OldTraitDefMissing".Translate(oldBuyableTrait.Name);

                if (newTraitDef == null)
                    return "RICS.TCH.Replace.NewTraitDefMissing".Translate(newBuyableTrait.Name);

                var existingTrait = pawn.story.traits.allTraits.FirstOrDefault(t =>
                    t.def.defName == oldBuyableTrait.DefName && t.Degree == oldBuyableTrait.Degree);

                if (existingTrait == null)
                    return "RICS.TCH.Replace.DoesNotHaveOld".Translate(oldBuyableTrait.Name);

                if (existingTrait.sourceGene != null || existingTrait.ScenForced)
                    return "RICS.TCH.Replace.OldTraitForced".Translate(oldBuyableTrait.Name);

                if (oldBuyableTrait.DefName != newBuyableTrait.DefName || oldBuyableTrait.Degree != newBuyableTrait.Degree)
                {
                    if (pawn.story.traits.allTraits.Any(t =>
                            t.def.defName == newBuyableTrait.DefName && t.Degree == newBuyableTrait.Degree))
                    {
                        return "RICS.TCH.Replace.AlreadyHasNew".Translate(newBuyableTrait.Name);
                    }
                }

                foreach (var otherTrait in pawn.story.traits.allTraits.Where(t => t != existingTrait))
                {
                    if (newTraitDef.ConflictsWith(otherTrait) || otherTrait.def.ConflictsWith(newTraitDef))
                    {
                        return "RICS.TCH.Replace.ConflictWithExisting".Translate(
                            newBuyableTrait.Name,
                            otherTrait.Label);
                    }
                }

                int totalCost = oldBuyableTrait.RemovePrice + newBuyableTrait.AddPrice;

                if (viewer.Coins < totalCost)
                {
                    return "RICS.TCH.Replace.NotEnoughCoins".Translate(
                        totalCost,
                        oldBuyableTrait.Name,
                        newBuyableTrait.Name,
                        currencySymbol,
                        viewer.Coins);
                }

                pawn.story.traits.RemoveTrait(existingTrait);
                Trait newTrait = new Trait(newTraitDef, newBuyableTrait.Degree, false);
                pawn.story.traits.GainTrait(newTrait);
                viewer.TakeCoins(totalCost);

                return "RICS.TCH.Replace.Success".Translate(
                    oldBuyableTrait.Name,
                    newBuyableTrait.Name,
                    pawn.Name.ToString(),
                    totalCost,
                    currencySymbol);
            }
            catch (Exception ex)
            {
                Logger.Error($"[Traits] Error in ReplaceTrait: {ex}");
                return "RICS.TCH.Error.Replace".Translate();
            }
        }

        public static string HandleSetTraitsCommand(ChatMessageWrapper messageWrapper, string[] args)
        {
            try
            {
                args = args ?? Array.Empty<string>();
                if (args.Length < 1)
                    return "RICS.TCH.Set.Usage".Translate();

                var viewer = Viewers.GetViewer(messageWrapper);
                if (viewer == null)
                    return "RICS.TCH.Add.NoViewerData".Translate();

                Verse.Pawn pawn = PawnItemHelper.GetViewerPawn(messageWrapper);
                string pawnError = ValidateLivingViewerPawn(pawn);
                if (pawnError != null)
                    return pawnError;

                if (pawn.story?.traits?.allTraits == null)
                    return "RICS.TCH.Error.setting".Translate();

                var resolvedTraits = new List<BuyableTrait>();
                int bypassCount = 0;

                for (int i = 0; i < args.Length; i++)
                {
                    BuyableTrait trait = TraitsManager.AllBuyableTraits.Values
                        .FirstOrDefault(t =>
                            t.Name.Equals(args[i], StringComparison.OrdinalIgnoreCase) ||
                            t.DefName.Equals(args[i], StringComparison.OrdinalIgnoreCase));

                    if (trait == null && i + 1 < args.Length)
                    {
                        string joined = $"{args[i]} {args[i + 1]}";
                        trait = TraitsManager.AllBuyableTraits.Values
                            .FirstOrDefault(t =>
                                t.Name.Equals(joined, StringComparison.OrdinalIgnoreCase) ||
                                t.DefName.Equals(joined, StringComparison.OrdinalIgnoreCase));

                        if (trait != null)
                            i++;
                    }

                    if (trait == null)
                        return "RICS.TCH.Set.TraitNotFound".Translate(args[i]);

                    if (!trait.CanAdd)
                        return "RICS.TCH.Set.CannotAdd".Translate(trait.Name);

                    if (trait.BypassLimit)
                        bypassCount++;

                    resolvedTraits.Add(trait);
                }

                for (int i = 0; i < resolvedTraits.Count; i++)
                {
                    var traitA = resolvedTraits[i];
                    var traitDefA = DefDatabase<TraitDef>.GetNamedSilentFail(traitA.DefName);

                    for (int j = i + 1; j < resolvedTraits.Count; j++)
                    {
                        var traitB = resolvedTraits[j];
                        var traitDefB = DefDatabase<TraitDef>.GetNamedSilentFail(traitB.DefName);

                        bool nameConflict =
                            traitA.Conflicts != null && traitA.Conflicts.Any(c =>
                                c.Equals(traitB.Name, StringComparison.OrdinalIgnoreCase))
                            || traitB.Conflicts != null && traitB.Conflicts.Any(c =>
                                c.Equals(traitA.Name, StringComparison.OrdinalIgnoreCase));

                        bool defConflict = traitDefA != null && traitDefB != null &&
                            (traitDefA.ConflictsWith(traitDefB) || traitDefA.defName == traitDefB.defName);

                        if (nameConflict || defConflict)
                        {
                            return "RICS.TCH.Set.ConflictBetweenRequested".Translate(
                                traitA.Name,
                                traitB.Name);
                        }
                    }
                }

                var forcedList = pawn.story.traits.allTraits
                    .Where(t => t.ScenForced || t.sourceGene != null)
                    .ToList();

                var unremovableList = pawn.story.traits.allTraits
                    .Where(existing =>
                    {
                        if (forcedList.Contains(existing))
                            return false;
                        var buyable = TraitsManager.AllBuyableTraits.Values
                            .FirstOrDefault(t => t.DefName == existing.def.defName && t.Degree == existing.Degree);
                        return buyable != null && !buyable.CanRemove;
                    })
                    .ToList();

                var protectedTraits = forcedList.Concat(unremovableList)
                    .Select(existing => TraitsManager.AllBuyableTraits.Values
                        .FirstOrDefault(t => t.DefName == existing.def.defName && t.Degree == existing.Degree))
                    .Where(t => t != null)
                    .ToList();

                foreach (var requestedTrait in resolvedTraits)
                {
                    var traitDefA = DefDatabase<TraitDef>.GetNamedSilentFail(requestedTrait.DefName);
                    foreach (var protectedTrait in protectedTraits)
                    {
                        var traitDefB = DefDatabase<TraitDef>.GetNamedSilentFail(protectedTrait.DefName);

                        bool nameConflict =
                            requestedTrait.Conflicts != null && requestedTrait.Conflicts.Any(c =>
                                c.Equals(protectedTrait.Name, StringComparison.OrdinalIgnoreCase))
                            || protectedTrait.Conflicts != null && protectedTrait.Conflicts.Any(c =>
                                c.Equals(requestedTrait.Name, StringComparison.OrdinalIgnoreCase));

                        bool defConflict = traitDefA != null && traitDefB != null &&
                            (traitDefA.ConflictsWith(traitDefB) || traitDefA.defName == traitDefB.defName);

                        if (nameConflict || defConflict)
                        {
                            return "RICS.TCH.Set.ConflictWithProtected".Translate(
                                requestedTrait.Name,
                                protectedTrait.Name);
                        }
                    }
                }

                var settings = CAPChatInteractiveMod.Instance?.Settings?.GlobalSettings;
                int maxTraits = settings?.MaxTraits ?? 4;
                int protectedCount = forcedList.Count + unremovableList.Count;
                int effectiveMax = Math.Max(0, maxTraits - protectedCount);
                int requestedCount = resolvedTraits.Count - bypassCount;

                if (requestedCount > effectiveMax)
                {
                    return "RICS.TCH.Set.TooManyTraits".Translate(
                        requestedCount,
                        protectedCount,
                        effectiveMax);
                }

                var existingTraits = pawn.story.traits.allTraits
                    .Where(existing => !resolvedTraits.Any(rt =>
                        rt.DefName == existing.def.defName && rt.Degree == existing.Degree))
                    .ToList();

                resolvedTraits = resolvedTraits
                    .Where(rt => !pawn.story.traits.allTraits.Any(et =>
                        et.def.defName == rt.DefName && et.Degree == rt.Degree))
                    .ToList();

                var removableTraits = existingTraits
                    .Except(forcedList)
                    .Except(unremovableList)
                    .ToList();

                int totalCost = 0;
                foreach (var t in removableTraits)
                {
                    BuyableTrait bT = TraitsManager.AllBuyableTraits.Values
                        .FirstOrDefault(bt => bt.DefName == t.def.defName && bt.Degree == t.Degree);
                    if (bT != null)
                        totalCost += bT.RemovePrice;
                }

                foreach (var t in resolvedTraits)
                    totalCost += t.AddPrice;

                string currencySymbol = settings?.CurrencyName?.Trim() ?? "¢";

                if (viewer.Coins < totalCost)
                {
                    return "RICS.TCH.Set.NotEnoughCoins".Translate(
                        totalCost,
                        currencySymbol,
                        viewer.Coins);
                }

                foreach (var t in removableTraits)
                    pawn.story.traits.RemoveTrait(t);

                foreach (var t in resolvedTraits)
                {
                    TraitDef newTraitDef = DefDatabase<TraitDef>.GetNamedSilentFail(t.DefName);
                    if (newTraitDef == null)
                        return "RICS.TCH.Set.TraitDefMissing".Translate(t.Name);

                    pawn.story.traits.GainTrait(new Trait(newTraitDef, t.Degree, false));
                }

                viewer.TakeCoins(totalCost);

                return "RICS.TCH.Set.Success".Translate(
                    string.Join(", ", resolvedTraits.Select(t => t.Name)),
                    totalCost,
                    currencySymbol);
            }
            catch (Exception ex)
            {
                Logger.Error($"[Traits] Error in SetTraits: {ex}");
                return "RICS.TCH.Error.setting".Translate();
            }
        }

        public static string HandleListTraitsCommand(ChatMessageWrapper messageWrapper, string[] args)
        {
            try
            {
                var enabledTraits = TraitsManager.GetEnabledTraits().ToList();
                if (!enabledTraits.Any())
                    return "RICS.TCH.List.NoTraits".Translate();

                var response = new StringBuilder();
                response.Append("RICS.TCH.List.Header".Translate());

                var traitsByMod = enabledTraits.GroupBy(t => t.ModSource).OrderBy(g => g.Key);

                foreach (var modGroup in traitsByMod)
                {
                    response.Append(ReturnDivider);
                    response.Append("RICS.TCH.List.ModGroup".Translate(modGroup.Key));
                    response.Append(ReturnDivider);

                    var traitList = modGroup.Select(t => t.Name).OrderBy(label => label).Take(10);
                    response.Append(string.Join(", ", traitList));

                    if (modGroup.Count() > 10)
                    {
                        response.Append(ReturnDivider);
                        response.Append("RICS.TCH.List.MoreTraits".Translate(modGroup.Count() - 10));
                    }
                }

                response.Append(ReturnDivider);
                response.Append("RICS.TCH.List.Footer".Translate());
                return response.ToString();
            }
            catch (Exception ex)
            {
                Logger.Error($"[Traits] Error in ListTraits: {ex}");
                return "RICS.TCH.Error.list".Translate();
            }
        }

        private static string ValidateLivingViewerPawn(Verse.Pawn pawn)
        {
            if (pawn == null)
                return "RICS.Pawn.NoPawn".Translate();

            if (pawn.Destroyed || pawn.Dead)
            {
                var deathInfo = GameComponent_PawnAssignmentManager.GetPawnDeathInfo(pawn);
                return "RICS.Pawn.Dead".Translate()
                       + ReturnDivider
                       + "RICS.Return.PawnDeadReason".Translate(deathInfo.ToString());
            }

            return null;
        }

        private static string ParseTraitNames(string[] args, out string newTraitName)
        {
            newTraitName = null;

            if (args.Length == 2)
            {
                newTraitName = args[1].ToLowerInvariant();
                return args[0].ToLowerInvariant();
            }

            for (int splitPoint = 1; splitPoint < args.Length; splitPoint++)
            {
                string potentialOldTrait = string.Join(" ", args.Take(splitPoint));
                string potentialNewTrait = string.Join(" ", args.Skip(splitPoint));

                if (FindBuyableTrait(potentialOldTrait) != null && FindBuyableTrait(potentialNewTrait) != null)
                {
                    newTraitName = potentialNewTrait.ToLowerInvariant();
                    return potentialOldTrait.ToLowerInvariant();
                }
            }

            if (args.Length > 1)
            {
                string potentialOldTrait = args[0];
                string potentialNewTrait = string.Join(" ", args.Skip(1));

                if (FindBuyableTrait(potentialOldTrait) != null)
                {
                    newTraitName = potentialNewTrait.ToLowerInvariant();
                    return potentialOldTrait.ToLowerInvariant();
                }
            }

            return null;
        }

        private static BuyableTrait FindBuyableTrait(string searchTerm)
        {
            if (string.IsNullOrWhiteSpace(searchTerm))
                return null;

            string term = searchTerm.ToLowerInvariant();

            // Prefer exact name/defName, then contains
            return TraitsManager.AllBuyableTraits.Values
                       .FirstOrDefault(trait =>
                           trait.Name.Equals(searchTerm, StringComparison.OrdinalIgnoreCase) ||
                           trait.DefName.Equals(searchTerm, StringComparison.OrdinalIgnoreCase))
                   ?? TraitsManager.AllBuyableTraits.Values
                       .FirstOrDefault(trait =>
                           trait.Name.ToLowerInvariant().Contains(term) ||
                           trait.DefName.ToLowerInvariant().Contains(term));
        }

        private static string CheckTraitConflicts(Verse.Pawn pawn, BuyableTrait newTrait)
        {
            TraitDef newTraitDef = DefDatabase<TraitDef>.GetNamedSilentFail(newTrait.DefName);
            if (newTraitDef == null || pawn.story?.traits?.allTraits == null)
                return null;

            foreach (var existingTrait in pawn.story.traits.allTraits)
            {
                if (newTraitDef.ConflictsWith(existingTrait) || existingTrait.def.ConflictsWith(newTraitDef))
                {
                    return "RICS.TCH.ConflictWithExisting".Translate(
                        newTrait.Name,
                        existingTrait.Label);
                }
            }

            return null;
        }

        private static int GetEffectiveTraitCount(Verse.Pawn pawn)
        {
            if (pawn?.story?.traits?.allTraits == null)
                return 0;

            int counted = 0;
            foreach (var trait in pawn.story.traits.allTraits)
            {
                var buyable = TraitsManager.AllBuyableTraits.Values
                    .FirstOrDefault(bt => bt.DefName == trait.def.defName && bt.Degree == trait.Degree);

                if (buyable == null || !buyable.BypassLimit)
                    counted++;
            }

            return counted;
        }
    }
}
