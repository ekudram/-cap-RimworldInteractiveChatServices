// Filename: ItemDeliveryHelper.cs
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

using CAP_ChatInteractive;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using static _CAP__Chat_Interactive.Command.CommandHelpers.StoreCommandHelper;
using Logger = CAP_ChatInteractive.Logger;
namespace _CAP__Chat_Interactive.Command.CommandHelpers
{
    public class DeliveryResult
    {
        public List<Thing> LockerDeliveredItems { get; set; } = new List<Thing>();
        public List<Thing> DropPodDeliveredItems { get; set; } = new List<Thing>();
        public List<Thing> DirectlyDeliveredItems { get; set; } = new List<Thing>();
        public IntVec3 DeliveryPosition { get; set; }
        public DeliveryMethod PrimaryMethod { get; set; }
    }

    public enum DeliveryMethod
    {
        Locker,
        DropPod,
        Inventory,
        Equipped,
        Worn,
        PawnDelivery
    }
    public static class ItemDeliveryHelper
    {

        // Replace the FindSuitableLockerFor method with this more robust version
        public static Building_RimazonLocker FindSuitableLockerFor(Thing thing, Map map, Pawn forPawn = null)
        {
            try
            {
                if (map == null || thing == null)
                {
                    Logger.Debug("FindSuitableLocker: Map or thing is null");
                    return null;
                }

                if (thing.def.Minifiable)
                {
                    Logger.Debug($"{thing.def.defName} is minifiable - skipping locker delivery");
                    return null;
                }

                // Get all lockers on the map
                var allLockers = new List<Building_RimazonLocker>();

                // Try small locker def (exists)
                ThingDef smallLockerDef = DefDatabase<ThingDef>.GetNamedSilentFail("RimazonLockerSmall");
                if (smallLockerDef != null)
                {
                    var smallLockers = map.listerThings.ThingsOfDef(smallLockerDef)
                        .OfType<Building_RimazonLocker>();
                    allLockers.AddRange(smallLockers);
                    Logger.Debug($"Found {smallLockers.Count()} small lockers");
                }

                // Try large locker def (might not exist yet)
                ThingDef largeLockerDef = DefDatabase<ThingDef>.GetNamedSilentFail("RimazonLockerLarge");
                if (largeLockerDef != null)
                {
                    var largeLockers = map.listerThings.ThingsOfDef(largeLockerDef)
                        .OfType<Building_RimazonLocker>();
                    allLockers.AddRange(largeLockers);
                    Logger.Debug($"Found {largeLockers.Count()} large lockers");
                }

                if (!allLockers.Any())
                {
                    Logger.Debug($"No RimazonLockers found on map");
                    return null;
                }

                Logger.Debug($"Found {allLockers.Count} total lockers on map");

                // CRITICAL FIX: Check if the locker can accept this SPECIFIC thing
                // This considers merging with existing stacks
                var suitableLockers = allLockers
                    .Where(locker =>
                        locker.Spawned &&
                        locker.Map == map &&
                        !locker.Destroyed &&
                        locker.Accepts(thing)) // This is the key - uses the locker's own Accepts() logic
                    .ToList();

                if (!suitableLockers.Any())
                {
                    Logger.Debug($"No suitable RimazonLocker found for {thing.def.defName} x{thing.stackCount}");

                    // Log debug info for each locker
                    foreach (var locker in allLockers.Take(3)) // Just first 3 for brevity
                    {
                        Logger.Debug($"Locker at {locker.Position}: " +
                                   $"Spawned={locker.Spawned}, " +
                                   $"MapMatch={locker.Map == map}, " +
                                   $"Destroyed={locker.Destroyed}, " +
                                   $"Accepts={locker.Accepts(thing)}, " +
                                   $"CurrentItems={locker.InnerContainer.TotalStackCount}/{locker.MaxStacks}");
                    }

                    return null;
                }

                Logger.Debug($"Found {suitableLockers.Count} suitable lockers for {thing.def.defName}");

                // Selection priority:
                Building_RimazonLocker bestLocker = null;

                // 1. Try to find lockers that already have this item type (for stack merging)
                var lockersWithSameItem = suitableLockers
                    .Where(l => l.InnerContainer.Any(t => t.def == thing.def))
                    .ToList();

                if (lockersWithSameItem.Any())
                {
                    bestLocker = lockersWithSameItem
                        .OrderBy(l => l.Position.DistanceToSquared(forPawn?.Position ?? GetCustomDropSpot(map)))
                        .FirstOrDefault();

                    if (bestLocker != null)
                    {
                        Logger.Debug($"Selected locker with same item type at {bestLocker.Position} (for stack merging)");
                        return bestLocker;
                    }
                }

                // 2. Fall back to proximity
                IntVec3 targetPos = forPawn != null && forPawn.Spawned && forPawn.Map == map
                    ? forPawn.Position
                    : GetCustomDropSpot(map);

                bestLocker = suitableLockers
                    .OrderBy(locker => locker.Position.DistanceToSquared(targetPos))
                    .ThenByDescending(locker => locker.MaxStacks - locker.InnerContainer.TotalStackCount) // Most space left
                    .FirstOrDefault();

                if (bestLocker != null)
                {
                    Logger.Debug($"Selected locker at {bestLocker.Position} " +
                                $"(distance to target: {bestLocker.Position.DistanceTo(targetPos):F1}, " +
                                $"space left: {bestLocker.MaxStacks - bestLocker.InnerContainer.TotalStackCount})");
                }

                return bestLocker;
            }
            catch (Exception ex)
            {
                Logger.Error($"Error finding suitable locker: {ex}");
                return null;
            }
        }

