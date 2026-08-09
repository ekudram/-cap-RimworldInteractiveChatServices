// SurgeryCommandHandler.cs
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
// !surgery — purchase implant / biotech / cosmetic surgeries and queue medical bills
using _CAP__Chat_Interactive.Command.CommandHelpers;
using CAP_ChatInteractive.Utilities;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using UnityEngine;
using Verse;

namespace CAP_ChatInteractive.Commands.CommandHandlers
{
    internal static class SurgeryCommandHandler
    {
        private const string ReturnDivider = " | ";

        private static readonly Dictionary<string, string> BiotechSurgeryCommands = new()
        {
            { "hemogen", "ExtractHemogenPack" },
            { "giveblood", "ExtractHemogenPack" },
            { "transfusion", "BloodTransfusion" },
            { "getblood", "BloodTransfusion" },
            { "tubal", "TubalLigation" },
            { "tuballigation", "TubalLigation" },
            { "vasectomy", "Vasectomy" },
            { "sterilize", "STERILIZE" },
            { "iud", "ImplantIUD" },
            { "iudimplant", "ImplantIUD" },
            { "iudremove", "RemoveIUD" },
            { "vasreverse", "ReverseVasectomy" },
            { "reversovasectomy", "ReverseVasectomy" },
            { "terminate", "TerminatePregnancy" },
            { "abortion", "TerminatePregnancy" }
        };

        private static CommandSettings GetSurgerySettings()
            => CommandSettingsManager.GetSettings("surgery");

        public static string HandleSurgery(ChatMessageWrapper messageWrapper, string[] args)
        {
            try
            {
                args = args ?? Array.Empty<string>();
                if (args.Length == 0)
                    return "RICS.SBCH.Usage".Translate();

                var viewer = Viewers.GetViewer(messageWrapper);
                if (viewer == null)
                    return "RICS.SBCH.GenericError".Translate();

                var currencySymbol = CAPChatInteractiveMod.Instance?.Settings?.GlobalSettings?.CurrencyName?.Trim() ?? "¢";

                var parsed = CommandParserUtility.ParseCommandArguments(
                    args, allowQuality: false, allowMaterial: false, allowSide: true, allowQuantity: true);
                if (parsed.HasError)
                    return parsed.Error;

                string sideStr = parsed.Side ?? string.Empty;
                string quantityStr = parsed.Quantity.ToString();
                string itemName = (parsed.ItemName ?? string.Empty).ToLowerInvariant();

                string surgeryCategory = null;
                string recipeDefName = null;
                string displayName = null;
                string handlerType = null;

                if (new[] { "genderswap", "gender swap", "swapgender" }.Contains(itemName))
                {
                    handlerType = "gender";
                    displayName = "Gender Swap";
                }
                else if (new[] { "fatbody", "fat body", "fat", "body fat" }.Contains(itemName))
                {
                    handlerType = "body";
                    surgeryCategory = "fat body";
                    recipeDefName = "FatBodySurgery";
                    displayName = "Fat Body";
                }
                else if (new[] { "femininebody", "feminine body", "feminine", "bodyfeminine", "female" }.Contains(itemName))
                {
                    handlerType = "body";
                    surgeryCategory = "feminine body";
                    recipeDefName = "FeminineBodySurgery";
                    displayName = "Feminine Body";
                }
                else if (new[] { "hulkingbody", "hulking body", "hulk", "bodyhulking" }.Contains(itemName))
                {
                    handlerType = "body";
                    surgeryCategory = "hulking body";
                    recipeDefName = "HulkingBodySurgery";
                    displayName = "Hulking Body";
                }
                else if (new[] { "masculinebody", "masculine body", "masculine", "bodymasculine", "male" }.Contains(itemName))
                {
                    handlerType = "body";
                    surgeryCategory = "masculine body";
                    recipeDefName = "MasculineBodySurgery";
                    displayName = "Masculine Body";
                }
                else if (new[] { "thinbody", "thin body", "thin", "bodythin" }.Contains(itemName))
                {
                    handlerType = "body";
                    surgeryCategory = "thin body";
                    recipeDefName = "ThinBodySurgery";
                    displayName = "Thin Body";
                }
                else if (BiotechSurgeryCommands.TryGetValue(itemName, out string recipeKey))
                {
                    handlerType = "biotech";
                    recipeDefName = recipeKey;
                    displayName = CultureInfo.InvariantCulture.TextInfo.ToTitleCase(itemName);
                }

                // Only gate special surgeries — regular store implants are not misc-biotech toggles
                if (handlerType != null)
                {
                    CheckSurgeryEnabled(itemName, out bool isAllowed, out string disabledMessage);
                    if (!isAllowed)
                        return disabledMessage;
                }

                switch (handlerType)
                {
                    case "gender":
                        return HandleGenderSwapSurgery(messageWrapper, viewer, currencySymbol);
                    case "body":
                        return HandleBodyChangeSurgery(messageWrapper, viewer, currencySymbol, surgeryCategory, recipeDefName, displayName);
                    case "biotech":
                        return HandleBiotechSurgery(messageWrapper, viewer, currencySymbol, recipeDefName, displayName);
                }

                return HandleImplantSurgery(messageWrapper, viewer, currencySymbol, itemName, sideStr, quantityStr);
            }
            catch (Exception ex)
            {
                Logger.Error($"[Surgery] Error in HandleSurgery: {ex}");
                return "RICS.SBCH.GenericError".Translate();
            }
        }

