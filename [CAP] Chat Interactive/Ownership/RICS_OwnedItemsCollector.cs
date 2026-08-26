// RICS_OwnedItemsCollector.cs
// Copyright (c) Captolamia — AGPLv3
// Gather RICS-owned weapons/apparel for a pawn (chat, ITab). Null-safe, no Personal Storage compile dep.
using RimWorld;
using RimWorld.Planet;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace CAP_ChatInteractive.Ownership
{
    public class RICS_OwnedItem
    {
        public Thing Thing;
        public Pawn Owner;
        public string Where;
        public bool IsWeapon;
        public bool IsApparel;
        public string QualityLabel;
        public float ArmorSharp;
        public float ArmorBlunt;
        public float ArmorHeat;
        public float MarketValue;
        public QualityCategory? Quality;
    }

    public static class RICS_OwnedItemsCollector
    {
        public static List<RICS_OwnedItem> CollectForPawn(Pawn owner)
        {
            var list = new List<RICS_OwnedItem>();
            if (owner == null || owner.Destroyed)
                return list;
            if (!RICS_OwnershipUtility.IsRicsOwnershipActive())
                return list;

            var seen = new HashSet<int>();

            void Consider(Thing t, string whereHint)
            {
                if (t == null || t.Destroyed)
                    return;
                if (!seen.Add(t.thingIDNumber))
                    return;

                var comp = t.TryGetComp<Comp_RICS_OwnedByPawn>();
                if (comp?.Owner != owner)
                    return;

                bool weapon = t.def?.IsWeapon == true;
                bool apparel = t.def?.IsApparel == true;
                if (!weapon && !apparel)
                    return;

                string quality = "-";
                QualityCategory? qcat = null;
                try
                {
                    var q = t.TryGetComp<CompQuality>();
                    if (q != null)
                    {
                        qcat = q.Quality;
                        quality = q.Quality.GetLabel();
                    }
                }
                catch { }

                float sharp = 0f, blunt = 0f, heat = 0f;
                if (apparel)
                {
                    try { sharp = t.GetStatValue(StatDefOf.ArmorRating_Sharp); } catch { }
                    try { blunt = t.GetStatValue(StatDefOf.ArmorRating_Blunt); } catch { }
                    try { heat = t.GetStatValue(StatDefOf.ArmorRating_Heat); } catch { }
                }

                list.Add(new RICS_OwnedItem
                {
                    Thing = t,
                    Owner = owner,
                    Where = whereHint ?? WhereOf(t, owner),
                    IsWeapon = weapon,
                    IsApparel = apparel,
                    QualityLabel = quality,
                    Quality = qcat,
                    ArmorSharp = sharp,
                    ArmorBlunt = blunt,
                    ArmorHeat = heat,
                    MarketValue = t.MarketValue
                });
            }

            try
            {
                if (owner.equipment?.Primary != null)
                    Consider(owner.equipment.Primary, "Equipped");
                if (owner.equipment?.AllEquipmentListForReading != null)
                {
                    foreach (var eq in owner.equipment.AllEquipmentListForReading)
                        Consider(eq, "Equipped");
                }
            }
            catch { }

            try
            {
                if (owner.apparel?.WornApparel != null)
                {
                    foreach (var ap in owner.apparel.WornApparel)
                        Consider(ap, "Worn");
                }
            }
            catch { }

            try
            {
                if (owner.inventory?.innerContainer != null)
                {
                    foreach (var t in owner.inventory.innerContainer)
                        Consider(t, "Inventory");
                }
            }
            catch { }

            try
            {
                foreach (var map in Find.Maps ?? Enumerable.Empty<Map>())
                {
                    if (map == null || map.Disposed)
                        continue;
                    var things = map.listerThings?.AllThings;
                    if (things == null)
                        continue;
                    foreach (var t in things)
                    {
                        if (t == null || t.Destroyed)
                            continue;
                        if (t.TryGetComp<Comp_RICS_OwnedByPawn>()?.Owner != owner)
                            continue;
                        Consider(t, WhereOf(t, owner));
                    }
                }
            }
            catch { }

            try
            {
                var caravans = Find.WorldObjects?.Caravans;
                if (caravans != null)
                {
                    foreach (var c in caravans)
                    {
                        if (c == null || c.Faction != Faction.OfPlayer)
                            continue;
                        foreach (var p in c.PawnsListForReading ?? Enumerable.Empty<Pawn>())
                        {
                            if (p == null)
                                continue;
                            if (p.inventory?.innerContainer == null)
                                continue;
                            foreach (var t in p.inventory.innerContainer)
                                Consider(t, "Caravan");
                        }
                    }
                }
            }
            catch { }

            return list;
        }

        public static List<RICS_OwnedItem> WeaponsSorted(List<RICS_OwnedItem> all)
        {
            return all.Where(i => i.IsWeapon)
                .OrderByDescending(i => i.Quality ?? QualityCategory.Awful)
                .ThenByDescending(i => i.MarketValue)
                .ThenBy(i => i.Thing?.LabelCap.ToString() ?? "")
                .ToList();
        }

        public static List<RICS_OwnedItem> ApparelSorted(List<RICS_OwnedItem> all)
        {
            return all.Where(i => i.IsApparel)
                .OrderByDescending(i => i.ArmorSharp)
                .ThenByDescending(i => i.ArmorBlunt)
                .ThenByDescending(i => i.ArmorHeat)
                .ThenBy(i => i.Thing?.LabelCap.ToString() ?? "")
                .ToList();
        }

        public static string ArmorSummary(RICS_OwnedItem item)
        {
            if (item == null || !item.IsApparel)
                return "";
            return $"{item.ArmorSharp:0%} / {item.ArmorBlunt:0%} / {item.ArmorHeat:0%}";
        }

        private static string WhereOf(Thing t, Pawn owner)
        {
            try
            {
                if (owner != null)
                {
                    if (owner.equipment?.AllEquipmentListForReading != null
                        && owner.equipment.AllEquipmentListForReading.Contains(t))
                        return "Equipped";
                    if (owner.apparel?.WornApparel != null && owner.apparel.WornApparel.Contains(t as Apparel))
                        return "Worn";
                    if (owner.inventory?.innerContainer != null && owner.inventory.innerContainer.Contains(t))
                        return "Inventory";
                }

                var holder = t.ParentHolder as Thing;
                string def = holder?.def?.defName ?? t.ParentHolder?.GetType().Name;
                if (!string.IsNullOrEmpty(def) &&
                    (def.IndexOf("Personal", StringComparison.OrdinalIgnoreCase) >= 0
                     || def.IndexOf("Chest", StringComparison.OrdinalIgnoreCase) >= 0
                     || def.IndexOf("Locker", StringComparison.OrdinalIgnoreCase) >= 0))
                    return "Chest";

                if (t.GetCaravan() != null)
                    return "Caravan";
                if (t.Spawned)
                    return "Map";
            }
            catch { }
            return "Unknown";
        }
    }
}