        public static IntVec3 GetCustomDropSpot(Map map)
        {
            if (map == null)
            {
                Logger.Error("GetCustomDropSpot: Map is null");
                return IntVec3.Invalid;
            }

            // ───────────────────────────────────────────────
            // 1. Highest priority: explicitly named "drop spot" thing (case-insensitive)
            // ───────────────────────────────────────────────
            var allThings = map.listerThings.AllThings;
            var dropSpotThing = allThings
                .Where(t => t.Spawned && !t.Destroyed && t.Map == map)
                .FirstOrDefault(t =>
                {
                    string label = t.Label?.ToLowerInvariant();
                    return !string.IsNullOrWhiteSpace(label) && label.Contains("drop spot");
                });

            if (dropSpotThing != null)
            {
                Logger.Debug($"Using explicit 'drop spot' item '{dropSpotThing.Label}' at {dropSpotThing.Position}");
                return dropSpotThing.Position;
            }

            // ───────────────────────────────────────────────
            // 2. Orbital trade beacon — but only if outdoors (no roof)
            // ───────────────────────────────────────────────
            var tradeBeacons = map.listerBuildings.AllBuildingsColonistOfDef(ThingDefOf.OrbitalTradeBeacon)
                .Where(b => b.Spawned && !b.Destroyed && !map.roofGrid.Roofed(b.Position))
                .ToList();

            if (tradeBeacons.Any())
            {
                var beacon = tradeBeacons.First(); // or .OrderBy(b => some criteria).First()
                Logger.Debug($"Using roof-free Orbital Trade Beacon at {beacon.Position}");
                return beacon.Position;
            }

            // ───────────────────────────────────────────────
            // 3. Ship landing beacon (usually placed outside anyway)
            // ───────────────────────────────────────────────
            Building shipBeacon = map.listerBuildings.AllBuildingsColonistOfDef(ThingDefOf.ShipLandingBeacon).FirstOrDefault();
            if (shipBeacon != null)
            {
                Logger.Debug($"Using Ship Landing Beacon position: {shipBeacon.Position}");
                return shipBeacon.Position;
            }

            // ───────────────────────────────────────────────
            // 4. Caravan hitching / packing spot
            // ───────────────────────────────────────────────
            ThingDef hitchingDef = ThingDefOf.CaravanPackingSpot
                ?? DefDatabase<ThingDef>.GetNamedSilentFail("CaravanPackingSpot");

            if (hitchingDef != null)
            {
                Building hitchingSpot = map.listerBuildings.AllBuildingsColonistOfDef(hitchingDef).FirstOrDefault();
                if (hitchingSpot != null)
                {
                    Logger.Debug($"Using Caravan Hitching/Packing Spot at: {hitchingSpot.Position}");
                    return hitchingSpot.Position;
                }
            }
            else
            {
                Logger.Warning("Caravan Hitching/Packing Spot def not found");
            }

            // ───────────────────────────────────────────────
            // 5. Fallback: near average free colonist position
            // ───────────────────────────────────────────────
            var freeColonists = map.mapPawns.FreeColonistsSpawned;
            if (freeColonists.Count > 0)
            {
                IntVec3 average = IntVec3.Zero;
                foreach (var colonist in freeColonists)
                    average += colonist.Position;

                average /= freeColonists.Count;

                if (CellFinder.TryFindRandomCellNear(average, map, 20,
                    c => c.Standable(map) && !c.Fogged(map),
                    out IntVec3 spot))
                {
                    Logger.Debug($"Using central colonist area fallback: {spot}");
                    return spot;
                }
            }

            // ───────────────────────────────────────────────
            // 6. Underground map → try surface if possible
            // ───────────────────────────────────────────────
            if (IsUndergroundMap(map) && Find.Maps.Any(m => !IsUndergroundMap(m) && m.IsPlayerHome))
            {
                Map surface = Find.Maps.First(m => !IsUndergroundMap(m) && m.IsPlayerHome);
                Logger.Debug("Underground map detected → falling back to surface map drop spot");
                return GetCustomDropSpot(surface); // recursive, but surface won't redirect again
            }

            // ───────────────────────────────────────────────
            // 7. Absolute last resort: map center
            // ───────────────────────────────────────────────
            Logger.Warning("No good drop spot found → using map center");
            return map.Center;
        }

