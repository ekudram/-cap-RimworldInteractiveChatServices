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

// Filename: SurgeryBuyItemCommandHandler.cs
using _CAP__Chat_Interactive.Command.CommandHelpers;
using CAP_ChatInteractive.Utilities;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
using Verse;

/// <summary>
/// Surgery Command Handler for CAP Chat Interactive
/// </summary>
namespace CAP_ChatInteractive.Commands.CommandHandlers
{
    /// <summary>
    /// SurgeryBuyItemCommandHandler handles the !surgery command for purchasing and scheduling surgeries for implants.
    /// </summary>
    internal static class SurgeryItemCommandHandler
    {
        private static readonly Dictionary<string, string> BiotechSurgeryCommands = new()
        {
            { "hemogen", "ExtractHemogenPack" },
            { "giveblood", "ExtractHemogenPack" },
            { "transfusion", "BloodTransfusion" },
            { "getblood", "BloodTransfusion" },
            { "tubal", "TubalLigation" },
            { "tuballigation", "TubalLigation" },
            { "vasectomy", "Vasectomy" },
            { "sterilize", "STERILIZE" }, // Special: auto-select based on gender
            { "iud", "ImplantIUD" },
            { "iudimplant", "ImplantIUD" },
            { "iudremove", "RemoveIUD" },
            { "vasreverse", "ReverseVasectomy" },
            { "reversovasectomy", "ReverseVasectomy" },
            { "terminate", "TerminatePregnancy" },
            { "abortion", "TerminatePregnancy" }
            // Add more as needed, e.g. "ovum" -> "ExtractOvum" if you want IVF chain
        };
        /// <summary>
        /// Main handler for the !surgery command.
        /// </summary>
        /// <param name="messageWrapper"></param>
        /// <param name="args"></param>
        /// <returns></returns>
        public static string HandleSurgery(ChatMessageWrapper messageWrapper, string[] args)
        {
            try
            {
                Logger.Debug($"HandleSurgery called for user: {messageWrapper.Username}, args: {string.Join(", ", args)}");

                if (args.Length == 0)
                {
                    return "RICS.SBCH.Usage".Translate();
                }

                var settings = CAPChatInteractiveMod.Instance.Settings.GlobalSettings;
                var currencySymbol = settings.CurrencyName?.Trim() ?? "¢";
                var viewer = Viewers.GetViewer(messageWrapper);

                var parsed = CommandParserUtility.ParseCommandArguments(args, allowQuality: false, allowMaterial: false, allowSide: true, allowQuantity: true);
                if (parsed.HasError)
                    return parsed.Error;

                string sideStr = parsed.Side;
                string quantityStr = parsed.Quantity.ToString();

                string itemName = parsed.ItemName.ToLowerInvariant();

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
                    handlerType = "body"; surgeryCategory = "fat body"; recipeDefName = "FatBodySurgery"; displayName = "Fat Body";
                }
                else if (new[] { "femininebody", "feminine body", "feminine", "bodyfeminine", "female" }.Contains(itemName))
                {
                    handlerType = "body"; surgeryCategory = "feminine body"; recipeDefName = "FeminineBodySurgery"; displayName = "Feminine Body";
                }
                else if (new[] { "hulkingbody", "hulking body", "hulk", "bodyhulking" }.Contains(itemName))
                {
                    handlerType = "body"; surgeryCategory = "hulking body"; recipeDefName = "HulkingBodySurgery"; displayName = "Hulking Body";
                }
                else if (new[] { "masculinebody", "masculine body", "masculine", "bodymasculine", "male" }.Contains(itemName))
                {
                    handlerType = "body"; surgeryCategory = "masculine body"; recipeDefName = "MasculineBodySurgery"; displayName = "Masculine Body";
                }
                else if (new[] { "thinbody", "thin body", "thin", "bodythin" }.Contains(itemName))
                {
                    handlerType = "body"; surgeryCategory = "thin body"; recipeDefName = "ThinBodySurgery"; displayName = "Thin Body";
                }
                else if (BiotechSurgeryCommands.TryGetValue(itemName, out string recipeKey))
                {
                    handlerType = "biotech";
                    recipeDefName = recipeKey;
                    displayName = CultureInfo.InvariantCulture.TextInfo.ToTitleCase(itemName);
                }

                bool isAllowed;
                string disabledMessage;
                CheckSurgeryEnabled(settings, itemName, out isAllowed, out disabledMessage);

                if (!isAllowed)
                {
                    return disabledMessage;
                }

                switch (handlerType)
                {
                    case "gender": return HandleGenderSwapSurgery(messageWrapper, viewer, currencySymbol);
                    case "body": return HandleBodyChangeSurgery(messageWrapper, viewer, currencySymbol, surgeryCategory, recipeDefName, displayName);
                    case "biotech": return HandleBiotechSurgery(messageWrapper, viewer, currencySymbol, recipeDefName, displayName);
                }

                // ── Regular implant surgery path (unchanged logic, only strings translated) ──
                var storeItem = StoreCommandHelper.GetStoreItemByName(itemName);
                if (storeItem == null) return "RICS.SBCH.ImplantNotFound".Translate(itemName);
                if (!storeItem.Enabled) return "RICS.SBCH.ImplantNotAvailable".Translate(itemName);

                var thingDef = DefDatabase<ThingDef>.GetNamedSilentFail(storeItem.DefName);
                if (thingDef == null) return "RICS.SBCH.ImplantDefNotFound".Translate();

                if (!IsValidSurgeryItem(thingDef))
                    return "RICS.SBCH.NotValidSurgeryItem".Translate(itemName);

                if (!StoreCommandHelper.HasRequiredResearch(storeItem))
                    return "RICS.SBCH.ResearchNotCompleted".Translate(itemName);

                Verse.Pawn viewerPawn = PawnItemHelper.GetViewerPawn(messageWrapper);
                if (viewerPawn == null) return "RICS.SBCH.NoPawn".Translate();
                if (viewerPawn.Dead) return "RICS.SBCH.PawnDead".Translate();

                if (!int.TryParse(quantityStr, out int quantity) || quantity < 1) quantity = 1;
                int surgeryQuantityLimit = Math.Max(storeItem.QuantityLimit, 2);
                if (quantity > surgeryQuantityLimit) quantity = surgeryQuantityLimit;

                int finalPrice = storeItem.BasePrice * quantity;

                if (!StoreCommandHelper.CanUserAfford(messageWrapper, finalPrice))
                {
                    return $"You need {StoreCommandHelper.FormatCurrencyMessage(finalPrice, currencySymbol)} for {quantity}x {itemName} surgery! You have {StoreCommandHelper.FormatCurrencyMessage(viewer.Coins, currencySymbol)}.";
                }

                var recipe = FindSurgeryRecipeForImplant(thingDef, viewerPawn);
                if (recipe == null) return "RICS.SBCH.NoProcedure".Translate(itemName);

                var bodyParts = FindBodyPartsForSurgery(recipe, viewerPawn, sideStr, quantity);
                if (bodyParts.Count == 0)
                {
                    string available = GetAvailableBodyPartsDescription(recipe, viewerPawn);
                    return "RICS.SBCH.NoBodyParts".Translate(itemName, available);
                }

                // Limit quantity to available body parts
                quantity = Math.Min(quantity, bodyParts.Count);

                // Adjust final price for actual quantity
                finalPrice = storeItem.BasePrice * quantity;

                // Deduct coins
                viewer.TakeCoins(finalPrice);

                int karmaEarned = finalPrice / 100;
                if (karmaEarned > 0)
                {
                    viewer.GiveKarma(karmaEarned);
                    // Logger.Debug($"Awarded {karmaEarned} karma for {finalPrice} coin surgery");
                }

                // Track delivery results
                List<DeliveryResult> surgeryDeliveryResults = new List<DeliveryResult>();
                List<Thing> allSurgeryItems = new List<Thing>();
                IntVec3 surgeryDropPos = IntVec3.Invalid;

                for (int i = 0; i < quantity; i++)
                {
                    var spawnResult = ItemDeliveryHelper.SpawnItemForPawn(thingDef, 1, null, null, viewerPawn, false);
                    allSurgeryItems.AddRange(spawnResult.spawnedThings);
                    surgeryDeliveryResults.Add(spawnResult.deliveryResult);

                    if (spawnResult.deliveryPos.IsValid)
                    {
                        surgeryDropPos = spawnResult.deliveryPos;
                    }

                    Logger.Debug($"Spawned surgery item {i + 1} of {quantity}: {thingDef.defName}");
                }

                // Schedule the surgeries
                ScheduleSurgeries(viewerPawn, recipe, bodyParts.Take(quantity).ToList());


                // Update the LookTargets creation for surgery:
                LookTargets surgeryLookTargets;

                // Determine primary delivery method from all results
                DeliveryMethod primaryMethod = DeterminePrimaryDeliveryMethod(surgeryDeliveryResults);

                // Create a combined delivery result for the invoice
                DeliveryResult combinedResult = new DeliveryResult
                {
                    DeliveryPosition = surgeryDropPos,
                    PrimaryMethod = primaryMethod,
                    LockerDeliveredItems = surgeryDeliveryResults.SelectMany(r => r.LockerDeliveredItems).ToList(),
                    DropPodDeliveredItems = surgeryDeliveryResults.SelectMany(r => r.DropPodDeliveredItems).ToList()
                };

                if (combinedResult.PrimaryMethod == DeliveryMethod.Locker && combinedResult.DeliveryPosition.IsValid)
                {
                    // Items went to locker - target the locker
                    surgeryLookTargets = new LookTargets(combinedResult.DeliveryPosition, viewerPawn.Map);
                    // Logger.Debug($"Created LookTargets for locker position: {combinedResult.DeliveryPosition}");
                }
                else if (surgeryDropPos.IsValid)
                {
                    // Items were dropped somewhere - target that spot
                    surgeryLookTargets = new LookTargets(surgeryDropPos, viewerPawn.Map);
                    // Logger.Debug($"Created LookTargets for surgery item drop position: {surgeryDropPos}");
                }
                else
                {
                    // Fallback to targeting the patient
                    surgeryLookTargets = new LookTargets(viewerPawn);
                    // Logger.Debug($"Created LookTargets for patient: {viewerPawn.Name}");
                }

                // Create the invoice with actual delivery info
                // string invoiceLabel = $"🏥 Rimazon Surgery - {messageWrapper.Username}";
                string invoiceLabel = "RICS.SBCH.InvoiceSurgeryLabal".Translate(messageWrapper.Username);
                string invoiceMessage = CreateRimazonSurgeryInvoice(
                    messageWrapper.Username, itemName, quantity, finalPrice, currencySymbol,
                    bodyParts.Take(quantity).ToList(), combinedResult);
                MessageHandler.SendBlueLetter(invoiceLabel, invoiceMessage, surgeryLookTargets);

                // Logger.Debug($"Surgery scheduled: {messageWrapper.Username} scheduled {quantity}x {itemName} for {finalPrice}{currencySymbol}");

                string deliveryMessage = combinedResult.PrimaryMethod switch
                {
                    DeliveryMethod.Locker => "delivered to your Rimazon Locker",
                    DeliveryMethod.DropPod => "delivered via drop pod",
                    _ => "delivered to colony"
                };

                return "RICS.SBCH.SuccessScheduled".Translate(quantity, itemName, StoreCommandHelper.FormatCurrencyMessage(finalPrice, currencySymbol), deliveryMessage, StoreCommandHelper.FormatCurrencyMessage(viewer.Coins, currencySymbol));
            }
            catch (Exception ex)
            {
                Logger.Error($"Error in HandleSurgery: {ex}");
                return "RICS.SBCH.GenericError".Translate();
            }
        }

