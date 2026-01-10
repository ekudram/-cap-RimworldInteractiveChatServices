// Dialog_RICS_updates.cs (updated)
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

using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace CAP_ChatInteractive
{
    public class Dialog_RICS_Updates : Window
    {
        private Vector2 scrollPosition = Vector2.zero;
        private string updateNotes = "";

        public Dialog_RICS_Updates(string notes)
        {
            doCloseButton = true;
            forcePause = true;
            absorbInputAroundWindow = true;
            updateNotes = notes;
            closeOnClickedOutside = false;
        }

        public override Vector2 InitialSize => new Vector2(700f, 700f); // Increased from 600f to 650f

        public override void DoWindowContents(Rect inRect)
        {
            // Draw header
            DrawHeader(new Rect(inRect.x, inRect.y, inRect.width, 100f));

            // Main content area
            float closeButtonHeight = CloseButSize.y;
            float bottomMargin = 10f; // Extra margin at bottom
            Rect contentRect = new Rect(
                inRect.x,
                inRect.y + 60f, // Increased from 40f to 45f
                inRect.width,
                inRect.height - 96f - closeButtonHeight - bottomMargin
            );

            Widgets.DrawMenuSection(contentRect);

            // Calculate text height
            float textWidth = contentRect.width - 30f; // Account for margins
            float textHeight = Text.CalcHeight(updateNotes, textWidth);

            // Make sure viewRect is tall enough for all content
            float viewRectHeight = Mathf.Max(contentRect.height - 20f, textHeight + 20f);
            Rect viewRect = new Rect(0f, 0f, contentRect.width - 20f, viewRectHeight);

            Widgets.BeginScrollView(contentRect.ContractedBy(10f), ref scrollPosition, viewRect);

            // Update notes text with word wrap
            Text.Font = GameFont.Small;
            GUI.color = Color.white;

            Rect textRect = new Rect(0f, 0f, textWidth, textHeight);
            Widgets.Label(textRect, updateNotes);

            Widgets.EndScrollView();

            // Add warning for critical migrations
            if (updateNotes.Contains("CRITICAL MIGRATION REQUIRED"))
            {
                Rect warningRect = new Rect(
                    inRect.x,
                    contentRect.yMax + 5f,
                    inRect.width,
                    25f
                );

                Text.Font = GameFont.Medium;
                GUI.color = Color.red;
                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(warningRect, "IMPORTANT: Critical Migration Required!");
                Text.Anchor = TextAnchor.UpperLeft;
                GUI.color = Color.white;
                Text.Font = GameFont.Small;
            }
        }

        private void DrawHeader(Rect rect)
        {
            Widgets.BeginGroup(rect);

            // Title row - Orange with underline
            Text.Font = GameFont.Medium;
            GUI.color = ColorLibrary.Orange;
            Rect titleRect = new Rect(0f, 0f, rect.width, 30f);
            Widgets.Label(titleRect, "RICS Update Notification");

            // Draw underline
            Rect underlineRect = new Rect(titleRect.x, titleRect.yMax - 2f, titleRect.width, 2f);
            Widgets.DrawLineHorizontal(underlineRect.x, underlineRect.y, underlineRect.width);

            // Subtitle with version info
            Text.Font = GameFont.Small;
            GUI.color = Color.gray;
            Rect subtitleRect = new Rect(0f, titleRect.yMax + 2f, rect.width, 20f);

            var settings = CAPChatInteractiveMod.Instance?.Settings?.GlobalSettings;
            string versionText = settings != null ?
                $"Version {settings.modVersion} - What's New" :
                "Version Update - What's New";

            Widgets.Label(subtitleRect, versionText);

            Text.Font = GameFont.Small;
            GUI.color = Color.white;

            Widgets.EndGroup();
        }

        public override void PostClose()
        {
            base.PostClose();
            Logger.Debug("Update notification window closed");
        }
    }
}