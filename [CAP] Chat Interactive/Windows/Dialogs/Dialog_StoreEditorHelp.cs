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
            GUI.color = ColorLibrary.Orange;
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
            sb.AppendLine($"1. <b>Left Panel</b> - Filter items by Category");
            sb.AppendLine($"   • Click any category to filter items");
            sb.AppendLine($"   • 'All' shows all items");
            sb.AppendLine($"   • Numbers in parentheses show item counts");
            sb.AppendLine($"");

            sb.AppendLine($"2. <b>Right Panel</b> - Item list with editing controls");
            sb.AppendLine($"   • Items are displayed with icons, names, categories, and mod sources");
            sb.AppendLine($"   • Click the item icon to view detailed Def information and set custom names");
            sb.AppendLine($"   • Use controls to enable/disable items, set prices, and configure quantity limits");
            sb.AppendLine($"   • Custom names are shown with an orange dot indicator");
            sb.AppendLine($"");

            sb.AppendLine($"<b>Header Controls:</b>");
            sb.AppendLine($"• <b>Search Bar</b> - Filter items by name, category, or mod source");
            sb.AppendLine($"• <b>Sort Buttons</b> - Sort items by Name, Price, or Category");
            sb.AppendLine($"   - Click once to sort by that field");
            sb.AppendLine($"   - Click again to reverse sort order");
            sb.AppendLine($"   - Current sort direction is shown by arrow");
            sb.AppendLine($"");

            sb.AppendLine($"<b>Action Buttons:</b>");
            sb.AppendLine($"• <b>Reset All</b> - Reset all item prices to default market values");
            sb.AppendLine($"• <b>Enable →</b> - Bulk enable items by category or type");
            sb.AppendLine($"   - Enable All Items: Enable all items in the store");
            sb.AppendLine($"   - Enable by Category: Enable all items in specific categories");
            sb.AppendLine($"   - Enable by Type: Enable all weapons, apparel, or usable items");
            sb.AppendLine($"• <b>Disable →</b> - Bulk disable items by category or type");
            sb.AppendLine($"   - Disable All Items: Disable all items in the store");
            sb.AppendLine($"   - Disable by Category: Disable all items in specific categories");
            sb.AppendLine($"   - Disable by Type: Disable all weapons, apparel, or usable items");
            sb.AppendLine($"• <b>Quality/Research</b> - Open quality and research settings");
            sb.AppendLine($"");

            sb.AppendLine($"<b>Bulk Quantity Controls:</b>");
            sb.AppendLine($"• <b>Set All Qty:</b> - Configure quantity limits for all visible items");
            sb.AppendLine($"   • Checkbox: Enable/disable quantity limits for all items");
            sb.AppendLine($"   • 1x, 3x, 5x buttons: Set limit to 1, 3, or 5 stacks of the item");
            sb.AppendLine($"   • Mixed state (partially checked) means some items have limits, some don't");
            sb.AppendLine($"");

            sb.AppendLine($"<b>Category Price Controls:</b>");
            sb.AppendLine($"• Only appears when a specific category is selected (not 'All')");
            sb.AppendLine($"• <b>Category Price:</b> - Set price for all items in the selected category");
            sb.AppendLine($"• <b>Set All</b> - Apply the entered price to all items in the category");
            sb.AppendLine($"• <b>Enable →</b> - Enable items within the selected category");
            sb.AppendLine($"   - Enable All Items: Enable all items in this category");
            sb.AppendLine($"   - Enable Usable Items: Enable all usable items (food, medicine, drugs) in this category");
            sb.AppendLine($"   - Enable Wearable Items: Enable all apparel items in this category");
            sb.AppendLine($"   - Enable Equippable Items: Enable all weapons in this category");
            sb.AppendLine($"• <b>Disable →</b> - Disable items within the selected category");
            sb.AppendLine($"   - Disable All Items: Disable all items in this category");
            sb.AppendLine($"   - Disable Usable Items: Disable all usable items in this category");
            sb.AppendLine($"   - Disable Wearable Items: Disable all apparel items in this category");
            sb.AppendLine($"   - Disable Equippable Items: Disable all weapons in this category");
            sb.AppendLine($"• <b>Reset All</b> - Reset all items in the category to default prices");
            sb.AppendLine($"");

            sb.AppendLine($"<b>Item Row Controls (for each item):</b>");
            sb.AppendLine($"• <b>Icon</b> - Shows item graphic; click to view Def information and set custom names");
            sb.AppendLine($"   - Hover tooltip: 'Click icon for detailed item information and to set custom name'");
            sb.AppendLine($"• <b>Info Card Button</b> - RimWorld's built-in info card (if available)");
            sb.AppendLine($"• <b>Name Display</b> - Shows custom name (if set) or default item name");
            sb.AppendLine($"   - Hover to see full name details (Custom Name, Default Name, DefName, LabelCap)");
            sb.AppendLine($"   - Orange dot indicates a custom name is set");
            sb.AppendLine($"• <b>Enabled Checkbox</b> - Toggle if item is available for purchase");
            sb.AppendLine($"• <b>Type Checkbox</b> - Mark item as Usable, Wearable, or Equippable");
            sb.AppendLine($"   - <b>Usable</b>: Items that can be used/consumed (food, medicine, drugs)");
            sb.AppendLine($"   - <b>Wearable</b>: Apparel items that can be worn");
            sb.AppendLine($"   - <b>Equippable</b>: Weapons and tools that can be equipped");
            sb.AppendLine($"• <b>Price Controls</b> - Set custom price with Reset button");
            sb.AppendLine($"• <b>Quantity Limit Controls</b> - Configure purchase limits");
            sb.AppendLine($"");

            sb.AppendLine($"<b>Quantity Limit Controls Details:</b>");
            sb.AppendLine($"• <b>Checkbox</b> - Enable/disable purchase limits for this item");
            sb.AppendLine($"• <b>Qty:</b> label");
            sb.AppendLine($"• <b>1x, 3x, 5x buttons</b> - Quick presets for stack limits");
            sb.AppendLine($"• <b>Numeric box</b> - Manual limit entry (1-9999)");
            sb.AppendLine($"• Limits are based on item's stack size (e.g., 1 stack of 75 meals = 75 meals)");
            sb.AppendLine($"");

            sb.AppendLine($"<b>Settings Gear Icon:</b>");
            sb.AppendLine($"• Opens general mod settings");
            sb.AppendLine($"• Configure global multipliers, permissions, and other options");
            sb.AppendLine($"");

            sb.AppendLine($"<b>Def Information Window:</b>");
            sb.AppendLine($"• Click any item icon to open detailed information");
            sb.AppendLine($"• Shows all ThingDef properties including:");
            sb.AppendLine($"   - Market value and technical properties");
            sb.AppendLine($"   - Stack limits and size");
            sb.AppendLine($"   - Delivery information (minification status)");
            sb.AppendLine($"   - Component properties");
            sb.AppendLine($"   - Ingestible/Apparel/Weapon specific data");
            sb.AppendLine($"   - Current store item configuration");
            sb.AppendLine($"• <b>Custom Name Editor</b> - Set a custom display name for the item");
            sb.AppendLine($"   - Custom names override the default item name in chat store");
            sb.AppendLine($"   - Names must be unique (cannot duplicate other item names)");
            sb.AppendLine($"   - Warning shown if name duplicates another item");
            sb.AppendLine($"   - Clear button removes custom name (reverts to default)");
            sb.AppendLine($"   - Default-assigned names (same as LabelCap) are not treated as custom");
            sb.AppendLine($"");

            sb.AppendLine($"<b>Custom Name Feature:</b>");
            sb.AppendLine($"• <b>Purpose</b>: Set alternate display names for items in chat store");
            sb.AppendLine($"• <b>Access</b>: Click item icon → Def Information Window → 'Custom Name' field");
            sb.AppendLine($"• <b>Validation</b>: Names must be unique across all items");
            sb.AppendLine($"   - Checks against other custom names, def names, and label caps");
            sb.AppendLine($"   - Case-insensitive comparison");
            sb.AppendLine($"• <b>Visual Indicator</b>: Items with custom names show orange dot in main list");
            sb.AppendLine($"• <b>Tooltip</b>: Hover over item name to see custom and default names");
            sb.AppendLine($"• <b>Default Behavior</b>: If no custom name, displays default RimWorld name");
            sb.AppendLine($"");

            sb.AppendLine($"<b>Item Types Explained:</b>");
            sb.AppendLine($"• <b>Usable Items</b>: Can be consumed, used, or applied");
            sb.AppendLine($"   - Examples: Food, medicine, drugs, usable items");
            sb.AppendLine($"   - Displayed with 'Use' button in chat store");
            sb.AppendLine($"");

            sb.AppendLine($"• <b>Wearable Items</b>: Apparel that can be worn");
            sb.AppendLine($"   - Examples: Clothing, armor, accessories");
            sb.AppendLine($"   - Displayed with 'Wear' button in chat store");
            sb.AppendLine($"");

            sb.AppendLine($"• <b>Equippable Items</b>: Weapons and tools");
            sb.AppendLine($"   - Examples: Guns, melee weapons, tools");
            sb.AppendLine($"   - Displayed with 'Equip' button in chat store");
            sb.AppendLine($"");

            sb.AppendLine($"<b>Delivery System:</b>");
            sb.AppendLine($"• Items are delivered to a designated drop spot");
            sb.AppendLine($"• Large items (furniture, buildings) are minified for delivery");
            sb.AppendLine($"• Minifiable items show 'Will be delivered as minified crate' in Def info");
            sb.AppendLine($"• Item size affects delivery spot requirements");
            sb.AppendLine($"");

            sb.AppendLine($"<b>Tips & Best Practices:</b>");
            sb.AppendLine($"• Changes are saved automatically when closing the window");
            sb.AppendLine($"• Use category filters to work with specific item types");
            sb.AppendLine($"• Set quantity limits for balance (prevent spamming expensive items)");
            sb.AppendLine($"• Use the new category-specific Enable/Disable buttons to quickly manage modded items");
            sb.AppendLine($"• Perfect for streamers with many mods - quickly disable entire categories");
            sb.AppendLine($"• Bulk operations save time when configuring many items");
            sb.AppendLine($"• Check Def information for delivery details on large items");
            sb.AppendLine($"• Usable/Wearable/Equippable flags affect chat store button display");
            sb.AppendLine($"• Search supports partial matching (case-insensitive)");
            sb.AppendLine($"• Default prices are based on RimWorld's BaseMarketValue");
            sb.AppendLine($"• Some items may not appear if they lack proper ThingDef definitions");
            sb.AppendLine($"• Use custom names to create user-friendly names for technical items");
            sb.AppendLine($"• Custom names are preserved even if the mod updates or changes");
            sb.AppendLine($"• Duplicate name warnings help prevent confusion in chat commands");
            sb.AppendLine($"• Enable/Disable → buttons apply only to items in the current category");
            sb.AppendLine($"• Great for disabling all weapons from a specific weapon mod category");

            string fullText = sb.ToString();

            // Calculate text height with proper formatting
            float textHeight = Text.CalcHeight(fullText, rect.width - 30f);
            Rect viewRect = new Rect(0f, 0f, rect.width - 20f, textHeight + 20f);

            // Scroll view with background
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