        private static string HandleImplantSurgery(
            ChatMessageWrapper messageWrapper,
            Viewer viewer,
            string currencySymbol,
            string itemName,
            string sideStr,
            string quantityStr)
        {
            var storeItem = StoreCommandHelper.GetStoreItemByName(itemName);
            if (storeItem == null)
                return "RICS.SBCH.ImplantNotFound".Translate(itemName);
            if (!storeItem.Enabled)
                return "RICS.SBCH.ImplantNotAvailable".Translate(itemName);

            var thingDef = DefDatabase<ThingDef>.GetNamedSilentFail(storeItem.DefName);
            if (thingDef == null)
                return "RICS.SBCH.ImplantDefNotFound".Translate();

            if (!IsValidSurgeryItem(thingDef))
                return "RICS.SBCH.NotValidSurgeryItem".Translate(itemName);

            var researchResult = StoreCommandHelper.HasRequiredResearch(storeItem);
            if (!researchResult.Allowed)
            {
                string researchInfo = string.IsNullOrEmpty(researchResult.BlockingResearchLabel)
                    ? string.Empty
                    : ReturnDivider + researchResult.BlockingResearchLabel;
                return "RICS.SBCH.ResearchNotCompleted".Translate(itemName) + researchInfo;
            }

            Verse.Pawn viewerPawn = PawnItemHelper.GetViewerPawn(messageWrapper);
            if (viewerPawn == null)
                return "RICS.Pawn.NoPawn".Translate();

            if (viewerPawn.Destroyed || viewerPawn.Dead)
            {
                var deathInfo = GameComponent_PawnAssignmentManager.GetPawnDeathInfo(viewerPawn);
                return "RICS.Pawn.Dead".Translate()
                       + ReturnDivider
                       + "RICS.Return.PawnDeadReason".Translate(deathInfo.ToString());
            }

            if (!int.TryParse(quantityStr, out int quantity) || quantity < 1)
                quantity = 1;
            int surgeryQuantityLimit = Math.Max(storeItem.QuantityLimit, 2);
            if (quantity > surgeryQuantityLimit)
                quantity = surgeryQuantityLimit;

            int unitPrice = storeItem.BasePrice;
            int finalPrice = unitPrice * quantity;

            if (!StoreCommandHelper.CanUserAfford(messageWrapper, finalPrice))
            {
                return "RICS.SBCH.ImplantCannotAfford".Translate(
                    StoreCommandHelper.FormatCurrencyMessage(finalPrice, currencySymbol),
                    quantity,
                    itemName,
                    StoreCommandHelper.FormatCurrencyMessage(viewer.Coins, currencySymbol));
            }

            var recipe = FindSurgeryRecipeForImplant(thingDef, viewerPawn);
            if (recipe == null)
                return "RICS.SBCH.NoProcedure".Translate(itemName);

            string partError;
            var bodyParts = FindBodyPartsForSurgery(recipe, viewerPawn, sideStr, quantity, out partError);
            if (!string.IsNullOrEmpty(partError))
                return partError;

            if (bodyParts.Count == 0)
            {
                string available = GetAvailableBodyPartsDescription(recipe, viewerPawn);
                return "RICS.SBCH.NoBodyParts".Translate(itemName, available);
            }

            quantity = Math.Min(quantity, bodyParts.Count);
            finalPrice = unitPrice * quantity;

            // Spawn implants + schedule first, then charge (charge-after-success)
            var surgeryDeliveryResults = new List<DeliveryResult>();
            IntVec3 surgeryDropPos = IntVec3.Invalid;

            for (int i = 0; i < quantity; i++)
            {
                var spawnResult = ItemDeliveryHelper.SpawnItemForPawn(thingDef, 1, null, null, viewerPawn, false);
                surgeryDeliveryResults.Add(spawnResult.deliveryResult);
                if (spawnResult.deliveryPos.IsValid)
                    surgeryDropPos = spawnResult.deliveryPos;
            }

            ScheduleSurgeries(viewerPawn, recipe, bodyParts.Take(quantity).ToList());

            viewer.TakeCoins(finalPrice);
            AwardSurgeryKarma(viewer, finalPrice);

            DeliveryMethod primaryMethod = DeterminePrimaryDeliveryMethod(surgeryDeliveryResults);
            var combinedResult = new DeliveryResult
            {
                DeliveryPosition = surgeryDropPos,
                PrimaryMethod = primaryMethod,
                LockerDeliveredItems = surgeryDeliveryResults.SelectMany(r => r.LockerDeliveredItems).ToList(),
                DropPodDeliveredItems = surgeryDeliveryResults.SelectMany(r => r.DropPodDeliveredItems).ToList()
            };

            LookTargets surgeryLookTargets;
            if (combinedResult.PrimaryMethod == DeliveryMethod.Locker && combinedResult.DeliveryPosition.IsValid)
                surgeryLookTargets = new LookTargets(combinedResult.DeliveryPosition, viewerPawn.Map);
            else if (surgeryDropPos.IsValid)
                surgeryLookTargets = new LookTargets(surgeryDropPos, viewerPawn.Map);
            else
                surgeryLookTargets = new LookTargets(viewerPawn);

            string invoiceLabel = "RICS.SBCH.InvoiceSurgeryLabal".Translate(messageWrapper.Username);
            string invoiceMessage = CreateRimazonSurgeryInvoice(
                messageWrapper.Username, itemName, quantity, finalPrice, currencySymbol,
                bodyParts.Take(quantity).ToList(), combinedResult);
            MessageHandler.SendBlueLetter(invoiceLabel, invoiceMessage, surgeryLookTargets);

            string deliveryMessage = combinedResult.PrimaryMethod switch
            {
                DeliveryMethod.Locker => "RICS.SBCH.DeliveryLockerShort".Translate(),
                DeliveryMethod.DropPod => "RICS.SBCH.DeliveryDropPodShort".Translate(),
                _ => "RICS.SBCH.DeliveryColonyShort".Translate()
            };

            return "RICS.SBCH.SuccessScheduled".Translate(
                quantity,
                itemName,
                StoreCommandHelper.FormatCurrencyMessage(finalPrice, currencySymbol),
                deliveryMessage,
                StoreCommandHelper.FormatCurrencyMessage(viewer.Coins, currencySymbol));
        }

