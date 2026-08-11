// ItemDeliveryHelper.cs
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
// Item/pawn delivery: lockers, drop pods, map spawn, vacuum gear.
// Logger.Debug removed; logic recovered from last good build.
using System;
using System.Collections.Generic;
using System.Linq;
using CAP_ChatInteractive;
using RimWorld;
using RimWorld.Planet;
using Verse;
using UnityEngine;
using Logger = CAP_ChatInteractive.Logger;

namespace _CAP__Chat_Interactive.Command.CommandHelpers
{

public static class ItemDeliveryHelper
{
	private static int _undergroundCacheTick = -1;

	private static readonly Dictionary<int, bool> _undergroundByMapId = new Dictionary<int, bool>();

	public static Map ResolveDeliveryMap(Pawn anchorPawn = null, bool allowUndergroundRedirect = true)
	{
		try
		{
			Map map = null;
			string text = "none";
			if (anchorPawn != null && !anchorPawn.Destroyed && anchorPawn.Spawned && anchorPawn.Map != null)
			{
				map = anchorPawn.Map;
				text = "anchorPawn=" + anchorPawn.LabelShort;
			}
			if (map == null && Find.CurrentMap != null)
			{
				map = Find.CurrentMap;
				text = "CurrentMap";
			}
			if (map == null)
			{
				map = Find.Maps?.FirstOrDefault((Map m) => m?.IsPlayerHome ?? false);
				if (map != null)
				{
					text = "IsPlayerHome";
				}
			}
			if (map == null)
			{
				map = Find.Maps?.FirstOrDefault(delegate(Map m)
				{
					int result3;
					if (m != null)
					{
						MapPawns mapPawns2 = m.mapPawns;
						result3 = ((mapPawns2 != null && mapPawns2.FreeColonistsSpawned?.Count > 0) ? 1 : 0);
					}
					else
					{
						result3 = 0;
					}
					return (byte)result3 != 0;
				});
				if (map != null)
				{
					text = "FreeColonistsSpawned";
				}
			}
			if (map == null)
			{
				map = Find.Maps?.FirstOrDefault(delegate(Map m)
				{
					int result2;
					if (m != null)
					{
						ListerThings listerThings = m.listerThings;
						result2 = ((listerThings != null && (listerThings.AllThings?.OfType<Building_RimazonLocker>().Any((Building_RimazonLocker l) => l.Spawned)).GetValueOrDefault()) ? 1 : 0);
					}
					else
					{
						result2 = 0;
					}
					return (byte)result2 != 0;
				});
				if (map != null)
				{
					text = "hasRimazonLocker";
				}
			}
			if (map == null)
			{
				map = Find.Maps?.FirstOrDefault(delegate(Map m)
				{
					int result;
					if (m != null)
					{
						MapPawns mapPawns = m.mapPawns;
						result = ((mapPawns != null && (mapPawns.AllPawnsSpawned?.Any((Pawn p) => p.Faction == Faction.OfPlayer && !p.Dead)).GetValueOrDefault()) ? 1 : 0);
					}
					else
					{
						result = 0;
					}
					return (byte)result != 0;
				});
				if (map != null)
				{
					text = "playerFactionPawns";
				}
			}
			if (map == null)
			{
				LogMapSnapshot("ResolveDeliveryMap: no suitable map");
				return null;
			}
			bool flag = IsSealedOrPocketMap(map);
			Map surfaceHomeMap = GetSurfaceHomeMap();
			bool flag2 = ShouldPreferSealedLocalMap(map, text);
			if (allowUndergroundRedirect && flag)
			{
				if (flag2)
				{
					return map;
				}
				if (surfaceHomeMap != null && surfaceHomeMap != map)
				{
					return surfaceHomeMap;
				}
				return map;
			}
			return map;
		}
		catch (Exception ex)
		{
			Logger.Error("ResolveDeliveryMap failed: " + ex.Message);
			return Find.Maps?.FirstOrDefault((Map m) => m?.IsPlayerHome ?? false) ?? Find.CurrentMap ?? Find.Maps?.FirstOrDefault((Map m) => m != null);
		}
	}

	public static string DescribeMap(Map map)
	{
		if (map == null)
		{
			return "null";
		}
		string text = map.Parent?.LabelCap ?? map.ToString();
		bool flag = false;
		try
		{
			flag = map.IsPocketMap;
		}
		catch
		{
		}
		return $"{text}[home={map.IsPlayerHome}, pocket={flag}, size={map.Size.x}x{map.Size.z}]";
	}

	public static void LogMapSnapshot(string prefix = "Maps")
	{
		if (Find.Maps == null || Find.Maps.Count == 0)
		{
			return;
		}
		foreach (Map map in Find.Maps)
		{
			if (map != null)
			{
				int valueOrDefault = (map.mapPawns?.FreeColonistsSpawned?.Count).GetValueOrDefault();
				int valueOrDefault2 = (map.listerThings?.AllThings?.OfType<Building_RimazonLocker>().Count((Building_RimazonLocker l) => l.Spawned)).GetValueOrDefault();
			}
		}
	}

	public static Map GetDropMapForItems(Map preferredLocal)
	{
		Map surfaceHomeMap = GetSurfaceHomeMap();
		if (preferredLocal != null && IsSealedOrPocketMap(preferredLocal))
		{
			if (ShouldPreferSealedLocalMap(preferredLocal, "dropPreferred"))
			{
				return preferredLocal;
			}
			if (surfaceHomeMap != null && surfaceHomeMap != preferredLocal)
			{
				return surfaceHomeMap;
			}
			return preferredLocal;
		}
		return preferredLocal ?? surfaceHomeMap ?? Find.CurrentMap ?? Find.Maps?.FirstOrDefault(delegate(Map m)
		{
			int result;
			if (m != null)
			{
				MapPawns mapPawns = m.mapPawns;
				result = ((mapPawns != null && mapPawns.FreeColonistsSpawned?.Count > 0) ? 1 : 0);
			}
			else
			{
				result = 0;
			}
			return (byte)result != 0;
		}) ?? Find.Maps?.FirstOrDefault((Map m) => m != null);
	}

	private static bool MapHasFreeColonists(Map map)
	{
		return MapFreeColonistCount(map) > 0;
	}

	private static int MapFreeColonistCount(Map map)
	{
		return (map?.mapPawns?.FreeColonistsSpawned?.Count).GetValueOrDefault();
	}

	private static int MapPlayerPawnCount(Map map)
	{
		if (map?.mapPawns?.AllPawnsSpawned == null)
		{
			return 0;
		}
		return map.mapPawns.AllPawnsSpawned.Count((Pawn p) => p != null && !p.Dead && p.Faction == Faction.OfPlayer);
	}

	private static bool ShouldPreferSealedLocalMap(Map map, string reason)
	{
		if (map == null)
		{
			return false;
		}
		if (Find.CurrentMap == map)
		{
			return true;
		}
		if (!string.IsNullOrEmpty(reason) && (reason.StartsWith("CurrentMap", StringComparison.Ordinal) || reason.StartsWith("anchorPawn", StringComparison.Ordinal) || reason.StartsWith("FreeColonistsSpawned", StringComparison.Ordinal) || reason.StartsWith("playerFactionPawns", StringComparison.Ordinal)))
		{
			return true;
		}
		if (MapFreeColonistCount(map) > 0)
		{
			return true;
		}
		if (MapPlayerPawnCount(map) > 0)
		{
			return true;
		}
		return false;
	}

	public static void LogItemSpawnResult(ThingDef thingDef, int quantity, DeliveryResult result, string path, Pawn forPawn = null)
	{
		try
		{
			string text = thingDef?.defName ?? "null";
			string text2 = result?.PrimaryMethod.ToString() ?? "null";
			string text3 = ((result != null && result.DeliveryPosition.IsValid) ? result.DeliveryPosition.ToString() : "invalid");
			int num = result?.LockerDeliveredCount ?? 0;
			int num2 = result?.DropPodDeliveredCount ?? 0;
			int valueOrDefault = (result?.DirectlyDeliveredItems?.Sum((Thing t) => t?.stackCount ?? 0)).GetValueOrDefault();
			if (result != null && num == 0 && num2 == 0 && valueOrDefault == 0)
			{
				Logger.Warning($"[ItemSpawn] NOTHING delivered for {text} x{quantity} via {path} — check maps/lockers");
				LogMapSnapshot("[ItemSpawn maps]");
			}
		}
		catch (Exception) { }
	}

