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
                    return "RICS.WCH.ViewerNotFound".Translate();
                }

                // Find the weather by command input (supports defName, label, or partial match)
                var buyableWeather = FindBuyableWeather(weatherType);
                if (buyableWeather == null)
                {
                    var availableTypes = GetAvailableWeatherTypes().Take(8).Select(w => w.Key);
                    return "RICS.WCH.UnknownWeather".Translate(weatherType, string.Join(", ", availableTypes));
                }

                // Check if weather is enabled
                if (!buyableWeather.Enabled)
                {
                    return "RICS.WCH.WeatherDisabled".Translate(buyableWeather.Label);
                }

                // NEW: Check global cooldowns using the unified system
                var cooldownManager = Current.Game.GetComponent<GlobalCooldownManager>();
                if (cooldownManager != null)
                {
                    //Logger.Debug($"=== WEATHER COOLDOWN DEBUG ===");
                    //Logger.Debug($"Weather: {buyableWeather.Label}");
                    //Logger.Debug($"DefName: {buyableWeather.DefName}");
                    //Logger.Debug($"KarmaType: {buyableWeather.KarmaType}");

                    // Get command settings for weather command
                    var commandSettings = CommandSettingsManager.GetSettings("weather");

                    // Use the unified cooldown check
                    if (!cooldownManager.CanUseCommand("weather", commandSettings, settings))
                    {
                        // Provide appropriate feedback based on what failed
                        if (!cooldownManager.CanUseGlobalEvents(settings))
                        {
                            int totalEvents = cooldownManager.data.EventUsage.Values.Sum(record => record.CurrentPeriodUses);
                            // Logger.Debug($"Global event limit reached: {totalEvents}/{settings.EventsperCooldown}");
                            return "RICS.WCH.GlobalEventLimitReached".Translate(totalEvents, settings.EventsperCooldown);
                        }

                        // Check karma-type specific limit
                        if (settings.KarmaTypeLimitsEnabled)
                        {
                            string eventType = GetKarmaTypeForWeather(buyableWeather.KarmaType);
                            // Logger.Debug($"Converted event type: {eventType}");

                            if (!cooldownManager.CanUseEvent(eventType, settings))
                            {
                                var record = cooldownManager.data.EventUsage.GetValueOrDefault(eventType);
                                int used = record?.CurrentPeriodUses ?? 0;
                                int max = eventType switch
                                {
                                    "good" => settings.MaxGoodEvents,
                                    "bad" => settings.MaxBadEvents,
                                    "neutral" => settings.MaxNeutralEvents,
                                    "doom" => settings.MaxBadEvents,
                                    _ => 10
                                };
                                // string cooldownMessage = $"❌ {eventType.ToUpper()} event limit reached! ({used}/{max} used this period)";
                                string cooldownMessage = "RICS.WCH.KarmaTypeLimitReached"
                                    .Translate(
                                        eventType.ToUpper(),
                                        used,
                                        max
                                    );
                                // Logger.Debug($"Karma type limit reached: {used}/{max}");
                                return cooldownMessage;
                            }
                        }

                        //return $"❌ Weather command is on cooldown.";
                        return "RICS.WCH.CommandOnCooldown".Translate();
                    }

                    // Logger.Debug($"Weather cooldown check passed");
                }

                // Get cost and check if viewer can afford it
                int cost = buyableWeather.BaseCost;
                if (viewer.Coins < cost)
                {
                    return "RICS.WCH.InsufficientFunds".Translate(cost, currencySymbol, buyableWeather.Label);
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
                    // Add Karma for successful weather change 1.0.15
                    if (buyableWeather.KarmaType == "good")
                        viewer.GiveKarma(buyableWeather.BaseCost/100);
                    else if (buyableWeather.KarmaType == "bad" || buyableWeather.KarmaType == "doom")
                        viewer.TakeKarma(buyableWeather.BaseCost / 100);

                    // Record weather usage for cooldowns ONLY ON SUCCESS
                    if (success && cooldownManager != null)
                    {
                        string eventType = GetKarmaTypeForWeather(buyableWeather.KarmaType);
                        cooldownManager.RecordEventUse(eventType);
                        // Logger.Debug($"Recorded weather usage as {eventType} event");

                        // Log current state after recording
                        var record = cooldownManager.data.EventUsage.GetValueOrDefault(eventType);
                        //if (record != null)
                        //{
                        //    Logger.Debug($"Current {eventType} event usage: {record.CurrentPeriodUses}");
                        //}
                    }
                    //MessageHandler.SendBlueLetter("Weather Changed",
                    //    $"{user.Username} changed the weather to {buyableWeather.Label} for {cost}{currencySymbol}\n\n{resultMessage}");
                    MessageHandler.SendBlueLetter(
                        "RICS.WCH.WeatherChangedTitle".Translate(),
                        "RICS.WCH.WeatherChangedBody".Translate(
                            user.Username,
                            buyableWeather.Label,
                            cost,
                            currencySymbol,
                            resultMessage
                        )
                    );
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
                return $"Error changing weather. {ex}";
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
                immersiveMessage = "RICS.WCH.SimpleWeatherDefNotFound".Translate(weather.Label);
                return false;
            }

            var playerMaps = Current.Game.Maps.Where(map => map.IsPlayerHome).ToList();

            var suitableMaps = playerMaps
                .Where(map => map.weatherManager.curWeather != weatherDef)
                .ToList();

            if (!suitableMaps.Any())
            {
                immersiveMessage = "RICS.WCH.WeatherAlreadyActive".Translate(weather.Label);
                return false;
            }

            // Apply weather to a random suitable map
            var targetMap = suitableMaps.RandomElement();

            if (!IsBiomeValidForWeather(targetMap))
            {
                string biomeMsg = GetBiomeRestrictionMessage(targetMap);
                immersiveMessage = "RICS.WCH.BiomeRestriction".Translate(biomeMsg);
                return false;
            }

            // Check for temperature-based conversions
            var finalWeatherDef = GetTemperatureAdjustedWeather(weatherDef, targetMap, out string conversionMessage);

            // Actually change the weather
            targetMap.weatherManager.TransitionTo(finalWeatherDef);

            // Build immersive message
            if (finalWeatherDef != weatherDef)
            {
                immersiveMessage = "RICS.WCH.WeatherConvertedByCold".Translate(
                    weather.Label,
                    finalWeatherDef.label,
                    conversionMessage
                );
            }
            else
            {
                immersiveMessage = "RICS.WCH.WeatherTransitionSuccess".Translate(weather.Label);
            }

            return true;
        }

        private static WeatherDef GetTemperatureAdjustedWeather(WeatherDef requestedWeather, Map map, out string conversionMessage)
        {
            conversionMessage = "";
            float currentTemp = map.mapTemperature.OutdoorTemp;
            string requestedName = requestedWeather.defName;

            // Temperature-based conversions
            if (currentTemp < 0f)
            {
                switch (requestedName)
                {
                    case "Rain":
                        var snowDef = DefDatabase<WeatherDef>.GetNamedSilentFail("SnowGentle");
                        if (snowDef != null)
                        {
                            conversionMessage = "RICS.WCH.ConvertRainToSnow".Translate();
                            return snowDef;
                        }
                        break;

                    case "RainyThunderstorm":
                    case "DryThunderstorm":
                        var thundersnowDef = DefDatabase<WeatherDef>.GetNamedSilentFail("SnowyThunderStorm");
                        if (thundersnowDef != null)
                        {
                            conversionMessage = "RICS.WCH.ConvertThunderToThundersnow".Translate();
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
                            conversionMessage = "RICS.WCH.ConvertSnowGentleToRain".Translate();
                            return rainDef;
                        }
                        break;

                    case "SnowHard":
                        var snowGentleDef = DefDatabase<WeatherDef>.GetNamedSilentFail("SnowGentle");
                        if (snowGentleDef != null)
                        {
                            conversionMessage = "RICS.WCH.ConvertHeavySnowToLight".Translate();
                            return snowGentleDef;
                        }
                        break;
                }
            }

            // No conversion occurred
            return requestedWeather;
        }

        private static bool TriggerGameConditionWeather(BuyableWeather weather, string username, out string immersiveMessage)
        {
            immersiveMessage = "";
            var incidentDef = DefDatabase<IncidentDef>.GetNamedSilentFail(weather.DefName);
            if (incidentDef == null)
            {
                Logger.Error($"IncidentDef not found: {weather.DefName}");
                immersiveMessage = "RICS.WCH.GameConditionDefNotFound".Translate(weather.Label);
                return false;
            }

            var worker = incidentDef.Worker;
            if (worker == null)
            {
                Logger.Error($"No worker for incident: {weather.DefName}");
                immersiveMessage = "RICS.WCH.NoWorkerForIncident".Translate(weather.Label);
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
                immersiveMessage = "RICS.WCH.GameConditionCosmicAlignment".Translate(weather.Label);
            }
            else
            {
                immersiveMessage = "RICS.WCH.GameConditionNoSuitableLocation".Translate(weather.Label);
            }
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

                    // Use translatable entry format
                    return "RICS.WCH.WeatherListEntry".Translate(
                        kvp.Value.Label,
                        kvp.Value.BaseCost,
                        currencySymbol,
                        status
                    );
                })
                .ToList();

            // Build the entries part
            string entriesPart = string.Join(", ", availableWeathers.Take(8));

            // Cooldown summary (still comes from GetCooldownSummary – we'll handle that next if needed)
            string cooldownSummary = "";
            if (settings.KarmaTypeLimitsEnabled && cooldownManager != null)
            {
                cooldownSummary = GetCooldownSummary(settings, cooldownManager);
            }

            // Assemble final message
            string message = "RICS.WCH.WeatherListTitle".Translate() + " " + entriesPart;

            if (!string.IsNullOrEmpty(cooldownSummary))
            {
                message += "RICS.WCH.WeatherListSeparator".Translate() + cooldownSummary;
            }

            if (availableWeathers.Count > 8)
            {
                // Assuming your weather command prefix is "weather " or "!"
                // → adjust the argument if your actual prefix/command is different
                string listCommandExample = "weather list2";   // or "!weather list2", etc.
                message += " " + "RICS.WCH.WeatherListTruncated".Translate(listCommandExample);
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
                summaries.Add("RICS.WCH.CooldownTotal".Translate(
                    totalEvents,
                    settings.EventsperCooldown
                ));
            }

            // Karma-type limits
            if (settings.KarmaTypeLimitsEnabled)
            {
                if (settings.MaxGoodEvents > 0)
                {
                    var goodRecord = cooldownManager.data.EventUsage.GetValueOrDefault("good");
                    int goodUsed = goodRecord?.CurrentPeriodUses ?? 0;
                    summaries.Add("RICS.WCH.CooldownGood".Translate(
                        goodUsed,
                        settings.MaxGoodEvents
                    ));
                }

                if (settings.MaxBadEvents > 0)
                {
                    var badRecord = cooldownManager.data.EventUsage.GetValueOrDefault("bad");
                    int badUsed = badRecord?.CurrentPeriodUses ?? 0;
                    summaries.Add("RICS.WCH.CooldownBad".Translate(
                        badUsed,
                        settings.MaxBadEvents
                    ));
                }

                if (settings.MaxNeutralEvents > 0)
                {
                    var neutralRecord = cooldownManager.data.EventUsage.GetValueOrDefault("neutral");
                    int neutralUsed = neutralRecord?.CurrentPeriodUses ?? 0;
                    summaries.Add("RICS.WCH.CooldownNeutral".Translate(
                        neutralUsed,
                        settings.MaxNeutralEvents
                    ));
                }
            }

            if (!summaries.Any())
            {
                return "";
            }

            return string.Join("RICS.WCH.CooldownSeparator".Translate(), summaries);
        }

        private static string GetWeatherListPage(int page)
        {
            var settings = CAPChatInteractiveMod.Instance.Settings.GlobalSettings;
            var currencySymbol = settings.CurrencyName?.Trim() ?? "¢";

            var availableWeathers = GetAvailableWeatherTypes()
                .Where(kvp => !IsGameConditionWeather(kvp.Value.DefName))
                .Select(kvp =>
                {
                    // For consistency with GetWeatherList() — include status if you want
                    // (currently your original code does NOT show ✅/❌ on paged views)
                    // If you want to match the non-paged version, add status logic here too.

                    return "RICS.WCH.WeatherListEntry".Translate(
                        kvp.Value.Label,
                        kvp.Value.BaseCost,
                        currencySymbol,
                        ""   // ← empty status if you don't want icons on pages
                             // or compute status the same way as in GetWeatherList() if desired
                    );
                })
                .ToList();

            int itemsPerPage = 8;
            int startIndex = (page - 1) * itemsPerPage;

            if (startIndex >= availableWeathers.Count)
            {
                return "RICS.WCH.WeatherListPageNoMore".Translate();
            }

            int endIndex = Math.Min(startIndex + itemsPerPage, availableWeathers.Count);
            var pageItems = availableWeathers.Skip(startIndex).Take(itemsPerPage);

            string entries = string.Join(", ", pageItems);

            return "RICS.WCH.WeatherListPageTitle".Translate(page) + " " + entries;
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
            return "RICS.WCH.BiomeRestriction".Translate(biomeName);
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