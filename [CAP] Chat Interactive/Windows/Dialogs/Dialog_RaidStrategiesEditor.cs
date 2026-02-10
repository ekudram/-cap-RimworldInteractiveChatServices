// Dialog_RaidStrategiesEditor.cs
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
// A dialog window for editing allowed raid strategies in chat commands
using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace CAP_ChatInteractive
{
    public class Dialog_RaidStrategiesEditor : Window
    {
        private CommandSettings settings;
        private Vector2 scrollPosition = Vector2.zero;

        // All possible raid strategies with display names
        private static readonly Dictionary<string, string> AllStrategies = new Dictionary<string, string>
        {
            { "default", "Default (Storyteller Choice)" },
            { "immediate", "Immediate Attack" },
            { "smart", "Smart Attack (Avoids Traps)" },
            { "sappers", "Sappers (Uses Explosives)" },
            { "breach", "Breach (Focuses on Walls)" },
            { "breachsmart", "Smart Breach" },
            { "stage", "Stage Then Attack" },
            { "siege", "Siege (Builds Mortars)" }
        };

        public override Vector2 InitialSize => new Vector2(400f, 500f);

        public Dialog_RaidStrategiesEditor(CommandSettings settings)
        {
            this.settings = settings;
            doCloseButton = true;
            forcePause = true;
            absorbInputAroundWindow = true;
            // optionalTitle = "Configure Raid Strategies";
        }

        public override void DoWindowContents(Rect inRect)
        {

            // Make OptionalTitle the Header.
            // GUI.color = ColorLibrary.HeaderAccent;

            // Header
            Text.Font = GameFont.Medium;
            GUI.color = ColorLibrary.HeaderAccent;
            Widgets.Label(new Rect(0f, 0f, inRect.width, 30f), "Allowed Raid Strategies");
            Text.Font = GameFont.Small;
            GUI.color = Color.white;

            // Description
            Widgets.Label(new Rect(0f, 35f, inRect.width, 40f),
                "Check which raid strategies viewers can use. Unchecked strategies will be disabled.");

            // Quick action buttons
            Rect quickActionsRect = new Rect(0f, 80f, inRect.width, 30f);
            DrawQuickActions(quickActionsRect);

            // List of strategies
            Rect listRect = new Rect(0f, 115f, inRect.width, inRect.height - 115f - CloseButSize.y);
            float itemHeight = 30f;
            Rect viewRect = new Rect(0f, 0f, listRect.width - 20f, AllStrategies.Count * itemHeight);

            Widgets.BeginScrollView(listRect, ref scrollPosition, viewRect);
            {
                float y = 0f;
                foreach (var strategy in AllStrategies)
                {
                    Rect itemRect = new Rect(10f, y, viewRect.width - 20f, itemHeight - 2f);

                    bool isAllowed = settings.AllowedRaidStrategies.Contains(strategy.Key);
                    bool newValue = isAllowed;
                    Widgets.CheckboxLabeled(itemRect, strategy.Value, ref newValue);

                    if (newValue != isAllowed)
                    {
                        if (newValue)
                            settings.AllowedRaidStrategies.Add(strategy.Key);
                        else
                            settings.AllowedRaidStrategies.Remove(strategy.Key);
                    }

                    y += itemHeight;
                }
            }
            Widgets.EndScrollView();
        }

        private void DrawQuickActions(Rect rect)
        {
            Widgets.BeginGroup(rect);

            float buttonWidth = 120f;
            float spacing = 5f;
            float x = 0f;

            // Select All button
            if (Widgets.ButtonText(new Rect(x, 0f, buttonWidth, 30f), "Select All"))
            {
                settings.AllowedRaidStrategies.Clear();
                foreach (var strategy in AllStrategies.Keys)
                {
                    settings.AllowedRaidStrategies.Add(strategy);
                }
            }
            x += buttonWidth + spacing;

            // Select None button
            if (Widgets.ButtonText(new Rect(x, 0f, buttonWidth, 30f), "Select None"))
            {
                settings.AllowedRaidStrategies.Clear();
            }
            x += buttonWidth + spacing;

            // Select Basic button
            if (Widgets.ButtonText(new Rect(x, 0f, buttonWidth, 30f), "Basic Only"))
            {
                settings.AllowedRaidStrategies.Clear();
                settings.AllowedRaidStrategies.Add("default");
                settings.AllowedRaidStrategies.Add("immediate");
                settings.AllowedRaidStrategies.Add("smart");
            }

            Widgets.EndGroup();
        }
    }
}