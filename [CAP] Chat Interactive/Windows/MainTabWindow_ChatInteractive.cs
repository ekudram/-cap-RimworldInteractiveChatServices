// MainTabWindow_ChatInteractive.cs
// Copyright (c) Captolamia
// This file is part of CAP Chat Interactive.
// 
// CAP Chat Interactive is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License as published
// by the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// 
// CAP Chat Interactive is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
// GNU Affero General Public License for more details.
// 
// You should have received a copy of the GNU Affero General Public License
// along with CAP Chat Interactive. If not, see <https://www.gnu.org/licenses/>.
// Main tab window for CAP Chat Interactive mod
using RimWorld;
using System;
using System.Linq;
using UnityEngine;
using Verse;

namespace CAP_ChatInteractive.Windows
{
    public class MainTabWindow_ChatInteractive : MainTabWindow
    {
        public MainTabWindow_ChatInteractive()
        {
            // Logger.Debug("MainTabWindow_ChatInteractive constructor called");
        }

        public override void DoWindowContents(Rect inRect)
        {
            var listing = new Listing_Standard();
            listing.Begin(inRect);

            listing.Label("RICS - Quick Menu");
            listing.GapLine();

            var defs = AddonRegistry.AddonDefs;
            if (defs == null || defs.Count == 0)
            {
                listing.Label("No addon buttons loaded.");
                listing.End();
                return;
            }

            var groupedButtons = defs
                .Where(def => def != null && def.enabled)
                .GroupBy(def => def.sourceMod ?? "Unknown")
                .OrderBy(g => g.Key == "RICS" ? 0 : 1)
                .ThenBy(g => g.Key)
                .ToList();

            foreach (var group in groupedButtons)
            {
                // Add mod header (except for RICS which already has one)
                if (group.Key != "RICS")
                {
                    listing.Gap(8f);
                    listing.Label($"{group.Key} Features");
                    listing.GapLine(4f);
                }

                // Add buttons for this mod — DIVIDER SUPPORT ADDED
                foreach (var addonDef in group.OrderBy(d => d.displayOrder))
                {
                    if (addonDef.buttonType == ButtonType.Divider)
                    {
                        listing.GapLine();          // Clean visual separator (exactly the "----" look)
                        continue;
                    }

                    if (listing.ButtonText(addonDef.label))
                    {
                        addonDef.ExecuteDirectly();
                    }
                }

                // Add extra gap after mod group (except after last)
                if (group != groupedButtons.Last())
                {
                    listing.Gap(12f);
                }
            }

            listing.End();
        }

        //public override Vector2 RequestedTabSize => new Vector2(300f, 100f + (AddonRegistry.AddonDefs.Count * 32f));
        public override Vector2 RequestedTabSize
        {
            get
            {
                // Count everything that takes space
                int realButtonCount = AddonRegistry.AddonDefs.Count(d => d.enabled && d.buttonType != ButtonType.Divider);
                int dividerCount = AddonRegistry.AddonDefs.Count(d => d.enabled && d.buttonType == ButtonType.Divider);

                int realButtonCount = defs.Count(d => d != null && d.enabled && d.buttonType != ButtonType.Divider);
                int dividerCount = defs.Count(d => d != null && d.enabled && d.buttonType == ButtonType.Divider);

                float height = 85f;
                height += realButtonCount * 34f;
                height += dividerCount * 16f;
                height += 40f;

                return new Vector2(320f, height);           // slightly wider for long labels
            }
        }
        
        
        // CHANGED: Force right anchor position
        public override MainTabWindowAnchor Anchor => MainTabWindowAnchor.Right;

        public override void PostOpen()
        {
            base.PostOpen();
            Logger.Debug("MainTabWindow_ChatInteractive opened");
        }
    }
}