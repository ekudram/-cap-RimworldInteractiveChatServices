// Source/RICS/Harmony/RimazonLockerHarmony.cs
// Copyright (c) Captolamia
// This file is part of CAP Chat Interactive (RICS).
// GNU Affero GPL v3 — keep open-source friendly.

using HarmonyLib;
using RimWorld;
using Verse;
using System;

namespace CAP_ChatInteractive
{
    /// <summary>
    /// Targeted Harmony fixes for RimazonLocker.
    /// 1. Prevents VPE/rot mod NREs when items inside the locker tick (CompRottable → AmbientTemperature).
    /// 2. Forces correct roof detection on the locker cell so rain/snow visuals never appear when roofed.
    /// Runs automatically on startup.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class RimazonLockerHarmony
    {
        static RimazonLockerHarmony()
        {
            var harmony = new Harmony("CAP_ChatInteractive.RimazonLockerFixes");
            Logger.Debug("[RICS] Applying RimazonLocker Harmony patches...");

            // Patch 1: Defensive temperature check (fixes the exact NRE you are seeing)
            harmony.Patch(
                original: AccessTools.Method(typeof(GenTemperature), nameof(GenTemperature.GetTemperatureForCell), new[] { typeof(IntVec3), typeof(Map) }),
                prefix: new HarmonyMethod(typeof(RimazonLockerHarmony), nameof(SafeGetTemperatureForCell))
            );

            // Patch 2: Force roofed status for the locker cell (fixes rain visuals)
            harmony.Patch(
                original: AccessTools.Method(typeof(GridsUtility), nameof(GridsUtility.Roofed), new[] { typeof(IntVec3), typeof(Map) }),
                postfix: new HarmonyMethod(typeof(RimazonLockerHarmony), nameof(ForceLockerRoofedPostfix))
            );

            Logger.Debug("[RICS] RimazonLocker Harmony patches applied successfully");
        }

        /// <summary>
        /// Prefix for GenTemperature.GetTemperatureForCell.
        /// Catches the exact NRE from VPE and other rot/heat mods when an item inside our custom ThingOwner is ticked.
        /// </summary>
        private static bool SafeGetTemperatureForCell(IntVec3 c, Map map, ref float __result)
        {
            if (map == null)
            {
                __result = 21f; // neutral fallback
                return false;   // skip original + all other prefixes (prevents VPE crash)
            }

            // If the cell has a RimazonLocker, use the locker's own safe temperature
            if (c.InBounds(map))
            {
                Building edifice = c.GetEdifice(map);
                if (edifice is Building_RimazonLocker locker && locker.Spawned)
                {
                    __result = GenTemperature.GetTemperatureForCell(locker.Position, locker.Map);
                    return false; // short-circuit safely
                }
            }

            return true; // let vanilla + other mods run normally
        }

        /// <summary>
        /// Postfix for GridsUtility.Roofed(IntVec3, Map).
        /// Forces the locker cell to report as roofed when it actually is (fixes rain visuals on the tile).
        /// </summary>
        private static void ForceLockerRoofedPostfix(IntVec3 c, Map map, ref bool __result)
        {
            if (map == null || !c.InBounds(map)) return;

            Building edifice = c.GetEdifice(map);
            if (edifice is Building_RimazonLocker locker && locker.Spawned)
            {
                __result = map.roofGrid.Roofed(c); // force correct roof status
            }
        }
    }
}