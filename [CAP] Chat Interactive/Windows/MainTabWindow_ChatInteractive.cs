// File: MainTabWindow_ChatInteractive.cs
//
// Copyright (c) Captolamia
// This file is part of CAP Chat Interactive (RICS).
// Licensed under the GNU Affero General Public License v3.0 or later.
// See LICENSE.txt in the project root for full license text.
//
// Main button tab: lists all enabled EnhancedChatInteractiveAddonDef (RICS + third-party XML).
using CAP_ChatInteractive;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace CAP_ChatInteractive.Windows
{
    public class MainTabWindow_ChatInteractive : MainTabWindow
    {
        private const float WindowWidth = 320f;
        private const float MinOuterHeight = 120f;
        private const float ScrollbarWidth = 16f;

        // Listing_Standard.ButtonText is GetRect(30) plus the default 2px vertical gap.
        private const float ButtonRowHeight = 32f;
        private const float LabelRowHeight = 24f;

        private Vector2 scrollPosition = Vector2.zero;

        /// <summary>Last laid-out list height. Zero until the first draw; the estimate is only a seed.</summary>
        private float contentHeight;

        public override void DoWindowContents(Rect inRect)
        {
            // Pinned to the bottom bar, and RimWorld caps the window at the screen.
            // Scroll so the top buttons stay reachable on short screens and high UI scale.
            float viewHeight = LayoutHeight;
            float maxScroll = Mathf.Max(0f, viewHeight - inRect.height);
            if (scrollPosition.y > maxScroll)
                scrollPosition.y = maxScroll;

            var viewRect = new Rect(0f, 0f, Mathf.Max(1f, inRect.width - ScrollbarWidth), viewHeight);
            Widgets.BeginScrollView(inRect, ref scrollPosition, viewRect);
            try
            {
                var listing = new Listing_Standard();
                listing.Begin(viewRect);
                DrawButtons(listing);
                listing.End();
                contentHeight = listing.CurHeight;
            }
            finally
            {
                Widgets.EndScrollView();
            }
        }

        public override void WindowOnGUI()
        {
            // Apply last frame's measured height before GUI.Window copies the rect.
            // PreOpen only sizes the tab once per open, so a short first estimate would stick.
            if (contentHeight >= 1f)
            {
                float outer = OuterHeight;
                if (Mathf.Abs(windowRect.height - outer) > 1f)
                {
                    windowRect.height = outer;
                    windowRect.y = (UI.screenHeight - 35f) - outer;
                    windowRect.x = Anchor == MainTabWindowAnchor.Left
                        ? 0f
                        : UI.screenWidth - windowRect.width;
                }
            }

            base.WindowOnGUI();
        }

        public override Vector2 RequestedTabSize => new Vector2(WindowWidth, OuterHeight);

        public override MainTabWindowAnchor Anchor => MainTabWindowAnchor.Right;

        private float LayoutHeight => contentHeight >= 1f ? contentHeight : EstimateContentHeight();

        /// <summary>Outer height: the list plus window margins, never taller than the space above the bottom bar.</summary>
        private float OuterHeight
        {
            get
            {
                float available = Mathf.Max(80f, UI.screenHeight - 35f);
                float desired = LayoutHeight + (Margin * 2f);
                float minimum = Mathf.Min(MinOuterHeight, available);
                return Mathf.Clamp(desired, minimum, available);
            }
        }

        private static void DrawButtons(Listing_Standard listing)
        {
            listing.Label("RICS - Quick Menu");
            if (listing.ButtonText("RICS - Settings"))
                OpenSettings();
            listing.GapLine();

            var groupedButtons = VisibleGroups();
            if (groupedButtons == null || groupedButtons.Count == 0)
            {
                listing.Label("No addon buttons loaded.");
                return;
            }

            foreach (var group in groupedButtons)
            {
                if (group.Key != "RICS")
                {
                    listing.Gap(8f);
                    listing.Label($"{group.Key} Features");
                    listing.GapLine(4f);
                }

                foreach (var addonDef in group.OrderBy(d => d.displayOrder))
                {
                    if (addonDef.buttonType == ButtonType.Divider)
                    {
                        listing.GapLine();
                        continue;
                    }

                    string label = string.IsNullOrEmpty(addonDef.label) ? addonDef.defName : addonDef.label;
                    if (listing.ButtonText(label))
                    {
                        try
                        {
                            addonDef.ExecuteDirectly();
                        }
                        catch (Exception ex)
                        {
                            Logger.Error($"[AddonMenu] Main tab execute {addonDef.defName}: {ex}");
                        }
                    }
                }

                if (group != groupedButtons.Last())
                    listing.Gap(12f);
            }
        }

        /// <summary>First-open seed. Real height comes from <see cref="Listing.CurHeight"/> after draw.</summary>
        private static float EstimateContentHeight()
        {
            float height = LabelRowHeight + ButtonRowHeight + 12f; // title, Settings button, gap line

            var groupedButtons = VisibleGroups();
            if (groupedButtons == null || groupedButtons.Count == 0)
                return height + LabelRowHeight;

            for (int i = 0; i < groupedButtons.Count; i++)
            {
                var group = groupedButtons[i];
                if (group.Key != "RICS")
                    height += 8f + LabelRowHeight + 4f;

                foreach (var addonDef in group)
                {
                    height += addonDef.buttonType == ButtonType.Divider ? 12f : ButtonRowHeight;
                }

                if (i < groupedButtons.Count - 1)
                    height += 12f;
            }

            return height;
        }

        /// <summary>Opens the RICS mod settings window. Does nothing if that window is already open.</summary>
        private static void OpenSettings()
        {
            try
            {
                CAPChatInteractiveMod mod = CAPChatInteractiveMod.Instance;
                if (mod == null)
                {
                    Logger.Error("[AddonMenu] Cannot open RICS settings: mod instance is missing.");
                    return;
                }

                if (Find.WindowStack.WindowOfType<Dialog_ModSettings>() != null)
                    return;

                Find.WindowStack.Add(new Dialog_ModSettings(mod));
            }
            catch (Exception ex)
            {
                Logger.Error($"[AddonMenu] Open RICS settings: {ex}");
            }
        }

        private static List<IGrouping<string, EnhancedChatInteractiveAddonDef>> VisibleGroups()
        {
            var defs = AddonRegistry.AddonDefs;
            if (defs == null || defs.Count == 0)
                return null;

            return defs
                .Where(def => def != null && def.IsCurrentlyVisible() && def.buttonType != ButtonType.OpenMainTab)
                .GroupBy(def => def.sourceMod ?? "Unknown")
                .OrderBy(g => g.Key == "RICS" ? 0 : 1)
                .ThenBy(g => g.Key)
                .ToList();
        }
    }
}
