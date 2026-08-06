// AiMapSliceBuilder.cs
// Copyright (c) Captolamia
// Part of RICS (Rimworld Interactive Chat Services) — AGPLv3
//
// Builds compact map slices for AI bot event/toast payloads (MAPGRID design).
// Strongly typed only — no dynamic. Fail soft; never invent cells.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using Verse;

namespace CAP_ChatInteractive.AI
{
    /// <summary>Top-level map slice for JSON event payloads.</summary>
    public sealed class AiMapSlicePayload
    {
        public AiMapSliceCenter center;
        public string relativeToColony;
        public int? directionFromNorth;
        public int sliceSize;
        public AiMapTerrainGrid terrainGrid;
        public List<AiMapCoverEntry> cover;
        public List<AiMapPawnEntry> pawns;
        public List<AiMapNotableEntry> notableThings;
        public List<AiMapFurnitureEntry> furniture;
        public string summary;
    }

    public sealed class AiMapSliceCenter
    {
        public int x;
        public int z;
    }

    public sealed class AiMapTerrainGrid
    {
        public int size;
        public bool northIsTop = true;
        public Dictionary<string, string> legend;
        public List<string> grid;
    }

    public sealed class AiMapCoverEntry
    {
        public string type;
        public int relX;
        public int relZ;
        public string size;
    }

    public sealed class AiMapPawnEntry
    {
        public string name;
        public string faction;
        public string status;
        public bool drafted;
        public string health;
        public string weapon;
        public int relX;
        public int relZ;
        public string job;
        /// <summary>True if RestUtility says pawn is in a bed (Building_Bed occupant).</summary>
        public bool inBed;
        /// <summary>True if laying/sleep job or in bed rest posture.</summary>
        public bool sleepingOrResting;
        /// <summary>Bed / crib / bedroll label if on or in rest furniture.</summary>
        public string furniture;
        /// <summary>Bed | Bedroll | Crib | SleepingSpot | HospitalBed | RestFurniture | null</summary>
        public string furnitureKind;
    }

    public sealed class AiMapNotableEntry
    {
        public string type;
        public string label;
        public int relX;
        public int relZ;
    }

    /// <summary>Furniture / rest object in the map slice (beds, cribs, tables, etc.).</summary>
    public sealed class AiMapFurnitureEntry
    {
        public string kind;
        public string label;
        public int relX;
        public int relZ;
        public string size;
        public bool isRestFurniture;
        public int? occupants;
    }

    /// <summary>
    /// Builds a compact event-centered map slice for Masie (terrain grid + nearby pawns/cover).
    /// </summary>
    public static class AiMapSliceBuilder
    {
        public const int SliceSizeLetter = 20;
        public const int SliceSizeToast = 12;
        public const int SliceSizeDeath = 12;

        private const int MinSize = 8;
        private const int MaxSize = 24;
        private const int MaxCover = 25;
        private const int MaxPawns = 20;
        private const int MaxNotable = 15;
        private const int MaxFurniture = 20;
        private const int MaxSummaryLen = 280;

        private static readonly Dictionary<string, string> TerrainLegend = new Dictionary<string, string>
        {
            { ".", "Soil" },
            { "g", "Gravel" },
            { "s", "Sand" },
            { "r", "Rock" },
            { "f", "Floor" },
            { "#", "Wall/impassable" },
            { "w", "Water" },
            { "m", "Mud" },
            { "?", "Other" }
        };

