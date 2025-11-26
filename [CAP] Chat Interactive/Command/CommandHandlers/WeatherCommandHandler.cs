// WeatherCommandHandler.cs
// Copyright (c) Captolamia. All rights reserved.
// Licensed under the AGPLv3 License. See LICENSE file in the project root for full license information.
//
// Handles the !weather command to change in-game weather conditions via chat.
using CAP_ChatInteractive.Commands.Cooldowns;
using CAP_ChatInteractive.Incidents;
using CAP_ChatInteractive.Incidents.Weather;
using CAP_ChatInteractive.Utilities;
using LudeonTK;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;
using Verse.Noise;

namespace CAP_ChatInteractive.Commands.CommandHandlers
{
    public static class WeatherCommandHandler
    {
        public static string HandleWeatherCommand(ChatMessageWrapper user, string weatherType)
        {
            try
            {
                var settings = CAPChatInteractiveMod.Instance.Settings.GlobalSettings;
                var currencySymbol = settings.CurrencyName?.Trim() ?? "¢";

                // Handle list commands first
                if (weatherType.Equals("list", StringComparison.OrdinalIgnoreCase))
                {
                    return GetWeatherList();
                }
                else if (weatherType.StartsWith("list", StringComparison.OrdinalIgnoreCase))
                {
                    if (int.TryParse(weatherType.Substring(4), out int page) && page > 0)
                    {
                        return GetWeatherListPage(page);
                    }
                    return GetWeatherList();
                }

                // Check if viewer exists
                var viewer = Viewers.GetViewer(user);
                if (viewer == null)
                {
                    return "Error: Could not find your viewer data.";
                }

                // Find the weather by command input (supports defName, label, or partial match)
                var buyableWeather = FindBuyableWeather(weatherType);
                if (buyableWeather == null)
                {
                    var availableTypes = GetAvailableWeatherTypes().Take(8).Select(w => w.Key);
                    return $"Unknown weather type: {weatherType}. Available: {string.Join(", ", availableTypes)}...";
                }

                // Check if weather is enabled
                if (!buyableWeather.Enabled)
                {
                    return $"The {buyableWeather.Label} weather type is currently disabled.";
                }

                // NEW: Check global cooldowns using the unified system
                var cooldownManager = Current.Game.GetComponent<GlobalCooldownManager>();
                if (cooldownManager != null)
                {
                    Logger.Debug($"=== WEATHER COOLDOWN DEBUG ===");
                    Logger.Debug($"Weather: {buyableWeather.Label}");
                    Logger.Debug($"DefName: {buyableWeather.DefName}");
                    Logger.Debug($"KarmaType: {buyableWeather.KarmaType}");

                    // Get command settings for weather command
                    var commandSettings = CommandSettingsManager.GetSettings("weather");

                    // Use the unified cooldown check
                    if (!cooldownManager.CanUseCommand("weather", commandSettings, settings))
                    {
                        // Provide appropriate feedback based on what failed
                        if (!cooldownManager.CanUseGlobalEvents(settings))
                        {
                            int totalEvents = cooldownManager.data.EventUsage.Values.Sum(record => record.CurrentPeriodUses);
                            Logger.Debug($"Global event limit reached: {totalEvents}/{settings.EventsperCooldown}");
                            return $"❌ Global event limit reached! ({totalEvents}/{settings.EventsperCooldown} used this period)";
                        }

                        // Check karma-type specific limit
                        if (settings.KarmaTypeLimitsEnabled)
                        {
                            string eventType = GetKarmaTypeForWeather(buyableWeather.KarmaType);
                            Logger.Debug($"Converted event type: {eventType}");

                            if (!cooldownManager.CanUseEvent(eventType, settings))
                            {
                                var record = cooldownManager.data.EventUsage.GetValueOrDefault(eventType);
                                int used = record?.CurrentPeriodUses ?? 0;
                                int max = eventType switch
                                {
                                    "good" => settings.MaxGoodEvents,
                                    "bad" => settings.MaxBadEvents,
                                    "neutral" => settings.MaxNeutralEvents,
                                    "doom" => 1,
                                    _ => 10
                                };
                                string cooldownMessage = $"❌ {eventType.ToUpper()} event limit reached! ({used}/{max} used this period)";
                                Logger.Debug($"Karma type limit reached: {used}/{max}");
                                return cooldownMessage;
                            }
                        }

                        return $"❌ Weather command is on cooldown.";
                    }

                    Logger.Debug($"Weather cooldown check passed");
                }

                // Get cost and check if viewer can afford it
                int cost = buyableWeather.BaseCost;
                if (viewer.Coins < cost)
                {
                    MessageHandler.SendFailureLetter("Weather Change Failed",
                        $"{user.Username} doesn't have enough {currencySymbol} for {buyableWeather.Label}\n\nNeeded: {cost}{currencySymbol}, Has: {viewer.Coins}{currencySymbol}");
                    return $"You need {cost}{currencySymbol} for {buyableWeather.Label}!";
                }

                bool success = false;
                string resultMessage = "";

                // Check if this is a game condition or simple weather
                bool isGameCondition = IsGameConditionWeather(buyableWeather.DefName);

                if (isGameCondition)
                {
                    success = TriggerGameConditionWeather(buyableWeather, user.Username, out resultMessage);
                }
                else
                {
                    success = TriggerSimpleWeather(buyableWeather, user.Username, out resultMessage);
                }

                // Handle the result - ONLY deduct coins on success
                if (success)
                {
                    viewer.TakeCoins(cost);
                    // Record weather usage for cooldowns ONLY ON SUCCESS
                    if (success && cooldownManager != null)
                    {
                        string eventType = GetKarmaTypeForWeather(buyableWeather.KarmaType);
                        cooldownManager.RecordEventUse(eventType);
                        Logger.Debug($"Recorded weather usage as {eventType} event");

                        // Log current state after recording
                        var record = cooldownManager.data.EventUsage.GetValueOrDefault(eventType);
                        if (record != null)
                        {
                            Logger.Debug($"Current {eventType} event usage: {record.CurrentPeriodUses}");
                        }
                    }

                    MessageHandler.SendBlueLetter("Weather Changed",
                        $"{user.Username} changed the weather to {buyableWeather.Label} for {cost}{currencySymbol}\n\n{resultMessage}");
                }
                else
                {
                    resultMessage = $"{resultMessage} No {currencySymbol} were deducted.";
                }
                return resultMessage;
            }
            catch (Exception ex)
            {
                Logger.Error($"Error handling weather command: {ex}");
                MessageHandler.SendFailureLetter("Weather Error",
                    $"Error changing weather: {ex.Message}\n\nPlease try again later.");
                return "Error changing weather. Please try again.";
            }
        }