        public static bool IsUndergroundMap(Map map)
        {
            if (map == null) return false;

            int naturalThickRoofCount = 0;
            int totalCells = 0;

            foreach (IntVec3 cell in map.AllCells)
            {
                totalCells++;
                RoofDef roof = map.roofGrid.RoofAt(cell);
                if (roof != null && roof.isNatural && roof == RoofDefOf.RoofRockThick)
                {
                    naturalThickRoofCount++;
                }
            }

            if (totalCells == 0) return false;

            float thickNaturalPercentage = (float)naturalThickRoofCount / totalCells;

            Logger.Debug($"Thick natural overhead mountain roof: {thickNaturalPercentage:P2} ({naturalThickRoofCount}/{totalCells})");

            // Tune this threshold based on testing — 0.88–0.92 works well for most Anomaly pits
            const float UNDERGROUND_THRESHOLD = 0.92f;

            if (thickNaturalPercentage > UNDERGROUND_THRESHOLD)
            {
                Logger.Debug($"Detected underground map (> {UNDERGROUND_THRESHOLD:P0} thick natural roof)");
                return true;
            }

            // Optional extra safety check for very small + very roofed maps
            if (map.Size.z < 220 && thickNaturalPercentage > 0.80f)
            {
                Logger.Debug("Small map with high thick roof coverage → likely underground");
                return true;
            }

            return false;
        }

        // Replace the existing SpawnItemAtTradeSpot method with this improved version
        public static void SpawnItemAtTradeSpot(Thing thing, Map map, Pawn forPawn = null)
        {
            if (map == null)
            {
                Logger.Error("SpawnItemAtTradeSpot: Map is null");
                return;
            }

            // First, try to deliver to a locker
            var locker = FindSuitableLockerFor(thing, map, forPawn);
            if (locker != null && locker.TryAcceptThing(thing))
            {
                Logger.Debug($"Delivered {thing.def.defName} x{thing.stackCount} to RimazonLocker at {locker.Position}");

                // Show delivery effect
                MoteMaker.ThrowText(locker.DrawPos + new Vector3(0f, 0f, 0.25f), map,
                    "Delivery Received", Color.white, 2f);
                return;
            }

            // Fallback to normal drop at trade spot
            IntVec3 dropPos = GetCustomDropSpot(map);
            Logger.Debug($"Dropping item at trade spot {dropPos} (no suitable locker found)");

            // Use proper drop logic
            if (DropCellFinder.TryFindDropSpotNear(dropPos, map, out IntVec3 actualDropPos,
                allowFogged: false, canRoofPunch: true, maxRadius: 15))
            {
                GenDrop.TryDropSpawn(thing, actualDropPos, map, ThingPlaceMode.Near, out Thing resultingThing);
                Logger.Debug($"Dropped at {actualDropPos} (adjusted from {dropPos})");
            }
            else
            {
                // Last resort - drop at position
                GenDrop.TryDropSpawn(thing, dropPos, map, ThingPlaceMode.Near, out Thing resultingThing);
            }
        }

