// StoreCommandHelper.cs
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
// Helper methods for store command handling
using _CAP__Chat_Interactive.Utilities;
using CAP_ChatInteractive.Store;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace CAP_ChatInteractive.Commands.CommandHandlers
{
    public static class StoreCommandHelper
    {
        public static StoreItem GetStoreItemByName(string itemName)
        {
            if (string.IsNullOrWhiteSpace(itemName))
                return null;

            // Clean the item name
            string cleanItemName = itemName.Trim();
            cleanItemName = cleanItemName.TrimEnd('(', '[', '{').TrimStart(')', ']', '}').Trim();

            Logger.Debug($"Looking up store item for: '{itemName}' (cleaned: '{cleanItemName}')");

            // Check if this is a banned race first
            if (IsRaceBannedByName(cleanItemName))
            {
                Logger.Debug($"Item '{cleanItemName}' is a banned race, skipping store lookup");
                return null;
            }
            // Try exact matches first
            var exactMatch = StoreInventory.AllStoreItems.Values
                .FirstOrDefault(item =>
                    item.DefName.Equals(cleanItemName, StringComparison.OrdinalIgnoreCase) ||
                    item.CustomName?.Equals(cleanItemName, StringComparison.OrdinalIgnoreCase) == true);

            if (exactMatch != null)
            {
                Logger.Debug($"Found exact match: {exactMatch.DefName}");
                return exactMatch;
            }

            // Try partial match on thingDef label (case insensitive, whole word)
            var thingDef = DefDatabase<ThingDef>.AllDefs
                .FirstOrDefault(def =>
                    def.label != null &&
                    def.label.Equals(cleanItemName, StringComparison.OrdinalIgnoreCase));

            if (thingDef != null)
            {
                Logger.Debug($"Found via label exact match: {thingDef.defName}");
                return StoreInventory.GetStoreItem(thingDef.defName);
            }

            // Try label without spaces
            thingDef = DefDatabase<ThingDef>.AllDefs
                .FirstOrDefault(def =>
                {
                    if (def.label == null) return false;

                    string labelWithoutSpaces = def.label.Replace(" ", "");
                    return labelWithoutSpaces.Equals(cleanItemName.Replace(" ", ""), StringComparison.OrdinalIgnoreCase);
                });

            if (thingDef != null)
            {
                Logger.Debug($"Found via label without spaces: {thingDef.defName}");
                return StoreInventory.GetStoreItem(thingDef.defName);
            }

            // Try contains match as last resort, but only if we have at least 3 characters
            if (cleanItemName.Length >= 3)
            {
                thingDef = DefDatabase<ThingDef>.AllDefs
                    .FirstOrDefault(def => def.label?.ToLower().Contains(cleanItemName.ToLower()) == true);

                if (thingDef != null)
                {
                    Logger.Debug($"Found via contains match: {thingDef.defName}");
                    return StoreInventory.GetStoreItem(thingDef.defName);
                }
            }

            Logger.Debug($"No store item found for: '{cleanItemName}'");
            return null;
        }

        public static bool CanUserAfford(ChatMessageWrapper user, int price)
        {
            var viewer = Viewers.GetViewer(user);
            return viewer.Coins >= price;
        }

        // In StoreCommandHelper.cs - FIX HasRequiredResearch method
        public static bool HasRequiredResearch(StoreItem storeItem)
        {
            // Get settings from the mod instance
            var settings = CAPChatInteractiveMod.Instance?.Settings?.GlobalSettings;
            if (settings == null)
            {
                Logger.Debug($"HasRequiredResearch: No settings found, allowing purchase");
                return true;
            }

            // If research requirement is disabled, allow purchase
            if (!settings.RequireResearch)
            {
                Logger.Debug($"HasRequiredResearch: Research requirement disabled, allowing purchase");
                return true;
            }

            // Get the thing definition
            var thingDef = DefDatabase<ThingDef>.GetNamedSilentFail(storeItem.DefName);
            if (thingDef == null)
            {
                Logger.Debug($"HasRequiredResearch: ThingDef not found for {storeItem.DefName}, allowing purchase");
                return true;
            }

            // Check research prerequisites
            if (thingDef.researchPrerequisites != null && thingDef.researchPrerequisites.Count > 0)
            {
                foreach (var research in thingDef.researchPrerequisites)
                {
                    if (research != null && !research.IsFinished)
                    {
                        Logger.Debug($"HasRequiredResearch: Research prerequisite {research.defName} not completed for {storeItem.DefName}");
                        return false;
                    }
                }
            }

            // Also check recipe prerequisites if this is a building or complex item
            if (thingDef.recipeMaker != null && thingDef.recipeMaker.researchPrerequisite != null)
            {
                if (!thingDef.recipeMaker.researchPrerequisite.IsFinished)
                {
                    Logger.Debug($"HasRequiredResearch: Recipe research prerequisite {thingDef.recipeMaker.researchPrerequisite.defName} not completed for {storeItem.DefName}");
                    return false;
                }
            }

            Logger.Debug($"HasRequiredResearch: All research prerequisites met for {storeItem.DefName}");
            return true;
        }

        public static QualityCategory? ParseQuality(string qualityStr)
        {
            if (string.IsNullOrEmpty(qualityStr) || qualityStr.Equals("random", StringComparison.OrdinalIgnoreCase))
                return null;

            return qualityStr.ToLower() switch
            {
                "awful" => QualityCategory.Awful,
                "poor" => QualityCategory.Poor,
                "normal" => QualityCategory.Normal,
                "good" => QualityCategory.Good,
                "excellent" => QualityCategory.Excellent,
                "masterwork" => QualityCategory.Masterwork,
                "legendary" => QualityCategory.Legendary,
                _ => null
            };
        }

        // In StoreCommandHelper.cs - FIX IsQualityAllowed method
        public static bool IsQualityAllowed(QualityCategory? quality)
        {
            if (!quality.HasValue)
            {
                Logger.Debug($"IsQualityAllowed: No quality specified, allowing");
                return true;
            }

            // Get settings from the mod instance
            var settings = CAPChatInteractiveMod.Instance?.Settings?.GlobalSettings;
            if (settings == null)
            {
                Logger.Debug($"IsQualityAllowed: No settings found, allowing quality {quality.Value}");
                return true;
            }

            bool isAllowed = quality.Value switch
            {
                QualityCategory.Awful => settings.AllowAwfulQuality,
                QualityCategory.Poor => settings.AllowPoorQuality,
                QualityCategory.Normal => settings.AllowNormalQuality,
                QualityCategory.Good => settings.AllowGoodQuality,
                QualityCategory.Excellent => settings.AllowExcellentQuality,
                QualityCategory.Masterwork => settings.AllowMasterworkQuality,
                QualityCategory.Legendary => settings.AllowLegendaryQuality,
                _ => true
            };

            Logger.Debug($"IsQualityAllowed: Quality {quality.Value} - Allowed: {isAllowed}");
            return isAllowed;
        }

        public static ThingDef ParseMaterial(string materialStr, ThingDef thingDef)
        {
            if (string.IsNullOrEmpty(materialStr) || materialStr.Equals("random", StringComparison.OrdinalIgnoreCase))
                return null;

            // If the thing doesn't use materials, return null
            if (!thingDef.MadeFromStuff)
                return null;

            // Try to find the material def
            var materialDef = DefDatabase<ThingDef>.AllDefs
                .FirstOrDefault(def => def.IsStuff &&
                    (def.defName.Equals(materialStr, StringComparison.OrdinalIgnoreCase) ||
                     def.label?.Equals(materialStr, StringComparison.OrdinalIgnoreCase) == true));

            // Check if this material can be used for the thing
            if (materialDef != null && thingDef.stuffCategories != null)
            {
                foreach (var stuffCategory in thingDef.stuffCategories)
                {
                    if (materialDef.stuffProps?.categories?.Contains(stuffCategory) == true)
                        return materialDef;
                }
            }

            return null;
        }

        public static int CalculateFinalPrice(StoreItem storeItem, int quantity, QualityCategory? quality, ThingDef material)
        {
            try
            {
                // Get the thing def for accurate price calculation
                var thingDef = DefDatabase<ThingDef>.GetNamedSilentFail(storeItem.DefName);
                if (thingDef == null)
                {
                    Logger.Error($"ThingDef not found for store item: {storeItem.DefName}");
                    return storeItem.BasePrice * quantity; // Fallback
                }

                // Start with the base market value from storeItem  
                float baseCost = storeItem.BasePrice;

                Logger.Debug($"Base market value for {thingDef.defName}: {baseCost}");

                // Apply material cost if it's a stuff-based item and material is specified
                if (thingDef.MadeFromStuff && material != null)
                {
                    // RimWorld's formula: baseCost * (stuffMarketValue / defaultStuffMarketValue)
                    float stuffCost = material.BaseMarketValue;
                    float materialMultiplier = stuffCost;

                    // Apply the material multiplier to base cost
                    baseCost *= materialMultiplier;

                    Logger.Debug($"Material cost: {material.defName} ({stuffCost}) = multiplier {materialMultiplier:F2}");
                }

                // Apply quality multiplier if the item supports quality
                if (quality.HasValue && thingDef.HasComp(typeof(CompQuality)))
                {
                    float qualityMultiplier = GetQualityMultiplier(quality.Value);
                    baseCost *= qualityMultiplier;
                    Logger.Debug($"Quality multiplier for {quality.Value}: {qualityMultiplier}");
                }

                // Apply quantity and round to whole number
                int finalPrice = (int)(baseCost * quantity);

                Logger.Debug($"Final price for {quantity}x {thingDef.defName}: {finalPrice} (Base: {thingDef.BaseMarketValue}, Quality: {quality}, Material: {material?.defName})");

                return Math.Max(1, finalPrice); // Ensure at least 1 coin
            }
            catch (Exception ex)
            {
                Logger.Error($"Error calculating final price for {storeItem.DefName}: {ex}");
                // Fallback to simple calculation
                return storeItem.BasePrice * quantity;
            }
        }

        private static float GetQualityMultiplier(QualityCategory quality)
        {
            // Get settings from the mod instance
            var settings = CAPChatInteractiveMod.Instance?.Settings?.GlobalSettings;
            if (settings == null)
            {
                Logger.Debug($"GetQualityMultiplier: No settings found, using default values");
                // Fallback to default values if settings aren't available
                return quality switch
                {
                    QualityCategory.Awful => 0.5f,
                    QualityCategory.Poor => 0.75f,
                    QualityCategory.Normal => 1.0f,
                    QualityCategory.Good => 1.5f,
                    QualityCategory.Excellent => 2.0f,
                    QualityCategory.Masterwork => 3.0f,
                    QualityCategory.Legendary => 5.0f,
                    _ => 1.0f
                };
            }

            // Use the configurable settings from GlobalSettings
            return quality switch
            {
                QualityCategory.Awful => settings.AwfulQuality,
                QualityCategory.Poor => settings.PoorQuality,
                QualityCategory.Normal => settings.NormalQuality,
                QualityCategory.Good => settings.GoodQuality,
                QualityCategory.Excellent => settings.ExcellentQuality,
                QualityCategory.Masterwork => settings.MasterworkQuality,
                QualityCategory.Legendary => settings.LegendaryQuality,
                _ => 1.0f
            };
        }

        public static Pawn GetViewerPawn(string username)
        {
            var assignmentManager = CAPChatInteractiveMod.GetPawnAssignmentManager();
            if (assignmentManager != null && assignmentManager.HasAssignedPawn(username))
            {
                return assignmentManager.GetAssignedPawn(username);
            }
            return null;
        }

        public static Pawn GetViewerPawn(ChatMessageWrapper user)
        {
            try
            {
                var assignmentManager = CAPChatInteractiveMod.GetPawnAssignmentManager();
                if (assignmentManager != null && assignmentManager.HasAssignedPawn(user))
                {
                    return assignmentManager.GetAssignedPawn(user);
                }
                return null;
            }
            catch (Exception ex)
            {
                Logger.Error($"Error getting viewer pawn for {user.Username}: {ex}");
                return null;
            }
        }

        public static (List<Thing> spawnedThings, IntVec3 deliveryPos) SpawnItemForPawn(ThingDef thingDef, int quantity, QualityCategory? quality,
            ThingDef material, Pawn pawn, bool addToInventory = false)
        {
            return SpawnItemForPawn(thingDef, quantity, quality, material, pawn, addToInventory, false, false);
        }
        public static (List<Thing> spawnedThings, IntVec3 deliveryPos) SpawnItemForPawn(ThingDef thingDef, int quantity, QualityCategory? quality,
            ThingDef material, Pawn pawn, bool addToInventory, bool equipItem, bool wearItem)
        {
            List<Thing> spawnedThings = new List<Thing>();
            IntVec3 deliveryPos = IntVec3.Invalid;

            try
            {
                Logger.Debug($"Spawning item: {thingDef.defName}, quantity: {quantity}, for pawn: {pawn?.Name}, addToInventory: {addToInventory}, equipItem: {equipItem}, wearItem: {wearItem}");

                // Handle stuff materials - if the item requires stuff but no material was specified, get a random valid material
                ThingDef finalMaterial = material;
                if (thingDef.MadeFromStuff && finalMaterial == null)
                {
                    finalMaterial = GenStuff.RandomStuffFor(thingDef);
                    Logger.Debug($"Item requires stuff, selected random material: {finalMaterial?.defName}");
                }

                // SPECIAL CASE: If this is a pawn (animal), use pawn delivery regardless of other parameters
                if (thingDef.thingClass == typeof(Verse.Pawn) || thingDef.race != null)
                {
                    Logger.Debug($"Using special pawn delivery for {thingDef.defName} (IsPawn: {thingDef.thingClass == typeof(Verse.Pawn)}, HasRace: {thingDef.race != null})");

                    Map targetMap = pawn?.Map ?? Find.CurrentMap ?? Find.Maps.FirstOrDefault(m => m.IsPlayerHome);
                    if (targetMap == null)
                    {
                        Logger.Error("No valid map found for pawn delivery");
                        return (spawnedThings, deliveryPos);
                    }

                    if (!TryFindSafeDropPosition(targetMap, out deliveryPos))
                    {
                        Logger.Error("No safe drop position found for pawn delivery");
                        return (spawnedThings, deliveryPos);
                    }

                    // Get the actual spawn position from the pawn delivery
                    var pawnDeliveryResult = TryPawnDelivery(thingDef, quantity, quality, material, deliveryPos, targetMap, pawn);
                    if (pawnDeliveryResult.success)
                    {
                        // Use the actual spawn position instead of the trade spot
                        deliveryPos = pawnDeliveryResult.spawnPosition;
                        Logger.Debug($"Updated delivery position to actual spawn position: {deliveryPos}");
                    }

                    return (spawnedThings, deliveryPos);
                }

                // Create the thing
                Thing thing = ThingMaker.MakeThing(thingDef, finalMaterial);
                thing.stackCount = Math.Min(quantity, thingDef.stackLimit);
                Logger.Debug($"Created thing: {thingDef.defName} with stack count: {thing.stackCount}");

                // Set quality if applicable and the item supports quality
                if (quality.HasValue && thingDef.HasComp(typeof(CompQuality)))
                {
                    if (thing.TryGetQuality(out QualityCategory existingQuality))
                    {
                        thing.TryGetComp<CompQuality>()?.SetQuality(quality.Value, ArtGenerationContext.Outsider);
                    }
                }

                // Handle different delivery methods
                if (equipItem && pawn != null && pawn.Map != null)
                {
                    if (EquipItemOnPawn(thing, pawn))
                    {
                        Logger.Debug($"Item equipped on pawn");
                        // BuyItemCommandHandler.TrySetItemOwnership(thing, pawn); // Set ownership here
                        spawnedThings.Add(thing);
                        deliveryPos = pawn.Position; // Use pawn position for targeting
                        return (spawnedThings, deliveryPos);
                    }
                    else
                    {
                        Logger.Debug($"Failed to equip item, falling back to backpack");
                        // Fall back to backpack/inventory
                        // BuyItemCommandHandler.TrySetItemOwnership(thing, pawn); // Set ownership here
                        if (!TryBackpackItem(thing, pawn))
                        {
                            PurchaseHelper.SpawnItemAtTradeSpot(thing, pawn.Map);
                        }
                        spawnedThings.Add(thing);
                        deliveryPos = pawn.Position; // Use pawn position for targeting
                    }
                }
                else if (wearItem && pawn != null && pawn.Map != null)
                {
                    if (WearApparelOnPawn(thing, pawn))
                    {
                        Logger.Debug($"Item worn by pawn");
                        // BuyItemCommandHandler.TrySetItemOwnership(thing, pawn); // Set ownership here
                        spawnedThings.Add(thing);
                        deliveryPos = pawn.Position; // Use pawn position for targeting
                        return (spawnedThings, deliveryPos);
                    }
                    else
                    {
                        Logger.Debug($"Failed to wear item, falling back to backpack");
                        // Fall back to backpack/inventory

                        if (!TryBackpackItem(thing, pawn))
                        {
                            PurchaseHelper.SpawnItemAtTradeSpot(thing, pawn.Map);
                        }
                        // BuyItemCommandHandler.TrySetItemOwnership(thing, pawn); // Set ownership here
                        spawnedThings.Add(thing);
                        deliveryPos = pawn.Position; // Use pawn position for targeting
                    }
                }
                else if (addToInventory && pawn != null && pawn.Map != null)
                {
                    // Add to pawn's inventory
                    if (!pawn.inventory.innerContainer.TryAdd(thing))
                    {
                        // If inventory full, drop at pawn's position
                        Logger.Debug($"Inventory full, dropping at pawn position");
                        GenPlace.TryPlaceThing(thing, pawn.Position, pawn.Map, ThingPlaceMode.Near);
                        // BuyItemCommandHandler.TrySetItemOwnership(thing, pawn); // Set ownership here
                        deliveryPos = pawn.Position; // Use pawn position for targeting
                    }
                    else
                    {
                        Logger.Debug($"Item added to pawn inventory");
                        deliveryPos = pawn.Position; // Use pawn position for targeting
                    }
                    spawnedThings.Add(thing);
                }
                else
                {
                    // For drop pod deliveries, we need to handle multiple stacks differently
                    spawnedThings = CreateThingsForDelivery(thingDef, quantity, quality, finalMaterial);

                    // IMPROVED DELIVERY - Prioritize trade spots, then pawn proximity
                    Map targetMap = null;

                    if (pawn != null && pawn.Map != null)
                    {
                        targetMap = pawn.Map;

                        // Get the trade spot once
                        IntVec3 tradeSpot = DropCellFinder.TradeDropSpot(targetMap);

                        // First try to find drop spot near trade spot (highest priority)
                        if (DropCellFinder.TryFindDropSpotNear(tradeSpot, targetMap, out deliveryPos,
                            allowFogged: false, canRoofPunch: true, maxRadius: 15))
                        {
                            Logger.Debug($"Using drop spot near trade spot: {deliveryPos} (trade spot: {tradeSpot})");
                        }
                        // If no trade spot available, try near pawn
                        else if (DropCellFinder.TryFindDropSpotNear(pawn.Position, targetMap, out deliveryPos,
                                 allowFogged: false, canRoofPunch: true, maxRadius: 15))
                        {
                            Logger.Debug($"Using position near pawn for delivery: {deliveryPos} (pawn: {pawn.Position})");
                        }
                        else
                        {
                            // Fallback to trade spot directly
                            deliveryPos = tradeSpot;
                            Logger.Debug($"Using trade spot directly as fallback: {deliveryPos}");
                        }
                    }
                    else
                    {
                        targetMap = Find.CurrentMap ?? Find.Maps.FirstOrDefault(m => m.IsPlayerHome);
                        if (targetMap == null)
                        {
                            Logger.Error("No valid map found for item delivery");
                            // Try any player map with spawned colonists as last resort
                            targetMap = Find.Maps.FirstOrDefault(m => m.IsPlayerHome && m.mapPawns.ColonistsSpawnedCount > 0);
                            if (targetMap == null)
                            {
                                Logger.Error("No player home map with spawned colonists found for item delivery");
                                return (spawnedThings, deliveryPos);
                            }
                        }

                        Logger.Debug($"Selected map for delivery: {targetMap.info?.parent?.Label ?? "Unknown"} (Size: {targetMap.Size})");

                        // For colony-wide delivery, find a safe drop position
                        if (!TryFindSafeDropPosition(targetMap, out deliveryPos))
                        {
                            Logger.Error("No safe drop position found on map");
                            return (spawnedThings, deliveryPos);
                        }

                        Logger.Debug($"Using safe drop position for colony delivery: {deliveryPos}");
                    }

                    Logger.Debug($"Delivering {quantity}x {thingDef.defName} at position {deliveryPos} on map {targetMap}");

                    // Validate the delivery position
                    if (!IsValidDeliveryPosition(deliveryPos, targetMap))
                    {
                        Logger.Warning($"Invalid delivery position {deliveryPos}, finding alternative...");

                        // Try to find any valid position on the map
                        if (CellFinderLoose.TryFindRandomNotEdgeCellWith(10, (IntVec3 c) =>
                            c.InBounds(targetMap) && !c.Fogged(targetMap) && c.Standable(targetMap),
                            targetMap, out IntVec3 altPos))
                        {
                            deliveryPos = altPos;
                            Logger.Debug($"Using alternative delivery position: {deliveryPos}");
                        }
                        else
                        {
                            Logger.Error("No valid delivery position found on map");
                            return (spawnedThings, deliveryPos);
                        }
                    }

                    // Use shuttle delivery if available, otherwise use drop pods
                    if (TryShuttleDelivery(spawnedThings, deliveryPos, targetMap))
                    {
                        Logger.Debug($"Items delivered via shuttle/drop pod");
                    }
                    else
                    {
                        // Fallback to drop pod delivery
                        DeliverItemsInDropPods(spawnedThings, deliveryPos, targetMap);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error spawning item for pawn: {ex}");
                throw;
            }
            return (spawnedThings, deliveryPos);
        }

        // New helper method to create things for delivery
        private static List<Thing> CreateThingsForDelivery(ThingDef thingDef, int quantity, QualityCategory? quality, ThingDef material)
        {
            List<Thing> things = new List<Thing>();
            int remainingQuantity = quantity;

            // Check if this item should be minified
            bool shouldMinify = ShouldMinifyForDelivery(thingDef);

            while (remainingQuantity > 0)
            {
                Thing thing;

                if (shouldMinify)
                {
                    // For minified items, deliver one at a time
                    thing = CreateMinifiedThing(thingDef, quality, material);
                    remainingQuantity -= 1;
                }
                else
                {
                    // For regular items, use normal stack logic
                    int stackSize = Math.Min(remainingQuantity, thingDef.stackLimit);
                    thing = ThingMaker.MakeThing(thingDef, material);
                    thing.stackCount = stackSize;

                    // Set quality if applicable
                    if (quality.HasValue && thingDef.HasComp(typeof(CompQuality)))
                    {
                        if (thing.TryGetQuality(out QualityCategory existingQuality))
                        {
                            thing.TryGetComp<CompQuality>()?.SetQuality(quality.Value, ArtGenerationContext.Outsider);
                        }
                    }

                    remainingQuantity -= stackSize;
                }

                things.Add(thing);
            }

            return things;
        }

        // Updated shuttle delivery to accept pre-created things
        private static bool TryShuttleDelivery(List<Thing> thingsToDeliver, IntVec3 dropPos, Map map)
        {
            try
            {
                Logger.Debug($"Attempting delivery at position: {dropPos}, map: {map?.info?.parent?.Label ?? "null"}, map size: {map?.Size}, in bounds: {dropPos.InBounds(map)}");

                if (map == null)
                {
                    Logger.Error("Map is null for delivery");
                    return false;
                }

                if (!dropPos.InBounds(map))
                {
                    Logger.Error($"Delivery position {dropPos} is out of map bounds (map size: {map.Size})");
                    return false;
                }

                Logger.Debug($"Calling DropPodUtility.DropThingsNear with {thingsToDeliver.Count} stacks at position {dropPos}");
                LogDropPodDetails(thingsToDeliver, dropPos, map);

                // Use DropPodUtility which automatically handles both shuttles and drop pods
                // IMPORTANT: Set instigator to null to prevent automatic letter generation
                DropPodUtility.DropThingsNear(
                    dropPos,
                    map,
                    thingsToDeliver,
                    // instigator: null, // This prevents the automatic "Cargo pod crash" letter
                    openDelay: 110,
                    leaveSlag: false,
                    canRoofPunch: true,
                    forbid: false,
                    allowFogged: false
                );

                Logger.Debug($"Successfully called DropPodUtility for {thingsToDeliver.Count} items at {dropPos}");
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error($"Error in delivery at position {dropPos}: {ex}");
                return false;
            }
        }

        private static void LogDropPodDetails(List<Thing> thingsToDeliver, IntVec3 dropPos, Map map)
        {
            Logger.Debug($"=== DROP POD DELIVERY DEBUG ===");
            Logger.Debug($"Position: {dropPos}");
            Logger.Debug($"Map: {map?.info?.parent?.Label ?? "null"}");
            Logger.Debug($"Things to deliver: {thingsToDeliver.Count}");
            foreach (var thing in thingsToDeliver)
            {
                Logger.Debug($"  - {thing.def.defName} x{thing.stackCount}");
            }
            Logger.Debug($"Position valid: {IsValidDeliveryPosition(dropPos, map)}");
            Logger.Debug($"Position in bounds: {dropPos.InBounds(map)}");
            Logger.Debug($"Position fogged: {dropPos.Fogged(map)}");
            Logger.Debug($"Position standable: {dropPos.Standable(map)}");
            Logger.Debug($"=== END DEBUG ===");
        }

        private static void DeliverItemsInDropPods(List<Thing> thingsToDeliver, IntVec3 dropPos, Map map)
        {
            // Just call the shuttle delivery method as a fallback
            TryShuttleDelivery(thingsToDeliver, dropPos, map);
        }

        private static bool TryFindSafeDropPosition(Map map, out IntVec3 dropPos)
        {
            dropPos = IntVec3.Invalid;

            if (map == null)
                return false;

            // First try trade spot
            IntVec3 tradeSpot = DropCellFinder.TradeDropSpot(map);
            if (IsValidDeliveryPosition(tradeSpot, map))
            {
                dropPos = tradeSpot;
                Logger.Debug($"Using valid trade spot: {tradeSpot}");
                return true;
            }

            // Try near trade spot
            if (DropCellFinder.TryFindDropSpotNear(tradeSpot, map, out dropPos,
                allowFogged: false, canRoofPunch: true, maxRadius: 30))
            {
                if (IsValidDeliveryPosition(dropPos, map))
                {
                    Logger.Debug($"Using position near trade spot: {dropPos} (trade spot: {tradeSpot})");
                    return true;
                }
            }

            // Try map center
            IntVec3 mapCenter = map.Center;
            if (IsValidDeliveryPosition(mapCenter, map))
            {
                dropPos = mapCenter;
                Logger.Debug($"Using map center: {mapCenter}");
                return true;
            }

            // Try near map center
            if (DropCellFinder.TryFindDropSpotNear(mapCenter, map, out dropPos,
                allowFogged: false, canRoofPunch: true, maxRadius: 50))
            {
                if (IsValidDeliveryPosition(dropPos, map))
                {
                    Logger.Debug($"Using position near map center: {dropPos} (center: {mapCenter})");
                    return true;
                }
            }

            // Final fallback: find any valid cell on the map
            if (CellFinderLoose.TryFindRandomNotEdgeCellWith(10,
                (IntVec3 c) => IsValidDeliveryPosition(c, map),
                map, out dropPos))
            {
                Logger.Debug($"Using random valid cell: {dropPos}");
                return true;
            }

            Logger.Error("No safe drop position found after all attempts");
            return false;
        }

        private static Thing CreateMinifiedThing(ThingDef thingDef, QualityCategory? quality, ThingDef material)
        {
            try
            {
                // Create the original thing first
                Thing originalThing = ThingMaker.MakeThing(thingDef, material);

                // Set quality if applicable
                if (quality.HasValue && thingDef.HasComp(typeof(CompQuality)))
                {
                    if (originalThing.TryGetQuality(out QualityCategory existingQuality))
                    {
                        originalThing.TryGetComp<CompQuality>()?.SetQuality(quality.Value, ArtGenerationContext.Outsider);
                    }
                }

                // Minify the thing
                Thing minifiedThing = MinifyUtility.TryMakeMinified(originalThing);

                if (minifiedThing != null)
                {
                    Logger.Debug($"Successfully minified {thingDef.defName}");
                    return minifiedThing;
                }
                else
                {
                    Logger.Debug($"Minification returned null for {thingDef.defName}, returning original");
                    return originalThing;
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error minifying {thingDef.defName}: {ex}");
                // Return regular thing as fallback
                return ThingMaker.MakeThing(thingDef, material);
            }
        }

        private static bool IsValidDeliveryPosition(IntVec3 pos, Map map)
        {
            if (map == null)
            {
                Logger.Debug("Map is null");
                return false;
            }

            if (!pos.InBounds(map))
            {
                Logger.Debug($"Position {pos} is out of map bounds (map size: {map.Size})");
                return false;
            }

            if (pos.Fogged(map))
            {
                Logger.Debug($"Position {pos} is fogged");
                return false;
            }

            // Check if position is standable or can be roof-punched
            if (!pos.Standable(map))
            {
                // Check if we can place on this cell (non-standable but passable)
                if (!GenGrid.Walkable(pos, map))
                {
                    Logger.Debug($"Position {pos} is not walkable or standable");
                    return false;
                }
            }

            // Additional check: ensure it's not in a solid rock wall
            Building edifice = pos.GetEdifice(map);
            if (edifice != null && edifice.def.passability == Traversability.Impassable && edifice.def.building.isNaturalRock)
            {
                Logger.Debug($"Position {pos} is in solid rock");
                return false;
            }

            Logger.Debug($"Position {pos} is valid for delivery");
            return true;
        }

        private static bool ShouldMinifyForDelivery(ThingDef thingDef)
        {
            if (thingDef == null) return false;

            // Only check if the thing can be minified - that's the main requirement
            if (!thingDef.Minifiable)
            {
                Logger.Debug($"{thingDef.defName} is not minifiable");
                return false;
            }

            Logger.Debug($"{thingDef.defName} should be minified for delivery");
            return true;
        }

        public static bool EquipItemOnPawn(Thing item, Verse.Pawn pawn)
        {
            try
            {
                if (pawn == null || item == null) return false;

                // Check if it's a weapon
                if (item.def.IsWeapon)
                {
                    var weapon = item as ThingWithComps;
                    if (weapon != null)
                    {
                        // Check if pawn can equip this weapon
                        if (!EquipmentUtility.CanEquip(weapon, pawn))
                        {
                            Logger.Debug($"Pawn cannot equip {weapon.def.defName}");
                            return false;
                        }

                        // Check if pawn can carry anything
                        if (!MassUtility.CanEverCarryAnything(pawn))
                        {
                            Logger.Debug($"Pawn cannot carry anything");
                            return false;
                        }

                        ThingWithComps oldWeapon = null;

                        // Try to handle current equipment
                        if (pawn.equipment.Primary != null)
                        {
                            // Try to move current weapon to inventory
                            if (!pawn.equipment.TryTransferEquipmentToContainer(pawn.equipment.Primary, pawn.inventory.innerContainer))
                            {
                                // If inventory full, try to drop it
                                if (!pawn.equipment.TryDropEquipment(pawn.equipment.Primary, out oldWeapon, pawn.Position))
                                {
                                    Logger.Warning($"Could not make room for {pawn.Name}'s new weapon.");
                                }
                            }
                        }

                        // Check if pawn would be over encumbered
                        if (MassUtility.WillBeOverEncumberedAfterPickingUp(pawn, weapon, 1) && oldWeapon != null)
                        {
                            // Re-equip old weapon and spawn new one
                            pawn.equipment.AddEquipment(oldWeapon);
                            Logger.Debug($"Pawn would be over encumbered, spawning weapon instead");
                            return false;
                        }

                        // Equip the new weapon
                        pawn.equipment.AddEquipment(weapon);
                        Logger.Debug($"Equipped weapon: {item.def.defName} on pawn {pawn.Name}");
                        return true;
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                Logger.Error($"Error equipping item on pawn: {ex}");
                return false;
            }
        }

        public static bool WearApparelOnPawn(Thing item, Verse.Pawn pawn)
        {
            try
            {
                if (pawn == null || item == null) return false;

                // Check if it's apparel
                if (item.def.IsApparel)
                {
                    var apparel = item as Apparel;
                    if (apparel != null)
                    {
                        // Check if pawn has body parts to wear this apparel
                        if (!ApparelUtility.HasPartsToWear(pawn, item.def))
                        {
                            Logger.Debug($"Pawn lacks body parts to wear {item.def.defName}");
                            return false;
                        }

                        // Check if this would replace locked apparel
                        if (pawn.apparel.WouldReplaceLockedApparel(apparel))
                        {
                            Logger.Debug($"Would replace locked apparel with {item.def.defName}");
                            return false;
                        }

                        // Check if pawn can equip this apparel
                        if (!EquipmentUtility.CanEquip(apparel, pawn))
                        {
                            Logger.Debug($"Pawn cannot equip {item.def.defName}");
                            return false;
                        }

                        // Wear the apparel and force it to be worn
                        pawn.apparel.Wear(apparel, dropReplacedApparel: true);
                        pawn.outfits.forcedHandler.SetForced(apparel, true);
                        Logger.Debug($"Wore apparel: {item.def.defName} on pawn {pawn.Name}");
                        return true;
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                Logger.Error($"Error wearing apparel on pawn: {ex}");
                return false;
            }
        }

        public static bool IsItemTypeValid(StoreItem storeItem, bool requireEquippable, bool requireWearable, bool requireUsable)
        {
            if (requireEquippable && !storeItem.IsEquippable)
                return false;

            if (requireWearable && !storeItem.IsWearable)
                return false;

            if (requireUsable && !storeItem.IsUsable)
                return false;

            return true;
        }

        public static string GetItemTypeDescription(StoreItem storeItem)
        {
            // FIX: Use the correct StoreItem properties that users can toggle
            if (storeItem.IsEquippable) return "equippable";
            if (storeItem.IsWearable) return "wearable";
            if (storeItem.IsUsable) return "usable";
            return "item";
        }

        public static string FormatCurrencyMessage(int amount, string currencySymbol)
        {
            var settings = CAPChatInteractiveMod.Instance.Settings.GlobalSettings;
            return $"{amount} {currencySymbol}";
        }

        private static bool TryBackpackItem(Thing thing, Verse.Pawn pawn)
        {
            if (pawn.inventory.innerContainer.TryAdd(thing))
            {
                Logger.Debug($"Item added to pawn inventory");
                return true;
            }
            return false;
        }

        public static bool IsItemSuitableForSurgery(StoreItem storeItem)
        {
            var thingDef = DefDatabase<ThingDef>.GetNamedSilentFail(storeItem.DefName);
            if (thingDef == null) return false;

            // Check if this is an implant, bionic part, or other surgical item
            if (thingDef.isTechHediff) return true;
            if (thingDef.defName.Contains("Bionic") || thingDef.defName.Contains("Prosthetic")) return true;
            if (thingDef.defName.Contains("Implant")) return true;

            // Check if there are any surgery recipes that use this item
            return DefDatabase<RecipeDef>.AllDefs
                .Where(r => r.IsSurgery)
                .Any(r => r.ingredients.Any(i => i.filter.AllowedThingDefs.Contains(thingDef)));
        }

        public static bool IsRaceBanned(ThingDef thingDef)
        {
            if (thingDef?.race == null)
                return false;

            // Ban humanlike races
            if (thingDef.race.Humanlike)
            {
                Logger.Debug($"Banned race detected: {thingDef.defName} (Humanlike)");
                return true;
            }

            // Add other banned race conditions here if needed
            string[] bannedRaces = {
        "Human", "Colonist", "Slave", "Refugee", "Prisoner",
        "Spacer", "Tribal", "Pirate", "Outlander", "Villager"
    };

            if (bannedRaces.Any(race => thingDef.defName.Contains(race) ||
                                       (thingDef.label?.Contains(race) == true)))
            {
                Logger.Debug($"Banned race detected: {thingDef.defName}");
                return true;
            }

            return false;
        }

        public static bool IsRaceBannedByName(string itemName)
        {
            if (string.IsNullOrWhiteSpace(itemName))
                return false;

            // Clean the item name first (using the same logic as GetStoreItemByName)
            string cleanItemName = itemName.Trim();
            cleanItemName = cleanItemName.TrimEnd('(', '[', '{').TrimStart(')', ']', '}').Trim();

            // Try to find if this matches any humanlike race
            var raceDef = RaceUtils.FindRaceByName(cleanItemName);
            if (raceDef != null)
            {
                Logger.Debug($"Banned race detected by name: '{cleanItemName}' -> {raceDef.defName}");
                return true;
            }

            return false;
        }

        // In StoreCommandHelper.cs - Update the TryPawnDelivery method
        private static (bool success, IntVec3 spawnPosition) TryPawnDelivery(ThingDef pawnDef, int quantity, QualityCategory? quality, ThingDef material, IntVec3 dropPos, Map map, Pawn viewerPawn = null)
        {
            IntVec3 spawnPosition = IntVec3.Invalid;

            try
            {
                Logger.Debug($"Attempting pawn delivery for {quantity}x {pawnDef.defName} at position: {dropPos}");

                if (map == null)
                {
                    Logger.Error("Map is null for pawn delivery");
                    return (false, spawnPosition);
                }

                if (!dropPos.InBounds(map))
                {
                    Logger.Error($"Pawn delivery position {dropPos} is out of map bounds");
                    return (false, spawnPosition);
                }

                List<Pawn> pawnsToDeliver = new List<Pawn>();

                for (int i = 0; i < quantity; i++)
                {
                    // Create pawn using RimWorld's proper pawn generation
                    PawnGenerationRequest request = new PawnGenerationRequest(
                        kind: pawnDef.race.AnyPawnKind,
                        faction: null, // Keep as null initially, we'll set faction after creation
                        context: PawnGenerationContext.NonPlayer,
                        tile: -1,
                        forceGenerateNewPawn: true,
                        allowDead: false,
                        allowDowned: false,
                        canGeneratePawnRelations: false,
                        mustBeCapableOfViolence: false,
                        colonistRelationChanceFactor: 0f,
                        forceAddFreeWarmLayerIfNeeded: false,
                        allowGay: true,
                        allowFood: true,
                        allowAddictions: true,
                        inhabitant: false,
                        certainlyBeenInCryptosleep: false,
                        forceRedressWorldPawnIfFormerColonist: false,
                        worldPawnFactionDoesntMatter: false,
                        biocodeWeaponChance: 0f,
                        biocodeApparelChance: 0f,
                        validatorPreGear: null,
                        validatorPostGear: null,
                        forcedTraits: null,
                        prohibitedTraits: null,
                        minChanceToRedressWorldPawn: 0f,
                        fixedBiologicalAge: null,
                        fixedChronologicalAge: null,
                        fixedGender: null,
                        fixedLastName: null,
                        fixedBirthName: null
                    );

                    Pawn pawn = PawnGenerator.GeneratePawn(request);

                    // CRITICAL FIX: Handle mechanoids by setting faction to player
                    if (pawn.RaceProps.IsMechanoid)
                    {
                        Logger.Debug($"Detected mechanoid: {pawn.def.defName}, setting faction to player");
                        pawn.SetFaction(Faction.OfPlayer);
                    }
                    // Also handle animals (keeping existing logic)
                    else if (pawn.RaceProps.Animal)
                    {
                        pawn.SetFaction(Faction.OfPlayer);
                        Logger.Debug($"Tamed animal: {pawn.Name}");
                    }

                    pawnsToDeliver.Add(pawn);
                    Logger.Debug($"Created pawn: {pawn.Name} ({pawn.def.defName}), Faction: {pawn.Faction?.Name ?? "null"}, IsMechanoid: {pawn.RaceProps.IsMechanoid}");
                }

                // Use a gentler delivery method for pawns - walk them in from the edge
                if (pawnsToDeliver.Count > 0)
                {
                    spawnPosition = FindPawnSpawnPosition(map, dropPos, viewerPawn);
                    Logger.Debug($"Spawning {pawnsToDeliver.Count} pawns at position: {spawnPosition}");

                    foreach (var pawn in pawnsToDeliver)
                    {
                        // Spawn the pawn properly
                        GenSpawn.Spawn(pawn, spawnPosition, map);

                        // Add some arrival effects
                        FleckMaker.ThrowDustPuff(spawnPosition, map, 2f);

                        // Debug logging
                        Logger.Debug($"Spawned pawn {pawn.Name} ({pawn.def.defName}) with faction: {pawn.Faction?.Name ?? "null"}");
                    }

                    Logger.Debug($"Successfully delivered {pawnsToDeliver.Count}x {pawnDef.defName} at {spawnPosition}");
                    return (true, spawnPosition);
                }

                return (false, spawnPosition);
            }
            catch (Exception ex)
            {
                Logger.Error($"Error in pawn delivery: {ex}");
                return (false, spawnPosition);
            }
        }
        private static IntVec3 FindPawnSpawnPosition(Map map, IntVec3 preferredPos, Pawn viewerPawn = null)
        {
            // First priority: try to spawn near the viewer's pawn if available
            if (viewerPawn != null && viewerPawn.Map == map && !viewerPawn.Dead)
            {
                if (CellFinder.TryFindRandomCellNear(viewerPawn.Position, map, 8,
                    (IntVec3 c) => c.Standable(map) && !c.Fogged(map) && c.Walkable(map) && c.GetRoom(map) == viewerPawn.GetRoom(),
                    out IntVec3 spawnPos))
                {
                    Logger.Debug($"Found spawn position near viewer pawn: {spawnPos}");
                    return spawnPos;
                }

                // If no room-appropriate position, try any nearby position
                if (CellFinder.TryFindRandomCellNear(viewerPawn.Position, map, 15,
                    (IntVec3 c) => c.Standable(map) && !c.Fogged(map) && c.Walkable(map),
                    out spawnPos))
                {
                    Logger.Debug($"Found nearby spawn position: {spawnPos}");
                    return spawnPos;
                }
            }

            // Second priority: try near preferred position (usually trade spot)
            if (CellFinder.TryFindRandomCellNear(preferredPos, map, 10,
                (IntVec3 c) => c.Standable(map) && !c.Fogged(map) && c.Walkable(map),
                out IntVec3 spawnPos2))
            {
                Logger.Debug($"Found spawn position near preferred position: {spawnPos2}");
                return spawnPos2;
            }

            // Fallback: find any valid cell on the map edge
            if (CellFinder.TryFindRandomEdgeCellWith(
                (IntVec3 c) => c.Standable(map) && !c.Fogged(map) && c.Walkable(map),
                map, CellFinder.EdgeRoadChance_Ignore, out spawnPos2))
            {
                Logger.Debug($"Found edge spawn position: {spawnPos2}");
                return spawnPos2;
            }

            // Final fallback: use the preferred position
            Logger.Debug($"Using preferred position as fallback: {preferredPos}");
            return preferredPos;
        }

        public static class PurchaseHelper
        {
            public static void SpawnItemAtTradeSpot(Thing thing, Map map)
            {
                IntVec3 dropPos = DropCellFinder.TradeDropSpot(map);
                Logger.Debug($"Dropping item at trade spot {dropPos}");
                GenDrop.TryDropSpawn(thing, dropPos, map, ThingPlaceMode.Near, out Thing resultingThing);
            }
        }
    }
}