        // ──── SURGERY ENABLED CHECK METHOD ────
        private static void CheckSurgeryEnabled(CAPGlobalChatSettings settings, string itemNameLower, out bool isAllowed, out string disabledMessage)
        {
            isAllowed = true;
            disabledMessage = null;

            switch (itemNameLower)
            {
                case "genderswap" or "gender swap" or "swapgender":
                    isAllowed = settings.SurgeryAllowGenderSwap;
                    disabledMessage = "RICS.SBCH.GenderSwapDisabled".Translate();
                    break;

                case "fatbody" or "fat body" or "fat" or "body fat" or "femininebody" or "feminine body" or "feminine" or "bodyfeminine" or "hulkingbody" or "hulking body" or "hulk" or "bodyhulking" or "masculinebody" or "masculine body" or "masculine" or "bodymasculine" or "thinbody" or "thin body" or "thin" or "bodythin":
                    isAllowed = settings.SurgeryAllowBodyChange;
                    disabledMessage = "RICS.SBCH.BodyChangeDisabled".Translate();
                    break;

                case "sterilize" or "vasectomy" or "tubal" or "tuballigation":
                    isAllowed = settings.SurgeryAllowSterilize;
                    disabledMessage = "RICS.SBCH.SterilizeDisabled".Translate();
                    break;

                case "iud" or "iudimplant" or "implant iud" or "iudremove" or "removeiud" or "remove iud":
                    isAllowed = settings.SurgeryAllowIUD;
                    disabledMessage = "RICS.SBCH.IUDDisabled".Translate();
                    break;

                case "vasreverse" or "vas reverse" or "reversovasectomy" or "reverse vasectomy" or "reversevasectomy":
                    isAllowed = settings.SurgeryAllowVasReverse;
                    disabledMessage = "RICS.SBCH.VasReverseDisabled".Translate();
                    break;

                case "terminate" or "termination" or "pregnancy termination" or "pregnancytermination" or "abortion":
                    isAllowed = settings.SurgeryAllowTerminate;
                    disabledMessage = "RICS.SBCH.TerminateDisabled".Translate();
                    break;

                case "hemogen" or "giveblood" or "extract hemogen" or "extracthemogen":
                    isAllowed = settings.SurgeryAllowHemogen;
                    disabledMessage = "RICS.SBCH.HemogenDisabled".Translate();
                    break;

                case "transfusion" or "getblood" or "blood transfusion" or "bloodtransfusion" or "blood":
                    isAllowed = settings.SurgeryAllowTransfusion;
                    disabledMessage = "RICS.SBCH.TransfusionDisabled".Translate();
                    break;

                default:
                    isAllowed = settings.SurgeryAllowMiscBiotech;
                    disabledMessage = "RICS.SBCH.MiscBiotechDisabled".Translate();
                    break;
            }
        }
        // In SurgeryBuyItemCommandHandler.cs, add these new methods below HandleGenderSwapSurgery

