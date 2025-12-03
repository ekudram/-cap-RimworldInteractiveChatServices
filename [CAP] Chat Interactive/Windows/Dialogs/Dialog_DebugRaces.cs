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
using _CAP__Chat_Interactive.Utilities;
using CAP_ChatInteractive;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

public class Dialog_DebugRaces : Window
{
    private Vector2 scrollPosition = Vector2.zero;
    private Vector2 excludedScrollPosition = Vector2.zero;
    private List<string> excludedRaces;
    private List<ThingDef> availableRaces;
    private List<ThingDef> allHumanlikeRaces;
    private int totalHumanlike;

    private CAPGlobalChatSettings settingsGlobalChat;
    private Dictionary<string, string> numericBuffers = new Dictionary<string, string>();


    public override Vector2 InitialSize => new Vector2(900f, 700f);

    public Dialog_DebugRaces()
    {
        doCloseButton = true;
        forcePause = true;
        absorbInputAroundWindow = true;
        optionalTitle = "Race Debug Information";

        // Gather data
        excludedRaces = RaceUtils.GetExcludedRaceList();
        availableRaces = RaceUtils.GetAllHumanlikeRaces().ToList();
        allHumanlikeRaces = RaceUtils.GetAllHumanlikeRacesUnfiltered().ToList();
        totalHumanlike = allHumanlikeRaces.Count;

        // Log for debugging
        CAP_ChatInteractive.Logger.Debug($"Debug Races - Total: {totalHumanlike}, Available: {availableRaces.Count}, Excluded: {excludedRaces.Count}");
        foreach (var race in availableRaces)
        {
            CAP_ChatInteractive.Logger.Debug($"Available: {race.defName}");
        }
        foreach (var excluded in excludedRaces)
        {
            CAP_ChatInteractive.Logger.Debug($"Excluded: {excluded}");
        }
    }

    public override void DoWindowContents(Rect inRect)
    {
        // Header with summary
        Rect headerRect = new Rect(0f, 0f, inRect.width, 60f);
        DrawHeader(headerRect);

        // Main content
        Rect contentRect = new Rect(0f, 65f, inRect.width, inRect.height - 65f - CloseButSize.y);
        DrawContent(contentRect);
    }

    private void DrawHeader(Rect rect)
    {
        Widgets.BeginGroup(rect);

        Text.Font = GameFont.Medium;
        string headerText = $"Race Debug - Total Humanlike: {totalHumanlike}, Available: {availableRaces.Count}, Excluded: {excludedRaces.Count}";
        Widgets.Label(new Rect(0f, 0f, rect.width, 25f), headerText);
        Text.Font = GameFont.Small;

        // Add explanation
        string explanation = "Green: Available, Red: Excluded, Yellow: In both lists (ERROR)";
        Widgets.Label(new Rect(0f, 30f, rect.width, 25f), explanation);

        Widgets.EndGroup();
    }

    private void DrawContent(Rect rect)
    {
        // Split into two columns
        float columnWidth = (rect.width - 10f) / 2f;

        Rect availableRect = new Rect(rect.x, rect.y, columnWidth, rect.height);
        Rect excludedRect = new Rect(rect.x + columnWidth + 10f, rect.y, columnWidth, rect.height);

        DrawAvailableRaces(availableRect);
        DrawExcludedRaces(excludedRect);
    }

    private void DrawAvailableRaces(Rect rect)
    {
        Widgets.DrawMenuSection(rect);

        // Header
        Rect headerRect = new Rect(rect.x, rect.y, rect.width, 30f);
        Text.Font = GameFont.Medium;
        Text.Anchor = TextAnchor.MiddleCenter;
        Widgets.Label(headerRect, $"Available Races ({availableRaces.Count})");
        Text.Anchor = TextAnchor.UpperLeft;
        Text.Font = GameFont.Small;

        // List
        Rect listRect = new Rect(rect.x, rect.y + 35f, rect.width, rect.height - 35f);
        float rowHeight = 22f;
        Rect viewRect = new Rect(0f, 0f, listRect.width - 20f, availableRaces.Count * rowHeight);

        Widgets.BeginScrollView(listRect, ref scrollPosition, viewRect);
        {
            float y = 0f;
            foreach (var race in availableRaces)
            {
                Rect raceRect = new Rect(5f, y, viewRect.width - 10f, rowHeight - 2f);

                // Check if this race should actually be excluded (error case)
                bool shouldBeExcluded = RaceUtils.IsRaceExcluded(race);

                if (shouldBeExcluded)
                {
                    GUI.color = Color.yellow; // Yellow for error - should be excluded but isn't
                }
                else
                {
                    GUI.color = Color.green; // Green for properly available
                }

                string raceInfo = $"{race.defName} - {race.LabelCap}";
                if (race.modContentPack != null)
                {
                    raceInfo += $" [{race.modContentPack.Name}]";
                }

                Widgets.Label(raceRect, raceInfo);
                GUI.color = Color.white;

                y += rowHeight;
            }
        }
        Widgets.EndScrollView();
    }

    private void DrawExcludedRaces(Rect rect)
    {
        Widgets.DrawMenuSection(rect);

        // Header
        Rect headerRect = new Rect(rect.x, rect.y, rect.width, 30f);
        Text.Font = GameFont.Medium;
        Text.Anchor = TextAnchor.MiddleCenter;
        Widgets.Label(headerRect, $"Excluded Races ({excludedRaces.Count})");
        Text.Anchor = TextAnchor.UpperLeft;
        Text.Font = GameFont.Small;

        // List
        Rect listRect = new Rect(rect.x, rect.y + 35f, rect.width, rect.height - 35f);
        float rowHeight = 22f;
        Rect viewRect = new Rect(0f, 0f, listRect.width - 20f, excludedRaces.Count * rowHeight);

        Widgets.BeginScrollView(listRect, ref excludedScrollPosition, viewRect);
        {
            float y = 0f;
            foreach (var excluded in excludedRaces)
            {
                Rect excludedRect = new Rect(5f, y, viewRect.width - 10f, rowHeight - 2f);

                GUI.color = Color.red; // Red for excluded
                Widgets.Label(excludedRect, excluded);
                GUI.color = Color.white;

                y += rowHeight;
            }
        }
        Widgets.EndScrollView();
    }
}