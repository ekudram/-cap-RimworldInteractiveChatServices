// Dialog_StoreEditorHelp.cs
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
// A help dialog for the Store Items Editor
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;

namespace CAP_ChatInteractive
{
    public class Dialog_StoreEditorHelp : Window
    {
        private Vector2 scrollPosition = Vector2.zero;

        public override Vector2 InitialSize => new Vector2(850f, 700f);

        public Dialog_StoreEditorHelp()
        {
            doCloseButton = true;
            forcePause = true;
            absorbInputAroundWindow = true;
            resizeable = true;
            draggable = true;
        }

        public override void DoWindowContents(Rect inRect)
        {
            // Title
            Rect titleRect = new Rect(0f, 0f, inRect.width, 35f);
            Text.Font = GameFont.Medium;
            GUI.color = ColorLibrary.HeaderAccent;
            Widgets.Label(titleRect, "Store Items Editor Help");
            Text.Font = GameFont.Small;
            GUI.color = Color.white;

            // Content area
            Rect contentRect = new Rect(0f, 40f, inRect.width, inRect.height - 40f - CloseButSize.y);
            DrawHelpContent(contentRect);
        }

        private void DrawHelpContent(Rect rect)
        {
            StringBuilder sb = new StringBuilder();

            sb.AppendLine($"<b>Store Items Editor Overview</b>");
            sb.AppendLine($"The Store Items Editor allows you to configure which items are available for purchase via chat commands, and set their prices, quantity limits, and item types.");
            sb.AppendLine($"");

            sb.AppendLine($"<b>Main Interface Sections:</b>");
            sb.AppendLine($"1. <b>Left Panel</b> - Filter items by Category or Mod Source");
            sb.AppendLine($"   • Click header to toggle between Category and Mod Source view");
            sb.AppendLine($"   • 'All' shows every item");
            sb.AppendLine($"   • Numbers in parentheses show current filtered count");
            sb.AppendLine($"");

            sb.AppendLine($"2. <b>Right Panel</b> - Item list with editing controls");
            sb.AppendLine($"   • Items show icon, name (custom name = orange dot), category, mod source");
            sb.AppendLine($"   • Click icon → Def Information Window (custom name editor + full ThingDef data)");
            sb.AppendLine($"   • Controls: Enabled, Usable/Wearable/Equippable, Price, Quantity Limit");
            sb.AppendLine($"");

            sb.AppendLine($"<b>Header Controls:</b>");
            sb.AppendLine($"• <b>Search Bar</b> - Filter by name, category, or mod source (live)");
            sb.AppendLine($"• <b>Sort Buttons</b> - Name, Price, Category, or Mod Source (toggle direction with ↑/↓)");
            sb.AppendLine($"• <b>Action Buttons</b> - Reset All, Enable →, Disable →, Quality/Research");
            sb.AppendLine($"");

            sb.AppendLine($"<b>Bulk Actions (Enable/Disable menus):</b>");
            sb.AppendLine($"• Enable/Disable **All Items**, by **Category**, or by **Type** (Weapons, Apparel, Usable)");
            sb.AppendLine($"• When a specific category or mod source is selected, bulk actions apply only to visible items");
            sb.AppendLine($"");

            sb.AppendLine($"<b>Bulk Quantity Controls (top of item list):</b>");
            sb.AppendLine($"• Checkbox: Enable/disable quantity limits for all visible items");
            sb.AppendLine($"• 1× / 3× / 5× icons: Quick-set to that many stacks (based on ThingDef.stackLimit)");
            sb.AppendLine($"• Mixed state (□) = some items have limits, some don't");
            sb.AppendLine($"");

            sb.AppendLine($"<b>Item Row Controls:</b>");
            sb.AppendLine($"• **Icon** – Click for Def info + custom name editor");
            sb.AppendLine($"• **Enabled** – Makes item buyable via chat");
            sb.AppendLine($"• **Usable / Wearable / Equippable** – Determines chat store button text ('Use'/'Wear'/'Equip')");
            sb.AppendLine($"• **Price** – Custom price (Reset pulls ThingDef.BaseMarketValue)");
            sb.AppendLine($"• **Quantity Limit** – Checkbox + presets + manual numeric field (1-9999)");
            sb.AppendLine($"");

            sb.AppendLine($"<b>Quantity Limit Details:</b>");
            sb.AppendLine($"• Limits are per-stack (e.g. 3× on a 75-stack meal = 225 meals max per purchase)");
            sb.AppendLine($"• Prevents chat spam of high-value items");
            sb.AppendLine($"");

            sb.AppendLine($"<b>Custom Name Feature:</b>");
            sb.AppendLine($"• Set friendly names that appear in chat store");
            sb.AppendLine($"• Orange dot indicator on main list");
            sb.AppendLine($"• Hover name for tooltip showing Custom / Default / DefName");
            sb.AppendLine($"• Names must be unique (validation + warning on duplicate)");
            sb.AppendLine($"");

            sb.AppendLine($"<b>Quality & Research Button:</b>");
            sb.AppendLine($"• Opens global quality multipliers and research requirement toggle");
            sb.AppendLine($"");

            sb.AppendLine($"<b>Delivery System Reminder:</b>");
            sb.AppendLine($"• Large/minifiable items are delivered as crates");
            sb.AppendLine($"• Check Def info window for minification status and size");
            sb.AppendLine($"");

            sb.AppendLine($"<b>Tips & Best Practices:</b>");
            sb.AppendLine($"• All changes auto-save (real-time JSON + in-memory)");
            sb.AppendLine($"• Use Mod Source view for large modpacks");
            sb.AppendLine($"• Quantity limits are strongly recommended for balance");
            sb.AppendLine($"• Custom names survive mod updates");
            sb.AppendLine($"• Search + Category/Mod filters = fast workflow");
            sb.AppendLine($"• Reset prices uses vanilla BaseMarketValue (rounded)");

            string fullText = sb.ToString();

            float textHeight = Text.CalcHeight(fullText, rect.width - 30f);
            Rect viewRect = new Rect(0f, 0f, rect.width - 20f, textHeight + 20f);

            Widgets.DrawMenuSection(rect);
            Widgets.BeginScrollView(new Rect(rect.x + 5f, rect.y + 5f, rect.width - 10f, rect.height - 10f),
                                   ref scrollPosition, viewRect);

            GUI.color = new Color(0.9f, 0.9f, 0.9f, 1f);
            Widgets.Label(new Rect(0f, 0f, viewRect.width, textHeight), fullText);
            GUI.color = Color.white;

            Widgets.EndScrollView();
        }
    }
}