        private static string HandleBodyChangeSurgery(ChatMessageWrapper messageWrapper, Viewer viewer, string currencySymbol, string surgeryType, string recipeDefName, string displayName)
        {
            const int quantity = 1;

            var globalSettings = CAPChatInteractiveMod.Instance.Settings.GlobalSettings;
            int finalPrice = globalSettings.SurgeryBodyChangeCost; // Assume a new global setting for body change cost, e.g., 800 default

            if (!StoreCommandHelper.CanUserAfford(messageWrapper, finalPrice))
            {
                return "RICS.SBCH.BodyChangeCannotAfford".Translate(
                    StoreCommandHelper.FormatCurrencyMessage(finalPrice, currencySymbol),
                    displayName,
                    StoreCommandHelper.FormatCurrencyMessage(viewer.Coins, currencySymbol));
            }
            // Get viewer's pawn
            Verse.Pawn pawn = PawnItemHelper.GetViewerPawn(messageWrapper);
            if (pawn == null)
                // return "You need to have a pawn in the colony to perform surgery. Use !buy pawn first.";
                return "RICS.SBCH.NoPawn".Translate();
            if (pawn.Dead)
                // return "Your pawn is dead. You cannot perform surgery.";
                return "RICS.SBCH.PawnDead".Translate();

            // Body validation
            if (!IsSuitableForBodyChangingSurgery(pawn, out string restrictionReason))
            {
                // return $"Sorry, this surgery cannot be performed: {restrictionReason}";
                return "RICS.SBCH.Sorry".Translate(restrictionReason);
            }

            var recipe = DefDatabase<RecipeDef>.GetNamedSilentFail(recipeDefName);
            if (recipe == null)
            {
                // Logger.Error($"{recipeDefName} RecipeDef not found.");
                return $"Error: {displayName} procedure not available (mod configuration issue).";
            }

            var corePart = pawn.RaceProps.body.corePart;
            if (corePart == null)
                // return "Error: No suitable body part found for surgery.";
                return "RICS.SBCH.NoBodyPartsNotSpecified".Translate() ;

            if (HasSurgeryScheduled(pawn, recipe, corePart))
                // return $"{displayName} surgery is already scheduled for your pawn. Please wait.";
                return "RICS.SBCH.SurgeryAlreadyScheduled".Translate(displayName);

            BodyTypeDef targetBodyType = GetTargetBodyTypeForSurgery(surgeryType); // Define this helper method
            if (targetBodyType != null && pawn.story.bodyType == targetBodyType)
            {
                // return $"Your pawn already has a {displayName.ToLower()} body type. No change needed!";
                return "RICS.SBCH.AlreadyHasBodyType".Translate(displayName);
            }

            viewer.TakeCoins(finalPrice);

            int karmaEarned = finalPrice / 100; // Needs to be a configurable setting.
            if (karmaEarned > 0)
            {
                viewer.GiveKarma(karmaEarned);
                // Logger.Debug($"Awarded {karmaEarned} karma for {displayName} surgery");
            }

            ScheduleSurgeries(pawn, recipe, new List<BodyPartRecord> { corePart });

            LookTargets targets = new LookTargets(pawn);
            //string invoiceLabel = $"🏥 Rimazon Surgery - {messageWrapper.Username}";
            string invoiceLabel = "RICS.SBCH.InvoiceSurgeryLabal".Translate(messageWrapper.Username);
            string invoiceMessage = CreateRimazonSurgeryInvoice(
                messageWrapper.Username, displayName, quantity, finalPrice, currencySymbol,
                new List<BodyPartRecord> { corePart });

            MessageHandler.SendBlueLetter(invoiceLabel, invoiceMessage, targets);

            // Logger.Debug($"{displayName} surgery scheduled for {messageWrapper.Username} - {finalPrice}{currencySymbol}");

            return "RICS.SBCH.BodyChangeSuccess".Translate(
                    displayName,
                    StoreCommandHelper.FormatCurrencyMessage(finalPrice, currencySymbol),
                    StoreCommandHelper.FormatCurrencyMessage(viewer.Coins, currencySymbol));
        }