        // === ItemDeliveryHelper
        public static (List<Thing> spawnedThings, IntVec3 deliveryPos, DeliveryResult deliveryResult) SpawnItemForPawn(ThingDef thingDef, int quantity, QualityCategory? quality,
            ThingDef material, Pawn pawn, bool addToInventory = false)
        {
            var result = SpawnItemForPawn(thingDef, quantity, quality, material, pawn, addToInventory, false, false);

            // Combine all items from delivery result for backward compatibility
            List<Thing> allItems = new List<Thing>();
            allItems.AddRange(result.LockerDeliveredItems);
            allItems.AddRange(result.DropPodDeliveredItems);
            allItems.AddRange(result.DirectlyDeliveredItems);
            return (allItems, result.DeliveryPosition, result);
        }

        /// <summary>
        /// Spawns Items for delivery, checks for method of delivery and validates.  Returns results
        /// </summary>
        /// <param name="thingDef"></param> 
        /// <param name="quantity"></param>
        /// <param name="quality"></param>
        /// <param name="material"></param>
        /// <param name="pawn"></param>
        /// <param name="addToInventory"></param>
        /// <param name="equipItem"></param>
        /// <param name="wearItem"></param>
        /// <returns></returns>
        public static DeliveryResult SpawnItemForPawn(ThingDef thingDef, int quantity, QualityCategory? quality,
            ThingDef material, Pawn pawn, bool addToInventory, bool equipItem, bool wearItem)
        {
            DeliveryResult result = new DeliveryResult
            {
                LockerDeliveredItems = new List<Thing>(),
                DropPodDeliveredItems = new List<Thing>(),
                DeliveryPosition = IntVec3.Invalid,
                PrimaryMethod = DeliveryMethod.DropPod
            };

            try
            {
                Logger.Debug($"Spawning item: {thingDef.defName}, quantity: {quantity}, for pawn: {pawn?.Name}, " +
                            $"addToInventory: {addToInventory}, equipItem: {equipItem}, wearItem: {wearItem}");

                // Handle special case for pawns (animals)
                if (IsPawnThingDef(thingDef))
                {
                    return HandlePawnDelivery(thingDef, quantity, quality, material, pawn);
                }

                // Handle direct pawn interactions (equip, wear, add to inventory)
                if (equipItem || wearItem || addToInventory)
                {
                    return HandleDirectPawnInteraction(thingDef, quantity, quality, material, pawn, equipItem, wearItem, addToInventory);
                }

                // Handle regular deliveries
                return HandleRegularDelivery(thingDef, quantity, quality, material, pawn);
            }
            catch (Exception ex)
            {
                Logger.Error($"Error spawning item for pawn: {ex}");
                throw;
            }
        }

        // ===== HELPER METHODS =====

        private static bool IsPawnThingDef(ThingDef thingDef)
        {
            return thingDef.thingClass == typeof(Verse.Pawn) || thingDef.race != null;
        }

        private static DeliveryResult HandlePawnDelivery(ThingDef thingDef, int quantity, QualityCategory? quality,
            ThingDef material, Pawn viewerPawn)
        {
            var result = new DeliveryResult
            {
                PrimaryMethod = DeliveryMethod.PawnDelivery,
                DeliveryPosition = IntVec3.Invalid
            };

            Map targetMap = viewerPawn?.Map ?? Find.CurrentMap ?? Find.Maps.FirstOrDefault(m => m.IsPlayerHome);
            if (targetMap == null)
            {
                Logger.Error("No valid map found for pawn delivery");
                return result;
            }

            if (!TryFindSafeDropPosition(targetMap, out IntVec3 deliveryPos))
            {
                Logger.Error("No safe drop position found for pawn delivery");
                return result;
            }

            var pawnDeliveryResult = TryPawnDelivery(thingDef, quantity, quality, material, deliveryPos, targetMap, viewerPawn);
            if (pawnDeliveryResult.success)
            {
                result.DeliveryPosition = pawnDeliveryResult.spawnPosition;
                Logger.Debug($"Pawn delivery successful at position: {result.DeliveryPosition}");
            }

            return result;
        }

