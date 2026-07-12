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
        /// <summary>Units requested for this delivery (stack counts).</summary>
        public int RequestedCount { get; set; } = 0;
        /// <summary>Units that could not be placed (locker full + map full). Culled / not charged.</summary>
        public int UndeliveredCount { get; set; } = 0;
        public IntVec3 DeliveryPosition { get; set; }
        public DeliveryMethod PrimaryMethod { get; set; }

        /// <summary>Units successfully placed (locker + map overflow + direct).</summary>
        public int TotalUnitsDelivered =>
            LockerDeliveredCount
            + DropPodDeliveredCount
            + (DirectlyDeliveredItems?.Sum(t => t?.stackCount ?? 0) ?? 0);

        public bool NothingDelivered => TotalUnitsDelivered <= 0;
        public bool PartiallyDelivered => TotalUnitsDelivered > 0 && UndeliveredCount > 0;
    }

    public enum DeliveryMethod
    {
        Locker,
        DropPod,
        Inventory,
        Equipped,
        Worn,
        PawnDelivery,
        /// <summary>Nothing landed (no space).</summary>
        Failed
    }
    public static class ItemDeliveryHelper
    {
        // ═══════════════════════════════════════════════════════════════════════
        // DELIVERY PIPELINE (single source of truth)
        //
        // Map / cell (shared):
        //   1) ResolveDeliveryMap — which map for pods/spawn (not for locker *search*)
        //   2) TryFindDeliveryCell — which cell on that map only
        //
        // HOW things arrive (do not mix these):
        //   • LOOSE ITEMS (not equip / wear / backpack):
        //       1) Rimazon lockers FIRST — FindSuitableLockerFor scans ALL loaded maps;
        //          preferredMap only sorts which accepting locker wins (no map gate).
        //          Accepts() still applies storage filters + space.
        //       2) Drop pod for leftovers. Sealed/pocket maps with free colonists keep the
        //          local map (vehicle interiors, Anomaly pockets). Surface home only if the
        //          sealed map has no free colonists.
        //       3) GenSpawn near colonist/center if pod fails on any sealed/pocket map.
        //       Entry: HandleRegularDelivery / HandleRegularDeliveryWithPreCreated
        //   • EQUIP / WEAR / BACKPACK:
        //       On pawn first → locker fallback (any accepting locker, all maps) if that fails
        //   • PAWNS (!pawn viewer colonist, !buy animals/mechs):
        //       Drop pod first → GenSpawn near locker/colonist/center (position only;
        //       never put living pawns inside lockers). Mechs may assign overseer after spawn.
        //       Entry: TryDeliverGeneratedPawn
        //
        // Sealed / no-sky maps (IsUndergroundMap / IsSealedOrPocketMap):
        //   map.IsPocketMap | PocketMapParent | high % any thick roof | natural thick rock.
        //   Covers VIM vehicle interiors (custom non-natural thick roof) without third-party types.
        // ═══════════════════════════════════════════════════════════════════════

        private static int _undergroundCacheTick = -1;
        private static readonly Dictionary<int, bool> _undergroundByMapId = new Dictionary<int, bool>();

        /// <summary>
        /// Chooses the map for any RICS delivery (items, !pawn, store animals/mechs).
        /// Priority: anchor pawn → CurrentMap → player home → any map with colonists/lockers/player pawns.
        /// Sealed/pocket maps with free colonists stay local (e.g. RV interior). Surface redirect
        /// only when the sealed map has no free colonists and a non-sealed home exists.
        /// </summary>
        public static Map ResolveDeliveryMap(Pawn anchorPawn = null, bool allowUndergroundRedirect = true)
        {
            try
            {
                Map candidate = null;
                string reason = "none";

                if (anchorPawn != null && !anchorPawn.Destroyed && anchorPawn.Spawned && anchorPawn.Map != null)
                {
                    candidate = anchorPawn.Map;
                    reason = $"anchorPawn={anchorPawn.LabelShort}";
                }

                if (candidate == null && Find.CurrentMap != null)
                {
                    candidate = Find.CurrentMap;
                    reason = "CurrentMap";
                }

                if (candidate == null)
                {
                    candidate = Find.Maps?.FirstOrDefault(m => m != null && m.IsPlayerHome);
                    if (candidate != null) reason = "IsPlayerHome";
                }

                // Nomadic / caravan / pocket: no formal home, but colony is on a map
                if (candidate == null)
                {
                    candidate = Find.Maps?.FirstOrDefault(m =>
                        m != null && m.mapPawns?.FreeColonistsSpawned?.Count > 0);
                    if (candidate != null) reason = "FreeColonistsSpawned";
                }

                if (candidate == null)
                {
                    candidate = Find.Maps?.FirstOrDefault(m =>
                        m != null &&
                        m.listerThings?.AllThings?.OfType<Building_RimazonLocker>().Any(l => l.Spawned) == true);
                    if (candidate != null) reason = "hasRimazonLocker";
                }

                if (candidate == null)
                {
                    candidate = Find.Maps?.FirstOrDefault(m =>
                        m != null &&
                        m.mapPawns?.AllPawnsSpawned?.Any(p => p.Faction == Faction.OfPlayer && !p.Dead) == true);
                    if (candidate != null) reason = "playerFactionPawns";
                }

                if (candidate == null)
                {
                    LogMapSnapshot("ResolveDeliveryMap: no suitable map");
                    return null;
                }

                bool sealedMap = IsSealedOrPocketMap(candidate);
                Map surfaceHome = GetSurfaceHomeMap();
                bool preferSealedLocal = ShouldPreferSealedLocalMap(candidate, reason);

                if (allowUndergroundRedirect && sealedMap)
                {
                    // Stay on RV interior / pocket when the player is there or colony is present.
                    // FreeColonistsSpawned alone is too strict (empty-looking interiors while CurrentMap
                    // is the pocket → was wrongly redirecting to surface Colony home).
                    if (preferSealedLocal)
                    {
                        Logger.Debug(
                            $"ResolveDeliveryMap: sealed local preferred — keeping " +
                            $"{DescribeMap(candidate)} (reason={reason}, " +
                            $"freeCol={MapFreeColonistCount(candidate)}, " +
                            $"playerPawns={MapPlayerPawnCount(candidate)}, current={candidate == Find.CurrentMap})");
                        return candidate;
                    }

                    if (surfaceHome != null && surfaceHome != candidate)
                    {
                        Logger.Debug(
                            $"ResolveDeliveryMap: sealed empty → surface home " +
                            $"{DescribeMap(surfaceHome)} (from {DescribeMap(candidate)}, reason={reason})");
                        return surfaceHome;
                    }

                    Logger.Debug(
                        $"ResolveDeliveryMap: NOMADIC/POCKET — no surface home; " +
                        $"keeping {DescribeMap(candidate)} (reason={reason}, sealed=true)");
                    return candidate;
                }

                Logger.Debug(
                    $"ResolveDeliveryMap: {DescribeMap(candidate)} " +
                    $"(reason={reason}, home={candidate.IsPlayerHome}, sealed={sealedMap}, " +
                    $"surfaceHome={(surfaceHome != null ? DescribeMap(surfaceHome) : "none")})");
                return candidate;
            }
            catch (Exception ex)
            {
                Logger.Error($"ResolveDeliveryMap failed: {ex.Message}");
                return Find.Maps?.FirstOrDefault(m => m != null && m.IsPlayerHome)
                    ?? Find.CurrentMap
                    ?? Find.Maps?.FirstOrDefault(m => m != null);
            }
        }

        /// <summary>Short map label for logs.</summary>
        public static string DescribeMap(Map map)
        {
            if (map == null) return "null";
            string label = map.Parent?.LabelCap ?? map.ToString();
            bool pocket = false;
            try { pocket = map.IsPocketMap; } catch { /* older API edge */ }
            return $"{label}[home={map.IsPlayerHome}, pocket={pocket}, size={map.Size.x}x{map.Size.z}]";
        }

        /// <summary>Dump all loaded maps (debug aid for nomadic / multi-map).</summary>
        public static void LogMapSnapshot(string prefix = "Maps")
        {
            if (Find.Maps == null || Find.Maps.Count == 0)
            {
                Logger.Debug($"{prefix}: no maps loaded");
                return;
            }

            foreach (Map m in Find.Maps)
            {
                if (m == null) continue;
                int colonists = m.mapPawns?.FreeColonistsSpawned?.Count ?? 0;
                int lockers = m.listerThings?.AllThings?.OfType<Building_RimazonLocker>().Count(l => l.Spawned) ?? 0;
                Logger.Debug(
                    $"{prefix}: {DescribeMap(m)} sealed={IsSealedOrPocketMap(m)} " +
                    $"colonists={colonists} lockers={lockers} current={m == Find.CurrentMap}");
            }
        }

        /// <summary>
        /// Map used for drop pods after locker miss.
        /// Sealed local preferred when colony is present or player is viewing that map.
        /// </summary>
        public static Map GetDropMapForItems(Map preferredLocal)
        {
            Map surface = GetSurfaceHomeMap();

            if (preferredLocal != null && IsSealedOrPocketMap(preferredLocal))
            {
                if (ShouldPreferSealedLocalMap(preferredLocal, reason: "dropPreferred"))
                {
                    Logger.Debug(
                        $"GetDropMapForItems: sealed local preferred — keep {DescribeMap(preferredLocal)}");
                    return preferredLocal;
                }

                if (surface != null && surface != preferredLocal)
                {
                    Logger.Debug(
                        $"GetDropMapForItems: sealed empty → surface {DescribeMap(surface)}");
                    return surface;
                }

                Logger.Debug($"GetDropMapForItems: NOMADIC sealed drop on {DescribeMap(preferredLocal)}");
                return preferredLocal;
            }

            Map map = preferredLocal ?? surface ?? Find.CurrentMap
                ?? Find.Maps?.FirstOrDefault(m => m != null && m.mapPawns?.FreeColonistsSpawned?.Count > 0)
                ?? Find.Maps?.FirstOrDefault(m => m != null);

            return map;
        }

        private static bool MapHasFreeColonists(Map map)
        {
            return MapFreeColonistCount(map) > 0;
        }

        private static int MapFreeColonistCount(Map map)
        {
            return map?.mapPawns?.FreeColonistsSpawned?.Count ?? 0;
        }

        private static int MapPlayerPawnCount(Map map)
        {
            if (map?.mapPawns?.AllPawnsSpawned == null) return 0;
            return map.mapPawns.AllPawnsSpawned.Count(p =>
                p != null && !p.Dead && p.Faction == Faction.OfPlayer);
        }

        /// <summary>
        /// Whether a sealed/pocket map should keep deliveries instead of redirecting to surface home.
        /// CurrentMap alone is enough (streamer is inside the RV). FreeColonists is unreliable alone
        /// on some vehicle interiors.
        /// </summary>
        private static bool ShouldPreferSealedLocalMap(Map map, string reason)
        {
            if (map == null) return false;

            // Player is viewing this map (inside RV / pocket)
            if (Find.CurrentMap == map)
                return true;

            // Candidate chosen because of who's on the map
            if (!string.IsNullOrEmpty(reason) &&
                (reason.StartsWith("CurrentMap", StringComparison.Ordinal) ||
                 reason.StartsWith("anchorPawn", StringComparison.Ordinal) ||
                 reason.StartsWith("FreeColonistsSpawned", StringComparison.Ordinal) ||
                 reason.StartsWith("playerFactionPawns", StringComparison.Ordinal)))
                return true;

            if (MapFreeColonistCount(map) > 0)
                return true;

            if (MapPlayerPawnCount(map) > 0)
                return true;

            return false;
        }

        /// <summary>Structured log after an item delivery completes.</summary>
        public static void LogItemSpawnResult(
            ThingDef thingDef, int quantity, DeliveryResult result, string path, Pawn forPawn = null)
        {
            try
            {
                string defName = thingDef?.defName ?? "null";
                string method = result?.PrimaryMethod.ToString() ?? "null";
                string pos = result?.DeliveryPosition.IsValid == true
                    ? result.DeliveryPosition.ToString()
                    : "invalid";
                int locker = result?.LockerDeliveredCount ?? 0;
                int pod = result?.DropPodDeliveredCount ?? 0;
                int direct = result?.DirectlyDeliveredItems?.Sum(t => t?.stackCount ?? 0) ?? 0;

                Logger.Debug(
                    $"[ItemSpawn] path={path} def={defName} qty={quantity} method={method} " +
                    $"pos={pos} locker={locker} pod={pod} direct={direct} " +
                    $"forPawn={(forPawn?.LabelShort ?? "none")} " +
                    $"pawnMap={(forPawn?.Map != null ? DescribeMap(forPawn.Map) : "none")}");

                if (result != null && locker == 0 && pod == 0 && direct == 0)
                {
                    Logger.Warning(
                        $"[ItemSpawn] NOTHING delivered for {defName} x{quantity} via {path} — check maps/lockers");
                    LogMapSnapshot("[ItemSpawn maps]");
                }
            }
            catch (Exception ex)
            {
                Logger.Debug($"LogItemSpawnResult failed: {ex.Message}");
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
        /// Places an already-generated pawn on the map.
        /// Open-sky maps: drop pod preferred (only success if pawn ends up Spawned).
        /// Sealed/pocket/tiny interiors: skip pods (they fail out-of-bounds) → GenSpawn near
        /// locker / colonist / standable cell / center.
        /// Equips vacsuit on space/vacuum maps before placement.
        /// </summary>
        public static bool TryDeliverGeneratedPawn(Pawn pawn, Map map, out IntVec3 deliveryPosition)
        {
            deliveryPosition = IntVec3.Invalid;
            if (pawn == null || map == null) return false;

            try
            {
                // Only real vacuum/space maps — never vehicle interiors named *Space* (e.g. Furia_InteriorSpace)
                if (IsSpaceMap(map))
                    EquipVacsuitIfNeeded(pawn);

                bool sealedMap = IsSealedOrPocketMap(map);
                bool tinyMap = map.Size.x < 40 || map.Size.z < 40;

                // Priority 1: drop pod — only on open-sky maps large enough for pods
                if (!sealedMap && !tinyMap && TryFindDeliveryCell(map, out IntVec3 safePos))
                {
                    try
                    {
                        DropPodUtility.DropThingsNear(
                            safePos, map, new List<Thing> { pawn },
                            openDelay: 110, leaveSlag: false, canRoofPunch: true, forbid: true);
                    }
                    catch (Exception podEx)
                    {
                        Logger.Warning($"TryDeliverGeneratedPawn: DropThingsNear threw: {podEx.Message}");
                    }

                    // DropThingsNear can "succeed" while spawning DropPodIncoming at (-1000,-1000,-1000)
                    // and leaving the pawn unspawned — never treat that as delivery.
                    if (IsPawnDeliveredOnMap(pawn, map))
                    {
                        deliveryPosition = pawn.Position.IsValid ? pawn.Position : safePos;
                        Logger.Debug(
                            $"TryDeliverGeneratedPawn: drop pod OK at {deliveryPosition} on {DescribeMap(map)}");
                        return true;
                    }

                    Logger.Warning(
                        $"TryDeliverGeneratedPawn: drop pod did not place pawn on {DescribeMap(map)} " +
                        $"(requested {safePos}) — falling back to GenSpawn");
                }
                else if (sealedMap || tinyMap)
                {
                    Logger.Debug(
                        $"TryDeliverGeneratedPawn: skip drop pod on " +
                        $"{(sealedMap ? "sealed/pocket" : "tiny")} {DescribeMap(map)}");
                }

                // Priority 2–4: direct GenSpawn (required for vehicle interiors / sealed maps)
                if (TryGenSpawnPawnOnMap(pawn, map, out deliveryPosition))
                    return true;

                Logger.Error($"TryDeliverGeneratedPawn: all strategies failed on {DescribeMap(map)}");
                return false;
            }
            catch (Exception ex)
            {
                Logger.Error($"TryDeliverGeneratedPawn failed: {ex}");
                deliveryPosition = map?.Center ?? IntVec3.Invalid;
                return false;
            }
        }

        /// <summary>True if pawn is alive and currently spawned on the given map.</summary>
        private static bool IsPawnDeliveredOnMap(Pawn pawn, Map map)
        {
            return pawn != null && map != null && !pawn.Destroyed && pawn.Spawned && pawn.Map == map;
        }

        /// <summary>
        /// Direct placement for sealed/pocket maps or when drop pods fail.
        /// Order: near locker → near free colonist → near any player pawn →
        /// standable near map center → map center.
        /// </summary>
        private static bool TryGenSpawnPawnOnMap(Pawn pawn, Map map, out IntVec3 deliveryPosition)
        {
            deliveryPosition = IntVec3.Invalid;
            if (pawn == null || map == null) return false;

            // Already delivered (e.g. pod opened same tick in some edge cases)
            if (IsPawnDeliveredOnMap(pawn, map))
            {
                deliveryPosition = pawn.Position;
                return true;
            }

            // Must not already be held by a broken drop pod / container
            if (pawn.Spawned)
            {
                try { pawn.DeSpawn(); }
                catch (Exception ex)
                {
                    Logger.Warning($"TryGenSpawnPawnOnMap: DeSpawn before re-place: {ex.Message}");
                }
            }

            Predicate<IntVec3> standable = c =>
                c.InBounds(map) && c.Standable(map) && c.Walkable(map) && !c.Fogged(map);

            // Near locker
            var locker = map.listerThings?.AllThings?
                .OfType<Building_RimazonLocker>()
                .FirstOrDefault(l => l.Spawned && !l.Destroyed);
            if (locker != null &&
                CellFinder.TryFindRandomCellNear(locker.Position, map, 6, standable, out var nearLocker))
            {
                if (TrySpawnPawnAt(pawn, nearLocker, map, out deliveryPosition, "locker"))
                    return true;
            }

            // Near free colonist
            var colonist = map.mapPawns?.FreeColonistsSpawned?.FirstOrDefault();
            if (colonist != null &&
                CellFinder.TryFindRandomCellNear(colonist.Position, map, 8, standable, out var nearCol))
            {
                if (TrySpawnPawnAt(pawn, nearCol, map, out deliveryPosition, "colonist"))
                    return true;
            }

            // Near any player pawn
            var anyPlayer = map.mapPawns?.AllPawnsSpawned?
                .FirstOrDefault(p => p != pawn && p.Faction == Faction.OfPlayer && p.Spawned && !p.Dead);
            if (anyPlayer != null &&
                CellFinder.TryFindRandomCellNear(anyPlayer.Position, map, 8, standable, out var nearAny))
            {
                if (TrySpawnPawnAt(pawn, nearAny, map, out deliveryPosition, "playerPawn"))
                    return true;
            }

            // Any standable cell (small interiors: center may be wall)
            if (CellFinder.TryFindRandomCell(map, standable, out var randomStandable))
            {
                if (TrySpawnPawnAt(pawn, randomStandable, map, out deliveryPosition, "randomStandable"))
                    return true;
            }

            // Last resort: map center even if imperfect
            if (TrySpawnPawnAt(pawn, map.Center, map, out deliveryPosition, "center"))
                return true;

            return false;
        }

        private static bool TrySpawnPawnAt(
            Pawn pawn, IntVec3 cell, Map map, out IntVec3 deliveryPosition, string via)
        {
            deliveryPosition = IntVec3.Invalid;
            try
            {
                if (!cell.InBounds(map))
                    return false;

                // Nudge to standable if needed
                if (!cell.Standable(map) || !cell.Walkable(map))
                {
                    if (!CellFinder.TryFindRandomCellNear(cell, map, 10,
                            c => c.InBounds(map) && c.Standable(map) && c.Walkable(map), out cell))
                        return false;
                }

                GenSpawn.Spawn(pawn, cell, map);
                if (!IsPawnDeliveredOnMap(pawn, map))
                {
                    Logger.Warning(
                        $"TrySpawnPawnAt: GenSpawn reported but pawn not on map at {cell} via {via}");
                    return false;
                }

                deliveryPosition = pawn.Position;
                Logger.Debug(
                    $"TryDeliverGeneratedPawn: GenSpawn via {via} at {deliveryPosition} on {DescribeMap(map)}");
                return true;
            }
            catch (Exception ex)
            {
                Logger.Warning($"TrySpawnPawnAt failed via {via} at {cell}: {ex.Message}");
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

        /// <summary>
        /// Finds any spawned Rimazon locker that <see cref="Building_RimazonLocker.Accepts"/> the thing.
        /// Searches <b>all loaded maps</b> (pocket, surface, caravan maps, etc.) — not limited to one map.
        /// <paramref name="preferredMap"/> only affects ordering (prefer lockers on that map first).
        /// Locker storage filters still apply (player can block food etc.).
        /// </summary>
        public static Building_RimazonLocker FindSuitableLockerFor(Thing thing, Map preferredMap = null, Pawn forPawn = null)
        {
            try
            {
                if (thing == null || thing.Destroyed) return null;

                // Collect EVERY locker on every map
                var allLockers = new List<Building_RimazonLocker>();
                if (Find.Maps != null)
                {
                    foreach (Map m in Find.Maps)
                    {
                        if (m?.listerThings?.AllThings == null) continue;
                        foreach (Thing t in m.listerThings.AllThings)
                        {
                            if (t is Building_RimazonLocker locker && locker.Spawned && !locker.Destroyed)
                                allLockers.Add(locker);
                        }
                    }
                }

                if (allLockers.Count == 0)
                {
                    Logger.Debug($"FindSuitableLocker: no Rimazon lockers on any of {Find.Maps?.Count ?? 0} map(s)");
                    return null;
                }

                var suitable = new List<Building_RimazonLocker>();
                foreach (var locker in allLockers)
                {
                    try
                    {
                        if (locker.Accepts(thing))
                            suitable.Add(locker);
                    }
                    catch (Exception ex)
                    {
                        Logger.Debug($"FindSuitableLocker: Accepts threw on locker at {locker.Position}: {ex.Message}");
                    }
                }

                if (suitable.Count == 0)
                {
                    // Help diagnose filter/space rejects
                    var sample = allLockers.Take(3).Select(l =>
                    {
                        bool filterOk = false;
                        try { filterOk = l.settings?.AllowedToAccept(thing) ?? false; } catch { /* ignore */ }
                        return $"{l.Map?.Parent?.LabelCap ?? "?"}@{l.Position} filter={filterOk} stacks={l.InnerContainer?.Count ?? 0}/{l.MaxStacks}";
                    });
                    Logger.Debug(
                        $"FindSuitableLocker: 0/{allLockers.Count} lockers accept {thing.def.defName} x{thing.stackCount}. " +
                        $"Sample: {string.Join("; ", sample)}");
                    return null;
                }

                Map orderMap = preferredMap
                    ?? (forPawn != null && forPawn.Spawned ? forPawn.Map : null)
                    ?? Find.CurrentMap;

                // Prefer: same map as streamer/preferred → already has this stack → most free slots → nearest on same map
                Building_RimazonLocker best = suitable
                    .OrderBy(l => orderMap != null && l.Map == orderMap ? 0 : 1)
                    .ThenBy(l => l.InnerContainer.Any(t => t != null && t.def == thing.def && t.CanStackWith(thing)) ? 0 : 1)
                    .ThenByDescending(l => l.MaxStacks - (l.InnerContainer?.Count ?? 0))
                    .ThenBy(l =>
                    {
                        if (forPawn != null && forPawn.Spawned && forPawn.Map == l.Map)
                            return l.Position.DistanceToSquared(forPawn.Position);
                        return 0;
                    })
                    .First();

                Logger.Debug(
                    $"FindSuitableLocker: chose locker on {best.Map?.Parent?.LabelCap ?? "?"} " +
                    $"at {best.Position} for {thing.def.defName} x{thing.stackCount} " +
                    $"(candidates={suitable.Count}/{allLockers.Count})");

                return best;
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
        /// Sealed / no-sky map: vanilla pocket maps, PocketMapParent, or high thick-roof coverage
        /// (any thick roof — not only natural rock). Covers Anomaly pockets and vehicle interiors
        /// (e.g. VIM/RVwithPD custom ceilings) without third-party type refs.
        /// Cached per map for the current game tick (roof scan is expensive).
        /// Alias of <see cref="IsSealedOrPocketMap"/>.
        /// </summary>
        public static bool IsUndergroundMap(Map map) => IsSealedOrPocketMap(map);

        /// <summary>
        /// Preferred name for sealed/no-sky detection used by delivery policy.
        /// </summary>
        public static bool IsSealedOrPocketMap(Map map)
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

            bool result = ComputeIsSealedOrPocketMap(map);
            _undergroundByMapId[id] = result;
            return result;
        }

        private static bool ComputeIsSealedOrPocketMap(Map map)
        {
            // Vanilla pocket maps (Anomaly, PocketMapUtility vehicle interiors, etc.)
            try
            {
                if (map.IsPocketMap)
                    return true;
            }
            catch
            {
                // IsPocketMap missing on unexpected API — fall through to roof heuristics
            }

            if (map.Parent is PocketMapParent)
                return true;

            // Soft label match (some pocket parents report as "Pocket map" without typed Parent)
            string parentLabel = map.Parent?.LabelCap ?? map.Parent?.def?.defName ?? "";
            if (!string.IsNullOrEmpty(parentLabel) &&
                parentLabel.IndexOf("pocket", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;

            // Thick-roof scan: any thick roof (VIM uses non-natural custom ceilings) + natural rock
            int anyThickRoofCount = 0;
            int naturalThickRoofCount = 0;
            int totalCells = 0;

            foreach (IntVec3 cell in map.AllCells)
            {
                totalCells++;
                RoofDef roof = map.roofGrid?.RoofAt(cell);
                if (roof == null) continue;

                if (roof.isThickRoof)
                    anyThickRoofCount++;

                if (roof.isNatural && roof == RoofDefOf.RoofRockThick)
                    naturalThickRoofCount++;
            }

            if (totalCells == 0) return false;

            float anyThickPct = (float)anyThickRoofCount / totalCells;
            float naturalThickPct = (float)naturalThickRoofCount / totalCells;
            const float THRESHOLD = 0.92f;
            const float SMALL_MAP_THRESHOLD = 0.80f;

            if (anyThickPct > THRESHOLD || naturalThickPct > THRESHOLD)
                return true;

            // Vehicle interiors are often small (e.g. 13×9 / 21×13); lower threshold for small maps
            if (map.Size.x < 220 && map.Size.z < 220 &&
                (anyThickPct > SMALL_MAP_THRESHOLD || naturalThickPct > SMALL_MAP_THRESHOLD))
                return true;

            // Tiny fully enclosed interiors with any significant thick roof
            if ((map.Size.x < 40 || map.Size.z < 40) && anyThickPct > 0.5f)
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

            string path = "unknown";
            try
            {
                Logger.Debug(
                    $"[ItemSpawn] BEGIN def={thingDef?.defName ?? "null"} qty={quantity} " +
                    $"equip={equipItem} wear={wearItem} inv={addToInventory} " +
                    $"preCreated={preCreatedItem != null} " +
                    $"forPawn={(pawn?.LabelShort ?? "none")} " +
                    $"pawnMap={(pawn?.Map != null ? DescribeMap(pawn.Map) : "none")} " +
                    $"currentMap={(Find.CurrentMap != null ? DescribeMap(Find.CurrentMap) : "none")}");

                if (preCreatedItem != null)
                {
                    Logger.Debug($"[ItemSpawn] pre-created item: {preCreatedItem.def.defName}");

                    if (equipItem || wearItem || addToInventory)
                    {
                        path = "preCreated+direct";
                        result = HandleDirectPawnInteractionWithPreCreated(
                            preCreatedItem, pawn, equipItem, wearItem, addToInventory, result);
                    }
                    else
                    {
                        path = "preCreated+regular";
                        result = HandleRegularDeliveryWithPreCreated(preCreatedItem, pawn, result);
                    }

                    LogItemSpawnResult(thingDef, quantity, result, path, pawn);
                    return result;
                }

                // Living things (store animals / mechs): drop-pod first — never locker
                if (IsPawnThingDef(thingDef))
                {
                    path = "pawnDelivery";
                    result = HandlePawnDelivery(thingDef, quantity, quality, material, pawn);
                    LogItemSpawnResult(thingDef, quantity, result, path, pawn);
                    return result;
                }

                // Equip / wear / backpack: on-pawn first, locker only if that fails
                if (equipItem || wearItem || addToInventory)
                {
                    path = "directPawn";
                    result = HandleDirectPawnInteraction(
                        thingDef, quantity, quality, material, pawn, equipItem, wearItem, addToInventory);
                    LogItemSpawnResult(thingDef, quantity, result, path, pawn);
                    return result;
                }

                // Loose store items (!buy without equip/wear/backpack): LOCKER first, then drop pod
                path = "regularLoose";
                result = HandleRegularDelivery(thingDef, quantity, quality, material, pawn);
                LogItemSpawnResult(thingDef, quantity, result, path, pawn);
                return result;
            }
            catch (Exception ex)
            {
                Logger.Error($"[ItemSpawn] ERROR path={path} def={thingDef?.defName}: {ex}");
                LogMapSnapshot("[ItemSpawn error maps]");
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
        /// Loose item delivery (not equip/wear/backpack):
        /// 1) ANY accepting locker on ANY map (preferred map is only a soft priority)
        /// 2) Drop pod leftovers (surface if streamer map is underground)
        /// </summary>
        private static DeliveryResult HandleRegularDelivery(ThingDef thingDef, int quantity, QualityCategory? quality,
            ThingDef material, Pawn pawn)
        {
            var result = new DeliveryResult
            {
                DeliveryPosition = IntVec3.Invalid
            };

            ThingDef finalMaterial = material;
            if (thingDef.MadeFromStuff && finalMaterial == null)
                finalMaterial = GenStuff.RandomStuffFor(thingDef);

            List<Thing> itemsToDeliver = CreateItemsForDelivery(thingDef, quantity, quality, finalMaterial);
            result.RequestedCount = quantity;

            // Preferred map for ordering + map overflow only (locker search is all maps)
            Map preferredMap = ResolveDeliveryMap(pawn, allowUndergroundRedirect: false);
            Map surfaceMap = GetSurfaceHomeMap();
            bool sealedPreferred = preferredMap != null && IsSealedOrPocketMap(preferredMap);

            Logger.Debug(
                $"[ItemSpawn] regularLoose maps: preferred={DescribeMap(preferredMap)} " +
                $"surface={DescribeMap(surfaceMap)} sealed={sealedPreferred} " +
                $"loadedMaps={Find.Maps?.Count ?? 0}");

            if (sealedPreferred || preferredMap == null)
                LogMapSnapshot("[ItemSpawn regularLoose]");

            // ── STEP 1: lockers anywhere that Accepts the item ──
            var undeliveredItems = new List<Thing>();
            foreach (var item in itemsToDeliver)
            {
                if (!TryDeliverToLocker(item, preferredMap, pawn, result))
                    undeliveredItems.Add(item);
            }

            // ── STEP 2: leftovers → drop pod (open sky) or GenPlace (sealed/pocket) ──
            if (undeliveredItems.Count > 0)
            {
                int dropCount = undeliveredItems.Sum(t => t?.stackCount ?? 0);
                Map dropMap = GetDropMapForItems(preferredMap);

                if (dropMap == null)
                {
                    Logger.Error("[ItemSpawn] No map for overflow after locker miss — culling leftovers");
                    result.UndeliveredCount += CullUndeliveredThings(undeliveredItems);
                    LogMapSnapshot("[ItemSpawn no-drop-map]");
                    FinalizeRegularLooseResult(result, preferredMap, surfaceMap);
                    return result;
                }

                bool sealedDrop = IsSealedOrPocketMap(dropMap) || dropMap.Size.x < 40 || dropMap.Size.z < 40;
                Logger.Debug(
                    $"[ItemSpawn] locker filled={result.LockerDeliveredCount}, " +
                    $"{undeliveredItems.Count} stack(s)/{dropCount} units → " +
                    $"{(sealedDrop ? "GenPlace on sealed/pocket" : "drop pod")} " +
                    $"on {DescribeMap(dropMap)}");

                IntVec3 dropPos = GetDeliveryPosition(dropMap, pawn);
                var placedStacks = new List<Thing>();
                var failedStacks = new List<Thing>();

                // Sealed/tiny: never call DropThingsNear — pods fail at (-1000,-1000,-1000)
                if (sealedDrop)
                {
                    Logger.Debug(
                        $"[ItemSpawn] skip drop pod on sealed/tiny {DescribeMap(dropMap)} — GenPlace overflow");
                    TryDirectSpawnItemsNearColony(undeliveredItems, dropMap, out dropPos, placedStacks, failedStacks);
                }
                else
                {
                    bool podOk = TryShuttleDelivery(undeliveredItems, dropPos, dropMap);
                    if (podOk)
                    {
                        // Pod accepted stacks (held in pod container) — treat all non-destroyed as placed
                        foreach (var t in undeliveredItems)
                        {
                            if (t != null && !t.Destroyed && (t.Spawned || t.ParentHolder != null))
                                placedStacks.Add(t);
                            else if (t != null && !t.Destroyed)
                                failedStacks.Add(t);
                        }
                    }
                    else
                    {
                        Logger.Warning(
                            $"[ItemSpawn] Drop pod failed on {DescribeMap(dropMap)} — GenPlace fallback");
                        TryDirectSpawnItemsNearColony(undeliveredItems, dropMap, out dropPos, placedStacks, failedStacks);
                    }
                }

                if (placedStacks.Count > 0)
                {
                    result.DropPodDeliveredItems.AddRange(placedStacks);
                    result.DropPodDeliveredCount += placedStacks.Sum(t => t.stackCount);
                    result.DeliveryPosition = dropPos;
                    Logger.Debug(
                        $"[ItemSpawn] map delivery OK at {dropPos} on {DescribeMap(dropMap)} " +
                        $"(overflow stacks={placedStacks.Count} placed, {failedStacks.Count} culled)");
                }

                if (failedStacks.Count > 0)
                {
                    int culled = CullUndeliveredThings(failedStacks);
                    result.UndeliveredCount += culled;
                    Logger.Warning(
                        $"[ItemSpawn] no space for {culled} units — culled (locker full + map full)");
                    LogMapSnapshot("[ItemSpawn deliver-partial-or-fail]");
                }
            }
            else
            {
                Logger.Debug($"[ItemSpawn] all {result.LockerDeliveredCount} units accepted by locker(s)");
            }

            FinalizeRegularLooseResult(result, preferredMap, surfaceMap);
            return result;
        }

        private static void FinalizeRegularLooseResult(DeliveryResult result, Map preferredMap, Map surfaceMap)
        {
            DeterminePrimaryDeliveryMethod(result);

            if (result.DeliveryPosition == IntVec3.Invalid || result.DeliveryPosition == default(IntVec3))
            {
                Map fallbackMap = preferredMap ?? surfaceMap ?? Find.CurrentMap;
                if (fallbackMap != null)
                    result.DeliveryPosition = GetFallbackDeliveryPosition(fallbackMap, result);
            }

            // Sanity: undelivered should match request − delivered when we tracked culls
            int delivered = result.TotalUnitsDelivered;
            if (result.RequestedCount > 0 && result.UndeliveredCount == 0 && delivered < result.RequestedCount)
                result.UndeliveredCount = result.RequestedCount - delivered;

            Logger.Debug(
                $"[ItemSpawn] regularLoose DONE method={result.PrimaryMethod} pos={result.DeliveryPosition} " +
                $"locker={result.LockerDeliveredCount} map={result.DropPodDeliveredCount} " +
                $"undelivered={result.UndeliveredCount} requested={result.RequestedCount}");
        }

        /// <summary>Destroy unspawned leftover stacks so they are not charged or leaked.</summary>
        private static int CullUndeliveredThings(IEnumerable<Thing> things)
        {
            int units = 0;
            if (things == null) return 0;
            foreach (Thing t in things)
            {
                if (t == null || t.Destroyed) continue;
                units += t.stackCount;
                try
                {
                    if (t.Spawned)
                        t.Destroy(DestroyMode.Vanish);
                    else
                        t.Destroy(DestroyMode.Vanish);
                }
                catch (Exception ex)
                {
                    Logger.Warning($"[ItemSpawn] cull failed for {t.def?.defName}: {ex.Message}");
                }
            }
            return units;
        }

        /// <summary>
        /// Place loose stacks on the map when drop pods cannot land (sealed/pocket/tiny maps).
        /// Fills <paramref name="placed"/> / <paramref name="failed"/>; does not destroy failed stacks
        /// (caller culls so purchase can charge only delivered units).
        /// </summary>
        private static bool TryDirectSpawnItemsNearColony(
            List<Thing> items,
            Map map,
            out IntVec3 spawnPos,
            List<Thing> placed = null,
            List<Thing> failed = null)
        {
            spawnPos = IntVec3.Invalid;
            if (map == null || items == null || items.Count == 0) return false;

            placed = placed ?? new List<Thing>();
            failed = failed ?? new List<Thing>();

            try
            {
                Predicate<IntVec3> standable = c =>
                    c.InBounds(map) && c.Standable(map) && c.Walkable(map) && !c.Fogged(map);

                // Anchor: near locker → colonist → player pawn → delivery cell → center
                var locker = map.listerThings?.AllThings?
                    .OfType<Building_RimazonLocker>()
                    .FirstOrDefault(l => l.Spawned && !l.Destroyed);
                if (locker != null &&
                    CellFinder.TryFindRandomCellNear(locker.Position, map, 6, standable, out var nearLocker))
                    spawnPos = nearLocker;

                if (!spawnPos.IsValid)
                {
                    var colonist = map.mapPawns?.FreeColonistsSpawned?.FirstOrDefault();
                    if (colonist != null &&
                        CellFinder.TryFindRandomCellNear(colonist.Position, map, 8, standable, out var nearCol))
                        spawnPos = nearCol;
                }

                if (!spawnPos.IsValid)
                {
                    var anyPlayer = map.mapPawns?.AllPawnsSpawned?
                        .FirstOrDefault(p => p.Faction == Faction.OfPlayer && p.Spawned && !p.Dead);
                    if (anyPlayer != null &&
                        CellFinder.TryFindRandomCellNear(anyPlayer.Position, map, 8, standable, out var nearAny))
                        spawnPos = nearAny;
                }

                if (!spawnPos.IsValid && TryFindDeliveryCell(map, out var cell))
                    spawnPos = cell;

                if (!spawnPos.IsValid)
                    spawnPos = map.Center;

                foreach (Thing item in items)
                {
                    if (item == null || item.Destroyed) continue;
                    if (item.Spawned)
                    {
                        placed.Add(item);
                        continue;
                    }

                    if (GenPlace.TryPlaceThing(item, spawnPos, map, ThingPlaceMode.Near))
                    {
                        placed.Add(item);
                        continue;
                    }

                    if (CellFinder.TryFindRandomCell(map, standable, out var alt) &&
                        GenPlace.TryPlaceThing(item, alt, map, ThingPlaceMode.Near))
                    {
                        placed.Add(item);
                        spawnPos = alt;
                        continue;
                    }

                    failed.Add(item);
                    Logger.Warning(
                        $"[ItemSpawn] GenPlace failed for {item.def.defName} x{item.stackCount} " +
                        $"on {DescribeMap(map)} at/near {spawnPos}");
                }

                Logger.Debug(
                    $"[ItemSpawn] GenPlace overflow placed {placed.Count}/{items.Count} stacks " +
                    $"(failed={failed.Count}) near {spawnPos} on {DescribeMap(map)}");
                return placed.Count > 0;
            }
            catch (Exception ex)
            {
                Logger.Error($"TryDirectSpawnItemsNearColony: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Attempts to deliver a single item to a suitable Rimazon locker on the map. If successful, updates the delivery result with the count and position.
        /// </summary>
        /// <param name="item"></param>
        /// <param name="map"></param>
        /// <param name="pawn"></param>
        /// <param name="result"></param>
        /// <returns></returns>
        /// <param name="preferredMap">Hint for which map's lockers to prefer; search is still all maps.</param>
        private static bool TryDeliverToLocker(Thing item, Map preferredMap, Pawn pawn, DeliveryResult result)
        {
            if (item == null || item.Destroyed)
            {
                Logger.Error("TryDeliverToLocker: Item is null or already destroyed before delivery attempt");
                return false;
            }

            // preferredMap is ordering only — FindSuitableLockerFor scans every map
            var locker = FindSuitableLockerFor(item, preferredMap, pawn);
            if (locker == null)
                return false;

            int attemptedCount = item.stackCount;  // Snapshot BEFORE possible merge/destroy
            Map lockerMap = locker.Map;

            bool accepted = locker.TryAcceptThing(item, allowSpecialEffects: false);

            if (accepted)
            {
                result.LockerDeliveredCount += attemptedCount;
                // Item may be destroyed if fully merged — still track for letters via count;
                // keep a live reference only if it survived as its own stack.
                if (!item.Destroyed)
                    result.LockerDeliveredItems.Add(item);

                if (result.DeliveryPosition == IntVec3.Invalid)
                    result.DeliveryPosition = locker.Position;

                Logger.Debug(
                    $"Locker accepted {item.def.defName} x{attemptedCount} on " +
                    $"{lockerMap?.Parent?.LabelCap ?? "?"} at {locker.Position}");
                return true;
            }

            Logger.Debug(
                $"Locker at {locker.Position} on {lockerMap?.Parent?.LabelCap} " +
                $"failed TryAcceptThing for {item.def.defName} x{attemptedCount} (Accepts was true)");
            return false;
        }

        /// <summary>
        /// Map for item/equip paths that need a place to stand: prefer local map (no auto surface redirect).
        /// Loose-item locker search uses this first so pocket lockers win over surface pods.
        /// </summary>
        private static Map GetTargetMapForDelivery(Pawn pawn)
        {
            return ResolveDeliveryMap(pawn, allowUndergroundRedirect: false);
        }

        /// <summary>
        /// Best non-sealed player-home map, if any (open-sky home for pod fallback).
        /// Returns null for pure nomadic / pocket-only play.
        /// </summary>
        private static Map GetSurfaceHomeMap()
        {
            return Find.Maps?.FirstOrDefault(m => m != null && m.IsPlayerHome && !IsSealedOrPocketMap(m));
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
            if (result.NothingDelivered)
            {
                result.PrimaryMethod = DeliveryMethod.Failed;
                return;
            }

            if (result.LockerDeliveredCount > 0)
            {
                if (result.DropPodDeliveredCount > 0)
                    result.PrimaryMethod = DeliveryMethod.DropPod; // mixed
                else
                    result.PrimaryMethod = DeliveryMethod.Locker;
            }
            else if (result.DropPodDeliveredCount > 0)
            {
                result.PrimaryMethod = DeliveryMethod.DropPod;
            }
            else if (result.DirectlyDeliveredItems?.Count > 0)
            {
                result.PrimaryMethod = DeliveryMethod.Inventory;
            }
            else
            {
                result.PrimaryMethod = DeliveryMethod.Failed;
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

                    // Colony ownership; mechanitor link attempted after spawn (needs IsColonyMech + map)
                    if (pawn.RaceProps.IsMechanoid || pawn.RaceProps.Animal)
                    {
                        pawn.SetFaction(Faction.OfPlayer);
                        Logger.Debug(
                            pawn.RaceProps.IsMechanoid
                                ? $"Mechanoid {pawn.def.defName}: faction → player (overseer assign after spawn)"
                                : $"Tamed animal: {pawn.Name}");
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

                            // Mechanoids: try bind to viewer's pawn if they are a mechanitor with bandwidth
                            if (deliveredPawn.RaceProps.IsMechanoid)
                                TryAssignMechToViewer(deliveredPawn, viewerPawn);
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
        /// After a store-bought mech spawns as a colony mech, try to assign it to the viewer's pawn.
        /// Requires Biotech, Mechlink (IsMechanitor), free bandwidth, and a controllable mech.
        /// Failure is non-fatal: mech still exists as an uncontrolled colony mechanoid (chaos is OK).
        /// Vanilla path matches gestator / quest: Overseer relation (+ control group).
        /// </summary>
        private static bool TryAssignMechToViewer(Pawn mech, Pawn viewerPawn)
        {
            try
            {
                if (!ModsConfig.BiotechActive)
                {
                    Logger.Debug("Mech assign: Biotech inactive — leave uncontrolled");
                    return false;
                }

                if (mech == null || mech.Destroyed || mech.Dead || !mech.RaceProps.IsMechanoid)
                    return false;

                if (viewerPawn == null || viewerPawn.Destroyed || viewerPawn.Dead)
                {
                    Logger.Debug("Mech assign: no viewer pawn — leave uncontrolled");
                    return false;
                }

                // Mechlink implant + mechanitor tracker
                if (!MechanitorUtility.IsMechanitor(viewerPawn))
                {
                    Logger.Debug(
                        $"Mech assign: {viewerPawn.LabelShort} is not a mechanitor " +
                        "(needs Mechlink implant) — leave uncontrolled");
                    return false;
                }

                if (mech.Faction != Faction.OfPlayer)
                    mech.SetFaction(Faction.OfPlayer);

                if (!MechanitorUtility.EverControllable(mech))
                {
                    Logger.Debug(
                        $"Mech assign: {mech.def.defName} has no OverseerSubject — leave uncontrolled");
                    return false;
                }

                // Full vanilla gate: colony mech, not already controlled, enough free bandwidth, etc.
                AcceptanceReport canControl = MechanitorUtility.CanControlMech(viewerPawn, mech);
                if (!canControl.Accepted)
                {
                    float cost = mech.GetStatValue(StatDefOf.BandwidthCost);
                    int freeBw = viewerPawn.mechanitor.TotalBandwidth - viewerPawn.mechanitor.UsedBandwidth;
                    Logger.Debug(
                        $"Mech assign: cannot assign {mech.LabelShort} to {viewerPawn.LabelShort}: " +
                        $"{canControl.Reason} (free BW {freeBw}, cost {cost:0}) — leave uncontrolled");
                    return false;
                }

                // Same as Bill_ProductionMech / QuestPart_AssignMechToMechanitor
                if (mech.GetOverseer() != viewerPawn)
                    viewerPawn.relations.AddDirectRelation(PawnRelationDefOf.Overseer, mech);

                // Ensure it lands in a control group (UI-equivalent)
                try
                {
                    viewerPawn.mechanitor.AssignPawnControlGroup(mech);
                }
                catch (Exception assignEx)
                {
                    Logger.Debug($"Mech assign: control group assign soft-failed: {assignEx.Message}");
                }

                viewerPawn.mechanitor.Notify_BandwidthChanged();

                Logger.Message(
                    $"Mech assign: {mech.LabelShortCap} overseen by {viewerPawn.LabelShort} " +
                    $"(BW {viewerPawn.mechanitor.UsedBandwidth}/{viewerPawn.mechanitor.TotalBandwidth})");
                return true;
            }
            catch (Exception ex)
            {
                Logger.Warning($"Mech assign failed (mech still spawned): {ex.Message}");
                return false;
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
                Logger.Debug(
                    $"Attempting delivery at position: {dropPos}, map: {map?.info?.parent?.Label ?? "null"}, " +
                    $"map size: {map?.Size}, in bounds: {(map != null && dropPos.InBounds(map))}");

                if (map == null)
                {
                    Logger.Error("Map is null for delivery");
                    return false;
                }

                // Drop pods cannot land on sealed/pocket/tiny interiors — caller should GenPlace instead
                if (IsSealedOrPocketMap(map) || map.Size.x < 40 || map.Size.z < 40)
                {
                    Logger.Debug($"TryShuttleDelivery: refused sealed/tiny {DescribeMap(map)}");
                    return false;
                }

                if (!dropPos.InBounds(map))
                {
                    Logger.Error($"Delivery position {dropPos} is out of map bounds (map size: {map.Size})");
                    return false;
                }

                if (thingsToDeliver == null || thingsToDeliver.Count == 0)
                    return false;

                DropPodUtility.DropThingsNear(
                    dropPos,
                    map,
                    thingsToDeliver,
                    openDelay: 110,
                    leaveSlag: false,
                    canRoofPunch: true,
                    forbid: false,
                    allowFogged: false
                );

                // DropThingsNear can "succeed" while spawning pods at (-1000,-1000,-1000).
                // If every stack is still unheld and unspawned, treat as failure.
                bool anyPlaced = thingsToDeliver.Any(t =>
                    t != null && !t.Destroyed && (t.Spawned || t.ParentHolder != null));
                if (!anyPlaced)
                {
                    Logger.Warning(
                        $"TryShuttleDelivery: DropThingsNear left 0 stacks placed on {DescribeMap(map)}");
                    return false;
                }

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
        /// True for real vacuum / orbital space maps (vacsuits needed).
        /// Not true for vehicle pocket interiors whose biome merely contains "Space"
        /// (e.g. VIM <c>Furia_InteriorSpace</c>).
        /// </summary>
        public static bool IsSpaceMap(Map map)
        {
            if (map == null) return false;

            // Odyssey / vacuum biomes — authoritative
            if (map.Biome?.inVacuum == true)
                return true;

            // Space map parent (orbital encounters, etc.)
            if (map.Parent is SpaceMapParent)
                return true;

            // Pocket / sealed interiors are never "space" unless biome is actually vacuum
            // (avoids Furia_InteriorSpace matching a naive "Space" substring check).
            if (IsSealedOrPocketMap(map))
                return false;

            string name = map.Biome?.defName;
            if (string.IsNullOrEmpty(name))
                return false;

            // Conservative name tokens only — not "InteriorSpace", "PersonalSpace", etc.
            if (name.Equals("Space", StringComparison.OrdinalIgnoreCase) ||
                name.StartsWith("Space_", StringComparison.OrdinalIgnoreCase) ||
                name.EndsWith("_Space", StringComparison.OrdinalIgnoreCase) ||
                name.IndexOf("Orbit", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;

            return false;
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
        /// <summary>
        /// Pre-created loose item: any accepting locker on any map → drop pod fallback.
        /// </summary>
        private static DeliveryResult HandleRegularDeliveryWithPreCreated(Thing item, Pawn pawn, DeliveryResult result)
        {
            Map preferredMap = ResolveDeliveryMap(pawn, allowUndergroundRedirect: false);
            Map surfaceMap = GetSurfaceHomeMap();
            bool sealedPreferred = preferredMap != null && IsSealedOrPocketMap(preferredMap);

            Logger.Debug(
                $"[ItemSpawn] preCreated maps preferred={DescribeMap(preferredMap)} " +
                $"surface={DescribeMap(surfaceMap)} sealed={sealedPreferred}");

            // Any map's locker that Accepts this item
            if (TryDeliverToLocker(item, preferredMap, pawn, result))
            {
                result.PrimaryMethod = DeliveryMethod.Locker;
                return result;
            }

            Map dropMap = GetDropMapForItems(preferredMap);
            if (dropMap == null)
            {
                Logger.Error("[ItemSpawn] No map for pre-created item drop-pod fallback");
                LogMapSnapshot("[ItemSpawn preCreated no-map]");
                return result;
            }

            result.RequestedCount = Math.Max(result.RequestedCount, item.stackCount);
            IntVec3 dropPos = GetDeliveryPosition(dropMap, pawn);
            var stack = new List<Thing> { item };
            var placed = new List<Thing>();
            var failed = new List<Thing>();
            bool sealedDrop = IsSealedOrPocketMap(dropMap) || dropMap.Size.x < 40 || dropMap.Size.z < 40;
            bool ok;

            if (sealedDrop)
            {
                Logger.Debug(
                    $"[ItemSpawn] preCreated skip drop pod on sealed/tiny {DescribeMap(dropMap)} — GenPlace");
                ok = TryDirectSpawnItemsNearColony(stack, dropMap, out dropPos, placed, failed);
            }
            else
            {
                ok = TryShuttleDelivery(stack, dropPos, dropMap);
                if (ok && item != null && !item.Destroyed && (item.Spawned || item.ParentHolder != null))
                    placed.Add(item);
                else if (!ok)
                    ok = TryDirectSpawnItemsNearColony(stack, dropMap, out dropPos, placed, failed);
            }

            if (ok && placed.Count > 0)
            {
                result.DropPodDeliveredItems.AddRange(placed);
                result.DropPodDeliveredCount += placed.Sum(t => t.stackCount);
                result.PrimaryMethod = DeliveryMethod.DropPod;
                result.DeliveryPosition = dropPos;
                Logger.Debug(
                    $"[ItemSpawn] preCreated {item.LabelCap} → map at {dropPos} on {DescribeMap(dropMap)}");
            }
            else
            {
                result.UndeliveredCount += CullUndeliveredThings(failed.Count > 0 ? failed : stack);
                result.PrimaryMethod = DeliveryMethod.Failed;
                Logger.Error($"[ItemSpawn] preCreated {item.def.defName} failed locker and map delivery — culled");
            }

            return result;
        }

    }
}
