// Dialog_RICS_AssignItemOwner.cs
// Copyright (c) Captolamia
// Part of CAP Chat Interactive (RICS) — AGPLv3
//
// Distinct from Possessions Plus assign UI (title, layout, RICS branding).
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace CAP_ChatInteractive.Ownership
{
    public class Dialog_RICS_AssignItemOwner : Window
    {
        private readonly Comp_RICS_OwnedByPawn comp;
        private Vector2 scrollPos;

        public override Vector2 InitialSize => new Vector2(480f, 560f);

        public Dialog_RICS_AssignItemOwner(Comp_RICS_OwnedByPawn comp)
        {
            this.comp = comp;
            forcePause = true;
            absorbInputAroundWindow = true;
            closeOnClickedOutside = true;
            doCloseX = true;
            doCloseButton = true;
        }

        public override void DoWindowContents(Rect inRect)
        {
            if (comp?.parent == null || comp.parent.Destroyed)
            {
                Close();
                return;
            }

            Text.Font = GameFont.Medium;
            GUI.color = ColorLibrary.HeaderAccent;
            Widgets.Label(new Rect(0f, 0f, inRect.width, 32f), "RICS.Ownership.Dialog.Title".Translate());
            GUI.color = Color.white;
            Text.Font = GameFont.Small;

            Widgets.Label(new Rect(0f, 36f, inRect.width, 24f),
                "RICS.Ownership.Dialog.Item".Translate(comp.parent.LabelCap));

            float y = 68f;
            var owner = comp.Owner;
            if (owner != null)
            {
                Widgets.Label(new Rect(0f, y, inRect.width - 160f, 28f),
                    "RICS.Ownership.Dialog.CurrentOwner".Translate(owner.LabelShortCap));
                if (Widgets.ButtonText(new Rect(inRect.width - 150f, y, 140f, 28f),
                    "RICS.Ownership.Dialog.Clear".Translate()))
                {
                    comp.ClearOwner("RICS UI");
                    Messages.Message("RICS.Ownership.Cleared".Translate(comp.parent.LabelNoCount),
                        MessageTypeDefOf.TaskCompletion, historical: false);
                    Close();
                    return;
                }
                y += 36f;
                Widgets.DrawLineHorizontal(0f, y, inRect.width);
                y += 10f;
            }

            Widgets.Label(new Rect(0f, y, inRect.width, 24f), "RICS.Ownership.Dialog.Choose".Translate());
            y += 28f;

            List<Pawn> colonists;
            try
            {
                colonists = PawnsFinder.AllMaps_FreeColonistsSpawned?.ToList() ?? new List<Pawn>();
            }
            catch
            {
                colonists = new List<Pawn>();
            }

            Rect outRect = new Rect(0f, y, inRect.width, inRect.height - y - 50f);
            float viewH = Mathf.Max(colonists.Count * 40f, outRect.height);
            Rect viewRect = new Rect(0f, 0f, outRect.width - 16f, viewH);
            Widgets.BeginScrollView(outRect, ref scrollPos, viewRect);

            float rowY = 0f;
            foreach (var pawn in colonists)
            {
                if (pawn == null || pawn.Destroyed || pawn == owner)
                    continue;

                Rect row = new Rect(0f, rowY, viewRect.width, 36f);
                Widgets.DrawHighlightIfMouseover(row);
                Widgets.Label(new Rect(8f, rowY + 8f, 200f, 24f), pawn.LabelShortCap);
                if (Widgets.ButtonText(new Rect(220f, rowY + 4f, 160f, 28f),
                    "RICS.Ownership.Dialog.MakeOwner".Translate()))
                {
                    comp.SetOwner(pawn, "RICS UI");
                    Messages.Message(
                        "RICS.Ownership.Assigned".Translate(comp.parent.LabelNoCount, pawn.LabelShortCap),
                        MessageTypeDefOf.TaskCompletion,
                        historical: false);
                    Close();
                    break;
                }
                rowY += 40f;
            }

            Widgets.EndScrollView();
        }
    }
}
