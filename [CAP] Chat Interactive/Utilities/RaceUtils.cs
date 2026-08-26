// RaceUtils.cs - Replace the entire file with this fixed version
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
using CAP_ChatInteractive;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Verse;

namespace _CAP__Chat_Interactive.Utilities
{
    public static class RaceUtils
    {
        // List of race defNames to always exclude
        public static readonly HashSet<string> ExcludedRaces = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Corpse_Human",
            "UnnaturalCorpse_Human",
            "CreepJoiner"
        };

        // Keywords in defName or label that indicate we should exclude the race
        public static readonly HashSet<string> ExcludedKeywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "corpse",
            "dead",
            "ghoul",
            "zombie",
            "skeleton"
        };

        public static IEnumerable<ThingDef> GetAllHumanlikeRaces()
        {
            return DefDatabase<ThingDef>.AllDefs.Where(d =>
                d.race?.Humanlike ?? false &&
                !IsRaceExcluded(d));
        }

        public static ThingDef FindRaceByName(string raceName)
        {
            return GetAllHumanlikeRaces().FirstOrDefault(race =>
                race.defName.Equals(raceName, StringComparison.OrdinalIgnoreCase) ||
                race.label.Equals(raceName, StringComparison.OrdinalIgnoreCase));
        }

        public static List<ThingDef> GetEnabledRaces()
        {
            var allRaces = GetAllHumanlikeRaces();
            var enabledRaces = new List<ThingDef>();
            var raceSettings = RaceSettingsManager.RaceSettings; // Use centralized manager

            foreach (var race in allRaces)
            {
                // Race should only be in settings if it's not excluded, so we can just check enabled status
                if (raceSettings.ContainsKey(race.defName) && raceSettings[race.defName].Enabled)
                {
                    enabledRaces.Add(race);
                }
                // Note: We don't need the "else if not in settings" case anymore because
                // RaceSettingsManager ensures all non-excluded races are in settings
            }

            return enabledRaces;
        }

        /// <summary>
        /// Chat list of enabled races and base prices. Same look as !races.
        /// Does not include xenotypes.
        /// </summary>
        public static string FormatEnabledRacesPriceList(int maxEntries, out int shown, out int total)
        {
            shown = 0;
            total = 0;
            try
            {
                var enabledRaces = GetEnabledRaces()
                    .Where(r => r != null)
                    .OrderBy(r => r.LabelCap.RawText)
                    .ToList();

                total = enabledRaces.Count;
                if (total == 0)
                    return "RICS.LCH.NoRacesEnabled".Translate();

                var settings = CAPChatInteractiveMod.Instance?.Settings?.GlobalSettings;
                var currency = settings?.CurrencyName?.Trim() ?? "¢";
                var lines = new List<string>();

                foreach (var race in enabledRaces)
                {
                    var raceSettings = RaceSettingsManager.GetRaceSettings(race.defName);
                    if (raceSettings?.Enabled != true)
                        continue;

                    string name = race.LabelCap.RawText ?? race.defName;
                    if (!string.IsNullOrEmpty(name))
                        name = Regex.Replace(name, @"<[^>]+>", string.Empty).Trim();
                    lines.Add($"{name}: {raceSettings.BasePrice} {currency}");
                }

                if (lines.Count == 0)
                    return "RICS.LCH.NoRacesEnabled".Translate();

                shown = Math.Min(Math.Max(maxEntries, 0), lines.Count);
                string resultList = string.Join(" | ", lines.Take(shown));
                string header = "RICS.LCH.AllRaces".Translate();
                return $"🔍 {header}: {resultList}";
            }
            catch (Exception ex)
            {
                Logger.Error($"Error listing races: {ex}");
                shown = 0;
                total = 0;
                string msg = ex.Message != null && ex.Message.Length > 80
                    ? ex.Message.Substring(0, 80) + "..."
                    : ex.Message ?? "";
                return "RICS.LCH.ErrorListingRaces".Translate(msg);
            }
        }

        public static bool IsRaceEnabled(string raceDefName)
        {
            var settings = JsonFileManager.GetRaceSettings(raceDefName);
            return settings.Enabled;
        }

        public static bool IsRaceExcluded(ThingDef raceDef)
        {
            if (raceDef == null) return true;

            // Check explicit exclusion list
            if (ExcludedRaces.Contains(raceDef.defName))
            {
                //CAP_ChatInteractive.Logger.Debug($"Excluded race by defName: {raceDef.defName}");
                return true;
            }

            // Check for keywords in defName
            if (ExcludedKeywords.Any(keyword =>
                raceDef.defName.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0))
            {
                // CAP_ChatInteractive.Logger.Debug($"Excluded race by defName keyword: {raceDef.defName}");
                return true;
            }

            // Check for keywords in label
            if (ExcludedKeywords.Any(keyword =>
                raceDef.label.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0))
            {
                // CAP_ChatInteractive.Logger.Debug($"Excluded race by label keyword: {raceDef.defName} - {raceDef.label}");
                return true;
            }

            // Check if it's a corpse by checking the description or other properties
            if (IsCorpseRace(raceDef))
            {
                // CAP_ChatInteractive.Logger.Debug($"Excluded race as corpse: {raceDef.defName}");
                return true;
            }

            return false;
        }

        private static bool IsCorpseRace(ThingDef raceDef)
        {
            // Check if description indicates it's a corpse
            if (!string.IsNullOrEmpty(raceDef.description) &&
                raceDef.description.IndexOf("dead body", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;

            // Check if defName starts with "Corpse_"
            if (raceDef.defName.StartsWith("Corpse_", StringComparison.OrdinalIgnoreCase))
                return true;

            // Check if label contains "corpse"
            if (raceDef.label.IndexOf("corpse", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;

            return false;
        }

        // Method to get a list of all excluded races for debugging
        public static List<string> GetExcludedRaceList()
        {
            var allRaces = DefDatabase<ThingDef>.AllDefs.Where(d => d.race?.Humanlike ?? false);
            var excluded = allRaces.Where(IsRaceExcluded)
                                  .Select(r => $"{r.defName} - {r.label}")
                                  .ToList();
            return excluded;
        }

        // Method to get ALL humanlike races without filtering (for debug comparison)
        public static IEnumerable<ThingDef> GetAllHumanlikeRacesUnfiltered()
        {
            return DefDatabase<ThingDef>.AllDefs.Where(d => d.race?.Humanlike ?? false);
        }
    }

    // Add this to RaceUtils.cs or create a new GeneUtils.cs

}