// File: Dialog_LockerContents.cs
//
// Copyright (c) Captolamia
// This file is part of CAP Chat Interactive (RICS).
// Licensed under the GNU Affero General Public License v3.0 or later.
// See LICENSE.txt in the project root for full license text.
//
// This file defines a dialog window that shows the contents of a Rimazon locker in a scrollable list.

using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace CAP_ChatInteractive
{
    public class Dialog_LockerContents : Window
    {
        private const float RowHeight = 35f;
        private const float ScrollBottomPadding = 35f; // one extra row so the last stack is not clipped
        private const float ScrollStartY = 155f;
        private const float ButtonAreaHeight = 40f;

        private Building_RimazonLocker locker;
        private Vector2 scrollPosition;
        private List<Thing> cachedContents;

        public Dialog_LockerContents(Building_RimazonLocker locker)
        {
            this.locker = locker;
            forcePause = true;
            doCloseX = true;
            absorbInputAroundWindow = true;
            closeOnClickedOutside = true;
            CacheContents();
        }

        private void CacheContents()
        {
            cachedContents = new List<Thing>();
            if (locker != null && locker.InnerContainer != null)
            {
                cachedContents.AddRange(locker.InnerContainer);
            }
        }

        public override Vector2 InitialSize => new Vector2(720f, 600f);

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            string title = locker.customName.NullOrEmpty()
                ? "RICS_LockerContents".Translate()
                : "RICS_ContentsOf".Translate(locker.customName);
            Widgets.Label(new Rect(0f, 0f, inRect.width, 35f), title);

            Text.Font = GameFont.Small;
            Widgets.Label(new Rect(0f, 40f, inRect.width, 25f),
                "RICS.Storage.StackSlots".Translate(locker.InnerContainer.Count, locker.MaxStacks));
            Widgets.Label(new Rect(0f, 60f, inRect.width, 25f),
                "RICS.Storage.TotalItems".Translate(locker.InnerContainer.TotalStackCount));

            // Column headers stay outside the scroll view so they do not overlap rows.
            Rect headerRect = new Rect(0f, 120f, inRect.width, 25f);
            if (cachedContents.Count > 0)
            {
                GUI.color = Color.gray;
                Widgets.DrawLineHorizontal(headerRect.x, headerRect.y + 24f, headerRect.width);
                GUI.color = Color.white;

                Text.Anchor = TextAnchor.MiddleLeft;
                Widgets.Label(new Rect(headerRect.x + 58f, headerRect.y, 180f, 25f), "RICS.Storage.Item".Translate());
                Widgets.Label(new Rect(headerRect.x + 250f, headerRect.y, 70f, 25f), "RICS.Storage.Quantity".Translate());
                Widgets.Label(new Rect(headerRect.x + 330f, headerRect.y, 90f, 25f), "RICS.Storage.EachValue".Translate());
                Widgets.Label(new Rect(headerRect.x + 430f, headerRect.y, 120f, 25f), "RICS.Storage.TotalValue".Translate());
                Text.Anchor = TextAnchor.UpperLeft;
            }

            Rect viewRect = new Rect(0f, ScrollStartY, inRect.width, inRect.height - ScrollStartY - ButtonAreaHeight);
            // Inner height used to start 25px down (old in-scroll header) while only counting N*35 —
            // last row was drawn past the scroll content rect and clipped. Rows now start at y=0
            // with one extra line of padding at the bottom.
            float contentHeight = cachedContents.Count * RowHeight + ScrollBottomPadding;
            Rect listRect = new Rect(0f, 0f, viewRect.width - 16f, contentHeight);

            Widgets.BeginScrollView(viewRect, ref scrollPosition, listRect);
            float y = 0f;

            for (int i = 0; i < cachedContents.Count; i++)
            {
                Thing thing = cachedContents[i];
                Rect rowRect = new Rect(0f, y, listRect.width, 32f);

                if (i % 2 == 0)
                {
                    Widgets.DrawLightHighlight(rowRect);
                }

                Rect ejectRect = new Rect(2f, y + 4f, 24f, 24f);
                Texture2D ejectIcon = ContentFinder<Texture2D>.Get("UI/Commands/RICS_Eject", true);
                if (Widgets.ButtonImage(ejectRect, ejectIcon))
                {
                    if (thing != null && locker.InnerContainer != null && locker.InnerContainer.Contains(thing))
                    {
                        SoundDefOf.Click.PlayOneShotOnCamera();
                        locker.EjectSingleItem(thing);
                        CacheContents();
                    }
                }
                if (Mouse.IsOver(ejectRect))
                {
                    TooltipHandler.TipRegion(ejectRect, "RICS.Locker.EjectThisItem".Translate());
                }

                Widgets.ThingIcon(new Rect(28f, y + 2f, 28f, 28f), thing);

                Text.Anchor = TextAnchor.MiddleLeft;
                string itemName = thing.LabelCapNoCount ?? thing.def?.label ?? "RICS.Unknown".Translate();
                Widgets.Label(new Rect(58f, y, 190f, 32f), itemName);

                Widgets.Label(new Rect(250f, y, 80f, 32f), thing.stackCount.ToString());
                Widgets.Label(new Rect(330f, y, 100f, 32f), thing.MarketValue.ToStringMoney());

                float totalValue = thing.MarketValue * thing.stackCount;
                Widgets.Label(new Rect(430f, y, 130f, 32f), totalValue.ToStringMoney());

                if (Widgets.ButtonImage(new Rect(listRect.width - 24f, y + 4f, 24f, 24f), TexButton.Info))
                {
                    if (thing?.def != null)
                    {
                        Find.WindowStack.Add(new Dialog_InfoCard(thing.def, thing.Stuff));
                    }
                    else
                    {
                        Messages.Message("RICS.CannotShowInfo".Translate(), MessageTypeDefOf.RejectInput);
                    }
                }

                string tooltip = thing.GetInspectString();
                if (!string.IsNullOrEmpty(tooltip))
                {
                    TooltipHandler.TipRegion(rowRect, tooltip);
                }

                y += RowHeight;
            }

            Widgets.EndScrollView();
            Text.Anchor = TextAnchor.UpperLeft;

            Rect buttonArea = new Rect(10f, inRect.height - 35f, inRect.width - 20f, 30f);
            Rect ejectAllRect = new Rect(buttonArea.x, buttonArea.y, 150f, 30f);
            Rect closeRect = new Rect(buttonArea.xMax - 110f, buttonArea.y, 110f, 30f);

            if (cachedContents.Count > 0)
            {
                if (Widgets.ButtonText(ejectAllRect, "RICS.EjectAll".Translate()))
                {
                    SoundDefOf.Click.PlayOneShotOnCamera();
                    locker.SafeEjectAllContents();
                    CacheContents();
                }
            }

            if (Widgets.ButtonText(closeRect, "Close".Translate()))
            {
                Close();
            }
        }
    }
}
