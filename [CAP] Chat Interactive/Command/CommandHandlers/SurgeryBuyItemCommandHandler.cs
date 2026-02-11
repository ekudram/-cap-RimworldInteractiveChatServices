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
using CAP_ChatInteractive.Utilities;
using RimWorld;
using RimWorld.BaseGen;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
using Verse;
using static Verse.ParseHelper;

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
                    return "Usage: !surgery [implant] [left/right] [quantity] or [genderswap]";
                }

                var settings = CAPChatInteractiveMod.Instance.Settings.GlobalSettings;
                var currencySymbol = settings.CurrencyName?.Trim() ?? "¢";
                var viewer = Viewers.GetViewer(messageWrapper);

                // REPLACE the parsing code (about 40 lines) with:
                var parsed = CommandParserUtility.ParseCommandArguments(args, allowQuality: false, allowMaterial: false, allowSide: true, allowQuantity: true);
                if (parsed.HasError)
                    return parsed.Error;

                //string itemName = parsed.ItemName.ToLower(); // Normalize to lower for case-insensitive matching
                string sideStr = parsed.Side;
                string quantityStr = parsed.Quantity.ToString();

                // Special handling for custom surgeries (no ThingDef)

                bool isAllowed;
                string disabledMessage;

                string itemName = parsed.ItemName.ToLowerInvariant(); // case-insensitive

                // ── Determine surgery category and basic info ──
                string surgeryCategory = null;
                string recipeDefName = null;
                string displayName = null;
                string handlerType = null; // "gender", "body", "biotech"

                // ── Gender Swap (special case - own handler) ──
                if (new[] { "genderswap", "gender swap", "swapgender" }.Contains(itemName))
                {
                    handlerType = "gender";
                    displayName = "Gender Swap";
                }

                // ── Body Change Surgeries ──
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

                // ── Biotech / Misc Surgeries ──
                else if (BiotechSurgeryCommands.TryGetValue(itemName, out string recipeKey))
                {
                    handlerType = "biotech";
                    recipeDefName = recipeKey;
                    displayName = CultureInfo.InvariantCulture.TextInfo.ToTitleCase(itemName);
                }

                // ── Not recognized ──  DONT DO THIS IT STOPS IMPLANT HANDLING
                //if (handlerType == null)
                //{
                //    return "Unknown surgery type. Try: genderswap, fat body, feminine body, hulking body, masculine body, thin body, or a biotech surgery.";
                //}

                // ── Single place to check permission ──
                CheckSurgeryEnabled(settings, itemName, out isAllowed, out disabledMessage);

                Logger.Debug($"[Surgery Handler] Permission result for '{itemName}': Allowed = {isAllowed}, Message = '{disabledMessage ?? "none"}'");

                if (!isAllowed)
                {
                    Logger.Debug($"[Surgery Handler] BLOCKED: {disabledMessage} for user {messageWrapper.Username}");
                    return disabledMessage + " (use !surgerycosts to see available options)";
                }

                // ── Dispatch to correct handler ──
                switch (handlerType)
                {
                    case "gender":
                        return HandleGenderSwapSurgery(messageWrapper, viewer, currencySymbol);

                    case "body":
                        return HandleBodyChangeSurgery(messageWrapper, viewer, currencySymbol, surgeryCategory, recipeDefName, displayName);

                    case "biotech":
                        return HandleBiotechSurgery(messageWrapper, viewer, currencySymbol, recipeDefName, displayName);
                }

                // Get store item
                var storeItem = StoreCommandHelper.GetStoreItemByName(itemName);
                if (storeItem == null)
                {
                    return $"Implant '{itemName}' not found in Rimazon.";
                }

                if (!storeItem.Enabled)
                {
                    return $"Implant '{itemName}' is not available for purchase.";
                }

                // Check if this is actually an implant/surgery item
                var thingDef = DefDatabase<ThingDef>.GetNamedSilentFail(storeItem.DefName);
                if (thingDef == null && itemName != "gender swap") // Allow gender swap even without ThingDef
                {
                    Logger.Error($"ThingDef not found: {storeItem.DefName}");
                    return $"Error: Implant definition not found.";
                }



                // Check if this is a valid surgery item (bionic, implant, etc.)
                if (!IsValidSurgeryItem(thingDef))
                {
                    return $"{itemName} is not a valid implant or surgery item. Use !buy instead for regular items.";
                }

                // Check research requirements
                if (!StoreCommandHelper.HasRequiredResearch(storeItem))
                {
                    return $"{itemName} requires research that hasn't been completed yet.";
                }

                // Get viewer's pawn
                Verse.Pawn viewerPawn = PawnItemHelper.GetViewerPawn(messageWrapper);
                if (viewerPawn == null)
                {
                    return "You need to have a pawn in the colony to perform surgery. Use !buy pawn first.";
                }

                if (viewerPawn.Dead)
                {
                    return "Your pawn is dead. You cannot perform surgery.";
                }

                // Parse quantity
                if (!int.TryParse(quantityStr, out int quantity) || quantity < 1)
                {
                    quantity = 1;
                }

                // SPECIAL HANDLING FOR SURGERY ITEMS: Allow up to 2 for body parts
                int surgeryQuantityLimit = Math.Max(storeItem.QuantityLimit, 2);
                if (quantity > surgeryQuantityLimit)
                {
                    Logger.Debug($"Quantity {quantity} exceeds surgery limit of {surgeryQuantityLimit} for {itemName}, clamping");
                    quantity = surgeryQuantityLimit;
                }

                // Calculate final price (no quality/material for surgery items)
                int finalPrice = storeItem.BasePrice * quantity;

                // Check if user can afford
                if (!StoreCommandHelper.CanUserAfford(messageWrapper, finalPrice))
                {
                    return $"You need {StoreCommandHelper.FormatCurrencyMessage(finalPrice, currencySymbol)} for {quantity}x {itemName} surgery! You have {StoreCommandHelper.FormatCurrencyMessage(viewer.Coins, currencySymbol)}.";
                }

                // Find appropriate recipe for this implant
                var recipe = FindSurgeryRecipeForImplant(thingDef, viewerPawn);
                if (recipe == null)
                {
                    return $"No surgical procedure found for {itemName} on your pawn.";
                }

                // Find body parts for the surgery - let RimWorld decide which parts, we just filter by side
                var bodyParts = FindBodyPartsForSurgery(recipe, viewerPawn, sideStr, quantity);
                if (bodyParts.Count == 0)
                {
                    string availableParts = GetAvailableBodyPartsDescription(recipe, viewerPawn);
                    return $"No suitable body parts found for {itemName} surgery. Available: {availableParts}. Try specifying left/right.";
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
                    Logger.Debug($"Awarded {karmaEarned} karma for {finalPrice} coin surgery");
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
                    Logger.Debug($"Created LookTargets for locker position: {combinedResult.DeliveryPosition}");
                }
                else if (surgeryDropPos.IsValid)
                {
                    // Items were dropped somewhere - target that spot
                    surgeryLookTargets = new LookTargets(surgeryDropPos, viewerPawn.Map);
                    Logger.Debug($"Created LookTargets for surgery item drop position: {surgeryDropPos}");
                }
                else
                {
                    // Fallback to targeting the patient
                    surgeryLookTargets = new LookTargets(viewerPawn);
                    Logger.Debug($"Created LookTargets for patient: {viewerPawn.Name}");
                }

                // Create the invoice with actual delivery info
                string invoiceLabel = $"🏥 Rimazon Surgery - {messageWrapper.Username}";
                string invoiceMessage = CreateRimazonSurgeryInvoice(
                    messageWrapper.Username, itemName, quantity, finalPrice, currencySymbol,
                    bodyParts.Take(quantity).ToList(), combinedResult);
                MessageHandler.SendBlueLetter(invoiceLabel, invoiceMessage, surgeryLookTargets);

                Logger.Debug($"Surgery scheduled: {messageWrapper.Username} scheduled {quantity}x {itemName} for {finalPrice}{currencySymbol}");

                // Update the return success message:
                string deliveryMessage = combinedResult.PrimaryMethod switch
                {
                    DeliveryMethod.Locker => "delivered to your Rimazon Locker",
                    DeliveryMethod.DropPod => "delivered via drop pod",
                    _ => "delivered to colony"
                };

                return $"Scheduled {quantity}x {itemName} surgery for {StoreCommandHelper.FormatCurrencyMessage(finalPrice, currencySymbol)}! " +
                       $"Implant {deliveryMessage} please give them to the doctor. " +
                       $"Remaining: {StoreCommandHelper.FormatCurrencyMessage(viewer.Coins, currencySymbol)}.";
            }
            catch (Exception ex)
            {
                Logger.Error($"Error in HandleSurgery: {ex}");
                return $"Error scheduling surgery. Please try again.{ex}";
            }
        }

        // ──── SURGERY ENABLED CHECK METHOD ────
        private static void CheckSurgeryEnabled(CAPGlobalChatSettings settings, string itemNameLower, out bool isAllowed, out string disabledMessage)
        {
            isAllowed = true;
            disabledMessage = null;

            switch (itemNameLower)
            {
                // ── Gender Swap ──
                case "genderswap":
                case "gender swap":
                case "swapgender":
                    isAllowed = settings.SurgeryAllowGenderSwap;
                    disabledMessage = "Gender swap surgery is currently disabled.";
                    break;

                // ── Body Changes (all share one toggle) ──
                case "fatbody":
                case "fat body":
                case "fat":
                case "body fat":
                case "femininebody":
                case "feminine body":
                case "feminine":
                case "bodyfeminine":
                case "hulkingbody":
                case "hulking body":
                case "hulk":
                case "bodyhulking":
                case "masculinebody":
                case "masculine body":
                case "masculine":
                case "bodymasculine":
                case "thinbody":
                case "thin body":
                case "thin":
                case "bodythin":
                    isAllowed = settings.SurgeryAllowBodyChange;
                    disabledMessage = "Body modification surgeries are currently disabled.";
                    break;

                // ── Sterilization (vasectomy, tubal ligation, sterilize) ──
                case "sterilize":
                case "vasectomy":
                case "tubal":
                case "tuballigation":
                    isAllowed = settings.SurgeryAllowSterilize;
                    disabledMessage = "Sterilization (vasectomy/tubal ligation) is currently disabled.";
                    break;

                // ── IUD (implant + remove) ──
                case "iud":
                case "iudimplant":
                case "implant iud":
                case "iudremove":
                case "removeiud":
                case "remove iud":
                    isAllowed = settings.SurgeryAllowIUD;
                    disabledMessage = "IUD implantation/removal is currently disabled.";
                    break;

                // ── Vasectomy Reversal ──
                case "vasreverse":
                case "vas reverse":
                case "reversovasectomy":
                case "reverse vasectomy":
                case "reversevasectomy":
                    isAllowed = settings.SurgeryAllowVasReverse;
                    disabledMessage = "Vasectomy reversal is currently disabled.";
                    break;

                // ── Pregnancy Termination ──
                case "terminate":
                case "termination":
                case "pregnancy termination":
                case "pregnancytermination":
                case "abortion":
                    isAllowed = settings.SurgeryAllowTerminate;
                    disabledMessage = "Pregnancy termination is currently disabled.";
                    break;

                // ── Hemogen Extraction ──
                case "hemogen":
                case "extract hemogen":
                case "extracthemogen":
                    isAllowed = settings.SurgeryAllowHemogen;
                    disabledMessage = "Hemogen extraction is currently disabled.";
                    break;

                // ── Transfusion ──
                case "transfusion":
                case "blood transfusion":
                case "bloodtransfusion":
                case "blood":
                    isAllowed = settings.SurgeryAllowTransfusion;
                    disabledMessage = "Blood transfusion is currently disabled.";
                    break;

                // ── Fallback for truly misc/uncategorized biotech (add future ones here if needed) ──
                default:
                    isAllowed = settings.SurgeryAllowMiscBiotech;
                    disabledMessage = "Miscellaneous biotech surgeries are currently disabled.";
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
                return $"You need {StoreCommandHelper.FormatCurrencyMessage(finalPrice, currencySymbol)} for {displayName} surgery! " +
                       $"You have {StoreCommandHelper.FormatCurrencyMessage(viewer.Coins, currencySymbol)}.";
            }
            // Get viewer's pawn
            Verse.Pawn pawn = PawnItemHelper.GetViewerPawn(messageWrapper);
            if (pawn == null)
                return "You need to have a pawn in the colony to perform surgery. Use !buy pawn first.";
            if (pawn.Dead)
                return "Your pawn is dead. You cannot perform surgery.";

            // Body validation
            if (!IsSuitableForBodyChangingSurgery(pawn, out string restrictionReason))
            {
                return $"Sorry, this surgery cannot be performed: {restrictionReason}";
            }

            var recipe = DefDatabase<RecipeDef>.GetNamedSilentFail(recipeDefName);
            if (recipe == null)
            {
                Logger.Error($"{recipeDefName} RecipeDef not found.");
                return $"Error: {displayName} procedure not available (mod configuration issue).";
            }

            var corePart = pawn.RaceProps.body.corePart;
            if (corePart == null)
                return "Error: No suitable body part found for surgery.";

            if (HasSurgeryScheduled(pawn, recipe, corePart))
                return $"{displayName} surgery is already scheduled for your pawn. Please wait.";

            // Optional: Check if pawn already has this body type (to prevent redundant surgery)
            BodyTypeDef targetBodyType = GetTargetBodyTypeForSurgery(surgeryType); // Define this helper method
            if (targetBodyType != null && pawn.story.bodyType == targetBodyType)
            {
                return $"Your pawn already has a {displayName.ToLower()} body type. No change needed!";
            }

            // Optional: Gender compatibility check (e.g., block "masculine" on female pawns?)
            // if (surgeryType == "masculine" && pawn.gender == Gender.Female) return "This surgery is not compatible with female pawns.";

            viewer.TakeCoins(finalPrice);

            int karmaEarned = finalPrice / 200; // Conservative karma
            if (karmaEarned > 0)
            {
                viewer.GiveKarma(karmaEarned);
                Logger.Debug($"Awarded {karmaEarned} karma for {displayName} surgery");
            }

            ScheduleSurgeries(pawn, recipe, new List<BodyPartRecord> { corePart });

            LookTargets targets = new LookTargets(pawn);
            string invoiceLabel = $"🏥 Rimazon Surgery - {messageWrapper.Username}";
            string invoiceMessage = CreateRimazonSurgeryInvoice(
                messageWrapper.Username, displayName, quantity, finalPrice, currencySymbol,
                new List<BodyPartRecord> { corePart });

            MessageHandler.SendBlueLetter(invoiceLabel, invoiceMessage, targets);

            Logger.Debug($"{displayName} surgery scheduled for {messageWrapper.Username} - {finalPrice}{currencySymbol}");

            return $"{displayName} surgery scheduled for {StoreCommandHelper.FormatCurrencyMessage(finalPrice, currencySymbol)}! " +
                   $"Your doctors will transform your pawn. Remaining balance: {StoreCommandHelper.FormatCurrencyMessage(viewer.Coins, currencySymbol)}.";
        }

        // New method for handling gender swap surgery
        private static string HandleGenderSwapSurgery(ChatMessageWrapper messageWrapper, Viewer viewer, string currencySymbol)
        {
            const int quantity = 1;

            var globalSettings = CAPChatInteractiveMod.Instance.Settings.GlobalSettings;
            int finalPrice = globalSettings.SurgeryGenderSwapCost;

            if (!StoreCommandHelper.CanUserAfford(messageWrapper, finalPrice))
            {
                return $"You need {StoreCommandHelper.FormatCurrencyMessage(finalPrice, currencySymbol)} for gender swap surgery! " +
                       $"You have {StoreCommandHelper.FormatCurrencyMessage(viewer.Coins, currencySymbol)}.";
            }

            // Get viewer's pawn
            Verse.Pawn pawn = PawnItemHelper.GetViewerPawn(messageWrapper);
            if (pawn == null)
                return "You need to have a pawn in the colony to perform surgery. Use !buy pawn first.";
            if (pawn.Dead)
                return "Your pawn is dead. You cannot perform surgery.";

            // Age validation
            if (!IsAdultForBodySurgery(pawn, out string restrictionReason))
            {
                return $"Sorry, gender swap surgery cannot be performed: {restrictionReason}";
            }

            var recipe = DefDatabase<RecipeDef>.GetNamedSilentFail("GenderSwapSurgery");
            if (recipe == null)
            {
                Logger.Error("GenderSwapSurgery RecipeDef not found.");
                return "Error: Gender swap procedure not available (mod configuration issue).";
            }

            var corePart = pawn.RaceProps.body.corePart;
            if (corePart == null)
                return "Error: No suitable body part found for surgery.";

            if (HasSurgeryScheduled(pawn, recipe, corePart))
                return "Gender swap surgery is already scheduled for your pawn. Please wait.";

            // Optional: prevent redundant swaps (comment out if you want to allow funny double-swaps)
            if (pawn.gender == Gender.None) return "Your pawn has no gender to swap... mysterious.";

            viewer.TakeCoins(finalPrice);

            // Optional: smaller or no karma
            int karmaEarned = finalPrice / 200; // ← more conservative, or set to 0 / small fixed value
            if (karmaEarned > 0)
            {
                viewer.GiveKarma(karmaEarned);
                Logger.Debug($"Awarded {karmaEarned} karma for gender swap purchase");
            }

            ScheduleSurgeries(pawn, recipe, new List<BodyPartRecord> { corePart });

            LookTargets targets = new LookTargets(pawn);
            string invoiceLabel = $"🏥 Rimazon Surgery - {messageWrapper.Username}";
            string invoiceMessage = CreateRimazonSurgeryInvoice(
                messageWrapper.Username, "Gender Swap", quantity, finalPrice, currencySymbol,
                new List<BodyPartRecord> { corePart });

            MessageHandler.SendBlueLetter(invoiceLabel, invoiceMessage, targets);

            Logger.Debug($"Gender swap scheduled for {messageWrapper.Username} - {finalPrice}{currencySymbol}");

            return $"Gender swap surgery scheduled for {StoreCommandHelper.FormatCurrencyMessage(finalPrice, currencySymbol)}! " +
                   $"Your doctors will take care of it. Remaining balance: {StoreCommandHelper.FormatCurrencyMessage(viewer.Coins, currencySymbol)}.";
        }

        private static string HandleBiotechSurgery(ChatMessageWrapper messageWrapper, Viewer viewer, string currencySymbol, string recipeKey, string displayName)
        {
            const int quantity = 1; // Fixed to 1 for misc surgeries

            Verse.Pawn pawn = PawnItemHelper.GetViewerPawn(messageWrapper);
            if (pawn == null) return "You need a pawn. Use !buy pawn first.";
            if (pawn.Dead) return "Pawn is dead.";

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
                return $"{displayName} requires research that hasn't been completed yet.";
            }

            // Validation
            if (!IsSuitableForMiscSurgery(pawn, recipe, out string restrictionReason))
            {
                return $"Cannot perform {displayName}: {restrictionReason}";
            }

            // Affordability
            if (!StoreCommandHelper.CanUserAfford(messageWrapper, finalPrice))
            {
                return $"Need {StoreCommandHelper.FormatCurrencyMessage(finalPrice, currencySymbol)} for {displayName}! " +
                       $"You have {StoreCommandHelper.FormatCurrencyMessage(viewer.Coins, currencySymbol)}.";
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
            string invoiceLabel = $"🏥 Rimazon Surgery - {messageWrapper.Username}";
            string invoiceMessage = CreateRimazonSurgeryInvoice(
                messageWrapper.Username, displayName, quantity, finalPrice, currencySymbol, bodyParts);
            MessageHandler.SendBlueLetter(invoiceLabel, invoiceMessage, targets);

            Logger.Debug($"{displayName} ({recipeKey}) scheduled for {messageWrapper.Username} - {finalPrice}{currencySymbol}");

            return $"{displayName} surgery scheduled for {StoreCommandHelper.FormatCurrencyMessage(finalPrice, currencySymbol)}! " +
                   $"Your doctors will take care of it. Remaining balance: {StoreCommandHelper.FormatCurrencyMessage(viewer.Coins, currencySymbol)}.";
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
        private static string CreateRimazonSurgeryInvoice(string username,
            string itemName, int quantity, int price,
            string currencySymbol, List<BodyPartRecord> bodyParts, DeliveryResult deliveryResult = null)
        {
            string invoice = $"RIMAZON SURGERY SERVICE\n";
            invoice += $"====================\n";
            invoice += $"Customer: {username}\n";
            invoice += $"Procedure: {itemName} x{quantity}\n";

            if (bodyParts.Count > 0)
            {
                invoice += $"Body Parts: {string.Join(", ", bodyParts.Select(bp => bp.Label))}\n";
            }

            // Add delivery information based on actual delivery method
            if (deliveryResult != null)
            {
                int lockerCount = deliveryResult.LockerDeliveredItems.Sum(t => t.stackCount);
                int dropPodCount = deliveryResult.DropPodDeliveredItems.Sum(t => t.stackCount);

                if (deliveryResult.PrimaryMethod == DeliveryMethod.Locker)
                {
                    invoice += $"Delivery: Rimazon Locker\n";
                    invoice += $"Location: Locker Delivery\n";
                }
                else if (deliveryResult.PrimaryMethod == DeliveryMethod.DropPod)
                {
                    invoice += $"Delivery: Standard Drop Pod\n";
                    invoice += $"Location: Drop Pod Delivery\n";
                }
                else if (lockerCount > 0 && dropPodCount > 0)
                {
                    invoice += $"Delivery: Mixed Delivery\n";
                    invoice += $"  • Locker: x{lockerCount}\n";
                    invoice += $"  • Drop Pod: x{dropPodCount}\n";
                }
                else
                {
                    invoice += $"Delivery: Colony Delivery\n";
                }
            }
            else
            {
                // Fallback for old behavior
                invoice += $"Service: Surgical Implantation\n";
            }

            invoice += $"====================\n";
            invoice += $"Total: {price:N0}{currencySymbol}\n";
            invoice += $"====================\n";
            invoice += $"Thank you for using Rimazon Surgery!\n";

            // Update the delivery message based on actual method
            if (deliveryResult != null)
            {
                if (deliveryResult.PrimaryMethod == DeliveryMethod.Locker)
                {
                    invoice += $"Implants delivered to your Rimazon Locker.\n";
                }
                else if (deliveryResult.PrimaryMethod == DeliveryMethod.DropPod)
                {
                    invoice += $"Implants delivered via drop pod.\n";
                }
                else
                {
                    invoice += $"Implants delivered to colony.\n";
                }
            }
            else
            {
                // Old fallback message
                invoice += $"Implants delivered to pawn's inventory.\n";
            }

            invoice += $"Surgery scheduled with colony doctors.";

            return invoice;
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
            if (pawn == null)
            {
                reason = "Error: Null Pawn.";
                return false;
            }

            // 1. Biological age check (most reliable)
            if (pawn.ageTracker != null)
            {
                float biologicalAge = pawn.ageTracker.AgeBiologicalYearsFloat;

                // RimWorld vanilla adulthood threshold is usually 18
                // You can make this configurable later if desired
               const float MIN_ADULT_AGE = 14f;

                if (biologicalAge < MIN_ADULT_AGE)
                {
                    reason = $"Your pawn is too young (biological age: {biologicalAge:F1}). Minimum age for body-altering surgery is {MIN_ADULT_AGE}.";
                    Logger.Debug($"Pawn {pawn.Name} is too young (bio age: {biologicalAge:F1}) for body-changing surgery");
                    return false;
                }
            }

            // 2. Fallback: Check body type (useful when age tracker is missing or for modded races)
            if (pawn.story != null && pawn.story.bodyType != null)
            {
                if (pawn.story.bodyType == BodyTypeDefOf.Child)
                {
                    reason = "Your pawn has a Child body type, indicating they are not an adult.";
                    Logger.Debug($"Pawn {pawn.Name} has Child body type - blocking body surgery");
                    return false;
                }
            }

            // Pregnancy check
            if (pawn.health?.hediffSet != null)
            {
                var pregnancyHediff = pawn.health.hediffSet.hediffs
                    .FirstOrDefault(h => h.def.defName.ToLower().Contains("pregnancy") || h is Hediff_Pregnant);

                if (pregnancyHediff != null)
                {
                    reason = "Your pawn is currently pregnant. Major body-altering surgeries are not safe during pregnancy.";
                    return false;
                }

                // Lactating block (optional - you already have it commented; keep if wanted)
                // if (pawn.health.hediffSet.HasHediff(HediffDefOf.Lactating)) { ... }
            }

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
                reason = $"Minimum age {recipe.minAllowedAge} required.";
                return false;
            }

            // Gender prerequisite (nullable Gender)
            if (recipe.genderPrerequisite == Gender.Female && pawn.gender != Gender.Female)
            {
                reason = "Requires female pawn.";
                return false;
            }
            if (recipe.genderPrerequisite == Gender.Male && pawn.gender != Gender.Male)
            {
                reason = "Requires male pawn.";
                return false;
            }

            // Incompatible hediff tags  tags
            if (recipe.incompatibleWithHediffTags != null)
            {
                foreach (string forbiddenTag in recipe.incompatibleWithHediffTags)
                {
                    if (pawn.health.hediffSet.hediffs.Any(h =>
                        h.def.tags != null && h.def.tags.Contains(forbiddenTag)))
                    {
                        reason = $"Incompatible: {forbiddenTag} condition present (e.g. already sterilized or ovum extracted).";
                        return false;
                    }
                }
            }

            // Already scheduled (null part for most misc surgeries)
            if (HasSurgeryScheduled(pawn, recipe, null))
            {
                reason = "Already scheduled for this pawn.";
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
                        reason = "Pawn is not pregnant.";
                        return false;
                    }
                    break;

                case "ReverseVasectomy":
                    if (!pawn.health.hediffSet.HasHediff(DefDatabase<HediffDef>.GetNamedSilentFail("Vasectomy")))
                    {
                        reason = "Pawn lacks vasectomy.";
                        return false;
                    }
                    break;

                case "RemoveIUD":
                    if (!pawn.health.hediffSet.HasHediff(DefDatabase<HediffDef>.GetNamedSilentFail("ImplantedIUD")))
                    {
                        reason = "No IUD found.";
                        return false;
                    }
                    break;
                case "BloodTransfusion":
                    bool needsBlood = pawn.health.hediffSet.HasHediff(HediffDefOf.BloodLoss);

                    bool isHemogenic = pawn.genes != null &&
                        pawn.genes.GenesListForReading.Any(g =>
                            //g.def.hemogenOffset != 0 ||   // any gene affecting hemogen
                            g.def == GeneDefOf.Bloodfeeder ||
                            g.def == GeneDefOf.Hemogenic   // if your mod adds custom ones
                        );

                    if (!needsBlood && !isHemogenic)
                    {
                        reason = "Pawn has no blood loss and is not hemogenic—no benefit from transfusion.";
                        return false;
                    }
                    break;
            }

            // Optional: Reuse body surgery adult check
            if (!IsAdultForBodySurgery(pawn, out _))
            {
                reason = "Pawn is not suitable for this procedure (age/body type).";
                return false;
            }

            return true;
        }
    }
}