// TabWorker.cs
// Copyright (c) Captolamia. All rights reserved.
// Licensed under the AGPLv3 License. See LICENSE file in the project root for full license information.
// A utility class to manage and render tabbed interfaces in the mod.
using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace CAP_ChatInteractive
{
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