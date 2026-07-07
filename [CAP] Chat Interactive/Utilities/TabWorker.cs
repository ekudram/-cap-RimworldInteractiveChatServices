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
// A utility class to manage and render tabbed interfaces in the mod.

/*
 * IMPLEMENTATION NOTES:
 * - Standard UI tab pattern implementation for RimWorld modding
 * - Self-contained rendering system (vs external UI dependencies)
 * - Custom visual design and interaction handling
 * - Common solution to tabbed interface requirements
 */

using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace CAP_ChatInteractive
{
    /// <summary>
    /// Represents a single tab item in the tabbed interface.
    /// Needs a lot of work,
    /// Needs to handle multiple lines of tabs
    /// </summary>
    public class TabItem
    {
        public string Label { get; set; }
        public string Tooltip { get; set; }
        public Action<Rect> ContentDrawer { get; set; }
        public Func<bool> Clicked { get; set; }

        public float Width => Text.CalcSize(Label).x + 20f;

        public void Draw(Rect region)
        {
            ContentDrawer?.Invoke(region);
        }
    }

    public class TabWorker
    {
        private readonly List<TabItem> _tabItems = new();
        public TabItem SelectedTab { get; set; }

        public void AddTab(TabItem tab)
        {
            _tabItems.Add(tab);
            SelectedTab ??= tab;
        }

        public void RemoveTab(string label)
        {
            TabItem tab = GetTab(label);
            if (tab != null)
            {
                _tabItems.Remove(tab);
            }
        }

        public TabItem GetTab(string label)
        {
            return _tabItems.Find(t => t.Label.Equals(label, StringComparison.InvariantCulture));
        }

        public void Draw(Rect region, bool vertical = false, bool paneled = false)
        {
            float offset = 0;

            foreach (TabItem tab in _tabItems)
            {
                var tabRegion = new Rect(region.x + offset, region.y, tab.Width + 25f, region.height);

                if (DrawTabButton(tabRegion, tab.Label, SelectedTab == tab) && (tab.Clicked == null || tab.Clicked()))
                {
                    SelectedTab = tab;
                }

                offset += tabRegion.width;

                if (paneled)
                {
                    GUI.color = Color.gray;
                    Widgets.DrawLineHorizontal(tabRegion.x, tabRegion.yMax - 1f, tabRegion.width);
                    GUI.color = Color.white;
                }
            }
        }

        /// <summary>
        /// Draws a single tab button with the specified label and active state.
        /// TODO MAKE A BETTER LOOKING TAB BUTTON
        /// </summary>
        /// <param name="rect"></param>
        /// <param name="label"></param>
        /// <param name="active"></param>
        /// <returns></returns>
        private bool DrawTabButton(Rect rect, string label, bool active)
        {
            var buttonRect = rect.ContractedBy(2f);

            // Draw background
            var bgColor = active ? new Color(0.3f, 0.3f, 0.3f, 1f) : new Color(0.2f, 0.2f, 0.2f, 1f);
            Widgets.DrawBoxSolid(buttonRect, bgColor);

            // Draw border
            Widgets.DrawBox(buttonRect);

            // Draw label
            var labelColor = active ? Color.green : Color.gray;
            var textRect = buttonRect.ContractedBy(4f);
            Widgets.Label(textRect, label);

            return Widgets.ButtonInvisible(rect);
        }
    }
}