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
// !lookup — search store items, events, weather, traits, races, xenotypes
using _CAP__Chat_Interactive.Command.CommandHelpers;
using _CAP__Chat_Interactive.Utilities;
using CAP_ChatInteractive.Incidents;
using CAP_ChatInteractive.Incidents.Weather;
using CAP_ChatInteractive.Store;
using CAP_ChatInteractive.Traits;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Verse;

namespace CAP_ChatInteractive.Commands.CommandHandlers
{
    public static class LookupCommandHandler
    {
        private const string ReturnDivider = " | ";

        public static string HandleLookupCommand(ChatMessageWrapper messageWrapper, string searchTerm, string searchType)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(searchTerm))
                    return "RICS.LCH.Usage".Translate();

                searchType = string.IsNullOrWhiteSpace(searchType) ? "all" : searchType.Trim().ToLowerInvariant();
                searchTerm = searchTerm.Trim();

                var settings = CAPChatInteractiveMod.Instance?.Settings?.GlobalSettings;
                var currencySymbol = settings?.CurrencyName?.Trim() ?? "¢";

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
                        return "RICS.LCH.NoResultsAll".Translate(searchTerm);
                    return "RICS.LCH.NoResults".Translate(searchType, searchTerm);
                }

                string displayCategory = searchType == "all"
                    ? "RICS.LCH.All".Translate()
                    : $"RICS.LCH.{searchType.CapitalizeFirst()}".Translate();

                string header = "🔍 " + "RICS.LCH.ResultsFor".Translate(displayCategory, searchTerm) + ": ";

                string body = string.Join(ReturnDivider, results.Select(r =>
                {
                    string displayType = r.Type == "RICS.LCH.Xenotype"
                        ? ""
                        : $" ({r.Type.Translate()})";

                    string emojiPrefix = r.ResearchStatusEmoji ?? "";
                    return $"{emojiPrefix}{TextUtilities.StripTags(r.Name)}{displayType}: {r.Cost} {currencySymbol}";
                }));

                return header + body;
            }
            catch (Exception ex)
            {
                Logger.Error($"[Lookup] Error in HandleLookupCommand: {ex}");
                return "RICS.LCH.ErrorGeneric".Translate();
            }
        }

        private static IEnumerable<LookupResult> SearchItems(string searchTerm, int maxResults)
        {
            var normalizedSearchTerm = searchTerm.ToLowerInvariant();

            return StoreInventory.GetEnabledItems()
                .Where(item =>
                {
                    string customName = TextUtilities.CleanAndNormalize(item.CustomName);
                    string displayName = TextUtilities.CleanAndNormalize(GetItemDisplayName(item));
                    string defName = item.DefName?.ToLowerInvariant() ?? "";

                    return customName.Contains(normalizedSearchTerm) ||
                           displayName.Contains(normalizedSearchTerm) ||
                           defName.Contains(normalizedSearchTerm);
                })
                .Take(maxResults)
                .Select(item =>
                {
                    var researchResult = StoreCommandHelper.HasRequiredResearch(item);
                    string researchEmoji = researchResult.Allowed ? "🔬✅" : "🔬🔒";

                    return new LookupResult
                    {
                        Name = item.CustomName ?? GetItemDisplayName(item) ?? item.DefName,
                        Type = "RICS.LCH.Item",
                        Cost = item.BasePrice,
                        DefName = item.DefName,
                        ResearchStatusEmoji = researchEmoji
                    };
                });
        }

        private static IEnumerable<LookupResult> SearchEvents(string searchTerm, int maxResults)
        {
            var normalizedSearchTerm = searchTerm.ToLowerInvariant();

            return IncidentsManager.AllBuyableIncidents.Values
                .Where(incident => incident.Enabled &&
                       (TextUtilities.CleanAndNormalize(incident.Label).Contains(normalizedSearchTerm) ||
                        (incident.DefName?.IndexOf(normalizedSearchTerm, StringComparison.OrdinalIgnoreCase) >= 0)))
                .Take(maxResults)
                .Select(incident => new LookupResult
                {
                    Name = incident.Label,
                    Type = "RICS.LCH.Event",
                    Cost = incident.BaseCost,
                    DefName = incident.DefName
                });
        }

        private static IEnumerable<LookupResult> SearchWeather(string searchTerm, int maxResults)
        {
            var normalizedSearchTerm = searchTerm.ToLowerInvariant();

            return BuyableWeatherManager.AllBuyableWeather.Values
                .Where(w => w.Enabled &&
                       (TextUtilities.CleanAndNormalize(w.Label).Contains(normalizedSearchTerm) ||
                        w.DefName.IndexOf(normalizedSearchTerm, StringComparison.OrdinalIgnoreCase) >= 0))
                .Take(maxResults)
                .Select(w => new LookupResult
                {
                    Name = w.Label,
                    Type = "RICS.LCH.Weather",
                    Cost = w.BaseCost,
                    DefName = w.DefName
                });
        }

        private static IEnumerable<LookupResult> SearchTraits(string searchTerm, int maxResults)
        {
            var normalizedSearchTerm = searchTerm.ToLowerInvariant();

            return TraitsManager.GetEnabledTraits()
                .Where(trait =>
                {
                    string cleanedName = TextUtilities.CleanAndNormalize(trait.Name);
                    string cleanedDefName = trait.DefName?.ToLowerInvariant() ?? "";
                    return cleanedName.Contains(normalizedSearchTerm) ||
                           cleanedDefName.Contains(normalizedSearchTerm);
                })
                .Take(maxResults)
                .Select(trait => new LookupResult
                {
                    Name = trait.Name,
                    Type = "RICS.LCH.Trait",
                    Cost = trait.AddPrice,
                    DefName = trait.DefName
                });
        }

        private static IEnumerable<LookupResult> SearchRaces(string searchTerm, int maxResults)
        {
            try
            {
                var normalizedSearchTerm = searchTerm.ToLowerInvariant();
                var enabledRaces = RaceUtils.GetEnabledRaces();

                return enabledRaces
                    .Where(race =>
                    {
                        if (race == null) return false;
                        string label = TextUtilities.CleanAndNormalize(race.LabelCap.RawText);
                        string defName = race.defName?.ToLowerInvariant() ?? "";
                        return label.Contains(normalizedSearchTerm) ||
                               defName.Contains(normalizedSearchTerm);
                    })
                    .Take(maxResults)
                    .Select(race =>
                    {
                        var settings = RaceSettingsManager.GetRaceSettings(race.defName);
                        if (settings == null)
                            return null;

                        string extra = GetXenotypesForRace(race.defName);
                        return new LookupResult
                        {
                            Name = race.LabelCap.RawText + extra,
                            Type = "RICS.LCH.Race",
                            Cost = settings.BasePrice,
                            DefName = race.defName
                        };
                    })
                    .Where(result => result != null);
            }
            catch (Exception ex)
            {
                Logger.Error($"[Lookup] Error in SearchRaces: {ex}");
                return Enumerable.Empty<LookupResult>();
            }
        }

        private static IEnumerable<LookupResult> SearchXenotypes(string searchTerm, int maxResults)
        {
            try
            {
                if (!ModsConfig.BiotechActive || string.IsNullOrWhiteSpace(searchTerm))
                    return Enumerable.Empty<LookupResult>();

                var normalizedSearchTerm = searchTerm.ToLowerInvariant().Trim();
                var enabledRaces = RaceUtils.GetEnabledRaces();

                // Race-first: matching a race lists its enabled xenotypes
                var matchingRace = enabledRaces.FirstOrDefault(race =>
                {
                    if (race == null) return false;
                    string label = TextUtilities.CleanAndNormalize(race.LabelCap.RawText);
                    string defName = race.defName?.ToLowerInvariant() ?? "";
                    return label.Contains(normalizedSearchTerm) ||
                           defName.Contains(normalizedSearchTerm);
                });

                if (matchingRace != null)
                {
                    var settings = RaceSettingsManager.GetRaceSettings(matchingRace.defName);
                    if (settings == null || !settings.Enabled || !settings.ModActive)
                        return Enumerable.Empty<LookupResult>();

                    var allowedDefNames = Dialog_PawnRaceSettings.GetAllowedXenotypes(matchingRace);
                    var enabledXenotypes = allowedDefNames
                        .Where(defName => settings.EnabledXenotypes.TryGetValue(defName, out bool isEnabled) && isEnabled)
                        .Select(defName => DefDatabase<XenotypeDef>.GetNamedSilentFail(defName))
                        .Where(x => x != null)
                        .OrderBy(x => x.label)
                        .ToList();

                    if (!enabledXenotypes.Any() && matchingRace == ThingDefOf.Human)
                    {
                        var baseliner = XenotypeDefOf.Baseliner;
                        if (baseliner != null)
                            enabledXenotypes.Add(baseliner);
                    }

                    return enabledXenotypes
                        .Take(maxResults)
                        .Select(x =>
                        {
                            float price = 0f;
                            if (settings.XenotypePrices.TryGetValue(x.defName, out float p))
                                price = p;

                            return new LookupResult
                            {
                                Name = x.LabelCap.RawText,
                                Type = "RICS.LCH.Xenotype",
                                Cost = (int)Math.Round(price),
                                DefName = x.defName
                            };
                        });
                }

                // Specific xenotype name search
                var matchingXenos = new List<XenotypeDef>();

                var exactLabelMatch = DefDatabase<XenotypeDef>.AllDefs
                    .FirstOrDefault(x => x.label != null &&
                                         x.label.Equals(searchTerm, StringComparison.OrdinalIgnoreCase));
                if (exactLabelMatch != null)
                    matchingXenos.Add(exactLabelMatch);

                var partialMatches = DefDatabase<XenotypeDef>.AllDefs
                    .Where(x => !string.IsNullOrEmpty(x.defName) &&
                                x != exactLabelMatch &&
                                (TextUtilities.CleanAndNormalize(x.label).Contains(normalizedSearchTerm) ||
                                 x.defName.IndexOf(normalizedSearchTerm, StringComparison.OrdinalIgnoreCase) >= 0))
                    .ToList();
                matchingXenos.AddRange(partialMatches);
                matchingXenos = matchingXenos.Distinct().ToList();

                if (!matchingXenos.Any())
                    return Enumerable.Empty<LookupResult>();

                var results = new List<LookupResult>();

                foreach (var xeno in matchingXenos.Take(maxResults))
                {
                    var compatibleRaces = enabledRaces
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
                            {
                                return settings.EnabledXenotypes == null ||
                                       !settings.EnabledXenotypes.ContainsKey("Baseliner") ||
                                       settings.EnabledXenotypes["Baseliner"];
                            }

                            return false;
                        })
                        .Select(r => r.LabelCap.RawText)
                        .OrderBy(name => name)
                        .ToList();

                    string compatInfo = compatibleRaces.Any()
                        ? "RICS.LCH.Compatible".Translate(
                            string.Join(", ", compatibleRaces.Take(5)) +
                            (compatibleRaces.Count > 5
                                ? "RICS.LCH.More".Translate(compatibleRaces.Count - 5)
                                : ""))
                        : "RICS.LCH.NoneCustomOnly".Translate();

                    string displayName = $"{xeno.LabelCap.RawText} ({compatInfo})";

                    float price = 0f;
                    if (compatibleRaces.Any())
                    {
                        string firstRaceName = compatibleRaces.First();
                        var firstRaceDef = enabledRaces
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
                        Cost = (int)Math.Round(price),
                        DefName = xeno.defName
                    });
                }

                return results;
            }
            catch (Exception ex)
            {
                Logger.Error($"[Lookup] Error in SearchXenotypes: {ex}");
                return Enumerable.Empty<LookupResult>();
            }
        }

        private static string GetXenotypesForRace(string raceDefName)
        {
            if (!ModsConfig.BiotechActive)
                return string.Empty;

            try
            {
                var race = DefDatabase<ThingDef>.GetNamedSilentFail(raceDefName);
                if (race == null || race.race?.Humanlike != true)
                    return string.Empty;

                var settings = RaceSettingsManager.GetRaceSettings(raceDefName);
                if (settings == null)
                    return string.Empty;

                var allowedDefNames = Dialog_PawnRaceSettings.GetAllowedXenotypes(race);
                var enabledXenoNames = new List<string>();

                foreach (var defName in allowedDefNames)
                {
                    if (settings.EnabledXenotypes.TryGetValue(defName, out bool isEnabled) && isEnabled)
                    {
                        var xenoDef = DefDatabase<XenotypeDef>.GetNamedSilentFail(defName);
                        enabledXenoNames.Add(xenoDef?.LabelCap.RawText ?? defName);
                    }
                }

                string xenoList;
                if (!enabledXenoNames.Any())
                {
                    if (!settings.AllowCustomXenotypes)
                        return string.Empty;
                    xenoList = "RICS.LCH.CustomOnly".Translate();
                }
                else
                {
                    enabledXenoNames.Sort();
                    if (enabledXenoNames.Count <= 3)
                        xenoList = string.Join(", ", enabledXenoNames);
                    else
                        xenoList = string.Join(", ", enabledXenoNames.Take(3)) +
                                   "RICS.LCH.More".Translate(enabledXenoNames.Count - 3);

                    if (settings.AllowCustomXenotypes)
                        xenoList += "RICS.LCH.PlusCustom".Translate();
                }

                return " (" + "RICS.LCH.XenotypesForRace".Translate(xenoList) + ")";
            }
            catch (Exception ex)
            {
                Logger.Error($"[Lookup] Error getting xenotypes for race {raceDefName}: {ex}");
                return string.Empty;
            }
        }

        private static string GetItemDisplayName(StoreItem storeItem)
        {
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

        /// <summary>🔬🔒 locked / 🔬✅ ready for items; empty for other categories.</summary>
        public string ResearchStatusEmoji { get; set; } = "";
    }

    public static class TextUtilities
    {
        public static string StripTags(string input)
        {
            if (string.IsNullOrEmpty(input))
                return input;

            return Regex.Replace(input, @"<[^>]+>", string.Empty).Trim();
        }

        public static string CleanAndNormalize(string input)
        {
            return StripTags(input)?.ToLowerInvariant() ?? "";
        }
    }
}
