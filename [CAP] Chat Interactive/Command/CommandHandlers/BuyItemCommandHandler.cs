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
// Command handler for buying items from Rimazon store
using _CAP__Chat_Interactive.Command.CommandHelpers;
using CAP_ChatInteractive.Commands.Cooldowns;
using CAP_ChatInteractive.Utilities;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Verse;
using static _CAP__Chat_Interactive.Command.CommandHelpers.StoreCommandHelper;
using Pawn = CAP_ChatInteractive.Commands.ViewerCommands.Pawn;

namespace CAP_ChatInteractive.Commands.CommandHandlers
{
    public static class BuyItemCommandHandler
    {
        // ===== MAIN COMMAND HANDLERS =====
        public static string HandleBuyItem(ChatMessageWrapper messageWrapper, string[] args, bool requireEquippable = false, bool requireWearable = false, bool addToInventory = false)
        {
            try
            {
                Logger.Debug($"HandleBuyItem called for user: {messageWrapper.Username}, command {messageWrapper.Message}, args: {string.Join(", ", args)}, requireEquippable: {requireEquippable}, requireWearable: {requireWearable}, addToInventory: {addToInventory}");

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
                var viewer = Viewers.GetViewer(messageWrapper);

                // Get store item
                var storeItem = StoreCommandHelper.GetStoreItemByName(itemName);
                Logger.Debug($"Store item lookup for '{itemName}': {(storeItem != null ? $"Found: {storeItem.DefName}, Enabled: {storeItem.Enabled}" : "Not Found")}");

                if (storeItem == null)
                {
                    Logger.Debug($"Item not found: {itemName}");
                    // return $"Item '{itemName}' not found in Rimazon.";
                    return "RICS.BICH.Return.ItemNotFound".Translate(itemName);
                }

                if (!storeItem.Enabled && !requireEquippable && !requireWearable)
                {
                    Logger.Debug($"Item disabled: {itemName}");
                    // return $"Item '{itemName}' is not available for purchase.";
                    return "RICS.BICH.Return.ItemDisabled".Translate(itemName);
                }

                if (requireEquippable && !storeItem.IsEquippable)
                {
                    Logger.Debug($"Item not equippable: {itemName}");
                    // return $"{itemName} is not availible to be equiped.";
                    return "RICS.BICH.Return.ItemNotEquippable".Translate(itemName);
                }

                if (requireWearable && !storeItem.IsWearable)
                {
                    Logger.Debug($"Item not wearable: {itemName}");
                    // return $"{itemName} iis not availible to be worn.";
                    return "RICS.BICH.Return.ItemNotWearable".Translate(itemName);
                }

                // Check item type requirements
                if (!StoreCommandHelper.IsItemTypeValid(storeItem, requireEquippable, requireWearable, false))
                {
                    string itemType = StoreCommandHelper.GetItemTypeDescription(storeItem);
                    string expectedType = requireEquippable ? "equippable" : requireWearable ? "wearable" : "purchasable";
                    // return $"{itemName} is a {itemType}, not an {expectedType} item. Use !buy instead.";
                    return "RICS.BICH.Return.WrongItemType".Translate(itemName, itemType, expectedType);
                }

                // Check research requirements
                Logger.Debug($"Checking research requirements for {itemName}");
                if (!StoreCommandHelper.HasRequiredResearch(storeItem))
                {
                    Logger.Debug($"Research requirement failed for {itemName}");
                    // return $"{itemName} requires research that hasn't been completed yet.";
                    return "RICS.BICH.Return.ResearchRequired".Translate(itemName);
                }
                Logger.Debug($"Research requirements met for {itemName}");

                // Parse quality
                var quality = ItemConfigHelper.ParseQuality(qualityStr);
                Logger.Debug($"Parsed quality: {qualityStr} -> {quality}");

                if (!ItemConfigHelper.IsQualityAllowed(quality))
                {
                    Logger.Debug($"Quality {qualityStr} is not allowed for purchases");
                    // return $"Quality '{qualityStr}' is not allowed for purchases.";
                    return "RICS.BICH.Return.QualityNotAllowed".Translate(qualityStr);
                }
                Logger.Debug($"Quality {qualityStr} is allowed");

                // Get thing def
                var thingDef = DefDatabase<ThingDef>.GetNamedSilentFail(storeItem.DefName);
                if (thingDef == null)
                {
                    Logger.Error($"ThingDef not found: {storeItem.DefName}");
                    // return $"Error: Item definition not found.";
                    return "RICS.BICH.Return.DefNotFound".Translate();
                }

                // Check for banned races  -- Needed?
                if (StoreCommandHelper.IsRaceBanned(thingDef))
                {
                    // return $"Item '{itemName}' is a banned race and cannot be purchased.";
                    return "RICS.BICH.Return.RaceBanned".Translate(itemName);
                }

                // Parse material
                ThingDef material = null;
                if (thingDef.MadeFromStuff)
                {
                    material = ItemConfigHelper.ParseMaterial(materialStr, thingDef);
                    if (materialStr != "random" && material == null)
                    {
                        // return $"Material '{materialStr}' is not valid for {itemName}.";
                        return "RICS.BICH.Return.InvalidMaterial".Translate(materialStr, itemName);
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

                // Calculate final price
                int finalPrice = ItemConfigHelper.CalculateFinalPrice(storeItem, quantity, quality, material);

                // Check if user can afford
                if (!StoreCommandHelper.CanUserAfford(messageWrapper, finalPrice))
                {
                    return $"You need {StoreCommandHelper.FormatCurrencyMessage(finalPrice, currencySymbol)} to purchase {quantity}x {itemName}! You have {StoreCommandHelper.FormatCurrencyMessage(viewer.Coins, currencySymbol)}.";
                }

                // Get viewer's pawn for equip/wear/backpack commands
                Verse.Pawn viewerPawn = null;

                if (requireEquippable || requireWearable || addToInventory)
                {
                    viewerPawn = PawnItemHelper.GetViewerPawn(messageWrapper);
                    if (viewerPawn == null)
                    {
                        // return "You need to have a pawn in the colony. Use !buy pawn first.";
                        return "RICS.BICH.Return.NoPawn".Translate();
                    }

                    if (viewerPawn.Dead)
                    {
                        // return "Your pawn is dead. You cannot equip/wear items.";
                        return "RICS.BICH.Return.PawnDead".Translate();
                    }
                }
                else
                {
                    // For regular buy commands, try to get the pawn but don't require it
                    viewerPawn = PawnItemHelper.GetViewerPawn(messageWrapper);
                    // Log if no pawn found for debugging
                    if (viewerPawn == null)
                    {
                        Logger.Debug($"No pawn assigned to {messageWrapper.Username}, using colony-wide delivery");
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

                // Spawn the item
                var cooldownManager = Current.Game.GetComponent<GlobalCooldownManager>();
                //(List<Thing> spawnedItems, IntVec3 deliveryPos) spawnResult;
                DeliveryResult deliveryResult;

                if (requireEquippable || requireWearable)
                {
                    deliveryResult = ItemDeliveryHelper.SpawnItemForPawn(thingDef,
                        quantity, quality, material, viewerPawn, false, requireEquippable, requireWearable);
                }
                else
                {
                    deliveryResult = ItemDeliveryHelper.SpawnItemForPawn(thingDef,
                        quantity, quality, material, viewerPawn, addToInventory, false, false);
                }

                List<Thing> allSpawnedItems = new List<Thing>();
                allSpawnedItems.AddRange(deliveryResult.LockerDeliveredItems);
                allSpawnedItems.AddRange(deliveryResult.DropPodDeliveredItems);
                allSpawnedItems.AddRange(deliveryResult.DirectlyDeliveredItems);

                // Set ownership for each spawned item if this is a direct pawn delivery
                Logger.Debug($"Setting ownership for spawned items - requireEquippable: {requireEquippable}, requireWearable: {requireWearable}, addToInventory: {addToInventory}");
                if (requireEquippable || requireWearable || addToInventory)
                {
                    foreach (Thing spawnedItem in allSpawnedItems)
                    {
                        Logger.Debug($"Setting ownership for {spawnedItem.def.defName} to {viewerPawn.Name}");
                        TrySetItemOwnership(spawnedItem, viewerPawn);
                    }
                }

                // Create look targets - use the delivery position we know items will be at
                LookTargets lookTargets = null;

                // For the LookTargets code, update to use deliveryResult.DeliveryPosition:
                if (thingDef.thingClass == typeof(Verse.Pawn))
                {
                    // For animal deliveries, target the EXACT SPAWN POSITION
                    if (deliveryResult.DeliveryPosition.IsValid)
                    {
                        Map targetMap = viewerPawn?.Map ?? Find.CurrentMap ?? Find.Maps.FirstOrDefault(m => m.IsPlayerHome);
                        if (targetMap != null)
                        {
                            lookTargets = new LookTargets(deliveryResult.DeliveryPosition, targetMap);
                            Logger.Debug($"Created LookTargets for exact animal spawn position: {deliveryResult.DeliveryPosition}");
                        }
                    }
                }
                else if (requireEquippable || requireWearable || addToInventory)
                {
                    // For direct pawn interactions, target the pawn
                    lookTargets = viewerPawn != null ? new LookTargets(viewerPawn) : null;
                    Logger.Debug($"Created LookTargets for pawn: {viewerPawn?.Name}");
                }
                else if (deliveryResult.PrimaryMethod == DeliveryMethod.Locker)
                {
                    // For locker deliveries, target the locker position
                    if (deliveryResult.DeliveryPosition.IsValid)
                    {
                        Map targetMap = viewerPawn?.Map ?? Find.CurrentMap ?? Find.Maps.FirstOrDefault(m => m.IsPlayerHome);
                        if (targetMap != null)
                        {
                            lookTargets = new LookTargets(deliveryResult.DeliveryPosition, targetMap);
                            Logger.Debug($"Created LookTargets for locker position: {deliveryResult.DeliveryPosition}");
                        }
                    }
                }
                else if (deliveryResult.DeliveryPosition.IsValid)
                {
                    // For drop pod deliveries, target the delivery position
                    Map targetMap = viewerPawn?.Map ?? Find.CurrentMap ?? Find.Maps.FirstOrDefault(m => m.IsPlayerHome);
                    if (targetMap != null)
                    {
                        lookTargets = new LookTargets(deliveryResult.DeliveryPosition, targetMap);
                        Logger.Debug($"Created LookTargets for delivery position: {deliveryResult.DeliveryPosition} on map {targetMap}");
                    }
                }

                Logger.Debug($"Final LookTargets: {lookTargets?.ToString() ?? "null"}");

                // Log success
                Logger.Debug($"Purchase successful: {messageWrapper.Username} bought {quantity}x {itemName} for {finalPrice} {currencySymbol}");

                // Send appropriate letter notification
                string itemLabel = thingDef?.LabelCap ?? itemName;
                string invoiceLabel = "";
                string invoiceMessage = "";
                string tClass = thingDef.thingClass.ToString();

                // SPECIAL CASE: Animal deliveries - CHECK THIS FIRST
                Logger.Debug($"Checking for special invoice case for item: {thingDef.thingClass} tClass: {tClass}");
                if (thingDef.thingClass == typeof(Verse.Pawn) || tClass == "Verse.Pawn")
                {
                    invoiceLabel = "RICS.BICH.Letter.Label.Pet".Translate(messageWrapper.Username);
                    invoiceMessage = CreateRimazonPetInvoice(messageWrapper.Username, itemLabel, quantity, finalPrice, currencySymbol);
                }
                // Then check for backpack/equip/wear
                else if (addToInventory || requireEquippable || requireWearable)
                {
                    // Backpack, Equip, and Wear all involve direct delivery to pawn
                    string serviceType = requireEquippable ? "Equip" : requireWearable ? "Wear" : "Backpack";
                    string emoji = requireEquippable ? "RICS.BICH.Letter.Emoji.Equip".Translate() :
                                   requireWearable ? "RICS.BICH.Letter.Emoji.Wear".Translate() :
                                   "RICS.BICH.Letter.Emoji.Backpack".Translate();

                    invoiceLabel = "RICS.BICH.Letter.Label.Direct".Translate(emoji, serviceType, messageWrapper.Username);
                    invoiceMessage = CreateRimazonDirectInvoice(messageWrapper.Username, itemLabel, quantity, finalPrice, currencySymbol, serviceType);
                }
                else
                {
                    // Regular delivery with tracking
                    invoiceLabel = "RICS.BICH.Letter.Label.Standard".Translate(messageWrapper.Username);

                    // Check if we have mixed delivery
                    if (deliveryResult.LockerDeliveredItems.Count > 0 && deliveryResult.DropPodDeliveredItems.Count > 0)
                    {
                        // Use split invoice for mixed delivery
                        invoiceMessage = CreateSplitInvoice(messageWrapper.Username, itemLabel, quantity, finalPrice,
                            currencySymbol, quality, material, deliveryResult);
                    }
                    else
                    {
                        // Single delivery method
                        invoiceMessage = CreateRimazonInvoice(messageWrapper.Username, itemLabel, quantity, finalPrice,
                            currencySymbol, quality, material, deliveryResult);
                    }
                }

                // Send the letter
                if (UseItemCommandHandler.IsMajorPurchase(finalPrice, quality))
                {
                    MessageHandler.SendGoldLetter(invoiceLabel, invoiceMessage, lookTargets);
                }
                else
                {
                    MessageHandler.SendGreenLetter(invoiceLabel, invoiceMessage, lookTargets);
                }

                // Return success message
                // string action = addToInventory ? "added to your pawn's inventory" :
                //              requireEquippable ? "equipped to your pawn" :
                //              requireWearable ? "worn by your pawn" : "delivered via Rimazon";
                string action = requireEquippable ? "RICS.BICH.Return.Action.Equipped".Translate() :
                                requireWearable ? "RICS.BICH.Return.Action.Worn".Translate() :
                                addToInventory ? "RICS.BICH.Return.Action.AddedToInventory".Translate() :
                                "RICS.BICH.Return.Action.Delivered".Translate();

                // return $"Purchased {quantity}x {itemName} for {StoreCommandHelper.FormatCurrencyMessage(finalPrice, currencySymbol)} and {action}! Remaining: {StoreCommandHelper.FormatCurrencyMessage(viewer.Coins, currencySymbol)}.";
                return "RICS.BICH.Return.Success".Translate(quantity, itemName, StoreCommandHelper.FormatCurrencyMessage(finalPrice, currencySymbol), action, StoreCommandHelper.FormatCurrencyMessage(viewer.Coins, currencySymbol));
            }
            catch (Exception ex)
            {
                Logger.Error($"Error in HandleBuyItem: {ex}");
                // return "Error processing purchase. Please try again.";
                return "RICS.BICH.Return.GenericError".Translate();
            }
        }

        private static string CreateRimazonInvoice(string username, string itemName, int quantity, int price,
            string currencySymbol, QualityCategory? quality, ThingDef material, DeliveryResult deliveryResult)
        {
            int lockerCount = deliveryResult.LockerDeliveredItems.Sum(t => t.stackCount);
            int dropPodCount = deliveryResult.DropPodDeliveredItems.Sum(t => t.stackCount);

            // ───────────────────────────────────────────────
            // Build clean delivery line(s)
            // ───────────────────────────────────────────────
            string deliveryLine = "";

            if (lockerCount > 0 && dropPodCount > 0)
            {
                deliveryLine = "RICS.BICH.Letter.Delivery.Mixed".Translate(lockerCount, dropPodCount);
            }
            else if (lockerCount > 0)
            {
                deliveryLine = "RICS.BICH.Letter.Delivery.Locker".Translate(lockerCount);
            }
            else
            {
                deliveryLine = "RICS.BICH.Letter.Delivery.DropPod".Translate();
            }

            // Optional quality & material as separate lines
            string extraSpecs = "";
            if (quality.HasValue)
            {
                extraSpecs += "\n" + "RICS.BICH.Letter.Quality".Translate(quality.Value.ToStringHuman());
            }
            if (material != null)
            {
                extraSpecs += "\n" + "RICS.BICH.Letter.Material".Translate(material.LabelCap);
            }

            // Only show pricing breakdown in mixed case (and put it after Total)
            string pricingNote = "";
            if (lockerCount > 0 && dropPodCount > 0)
            {
                float pricePer = (float)price / quantity;
                int dropPodPrice = (int)(pricePer * dropPodCount);

                pricingNote = "\n" +
                    "RICS.BICH.Letter.Pricing.Locker".Translate(lockerCount, currencySymbol) +
                    "RICS.BICH.Letter.Pricing.DropPod".Translate(dropPodCount, dropPodPrice.ToString("N0"), currencySymbol) +
                    "\n" +
                    "RICS.BICH.Letter.Pricing.Total".Translate(price.ToString("N0"), currencySymbol);
            }

            // ───────────────────────────────────────────────
            // Final invoice assembly
            // ───────────────────────────────────────────────
            string body = "RICS.BICH.Letter.Body.Standard".Translate(
                username,               // {0}
                $"{itemName} x{quantity}", // {1}
                deliveryLine + extraSpecs, // {2}  ← changed meaning: now only delivery + specs
                price.ToString("N0"),   // {3}
                currencySymbol          // {4}
            );

            // Append pricing note only when relevant (after the thank you line is fine)
            if (!string.IsNullOrEmpty(pricingNote))
            {
                body += "\n" + pricingNote;
            }

            // Optional contextual note
            if (lockerCount > 0 && dropPodCount == 0)
            {
                body += "\n" + "RICS.BICH.Letter.Locker.Note".Translate();
            }
            else if (lockerCount > 0 && dropPodCount > 0)
            {
                body += "\n" + "RICS.BICH.Letter.Mixed.Note".Translate();
            }

            return body;
        }

        private static string BuildDeliveryDetails(int lockerCount, int dropPodCount, QualityCategory? quality, ThingDef material)
        {
            string deliveryInfo = "";

            // Add delivery method
            if (lockerCount > 0 && dropPodCount > 0)
            {
                deliveryInfo += "RICS.BICH.Letter.Delivery.Mixed".Translate(lockerCount.ToString(), dropPodCount.ToString());
            }
            else if (lockerCount > 0)
            {
                deliveryInfo += "RICS.BICH.Letter.Delivery.Locker".Translate(lockerCount.ToString());
            }
            else
            {
                deliveryInfo += "RICS.BICH.Letter.Delivery.DropPod".Translate();
            }

            // Add quality if specified
            if (quality.HasValue)
            {
                deliveryInfo += "RICS.BICH.Letter.Quality".Translate(quality.Value.ToString());
            }

            // Add material if specified
            if (material != null)
            {
                deliveryInfo += "RICS.BICH.Letter.Material".Translate(material.LabelCap);
            }

            return deliveryInfo;
        }

        // Alternative: Split invoice method
        private static string CreateSplitInvoice(string username, string itemName, int quantity, int price,
    string currencySymbol, QualityCategory? quality, ThingDef material, DeliveryResult deliveryResult)
        {
            int lockerCount = deliveryResult.LockerDeliveredItems.Sum(t => t.stackCount);
            int dropPodCount = deliveryResult.DropPodDeliveredItems.Sum(t => t.stackCount);

            StringBuilder invoice = new StringBuilder();

            // Locker delivery section
            if (lockerCount > 0)
            {
                invoice.AppendLine("RICS.BICH.Letter.Split.LockerHeader".Translate());
                invoice.AppendLine("RICS.BICH.Letter.Split.CustomerLine".Translate(username));
                invoice.AppendLine("RICS.BICH.Letter.Split.ItemLine".Translate(itemName, lockerCount.ToString()));
                invoice.AppendLine("RICS.BICH.Letter.Split.Separator".Translate());
                invoice.AppendLine("RICS.BICH.Letter.Split.DeliveryLocker".Translate());
                invoice.AppendLine("RICS.BICH.Letter.Split.StatusDelivered".Translate());
                invoice.AppendLine("RICS.BICH.Letter.Split.TotalFree".Translate(currencySymbol));
                invoice.AppendLine("RICS.BICH.Letter.Split.Separator".Translate());
                invoice.AppendLine();
            }

            // Drop pod delivery section
            if (dropPodCount > 0)
            {
                invoice.AppendLine("RICS.BICH.Letter.Split.DropPodHeader".Translate());
                invoice.AppendLine("RICS.BICH.Letter.Split.CustomerLine".Translate(username));
                invoice.AppendLine("RICS.BICH.Letter.Split.ItemLine".Translate(itemName, dropPodCount.ToString()));
                invoice.AppendLine("RICS.BICH.Letter.Split.Separator".Translate());

                // Add quality if specified
                if (quality.HasValue)
                {
                    invoice.AppendLine("RICS.BICH.Letter.Split.QualityLine".Translate(quality.Value.ToString()));
                }

                // Add material if specified
                if (material != null)
                {
                    invoice.AppendLine("RICS.BICH.Letter.Split.MaterialLine".Translate(material.LabelCap));
                }

                invoice.AppendLine("RICS.BICH.Letter.Split.DeliveryDropPod".Translate());
                invoice.AppendLine("RICS.BICH.Letter.Split.StatusDelivered".Translate());

                // Calculate price only for drop pod items
                float pricePerItem = (float)price / quantity;
                int dropPodPrice = (int)(pricePerItem * dropPodCount);

                invoice.AppendLine("RICS.BICH.Letter.Split.TotalLine".Translate(dropPodPrice.ToString("N0"), currencySymbol));
                invoice.AppendLine("RICS.BICH.Letter.Split.Separator".Translate());
            }

            invoice.AppendLine("RICS.BICH.Letter.Split.Footer".Translate());

            return invoice.ToString();
        }

        private static string CreateRimazonDirectInvoice(string username, string itemName, int quantity, int price, string currencySymbol, string serviceType)
        {
            string invoice = "RICS.BICH.Letter.Direct.Body".Translate(
                serviceType.ToUpper(),
                username,
                itemName,
                quantity.ToString(),
                price.ToString("N0"),
                currencySymbol,
                GetDirectServiceMessage(serviceType)
            );

            return invoice;
        }

        private static string GetDirectServiceMessage(string serviceType)
        {
            return serviceType switch
            {
                "Equip" => "RICS.BICH.Letter.Direct.EquipMessage".Translate(),
                "Wear" => "RICS.BICH.Letter.Direct.WearMessage".Translate(),
                "Backpack" => "RICS.BICH.Letter.Direct.BackpackMessage".Translate(),
                _ => ""
            };
        }
        private static string CreateRimazonPetInvoice(string username, string itemName, int quantity, int price, string currencySymbol)
        {
            string petMessage = quantity == 1
                ? "RICS.BICH.Letter.Pet.Singular".Translate()
                : "RICS.BICH.Letter.Pet.Plural".Translate();

            string invoice = "RICS.BICH.Letter.Body.Pet".Translate(
                username,
                itemName,
                quantity.ToString(),
                price.ToString("N0"),
                currencySymbol,
                petMessage
            );

            return invoice;
        }
        // ===== PossessionsPlus  METHODS =====

        private static void TrySetItemOwnership(Thing item, Verse.Pawn ownerPawn)
        {
            try
            {
                if (item == null || ownerPawn == null)
                {
                    Logger.Debug($"Cannot set ownership - item or pawn is null");
                    return;
                }

                Logger.Debug($"Attempting to set ownership for {item.def.defName} to pawn {ownerPawn.Name}");

                // Use reflection to get the PossessionsPlus ownership component
                Type ownershipCompType = Type.GetType("PossessionsPlus.CompOwnedByPawn_Item, PossessionsPlus");

                if (ownershipCompType == null)
                {
                    Logger.Debug("PossessionsPlus mod not found - ownership not set");
                    return;
                }

                if (!(item is ThingWithComps thingWithComps))
                {
                    Logger.Debug($"Item {item.def.defName} is not a ThingWithComps - ownership not set");
                    return;
                }

                // Get the ownership component from the item
                var getCompMethod = typeof(ThingWithComps).GetMethod("GetComp")?.MakeGenericMethod(ownershipCompType);
                if (getCompMethod == null)
                {
                    Logger.Debug("Could not find GetComp method - ownership not set");
                    return;
                }

                var ownershipComp = getCompMethod.Invoke(thingWithComps, null);

                if (ownershipComp == null)
                {
                    Logger.Debug($"Item {item.def.defName} does not have CompOwnedByPawn_Item component - ownership not set");
                    return;
                }

                Logger.Debug($"Found ownership component for {item.def.defName}");

                // Direct field assignment - bypasses all checks
                var ownerField = ownershipCompType.GetField("owner", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
                if (ownerField != null)
                {
                    ownerField.SetValue(ownershipComp, ownerPawn);
                    Logger.Debug($"Owner field set to {ownerPawn.Name}");
                }
                else
                {
                    Logger.Debug("Could not find owner field - ownership not set");
                    return;
                }

                // Set ownership start day
                var startDayField = ownershipCompType.GetField("OwnershipStartDay", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
                if (startDayField != null)
                {
                    int currentDay = GenLocalDate.DayOfYear(ownerPawn.MapHeld ?? Find.CurrentMap) + 1;
                    startDayField.SetValue(ownershipComp, currentDay);
                    Logger.Debug($"OwnershipStartDay set to {currentDay}");
                }

                // Optional: Add to inheritance history
                try
                {
                    var inheritanceHistoryType = Type.GetType("PossessionsPlus.InheritanceHistoryComp, PossessionsPlus");
                    if (inheritanceHistoryType != null)
                    {
                        var addHistoryMethod = inheritanceHistoryType.GetMethod("AddHistoryEntry",
                            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);

                        if (addHistoryMethod != null)
                        {
                            // addHistoryMethod.Invoke(null, new object[] { item, ownerPawn, "Purchased via Rimazon" });
                            addHistoryMethod.Invoke(null, new object[] { item, ownerPawn, "RICS.BICH.Ownership.HistoryEntry".Translate() });
                            Logger.Debug("Added inheritance history entry");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.Debug($"Could not add inheritance history (this is optional): {ex.Message}");
                }

                Logger.Debug($"Successfully set ownership of {item.def.defName} to {ownerPawn.Name}");
            }
            catch (Exception ex)
            {
                Logger.Error($"Error setting item ownership: {ex}");
            }
        }

        // ===== DEBUG METHODS =====
    }
}