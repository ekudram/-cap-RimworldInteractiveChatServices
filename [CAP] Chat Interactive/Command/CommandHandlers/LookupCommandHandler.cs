// LookupCommandHandler.cs
// Copyright (c) Captolamia. All rights reserved.
// Licensed under the AGPLv3 License. See LICENSE file in the project root for full license information.
//
// Handles the !lookup command to search across items, events, and weather
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
                    $"{r.Name} ({r.Type}): {r.Cost}{currencySymbol}"));

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
            return StoreInventory.GetEnabledItems()
                .Where(item => (item.CustomName?.ToLower().Contains(searchTerm) == true ||
                               GetItemDisplayName(item)?.ToLower().Contains(searchTerm) == true ||
                               item.DefName?.ToLower().Contains(searchTerm) == true))
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
            return IncidentsManager.AllBuyableIncidents.Values
                .Where(incident => incident.Enabled &&
                       (incident.Label?.ToLower().Contains(searchTerm) == true ||
                        incident.DefName?.ToLower().Contains(searchTerm) == true))
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
            return BuyableWeatherManager.AllBuyableWeather.Values
                .Where(w => w.Enabled &&
                       (w.Label?.ToLower().Contains(searchTerm) == true ||
                        w.DefName.ToLower().Contains(searchTerm)))
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
            return TraitsManager.GetEnabledTraits()
                .Where(trait => trait.Name?.ToLower().Contains(searchTerm) == true ||
                               trait.DefName?.ToLower().Contains(searchTerm) == true)
                .Take(maxResults)
                .Select(trait => new LookupResult
                {
                    Name = trait.Name,
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
}