// RaceSettingsManager.cs
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
using System.IO;
using System.Collections.Generic;
using System.Linq;
using Verse;
using LudeonTK;

namespace _CAP__Chat_Interactive.Utilities
{
    public class RaceSettings
    {
        public string DisplayName { get; set; } = string.Empty;
        public bool Enabled { get; set; } = true;
        public bool ModActive { get; set; } = true;  // This can be used to track if the mod that added
        public int BasePrice { get; set; } = 1000;
        public int MinAge { get; set; } = 16;
        public int MaxAge { get; set; } = 65;
        public bool AllowCustomXenotypes { get; set; } = true;
        public string DefaultXenotype { get; set; } = "Baseliner";
        public string PreferredPawnKindDefName { get; set; } = null;   // null = use auto-detection
        public AllowedGenders AllowedGenders { get; set; } = new AllowedGenders();
        public Dictionary<string, float> XenotypePrices { get; set; } = new Dictionary<string, float>();
        public Dictionary<string, bool> EnabledXenotypes { get; set; } = new Dictionary<string, bool>();
    }

    public class AllowedGenders
    {
        public bool AllowMale { get; set; } = true;
        public bool AllowFemale { get; set; } = true;
        public bool AllowOther { get; set; } = true;
    }

    public enum PawnSortMethod
    {
        Name,
        Category,
        Status
    }

    public static class RaceSettingsManager
    {
        // This dictionary loads the JSON file to check against what actually exists in the game.  
        public static Dictionary<string, RaceSettings> _raceSettings;  
        private static bool _isInitialized = false;
        // This is the point of truth
        public static Dictionary<string, RaceSettings> RaceSettings
        {
            get
            {
                Logger.Debug($"RaceSettings getter called - _isInitialized: {_isInitialized}");
                if (!_isInitialized)
                {
                    Logger.Debug("=== LOADING AND INITIALIZING RACE SETTINGS ===");
                    Logger.Debug($"AlienProvider available: {CAPChatInteractiveMod.Instance?.AlienProvider != null}");
                    LoadAndInitializeSettings();
                    Logger.Debug("=== FINISHED LOADING RACE SETTINGS ===");
                }
                return _raceSettings;
            }
        }

        public static void LoadAndInitializeSettings()
        {
            // Load from JSON file
            _raceSettings = JsonFileManager.LoadRaceSettings();

            // First pass: Mark all existing settings as inactive initially
            foreach (var key in _raceSettings.Keys.ToList())
            {
                _raceSettings[key].ModActive = false;
            }

            // Initialize defaults for any missing NON-EXCLUDED races AND update existing ones
            foreach (var race in RaceUtils.GetAllHumanlikeRaces())
            {
                // Skip excluded races entirely - don't even add them to settings
                if (RaceUtils.IsRaceExcluded(race))
                {
                    Logger.Debug($"Skipping excluded race in settings: {race.defName}");
                    continue;
                }

                if (!_raceSettings.ContainsKey(race.defName))
                {
                    // New race - create default settings
                    _raceSettings[race.defName] = CreateDefaultSettings(race);
                }
                else
                {
                    // Existing race - mark as active
                    _raceSettings[race.defName].ModActive = true;
                }
            }

            // Remove any excluded races that might have been in the saved settings
            var excludedRaces = _raceSettings.Keys.Where(key =>
            {
                var raceDef = DefDatabase<ThingDef>.AllDefs.FirstOrDefault(d => d.defName == key);
                return raceDef != null && RaceUtils.IsRaceExcluded(raceDef);
            }).ToList();

            foreach (var excludedKey in excludedRaces)
            {
                Logger.Debug($"Removing excluded race from settings: {excludedKey}");
                _raceSettings.Remove(excludedKey);
            }

            _isInitialized = true;

            // Save the cleaned-up and updated settings
            SaveSettings();
        }

        public static void SaveSettings()
        {
            JsonFileManager.SaveRaceSettings(_raceSettings);
        }

        public static RaceSettings GetRaceSettings(string raceDefName)
        {
            if (RaceSettings.TryGetValue(raceDefName, out var settings))
            {
                // If race is from an inactive mod, still return the settings but with Enabled=false
                if (!settings.ModActive)
                {
                    var disabledSettings = new RaceSettings
                    {
                        DisplayName = settings.DisplayName,
                        Enabled = false,
                        ModActive = false
                        // Keep other settings for web UI but disable in-game
                    };
                    return disabledSettings;
                }
                return settings;
            }
            //if (RaceSettings.TryGetValue(raceDefName, out var settings))
            //{
            //    return settings;
            //}

            // If race is not in settings but is a valid non-excluded race, create default settings
            var raceDef = RaceUtils.FindRaceByName(raceDefName);
            if (raceDef != null && !RaceUtils.IsRaceExcluded(raceDef))
            {
                var defaultSettings = CreateDefaultSettings(raceDef);
                _raceSettings[raceDef.defName] = defaultSettings;
                SaveSettings();
                return defaultSettings;
            }

            // Return disabled settings for excluded or invalid races
            return new RaceSettings { Enabled = false };
        }

