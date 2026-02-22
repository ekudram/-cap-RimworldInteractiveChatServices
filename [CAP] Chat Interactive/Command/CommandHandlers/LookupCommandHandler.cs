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
using CAP_ChatInteractive.Incidents;
using CAP_ChatInteractive.Incidents.Weather;
using CAP_ChatInteractive.Store;
using CAP_ChatInteractive.Traits;
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
                    case "all":
                    default:
                        // Search all categories with limits
                        results.AddRange(SearchItems(searchTerm, 3));
                        results.AddRange(SearchEvents(searchTerm, 2));
                        results.AddRange(SearchWeather(searchTerm, 2));
                        results.AddRange(SearchTraits(searchTerm, 1));
                        break;
                }

                if (!results.Any())
                {
                    return $"No {searchType}s found matching '{searchTerm}'. Try a broader search term.";
                }

                var response = $"🔍 {searchType.ToUpper()} results for '{searchTerm}': ";
                response += string.Join(" | ", results.Select(r =>
                    $"{TextUtilities.StripTags(r.Name)} ({r.Type}): {r.Cost} {currencySymbol}"));

                return response;
            }
            catch (Exception ex)
            {
                Logger.Error($"Error in HandleLookupCommand: {ex}");
                return "Error searching. Please try again.";
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
                    Type = "Item",
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
                    Type = "Event",
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
                    Type = "Weather",
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
                    Type = "Trait",
                    Cost = trait.AddPrice,
                    DefName = trait.DefName
                });
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