// HealPawnCommandHandler.cs
// Copyright (c) Captolamia. All rights reserved.
// Licensed under the AGPLv3 License. See LICENSE file in the project root for full license information.
//
// Command handler for the !healpawn command
using CAP_ChatInteractive.Commands.ViewerCommands;
using CAP_ChatInteractive.Store;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;
using Verse.Sound;

namespace CAP_ChatInteractive.Commands.CommandHandlers
{
    public static class HealPawnCommandHandler
    {
        public static string HandleHealPawn(ChatMessageWrapper user, string[] args)
        {
            try
            {
                Logger.Debug($"HandleHealPawn called for user: {user.Username}, args: {string.Join(", ", args)}");

                var settings = CAPChatInteractiveMod.Instance.Settings.GlobalSettings;
                var currencySymbol = settings.CurrencyName?.Trim() ?? "¢";
                var viewer = Viewers.GetViewer(user);

                // Get the Healer Mech Serum store item for pricing
                var healerSerum = StoreInventory.GetStoreItem("MechSerumHealer");
                if (healerSerum == null || !healerSerum.Enabled)
                {
                    return "Healer Mech Serum is not available for healing services.";
                }

                int pricePerHeal = healerSerum.BasePrice;

                // Parse command arguments
                if (args.Length == 0)
                {
                    // Heal self
                    return HealSelf(user, viewer, pricePerHeal, currencySymbol, 1);
                }

                string target = args[0].ToLowerInvariant();
                int quantity = 1;

                // Check if first argument is a number (quantity)
                if (int.TryParse(target, out int parsedQuantity) && parsedQuantity > 0)
                {
                    quantity = parsedQuantity;
                    target = args.Length > 1 ? args[1].ToLowerInvariant() : user.Username.ToLowerInvariant();
                }
                else if (args.Length > 1 && int.TryParse(args[1], out parsedQuantity) && parsedQuantity > 0)
                {
                    quantity = parsedQuantity;
                }

                if (target == "all")
                {
                    return HealAllSelf(user, viewer, pricePerHeal, currencySymbol, quantity);
                }
                if (target == "allpawns")
                {
                    // Heal all pawns
                    return HealAllPawns(user, viewer, pricePerHeal, currencySymbol, quantity);
                }
                else
                {
                    // Heal specific user's pawn
                    return HealSpecificUser(user, viewer, target, pricePerHeal, currencySymbol, quantity);
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error in HandleHealPawn: {ex}");
                return "Error processing heal command. Please try again.";
            }
        }

        private static string HealAllSelf(ChatMessageWrapper user, Viewer viewer, int pricePerHeal, string currencySymbol, int quantity)
        {
            var viewerPawn = StoreCommandHelper.GetViewerPawn(user);

            if (viewerPawn == null)
            {
                return "You don't have a pawn assigned. Use !buy pawn first.";
            }

            if (viewerPawn.Dead)
            {
                return "Your pawn is dead. Use !revivepawn first.";
            }

            if (!HasInjuriesToHeal(viewerPawn))
            {
                return "Your pawn doesn't have any injuries to heal.";
            }

            // Count all healable injuries
            int totalInjuries = CountHealableInjuries(viewerPawn);
            int maxAffordableHeals = viewer.Coins / pricePerHeal;
            int injuriesToHeal = Math.Min(totalInjuries, maxAffordableHeals);

            if (injuriesToHeal == 0)
            {
                return $"You need {StoreCommandHelper.FormatCurrencyMessage(pricePerHeal, currencySymbol)} to heal any injuries! You have {StoreCommandHelper.FormatCurrencyMessage(viewer.Coins, currencySymbol)}.";
            }

            int totalCost = injuriesToHeal * pricePerHeal;

            // Deduct coins and heal all affordable injuries
            viewer.TakeCoins(totalCost);
            int injuriesHealed = ApplyCompleteHealing(viewerPawn);

            int karmaEarned = totalCost / 100;
            if (karmaEarned > 0)
            {
                viewer.GiveKarma(karmaEarned);
                Logger.Debug($"Awarded {karmaEarned} karma for {totalCost} coin purchase");
            }

            // Send healing invoice
            string invoiceLabel = $"💚 Rimazon Complete Healing - {user.Username}";
            string invoiceMessage = CreateCompleteHealingInvoice(user.Username, injuriesHealed, totalCost, currencySymbol);
            MessageHandler.SendGreenLetter(invoiceLabel, invoiceMessage);

            Logger.Debug($"Heal all self successful: {user.Username} healed {injuriesHealed} injuries for {totalCost}{currencySymbol}");

            if (injuriesHealed < totalInjuries)
            {
                return $"💚 COMPLETE HEALING! Healed {injuriesHealed} injuries on your pawn for {StoreCommandHelper.FormatCurrencyMessage(totalCost, currencySymbol)}! {totalInjuries - injuriesHealed} injuries remain (insufficient funds). Remaining: {StoreCommandHelper.FormatCurrencyMessage(viewer.Coins, currencySymbol)}.";
            }
            else
            {
                return $"💚 COMPLETE HEALING! Healed all {injuriesHealed} injuries on your pawn for {StoreCommandHelper.FormatCurrencyMessage(totalCost, currencySymbol)}! Remaining: {StoreCommandHelper.FormatCurrencyMessage(viewer.Coins, currencySymbol)}.";
            }
        }



        private static string HealSelf(ChatMessageWrapper user, Viewer viewer, int pricePerHeal, string currencySymbol, int quantity)
        {
            var viewerPawn = StoreCommandHelper.GetViewerPawn(user);

            if (viewerPawn == null)
            {
                return "You don't have a pawn assigned. Use !buy pawn first.";
            }

            if (viewerPawn.Dead)
            {
                return "Your pawn is dead. Use !revivepawn first.";
            }

            if (!HasInjuriesToHeal(viewerPawn))
            {
                return "Your pawn doesn't have any injuries to heal.";
            }

            int totalCost = pricePerHeal * quantity;

            // Check if user can afford
            if (!StoreCommandHelper.CanUserAfford(user, totalCost))
            {
                return $"You need {StoreCommandHelper.FormatCurrencyMessage(totalCost, currencySymbol)} to heal your pawn {quantity} time(s)! You have {StoreCommandHelper.FormatCurrencyMessage(viewer.Coins, currencySymbol)}.";
            }

            // Deduct coins and heal
            viewer.TakeCoins(totalCost);
            int injuriesHealed = ApplyHealing(viewerPawn, quantity);

            int karmaEarned = totalCost / 100;
            if (karmaEarned > 0)
            {
                viewer.GiveKarma(karmaEarned);
                Logger.Debug($"Awarded {karmaEarned} karma for {totalCost} coin purchase");
            }

            // Send healing invoice
            string invoiceLabel = $"💚 Rimazon Healing - {user.Username}";
            string invoiceMessage = CreateHealingInvoice(user.Username, user.Username, injuriesHealed, totalCost, currencySymbol);
            MessageHandler.SendGreenLetter(invoiceLabel, invoiceMessage);

            Logger.Debug($"Heal self successful: {user.Username} healed {injuriesHealed} injuries for {totalCost}{currencySymbol}");
            return $"💚 HEALING! Healed {injuriesHealed} injuries on your pawn for {StoreCommandHelper.FormatCurrencyMessage(totalCost, currencySymbol)}! Remaining: {StoreCommandHelper.FormatCurrencyMessage(viewer.Coins, currencySymbol)}.";
        }

        private static string HealSpecificUser(ChatMessageWrapper user, Viewer viewer, string targetUsername, int pricePerHeal, string currencySymbol, int quantity)
        {
            int totalCost = pricePerHeal * quantity;

            // Check if user can afford
            if (!StoreCommandHelper.CanUserAfford(user, totalCost))
            {
                return $"You need {StoreCommandHelper.FormatCurrencyMessage(totalCost, currencySymbol)} to heal {targetUsername}'s pawn {quantity} time(s)! You have {StoreCommandHelper.FormatCurrencyMessage(viewer.Coins, currencySymbol)}.";
            }

            // FIXED: Use the assignment manager directly to find the pawn by username
            var assignmentManager = CAPChatInteractiveMod.GetPawnAssignmentManager();
            var targetPawn = assignmentManager.GetAssignedPawn(targetUsername);

            if (targetPawn == null)
            {
                return $"{targetUsername} doesn't have a pawn assigned.";
            }

            if (targetPawn.Dead)
            {
                return $"{targetUsername}'s pawn is dead. Use !revivepawn first.";
            }

            if (!HasInjuriesToHeal(targetPawn))
            {
                return $"{targetUsername}'s pawn doesn't have any injuries to heal.";
            }

            // Deduct coins and heal
            viewer.TakeCoins(totalCost);
            int injuriesHealed = ApplyHealing(targetPawn, quantity);

            int karmaEarned = totalCost / 100;
            if (karmaEarned > 0)
            {
                viewer.GiveKarma(karmaEarned);
                Logger.Debug($"Awarded {karmaEarned} karma for {totalCost} coin purchase");
            }

            // Send healing invoice
            string invoiceLabel = $"💚 Rimazon Healing - {user.Username} → {targetUsername}";
            string invoiceMessage = CreateMultiUserHealingInvoice(user.Username, targetUsername, injuriesHealed, totalCost, currencySymbol);
            MessageHandler.SendGreenLetter(invoiceLabel, invoiceMessage);

            Logger.Debug($"Heal specific user successful: {user.Username} healed {targetUsername}'s pawn ({injuriesHealed} injuries) for {totalCost}{currencySymbol}");
            return $"💚 HEALING! Healed {injuriesHealed} injuries on {targetUsername}'s pawn for {StoreCommandHelper.FormatCurrencyMessage(totalCost, currencySymbol)}! Remaining: {StoreCommandHelper.FormatCurrencyMessage(viewer.Coins, currencySymbol)}.";
        }

        private static string HealAllPawns(ChatMessageWrapper user, Viewer viewer, int pricePerHeal, string currencySymbol, int quantity)
        {
            var assignmentManager = CAPChatInteractiveMod.GetPawnAssignmentManager();
            var allAssignedUsernames = assignmentManager.GetAllAssignedUsernames().ToList();

            var injuredPawns = new List<(string username, Verse.Pawn pawn, int injuryCount)>();

            // Find all injured assigned pawns and count their injuries
            foreach (var username in allAssignedUsernames)
            {
                var pawn = StoreCommandHelper.GetViewerPawn(username);
                if (pawn != null && !pawn.Dead)
                {
                    int injuryCount = CountHealableInjuries(pawn);
                    if (injuryCount > 0)
                    {
                        injuredPawns.Add((username, pawn, injuryCount));
                    }
                }
            }

            if (injuredPawns.Count == 0)
            {
                return "No injured pawns found to heal.";
            }

            // Calculate cost: price per heal × total injuries across all pawns
            int totalInjuries = injuredPawns.Sum(x => x.injuryCount);
            int totalCost = totalInjuries * pricePerHeal;

            // Check if user can afford all heals
            if (!StoreCommandHelper.CanUserAfford(user, totalCost))
            {
                return $"You need {StoreCommandHelper.FormatCurrencyMessage(totalCost, currencySymbol)} to heal all {totalInjuries} injuries across {injuredPawns.Count} pawns! You have {StoreCommandHelper.FormatCurrencyMessage(viewer.Coins, currencySymbol)}.";
            }

            // Heal all injuries on all pawns
            int totalInjuriesHealed = 0;
            foreach (var (username, pawn, injuryCount) in injuredPawns)
            {
                int injuriesHealed = ApplyCompleteHealing(pawn);
                totalInjuriesHealed += injuriesHealed;
            }

            // Deduct total cost
            viewer.TakeCoins(totalCost);

            int karmaEarned = totalCost / 100;
            if (karmaEarned > 0)
            {
                viewer.GiveKarma(karmaEarned);
                Logger.Debug($"Awarded {karmaEarned} karma for {totalCost} coin purchase");
            }

            // Send healing invoice
            string invoiceLabel = $"💚 Rimazon Complete Healing - {user.Username}";
            string invoiceMessage = CreateMassHealingInvoice(user.Username, injuredPawns.Count, totalInjuriesHealed, totalCost, currencySymbol);
            MessageHandler.SendGreenLetter(invoiceLabel, invoiceMessage);

            Logger.Debug($"Heal all successful: {user.Username} healed {totalInjuriesHealed} injuries across {injuredPawns.Count} pawns for {totalCost}{currencySymbol}");
            return $"💚 COMPLETE HEALING! Healed all {totalInjuriesHealed} injuries across {injuredPawns.Count} pawns for {StoreCommandHelper.FormatCurrencyMessage(totalCost, currencySymbol)}! Remaining: {StoreCommandHelper.FormatCurrencyMessage(viewer.Coins, currencySymbol)}.";
        }

        private static int CountHealableInjuries(Verse.Pawn pawn)
        {
            return pawn.health.hediffSet.hediffs.Count(h => h.def.isBad && h.Visible && h.def.everCurableByItem);
        }

        private static int ApplyCompleteHealing(Verse.Pawn pawn)
        {
            var healableInjuries = pawn.health.hediffSet.hediffs
                .Where(h => h.def.isBad && h.Visible && h.def.everCurableByItem)
                .ToList();

            int injuriesHealed = 0;
            foreach (var injury in healableInjuries)
            {
                pawn.health.RemoveHediff(injury);
                injuriesHealed++;
                Logger.Debug($"Healed injury: {injury.def.defName} on pawn {pawn.Name}");
            }

            // Play healing sound (only once per pawn)
            if (injuriesHealed > 0)
            {
                DefDatabase<SoundDef>.GetNamed("Ingest_Inject").PlayOneShot(new TargetInfo(pawn.Position, pawn.Map));
            }

            return injuriesHealed;
        }

        private static bool HasInjuriesToHeal(Verse.Pawn pawn)
        {
            return pawn.health.hediffSet.hediffs.Any(h => h.def.isBad && h.Visible && h.def.everCurableByItem);
        }

        private static int ApplyHealing(Verse.Pawn pawn, int quantity)
        {
            int injuriesHealed = 0;

            for (int i = 0; i < quantity; i++)
            {
                // Get all healable injuries
                var healableInjuries = pawn.health.hediffSet.hediffs
                    .Where(h => h.def.isBad && h.Visible && h.def.everCurableByItem)
                    .ToList();

                if (healableInjuries.Count == 0)
                    break;

                // Heal a random injury
                var injuryToHeal = healableInjuries.RandomElement();
                pawn.health.RemoveHediff(injuryToHeal);
                injuriesHealed++;

                Logger.Debug($"Healed injury: {injuryToHeal.def.defName} on pawn {pawn.Name}");
            }

            // Play healing sound
            DefDatabase<SoundDef>.GetNamed("Ingest_Inject").PlayOneShot(new TargetInfo(pawn.Position, pawn.Map));
            return injuriesHealed;
        }

        private static string CreateHealingInvoice(string healerUsername, string targetUsername, int injuriesHealed, int price, string currencySymbol)
        {
            string invoice = $"RIMAZON HEALING SERVICE\n";
            invoice += $"====================\n";
            invoice += $"Patient: {targetUsername}\n";
            invoice += $"Service: Injury Healing\n";
            invoice += $"Injuries Healed: {injuriesHealed}\n";
            invoice += $"====================\n";
            invoice += $"Total: {price}{currencySymbol}\n";
            invoice += $"====================\n";
            invoice += $"Thank you for using Rimazon Healing!\n";
            invoice += $"Your pawn is feeling better! 💚";

            return invoice;
        }

        private static string CreateCompleteHealingInvoice(string username, int injuriesHealed, int totalPrice, string currencySymbol)
        {
            string invoice = $"RIMAZON COMPLETE HEALING SERVICE\n";
            invoice += $"====================\n";
            invoice += $"Patient: {username}\n";
            invoice += $"Service: Complete Healing\n";
            invoice += $"Injuries Healed: {injuriesHealed}\n";
            invoice += $"====================\n";
            invoice += $"Total: {totalPrice}{currencySymbol}\n";
            invoice += $"====================\n";
            invoice += $"Thank you for using Rimazon Complete Healing!\n";
            invoice += $"Your pawn is now completely healed! 💚";

            return invoice;
        }

        private static string CreateMultiUserHealingInvoice(string healerUsername, string targetUsername, int injuriesHealed, int price, string currencySymbol)
        {
            string invoice = $"RIMAZON HEALING SERVICE\n";
            invoice += $"====================\n";
            invoice += $"Healer: {healerUsername}\n";
            invoice += $"Patient: {targetUsername}\n";
            invoice += $"Service: Injury Healing\n";
            invoice += $"Injuries Healed: {injuriesHealed}\n";
            invoice += $"====================\n";
            invoice += $"Total: {price}{currencySymbol}\n";
            invoice += $"====================\n";
            invoice += $"Thank you for using Rimazon Healing!\n";
            invoice += $"A kind soul has healed {targetUsername}'s pawn! 💚";

            return invoice;
        }

        private static string CreateMassHealingInvoice(string healerUsername, int pawnsHealed, int totalInjuriesHealed, int totalPrice, string currencySymbol)
        {
            string invoice = $"RIMAZON MASS HEALING SERVICE\n";
            invoice += $"====================\n";
            invoice += $"Healer: {healerUsername}\n";
            invoice += $"Service: Mass Healing\n";
            invoice += $"Pawns Treated: {pawnsHealed}\n";
            invoice += $"Injuries Healed: {totalInjuriesHealed}\n";
            invoice += $"====================\n";
            invoice += $"Total: {totalPrice}{currencySymbol}\n";
            invoice += $"====================\n";
            invoice += $"Thank you for using Rimazon Mass Healing!\n";
            invoice += $"You have healed {totalInjuriesHealed} injuries across {pawnsHealed} pawns!\n";
            invoice += $"The colony thanks you for your generosity! 💚";

            return invoice;
        }
    }
}