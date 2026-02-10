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
using CAP_ChatInteractive.Commands.Cooldowns;
using CAP_ChatInteractive.Utilities;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Verse;
using static CAP_ChatInteractive.Commands.CommandHandlers.StoreCommandHelper;
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
                    return $"Item '{itemName}' not found in Rimazon.";
                }

                if (!storeItem.Enabled && !requireEquippable && !requireWearable)
                {
                    Logger.Debug($"Item disabled: {itemName}");
                    return $"Item '{itemName}' is not available for purchase.";
                }

                if (requireEquippable && !storeItem.IsEquippable)
                {
                    Logger.Debug($"Item not equippable: {itemName}");
                    return $"{itemName} is not availible to be equiped.";
                }

                if (requireWearable && !storeItem.IsWearable)
                {
                    Logger.Debug($"Item not wearable: {itemName}");
                    return $"{itemName} iis not availible to be worn.";
                }

                // Check item type requirements
                if (!StoreCommandHelper.IsItemTypeValid(storeItem, requireEquippable, requireWearable, false))
                {
                    string itemType = StoreCommandHelper.GetItemTypeDescription(storeItem);
                    string expectedType = requireEquippable ? "equippable" : requireWearable ? "wearable" : "purchasable";
                    return $"{itemName} is a {itemType}, not an {expectedType} item. Use !buy instead.";
                }

                // Check research requirements
                Logger.Debug($"Checking research requirements for {itemName}");
                if (!StoreCommandHelper.HasRequiredResearch(storeItem))
                {
                    Logger.Debug($"Research requirement failed for {itemName}");
                    return $"{itemName} requires research that hasn't been completed yet.";
                }
                Logger.Debug($"Research requirements met for {itemName}");

                // Parse quality
                var quality = ItemConfigHelper.ParseQuality(qualityStr);
                Logger.Debug($"Parsed quality: {qualityStr} -> {quality}");

                if (!ItemConfigHelper.IsQualityAllowed(quality))
                {
                    Logger.Debug($"Quality {qualityStr} is not allowed for purchases");
                    return $"Quality '{qualityStr}' is not allowed for purchases.";
                }
                Logger.Debug($"Quality {qualityStr} is allowed");

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
                    material = ItemConfigHelper.ParseMaterial(materialStr, thingDef);
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
                (List<Thing> spawnedItems, IntVec3 deliveryPos) spawnResult;
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
                    string emoji = "🐾";
                    invoiceLabel = $"{emoji} Rimazon Pet Delivery - {messageWrapper.Username}";
                    invoiceMessage = CreateRimazonPetInvoice(messageWrapper.Username, itemLabel, quantity, finalPrice, currencySymbol);
                }
                // Then check for backpack/equip/wear
                else if (addToInventory || requireEquippable || requireWearable)
                {
                    // Backpack, Equip, and Wear all involve direct delivery to pawn
                    string serviceType = requireEquippable ? "Equip" : requireWearable ? "Wear" : "Backpack";
                    string emoji = requireEquippable ? "⚔️" : requireWearable ? "👕" : "🎒";

                    invoiceLabel = $"{emoji} Rimazon {serviceType} - {messageWrapper.Username}";
                    invoiceMessage = CreateRimazonDirectInvoice(messageWrapper.Username, itemLabel, quantity, finalPrice, currencySymbol, serviceType);
                }
                else
                {
                    // Regular delivery with tracking
                    invoiceLabel = $"🟡 Rimazon Delivery - {messageWrapper.Username}";

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

        private static string CreateRimazonInvoice(string username, string itemName, int quantity, int price,
            string currencySymbol, QualityCategory? quality, ThingDef material, DeliveryResult deliveryResult)
        {
            // Get counts from delivery result
            int lockerCount = deliveryResult.LockerDeliveredItems.Sum(t => t.stackCount);
            int dropPodCount = deliveryResult.DropPodDeliveredItems.Sum(t => t.stackCount);

            string invoice = $"RIMAZON INVOICE\n";
            invoice += $"====================\n";
            invoice += $"Customer: {username}\n";
            invoice += $"Item: {itemName} x{quantity}\n";

            // Show delivery breakdown
            if (lockerCount > 0 && dropPodCount > 0)
            {
                invoice += $"Delivery: Mixed Delivery\n";
                invoice += $"  • Locker Delivery: x{lockerCount}\n";
                invoice += $"  • Drop Pod Delivery: x{dropPodCount}\n";
            }
            else if (lockerCount > 0)
            {
                invoice += $"Delivery: Locker Delivery (x{lockerCount})\n";
            }
            else
            {
                invoice += $"Delivery: Standard Drop Pod\n";
            }

            // Add quality info if specified
            if (quality.HasValue)
            {
                invoice += $"Quality: {quality.Value}\n";
            }

            // Add material info if specified
            if (material != null)
            {
                invoice += $"Material: {material.LabelCap}\n";
            }

            invoice += $"====================\n";

            // Show pricing breakdown for mixed delivery
            if (lockerCount > 0 && dropPodCount > 0)
            {
                // Calculate price per item
                float pricePerItem = (float)price / quantity;
                int lockerPrice = (int)(pricePerItem * dropPodCount); // Only charge for drop pod items
                int dropPodPrice = (int)(pricePerItem * dropPodCount);

                invoice += $"Locker Delivery (x{lockerCount}): 0{currencySymbol}\n";
                invoice += $"Drop Pod Delivery (x{dropPodCount}): {dropPodPrice:N0}{currencySymbol}\n";
                invoice += $"====================\n";
                invoice += $"Total: {price:N0}{currencySymbol}\n";
            }
            else
            {
                invoice += $"Total: {price:N0}{currencySymbol}\n";
            }

            invoice += $"====================\n";
            invoice += $"Thank you for shopping with Rimazon!\n";

            if (lockerCount > 0 && dropPodCount > 0)
            {
                invoice += $"Items delivered via both Locker and Drop Pod.\n";
            }
            else if (lockerCount > 0)
            {
                invoice += $"Items delivered to your Rimazon Locker.\n";
            }

            invoice += $"Satisfaction guaranteed or your coins back!";

            return invoice;
        }

        // Alternative: Split invoice method
        private static string CreateSplitInvoice(string username, string itemName, int quantity, int price,
            string currencySymbol, QualityCategory? quality, ThingDef material, DeliveryResult deliveryResult)
        {
            int lockerCount = deliveryResult.LockerDeliveredItems.Sum(t => t.stackCount);
            int dropPodCount = deliveryResult.DropPodDeliveredItems.Sum(t => t.stackCount);

            StringBuilder invoice = new StringBuilder();

            if (lockerCount > 0)
            {
                invoice.AppendLine("=== LOCKER DELIVERY INVOICE ===");
                invoice.AppendLine($"Customer: {username}");
                invoice.AppendLine($"Item: {itemName} x{lockerCount}");
                invoice.AppendLine("====================");
                invoice.AppendLine($"Delivery: Locker Delivery");
                invoice.AppendLine($"Status: Delivered ✅");
                invoice.AppendLine($"Total: 0{currencySymbol}"); // Free
                invoice.AppendLine("====================");
                invoice.AppendLine();
            }

            if (dropPodCount > 0)
            {
                invoice.AppendLine("=== DROP POD DELIVERY INVOICE ===");
                invoice.AppendLine($"Customer: {username}");
                invoice.AppendLine($"Item: {itemName} x{dropPodCount}");
                invoice.AppendLine("====================");

                if (quality.HasValue)
                {
                    invoice.AppendLine($"Quality: {quality.Value}");
                }

                if (material != null)
                {
                    invoice.AppendLine($"Material: {material.LabelCap}");
                }

                invoice.AppendLine($"Delivery: Standard Drop Pod");
                invoice.AppendLine($"Status: Delivered ✅");

                // Calculate price only for drop pod items
                float pricePerItem = (float)price / quantity;
                int dropPodPrice = (int)(pricePerItem * dropPodCount);

                invoice.AppendLine($"Total: {dropPodPrice:N0}{currencySymbol}");
                invoice.AppendLine("====================");
            }

            invoice.AppendLine("Thank you for shopping with Rimazon!");
            invoice.AppendLine("Satisfaction guaranteed or your coins back!");

            return invoice.ToString();
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
            invoice += $"Total: {price:N0}{currencySymbol}\n";
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

        private static string CreateRimazonPetInvoice(string username, string itemName, int quantity, int price, string currencySymbol)
        {
            string invoice = $"RIMAZON PET DELIVERY\n";
            invoice += $"====================\n";
            invoice += $"Customer: {username}\n";
            invoice += $"Pet: {itemName} x{quantity}\n";
            invoice += $"Service: Live Animal Delivery\n";
            invoice += $"====================\n";
            invoice += $"Total: {price:N0}{currencySymbol}\n";
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
                            addHistoryMethod.Invoke(null, new object[] { item, ownerPawn, "Purchased via Rimazon" });
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