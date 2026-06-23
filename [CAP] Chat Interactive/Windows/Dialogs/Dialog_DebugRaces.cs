// Dialog_DebugRaces.cs
// Copyright (c) Captolamia
// This file is part of CAP Chat Interactive (RICS).
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
// PURPOSE:
// Debug dialog to inspect which humanlike races are considered "available" for
// viewer pawn purchases vs which are excluded (corpses, creepjoiners, zombies, ghouls, etc.).
// Originally a dev tool to catch TTK-style corpse spawn bugs. Now useful for streamers
// to understand why certain modded races do not appear in the !pawn / buy list.
//
// This version is fully aligned with the current RaceUtils exclusion logic and
// RaceSettingsManager initialization (June 2026).

using _CAP__Chat_Interactive.Utilities;
using CAP_ChatInteractive;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using RimWorld;

public class Dialog_DebugRaces : Window
{
    private Vector2 scrollPosition = Vector2.zero;
    private Vector2 excludedScrollPosition = Vector2.zero;
    private List<string> excludedRacesWithReasons;
    private List<ThingDef> availableRaces;
    private List<ThingDef> allHumanlikeRaces;
    private Dictionary<string, RaceSettings> raceSettingsSnapshot;
    private int totalHumanlike;

    public override Vector2 InitialSize => new Vector2(1100f, 750f);

    public Dialog_DebugRaces()
    {
        doCloseButton = true;
        forcePause = true;
        absorbInputAroundWindow = true;
        optionalTitle = "RICS Race Debug - Available vs Excluded";

        // Gather current data from the single source of truth
        RefreshData();
    }

    private void RefreshData()
    {
        // Use the exact same filtering that the rest of RICS uses
        availableRaces = RaceUtils.GetAllHumanlikeRaces().ToList();
        allHumanlikeRaces = RaceUtils.GetAllHumanlikeRacesUnfiltered().ToList();
        totalHumanlike = allHumanlikeRaces.Count;

        // Build excluded list WITH reasons for streamer usefulness
        excludedRacesWithReasons = new List<string>();
        foreach (var race in allHumanlikeRaces)
        {
            if (RaceUtils.IsRaceExcluded(race))
            {
                string reason = GetExclusionReason(race);
                excludedRacesWithReasons.Add($"{race.defName} - {race.LabelCap}  |  {reason}");
            }
        }

        // Snapshot of current race settings for additional context in available list
        raceSettingsSnapshot = new Dictionary<string, RaceSettings>(RaceSettingsManager.RaceSettings);

        CAP_ChatInteractive.Logger.Debug($"[RaceDebug] Refresh: Total humanlike={totalHumanlike}, Available={availableRaces.Count}, Excluded={excludedRacesWithReasons.Count}");
    }

    /// <summary>
    /// Returns a human-readable reason why a race is excluded.
    /// Mirrors the logic in RaceUtils.IsRaceExcluded so the debug view stays in sync.
    /// </summary>
    private string GetExclusionReason(ThingDef raceDef)
    {
        if (raceDef == null) return "Null definition";

        // 1. Explicit list (highest priority)
        if (RaceUtils.ExcludedRaces.Contains(raceDef.defName))
            return "EXPLICIT: In ExcludedRaces set (Corpse_Human / UnnaturalCorpse_Human / CreepJoiner)";

        // 2. Keyword in defName
        foreach (var keyword in RaceUtils.ExcludedKeywords)
        {
            if (raceDef.defName.IndexOf(keyword, System.StringComparison.OrdinalIgnoreCase) >= 0)
                return $"KEYWORD in defName: '{keyword}'";
        }

        // 3. Keyword in label
        foreach (var keyword in RaceUtils.ExcludedKeywords)
        {
            if (raceDef.label != null && raceDef.label.IndexOf(keyword, System.StringComparison.OrdinalIgnoreCase) >= 0)
                return $"KEYWORD in label: '{keyword}'";
        }

        // 4. Corpse detection (description / prefix / label)
        if (!string.IsNullOrEmpty(raceDef.description) &&
            raceDef.description.IndexOf("dead body", System.StringComparison.OrdinalIgnoreCase) >= 0)
            return "CORPSE: description contains 'dead body'";

        if (raceDef.defName.StartsWith("Corpse_", System.StringComparison.OrdinalIgnoreCase))
            return "CORPSE: defName starts with 'Corpse_'";

        if (raceDef.label != null && raceDef.label.IndexOf("corpse", System.StringComparison.OrdinalIgnoreCase) >= 0)
            return "CORPSE: label contains 'corpse'";

        return "UNKNOWN REASON (check RaceUtils.IsRaceExcluded)";
    }