        private static string HandleGenderSwapSurgery(ChatMessageWrapper messageWrapper, Viewer viewer, string currencySymbol)
        {
            const int quantity = 1;

            var globalSettings = CAPChatInteractiveMod.Instance.Settings.GlobalSettings;
            int finalPrice = globalSettings.SurgeryGenderSwapCost;
            if (!StoreCommandHelper.CanUserAfford(messageWrapper, finalPrice))
            {
                return "RICS.SBCH.GenderSwapCannotAfford".Translate(
                    StoreCommandHelper.FormatCurrencyMessage(finalPrice, currencySymbol),
                    StoreCommandHelper.FormatCurrencyMessage(viewer.Coins, currencySymbol));
            }

            // Get viewer's pawn
            Verse.Pawn pawn = PawnItemHelper.GetViewerPawn(messageWrapper);
            if (pawn == null)
                return "RICS.SBCH.NoPawn".Translate(); //"You need to have a pawn in the colony to perform surgery. Use !buy pawn first.";
            if (pawn.Dead)
                return "RICS.SBCH.PawnDead".Translate();  //"Your pawn is dead. You cannot perform surgery.";

            // Age validation
            if (!IsAdultForBodySurgery(pawn, out string restrictionReason))
            {
                return "RICS.SBCH.DeniedReason".Translate(restrictionReason);  //$"Sorry, gender swap surgery cannot be performed: {restrictionReason}";
            }

            var recipe = DefDatabase<RecipeDef>.GetNamedSilentFail("GenderSwapSurgery");
            if (recipe == null)
            {
                Logger.Error("GenderSwapSurgery RecipeDef not found.");
                return "RICS.SBCH.GenericError".Translate(); //  Error: Gender swap procedure not available (mod configuration issue).";
            }

            var corePart = pawn.RaceProps.body.corePart;
            if (corePart == null)
                // return "Error: No suitable body part found for surgery.";\
                return "RICS.SBCH.NotSuitableAge".Translate();



            if (HasSurgeryScheduled(pawn, recipe, corePart))
                return "RICS.SBCH.AlreadyScheduled.GenderSwap".Translate(); // Gender swap surgery is already scheduled for your pawn. Please wait.";

            // Optional: prevent redundant swaps (comment out if you want to allow funny double-swaps)
            if (pawn.gender == Gender.None) return "Your pawn has no gender to swap... mysterious.";

            viewer.TakeCoins(finalPrice);

            // Optional: smaller or no karma
            int karmaEarned = finalPrice / 200; // ← more conservative, or set to 0 / small fixed value
            if (karmaEarned > 0)
            {
                viewer.GiveKarma(karmaEarned);
                // Logger.Debug($"Awarded {karmaEarned} karma for gender swap purchase");
            }

            ScheduleSurgeries(pawn, recipe, new List<BodyPartRecord> { corePart });

            LookTargets targets = new LookTargets(pawn);
            string invoiceLabel = "RICS.SBCH.InvoiceSurgeryLabal".Translate(messageWrapper.Username);  //$"🏥 Rimazon Surgery - {messageWrapper.Username}";
            string invoiceMessage = CreateRimazonSurgeryInvoice(
                messageWrapper.Username, "Gender Swap", quantity, finalPrice, currencySymbol,
                new List<BodyPartRecord> { corePart });

            MessageHandler.SendBlueLetter(invoiceLabel, invoiceMessage, targets);

            Logger.Debug($"Gender swap scheduled for {messageWrapper.Username} - {finalPrice}{currencySymbol}");

            return "RICS.SBCH.GenderSwapSuccess".Translate(
                    StoreCommandHelper.FormatCurrencyMessage(finalPrice, currencySymbol),
                    StoreCommandHelper.FormatCurrencyMessage(viewer.Coins, currencySymbol));
        }

