// RevivePawnCommandHandler.cs
// Copyright (c) Captolamia. All rights reserved.
// Licensed under the AGPLv3 License. See LICENSE file in the project root for full license information.
//
// Handles the !revivepawn command to resurrect dead pawns for viewers using in-game currency.
using CAP_ChatInteractive.Commands.Cooldowns;
using CAP_ChatInteractive.Store;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace CAP_ChatInteractive.Commands.CommandHandlers
{
    public static class RevivePawnCommandHandler
    {
        public static string HandleRevivePawn(ChatMessageWrapper user, string[] args)
        {
            try
            {
                Logger.Debug($"HandleRevivePawn called for user: {user.Username}, args: {string.Join(", ", args)}");

                var settings = CAPChatInteractiveMod.Instance.Settings.GlobalSettings;
                var currencySymbol = settings.CurrencyName?.Trim() ?? "¢";
                var viewer = Viewers.GetViewer(user);

                // Get the Resurrector Mech Serum store item for pricing
                var resurrectorSerum = StoreInventory.GetStoreItem("MechSerumResurrector");
                if (resurrectorSerum == null || !resurrectorSerum.Enabled)
                {
                    return "Resurrector Mech Serum is not available for revival services.";
                }

                int pricePerRevive = resurrectorSerum.BasePrice;

                // Parse command arguments
                if (args.Length == 0)
                {
                    // Revive self
                    return ReviveSelf(user, viewer, pricePerRevive, currencySymbol);
                }

                string target = args[0].ToLowerInvariant();

                if (target == "all")
                {
                    // Revive all dead pawns
                    return ReviveAll(user, viewer, pricePerRevive, currencySymbol);
                }
                else
                {
                    // Revive specific user's pawn
                    return ReviveSpecificUser(user, viewer, target, pricePerRevive, currencySymbol);
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error in HandleRevivePawn: {ex}");
                return "Error processing revive command. Please try again.";
            }
        }

        private static string ReviveSelf(ChatMessageWrapper user, Viewer viewer, int pricePerRevive, string currencySymbol)
        {
            var viewerPawn = StoreCommandHelper.GetViewerPawn(user);

            if (viewerPawn == null)
            {
                return "You don't have a pawn assigned. Use !buy pawn first.";
            }

            if (!viewerPawn.Dead)
            {
                return "Your pawn is already alive!";
            }

            if (BuyItemCommandHandler.IsPawnCompletelyDestroyed(viewerPawn))
            {
                return "Your pawn's body has been completely destroyed and cannot be revived.";
            }

            // Check if user can afford
            if (!StoreCommandHelper.CanUserAfford(user, pricePerRevive))
            {
                return $"You need {StoreCommandHelper.FormatCurrencyMessage(pricePerRevive, currencySymbol)} to revive your pawn! You have {StoreCommandHelper.FormatCurrencyMessage(viewer.Coins, currencySymbol)}.";
            }

            // Deduct coins and revive
            viewer.TakeCoins(pricePerRevive);
            BuyItemCommandHandler.ResurrectPawn(viewerPawn);

            int karmaEarned = pricePerRevive / 100;
            if (karmaEarned > 0)
            {
                viewer.GiveKarma(karmaEarned);
                Logger.Debug($"Awarded {karmaEarned} karma for {pricePerRevive} coin purchase");
            }

            // Record the purchase for cooldown tracking
            var cooldownManager = Current.Game.GetComponent<GlobalCooldownManager>();
            if (cooldownManager != null)
            {
                cooldownManager.RecordItemPurchase("revive"); // or "heal" for healpawn
            }

            // Send resurrection invoice
            string invoiceLabel = $"💖 Rimazon Resurrection - {user.Username}";
            string invoiceMessage = BuyItemCommandHandler.CreateRimazonResurrectionInvoice(user.Username, "Pawn Resurrection", pricePerRevive, currencySymbol);
            MessageHandler.SendPinkLetter(invoiceLabel, invoiceMessage);

            Logger.Debug($"Revive self successful: {user.Username} revived their pawn for {pricePerRevive}{currencySymbol}");
            return $"💖 RESURRECTION! Revived your pawn for {StoreCommandHelper.FormatCurrencyMessage(pricePerRevive, currencySymbol)}! Remaining: {StoreCommandHelper.FormatCurrencyMessage(viewer.Coins, currencySymbol)}.";
        }

        private static string ReviveSpecificUser(ChatMessageWrapper user, Viewer viewer, string targetUsername, int pricePerRevive, string currencySymbol)
        {
            // Check if user can afford
            if (!StoreCommandHelper.CanUserAfford(user, pricePerRevive))
            {
                return $"You need {StoreCommandHelper.FormatCurrencyMessage(pricePerRevive, currencySymbol)} to revive {targetUsername}'s pawn! You have {StoreCommandHelper.FormatCurrencyMessage(viewer.Coins, currencySymbol)}.";
            }

            // FIXED: Use assignment manager directly for better debugging
            var assignmentManager = CAPChatInteractiveMod.GetPawnAssignmentManager();
            var targetPawn = assignmentManager.GetAssignedPawn(targetUsername);

            Logger.Debug($"ReviveSpecificUser: Looking for pawn for '{targetUsername}'");
            Logger.Debug($"ReviveSpecificUser: Assignment manager found pawn: {targetPawn != null}");

            if (targetPawn == null)
            {
                Logger.Debug($"ReviveSpecificUser: No pawn found for username '{targetUsername}'");
                return $"{targetUsername} doesn't have a pawn assigned.";
            }

            if (!targetPawn.Dead)
            {
                return $"{targetUsername}'s pawn is already alive!";
            }

            if (BuyItemCommandHandler.IsPawnCompletelyDestroyed(targetPawn))
            {
                return $"{targetUsername}'s pawn body has been completely destroyed and cannot be revived.";
            }

            // Rest of the method remains the same...
            viewer.TakeCoins(pricePerRevive);
            BuyItemCommandHandler.ResurrectPawn(targetPawn);

            int karmaEarned = pricePerRevive / 100;
            if (karmaEarned > 0)
            {
                viewer.GiveKarma(karmaEarned);
                Logger.Debug($"Awarded {karmaEarned} karma for {pricePerRevive} coin purchase");
            }

            // Record the purchase for cooldown tracking
            var cooldownManager = Current.Game.GetComponent<GlobalCooldownManager>();
            if (cooldownManager != null)
            {
                cooldownManager.RecordItemPurchase("revive"); // or "heal" for healpawn
            }

            // Send resurrection invoice
            string invoiceLabel = $"💖 Rimazon Resurrection - {user.Username} → {targetUsername}";
            string invoiceMessage = CreateMultiUserResurrectionInvoice(user.Username, targetUsername, pricePerRevive, currencySymbol);
            MessageHandler.SendPinkLetter(invoiceLabel, invoiceMessage);

            Logger.Debug($"Revive specific user successful: {user.Username} revived {targetUsername}'s pawn for {pricePerRevive}{currencySymbol}");
            return $"💖 RESURRECTION! Revived {targetUsername}'s pawn for {StoreCommandHelper.FormatCurrencyMessage(pricePerRevive, currencySymbol)}! Remaining: {StoreCommandHelper.FormatCurrencyMessage(viewer.Coins, currencySymbol)}.";
        }

        private static string ReviveAll(ChatMessageWrapper user, Viewer viewer, int pricePerRevive, string currencySymbol)
        {
            var assignmentManager = CAPChatInteractiveMod.GetPawnAssignmentManager();
            var allAssignedUsernames = assignmentManager.GetAllAssignedUsernames().ToList();

            var deadPawns = new List<(string username, Verse.Pawn pawn)>();

            // Find all dead assigned pawns - FIXED: Use assignment manager directly
            foreach (var username in allAssignedUsernames)
            {
                var pawn = assignmentManager.GetAssignedPawn(username);
                if (pawn != null && pawn.Dead && !BuyItemCommandHandler.IsPawnCompletelyDestroyed(pawn))
                {
                    deadPawns.Add((username, pawn));
                }
            }

            // Rest of the method remains the same...
            if (deadPawns.Count == 0)
            {
                return "No dead pawns found to revive.";
            }

            int totalCost = deadPawns.Count * pricePerRevive;

            // Check if user can afford all revives
            if (!StoreCommandHelper.CanUserAfford(user, totalCost))
            {
                return $"You need {StoreCommandHelper.FormatCurrencyMessage(totalCost, currencySymbol)} to revive all {deadPawns.Count} dead pawns! You have {StoreCommandHelper.FormatCurrencyMessage(viewer.Coins, currencySymbol)}.";
            }

            // Revive all dead pawns
            int revivedCount = 0;
            foreach (var (username, pawn) in deadPawns)
            {
                // Double-check pawn is still dead and not destroyed
                if (pawn.Dead && !BuyItemCommandHandler.IsPawnCompletelyDestroyed(pawn))
                {
                    BuyItemCommandHandler.ResurrectPawn(pawn);
                    revivedCount++;
                }
            }

            // Deduct total cost
            viewer.TakeCoins(totalCost);

            int karmaEarned = pricePerRevive / 100;
            if (karmaEarned > 0)
            {
                viewer.GiveKarma(karmaEarned);
                Logger.Debug($"Awarded {karmaEarned} karma for {pricePerRevive} coin purchase");
            }

            // Record the purchase for cooldown tracking
            var cooldownManager = Current.Game.GetComponent<GlobalCooldownManager>();
            if (cooldownManager != null)
            {
                cooldownManager.RecordItemPurchase("revive"); // or "heal" for healpawn
            }

            // Send resurrection invoice
            string invoiceLabel = $"💖 Rimazon Mass Resurrection - {user.Username}";
            string invoiceMessage = CreateMassResurrectionInvoice(user.Username, revivedCount, totalCost, currencySymbol);
            MessageHandler.SendPinkLetter(invoiceLabel, invoiceMessage);

            Logger.Debug($"Revive all successful: {user.Username} revived {revivedCount} pawns for {totalCost}{currencySymbol}");
            return $"💖 MASS RESURRECTION! Revived {revivedCount} pawns for {StoreCommandHelper.FormatCurrencyMessage(totalCost, currencySymbol)}! Remaining: {StoreCommandHelper.FormatCurrencyMessage(viewer.Coins, currencySymbol)}.";
        }

        private static string CreateMultiUserResurrectionInvoice(string reviverUsername, string targetUsername, int price, string currencySymbol)
        {
            string invoice = $"RIMAZON RESURRECTION SERVICE\n";
            invoice += $"====================\n";
            invoice += $"Reviver: {reviverUsername}\n";
            invoice += $"Target: {targetUsername}\n";
            invoice += $"Service: Pawn Resurrection\n";
            invoice += $"====================\n";
            invoice += $"Total: {price}{currencySymbol}\n";
            invoice += $"====================\n";
            invoice += $"Thank you for using Rimazon Resurrection!\n";
            invoice += $"A kind soul has restored {targetUsername}'s pawn to life!\n";
            invoice += $"Life is precious - cherish every moment! 💖";

            return invoice;
        }

        private static string CreateMassResurrectionInvoice(string reviverUsername, int revivedCount, int totalPrice, string currencySymbol)
        {
            string invoice = $"RIMAZON MASS RESURRECTION SERVICE\n";
            invoice += $"====================\n";
            invoice += $"Reviver: {reviverUsername}\n";
            invoice += $"Service: Mass Resurrection\n";
            invoice += $"Pawns Revived: {revivedCount}\n";
            invoice += $"====================\n";
            invoice += $"Total: {totalPrice}{currencySymbol}\n";
            invoice += $"====================\n";
            invoice += $"Thank you for using Rimazon Mass Resurrection!\n";
            invoice += $"You have restored {revivedCount} souls to life!\n";
            invoice += $"The colony thanks you for your generosity! 💖";

            return invoice;
        }
    }
}