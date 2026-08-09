// WeatherCommandHandler.cs
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
//
// !weather [type|list|listN] — purchase weather / game-condition changes
using CAP_ChatInteractive.Commands.Cooldowns;
using CAP_ChatInteractive.Incidents;
using CAP_ChatInteractive.Incidents.Weather;
using LudeonTK;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace CAP_ChatInteractive.Commands.CommandHandlers
{
    public static class WeatherCommandHandler
    {
        private const string ReturnDivider = " | ";

        public static string HandleWeatherCommand(ChatMessageWrapper user, string weatherType)
        {
            try
            {
                weatherType = weatherType?.Trim() ?? string.Empty;
                if (string.IsNullOrEmpty(weatherType))
                    return "RICS.WCH.ListCommandHint".Translate();

                var settings = CAPChatInteractiveMod.Instance?.Settings?.GlobalSettings;
                if (settings == null)
                    return "RICS.WCH.ViewerNotFound".Translate();

                string currencySymbol = settings.CurrencyName?.Trim() ?? "¢";

                if (weatherType.Equals("list", StringComparison.OrdinalIgnoreCase))
                    return GetWeatherList();

                if (weatherType.StartsWith("list", StringComparison.OrdinalIgnoreCase))
                {
                    if (int.TryParse(weatherType.Substring(4), out int page) && page > 0)
                        return GetWeatherListPage(page);
                    return GetWeatherList();
                }

                var viewer = Viewers.GetViewer(user);
                if (viewer == null)
                    return "RICS.WCH.ViewerNotFound".Translate();

                // Search all known weather (including disabled) so disabled → clear message, not "unknown"
                var buyableWeather = FindBuyableWeather(weatherType, enabledOnly: false);
                if (buyableWeather == null)
                {
                    var availableTypes = GetEnabledWeather()
                        .Take(8)
                        .Select(w => w.Label);
                    return "RICS.WCH.UnknownWeather".Translate(weatherType, string.Join(", ", availableTypes));
                }

                if (!buyableWeather.Enabled)
                    return "RICS.WCH.WeatherDisabled".Translate(buyableWeather.Label);

                var cooldownManager = Current.Game?.GetComponent<GlobalCooldownManager>();
                if (cooldownManager != null)
                {
                    var commandSettings = CommandSettingsManager.GetSettings("weather");
                    if (!cooldownManager.CanUseCommand("weather", commandSettings, settings))
                    {
                        if (!cooldownManager.CanUseGlobalEvents(settings))
                        {
                            int totalEvents = SumEventUses(cooldownManager);
                            return "RICS.WCH.GlobalEventLimitReached".Translate(totalEvents, settings.EventsperCooldown);
                        }

                        if (settings.KarmaTypeLimitsEnabled)
                        {
                            string eventType = GetKarmaTypeForWeather(buyableWeather.KarmaType);
                            if (!cooldownManager.CanUseEvent(eventType, settings))
                            {
                                int used = GetEventUses(cooldownManager, eventType);
                                int max = eventType switch
                                {
                                    "good" => settings.MaxGoodEvents,
                                    "bad" => settings.MaxBadEvents,
                                    "neutral" => settings.MaxNeutralEvents,
                                    "doom" => settings.MaxBadEvents,
                                    _ => 10
                                };
                                return "RICS.WCH.KarmaTypeLimitReached".Translate(eventType.ToUpper(), used, max);
                            }
                        }

                        return "RICS.WCH.CommandOnCooldown".Translate();
                    }
                }

                int cost = buyableWeather.BaseCost;
                if (viewer.Coins < cost)
                    return "RICS.WCH.InsufficientFunds".Translate(cost, currencySymbol, buyableWeather.Label);

                bool isGameCondition = IsGameConditionWeather(buyableWeather.DefName);
                bool success = isGameCondition
                    ? TriggerGameConditionWeather(buyableWeather, out string resultMessage)
                    : TriggerSimpleWeather(buyableWeather, out resultMessage);

                if (!success)
                {
                    return resultMessage
                           + ReturnDivider
                           + "RICS.WCH.NoCoinsDeducted".Translate(currencySymbol);
                }

                viewer.TakeCoins(cost);

                float karmaChange = CalculateEventKarmaChange(
                    buyableWeather.KarmaType,
                    buyableWeather.BaseCost,
                    settings);

                string karmaType = buyableWeather.KarmaType?.ToLowerInvariant() ?? "neutral";
                if (karmaChange > 0f)
                {
                    if (karmaType == "good" || karmaType == "neutral")
                        viewer.GiveKarma(karmaChange);
                    else
                        viewer.TakeKarma(karmaChange);
                }

                if (cooldownManager != null)
                {
                    string eventType = GetKarmaTypeForWeather(buyableWeather.KarmaType);
                    cooldownManager.RecordEventUse(eventType);
                }

                MessageHandler.SendBlueLetter(
                    "RICS.WCH.WeatherChangedTitle".Translate(),
                    "RICS.WCH.WeatherChangedBody".Translate(
                        user.Username,
                        buyableWeather.Label,
                        cost,
                        currencySymbol,
                        resultMessage));

                return resultMessage;
            }
            catch (Exception ex)
            {
                Logger.Error($"[Weather] Error in HandleWeatherCommand: {ex}");
                return "RICS.WCH.ViewerNotFound".Translate();
            }
        }

        private static float CalculateEventKarmaChange(string karmaType, int baseCost, CAPGlobalChatSettings settings)
        {
            if (settings == null)
            {
                return karmaType?.ToLowerInvariant() switch
                {
                    "good" or "neutral" => Mathf.Max(3f, baseCost / 300f),
                    "bad" or "doom" => Mathf.Max(8f, baseCost / 200f),
                    _ => 3f
                };
            }

            string typeLower = karmaType?.ToLowerInvariant() ?? "neutral";
            float total = typeLower switch
            {
                "good" => settings.KarmaGainPerGoodEvent + (baseCost * settings.KarmaEventPriceMultiplier / 100f),
                "bad" => settings.KarmaLossPerBadEvent - (baseCost * settings.KarmaEventPriceMultiplier / 100f),
                "doom" => settings.KarmaLossPerDoomEvent - (baseCost * settings.KarmaEventPriceMultiplier / 100f),
                _ => settings.KarmaGainPerNeutralEvent + (baseCost * settings.KarmaEventPriceMultiplier / 100f),
            };

            return Mathf.Max(total, 1f);
        }

        private static string GetKarmaTypeForWeather(string karmaType)
        {
            if (string.IsNullOrEmpty(karmaType))
                return "neutral";

            return karmaType.ToLowerInvariant() switch
            {
                "good" => "good",
                "bad" => "bad",
                "doom" => "doom",
                _ => "neutral"
            };
        }

        private static BuyableWeather FindBuyableWeather(string input, bool enabledOnly)
        {
            if (string.IsNullOrEmpty(input))
                return null;

            IEnumerable<BuyableWeather> pool = BuyableWeatherManager.AllBuyableWeather.Values;
            if (enabledOnly)
                pool = pool.Where(w => w != null && w.Enabled);

            var list = pool.Where(w => w != null).ToList();
            string inputLower = input.ToLowerInvariant();

            var exactDef = list.FirstOrDefault(w =>
                w.DefName != null && w.DefName.Equals(input, StringComparison.OrdinalIgnoreCase));
            if (exactDef != null)
                return exactDef;

            var exactLabel = list.FirstOrDefault(w =>
                w.Label != null && w.Label.Equals(input, StringComparison.OrdinalIgnoreCase));
            if (exactLabel != null)
                return exactLabel;

            return list.FirstOrDefault(w =>
                (w.DefName != null && w.DefName.ToLowerInvariant().Contains(inputLower)) ||
                (w.Label != null && w.Label.ToLowerInvariant().Contains(inputLower)));
        }

        private static IEnumerable<BuyableWeather> GetEnabledWeather()
        {
            return BuyableWeatherManager.AllBuyableWeather.Values
                .Where(w => w != null && w.Enabled);
        }

        private static bool IsGameConditionWeather(string defName)
        {
            var incidentDef = DefDatabase<IncidentDef>.GetNamedSilentFail(defName);
            return incidentDef?.workerClass != null && incidentDef.Worker != null;
        }

        private static bool TriggerSimpleWeather(BuyableWeather weather, out string immersiveMessage)
        {
            immersiveMessage = string.Empty;

            var weatherDef = DefDatabase<WeatherDef>.GetNamedSilentFail(weather.DefName);
            if (weatherDef == null)
            {
                Logger.Error($"[Weather] WeatherDef not found: {weather.DefName}");
                immersiveMessage = "RICS.WCH.SimpleWeatherDefNotFound".Translate(weather.Label);
                return false;
            }

            var playerMaps = Current.Game?.Maps?.Where(map => map.IsPlayerHome).ToList()
                             ?? new List<Map>();

            var suitableMaps = playerMaps
                .Where(map => map.weatherManager?.curWeather != weatherDef)
                .ToList();

            if (!suitableMaps.Any())
            {
                immersiveMessage = "RICS.WCH.WeatherAlreadyActive".Translate(weather.Label);
                return false;
            }

            var targetMap = suitableMaps.RandomElement();
            if (!IsBiomeValidForWeather(targetMap))
            {
                immersiveMessage = "RICS.WCH.BiomeRestriction".Translate(GetBiomeRestrictionMessage(targetMap));
                return false;
            }

            var finalWeatherDef = GetTemperatureAdjustedWeather(weatherDef, targetMap, out string conversionMessage);
            targetMap.weatherManager.TransitionTo(finalWeatherDef);

            if (finalWeatherDef != weatherDef)
            {
                immersiveMessage = "RICS.WCH.WeatherConvertedByCold".Translate(
                    weather.Label,
                    finalWeatherDef.label,
                    conversionMessage);
            }
            else
            {
                immersiveMessage = "RICS.WCH.WeatherTransitionSuccess".Translate(weather.Label);
            }

            return true;
        }

        private static WeatherDef GetTemperatureAdjustedWeather(WeatherDef requestedWeather, Map map, out string conversionMessage)
        {
            conversionMessage = string.Empty;
            if (map?.mapTemperature == null)
                return requestedWeather;

            float currentTemp = map.mapTemperature.OutdoorTemp;
            string requestedName = requestedWeather.defName;

            if (currentTemp < 0f)
            {
                switch (requestedName)
                {
                    case "Rain":
                    {
                        var snowDef = DefDatabase<WeatherDef>.GetNamedSilentFail("SnowGentle");
                        if (snowDef != null)
                        {
                            conversionMessage = "RICS.WCH.ConvertRainToSnow".Translate();
                            return snowDef;
                        }
                        break;
                    }
                    case "RainyThunderstorm":
                    case "DryThunderstorm":
                    {
                        var thundersnowDef = DefDatabase<WeatherDef>.GetNamedSilentFail("SnowyThunderStorm");
                        if (thundersnowDef != null)
                        {
                            conversionMessage = "RICS.WCH.ConvertThunderToThundersnow".Translate();
                            return thundersnowDef;
                        }
                        break;
                    }
                }
            }
            else if (currentTemp > 5f)
            {
                switch (requestedName)
                {
                    case "SnowGentle":
                    {
                        var rainDef = DefDatabase<WeatherDef>.GetNamedSilentFail("Rain");
                        if (rainDef != null)
                        {
                            conversionMessage = "RICS.WCH.ConvertSnowGentleToRain".Translate();
                            return rainDef;
                        }
                        break;
                    }
                    case "SnowHard":
                    {
                        var snowGentleDef = DefDatabase<WeatherDef>.GetNamedSilentFail("SnowGentle");
                        if (snowGentleDef != null)
                        {
                            conversionMessage = "RICS.WCH.ConvertHeavySnowToLight".Translate();
                            return snowGentleDef;
                        }
                        break;
                    }
                }
            }

            return requestedWeather;
        }

        private static bool TriggerGameConditionWeather(BuyableWeather weather, out string immersiveMessage)
        {
            immersiveMessage = string.Empty;
            var incidentDef = DefDatabase<IncidentDef>.GetNamedSilentFail(weather.DefName);
            if (incidentDef == null)
            {
                Logger.Error($"[Weather] IncidentDef not found: {weather.DefName}");
                immersiveMessage = "RICS.WCH.GameConditionDefNotFound".Translate(weather.Label);
                return false;
            }

            if (incidentDef.workerClass == null)
            {
                immersiveMessage = "RICS.WCH.NoWorkerForIncident".Translate(weather.Label);
                return false;
            }

            var worker = incidentDef.Worker;
            if (worker == null)
            {
                Logger.Error($"[Weather] No worker for incident: {weather.DefName}");
                immersiveMessage = "RICS.WCH.NoWorkerForIncident".Translate(weather.Label);
                return false;
            }

            var playerMaps = Current.Game?.Maps?.Where(map => map.IsPlayerHome).ToList()
                             ?? new List<Map>();
            playerMaps.Shuffle();

            foreach (var map in playerMaps)
            {
                if (!IsBiomeValidForWeather(map))
                    continue;

                var parms = new IncidentParms
                {
                    target = map,
                    forced = true,
                    points = StorytellerUtility.DefaultThreatPointsNow(map)
                };

                if (worker.CanFireNow(parms) && !worker.FiredTooRecently(map))
                {
                    if (worker.TryExecute(parms))
                    {
                        immersiveMessage = GetGameConditionMessage(weather);
                        return true;
                    }
                }
            }

            if (playerMaps.Any(IsBiomeValidForWeather))
                immersiveMessage = "RICS.WCH.GameConditionCosmicAlignment".Translate(weather.Label);
            else
                immersiveMessage = "RICS.WCH.GameConditionNoSuitableLocation".Translate(weather.Label);

            return false;
        }

        private static string GetGameConditionMessage(BuyableWeather weather)
        {
            return weather.DefName switch
            {
                "SolarFlare" => "RICS.WCH.SolarFlareMessage".Translate(),
                "ToxicFallout" => "RICS.WCH.ToxicFalloutMessage".Translate(),
                "Flashstorm" => "RICS.WCH.FlashstormMessage".Translate(),
                "Eclipse" => "RICS.WCH.EclipseMessage".Translate(),
                "Aurora" => "RICS.WCH.AuroraMessage".Translate(),
                "HeatWave" => "RICS.WCH.HeatWaveMessage".Translate(),
                "ColdSnap" => "RICS.WCH.ColdSnapMessage".Translate(),
                "VolcanicWinter" => "RICS.WCH.VolcanicWinterMessage".Translate(),
                _ => "RICS.WCH.GenericGameConditionMessage".Translate(weather.Label)
            };
        }

        private static string GetWeatherList()
        {
            var settings = CAPChatInteractiveMod.Instance?.Settings?.GlobalSettings;
            string currencySymbol = settings?.CurrencyName?.Trim() ?? "¢";
            var cooldownManager = Current.Game?.GetComponent<GlobalCooldownManager>();

            var availableWeathers = GetEnabledWeather()
                .Where(w => !IsGameConditionWeather(w.DefName))
                .Select(w =>
                {
                    string status = "✅";
                    if (cooldownManager != null && settings != null && settings.KarmaTypeLimitsEnabled)
                    {
                        string eventType = GetKarmaTypeForWeather(w.KarmaType);
                        if (!cooldownManager.CanUseEvent(eventType, settings))
                            status = "❌";
                    }

                    return "RICS.WCH.WeatherListEntry".Translate(
                        w.Label,
                        w.BaseCost,
                        currencySymbol,
                        status).ToString();
                })
                .ToList();

            string entriesPart = string.Join(", ", availableWeathers.Take(8));
            string message = "RICS.WCH.WeatherListTitle".Translate() + " " + entriesPart;

            if (settings != null && settings.KarmaTypeLimitsEnabled && cooldownManager != null)
            {
                string cooldownSummary = GetCooldownSummary(settings, cooldownManager);
                if (!string.IsNullOrEmpty(cooldownSummary))
                    message += "RICS.WCH.WeatherListSeparator".Translate() + cooldownSummary;
            }

            if (availableWeathers.Count > 8)
                message += " " + "RICS.WCH.WeatherListTruncated".Translate("weather list2");

            return message;
        }

        private static string GetCooldownSummary(CAPGlobalChatSettings settings, GlobalCooldownManager cooldownManager)
        {
            var summaries = new List<string>();

            if (settings.EventCooldownsEnabled && settings.EventsperCooldown > 0)
            {
                int totalEvents = SumEventUses(cooldownManager);
                summaries.Add("RICS.WCH.CooldownTotal".Translate(totalEvents, settings.EventsperCooldown).ToString());
            }

            if (settings.KarmaTypeLimitsEnabled)
            {
                if (settings.MaxGoodEvents > 0)
                {
                    summaries.Add("RICS.WCH.CooldownGood".Translate(
                        GetEventUses(cooldownManager, "good"),
                        settings.MaxGoodEvents).ToString());
                }

                if (settings.MaxBadEvents > 0)
                {
                    summaries.Add("RICS.WCH.CooldownBad".Translate(
                        GetEventUses(cooldownManager, "bad"),
                        settings.MaxBadEvents).ToString());
                }

                if (settings.MaxNeutralEvents > 0)
                {
                    summaries.Add("RICS.WCH.CooldownNeutral".Translate(
                        GetEventUses(cooldownManager, "neutral"),
                        settings.MaxNeutralEvents).ToString());
                }
            }

            if (summaries.Count == 0)
                return string.Empty;

            return string.Join("RICS.WCH.CooldownSeparator".Translate(), summaries);
        }

        private static string GetWeatherListPage(int page)
        {
            var settings = CAPChatInteractiveMod.Instance?.Settings?.GlobalSettings;
            string currencySymbol = settings?.CurrencyName?.Trim() ?? "¢";

            var availableWeathers = GetEnabledWeather()
                .Where(w => !IsGameConditionWeather(w.DefName))
                .Select(w => "RICS.WCH.WeatherListEntry".Translate(
                    w.Label,
                    w.BaseCost,
                    currencySymbol,
                    string.Empty).ToString())
                .ToList();

            const int itemsPerPage = 8;
            int startIndex = (page - 1) * itemsPerPage;
            if (startIndex >= availableWeathers.Count)
                return "RICS.WCH.WeatherListPageNoMore".Translate();

            var pageItems = availableWeathers.Skip(startIndex).Take(itemsPerPage);
            return "RICS.WCH.WeatherListPageTitle".Translate(page) + " " + string.Join(", ", pageItems);
        }

        private static bool IsBiomeValidForWeather(Map map)
        {
            if (map?.Biome == null)
                return false;

            string biomeDefName = map.Biome.defName ?? string.Empty;
            return !(biomeDefName.IndexOf("Underground", StringComparison.OrdinalIgnoreCase) >= 0 ||
                     biomeDefName.IndexOf("Space", StringComparison.OrdinalIgnoreCase) >= 0 ||
                     biomeDefName.IndexOf("Orbit", StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static string GetBiomeRestrictionMessage(Map map)
        {
            string biomeName = map?.Biome?.label ?? "this location";
            return "RICS.WCH.BiomeRestrictionError".Translate(biomeName);
        }

        private static int SumEventUses(GlobalCooldownManager cooldownManager)
        {
            if (cooldownManager?.data?.EventUsage == null)
                return 0;

            int total = 0;
            foreach (var record in cooldownManager.data.EventUsage.Values)
            {
                if (record != null)
                    total += record.CurrentPeriodUses;
            }

            return total;
        }

        private static int GetEventUses(GlobalCooldownManager cooldownManager, string eventType)
        {
            if (cooldownManager?.data?.EventUsage == null || string.IsNullOrEmpty(eventType))
                return 0;

            if (cooldownManager.data.EventUsage.TryGetValue(eventType, out var record) && record != null)
                return record.CurrentPeriodUses;

            return 0;
        }

        [DebugAction("CAP", "Test Weather Conversion", allowedGameStates = AllowedGameStates.Playing)]
        public static void DebugTestWeatherConversion()
        {
            Map map = Find.CurrentMap;
            if (map == null)
                return;

            float temp = map.mapTemperature.OutdoorTemp;
            Logger.Message($"[Weather] Current temperature: {temp}°C");

            var testWeathers = new[] { "Rain", "RainyThunderstorm", "SnowGentle", "SnowHard" };
            foreach (var weatherName in testWeathers)
            {
                var weatherDef = DefDatabase<WeatherDef>.GetNamedSilentFail(weatherName);
                if (weatherDef == null)
                    continue;

                var finalWeather = GetTemperatureAdjustedWeather(weatherDef, map, out string message);
                if (finalWeather != weatherDef)
                    Logger.Message($"[Weather] {weatherName} → {finalWeather.defName}: {message}");
                else
                    Logger.Message($"[Weather] {weatherName}: No conversion needed");
            }
        }
    }
}
