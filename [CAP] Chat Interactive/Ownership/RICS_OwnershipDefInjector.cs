// RICS_OwnershipDefInjector.cs
// Copyright (c) Captolamia
// Part of CAP Chat Interactive (RICS) — AGPLv3
//
// Injects RICS ownership comps onto weapons/apparel only when RICS ownership is enabled
// and Possessions Plus is NOT active (avoids double-comp / double-Harmony bugs).
using System.Collections.Generic;
using Verse;

namespace CAP_ChatInteractive.Ownership
{
    [StaticConstructorOnStartup]
    public static class RICS_OwnershipDefInjector
    {
        static RICS_OwnershipDefInjector()
        {
            try
            {
                // Settings may not be fully ready at StaticConstructorOnStartup on first boot;
                // inject conservatively — runtime checks still gate behavior.
                if (RICS_OwnershipModDetector.IsPossessionsPlusActive())
                {
                    Logger.Message("[RICS Ownership] Possessions Plus detected — skipping RICS comp injection.");
                    return;
                }

                // Inject comps always onto weapon/apparel defs so enabling mid-menu works without restart.
                // Behavior stays gated by UseRicsPawnOwnership. New-game warning is in settings UI.
                int added = 0;
                foreach (ThingDef def in DefDatabase<ThingDef>.AllDefsListForReading)
                {
                    if (def == null)
                        continue;
                    if (!def.IsWeapon && !def.IsApparel)
                        continue;

                    def.comps ??= new List<CompProperties>();

                    bool hasOwned = false;
                    bool hasNotOwnable = false;
                    foreach (var c in def.comps)
                    {
                        if (c is CompProperties_RICS_OwnedByPawn) hasOwned = true;
                        if (c is CompProperties_RICS_NotOwnable) hasNotOwnable = true;
                    }

                    if (!hasOwned)
                    {
                        def.comps.Add(new CompProperties_RICS_OwnedByPawn());
                        added++;
                    }
                    if (!hasNotOwnable)
                        def.comps.Add(new CompProperties_RICS_NotOwnable());
                }

                Logger.Message($"[RICS Ownership] Injected ownership comps onto {added} weapon/apparel defs (gated by settings at runtime).");
            }
            catch (System.Exception ex)
            {
                Logger.Error($"[RICS Ownership] Def injection failed: {ex.Message}");
            }
        }
    }
}