	public static bool TryFindDeliveryCell(Map map, out IntVec3 cell, bool allowRoofPunch = true)
	{
		cell = IntVec3.Invalid;
		if (map == null)
		{
			return false;
		}
		try
		{
			IntVec3 anchor = GetPreferredDropAnchorOnMap(map);
			if (anchor.IsValid && DropCellFinder.TryFindDropSpotNear(anchor, map, out cell, allowFogged: false, allowRoofPunch, 35) && IsValidDeliveryPosition(cell, map, strict: true, !allowRoofPunch))
			{
				return true;
			}
			if (!allowRoofPunch && anchor.IsValid && DropCellFinder.TryFindDropSpotNear(anchor, map, out cell, allowFogged: false, canRoofPunch: false, 55) && IsValidDeliveryPosition(cell, map, strict: true, rejectThickRoof: true))
			{
				return true;
			}
			if (anchor.IsValid && IsValidDeliveryPosition(anchor, map, strict: false, !allowRoofPunch))
			{
				cell = anchor;
				return true;
			}
			List<Building_RimazonLocker> list = (from l in map.listerThings.AllThings.OfType<Building_RimazonLocker>()
				where l.Spawned && !l.Destroyed
				select l).ToList();
			if (list.Any())
			{
				Building_RimazonLocker building_RimazonLocker = list.OrderBy((Building_RimazonLocker l) => l.Position.DistanceToSquared(anchor.IsValid ? anchor : map.Center)).First();
				if (DropCellFinder.TryFindDropSpotNear(building_RimazonLocker.Position, map, out cell, allowFogged: false, allowRoofPunch, 12) && IsValidDeliveryPosition(cell, map, strict: true, !allowRoofPunch))
				{
					return true;
				}
				if (CellFinder.TryFindRandomCellNear(building_RimazonLocker.Position, map, 6, (IntVec3 c) => c.Standable(map) && c.Walkable(map) && (allowRoofPunch || !IsThickRoofed(c, map)), out cell))
				{
					return true;
				}
			}
			List<Pawn> list2 = map.mapPawns.AllPawnsSpawned.Where((Pawn p) => p.Faction == Faction.OfPlayer && p.Spawned && !p.Dead).ToList();
			if (list2.Any())
			{
				Pawn pawn = list2.OrderBy((Pawn p) => p.Position.DistanceToSquared(anchor.IsValid ? anchor : map.Center)).First();
				if (DropCellFinder.TryFindDropSpotNear(pawn.Position, map, out cell, allowFogged: false, allowRoofPunch, 15) && IsValidDeliveryPosition(cell, map, strict: true, !allowRoofPunch))
				{
					return true;
				}
				if (CellFinder.TryFindRandomCellNear(pawn.Position, map, 8, (IntVec3 c) => c.Standable(map) && c.Walkable(map) && (allowRoofPunch || !IsThickRoofed(c, map)), out cell))
				{
					return true;
				}
			}
			if (CellFinder.TryFindRandomEdgeCellWith((IntVec3 c) => c.Standable(map) && !c.Fogged(map) && c.Walkable(map), map, CellFinder.EdgeRoadChance_Ignore, out cell) && IsValidDeliveryPosition(cell, map))
			{
				return true;
			}
			if (CellFinderLoose.TryFindRandomNotEdgeCellWith(10, (IntVec3 c) => IsValidDeliveryPosition(c, map, strict: false), map, out cell))
			{
				Logger.Warning($"TryFindDeliveryCell: relaxed cell last resort → {cell}");
				return true;
			}
			cell = map.Center;
			Logger.Warning("TryFindDeliveryCell: map center ultimate fallback");
			return cell.InBounds(map);
		}
		catch (Exception ex)
		{
			Logger.Error("TryFindDeliveryCell failed: " + ex.Message);
			cell = map?.Center ?? IntVec3.Invalid;
			return cell.IsValid && map != null && cell.InBounds(map);
		}
	}

	public static bool TryDeliverGeneratedPawn(Pawn pawn, Map map, out IntVec3 deliveryPosition)
	{
		deliveryPosition = IntVec3.Invalid;
		if (pawn == null || map == null)
		{
			return false;
		}
		try
		{
			if (IsSpaceMap(map))
			{
				EquipVacsuitIfNeeded(pawn);
			}
			bool flag = IsSealedOrPocketMap(map);
			bool flag2 = map.Size.x < 40 || map.Size.z < 40;
			if (!flag && !flag2 && TryFindDeliveryCell(map, out var cell, allowRoofPunch: false))
			{
				try
				{
					DropPodUtility.DropThingsNear(cell, map, new List<Thing> { pawn }, 110, canInstaDropDuringInit: false, leaveSlag: false, canRoofPunch: false, forbid: true, allowFogged: false);
				}
				catch (Exception ex)
				{
					Logger.Warning("TryDeliverGeneratedPawn: DropThingsNear threw: " + ex.Message);
				}
				if (IsPawnEnRouteOrOnMap(pawn, map))
				{
					deliveryPosition = ((pawn.Spawned && pawn.Position.IsValid) ? pawn.Position : cell);
					string arg = (pawn.Spawned ? "spawned" : ("in-flight (holder=" + (pawn.ParentHolder?.GetType().Name ?? "null") + ")"));
					return true;
				}
				Logger.Warning("TryDeliverGeneratedPawn: drop pod did not accept pawn on " + DescribeMap(map) + " " + string.Format("(requested {0}, holder={1}) ", cell, pawn.ParentHolder?.GetType().Name ?? "null") + "— falling back to GenSpawn");
			}
			else if (flag || flag2)
			{
			}
			if (IsPawnHeldInTransit(pawn))
			{
				deliveryPosition = ((pawn.Spawned && pawn.Position.IsValid) ? pawn.Position : map.Center);
				Logger.Warning("TryDeliverGeneratedPawn: pawn already in transit (holder=" + pawn.ParentHolder?.GetType().Name + ") — skip GenSpawn fallback");
				return true;
			}
			if (TryGenSpawnPawnOnMap(pawn, map, out deliveryPosition))
			{
				return true;
			}
			Logger.Error("TryDeliverGeneratedPawn: all strategies failed on " + DescribeMap(map));
			return false;
		}
		catch (Exception arg2)
		{
			Logger.Error($"TryDeliverGeneratedPawn failed: {arg2}");
			deliveryPosition = map?.Center ?? IntVec3.Invalid;
			return false;
		}
	}

	private static bool IsPawnDeliveredOnMap(Pawn pawn, Map map)
	{
		return pawn != null && map != null && !pawn.Destroyed && pawn.Spawned && pawn.Map == map;
	}

	private static bool IsPawnEnRouteOrOnMap(Pawn pawn, Map map)
	{
		if (pawn == null || map == null || pawn.Destroyed)
		{
			return false;
		}
		if (pawn.Spawned && pawn.Map == map)
		{
			return true;
		}
		return IsPawnHeldInTransit(pawn);
	}

	private static bool IsPawnHeldInTransit(Pawn pawn)
	{
		return pawn != null && !pawn.Destroyed && !pawn.Spawned && pawn.ParentHolder != null;
	}

