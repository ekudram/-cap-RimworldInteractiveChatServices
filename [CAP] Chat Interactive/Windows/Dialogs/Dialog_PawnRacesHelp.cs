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
using RimWorld;
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

        // In Dialog_PawnRacesHelp.cs, inside Dialog_PawnRacesHelp.DrawHelpContent()
        private void DrawHelpContent(Rect rect)
        {
            StringBuilder sb = new StringBuilder();

            sb.AppendLine($"<b>Pawn Race & Xenotype Settings Overview</b>");
            sb.AppendLine($"Configure which races and xenotypes viewers can purchase, set prices (absolute silver), age ranges, and enable/disable options.");
            sb.AppendLine($"");

            sb.AppendLine($"<b>⚠️ XENOTYPE PRICING — IMPORTANT UPDATE</b>");
            sb.AppendLine($"Prices are now <b>absolute silver values</b> (Race Base + Xenotype Price).");
            sb.AppendLine($"• Use the <b>Reset</b> button next to each xenotype (or <b>Reset All Prices</b> in header) after mod updates.");
            sb.AppendLine($"<color=orange>Click Reset on every xenotype you want updated to the new gene-based system!</color>");
            sb.AppendLine($"");

            sb.AppendLine($"<b>Main Interface Sections:</b>");
            sb.AppendLine($"1. <b>Left Panel</b> - Race List");
            sb.AppendLine($"   • All humanlike races from loaded mods (excluded races hidden)");
            sb.AppendLine($"   • [DISABLED] tag on disabled races");
            sb.AppendLine($"   • Click to edit → right panel updates instantly");
            sb.AppendLine($"");

            sb.AppendLine($"2. <b>Right Panel</b> - Detailed Settings");
            sb.AppendLine($"   • Race description, gender restrictions (HAR read-only), age range, base price");
            sb.AppendLine($"   • Xenotype list (only HAR-allowed xenotypes shown)");
            sb.AppendLine($"");

            sb.AppendLine($"<b>Header Controls:</b>");
            sb.AppendLine($"• <b>Search</b> - Live filter by name/description");
            sb.AppendLine($"• <b>Sort</b> - Name, Category (mod source), Status (enabled first)");
            sb.AppendLine($"• <b>Reset All Prices</b> - Reset xenotype prices for selected race (confirmation dialog)");
            sb.AppendLine($"• <b>Help (?)</b> - This window");
            sb.AppendLine($"• <b>Debug Gear</b> - Technical info + rebuild settings");
            sb.AppendLine($"");

            sb.AppendLine($"<b>Race Settings:</b>");
            sb.AppendLine($"• <b>Enabled</b> - Toggle availability for chat purchases");
            sb.AppendLine($"• <b>Base Price</b> - Silver cost for a baseliner of this race");
            sb.AppendLine($"• <b>Min / Max Age</b> - Text fields + sliders (buffered, auto-clamped)");
            sb.AppendLine($"• <b>Allow Custom Xenotypes</b> - Permit xenotypes not in the explicit list");
            sb.AppendLine($"• <b>Gender Restrictions</b> - Read-only (sourced from HAR)");
            sb.AppendLine($"");

            sb.AppendLine($"<b>Xenotype Section (Biotech required):</b>");
            sb.AppendLine($"• Only xenotypes allowed by HAR for the selected race are shown");
            sb.AppendLine($"• <b>Enabled</b> checkbox per xenotype");
            sb.AppendLine($"• <b>Price (silver)</b> - Additional cost on top of race base");
            sb.AppendLine($"• <b>Reset</b> button - Calculates proper gene market value");
            sb.AppendLine($"• <b>Bulk Button</b> - \"Set All Xenotypes To Base Price\" (one-click uniform pricing)");
            sb.AppendLine($"");

            sb.AppendLine($"<b>Price Formula (transparent & accurate):</b>");
            sb.AppendLine($"<b>Total = Race Base Price + Xenotype Price</b>");
            sb.AppendLine($"Xenotype Price = Gene contribution (marketValueFactor logic via GeneUtils)");
            sb.AppendLine($"");

            sb.AppendLine($"<b>Header Bulk Reset:</b>");
            sb.AppendLine($"• Only affects currently selected race");
            sb.AppendLine($"• Confirms before resetting every xenotype price for that race");
            sb.AppendLine($"");

            sb.AppendLine($"<b>Important Notes:</b>");
            sb.AppendLine($"• All changes auto-save (JSON + in-memory)");
            sb.AppendLine($"• HAR gender & xenotype restrictions are respected and shown read-only");
            sb.AppendLine($"• Excluded races never appear in the list");
            sb.AppendLine($"• Use Debug Gear → \"Delete RaceSettings & Rebuild\" after major mod changes");
            sb.AppendLine($"");

            sb.AppendLine($"<b>Common Tasks:</b>");
            sb.AppendLine($"• Disable unwanted races");
            sb.AppendLine($"• Fine-tune age ranges with sliders + text fields");
            sb.AppendLine($"• Reset xenotype prices after adding gene mods");
            sb.AppendLine($"• Use bulk button for simple uniform pricing");
            sb.AppendLine($"• Search to quickly locate a race");
            sb.AppendLine($"");

            sb.AppendLine($"<b>Related Chat Commands:</b>");
            sb.AppendLine($"• <b>!pawn [race] [xenotype] [gender] [age]</b>");
            sb.AppendLine($"• <b>!races</b> / <b>!xenotypes [race]</b>");
            sb.AppendLine($"");

            sb.AppendLine($"<b>Tips & Best Practices:</b>");
            sb.AppendLine($"• Reset prices after every major mod update");
            sb.AppendLine($"• Keep xenotype prices 0-5000 silver for balance");
            sb.AppendLine($"• Use bulk button when you want every xenotype the same price");
            sb.AppendLine($"• Check Debug window if a race/xenotype is missing");
            sb.AppendLine($"• Prices now match RimWorld caravan / gene economy values");

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