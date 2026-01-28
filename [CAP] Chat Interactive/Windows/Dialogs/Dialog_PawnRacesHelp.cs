// Dialog_PawnRacesHelp.cs
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
// A help dialog for the Pawn Race Settings
using System.Text;
using UnityEngine;
using Verse;

namespace CAP_ChatInteractive
{
    public class Dialog_PawnRacesHelp : Window
    {
        private Vector2 scrollPosition = Vector2.zero;

        public override Vector2 InitialSize => new Vector2(850f, 700f);

        public Dialog_PawnRacesHelp()
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
            Widgets.Label(titleRect, "Pawn Race Settings Help");
            Text.Font = GameFont.Small;
            GUI.color = Color.white;

            // Content area
            Rect contentRect = new Rect(0f, 40f, inRect.width, inRect.height - 40f - CloseButSize.y);
            DrawHelpContent(contentRect);
        }

        private void DrawHelpContent(Rect rect)
        {
            StringBuilder sb = new StringBuilder();

            sb.AppendLine($"<b>Pawn Race Settings Overview</b>");
            sb.AppendLine($"Configure which races and xenotypes are available for purchase via chat commands, and set their prices, age limits, and other options.");
            sb.AppendLine($"");

            sb.AppendLine($"<b>⚠️ IMPORTANT RECENT CHANGE - XENOTYPE PRICING UPDATE</b>");
            sb.AppendLine($"The system has been updated to use actual silver prices instead of arbitrary multipliers.");
            sb.AppendLine($"• <b>Old:</b> Race base price × xenotype multiplier");
            sb.AppendLine($"• <b>New:</b> Race base price + xenotype price");
            sb.AppendLine($"<color=orange>You MUST click the 'Reset' button next to each xenotype price to update to the new system!</color>");
            sb.AppendLine($"");

            sb.AppendLine($"<b>Main Interface Sections:</b>");
            sb.AppendLine($"1. <b>Left Panel</b> - Race List");
            sb.AppendLine($"   • Lists all humanlike races from loaded mods");
            sb.AppendLine($"   • Disabled races show [DISABLED]");
            sb.AppendLine($"   • Click any race to view and edit its settings");
            sb.AppendLine($"");

            sb.AppendLine($"2. <b>Right Panel</b> - Race Details & Settings");
            sb.AppendLine($"   • Shows race name, description, and configuration options");
            sb.AppendLine($"   • All changes are saved automatically");
            sb.AppendLine($"");

            sb.AppendLine($"<b>Header Controls:</b>");
            sb.AppendLine($"• <b>Search Bar</b> - Filter races by name or description");
            sb.AppendLine($"• <b>Sort Buttons</b> - Sort races by Name, Category, or Status");
            sb.AppendLine($"   - Name: Alphabetical order");
            sb.AppendLine($"   - Category: By mod source (Core, mod name)");
            sb.AppendLine($"   - Status: Enabled races first");
            sb.AppendLine($"• <b>Help Button (?)</b> - Open this help window");
            sb.AppendLine($"• <b>Reset All Prices</b> - Reset all xenotype prices for selected race");
            sb.AppendLine($"• <b>Debug Gear</b> - Open Race Debug Information");
            sb.AppendLine($"");

            sb.AppendLine($"<b>Race Settings Section:</b>");
            sb.AppendLine($"• <b>Enabled</b> - Toggle if this race is available for purchase");
            sb.AppendLine($"• <b>Base Price</b> - Base cost in silver for a baseliner of this race");
            sb.AppendLine($"• <b>Min/Max Age</b> - Age range allowed for this race");
            sb.AppendLine($"   - Use text fields for precise input");
            sb.AppendLine($"   - Use sliders for quick adjustment");
            sb.AppendLine($"   - Ages are in biological years");
            sb.AppendLine($"• <b>Allow Custom Xenotypes</b> - Allow custom xenotypes not in the list");
            sb.AppendLine($"");

            sb.AppendLine($"<b>Gender Restrictions (Read-Only):</b>");
            sb.AppendLine($"• Shows inherent gender limitations from HAR (Humanoid Alien Races)");
            sb.AppendLine($"• Cannot be changed here - set in the race's HAR definition");
            sb.AppendLine($"• Example: 'Female only', 'Male/Female only (no other)', etc.");
            sb.AppendLine($"");

            sb.AppendLine($"<b>Xenotype Settings Section (Biotech DLC Required):</b>");
            sb.AppendLine($"• Shows all available xenotypes from base game and mods");
            sb.AppendLine($"• <b>Enabled</b> - Checkbox to allow/disallow each xenotype");
            sb.AppendLine($"• <b>Price (silver)</b> - Additional cost on top of race base price");
            sb.AppendLine($"• <b>Reset Button</b> - Reset price to Rimworld's gene-based value");
            sb.AppendLine($"");

            sb.AppendLine($"<b>Price Calculation Formula:</b>");
            sb.AppendLine($"<b>Total Cost = Race Base Price + Xenotype Price</b>");
            sb.AppendLine($"• <b>Race Base Price</b>: Usually 1000-2000 silver (from BaseMarketValue)");
            sb.AppendLine($"• <b>Xenotype Price</b>: Sum of all gene values");
            sb.AppendLine($"• <b>Gene Value</b>: (gene.marketValueFactor - 1) × Race Base Price");
            sb.AppendLine($"");

            sb.AppendLine($"<b>Example Calculations (Human, 1000 silver base):</b>");
            sb.AppendLine($"• <b>Baseliner</b>: 1000 silver (no genes)");
            sb.AppendLine($"• <b>Sanguophage</b>: ~1500 silver (gene factor 1.5 = +500 silver)");
            sb.AppendLine($"• <b>Hussar</b>: ~1400 silver");
            sb.AppendLine($"• <b>Genie</b>: ~1300 silver");
            sb.AppendLine($"");

            sb.AppendLine($"<b>How Xenotypes Are Enabled:</b>");
            sb.AppendLine($"1. <b>Humans</b>: Only base game xenotypes enabled by default");
            sb.AppendLine($"2. <b>HAR Races</b>: Use whiteXenotypeList from HAR definitions");
            sb.AppendLine($"3. <b>Other Races</b>: All xenotypes enabled by default");
            sb.AppendLine($"• You can override these defaults in the settings");
            sb.AppendLine($"");

            sb.AppendLine($"<b>Important Notes:</b>");
            sb.AppendLine($"• Changes are saved automatically");
            sb.AppendLine($"• Prices are in actual silver (matches Rimworld economy)");
            sb.AppendLine($"• Xenotype prices should typically be 0-5000 silver");
            sb.AppendLine($"• 'Reset' button calculates proper gene-based prices");
            sb.AppendLine($"• Custom xenotypes without marketValueFactor default to 0 cost");
            sb.AppendLine($"• Age limits apply to all purchases of this race");
            sb.AppendLine($"• Gender restrictions from HAR cannot be overridden");
            sb.AppendLine($"");

            sb.AppendLine($"<b>Common Tasks:</b>");
            sb.AppendLine($"1. <b>Disable a Race</b>: Uncheck 'Enabled' checkbox");
            sb.AppendLine($"2. <b>Adjust Prices</b>: Edit Base Price or xenotype prices");
            sb.AppendLine($"3. <b>Limit Xenotypes</b>: Uncheck unwanted xenotypes");
            sb.AppendLine($"4. <b>Set Age Range</b>: Adjust Min/Max Age sliders");
            sb.AppendLine($"5. <b>Reset Prices</b>: Click 'Reset' next to each xenotype or 'Reset All Prices'");
            sb.AppendLine($"");

            sb.AppendLine($"<b>Troubleshooting:</b>");
            sb.AppendLine($"• <b>Race not showing up?</b> It might be excluded (check debug info)");
            sb.AppendLine($"• <b>Xenotype not available?</b> Check if it's enabled for that race");
            sb.AppendLine($"• <b>Price seems wrong?</b> Click 'Reset' to recalculate from genes");
            sb.AppendLine($"• <b>Gender options limited?</b> That's from HAR - cannot be changed");
            sb.AppendLine($"• <b>Biotech xenotypes missing?</b> Ensure Biotech DLC is enabled");
            sb.AppendLine($"");

            sb.AppendLine($"<b>Debug Information (Gear Icon):</b>");
            sb.AppendLine($"• Shows detailed technical information about races");
            sb.AppendLine($"• Lists excluded races (not shown in settings)");
            sb.AppendLine($"• Shows HAR integration status");
            sb.AppendLine($"• Can delete and rebuild race settings");
            sb.AppendLine($"");

            sb.AppendLine($"<b>Chat Commands Related to This Settings:</b>");
            sb.AppendLine($"• <b>!pawn [race] [xenotype] [gender] [age]</b> - Purchase a pawn");
            sb.AppendLine($"• <b>!mypawn</b> - Check on your assigned pawn");
            sb.AppendLine($"• <b>!races</b> - List available races for purchase");
            sb.AppendLine($"• <b>!xenotypes [race]</b> - List xenotypes available for a race");
            sb.AppendLine($"");

            sb.AppendLine($"<b>Tips & Best Practices:</b>");
            sb.AppendLine($"• Use 'Reset All Prices' after mod updates or price changes");
            sb.AppendLine($"• Set reasonable age ranges for each race");
            sb.AppendLine($"• Disable overpowered xenotypes for balance");
            sb.AppendLine($"• Adjust prices based on your stream's economy");
            sb.AppendLine($"• Use search to find specific races quickly");
            sb.AppendLine($"• Check debug info if something seems wrong");
            sb.AppendLine($"• Xenotype prices now match Rimworld's caravan values");
            sb.AppendLine($"• The new pricing system is more transparent and accurate");

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