	private static bool TryGenSpawnPawnOnMap(Pawn pawn, Map map, out IntVec3 deliveryPosition)
	{
		deliveryPosition = IntVec3.Invalid;
		if (pawn == null || map == null)
		{
			return false;
		}
		if (IsPawnDeliveredOnMap(pawn, map))
		{
			deliveryPosition = pawn.Position;
			return true;
		}
		if (IsPawnHeldInTransit(pawn))
		{
			Logger.Warning("TryGenSpawnPawnOnMap: refusing to GenSpawn pawn held by " + pawn.ParentHolder?.GetType().Name + " (in-flight delivery)");
			return false;
		}
		if (pawn.Spawned)
		{
			try
			{
				pawn.DeSpawn();
			}
			catch (Exception ex)
			{
				Logger.Warning("TryGenSpawnPawnOnMap: DeSpawn before re-place: " + ex.Message);
			}
		}
		Predicate<IntVec3> validator = (IntVec3 c) => c.InBounds(map) && c.Standable(map) && c.Walkable(map) && !c.Fogged(map);
		Building_RimazonLocker building_RimazonLocker = map.listerThings?.AllThings?.OfType<Building_RimazonLocker>().FirstOrDefault((Building_RimazonLocker l) => l.Spawned && !l.Destroyed);
		if (building_RimazonLocker != null && CellFinder.TryFindRandomCellNear(building_RimazonLocker.Position, map, 6, validator, out var result) && TrySpawnPawnAt(pawn, result, map, out deliveryPosition, "locker"))
		{
			return true;
		}
		Pawn pawn2 = map.mapPawns?.FreeColonistsSpawned?.FirstOrDefault();
		if (pawn2 != null && CellFinder.TryFindRandomCellNear(pawn2.Position, map, 8, validator, out var result2) && TrySpawnPawnAt(pawn, result2, map, out deliveryPosition, "colonist"))
		{
			return true;
		}
		Pawn pawn3 = map.mapPawns?.AllPawnsSpawned?.FirstOrDefault((Pawn p) => p != pawn && p.Faction == Faction.OfPlayer && p.Spawned && !p.Dead);
		if (pawn3 != null && CellFinder.TryFindRandomCellNear(pawn3.Position, map, 8, validator, out var result3) && TrySpawnPawnAt(pawn, result3, map, out deliveryPosition, "playerPawn"))
		{
			return true;
		}
		if (CellFinder.TryFindRandomCell(map, validator, out var result4) && TrySpawnPawnAt(pawn, result4, map, out deliveryPosition, "randomStandable"))
		{
			return true;
		}
		if (TrySpawnPawnAt(pawn, map.Center, map, out deliveryPosition, "center"))
		{
			return true;
		}
		return false;
	}

	private static bool TrySpawnPawnAt(Pawn pawn, IntVec3 cell, Map map, out IntVec3 deliveryPosition, string via)
	{
		deliveryPosition = IntVec3.Invalid;
		try
		{
			if (!cell.InBounds(map))
			{
				return false;
			}
			if ((!cell.Standable(map) || !cell.Walkable(map)) && !CellFinder.TryFindRandomCellNear(cell, map, 10, (IntVec3 c) => c.InBounds(map) && c.Standable(map) && c.Walkable(map), out cell))
			{
				return false;
			}
			GenSpawn.Spawn(pawn, cell, map);
			if (!IsPawnDeliveredOnMap(pawn, map))
			{
				Logger.Warning($"TrySpawnPawnAt: GenSpawn reported but pawn not on map at {cell} via {via}");
				return false;
			}
			deliveryPosition = pawn.Position;
			return true;
		}
		catch (Exception ex)
		{
			Logger.Warning($"TrySpawnPawnAt failed via {via} at {cell}: {ex.Message}");
			return false;
		}
	}

	private static IntVec3 GetPreferredDropAnchorOnMap(Map map)
	{
		if (map == null)
		{
			return IntVec3.Invalid;
		}
		ThingDef namedSilentFail = DefDatabase<ThingDef>.GetNamedSilentFail("RimazonDropMarker");
		if (namedSilentFail != null)
		{
			Building building = map.listerBuildings.AllBuildingsColonistOfDef(namedSilentFail).FirstOrDefault((Building b) => b.Spawned && !b.Destroyed);
			if (building != null)
			{
				return building.Position;
			}
		}
		Thing thing = map.listerThings.AllThings.Where((Thing t) => t.Spawned && !t.Destroyed && t.Map == map).FirstOrDefault(delegate(Thing t)
		{
			string text = t.Label?.ToLowerInvariant();
			return !string.IsNullOrWhiteSpace(text) && text.Contains("drop spot");
		});
		if (thing != null)
		{
			return thing.Position;
		}
		Building building2 = map.listerBuildings.AllBuildingsColonistOfDef(ThingDefOf.OrbitalTradeBeacon).FirstOrDefault((Building b) => b.Spawned && !b.Destroyed && !map.roofGrid.Roofed(b.Position));
		if (building2 != null)
		{
			return building2.Position;
		}
		Building building3 = map.listerBuildings.AllBuildingsColonistOfDef(ThingDefOf.ShipLandingBeacon).FirstOrDefault((Building b) => b.Spawned && !b.Destroyed);
		if (building3 != null)
		{
			return building3.Position;
		}
		ThingDef thingDef = ThingDefOf.CaravanPackingSpot ?? DefDatabase<ThingDef>.GetNamedSilentFail("CaravanPackingSpot");
		if (thingDef != null)
		{
			Building building4 = map.listerBuildings.AllBuildingsColonistOfDef(thingDef).FirstOrDefault((Building b) => b.Spawned && !b.Destroyed);
			if (building4 != null)
			{
				return building4.Position;
			}
		}
		List<Pawn> freeColonistsSpawned = map.mapPawns.FreeColonistsSpawned;
		if (freeColonistsSpawned.Count > 0)
		{
			IntVec3 zero = IntVec3.Zero;
			foreach (Pawn item in freeColonistsSpawned)
			{
				zero += item.Position;
			}
			zero /= freeColonistsSpawned.Count;
			if (CellFinder.TryFindRandomCellNear(zero, map, 20, (IntVec3 c) => c.Standable(map) && !c.Fogged(map), out var result))
			{
				return result;
			}
			return zero;
		}
		return IntVec3.Invalid;
	}

	public static bool HasVacuumProtection(Pawn pawn)
	{
		if (pawn?.apparel?.WornApparel == null)
		{
			return false;
		}
		foreach (Apparel item in pawn.apparel.WornApparel)
		{
			if (item?.def == null || !ApparelProvidesVacuumProtection(item))
			{
				continue;
			}
			return true;
		}
		return false;
	}

	public static bool ApparelProvidesVacuumProtection(Apparel apparel)
	{
		if (apparel?.def == null)
		{
			return false;
		}
		try
		{
			string[] array = new string[4] { "VacuumResistance", "VacuumEnvironmentResistance", "ApparelVacuumResistance", "SpaceSuit" };
			string[] array2 = array;
			foreach (string defName in array2)
			{
				StatDef namedSilentFail = DefDatabase<StatDef>.GetNamedSilentFail(defName);
				if (namedSilentFail != null && apparel.GetStatValue(namedSilentFail) > 0.05f)
				{
					return true;
				}
			}
		}
		catch
		{
		}
		string hay = apparel.def.defName ?? "";
		string hay2 = apparel.def.label ?? "";
		if (ContainsIgnoreCase(hay, "Vacsuit") || ContainsIgnoreCase(hay2, "vac suit") || ContainsIgnoreCase(hay, "SpaceSuit") || ContainsIgnoreCase(hay2, "space suit"))
		{
			return true;
		}
		if (ContainsIgnoreCase(hay, "Cataphract") || ContainsIgnoreCase(hay2, "cataphract"))
		{
			return true;
		}
		if (ContainsIgnoreCase(hay, "ReconArmor") || ContainsIgnoreCase(hay, "Apparel_ArmorRecon") || (ContainsIgnoreCase(hay, "Recon") && ContainsIgnoreCase(hay, "Armor")) || (ContainsIgnoreCase(hay2, "recon") && ContainsIgnoreCase(hay2, "marine")))
		{
			return true;
		}
		if ((ContainsIgnoreCase(hay, "Marine") || ContainsIgnoreCase(hay2, "marine")) && (ContainsIgnoreCase(hay, "Armor") || ContainsIgnoreCase(hay, "Power") || ContainsIgnoreCase(hay2, "armor") || ContainsIgnoreCase(hay2, "power")))
		{
			return true;
		}
		if (ContainsIgnoreCase(hay, "PowerArmor") || ContainsIgnoreCase(hay2, "power armor"))
		{
			return true;
		}
		return false;
	}

	private static bool ContainsIgnoreCase(string hay, string needle)
	{
		return !string.IsNullOrEmpty(hay) && !string.IsNullOrEmpty(needle) && hay.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0;
	}

