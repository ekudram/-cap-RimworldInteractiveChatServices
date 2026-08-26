// Patch_RICS_PawnInspectTabs.cs
// Copyright (c) Captolamia — AGPLv3
// GetInspectTabs is declared on Thing, not Pawn — Harmony must patch the declaring type.
using HarmonyLib;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace CAP_ChatInteractive.Ownership.Harmony
{
    [HarmonyPatch(typeof(Thing), nameof(Thing.GetInspectTabs))]
    public static class Patch_RICS_PawnInspectTabs
    {
        [HarmonyPostfix]
        public static void Postfix(Thing __instance, ref IEnumerable<InspectTabBase> __result)
        {
            try
            {
                if (__instance is not Pawn pawn || __result == null)
                    return;
                if (!RICS_OwnershipUtility.IsRicsOwnershipActive())
                    return;
                if (pawn.Faction != Faction.OfPlayer || !pawn.IsColonist)
                    return;

                var tab = InspectTabManager.GetSharedInstance(typeof(ITab_RICS_Owned));
                if (tab == null)
                    return;
                if (__result.Contains(tab))
                    return;

                var list = __result as IList<InspectTabBase> ?? __result.ToList();
                list.Add(tab);
                __result = list;
            }
            catch
            {
                // never break inspect pane or PatchAll
            }
        }
    }
}
