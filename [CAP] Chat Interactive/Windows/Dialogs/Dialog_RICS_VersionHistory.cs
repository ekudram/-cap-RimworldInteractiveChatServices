// Source/RICS/UI/Dialog_RICS_VersionHistory.cs
// Copyright (c) Captolamia
// This file is part of CAP Chat Interactive.
// 
// Styled exactly like your Dialog_WeatherEditor for consistency (left list, right content, nice header, scroll).

using Newtonsoft.Json;
using RimWorld;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using Verse;

namespace CAP_ChatInteractive
{
    public class Dialog_RICS_VersionHistory : Window
    {
        private Vector2 leftScroll = Vector2.zero;
        private Vector2 rightScroll = Vector2.zero;
        private string selectedVersion = "";
        private List<string> versionList = new List<string>();

        public override Vector2 InitialSize => new Vector2(1100f, 720f);

        /// <summary>
        /// Parameterless constructor required for Activator.CreateInstance (XML buttons, toolbar, MainTabWindow, etc.)
        /// Calls the real constructor with null so it still auto-selects latest version.
        /// </summary>
        public Dialog_RICS_VersionHistory() : this(null) { }

        public Dialog_RICS_VersionHistory(string autoSelectVersion = null)
        {
            doCloseButton = false;           // easy close, as requested
            forcePause = true;
            absorbInputAroundWindow = true;

            // Build sorted list (newest first — matches your WeatherEditor sort style)
            versionList = VersionHistory.UpdateNotes.Keys.OrderByDescending(v => v).ToList();

            // Auto-focus latest version (or the one passed from update check)
            if (!string.IsNullOrEmpty(autoSelectVersion) && versionList.Contains(autoSelectVersion))
                selectedVersion = autoSelectVersion;
            else if (versionList.Any())
                selectedVersion = versionList[0];
        }

        public override void DoWindowContents(Rect inRect)
        {
            // Thinned left panel (was 280f) — still wide enough for "Version History" header + 5f side padding.
            // Freed ~60px transferred to right notes panel for better changelog readability.
            Rect leftRect = new Rect(inRect.x, inRect.y, 220f, inRect.height);
            Rect rightRect = new Rect(leftRect.xMax + 10f, inRect.y, inRect.width - leftRect.width - 10f, inRect.height);

            DrawVersionList(leftRect);
            DrawNotesPanel(rightRect);
        }

        private void DrawVersionList(Rect rect)
        {
            Widgets.DrawMenuSection(rect);

            // Header (matches your WeatherEditor style)
            Rect headerRect = new Rect(rect.x + 5f, rect.y + 5f, rect.width - 10f, 35f);
            Text.Font = GameFont.Medium;
            GUI.color = ColorLibrary.SubHeader;
            Widgets.Label(headerRect, "Version History");
            GUI.color = Color.white;    

            // Version list (exactly like your mod sources column)
            Rect listRect = new Rect(rect.x + 5f, rect.y + 45f, rect.width - 10f, rect.height - 85f);
            Rect viewRect = new Rect(0f, 0f, listRect.width - 20f, versionList.Count * 36f);

            Widgets.BeginScrollView(listRect, ref leftScroll, viewRect);
            float y = 0f;
            foreach (string ver in versionList)
            {
                Rect row = new Rect(0f, y, viewRect.width, 32f);
                bool isSelected = ver == selectedVersion;

                if (isSelected) Widgets.DrawHighlightSelected(row);
                else if (Mouse.IsOver(row)) Widgets.DrawHighlight(row);

                if (Widgets.ButtonText(row, ver))
                {
                    selectedVersion = ver;
                }
                y += 36f;
            }
            Widgets.EndScrollView();
        }

        private void DrawNotesPanel(Rect rect)
        {
            Widgets.DrawMenuSection(rect);

            if (string.IsNullOrEmpty(selectedVersion))
            {
                Widgets.Label(new Rect(rect.x + 20f, rect.y + 20f, rect.width - 40f, 60f), "Select a version on the left.");
                return;
            }

            // Version header
            Rect headerRect = new Rect(rect.x + 15f, rect.y + 8f, rect.width - 30f, 40f);
            Text.Font = GameFont.Medium;
            Widgets.Label(headerRect, $"Version {selectedVersion}");

            // Notes area (scrollable, handles long changelogs)
            if (VersionHistory.UpdateNotes.TryGetValue(selectedVersion, out string notes))
            {
                Rect textArea = new Rect(rect.x + 15f, rect.y + 55f, rect.width - 35f, rect.height - 70f);
                float height = Text.CalcHeight(notes, textArea.width - 20f) + 50f;
                Widgets.BeginScrollView(textArea, ref rightScroll, new Rect(0, 0, textArea.width - 20f, height));  // ← NO assignment (void method, exactly like your WeatherEditor)
                Widgets.Label(new Rect(0, 0, textArea.width - 20f, height), notes);
                Widgets.EndScrollView();
            }
        }
    }
}