	public static void EquipVacsuitIfNeeded(Pawn pawn)
	{
		try
		{
			if (pawn?.apparel == null)
			{
				return;
			}
			Pawn_AgeTracker ageTracker = pawn.ageTracker;
			ThingDef thingDef = ((ageTracker != null && ageTracker.CurLifeStageIndex <= 1) ? DefDatabase<ThingDef>.GetNamedSilentFail("Apparel_VacsuitChildren") : DefDatabase<ThingDef>.GetNamedSilentFail("Apparel_Vacsuit"));
			ThingDef thingDef2 = DefDatabase<ThingDef>.GetNamedSilentFail("Apparel_VacsuitHelmet") ?? DefDatabase<ThingDef>.GetNamedSilentFail("Apparel_VacsuitHelmetChildren");
			bool flag = false;
			if (thingDef != null && !HasVacuumProtection(pawn))
			{
				Apparel apparel = PawnApparelGenerator.GenerateApparelOfDefFor(pawn, thingDef);
				if (apparel != null && ApparelUtility.HasPartsToWear(pawn, apparel.def))
				{
					pawn.apparel.Wear(apparel);
					flag = true;
				}
			}
			if (thingDef2 != null && !HasVacuumHeadProtection(pawn) && ApparelUtility.HasPartsToWear(pawn, thingDef2))
			{
				Apparel apparel2 = PawnApparelGenerator.GenerateApparelOfDefFor(pawn, thingDef2);
				if (apparel2 != null)
				{
					pawn.apparel.Wear(apparel2);
					flag = true;
				}
			}
			if (flag)
			{
			}
		}
		catch (Exception ex)
		{
			Logger.Warning("Failed to equip vacsuit on " + pawn?.LabelShort + ": " + ex.Message);
		}
	}

	public static bool HasVacuumHeadProtection(Pawn pawn)
	{
		if (pawn?.apparel?.WornApparel == null)
		{
			return false;
		}
		foreach (Apparel item in pawn.apparel.WornApparel)
		{
			if (item?.def == null)
			{
				continue;
			}
			bool flag = false;
			if (item.def.apparel?.bodyPartGroups != null)
			{
				foreach (BodyPartGroupDef bodyPartGroup in item.def.apparel.bodyPartGroups)
				{
					if (bodyPartGroup == BodyPartGroupDefOf.FullHead || bodyPartGroup == BodyPartGroupDefOf.UpperHead)
					{
						flag = true;
						break;
					}
				}
			}
			if (flag)
			{
				if (ApparelProvidesVacuumProtection(item))
				{
					return true;
				}
				string hay = item.def.defName ?? "";
				string hay2 = item.def.label ?? "";
				if (ContainsIgnoreCase(hay, "VacsuitHelmet") || ContainsIgnoreCase(hay, "VacHelmet") || ContainsIgnoreCase(hay2, "vac suit helmet") || ContainsIgnoreCase(hay2, "vacsuit helmet") || (ContainsIgnoreCase(hay, "Vacsuit") && ContainsIgnoreCase(hay, "Helmet")) || (ContainsIgnoreCase(hay2, "vac") && ContainsIgnoreCase(hay2, "helmet")))
				{
					return true;
				}
			}
		}
		return false;
	}

	public static Building_RimazonLocker FindSuitableLockerFor(Thing thing, Map preferredMap = null, Pawn forPawn = null)
	{
		try
		{
			if (thing == null || thing.Destroyed)
			{
				return null;
			}
			List<Building_RimazonLocker> list = new List<Building_RimazonLocker>();
			if (Find.Maps != null)
			{
				foreach (Map map in Find.Maps)
				{
					if (map?.listerThings?.AllThings == null)
					{
						continue;
					}
					foreach (Thing allThing in map.listerThings.AllThings)
					{
						if (allThing is Building_RimazonLocker { Spawned: not false, Destroyed: false } building_RimazonLocker)
						{
							list.Add(building_RimazonLocker);
						}
					}
				}
			}
			if (list.Count == 0)
			{
				return null;
			}
			List<Building_RimazonLocker> list2 = new List<Building_RimazonLocker>();
			foreach (Building_RimazonLocker item in list)
			{
				try
				{
					if (item.Accepts(thing))
					{
						list2.Add(item);
					}
				}
				catch (Exception) { }
			}
			if (list2.Count == 0)
			{
				IEnumerable<string> values = list.Take(3).Select(delegate(Building_RimazonLocker l)
				{
					bool flag = false;
					try
					{
						flag = l.settings?.AllowedToAccept(thing) ?? false;
					}
					catch
					{
					}
					return string.Format("{0}@{1} filter={2} stacks={3}/{4}", l.Map?.Parent?.LabelCap ?? "?", l.Position, flag, l.InnerContainer?.Count ?? 0, l.MaxStacks);
				});
				return null;
			}
			Map orderMap = preferredMap ?? ((forPawn != null && forPawn.Spawned) ? forPawn.Map : null) ?? Find.CurrentMap;
			Building_RimazonLocker building_RimazonLocker2 = (from l in list2
				orderby (orderMap == null || l.Map != orderMap) ? 1 : 0, (!l.InnerContainer.Any((Thing t) => t != null && t.def == thing.def && t.CanStackWith(thing))) ? 1 : 0, l.MaxStacks - (l.InnerContainer?.Count ?? 0) descending, (forPawn != null && forPawn.Spawned && forPawn.Map == l.Map) ? l.Position.DistanceToSquared(forPawn.Position) : 0
				select l).First();
			return building_RimazonLocker2;
		}
		catch (Exception arg)
		{
			Logger.Error($"Error finding suitable locker: {arg}");
			return null;
		}
	}

	public static IntVec3 GetCustomDropSpot(Map map)
	{
		if (map == null)
		{
			return IntVec3.Invalid;
		}
		IntVec3 preferredDropAnchorOnMap = GetPreferredDropAnchorOnMap(map);
		if (preferredDropAnchorOnMap.IsValid)
		{
			return preferredDropAnchorOnMap;
		}
		Logger.Warning("GetCustomDropSpot: no anchor → map center");
		return map.Center;
	}

	public static bool IsUndergroundMap(Map map)
	{
		return IsSealedOrPocketMap(map);
	}

	public static bool IsSealedOrPocketMap(Map map)
	{
		if (map == null)
		{
			return false;
		}
		int num = Find.TickManager?.TicksGame ?? 0;
		if (num != _undergroundCacheTick)
		{
			_undergroundByMapId.Clear();
			_undergroundCacheTick = num;
		}
		int uniqueID = map.uniqueID;
		if (_undergroundByMapId.TryGetValue(uniqueID, out var value))
		{
			return value;
		}
		bool flag = ComputeIsSealedOrPocketMap(map);
		_undergroundByMapId[uniqueID] = flag;
		return flag;
	}

	private static bool ComputeIsSealedOrPocketMap(Map map)
	{
		try
		{
			if (map.IsPocketMap)
			{
				return true;
			}
		}
		catch
		{
		}
		if (map.Parent is PocketMapParent)
		{
			return true;
		}
		string text = map.Parent?.LabelCap ?? map.Parent?.def?.defName ?? "";
		if (!string.IsNullOrEmpty(text) && text.IndexOf("pocket", StringComparison.OrdinalIgnoreCase) >= 0)
		{
			return true;
		}
		int num = 0;
		int num2 = 0;
		int num3 = 0;
		foreach (IntVec3 allCell in map.AllCells)
		{
			num3++;
			RoofDef roofDef = map.roofGrid?.RoofAt(allCell);
			if (roofDef != null)
			{
				if (roofDef.isThickRoof)
				{
					num++;
				}
				if (roofDef.isNatural && roofDef == RoofDefOf.RoofRockThick)
				{
					num2++;
				}
			}
		}
		if (num3 == 0)
		{
			return false;
		}
		float num4 = (float)num / (float)num3;
		float num5 = (float)num2 / (float)num3;
		if (num4 > 0.92f || num5 > 0.92f)
		{
			return true;
		}
		if (map.Size.x < 220 && map.Size.z < 220 && (num4 > 0.8f || num5 > 0.8f))
		{
			return true;
		}
		if ((map.Size.x < 40 || map.Size.z < 40) && num4 > 0.5f)
		{
			return true;
		}
		return false;
	}

	public static (List<Thing> spawnedThings, IntVec3 deliveryPos, DeliveryResult deliveryResult) SpawnItemForPawn(ThingDef thingDef, int quantity, QualityCategory? quality, ThingDef material, Pawn pawn, bool addToInventory = false)
	{
		DeliveryResult deliveryResult = SpawnItemForPawn(thingDef, quantity, quality, material, pawn, addToInventory, equipItem: false, wearItem: false);
		List<Thing> list = new List<Thing>();
		list.AddRange(deliveryResult.LockerDeliveredItems);
		list.AddRange(deliveryResult.DropPodDeliveredItems);
		list.AddRange(deliveryResult.DirectlyDeliveredItems);
		return (spawnedThings: list, deliveryPos: deliveryResult.DeliveryPosition, deliveryResult: deliveryResult);
	}

