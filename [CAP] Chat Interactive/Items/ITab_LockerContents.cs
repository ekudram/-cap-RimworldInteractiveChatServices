// Filename: RimazonLocker.cs
//
// Copyright (c) Captolamia
// This file is part of CAP Chat Interactive (RICS).
// Licensed under the GNU Affero General Public License v3.0 or later.
// See LICENSE.txt in the project root for full license text.
//
// This file defines a Rimazon locker building for RimWorld that can be renamed by the player.

using RimWorld;
using System;
using System.Linq;
using UnityEngine;
using Verse;
namespace CAP_ChatInteractive
{
    /// <summary>
    /// SHows us what is in the locker
    /// </summary>
    public class ITab_LockerContents : ITab
    {
        private Vector2 scrollPosition = Vector2.zero;

        public ITab_LockerContents()
        {
            size = new Vector2(520f, 480f);
            labelKey = "TabLockerContents";
            tutorTag = "LockerContents";
        }

        public override bool IsVisible => SelThing is Building_RimazonLocker;

        protected override void FillTab()
        {
            try
            {
                var container = SelThing as Building_RimazonLocker;
                if (container == null || !container.Spawned || container.settings == null)
                {
                    Log.Warning("[DEBUG] ITab_ContainerStorage: No valid container");
                    return;
                }


                var locker = SelThing as Building_RimazonLocker;
                if (locker == null || locker.InnerContainer == null)
                {
                    Text.Font = GameFont.Small;
                    Widgets.Label(new Rect(0f, 0f, size.x, size.y).ContractedBy(10f),
                        "Locker not available.\nTry re-selecting the building.");
                    return;
                }

                Rect rect = new Rect(0f, 0f, size.x, size.y).ContractedBy(10f);
                var listing = new Listing_Standard();
                listing.Begin(rect);

                // Header
                if (!locker.customName.NullOrEmpty())
                {
                    listing.Label($"Locker: {locker.customName}");
                    listing.GapLine();
                }

                float totalMass = locker.InnerContainer.Sum(t => t.GetStatValue(StatDefOf.Mass) * t.stackCount);
                listing.Label($"Total mass: {totalMass:F2} kg");
                listing.Label($"Stack slots: {locker.InnerContainer.Count}/{locker.MaxStacks}");
                listing.Label($"Total items: {locker.InnerContainer.TotalStackCount}");
                listing.Gap(12f);

                // Search bar
                Rect searchRect = listing.GetRect(30f);
                string searchString = ""; // You can make this a field if you want persistence
                searchString = Widgets.TextField(searchRect, searchString);

                if (locker.InnerContainer.Count == 0)
                {
                    listing.Label("(Empty)");
                }
                else
                {
                    // Filter items (case-insensitive)
                    var filtered = locker.InnerContainer
                        .Where(t => t != null && !t.Destroyed && t.LabelCap.ToLowerInvariant().Contains(searchString.ToLowerInvariant()))
                        .ToList();

                    // Calculate scroll height dynamically (prevents overflow/clipping buttons)
                    float headerUsed = listing.CurHeight;
                    float availableForScroll = rect.height - headerUsed - 50f; // leave room for buttons + gaps
                    if (availableForScroll < 100f) availableForScroll = 100f; // minimum

                    Rect scrollOuter = listing.GetRect(availableForScroll);
                    float estimatedContentHeight = filtered.Count * 35f + 20f; // rough per-row height + padding
                    Rect viewRect = new Rect(0f, 0f, scrollOuter.width - 16f, Mathf.Max(estimatedContentHeight, availableForScroll));

                    Widgets.BeginScrollView(scrollOuter, ref scrollPosition, viewRect);

                    try
                    {
                        var innerListing = new Listing_Standard();
                        innerListing.Begin(viewRect);

                        foreach (Thing thing in filtered)
                        {
                            Rect row = innerListing.GetRect(28f);

                            // Alternate row highlight
                            if (filtered.IndexOf(thing) % 2 == 0)
                            {
                                Widgets.DrawLightHighlight(row);
                            }

                            Widgets.ThingIcon(new Rect(row.x, row.y, 28f, 28f), thing);

                            Text.Anchor = TextAnchor.MiddleLeft;
                            string label = thing.LabelCap ?? thing.def?.LabelCap ?? "[Unknown Item]";
                            Widgets.Label(new Rect(row.x + 32f, row.y, row.width - 60f, 28f), label);
                            Text.Anchor = TextAnchor.UpperLeft;  // CRITICAL RESET - prevents alignment error

                            // Info button (safe)
                            if (Widgets.ButtonImage(new Rect(row.xMax - 24f, row.y + 2f, 24f, 24f), TexButton.Info))
                            {
                                if (thing?.def != null)
                                {
                                    Find.WindowStack.Add(new Dialog_InfoCard(thing.def, thing.Stuff));
                                }
                            }

                            TooltipHandler.TipRegion(row, thing.GetInspectString() ?? label);

                            innerListing.Gap(4f);
                        }

                        innerListing.End();
                    }
                    finally
                    {
                        Widgets.EndScrollView();  // Always close
                    }
                }

                listing.Gap(12f);

                // Buttons (now always visible)
                Rect btnRect = listing.GetRect(30f);
                if (Widgets.ButtonText(new Rect(btnRect.x, btnRect.y, btnRect.width / 2 - 5f, 30f), "Eject All"))
                {
                    locker.SafeEjectAllContents();
                }

                if (Widgets.ButtonText(new Rect(btnRect.x + btnRect.width / 2 + 5f, btnRect.y, btnRect.width / 2 - 5f, 30f), "Detailed View"))
                {
                    Find.WindowStack.Add(new Dialog_LockerContents(locker));
                }

                listing.End();

                // Global safety reset (extra protection)
                Text.Anchor = TextAnchor.UpperLeft;
            }
            catch (Exception ex)
            {
                Log.Error($"[DEBUG] ITab_ContainerStorage.FillTab error: {ex}");
            }
        }
    }
}

