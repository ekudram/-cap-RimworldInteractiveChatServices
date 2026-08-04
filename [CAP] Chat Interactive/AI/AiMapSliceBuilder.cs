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
    }

    public sealed class AiMapNotableEntry
    {
        public string type;
        public string label;
        public int relX;
        public int relZ;
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
        private const int MaxSummaryLen = 240;

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
                BuildRelativeToColony(map, center, out string relative, out int? dirFromNorth);
                string summary = BuildSummary(pawns, cover, notable);

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
                        job = job
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

        private static string BuildSummary(List<AiMapPawnEntry> pawns, List<AiMapCoverEntry> cover, List<AiMapNotableEntry> notable)
        {
            try
            {
                int player = pawns?.Count(p => p.faction == "Player") ?? 0;
                int enemy = pawns?.Count(p => p.faction == "Enemy") ?? 0;
                int neutral = pawns?.Count(p => p.faction == "Neutral") ?? 0;
                int downed = pawns?.Count(p => p.status == "Downed") ?? 0;
                int dead = pawns?.Count(p => p.status == "Dead") ?? 0;
                int drafted = pawns?.Count(p => p.drafted) ?? 0;

                var parts = new List<string>();
                if (enemy > 0) parts.Add($"{enemy} enemy pawn(s)");
                if (player > 0) parts.Add($"{player} player pawn(s)" + (drafted > 0 ? $" ({drafted} drafted)" : ""));
                if (neutral > 0) parts.Add($"{neutral} neutral");
                if (downed > 0) parts.Add($"{downed} downed");
                if (dead > 0) parts.Add($"{dead} dead nearby");

                bool hasBags = cover?.Any(c => c.type == "Sandbags") == true;
                bool hasWall = cover?.Any(c => c.type == "Wall" || c.type == "RockWall") == true;
                bool hasTurret = cover?.Any(c => c.type == "Turret") == true;
                if (hasBags) parts.Add("sandbags present");
                if (hasWall) parts.Add("walls/rock nearby");
                if (hasTurret) parts.Add("turret(s)");

                int corpses = notable?.Count(n => n.type == "Corpse") ?? 0;
                if (corpses > 0) parts.Add($"{corpses} corpse(s)");

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