	public static DeliveryResult SpawnItemForPawn(ThingDef thingDef, int quantity, QualityCategory? quality, ThingDef material, Pawn pawn, bool addToInventory, bool equipItem, bool wearItem, Thing preCreatedItem = null)
	{
		DeliveryResult result = new DeliveryResult
		{
			LockerDeliveredItems = new List<Thing>(),
			DropPodDeliveredItems = new List<Thing>(),
			DeliveryPosition = IntVec3.Invalid,
			PrimaryMethod = DeliveryMethod.DropPod
		};
		string text = "unknown";
		try
		{
			if (preCreatedItem != null)
			{
				if (equipItem || wearItem || addToInventory)
				{
					text = "preCreated+direct";
					result = HandleDirectPawnInteractionWithPreCreated(preCreatedItem, pawn, equipItem, wearItem, addToInventory, result);
				}
				else
				{
					text = "preCreated+regular";
					result = HandleRegularDeliveryWithPreCreated(preCreatedItem, pawn, result);
				}
				LogItemSpawnResult(thingDef, quantity, result, text, pawn);
				return result;
			}
			if (IsPawnThingDef(thingDef))
			{
				text = "pawnDelivery";
				result = HandlePawnDelivery(thingDef, quantity, quality, material, pawn);
				LogItemSpawnResult(thingDef, quantity, result, text, pawn);
				return result;
			}
			if (equipItem || wearItem || addToInventory)
			{
				text = "directPawn";
				result = HandleDirectPawnInteraction(thingDef, quantity, quality, material, pawn, equipItem, wearItem, addToInventory);
				LogItemSpawnResult(thingDef, quantity, result, text, pawn);
				return result;
			}
			text = "regularLoose";
			result = HandleRegularDelivery(thingDef, quantity, quality, material, pawn);
			LogItemSpawnResult(thingDef, quantity, result, text, pawn);
			return result;
		}
		catch (Exception arg)
		{
			Logger.Error($"[ItemSpawn] ERROR path={text} def={thingDef?.defName}: {arg}");
			LogMapSnapshot("[ItemSpawn error maps]");
			throw;
		}
	}

	private static bool IsPawnThingDef(ThingDef thingDef)
	{
		return thingDef.thingClass == typeof(Pawn) || thingDef.race != null;
	}

	private static DeliveryResult HandlePawnDelivery(ThingDef thingDef, int quantity, QualityCategory? quality, ThingDef material, Pawn viewerPawn)
	{
		DeliveryResult deliveryResult = new DeliveryResult
		{
			PrimaryMethod = DeliveryMethod.PawnDelivery,
			DeliveryPosition = IntVec3.Invalid,
			RequestedCount = quantity
		};
		Map map = ResolveDeliveryMap(viewerPawn);
		if (map == null)
		{
			Logger.Error("No valid map found for pawn delivery");
			deliveryResult.UndeliveredCount = quantity;
			deliveryResult.PrimaryMethod = DeliveryMethod.Failed;
			return deliveryResult;
		}
		if (!TryFindSafeDropPosition(map, out var dropPos))
		{
			Logger.Error("No safe drop position found for pawn delivery");
			deliveryResult.UndeliveredCount = quantity;
			deliveryResult.PrimaryMethod = DeliveryMethod.Failed;
			return deliveryResult;
		}
		(int, IntVec3, List<Pawn>) tuple = TryDeliverPawnFromStore(thingDef, quantity, quality, material, dropPos, map, viewerPawn);
		if (tuple.Item3 != null)
		{
			foreach (Pawn item in tuple.Item3)
			{
				if (item != null && !item.Destroyed)
				{
					deliveryResult.DropPodDeliveredItems.Add(item);
				}
			}
		}
		deliveryResult.DropPodDeliveredCount = tuple.Item1;
		deliveryResult.UndeliveredCount = Math.Max(0, quantity - tuple.Item1);
		deliveryResult.DeliveryPosition = tuple.Item2;
		if (tuple.Item1 > 0)
		{
			deliveryResult.PrimaryMethod = DeliveryMethod.PawnDelivery;
		}
		else
		{
			deliveryResult.PrimaryMethod = DeliveryMethod.Failed;
			Logger.Warning($"[ItemSpawn] pawnDelivery FAILED 0/{quantity} {thingDef.defName}");
		}
		return deliveryResult;
	}

	private static DeliveryResult HandleDirectPawnInteraction(ThingDef thingDef, int quantity, QualityCategory? quality, ThingDef material, Pawn pawn, bool equipItem, bool wearItem, bool addToInventory)
	{
		DeliveryResult deliveryResult = new DeliveryResult
		{
			DeliveryPosition = (pawn?.Position ?? IntVec3.Invalid)
		};
		ThingDef thingDef2 = material;
		if (thingDef.MadeFromStuff && thingDef2 == null)
		{
			thingDef2 = GenStuff.RandomStuffFor(thingDef);
		}
		List<Thing> list = CreateItemsForDelivery(thingDef, quantity, quality, thingDef2);
		List<Thing> list2 = new List<Thing>();
		if (equipItem && pawn != null)
		{
			deliveryResult.PrimaryMethod = DeliveryMethod.Equipped;
			foreach (Thing item in list)
			{
				if (PawnItemHelper.EquipItemOnPawn(item, pawn))
				{
					list2.Add(item);
				}
				else
				{
					TryDeliverToLocker(item, pawn.Map, pawn, deliveryResult);
				}
			}
		}
		else if (wearItem && pawn != null)
		{
			deliveryResult.PrimaryMethod = DeliveryMethod.Worn;
			foreach (Thing item2 in list)
			{
				if (PawnItemHelper.WearApparelOnPawn(item2, pawn))
				{
					list2.Add(item2);
				}
				else
				{
					TryDeliverToLocker(item2, pawn.Map, pawn, deliveryResult);
				}
			}
		}
		else if (addToInventory && pawn != null)
		{
			deliveryResult.PrimaryMethod = DeliveryMethod.Inventory;
			foreach (Thing item3 in list)
			{
				if (pawn.inventory.innerContainer.TryAdd(item3))
				{
					list2.Add(item3);
				}
				else
				{
					TryDeliverToLocker(item3, pawn.Map, pawn, deliveryResult);
				}
			}
		}
		deliveryResult.DirectlyDeliveredItems = list2;
		return deliveryResult;
	}

