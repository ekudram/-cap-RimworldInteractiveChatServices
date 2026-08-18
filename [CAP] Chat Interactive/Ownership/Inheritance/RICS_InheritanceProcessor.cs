// RICS_InheritanceProcessor.cs
// Copyright (c) Captolamia
// Part of CAP Chat Interactive (RICS) — AGPLv3
//
// Safe inheritance — called AFTER death settles (Postfix), never Kill Prefix.
// Null-safe for destroyed gear / maps (avoids Possessions Plus error spam & unkillable bugs).
using RimWorld;
using System;
using System.Collections.Generic;
using Verse;

namespace CAP_ChatInteractive.Ownership.Inheritance
{
    public static class RICS_InheritanceProcessor
    {
        public static void RunForDeceased(Pawn deceased)
        {
            if (!RICS_OwnershipUtility.IsRicsOwnershipActive())
                return;

            var settings = CAPChatInteractiveMod.Instance?.Settings?.GlobalSettings;
            if (settings == null || !settings.RicsOwnershipInheritance)
                return;

            if (deceased == null || deceased.Faction != Faction.OfPlayer)
                return;
            if (deceased.RaceProps?.Humanlike != true)
                return;

            try
            {
                Pawn heir = RICS_InheritanceHeirSelector.SelectHeir(deceased);
                var items = CollectOwnedThings(deceased);
                if (items.Count == 0)
                    return;

                if (heir == null)
                {
                    foreach (var t in items)
                        RICS_OwnershipUtility.ClearOwner(t, "owner died — no heir");
                    return;
                }

                int transferred = 0;
                foreach (var t in items)
                {
                    if (t == null || t.Destroyed)
                        continue;
                    if (RICS_OwnershipUtility.SetOwner(t, heir, $"inherited from {deceased.LabelShortCap}"))
                        transferred++;
                }

                if (transferred > 0)
                {
                    Messages.Message(
                        "RICS.Ownership.Inheritance.Message".Translate(
                            heir.LabelShortCap,
                            deceased.LabelShortCap,
                            transferred),
                        heir,
                        MessageTypeDefOf.PositiveEvent,
                        historical: true);
                }
            }
            catch (Exception ex)
            {
                // Never rethrow — inheritance must not block death cleanup
                Logger.Warning($"[RICS Ownership] Inheritance failed (non-fatal): {ex.Message}");
            }
        }

        private static List<Thing> CollectOwnedThings(Pawn deceased)
        {
            var list = new List<Thing>();
            try
            {
                // Equipped apparel / weapons
                if (deceased.apparel?.WornApparel != null)
                {
                    foreach (var a in deceased.apparel.WornApparel)
                        TryAdd(list, a, deceased);
                }
                if (deceased.equipment?.AllEquipmentListForReading != null)
                {
                    foreach (var e in deceased.equipment.AllEquipmentListForReading)
                        TryAdd(list, e, deceased);
                }
                if (deceased.inventory?.innerContainer != null)
                {
                    foreach (var t in deceased.inventory.innerContainer)
                        TryAdd(list, t, deceased);
                }

                // Also scan map for things owned by deceased (dropped gear) — bounded / null-safe
                Map map = deceased.MapHeld ?? deceased.Corpse?.Map;
                if (map?.listerThings != null)
                {
                    foreach (Thing t in map.listerThings.AllThings)
                    {
                        if (t == null || t.Destroyed)
                            continue;
                        if (!(t.def?.IsWeapon == true || t.def?.IsApparel == true))
                            continue;
                        TryAdd(list, t, deceased);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Warning($"[RICS Ownership] CollectOwnedThings: {ex.Message}");
            }
            return list;
        }

        private static void TryAdd(List<Thing> list, Thing t, Pawn deceased)
        {
            if (t == null || t.Destroyed)
                return;
            if (list.Contains(t))
                return;
            var owner = RICS_OwnershipUtility.GetOwner(t);
            if (owner == deceased)
                list.Add(t);
        }

    }
}
