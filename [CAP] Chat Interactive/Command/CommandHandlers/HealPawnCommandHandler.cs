// HealPawnCommandHandler.cs
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
// Command handler for the !healpawn command
using _CAP__Chat_Interactive.Command.CommandHelpers;
using CAP_ChatInteractive.Commands.Cooldowns;
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
        public static string HandleHealPawn(ChatMessageWrapper messageWrapper, string[] args)
        {
            try
            {
                Logger.Debug($"HandleHealPawn called for user: {messageWrapper.Username}, args: {string.Join(", ", args)}");

                var settings = CAPChatInteractiveMod.Instance.Settings.GlobalSettings;
                var currencySymbol = settings.CurrencyName?.Trim() ?? "¢";
                var viewer = Viewers.GetViewer(messageWrapper);

                // Get the Healer Mech Serum store item for pricing
                var healerSerum = StoreInventory.GetStoreItem("MechSerumHealer");
                if (healerSerum == null)
                {
                    // return "Healer Mech Serum is not available for healing services.";
                    return "RICS.HPCH.Return.Notavailble".Translate();
                }

                // Check if the item is usable (IsUsable = true) OR if it's enabled (Enabled = true)
                if (!healerSerum.IsUsable && !healerSerum.Enabled)
                {
                    // return "Healer Mech Serum is not available for healing services.";
                    return "RICS.HPCH.Return.Notavailble".Translate();
                }

                int pricePerHeal = healerSerum.BasePrice;

                var cmdSettings = CommandSettingsManager.GetSettings("healpawn");
                float mult = cmdSettings.GetCustom<float>("healCostMultiplier", 1.0f);
                pricePerHeal = (int)(pricePerHeal * mult);

                // Parse command arguments
                if (args.Length == 0)
                {
                    // Heal self
                    if (!cmdSettings.GetCustom<bool>("enableSelfHeal", true))
                        return "Sub Command self is disabled.";
                    return HealSelf(messageWrapper, viewer, pricePerHeal, currencySymbol, 1);
                }

                string target = args[0].ToLowerInvariant();
                int quantity = 1;

                // Check if first argument is a number (quantity)
                if (int.TryParse(target, out int parsedQuantity) && parsedQuantity > 0)
                {
                    quantity = parsedQuantity;
                    target = args.Length > 1 ? args[1].ToLowerInvariant() : messageWrapper.Username.ToLowerInvariant();
                }
                else if (args.Length > 1 && int.TryParse(args[1], out parsedQuantity) && parsedQuantity > 0)
                {
                    quantity = parsedQuantity;
                }

                if (target == "all")
                {
                    if (!cmdSettings.GetCustom<bool>("enableAllHeal", true))
                        return "Sub Command all is disabled.";
                    return HealAllSelf(messageWrapper, viewer, pricePerHeal, currencySymbol, quantity);
                }
                if (target == "allpawns")
                {
                    if (!cmdSettings.GetCustom<bool>("enableAllHeal", true))
                        return "Sub Command all is disabled.";
                    // Heal all pawns
                    return HealAllPawns(messageWrapper, viewer, pricePerHeal, currencySymbol, quantity);
                }
                else
                {
                    if (!cmdSettings.GetCustom<bool>("enableTargetHeal", true))
                        return "Sub Command target is disabled.";
                    // Heal specific user's pawn
                    return HealSpecificUser(messageWrapper, viewer, target, pricePerHeal, currencySymbol, quantity);
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error in HandleHealPawn: {ex}");
                // return "Error processing heal command. Please try again.";  <- Better phrasing
                return "RICS.HPCH.Return.Error".Translate();
            }
        }

        private static string HealSelf(ChatMessageWrapper messageWrapper, Viewer viewer, int pricePerHeal, string currencySymbol, int quantity)
        {
            var viewerPawn = PawnItemHelper.GetViewerPawn(messageWrapper);

            if (viewerPawn == null)
                return "RICS.Pawn.NoPawn".Translate();

            if (viewerPawn.Dead)
            {
                var deathInfo = GameComponent_PawnAssignmentManager.GetPawnDeathInfo(viewerPawn);
                string deathDetails = deathInfo.ToString();
                return "RICS.HPCH.Return.PawnDead".Translate() + "RICS.Return.PawnDeadReason".Translate(deathDetails);
            }

            int maxDoses = Math.Max(1, quantity);
            maxDoses = Math.Min(maxDoses, Math.Max(0, viewer.Coins / Math.Max(1, pricePerHeal)));
            if (maxDoses == 0)
            {
                return "RICS.HPCH.Return.CantAfford".Translate(
                    StoreCommandHelper.FormatCurrencyMessage(pricePerHeal, currencySymbol),
                    StoreCommandHelper.FormatCurrencyMessage(viewer.Coins, currencySymbol));
            }

            var (successfulHeals, healedDescriptions) = ApplyHealerSerumDoses(viewerPawn, maxDoses);
            if (successfulHeals == 0)
                return "RICS.HPCH.Return.NoInjuriesHealed".Translate();

            int totalCost = successfulHeals * pricePerHeal;
            viewer.TakeCoins(totalCost);

            AwardPurchaseKarma(viewer, totalCost, "healing");
            Current.Game.GetComponent<GlobalCooldownManager>()?.RecordItemPurchase("heal");

            string invoiceLabel = $"💚 Rimazon Healing - {messageWrapper.Username}";
            string invoiceMessage = CreateHealingInvoice(
                messageWrapper.Username, messageWrapper.Username, successfulHeals, totalCost, currencySymbol, healedDescriptions);
            MessageHandler.SendGreenLetter(invoiceLabel, invoiceMessage);

            Logger.Debug(
                $"Heal self successful: {messageWrapper.Username} used {successfulHeals} Healer Serum dose(s): " +
                string.Join("; ", healedDescriptions));

            return "RICS.HPCH.Return.HealSuccess".Translate(
                StoreCommandHelper.FormatCurrencyMessage(totalCost, currencySymbol),
                StoreCommandHelper.FormatCurrencyMessage(viewer.Coins, currencySymbol),
                successfulHeals);
        }

        /// <summary>
        /// !healpawn all — apply Healer Mech Serum doses until healthy or funds run out.
        /// One dose = one vanilla serum use (worst permanent condition), NOT "remove every everCurableByItem hediff".
        /// </summary>
        private static string HealAllSelf(ChatMessageWrapper messageWrapper, Viewer viewer, int pricePerHeal, string currencySymbol, int quantity)
        {
            var viewerPawn = PawnItemHelper.GetViewerPawn(messageWrapper);

            if (viewerPawn == null)
                return "RICS.Pawn.NoPawn".Translate();

            if (viewerPawn.Dead)
            {
                var deathInfo = GameComponent_PawnAssignmentManager.GetPawnDeathInfo(viewerPawn);
                string deathDetails = deathInfo.ToString();
                return "RICS.HPCH.Return.PawnDead".Translate() + "RICS.Return.PawnDeadReason".Translate(deathDetails);
            }

            if (!CanApplyHealerSerum(viewerPawn))
                return "RICS.HPCH.Return.NoInjuries".Translate();

            // Loop until out of money OR serum has nothing left to fix (no artificial dose cap)
            int maxAffordable = Math.Max(0, viewer.Coins / Math.Max(1, pricePerHeal));
            if (maxAffordable == 0)
            {
                return "RICS.HPCH.Return.CantAfford".Translate(
                    StoreCommandHelper.FormatCurrencyMessage(pricePerHeal, currencySymbol),
                    StoreCommandHelper.FormatCurrencyMessage(viewer.Coins, currencySymbol));
            }

            // quantity arg ignored for "all" — spend as many serum doses as coins allow
            var (dosesApplied, healedDescriptions) = ApplyHealerSerumDoses(viewerPawn, maxAffordable);

            if (dosesApplied == 0)
                return "RICS.HPCH.Return.NoInjuriesHealed".Translate();

            int totalCost = dosesApplied * pricePerHeal;
            viewer.TakeCoins(totalCost);

            AwardPurchaseKarma(viewer, totalCost, "healing");
            Current.Game.GetComponent<GlobalCooldownManager>()?.RecordItemPurchase("heal");

            string invoiceLabel = $"💚 Rimazon Complete Healing - {messageWrapper.Username}";
            string invoiceMessage = CreateCompleteHealingInvoice(
                messageWrapper.Username, dosesApplied, totalCost, currencySymbol, healedDescriptions);
            MessageHandler.SendGreenLetter(invoiceLabel, invoiceMessage);

            bool moreCouldHeal = CanApplyHealerSerum(viewerPawn);
            Logger.Debug(
                $"Heal all self: {messageWrapper.Username} applied {dosesApplied} serum dose(s) for {totalCost}; " +
                $"moreNeeded={moreCouldHeal}. Fixed: {string.Join("; ", healedDescriptions)}");

            if (moreCouldHeal)
            {
                return "RICS.HPCH.Return.HealPartialSuccess".Translate(
                    StoreCommandHelper.FormatCurrencyMessage(totalCost, currencySymbol),
                    StoreCommandHelper.FormatCurrencyMessage(viewer.Coins, currencySymbol),
                    dosesApplied,
                    1); // residual “conditions remain” (funds/cap)
            }

            return "RICS.HPCH.Return.HealMassSuccess".Translate(
                StoreCommandHelper.FormatCurrencyMessage(totalCost, currencySymbol),
                StoreCommandHelper.FormatCurrencyMessage(viewer.Coins, currencySymbol),
                dosesApplied,
                1);
        }

        private static string HealSpecificUser(ChatMessageWrapper messageWrapper, Viewer viewer, string targetUsername, int pricePerHeal, string currencySymbol, int quantity)
        {
            var assignmentManager = CAPChatInteractiveMod.GetPawnAssignmentManager();
            var targetPawn = assignmentManager.GetAssignedPawn(targetUsername);

            if (targetPawn == null)
                return "RICS.HPCH.Return.TargetNoPawn".Translate(targetUsername);

            if (targetPawn.Dead)
                return "RICS.HPCH.Return.TargetPawnDead".Translate(targetUsername);

            int maxDoses = Math.Max(1, quantity);
            maxDoses = Math.Min(maxDoses, Math.Max(0, viewer.Coins / Math.Max(1, pricePerHeal)));
            if (maxDoses == 0)
            {
                return "RICS.HPCH.Return.CantAffordMultipleTarget".Translate(
                    StoreCommandHelper.FormatCurrencyMessage(pricePerHeal, currencySymbol),
                    StoreCommandHelper.FormatCurrencyMessage(viewer.Coins, currencySymbol),
                    targetUsername,
                    quantity);
            }

            var (successfulHeals, healedDescriptions) = ApplyHealerSerumDoses(targetPawn, maxDoses);
            if (successfulHeals == 0)
                return "RICS.HPCH.Return.NoInjuriesHealed".Translate();

            int totalCost = successfulHeals * pricePerHeal;
            viewer.TakeCoins(totalCost);

            AwardPurchaseKarma(viewer, totalCost, "healing");
            Current.Game.GetComponent<GlobalCooldownManager>()?.RecordItemPurchase("heal");

            string invoiceLabel = $"💚 Rimazon Healing - {messageWrapper.Username} → {targetUsername}";
            string invoiceMessage = CreateMultiUserHealingInvoice(
                messageWrapper.Username, targetUsername, successfulHeals, totalCost, currencySymbol, healedDescriptions);
            MessageHandler.SendGreenLetter(invoiceLabel, invoiceMessage);

            Logger.Debug(
                $"Heal specific: {messageWrapper.Username} → {targetUsername} {successfulHeals} dose(s): " +
                string.Join("; ", healedDescriptions));

            return "RICS.HPCH.Return.HealSuccessTarget".Translate(
                StoreCommandHelper.FormatCurrencyMessage(totalCost, currencySymbol),
                StoreCommandHelper.FormatCurrencyMessage(viewer.Coins, currencySymbol),
                successfulHeals,
                targetUsername);
        }

        private static string HealAllPawns(ChatMessageWrapper messageWrapper, Viewer viewer, int pricePerHeal, string currencySymbol, int quantity)
        {
            var assignmentManager = CAPChatInteractiveMod.GetPawnAssignmentManager();
            var allAssignedUsernames = assignmentManager.GetAllAssignedUsernames().ToList();

            var candidates = new List<(string username, Pawn pawn)>();
            foreach (var username in allAssignedUsernames)
            {
                var pawn = PawnItemHelper.GetViewerPawn(username);
                if (pawn != null && !pawn.Dead && CanApplyHealerSerum(pawn))
                    candidates.Add((username, pawn));
            }

            if (candidates.Count == 0)
                return "RICS.HPCH.Return.NoInjuredPawns".Translate();

            // Spend until coins run out or every assigned pawn is fully serum-healed
            int remainingBudget = Math.Max(0, viewer.Coins / Math.Max(1, pricePerHeal));
            if (remainingBudget == 0)
            {
                return "RICS.HPCH.Return.CantAfford".Translate(
                    StoreCommandHelper.FormatCurrencyMessage(pricePerHeal, currencySymbol),
                    StoreCommandHelper.FormatCurrencyMessage(viewer.Coins, currencySymbol));
            }

            int totalDoses = 0;
            int pawnsTreated = 0;
            var allHealedDescriptions = new List<string>();

            foreach (var (username, pawn) in candidates)
            {
                if (remainingBudget <= 0) break;

                var (doses, descs) = ApplyHealerSerumDoses(pawn, remainingBudget);
                if (doses <= 0) continue;

                totalDoses += doses;
                remainingBudget -= doses;
                pawnsTreated++;
                if (descs != null)
                {
                    foreach (var d in descs)
                        allHealedDescriptions.Add($"{username}: {d}");
                }
            }

            if (totalDoses == 0)
                return "RICS.HPCH.Return.NoInjuredPawns".Translate();

            int totalCost = totalDoses * pricePerHeal;
            viewer.TakeCoins(totalCost);

            AwardPurchaseKarma(viewer, totalCost, "mass healing");
            Current.Game.GetComponent<GlobalCooldownManager>()?.RecordItemPurchase("heal");

            string invoiceLabel = $"💚 Rimazon Complete Healing - {messageWrapper.Username}";
            string invoiceMessage = CreateMassHealingInvoice(
                messageWrapper.Username, pawnsTreated, totalDoses, totalCost, currencySymbol, allHealedDescriptions);
            MessageHandler.SendGreenLetter(invoiceLabel, invoiceMessage);

            Logger.Debug(
                $"Heal all pawns: {messageWrapper.Username} {totalDoses} dose(s) on {pawnsTreated} pawn(s) for {totalCost}");

            return "RICS.HPCH.Return.HealMassSuccess".Translate(
                StoreCommandHelper.FormatCurrencyMessage(totalCost, currencySymbol),
                StoreCommandHelper.FormatCurrencyMessage(viewer.Coins, currencySymbol),
                totalDoses,
                pawnsTreated);
        }

        // ─── Serum helpers (vanilla MechSerumHealer use-effects only) ───

        private struct HediffSnap
        {
            public string Key;
            public string Description;
            public bool IsMissingPart;
        }

        private static string GetHediffDescription(Hediff h)
        {
            if (h?.def == null)
                return "RICS.HPCH.Return.UnknownInjury".Translate();

            string injuryName = h.def.label ?? "injury";
            if (h.Part != null && !string.IsNullOrWhiteSpace(h.Part.Label))
                return $"{injuryName} ({h.Part.Label})";
            return injuryName;
        }

        private static string HediffFingerprint(Hediff h)
        {
            if (h?.def == null) return "null";
            string part = h.Part != null
                ? $"{h.Part.def?.defName ?? "?"}:{h.Part.Index}"
                : "none";
            return $"{h.def.defName}|{part}|{h.GetType().Name}";
        }

        private static List<HediffSnap> SnapshotHediffs(Pawn pawn)
        {
            var list = new List<HediffSnap>();
            if (pawn?.health?.hediffSet?.hediffs == null) return list;

            foreach (var h in pawn.health.hediffSet.hediffs)
            {
                if (h?.def == null) continue;
                bool missing = h is Hediff_MissingPart
                               || (h.def.defName != null &&
                                   h.def.defName.IndexOf("Missing", StringComparison.OrdinalIgnoreCase) >= 0);
                list.Add(new HediffSnap
                {
                    Key = HediffFingerprint(h),
                    Description = GetHediffDescription(h),
                    IsMissingPart = missing
                });
            }
            return list;
        }

        private static string DescribeHealDiff(List<HediffSnap> before, Pawn afterPawn)
        {
            if (before == null || before.Count == 0)
                return "health condition improved (unspecified)";

            var afterKeys = new HashSet<string>(
                SnapshotHediffs(afterPawn).Select(s => s.Key));

            var removed = before.Where(b => !afterKeys.Contains(b.Key)).ToList();
            if (removed.Count == 0)
                return "health condition improved (unspecified)";

            // Prefer permanent injuries/scars; then missing parts; then first
            var pick = removed.FirstOrDefault(r => !r.IsMissingPart);
            if (string.IsNullOrEmpty(pick.Key))
                pick = removed[0];

            if (pick.IsMissingPart)
            {
                // Description already like "missing body part (left arm)"
                string part = pick.Description;
                if (part.StartsWith("missing", StringComparison.OrdinalIgnoreCase))
                    return "restored " + part;
                return "restored missing body part (" + part + ")";
            }

            return pick.Description;
        }

        /// <summary>True if vanilla Healer Mech Serum use-effects accept this pawn right now.</summary>
        private static bool CanApplyHealerSerum(Pawn pawn)
        {
            if (pawn == null || pawn.Dead) return false;

            Thing serum = null;
            try
            {
                var serumDef = DefDatabase<ThingDef>.GetNamedSilentFail("MechSerumHealer");
                if (serumDef == null) return false;

                serum = ThingMaker.MakeThing(serumDef);
                if (!(serum is ThingWithComps twc)) return false;

                foreach (var comp in twc.AllComps)
                {
                    if (comp is CompUseEffect use && use.CanBeUsedBy(pawn).Accepted)
                        return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                Logger.Warning($"CanApplyHealerSerum failed: {ex.Message}");
                return false;
            }
            finally
            {
                SafeDestroyThing(serum);
            }
        }

        /// <summary>
        /// Apply up to <paramref name="maxDoses"/> vanilla Healer Mech Serum effects.
        /// Returns dose count + human-readable list of what each dose fixed (hediff snapshot diff).
        /// </summary>
        private static (int doses, List<string> descriptions) ApplyHealerSerumDoses(Pawn pawn, int maxDoses)
        {
            var descriptions = new List<string>();
            int doses = 0;
            if (pawn == null || maxDoses <= 0) return (0, descriptions);

            for (int i = 0; i < maxDoses; i++)
            {
                if (!CanApplyHealerSerum(pawn))
                    break;

                var before = SnapshotHediffs(pawn);
                if (!ApplyHealerSerumEffect(pawn))
                    break;

                doses++;
                string what = DescribeHealDiff(before, pawn);
                descriptions.Add(what);
                Logger.Debug($"Healer serum dose {doses} on {pawn.LabelShort}: {what}");
            }

            return (doses, descriptions);
        }

        private static void SafeDestroyThing(Thing t)
        {
            if (t == null || t.Destroyed) return;
            try
            {
                if (t.Spawned)
                    t.Destroy(DestroyMode.Vanish);
                else
                    t.Destroy(DestroyMode.Vanish);
            }
            catch { /* ignore */ }
        }

        private static string CreateHealingInvoice(string healerUsername, string targetUsername, int injuriesHealed, int price, string currencySymbol, List<string> healedItems = null)
        {
            string baseInvoice = "RICS.HPCH.Invoice.HealingTitle".Translate() + "\n" +
                "====================".Translate() + "\n" +
                "RICS.HPCH.Invoice.Healer".Translate(healerUsername) + "\n" +
                "RICS.HPCH.Invoice.Patient".Translate(targetUsername) + "\n" +
                "RICS.HPCH.Invoice.ServiceInjuryHealing".Translate() + "\n" +
                "RICS.HPCH.Invoice.InjuriesHealed".Translate(injuriesHealed) + "\n";

            if (healedItems != null && healedItems.Count > 0)
            {
                baseInvoice += "Injuries Treated:\n";
                int showCount = Math.Min(healedItems.Count, 6);
                for (int i = 0; i < showCount; i++)
                {
                    baseInvoice += $"• {healedItems[i]}\n";
                }
                if (healedItems.Count > showCount)
                {
                    baseInvoice += $"• ...and {healedItems.Count - showCount} more\n";
                }
                baseInvoice += "====================".Translate() + "\n";
            }
            else
            {
                baseInvoice += "====================".Translate() + "\n";
            }

            baseInvoice += "RICS.HPCH.Invoice.Total".Translate(StoreCommandHelper.FormatCurrencyMessage(price, currencySymbol)) + "\n" +
                "====================".Translate() + "\n" +
                "RICS.HPCH.Invoice.ThankYouHealing".Translate() + "\n" +
                "RICS.HPCH.Invoice.PawnFeelingBetter".Translate();

            return baseInvoice;
        }

        private static string CreateCompleteHealingInvoice(string username, int injuriesHealed, int totalPrice, string currencySymbol, List<string> healedItems = null)
        {
            string baseInvoice = "RICS.HPCH.Invoice.CompleteHealingTitle".Translate() + "\n" +
                "====================".Translate() + "\n" +
                "RICS.HPCH.Invoice.Patient".Translate(username) + "\n" +
                "RICS.HPCH.Invoice.ServiceCompleteHealing".Translate() + "\n" +
                "RICS.HPCH.Invoice.InjuriesHealed".Translate(injuriesHealed) + "\n";

            if (healedItems != null && healedItems.Count > 0)
            {
                baseInvoice += "Injuries Treated:\n";
                int showCount = Math.Min(healedItems.Count, 6);
                for (int i = 0; i < showCount; i++)
                {
                    baseInvoice += $"• {healedItems[i]}\n";
                }
                if (healedItems.Count > showCount)
                {
                    baseInvoice += $"• ...and {healedItems.Count - showCount} more\n";
                }
                baseInvoice += "====================".Translate() + "\n";
            }
            else
            {
                baseInvoice += "====================".Translate() + "\n";
            }

            baseInvoice += "RICS.HPCH.Invoice.Total".Translate(StoreCommandHelper.FormatCurrencyMessage(totalPrice, currencySymbol)) + "\n" +
                "====================".Translate() + "\n" +
                "RICS.HPCH.Invoice.ThankYouComplete".Translate() + "\n" +
                "RICS.HPCH.Invoice.PawnFeelingBetter".Translate();

            return baseInvoice;
        }

        private static string CreateMultiUserHealingInvoice(string healerUsername, string targetUsername, int injuriesHealed, int price, string currencySymbol, List<string> healedItems = null)
        {
            string baseInvoice = "RICS.HPCH.Invoice.HealingTitle".Translate() + "\n" +
                "====================".Translate() + "\n" +
                "RICS.HPCH.Invoice.Healer".Translate(healerUsername) + "\n" +
                "RICS.HPCH.Invoice.Patient".Translate(targetUsername) + "\n" +
                "RICS.HPCH.Invoice.ServiceInjuryHealing".Translate() + "\n" +
                "RICS.HPCH.Invoice.InjuriesHealed".Translate(injuriesHealed) + "\n";

            if (healedItems != null && healedItems.Count > 0)
            {
                baseInvoice += "Injuries Treated:\n";
                int showCount = Math.Min(healedItems.Count, 6);
                for (int i = 0; i < showCount; i++)
                {
                    baseInvoice += $"• {healedItems[i]}\n";
                }
                if (healedItems.Count > showCount)
                {
                    baseInvoice += $"• ...and {healedItems.Count - showCount} more\n";
                }
                baseInvoice += "====================".Translate() + "\n";
            }
            else
            {
                baseInvoice += "====================".Translate() + "\n";
            }

            baseInvoice += "RICS.HPCH.Invoice.Total".Translate(StoreCommandHelper.FormatCurrencyMessage(price, currencySymbol)) + "\n" +
                "====================".Translate() + "\n" +
                "RICS.HPCH.Invoice.ThankYouHealing".Translate() + "\n" +
                "RICS.HPCH.Invoice.KindSoulHealed".Translate(targetUsername);

            return baseInvoice;
        }

        private static string CreateMassHealingInvoice(string healerUsername, int pawnsHealed, int totalInjuriesHealed, int totalPrice, string currencySymbol, List<string> healedItems = null)
        {
            string baseInvoice = "RICS.HPCH.Invoice.MassHealingTitle".Translate() + "\n" +
                "====================".Translate() + "\n" +
                "RICS.HPCH.Invoice.Healer".Translate(healerUsername) + "\n" +
                "RICS.HPCH.Invoice.ServiceMassHealing".Translate() + "\n" +
                "RICS.HPCH.Invoice.PawnsTreated".Translate(pawnsHealed) + "\n" +
                "RICS.HPCH.Invoice.InjuriesHealed".Translate(totalInjuriesHealed) + "\n";

            if (healedItems != null && healedItems.Count > 0)
            {
                baseInvoice += "Sample Injuries Treated:\n";
                int showCount = Math.Min(healedItems.Count, 5);
                for (int i = 0; i < showCount; i++)
                {
                    baseInvoice += $"• {healedItems[i]}\n";
                }
                if (healedItems.Count > showCount)
                {
                    baseInvoice += $"• ...and {healedItems.Count - showCount} more across all pawns\n";
                }
                baseInvoice += "====================".Translate() + "\n";
            }
            else
            {
                baseInvoice += "====================".Translate() + "\n";
            }

            baseInvoice += "RICS.HPCH.Invoice.Total".Translate(StoreCommandHelper.FormatCurrencyMessage(totalPrice, currencySymbol)) + "\n" +
                "====================".Translate() + "\n" +
                "RICS.HPCH.Invoice.ThankYouMass".Translate() + "\n" +
                "RICS.HPCH.Invoice.ColonyThanks".Translate(totalInjuriesHealed, pawnsHealed);

            return baseInvoice;
        }

        /// <summary>
        /// Awards karma for any store/healing purchase using the unified KarmaPerStoreItem setting.
        /// Call this after every successful paid action (heal, buy, etc.).
        /// </summary>
        private static void AwardPurchaseKarma(Viewer viewer, int totalCost, string actionType = "purchase")
        {
            if (viewer == null || totalCost <= 0) return;

            var settings = CAPChatInteractiveMod.Instance?.Settings?.GlobalSettings;
            float karmaPerItem = settings?.KarmaPerStoreItem ?? 0.01f;

            float karmaEarned = totalCost * karmaPerItem / 100;

            if (karmaEarned > 0f)
            {
                viewer.GiveKarma(karmaEarned);
                Logger.Debug($"Awarded {karmaEarned:F2} karma for {totalCost} coin {actionType}");
            }
        }

        /// <summary>
        /// Applies the EXACT vanilla effect of one Healer Mech Serum to the pawn.
        /// Delegates hediff selection to vanilla CompUseEffect (never strips implants).
        /// Map may be null (caravan) — skip sound only.
        /// </summary>
        private static bool ApplyHealerSerumEffect(Pawn pawn)
        {
            if (pawn == null || pawn.Dead)
                return false;

            var serumDef = DefDatabase<ThingDef>.GetNamedSilentFail("MechSerumHealer");
            if (serumDef == null)
            {
                Logger.Error("MechSerumHealer def not found — cannot apply heal effect.");
                return false;
            }

            Thing serum = null;
            try
            {
                serum = ThingMaker.MakeThing(serumDef);

                if (serum is ThingWithComps thingWithComps)
                {
                    bool anyEffectApplied = false;

                    foreach (var comp in thingWithComps.AllComps)
                    {
                        if (comp is CompUseEffect compUseEffect)
                        {
                            AcceptanceReport report = compUseEffect.CanBeUsedBy(pawn);
                            if (report.Accepted)
                            {
                                compUseEffect.DoEffect(pawn);
                                anyEffectApplied = true;
                                Logger.Debug($"Applied {comp.GetType().Name} (Healer Serum) to {pawn.Name}");
                            }
                            else
                            {
                                Logger.Debug($"CompUseEffect {comp.GetType().Name} rejected: {report.Reason}");
                            }
                        }
                    }

                    if (anyEffectApplied)
                    {
                        if (pawn.Map != null && pawn.Spawned)
                            SoundDefOf.MechSerumUsed.PlayOneShot(new TargetInfo(pawn.Position, pawn.Map));
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error applying Healer Serum effect: {ex}");
            }
            finally
            {
                SafeDestroyThing(serum);
            }

            return false;
        }
    }
}