// JsonFileManager.cs
// Copyright (c) Captolamia. All rights reserved.
// Licensed under the AGPLv3 License. See LICENSE file in the project root for full license information.
// Manages JSON file operations for the CAP Chat Interactive mod.
// Handles loading, saving, and serialization/deserialization of various mod data types.
using CAP_ChatInteractive.Incidents;
using CAP_ChatInteractive.Store;
using CAP_ChatInteractive.Traits;
using _CAP__Chat_Interactive.Utilities;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Verse;

namespace CAP_ChatInteractive
{
    public static class JsonFileManager
    {
        private static readonly string ModDataFolder;

        static JsonFileManager()
        {
            // RimWorld's AppData folder + our mod folder
            ModDataFolder = Path.Combine(GenFilePaths.ConfigFolderPath, "CAP_ChatInteractive");

            // Ensure directory exists
            if (!Directory.Exists(ModDataFolder))
            {
                Directory.CreateDirectory(ModDataFolder);
            }
        }

        public static string GetFilePath(string fileName)
        {
            return Path.Combine(ModDataFolder, fileName);
        }

        public static bool FileExists(string fileName)
        {
            return File.Exists(GetFilePath(fileName));
        }

        public static string LoadFile(string fileName)
        {
            try
            {
                string filePath = GetFilePath(fileName);
                if (File.Exists(filePath))
                {
                    return File.ReadAllText(filePath);
                }
                return null;
            }
            catch (Exception ex)
            {
                Logger.Error($"Error loading file {fileName}: {ex.Message}");
                return null;
            }
        }

        public static bool SaveFile(string fileName, string content)
        {
            try
            {
                string filePath = GetFilePath(fileName);
                File.WriteAllText(filePath, content);
                Logger.Message($"Saved {fileName} to: {filePath}");
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error($"Error saving file {fileName}: {ex.Message}");
                return false;
            }
        }

        public static string GetClientSecretsTemplate()
        {
            return @"{
                  ""installed"": {
                    ""client_id"": ""YOUR_CLIENT_ID_HERE"", // ← Get from Google Cloud Console OAuth 2.0
                    ""project_id"": ""cap-chat-interactive"", // ← Keep this as-is
                    ""auth_uri"": ""https://accounts.google.com/o/oauth2/auth"",
                    ""token_uri"": ""https://oauth2.googleapis.com/token"",
                    ""auth_provider_x509_cert_url"": ""https://www.googleapis.com/oauth2/v1/certs"",
                    ""client_secret"": ""YOUR_CLIENT_SECRET_HERE"", // ← Get from Google Cloud Console OAuth 2.0
                    ""redirect_uris"": [ ""urn:ietf:wg:oauth:2.0:oob"", ""http://localhost"" ]
                  }
            }";
        }
        /// <summary>
        /// Store item serialization with manual JObject construction for better control
        /// </summary>
        /// <param name="storeItems"></param>
        /// <returns></returns>
        public static string SerializeStoreItems(Dictionary<string, StoreItem> storeItems)
        {
            try
            {
                var rootObject = new JObject();
                var itemsObject = new JObject();

                foreach (var kvp in storeItems)
                {
                    var storeItem = kvp.Value;
                    var itemObject = new JObject();

                    // ADD DefName to serialization
                    itemObject["DefName"] = storeItem.DefName;
                    itemObject["CustomName"] = storeItem.CustomName ?? null;
                    itemObject["HasQuantityLimit"] = storeItem.HasQuantityLimit;
                    itemObject["IsMelee"] = storeItem.IsMelee;
                    itemObject["IsRanged"] = storeItem.IsRanged;
                    itemObject["IsStuffAllowed"] = storeItem.IsStuffAllowed;
                    itemObject["IsWeapon"] = storeItem.IsWeapon;
                    itemObject["QuantityLimit"] = storeItem.QuantityLimit;
                    itemObject["LimitMode"] = storeItem.LimitMode.ToString();
                    itemObject["ResearchOverrides"] = null;
                    itemObject["IsUsable"] = storeItem.IsUsable;
                    itemObject["IsEquippable"] = storeItem.IsEquippable;
                    itemObject["IsWearable"] = storeItem.IsWearable;
                    itemObject["Mod"] = storeItem.ModSource ?? "RimWorld";
                    itemObject["BasePrice"] = storeItem.BasePrice;
                    itemObject["Category"] = storeItem.Category ?? "Misc";
                    itemObject["Enabled"] = storeItem.Enabled;

                    itemsObject[kvp.Key] = itemObject;
                }

                rootObject["items"] = itemsObject;

                return rootObject.ToString(Formatting.Indented);
            }
            catch (System.Exception ex)
            {
                Logger.Error($"Error serializing store items: {ex.Message}");
                return "{}";
            }
        }

