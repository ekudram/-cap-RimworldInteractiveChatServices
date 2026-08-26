// Dialog_RICS_OwnedItemsBrowser.cs
// Copyright (c) Captolamia
// Part of CAP Chat Interactive (RICS) — AGPLv3
//
// Searchable browser of RICS-owned weapons/apparel. Opened from Play Settings HUD toggle.
using RimWorld;
using RimWorld.Planet;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace CAP_ChatInteractive.Ownership
{
    public class Dialog_RICS_OwnedItemsBrowser : Window
    {
        private enum LocationFilter { All, ThisMap, AnyColony, Caravans }
        private enum SortColumn { Item, Owner, Quality, Where }

        private Vector2 scrollPos;
        private string searchText = "";
        private Pawn selectedPawn;
        private bool showWeapons = true;
        private bool showApparel = true;
        private LocationFilter locationFilter = LocationFilter.All;
        private SortColumn sortColumn = SortColumn.Item;
        private bool sortAsc = true;

        private List<OwnedRow> cachedRows = new List<OwnedRow>();
        private string lastFilterKey = "";

        private struct OwnedRow
        {
            public Thing Thing;
            public Pawn Owner;
            public string Where;
            public string ItemLabel;
            public string OwnerLabel;
            public string QualityLabel;
            public string TypeLabel;
        }

        public override Vector2 InitialSize => new Vector2(980f, 620f);

        public Dialog_RICS_OwnedItemsBrowser()
        {
            forcePause = false;
            doCloseButton = false;
            doCloseX = true;
            draggable = true;
            absorbInputAroundWindow = false;
        }

        public override void PreOpen()
        {
            base.PreOpen();
            RebuildCache(force: true);
        }

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            GUI.color = ColorLibrary.HeaderAccent;
            Widgets.Label(new Rect(0f, 0f, inRect.width, 30f), "RICS.Ownership.Browser.Title".Translate());
            GUI.color = Color.white;
            Text.Font = GameFont.Small;

            if (!RICS_OwnershipUtility.IsRicsOwnershipActive())
            {
                Widgets.Label(new Rect(0f, 40f, inRect.width, 80f), "RICS.Ownership.Browser.DisabledHint".Translate());
                if (Widgets.ButtonText(new Rect(0f, inRect.height - 36f, 120f, 32f), "Close"))
                    Close();
                return;
            }

            float y = 36f;
            float filterH = 30f;
            float gap = 8f;

            // Search
            Rect searchRect = new Rect(0f, y, 220f, filterH);
            string newSearch = Widgets.TextField(searchRect, searchText);
            if (newSearch != searchText)
            {
                searchText = newSearch;
                RebuildCache(force: true);
            }

            // Pawn filter
            Rect pawnRect = new Rect(searchRect.xMax + gap, y, 180f, filterH);
            string pawnLabel = selectedPawn != null
                ? selectedPawn.LabelShortCap
                : "RICS.Ownership.Browser.AllPawns".Translate();
            if (Widgets.ButtonText(pawnRect, pawnLabel))
            {
                Find.WindowStack.Add(new Dialog_RICS_AssignItemOwner(
                    "RICS.Ownership.Browser.PickPawnTitle".Translate(),
                    "RICS.Ownership.Dialog.Choose".Translate(),
                    selectedPawn,
                    pawn =>
                    {
                        selectedPawn = pawn;
                        RebuildCache(force: true);
                    },
                    allowClear: false,
                    includeAllPawnsOption: true,
                    onAllPawns: () =>
                    {
                        selectedPawn = null;
                        RebuildCache(force: true);
                    },
                    pickButtonLabel: "RICS.Ownership.Browser.SelectPawn".Translate().ToString(),
                    currentPawnLine: selectedPawn != null
                        ? "RICS.Ownership.Browser.CurrentlyViewing".Translate(selectedPawn.LabelShortCap).ToString()
                        : null));
            }

            // Location filter
            Rect locRect = new Rect(pawnRect.xMax + gap, y, 160f, filterH);
            string[] locLabels =
            {
                "RICS.Ownership.Browser.Loc.All".Translate(),
                "RICS.Ownership.Browser.Loc.ThisMap".Translate(),
                "RICS.Ownership.Browser.Loc.AnyColony".Translate(),
                "RICS.Ownership.Browser.Loc.Caravans".Translate()
            };
            if (Widgets.ButtonText(locRect, locLabels[(int)locationFilter]))
            {
                locationFilter = (LocationFilter)(((int)locationFilter + 1) % 4);
                SoundDefOf.Tick_High.PlayOneShotOnCamera();
                RebuildCache(force: true);
            }

            // Weapons / Apparel toggles
            float tx = locRect.xMax + gap + 4f;
            Widgets.Label(new Rect(tx, y + 4f, 70f, filterH), "RICS.Ownership.Browser.Weapons".Translate());
            tx += 70f;
            if (Widgets.ButtonImage(new Rect(tx, y + 3f, 24f, 24f), showWeapons ? Widgets.CheckboxOnTex : Widgets.CheckboxOffTex))
            {
                showWeapons = !showWeapons;
                SoundDefOf.Tick_Tiny.PlayOneShotOnCamera();
                RebuildCache(force: true);
            }
            tx += 32f;
            Widgets.Label(new Rect(tx, y + 4f, 70f, filterH), "RICS.Ownership.Browser.Apparel".Translate());
            tx += 70f;
            if (Widgets.ButtonImage(new Rect(tx, y + 3f, 24f, 24f), showApparel ? Widgets.CheckboxOnTex : Widgets.CheckboxOffTex))
            {
                showApparel = !showApparel;
                SoundDefOf.Tick_Tiny.PlayOneShotOnCamera();
                RebuildCache(force: true);
            }

            // Refresh
            if (Widgets.ButtonText(new Rect(inRect.width - 100f, y, 100f, filterH), "RICS.Ownership.Browser.Refresh".Translate()))
                RebuildCache(force: true);

            y += filterH + 12f;

            EnsureCache();

            // Headers
            float iconCol = 28f;
            float colItem = 280f;
            float colOwner = 140f;
            float colQuality = 100f;
            float colWhere = 140f;
            float rowH = 32f;
            float x0 = 0f;

            DrawSortHeader(new Rect(x0 + iconCol, y, colItem, 28f), "RICS.Ownership.Browser.Col.Item".Translate(), SortColumn.Item);
            DrawSortHeader(new Rect(x0 + iconCol + colItem, y, colOwner, 28f), "RICS.Ownership.Browser.Col.Owner".Translate(), SortColumn.Owner);
            DrawSortHeader(new Rect(x0 + iconCol + colItem + colOwner, y, colQuality, 28f), "RICS.Ownership.Browser.Col.Quality".Translate(), SortColumn.Quality);
            DrawSortHeader(new Rect(x0 + iconCol + colItem + colOwner + colQuality, y, colWhere, 28f), "RICS.Ownership.Browser.Col.Where".Translate(), SortColumn.Where);
            y += 30f;

            Rect outRect = new Rect(0f, y, inRect.width, inRect.height - y - 44f);
            Rect viewRect = new Rect(0f, 0f, outRect.width - 16f, Math.Max(cachedRows.Count * rowH, outRect.height));
            Widgets.BeginScrollView(outRect, ref scrollPos, viewRect);

            for (int i = 0; i < cachedRows.Count; i++)
            {
                var row = cachedRows[i];
                float ry = i * rowH;
                Rect rowRect = new Rect(0f, ry, viewRect.width, rowH);
                if (i % 2 == 1)
                    Widgets.DrawHighlight(rowRect);
                Widgets.DrawHighlightIfMouseover(rowRect);

                if (Widgets.ButtonInvisible(rowRect))
                {
                    try
                    {
                        if (row.Thing != null && !row.Thing.Destroyed)
                        {
                            CameraJumper.TryJumpAndSelect((GlobalTargetInfo)row.Thing);
                        }
                        else if (row.Owner != null && !row.Owner.Destroyed)
                        {
                            CameraJumper.TryJumpAndSelect((GlobalTargetInfo)row.Owner);
                        }
                    }
                    catch { }
                }

                if (row.Thing != null && !row.Thing.Destroyed)
                    Widgets.ThingIcon(new Rect(2f, ry + 2f, 24f, 24f), row.Thing);

                Text.Anchor = TextAnchor.MiddleLeft;
                Widgets.Label(new Rect(iconCol, ry, colItem - 4f, rowH), row.ItemLabel);
                Widgets.Label(new Rect(iconCol + colItem, ry, colOwner, rowH), row.OwnerLabel);
                Widgets.Label(new Rect(iconCol + colItem + colOwner, ry, colQuality, rowH), row.QualityLabel);
                Widgets.Label(new Rect(iconCol + colItem + colOwner + colQuality, ry, colWhere, rowH), row.Where);
                Text.Anchor = TextAnchor.UpperLeft;
            }

            Widgets.EndScrollView();

            Widgets.Label(new Rect(0f, inRect.height - 36f, 400f, 28f),
                "RICS.Ownership.Browser.Count".Translate(cachedRows.Count));

            if (Widgets.ButtonText(new Rect(inRect.width - 120f, inRect.height - 36f, 120f, 32f), "Close"))
                Close();
        }

        private void DrawSortHeader(Rect rect, string label, SortColumn col)
        {
            string mark = sortColumn == col ? (sortAsc ? " ▲" : " ▼") : "";
            if (Widgets.ButtonText(rect, label + mark, drawBackground: false))
            {
                if (sortColumn == col)
                    sortAsc = !sortAsc;
                else
                {
                    sortColumn = col;
                    sortAsc = true;
                }
                SoundDefOf.Tick_High.PlayOneShotOnCamera();
                RebuildCache(force: true);
            }
        }

        private void EnsureCache()
        {
            string key = $"{searchText}|{selectedPawn?.thingIDNumber}|{showWeapons}|{showApparel}|{locationFilter}|{sortColumn}|{sortAsc}";
            if (key != lastFilterKey)
                RebuildCache(force: true);
        }

        private void RebuildCache(bool force)
        {
            lastFilterKey = $"{searchText}|{selectedPawn?.thingIDNumber}|{showWeapons}|{showApparel}|{locationFilter}|{sortColumn}|{sortAsc}";
            cachedRows = CollectRows();
        }

        private List<OwnedRow> CollectRows()
        {
            var rows = new List<OwnedRow>();
            if (!RICS_OwnershipUtility.IsRicsOwnershipActive())
                return rows;

            var seen = new HashSet<int>();

            void Consider(Thing t, string where)
            {
                if (t == null || t.Destroyed || !(t is ThingWithComps))
                    return;
                if (!seen.Add(t.thingIDNumber))
                    return;

                var comp = t.TryGetComp<Comp_RICS_OwnedByPawn>();
                var owner = comp?.Owner;
                if (owner == null || owner.Destroyed || owner.Dead)
                    return;

                if (selectedPawn != null && owner != selectedPawn)
                    return;

                bool isWeapon = t.def?.IsWeapon == true;
                bool isApparel = t.def?.IsApparel == true;
                if (isWeapon && !showWeapons)
                    return;
                if (isApparel && !showApparel)
                    return;
                if (!isWeapon && !isApparel)
                    return;

                string needle = searchText?.Trim() ?? "";
                if (!string.IsNullOrEmpty(needle))
                {
                    string hay = (t.LabelCap + " " + owner.LabelShortCap).ToLowerInvariant();
                    if (hay.IndexOf(needle.ToLowerInvariant(), StringComparison.Ordinal) < 0)
                        return;
                }

                string quality = "-";
                try
                {
                    var q = t.TryGetComp<CompQuality>();
                    if (q != null)
                        quality = q.Quality.GetLabel();
                }
                catch { }

                rows.Add(new OwnedRow
                {
                    Thing = t,
                    Owner = owner,
                    Where = where,
                    ItemLabel = t.LabelCap,
                    OwnerLabel = owner.LabelShortCap,
                    QualityLabel = quality,
                    TypeLabel = isWeapon ? "Weapon" : "Apparel"
                });
            }

            try
            {
                // Pawn gear (all maps + caravans depending on filter)
                IEnumerable<Pawn> pawns = Enumerable.Empty<Pawn>();
                switch (locationFilter)
                {
                    case LocationFilter.ThisMap:
                        if (Find.CurrentMap?.mapPawns != null)
                            pawns = Find.CurrentMap.mapPawns.FreeColonistsSpawned;
                        break;
                    case LocationFilter.Caravans:
                        pawns = Find.WorldObjects?.Caravans?
                            .Where(c => c != null && c.Faction == Faction.OfPlayer)
                            .SelectMany(c => c.PawnsListForReading ?? Enumerable.Empty<Pawn>())
                            ?? Enumerable.Empty<Pawn>();
                        break;
                    default:
                        pawns = PawnsFinder.AllMaps_FreeColonistsSpawned ?? Enumerable.Empty<Pawn>();
                        if (locationFilter == LocationFilter.All)
                        {
                            var caravanPawns = Find.WorldObjects?.Caravans?
                                .Where(c => c != null && c.Faction == Faction.OfPlayer)
                                .SelectMany(c => c.PawnsListForReading ?? Enumerable.Empty<Pawn>())
                                ?? Enumerable.Empty<Pawn>();
                            pawns = pawns.Concat(caravanPawns);
                        }
                        break;
                }

                foreach (var p in pawns)
                {
                    if (p == null || p.Destroyed)
                        continue;
                    bool onCaravan = p.GetCaravan() != null;
                    if (locationFilter == LocationFilter.AnyColony && onCaravan)
                        continue;
                    if (locationFilter == LocationFilter.Caravans && !onCaravan)
                        continue;

                    if (p.apparel?.WornApparel != null)
                    {
                        foreach (var a in p.apparel.WornApparel)
                            Consider(a, onCaravan ? "Caravan / Worn" : "Worn");
                    }
                    if (p.equipment?.AllEquipmentListForReading != null)
                    {
                        foreach (var e in p.equipment.AllEquipmentListForReading)
                            Consider(e, onCaravan ? "Caravan / Equipped" : "Equipped");
                    }
                    if (p.inventory?.innerContainer != null)
                    {
                        foreach (var inv in p.inventory.innerContainer)
                            Consider(inv, onCaravan ? "Caravan / Inventory" : "Inventory");
                    }
                }

                // Loose map things (owned gear on ground / in storage) — only for map filters
                if (locationFilter != LocationFilter.Caravans)
                {
                    IEnumerable<Map> maps;
                    if (locationFilter == LocationFilter.ThisMap)
                        maps = Find.CurrentMap != null ? new[] { Find.CurrentMap } : Enumerable.Empty<Map>();
                    else
                        maps = Find.Maps?.Where(m => m != null && (m.IsPlayerHome || m.ParentFaction == Faction.OfPlayer))
                               ?? Enumerable.Empty<Map>();

                    foreach (var map in maps)
                    {
                        if (map?.listerThings == null)
                            continue;
                        // Prefer weapon/apparel request groups when available
                        IEnumerable<Thing> candidates = map.listerThings.AllThings;
                        foreach (var t in candidates)
                        {
                            if (t?.def == null)
                                continue;
                            if (!t.def.IsWeapon && !t.def.IsApparel)
                                continue;
                            // Skip things already held by a pawn (covered above)
                            if (t.ParentHolder is Pawn_ApparelTracker || t.ParentHolder is Pawn_EquipmentTracker
                                || t.ParentHolder is Pawn_InventoryTracker)
                                continue;
                            Consider(t, "Map");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Warning($"[RICS Ownership] Owned items browser gather failed: {ex.Message}");
            }

            // Sort
            IOrderedEnumerable<OwnedRow> ordered;
            switch (sortColumn)
            {
                case SortColumn.Owner:
                    ordered = sortAsc
                        ? rows.OrderBy(r => r.OwnerLabel)
                        : rows.OrderByDescending(r => r.OwnerLabel);
                    break;
                case SortColumn.Quality:
                    ordered = sortAsc
                        ? rows.OrderBy(r => r.QualityLabel)
                        : rows.OrderByDescending(r => r.QualityLabel);
                    break;
                case SortColumn.Where:
                    ordered = sortAsc
                        ? rows.OrderBy(r => r.Where)
                        : rows.OrderByDescending(r => r.Where);
                    break;
                default:
                    ordered = sortAsc
                        ? rows.OrderBy(r => r.ItemLabel)
                        : rows.OrderByDescending(r => r.ItemLabel);
                    break;
            }

            return ordered.ToList();
        }
    }
}