        private static DeliveryResult HandleDirectPawnInteraction(ThingDef thingDef, int quantity, QualityCategory? quality,
            ThingDef material, Pawn pawn, bool equipItem, bool wearItem, bool addToInventory)
        {
            var result = new DeliveryResult
            {
                DeliveryPosition = pawn?.Position ?? IntVec3.Invalid
            };

            // Determine final material for stuffable items
            ThingDef finalMaterial = material;
            if (thingDef.MadeFromStuff && finalMaterial == null)
            {
                finalMaterial = GenStuff.RandomStuffFor(thingDef);
                Logger.Debug($"Item requires stuff, selected random material: {finalMaterial?.defName}");
            }

            // Create items
            List<Thing> itemsToDeliver = CreateItemsForDelivery(thingDef, quantity, quality, finalMaterial);

            // Track successfully direct-delivered items
            List<Thing> directlyDelivered = new List<Thing>();

            // Try to deliver based on interaction type
            if (equipItem && pawn != null)
            {
                result.PrimaryMethod = DeliveryMethod.Equipped;
                foreach (var item in itemsToDeliver)
                {
                    if (PawnItemHelper.EquipItemOnPawn(item, pawn))
                    {
                        directlyDelivered.Add(item);
                        Logger.Debug($"Item equipped on pawn");
                    }
                    else
                    {
                        // Optional: Fallback to inventory or drop if equip fails
                        Logger.Debug($"Failed to equip {item.def.defName}, falling back to locker");
                        TryDeliverToLocker(item, pawn.Map, pawn, result);
                    }
                }
            }
            else if (wearItem && pawn != null)
            {
                result.PrimaryMethod = DeliveryMethod.Worn;
                foreach (var item in itemsToDeliver)
                {
                    if (PawnItemHelper.WearApparelOnPawn(item, pawn))
                    {
                        directlyDelivered.Add(item);
                        Logger.Debug($"Item worn by pawn");
                    }
                    else
                    {
                        // Optional: Fallback if wear fails
                        Logger.Debug($"Failed to wear {item.def.defName}, falling back to locker");
                        TryDeliverToLocker(item, pawn.Map, pawn, result);
                    }
                }
            }
            else if (addToInventory && pawn != null)
            {
                result.PrimaryMethod = DeliveryMethod.Inventory;
                foreach (var item in itemsToDeliver)
                {
                    if (pawn.inventory.innerContainer.TryAdd(item))
                    {
                        directlyDelivered.Add(item);
                    }
                    else
                    {
                        Logger.Debug($"Inventory full for item {item.def.defName}");
                        // Fallback to locker delivery for failed inventory items
                        TryDeliverToLocker(item, pawn.Map, pawn, result);
                    }
                }
            }

            result.DirectlyDeliveredItems = directlyDelivered; // Assign the successful ones

            return result;
        }