        public static Dictionary<string, StoreItem> DeserializeStoreItems(string jsonContent)
        {
            var storeItems = new Dictionary<string, StoreItem>();

            if (string.IsNullOrEmpty(jsonContent))
                return storeItems;

            try
            {
                var rootObject = JObject.Parse(jsonContent);
                var itemsObject = rootObject["items"] as JObject;

                if (itemsObject == null)
                    return storeItems;

                foreach (var property in itemsObject.Properties())
                {
                    var itemToken = property.Value;
                    var storeItem = new StoreItem();

                    storeItem.DefName = property.Name;
                    storeItem.CustomName = itemToken["CustomName"]?.Value<string>();
                    storeItem.HasQuantityLimit = itemToken["HasQuantityLimit"]?.Value<bool>() ?? true; // Changed default to true
                    storeItem.IsMelee = itemToken["IsMelee"]?.Value<bool>() ?? false;
                    storeItem.IsRanged = itemToken["IsRanged"]?.Value<bool>() ?? false;
                    storeItem.IsStuffAllowed = itemToken["IsStuffAllowed"]?.Value<bool>() ?? true;
                    storeItem.IsWeapon = itemToken["IsWeapon"]?.Value<bool>() ?? false;
                    storeItem.QuantityLimit = itemToken["QuantityLimit"]?.Value<int>() ?? 1; // Changed default to 1

                    // ADD THIS: Deserialize LimitMode with fallback
                    string limitModeString = itemToken["LimitMode"]?.Value<string>();
                    if (!string.IsNullOrEmpty(limitModeString) && Enum.TryParse<QuantityLimitMode>(limitModeString, out var limitMode))
                    {
                        storeItem.LimitMode = limitMode;
                    }
                    else
                    {
                        storeItem.LimitMode = QuantityLimitMode.OneStack; // Default fallback
                    }

                    storeItem.IsUsable = itemToken["IsUsable"]?.Value<bool>() ?? true;
                    storeItem.IsEquippable = itemToken["IsEquippable"]?.Value<bool>() ?? false;
                    storeItem.IsWearable = itemToken["IsWearable"]?.Value<bool>() ?? false;
                    storeItem.ModSource = itemToken["Mod"]?.Value<string>() ?? "RimWorld";
                    storeItem.BasePrice = itemToken["BasePrice"]?.Value<int>() ?? 0;
                    storeItem.Category = itemToken["Category"]?.Value<string>() ?? "Misc";
                    storeItem.Enabled = itemToken["Enabled"]?.Value<bool>() ?? true;

                    storeItems[property.Name] = storeItem;
                }
            }
            catch (System.Exception ex)
            {
                Logger.Error($"Error deserializing store items: {ex.Message}");
            }

            return storeItems;
        }

        /// <summary>
        /// trait serialization with settings to ignore nulls and defaults
        /// </summary>
        /// <param name="traits"></param>
        /// <returns></returns> 
        public static string SerializeTraits(Dictionary<string, BuyableTrait> traits)
        {
            var settings = new JsonSerializerSettings
            {
                Formatting = Formatting.Indented,
                NullValueHandling = NullValueHandling.Include  ,
                DefaultValueHandling = DefaultValueHandling.Include
            };

            return JsonConvert.SerializeObject(traits, settings);
        }

        public static Dictionary<string, BuyableTrait> DeserializeTraits(string jsonContent)
        {
            return JsonConvert.DeserializeObject<Dictionary<string, BuyableTrait>>(jsonContent);
        }

        // Add these methods to JsonFileManager.cs
        public static string SerializeIncidents(Dictionary<string, BuyableIncident> incidents)
        {
            try
            {
                var settings = new JsonSerializerSettings
                {
                    Formatting = Formatting.Indented,
                    NullValueHandling = NullValueHandling.Include,
                    DefaultValueHandling = DefaultValueHandling.Include,
                    ReferenceLoopHandling = ReferenceLoopHandling.Ignore
                };

                return JsonConvert.SerializeObject(incidents, settings);
            }
            catch (Exception ex)
            {
                Logger.Error($"Error serializing incidents: {ex.Message}");
                // Fallback: manual serialization
                return SerializeIncidentsManual(incidents);
            }
        }

        private static string SerializeIncidentsManual(Dictionary<string, BuyableIncident> incidents)
        {
            var rootObject = new JObject();

            foreach (var kvp in incidents)
            {
                var incident = kvp.Value;
                var incidentObject = new JObject();

                // Only serialize simple properties that won't cause issues
                incidentObject["DefName"] = incident.DefName;
                incidentObject["Label"] = incident.Label;
                incidentObject["Description"] = incident.Description;
                incidentObject["WorkerClassName"] = incident.WorkerClassName;
                incidentObject["CategoryName"] = incident.CategoryName;
                incidentObject["BaseCost"] = incident.BaseCost;
                incidentObject["KarmaType"] = incident.KarmaType;
                incidentObject["EventCap"] = incident.EventCap;
                incidentObject["Enabled"] = incident.Enabled;
                incidentObject["ModSource"] = incident.ModSource;
                incidentObject["Version"] = incident.Version;
                incidentObject["IsWeatherIncident"] = incident.IsWeatherIncident;
                incidentObject["IsRaidIncident"] = incident.IsRaidIncident;
                incidentObject["IsDiseaseIncident"] = incident.IsDiseaseIncident;
                incidentObject["IsQuestIncident"] = incident.IsQuestIncident;
                incidentObject["BaseChance"] = incident.BaseChance;
                incidentObject["PointsScaleable"] = incident.PointsScaleable;
                incidentObject["MinThreatPoints"] = incident.MinThreatPoints;
                incidentObject["MaxThreatPoints"] = incident.MaxThreatPoints;

                rootObject[kvp.Key] = incidentObject;
            }

            return rootObject.ToString(Formatting.Indented);
        }

