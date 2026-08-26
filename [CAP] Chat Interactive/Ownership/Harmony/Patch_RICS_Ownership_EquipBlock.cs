// Patch_RICS_Ownership_EquipBlock.cs
// Copyright (c) Captolamia
// Part of CAP Chat Interactive (RICS) — AGPLv3
//
// Blocks equip/wear of RICS-owned items by non-owners. All patches no-op when RICS ownership is off
// or Possessions Plus is active (PP owns that path).
using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace CAP_ChatInteractive.Ownership.Harmony
{
    [HarmonyPatch(typeof(JobGiver_OptimizeApparel), "ApparelScoreRaw")]
    public static class Patch_RICS_Ownership_ApparelScore
    {
        [HarmonyPostfix]
        public static void Postfix(Pawn pawn, Apparel ap, ref float __result)
        {
            if (!RICS_OwnershipUtility.IsRicsOwnershipActive())
                return;
            if (ap == null || pawn == null)
                return;
            if (RICS_OwnershipUtility.BlocksUseBy(ap, pawn))
                __result = float.NegativeInfinity;
        }
    }

    [HarmonyPatch(typeof(JobDriver_Equip), "MakeNewToils")]
    public static class Patch_RICS_Ownership_EquipToils
    {
        [HarmonyPostfix]
        public static void Postfix(JobDriver_Equip __instance, ref IEnumerable<Toil> __result)
        {
            if (!RICS_OwnershipUtility.IsRicsOwnershipActive() || __instance == null || __result == null)
                return;

            var list = new List<Toil>(__result);
            if (list.Count == 0)
                return;

            list[0].AddPreInitAction(() =>
            {
                try
                {
                    Pawn pawn = __instance.pawn;
                    Thing thing = __instance.job?.targetA.Thing;
                    if (thing == null || pawn == null)
                        return;
                    if (!RICS_OwnershipUtility.BlocksUseBy(thing, pawn))
                        return;

                    string ownerName = RICS_OwnershipUtility.GetOwner(thing)?.LabelShortCap ?? "someone";
                    Messages.Message(
                        "RICS.Ownership.Block.Equip".Translate(pawn.LabelShortCap, thing.LabelNoCount, ownerName),
                        pawn,
                        MessageTypeDefOf.RejectInput,
                        historical: false);
                    __instance.EndJobWith(JobCondition.Incompletable);
                }
                catch
                {
                    // Never let ownership block crash a job driver (PP-style death/equip bugs)
                }
            });
            __result = list;
        }
    }

    [HarmonyPatch(typeof(JobDriver_Wear), "MakeNewToils")]
    public static class Patch_RICS_Ownership_WearToils
    {
        [HarmonyPostfix]
        public static void Postfix(JobDriver_Wear __instance, ref IEnumerable<Toil> __result)
        {
            if (!RICS_OwnershipUtility.IsRicsOwnershipActive() || __instance == null || __result == null)
                return;

            var list = new List<Toil>(__result);
            if (list.Count == 0)
                return;

            list[0].AddPreInitAction(() =>
            {
                try
                {
                    Pawn pawn = __instance.pawn;
                    Thing thing = __instance.job?.targetA.Thing;
                    if (thing == null || pawn == null)
                        return;
                    if (!RICS_OwnershipUtility.BlocksUseBy(thing, pawn))
                        return;

                    string ownerName = RICS_OwnershipUtility.GetOwner(thing)?.LabelShortCap ?? "someone";
                    Messages.Message(
                        "RICS.Ownership.Block.Wear".Translate(pawn.LabelShortCap, thing.LabelNoCount, ownerName),
                        pawn,
                        MessageTypeDefOf.RejectInput,
                        historical: false);
                    __instance.EndJobWith(JobCondition.Incompletable);
                }
                catch { }
            });
            __result = list;
        }
    }
}
