// BuyItemCommandHandler.cs
// Copyright (c) Captolamia. All rights reserved.
// Licensed under the AGPLv3 License. See LICENSE file in the project root for full license information.
//
// Command handler for buying items from Rimazon store
using CAP_ChatInteractive.Commands.ViewerCommands;
using CAP_ChatInteractive.Store;
using CAP_ChatInteractive.Utilities;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;
using Verse.Sound;
using Pawn = CAP_ChatInteractive.Commands.ViewerCommands.Pawn;

namespace CAP_ChatInteractive.Commands.CommandHandlers
{
    public static class BuyItemCommandHandler
    {
        // ===== MAIN COMMAND HANDLERS =====
        public static string HandleBuyItem(ChatMessageWrapper user, string[] args, bool requireEquippable = false, bool requireWearable = false, bool addToInventory = false)
        {
            try
            {
                Logger.Debug($"HandleBuyItem called for user: {user.Username}, args: {string.Join(", ", args)}, requireEquippable: {requireEquippable}, requireWearable: {requireWearable}, addToInventory: {addToInventory}");


                // REPLACE all the parsing code (about 80 lines) with just:
                var parsed = CommandParserUtility.ParseCommandArguments(args, allowQuality: true, allowMaterial: true, allowSide: false, allowQuantity: true);
                if (parsed.HasError)
                    return parsed.Error;

                string itemName = parsed.ItemName;
                string qualityStr = parsed.Quality;
                string materialStr = parsed.Material;
                string quantityStr = parsed.Quantity.ToString();

                var settings = CAPChatInteractiveMod.Instance.Settings.GlobalSettings;
                var currencySymbol = settings.CurrencyName?.Trim() ?? "¢";
                var viewer = Viewers.GetViewer(user);

                // Get store item
                var storeItem = StoreCommandHelper.GetStoreItemByName(itemName);
                Logger.Debug($"Store item lookup for '{itemName}': {(storeItem != null ? $"Found: {storeItem.DefName}, Enabled: {storeItem.Enabled}" : "Not Found")}");

                if (storeItem == null)
                {
                    Logger.Debug($"Item not found: {itemName}");
                    return $"Item '{itemName}' not found in Rimazon.";
                }

                if (!storeItem.Enabled)
                {
                    Logger.Debug($"Item disabled: {itemName}");
                    return $"Item '{itemName}' is not available for purchase.";
                }

                // Check item type requirements
                if (!StoreCommandHelper.IsItemTypeValid(storeItem, requireEquippable, requireWearable, false))
                {
                    string itemType = StoreCommandHelper.GetItemTypeDescription(storeItem);
                    string expectedType = requireEquippable ? "equippable" : requireWearable ? "wearable" : "purchasable";
                    return $"{itemName} is a {itemType}, not an {expectedType} item. Use !buy instead.";
                }

                // Check research requirements
                if (!StoreCommandHelper.HasRequiredResearch(storeItem))
                {
                    return $"{itemName} requires research that hasn't been completed yet.";
                }

                // Parse quality
                var quality = StoreCommandHelper.ParseQuality(qualityStr);
                if (!StoreCommandHelper.IsQualityAllowed(quality))
                {
                    return $"Quality '{qualityStr}' is not allowed for purchases.";
                }

                // Get thing def
                var thingDef = DefDatabase<ThingDef>.GetNamedSilentFail(storeItem.DefName);
                if (thingDef == null)
                {
                    Logger.Error($"ThingDef not found: {storeItem.DefName}");
                    return $"Error: Item definition not found.";
                }

                // Check for banned races  -- Needed?
                if (StoreCommandHelper.IsRaceBanned(thingDef))
                {
                    return $"Item '{itemName}' is a banned race and cannot be purchased.";
                }

                // Parse material
                ThingDef material = null;
                if (thingDef.MadeFromStuff)
                {
                    material = StoreCommandHelper.ParseMaterial(materialStr, thingDef);
                    if (materialStr != "random" && material == null)
                    {
                        return $"Material '{materialStr}' is not valid for {itemName}.";
                    }
                }

                // Parse quantity
                if (!int.TryParse(quantityStr, out int quantity) || quantity < 1)
                {
                    quantity = 1;
                }

                Logger.Debug($"Parsed quantity: {quantity}");

                // Check quantity limits and clamp to maximum allowed
                if (storeItem.HasQuantityLimit && quantity > storeItem.QuantityLimit)
                {
                    Logger.Debug($"Quantity {quantity} exceeds limit of {storeItem.QuantityLimit} for {itemName}, clamping to maximum");
                    quantity = storeItem.QuantityLimit;
                }

                Logger.Debug($"Final quantity after limits: {quantity}");

                // Check quantity limits and clamp to maximum allowed
                if (storeItem.HasQuantityLimit && quantity > storeItem.QuantityLimit)
                {
                    Logger.Debug($"Quantity {quantity} exceeds limit of {storeItem.QuantityLimit} for {itemName}, clamping to maximum");
                    quantity = storeItem.QuantityLimit;
                }

                // Calculate final price
                int finalPrice = StoreCommandHelper.CalculateFinalPrice(storeItem, quantity, quality, material);

                // Check if user can afford
                if (!StoreCommandHelper.CanUserAfford(user, finalPrice))
                {
                    return $"You need {StoreCommandHelper.FormatCurrencyMessage(finalPrice, currencySymbol)} to purchase {quantity}x {itemName}! You have {StoreCommandHelper.FormatCurrencyMessage(viewer.Coins, currencySymbol)}.";
                }

                // Get viewer's pawn for equip/wear/backpack commands
                Verse.Pawn viewerPawn = null;

                if (requireEquippable || requireWearable || addToInventory)
                {
                    viewerPawn = StoreCommandHelper.GetViewerPawn(user);
                    if (viewerPawn == null)
                    {
                        return "You need to have a pawn in the colony. Use !buy pawn first.";
                    }

                    if (viewerPawn.Dead)
                    {
                        return "Your pawn is dead. You cannot equip/wear items.";
                    }
                }
                else
                {
                    // For regular buy commands, try to get the pawn but don't require it
                    viewerPawn = StoreCommandHelper.GetViewerPawn(user);
                    // Log if no pawn found for debugging
                    if (viewerPawn == null)
                    {
                        Logger.Debug($"No pawn assigned to {user.Username}, using colony-wide delivery");
                    }
                    else
                    {
                        Logger.Debug($"Using pawn {viewerPawn.Name} for delivery positioning");
                    }
                    // If no pawn, items will be delivered to a random colony location
                }

                // Deduct coins and process purchase
                viewer.TakeCoins(finalPrice);

                int karmaEarned = finalPrice / 100;
                if (karmaEarned > 0)
                {
                    viewer.GiveKarma(karmaEarned);
                    Logger.Debug($"Awarded {karmaEarned} karma for {finalPrice} coin purchase");
                }

                (List<Thing> spawnedItems, IntVec3 deliveryPos) spawnResult;

                // Spawn the item
                if (requireEquippable || requireWearable)
                {
                    spawnResult = StoreCommandHelper.SpawnItemForPawn(thingDef, quantity, quality, material, viewerPawn, false, requireEquippable, requireWearable);
                }
                else
                {
                    spawnResult = StoreCommandHelper.SpawnItemForPawn(thingDef, quantity, quality, material, viewerPawn, addToInventory);
                }
                List<Thing> spawnedItems = spawnResult.spawnedItems;
                IntVec3 deliveryPos = spawnResult.deliveryPos;

                // Create look targets - use the delivery position we know items will be at
                LookTargets lookTargets = null;

                if (thingDef.thingClass == typeof(Verse.Pawn))
                {
                    // For animal deliveries, target the EXACT SPAWN POSITION
                    if (deliveryPos.IsValid)
                    {
                        Map targetMap = viewerPawn?.Map ?? Find.CurrentMap ?? Find.Maps.FirstOrDefault(m => m.IsPlayerHome);
                        if (targetMap != null)
                        {
                            lookTargets = new LookTargets(deliveryPos, targetMap);
                            Logger.Debug($"Created LookTargets for exact animal spawn position: {deliveryPos}");
                        }
                    }
                }
                else if (requireEquippable || requireWearable || addToInventory)
                {
                    // For direct pawn interactions, target the pawn
                    lookTargets = viewerPawn != null ? new LookTargets(viewerPawn) : null;
                    Logger.Debug($"Created LookTargets for pawn: {viewerPawn?.Name}");
                }
                else if (deliveryPos.IsValid)
                {
                    // For drop pod deliveries, target the delivery position
                    Map targetMap = viewerPawn?.Map ?? Find.CurrentMap ?? Find.Maps.FirstOrDefault(m => m.IsPlayerHome);
                    if (targetMap != null)
                    {
                        lookTargets = new LookTargets(deliveryPos, targetMap);
                        Logger.Debug($"Created LookTargets for delivery position: {deliveryPos} on map {targetMap}");
                    }
                }

                Logger.Debug($"Final LookTargets: {lookTargets?.ToString() ?? "null"}");

                // Log success
                Logger.Debug($"Purchase successful: {user.Username} bought {quantity}x {itemName} for {finalPrice}{currencySymbol}");

                // Send appropriate letter notification
                string itemLabel = thingDef?.LabelCap ?? itemName;
                string invoiceLabel = "";
                string invoiceMessage = "";
                string tClass = thingDef.thingClass.ToString();

                // SPECIAL CASE: Animal deliveries - CHECK THIS FIRST
                Logger.Debug($"Checking for special invoice case for item: {thingDef.thingClass} tClass: {tClass}");
                if (thingDef.thingClass == typeof(Verse.Pawn) || tClass == "Verse.Pawn")
                {
                    string emoji = "🐾";
                    invoiceLabel = $"{emoji} Rimazon Pet Delivery - {user.Username}";
                    invoiceMessage = CreateRimazonPetInvoice(user.Username, itemLabel, quantity, finalPrice, currencySymbol);

                }
                // Then check for backpack/equip/wear
                else if (addToInventory || requireEquippable || requireWearable)
                {
                    // Backpack, Equip, and Wear all involve direct delivery to pawn
                    string serviceType = requireEquippable ? "Equip" : requireWearable ? "Wear" : "Backpack";
                    string emoji = requireEquippable ? "⚔️" : requireWearable ? "👕" : "🎒";

                    invoiceLabel = $"{emoji} Rimazon {serviceType} - {user.Username}";
                    invoiceMessage = CreateRimazonDirectInvoice(user.Username, itemLabel, quantity, finalPrice, currencySymbol, serviceType);
                }
                else
                {
                    // Regular drop pod delivery
                    invoiceLabel = $"🟡 Rimazon Delivery - {user.Username}";
                    invoiceMessage = CreateRimazonInvoice(user.Username, itemLabel, quantity, finalPrice, currencySymbol, quality, material);
                }

                // Send the letter
                if (IsMajorPurchase(finalPrice, quality))
                {
                    MessageHandler.SendGoldLetter(invoiceLabel, invoiceMessage, lookTargets);
                }
                else
                {
                    MessageHandler.SendGreenLetter(invoiceLabel, invoiceMessage, lookTargets);
                }


                // Return success message
                string action = addToInventory ? "added to your pawn's inventory" :
                              requireEquippable ? "equipped to your pawn" :
                              requireWearable ? "worn by your pawn" : "delivered via Rimazon";

                return $"Purchased {quantity}x {itemName} for {StoreCommandHelper.FormatCurrencyMessage(finalPrice, currencySymbol)} and {action}! Remaining: {StoreCommandHelper.FormatCurrencyMessage(viewer.Coins, currencySymbol)}.";

            }
            catch (Exception ex)
            {
                Logger.Error($"Error in HandleBuyItem: {ex}");
                return "Error processing purchase. Please try again.";
            }
        }