        private static string GetKarmaTypeForWeather(string karmaType)
        {
            if (string.IsNullOrEmpty(karmaType))
                return "neutral";

            return karmaType?.ToLower() switch
            {
                "good" => "good",
                "bad" => "bad",
                "doom" => "doom",
                _ => "neutral"
            };
        }

        private static BuyableWeather FindBuyableWeather(string input)
        {
            if (string.IsNullOrEmpty(input))
                return null;

            string inputLower = input.ToLower();
            var allWeather = GetAvailableWeatherTypes();

            // First try exact def name match
            if (allWeather.TryGetValue(input, out var weather))
                return weather;

            // Try case-insensitive def name match
            var defNameMatch = allWeather.Values.FirstOrDefault(w =>
                w.DefName.Equals(input, StringComparison.OrdinalIgnoreCase));
            if (defNameMatch != null)
                return defNameMatch;

            // Try label match (case-insensitive)
            var labelMatch = allWeather.Values.FirstOrDefault(w =>
                w.Label.Equals(input, StringComparison.OrdinalIgnoreCase));
            if (labelMatch != null)
                return labelMatch;

            // Try partial match on def name or label
            var partialMatch = allWeather.Values.FirstOrDefault(w =>
                w.DefName.ToLower().Contains(inputLower) ||
                w.Label.ToLower().Contains(inputLower));

            return partialMatch;
        }