        private static void CheckSurgeryEnabled(string itemNameLower, out bool isAllowed, out string disabledMessage)
        {
            isAllowed = true;
            disabledMessage = null;
            var s = GetSurgerySettings();

            switch (itemNameLower)
            {
                case "genderswap" or "gender swap" or "swapgender":
                    isAllowed = s.GetCustom("allowGenderSwap", true);
                    disabledMessage = "RICS.SBCH.GenderSwapDisabled".Translate();
                    break;

                case "fatbody" or "fat body" or "fat" or "body fat"
                    or "femininebody" or "feminine body" or "feminine" or "bodyfeminine" or "female"
                    or "hulkingbody" or "hulking body" or "hulk" or "bodyhulking"
                    or "masculinebody" or "masculine body" or "masculine" or "bodymasculine" or "male"
                    or "thinbody" or "thin body" or "thin" or "bodythin":
                    isAllowed = s.GetCustom("allowBodyChange", true);
                    disabledMessage = "RICS.SBCH.BodyChangeDisabled".Translate();
                    break;

                case "sterilize" or "vasectomy" or "tubal" or "tuballigation":
                    isAllowed = s.GetCustom("allowSterilize", true);
                    disabledMessage = "RICS.SBCH.SterilizeDisabled".Translate();
                    break;

                case "iud" or "iudimplant" or "implant iud" or "iudremove" or "removeiud" or "remove iud":
                    isAllowed = s.GetCustom("allowIUD", true);
                    disabledMessage = "RICS.SBCH.IUDDisabled".Translate();
                    break;

                case "vasreverse" or "vas reverse" or "reversovasectomy" or "reverse vasectomy" or "reversevasectomy":
                    isAllowed = s.GetCustom("allowVasReverse", true);
                    disabledMessage = "RICS.SBCH.VasReverseDisabled".Translate();
                    break;

                case "terminate" or "termination" or "pregnancy termination" or "pregnancytermination" or "abortion":
                    isAllowed = s.GetCustom("allowTerminate", true);
                    disabledMessage = "RICS.SBCH.TerminateDisabled".Translate();
                    break;

                case "hemogen" or "giveblood" or "extract hemogen" or "extracthemogen":
                    isAllowed = s.GetCustom("allowHemogen", true);
                    disabledMessage = "RICS.SBCH.HemogenDisabled".Translate();
                    break;

                case "transfusion" or "getblood" or "blood transfusion" or "bloodtransfusion" or "blood":
                    isAllowed = s.GetCustom("allowTransfusion", true);
                    disabledMessage = "RICS.SBCH.TransfusionDisabled".Translate();
                    break;

                default:
                    isAllowed = s.GetCustom("allowMiscBiotech", true);
                    disabledMessage = "RICS.SBCH.MiscBiotechDisabled".Translate();
                    break;
            }
        }

        private static string HandleBodyChangeSurgery(
            ChatMessageWrapper messageWrapper,
            Viewer viewer,
            string currencySymbol,
            string surgeryType,
            string recipeDefName,
            string displayName)
        {
            const int quantity = 1;
            int finalPrice = GetSurgerySettings().GetCustom("bodyChangeCost", 800);

            if (!StoreCommandHelper.CanUserAfford(messageWrapper, finalPrice))
            {
                return "RICS.SBCH.BodyChangeCannotAfford".Translate(
                    StoreCommandHelper.FormatCurrencyMessage(finalPrice, currencySymbol),
                    displayName,
                    StoreCommandHelper.FormatCurrencyMessage(viewer.Coins, currencySymbol));
            }

            Verse.Pawn pawn = PawnItemHelper.GetViewerPawn(messageWrapper);
            if (pawn == null)
                return "RICS.Pawn.NoPawn".Translate();

            if (pawn.Destroyed || pawn.Dead)
            {
                var deathInfo = GameComponent_PawnAssignmentManager.GetPawnDeathInfo(pawn);
                return "RICS.Pawn.Dead".Translate()
                       + ReturnDivider
                       + "RICS.Return.PawnDeadReason".Translate(deathInfo.ToString());
            }

            if (!IsSuitableForBodyChangingSurgery(pawn, out string restrictionReason))
                return "RICS.SBCH.Sorry".Translate(restrictionReason);

            var recipe = DefDatabase<RecipeDef>.GetNamedSilentFail(recipeDefName);
            if (recipe == null)
                return "RICS.SBCH.ProcedureMissing".Translate(displayName);

            var corePart = pawn.RaceProps.body.corePart;
            if (corePart == null)
                return "RICS.SBCH.NoBodyPartsNotSpecified".Translate();

            if (HasSurgeryScheduled(pawn, recipe, corePart))
                return "RICS.SBCH.SurgeryAlreadyScheduled".Translate(displayName);

            BodyTypeDef targetBodyType = GetTargetBodyTypeForSurgery(surgeryType);
            if (targetBodyType != null && pawn.story?.bodyType == targetBodyType)
                return "RICS.SBCH.AlreadyHasBodyType".Translate(displayName);

            ScheduleSurgeries(pawn, recipe, new List<BodyPartRecord> { corePart });
            viewer.TakeCoins(finalPrice);
            AwardSurgeryKarma(viewer, finalPrice);

            LookTargets targets = new LookTargets(pawn);
            string invoiceLabel = "RICS.SBCH.InvoiceSurgeryLabal".Translate(messageWrapper.Username);
            string invoiceMessage = CreateRimazonSurgeryInvoice(
                messageWrapper.Username, displayName, quantity, finalPrice, currencySymbol,
                new List<BodyPartRecord> { corePart });
            MessageHandler.SendBlueLetter(invoiceLabel, invoiceMessage, targets);

            return "RICS.SBCH.BodyChangeSuccess".Translate(
                displayName,
                StoreCommandHelper.FormatCurrencyMessage(finalPrice, currencySymbol),
                StoreCommandHelper.FormatCurrencyMessage(viewer.Coins, currencySymbol));
        }