        public static string HandleUseItem(ChatMessageWrapper user, string[] args)
        {
            try
            {
                Logger.Debug($"HandleUseItem called for user: {user.Username}, args: {string.Join(", ", args)}");

                if (args.Length == 0)
                {
                    return "Usage: !use <item> [quantity]";
                }

                var settings = CAPChatInteractiveMod.Instance.Settings.GlobalSettings;
                var currencySymbol = settings.CurrencyName?.Trim() ?? "¢";
                var viewer = Viewers.GetViewer(user.Username);

                // REPLACE the parsing code (about 30 lines) with:
                var parsed = CommandParserUtility.ParseCommandArguments(args, allowQuality: false, allowMaterial: false, allowSide: false, allowQuantity: true);
                if (parsed.HasError)
                    return parsed.Error;

                string itemName = parsed.ItemName;
                string quantityStr = parsed.Quantity.ToString();

                // Get store item
                var storeItem = StoreCommandHelper.GetStoreItemByName(itemName);
                if (storeItem == null)
                {
                    return $"Item '{itemName}' not found in Rimazon.";
                }

                if (!storeItem.Enabled)
                {
                    return $"Item '{itemName}' is not available for purchase.";
                }

                if (!storeItem.IsUsable)
                {
                    return $"{itemName} is not a usable item.";
                }

                // Check research requirements
                if (!StoreCommandHelper.HasRequiredResearch(storeItem))
                {
                    return $"{itemName} requires research that hasn't been completed yet.";
                }

                // Parse quantity
                if (!int.TryParse(quantityStr, out int quantity) || quantity < 1)
                {
                    quantity = 1;
                }

                // Check quantity limits and clamp to maximum allowed
                if (storeItem.HasQuantityLimit && quantity > storeItem.QuantityLimit)
                {
                    Logger.Debug($"Quantity {quantity} exceeds limit of {storeItem.QuantityLimit} for {itemName}, clamping to maximum");
                    quantity = storeItem.QuantityLimit;
                }

                // Get viewer's pawn
                var viewerPawn = StoreCommandHelper.GetViewerPawn(user);
                Verse.Pawn rimworldPawn = viewerPawn; // This is already a Verse.Pawn
                Logger.Debug($"Viewer pawn for {user.Username}: {(viewerPawn != null ? viewerPawn.Name.ToString() : "null")}");
                Logger.Debug($"Viewer pawn dead status: {(viewerPawn != null ? viewerPawn.Dead.ToString() : "N/A")}");
                Logger.Debug($"Rimworld pawn for {user.Username}: {(rimworldPawn != null ? rimworldPawn.Name.ToString() : "null")}");
                Logger.Debug($"Rimworld pawn dead status: {(rimworldPawn != null ? rimworldPawn.Dead.ToString() : "N/A")}");

                if (viewerPawn == null)
                {
                    return "You need to have a pawn in the colony to use items. Use !buy pawn first.";
                }

                // SPECIAL RESURRECTION LOGIC: Allow Resurrector Mech Serum on dead pawns
                bool isResurrectorSerum = storeItem.DefName == "MechSerumResurrector";

                if (viewerPawn.Dead && !isResurrectorSerum)
                {
                    return "Your pawn is dead. You cannot use items.";
                }

                // For resurrector serum on dead pawns, force quantity to 1
                if (isResurrectorSerum && viewerPawn.Dead)
                {
                    quantity = 1;
                    Logger.Debug($"Using Resurrector Mech Serum on dead pawn, quantity forced to 1");
                }

                // Calculate final price (no quality/material multipliers for usable items)
                int finalPrice = storeItem.BasePrice * quantity;

                // Check if user can afford
                if (!StoreCommandHelper.CanUserAfford(user, finalPrice))
                {
                    return $"You need {StoreCommandHelper.FormatCurrencyMessage(finalPrice, currencySymbol)} to use {quantity}x {itemName}! You have {StoreCommandHelper.FormatCurrencyMessage(viewer.Coins, currencySymbol)}.";
                }

                // Get thing def
                var thingDef = DefDatabase<ThingDef>.GetNamedSilentFail(storeItem.DefName);
                if (thingDef == null)
                {
                    Logger.Error($"ThingDef not found: {storeItem.DefName}");
                    return $"Error: Item definition not found.";
                }

                // Deduct coins
                viewer.TakeCoins(finalPrice);

                int karmaEarned = finalPrice / 100;
                if (karmaEarned > 0)
                {
                    viewer.GiveKarma(karmaEarned);
                    Logger.Debug($"Awarded {karmaEarned} karma for {finalPrice} coin purchase");
                }

                // SPECIAL RESURRECTION: Handle resurrector serum differently
                if (isResurrectorSerum && viewerPawn.Dead)
                {
                    ResurrectPawn(viewerPawn);
                }
                else
                {
                    // Normal item usage
                    UseItemImmediately(thingDef, quantity, rimworldPawn);
                }

                // Send appropriate letter notification
                string itemLabel = thingDef?.LabelCap ?? itemName;
                string invoiceLabel;
                string invoiceMessage;

                if (isResurrectorSerum && viewerPawn.Dead)
                {
                    // Pink letter for resurrection
                    invoiceLabel = $"💖 Rimazon Resurrection - {user.Username}";
                    invoiceMessage = CreateRimazonResurrectionInvoice(user.Username, itemLabel, finalPrice, currencySymbol);
                    LookTargets resurrectionLookTargets = new LookTargets(viewerPawn);
                    Logger.Debug($"Sending resurrection letter to {user.Username} for pawn {viewerPawn.Name}");
                    Logger.Debug($"Resurrection look targets: {resurrectionLookTargets}");
                    MessageHandler.SendPinkLetter(invoiceLabel, invoiceMessage, resurrectionLookTargets);
                }
                else if (IsMajorPurchase(finalPrice, null)) // Don't check quality for use commands
                {
                    invoiceLabel = $"🔵 Rimazon Instant - {user.Username}";
                    invoiceMessage = CreateRimazonInstantInvoice(user.Username, itemLabel, quantity, finalPrice, currencySymbol);
                    LookTargets useLookTargets = new LookTargets(rimworldPawn);
                    Logger.Debug($"Sending major use letter to {user.Username} for pawn {rimworldPawn.Name}");
                    Logger.Debug($"Use look targets: {useLookTargets}");
                    MessageHandler.SendBlueLetter(invoiceLabel, invoiceMessage, useLookTargets);
                }
                else
                {
                    invoiceLabel = $"🔵 Rimazon Instant - {user.Username}";
                    invoiceMessage = CreateRimazonInstantInvoice(user.Username, itemLabel, quantity, finalPrice, currencySymbol);
                    LookTargets useLookTargets = new LookTargets(rimworldPawn);
                    Logger.Debug($"Sending regular use letter to {user.Username} for pawn {rimworldPawn.Name}");
                    Logger.Debug($"Use look targets: {useLookTargets}");
                    MessageHandler.SendBlueLetter(invoiceLabel, invoiceMessage, useLookTargets);
                }

                Logger.Debug($"Use item successful: {user.Username} used {quantity}x {itemName} for {finalPrice}{currencySymbol}");

                // Return appropriate success message
                if (isResurrectorSerum && viewerPawn.Dead)
                {
                    return $"💖 RESURRECTION! Used {itemName} to bring your pawn back to life for {StoreCommandHelper.FormatCurrencyMessage(finalPrice, currencySymbol)}! Remaining: {StoreCommandHelper.FormatCurrencyMessage(viewer.Coins, currencySymbol)}.";
                }
                else
                {
                    return $"Used {quantity}x {itemName} for {StoreCommandHelper.FormatCurrencyMessage(finalPrice, currencySymbol)}! Remaining: {StoreCommandHelper.FormatCurrencyMessage(viewer.Coins, currencySymbol)}.";
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error in HandleUseItem: {ex}");
                return "Error using item. Please try again.";
            }
        }

        public static string HandleSurgery(ChatMessageWrapper user, string[] args)
        {
            try
            {
                Logger.Debug($"HandleSurgery called for user: {user.Username}, args: {string.Join(", ", args)}");

                if (args.Length == 0)
                {
                    return "Usage: !surgery <implant> [left/right] [quantity] - Example: !surgery bionic arm left 1";
                }

                var settings = CAPChatInteractiveMod.Instance.Settings.GlobalSettings;
                var currencySymbol = settings.CurrencyName?.Trim() ?? "¢";
                var viewer = Viewers.GetViewer(user);

                // REPLACE the parsing code (about 40 lines) with:
                var parsed = CommandParserUtility.ParseCommandArguments(args, allowQuality: false, allowMaterial: false, allowSide: true, allowQuantity: true);
                if (parsed.HasError)
                    return parsed.Error;

                string itemName = parsed.ItemName;
                string sideStr = parsed.Side;
                string quantityStr = parsed.Quantity.ToString();

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
                if (thingDef == null)
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
                Verse.Pawn viewerPawn = StoreCommandHelper.GetViewerPawn(user);
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

                // Check quantity limits
                if (storeItem.HasQuantityLimit && quantity > storeItem.QuantityLimit)
                {
                    Logger.Debug($"Quantity {quantity} exceeds limit of {storeItem.QuantityLimit} for {itemName}, clamping to maximum");
                    quantity = storeItem.QuantityLimit;
                }

                // Calculate final price (no quality/material for surgery items)
                int finalPrice = storeItem.BasePrice * quantity;

                // Check if user can afford
                if (!StoreCommandHelper.CanUserAfford(user, finalPrice))
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

                // Spawn the implant items in pawn's inventory
                StoreCommandHelper.SpawnItemForPawn(thingDef, quantity, null, null, viewerPawn, true); // Add to inventory

                // Schedule the surgeries
                ScheduleSurgeries(viewerPawn, recipe, bodyParts.Take(quantity).ToList());

                // In HandleSurgery method, after scheduling surgeries:
                LookTargets surgeryLookTargets = new LookTargets(viewerPawn);
                string invoiceLabel = $"🏥 Rimazon Surgery - {user.Username}";
                string invoiceMessage = CreateRimazonSurgeryInvoice(user.Username, itemName, quantity, finalPrice, currencySymbol, bodyParts.Take(quantity).ToList());
                MessageHandler.SendBlueLetter(invoiceLabel, invoiceMessage, surgeryLookTargets);

                Logger.Debug($"Surgery scheduled: {user.Username} scheduled {quantity}x {itemName} for {finalPrice}{currencySymbol}");

                return $"Scheduled {quantity}x {itemName} surgery for {StoreCommandHelper.FormatCurrencyMessage(finalPrice, currencySymbol)}! Implant delivered to pawn's inventory please give them to the doctor. Remaining: {StoreCommandHelper.FormatCurrencyMessage(viewer.Coins, currencySymbol)}.";

            }
            catch (Exception ex)
            {
                Logger.Error($"Error in HandleSurgery: {ex}");
                return "Error scheduling surgery. Please try again.";
            }
        }

        // ===== ITEM USAGE & SURGERY METHODS =====

        private static void UseItemImmediately(ThingDef thingDef, int quantity, Verse.Pawn pawn)
        {
            for (int i = 0; i < quantity; i++)
            {
                Thing thing = ThingMaker.MakeThing(thingDef);

                // Handle different types of usable items with appropriate sounds
                if (thingDef.IsIngestible && thingDef.ingestible != null)
                {
                    // DEBUG: Log nutrition before ingestion
                    float nutritionBefore = pawn.needs.food?.CurLevel ?? 0f;
                    Logger.Debug($"Nutrition before ingestion: {nutritionBefore}");

                    // SPAWN THE ITEM FIRST so ingestion works properly
                    GenSpawn.Spawn(thing, pawn.Position, pawn.Map);

                    // Now ingest the spawned item and APPLY the nutrition
                    float nutritionWanted = pawn.needs.food?.NutritionWanted ?? 0f;
                    Logger.Debug($"Nutrition wanted: {nutritionWanted}");

                    // Ingest returns the nutrition gained - we need to apply it to the pawn
                    float nutritionGained = thing.Ingested(pawn, nutritionWanted);
                    Logger.Debug($"Nutrition gained from ingestion: {nutritionGained}");

                    // Apply the nutrition to the pawn's food need
                    if (pawn.needs.food != null)
                    {
                        pawn.needs.food.CurLevel += nutritionGained;
                        Logger.Debug($"Nutrition after manual application: {pawn.needs.food.CurLevel}");
                    }

                    // Play appropriate sound - use safe sound playing method
                    PlayIngestSoundSafely(thingDef, pawn);

                    // Clean up - the item should be consumed/destroyed by Ingested(), but ensure it's gone
                    if (thing.Spawned)
                    {
                        thing.Destroy();
                    }
                }
                else if (thingDef.IsMedicine)
                {
                    // Medicine - add to inventory since immediate use is complex
                    if (!pawn.inventory.innerContainer.TryAdd(thing))
                    {
                        GenPlace.TryPlaceThing(thing, pawn.Position, pawn.Map, ThingPlaceMode.Near);
                    }
                    SoundDefOf.Interact_Tend.PlayOneShot(new TargetInfo(pawn.Position, pawn.Map));
                }
                else if (thingDef.defName.Contains("Psytrainer") || thingDef.defName.Contains("Neurotrainer") || thingDef.defName == "PsychicAmplifier")
                {
                    // FIX: Actually use psy trainers and neurotrainers instead of just adding to inventory
                    UseCompUseEffectItem(thing, pawn);
                }
                else if (thingDef.defName.Contains("Neuroformer"))
                {
                    // Neuroformers - add to inventory (these are typically used via right-click)
                    if (!pawn.inventory.innerContainer.TryAdd(thing))
                    {
                        GenPlace.TryPlaceThing(thing, pawn.Position, pawn.Map, ThingPlaceMode.Near);
                    }
                    SoundDefOf.PsychicPulseGlobal.PlayOneShot(new TargetInfo(pawn.Position, pawn.Map));
                }
                else if (thingDef.defName.Contains("MechSerum"))
                {
                    // Mech serums - add to inventory
                    if (!pawn.inventory.innerContainer.TryAdd(thing))
                    {
                        GenPlace.TryPlaceThing(thing, pawn.Position, pawn.Map, ThingPlaceMode.Near);
                    }
                    SoundDefOf.MechSerumUsed.PlayOneShot(new TargetInfo(pawn.Position, pawn.Map));
                }
                else
                {
                    // Fallback for other usable items - add to inventory
                    if (!pawn.inventory.innerContainer.TryAdd(thing))
                    {
                        GenPlace.TryPlaceThing(thing, pawn.Position, pawn.Map, ThingPlaceMode.Near);
                    }
                    SoundDefOf.Standard_Pickup.PlayOneShot(new TargetInfo(pawn.Position, pawn.Map));
                }

                Logger.Debug($"Used item {thingDef.defName}, played sound effect");
            }
        }

        private static void UseCompUseEffectItem(Thing thing, Verse.Pawn pawn)
        {
            try
            {
                Logger.Debug($"Attempting to use item: {thing.def.defName} on pawn {pawn.Name}");

                // Spawn the item temporarily so comps can initialize
                GenSpawn.Spawn(thing, pawn.Position, pawn.Map);

                List<CompUseEffect> compUseEffects = new List<CompUseEffect>();

                // Get ALL CompUseEffect components, not just the first one
                if (thing is ThingWithComps thingWithComps)
                {
                    foreach (var comp in thingWithComps.AllComps)
                    {
                        if (comp is CompUseEffect compUseEffect)
                        {
                            compUseEffects.Add(compUseEffect);
                        }
                    }
                }

                Logger.Debug($"Found {compUseEffects.Count} CompUseEffect components");

                bool anyEffectApplied = false;

                if (thing.def.defName.Contains("Psytrainer") && !HasPsylink(pawn))
                {
                    Logger.Debug($"Pawn {pawn.Name} does not have psylink, cannot use psy trainer");
                    // Add to inventory instead of using
                    if (thing.Spawned)
                    {
                        thing.DeSpawn();
                    }
                    if (!pawn.inventory.innerContainer.TryAdd(thing))
                    {
                        GenPlace.TryPlaceThing(thing, pawn.Position, pawn.Map, ThingPlaceMode.Near);
                    }
                    return;
                }

                foreach (var compUseEffect in compUseEffects)
                {
                    Logger.Debug($"Processing CompUseEffect: {compUseEffect.GetType().FullName}");

                    AcceptanceReport acceptance = compUseEffect.CanBeUsedBy(pawn);
                    Logger.Debug($"CanBeUsedBy result for {compUseEffect.GetType().Name}: Accepted={acceptance.Accepted}, Reason={acceptance.Reason}");

                    if (acceptance.Accepted)
                    {
                        Logger.Debug($"Calling DoEffect on {compUseEffect.GetType().Name}...");
                        compUseEffect.DoEffect(pawn);
                        Logger.Debug($"DoEffect completed on {compUseEffect.GetType().Name}");
                        anyEffectApplied = true;

                        // Try SelectedUseOption as well
                        try
                        {
                            Logger.Debug($"Calling SelectedUseOption on {compUseEffect.GetType().Name}...");
                            bool selectedResult = compUseEffect.SelectedUseOption(pawn);
                            Logger.Debug($"SelectedUseOption result on {compUseEffect.GetType().Name}: {selectedResult}");
                        }
                        catch (Exception ex)
                        {
                            Logger.Debug($"SelectedUseOption failed on {compUseEffect.GetType().Name} (may be normal): {ex.Message}");
                        }
                    }
                    else
                    {
                        Logger.Warning($"Cannot use {compUseEffect.GetType().Name} on pawn {pawn.Name}: {acceptance.Reason}");
                    }
                }

                if (!anyEffectApplied)
                {
                    Logger.Warning("No CompUseEffect components could be applied to pawn");
                }

                // Despawn the item after use (it's consumed)
                if (thing.Spawned)
                {
                    thing.DeSpawn();
                }

                SoundDefOf.PsychicPulseGlobal.PlayOneShot(new TargetInfo(pawn.Position, pawn.Map));

                // Log skill levels for debugging
                if (thing.def.defName.Contains("Neurotrainer"))
                {
                    var skillDef = GetSkillDefFromNeurotrainer(thing.def.defName);
                    if (skillDef != null)
                    {
                        int skillLevel = pawn.skills.GetSkill(skillDef).Level;
                        Logger.Debug($"Pawn {pawn.Name} {skillDef.defName} skill level after use: {skillLevel}");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error using item {thing.def.defName}: {ex}");
                // Fallback: add to inventory if usage fails
                if (thing.Spawned)
                {
                    thing.DeSpawn();
                }
                if (!pawn.inventory.innerContainer.TryAdd(thing))
                {
                    GenPlace.TryPlaceThing(thing, pawn.Position, pawn.Map, ThingPlaceMode.Near);
                }
            }
        }

        private static void ScheduleSurgeries(Verse.Pawn pawn, RecipeDef recipe, List<BodyPartRecord> bodyParts)
        {
            foreach (var bodyPart in bodyParts)
            {
                var bill = new Bill_Medical(recipe, null) { Part = bodyPart };
                pawn.health.surgeryBills.AddBill(bill);
                Logger.Debug($"Scheduled {recipe.defName} on {bodyPart.Label} for pawn {pawn.Name}");
            }
        }

        private static RecipeDef FindSurgeryRecipeForImplant(ThingDef implantDef, Verse.Pawn pawn)
        {
            return DefDatabase<RecipeDef>.AllDefs
                .Where(r => r.IsSurgery && r.AvailableOnNow(pawn))
                .FirstOrDefault(r => r.ingredients.Any(i => i.filter.AllowedThingDefs.Contains(implantDef)));
        }

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

        private static bool HasSurgeryScheduled(Verse.Pawn pawn, RecipeDef recipe, BodyPartRecord part)
        {
            return pawn.health.surgeryBills.Bills.Any(bill =>
                bill is Bill_Medical medicalBill &&
                medicalBill.recipe == recipe &&
                medicalBill.Part == part);
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

        private static bool HasPsylink(Verse.Pawn pawn)
        {
            if (pawn?.health?.hediffSet?.hediffs == null)
                return false;

            // Check for any psylink hediff
            return pawn.health.hediffSet.hediffs.Any(hediff =>
                hediff.def?.defName?.Contains("Psylink") == true ||
                hediff.def?.defName?.Contains("Psychic") == true);
        }

        private static IntVec3 FindAnimalSpawnPosition(Verse.Pawn viewerPawn)
        {
            if (viewerPawn == null || viewerPawn.Map == null)
                return IntVec3.Invalid;

            // Try to find a position near the viewer pawn where animals would spawn
            if (CellFinder.TryFindRandomCellNear(viewerPawn.Position, viewerPawn.Map, 8,
                (IntVec3 c) => c.Standable(viewerPawn.Map) && !c.Fogged(viewerPawn.Map) && c.Walkable(viewerPawn.Map),
                out IntVec3 spawnPos))
            {
                Logger.Debug($"Found animal spawn position near viewer: {spawnPos}");
                return spawnPos;
            }

            // Fallback to viewer position
            Logger.Debug($"Using viewer position for animal spawn: {viewerPawn.Position}");
            return viewerPawn.Position;
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

        // ===== RESURRECTION METHODS =====
        public static void ResurrectPawn(Verse.Pawn pawn)
        {
            try
            {
                Logger.Debug($"Attempting to resurrect pawn: {pawn?.Name}");

                // Safety check - ensure pawn exists and is actually dead
                if (pawn == null)
                {
                    Logger.Error("Cannot resurrect - pawn is null");
                    return;
                }

                if (!pawn.Dead)
                {
                    Logger.Warning($"Pawn {pawn.Name} is not dead, cannot resurrect");
                    return;
                }

                // Check if pawn is completely destroyed (no corpse exists)
                if (IsPawnCompletelyDestroyed(pawn))
                {
                    Logger.Error($"Cannot resurrect {pawn.Name} - pawn is completely destroyed (no corpse exists)");
                    return;
                }

                Logger.Debug($"Resurrecting pawn: {pawn.Name}");

                // Use RimWorld's built-in resurrection method with side effects
                try
                {
                    ResurrectionUtility.TryResurrectWithSideEffects(pawn);
                }
                catch (NullReferenceException)
                {
                    Logger.Warning("Failed to revive with side effects -- falling back to regular revive");
                    ResurrectionUtility.TryResurrect(pawn);
                }

                Logger.Debug($"Successfully resurrected pawn: {pawn.Name}");
            }
            catch (Exception ex)
            {
                Logger.Error($"Error resurrecting pawn: {ex}");
                throw;
            }
        }

        public static bool IsPawnCompletelyDestroyed(Verse.Pawn pawn)
        {
            try
            {
                // Check if the pawn exists as a corpse in any map
                foreach (var map in Find.Maps)
                {
                    foreach (var thing in map.listerThings.AllThings)
                    {
                        if (thing is Corpse corpse && corpse.InnerPawn == pawn)
                        {
                            return false; // Corpse exists, not completely destroyed
                        }
                    }
                }

                // Check if pawn exists in world pawns (dead)
                if (Find.WorldPawns.AllPawnsDead.Contains(pawn))
                {
                    return false; // Pawn exists in world pawns
                }

                // If we get here, the pawn is completely gone
                Logger.Debug($"Pawn {pawn.Name} is completely destroyed - no corpse found in any map or world pawns");
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error($"Error checking if pawn is destroyed: {ex}");
                return true; // Assume destroyed if we can't check
            }
        }

        // ===== SOUND METHODS =====
        private static void PlayIngestSoundSafely(ThingDef thingDef, Verse.Pawn pawn)
        {
            try
            {
                // Try to use the ingest sound from the thing definition first
                if (thingDef.ingestible.ingestSound != null)
                {
                    // Check if this is a sustainer sound that shouldn't be played as one-shot
                    string soundName = thingDef.ingestible.ingestSound.defName;
                    if (IsSustainerSound(soundName))
                    {
                        Logger.Debug($"Skipping sustainer sound: {soundName}, using fallback");
                        PlayFallbackIngestSound(thingDef, pawn);
                    }
                    else
                    {
                        // It's safe to play as one-shot
                        thingDef.ingestible.ingestSound.PlayOneShot(new TargetInfo(pawn.Position, pawn.Map));
                    }
                }
                else
                {
                    // No specific ingest sound defined, use fallback
                    PlayFallbackIngestSound(thingDef, pawn);
                }
            }
            catch (Exception ex)
            {
                Logger.Debug($"Error playing ingest sound for {thingDef.defName}: {ex.Message}");
                PlayFallbackIngestSound(thingDef, pawn);
            }
        }

        private static void PlayFallbackIngestSound(ThingDef thingDef, Verse.Pawn pawn)
        {
            try
            {
                if (thingDef.IsDrug)
                {
                    // Use specific drug sounds based on drug type
                    if (thingDef.ingestible.drugCategory == DrugCategory.Social || thingDef.defName.Contains("Smoke"))
                    {
                        SoundDefOf.Interact_Ignite.PlayOneShot(new TargetInfo(pawn.Position, pawn.Map));
                    }
                    else if (thingDef.ingestible.drugCategory == DrugCategory.Hard)
                    {
                        SoundDefOf.Crunch.PlayOneShot(new TargetInfo(pawn.Position, pawn.Map));
                    }
                    else
                    {
                        SoundDefOf.Click.PlayOneShot(new TargetInfo(pawn.Position, pawn.Map));
                    }
                }
                else if (thingDef.ingestible.IsMeal)
                {
                    // Use crunch sound for meals (eating sound)
                    SoundDefOf.Crunch.PlayOneShot(new TargetInfo(pawn.Position, pawn.Map));
                }
                else if (thingDef.IsCorpse || thingDef.defName.Contains("Meat"))
                {
                    SoundDefOf.RawMeat_Eat.PlayOneShot(new TargetInfo(pawn.Position, pawn.Map));
                }
                else if (thingDef.IsIngestible && thingDef.ingestible != null &&
                         (thingDef.ingestible.foodType & FoodTypeFlags.Liquor) != 0)
                {
                    // For beer and other liquor, use a liquid sound
                    SoundDefOf.HissSmall.PlayOneShot(new TargetInfo(pawn.Position, pawn.Map));
                }
                else if (thingDef.defName.Contains("Berry") || thingDef.defName.Contains("Fruit"))
                {
                    // For fruits/berries, use raw vegetable eat sound
                    SoundDefOf.RawMeat_Eat.PlayOneShot(new TargetInfo(pawn.Position, pawn.Map));
                }
                else
                {
                    // Default for vegetables and other foods
                    SoundDefOf.Crunch.PlayOneShot(new TargetInfo(pawn.Position, pawn.Map));
                }
            }
            catch (Exception ex)
            {
                Logger.Debug($"Error in PlayFallbackIngestSound: {ex.Message}");
                // Final fallback - use a very basic sound
                SoundDefOf.Click.PlayOneShot(new TargetInfo(pawn.Position, pawn.Map));
            }
        }

        private static bool IsSustainerSound(string soundDefName)
        {
            if (string.IsNullOrEmpty(soundDefName)) return false;

            // Common sustainer sound names that shouldn't be played as one-shot
            string[] sustainerKeywords = {
        "Sustain", "Loop", "Ambient", "Meal_Eat", "Ingest_", "Burning",
        "Wind", "Engine", "Working", "Charging", "Ritual"
    };

            foreach (string keyword in sustainerKeywords)
            {
                if (soundDefName.Contains(keyword))
                    return true;
            }

            return false;
        }

        // ===== INVOICE CREATION METHODS =====
        private static string CreateRimazonSurgeryInvoice(string username, string itemName, int quantity, int price, string currencySymbol, List<BodyPartRecord> bodyParts)
        {
            string invoice = $"RIMAZON SURGERY SERVICE\n";
            invoice += $"====================\n";
            invoice += $"Customer: {username}\n";
            invoice += $"Procedure: {itemName} x{quantity}\n";

            if (bodyParts.Count > 0)
            {
                invoice += $"Body Parts: {string.Join(", ", bodyParts.Select(bp => bp.Label))}\n";
            }

            invoice += $"Service: Surgical Implantation\n";
            invoice += $"====================\n";
            invoice += $"Total: {price}{currencySymbol}\n";
            invoice += $"====================\n";
            invoice += $"Thank you for using Rimazon Surgery!\n";
            invoice += $"Implant delivered to pawn's inventory.\n";
            invoice += $"Surgery scheduled with colony doctors.";

            return invoice;
        }

        private static string CreateRimazonInvoice(string username, string itemName, int quantity, int price, string currencySymbol, QualityCategory? quality, ThingDef material)
        {
            string invoice = $"RIMAZON INVOICE\n";
            invoice += $"====================\n";
            invoice += $"Customer: {username}\n";
            invoice += $"Item: {itemName} x{quantity}\n";

            // Add quality info if specified
            if (quality.HasValue)
            {
                invoice += $"Quality: {quality.Value}\n";
            }

            // Add material info if specified and different from default
            if (material != null)
            {
                invoice += $"Material: {material.LabelCap}\n";
            }

            // Add minification note if applicable
            var thingDef = DefDatabase<ThingDef>.GetNamedSilentFail(itemName.Replace(" ", ""));
            if (thingDef != null && thingDef.Minifiable)
            {
                invoice += $"Note: Delivered in minified form for easy handling\n";
            }

            invoice += $"====================\n";
            invoice += $"Total: {price}{currencySymbol}\n";
            invoice += $"====================\n";
            invoice += $"Thank you for shopping with Rimazon!\n";
            invoice += $"Delivery: Standard Drop Pod\n";
            invoice += $"Satisfaction guaranteed or your coins back!";

            return invoice;
        }

        private static string CreateRimazonInstantInvoice(string username, string itemName, int quantity, int price, string currencySymbol)
        {
            string invoice = $"RIMAZON INSTANT\n";
            invoice += $"====================\n";
            invoice += $"Customer: {username}\n";
            invoice += $"Item: {itemName} x{quantity}\n";
            invoice += $"Service: Immediate Use\n";
            invoice += $"====================\n";
            invoice += $"Total: {price}{currencySymbol}\n";
            invoice += $"====================\n";
            invoice += $"Thank you for using Rimazon Instant!\n";
            invoice += $"No delivery required - instant satisfaction!";

            return invoice;
        }

        private static string CreateRimazonDirectInvoice(string username, string itemName, int quantity, int price, string currencySymbol, string serviceType)
        {
            string emoji = serviceType switch
            {
                "Equip" => "⚔️",
                "Wear" => "👕",
                "Backpack" => "🎒",
                _ => "📦"
            };

            string invoice = $"RIMAZON {serviceType.ToUpper()}\n";
            invoice += $"====================\n";
            invoice += $"Customer: {username}\n";
            invoice += $"Item: {itemName} x{quantity}\n";
            invoice += $"Service: Direct {serviceType}\n";
            invoice += $"====================\n";
            invoice += $"Total: {price}{currencySymbol}\n";
            invoice += $"====================\n";
            invoice += $"Thank you for using Rimazon {serviceType}!\n";

            // Custom message based on service type
            switch (serviceType)
            {
                case "Equip":
                    invoice += $"Weapon equipped and ready for action!";
                    break;
                case "Wear":
                    invoice += $"Apparel worn and looking stylish!";
                    break;
                case "Backpack":
                    invoice += $"Items delivered to your pawn's inventory.";
                    break;
            }

            return invoice;
        }

        public static string CreateRimazonResurrectionInvoice(string username, string itemName, int price, string currencySymbol)
        {
            string invoice = $"RIMAZON RESURRECTION SERVICE\n";
            invoice += $"====================\n";
            invoice += $"Customer: {username}\n";
            invoice += $"Service: Pawn Resurrection\n";
            invoice += $"Item: {itemName}\n";
            invoice += $"====================\n";
            invoice += $"Total: {price}{currencySymbol}\n";
            invoice += $"====================\n";
            invoice += $"Thank you for using Rimazon Resurrection!\n";
            invoice += $"Your pawn has been restored to life!\n";
            invoice += $"Life is precious - cherish every moment! 💖";

            return invoice;
        }

        private static string CreateRimazonPetInvoice(string username, string itemName, int quantity, int price, string currencySymbol)
        {
            string invoice = $"RIMAZON PET DELIVERY\n";
            invoice += $"====================\n";
            invoice += $"Customer: {username}\n";
            invoice += $"Pet: {itemName} x{quantity}\n";
            invoice += $"Service: Live Animal Delivery\n";
            invoice += $"====================\n";
            invoice += $"Total: {price}{currencySymbol}\n";
            invoice += $"====================\n";
            invoice += $"Thank you for using Rimazon Pets!\n";

            if (quantity == 1)
            {
                invoice += $"Your new companion has arrived safely!\n";
            }
            else
            {
                invoice += $"Your new companions have arrived safely!\n";
            }

            invoice += $"All animals are tame and ready for your colony!";

            return invoice;
        }

        // ===== UTILITY METHODS =====
        private static bool IsMajorPurchase(int price, QualityCategory? quality)
        {
            // Legendary quality items
            if (quality.HasValue && quality.Value == QualityCategory.Legendary)
                return true;

            // Very expensive items (adjust threshold as needed)
            if (price >= 5000)
                return true;

            return false;
        }

        private static SkillDef GetSkillDefFromNeurotrainer(string defName)
        {
            return defName.ToLower() switch
            {
                string s when s.Contains("melee") => SkillDefOf.Melee,
                string s when s.Contains("shooting") => SkillDefOf.Shooting,
                string s when s.Contains("construction") => SkillDefOf.Construction,
                string s when s.Contains("mining") => SkillDefOf.Mining,
                string s when s.Contains("cooking") => SkillDefOf.Cooking,
                string s when s.Contains("plants") => SkillDefOf.Plants,
                string s when s.Contains("animals") => SkillDefOf.Animals,
                string s when s.Contains("crafting") => SkillDefOf.Crafting,
                string s when s.Contains("artistic") => SkillDefOf.Artistic,
                string s when s.Contains("medical") => SkillDefOf.Medicine,
                string s when s.Contains("social") => SkillDefOf.Social,
                string s when s.Contains("intellectual") => SkillDefOf.Intellectual,
                _ => null
            };
        }

    }
}