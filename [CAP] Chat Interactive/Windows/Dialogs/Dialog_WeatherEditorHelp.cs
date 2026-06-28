// Dialog_WeatherEditorHelp.cs
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
// A help dialog for the Weather Editor
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;

namespace CAP_ChatInteractive
{
    internal class Dialog_WeatherEditorHelp : Window
    {
        private Vector2 scrollPosition = Vector2.zero;

        public override Vector2 InitialSize => new Vector2(800f, 600f);

        public Dialog_WeatherEditorHelp()
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
            Widgets.Label(titleRect, "RICS.WeatherEditorHelp.Title".Translate());
            Text.Font = GameFont.Small;
            GUI.color = Color.white;

            // Content area
            Rect contentRect = new Rect(0f, 40f, inRect.width, inRect.height - 40f - CloseButSize.y);
            DrawHelpContent(contentRect);
        }

        private void DrawHelpContent(Rect rect)
        {
            StringBuilder sb = new StringBuilder();

            sb.AppendLine($"<b>Weather Editor Overview</b>");
            sb.AppendLine($"The Weather Editor allows you to configure which weather types are available for purchase via chat commands, and set their prices and karma types.");
            sb.AppendLine($"");

            sb.AppendLine($"<b>Main Interface Sections:</b>");
            sb.AppendLine($"1. <b>Left Panel</b> - Filter weather by Mod Source");
            sb.AppendLine($"   • Shows all mod sources that provide weather types");
            sb.AppendLine($"   • Click any mod source to filter weather from that mod");
            sb.AppendLine($"   • Click \"All\" to show weather from all mods");
            sb.AppendLine($"   • Numbers in parentheses show weather counts");
            sb.AppendLine($"   • Hover over truncated names to see full mod name");
            sb.AppendLine($"");

            sb.AppendLine($"2. <b>Right Panel</b> - Weather list with editing controls");
            sb.AppendLine($"   • Weather types are listed with name, description, and source mod");
            sb.AppendLine($"   • Weather names are automatically capitalized for display");
            sb.AppendLine($"   • Long descriptions are truncated with \"...\"");
            sb.AppendLine($"   • Use checkboxes to enable/disable weather types");
            sb.AppendLine($"   • Set custom prices and karma types for each weather");
            sb.AppendLine($"");

            sb.AppendLine($"<b>Header Controls:</b>");
            sb.AppendLine($"• <b>Search Bar</b> - Filter weather by name, description, or mod source");
            sb.AppendLine($"   - Searches are case-insensitive");
            sb.AppendLine($"   - Supports partial matching");
            sb.AppendLine($"");

            sb.AppendLine($"• <b>Sort Buttons</b> - Sort weather by Name, Cost, or Mod Source");
            sb.AppendLine($"   - Click once to sort by that field");
            sb.AppendLine($"   - Click again to reverse sort order");
            sb.AppendLine($"   - Arrow icon shows current sort direction");
            sb.AppendLine($"   - Hover over arrow for sort details");
            sb.AppendLine($"");

            sb.AppendLine($"<b>Action Buttons:</b>");
            sb.AppendLine($"• <b>Reset Prices</b> - Reset all weather prices to default values");
            sb.AppendLine($"   - Shows confirmation dialog before resetting");
            sb.AppendLine($"• <b>Enable →</b> - Bulk enable weather by mod source");
            sb.AppendLine($"   - Opens menu to select which mod's weather to enable");
            sb.AppendLine($"• <b>Disable →</b> - Bulk disable weather by mod source");
            sb.AppendLine($"   - Opens menu to select which mod's weather to disable");
            sb.AppendLine($"");

            sb.AppendLine($"<b>Weather Row Controls:</b>");
            sb.AppendLine($"• <b>Enabled Checkbox</b> - Toggle if weather is available for purchase");
            sb.AppendLine($"   - When enabled, weather can be bought via chat commands");
            sb.AppendLine($"   - When disabled, weather is hidden from chat store");
            sb.AppendLine($"");

            sb.AppendLine($"• <b>Cost Control</b> - Set the price in points");
            sb.AppendLine($"   - Enter custom price (0-1,000,000)");
            sb.AppendLine($"   - Click \"Reset\" to restore default price");
            sb.AppendLine($"   - Default prices are based on weather impact and duration");
            sb.AppendLine($"");

            sb.AppendLine($"• <b>Karma Dropdown</b> - Set karma type: Good, Bad, or Neutral");
            sb.AppendLine($"   - Affects player karma when weather is purchased");
            sb.AppendLine($"   - Good weather (like Clear) gives positive karma");
            sb.AppendLine($"   - Bad weather (like Toxic Rain) gives negative karma");
            sb.AppendLine($"   - Neutral weather has no karma effect");
            sb.AppendLine($"");

            sb.AppendLine($"<b>Header Icons:</b>");
            sb.AppendLine($"• <b>Help Icon (?)</b> - Opens this help window");
            sb.AppendLine($"• <b>Settings Gear</b> - Opens Event Settings dialog");
            sb.AppendLine($"   - Configure global settings for the events/weather system");
            sb.AppendLine($"   - Set default pricing multipliers");
            sb.AppendLine($"   - Configure global caps and cooldowns");
            sb.AppendLine($"");

            sb.AppendLine($"<b>Weather Availability:</b>");
            sb.AppendLine($"• All weather types from loaded mods are shown");
            sb.AppendLine($"• Weather must be enabled to appear in chat store");
            sb.AppendLine($"• Disabled weather types are still visible in editor");
            sb.AppendLine($"");

            sb.AppendLine($"<b>Mod Source Display:</b>");
            sb.AppendLine($"• \"RimWorld\" indicates vanilla/core weather types");
            sb.AppendLine($"• Mod names are extracted from package IDs");
            sb.AppendLine($"• Long mod names are truncated to fit panel");
            sb.AppendLine($"• Hover over truncated names for full name");
            sb.AppendLine($"");

            sb.AppendLine($"<b>Default Pricing:</b>");
            sb.AppendLine($"• Clear skies: Low cost (good weather)");
            sb.AppendLine($"• Rain/Snow: Moderate cost (neutral utility)");
            sb.AppendLine($"• Blood Rain/Thunderstorm: High cost (negative impact)");
            sb.AppendLine($"• Dry Thunderstorm/Flashstorm: Very high cost (dangerous)");
            sb.AppendLine($"• Custom weather from mods use similar logic");
            sb.AppendLine($"");

            sb.AppendLine($"<b>Karma Types:</b>");
            sb.AppendLine($"• <color=green>Good</color> - Weather that benefits the colony");
            sb.AppendLine($"• <color=yellow>Neutral</color> - Weather with mixed or no effect");
            sb.AppendLine($"• <color=red>Bad</color> - Weather that harms the colony");
            sb.AppendLine($"");

            sb.AppendLine($"<b>Tips:</b>");
            sb.AppendLine($"• Changes are saved automatically when you close the window");
            sb.AppendLine($"• Use bulk operations to quickly enable/disable mods");
            sb.AppendLine($"• Sort by cost to find expensive or cheap weather types");
            sb.AppendLine($"• Reset prices if you've made pricing mistakes");
            sb.AppendLine($"• Search supports partial matching (e.g., \"rain\" finds \"Rainy\" and \"Heavy Rain\")");
            sb.AppendLine($"• Consider karma impact when setting weather prices");
            sb.AppendLine($"• Disable weather types that don't make sense for your stream");
            sb.AppendLine($"• Save frequently if making many changes");

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