        /// <summary>
        /// Build slice around center. Returns null if map/cell invalid or on any failure.
        /// </summary>
        public static AiMapSlicePayload TryBuild(Map map, IntVec3 center, int sliceSize)
        {
            try
            {
                if (map == null || !center.IsValid)
                    return null;

                int size = UnityEngine.Mathf.Clamp(sliceSize, MinSize, MaxSize);
                // Prefer odd size so center cell is true middle
                if (size % 2 == 0)
                    size++;

                int half = size / 2;
                int minX = center.x - half;
                int maxX = center.x + half;
                int minZ = center.z - half;
                int maxZ = center.z + half;

                var gridRows = new List<string>(size);
                // North = top of grid = highest Z first
                for (int z = maxZ; z >= minZ; z--)
                {
                    var row = new StringBuilder(size);
                    for (int x = minX; x <= maxX; x++)
                    {
                        var cell = new IntVec3(x, 0, z);
                        row.Append(GetTerrainChar(map, cell));
                    }
                    gridRows.Add(row.ToString());
                }

                var cover = CollectCover(map, center, minX, maxX, minZ, maxZ);
                var pawns = CollectPawns(map, center, minX, maxX, minZ, maxZ);
                var notable = CollectNotable(map, center, minX, maxX, minZ, maxZ);
                var furniture = CollectFurniture(map, center, minX, maxX, minZ, maxZ);
                BuildRelativeToColony(map, center, out string relative, out int? dirFromNorth);
                string summary = BuildSummary(pawns, cover, notable, furniture);

                return new AiMapSlicePayload
                {
                    center = new AiMapSliceCenter { x = center.x, z = center.z },
                    relativeToColony = relative,
                    directionFromNorth = dirFromNorth,
                    sliceSize = size,
                    terrainGrid = new AiMapTerrainGrid
                    {
                        size = size,
                        northIsTop = true,
                        legend = new Dictionary<string, string>(TerrainLegend),
                        grid = gridRows
                    },
                    cover = cover,
                    pawns = pawns,
                    notableThings = notable,
                    furniture = furniture,
                    summary = summary
                };
            }
            catch (Exception ex)
            {
                Logger.Warning($"[RICS AI] Map slice build failed (non-fatal): {ex.Message}");
                return null;
            }
        }

        /// <summary>Build from AiMapLocation + map if still loaded.</summary>
        public static AiMapSlicePayload TryBuildFromLocation(AiMapLocation loc, int sliceSize)
        {
            if (loc == null)
                return null;

            try
            {
                Map map = Find.Maps?.FirstOrDefault(m => m != null && m.uniqueID == loc.mapId)
                          ?? Find.CurrentMap
                          ?? Find.AnyPlayerHomeMap;
                if (map == null)
                    return null;

                var cell = new IntVec3(loc.x, loc.y, loc.z);
                return TryBuild(map, cell, sliceSize);
            }
            catch
            {
                return null;
            }
        }

