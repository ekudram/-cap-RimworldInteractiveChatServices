// Patch_RICS_Ownership_TradeBlock.cs
// Copyright (c) Captolamia
// Part of CAP Chat Interactive (RICS) — AGPLv3
using HarmonyLib;
using RimWorld;
using System.Collections.Generic;
using Verse;

namespace CAP_ChatInteractive.Ownership.Harmony
{
    // AddAllTradeables is private in RimWorld 1.6 — must patch by name string (not nameof).
    [HarmonyPatch(typeof(TradeDeal), "AddAllTradeables")]
    public static class Patch_RICS_Ownership_TradeBlock
    {
        [HarmonyPostfix]
        public static void Postfix(TradeDeal __instance)
        {
            try
            {
                if (!RICS_OwnershipUtility.IsRicsOwnershipActive())
                    return;

                var settings = CAPChatInteractiveMod.Instance?.Settings?.GlobalSettings;
                if (settings == null || !settings.RicsOwnershipBlockTrading)
                    return;

                List<Tradeable> all = __instance.AllTradeables;
                if (all == null)
                    return;

                for (int i = all.Count - 1; i >= 0; i--)
                {
                    Tradeable t = all[i];
                    if (t?.thingsColony == null)
                        continue;

                    bool owned = false;
                    foreach (Thing thing in t.thingsColony)
                    {
                        if (thing == null || thing.Destroyed)
                            continue;
                        if (RICS_OwnershipUtility.GetOwner(thing) != null)
                        {
                            owned = true;
                            break;
                        }
                    }
                    if (owned)
                        all.RemoveAt(i);
                }
            }
            catch (System.Exception ex)
            {
                Logger.Warning($"[RICS Ownership] Trade block patch failed (non-fatal): {ex.Message}");
            }
        }
    }
}