        private static string HandleGenderSwapSurgery(ChatMessageWrapper messageWrapper, Viewer viewer, string currencySymbol)
        {
            const int quantity = 1;
            int finalPrice = GetSurgerySettings().GetCustom("genderSwapCost", 1000);

            if (!StoreCommandHelper.CanUserAfford(messageWrapper, finalPrice))
            {
                return "RICS.SBCH.GenderSwapCannotAfford".Translate(
                    StoreCommandHelper.FormatCurrencyMessage(finalPrice, currencySymbol),
                    StoreCommandHelper.FormatCurrencyMessage(viewer.Coins, currencySymbol));
            }

            Verse.Pawn pawn = PawnItemHelper.GetViewerPawn(messageWrapper);
            if (pawn == null)
                return "RICS.SBCH.NoPawn".Translate();

            if (pawn.Destroyed || pawn.Dead)
            {
                var deathInfo = GameComponent_PawnAssignmentManager.GetPawnDeathInfo(pawn);
                return "RICS.SBCH.PawnDead".Translate()
                       + ReturnDivider
                       + "RICS.Return.PawnDeadReason".Translate(deathInfo.ToString());
            }

            if (!IsAdultForBodySurgery(pawn, out string restrictionReason))
                return "RICS.SBCH.DeniedReason".Translate(restrictionReason);

            var recipe = DefDatabase<RecipeDef>.GetNamedSilentFail("GenderSwapSurgery");
            if (recipe == null)
            {
                Logger.Error("[Surgery] GenderSwapSurgery RecipeDef not found.");
                return "RICS.SBCH.GenericError".Translate();
            }

            var corePart = pawn.RaceProps.body.corePart;
            if (corePart == null)
                return "RICS.SBCH.NoBodyPartsNotSpecified".Translate();

            if (HasSurgeryScheduled(pawn, recipe, corePart))
                return "RICS.SBCH.AlreadyScheduled.GenderSwap".Translate();

            if (pawn.gender == Gender.None)
                return "RICS.SBCH.GenderNone".Translate();

            ScheduleSurgeries(pawn, recipe, new List<BodyPartRecord> { corePart });
            viewer.TakeCoins(finalPrice);
            AwardSurgeryKarma(viewer, finalPrice);

            LookTargets targets = new LookTargets(pawn);
            string invoiceLabel = "RICS.SBCH.InvoiceSurgeryLabal".Translate(messageWrapper.Username);
            string invoiceMessage = CreateRimazonSurgeryInvoice(
                messageWrapper.Username, "Gender Swap", quantity, finalPrice, currencySymbol,
                new List<BodyPartRecord> { corePart });
            MessageHandler.SendBlueLetter(invoiceLabel, invoiceMessage, targets);

            return "RICS.SBCH.GenderSwapSuccess".Translate(
                StoreCommandHelper.FormatCurrencyMessage(finalPrice, currencySymbol),
                StoreCommandHelper.FormatCurrencyMessage(viewer.Coins, currencySymbol));
        }

        private static string HandleBiotechSurgery(
            ChatMessageWrapper messageWrapper,
            Viewer viewer,
            string currencySymbol,
            string recipeKey,
            string displayName)
        {
            const int quantity = 1;

            Verse.Pawn pawn = PawnItemHelper.GetViewerPawn(messageWrapper);
            if (pawn == null)
                return "RICS.Pawn.NoPawn".Translate();

            if (pawn.Destroyed || pawn.Dead)
            {
                var deathInfo = GameComponent_PawnAssignmentManager.GetPawnDeathInfo(pawn);
                return "RICS.Pawn.Dead".Translate()
                       + ReturnDivider
                       + "RICS.Return.PawnDeadReason".Translate(deathInfo.ToString());
            }

            if (recipeKey == "STERILIZE")
            {
                recipeKey = pawn.gender == Gender.Female ? "TubalLigation" : "Vasectomy";
                displayName = pawn.gender == Gender.Female ? "Tubal Ligation" : "Vasectomy";
            }

            RecipeDef recipe = DefDatabase<RecipeDef>.GetNamedSilentFail(recipeKey);
            if (recipe == null)
            {
                Logger.Error($"[Surgery] Biotech recipe not found: {recipeKey}");
                return "RICS.SBCH.ProcedureMissing".Translate(displayName);
            }

            int finalPrice = GetBiotechSurgeryCost(recipe.defName);

            var globalSettings = CAPChatInteractiveMod.Instance?.Settings?.GlobalSettings;
            if (globalSettings == null || globalSettings.RequireResearch)
            {
                if (recipe.researchPrerequisites != null && recipe.researchPrerequisites.Count > 0)
                {
                    var missing = recipe.researchPrerequisites
                        .Where(rp => rp != null && !rp.IsFinished)
                        .Select(rp => rp.LabelCap.ToString())
                        .Where(s => !string.IsNullOrWhiteSpace(s))
                        .Distinct()
                        .ToList();
                    if (missing.Count > 0)
                        return "RICS.SBCH.NoResearchNamed".Translate(displayName, string.Join(", ", missing));
                }
            }

            if (!IsSuitableForMiscSurgery(pawn, recipe, out string restrictionReason))
                return "RICS.SBCH.Sorry".Translate(restrictionReason);

            // Hemogen can be negative (viewer earns) — only block when they must pay
            if (finalPrice > 0 && !StoreCommandHelper.CanUserAfford(messageWrapper, finalPrice))
            {
                return "RICS.SBCH.BiotechCannotAfford".Translate(
                    StoreCommandHelper.FormatCurrencyMessage(finalPrice, currencySymbol),
                    displayName,
                    StoreCommandHelper.FormatCurrencyMessage(viewer.Coins, currencySymbol));
            }

            string partError = null;
            List<BodyPartRecord> bodyParts = recipe.targetsBodyPart
                ? FindBodyPartsForSurgery(recipe, pawn, string.Empty, 1, out partError)
                : new List<BodyPartRecord>();

            if (recipe.targetsBodyPart && !string.IsNullOrEmpty(partError))
                return partError;

            SpawnSurgeryIngredients(pawn, recipe);
            ScheduleSurgeries(pawn, recipe, bodyParts);

            if (finalPrice > 0)
                viewer.TakeCoins(finalPrice);
            else if (finalPrice < 0)
                viewer.GiveCoins(-finalPrice);

            AwardSurgeryKarma(viewer, Math.Abs(finalPrice));

            LookTargets targets = new LookTargets(pawn);
            string invoiceLabel = "RICS.SBCH.InvoiceSurgeryLabal".Translate(messageWrapper.Username);
            string invoiceMessage = CreateRimazonSurgeryInvoice(
                messageWrapper.Username, displayName, quantity, finalPrice, currencySymbol, bodyParts);
            MessageHandler.SendBlueLetter(invoiceLabel, invoiceMessage, targets);

            return "RICS.SBCH.BiotechSuccess".Translate(
                displayName,
                StoreCommandHelper.FormatCurrencyMessage(Math.Abs(finalPrice), currencySymbol),
                StoreCommandHelper.FormatCurrencyMessage(viewer.Coins, currencySymbol));
        }

