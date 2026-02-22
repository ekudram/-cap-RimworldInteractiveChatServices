// LookupCommandHandler.cs
// Copyright (c) Captolamia
// This file is part of: RICS - Rimworld Interactive Chat Services
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
// Handles the !lookup command to search across items, events, and weather
using _CAP__Chat_Interactive.Utilities;
using CAP_ChatInteractive.Incidents;
using CAP_ChatInteractive.Incidents.Weather;
using CAP_ChatInteractive.Store;
using CAP_ChatInteractive.Traits;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace CAP_ChatInteractive.Commands.CommandHandlers
{
    public static class LookupCommandHandler
    {
        public static string HandleLookupCommand(ChatMessageWrapper messageWrapper, string searchTerm, string searchType)
        {
            try
            {
                var settings = CAPChatInteractiveMod.Instance.Settings.GlobalSettings;
                var currencySymbol = settings.CurrencyName?.Trim() ?? "¢";

                var results = new List<LookupResult>();

                switch (searchType)
                {
                    case "item":
                        results.AddRange(SearchItems(searchTerm, 8));
                        break;
                    case "event":
                        results.AddRange(SearchEvents(searchTerm, 8));
                        break;
                    case "weather":
                        results.AddRange(SearchWeather(searchTerm, 8));
                        break;
                    case "trait":
                        results.AddRange(SearchTraits(searchTerm, 8));
                        break;
                    case "race":
                        results.AddRange(SearchRaces(searchTerm, 8));
                        break;
                    case "xenotype":
                        results.AddRange(SearchXenotypes(searchTerm, 8));
                        break;
                    case "all":
                    default:
                        // Search all categories with limits (now includes new types)
                        results.AddRange(SearchItems(searchTerm, 3));
                        results.AddRange(SearchEvents(searchTerm, 2));
                        results.AddRange(SearchWeather(searchTerm, 2));
                        results.AddRange(SearchTraits(searchTerm, 1));
                        results.AddRange(SearchRaces(searchTerm, 1));
                        results.AddRange(SearchXenotypes(searchTerm, 1));
                        break;
                }

                if (!results.Any())
                {
                    if (searchType == "all")
                    {
                        return "RICS.LCH.NoResultsAll".Translate(searchTerm);
                    }
                    return "RICS.LCH.NoResults".Translate(searchType, searchTerm);
                }

                // var response = $"🔍 {searchType.ToUpper()} results for '{searchTerm}': ";
                // In HandleLookupCommand – replace the response-building block
                string displayCategory = searchType == "all"
                    ? "RICS.LCH.All".Translate()
                    : $"RICS.LCH.{searchType.CapitalizeFirst()}".Translate();

                var response = $"🔍 {"RICS.LCH.ResultsFor".Translate(displayCategory, searchTerm)}: ";
                response += string.Join(" | ", results.Select(r =>
                    $"{TextUtilities.StripTags(r.Name)} ({r.Type.Translate()}): {r.Cost} {currencySymbol}"));

                return response;
            }
            catch (Exception ex)
            {
                Logger.Error($"Error in HandleLookupCommand: {ex}");
                // return "Error searching. Please try again.";
                string exMessage = ex.Message.Length > 100 ? ex.Message.Substring(0, 100) + "..." : ex.Message;
                return "RICS.LCH.Error".Translate(exMessage); // for localization
            }
        }

        private static IEnumerable<LookupResult> SearchItems(string searchTerm, int maxResults)
        {
            var normalizedSearchTerm = searchTerm.ToLower();

            return StoreInventory.GetEnabledItems()
                .Where(item => {
                    string customName = TextUtilities.CleanAndNormalize(item.CustomName);
                    string displayName = TextUtilities.CleanAndNormalize(GetItemDisplayName(item));
                    string defName = item.DefName?.ToLower() ?? "";

                    return customName.Contains(normalizedSearchTerm) ||
                           displayName.Contains(normalizedSearchTerm) ||
                           defName.Contains(normalizedSearchTerm);
                })
                .Take(maxResults)
                .Select(item => new LookupResult
                {
                    Name = item.CustomName ?? GetItemDisplayName(item) ?? item.DefName,
                    // Type = "Item",
                    Type = "RICS.LCH.Item",
                    Cost = item.BasePrice,
                    DefName = item.DefName
                });
        }

        private static IEnumerable<LookupResult> SearchEvents(string searchTerm, int maxResults)
        {
            var normalizedSearchTerm = searchTerm.ToLower();

            return IncidentsManager.AllBuyableIncidents.Values
                .Where(incident => incident.Enabled &&
                       (TextUtilities.CleanAndNormalize(incident.Label).Contains(normalizedSearchTerm) ||
                        (incident.DefName?.ToLower().Contains(normalizedSearchTerm) == true)))
                .Take(maxResults)
                .Select(incident => new LookupResult
                {
                    Name = incident.Label,
                    // Type = "Event",
                    Type = "RICS.LCH.Event",
                    Cost = incident.BaseCost,
                    DefName = incident.DefName
                });
        }

        private static IEnumerable<LookupResult> SearchWeather(string searchTerm, int maxResults)
        {
            var normalizedSearchTerm = searchTerm.ToLower();

            return BuyableWeatherManager.AllBuyableWeather.Values
                .Where(w => w.Enabled &&
                       (TextUtilities.CleanAndNormalize(w.Label).Contains(normalizedSearchTerm) ||
                        w.DefName.ToLower().Contains(normalizedSearchTerm)))
                .Take(maxResults)
                .Select(w => new LookupResult
                {
                    Name = w.Label,
                    // Type = "Weather",
                    Type = "RICS.LCH.Weather",
                    Cost = w.BaseCost,
                    DefName = w.DefName
                });
        }

        private static IEnumerable<LookupResult> SearchTraits(string searchTerm, int maxResults)
        {
            var normalizedSearchTerm = searchTerm.ToLower();

            return TraitsManager.GetEnabledTraits()
                .Where(trait => {
                    // Clean the trait name for searching
                    string cleanedName = TextUtilities.CleanAndNormalize(trait.Name);
                    string cleanedDefName = trait.DefName?.ToLower() ?? "";

                    // Search in both cleaned name and defName
                    return cleanedName.Contains(normalizedSearchTerm) ||
                           cleanedDefName.Contains(normalizedSearchTerm);
                })
                .Take(maxResults)
                .Select(trait => new LookupResult
                {
                    Name = trait.Name, // Keep original name with colors for display
                    // Type = "Trait",
                    Type = "RICS.LCH.Trait",
                    Cost = trait.AddPrice,
                    DefName = trait.DefName
                });
        }

        private static IEnumerable<LookupResult> SearchRaces(string searchTerm, int maxResults)
        {
            try
            {
                var normalizedSearchTerm = searchTerm.ToLower();

                // Reuses exactly the same enabled-race source as BuyPawnCommandHandler.ListAvailableRaces
                var enabledRaces = RaceUtils.GetEnabledRaces();

                return enabledRaces
                    .Where(race =>
                    {
                        if (race == null) return false;
                        string label = TextUtilities.CleanAndNormalize(race.LabelCap.RawText);
                        string defName = race.defName?.ToLower() ?? "";
                        return label.Contains(normalizedSearchTerm) ||
                                defName.Contains(normalizedSearchTerm);
                    })
                    .Take(maxResults)
                    .Select(race =>
                    {
                        var settings = RaceSettingsManager.GetRaceSettings(race.defName);
                        if (settings == null) return null; // skip if null (rare)

                        string extra = GetXenotypesForRace(race.defName);

                        return new LookupResult
                        {
                            Name = race.LabelCap.RawText + extra,
                            Type = "RICS.LCH.Race",
                            Cost = settings.BasePrice,
                            DefName = race.defName
                        };
                    })
                    .Where(result => result != null);  // remove any nulls
            }
            catch (Exception ex)
            {
                Logger.Error($"Error in SearchRaces: {ex}");
                return Enumerable.Empty<LookupResult>();
            }
        }

        private static IEnumerable<LookupResult> SearchXenotypes(string searchTerm, int maxResults)
        {
            try
            {
                if (!ModsConfig.BiotechActive)
                    return Enumerable.Empty<LookupResult>();

                var normalizedSearchTerm = searchTerm.ToLower();

                // Get all xenotypes that match the search (same as before)
                var matchingXenos = DefDatabase<XenotypeDef>.AllDefs
                    .Where(x => !string.IsNullOrEmpty(x.defName) &&
                                (TextUtilities.CleanAndNormalize(x.label).Contains(normalizedSearchTerm) ||
                                 x.defName.ToLower().Contains(normalizedSearchTerm)))
                    .ToList();

                if (!matchingXenos.Any())
                    return Enumerable.Empty<LookupResult>();

                var results = new List<LookupResult>();

                // Reuse the same enabled races source as BuyPawn / ListAvailableRaces
                var enabledRaces = RaceUtils.GetEnabledRaces();

                foreach (var xeno in matchingXenos.Take(maxResults))
                {
                    // Find races that allow this xenotype (or allow custom xenotypes)
                    var compatibleRaces = enabledRaces
                        .Where(race =>
                        {
                            var settings = RaceSettingsManager.GetRaceSettings(race.defName);
                            if (settings == null) return false;

                            // Case 1: Explicitly enabled for this xenotype
                            if (settings.EnabledXenotypes?.ContainsKey(xeno.defName) == true &&
                                settings.EnabledXenotypes[xeno.defName])
                            {
                                return true;
                            }

                            // Case 2: Custom xenotypes allowed AND this isn't Baseliner (Baseliner usually always allowed)
                            if (settings.AllowCustomXenotypes && xeno != XenotypeDefOf.Baseliner)
                            {
                                return true;
                            }

                            // Baseliner fallback (almost always allowed unless explicitly disabled)
                            if (xeno == XenotypeDefOf.Baseliner)
                            {
                                return !settings.EnabledXenotypes?.ContainsKey("Baseliner") == true ||
                                       settings.EnabledXenotypes?["Baseliner"] != false;
                            }

                            return false;
                        })
                        .Select(r => r.LabelCap.RawText)
                        .OrderBy(name => name)
                        .ToList();

                    string compatInfo;
                    if (compatibleRaces.Any())
                    {
                        string listPart = string.Join(", ", compatibleRaces.Take(5));
                        if (compatibleRaces.Count > 5)
                        {
                            listPart += "RICS.LCH.More".Translate(compatibleRaces.Count - 5);
                        }
                        compatInfo = "RICS.LCH.Compatible".Translate(listPart);
                    }
                    else
                    {
                        compatInfo = "RICS.LCH.NoneCustomOnly".Translate();
                    }

                    string displayName = $"{xeno.LabelCap.RawText} ({compatInfo})";

                    results.Add(new LookupResult
                    {
                        Name = displayName,
                        Type = "RICS.LCH.Xenotype",
                        Cost = 0,
                        DefName = xeno.defName
                    });
                }

                return results;
            }
            catch (Exception ex)
            {
                Logger.Error($"Error in SearchXenotypes: {ex}");
                return Enumerable.Empty<LookupResult>();
            }
        }

        private static string GetXenotypesForRace(string raceDefName)
        {
            if (!ModsConfig.BiotechActive) return string.Empty;

            try
            {
                var race = DefDatabase<ThingDef>.GetNamedSilentFail(raceDefName);
                if (race == null || !race.race?.Humanlike == true) return string.Empty;

                var settings = RaceSettingsManager.GetRaceSettings(raceDefName);
                if (settings == null) return string.Empty;

                // Use EXACT same allowed pool as the settings GUI (HAR reflection + Biotech logic)
                var allowedDefNames = Dialog_PawnRaceSettings.GetAllowedXenotypes(race);

                var enabledXenoNames = new List<string>();

                foreach (var defName in allowedDefNames)
                {
                    if (settings.EnabledXenotypes.TryGetValue(defName, out bool isEnabled) && isEnabled)
                    {
                        var xenoDef = DefDatabase<XenotypeDef>.GetNamedSilentFail(defName);
                        string display = xenoDef?.LabelCap.RawText ?? defName;
                        enabledXenoNames.Add(display);
                    }
                }

                string xenoList;
                if (!enabledXenoNames.Any())
                {
                    if (settings.AllowCustomXenotypes)
                    {
                        xenoList = "RICS.LCH.CustomOnly".Translate();
                    }
                    else
                    {
                        return string.Empty;
                    }
                }
                else
                {
                    enabledXenoNames.Sort();

                    if (enabledXenoNames.Count <= 3)
                    {
                        xenoList = string.Join(", ", enabledXenoNames);
                    }
                    else
                    {
                        xenoList = string.Join(", ", enabledXenoNames.Take(3)) +
                                   "RICS.LCH.More".Translate(enabledXenoNames.Count - 3);
                    }

                    if (settings.AllowCustomXenotypes)
                    {
                        xenoList += "RICS.LCH.PlusCustom".Translate();
                    }
                }

                return " (" + "RICS.LCH.XenotypesForRace".Translate(xenoList) + ")";
            }
            catch (Exception ex)
            {
                Logger.Error($"Error getting xenotypes for race {raceDefName}: {ex}");
                return string.Empty; // silent – never break lookup
            }
        }

        private static string GetItemDisplayName(StoreItem storeItem)
        {
            // Get the display name from the ThingDef
            var thingDef = DefDatabase<ThingDef>.GetNamedSilentFail(storeItem.DefName);
            return thingDef?.label ?? storeItem.DefName;
        }
    }

    public class LookupResult
    {
        public string Name { get; set; }
        public string Type { get; set; }
        public int Cost { get; set; }
        public string DefName { get; set; }
    }

    // Add this static class for text utilities
    public static class TextUtilities
    {
        public static string StripTags(string input)
        {
            if (string.IsNullOrEmpty(input))
                return input;

            // Simple regex to remove HTML/XML tags
            return System.Text.RegularExpressions.Regex.Replace(
                input,
                @"<[^>]+>",
                string.Empty
            ).Trim();
        }

        public static string CleanAndNormalize(string input)
        {
            return StripTags(input)?.ToLower() ?? "";
        }
    }
}