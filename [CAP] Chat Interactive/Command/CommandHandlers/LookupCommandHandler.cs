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
                {
                    // For xenotypes: skip the redundant "(Xenotype)" type label
                    string displayType = r.Type == "RICS.LCH.Xenotype"
                        ? ""  // empty → no type shown
                        : $" ({r.Type.Translate()})";

                    return $"{TextUtilities.StripTags(r.Name)}{displayType}: {r.Cost} {currencySymbol}";
                }));

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

                if (string.IsNullOrWhiteSpace(searchTerm))
                    return Enumerable.Empty<LookupResult>();

                var normalizedSearchTerm = searchTerm.ToLower().Trim();

                // === NEW: Race-first mode ===
                // If the search term matches a race (e.g. "human", "kurin"), show that race's ENABLED xenotypes
                // This is the source of truth (RaceSettingsManager dictionary) – exactly what you asked for
                var enabledRaces = RaceUtils.GetEnabledRaces();
                var matchingRace = enabledRaces.FirstOrDefault(race =>
                {
                    if (race == null) return false;
                    string label = TextUtilities.CleanAndNormalize(race.LabelCap.RawText);
                    string defName = race.defName?.ToLower() ?? "";
                    return label.Contains(normalizedSearchTerm) ||
                           defName.Contains(normalizedSearchTerm);
                });

                if (matchingRace != null)
                {
                    var settings = RaceSettingsManager.GetRaceSettings(matchingRace.defName);
                    if (settings == null || !settings.Enabled || !settings.ModActive)
                        return Enumerable.Empty<LookupResult>();

                    // Reuse exact same allowed pool as GetXenotypesForRace + Dialog GUI (HAR + Biotech)
                    var allowedDefNames = Dialog_PawnRaceSettings.GetAllowedXenotypes(matchingRace);

                    var enabledXenotypes = allowedDefNames
                        .Where(defName => settings.EnabledXenotypes.TryGetValue(defName, out bool isEnabled) && isEnabled)
                        .Select(defName => DefDatabase<XenotypeDef>.GetNamedSilentFail(defName))
                        .Where(x => x != null)
                        .OrderBy(x => x.label)   // readable alphabetical order
                        .ToList();

                    // Human fallback: always show at least Baseliner if nothing enabled (consistent UX)
                    if (!enabledXenotypes.Any() && matchingRace == ThingDefOf.Human)
                    {
                        var baseliner = XenotypeDefOf.Baseliner;
                        if (baseliner != null)
                            enabledXenotypes.Add(baseliner);
                    }

                    var raceSettings = RaceSettingsManager.GetRaceSettings(matchingRace.defName);

                    return enabledXenotypes
                        .Take(maxResults)
                        .Select(x =>
                        {
                            float price = 0f;
                            if (raceSettings != null &&
                                raceSettings.XenotypePrices.TryGetValue(x.defName, out float p))
                            {
                                price = p;
                            }

                            return new LookupResult
                            {
                                Name = x.LabelCap.RawText,
                                Type = "RICS.LCH.Xenotype",
                                Cost = (int)Math.Round(price),
                                DefName = x.defName
                            };
                        });
                }

                // === Original specific xenotype search (unchanged) ===
                // Falls through only if no race matched (e.g. "hussar", "sanguophage")
                var matchingXenos = new List<XenotypeDef>();

                // First: exact label match (highest priority, supports spaces/multi-word)
                var exactLabelMatch = DefDatabase<XenotypeDef>.AllDefs
                    .FirstOrDefault(x => x.label.Equals(searchTerm, StringComparison.OrdinalIgnoreCase));

                if (exactLabelMatch != null)
                {
                    matchingXenos.Add(exactLabelMatch);
                }

                // Then: partial matches on label or defName (fallback)
                var partialMatches = DefDatabase<XenotypeDef>.AllDefs
                    .Where(x => !string.IsNullOrEmpty(x.defName) &&
                                (TextUtilities.CleanAndNormalize(x.label).Contains(normalizedSearchTerm) ||
                                 x.defName.ToLower().Contains(normalizedSearchTerm)) &&
                                x != exactLabelMatch)
                    .ToList();

                matchingXenos.AddRange(partialMatches);

                matchingXenos = matchingXenos.Distinct().ToList();

                if (!matchingXenos.Any())
                    return Enumerable.Empty<LookupResult>();

                var results = new List<LookupResult>();

                var enabledRacesForCompat = RaceUtils.GetEnabledRaces();  // reuse for compatible races

                foreach (var xeno in matchingXenos.Take(maxResults))
                {
                    var compatibleRaces = enabledRacesForCompat
                        .Where(race =>
                        {
                            var settings = RaceSettingsManager.GetRaceSettings(race.defName);
                            if (settings == null) return false;

                            string xenoKey = xeno.defName;

                            if (settings.EnabledXenotypes?.ContainsKey(xenoKey) == true)
                                return settings.EnabledXenotypes[xenoKey];

                            if (settings.AllowCustomXenotypes && xeno != XenotypeDefOf.Baseliner)
                                return true;

                            if (xeno == XenotypeDefOf.Baseliner)
                                return !settings.EnabledXenotypes?.ContainsKey("Baseliner") == true ||
                                       settings.EnabledXenotypes?["Baseliner"] != false;

                            return false;
                        })
                        .Select(r => r.LabelCap.RawText)
                        .OrderBy(name => name)
                        .ToList();

                    string compatInfo = compatibleRaces.Any()
                        ? "RICS.LCH.Compatible".Translate(
                            string.Join(", ", compatibleRaces.Take(5)) +
                            (compatibleRaces.Count > 5 ? "RICS.LCH.More".Translate(compatibleRaces.Count - 5) : ""))
                        : "RICS.LCH.NoneCustomOnly".Translate();

                    string displayName = $"{xeno.LabelCap.RawText} ({compatInfo})";

                    // Default to 0 if no price found (fallback)
                    float price = 0f;

                    // Try to get price from the first compatible race's settings (most relevant)
                    // Or fall back to a "global-ish" price if no compatible races
                    if (compatibleRaces.Any())
                    {
                        // Pick the first compatible race (ordered alphabetically → deterministic)
                        string firstRaceName = compatibleRaces.First();
                        var firstRaceDef = enabledRacesForCompat
                            .FirstOrDefault(r => r.LabelCap.RawText == firstRaceName);

                        if (firstRaceDef != null)
                        {
                            var raceSettings = RaceSettingsManager.GetRaceSettings(firstRaceDef.defName);
                            if (raceSettings != null &&
                                raceSettings.XenotypePrices.TryGetValue(xeno.defName, out float racePrice))
                            {
                                price = racePrice;
                            }
                        }
                    }
                    else
                    {
                        // No compatible races → try human as fallback base price (common default)
                        var humanSettings = RaceSettingsManager.GetRaceSettings(ThingDefOf.Human.defName);
                        if (humanSettings != null &&
                            humanSettings.XenotypePrices.TryGetValue(xeno.defName, out float humanPrice))
                        {
                            price = humanPrice;
                        }
                    }

                    results.Add(new LookupResult
                    {
                        Name = displayName,
                        Type = "RICS.LCH.Xenotype",
                        Cost = (int)Math.Round(price),  // assuming integer display like other items
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

        private static XenotypeDef ResolveXenotype(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return null;

            input = input.Trim();

            // Fast path: exact defName match (case-insensitive)
            var byDefName = DefDatabase<XenotypeDef>.GetNamedSilentFail(input);
            if (byDefName != null) return byDefName;

            // Label match (supports multi-word like "vampire nyaron")
            return DefDatabase<XenotypeDef>.AllDefs
                .FirstOrDefault(x => x.label.Equals(input, StringComparison.OrdinalIgnoreCase));
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