        /// <summary>
        /// Select body parts for a surgery. Distinguishes missing parts, existing prosthetics/implants,
        /// already-scheduled bills, and invalid side filters so chat never says only "not found"
        /// when the real issue is an existing prosthetic (e.g. nose already has a prosthetic).
        /// </summary>
        private static List<BodyPartRecord> FindBodyPartsForSurgery(
            RecipeDef recipe,
            Verse.Pawn pawn,
            string sideFilter,
            int maxQuantity,
            out string validationError)
        {
            validationError = null;

            if (recipe == null || pawn == null)
            {
                validationError = "RICS.SBCH.NoProcedure".Translate("unknown");
                return new List<BodyPartRecord>();
            }

            var availableParts = recipe.Worker.GetPartsToApplyOn(pawn, recipe).ToList();

            if (availableParts.Count == 0)
            {
                validationError = DiagnoseNoApplicableParts(recipe, pawn);
                return new List<BodyPartRecord>();
            }

            if (!string.IsNullOrEmpty(sideFilter))
            {
                string filter = sideFilter.ToLowerInvariant();
                availableParts = availableParts
                    .Where(part =>
                        GetBodyPartSide(part).ToLowerInvariant().Contains(filter) ||
                        GetBodyPartDisplayName(part).ToLowerInvariant().Contains(filter))
                    .ToList();

                if (availableParts.Count == 0)
                {
                    string validParts = GetAvailableBodyPartsDescription(recipe, pawn);
                    validationError = "RICS.SBCH.InvalidBodyPart".Translate(sideFilter, validParts);
                    return new List<BodyPartRecord>();
                }
            }

            var installable = new List<BodyPartRecord>();
            var alreadyInstalled = new List<BodyPartRecord>();
            var alreadyScheduled = new List<BodyPartRecord>();

            foreach (var part in availableParts)
            {
                if (HasSurgeryScheduled(pawn, recipe, part))
                {
                    alreadyScheduled.Add(part);
                    continue;
                }

                if (HasImplantAlready(pawn, part, recipe))
                {
                    alreadyInstalled.Add(part);
                    continue;
                }

                installable.Add(part);
            }

            if (installable.Count == 0)
            {
                if (alreadyInstalled.Count > 0)
                {
                    string labels = string.Join(", ", alreadyInstalled.Select(GetBodyPartDisplayName).Distinct());
                    string existing = DescribeExistingImplants(pawn, alreadyInstalled);
                    validationError = "RICS.SBCH.AlreadyHasImplant".Translate(labels, existing);
                }
                else if (alreadyScheduled.Count > 0)
                {
                    validationError = "RICS.SBCH.SurgeryAlreadyScheduled".Translate(recipe.label);
                }
                else
                {
                    validationError = DiagnoseNoApplicableParts(recipe, pawn);
                }

                return new List<BodyPartRecord>();
            }

            return installable.Take(maxQuantity).ToList();
        }

        /// <summary>
        /// When GetPartsToApplyOn is empty, explain why (existing prosthetic, missing part, etc.).
        /// </summary>
        private static string DiagnoseNoApplicableParts(RecipeDef recipe, Verse.Pawn pawn)
        {
            var candidateParts = GetRecipeCandidateBodyParts(recipe, pawn);
            if (candidateParts.Count == 0)
                return "RICS.SBCH.NoBodyParts".Translate(recipe.label, "none");

            var blocked = new List<string>();
            var missing = new List<string>();

            foreach (var part in candidateParts)
            {
                string partName = GetBodyPartDisplayName(part);

                if (pawn.health.hediffSet.PartIsMissing(part))
                {
                    missing.Add(partName);
                    continue;
                }

                var implants = GetAddedPartsOrImplantsOnPart(pawn, part);
                if (implants.Count > 0)
                {
                    string names = string.Join(", ", implants.Select(h => h.LabelCap.ToString()).Distinct());
                    blocked.Add($"{partName} ({names})");
                    continue;
                }

                if (recipe.addsHediff != null &&
                    pawn.health.hediffSet.hediffs.Any(h => h.def == recipe.addsHediff && h.Part == part))
                {
                    blocked.Add($"{partName} ({recipe.addsHediff.LabelCap})");
                }
            }

            if (blocked.Count > 0)
            {
                return "RICS.SBCH.AlreadyHasImplant".Translate(
                    string.Join(", ", blocked),
                    "RICS.SBCH.RemoveExistingHint".Translate());
            }

            if (missing.Count > 0)
                return "RICS.SBCH.PartMissing".Translate(string.Join(", ", missing.Distinct()));

            return "RICS.SBCH.NoBodyParts".Translate(recipe.label, GetAvailableBodyPartsDescription(recipe, pawn));
        }

        private static List<BodyPartRecord> GetRecipeCandidateBodyParts(RecipeDef recipe, Verse.Pawn pawn)
        {
            var result = new List<BodyPartRecord>();
            if (pawn?.RaceProps?.body == null)
                return result;

            if (recipe.appliedOnFixedBodyParts != null)
            {
                foreach (BodyPartDef def in recipe.appliedOnFixedBodyParts)
                {
                    if (def == null)
                        continue;
                    foreach (BodyPartRecord part in pawn.RaceProps.body.GetPartsWithDef(def))
                    {
                        if (!result.Contains(part))
                            result.Add(part);
                    }
                }
            }

            if (recipe.appliedOnFixedBodyPartGroups != null)
            {
                foreach (BodyPartGroupDef group in recipe.appliedOnFixedBodyPartGroups)
                {
                    if (group == null)
                        continue;
                    foreach (BodyPartRecord part in pawn.RaceProps.body.AllParts)
                    {
                        if (part.groups != null && part.groups.Contains(group) && !result.Contains(part))
                            result.Add(part);
                    }
                }
            }

            // Fallback: any part currently carrying an implant that shares the recipe ingredient hediff chain
            if (result.Count == 0 && recipe.addsHediff != null)
            {
                foreach (var h in pawn.health.hediffSet.hediffs)
                {
                    if (h.Part != null && !result.Contains(h.Part) && IsAddedPartOrImplant(h))
                        result.Add(h.Part);
                }
            }

            return result;
        }