        public static Dictionary<string, BuyableIncident> DeserializeIncidents(string jsonContent)
        {
            if (string.IsNullOrEmpty(jsonContent))
                return new Dictionary<string, BuyableIncident>();

            try
            {
                return JsonConvert.DeserializeObject<Dictionary<string, BuyableIncident>>(jsonContent)
                    ?? new Dictionary<string, BuyableIncident>();
            }
            catch (Exception ex)
            {
                Logger.Error($"Error deserializing incidents: {ex.Message}");
                return new Dictionary<string, BuyableIncident>();
            }
        }

        // Add these methods to JsonFileManager.cs
        public static string SerializeWeather(Dictionary<string, BuyableWeather> weather)
        {
            try
            {
                var settings = new JsonSerializerSettings
                {
                    Formatting = Formatting.Indented,
                    NullValueHandling = NullValueHandling.Include,
                    DefaultValueHandling = DefaultValueHandling.Include
                };

                return JsonConvert.SerializeObject(weather, settings);
            }
            catch (Exception ex)
            {
                Logger.Error($"Error serializing weather: {ex.Message}");
                return "{}";
            }
        }

        public static Dictionary<string, BuyableWeather> DeserializeWeather(string jsonContent)
        {
            if (string.IsNullOrEmpty(jsonContent))
                return new Dictionary<string, BuyableWeather>();

            try
            {
                return JsonConvert.DeserializeObject<Dictionary<string, BuyableWeather>>(jsonContent)
                    ?? new Dictionary<string, BuyableWeather>();
            }
            catch (Exception ex)
            {
                Logger.Error($"Error deserializing weather: {ex.Message}");
                return new Dictionary<string, BuyableWeather>();
            }
        }

        // Race Settings Serialization
        public static string SerializeRaceSettings(Dictionary<string, RaceSettings> raceSettings)
        {
            try
            {
                var settings = new JsonSerializerSettings
                {
                    Formatting = Formatting.Indented,
                    NullValueHandling = NullValueHandling.Include,
                    DefaultValueHandling = DefaultValueHandling.Include
                };

                return JsonConvert.SerializeObject(raceSettings, settings);
            }
            catch (Exception ex)
            {
                Logger.Error($"Error serializing race settings: {ex.Message}");
                return "{}";
            }
        }

        public static Dictionary<string, RaceSettings> DeserializeRaceSettings(string jsonContent)
        {
            if (string.IsNullOrEmpty(jsonContent))
                return new Dictionary<string, RaceSettings>();

            try
            {
                return JsonConvert.DeserializeObject<Dictionary<string, RaceSettings>>(jsonContent)
                    ?? new Dictionary<string, RaceSettings>();
            }
            catch (Exception ex)
            {
                Logger.Error($"Error deserializing race settings: {ex.Message}");
                return new Dictionary<string, RaceSettings>();
            }
        }
        public static Dictionary<string, RaceSettings> LoadRaceSettings()
        {
            string json = LoadFile("RaceSettings.json");
            Dictionary<string, RaceSettings> settings = new Dictionary<string, RaceSettings>();

            if (!string.IsNullOrEmpty(json))
            {
                try
                {
                    settings = DeserializeRaceSettings(json);
                    Logger.Debug($"Loaded race settings for {settings.Count} races");
                }
                catch (Exception ex)
                {
                    Logger.Error($"Error loading race settings: {ex.Message}");
                    // Don't return empty, let the caller handle initialization
                }
            }
            else
            {
                Logger.Debug("No RaceSettings.json found, will initialize defaults");
            }

            return settings;
        }

        public static RaceSettings GetRaceSettings(string raceDefName)
        {
            var allSettings = LoadRaceSettings();
            if (allSettings.ContainsKey(raceDefName))
            {
                return allSettings[raceDefName];
            }

            // Return default settings if not found - enable by default for new races
            return new RaceSettings
            {
                Enabled = true, // Changed from false to true - new races should be enabled by default
                BasePrice = 1000,
                MinAge = 16,
                MaxAge = 65,
                AllowCustomXenotypes = true,
                // ADD THIS LINE for default display name
                DisplayName = raceDefName, // Use defName as fallback
                XenotypePrices = new Dictionary<string, float>(),
                EnabledXenotypes = new Dictionary<string, bool>()
            };
        }

        public static void SaveRaceSettings(Dictionary<string, RaceSettings> raceSettings)
        {
            try
            {
                string json = SerializeRaceSettings(raceSettings);
                SaveFile("RaceSettings.json", json);
                Logger.Debug($"Saved race settings for {raceSettings.Count} races");
            }
            catch (Exception ex)
            {
                Logger.Error($"Error saving race settings: {ex}");
            }
        }
    }
}