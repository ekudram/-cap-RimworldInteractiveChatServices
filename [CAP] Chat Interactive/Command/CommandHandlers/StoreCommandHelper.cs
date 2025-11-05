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
            // Try exact match first
            var storeItem = StoreInventory.AllStoreItems.Values
                .FirstOrDefault(item => item.DefName.Equals(itemName, StringComparison.OrdinalIgnoreCase) ||
                                       item.CustomName?.Equals(itemName, StringComparison.OrdinalIgnoreCase) == true);

            if (storeItem != null) return storeItem;

            // Try partial match on label (with spaces)
            var thingDef = DefDatabase<ThingDef>.AllDefs
                .FirstOrDefault(def => def.label?.ToLower().Contains(itemName.ToLower()) == true);

            if (thingDef != null) return StoreInventory.GetStoreItem(thingDef.defName);

            // Try match on label without spaces
            thingDef = DefDatabase<ThingDef>.AllDefs
                .FirstOrDefault(def =>
                {
                    string labelWithoutSpaces = def.label?.Replace(" ", "") ?? "";
                    return labelWithoutSpaces.Equals(itemName, StringComparison.OrdinalIgnoreCase) ||
                           labelWithoutSpaces.Contains(itemName, StringComparison.OrdinalIgnoreCase);
                });

            return thingDef != null ? StoreInventory.GetStoreItem(thingDef.defName) : null;
        }

        public static bool CanUserAfford(ChatMessageWrapper user, int price)
        {
            var viewer = Viewers.GetViewer(user.Username);
            return viewer.Coins >= price;
        }

        public static bool HasRequiredResearch(StoreItem storeItem)
        {
            if (!Dialog_QualityResearchSettings.RequireResearch)
                return true;

            if (Dialog_QualityResearchSettings.AllowUnresearchedItems)
                return true;

            // Check if any research prerequisites are unmet
            var thingDef = DefDatabase<ThingDef>.GetNamedSilentFail(storeItem.DefName);
            if (thingDef?.researchPrerequisites == null)
                return true;

            foreach (var research in thingDef.researchPrerequisites)
            {
                if (!research.IsFinished)
                    return false;
            }

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

        public static bool IsQualityAllowed(QualityCategory? quality)
        {
            if (!quality.HasValue) return true;

            return quality.Value switch
            {
                QualityCategory.Awful => Dialog_QualityResearchSettings.AllowAwfulQuality,
                QualityCategory.Poor => Dialog_QualityResearchSettings.AllowPoorQuality,
                QualityCategory.Normal => Dialog_QualityResearchSettings.AllowNormalQuality,
                QualityCategory.Good => Dialog_QualityResearchSettings.AllowGoodQuality,
                QualityCategory.Excellent => Dialog_QualityResearchSettings.AllowExcellentQuality,
                QualityCategory.Masterwork => Dialog_QualityResearchSettings.AllowMasterworkQuality,
                QualityCategory.Legendary => Dialog_QualityResearchSettings.AllowLegendaryQuality,
                _ => true
            };
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
            int basePrice = storeItem.BasePrice * quantity;
            float multiplier = 1.0f;

            // Quality multiplier
            if (quality.HasValue)
            {
                multiplier *= quality.Value switch
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

            // Material multiplier (if specified and different from default)
            if (material != null)
            {
                multiplier *= (material.BaseMarketValue / 10f); // Adjust this factor as needed
            }

            return (int)(basePrice * multiplier);
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

        public static void SpawnItemForPawn(ThingDef thingDef, int quantity, QualityCategory? quality, ThingDef material, Pawn pawn, bool addToInventory = false)
        {
            SpawnItemForPawn(thingDef, quantity, quality, material, pawn, addToInventory, false, false);
        }

        public static void SpawnItemForPawn(ThingDef thingDef, int quantity, QualityCategory? quality, ThingDef material, Pawn pawn, bool addToInventory, bool equipItem, bool wearItem)
        {
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
                        return;
                    }
                    else
                    {
                        Logger.Debug($"Failed to equip item, falling back to backpack");
                        // Fall back to backpack/inventory
                        if (!TryBackpackItem(thing, pawn))
                        {
                            PurchaseHelper.SpawnItemAtTradeSpot(thing, pawn.Map);
                        }
                    }
                }
                else if (wearItem && pawn != null && pawn.Map != null)
                {
                    if (WearApparelOnPawn(thing, pawn))
                    {
                        Logger.Debug($"Item worn by pawn");
                        return;
                    }
                    else
                    {
                        Logger.Debug($"Failed to wear item, falling back to backpack");
                        // Fall back to backpack/inventory
                        if (!TryBackpackItem(thing, pawn))
                        {
                            PurchaseHelper.SpawnItemAtTradeSpot(thing, pawn.Map);
                        }
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
                    }
                    else
                    {
                        Logger.Debug($"Item added to pawn inventory");
                    }
                }
                else
                {
                    // IMPROVED DROP POD DELIVERY
                    Map targetMap = null;
                    IntVec3 dropPos;

                    if (pawn != null && pawn.Map != null)
                    {
                        targetMap = pawn.Map;
                        if (!DropCellFinder.TryFindDropSpotNear(pawn.Position, targetMap, out dropPos, allowFogged: false, canRoofPunch: true, maxRadius: 15))
                        {
                            dropPos = pawn.Position;
                        }
                    }
                    else
                    {
                        targetMap = Find.CurrentMap ?? Find.Maps.FirstOrDefault(m => m.IsPlayerHome);
                        if (targetMap == null)
                        {
                            Logger.Error("No valid map found for item delivery");
                            return;
                        }

                        if (!DropCellFinder.TryFindDropSpotNear(targetMap.Center, targetMap, out dropPos, allowFogged: false, canRoofPunch: true, maxRadius: 30))
                        {
                            dropPos = DropCellFinder.TradeDropSpot(targetMap);
                        }
                    }

                    Logger.Debug($"Dropping {quantity}x {thingDef.defName} at position {dropPos} on map {targetMap}");

                    // Use the improved delivery method for handling stack limits
                    DeliverItemsInDropPods(thingDef, quantity, quality, finalMaterial, dropPos, targetMap);
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error spawning item for pawn: {ex}");
                throw;
            }
        }

        private static void DeliverItemsInDropPods(ThingDef thingDef, int quantity, QualityCategory? quality, ThingDef material, IntVec3 dropPos, Map map)
        {
            try
            {
                List<Thing> thingsToDeliver = new List<Thing>();
                int remainingQuantity = quantity;

                while (remainingQuantity > 0)
                {
                    int stackSize = Math.Min(remainingQuantity, thingDef.stackLimit);
                    Thing thing = ThingMaker.MakeThing(thingDef, material);
                    thing.stackCount = stackSize;

                    // Set quality if applicable
                    if (quality.HasValue && thingDef.HasComp(typeof(CompQuality)))
                    {
                        if (thing.TryGetQuality(out QualityCategory existingQuality))
                        {
                            thing.TryGetComp<CompQuality>()?.SetQuality(quality.Value, ArtGenerationContext.Outsider);
                        }
                    }

                    thingsToDeliver.Add(thing);
                    remainingQuantity -= stackSize;
                }

                // Deliver all items in one drop pod
                DropPodUtility.DropThingsNear(
                    dropPos,
                    map,
                    thingsToDeliver,
                    openDelay: 110,
                    // instaDrop: false,
                    leaveSlag: false,
                    canRoofPunch: true,
                    forbid: true,
                    allowFogged: false
                );

                Logger.Debug($"Delivered {quantity}x {thingDef.defName} in {thingsToDeliver.Count} stacks");
            }
            catch (Exception ex)
            {
                Logger.Error($"Error delivering items in drop pods: {ex}");
                throw;
            }
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
            return $"{amount}{currencySymbol}";
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