        private static List<Hediff> GetAddedPartsOrImplantsOnPart(Verse.Pawn pawn, BodyPartRecord part)
        {
            return pawn.health.hediffSet.hediffs
                .Where(h => h.Part == part && IsAddedPartOrImplant(h))
                .ToList();
        }

        private static bool IsAddedPartOrImplant(Hediff h)
        {
            if (h?.def == null)
                return false;

            if (h.def.countsAsAddedPartOrImplant)
                return true;

            if (typeof(Hediff_AddedPart).IsAssignableFrom(h.def.hediffClass))
                return true;

            if (!h.def.isBad && h.def.spawnThingOnRemoved != null)
                return true;

            return false;
        }

        private static string DescribeExistingImplants(Verse.Pawn pawn, List<BodyPartRecord> parts)
        {
            var labels = new List<string>();
            foreach (var part in parts)
            {
                foreach (var h in GetAddedPartsOrImplantsOnPart(pawn, part))
                    labels.Add(h.LabelCap.ToString());
            }

            if (labels.Count == 0)
                return "RICS.SBCH.RemoveExistingHint".Translate();

            return string.Join(", ", labels.Distinct()) + ReturnDivider + "RICS.SBCH.RemoveExistingHint".Translate();
        }

        private static RecipeDef FindSurgeryRecipeForImplant(ThingDef implantDef, Verse.Pawn pawn)
        {
            if (implantDef == null)
                return null;

            var candidates = DefDatabase<RecipeDef>.AllDefs
                .Where(r => r.IsSurgery
                            && r.ingredients != null
                            && r.ingredients.Any(i =>
                                i?.filter != null && i.filter.AllowedThingDefs.Contains(implantDef)))
                .ToList();

            if (candidates.Count == 0)
                return null;

            // Prefer a recipe that can actually apply to this pawn right now
            RecipeDef applyable = candidates.FirstOrDefault(r =>
            {
                try
                {
                    return r.AvailableOnNow(pawn) && r.Worker.GetPartsToApplyOn(pawn, r).Any();
                }
                catch
                {
                    return false;
                }
            });

            if (applyable != null)
                return applyable;

            // Fall back so we can still diagnose "already has prosthetic" instead of "no procedure"
            return candidates.FirstOrDefault(r => r.AvailableOnNow(pawn))
                   ?? candidates.FirstOrDefault();
        }

        private static string GetAvailableBodyPartsDescription(RecipeDef recipe, Verse.Pawn pawn)
        {
            try
            {
                var availableParts = recipe.Worker.GetPartsToApplyOn(pawn, recipe).ToList();
                if (availableParts.Count == 0)
                {
                    var candidates = GetRecipeCandidateBodyParts(recipe, pawn);
                    if (candidates.Count == 0)
                        return "none";
                    return string.Join(", ", candidates.Select(GetBodyPartDisplayName).Distinct());
                }

                var partGroups = availableParts
                    .GroupBy(p => GetBodyPartSide(p))
                    .Select(g => $"{g.Count()} {g.Key}")
                    .ToList();

                return string.Join(", ", partGroups);
            }
            catch
            {
                return "none";
            }
        }

        private static string CreateRimazonSurgeryInvoice(
            string username,
            string itemName,
            int quantity,
            int price,
            string currencySymbol,
            List<BodyPartRecord> bodyParts,
            DeliveryResult deliveryResult = null)
        {
            var sb = new StringBuilder();
            sb.AppendLine("RICS.SBCH.InvoiceHeader".Translate());
            sb.AppendLine("RICS.SBCH.InvoiceCustomer".Translate(username));
            sb.AppendLine("RICS.SBCH.InvoiceProcedure".Translate(itemName, quantity));

            if (bodyParts != null && bodyParts.Count > 0)
                sb.AppendLine("RICS.SBCH.InvoiceBodyParts".Translate(string.Join(", ", bodyParts.Select(bp => bp.Label))));

            if (deliveryResult != null)
            {
                if (deliveryResult.PrimaryMethod == DeliveryMethod.Locker)
                    sb.AppendLine("RICS.SBCH.InvoiceDeliveryLocker".Translate());
                else if (deliveryResult.PrimaryMethod == DeliveryMethod.DropPod)
                    sb.AppendLine("RICS.SBCH.InvoiceDeliveryDropPod".Translate());
                else if (deliveryResult.LockerDeliveredItems.Count > 0 && deliveryResult.DropPodDeliveredItems.Count > 0)
                {
                    sb.AppendLine("RICS.SBCH.InvoiceDeliveryMixed".Translate(
                        deliveryResult.LockerDeliveredItems.Sum(t => t.stackCount),
                        deliveryResult.DropPodDeliveredItems.Sum(t => t.stackCount)));
                }
                else
                    sb.AppendLine("RICS.SBCH.InvoiceDeliveryColony".Translate());
            }
            else
            {
                sb.AppendLine("RICS.SBCH.InvoiceServiceFallback".Translate());
            }

            sb.AppendLine("RICS.SBCH.InvoiceTotal".Translate(price, currencySymbol));
            sb.AppendLine("RICS.SBCH.InvoiceFooter".Translate());

            if (deliveryResult != null)
            {
                if (deliveryResult.PrimaryMethod == DeliveryMethod.Locker)
                    sb.AppendLine("RICS.SBCH.InvoiceImplantsLocker".Translate());
                else if (deliveryResult.PrimaryMethod == DeliveryMethod.DropPod)
                    sb.AppendLine("RICS.SBCH.InvoiceImplantsDropPod".Translate());
                else
                    sb.AppendLine("RICS.SBCH.InvoiceImplantsColony".Translate());
            }

            return sb.ToString();
        }

        private static DeliveryMethod DeterminePrimaryDeliveryMethod(List<DeliveryResult> results)
        {
            int lockerItems = results.Sum(r => r.LockerDeliveredItems.Count);
            int dropPodItems = results.Sum(r => r.DropPodDeliveredItems.Count);

            if (lockerItems > 0 && dropPodItems == 0)
                return DeliveryMethod.Locker;
            if (lockerItems == 0 && dropPodItems > 0)
                return DeliveryMethod.DropPod;
            return DeliveryMethod.DropPod;
        }

        private static string GetBodyPartDisplayName(BodyPartRecord part)
        {
            if (part == null)
                return "unknown";
            return !string.IsNullOrEmpty(part.customLabel) ? part.customLabel : part.Label;
        }