        private static string HandleBiotechSurgery(ChatMessageWrapper messageWrapper, Viewer viewer, string currencySymbol, string recipeKey, string displayName)
        {
            const int quantity = 1; // Fixed to 1 for misc surgeries

            Verse.Pawn pawn = PawnItemHelper.GetViewerPawn(messageWrapper);
            if (pawn == null)
                return "RICS.SBCH.NoPawn".Translate(); //"You need to have a pawn in the colony to perform surgery. Use !buy pawn first.";
            if (pawn.Dead)
                return "RICS.SBCH.PawnDead".Translate();  //"Your pawn is dead. You cannot perform surgery.";

            // Special handling for 'sterilize' → override key & name
            if (recipeKey == "STERILIZE")
            {
                recipeKey = pawn.gender == Gender.Female ? "TubalLigation" : "Vasectomy";
                displayName = pawn.gender == Gender.Female ? "Tubal Ligation" : "Vasectomy";
            }

            // Now look up the recipe (always done, after possible override)
            RecipeDef recipe = DefDatabase<RecipeDef>.GetNamedSilentFail(recipeKey);
            if (recipe == null)
            {
                Logger.Error($"Biotech recipe not found: {recipeKey}");
                return $"Error: {displayName} not available (recipe missing).";
            }

            // Price (uses the possibly overridden recipe.defName)
            int finalPrice = GetBiotechSurgeryCost(recipe.defName);

            // Research check – correct way in RimWorld
            if (recipe.researchPrerequisites != null &&
                !recipe.researchPrerequisites.All(rp => rp.IsFinished))
            {
                // return $"{displayName} requires research that hasn't been completed yet.";
                return "RICS.SBCH.NoResearch".Translate(displayName) ;
            }

            // Validation
            if (!IsSuitableForMiscSurgery(pawn, recipe, out string restrictionReason))
            {
                // return $"Cannot perform {displayName}: {restrictionReason}";
                return "RICS.SBCH.Sorry".Translate(restrictionReason) ;
            }

            // Affordability
            if (!StoreCommandHelper.CanUserAfford(messageWrapper, finalPrice))
            {
                return "RICS.SBCH.BiotechCannotAfford".Translate(
                    StoreCommandHelper.FormatCurrencyMessage(finalPrice, currencySymbol),
                    displayName,
                    StoreCommandHelper.FormatCurrencyMessage(viewer.Coins, currencySymbol));
            }

            // Spawn required ingredients (medicine + any fixed like HemogenPack)
            SpawnSurgeryIngredients(pawn, recipe);

            // Deduct coins & give karma
            viewer.TakeCoins(finalPrice);
            int karmaEarned = finalPrice / 200;
            if (karmaEarned > 0) viewer.GiveKarma(karmaEarned);

            // Body parts: most misc surgeries don't target parts → empty list
            List<BodyPartRecord> bodyParts = recipe.targetsBodyPart
                ? FindBodyPartsForSurgery(recipe, pawn, "", 1)   // 'parsed' must be in scope!
                : new List<BodyPartRecord>();

            // Schedule the bill(s)
            ScheduleSurgeries(pawn, recipe, bodyParts);

            // Invoice & notification
            LookTargets targets = new LookTargets(pawn);
            // string invoiceLabel = $"🏥 Rimazon Surgery - {messageWrapper.Username}";
            string invoiceLabel = "RICS.SBCH.InvoiceSurgeryLabal".Translate(messageWrapper.Username);
            string invoiceMessage = CreateRimazonSurgeryInvoice(
                messageWrapper.Username, displayName, quantity, finalPrice, currencySymbol, bodyParts);
            MessageHandler.SendBlueLetter(invoiceLabel, invoiceMessage, targets);

            // Logger.Debug($"{displayName} ({recipeKey}) scheduled for {messageWrapper.Username} - {finalPrice}{currencySymbol}");

            return "RICS.SBCH.BiotechSuccess".Translate(
                    displayName,
                    StoreCommandHelper.FormatCurrencyMessage(finalPrice, currencySymbol),
                    StoreCommandHelper.FormatCurrencyMessage(viewer.Coins, currencySymbol));
        }