        private static RaceSettings CreateDefaultSettings(ThingDef race)
        {
            string displayName = !string.IsNullOrEmpty(race.label) ? race.label.CapitalizeFirst() : race.defName;

            var settings = new RaceSettings
            {
                DisplayName = race.label ?? race.defName,
                Enabled = true,
                ModActive = true,
                BasePrice = CalculateDefaultPrice(race),  // This is race.BaseMarketValue
                MinAge = 16,
                MaxAge = 65,
                AllowCustomXenotypes = true,
                DefaultXenotype = "Baseliner",
                AllowedGenders = GetAllowedGendersFromRace(race),
                XenotypePrices = new Dictionary<string, float>(),  // Will store actual prices, not multipliers
                EnabledXenotypes = new Dictionary<string, bool>()
            };

            // Initialize default xenotype settings if Biotech is active
            if (ModsConfig.BiotechActive)
            {
                // Get ALL xenotypes
                var allXenotypes = DefDatabase<XenotypeDef>.AllDefs
                    .Where(x => !string.IsNullOrEmpty(x.defName))
                    .Select(x => x.defName)
                    .ToList();

                // Get allowed xenotypes from HAR for this specific race
                var allowedXenotypes = GetAllowedXenotypes(race);
                Logger.Debug($"HAR allowed xenotypes for {race.defName}: {string.Join(", ", allowedXenotypes)}");

                bool isHuman = race == ThingDefOf.Human;
                Logger.Debug($"Is human: {isHuman}, Allowed xenotypes count: {allowedXenotypes.Count}");
                Logger.Debug($"Initializing xenotypes for {race.defName}: {allowedXenotypes.Count} allowed xenotypes");

                foreach (var xenotype in allXenotypes)
                {
                    bool defaultEnabled = false; // Always start with false
                    Logger.Debug($"Xenotype: {xenotype}");

                    if (isHuman)
                    {
                        // For humans, only enable base game xenotypes
                        defaultEnabled = IsBaseGameXenotype(xenotype);
                    }
                    else if (allowedXenotypes.Count > 0)
                    {
                        // For HAR races, use whiteXenotypeList to determine which xenotypes to enable
                        defaultEnabled = allowedXenotypes.Contains(xenotype);

                        Logger.Debug($"Race: {race.defName} Xeno: {xenotype} - AllowedListCount: {allowedXenotypes.Count}, InList: {allowedXenotypes.Contains(xenotype)}, Enabled: {defaultEnabled}");

                        // If this xenotype matches the race name, set it as the default
                        if (defaultEnabled && xenotype.Equals(race.defName, StringComparison.OrdinalIgnoreCase))
                        {
                            settings.DefaultXenotype = xenotype;
                            Logger.Debug($"Default Xeno: {xenotype}");
                        }
                    }
                    else
                    {
                        Logger.Debug($"Race: {race.defName} - NO ALLOWED XENOTYPES LIST, defaulting all to TRUE");
                        defaultEnabled = true;
                    }

                    settings.EnabledXenotypes[xenotype] = defaultEnabled;

                    // NEW: Calculate actual price instead of multiplier
                    settings.XenotypePrices[xenotype] = GetDefaultXenotypePrice(race, xenotype);

                    Logger.Debug($"  {xenotype}: {defaultEnabled} (allowed: {allowedXenotypes.Contains(xenotype)})");
                }
            }

            return settings;
        }

        private static int CalculateDefaultPrice(ThingDef race)
        {
            // Simply use Rimworld's base market value for the race
            if (race.BaseMarketValue > 0)
            {
                return (int)(race.BaseMarketValue);
            }
            return race == ThingDefOf.Human ? 1000 : 1500;
        }

        // NEW: Calculate actual xenotype price instead of multiplier
        private static float GetDefaultXenotypePrice(ThingDef race, string xenotypeName)
        {
            return GeneUtils.CalculateXenotypeMarketValue(race, xenotypeName);
        }

        // Helper method to identify base game xenotypes
        private static bool IsBaseGameXenotype(string xenotypeName)
        {
            var baseGameXenotypes = new HashSet<string>
    {
        "Baseliner", "Dirtmole", "Genie", "Hussar", "Sanguophage",
        "Neanderthal", "Pigskin", "Impid", "Waster", "Yttakin",
        "Highmate", "Starjack"
    };

            return baseGameXenotypes.Contains(xenotypeName);
        }

