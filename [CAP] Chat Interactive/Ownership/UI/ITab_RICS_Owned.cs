// ITab_RICS_Owned.cs
// Copyright (c) Captolamia — AGPLv3
using RimWorld;
using RimWorld.Planet;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace CAP_ChatInteractive.Ownership
{
    public class ITab_RICS_Owned : ITab
    {
        private Vector2 scrollPos;
        private List<RICS_OwnedItem> cached = new List<RICS_OwnedItem>();
        private int lastPawnId = -1;

        public ITab_RICS_Owned()
        {
            size = new Vector2(520f, 440f);
            labelKey = "RICS.Ownership.ITab.Label";
        }

        public override bool IsVisible
        {
            get
            {
                if (!RICS_OwnershipUtility.IsRicsOwnershipActive())
                    return false;
                var p = SelPawn;
                return p != null && !p.Destroyed && p.Faction == Faction.OfPlayer && p.IsColonist;
            }
        }

        protected override void FillTab()
        {
            var pawn = SelPawn;
            if (pawn == null)
                return;

            if (pawn.thingIDNumber != lastPawnId)
            {
                lastPawnId = pawn.thingIDNumber;
                cached = RICS_OwnedItemsCollector.CollectForPawn(pawn);
            }

            Rect rect = new Rect(10f, 10f, size.x - 20f, size.y - 20f);
            Text.Font = GameFont.Small;
            Widgets.Label(new Rect(rect.x, rect.y, rect.width - 90f, 24f),
                "RICS.Ownership.ITab.Header".Translate(pawn.LabelShortCap, cached.Count));
            if (Widgets.ButtonText(new Rect(rect.xMax - 80f, rect.y, 80f, 24f),
                "RICS.Ownership.Browser.Refresh".Translate()))
            {
                cached = RICS_OwnedItemsCollector.CollectForPawn(pawn);
            }

            float y = rect.y + 30f;
            if (cached.Count == 0)
            {
                Widgets.Label(new Rect(rect.x, y, rect.width, 40f), "RICS.Ownership.ITab.Empty".Translate());
                return;
            }

            Rect outRect = new Rect(rect.x, y, rect.width, rect.height - 36f);
            float rowH = 28f;
            Rect viewRect = new Rect(0f, 0f, outRect.width - 16f, cached.Count * rowH);
            Widgets.BeginScrollView(outRect, ref scrollPos, viewRect);
            for (int i = 0; i < cached.Count; i++)
            {
                var item = cached[i];
                if (item.Thing == null || item.Thing.Destroyed)
                    continue;
                Rect row = new Rect(0f, i * rowH, viewRect.width, rowH);
                if (i % 2 == 1)
                    Widgets.DrawHighlight(row);
                Widgets.DrawHighlightIfMouseover(row);
                if (Widgets.ButtonInvisible(new Rect(row.x, row.y, row.width - 90f, row.height)))
                {
                    try { CameraJumper.TryJumpAndSelect((GlobalTargetInfo)item.Thing); }
                    catch { }
                }

                string extra = item.IsApparel
                    ? RICS_OwnedItemsCollector.ArmorSummary(item)
                    : item.QualityLabel;
                Widgets.Label(new Rect(4f, row.y + 4f, row.width - 100f, 22f),
                    $"{item.Thing.LabelCap}  [{item.Where}]  {extra}");
                if (Widgets.ButtonText(new Rect(row.xMax - 88f, row.y + 2f, 84f, 24f),
                    "RICS.Ownership.Dialog.Clear".Translate()))
                {
                    RICS_OwnershipUtility.ClearOwner(item.Thing, "ITab");
                    cached = RICS_OwnedItemsCollector.CollectForPawn(pawn);
                    break;
                }
            }
            Widgets.EndScrollView();
        }
    }
}