        // ===== BODY PART SELECTION METHODS =====
        private static List<BodyPartRecord> FindBodyPartsForSurgery(RecipeDef recipe, Verse.Pawn pawn, string sideFilter, int maxQuantity)
        {
            Logger.Debug($"FindBodyPartsForSurgery - Recipe: {recipe.defName}, SideFilter: {sideFilter}, MaxQuantity: {maxQuantity}");

            // Let RimWorld tell us which parts this surgery applies to
            var availableParts = recipe.Worker.GetPartsToApplyOn(pawn, recipe).ToList();
            Logger.Debug($"Initial available parts from recipe: {availableParts.Count}");

            // Filter by side if specified
            if (!string.IsNullOrEmpty(sideFilter))
            {
                var beforeFilterCount = availableParts.Count;
                availableParts = availableParts
                    .Where(part => GetBodyPartSide(part).ToLower().Contains(sideFilter.ToLower()))
                    .ToList();
                Logger.Debug($"After side filter '{sideFilter}': {beforeFilterCount} -> {availableParts.Count}");
            }

            // Remove parts that already have this surgery scheduled or the implant already installed
            var beforeDedupeCount = availableParts.Count;
            availableParts = availableParts
                .Where(part => !HasSurgeryScheduled(pawn, recipe, part) && !HasImplantAlready(pawn, part, recipe))
                .ToList();
            Logger.Debug($"After deduplication: {beforeDedupeCount} -> {availableParts.Count}");

            // Log available parts for debugging
            if (availableParts.Count > 0)
            {
                Logger.Debug($"Available body parts: {string.Join(", ", availableParts.Select(p => $"{GetBodyPartDisplayName(p)}"))}");
            }
            else
            {
                Logger.Debug("No available body parts found after all filters");
            }

            // Limit to requested quantity
            return availableParts.Take(maxQuantity).ToList();
        }

        private static RecipeDef FindSurgeryRecipeForImplant(ThingDef implantDef, Verse.Pawn pawn)
        {
            return DefDatabase<RecipeDef>.AllDefs
                .Where(r => r.IsSurgery && r.AvailableOnNow(pawn))
                .FirstOrDefault(r => r.ingredients.Any(i => i.filter.AllowedThingDefs.Contains(implantDef)));
        }

        private static string GetAvailableBodyPartsDescription(RecipeDef recipe, Verse.Pawn pawn)

        {
            var availableParts = recipe.Worker.GetPartsToApplyOn(pawn, recipe).ToList();
            if (availableParts.Count == 0) return "none";

            // Group by side and get unique part types
            var partGroups = availableParts
                .GroupBy(p => GetBodyPartSide(p))
                .Select(g => $"{g.Count()} {g.Key} parts")
                .ToList();

            return string.Join(", ", partGroups);
        }

        // ===== INVOICE CREATION METHODS =====
        private static string CreateRimazonSurgeryInvoice(string username, string itemName, int quantity, int price,
            string currencySymbol, List<BodyPartRecord> bodyParts, DeliveryResult deliveryResult = null)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("RICS.SBCH.InvoiceHeader".Translate());
            sb.AppendLine("RICS.SBCH.InvoiceCustomer".Translate(username));
            sb.AppendLine("RICS.SBCH.InvoiceProcedure".Translate(itemName, quantity));

            if (bodyParts.Count > 0)
                sb.AppendLine("RICS.SBCH.InvoiceBodyParts".Translate(string.Join(", ", bodyParts.Select(bp => bp.Label))));

            if (deliveryResult != null)
            {
                if (deliveryResult.PrimaryMethod == DeliveryMethod.Locker)
                    sb.AppendLine("RICS.SBCH.InvoiceDeliveryLocker".Translate());
                else if (deliveryResult.PrimaryMethod == DeliveryMethod.DropPod)
                    sb.AppendLine("RICS.SBCH.InvoiceDeliveryDropPod".Translate());
                else if (deliveryResult.LockerDeliveredItems.Count > 0 && deliveryResult.DropPodDeliveredItems.Count > 0)
                    sb.AppendLine("RICS.SBCH.InvoiceDeliveryMixed".Translate(
                        deliveryResult.LockerDeliveredItems.Sum(t => t.stackCount),
                        deliveryResult.DropPodDeliveredItems.Sum(t => t.stackCount)));
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
            else if (lockerItems == 0 && dropPodItems > 0)
                return DeliveryMethod.DropPod;
            else if (lockerItems > 0 && dropPodItems > 0)
                return DeliveryMethod.DropPod; // Mixed, prioritize drop pod
            else
                return DeliveryMethod.DropPod; // Default
        }

        // ===== BODY PART METHODS =====
        private static string GetBodyPartDisplayName(BodyPartRecord part)
        {
            return !string.IsNullOrEmpty(part.customLabel) ? part.customLabel : part.Label;
        }

        private static string GetBodyPartSide(BodyPartRecord part)
        {
            // Use customLabel if available, otherwise use label
            var label = (!string.IsNullOrEmpty(part.customLabel) ? part.customLabel : part.Label).ToLower();

            if (label.Contains("left")) return "left";
            if (label.Contains("right")) return "right";
            return "center";
        }

        private static bool HasImplantAlready(Verse.Pawn pawn, BodyPartRecord part, RecipeDef recipe)
        {
            // Check if the pawn already has the hediff that this surgery would add
            if (recipe.addsHediff != null)
            {
                return pawn.health.hediffSet.hediffs.Any(h =>
                    h.def == recipe.addsHediff && h.Part == part);
            }
            return false;
        }

        private static bool HasSurgeryScheduled(Verse.Pawn pawn, RecipeDef recipe, BodyPartRecord part)
        {
            if(part == null)
            {
                return pawn.health.surgeryBills.Bills.OfType<Bill_Medical>()
                .Any(b => b.recipe == recipe && (part == null || b.Part == part));
            }
            return pawn.health.surgeryBills.Bills.Any(bill =>
                bill is Bill_Medical medicalBill &&
                medicalBill.recipe == recipe &&
                medicalBill.Part == part);
        }

        // ===== VALIDATION METHODS =====