        private static DeliveryResult HandleRegularDelivery(ThingDef thingDef, int quantity, QualityCategory? quality,
            ThingDef material, Pawn pawn)
        {
            var result = new DeliveryResult
            {
                DeliveryPosition = IntVec3.Invalid
            };

            // Determine final material for stuffable items
            ThingDef finalMaterial = material;
            if (thingDef.MadeFromStuff && finalMaterial == null)
            {
                finalMaterial = GenStuff.RandomStuffFor(thingDef);
                Logger.Debug($"Item requires stuff, selected random material: {finalMaterial?.defName}");
            }

            // Create items
            List<Thing> itemsToDeliver = CreateItemsForDelivery(thingDef, quantity, quality, finalMaterial);

            // Determine target map
            Map targetMap = GetTargetMapForDelivery(pawn);
            if (targetMap == null)
            {
                Logger.Error("No valid map found for delivery");
                return result;
            }

            // Try to deliver each item to a locker
            List<Thing> undeliveredItems = new List<Thing>();
            foreach (var item in itemsToDeliver)
            {
                if (!TryDeliverToLocker(item, targetMap, pawn, result))
                {
                    undeliveredItems.Add(item);
                }
            }

            // Handle undelivered items with drop pod
            if (undeliveredItems.Count > 0)
            {
                Logger.Debug($"{undeliveredItems.Count} items couldn't fit in lockers, using drop pod");
                result.DropPodDeliveredItems.AddRange(undeliveredItems);

                IntVec3 dropPos = GetDeliveryPosition(targetMap, pawn);
                if (TryShuttleDelivery(undeliveredItems, dropPos, targetMap))
                {
                    result.DeliveryPosition = dropPos;
                    Logger.Debug($"Drop pod delivery successful at {dropPos}");
                }
            }

            // Determine primary delivery method
            DeterminePrimaryDeliveryMethod(result);

            // Set final delivery position if not set
            if (result.DeliveryPosition == IntVec3.Invalid || result.DeliveryPosition == default(IntVec3))
            {
                result.DeliveryPosition = GetFallbackDeliveryPosition(targetMap, result);
            }

            Logger.Debug($"Delivery result: Method={result.PrimaryMethod}, Position={result.DeliveryPosition}, " +
                        $"LockerItems={result.LockerDeliveredItems.Sum(t => t.stackCount)}, " +
                        $"DropPodItems={result.DropPodDeliveredItems.Sum(t => t.stackCount)}");

            return result;
        }

        private static bool TryDeliverToLocker(Thing item, Map map, Pawn pawn, DeliveryResult result)
        {
            var locker = FindSuitableLockerFor(item, map, pawn);
            if (locker != null && locker.TryAcceptThing(item))
            {
                Logger.Debug($"Delivered {item.def.defName} x{item.stackCount} to locker at {locker.Position}");
                result.LockerDeliveredItems.Add(item);

                if (result.DeliveryPosition == IntVec3.Invalid || result.DeliveryPosition == default(IntVec3))
                {
                    result.DeliveryPosition = locker.Position;
                }

                return true;
            }

            Logger.Debug($"Could not deliver {item.def.defName} x{item.stackCount} to locker");
            return false;
        }

        private static Map GetTargetMapForDelivery(Pawn pawn)
        {
            Map targetMap = pawn?.Map;

            if (targetMap != null && IsUndergroundMap(targetMap))
            {
                Map surfaceMap = Find.Maps.FirstOrDefault(m => m.IsPlayerHome && !IsUndergroundMap(m));
                if (surfaceMap != null)
                {
                    Logger.Debug($"Underground delivery detected → redirecting to surface map");
                    return surfaceMap;
                }
            }

            return targetMap ?? Find.CurrentMap ?? Find.Maps.FirstOrDefault(m => m.IsPlayerHome);
        }

        private static IntVec3 GetDeliveryPosition(Map map, Pawn pawn)
        {
            IntVec3 tradeSpot = GetCustomDropSpot(map);
            IntVec3 deliveryPos = IntVec3.Invalid;

            // First try near trade spot
            if (DropCellFinder.TryFindDropSpotNear(tradeSpot, map, out deliveryPos,
                allowFogged: false, canRoofPunch: true, maxRadius: 15))
            {
                Logger.Debug($"Using drop spot near trade spot: {deliveryPos}");
                return deliveryPos;
            }

            // Then try near pawn
            if (pawn != null && DropCellFinder.TryFindDropSpotNear(pawn.Position, map, out deliveryPos,
                allowFogged: false, canRoofPunch: true, maxRadius: 15))
            {
                Logger.Debug($"Using position near pawn: {deliveryPos}");
                return deliveryPos;
            }

            // Fallback to trade spot
            Logger.Debug($"Using trade spot directly: {tradeSpot}");
            return tradeSpot;
        }

        private static void DeterminePrimaryDeliveryMethod(DeliveryResult result)
        {
            if (result.LockerDeliveredItems.Count > 0 && result.DropPodDeliveredItems.Count == 0)
            {
                result.PrimaryMethod = DeliveryMethod.Locker;
            }
            else if (result.LockerDeliveredItems.Count == 0 && result.DropPodDeliveredItems.Count > 0)
            {
                result.PrimaryMethod = DeliveryMethod.DropPod;
            }
            else if (result.LockerDeliveredItems.Count > 0 && result.DropPodDeliveredItems.Count > 0)
            {
                result.PrimaryMethod = DeliveryMethod.DropPod; // Mixed, prioritize drop pod
            }
        }