        private static string GetBodyPartSide(BodyPartRecord part)
        {
            var label = (!string.IsNullOrEmpty(part.customLabel) ? part.customLabel : part.Label).ToLowerInvariant();
            if (label.Contains("left"))
                return "left";
            if (label.Contains("right"))
                return "right";
            return "center";
        }

        private static bool HasImplantAlready(Verse.Pawn pawn, BodyPartRecord part, RecipeDef recipe)
        {
            if (recipe.addsHediff != null)
            {
                if (pawn.health.hediffSet.hediffs.Any(h => h.def == recipe.addsHediff && h.Part == part))
                    return true;
            }

            // Same install item already present (e.g. prosthetic nose blocking another prosthetic nose)
            if (recipe.addsHediff == null)
                return false;

            // If the recipe would add a hediff that is already represented by an equal-or-better added part
            // of the same spawnThingOnRemoved, treat as already installed.
            var existing = GetAddedPartsOrImplantsOnPart(pawn, part);
            foreach (var h in existing)
            {
                if (h.def == recipe.addsHediff)
                    return true;
            }

            return false;
        }

        private static bool HasSurgeryScheduled(Verse.Pawn pawn, RecipeDef recipe, BodyPartRecord part)
        {
            if (pawn?.health?.surgeryBills?.Bills == null)
                return false;

            if (part == null)
            {
                return pawn.health.surgeryBills.Bills.OfType<Bill_Medical>()
                    .Any(b => b.recipe == recipe);
            }

            return pawn.health.surgeryBills.Bills.OfType<Bill_Medical>()
                .Any(b => b.recipe == recipe && b.Part == part);
        }