        private static bool IsValidSurgeryItem(ThingDef thingDef)
        {
            // Check if this is an implant, bionic part, or other surgical item
            if (thingDef.isTechHediff) return true;
            if (thingDef.defName.Contains("Bionic") || thingDef.defName.Contains("Prosthetic")) return true;
            if (thingDef.defName.Contains("Implant")) return true;

            // Check if there are any recipes that use this item as an ingredient for surgery
            var surgeryRecipes = DefDatabase<RecipeDef>.AllDefs
                .Where(r => r.IsSurgery && r.ingredients.Any(i => i.filter.AllowedThingDefs.Contains(thingDef)))
                .ToList();

            return surgeryRecipes.Count > 0;
        }

        private static void ScheduleSurgeries(Verse.Pawn pawn, RecipeDef recipe, List<BodyPartRecord> bodyParts)
        {
            if (bodyParts.Count == 0)
            {
                var bill = new Bill_Medical(recipe, null);
                // Part == null by default
                pawn.health.surgeryBills.AddBill(bill);
                Logger.Debug($"Scheduled {recipe.defName} (no part) for {pawn.Name}");
            }
            else
            {
                foreach (var bodyPart in bodyParts)
                {
                    var bill = new Bill_Medical(recipe, null) { Part = bodyPart };
                    pawn.health.surgeryBills.AddBill(bill);
                    Logger.Debug($"Scheduled {recipe.defName} on {bodyPart.Label} for {pawn.Name}");
                }
            }
        } 

        private static bool IsSuitableForBodyChangingSurgery(Verse.Pawn pawn, out string reason)
        {
            reason = null;

            if (pawn == null)
            {
                reason = "No pawn found.";
                return false;
            }

            // Age check
            if (!IsAdultForBodySurgery(pawn, out reason))
            {
                return false;
            }

            // Check for HAR (Humanoid Alien Races) custom body types
            var currentBodyType = pawn.story?.bodyType;
            if (currentBodyType != null)
            {
                var vanillaBodyTypes = new HashSet<BodyTypeDef>
        {
            BodyTypeDefOf.Fat,
            BodyTypeDefOf.Female,
            BodyTypeDefOf.Hulk,
            BodyTypeDefOf.Male,
            BodyTypeDefOf.Thin,
            BodyTypeDefOf.Child
        };

                if (!vanillaBodyTypes.Contains(currentBodyType))
                {
                    reason = $"Your pawn has a unique {currentBodyType.LabelCap} body type from their race. " +
                            "Major body reshaping is not compatible with their physiology.";
                    return false;
                }
            }

            // Gene checks
            if (pawn.genes != null)
            {
                // === NEW: Check for the 'Delicate' Gene (Genie Xenotype) ===
                GeneDef delicateGene = DefDatabase<GeneDef>.GetNamedSilentFail("Delicate");
                if (delicateGene != null && pawn.genes.HasActiveGene(delicateGene))
                {
                    reason = "Your pawn has the 'Delicate' gene, making major body reshaping unsafe.";
                    return false;
                }

                // Your existing other gene checks remain here...
                bool hasConflictingBodyGene = pawn.genes.GenesListForReading.Any(g =>
                    g.def.defName.Contains("Body") ||
                    g.def.defName.Contains("Furskin") ||
                    g.def.defName.Contains("Trotter") ||
                    g.def.defName.Contains("Waster")
                );

                if (hasConflictingBodyGene)
                {
                    reason = "Your pawn's unique genetic makeup (xenogenes) makes major body reshaping unsafe or incompatible. " +
                             "Consider gene extraction/reimplantation first.";
                    return false;
                }
            }



            // Ideology check (your existing code continues...)
            if (pawn.Ideo != null && pawn.Ideo.memes.Any(m =>
                m.defName == "FleshPurity" ||
                m.defName.Contains("Purity") ||
                m.defName.Contains("Purist")))
            {
                reason = "Your pawn follows a flesh purity ideology and refuses major artificial body modification.";
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

            // 1. Biological age check (most reliable)
            if (pawn.ageTracker != null)
            {
                float biologicalAge = pawn.ageTracker.AgeBiologicalYearsFloat;
                const float MIN_ADULT_AGE = 14f;  // ← you can later make this a setting if desired

                if (biologicalAge < MIN_ADULT_AGE)
                {
                    reason = "RICS.SBCH.TooYoung".Translate(biologicalAge, MIN_ADULT_AGE);
                    Logger.Debug($"Pawn {pawn.Name} is too young (bio age: {biologicalAge:F1}) for body-changing surgery");
                    return false;
                }
            }

            // 2. Fallback: Check body type (useful for modded races or missing age tracker)
            if (pawn.story != null && pawn.story.bodyType != null)
            {
                if (pawn.story.bodyType == BodyTypeDefOf.Child)
                {
                    reason = "RICS.SBCH.ChildBodyType".Translate();
                    Logger.Debug($"Pawn {pawn.Name} has Child body type - blocking body surgery");
                    return false;
                }
            }

            // 3. Pregnancy check – major body mods unsafe during pregnancy
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

                // Optional: you can add lactating check later if desired
                // if (pawn.health.hediffSet.HasHediff(HediffDefOf.Lactating))
                // {
                //     reason = "RICS.SBCH.LactatingBlocked".Translate();  // ← add key if you enable this
                //     return false;
                // }
            }

            // All checks passed
            reason = null;
            return true;
        }