        private static List<string> GetAllowedXenotypes(ThingDef raceDef)
        {
            Logger.Debug($"=== GET ALLOWED XENOTYPES FOR {raceDef.defName} ===");
            Logger.Debug($"AlienProvider: {CAPChatInteractiveMod.Instance?.AlienProvider != null}");

            if (CAPChatInteractiveMod.Instance?.AlienProvider != null)
            {
                Logger.Debug("Calling AlienProvider.GetAllowedXenotypes...");
                var result = CAPChatInteractiveMod.Instance.AlienProvider.GetAllowedXenotypes(raceDef);
                Logger.Debug($"AlienProvider returned {result.Count} xenotypes");
                return result;
            }

            if (ModsConfig.BiotechActive)
            {
                Logger.Debug("Provider not available, returning all xenotypes");
                var allXenotypes = DefDatabase<XenotypeDef>.AllDefs.Select(x => x.defName).ToList();
                Logger.Debug($"Returning {allXenotypes.Count} total xenotypes");
                return allXenotypes;
            }

            Logger.Debug("Biotech not active, returning empty list");
            return new List<string>();
        }

        private static AllowedGenders GetAllowedGendersFromRace(ThingDef race)
        {
            var allowedGenders = new AllowedGenders();
            //Logger.Debug("=== START GENDER CHECK ===");

            if (CAPChatInteractiveMod.Instance?.AlienProvider != null)
            {
                Logger.Debug("=== Reached AlienProvider Gender Check");
                var alienProvider = CAPChatInteractiveMod.Instance.AlienProvider;

                try
                {
                    var inherentGenders = alienProvider.GetAllowedGenders(race);
                    var probabilities = alienProvider.GetGenderProbabilities(race);

                    Logger.Debug($"HAR gender info for {race.defName}: " +
                                 $"Inherent={inherentGenders}, MaleProb={probabilities.maleProbability}, FemaleProb={probabilities.femaleProbability}");

                    // Set allowed genders based on HAR restrictions
                    switch (inherentGenders)
                    {
                        case GenderPossibility.Male:
                            allowedGenders.AllowMale = true;
                            allowedGenders.AllowFemale = false;
                            allowedGenders.AllowOther = false;
                            break;
                        case GenderPossibility.Female:
                            allowedGenders.AllowMale = false;
                            allowedGenders.AllowFemale = true;
                            allowedGenders.AllowOther = false;
                            break;
                        case GenderPossibility.Either:
                        default:
                            allowedGenders.AllowMale = true;
                            allowedGenders.AllowFemale = true;
                            allowedGenders.AllowOther = true;
                            break;
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error($"Error getting gender restrictions for {race.defName}: {ex}");
                }
            }
            else
            {
                Logger.Debug($"No alien provider available for {race.defName}, using default gender settings");
            }

            Logger.Debug($"Final gender settings for {race.defName}: " +
                         $"Male={allowedGenders.AllowMale}, Female={allowedGenders.AllowFemale}, Other={allowedGenders.AllowOther}");
            Logger.Debug("=== END GENDER CHECK ===");
            return allowedGenders;
        }

        public static bool IsGenderAllowed(string raceDefName, Gender gender)
        {
            var settings = GetRaceSettings(raceDefName);
            if (settings == null) return false;

            return gender switch
            {
                Gender.Male => settings.AllowedGenders.AllowMale,
                Gender.Female => settings.AllowedGenders.AllowFemale,
                Gender.None => settings.AllowedGenders.AllowOther,
                _ => true
            };
        }

        // Eventually move DebugActions to a separate class, but for now it's convenient to have it here since it directly relates
        [DebugAction("CAP", "Delete RaceSettings & Rebuild", allowedGameStates = AllowedGameStates.Playing)]
        public static void DebugRebuildRaceSettings()
        {
            try
            {
                // Delete the RaceSettings.json file
                string filePath = JsonFileManager.GetFilePath("RaceSettings.json");
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                    Logger.Message("Deleted RaceSettings.json file");
                }
                else
                {
                    Logger.Message("No RaceSettings.json file found to delete");
                }

                // Reset initialization flags
                _isInitialized = false;
                _raceSettings = null;

                // Force reload and reinitialize everything
                LoadAndInitializeSettings();

                Logger.Message("Race settings completely rebuilt from scratch with current HAR detection");

                // Log results
                var settings = RaceSettings;
                Logger.Message($"Rebuilt settings for {settings.Count} races:");
                foreach (var kvp in settings)
                {
                    var raceSettings = kvp.Value;
                    Logger.Message($"  {kvp.Key}: Enabled={raceSettings.Enabled}, Genders(M={raceSettings.AllowedGenders.AllowMale}, F={raceSettings.AllowedGenders.AllowFemale}, O={raceSettings.AllowedGenders.AllowOther}), Xenotypes={raceSettings.EnabledXenotypes?.Count ?? 0}");
                }
                SaveSettings();
            }
            catch (Exception ex)
            {
                Logger.Error($"Error rebuilding race settings: {ex.Message}");
            }
        }
    }
}