	private static DeliveryResult HandleRegularDelivery(ThingDef thingDef, int quantity, QualityCategory? quality, ThingDef material, Pawn pawn)
	{
		DeliveryResult deliveryResult = new DeliveryResult
		{
			DeliveryPosition = IntVec3.Invalid
		};
		ThingDef thingDef2 = material;
		if (thingDef.MadeFromStuff && thingDef2 == null)
		{
			thingDef2 = GenStuff.RandomStuffFor(thingDef);
		}
		List<Thing> list = CreateItemsForDelivery(thingDef, quantity, quality, thingDef2);
		deliveryResult.RequestedCount = quantity;
		Map map = ResolveDeliveryMap(pawn, allowUndergroundRedirect: false);
		Map surfaceHomeMap = GetSurfaceHomeMap();
		bool flag = map != null && IsSealedOrPocketMap(map);
		if (flag || map == null)
		{
			LogMapSnapshot("[ItemSpawn regularLoose]");
		}
		List<Thing> list2 = new List<Thing>();
		foreach (Thing item in list)
		{
			if (!TryDeliverToLocker(item, map, pawn, deliveryResult))
			{
				list2.Add(item);
			}
		}
		if (list2.Count > 0)
		{
			int num = list2.Sum((Thing t) => t?.stackCount ?? 0);
			Map dropMapForItems = GetDropMapForItems(map);
			if (dropMapForItems == null)
			{
				Logger.Error("[ItemSpawn] No map for overflow after locker miss — culling leftovers");
				deliveryResult.UndeliveredCount += CullUndeliveredThings(list2);
				LogMapSnapshot("[ItemSpawn no-drop-map]");
				FinalizeRegularLooseResult(deliveryResult, map, surfaceHomeMap);
				return deliveryResult;
			}
			bool flag2 = IsSealedOrPocketMap(dropMapForItems) || dropMapForItems.Size.x < 40 || dropMapForItems.Size.z < 40;
			IntVec3 spawnPos = GetDeliveryPosition(dropMapForItems, pawn);
			List<Thing> list3 = new List<Thing>();
			List<Thing> list4 = new List<Thing>();
			if (flag2)
			{
				TryDirectSpawnItemsNearColony(list2, dropMapForItems, out spawnPos, list3, list4);
			}
			else if (TryShuttleDelivery(list2, spawnPos, dropMapForItems))
			{
				foreach (Thing item2 in list2)
				{
					if (item2 != null && !item2.Destroyed && (item2.Spawned || item2.ParentHolder != null))
					{
						list3.Add(item2);
					}
					else if (item2 != null && !item2.Destroyed)
					{
						list4.Add(item2);
					}
				}
			}
			else
			{
				Logger.Warning("[ItemSpawn] Drop pod failed on " + DescribeMap(dropMapForItems) + " — GenPlace fallback");
				TryDirectSpawnItemsNearColony(list2, dropMapForItems, out spawnPos, list3, list4);
			}
			if (list3.Count > 0)
			{
				deliveryResult.DropPodDeliveredItems.AddRange(list3);
				deliveryResult.DropPodDeliveredCount += list3.Sum((Thing t) => t.stackCount);
				deliveryResult.DeliveryPosition = spawnPos;
			}
			if (list4.Count > 0)
			{
				int num2 = CullUndeliveredThings(list4);
				deliveryResult.UndeliveredCount += num2;
				Logger.Warning($"[ItemSpawn] no space for {num2} units — culled (locker full + map full)");
				LogMapSnapshot("[ItemSpawn deliver-partial-or-fail]");
			}
		}
		else
		{
		}
		FinalizeRegularLooseResult(deliveryResult, map, surfaceHomeMap);
		return deliveryResult;
	}

	private static void FinalizeRegularLooseResult(DeliveryResult result, Map preferredMap, Map surfaceMap)
	{
		DeterminePrimaryDeliveryMethod(result);
		if (result.DeliveryPosition == IntVec3.Invalid || result.DeliveryPosition == default(IntVec3))
		{
			Map map = preferredMap ?? surfaceMap ?? Find.CurrentMap;
			if (map != null)
			{
				result.DeliveryPosition = GetFallbackDeliveryPosition(map, result);
			}
		}
		int totalUnitsDelivered = result.TotalUnitsDelivered;
		if (result.RequestedCount > 0 && result.UndeliveredCount == 0 && totalUnitsDelivered < result.RequestedCount)
		{
			result.UndeliveredCount = result.RequestedCount - totalUnitsDelivered;
		}
	}

	private static int CullUndeliveredThings(IEnumerable<Thing> things)
	{
		int num = 0;
		if (things == null)
		{
			return 0;
		}
		foreach (Thing thing in things)
		{
			if (thing == null || thing.Destroyed)
			{
				continue;
			}
			num += thing.stackCount;
			try
			{
				if (thing.Spawned)
				{
					thing.Destroy();
				}
				else
				{
					thing.Destroy();
				}
			}
			catch (Exception ex)
			{
				Logger.Warning("[ItemSpawn] cull failed for " + thing.def?.defName + ": " + ex.Message);
			}
		}
		return num;
	}

	private static bool TryDirectSpawnItemsNearColony(List<Thing> items, Map map, out IntVec3 spawnPos, List<Thing> placed = null, List<Thing> failed = null)
	{
		spawnPos = IntVec3.Invalid;
		if (map == null || items == null || items.Count == 0)
		{
			return false;
		}
		placed = placed ?? new List<Thing>();
		failed = failed ?? new List<Thing>();
		try
		{
			Predicate<IntVec3> validator = (IntVec3 c) => c.InBounds(map) && c.Standable(map) && c.Walkable(map) && !c.Fogged(map);
			Building_RimazonLocker building_RimazonLocker = map.listerThings?.AllThings?.OfType<Building_RimazonLocker>().FirstOrDefault((Building_RimazonLocker l) => l.Spawned && !l.Destroyed);
			if (building_RimazonLocker != null && CellFinder.TryFindRandomCellNear(building_RimazonLocker.Position, map, 6, validator, out var result))
			{
				spawnPos = result;
			}
			if (!spawnPos.IsValid)
			{
				Pawn pawn = map.mapPawns?.FreeColonistsSpawned?.FirstOrDefault();
				if (pawn != null && CellFinder.TryFindRandomCellNear(pawn.Position, map, 8, validator, out var result2))
				{
					spawnPos = result2;
				}
			}
			if (!spawnPos.IsValid)
			{
				Pawn pawn2 = map.mapPawns?.AllPawnsSpawned?.FirstOrDefault((Pawn p) => p.Faction == Faction.OfPlayer && p.Spawned && !p.Dead);
				if (pawn2 != null && CellFinder.TryFindRandomCellNear(pawn2.Position, map, 8, validator, out var result3))
				{
					spawnPos = result3;
				}
			}
			if (!spawnPos.IsValid && TryFindDeliveryCell(map, out var cell))
			{
				spawnPos = cell;
			}
			if (!spawnPos.IsValid)
			{
				spawnPos = map.Center;
			}
			foreach (Thing item in items)
			{
				if (item != null && !item.Destroyed)
				{
					IntVec3 result4;
					if (item.Spawned)
					{
						placed.Add(item);
					}
					else if (GenPlace.TryPlaceThing(item, spawnPos, map, ThingPlaceMode.Near))
					{
						placed.Add(item);
					}
					else if (CellFinder.TryFindRandomCell(map, validator, out result4) && GenPlace.TryPlaceThing(item, result4, map, ThingPlaceMode.Near))
					{
						placed.Add(item);
						spawnPos = result4;
					}
					else
					{
						failed.Add(item);
						Logger.Warning($"[ItemSpawn] GenPlace failed for {item.def.defName} x{item.stackCount} " + $"on {DescribeMap(map)} at/near {spawnPos}");
					}
				}
			}
			return placed.Count > 0;
		}
		catch (Exception ex)
		{
			Logger.Error("TryDirectSpawnItemsNearColony: " + ex.Message);
			return false;
		}
	}

	private static bool TryDeliverToLocker(Thing item, Map preferredMap, Pawn pawn, DeliveryResult result)
	{
		if (item == null || item.Destroyed)
		{
			Logger.Error("TryDeliverToLocker: Item is null or already destroyed before delivery attempt");
			return false;
		}
		Building_RimazonLocker building_RimazonLocker = FindSuitableLockerFor(item, preferredMap, pawn);
		if (building_RimazonLocker == null)
		{
			return false;
		}
		int stackCount = item.stackCount;
		Map map = building_RimazonLocker.Map;
		if (building_RimazonLocker.TryAcceptThing(item, allowSpecialEffects: false))
		{
			result.LockerDeliveredCount += stackCount;
			if (!item.Destroyed)
			{
				result.LockerDeliveredItems.Add(item);
			}
			if (result.DeliveryPosition == IntVec3.Invalid)
			{
				result.DeliveryPosition = building_RimazonLocker.Position;
			}
			return true;
		}
		return false;
	}

	private static Map GetTargetMapForDelivery(Pawn pawn)
	{
		return ResolveDeliveryMap(pawn, allowUndergroundRedirect: false);
	}

	private static Map GetSurfaceHomeMap()
	{
		return Find.Maps?.FirstOrDefault((Map m) => m != null && m.IsPlayerHome && !IsSealedOrPocketMap(m));
	}