        private static BodyTypeDef GetTargetBodyTypeForSurgery(string surgeryType)
        {
            switch (surgeryType.ToLower())
            {
                case "fat body": return BodyTypeDefOf.Fat;
                case "feminine body": return BodyTypeDefOf.Female;
                case "hulking body": return BodyTypeDefOf.Hulk;
                case "masculine body": return BodyTypeDefOf.Male;
                case "thin body": return BodyTypeDefOf.Thin;
                default: return null;
            }
        }

        private static int GetBiotechSurgeryCost(string recipeDefName)
        {
            return recipeDefName switch
            {
                "TubalLigation" or "Vasectomy" => CAPChatInteractiveMod.Instance.Settings.GlobalSettings.SurgerySterilizeCost,
                "ImplantIUD" => CAPChatInteractiveMod.Instance.Settings.GlobalSettings.SurgeryIUDCost,
                "RemoveIUD" => CAPChatInteractiveMod.Instance.Settings.GlobalSettings.SurgeryIUDCost / 2, // Cheaper
                "ReverseVasectomy" => CAPChatInteractiveMod.Instance.Settings.GlobalSettings.SurgeryVasReverseCost,
                "TerminatePregnancy" => CAPChatInteractiveMod.Instance.Settings.GlobalSettings.SurgeryTerminateCost,
                "ExtractHemogenPack" => CAPChatInteractiveMod.Instance.Settings.GlobalSettings.SurgeryHemogenCost,
                "BloodTransfusion" => CAPChatInteractiveMod.Instance.Settings.GlobalSettings.SurgeryTransfusionCost,
                _ => CAPChatInteractiveMod.Instance.Settings.GlobalSettings.SurgeryMiscBiotechCost
            };
        }

        private static void SpawnSurgeryIngredients(Verse.Pawn pawn, RecipeDef recipe)
        {
            // Medicine (from ingredients) - sum the required counts
            int medCount = 0;

            // Revised: Separate medicine from other ingredients
            foreach (IngredientCount ing in recipe.ingredients)
            {
                float countFloat = ing.CountFor(recipe);
                int count = Mathf.RoundToInt(countFloat);
                if (count <= 0) continue;

                ThingDef toSpawn = ing.FixedIngredient ?? ThingDefOf.MedicineIndustrial;
                if (toSpawn == null)
                {
                    Logger.Warning($"No spawnable ThingDef for ingredient {ing} in {recipe.defName}");
                    continue;
                }

                Thing thing = ThingMaker.MakeThing(toSpawn);
                thing.stackCount = count;  // Stack if possible (e.g., multiple medicine)

                if (!pawn.inventory.innerContainer.TryAdd(thing))
                {
                    GenDrop.TryDropSpawn(thing, pawn.Position, pawn.Map, ThingPlaceMode.Near, out _);
                }
                Logger.Debug($"Spawned {count} {toSpawn.defName} for {recipe.defName}");
            }

            // Spawn the medicine stack(s)
            if (medCount > 0)
            {
                // You could spawn one stack of medCount, but spawning individually matches your original loop
                for (int i = 0; i < medCount; i++)
                {
                    Thing med = ThingMaker.MakeThing(ThingDefOf.MedicineIndustrial);
                    if (!pawn.inventory.innerContainer.TryAdd(med))
                    {
                        // Optional: drop if inventory full (rare for pawn inventory)
                        GenDrop.TryDropSpawn(med, pawn.Position, pawn.Map, ThingPlaceMode.Near, out _);
                    }
                }
                Logger.Debug($"Spawned {medCount} MedicineIndustrial for {recipe.defName}");
            }

            // Special fixed ingredient (e.g. HemogenPack for BloodTransfusion)
            if (recipe.fixedIngredientFilter?.AllowedThingDefs?.Any() == true)
            {
                // Most fixedIngredientFilter in Biotech surgeries allow exactly one ThingDef
                ThingDef specialDef = recipe.fixedIngredientFilter.AllowedThingDefs.FirstOrDefault();
                if (specialDef != null)
                {
                    Thing special = ThingMaker.MakeThing(specialDef);
                    if (!pawn.inventory.innerContainer.TryAdd(special))
                    {
                        GenDrop.TryDropSpawn(special, pawn.Position, pawn.Map, ThingPlaceMode.Near, out _);
                    }
                    Logger.Debug($"Spawned special ingredient: {specialDef.defName} for {recipe.defName}");
                }
            }
        }

        private static bool IsSuitableForMiscSurgery(Verse.Pawn pawn, RecipeDef recipe, out string reason)
        {
            reason = null;

            // Age
            if (recipe.minAllowedAge > 0 && pawn.ageTracker?.AgeBiologicalYearsFloat < recipe.minAllowedAge)
            {
                reason = "RICS.SBCH.MinAgeRequired".Translate(recipe.minAllowedAge);
                return false;
            }

            // Gender prerequisite (nullable Gender)
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

            // Incompatible hediff tags
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

            // Already scheduled (null part for most misc surgeries)
            if (HasSurgeryScheduled(pawn, recipe, null))
            {
                reason = "RICS.SBCH.AlreadyScheduled".Translate();
                return false;
            }

            // Specific checks
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
                            g.def == GeneDefOf.Hemogenic
                        );

                    if (!needsBlood && !isHemogenic)
                    {
                        reason = "RICS.SBCH.NoBenefitTransfusion".Translate();
                        return false;
                    }
                    break;
            }

            // Optional: Reuse body surgery adult check
            if (!IsAdultForBodySurgery(pawn, out _))
            {
                reason = "RICS.SBCH.NotSuitableProcedure".Translate();
                return false;
            }

            return true;
        }
    }
}