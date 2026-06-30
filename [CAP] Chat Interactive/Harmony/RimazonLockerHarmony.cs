//// Source/RICS/Harmony/RimazonLockerHarmony.cs
//// Copyright (c) Captolamia
//// This file is part of CAP Chat Interactive (RICS).
//// GNU Affero GPL v3 — keep open-source friendly.

using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace CAP_ChatInteractive
{
    [HarmonyPatch(typeof(WorkGiver_Flick), nameof(WorkGiver_Flick.HasJobOnThing))]
    public static class Patch_WorkGiver_Flick_HasJobOnLocker
    {
        static bool Prefix(Pawn pawn, Thing t, bool forced, ref bool __result)
        {
            if (t is Building_RimazonLocker locker && locker.allowPawnsToEmpty && locker.InnerContainer != null && locker.InnerContainer.Count > 0)
            {
                var desig = pawn.Map.designationManager.DesignationOn(t, DesignationDefOf.Flick);
                __result = desig != null && pawn.CanReserve(t, ignoreOtherReservations: forced);
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(WorkGiver_Flick), nameof(WorkGiver_Flick.JobOnThing))]
    public static class Patch_WorkGiver_Flick_JobOnLocker
    {
        static bool Prefix(Pawn pawn, Thing t, bool forced, ref Job __result)
        {
            if (t is Building_RimazonLocker locker && locker.allowPawnsToEmpty && locker.InnerContainer != null && locker.InnerContainer.Count > 0)
            {
                var desig = pawn.Map.designationManager.DesignationOn(t, DesignationDefOf.Flick);
                if (desig != null && pawn.CanReserve(t, ignoreOtherReservations: forced))
                {
                    __result = JobMaker.MakeJob(JobDefOf_CAP.CAP_EmptyLocker, t);
                    return false;
                }
            }
            return true;
        }
    }
}