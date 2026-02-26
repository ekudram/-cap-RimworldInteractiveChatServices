// RevivePawnCommandHandler.cs
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
// Handles the !revivepawn command to resurrect dead pawns for viewers using in-game currency.
using _CAP__Chat_Interactive.Command.CommandHelpers;
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
                var settings = CAPChatInteractiveMod.Instance.Settings.GlobalSettings;
                var currencySymbol = settings.CurrencyName?.Trim() ?? "¢";
                var viewer = Viewers.GetViewer(user);

                var resurrectorSerum = StoreInventory.GetStoreItem("MechSerumResurrector");
                if (resurrectorSerum == null || (!resurrectorSerum.IsUsable && !resurrectorSerum.Enabled))
                {
                    return "RICS.RPCH.SerumNotAvailable".Translate();
                }

                int pricePerRevive = resurrectorSerum.BasePrice;

                if (args.Length == 0)
                {
                    return ReviveSelf(user, viewer, pricePerRevive, currencySymbol);
                }

                string target = args[0].ToLowerInvariant();

                if (target == "all")
                {
                    return ReviveAll(user, viewer, pricePerRevive, currencySymbol);
                }
                else
                {
                    return ReviveSpecificUser(user, viewer, target, pricePerRevive, currencySymbol);
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error in HandleRevivePawn: {ex}");
                return "RICS.RPCH.GenericError".Translate();
            }
        }

        private static string ReviveSelf(ChatMessageWrapper user, Viewer viewer, int price, string currencySymbol)
        {
            var viewerPawn = PawnItemHelper.GetViewerPawn(user);

            if (viewerPawn == null)
                return "RICS.RPCH.NoArgsSelf".Translate();

            if (!viewerPawn.Dead)
                return "RICS.RPCH.AlreadyAlive".Translate();

            if (UseItemCommandHandler.CannotResurrectPawn(viewerPawn))
                return "RICS.RPCH.BodyDestroyed".Translate();

            if (!StoreCommandHelper.CanUserAfford(user, price))
            {
                return "RICS.RPCH.CannotAffordSelf".Translate(
                    StoreCommandHelper.FormatCurrencyMessage(price, currencySymbol),
                    StoreCommandHelper.FormatCurrencyMessage(viewer.Coins, currencySymbol)
                );
            }

            viewer.TakeCoins(price);
            UseItemCommandHandler.ResurrectPawn(viewerPawn);

            int karma = price / 100;
            if (karma > 0)
            {
                viewer.GiveKarma(karma);
            }

            var cooldownManager = Current.Game.GetComponent<GlobalCooldownManager>();
            cooldownManager?.RecordItemPurchase("revive");

            string label = "RICS.RPCH.InvoiceSelfLabel".Translate(user.Username);
            string message = BuildSelfResurrectionInvoice(user.Username, price, currencySymbol);
            MessageHandler.SendPinkLetter(label, message);

            return "RICS.RPCH.ReviveSelfSuccess".Translate(
                StoreCommandHelper.FormatCurrencyMessage(price, currencySymbol),
                StoreCommandHelper.FormatCurrencyMessage(viewer.Coins, currencySymbol)
            );
        }

        private static string ReviveSpecificUser(ChatMessageWrapper user, Viewer viewer, string targetUsername, int price, string currencySymbol)
        {
            if (!StoreCommandHelper.CanUserAfford(user, price))
            {
                return "RICS.RPCH.CannotAffordTarget".Translate(
                    StoreCommandHelper.FormatCurrencyMessage(price, currencySymbol),
                    targetUsername,
                    StoreCommandHelper.FormatCurrencyMessage(viewer.Coins, currencySymbol)
                );
            }

            var assignmentManager = CAPChatInteractiveMod.GetPawnAssignmentManager();
            var targetPawn = assignmentManager.GetAssignedPawn(targetUsername);

            if (targetPawn == null)
                return "RICS.RPCH.NoPawnForTarget".Translate(targetUsername);

            if (!targetPawn.Dead)
                return "RICS.RPCH.TargetAlreadyAlive".Translate(targetUsername);

            if (UseItemCommandHandler.CannotResurrectPawn(targetPawn))
                return "RICS.RPCH.TargetBodyDestroyed".Translate(targetUsername);

            viewer.TakeCoins(price);
            UseItemCommandHandler.ResurrectPawn(targetPawn);

            int karma = price / 100;
            if (karma > 0) viewer.GiveKarma(karma);

            var cooldownManager = Current.Game.GetComponent<GlobalCooldownManager>();
            cooldownManager?.RecordItemPurchase("revive");

            string label = "RICS.RPCH.InvoiceTargetLabel".Translate(user.Username, targetUsername);
            string message = BuildTargetResurrectionInvoice(user.Username, targetUsername, price, currencySymbol);
            MessageHandler.SendPinkLetter(label, message);

            return "RICS.RPCH.ReviveTargetSuccess".Translate(
                targetUsername,
                StoreCommandHelper.FormatCurrencyMessage(price, currencySymbol),
                StoreCommandHelper.FormatCurrencyMessage(viewer.Coins, currencySymbol)
            );
        }

        private static string ReviveAll(ChatMessageWrapper user, Viewer viewer, int pricePerRevive, string currencySymbol)
        {
            var assignmentManager = CAPChatInteractiveMod.GetPawnAssignmentManager();
            var allUsernames = assignmentManager.GetAllAssignedUsernames().ToList();

            var deadPawns = new List<(string username, Pawn pawn)>();

            foreach (var username in allUsernames)
            {
                var pawn = assignmentManager.GetAssignedPawn(username);
                if (pawn != null && pawn.Dead && !UseItemCommandHandler.CannotResurrectPawn(pawn))
                {
                    deadPawns.Add((username, pawn));
                }
            }

            if (deadPawns.Count == 0)
                return "RICS.RPCH.NoDeadPawns".Translate();

            int totalCost = deadPawns.Count * pricePerRevive;

            if (!StoreCommandHelper.CanUserAfford(user, totalCost))
            {
                return "RICS.RPCH.CannotAffordAll".Translate(
                    StoreCommandHelper.FormatCurrencyMessage(totalCost, currencySymbol),
                    deadPawns.Count,
                    StoreCommandHelper.FormatCurrencyMessage(viewer.Coins, currencySymbol)
                );
            }

            int revivedCount = 0;
            foreach (var (_, pawn) in deadPawns)
            {
                if (pawn.Dead && !UseItemCommandHandler.CannotResurrectPawn(pawn))
                {
                    UseItemCommandHandler.ResurrectPawn(pawn);
                    revivedCount++;
                }
            }

            viewer.TakeCoins(totalCost);

            int karma = pricePerRevive / 100;
            if (karma > 0) viewer.GiveKarma(karma);

            var cooldownManager = Current.Game.GetComponent<GlobalCooldownManager>();
            cooldownManager?.RecordItemPurchase("revive");

            string label = "RICS.RPCH.InvoiceMassLabel".Translate(user.Username);
            string message = BuildMassResurrectionInvoice(user.Username, revivedCount, totalCost, currencySymbol);
            MessageHandler.SendPinkLetter(label, message);

            return "RICS.RPCH.MassReviveSuccess".Translate(
                revivedCount,
                StoreCommandHelper.FormatCurrencyMessage(totalCost, currencySymbol),
                StoreCommandHelper.FormatCurrencyMessage(viewer.Coins, currencySymbol)
            );
        }

        // ───────────────────────────────────────────────
        // Helper methods for building translatable invoices
        // ───────────────────────────────────────────────

        private static string BuildSelfResurrectionInvoice(string username, int price, string currencySymbol)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("RICS.RPCH.InvoiceHeader".Translate());
            sb.AppendLine("RICS.RPCH.InvoiceReviver".Translate(username));
            sb.AppendLine("RICS.RPCH.InvoiceService".Translate());
            sb.AppendLine("RICS.RPCH.InvoiceTotal".Translate(StoreCommandHelper.FormatCurrencyMessage(price, currencySymbol)));
            sb.AppendLine("RICS.RPCH.InvoiceFooter".Translate());
            sb.AppendLine("RICS.RPCH.InvoiceSelfThanks".Translate());
            return sb.ToString();
        }

        private static string BuildTargetResurrectionInvoice(string reviver, string target, int price, string currencySymbol)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("RICS.RPCH.InvoiceHeader".Translate());
            sb.AppendLine("RICS.RPCH.InvoiceReviver".Translate(reviver));
            sb.AppendLine("RICS.RPCH.InvoiceTarget".Translate(target));
            sb.AppendLine("RICS.RPCH.InvoiceService".Translate());
            sb.AppendLine("RICS.RPCH.InvoiceTotal".Translate(StoreCommandHelper.FormatCurrencyMessage(price, currencySymbol)));
            sb.AppendLine("RICS.RPCH.InvoiceFooter".Translate());
            sb.AppendLine("RICS.RPCH.InvoiceTargetThanks".Translate(target));
            return sb.ToString();
        }

        private static string BuildMassResurrectionInvoice(string reviver, int count, int total, string currencySymbol)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("RICS.RPCH.InvoiceHeader".Translate());
            sb.AppendLine("RICS.RPCH.InvoiceReviver".Translate(reviver));
            sb.AppendLine("RICS.RPCH.InvoiceMassService".Translate());
            sb.AppendLine("RICS.RPCH.InvoicePawnsRevived".Translate(count));
            sb.AppendLine("RICS.RPCH.InvoiceTotal".Translate(StoreCommandHelper.FormatCurrencyMessage(total, currencySymbol)));
            sb.AppendLine("RICS.RPCH.InvoiceFooter".Translate());
            sb.AppendLine("RICS.RPCH.InvoiceMassThanks".Translate(count));
            return sb.ToString();
        }
    }
}