    public override void DoWindowContents(Rect inRect)
    {
        // Top bar with refresh
        Rect topBar = new Rect(0f, 0f, inRect.width, 32f);
        DrawTopBar(topBar);

        // Header summary
        Rect headerRect = new Rect(0f, 38f, inRect.width, 55f);
        DrawHeader(headerRect);

        // Main two-column content
        Rect contentRect = new Rect(0f, 100f, inRect.width, inRect.height - 100f - CloseButSize.y - 10f);
        DrawContent(contentRect);
    }

    private void DrawTopBar(Rect rect)
    {
        Widgets.BeginGroup(rect);

        if (Widgets.ButtonText(new Rect(10f, 4f, 120f, 24f), "Refresh Data"))
        {
            RefreshData();
            Messages.Message("Race debug data refreshed from current DefDatabase and RaceSettings.", MessageTypeDefOf.NeutralEvent);
        }

        Text.Anchor = TextAnchor.MiddleRight;
        Widgets.Label(new Rect(rect.width - 310f, 4f, 300f, 24f), 
            "Source of truth: RaceUtils.GetAllHumanlikeRaces() + RaceSettingsManager");
        Text.Anchor = TextAnchor.UpperLeft;

        Widgets.EndGroup();
    }

    private void DrawHeader(Rect rect)
    {
        Widgets.DrawMenuSection(rect);
        Widgets.BeginGroup(rect);

        Text.Font = GameFont.Medium;
        string headerText = $"Race Debug — Total Humanlike: {totalHumanlike}   |   Available (non-excluded): {availableRaces.Count}   |   Excluded: {excludedRacesWithReasons.Count}";
        Widgets.Label(new Rect(10f, 5f, rect.width - 20f, 25f), headerText);

        Text.Font = GameFont.Small;
        string explanation = "Green = Available & Enabled in settings    •    Yellow = Available but DISABLED in settings    •    Red = Explicitly excluded (corpses, creepjoiners, zombies, ghouls, etc.)";
        Widgets.Label(new Rect(10f, 30f, rect.width - 20f, 20f), explanation);

        Widgets.EndGroup();
    }

    private void DrawContent(Rect rect)
    {
        float columnWidth = (rect.width - 15f) / 2f;

        Rect availableRect = new Rect(rect.x, rect.y, columnWidth, rect.height);
        Rect excludedRect = new Rect(rect.x + columnWidth + 15f, rect.y, columnWidth, rect.height);

        DrawAvailableRaces(availableRect);
        DrawExcludedRaces(excludedRect);
    }