        private static Dictionary<string, BuyableWeather> GetAvailableWeatherTypes()
        {
            return BuyableWeatherManager.AllBuyableWeather
                .Where(kvp => kvp.Value.Enabled)
                .ToDictionary(kvp => kvp.Key.ToLower(), kvp => kvp.Value);
        }

        private static bool IsGameConditionWeather(string defName)
        {
            // Check if this weather is handled as a game condition incident
            var incidentDef = DefDatabase<IncidentDef>.GetNamedSilentFail(defName);
            return incidentDef != null && incidentDef.Worker != null;
        }

        private static bool TriggerSimpleWeather(BuyableWeather weather, string username, out string immersiveMessage)
        {
            immersiveMessage = "";
            
            var weatherDef = DefDatabase<WeatherDef>.GetNamedSilentFail(weather.DefName);
            if (weatherDef == null)
            {
                Logger.Error($"WeatherDef not found: {weather.DefName}");
                immersiveMessage = $"The weather spirits don't recognize '{weather.Label}'.";
                return false;
            }

            var playerMaps = Current.Game.Maps.Where(map => map.IsPlayerHome).ToList();

            var suitableMaps = playerMaps
                .Where(map => map.weatherManager.curWeather != weatherDef)
                .ToList();

            if (!suitableMaps.Any())
            {
                immersiveMessage = $"The weather is already {weather.Label} or transitioning to it.";
                return false;
            }


            // Apply weather to a random suitable map
            var targetMap = suitableMaps.RandomElement();

            if (!IsBiomeValidForWeather(targetMap))
            {
                immersiveMessage = GetBiomeRestrictionMessage(targetMap);
                return false;
            }

            // Check for temperature-based conversions
            var finalWeatherDef = GetTemperatureAdjustedWeather(weatherDef, targetMap, out string conversionMessage);

            // Actually change the weather
            targetMap.weatherManager.TransitionTo(finalWeatherDef);

            // Build immersive message
            if (finalWeatherDef != weatherDef)
            {
                immersiveMessage = $"Your {weather.Label} was transformed by the cold into {finalWeatherDef.label}! {conversionMessage}";
            }
            else
            {
                immersiveMessage = $"The skies shift as {weather.Label} begins to fall across the land.";
            }

            return true;
        }

        private static WeatherDef GetTemperatureAdjustedWeather(WeatherDef requestedWeather, Map map, out string conversionMessage)
        {
            conversionMessage = "";
            float currentTemp = map.mapTemperature.OutdoorTemp;
            string requestedName = requestedWeather.defName;

            // Temperature-based conversions (same logic as before)
            if (currentTemp < 0f)
            {
                switch (requestedName)
                {
                    case "Rain":
                        var snowDef = DefDatabase<WeatherDef>.GetNamedSilentFail("SnowGentle");
                        if (snowDef != null)
                        {
                            conversionMessage = "The freezing air turns the rain to snow.";
                            return snowDef;
                        }
                        break;
                    case "RainyThunderstorm":
                    case "DryThunderstorm":
                        var thundersnowDef = DefDatabase<WeatherDef>.GetNamedSilentFail("SnowyThunderStorm");
                        if (thundersnowDef != null)
                        {
                            conversionMessage = "The thunderstorm freezes into a raging thundersnow!";
                            return thundersnowDef;
                        }
                        break;
                }
            }
            else if (currentTemp > 5f)
            {
                switch (requestedName)
                {
                    case "SnowGentle":
                        var rainDef = DefDatabase<WeatherDef>.GetNamedSilentFail("Rain");
                        if (rainDef != null)
                        {
                            conversionMessage = "The warm air melts the snow into rain.";
                            return rainDef;
                        }
                        break;
                    case "SnowHard":
                        var snowGentleDef = DefDatabase<WeatherDef>.GetNamedSilentFail("SnowGentle");
                        if (snowGentleDef != null)
                        {
                            conversionMessage = "The warming air lightens the heavy snow.";
                            return snowGentleDef;
                        }
                        break;
                }
            }

            return requestedWeather;
        }

