// Dialog_RICS_VersionHistory.cs
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
// Version history browser — orange header + bottom Discord / Wiki / Close bar
// (matches Command Manager / Events Editor chrome).

using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace CAP_ChatInteractive
{
    public class Dialog_RICS_VersionHistory : Window
    {
        private const string DiscordUrl = "https://discord.com/invite/PcDqPmmxXj";
        private const string WikiUrl = "https://github.com/ekudram/-cap-RimworldInteractiveChatServices/wiki";

        private Vector2 leftScroll = Vector2.zero;
        private Vector2 rightScroll = Vector2.zero;
        private string selectedVersion = "";
        private List<string> versionList = new List<string>();

        public override Vector2 InitialSize => new Vector2(1100f, 720f);

        /// <summary>
        /// Parameterless constructor required for Activator.CreateInstance (XML buttons, toolbar, MainTabWindow, etc.)
        /// </summary>
        public Dialog_RICS_VersionHistory() : this(null) { }

        public Dialog_RICS_VersionHistory(string autoSelectVersion = null)
        {
            doCloseButton = false; // custom bottom Close button
            forcePause = true;
            absorbInputAroundWindow = true;
            closeOnClickedOutside = false;

            versionList = VersionHistory.UpdateNotes.Keys.OrderByDescending(v => v).ToList();

            if (!string.IsNullOrEmpty(autoSelectVersion) && versionList.Contains(autoSelectVersion))
                selectedVersion = autoSelectVersion;
            else if (versionList.Any())
                selectedVersion = versionList[0];
        }

        public override void DoWindowContents(Rect inRect)
        {
            float bottomBarHeight = 50f;
            float headerHeight = 50f;

            // Orange header (full width — same pattern as editors)
            Rect headerRect = new Rect(0f, 0f, inRect.width, headerHeight);
            DrawHeader(headerRect);

            // Content under header, above bottom bar
            Rect contentRect = new Rect(
                0f,
                headerHeight + 5f,
                inRect.width,
                inRect.height - headerHeight - 5f - bottomBarHeight);

            float leftWidth = 220f;
            Rect leftRect = new Rect(contentRect.x, contentRect.y, leftWidth, contentRect.height);
            Rect rightRect = new Rect(
                leftRect.xMax + 10f,
                contentRect.y,
                contentRect.width - leftWidth - 10f,
                contentRect.height);

            DrawVersionList(leftRect);
            DrawNotesPanel(rightRect);

            DrawBottomBar(inRect, bottomBarHeight);
        }

        private void DrawHeader(Rect rect)
        {
            Widgets.BeginGroup(rect);

            Text.Font = GameFont.Medium;
            GUI.color = ColorLibrary.HeaderAccent;
            Rect titleRect = new Rect(0f, 0f, rect.width - 10f, 30f);
            Widgets.Label(titleRect, "RICS.VersionHistory.Title".Translate());

            // Underline
            Rect underlineRect = new Rect(titleRect.x, titleRect.yMax - 2f, Mathf.Min(280f, titleRect.width), 2f);
            Widgets.DrawLineHorizontal(underlineRect.x, underlineRect.y, underlineRect.width);

            // Subtitle — current mod version when available
            Text.Font = GameFont.Small;
            GUI.color = Color.gray;
            var settings = CAPChatInteractiveMod.Instance?.Settings?.GlobalSettings;
            string sub = settings != null && !string.IsNullOrEmpty(settings.modVersion)
                ? "RICS.VersionHistory.SubtitleCurrent".Translate(settings.modVersion)
                : "RICS.VersionHistory.Subtitle".Translate();
            Widgets.Label(new Rect(0f, titleRect.yMax + 4f, rect.width, 18f), sub);

            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            Widgets.EndGroup();
        }

        private void DrawVersionList(Rect rect)
        {
            Widgets.DrawMenuSection(rect);

            Rect listHeader = new Rect(rect.x + 5f, rect.y + 5f, rect.width - 10f, 28f);
            Text.Font = GameFont.Small;
            GUI.color = ColorLibrary.SubHeader;
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(listHeader, "RICS.VersionHistory.ListHeader".Translate());
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = Color.white;

            Rect listRect = new Rect(rect.x + 5f, rect.y + 36f, rect.width - 10f, rect.height - 42f);
            Rect viewRect = new Rect(0f, 0f, listRect.width - 20f, versionList.Count * 36f);

            Widgets.BeginScrollView(listRect, ref leftScroll, viewRect);
            float y = 0f;
            foreach (string ver in versionList)
            {
                Rect row = new Rect(0f, y, viewRect.width, 32f);
                bool isSelected = ver == selectedVersion;

                if (isSelected) Widgets.DrawHighlightSelected(row);
                else if (Mouse.IsOver(row)) Widgets.DrawHighlight(row);

                if (Widgets.ButtonText(row, ver, drawBackground: false))
                    selectedVersion = ver;

                y += 36f;
            }
            Widgets.EndScrollView();
        }

        private void DrawNotesPanel(Rect rect)
        {
            Widgets.DrawMenuSection(rect);

            if (string.IsNullOrEmpty(selectedVersion))
            {
                Widgets.Label(
                    new Rect(rect.x + 20f, rect.y + 20f, rect.width - 40f, 60f),
                    "RICS.VersionHistory.SelectVersion".Translate());
                return;
            }

            // Selected version sub-header (not the main orange title)
            Rect headerRect = new Rect(rect.x + 15f, rect.y + 8f, rect.width - 30f, 32f);
            Text.Font = GameFont.Medium;
            GUI.color = ColorLibrary.SubHeader;
            Widgets.Label(headerRect, "RICS.VersionHistory.VersionPrefix".Translate(selectedVersion));
            GUI.color = Color.white;
            Text.Font = GameFont.Small;

            if (!VersionHistory.UpdateNotes.TryGetValue(selectedVersion, out string notes))
                return;

            Rect textArea = new Rect(rect.x + 15f, rect.y + 48f, rect.width - 35f, rect.height - 60f);
            float height = Text.CalcHeight(notes, textArea.width - 20f) + 50f;
            Widgets.BeginScrollView(textArea, ref rightScroll, new Rect(0, 0, textArea.width - 20f, height));
            Widgets.Label(new Rect(0, 0, textArea.width - 20f, height), notes);
            Widgets.EndScrollView();
        }

        private void DrawBottomBar(Rect inRect, float bottomBarHeight)
        {
            float btnH = 38f;
            float btnW = 140f;
            float gap = 8f;
            float padding = 12f;
            float currentY = inRect.yMax - bottomBarHeight + (bottomBarHeight - btnH) / 2f;

            // Discord (left)
            Rect discordRect = new Rect(padding, currentY, btnW, btnH);
            if (Widgets.ButtonText(discordRect, "RICS.VersionHistory.Discord".Translate()))
            {
                Application.OpenURL(DiscordUrl);
            }
            TooltipHandler.TipRegion(discordRect, DiscordUrl);

            // Wiki
            Rect wikiRect = new Rect(padding + btnW + gap, currentY, btnW, btnH);
            if (Widgets.ButtonText(wikiRect, "RICS.VersionHistory.Wiki".Translate()))
            {
                Application.OpenURL(WikiUrl);
            }
            TooltipHandler.TipRegion(wikiRect, WikiUrl);

            // Close (right-aligned) — same pattern as editors, no Save
            float closeX = inRect.xMax - btnW - padding;
            Rect closeRect = new Rect(closeX, currentY, btnW, btnH);
            if (Widgets.ButtonText(closeRect, "RICS.Editor.Close".Translate()))
            {
                Close();
            }
        }
    }
}
