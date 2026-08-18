// Patch_RICS_Ownership_Inheritance.cs
// Copyright (c) Captolamia
// Part of CAP Chat Interactive (RICS) — AGPLv3
//
// IMPORTANT (anti Possessions Plus bugs):
// Use Postfix on Pawn.Kill — NEVER Prefix. PP's Prefix inheritance interacted badly with
// death/armor patches (unkillable pawns). We only run after the pawn is actually dead.
using CAP_ChatInteractive.Ownership.Inheritance;
using HarmonyLib;
using RimWorld;
using System.Collections.Generic;
using Verse;

namespace CAP_ChatInteractive.Ownership.Harmony
{
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.Kill))]
    public static class Patch_RICS_Ownership_Inheritance
    {
        private static readonly HashSet<int> _handledThisTick = new HashSet<int>();
        private static int _lastTick = -1;

        [HarmonyPostfix]
        public static void Postfix(Pawn __instance)
        {
            try
            {
                if (__instance == null || !__instance.Dead)
                    return;
                if (!RICS_OwnershipUtility.IsRicsOwnershipActive())
                    return;

                int tick = Find.TickManager?.TicksGame ?? 0;
                if (tick != _lastTick)
                {
                    _lastTick = tick;
                    _handledThisTick.Clear();
                }

                // One inheritance pass per pawn death event
                if (!_handledThisTick.Add(__instance.thingIDNumber))
                    return;

                RICS_InheritanceProcessor.RunForDeceased(__instance);
            }
            catch (System.Exception ex)
            {
                Logger.Warning($"[RICS Ownership] Death Postfix inheritance error (non-fatal): {ex.Message}");
            }
        }
    }
}