    private void DrawAvailableRaces(Rect rect)
    {
        Widgets.DrawMenuSection(rect);

        // Column header
        Rect headerRect = new Rect(rect.x, rect.y, rect.width, 28f);
        Text.Font = GameFont.Medium;
        Text.Anchor = TextAnchor.MiddleCenter;
        Widgets.Label(headerRect, $"AVAILABLE RACES (buyable / show in !races) — {availableRaces.Count}");
        Text.Anchor = TextAnchor.UpperLeft;
        Text.Font = GameFont.Small;

        Rect listRect = new Rect(rect.x, rect.y + 32f, rect.width, rect.height - 32f);
        float rowHeight = 28f;
        Rect viewRect = new Rect(0f, 0f, listRect.width - 20f, availableRaces.Count * rowHeight);

        Widgets.BeginScrollView(listRect, ref scrollPosition, viewRect);
        {
            float y = 0f;
            foreach (var race in availableRaces)
            {
                Rect rowRect = new Rect(8f, y, viewRect.width - 16f, rowHeight - 4f);

                // Get settings for this race (should always exist for non-excluded)
                RaceSettings settings = null;
                raceSettingsSnapshot.TryGetValue(race.defName, out settings);

                bool isEnabled = settings?.Enabled ?? false;

                // Color logic
                if (isEnabled)
                    GUI.color = Color.green;
                else
                    GUI.color = Color.yellow;   // In available list but disabled in settings

                // Build rich info line
                string modName = race.modContentPack?.Name ?? "Unknown";
                string priceInfo = settings != null ? $"Price:{settings.BasePrice}" : "NoSettings";
                string xenoInfo = "";
                if (settings != null && ModsConfig.BiotechActive)
                {
                    int enabledCount = settings.EnabledXenotypes.Count(kvp => kvp.Value);
                    int totalAllowed = Dialog_PawnRaceSettings.GetAllowedXenotypes(race).Count;
                    xenoInfo = $" Xeno:{enabledCount}/{totalAllowed}";
                }

                string raceInfo = $"{race.defName} — {race.LabelCap}  [{modName}]  |  {priceInfo}{xenoInfo}  |  Ages:{settings?.MinAge ?? 0}-{settings?.MaxAge ?? 0}";

                Widgets.Label(rowRect, raceInfo);
                GUI.color = Color.white;

                // Tooltip with extra useful info for streamers
                if (Mouse.IsOver(rowRect))
                {
                    string tooltip = $"defName: {race.defName}\n" +
                                     $"Label: {race.LabelCap}\n" +
                                     $"Mod: {modName}\n" +
                                     $"BaseMarketValue: {race.BaseMarketValue:F0}\n" +
                                     $"Enabled in settings: {(isEnabled ? "YES — viewers can buy" : "NO — hidden from !pawn / buy")}\n" +
                                     $"AllowCustomXenotypes: {settings?.AllowCustomXenotypes}\n" +
                                     $"Gender restrictions: Male={settings?.AllowedGenders.AllowMale}, Female={settings?.AllowedGenders.AllowFemale}, Other={settings?.AllowedGenders.AllowOther}\n\n" +
                                     $"This race passed RaceUtils.IsRaceExcluded() check and is present in RaceSettingsManager.";
                    TooltipHandler.TipRegion(rowRect, tooltip);
                }

                y += rowHeight;
            }
        }
        Widgets.EndScrollView();
    }

    private void DrawExcludedRaces(Rect rect)
    {
        Widgets.DrawMenuSection(rect);

        Rect headerRect = new Rect(rect.x, rect.y, rect.width, 28f);
        Text.Font = GameFont.Medium;
        Text.Anchor = TextAnchor.MiddleCenter;
        Widgets.Label(headerRect, $"EXCLUDED RACES (never buyable) — {excludedRacesWithReasons.Count}");
        Text.Anchor = TextAnchor.UpperLeft;
        Text.Font = GameFont.Small;

        Rect listRect = new Rect(rect.x, rect.y + 32f, rect.width, rect.height - 32f);
        float rowHeight = 26f;
        Rect viewRect = new Rect(0f, 0f, listRect.width - 20f, excludedRacesWithReasons.Count * rowHeight);

        Widgets.BeginScrollView(listRect, ref excludedScrollPosition, viewRect);
        {
            float y = 0f;
            foreach (var line in excludedRacesWithReasons)
            {
                Rect rowRect = new Rect(8f, y, viewRect.width - 16f, rowHeight - 2f);

                GUI.color = Color.red;
                Widgets.Label(rowRect, line);
                GUI.color = Color.white;

                if (Mouse.IsOver(rowRect))
                {
                    TooltipHandler.TipRegion(rowRect, "This race is filtered out by RaceUtils.IsRaceExcluded().\n" +
                                                      "Common reasons: corpse, zombie, ghoul, skeleton, CreepJoiner (Anomaly), or explicit block to prevent invalid pawn purchases.");
                }

                y += rowHeight;
            }
        }
        Widgets.EndScrollView();
    }
}