        private static IntVec3 GetFallbackDeliveryPosition(Map map, DeliveryResult result)
        {
            // If we have locker items, use the first locker position
            if (result.LockerDeliveredItems.Count > 0)
            {
                var firstItem = result.LockerDeliveredItems.FirstOrDefault();
                var locker = FindSuitableLockerFor(firstItem, map, null);
                if (locker != null)
                {
                    Logger.Debug($"Using first locker position as fallback: {locker.Position}");
                    return locker.Position;
                }
            }

            // Otherwise use trade spot
            IntVec3 tradeSpot = GetCustomDropSpot(map);
            Logger.Debug($"Using trade spot as fallback: {tradeSpot}");
            return tradeSpot;
        }

        private static List<Thing> CreateItemsForDelivery(ThingDef thingDef, int quantity, QualityCategory? quality, ThingDef material)
        {
            List<Thing> things = new List<Thing>();
            int remainingQuantity = quantity;

            // Simple check: minifiable items get minified
            bool shouldMinify = thingDef.Minifiable;

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

            Logger.Debug($"Created {things.Count} items (minified: {shouldMinify})");
            return things;
        }

        private static void DeliverItemsInDropPods(List<Thing> thingsToDeliver, IntVec3 dropPos, Map map)
        {
            // Just call the shuttle delivery method as a fallback
            TryShuttleDelivery(thingsToDeliver, dropPos, map);
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

        private static bool TryFindSafeDropPosition(Map map, out IntVec3 dropPos)
        {
            dropPos = IntVec3.Invalid;

            if (map == null)
                return false;

            // First try trade spot
            IntVec3 tradeSpot = GetCustomDropSpot(map);
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

                // NEW: Prefer spawning near the nearest RimazonLocker if one exists
                // (we don't try to put pawns *inside* the locker, just use it as a safe/central drop point)
                Building_RimazonLocker nearestLocker = null;

                var allLockers = map.listerThings.AllThings
                    .OfType<Building_RimazonLocker>()
                    .Where(l => l.Spawned && l.Map == map && !l.Destroyed)
                    .ToList();  // Materialize to avoid multiple enumerations

                if (allLockers.Any())
                {
                    nearestLocker = allLockers
                        .OrderBy(l => l.Position.DistanceToSquared(dropPos))  // Sort by distance (squared = faster)
                        .First();  // Closest one

                    Logger.Debug($"Found nearest RimazonLocker at {nearestLocker.Position} (distance squared: {nearestLocker.Position.DistanceToSquared(dropPos)})");
                }
                else
                {
                    Logger.Debug("No RimazonLocker found on map");
                }

                if (nearestLocker != null)
                {
                    Logger.Debug($"Using nearest RimazonLocker at {nearestLocker.Position} as base for pawn spawn");
                    dropPos = nearestLocker.Position;  // Override the preferred drop position
                }
                else
                {
                    Logger.Debug("No RimazonLocker found — using original drop position");
                }

                List<Pawn> pawnsToDeliver = new List<Pawn>();

                for (int i = 0; i < quantity; i++)
                {
                    // Create pawn using RimWorld's proper pawn generation
                    PawnGenerationRequest request = new PawnGenerationRequest(
                        kind: pawnDef.race.AnyPawnKind,
                        faction: null,
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

        public static bool ShouldMinifyForDelivery(ThingDef thingDef)
        {
            if (thingDef == null) return false;

            // All minifiable items should be minified for delivery
            if (thingDef.Minifiable)
            {
                Logger.Debug($"{thingDef.defName} is minifiable - will be minified for delivery");
                return true;
            }

            Logger.Debug($"{thingDef.defName} is not minifiable - will be delivered normally");
            return false;
        }

        public static Thing CreateMinifiedThing(ThingDef thingDef, QualityCategory? quality, ThingDef material)
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

        public static List<Thing> CreateThingsForDelivery(ThingDef thingDef, int quantity, QualityCategory? quality, ThingDef material)
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
    }
}