// File: RevivePawnCommandHandler.cs
//
// Copyright (c) Captolamia
// This file is part of CAP Chat Interactive (RICS).
// Licensed under the GNU Affero General Public License v3.0 or later.
// See LICENSE.txt in the project root for full license text.
//
// !revivepawn [all|@user] — resurrect dead pawns with MechSerumResurrector pricing
using _CAP__Chat_Interactive.Command.CommandHelpers;
using CAP_ChatInteractive.Commands.Cooldowns;
using CAP_ChatInteractive.Store;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Verse;

namespace CAP_ChatInteractive.Commands.CommandHandlers
{
    public static class RevivePawnCommandHandler
    {
        public static string HandleRevivePawn(ChatMessageWrapper user, string[] args)
        {
            try
            {
                var settings = CAPChatInteractiveMod.Instance?.Settings?.GlobalSettings;
                if (settings == null)
                    return "RICS.RPCH.GenericError".Translate();

                var currencySymbol = settings.CurrencyName?.Trim() ?? "¢";
                var viewer = Viewers.GetViewer(user);
                if (viewer == null)
                    return "RICS.RPCH.GenericError".Translate();

                var resurrectorSerum = StoreInventory.GetStoreItem("MechSerumResurrector");
                if (resurrectorSerum == null || (!resurrectorSerum.IsUsable && !resurrectorSerum.Enabled))
                    return "RICS.RPCH.SerumNotAvailable".Translate();

                int pricePerRevive = resurrectorSerum.BasePrice;
                var cmdSettings = CommandSettingsManager.GetSettings("revivepawn");
                float mult = cmdSettings.GetCustom("reviveCostMultiplier", 1.0f);
                pricePerRevive = (int)(pricePerRevive * mult);

                args = args ?? Array.Empty<string>();
                if (args.Length == 0)
                {
                    if (!cmdSettings.GetCustom("enableSelfRevive", true))
                        return "RICS.RPCH.SelfDisabled".Translate();
                    return ReviveSelf(user, viewer, pricePerRevive, currencySymbol);
                }

                string target = args[0].Trim();
                if (target.StartsWith("@"))
                    target = target.Substring(1);

                // Multi-word: !revivepawn Cool Viewer
                if (args.Length > 1)
                    target = string.Join(" ", args.Select(a => a.Trim()).Where(a => a.Length > 0));
                if (target.StartsWith("@"))
                    target = target.Substring(1).Trim();

                if (target.Equals("all", StringComparison.OrdinalIgnoreCase))
                {
                    if (!cmdSettings.GetCustom("enableAllRevive", true))
                        return "RICS.RPCH.AllDisabled".Translate();
                    return ReviveAll(user, viewer, pricePerRevive, currencySymbol);
                }

                if (!cmdSettings.GetCustom("enableTargetRevive", true))
                    return "RICS.RPCH.TargetDisabled".Translate();
                return ReviveSpecificUser(user, viewer, target, pricePerRevive, currencySymbol);
            }
            catch (Exception ex)
            {
                Logger.Error($"[RevivePawn] Error in HandleRevivePawn: {ex}");
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
            {
                var deathInfo = GameComponent_PawnAssignmentManager.GetPawnDeathInfo(viewerPawn);
                return "RICS.RPCH.CannotResurrectSelf".Translate(deathInfo.BodyStatus, deathInfo.CauseOfDeath);
            }

            if (!StoreCommandHelper.CanUserAfford(user, price))
            {
                return "RICS.RPCH.CannotAffordSelf".Translate(
                    StoreCommandHelper.FormatCurrencyMessage(price, currencySymbol),
                    StoreCommandHelper.FormatCurrencyMessage(viewer.Coins, currencySymbol));
            }

            UseItemCommandHandler.ResurrectPawn(viewerPawn);
            if (viewerPawn.Dead)
                return "RICS.RPCH.GenericError".Translate();

            viewer.TakeCoins(price);
            AwardReviveKarma(viewer, price);
            Current.Game?.GetComponent<GlobalCooldownManager>()?.RecordItemPurchase("revive");

            string label = "RICS.RPCH.InvoiceSelfLabel".Translate(user.Username);
            string message = BuildSelfResurrectionInvoice(user.Username, price, currencySymbol);
            MessageHandler.SendPinkLetter(label, message);

            return "RICS.RPCH.ReviveSelfSuccess".Translate(
                StoreCommandHelper.FormatCurrencyMessage(price, currencySymbol),
                StoreCommandHelper.FormatCurrencyMessage(viewer.Coins, currencySymbol));
        }

        private static string ReviveSpecificUser(
            ChatMessageWrapper user,
            Viewer viewer,
            string targetUsername,
            int price,
            string currencySymbol)
        {
            if (!StoreCommandHelper.CanUserAfford(user, price))
            {
                return "RICS.RPCH.CannotAffordTarget".Translate(
                    StoreCommandHelper.FormatCurrencyMessage(price, currencySymbol),
                    targetUsername,
                    StoreCommandHelper.FormatCurrencyMessage(viewer.Coins, currencySymbol));
            }

            var assignmentManager = CAPChatInteractiveMod.GetPawnAssignmentManager();
            var targetPawn = assignmentManager?.GetAssignedPawn(targetUsername);

            if (targetPawn == null)
                return "RICS.RPCH.NoPawnForTarget".Translate(targetUsername);

            if (!targetPawn.Dead)
                return "RICS.RPCH.TargetAlreadyAlive".Translate(targetUsername);

            if (UseItemCommandHandler.CannotResurrectPawn(targetPawn))
            {
                var deathInfo = GameComponent_PawnAssignmentManager.GetPawnDeathInfo(targetPawn);
                return "RICS.RPCH.CannotResurrectTarget".Translate(
                    targetUsername, deathInfo.BodyStatus, deathInfo.CauseOfDeath);
            }

            UseItemCommandHandler.ResurrectPawn(targetPawn);
            if (targetPawn.Dead)
                return "RICS.RPCH.GenericError".Translate();

            viewer.TakeCoins(price);
            AwardReviveKarma(viewer, price);
            Current.Game?.GetComponent<GlobalCooldownManager>()?.RecordItemPurchase("revive");

            string label = "RICS.RPCH.InvoiceTargetLabel".Translate(user.Username, targetUsername);
            string message = BuildTargetResurrectionInvoice(user.Username, targetUsername, price, currencySymbol);
            MessageHandler.SendPinkLetter(label, message);

            return "RICS.RPCH.ReviveTargetSuccess".Translate(
                targetUsername,
                StoreCommandHelper.FormatCurrencyMessage(price, currencySymbol),
                StoreCommandHelper.FormatCurrencyMessage(viewer.Coins, currencySymbol));
        }

        private static string ReviveAll(ChatMessageWrapper user, Viewer viewer, int pricePerRevive, string currencySymbol)
        {
            var assignmentManager = CAPChatInteractiveMod.GetPawnAssignmentManager();
            if (assignmentManager == null)
                return "RICS.RPCH.NoDeadPawns".Translate();

            var allUsernames = assignmentManager.GetAllAssignedUsernames().ToList();
            var deadPawns = new List<(string username, Pawn pawn)>();

            foreach (var username in allUsernames)
            {
                var pawn = assignmentManager.GetAssignedPawn(username);
                if (pawn != null && pawn.Dead && !UseItemCommandHandler.CannotResurrectPawn(pawn))
                    deadPawns.Add((username, pawn));
            }

            if (deadPawns.Count == 0)
                return "RICS.RPCH.NoDeadPawns".Translate();

            int totalCost = deadPawns.Count * pricePerRevive;
            if (!StoreCommandHelper.CanUserAfford(user, totalCost))
            {
                return "RICS.RPCH.CannotAffordAll".Translate(
                    StoreCommandHelper.FormatCurrencyMessage(totalCost, currencySymbol),
                    deadPawns.Count,
                    StoreCommandHelper.FormatCurrencyMessage(viewer.Coins, currencySymbol));
            }

            int revivedCount = 0;
            foreach (var (_, pawn) in deadPawns)
            {
                if (pawn.Dead && !UseItemCommandHandler.CannotResurrectPawn(pawn))
                {
                    UseItemCommandHandler.ResurrectPawn(pawn);
                    if (!pawn.Dead)
                        revivedCount++;
                }
            }

            if (revivedCount == 0)
                return "RICS.RPCH.NoDeadPawns".Translate();

            // Charge for successful revives only
            int charge = revivedCount * pricePerRevive;
            viewer.TakeCoins(charge);
            AwardReviveKarma(viewer, charge);
            Current.Game?.GetComponent<GlobalCooldownManager>()?.RecordItemPurchase("revive");

            string label = "RICS.RPCH.InvoiceMassLabel".Translate(user.Username);
            string message = BuildMassResurrectionInvoice(user.Username, revivedCount, charge, currencySymbol);
            MessageHandler.SendPinkLetter(label, message);

            return "RICS.RPCH.MassReviveSuccess".Translate(
                revivedCount,
                StoreCommandHelper.FormatCurrencyMessage(charge, currencySymbol),
                StoreCommandHelper.FormatCurrencyMessage(viewer.Coins, currencySymbol));
        }

        private static string BuildSelfResurrectionInvoice(string username, int price, string currencySymbol)
        {
            var sb = new StringBuilder();
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
            var sb = new StringBuilder();
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
            var sb = new StringBuilder();
            sb.AppendLine("RICS.RPCH.InvoiceHeader".Translate());
            sb.AppendLine("RICS.RPCH.InvoiceReviver".Translate(reviver));
            sb.AppendLine("RICS.RPCH.InvoiceMassService".Translate());
            sb.AppendLine("RICS.RPCH.InvoicePawnsRevived".Translate(count));
            sb.AppendLine("RICS.RPCH.InvoiceTotal".Translate(StoreCommandHelper.FormatCurrencyMessage(total, currencySymbol)));
            sb.AppendLine("RICS.RPCH.InvoiceFooter".Translate());
            sb.AppendLine("RICS.RPCH.InvoiceMassThanks".Translate(count));
            return sb.ToString();
        }

        private static void AwardReviveKarma(Viewer viewer, int totalCost)
        {
            if (viewer == null || totalCost <= 0)
                return;

            var settings = CAPChatInteractiveMod.Instance?.Settings?.GlobalSettings;
            float karmaPerItem = settings?.KarmaPerStoreItem ?? 0.01f;
            float karmaEarned = totalCost * karmaPerItem / 100f;

            if (karmaEarned > 0f)
                viewer.GiveKarma(karmaEarned);
        }
    }
}