	private static IntVec3 GetDeliveryPosition(Map map, Pawn pawn)
	{
		if (map == null)
		{
			return IntVec3.Invalid;
		}
		if (TryFindDeliveryCell(map, out var cell))
		{
			return cell;
		}
		return map.Center;
	}

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
			{
				result.PrimaryMethod = DeliveryMethod.DropPod;
			}
			else
			{
				result.PrimaryMethod = DeliveryMethod.Locker;
			}
			return;
		}
		if (result.DropPodDeliveredCount > 0)
		{
			result.PrimaryMethod = DeliveryMethod.DropPod;
			return;
		}
		List<Thing> directlyDeliveredItems = result.DirectlyDeliveredItems;
		if (directlyDeliveredItems != null && directlyDeliveredItems.Count > 0)
		{
			result.PrimaryMethod = DeliveryMethod.Inventory;
		}
		else
		{
			result.PrimaryMethod = DeliveryMethod.Failed;
		}
	}

	private static IntVec3 GetFallbackDeliveryPosition(Map map, DeliveryResult result)
	{
		if (result.LockerDeliveredItems.Count > 0)
		{
			Thing thing = result.LockerDeliveredItems.FirstOrDefault();
			Building_RimazonLocker building_RimazonLocker = FindSuitableLockerFor(thing, map);
			if (building_RimazonLocker != null)
			{
				return building_RimazonLocker.Position;
			}
		}
		return GetCustomDropSpot(map);
	}

	private static List<Thing> CreateItemsForDelivery(ThingDef thingDef, int quantity, QualityCategory? quality, ThingDef material)
	{
		List<Thing> list = new List<Thing>();
		int num = quantity;
		bool minifiable = thingDef.Minifiable;
		while (num > 0)
		{
			Thing thing;
			if (minifiable)
			{
				thing = CreateMinifiedThing(thingDef, quality, material);
				num--;
			}
			else
			{
				int num2 = Math.Min(num, thingDef.stackLimit);
				thing = ThingMaker.MakeThing(thingDef, material);
				thing.stackCount = num2;
				if (quality.HasValue && thingDef.HasComp(typeof(CompQuality)) && thing.TryGetQuality(out var _))
				{
					thing.TryGetComp<CompQuality>()?.SetQuality(quality.Value, ArtGenerationContext.Outsider);
				}
				num -= num2;
			}
			list.Add(thing);
		}
		return list;
	}

	public static bool IsValidDeliveryPosition(IntVec3 pos, Map map, bool strict = true, bool rejectThickRoof = false)
	{
		if (map == null)
		{
			return false;
		}
		if (!pos.InBounds(map))
		{
			return false;
		}
		if (rejectThickRoof && IsThickRoofed(pos, map))
		{
			return false;
		}
		if (strict)
		{
			if (pos.Fogged(map))
			{
				return false;
			}
			if (!pos.Standable(map) && !pos.Walkable(map))
			{
				return false;
			}
			Building edifice = pos.GetEdifice(map);
			if (edifice != null && edifice.def.passability == Traversability.Impassable && edifice.def.building.isNaturalRock)
			{
				return false;
			}
		}
		else if (!pos.Walkable(map))
		{
			return false;
		}
		return true;
	}

	private static bool IsThickRoofed(IntVec3 pos, Map map)
	{
		if (map?.roofGrid == null || !pos.InBounds(map))
		{
			return false;
		}
		return map.roofGrid.RoofAt(pos)?.isThickRoof ?? false;
	}

	public static bool TryFindSafeDropPosition(Map map, out IntVec3 dropPos)
	{
		return TryFindDeliveryCell(map, out dropPos);
	}

	private static (int deliveredCount, IntVec3 spawnPosition, List<Pawn> deliveredPawns) TryDeliverPawnFromStore(ThingDef pawnDef, int quantity, QualityCategory? quality, ThingDef material, IntVec3 dropPos, Map map, Pawn viewerPawn = null)
	{
		IntVec3 intVec = IntVec3.Invalid;
		List<Pawn> list = new List<Pawn>();
		try
		{
			if (map == null)
			{
				Logger.Error("Map is null for pawn delivery");
				return (deliveredCount: 0, spawnPosition: intVec, deliveredPawns: list);
			}
			if (!dropPos.InBounds(map))
			{
				Logger.Error($"Pawn delivery position {dropPos} is out of map bounds");
				return (deliveredCount: 0, spawnPosition: intVec, deliveredPawns: list);
			}
			Building_RimazonLocker building_RimazonLocker = null;
			List<Building_RimazonLocker> list2 = (from l in map.listerThings.AllThings.OfType<Building_RimazonLocker>()
				where l.Spawned && l.Map == map && !l.Destroyed
				select l).ToList();
			if (list2.Any())
			{
				building_RimazonLocker = list2.OrderBy((Building_RimazonLocker l) => l.Position.DistanceToSquared(dropPos)).First();
			}
			if (building_RimazonLocker != null)
			{
				dropPos = building_RimazonLocker.Position;
			}
			List<Pawn> list3 = new List<Pawn>();
			for (int i = 0; i < quantity; i++)
			{
				PawnGenerationRequest request = new PawnGenerationRequest(pawnDef.race.AnyPawnKind, null, PawnGenerationContext.NonPlayer, -1, forceGenerateNewPawn: true, allowDead: false, allowDowned: false, canGeneratePawnRelations: false, mustBeCapableOfViolence: false, 0f, forceAddFreeWarmLayerIfNeeded: false, allowGay: true, allowPregnant: false, allowFood: true, allowAddictions: true, inhabitant: false, certainlyBeenInCryptosleep: false, forceRedressWorldPawnIfFormerColonist: false, worldPawnFactionDoesntMatter: false, 0f, 0f, null, 1f, null, null, null, null, 0f);
				Pawn pawn = PawnGenerator.GeneratePawn(request);
				if (pawn.RaceProps.IsMechanoid || pawn.RaceProps.Animal)
				{
					pawn.SetFaction(Faction.OfPlayer);
				}
				list3.Add(pawn);
			}
			foreach (Pawn item in list3)
			{
				if (TryDeliverGeneratedPawn(item, map, out var deliveryPosition))
				{
					intVec = deliveryPosition;
					list.Add(item);
					if (item.RaceProps.IsMechanoid)
					{
						TryAssignMechToViewer(item, viewerPawn);
					}
					continue;
				}
				Logger.Error("Failed to deliver store pawn " + item.LabelShort + " — culling");
				try
				{
					if (!item.Destroyed)
					{
						item.Destroy();
					}
				}
				catch
				{
				}
			}
			return (deliveredCount: list.Count, spawnPosition: intVec, deliveredPawns: list);
		}
		catch (Exception arg)
		{
			Logger.Error($"Error in pawn delivery: {arg}");
			return (deliveredCount: list.Count, spawnPosition: intVec, deliveredPawns: list);
		}
	}

	private static bool TryAssignMechToViewer(Pawn mech, Pawn viewerPawn)
	{
		try
		{
			if (!ModsConfig.BiotechActive)
			{
				return false;
			}
			if (mech == null || mech.Destroyed || mech.Dead || !mech.RaceProps.IsMechanoid)
			{
				return false;
			}
			if (viewerPawn == null || viewerPawn.Destroyed || viewerPawn.Dead)
			{
				return false;
			}
			if (!MechanitorUtility.IsMechanitor(viewerPawn))
			{
				return false;
			}
			if (mech.Faction != Faction.OfPlayer)
			{
				mech.SetFaction(Faction.OfPlayer);
			}
			if (!MechanitorUtility.EverControllable(mech))
			{
				return false;
			}
			AcceptanceReport acceptanceReport = MechanitorUtility.CanControlMech(viewerPawn, mech);
			if (!acceptanceReport.Accepted)
			{
				float statValue = mech.GetStatValue(StatDefOf.BandwidthCost);
				int num = viewerPawn.mechanitor.TotalBandwidth - viewerPawn.mechanitor.UsedBandwidth;
				return false;
			}
			if (mech.GetOverseer() != viewerPawn)
			{
				viewerPawn.relations.AddDirectRelation(PawnRelationDefOf.Overseer, mech);
			}
			try
			{
				viewerPawn.mechanitor.AssignPawnControlGroup(mech);
			}
			catch (Exception) { }
			viewerPawn.mechanitor.Notify_BandwidthChanged();
			Logger.Message("Mech assign: " + mech.LabelShortCap + " overseen by " + viewerPawn.LabelShort + " " + $"(BW {viewerPawn.mechanitor.UsedBandwidth}/{viewerPawn.mechanitor.TotalBandwidth})");
			return true;
		}
		catch (Exception ex2)
		{
			Logger.Warning("Mech assign failed (mech still spawned): " + ex2.Message);
			return false;
		}
	}

	private static bool TryShuttleDelivery(List<Thing> thingsToDeliver, IntVec3 dropPos, Map map)
	{
		try
		{
			if (map == null)
			{
				Logger.Error("Map is null for delivery");
				return false;
			}
			if (IsSealedOrPocketMap(map) || map.Size.x < 40 || map.Size.z < 40)
			{
				return false;
			}
			if (!dropPos.InBounds(map))
			{
				Logger.Error($"Delivery position {dropPos} is out of map bounds (map size: {map.Size})");
				return false;
			}
			if (thingsToDeliver == null || thingsToDeliver.Count == 0)
			{
				return false;
			}
			DropPodUtility.DropThingsNear(dropPos, map, thingsToDeliver, 110, canInstaDropDuringInit: false, leaveSlag: false, canRoofPunch: false, forbid: false, allowFogged: false);
			if (!thingsToDeliver.Any((Thing t) => t != null && !t.Destroyed && (t.Spawned || t.ParentHolder != null)))
			{
				Logger.Warning("TryShuttleDelivery: DropThingsNear left 0 stacks placed on " + DescribeMap(map));
				return false;
			}
			return true;
		}
		catch (Exception arg)
		{
			Logger.Error($"Error in delivery at position {dropPos}: {arg}");
			return false;
		}
	}

	public static bool ShouldMinifyForDelivery(ThingDef thingDef)
	{
		if (thingDef == null)
		{
			return false;
		}
		if (thingDef.Minifiable)
		{
			return true;
		}
		return false;
	}

	public static Thing CreateMinifiedThing(ThingDef thingDef, QualityCategory? quality, ThingDef material)
	{
		try
		{
			Thing thing = ThingMaker.MakeThing(thingDef, material);
			if (quality.HasValue && thingDef.HasComp(typeof(CompQuality)) && thing.TryGetQuality(out var _))
			{
				thing.TryGetComp<CompQuality>()?.SetQuality(quality.Value, ArtGenerationContext.Outsider);
			}
			Thing thing2 = thing.TryMakeMinified();
			if (thing2 != null)
			{
				return thing2;
			}
			return thing;
		}
		catch (Exception arg)
		{
			Logger.Error($"Error minifying {thingDef.defName}: {arg}");
			return ThingMaker.MakeThing(thingDef, material);
		}
	}

	public static List<Thing> CreateThingsForDelivery(ThingDef thingDef, int quantity, QualityCategory? quality, ThingDef material)
	{
		List<Thing> list = new List<Thing>();
		int num = quantity;
		bool flag = ShouldMinifyForDelivery(thingDef);
		while (num > 0)
		{
			Thing thing;
			if (flag)
			{
				thing = CreateMinifiedThing(thingDef, quality, material);
				num--;
			}
			else
			{
				int num2 = Math.Min(num, thingDef.stackLimit);
				thing = ThingMaker.MakeThing(thingDef, material);
				thing.stackCount = num2;
				if (quality.HasValue && thingDef.HasComp(typeof(CompQuality)) && thing.TryGetQuality(out var _))
				{
					thing.TryGetComp<CompQuality>()?.SetQuality(quality.Value, ArtGenerationContext.Outsider);
				}
				num -= num2;
			}
			list.Add(thing);
		}
		return list;
	}

	public static bool IsSpaceMap(Map map)
	{
		if (map == null)
		{
			return false;
		}
		BiomeDef biome = map.Biome;
		if (biome != null && biome.inVacuum)
		{
			return true;
		}
		if (map.Parent is SpaceMapParent)
		{
			return true;
		}
		if (IsSealedOrPocketMap(map))
		{
			return false;
		}
		string text = map.Biome?.defName;
		if (string.IsNullOrEmpty(text))
		{
			return false;
		}
		if (text.Equals("Space", StringComparison.OrdinalIgnoreCase) || text.StartsWith("Space_", StringComparison.OrdinalIgnoreCase) || text.EndsWith("_Space", StringComparison.OrdinalIgnoreCase) || text.IndexOf("Orbit", StringComparison.OrdinalIgnoreCase) >= 0)
		{
			return true;
		}
		return false;
	}

	private static DeliveryResult HandleDirectPawnInteractionWithPreCreated(Thing item, Pawn pawn, bool equipItem, bool wearItem, bool addToInventory, DeliveryResult result)
	{
		result.DirectlyDeliveredItems.Add(item);
		if (equipItem && PawnItemHelper.EquipItemOnPawn(item, pawn))
		{
			result.PrimaryMethod = DeliveryMethod.Equipped;
		}
		else if (wearItem && PawnItemHelper.WearApparelOnPawn(item, pawn))
		{
			result.PrimaryMethod = DeliveryMethod.Worn;
		}
		else if (addToInventory && pawn.inventory.innerContainer.TryAdd(item))
		{
			result.PrimaryMethod = DeliveryMethod.Inventory;
		}
		else
		{
			TryDeliverToLocker(item, pawn.Map, pawn, result);
		}
		return result;
	}

	private static DeliveryResult HandleRegularDeliveryWithPreCreated(Thing item, Pawn pawn, DeliveryResult result)
	{
		Map map = ResolveDeliveryMap(pawn, allowUndergroundRedirect: false);
		Map surfaceHomeMap = GetSurfaceHomeMap();
		bool flag = map != null && IsSealedOrPocketMap(map);
		if (TryDeliverToLocker(item, map, pawn, result))
		{
			result.PrimaryMethod = DeliveryMethod.Locker;
			return result;
		}
		Map dropMapForItems = GetDropMapForItems(map);
		if (dropMapForItems == null)
		{
			Logger.Error("[ItemSpawn] No map for pre-created item drop-pod fallback");
			LogMapSnapshot("[ItemSpawn preCreated no-map]");
			return result;
		}
		result.RequestedCount = Math.Max(result.RequestedCount, item.stackCount);
		IntVec3 spawnPos = GetDeliveryPosition(dropMapForItems, pawn);
		List<Thing> list = new List<Thing> { item };
		List<Thing> list2 = new List<Thing>();
		List<Thing> list3 = new List<Thing>();
		bool flag2;
		if (IsSealedOrPocketMap(dropMapForItems) || dropMapForItems.Size.x < 40 || dropMapForItems.Size.z < 40)
		{
			flag2 = TryDirectSpawnItemsNearColony(list, dropMapForItems, out spawnPos, list2, list3);
		}
		else
		{
			flag2 = TryShuttleDelivery(list, spawnPos, dropMapForItems);
			if (flag2 && item != null && !item.Destroyed && (item.Spawned || item.ParentHolder != null))
			{
				list2.Add(item);
			}
			else if (!flag2)
			{
				flag2 = TryDirectSpawnItemsNearColony(list, dropMapForItems, out spawnPos, list2, list3);
			}
		}
		if (flag2 && list2.Count > 0)
		{
			result.DropPodDeliveredItems.AddRange(list2);
			result.DropPodDeliveredCount += list2.Sum((Thing t) => t.stackCount);
			result.PrimaryMethod = DeliveryMethod.DropPod;
			result.DeliveryPosition = spawnPos;
		}
		else
		{
			result.UndeliveredCount += CullUndeliveredThings((list3.Count > 0) ? list3 : list);
			result.PrimaryMethod = DeliveryMethod.Failed;
			Logger.Error("[ItemSpawn] preCreated " + item.def.defName + " failed locker and map delivery — culled");
		}
		return result;
	}
}

	public enum DeliveryMethod
	{
		Locker,
		DropPod,
		Inventory,
		Equipped,
		Worn,
		PawnDelivery,
		Failed
	}

	public class DeliveryResult
	{
		public List<Thing> LockerDeliveredItems { get; set; } = new List<Thing>();
		public List<Thing> DropPodDeliveredItems { get; set; } = new List<Thing>();
		public List<Thing> DirectlyDeliveredItems { get; set; } = new List<Thing>();
		public int LockerDeliveredCount { get; set; }
		public int DropPodDeliveredCount { get; set; }
		public int RequestedCount { get; set; }
		public int UndeliveredCount { get; set; }
		public IntVec3 DeliveryPosition { get; set; }
		public DeliveryMethod PrimaryMethod { get; set; }

		public int TotalUnitsDelivered =>
			LockerDeliveredCount + DropPodDeliveredCount +
			(DirectlyDeliveredItems?.Sum(t => t?.stackCount ?? 0) ?? 0);

		public bool NothingDelivered => TotalUnitsDelivered <= 0;
		public bool PartiallyDelivered => TotalUnitsDelivered > 0 && UndeliveredCount > 0;
	}
}