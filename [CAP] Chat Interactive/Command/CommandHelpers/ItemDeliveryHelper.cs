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
using RimWorld.Planet;
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
        public int LockerDeliveredCount { get; set; } = 0;
        public int DropPodDeliveredCount { get; set; } = 0;
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
        // ═══════════════════════════════════════════════════════════════════════
        // DELIVERY PIPELINE (single source of truth)
        //
        // 1) ResolveDeliveryMap  — which map receives the delivery
        // 2) TryFindDeliveryCell — which cell on that map (ordered priorities)
        // 3) TryDeliverGeneratedPawn / locker+pod item paths — how it arrives
        //
        // Pocket / thick-roof / "underground" maps: when a normal surface/home map
        // exists, we REDIRECT the whole delivery to that map (coordinates stay on
        // the chosen map). Surface spawn above a pocket is intentional for now;
        // RICS does not depend on third-party nomadic map APIs.
        // ═══════════════════════════════════════════════════════════════════════

        private static int _undergroundCacheTick = -1;
        private static readonly Dictionary<int, bool> _undergroundByMapId = new Dictionary<int, bool>();

        /// <summary>
        /// Chooses the map for any RICS delivery (items, !pawn, store animals/mechs).
        /// Priority: anchor pawn map → CurrentMap → player home → map with colonists/lockers.
        /// Underground/pocket maps redirect to a non-underground player home when available.
        /// </summary>
        public static Map ResolveDeliveryMap(Pawn anchorPawn = null, bool allowUndergroundRedirect = true)
        {
            try
            {
                Map candidate = null;

                if (anchorPawn != null && !anchorPawn.Destroyed && anchorPawn.Spawned && anchorPawn.Map != null)
                    candidate = anchorPawn.Map;

                if (candidate == null && Find.CurrentMap != null)
                    candidate = Find.CurrentMap;

                if (candidate == null)
                    candidate = Find.Maps?.FirstOrDefault(m => m != null && m.IsPlayerHome);

                if (candidate == null)
                {
                    candidate = Find.Maps?.FirstOrDefault(m =>
                        m != null &&
                        (m.mapPawns?.FreeColonistsSpawned?.Count > 0 ||
                         m.listerThings?.AllThings?.OfType<Building_RimazonLocker>().Any(l => l.Spawned) == true));
                }

                if (candidate == null)
                {
                    Logger.Debug("ResolveDeliveryMap: no suitable map found");
                    return null;
                }

                if (allowUndergroundRedirect && IsUndergroundMap(candidate))
                {
                    Map surface = Find.Maps?.FirstOrDefault(m =>
                        m != null && m.IsPlayerHome && !IsUndergroundMap(m));
                    if (surface != null && surface != candidate)
                    {
                        Logger.Debug(
                            $"Delivery map redirected underground/pocket → surface " +
                            $"{surface.Parent?.LabelCap ?? surface.ToString()}");
                        return surface;
                    }
                }

                Logger.Debug(
                    $"ResolveDeliveryMap: {candidate.Parent?.LabelCap ?? candidate.ToString()} " +
                    $"(home={candidate.IsPlayerHome}, underground={IsUndergroundMap(candidate)})");
                return candidate;
            }
            catch (Exception ex)
            {
                Logger.Error($"ResolveDeliveryMap failed: {ex.Message}");
                return Find.Maps?.FirstOrDefault(m => m != null && m.IsPlayerHome);
            }
        }

        /// <summary>
        /// Finds a delivery cell on <paramref name="map"/> only (never returns coords from another map).
        /// Order: Drop Marker → labeled drop spot → trade beacon → ship beacon → hitching →
        /// colonists → drop near anchor → locker → friendly pawn → edge → relaxed → center.
        /// </summary>
        public static bool TryFindDeliveryCell(Map map, out IntVec3 cell)
        {
            cell = IntVec3.Invalid;
            if (map == null) return false;

            try
            {
                // 1–6: preferred anchors on THIS map
                IntVec3 anchor = GetPreferredDropAnchorOnMap(map);

                // Prefer a real drop-pod cell near the anchor
                if (anchor.IsValid && DropCellFinder.TryFindDropSpotNear(
                        anchor, map, out cell, allowFogged: false, canRoofPunch: true, maxRadius: 35)
                    && IsValidDeliveryPosition(cell, map))
                {
                    Logger.Debug($"TryFindDeliveryCell: drop near anchor {anchor} → {cell}");
                    return true;
                }

                if (anchor.IsValid && IsValidDeliveryPosition(anchor, map, strict: false))
                {
                    cell = anchor;
                    Logger.Debug($"TryFindDeliveryCell: using anchor cell {cell}");
                    return true;
                }

                // 7. Near Rimazon Locker
                var lockers = map.listerThings.AllThings.OfType<Building_RimazonLocker>()
                    .Where(l => l.Spawned && !l.Destroyed).ToList();
                if (lockers.Any())
                {
                    var nearest = lockers.OrderBy(l => l.Position.DistanceToSquared(
                        anchor.IsValid ? anchor : map.Center)).First();
                    if (DropCellFinder.TryFindDropSpotNear(
                            nearest.Position, map, out cell, allowFogged: false, canRoofPunch: true, maxRadius: 12)
                        && IsValidDeliveryPosition(cell, map))
                    {
                        Logger.Debug($"TryFindDeliveryCell: near locker {nearest.Position} → {cell}");
                        return true;
                    }
                    if (CellFinder.TryFindRandomCellNear(nearest.Position, map, 6,
                            c => c.Standable(map) && c.Walkable(map), out cell))
                    {
                        Logger.Debug($"TryFindDeliveryCell: standable near locker → {cell}");
                        return true;
                    }
                }

                // 8. Near friendly pawn
                var friendly = map.mapPawns.AllPawnsSpawned
                    .Where(p => p.Faction == Faction.OfPlayer && p.Spawned && !p.Dead).ToList();
                if (friendly.Any())
                {
                    var nearest = friendly.OrderBy(p => p.Position.DistanceToSquared(
                        anchor.IsValid ? anchor : map.Center)).First();
                    if (DropCellFinder.TryFindDropSpotNear(
                            nearest.Position, map, out cell, allowFogged: false, canRoofPunch: true, maxRadius: 15)
                        && IsValidDeliveryPosition(cell, map))
                    {
                        Logger.Debug($"TryFindDeliveryCell: near colonist {nearest.LabelShort} → {cell}");
                        return true;
                    }
                    if (CellFinder.TryFindRandomCellNear(nearest.Position, map, 8,
                            c => c.Standable(map) && c.Walkable(map), out cell))
                    {
                        Logger.Debug($"TryFindDeliveryCell: standable near colonist → {cell}");
                        return true;
                    }
                }

                // 9. Map edge
                if (CellFinder.TryFindRandomEdgeCellWith(
                        c => c.Standable(map) && !c.Fogged(map) && c.Walkable(map),
                        map, CellFinder.EdgeRoadChance_Ignore, out cell)
                    && IsValidDeliveryPosition(cell, map))
                {
                    Logger.Debug($"TryFindDeliveryCell: map edge → {cell}");
                    return true;
                }

                // 10. Relaxed walkable
                if (CellFinderLoose.TryFindRandomNotEdgeCellWith(10,
                        c => IsValidDeliveryPosition(c, map, strict: false), map, out cell))
                {
                    Logger.Warning($"TryFindDeliveryCell: relaxed cell last resort → {cell}");
                    return true;
                }

                // 11. Center
                cell = map.Center;
                Logger.Warning("TryFindDeliveryCell: map center ultimate fallback");
                return cell.InBounds(map);
            }
            catch (Exception ex)
            {
                Logger.Error($"TryFindDeliveryCell failed: {ex.Message}");
                cell = map?.Center ?? IntVec3.Invalid;
                return cell.IsValid && map != null && cell.InBounds(map);
            }
        }

        /// <summary>
        /// Places an already-generated pawn on the map: drop pod preferred, then GenSpawn near locker/colonist/center.
        /// Equips vacsuit on space/vacuum maps before pod.
        /// </summary>
        public static bool TryDeliverGeneratedPawn(Pawn pawn, Map map, out IntVec3 deliveryPosition)
        {
            deliveryPosition = IntVec3.Invalid;
            if (pawn == null || map == null) return false;

            try
            {
                if (IsSpaceMap(map) || map.Biome?.inVacuum == true)
                    EquipVacsuitIfNeeded(pawn);

                // Priority 1: drop pod at delivery cell
                if (TryFindDeliveryCell(map, out IntVec3 safePos))
                {
                    deliveryPosition = safePos;
                    DropPodUtility.DropThingsNear(
                        safePos, map, new List<Thing> { pawn },
                        openDelay: 110, leaveSlag: false, canRoofPunch: true, forbid: true);
                    Logger.Debug($"TryDeliverGeneratedPawn: drop pod at {safePos} on {map.Parent?.LabelCap}");
                    return true;
                }

                // Priority 2: GenSpawn near locker
                var locker = FindSuitableLockerFor(pawn, map) ??
                             map.listerThings.AllThings.OfType<Building_RimazonLocker>()
                                 .FirstOrDefault(l => l.Spawned && !l.Destroyed);
                if (locker != null &&
                    CellFinder.TryFindRandomCellNear(locker.Position, map, 6,
                        c => c.Standable(map) && c.Walkable(map), out var nearLocker))
                {
                    GenSpawn.Spawn(pawn, nearLocker, map);
                    deliveryPosition = nearLocker;
                    Logger.Debug($"TryDeliverGeneratedPawn: GenSpawn near locker at {nearLocker}");
                    return true;
                }

                // Priority 3: near any player pawn
                var anyPlayerPawn = map.mapPawns.AllPawnsSpawned
                    .FirstOrDefault(p => p.Faction == Faction.OfPlayer && p.Spawned && !p.Dead);
                if (anyPlayerPawn != null &&
                    CellFinder.TryFindRandomCellNear(anyPlayerPawn.Position, map, 8,
                        c => c.Standable(map) && c.Walkable(map), out var nearPawn))
                {
                    GenSpawn.Spawn(pawn, nearPawn, map);
                    deliveryPosition = nearPawn;
                    Logger.Debug($"TryDeliverGeneratedPawn: GenSpawn near colonist at {nearPawn}");
                    return true;
                }

                // Priority 4: center
                GenSpawn.Spawn(pawn, map.Center, map);
                deliveryPosition = map.Center;
                Logger.Warning("TryDeliverGeneratedPawn: GenSpawn at map center");
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error($"TryDeliverGeneratedPawn failed: {ex}");
                deliveryPosition = map?.Center ?? IntVec3.Invalid;
                return false;
            }
        }

        /// <summary>
        /// Preferred "trade spot" anchors on a single map (no cross-map coordinate return).
        /// Order: Rimazon Drop Marker → labeled drop spot → unroofed trade beacon →
        /// ship landing → caravan packing → average free colonist.
        /// </summary>
        private static IntVec3 GetPreferredDropAnchorOnMap(Map map)
        {
            if (map == null) return IntVec3.Invalid;

            ThingDef markerDef = DefDatabase<ThingDef>.GetNamedSilentFail("RimazonDropMarker");
            if (markerDef != null)
            {
                Building marker = map.listerBuildings.AllBuildingsColonistOfDef(markerDef)
                    .FirstOrDefault(b => b.Spawned && !b.Destroyed);
                if (marker != null)
                    return marker.Position;
            }

            var dropSpotThing = map.listerThings.AllThings
                .Where(t => t.Spawned && !t.Destroyed && t.Map == map)
                .FirstOrDefault(t =>
                {
                    string label = t.Label?.ToLowerInvariant();
                    return !string.IsNullOrWhiteSpace(label) && label.Contains("drop spot");
                });
            if (dropSpotThing != null)
                return dropSpotThing.Position;

            var tradeBeacon = map.listerBuildings.AllBuildingsColonistOfDef(ThingDefOf.OrbitalTradeBeacon)
                .FirstOrDefault(b => b.Spawned && !b.Destroyed && !map.roofGrid.Roofed(b.Position));
            if (tradeBeacon != null)
                return tradeBeacon.Position;

            Building shipBeacon = map.listerBuildings.AllBuildingsColonistOfDef(ThingDefOf.ShipLandingBeacon)
                .FirstOrDefault(b => b.Spawned && !b.Destroyed);
            if (shipBeacon != null)
                return shipBeacon.Position;

            ThingDef hitchingDef = ThingDefOf.CaravanPackingSpot
                ?? DefDatabase<ThingDef>.GetNamedSilentFail("CaravanPackingSpot");
            if (hitchingDef != null)
            {
                Building hitchingSpot = map.listerBuildings.AllBuildingsColonistOfDef(hitchingDef)
                    .FirstOrDefault(b => b.Spawned && !b.Destroyed);
                if (hitchingSpot != null)
                    return hitchingSpot.Position;
            }

            var freeColonists = map.mapPawns.FreeColonistsSpawned;
            if (freeColonists.Count > 0)
            {
                IntVec3 average = IntVec3.Zero;
                foreach (var colonist in freeColonists)
                    average += colonist.Position;
                average /= freeColonists.Count;

                if (CellFinder.TryFindRandomCellNear(average, map, 20,
                        c => c.Standable(map) && !c.Fogged(map), out IntVec3 spot))
                    return spot;

                return average;
            }

            return IntVec3.Invalid;
        }

        /// <summary>Equips vacsuit + helmet when available (space / vacuum maps).</summary>
        public static void EquipVacsuitIfNeeded(Pawn pawn)
        {
            try
            {
                if (pawn?.apparel == null) return;

                ThingDef suitDef = pawn.ageTracker.CurLifeStageIndex <= 1
                    ? DefDatabase<ThingDef>.GetNamedSilentFail("Apparel_VacsuitChildren")
                    : DefDatabase<ThingDef>.GetNamedSilentFail("Apparel_Vacsuit");
                ThingDef helmetDef = DefDatabase<ThingDef>.GetNamedSilentFail("Apparel_VacsuitHelmet");

                if (suitDef != null)
                {
                    Apparel suit = PawnApparelGenerator.GenerateApparelOfDefFor(pawn, suitDef);
                    if (suit != null && ApparelUtility.HasPartsToWear(pawn, suit.def))
                        pawn.apparel.Wear(suit);
                }

                if (helmetDef != null)
                {
                    Apparel helmet = PawnApparelGenerator.GenerateApparelOfDefFor(pawn, helmetDef);
                    if (helmet != null && ApparelUtility.HasPartsToWear(pawn, helmet.def))
                        pawn.apparel.Wear(helmet);
                }
            }
            catch (Exception ex)
            {
                Logger.Warning($"Failed to equip vacsuit on {pawn?.LabelShort}: {ex.Message}");
            }
        }

        public static Building_RimazonLocker FindSuitableLockerFor(Thing thing, Map map, Pawn forPawn = null)
        {
            try
            {
                if (map == null || thing == null)
                {
                    //Logger.Debug("FindSuitableLocker: Map or thing is null");
                    return null;
                }

                if (thing.def.Minifiable)
                {
                    //Logger.Debug($"{thing.def.defName} is minifiable - skipping locker delivery");
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
                    //Logger.Debug($"Found {smallLockers.Count()} small lockers");
                }

                // Try large locker def (might not exist yet)
                ThingDef largeLockerDef = DefDatabase<ThingDef>.GetNamedSilentFail("RimazonLockerLarge");
                if (largeLockerDef != null)
                {
                    var largeLockers = map.listerThings.ThingsOfDef(largeLockerDef)
                        .OfType<Building_RimazonLocker>();
                    allLockers.AddRange(largeLockers);
                    //Logger.Debug($"Found {largeLockers.Count()} large lockers");
                }

                if (!allLockers.Any())
                {
                    //Logger.Debug($"No RimazonLockers found on map");
                    return null;
                }

                //Logger.Debug($"Found {allLockers.Count} total lockers on map");

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
                    //Logger.Debug($"No suitable RimazonLocker found for {thing.def.defName} x{thing.stackCount}");

                    // Log debug info for each locker
                    //foreach (var locker in allLockers.Take(3)) // Just first 3 for brevity
                    //{
                    //    Logger.Debug($"Locker at {locker.Position}: " +
                    //               $"Spawned={locker.Spawned}, " +
                    //               $"MapMatch={locker.Map == map}, " +
                    //               $"Destroyed={locker.Destroyed}, " +
                    //               $"Accepts={locker.Accepts(thing)}, " +
                    //               $"CurrentItems={locker.InnerContainer.TotalStackCount}/{locker.MaxStacks}");
                    //}

                    return null;
                }

                //Logger.Debug($"Found {suitableLockers.Count} suitable lockers for {thing.def.defName}");

                // Selection priority:
                Building_RimazonLocker bestLocker = null;

                // 1. Try to find lockers that already have this item type (for stack merging)
                var matchingLockers = suitableLockers
                    .Where(l =>
                    {
                        return l.InnerContainer.Any(t =>
                            t.def == thing.def &&
                            t.Stuff == thing.Stuff &&                     // same material
                            (!t.TryGetQuality(out var q) ||               // either no quality
                             !thing.TryGetQuality(out var q2) ||
                             q == q2)                                     // or same quality
                        );
                    })
                    .ToList();

                if (matchingLockers.Any())
                {
                    // Prefer the one closest to target + with most free space
                    bestLocker = matchingLockers
                        .OrderBy(l => l.Position.DistanceToSquared(forPawn?.Position ?? GetCustomDropSpot(map)))
                        .ThenByDescending(l => l.MaxStacks - l.InnerContainer.TotalStackCount)
                        .FirstOrDefault();

                    if (bestLocker != null)
                    {
                        //Logger.Debug($"Selected BEST MATCH locker for stacking: {bestLocker.Position} " +
                        //             $"(free stacks: {bestLocker.MaxStacks - bestLocker.InnerContainer.TotalStackCount})");
                        return bestLocker;
                    }
                }

                // 2. If no perfect match, still prefer ANY suitable locker over drop pod
                if (suitableLockers.Any())
                {
                    bestLocker = suitableLockers
                        .OrderBy(l => l.Position.DistanceToSquared(forPawn?.Position ?? GetCustomDropSpot(map)))
                        .ThenByDescending(l => l.MaxStacks - l.InnerContainer.TotalStackCount)
                        .FirstOrDefault();

                    if (bestLocker != null)
                    {
                        // Logger.Debug($"No perfect stack match → selected nearest suitable locker anyway: {bestLocker.Position}");
                        return bestLocker;
                    }
                }

                // 3. Fall back to proximity
                IntVec3 targetPos = forPawn != null && forPawn.Spawned && forPawn.Map == map
                    ? forPawn.Position
                    : GetCustomDropSpot(map);

                bestLocker = suitableLockers
                    .OrderBy(locker => locker.Position.DistanceToSquared(targetPos))
                    .ThenByDescending(locker => locker.MaxStacks - locker.InnerContainer.TotalStackCount) // Most space left
                    .FirstOrDefault();

                //if (bestLocker != null)
                //{
                //    Logger.Debug($"Selected locker at {bestLocker.Position} " +
                //                $"(distance to target: {bestLocker.Position.DistanceTo(targetPos):F1}, " +
                //                $"space left: {bestLocker.MaxStacks - bestLocker.InnerContainer.TotalStackCount})");
                //}

                return bestLocker;
            }
            catch (Exception ex)
            {
                Logger.Error($"Error finding suitable locker: {ex}");
                return null;
            }
        }

        /// <summary>
        /// Preferred drop-anchor cell on this map only.
        /// Call ResolveDeliveryMap first so underground maps redirect at the map layer —
        /// never return coordinates from a different map.
        /// </summary>
        public static IntVec3 GetCustomDropSpot(Map map)
        {
            if (map == null) return IntVec3.Invalid;

            IntVec3 anchor = GetPreferredDropAnchorOnMap(map);
            if (anchor.IsValid) return anchor;

            Logger.Warning("GetCustomDropSpot: no anchor → map center");
            return map.Center;
        }

        /// <summary>
        /// Thick natural rock-roof heuristic for pocket/underground-like maps.
        /// Cached per map for the current game tick (full scan is expensive).
        /// </summary>
        public static bool IsUndergroundMap(Map map)
        {
            if (map == null) return false;

            int tick = Find.TickManager?.TicksGame ?? 0;
            if (tick != _undergroundCacheTick)
            {
                _undergroundByMapId.Clear();
                _undergroundCacheTick = tick;
            }

            int id = map.uniqueID;
            if (_undergroundByMapId.TryGetValue(id, out bool cached))
                return cached;

            bool result = ComputeIsUndergroundMap(map);
            _undergroundByMapId[id] = result;
            return result;
        }

        private static bool ComputeIsUndergroundMap(Map map)
        {
            int naturalThickRoofCount = 0;
            int totalCells = 0;

            foreach (IntVec3 cell in map.AllCells)
            {
                totalCells++;
                RoofDef roof = map.roofGrid.RoofAt(cell);
                if (roof != null && roof.isNatural && roof == RoofDefOf.RoofRockThick)
                    naturalThickRoofCount++;
            }

            if (totalCells == 0) return false;

            float thickNaturalPercentage = (float)naturalThickRoofCount / totalCells;
            const float UNDERGROUND_THRESHOLD = 0.92f;

            if (thickNaturalPercentage > UNDERGROUND_THRESHOLD)
                return true;

            if (map.Size.z < 220 && thickNaturalPercentage > 0.80f)
                return true;

            return false;
        }

        // SpawnItemAtTradeSpot removed — unused; use SpawnItemForPawn / locker+pod pipeline.

        // === ItemDeliveryHelper
        /// <summary>
        /// Spawns Items for delivery, checks for method of delivery and validates.  Returns results
        /// </summary>
        /// <param name="thingDef"></param>
        /// <param name="quantity"></param>
        /// <param name="quality"></param>
        /// <param name="material"></param>
        /// <param name="pawn"></param>
        /// <param name="addToInventory"></param>
        /// <returns></returns>
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
            ThingDef material, Pawn pawn, bool addToInventory, bool equipItem, bool wearItem,
            Thing preCreatedItem = null)
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
                if (preCreatedItem != null)
                {
                    // UNIQUE WEAPON PATH — reuse the already-created item with traits
                    Logger.Debug($"Using pre-created unique weapon for delivery: {preCreatedItem.def.defName}");

                    // For direct interactions (equip/wear/inventory)
                    if (equipItem || wearItem || addToInventory)
                    {
                        return HandleDirectPawnInteractionWithPreCreated(preCreatedItem, pawn, equipItem, wearItem, addToInventory, result);
                    }
                    else
                    {
                        // Regular delivery
                        return HandleRegularDeliveryWithPreCreated(preCreatedItem, pawn, result);
                    }
                }
                // Logger.Debug($"Spawning item: {thingDef.defName}, quantity: {quantity}, for pawn: {pawn?.Name}, " +
                //             $"addToInventory: {addToInventory}, equipItem: {equipItem}, wearItem: {wearItem}");

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

        /// <summary>
        /// Checks if the given ThingDef represents a pawn (animal or humanoid) by verifying its thingClass
        /// </summary>
        /// <param name="thingDef"></param>
        /// <returns></returns>
        private static bool IsPawnThingDef(ThingDef thingDef)
        {
            return thingDef.thingClass == typeof(Verse.Pawn) || thingDef.race != null;
        }

        /// <summary>
        /// Handles the delivery of a pawn (animal or humanoid) to the player's map, ensuring a safe drop position is found.
        /// </summary>
        /// <param name="thingDef"></param>
        /// <param name="quantity"></param>
        /// <param name="quality"></param>
        /// <param name="material"></param>
        /// <param name="viewerPawn"></param>
        /// <returns></returns>
        private static DeliveryResult HandlePawnDelivery(ThingDef thingDef, int quantity, QualityCategory? quality,
            ThingDef material, Pawn viewerPawn)
        {
            var result = new DeliveryResult
            {
                PrimaryMethod = DeliveryMethod.PawnDelivery,
                DeliveryPosition = IntVec3.Invalid
            };

            Map targetMap = ResolveDeliveryMap(viewerPawn);
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

            var pawnDeliveryResult = TryDeliverPawnFromStore(thingDef, quantity, quality, material, deliveryPos, targetMap, viewerPawn);
            if (pawnDeliveryResult.success)
            {
                result.DeliveryPosition = pawnDeliveryResult.spawnPosition;
                // Logger.Debug($"Pawn delivery successful at position: {result.DeliveryPosition}");
            }

            return result;
        }

        /// <summary>
        /// Handles direct interactions with a pawn, such as equipping, wearing, or adding items to their inventory. If the interaction fails, it falls back to locker delivery.
        /// </summary>
        /// <param name="thingDef"></param>
        /// <param name="quantity"></param>
        /// <param name="quality"></param>
        /// <param name="material"></param>
        /// <param name="pawn"></param>
        /// <param name="equipItem"></param>
        /// <param name="wearItem"></param>
        /// <param name="addToInventory"></param>
        /// <returns></returns>
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
                //Logger.Debug($"Item requires stuff, selected random material: {finalMaterial?.defName}");
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
                        // Logger.Debug($"Item equipped on pawn");
                    }
                    else
                    {
                        // Fallback to inventory or drop if equip fails
                        // Logger.Debug($"Failed to equip {item.def.defName}, falling back to locker");
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
                        // Logger.Debug($"Item worn by pawn");
                    }
                    else
                    {
                        // Optional: Fallback if wear fails
                        // Logger.Debug($"Failed to wear {item.def.defName}, falling back to locker");
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
                        // Logger.Debug($"Inventory full for item {item.def.defName}");
                        // Fallback to locker delivery for failed inventory items
                        TryDeliverToLocker(item, pawn.Map, pawn, result);
                    }
                }
            }

            result.DirectlyDeliveredItems = directlyDelivered; // Assign the successful ones

            return result;
        }

        /// <summary>
        /// Handles regular deliveries of items to the player's map, attempting to place them in Rimazon lockers first, and falling back to drop pods if necessary.
        /// </summary>
        /// <param name="thingDef"></param>
        /// <param name="quantity"></param>
        /// <param name="quality"></param>
        /// <param name="material"></param>
        /// <param name="pawn"></param>
        /// <returns></returns>
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
                // Logger.Debug($"Item requires stuff, selected random material: {finalMaterial?.defName}");
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
            int attemptedLockerTotal = 0;

            foreach (var item in itemsToDeliver)
            {
                int thisCount = item.stackCount;
                attemptedLockerTotal += thisCount;

                if (!TryDeliverToLocker(item, targetMap, pawn, result))
                {
                    undeliveredItems.Add(item);
                }
            }

            if (undeliveredItems.Count > 0)
            {
                int dropCount = undeliveredItems.Sum(t => t?.stackCount ?? 0);
                // Logger.Debug($"{undeliveredItems.Count} stacks ({dropCount} total units) rejected by lockers → using drop pod");
                result.DropPodDeliveredCount += dropCount;
            }

            // Handle undelivered items with drop pod
            if (undeliveredItems.Count > 0)
            {
                int dropCount = undeliveredItems.Sum(t => t?.stackCount ?? 0);  // Null-safe
                // Logger.Debug($"{undeliveredItems.Count} items ({dropCount} total count) couldn't fit in lockers, using drop pod");
                result.DropPodDeliveredItems.AddRange(undeliveredItems);

                IntVec3 dropPos = GetDeliveryPosition(targetMap, pawn);
                if (TryShuttleDelivery(undeliveredItems, dropPos, targetMap))
                {
                    result.DropPodDeliveredCount += dropCount;
                    result.DeliveryPosition = dropPos;
                    //Logger.Debug($"Drop pod delivery successful at {dropPos} with {dropCount} items");
                }
                else
                {
                    Logger.Error("Drop pod delivery failed");
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

        /// <summary>
        /// Attempts to deliver a single item to a suitable Rimazon locker on the map. If successful, updates the delivery result with the count and position.
        /// </summary>
        /// <param name="item"></param>
        /// <param name="map"></param>
        /// <param name="pawn"></param>
        /// <param name="result"></param>
        /// <returns></returns>
        private static bool TryDeliverToLocker(Thing item, Map map, Pawn pawn, DeliveryResult result)
        {
            if (item == null || item.Destroyed)
            {
                Logger.Error("TryDeliverToLocker: Item is null or already destroyed before delivery attempt");
                return false;
            }

            var locker = FindSuitableLockerFor(item, map, pawn);
            if (locker == null)
            {
                // Logger.Debug($"No suitable Rimazon locker found for {item.def.defName} x{item.stackCount}");
                return false;
            }

            int attemptedCount = item.stackCount;  // Snapshot BEFORE possible merge/destroy

            bool accepted = locker.TryAcceptThing(item, allowSpecialEffects: false);  // false = no extra effects/motes from storage

            if (accepted)
            {
                // Whether merged or new stack — delivery succeeded
                // Logger.Debug($"Successfully delivered/merged {item.def.defName} x{attemptedCount} → locker at {locker.Position}");

                result.LockerDeliveredCount += attemptedCount;

                // Set position if not already set (first successful locker wins for letter targeting)
                if (result.DeliveryPosition == IntVec3.Invalid)
                {
                    result.DeliveryPosition = locker.Position;
                }

                return true;
            }
            else
            {
                //Logger.Debug($"Locker {locker.Position} refused {item.def.defName} x{attemptedCount} " +
                //             $"(current: {locker.InnerContainer.TotalStackCount}/{locker.MaxStacks}, " +
                //             $"storage settings allow? {locker.settings?.AllowedToAccept(item) ?? false})");
                return false;
            }
        }

        /// <summary>Map for item delivery — shared ResolveDeliveryMap pipeline.</summary>
        private static Map GetTargetMapForDelivery(Pawn pawn)
        {
            return ResolveDeliveryMap(pawn, allowUndergroundRedirect: true);
        }

        /// <summary>Drop-pod cell for items on map (shared cell finder).</summary>
        private static IntVec3 GetDeliveryPosition(Map map, Pawn pawn)
        {
            if (map == null) return IntVec3.Invalid;
            if (TryFindDeliveryCell(map, out IntVec3 cell))
                return cell;
            return map.Center;
        }

        /// <summary>
        /// Determines the primary delivery method based on the counts of items delivered to lockers and drop pods, prioritizing lockers when available.
        /// </summary>
        /// <param name="result"></param>
        private static void DeterminePrimaryDeliveryMethod(DeliveryResult result)
        {
            if (result.LockerDeliveredCount > 0)
            {
                if (result.DropPodDeliveredCount > 0)
                {
                    result.PrimaryMethod = DeliveryMethod.DropPod; // mixed → show as drop pod (conservative letter)
                }
                else
                {
                    result.PrimaryMethod = DeliveryMethod.Locker;
                }
            }
            else if (result.DropPodDeliveredCount > 0)
            {
                result.PrimaryMethod = DeliveryMethod.DropPod;
            }
            else
            {
                // fallback (should rarely hit)
                result.PrimaryMethod = DeliveryMethod.DropPod;
            }
        }

        /// <summary>
        /// Finds a fallback delivery position when lockers are unavailable, prioritizing the first suitable locker if any items were delivered to lockers, and otherwise using the trade spot.
        /// </summary>
        /// <param name="map"></param>
        /// <param name="result"></param>
        /// <returns></returns>
        private static IntVec3 GetFallbackDeliveryPosition(Map map, DeliveryResult result)
        {
            // If we have locker items, use the first locker position
            if (result.LockerDeliveredItems.Count > 0)
            {
                var firstItem = result.LockerDeliveredItems.FirstOrDefault();
                var locker = FindSuitableLockerFor(firstItem, map, null);
                if (locker != null)
                {
                    // Logger.Debug($"Using first locker position as fallback: {locker.Position}");
                    return locker.Position;
                }
            }

            // Otherwise use trade spot
            IntVec3 tradeSpot = GetCustomDropSpot(map);
            // Logger.Debug($"Using trade spot as fallback: {tradeSpot}");
            return tradeSpot;
        }

        /// <summary>
        /// Creates a list of Thing instances for delivery, handling minifiable items and stackable items appropriately, and setting quality if applicable.
        /// </summary>
        /// <param name="thingDef"></param>
        /// <param name="quantity"></param>
        /// <param name="quality"></param>
        /// <param name="material"></param>
        /// <returns></returns>
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

            // Logger.Debug($"Created {things.Count} items (minified: {shouldMinify})");
            return things;
        }

        // FindPawnSpawnPosition removed — use TryFindDeliveryCell / TryDeliverGeneratedPawn.

        /// <summary>
        /// Validates whether a given position is suitable for drop pod delivery.
        /// </summary>
        /// <param name="pos"></param>
        /// <param name="map"></param>
        /// <param name="strict"></param>
        /// <returns></returns>
        public static bool IsValidDeliveryPosition(IntVec3 pos, Map map, bool strict = true)
        {
            if (map == null) return false;
            if (!pos.InBounds(map)) return false;

            if (strict)
            {
                if (pos.Fogged(map)) return false;
                if (!pos.Standable(map) && !GenGrid.Walkable(pos, map)) return false;

                Building edifice = pos.GetEdifice(map);
                if (edifice != null && edifice.def.passability == Traversability.Impassable && edifice.def.building.isNaturalRock)
                    return false;
            }
            else
            {
                // Last-resort mode: allow more cells
                if (!GenGrid.Walkable(pos, map)) return false;
            }

            return true;
        }

        // LogDropPodDetails removed — unused debug helper.

        /// <summary>
        /// Safe drop cell on this map. Thin wrapper over <see cref="TryFindDeliveryCell"/>.
        /// </summary>
        public static bool TryFindSafeDropPosition(Map map, out IntVec3 dropPos)
        {
            return TryFindDeliveryCell(map, out dropPos);
        }

        /// <summary>
        /// Specialized pawn delivery used by the general store system (!buy animal, !buy mech, etc.).
        /// For viewer pawn purchases (!pawn command) see BuyPawnCommandHandler → TryDeliverGeneratedPawn.
        /// </summary>
        /// <param name="pawnDef"></param>
        /// <param name="quantity"></param>
        /// <param name="quality"></param>
        /// <param name="material"></param>
        /// <param name="dropPos"></param>
        /// <param name="map"></param>
        /// <param name="viewerPawn"></param>
        /// <returns></returns>
        private static (bool success, IntVec3 spawnPosition) TryDeliverPawnFromStore(
    ThingDef pawnDef, int quantity, QualityCategory? quality, ThingDef material,
    IntVec3 dropPos, Map map, Pawn viewerPawn = null)
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

                // Prefer spawning near the nearest RimazonLocker if one exists
                Building_RimazonLocker nearestLocker = null;
                var allLockers = map.listerThings.AllThings
                    .OfType<Building_RimazonLocker>()
                    .Where(l => l.Spawned && l.Map == map && !l.Destroyed)
                    .ToList();

                if (allLockers.Any())
                {
                    nearestLocker = allLockers
                        .OrderBy(l => l.Position.DistanceToSquared(dropPos))
                        .First();

                    Logger.Debug($"Found nearest RimazonLocker at {nearestLocker.Position}");
                }

                if (nearestLocker != null)
                {
                    Logger.Debug($"Using nearest RimazonLocker at {nearestLocker.Position} as base for pawn spawn");
                    dropPos = nearestLocker.Position;
                }

                // Create all the pawns first
                List<Pawn> pawnsToDeliver = new List<Pawn>();

                for (int i = 0; i < quantity; i++)
                {
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

                    // Handle mechanoids and animals
                    if (pawn.RaceProps.IsMechanoid)
                    {
                        // TODO: ADD code to set controller to player pawn if mechinator.  DONT REMOVE THIS COMMENT UNTIL TODO IS DONE
                        Logger.Debug($"Detected mechanoid: {pawn.def.defName}, setting faction to player");
                        pawn.SetFaction(Faction.OfPlayer);
                    }
                    else if (pawn.RaceProps.Animal)
                    {
                        pawn.SetFaction(Faction.OfPlayer);
                        Logger.Debug($"Tamed animal: {pawn.Name}");
                    }

                    pawnsToDeliver.Add(pawn);
                    Logger.Debug($"Created pawn: {pawn.Name} ({pawn.def.defName})");
                }

                // === SPAWN PHASE (shared TryDeliverGeneratedPawn pipeline) ===
                if (pawnsToDeliver.Count > 0)
                {
                    foreach (var deliveredPawn in pawnsToDeliver)
                    {
                        if (TryDeliverGeneratedPawn(deliveredPawn, map, out IntVec3 pos))
                        {
                            spawnPosition = pos;
                            Logger.Debug($"Store pawn delivered {deliveredPawn.LabelShort} at {pos}");
                        }
                        else
                        {
                            Logger.Error($"Failed to deliver store pawn {deliveredPawn.LabelShort}");
                            return (false, spawnPosition);
                        }
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

        /// <summary>
        /// Attempts to deliver the given list of things via shuttle/drop pod at the specified position on the map.
        /// </summary>
        /// <param name="thingsToDeliver"></param>
        /// <param name="dropPos"></param>
        /// <param name="map"></param>
        /// <returns></returns>
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

                //LoggerDebug($"Calling DropPodUtility.DropThingsNear with {thingsToDeliver.Count} stacks at position {dropPos}");
                // LogDropPodDetails(thingsToDeliver, dropPos, map);

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

                //LoggerDebug($"Successfully called DropPodUtility for {thingsToDeliver.Count} items at {dropPos}");
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error($"Error in delivery at position {dropPos}: {ex}");
                return false;
            }
        }
        /// <summary>
        /// Determines if the given ThingDef should be minified for delivery.
        /// </summary>
        /// <param name="thingDef"></param>
        /// <returns></returns>
        public static bool ShouldMinifyForDelivery(ThingDef thingDef)
        {
            if (thingDef == null) return false;

            // All minifiable items should be minified for delivery
            if (thingDef.Minifiable)
            {
                //LoggerDebug($"{thingDef.defName} is minifiable - will be minified for delivery");
                return true;
            }

            //LoggerDebug($"{thingDef.defName} is not minifiable - will be delivered normally");
            return false;
        }
        /// <summary>
        /// Creates a minified version of the given thing, applying quality and material if applicable.
        /// </summary>
        /// <param name="thingDef"></param>
        /// <param name="quality"></param>
        /// <param name="material"></param>
        /// <returns></returns>
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
                    //LoggerDebug($"Successfully minified {thingDef.defName}");
                    return minifiedThing;
                }
                else
                {
                    //LoggerDebug($"Minification returned null for {thingDef.defName}, returning original");
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

        /// <summary>
        /// Creates a list of things for delivery, handling minification and stack sizes appropriately.
        /// </summary>
        /// <param name="thingDef"></param>
        /// <param name="quantity"></param>
        /// <param name="quality"></param>
        /// <param name="material"></param>
        /// <returns></returns>
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

        /// <summary>
        /// Determines if the given map is a space map.
        /// </summary>
        /// <param name="map"></param>
        /// <returns></returns>
        /// <remarks
        /// Why: Space maps are a distinct case from underground; pawns need vacsuits immediately.
        /// </remarks>
        public static bool IsSpaceMap(Map map)
        {
            if (map == null) return false;

            // Primary: BiomeDef.inVacuum flag (most accurate for Odyssey)
            if (map.Biome?.inVacuum == true)
                return true;

            // Secondary: Space / Orbit in biome name (current maps) for mod compatibility
            if (map.Biome?.defName?.Contains("Space", StringComparison.OrdinalIgnoreCase) == true ||
                map.Biome?.defName?.Contains("Orbit", StringComparison.OrdinalIgnoreCase) == true)
                return true;

            // Fallback: SpaceMapParent
            return map.Parent is SpaceMapParent;
        }
        /// <summary>
        /// Handles direct pawn interaction with pre-created items, such as equipping, wearing, or adding to inventory.
        /// </summary>
        /// <param name="item"></param>
        /// <param name="pawn"></param>
        /// <param name="equipItem"></param>
        /// <param name="wearItem"></param>
        /// <param name="addToInventory"></param>
        /// <param name="result"></param>
        /// <returns>DeliveryResult</returns>
        /// <remarks
        /// Is used when the item is already created and we want to directly interact with the pawn, bypassing lockers or drop pods.
        /// Generally used with unique type weapons to ensure we are charging the correct price and delivering the correct item to the pawn.
        /// </
        private static DeliveryResult HandleDirectPawnInteractionWithPreCreated(Thing item, Pawn pawn,
    bool equipItem, bool wearItem, bool addToInventory, DeliveryResult result)
        {
            result.DirectlyDeliveredItems.Add(item);

            if (equipItem && PawnItemHelper.EquipItemOnPawn(item, pawn))
                result.PrimaryMethod = DeliveryMethod.Equipped;
            else if (wearItem && PawnItemHelper.WearApparelOnPawn(item, pawn))
                result.PrimaryMethod = DeliveryMethod.Worn;
            else if (addToInventory && pawn.inventory.innerContainer.TryAdd(item))
                result.PrimaryMethod = DeliveryMethod.Inventory;
            else
            {
                // Fallback to locker
                TryDeliverToLocker(item, pawn.Map, pawn, result);
            }

            return result;
        }

        /// <summary>
        /// Handles regular item delivery with pre-created items, attempting locker delivery first, then drop pod as a fallback.
        /// This is used for unique type weapons and other items that are already created and need to be delivered to the pawn or colony.
        /// </summary>
        /// <param name="item"></param>
        /// <param name="pawn"></param>
        /// <param name="result"></param>
        /// <returns></returns>
        private static DeliveryResult HandleRegularDeliveryWithPreCreated(Thing item, Pawn pawn, DeliveryResult result)
        {
            Map map = GetTargetMapForDelivery(pawn);
            if (TryDeliverToLocker(item, map, pawn, result))
            {
                result.PrimaryMethod = DeliveryMethod.Locker;
            }
            else
            {
                // Drop pod fallback
                IntVec3 dropPos = GetDeliveryPosition(map, pawn);
                if (TryShuttleDelivery(new List<Thing> { item }, dropPos, map))
                {
                    result.DropPodDeliveredItems.Add(item);
                    result.PrimaryMethod = DeliveryMethod.DropPod;
                    result.DeliveryPosition = dropPos;
                }
            }
            return result;
        }

    }
}