        private static char GetTerrainChar(Map map, IntVec3 cell)
        {
            try
            {
                if (!cell.InBounds(map))
                    return '?';

                // Impassable buildings / thick rock
                var edifice = cell.GetEdifice(map);
                if (edifice != null)
                {
                    if (edifice.def.passability == Traversability.Impassable ||
                        edifice.def.Fillage == FillCategory.Full)
                        return '#';
                }

                TerrainDef terrain = cell.GetTerrain(map);
                if (terrain == null)
                    return '?';

                string dn = terrain.defName ?? "";
                string label = terrain.label ?? "";

                if (terrain.IsWater || dn.IndexOf("Water", StringComparison.OrdinalIgnoreCase) >= 0)
                    return 'w';

                if (dn.IndexOf("Mud", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    label.IndexOf("mud", StringComparison.OrdinalIgnoreCase) >= 0)
                    return 'm';

                if (dn.IndexOf("Sand", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    label.IndexOf("sand", StringComparison.OrdinalIgnoreCase) >= 0)
                    return 's';

                if (dn.IndexOf("Gravel", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    label.IndexOf("gravel", StringComparison.OrdinalIgnoreCase) >= 0)
                    return 'g';

                if (dn.IndexOf("Rock", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    dn.IndexOf("Stone", StringComparison.OrdinalIgnoreCase) >= 0)
                    return 'r';

                // Constructed floors
                if (terrain.layerable || dn.IndexOf("Floor", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    dn.IndexOf("Tile", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    dn.IndexOf("Carpet", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    dn.IndexOf("Concrete", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    dn.IndexOf("Paved", StringComparison.OrdinalIgnoreCase) >= 0)
                    return 'f';

                if (terrain.passability == Traversability.Impassable)
                    return '#';

                return '.';
            }
            catch
            {
                return '?';
            }
        }

        private static List<AiMapCoverEntry> CollectCover(Map map, IntVec3 center, int minX, int maxX, int minZ, int maxZ)
        {
            var list = new List<AiMapCoverEntry>();
            try
            {
                var things = map.listerThings?.AllThings;
                if (things == null)
                    return list;

                foreach (Thing t in things)
                {
                    if (list.Count >= MaxCover)
                        break;
                    if (t == null || t.Destroyed || !t.Spawned)
                        continue;
                    if (t is Pawn)
                        continue;

                    IntVec3 p = t.Position;
                    if (p.x < minX || p.x > maxX || p.z < minZ || p.z > maxZ)
                        continue;

                    string type = ClassifyCover(t);
                    if (type == null)
                        continue;

                    string sizeStr = "1x1";
                    try
                    {
                        if (t.def?.size != null)
                            sizeStr = $"{t.def.size.x}x{t.def.size.z}";
                    }
                    catch { /* ignore */ }

                    list.Add(new AiMapCoverEntry
                    {
                        type = type,
                        relX = p.x - center.x,
                        relZ = p.z - center.z,
                        size = sizeStr
                    });
                }
            }
            catch { /* partial ok */ }

            return list;
        }

        private static string ClassifyCover(Thing t)
        {
            if (t?.def == null)
                return null;

            string dn = t.def.defName ?? "";
            string label = t.def.label ?? "";

            // Skip tiny clutter / plants (except trees)
            if (t.def.category == ThingCategory.Plant)
            {
                if (t.def.plant != null && t.def.plant.IsTree)
                    return "Tree";
                return null;
            }

            if (t.def.building != null)
            {
                if (dn.IndexOf("Sandbag", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    label.IndexOf("sandbag", StringComparison.OrdinalIgnoreCase) >= 0)
                    return "Sandbags";

                if (dn.IndexOf("Barricade", StringComparison.OrdinalIgnoreCase) >= 0)
                    return "Barricade";

                if (dn.IndexOf("Turret", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    label.IndexOf("turret", StringComparison.OrdinalIgnoreCase) >= 0)
                    return "Turret";

                if (t.def.passability == Traversability.Impassable || t.def.Fillage == FillCategory.Full)
                {
                    if (dn.IndexOf("Rock", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        dn.IndexOf("Natural", StringComparison.OrdinalIgnoreCase) >= 0)
                        return "RockWall";
                    if (t.def.building.isNaturalRock)
                        return "RockWall";
                    return "Wall";
                }

                // Partial cover buildings
                if (t.def.Fillage == FillCategory.Partial)
                {
                    if (dn.IndexOf("Door", StringComparison.OrdinalIgnoreCase) >= 0)
                        return null;
                    return t.def.label?.CapitalizeFirst() ?? "Cover";
                }
            }

            if (dn.IndexOf("Chunk", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Chunk";
            if (dn.IndexOf("Slag", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Slag";

            return null;
        }

        private static List<AiMapPawnEntry> CollectPawns(Map map, IntVec3 center, int minX, int maxX, int minZ, int maxZ)
        {
            var list = new List<AiMapPawnEntry>();
            try
            {
                var all = map.mapPawns?.AllPawnsSpawned;
                if (all == null)
                    return list;

                foreach (Pawn p in all)
                {
                    if (list.Count >= MaxPawns)
                        break;
                    if (p == null || p.Destroyed)
                        continue;

                    IntVec3 pos = p.PositionHeld.IsValid ? p.PositionHeld : p.Position;
                    if (!pos.IsValid || pos.x < minX || pos.x > maxX || pos.z < minZ || pos.z > maxZ)
                        continue;

                    string status = "Standing";
                    if (p.Dead)
                        status = "Dead";
                    else if (p.Downed)
                        status = "Downed";

                    bool inBed = false;
                    bool sleepingOrResting = false;
                    string furnitureLabel = null;
                    string furnitureKind = null;
                    try
                    {
                        if (!p.Dead)
                            DescribePawnRestFurniture(p, map, pos, out inBed, out sleepingOrResting, out furnitureLabel, out furnitureKind);

                        if (status == "Standing" || status == "Downed")
                        {
                            if (inBed && sleepingOrResting)
                                status = "SleepingInBed";
                            else if (inBed)
                                status = "InBed";
                            else if (sleepingOrResting && furnitureKind != null)
                                status = "RestingOnFurniture";
                            else if (sleepingOrResting)
                                status = "Resting";
                            else if (furnitureKind != null && IsRestFurnitureKind(furnitureKind))
                                status = "OnBedFurniture"; // standing/downed on bed cell
                        }
                    }
                    catch { /* ignore rest detect */ }

                    string factionTag = "Neutral";
                    try
                    {
                        if (p.Faction != null)
                        {
                            if (p.Faction.IsPlayer)
                                factionTag = "Player";
                            else if (p.Faction.HostileTo(Faction.OfPlayer))
                                factionTag = "Enemy";
                            else
                                factionTag = "Neutral";
                        }
                    }
                    catch { /* ignore */ }

                    string health = "Healthy";
                    try
                    {
                        if (p.Dead)
                            health = "Dead";
                        else if (p.health?.summaryHealth != null)
                        {
                            float pct = p.health.summaryHealth.SummaryHealthPercent;
                            if (pct < 0.35f) health = "Critical";
                            else if (pct < 0.7f) health = "Injured";
                            else if (p.health.hediffSet?.HasTendableHediff() == true) health = "Wounded";
                            else health = "Healthy";
                        }
                    }
                    catch { /* ignore */ }

                    string weapon = "Unarmed";
                    try
                    {
                        if (p.equipment?.Primary != null)
                            weapon = p.equipment.Primary.LabelCap ?? p.equipment.Primary.def?.label ?? "Weapon";
                    }
                    catch { /* ignore */ }

                    string job = null;
                    try
                    {
                        if (!p.Dead)
                            job = p.jobs?.curDriver?.GetReport() ?? p.jobs?.curJob?.def?.label;
                        if (job != null && job.Length > 40)
                            job = job.Substring(0, 40) + "…";
                    }
                    catch { /* ignore */ }

                    bool drafted = false;
                    try { drafted = p.Drafted; } catch { /* ignore */ }

                    list.Add(new AiMapPawnEntry
                    {
                        name = p.LabelShortCap ?? p.Name?.ToStringShort ?? "Unknown",
                        faction = factionTag,
                        status = status,
                        drafted = drafted,
                        health = health,
                        weapon = weapon,
                        relX = pos.x - center.x,
                        relZ = pos.z - center.z,
                        job = job,
                        inBed = inBed,
                        sleepingOrResting = sleepingOrResting,
                        furniture = furnitureLabel,
                        furnitureKind = furnitureKind
                    });
                }
            }
            catch { /* partial ok */ }

            return list;
        }

        /// <summary>
        /// Rest/sleep + furniture under a pawn. Used by map slice and location enrichment.
        /// </summary>
        public static void DescribePawnRestFurniture(
            Pawn p,
            Map map,
            IntVec3 pos,
            out bool inBed,
            out bool sleepingOrResting,
            out string furnitureLabel,
            out string furnitureKind)
        {
            inBed = false;
            sleepingOrResting = false;
            furnitureLabel = null;
            furnitureKind = null;

            if (p == null)
                return;

            try
            {
                Building_Bed currentBed = null;
                try
                {
                    if (p.Spawned)
                        currentBed = p.CurrentBed();
                }
                catch { /* API variance */ }

                if (currentBed != null)
                {
                    inBed = true;
                    furnitureLabel = currentBed.LabelCap ?? currentBed.def?.label;
                    furnitureKind = ClassifyRestFurniture(currentBed);
                }

                // Laying / sleep job even without CurrentBed (floor rest, interrupted, etc.)
                try
                {
                    var posture = p.GetPosture();
                    if (posture.Laying() || posture.InBed())
                        sleepingOrResting = true;
                }
                catch { /* ignore */ }

                try
                {
                    if (p.jobs?.curJob?.def == JobDefOf.LayDown)
                        sleepingOrResting = true;
                }
                catch { /* ignore */ }

                if (inBed)
                    sleepingOrResting = true;

                // Co-located with bed/bedroll/crib even if not currently InBed()
                if (furnitureKind == null && map != null && pos.IsValid)
                {
                    if (TryFindRestFurnitureAt(map, pos, out Thing restThing, out string kind, out string label))
                    {
                        furnitureKind = kind;
                        furnitureLabel = label;
                        // Same cell as rest furniture is useful even when standing on it
                    }
                }
            }
            catch { /* best effort */ }
        }

        public static bool IsRestFurnitureKind(string kind)
        {
            if (string.IsNullOrEmpty(kind))
                return false;
            return kind == "Bed" || kind == "Bedroll" || kind == "Crib" ||
                   kind == "SleepingSpot" || kind == "HospitalBed" || kind == "RestFurniture";
        }

        /// <summary>
        /// Find bed / bedroll / crib / similar at cell (or multi-cell bed covering this cell).
        /// </summary>
        public static bool TryFindRestFurnitureAt(Map map, IntVec3 cell, out Thing restThing, out string kind, out string label)
        {
            restThing = null;
            kind = null;
            label = null;
            if (map == null || !cell.IsValid)
                return false;

            try
            {
                // Fast path: first Building_Bed on cell
                try
                {
                    var bed = cell.GetFirstThing<Building_Bed>(map);
                    if (bed != null && !bed.Destroyed)
                    {
                        restThing = bed;
                        kind = ClassifyRestFurniture(bed);
                        label = bed.LabelCap ?? bed.def?.label;
                        return true;
                    }
                }
                catch { /* ignore */ }

                List<Thing> things = null;
                try { things = cell.GetThingList(map); } catch { /* ignore */ }
                if (things != null)
                {
                    foreach (Thing t in things)
                    {
                        if (t == null || t.Destroyed || t is Pawn)
                            continue;
                        if (!IsRestFurnitureThing(t))
                            continue;
                        restThing = t;
                        kind = ClassifyRestFurniture(t);
                        label = t.LabelCap ?? t.def?.label;
                        return true;
                    }
                }

                // Multi-cell beds: scan nearby buildings for OccupiedRect containing cell
                try
                {
                    var buildings = map.listerBuildings?.allBuildingsColonist;
                    // Also check all buildings via listerThings for non-colony beds
                    var allBeds = map.listerThings?.ThingsInGroup(ThingRequestGroup.Bed);
                    if (allBeds != null)
                    {
                        foreach (Thing t in allBeds)
                        {
                            if (t == null || t.Destroyed || !t.Spawned)
                                continue;
                            try
                            {
                                if (t.OccupiedRect().Contains(cell))
                                {
                                    restThing = t;
                                    kind = ClassifyRestFurniture(t);
                                    label = t.LabelCap ?? t.def?.label;
                                    return true;
                                }
                            }
                            catch { /* continue */ }
                        }
                    }
                    else if (buildings != null)
                    {
                        foreach (Building b in buildings)
                        {
                            if (b == null || !(b is Building_Bed) && !IsRestFurnitureThing(b))
                                continue;
                            try
                            {
                                if (b.OccupiedRect().Contains(cell))
                                {
                                    restThing = b;
                                    kind = ClassifyRestFurniture(b);
                                    label = b.LabelCap ?? b.def?.label;
                                    return true;
                                }
                            }
                            catch { /* continue */ }
                        }
                    }
                }
                catch { /* ignore */ }
            }
            catch { /* ignore */ }

            return false;
        }

        public static bool IsRestFurnitureThing(Thing t)
        {
            if (t?.def == null)
                return false;
            if (t is Building_Bed)
                return true;
            try
            {
                if (t.def.IsBed)
                    return true;
            }
            catch { /* ignore */ }

            string dn = t.def.defName ?? "";
            string label = t.def.label ?? "";
            // Bedrolls, cribs, sleeping spots, animal beds, deathrest, etc.
            if (dn.IndexOf("Bed", StringComparison.OrdinalIgnoreCase) >= 0 ||
                dn.IndexOf("Crib", StringComparison.OrdinalIgnoreCase) >= 0 ||
                dn.IndexOf("Bedroll", StringComparison.OrdinalIgnoreCase) >= 0 ||
                dn.IndexOf("Sleeping", StringComparison.OrdinalIgnoreCase) >= 0 ||
                dn.IndexOf("Deathrest", StringComparison.OrdinalIgnoreCase) >= 0 ||
                label.IndexOf("bedroll", StringComparison.OrdinalIgnoreCase) >= 0 ||
                label.IndexOf("crib", StringComparison.OrdinalIgnoreCase) >= 0 ||
                label.IndexOf("sleeping spot", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;

            return false;
        }

        public static string ClassifyRestFurniture(Thing t)
        {
            if (t?.def == null)
                return "RestFurniture";

            string dn = t.def.defName ?? "";
            string label = t.def.label ?? "";

            if (dn.IndexOf("Crib", StringComparison.OrdinalIgnoreCase) >= 0 ||
                label.IndexOf("crib", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Crib";

            if (dn.IndexOf("Bedroll", StringComparison.OrdinalIgnoreCase) >= 0 ||
                label.IndexOf("bedroll", StringComparison.OrdinalIgnoreCase) >= 0 ||
                dn.IndexOf("SleepingSpot", StringComparison.OrdinalIgnoreCase) >= 0 ||
                label.IndexOf("sleeping spot", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                if (dn.IndexOf("SleepingSpot", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    label.IndexOf("sleeping spot", StringComparison.OrdinalIgnoreCase) >= 0)
                    return "SleepingSpot";
                return "Bedroll";
            }

            if (t is Building_Bed bed)
            {
                try
                {
                    if (bed.Medical)
                        return "HospitalBed";
                }
                catch { /* ignore */ }
                return "Bed";
            }

            if (dn.IndexOf("Bed", StringComparison.OrdinalIgnoreCase) >= 0 ||
                label.IndexOf("bed", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Bed";

            return "RestFurniture";
        }

        /// <summary>General furniture (tables, chairs, beds, etc.) for map slice context.</summary>
        public static string ClassifyFurnitureKind(Thing t)
        {
            if (t?.def == null)
                return null;

            if (IsRestFurnitureThing(t))
                return ClassifyRestFurniture(t);

            // Only buildings that look like furniture / work stations of interest
            if (t.def.category != ThingCategory.Building)
                return null;

            string dn = t.def.defName ?? "";
            string label = t.def.label ?? "";

            // Skip pure structure
            if (t.def.passability == Traversability.Impassable && t.def.Fillage == FillCategory.Full)
                return null;
            if (dn.IndexOf("Wall", StringComparison.OrdinalIgnoreCase) >= 0 ||
                dn.IndexOf("Door", StringComparison.OrdinalIgnoreCase) >= 0 ||
                dn.IndexOf("PowerConduit", StringComparison.OrdinalIgnoreCase) >= 0)
                return null;

            if (dn.IndexOf("Table", StringComparison.OrdinalIgnoreCase) >= 0 || label.IndexOf("table", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Table";
            if (dn.IndexOf("Chair", StringComparison.OrdinalIgnoreCase) >= 0 || dn.IndexOf("Stool", StringComparison.OrdinalIgnoreCase) >= 0 ||
                label.IndexOf("chair", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Chair";
            if (dn.IndexOf("Dresser", StringComparison.OrdinalIgnoreCase) >= 0 || dn.IndexOf("EndTable", StringComparison.OrdinalIgnoreCase) >= 0)
                return "BedroomFurniture";
            if (dn.IndexOf("Torch", StringComparison.OrdinalIgnoreCase) >= 0 || dn.IndexOf("Lamp", StringComparison.OrdinalIgnoreCase) >= 0 ||
                dn.IndexOf("Light", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Light";
            if (t.def.building != null && t.def.building.isSittable)
                return "Seat";

            // Surfaces / recreation that help context
            if (dn.IndexOf("Television", StringComparison.OrdinalIgnoreCase) >= 0 ||
                dn.IndexOf("Chess", StringComparison.OrdinalIgnoreCase) >= 0 ||
                dn.IndexOf("Poker", StringComparison.OrdinalIgnoreCase) >= 0 ||
                dn.IndexOf("Billiards", StringComparison.OrdinalIgnoreCase) >= 0 ||
                dn.IndexOf("Instrument", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Recreation";

            // Workbenches are furniture-like for context but skip to reduce noise unless near
            if (t.def.hasInteractionCell || (t.def.building?.isEdifice == true && t.def.Fillage == FillCategory.Partial))
            {
                // Prefer labeled furniture categories
                try
                {
                    if (t.def.designationCategory != null)
                    {
                        string cat = t.def.designationCategory.defName ?? "";
                        if (cat.IndexOf("Furniture", StringComparison.OrdinalIgnoreCase) >= 0)
                            return "Furniture";
                    }
                }
                catch { /* ignore */ }
            }

            try
            {
                if (t.def.designationCategory != null)
                {
                    string cat = t.def.designationCategory.defName ?? "";
                    if (cat.IndexOf("Furniture", StringComparison.OrdinalIgnoreCase) >= 0)
                        return "Furniture";
                }
            }
            catch { /* ignore */ }

            return null;
        }

        private static List<AiMapFurnitureEntry> CollectFurniture(Map map, IntVec3 center, int minX, int maxX, int minZ, int maxZ)
        {
            var list = new List<AiMapFurnitureEntry>();
            var seen = new HashSet<int>();
            try
            {
                // Prefer beds group + full thing scan for furniture designation
                var candidates = new List<Thing>();
                try
                {
                    var beds = map.listerThings?.ThingsInGroup(ThingRequestGroup.Bed);
                    if (beds != null)
                        candidates.AddRange(beds);
                }
                catch { /* ignore */ }

                try
                {
                    var all = map.listerThings?.AllThings;
                    if (all != null)
                    {
                        foreach (Thing t in all)
                        {
                            if (t == null || t is Pawn || t.Destroyed || !t.Spawned)
                                continue;
                            if (ClassifyFurnitureKind(t) != null)
                                candidates.Add(t);
                        }
                    }
                }
                catch { /* ignore */ }

                foreach (Thing t in candidates)
                {
                    if (list.Count >= MaxFurniture)
                        break;
                    if (t == null || t.Destroyed || !t.Spawned)
                        continue;
                    if (!seen.Add(t.thingIDNumber))
                        continue;

                    IntVec3 p = t.Position;
                    // Include if any part of multi-cell furniture intersects slice
                    bool inSlice = p.x >= minX && p.x <= maxX && p.z >= minZ && p.z <= maxZ;
                    if (!inSlice)
                    {
                        try
                        {
                            var rect = t.OccupiedRect();
                            inSlice = rect.minX <= maxX && rect.maxX >= minX && rect.minZ <= maxZ && rect.maxZ >= minZ;
                            if (inSlice)
                                p = rect.CenterCell;
                        }
                        catch { /* keep point check */ }
                    }
                    if (!inSlice)
                        continue;

                    string kind = ClassifyFurnitureKind(t);
                    if (kind == null)
                        continue;

                    string sizeStr = "1x1";
                    try
                    {
                        if (t.def?.size != null)
                            sizeStr = $"{t.def.size.x}x{t.def.size.z}";
                    }
                    catch { /* ignore */ }

                    int? occupants = null;
                    try
                    {
                        if (t is Building_Bed bed)
                        {
                            int n = 0;
                            for (int i = 0; i < bed.SleepingSlotsCount; i++)
                            {
                                if (bed.GetCurOccupant(i) != null)
                                    n++;
                            }
                            occupants = n;
                        }
                    }
                    catch { /* ignore */ }

                    list.Add(new AiMapFurnitureEntry
                    {
                        kind = kind,
                        label = t.LabelCap ?? t.def?.label ?? kind,
                        relX = p.x - center.x,
                        relZ = p.z - center.z,
                        size = sizeStr,
                        isRestFurniture = IsRestFurnitureKind(kind),
                        occupants = occupants
                    });
                }
            }
            catch { /* partial ok */ }

            return list;
        }

        private static List<AiMapNotableEntry> CollectNotable(Map map, IntVec3 center, int minX, int maxX, int minZ, int maxZ)
        {
            var list = new List<AiMapNotableEntry>();
            try
            {
                var things = map.listerThings?.AllThings;
                if (things == null)
                    return list;

                foreach (Thing t in things)
                {
                    if (list.Count >= MaxNotable)
                        break;
                    if (t == null || t.Destroyed || !t.Spawned || t is Pawn)
                        continue;

                    IntVec3 p = t.Position;
                    if (p.x < minX || p.x > maxX || p.z < minZ || p.z > maxZ)
                        continue;

                    string type = null;
                    string label = t.LabelCap ?? t.def?.label;

                    if (t is Corpse)
                        type = "Corpse";
                    else if (t.def?.category == ThingCategory.Filth ||
                             (t.def?.defName?.IndexOf("Filth", StringComparison.OrdinalIgnoreCase) ?? -1) >= 0 ||
                             (t.def?.defName?.IndexOf("Blood", StringComparison.OrdinalIgnoreCase) ?? -1) >= 0)
                        type = "Blood";
                    else if (t.def?.IsWeapon == true && t.def.category == ThingCategory.Item)
                        type = "Weapon";
                    else if (t.def?.IsMedicine == true)
                        type = "Medicine";

                    if (type == null)
                        continue;

                    list.Add(new AiMapNotableEntry
                    {
                        type = type,
                        label = label,
                        relX = p.x - center.x,
                        relZ = p.z - center.z
                    });
                }
            }
            catch { /* partial ok */ }

            return list;
        }

        private static void BuildRelativeToColony(Map map, IntVec3 center, out string relative, out int? directionFromNorth)
        {
            relative = null;
            directionFromNorth = null;

            try
            {
                Map home = Find.AnyPlayerHomeMap ?? (map.IsPlayerHome ? map : null);
                if (home == null)
                {
                    relative = "colony center unknown";
                    return;
                }

                // Same map: relative to map/colony center of mass of free colonists
                IntVec3 colonyCenter = home.Center;
                try
                {
                    var colonists = home.mapPawns?.FreeColonistsSpawned;
                    if (colonists != null && colonists.Count > 0)
                    {
                        long sx = 0, sz = 0;
                        int n = 0;
                        foreach (var c in colonists)
                        {
                            if (c == null || !c.Position.IsValid) continue;
                            sx += c.Position.x;
                            sz += c.Position.z;
                            n++;
                        }
                        if (n > 0)
                            colonyCenter = new IntVec3((int)(sx / n), 0, (int)(sz / n));
                    }
                }
                catch { /* use map center */ }

                if (map.uniqueID != home.uniqueID)
                {
                    relative = "on a different map from the home colony";
                    return;
                }

                int dx = center.x - colonyCenter.x;
                int dz = center.z - colonyCenter.z;
                double dist = Math.Sqrt(dx * (double)dx + dz * (double)dz);
                int distCells = (int)Math.Round(dist);

                // RimWorld: +Z is north, +X is east
                double angle = Math.Atan2(dx, dz) * (180.0 / Math.PI); // 0 = north, + = east
                if (angle < 0) angle += 360.0;
                directionFromNorth = (int)Math.Round(angle);

                string compass = AngleToCompass(angle);
                if (distCells < 3)
                    relative = "near colony center";
                else
                    relative = $"{distCells} cells {compass} of colony center";
            }
            catch
            {
                relative = "colony relative position unknown";
            }
        }

        private static string AngleToCompass(double angleDeg)
        {
            // 0=N, 45=NE, 90=E, ...
            string[] dirs = { "N", "NNE", "NE", "ENE", "E", "ESE", "SE", "SSE",
                              "S", "SSW", "SW", "WSW", "W", "WNW", "NW", "NNW" };
            int idx = (int)Math.Floor((angleDeg + 11.25) / 22.5) % 16;
            if (idx < 0) idx += 16;
            return dirs[idx];
        }

        private static string BuildSummary(List<AiMapPawnEntry> pawns, List<AiMapCoverEntry> cover, List<AiMapNotableEntry> notable, List<AiMapFurnitureEntry> furniture)
        {
            try
            {
                int player = pawns?.Count(p => p.faction == "Player") ?? 0;
                int enemy = pawns?.Count(p => p.faction == "Enemy") ?? 0;
                int neutral = pawns?.Count(p => p.faction == "Neutral") ?? 0;
                int downed = pawns?.Count(p => p.status == "Downed") ?? 0;
                int dead = pawns?.Count(p => p.status == "Dead") ?? 0;
                int drafted = pawns?.Count(p => p.drafted) ?? 0;
                int inBed = pawns?.Count(p => p.inBed || p.status == "InBed" || p.status == "SleepingInBed") ?? 0;
                int resting = pawns?.Count(p => p.sleepingOrResting) ?? 0;

                var parts = new List<string>();
                if (enemy > 0) parts.Add($"{enemy} enemy pawn(s)");
                if (player > 0) parts.Add($"{player} player pawn(s)" + (drafted > 0 ? $" ({drafted} drafted)" : ""));
                if (neutral > 0) parts.Add($"{neutral} neutral");
                if (downed > 0) parts.Add($"{downed} downed");
                if (dead > 0) parts.Add($"{dead} dead nearby");
                if (inBed > 0) parts.Add($"{inBed} in bed/crib");
                else if (resting > 0) parts.Add($"{resting} resting/sleeping");

                bool hasBags = cover?.Any(c => c.type == "Sandbags") == true;
                bool hasWall = cover?.Any(c => c.type == "Wall" || c.type == "RockWall") == true;
                bool hasTurret = cover?.Any(c => c.type == "Turret") == true;
                if (hasBags) parts.Add("sandbags present");
                if (hasWall) parts.Add("walls/rock nearby");
                if (hasTurret) parts.Add("turret(s)");

                int corpses = notable?.Count(n => n.type == "Corpse") ?? 0;
                if (corpses > 0) parts.Add($"{corpses} corpse(s)");

                int restFurn = furniture?.Count(f => f.isRestFurniture) ?? 0;
                int otherFurn = furniture?.Count(f => !f.isRestFurniture) ?? 0;
                if (restFurn > 0) parts.Add($"{restFurn} bed/crib/bedroll nearby");
                if (otherFurn > 0) parts.Add($"{otherFurn} furniture nearby");

                if (parts.Count == 0)
                    return "Quiet area around the event cell.";

                string s = string.Join("; ", parts) + ".";
                if (s.Length > MaxSummaryLen)
                    s = s.Substring(0, MaxSummaryLen - 1) + "…";
                return s;
            }
            catch
            {
                return "Map slice available.";
            }
        }
    }
}
