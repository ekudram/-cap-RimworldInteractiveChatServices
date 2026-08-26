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
//
// Main button tab: lists all enabled EnhancedChatInteractiveAddonDef (RICS + third-party XML).
using RimWorld;
using System;
using System.Linq;
using UnityEngine;
using Verse;

namespace CAP_ChatInteractive.Windows
{
    public class MainTabWindow_ChatInteractive : MainTabWindow
    {
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
                .Where(def => def != null && def.IsCurrentlyVisible())
                .GroupBy(def => def.sourceMod ?? "Unknown")
                .OrderBy(g => g.Key == "RICS" ? 0 : 1)
                .ThenBy(g => g.Key)
                .ToList();

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

            listing.End();
        }

        public override Vector2 RequestedTabSize
        {
            get
            {
                var defs = AddonRegistry.AddonDefs;
                if (defs == null || defs.Count == 0)
                    return new Vector2(320f, 120f);

                int realButtonCount = defs.Count(d => d != null && d.IsCurrentlyVisible() && d.buttonType != ButtonType.Divider);
                int dividerCount = defs.Count(d => d != null && d.IsCurrentlyVisible() && d.buttonType == ButtonType.Divider);

                float height = 85f;
                height += realButtonCount * 34f;
                height += dividerCount * 16f;
                height += 40f;

                return new Vector2(320f, height);
            }
        }

        public override MainTabWindowAnchor Anchor => MainTabWindowAnchor.Right;
    }
}
