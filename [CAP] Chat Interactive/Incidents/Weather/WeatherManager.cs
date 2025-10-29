// WeatherManager.cs
using LudeonTK;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace CAP_ChatInteractive.Incidents.Weather
{
    public static class BuyableWeatherManager
    {
        public static Dictionary<string, BuyableWeather> AllBuyableWeather { get; private set; } = new Dictionary<string, BuyableWeather>();
        private static bool isInitialized = false;
        private static readonly object lockObject = new object();

        private static List<TemperatureVariant> temperatureVariants = new List<TemperatureVariant>
{
    new TemperatureVariant { BaseWeatherDefName = "RainyThunderstorm", ColdVariantDefName = "SnowyThunderStorm", ThresholdTemperature = 0f },
    new TemperatureVariant { BaseWeatherDefName = "Rain", ColdVariantDefName = "SnowHard", ThresholdTemperature = 2f }
};
        public static void InitializeWeather()
        {
            if (isInitialized) return;

            lock (lockObject)
            {
                if (isInitialized) return;

                Logger.Debug("Initializing Buyable Weather System...");

                if (!LoadWeatherFromJson())
                {
                    Logger.Debug("No weather JSON found, creating default weather...");
                    CreateDefaultWeather();
                    SaveWeatherToJson();
                }
                else
                {
                    ValidateAndUpdateWeather();
                    SaveWeatherToJson();
                }

                isInitialized = true;
                Logger.Message($"[CAP] Buyable Weather System initialized with {AllBuyableWeather.Count} weather types");
            }
        }


        private static bool LoadWeatherFromJson()
        {
            string jsonContent = JsonFileManager.LoadFile("Weather.json");
            if (string.IsNullOrEmpty(jsonContent))
                return false;

            try
            {
                var loadedWeather = JsonFileManager.DeserializeWeather(jsonContent);
                AllBuyableWeather.Clear();

                foreach (var kvp in loadedWeather)
                {
                    AllBuyableWeather[kvp.Key] = kvp.Value;
                }

                Logger.Debug($"Loaded {AllBuyableWeather.Count} weather types from JSON");
                return true;
            }
            catch (System.Exception e)
            {
                Logger.Error($"Error loading weather JSON: {e.Message}");
                return false;
            }
        }

        private static void CreateDefaultWeather()
        {
            AllBuyableWeather.Clear();

            var allWeatherDefs = DefDatabase<WeatherDef>.AllDefs.ToList();
            Logger.Debug($"Processing {allWeatherDefs.Count} weather definitions");

            int weatherCreated = 0;
            foreach (var weatherDef in allWeatherDefs)
            {
                try
                {
                    if (!IsWeatherSuitableForStore(weatherDef))
                        continue;

                    string key = GetWeatherKey(weatherDef);
                    if (!AllBuyableWeather.ContainsKey(key))
                    {
                        var buyableWeather = new BuyableWeather(weatherDef);
                        AllBuyableWeather[key] = buyableWeather;
                        weatherCreated++;
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error($"Error creating buyable weather for {weatherDef.defName}: {ex.Message}");
                }
            }

            Logger.Debug($"Created default weather with {AllBuyableWeather.Count} entries");
        }

        private static bool IsWeatherSuitableForStore(WeatherDef weatherDef)
        {
            string defName = weatherDef.defName.ToLower();

            // Explicitly exclude problematic weather types
            if (defName.Contains("orbit") ||
                defName.Contains("underground") ||
                defName.Contains("undercave") ||
                defName.Contains("unnatural") ||
                defName.Contains("stage") ||
                defName.Contains("metalhell") ||
                // defName.Contains("bloodrain") ||
                defName.Contains("deathpall") ||
                defName.Contains("graypall"))
                return false;

            // Also exclude abstract/base weather definitions
            //if (weatherDef.abstract)  return false;

    // Include everything else that's not excluded
    return true;
        }

        private static string GetWeatherKey(WeatherDef weatherDef)
        {
            return weatherDef.defName;
        }

        private static void ValidateAndUpdateWeather()
        {
            // Similar to incidents validation
            var allWeatherDefs = DefDatabase<WeatherDef>.AllDefs;

            foreach (var weatherDef in allWeatherDefs)
            {
                if (!IsWeatherSuitableForStore(weatherDef))
                    continue;

                string key = GetWeatherKey(weatherDef);
                if (!AllBuyableWeather.ContainsKey(key))
                {
                    var buyableWeather = new BuyableWeather(weatherDef);
                    AllBuyableWeather[key] = buyableWeather;
                }
            }

            var keysToRemove = new List<string>();
            foreach (var kvp in AllBuyableWeather)
            {
                var weatherDef = DefDatabase<WeatherDef>.GetNamedSilentFail(kvp.Key);
                if (weatherDef == null || !IsWeatherSuitableForStore(weatherDef))
                {
                    keysToRemove.Add(kvp.Key);
                }
            }

            foreach (var key in keysToRemove)
            {
                AllBuyableWeather.Remove(key);
            }
        }

        public static void SaveWeatherToJson()
        {
            LongEventHandler.QueueLongEvent(() =>
            {
                lock (lockObject)
                {
                    try
                    {
                        string jsonContent = JsonFileManager.SerializeWeather(AllBuyableWeather);
                        JsonFileManager.SaveFile("Weather.json", jsonContent);
                        Logger.Debug("Weather JSON saved successfully");
                    }
                    catch (System.Exception e)
                    {
                        Logger.Error($"Error saving weather JSON: {e.Message}");
                    }
                }
            }, null, false, null, showExtraUIInfo: false, forceHideUI: true);
        }



        [DebugAction("CAP", "Reload Weather", allowedGameStates = AllowedGameStates.Playing)]
        public static void DebugReloadWeather()
        {
            isInitialized = false;
            InitializeWeather();
        }
    }
}