        private static bool IsValidSurgeryItem(ThingDef thingDef)
        {
            if (thingDef == null)
                return false;
            if (thingDef.isTechHediff)
                return true;
            if (thingDef.defName.IndexOf("Bionic", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
            if (thingDef.defName.IndexOf("Prosthetic", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
            if (thingDef.defName.IndexOf("Implant", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;

            return DefDatabase<RecipeDef>.AllDefs.Any(r =>
                r.IsSurgery
                && r.ingredients != null
                && r.ingredients.Any(i => i?.filter != null && i.filter.AllowedThingDefs.Contains(thingDef)));
        }

        private static void ScheduleSurgeries(Verse.Pawn pawn, RecipeDef recipe, List<BodyPartRecord> bodyParts)
        {
            if (bodyParts == null || bodyParts.Count == 0)
            {
                pawn.health.surgeryBills.AddBill(new Bill_Medical(recipe, null));
                return;
            }

            foreach (var bodyPart in bodyParts)
            {
                pawn.health.surgeryBills.AddBill(new Bill_Medical(recipe, null) { Part = bodyPart });
            }
        }

        private static bool IsSuitableForBodyChangingSurgery(Verse.Pawn pawn, out string reason)
        {
            reason = null;

            if (pawn == null)
            {
                reason = "RICS.SBCH.NullPawn".Translate();
                return false;
            }

            if (!IsAdultForBodySurgery(pawn, out reason))
                return false;

            var currentBodyType = pawn.story?.bodyType;
            if (currentBodyType != null)
            {
                var allowed = new HashSet<BodyTypeDef>
                {
                    BodyTypeDefOf.Fat,
                    BodyTypeDefOf.Female,
                    BodyTypeDefOf.Hulk,
                    BodyTypeDefOf.Male,
                    BodyTypeDefOf.Thin,
                    BodyTypeDefOf.Child
                };

                if (!allowed.Contains(currentBodyType))
                {
                    reason = "RICS.SBCH.UniqueBodyType".Translate(currentBodyType.LabelCap);
                    return false;
                }
            }

            if (pawn.genes != null)
            {
                GeneDef delicateGene = DefDatabase<GeneDef>.GetNamedSilentFail("Delicate");
                if (delicateGene != null && pawn.genes.HasActiveGene(delicateGene))
                {
                    reason = "RICS.SBCH.DelicateGene".Translate();
                    return false;
                }

                bool hasConflictingBodyGene = pawn.genes.GenesListForReading.Any(g =>
                    g.def.defName.Contains("Body") ||
                    g.def.defName.Contains("Furskin") ||
                    g.def.defName.Contains("Trotter") ||
                    g.def.defName.Contains("Waster"));

                if (hasConflictingBodyGene)
                {
                    reason = "RICS.SBCH.ConflictingGene".Translate();
                    return false;
                }
            }

            if (pawn.Ideo != null && pawn.Ideo.memes.Any(m =>
                    m.defName == "FleshPurity" ||
                    m.defName.Contains("Purity") ||
                    m.defName.Contains("Purist")))
            {
                reason = "RICS.SBCH.FleshPurity".Translate();
                return false;
            }

            return true;
        }

        private static bool IsAdultForBodySurgery(Verse.Pawn pawn, out string reason)
        {
            reason = null;

            if (pawn == null)
            {
                reason = "RICS.SBCH.NullPawn".Translate();
                return false;
            }

            if (pawn.ageTracker != null)
            {
                float biologicalAge = pawn.ageTracker.AgeBiologicalYearsFloat;
                const float minAdultAge = 14f;
                if (biologicalAge < minAdultAge)
                {
                    reason = "RICS.SBCH.TooYoung".Translate(biologicalAge, minAdultAge);
                    return false;
                }
            }

            if (pawn.story?.bodyType == BodyTypeDefOf.Child)
            {
                reason = "RICS.SBCH.ChildBodyType".Translate();
                return false;
            }

            if (pawn.health?.hediffSet != null)
            {
                var pregnancyHediff = pawn.health.hediffSet.hediffs
                    .FirstOrDefault(h =>
                        h.def.defName.ToLowerInvariant().Contains("pregnancy") ||
                        h is Hediff_Pregnant);

                if (pregnancyHediff != null)
                {
                    reason = "RICS.SBCH.Pregnant".Translate();
                    return false;
                }
            }

            return true;
        }

        private static BodyTypeDef GetTargetBodyTypeForSurgery(string surgeryType)
        {
            return (surgeryType ?? string.Empty).ToLowerInvariant() switch
            {
                "fat body" => BodyTypeDefOf.Fat,
                "feminine body" => BodyTypeDefOf.Female,
                "hulking body" => BodyTypeDefOf.Hulk,
                "masculine body" => BodyTypeDefOf.Male,
                "thin body" => BodyTypeDefOf.Thin,
                _ => null
            };
        }

        private static int GetBiotechSurgeryCost(string recipeDefName)
        {
            var s = GetSurgerySettings();
            return recipeDefName switch
            {
                "TubalLigation" or "Vasectomy" => s.GetCustom("sterilizeCost", 400),
                "ImplantIUD" => s.GetCustom("iudCost", 250),
                "RemoveIUD" => s.GetCustom("iudCost", 250) / 2,
                "ReverseVasectomy" => s.GetCustom("vasReverseCost", 500),
                "TerminatePregnancy" => s.GetCustom("terminateCost", 300),
                "ExtractHemogenPack" => s.GetCustom("hemogenCost", -100),
                "BloodTransfusion" => s.GetCustom("transfusionCost", 200),
                _ => s.GetCustom("miscBiotechCost", 350)
            };
        }

        private static void SpawnSurgeryIngredients(Verse.Pawn pawn, RecipeDef recipe)
        {
            if (pawn == null || recipe?.ingredients == null)
                return;

            foreach (IngredientCount ing in recipe.ingredients)
            {
                float countFloat = ing.CountFor(recipe);
                int count = Mathf.RoundToInt(countFloat);
                if (count <= 0)
                    continue;

                ThingDef toSpawn = ing.FixedIngredient ?? ThingDefOf.MedicineIndustrial;
                if (toSpawn == null)
                    continue;

                Thing thing = ThingMaker.MakeThing(toSpawn);
                thing.stackCount = count;

                if (pawn.inventory?.innerContainer == null || !pawn.inventory.innerContainer.TryAdd(thing))
                {
                    if (pawn.Map != null)
                        GenDrop.TryDropSpawn(thing, pawn.Position, pawn.Map, ThingPlaceMode.Near, out _);
                }
            }

            if (recipe.fixedIngredientFilter?.AllowedThingDefs != null)
            {
                ThingDef specialDef = recipe.fixedIngredientFilter.AllowedThingDefs.FirstOrDefault();
                if (specialDef != null)
                {
                    Thing special = ThingMaker.MakeThing(specialDef);
                    if (pawn.inventory?.innerContainer == null || !pawn.inventory.innerContainer.TryAdd(special))
                    {
                        if (pawn.Map != null)
                            GenDrop.TryDropSpawn(special, pawn.Position, pawn.Map, ThingPlaceMode.Near, out _);
                    }
                }
            }
        }

        private static bool IsSuitableForMiscSurgery(Verse.Pawn pawn, RecipeDef recipe, out string reason)
        {
            reason = null;

            if (recipe.minAllowedAge > 0 && pawn.ageTracker?.AgeBiologicalYearsFloat < recipe.minAllowedAge)
            {
                reason = "RICS.SBCH.MinAgeRequired".Translate(recipe.minAllowedAge);
                return false;
            }

            if (recipe.genderPrerequisite == Gender.Female && pawn.gender != Gender.Female)
            {
                reason = "RICS.SBCH.RequiresFemale".Translate();
                return false;
            }

            if (recipe.genderPrerequisite == Gender.Male && pawn.gender != Gender.Male)
            {
                reason = "RICS.SBCH.RequiresMale".Translate();
                return false;
            }

            if (recipe.incompatibleWithHediffTags != null)
            {
                foreach (string forbiddenTag in recipe.incompatibleWithHediffTags)
                {
                    if (pawn.health.hediffSet.hediffs.Any(h =>
                            h.def.tags != null && h.def.tags.Contains(forbiddenTag)))
                    {
                        reason = "RICS.SBCH.IncompatibleCondition".Translate(forbiddenTag);
                        return false;
                    }
                }
            }

            if (HasSurgeryScheduled(pawn, recipe, null))
            {
                reason = "RICS.SBCH.AlreadyScheduled".Translate();
                return false;
            }

            switch (recipe.defName)
            {
                case "TerminatePregnancy":
                    bool isPregnant = pawn.health.hediffSet.hediffs.Any(h =>
                        h.def.defName.ToLowerInvariant().Contains("pregnancy") ||
                        h is Hediff_Pregnant);
                    if (!isPregnant)
                    {
                        reason = "RICS.SBCH.NotPregnant".Translate();
                        return false;
                    }
                    break;

                case "ReverseVasectomy":
                    if (!pawn.health.hediffSet.HasHediff(DefDatabase<HediffDef>.GetNamedSilentFail("Vasectomy")))
                    {
                        reason = "RICS.SBCH.NoVasectomy".Translate();
                        return false;
                    }
                    break;

                case "RemoveIUD":
                    if (!pawn.health.hediffSet.HasHediff(DefDatabase<HediffDef>.GetNamedSilentFail("ImplantedIUD")))
                    {
                        reason = "RICS.SBCH.NoIUD".Translate();
                        return false;
                    }
                    break;

                case "BloodTransfusion":
                    bool needsBlood = pawn.health.hediffSet.HasHediff(HediffDefOf.BloodLoss);
                    bool isHemogenic = pawn.genes != null &&
                        pawn.genes.GenesListForReading.Any(g =>
                            g.def == GeneDefOf.Bloodfeeder ||
                            g.def == GeneDefOf.Hemogenic);

                    if (!needsBlood && !isHemogenic)
                    {
                        reason = "RICS.SBCH.NoBloodLoss".Translate();
                        return false;
                    }
                    break;
            }

            if (!IsAdultForBodySurgery(pawn, out reason))
                return false;

            return true;
        }

        private static void AwardSurgeryKarma(Viewer viewer, int totalCost)
        {
            if (viewer == null || totalCost <= 0)
                return;

            float karmaPerItem = CAPChatInteractiveMod.Instance?.Settings?.GlobalSettings?.KarmaPerStoreItem ?? 0.01f;
            float karmaEarned = totalCost * karmaPerItem / 100f;
            if (karmaEarned > 0f)
                viewer.GiveKarma(karmaEarned);
        }
    }
}
