// WeatherCommandHandler.cs - REFACTORED
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
                var viewer = Viewers.GetViewer(user.Username);
                if (viewer == null)
                {
                    MessageHandler.SendFailureLetter("Weather Change Failed",
                        $"Could not find viewer data for {user.Username}\n\nPlease try again or contact the streamer.");
                    return "Error: Could not find your viewer data.";
                }

                // Find the weather by command input (supports defName, label, or partial match)
                var buyableWeather = FindBuyableWeather(weatherType);
                if (buyableWeather == null)
                {
                    var availableTypes = GetAvailableWeatherTypes().Take(8).Select(w => w.Key);
                    MessageHandler.SendFailureLetter("Weather Change Failed",
                        $"{user.Username} tried unknown weather type: {weatherType}\n\nAvailable types: {string.Join(", ", availableTypes)}...");
                    return $"Unknown weather type: {weatherType}. Available: {string.Join(", ", availableTypes)}...";
                }

                // Check if weather is enabled
                if (!buyableWeather.Enabled)
                {
                    MessageHandler.SendFailureLetter("Weather Change Failed",
                        $"{user.Username} tried to use disabled weather: {buyableWeather.Label}\n\nThis weather type is currently disabled in the settings.");
                    return $"The {buyableWeather.Label} weather type is currently disabled.";
                }

                // Get cost and check if viewer can afford it
                int cost = buyableWeather.BaseCost;
                if (viewer.Coins < cost)
                {
                    MessageHandler.SendFailureLetter("Weather Change Failed",
                        $"{user.Username} doesn't have enough {currencySymbol} for {buyableWeather.Label}\n\nNeeded: {cost}{currencySymbol}, Has: {viewer.Coins}{currencySymbol}");
                    // SendChatResponse(user, $"You need {cost}{currencySymbol} to change the weather to {buyableWeather.Label}! You have {viewer.Coins}{currencySymbol}.");
                    return $"You need {cost}{currencySymbol} for {buyableWeather.Label}!";
                }

                // Logging current map count and game state

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
                    viewer.Coins -= cost;
                    MessageHandler.SendBlueLetter("Weather Changed",
                        $"{user.Username} changed the weather to {buyableWeather.Label} for {cost}{currencySymbol}\n\n{resultMessage}");
                }
                else
                {
                    resultMessage = $"{resultMessage} No {currencySymbol} were deducted.";
                    MessageHandler.SendFailureLetter("Weather Change Failed",
                        $"{user.Username} failed to change weather to {buyableWeather.Label}\n\n{resultMessage}");
                }
                return resultMessage;
            }
            catch (Exception ex)
            {
                Logger.Error($"Error handling weather command: {ex}");
                MessageHandler.SendFailureLetter("Weather Error",
                    $"Error changing weather: {ex.Message}\n\nPlease try again later.");
                string errorMsg = "Error changing weather. Please try again.";
                return errorMsg;
            }
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

            var availableWeathers = GetAvailableWeatherTypes()
                .Where(kvp => !IsGameConditionWeather(kvp.Value.DefName))
                .Select(kvp => $"{kvp.Value.Label} ({kvp.Value.BaseCost}{currencySymbol})")
                .ToList();

            var message = "Available weather: " + string.Join(", ", availableWeathers.Take(8));

            if (availableWeathers.Count > 8)
            {
                message += "... (see more with !weather list2)";
            }

            return message;
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