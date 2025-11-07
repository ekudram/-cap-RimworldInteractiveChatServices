// RaceUtils.cs - Replace the entire file with this fixed version
// Copyright (c) Captolamia. All rights reserved.
// Licensed under the AGPLv3 License. See LICENSE file in the project root for full license information.
using CAP_ChatInteractive;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
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
            var raceSettings = JsonFileManager.LoadRaceSettings();

            foreach (var race in allRaces)
            {
                if (raceSettings.ContainsKey(race.defName) && raceSettings[race.defName].Enabled)
                {
                    enabledRaces.Add(race);
                }
                else if (!raceSettings.ContainsKey(race.defName))
                {
                    // New race not in settings - include it by default
                    enabledRaces.Add(race);
                }
            }

            return enabledRaces;
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
                CAP_ChatInteractive.Logger.Debug($"Excluded race by defName: {raceDef.defName}");
                return true;
            }

            // Check for keywords in defName
            if (ExcludedKeywords.Any(keyword =>
                raceDef.defName.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0))
            {
                CAP_ChatInteractive.Logger.Debug($"Excluded race by defName keyword: {raceDef.defName}");
                return true;
            }

            // Check for keywords in label
            if (ExcludedKeywords.Any(keyword =>
                raceDef.label.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0))
            {
                CAP_ChatInteractive.Logger.Debug($"Excluded race by label keyword: {raceDef.defName} - {raceDef.label}");
                return true;
            }

            // Check if it's a corpse by checking the description or other properties
            if (IsCorpseRace(raceDef))
            {
                CAP_ChatInteractive.Logger.Debug($"Excluded race as corpse: {raceDef.defName}");
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