        private static bool TriggerGameConditionWeather(BuyableWeather weather, string username, out string immersiveMessage)
        {
            immersiveMessage = "";
            var incidentDef = DefDatabase<IncidentDef>.GetNamedSilentFail(weather.DefName);
            if (incidentDef == null)
            {
                Logger.Error($"IncidentDef not found: {weather.DefName}");
                immersiveMessage = $"The ancient powers refuse to summon {weather.Label}.";
                return false;
            }

            var worker = incidentDef.Worker;
            if (worker == null)
            {
                Logger.Error($"No worker for incident: {weather.DefName}");
                immersiveMessage = $"The weather mages cannot conjure {weather.Label}.";
                return false;
            }

            var playerMaps = Current.Game.Maps.Where(map => map.IsPlayerHome).ToList();
            playerMaps.Shuffle();

            foreach (var map in playerMaps)
            {
                // Check biome validity first
                if (!IsBiomeValidForWeather(map))
                {
                    continue; // Skip to next map instead of failing completely
                }

                var parms = new IncidentParms
                {
                    target = map,
                    forced = true,
                    points = StorytellerUtility.DefaultThreatPointsNow(map)
                };

                if (worker.CanFireNow(parms) && !worker.FiredTooRecently(map))
                {
                    bool executed = worker.TryExecute(parms);
                    if (executed)
                    {
                        immersiveMessage = GetGameConditionMessage(weather);
                        return true;
                    }
                }
            }

            // If we get here, either no valid biomes or weather couldn't trigger
            if (playerMaps.Any(map => IsBiomeValidForWeather(map)))
            {
                immersiveMessage = $"The cosmic alignment prevents {weather.Label} from forming right now.";
            }
            else
            {
                immersiveMessage = $"No suitable locations found for {weather.Label} in your current biomes.";
            }
            return false;
        }

        private static string GetGameConditionMessage(BuyableWeather weather)
        {
            return weather.DefName switch
            {
                "SolarFlare" => "The sun flares with cosmic energy, disrupting all electronics!",
                "ToxicFallout" => "A sickly green haze descends as toxic fallout begins...",
                "Flashstorm" => "Dark clouds gather as a flashstorm crackles to life!",
                "Eclipse" => "An unnatural darkness falls as the sun is eclipsed!",
                "Aurora" => "The sky dances with shimmering auroral lights!",
                "HeatWave" => "A blistering heat wave settles over the land!",
                "ColdSnap" => "An icy cold snap freezes the air!",
                "VolcanicWinter" => "Volcanic ash clouds block the sun, bringing endless winter!",
                _ => $"The {weather.Label} takes hold across the land."
            };
        }

        private static string GetWeatherList()
        {
            var settings = CAPChatInteractiveMod.Instance.Settings.GlobalSettings;
            var currencySymbol = settings.CurrencyName?.Trim() ?? "¢";
            var cooldownManager = Current.Game.GetComponent<GlobalCooldownManager>();

            var availableWeathers = GetAvailableWeatherTypes()
                .Where(kvp => !IsGameConditionWeather(kvp.Value.DefName))
                .Select(kvp =>
                {
                    string status = "✅";
                    if (cooldownManager != null && settings.KarmaTypeLimitsEnabled)
                    {
                        string eventType = GetKarmaTypeForWeather(kvp.Value.KarmaType);
                        if (!cooldownManager.CanUseEvent(eventType, settings))
                        {
                            status = "❌";
                        }
                    }
                    return $"{kvp.Value.Label} ({kvp.Value.BaseCost}{currencySymbol}){status}";
                })
                .ToList();

            // Add cooldown summary if limits are enabled
            string cooldownSummary = "";
            if (settings.KarmaTypeLimitsEnabled && cooldownManager != null)
            {
                cooldownSummary = GetCooldownSummary(settings, cooldownManager);
            }

            var message = "Available weather: " + string.Join(", ", availableWeathers.Take(8));

            if (!string.IsNullOrEmpty(cooldownSummary))
            {
                message += $" | {cooldownSummary}";
            }

            if (availableWeathers.Count > 8)
            {
                message += "... (see more with !weather list2)";
            }

            return message;
        }

        private static string GetCooldownSummary(CAPGlobalChatSettings settings, GlobalCooldownManager cooldownManager)
        {
            var summaries = new List<string>();

            // Global event limit
            if (settings.EventCooldownsEnabled && settings.EventsperCooldown > 0)
            {
                int totalEvents = cooldownManager.data.EventUsage.Values.Sum(record => record.CurrentPeriodUses);
                summaries.Add($"Total: {totalEvents}/{settings.EventsperCooldown}");
            }

            // Karma-type limits
            if (settings.KarmaTypeLimitsEnabled)
            {
                if (settings.MaxGoodEvents > 0)
                {
                    var goodRecord = cooldownManager.data.EventUsage.GetValueOrDefault("good");
                    int goodUsed = goodRecord?.CurrentPeriodUses ?? 0;
                    summaries.Add($"Good: {goodUsed}/{settings.MaxGoodEvents}");
                }

                if (settings.MaxBadEvents > 0)
                {
                    var badRecord = cooldownManager.data.EventUsage.GetValueOrDefault("bad");
                    int badUsed = badRecord?.CurrentPeriodUses ?? 0;
                    summaries.Add($"Bad: {badUsed}/{settings.MaxBadEvents}");
                }

                if (settings.MaxNeutralEvents > 0)
                {
                    var neutralRecord = cooldownManager.data.EventUsage.GetValueOrDefault("neutral");
                    int neutralUsed = neutralRecord?.CurrentPeriodUses ?? 0;
                    summaries.Add($"Neutral: {neutralUsed}/{settings.MaxNeutralEvents}");
                }
            }

            return string.Join(" | ", summaries);
        }

        private static string GetWeatherListPage(int page)
        {
            var settings = CAPChatInteractiveMod.Instance.Settings.GlobalSettings;
            var currencySymbol = settings.CurrencyName?.Trim() ?? "¢";

            var availableWeathers = GetAvailableWeatherTypes()
                .Where(kvp => !IsGameConditionWeather(kvp.Value.DefName))
                .Select(kvp => $"{kvp.Value.Label} ({kvp.Value.BaseCost}{currencySymbol})")
                .ToList();

            int itemsPerPage = 8;
            int startIndex = (page - 1) * itemsPerPage;
            int endIndex = Math.Min(startIndex + itemsPerPage, availableWeathers.Count);

            if (startIndex >= availableWeathers.Count)
                return "No more weather types to display.";

            var pageItems = availableWeathers.Skip(startIndex).Take(itemsPerPage);
            return $"Available weather (page {page}): " + string.Join(", ", pageItems);
        }

        private static bool IsBiomeValidForWeather(Map map)
        {
            if (map == null) return false;

            string biomeDefName = map.Biome?.defName ?? "";

            // Exclude underground and space biomes
            return !(biomeDefName.Contains("Underground") ||
                     biomeDefName.Contains("Space") ||
                     biomeDefName.Contains("Orbit"));
        }

        private static string GetBiomeRestrictionMessage(Map map)
        {
            string biomeName = map.Biome?.label ?? "this location";
            return $"Sorry, you can't change the weather in {biomeName}.";
        }

        [DebugAction("CAP", "Test Weather Conversion", allowedGameStates = AllowedGameStates.Playing)]
        public static void DebugTestWeatherConversion()
        {
            Map map = Find.CurrentMap;
            if (map == null) return;

            float temp = map.mapTemperature.OutdoorTemp;
            Logger.Message($"Current temperature: {temp}°C");

            var testWeathers = new[] { "Rain", "RainyThunderstorm", "SnowGentle", "SnowHard" };

            foreach (var weatherName in testWeathers)
            {
                var weatherDef = DefDatabase<WeatherDef>.GetNamedSilentFail(weatherName);
                if (weatherDef != null)
                {
                    var finalWeather = GetTemperatureAdjustedWeather(weatherDef, map, out string message);
                    if (finalWeather != weatherDef)
                    {
                        Logger.Message($"{weatherName} → {finalWeather.defName}: {message}");
                    }
                    else
                    {
                        Logger.Message($"{weatherName}: No conversion needed");
                    }
                }
            }
        }
    }
}