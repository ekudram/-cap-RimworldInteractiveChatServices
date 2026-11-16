// JsonFileManager.cs
// Copyright (c) Captolamia
// This file is part of CAP Chat Interactive aka RICS (Rimworld Interactive Chat System).
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

// Manages JSON file operations for the CAP Chat Interactive mod.
// Handles loading, saving, and serialization/deserialization of various mod data types.
using _CAP__Chat_Interactive.Utilities;
using CAP_ChatInteractive.Incidents;
using CAP_ChatInteractive.Store;
using CAP_ChatInteractive.Traits;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RimWorld;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
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

            // === SETTINGS BACKUP FOLDER (JSON overlay / recovery) ===
            // Fallback when RimWorld Scribe/ModSettings fails to persist large settings objects.
            BackupFolderPath = Path.Combine(ModDataFolder, "Backups");
            if (!Directory.Exists(BackupFolderPath))
            {
                Directory.CreateDirectory(BackupFolderPath);
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
        /// <summary>
        /// Saves Json content to a file with error handling. Special case for ActiveMods.json to avoid spamming logs on read-only disks.
        /// </summary>
        /// <param name="fileName"></param>
        /// <param name="content"></param>
        /// <returns></returns>
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
                // Special handling for ActiveMods.json so it doesn't spam on every startup if disk is read-only
                if (fileName == "ActiveMods.json")
                {
                    Logger.Warning($"Could not save ActiveMods.json (this is non-critical): {ex.Message}");
                }
                else
                {
                    Logger.Error($"Error saving file {fileName}: {ex.Message}");
                }
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
        /// Store item serialization - switched to JsonConvert for better version compatibility
        /// </summary>
        public static string SerializeStoreItems(Dictionary<string, StoreItem> storeItems)
        {
            try
            {
                var settings = new JsonSerializerSettings
                {
                    Formatting = Formatting.Indented,
                    NullValueHandling = NullValueHandling.Include,
                    DefaultValueHandling = DefaultValueHandling.Include,
                    ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
                    // TypeNameHandling = TypeNameHandling.None  // only if you need it elsewhere
                };

                // Wrap in the same structure you currently produce
                var rootObject = new
                {
                    items = storeItems.ToDictionary(
                        kvp => kvp.Key,
                        kvp =>
                        {
                            var storeItem = kvp.Value;
                            return new
                            {
                                DefName = storeItem.DefName,
                                CustomName = storeItem.CustomName,
                                HasQuantityLimit = storeItem.HasQuantityLimit,
                                QuantityLimit = storeItem.QuantityLimit,
                                LimitMode = storeItem.LimitMode.ToString(),
                                IsUsable = storeItem.IsUsable,
                                IsEquippable = storeItem.IsEquippable,
                                IsWearable = storeItem.IsWearable,
                                Mod = storeItem.ModSource ?? "RimWorld",
                                BasePrice = storeItem.BasePrice,
                                Category = storeItem.Category ?? "Misc",
                                Enabled = storeItem.Enabled,
                                modactive = storeItem.modactive
                            };
                        }
                    )
                };

                return JsonConvert.SerializeObject(rootObject, settings);
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
                    storeItem.HasQuantityLimit = itemToken["HasQuantityLimit"]?.Value<bool>() ?? true;
                    // storeItem.IsMelee = itemToken["IsMelee"]?.Value<bool>() ?? false;
                    // storeItem.IsRanged = itemToken["IsRanged"]?.Value<bool>() ?? false;
                    // storeItem.IsStuffAllowed = itemToken["IsStuffAllowed"]?.Value<bool>() ?? true;
                    // storeItem.IsWeapon = itemToken["IsWeapon"]?.Value<bool>() ?? false;
                    storeItem.QuantityLimit = itemToken["QuantityLimit"]?.Value<int>() ?? 1;

                    // Deserialize LimitMode with fallback
                    string limitModeString = itemToken["LimitMode"]?.Value<string>();
                    if (!string.IsNullOrEmpty(limitModeString) && Enum.TryParse<QuantityLimitMode>(limitModeString, out var limitMode))
                    {
                        storeItem.LimitMode = limitMode;
                    }
                    else
                    {
                        storeItem.LimitMode = QuantityLimitMode.OneStack;
                    }

                    storeItem.IsUsable = itemToken["IsUsable"]?.Value<bool>() ?? true;
                    storeItem.IsEquippable = itemToken["IsEquippable"]?.Value<bool>() ?? false;
                    storeItem.IsWearable = itemToken["IsWearable"]?.Value<bool>() ?? false;
                    storeItem.ModSource = itemToken["Mod"]?.Value<string>() ?? "RimWorld";
                    storeItem.BasePrice = itemToken["BasePrice"]?.Value<int>() ?? 0;
                    storeItem.Category = itemToken["Category"]?.Value<string>() ?? "Misc";
                    storeItem.Enabled = itemToken["Enabled"]?.Value<bool>() ?? true;
                    storeItem.modactive = itemToken["modactive"]?.Value<bool>() ?? false;
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
                NullValueHandling = NullValueHandling.Include,
                DefaultValueHandling = DefaultValueHandling.Include,
                ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
                TypeNameHandling = TypeNameHandling.None   // ← add this line
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
                // Fallback: manual serialization (implement a safe basic version)
                return SerializeIncidentsManual(incidents);
            }
        }

        private static string SerializeIncidentsManual(Dictionary<string, BuyableIncident> incidents)
        {
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine("{");
                bool first = true;
                foreach (var kvp in incidents.OrderBy(k => k.Key))
                {
                    if (!first) sb.AppendLine(",");
                    first = false;

                    sb.AppendLine($"  \"{kvp.Key}\": {{");
                    var inc = kvp.Value;

                    // Manually write core fields — expand as needed, but keep simple
                    sb.AppendLine($"    \"DefName\": \"{EscapeJson(inc.DefName)}\",");
                    sb.AppendLine($"    \"Label\": \"{EscapeJson(inc.Label)}\",");
                    sb.AppendLine($"    \"Description\": \"{EscapeJson(inc.Description)}\",");
                    sb.AppendLine($"    \"BaseCost\": {inc.BaseCost},");
                    sb.AppendLine($"    \"KarmaType\": \"{EscapeJson(inc.KarmaType)}\",");
                    sb.AppendLine($"    \"EventCap\": {inc.EventCap},");
                    sb.AppendLine($"    \"Enabled\": {inc.Enabled.ToString().ToLower()},");
                    sb.AppendLine($"    \"DisabledReason\": \"{EscapeJson(inc.DisabledReason)}\",");
                    sb.AppendLine($"    \"ShouldBeInStore\": {inc.ShouldBeInStore.ToString().ToLower()},");
                    // ... add other important fields you care about preserving
                    // Skip complex/runtime-only fields like WorkerClassName if they cause loops

                    sb.AppendLine("  }");
                }
                sb.AppendLine("}");
                return sb.ToString();
            }
            catch (Exception ex)
            {
                Logger.Error($"Manual serialization fallback failed: {ex.Message}");
                return "{}"; // emergency empty object — better than crash
            }
        }

        private static string EscapeJson(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            return value.Replace("\\", "\\\\")
                        .Replace("\"", "\\\"")
                        .Replace("\n", "\\n")
                        .Replace("\r", "\\r")
                        .Replace("\t", "\\t");
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

        public static string GetBackupPath(string fileName)
        {
            string basePath = GetFilePath(fileName);
            string dir = System.IO.Path.GetDirectoryName(basePath);
            string name = System.IO.Path.GetFileNameWithoutExtension(basePath);
            string ext = System.IO.Path.GetExtension(basePath);

            // Add timestamp to backup filename
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            return System.IO.Path.Combine(dir, $"{name}_CORRUPTED_{timestamp}{ext}");
        }

        // Optional: Add a method to check if backup exists
        public static bool HasCorruptedBackup(string fileName)
        {
            string dir = System.IO.Path.GetDirectoryName(GetFilePath(fileName));
            return System.IO.Directory.GetFiles(dir, "*CORRUPTED*").Length > 0;
        }

        public static T LoadDataWithRecovery<T>(
            string fileName,
            Func<string, T> deserializeFunc,
            Func<T> createDefaultFunc,
            Action<string, string> onCorruption = null) where T : new()
        {
            string jsonContent = LoadFile(fileName);

            if (string.IsNullOrEmpty(jsonContent))
            {
                Logger.Debug($"No {fileName} found - creating defaults");
                return createDefaultFunc != null ? createDefaultFunc() : new T();
            }

            try
            {
                T data = deserializeFunc(jsonContent);
                if (data == null)
                {
                    throw new Newtonsoft.Json.JsonException("Deserialization returned null");
                }

                Logger.Debug($"Successfully loaded {fileName}");
                return data;
            }
            catch (Newtonsoft.Json.JsonException jsonEx)
            {
                Logger.Error($"JSON CORRUPTION in {fileName}: {jsonEx.Message}");

                // Backup corrupted file
                BackupCorruptedFile(fileName, jsonContent);

                // Custom corruption handler
                if (onCorruption != null)
                {
                    onCorruption(fileName, jsonEx.Message);
                }

                // Show in-game warning if applicable
                if (Current.ProgramState == ProgramState.Playing && Find.LetterStack != null)
                {
                    Find.LetterStack.ReceiveLetter(
                        $"Chat Interactive: {Path.GetFileNameWithoutExtension(fileName)} Data Corrupted",
                        $"{Path.GetFileNameWithoutExtension(fileName)} data was corrupted and has been rebuilt with defaults.\n\n" +
                        "If this happens frequently, check your hard drive health.",
                        LetterDefOf.NegativeEvent
                    );
                }

                return createDefaultFunc != null ? createDefaultFunc() : new T();
            }
            catch (System.IO.IOException ioEx)
            {
                Logger.Error($"DISK ACCESS ERROR reading {fileName}: {ioEx.Message}");
                return createDefaultFunc != null ? createDefaultFunc() : new T();
            }
            catch (System.Exception ex)
            {
                Logger.Error($"Error loading {fileName}: {ex.Message}");
                return createDefaultFunc != null ? createDefaultFunc() : new T();
            }
        }

        private static void BackupCorruptedFile(string fileName, string content)
        {
            try
            {
                string backupPath = GetBackupPath(fileName);
                File.WriteAllText(backupPath, content);
                Logger.Debug($"Backed up corrupted {fileName} to: {backupPath}");
            }
            catch { /* Silent fail */ }
        }

        // ========== RICS SETTINGS BACKUP SYSTEM (JSON) ==========
        // Safety net for RimWorld ModSettings/Scribe issues with large settings graphs.
        // Timestamped history (max 5) + RICS_Settings_LatestBackup.json for load/overlay.
        // Secrets (tokens) are included because they live in the same local AppData as config.xml.
        private static string BackupFolderPath;

        public const string LatestSettingsBackupFileName = "RICS_Settings_LatestBackup.json";
        private const int MaxTimestampedSettingsBackups = 5;

        private static JsonSerializerSettings CreateSettingsBackupSerializerSettings()
        {
            return new JsonSerializerSettings
            {
                Formatting = Formatting.Indented,
                NullValueHandling = NullValueHandling.Include,
                DefaultValueHandling = DefaultValueHandling.Include,
                ReferenceLoopHandling = ReferenceLoopHandling.Ignore
            };
        }

        private static void EnsureBackupFolder()
        {
            if (string.IsNullOrEmpty(BackupFolderPath))
                BackupFolderPath = Path.Combine(ModDataFolder, "Backups");

            if (!Directory.Exists(BackupFolderPath))
                Directory.CreateDirectory(BackupFolderPath);
        }

        public static string GetLatestSettingsBackupPath()
        {
            EnsureBackupFolder();
            return Path.Combine(BackupFolderPath, LatestSettingsBackupFileName);
        }

        /// <summary>
        /// Builds the four-section JObject used for backup files and XML-vs-JSON compare.
        /// Uses JObject.FromObject only on nested settings (avoids Verse.TaggedString on Mod).
        /// </summary>
        public static JObject BuildSettingsSnapshot(CAPChatInteractiveSettings settings)
        {
            var jsonSettings = CreateSettingsBackupSerializerSettings();
            var serializer = JsonSerializer.Create(jsonSettings);

            return new JObject
            {
                ["TwitchSettings"] = JObject.FromObject(settings?.TwitchSettings ?? new StreamServiceSettings(), serializer),
                ["YouTubeSettings"] = JObject.FromObject(settings?.YouTubeSettings ?? new StreamServiceSettings(), serializer),
                ["KickSettings"] = JObject.FromObject(settings?.KickSettings ?? new StreamServiceSettings(), serializer),
                ["GlobalSettings"] = JObject.FromObject(settings?.GlobalSettings ?? new CAPGlobalChatSettings(), serializer)
            };
        }

        /// <summary>
        /// Saves the live RICS settings graph to JSON.
        /// Default: timestamped file + Latest, then prune to 5 timestamped files.
        /// </summary>
        public static void SaveSettingsBackup(CAPChatInteractiveSettings settings)
        {
            SaveSettingsBackup(settings, writeTimestamped: true, skipTimestampIfUnchanged: false);
        }

        /// <param name="writeTimestamped">When false, only RICS_Settings_LatestBackup.json is updated (token refresh / WriteSettings).</param>
        /// <param name="skipTimestampIfUnchanged">When true, skip a new timestamped file if canonical content matches Latest.</param>
        public static void SaveSettingsBackup(CAPChatInteractiveSettings settings, bool writeTimestamped, bool skipTimestampIfUnchanged)
        {
            if (settings == null)
            {
                Logger.Error("[Backup] Cannot save null settings object");
                return;
            }

            try
            {
                EnsureBackupFolder();

                JObject root = BuildSettingsSnapshot(settings);
                string json = root.ToString(Formatting.Indented);
                string latestPath = GetLatestSettingsBackupPath();

                string existingLatest = null;
                if (File.Exists(latestPath))
                {
                    try { existingLatest = File.ReadAllText(latestPath); }
                    catch { /* compare will treat as changed */ }
                }

                if (existingLatest != json)
                    File.WriteAllText(latestPath, json);

                if (writeTimestamped)
                {
                    bool writeStamp = true;
                    if (skipTimestampIfUnchanged && !string.IsNullOrEmpty(existingLatest))
                    {
                        try
                        {
                            writeStamp = !SettingsJsonPersistence.CanonicalEquals(JObject.Parse(existingLatest), root);
                        }
                        catch
                        {
                            writeStamp = true;
                        }
                    }

                    if (writeStamp)
                    {
                        string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                        string filePath = Path.Combine(BackupFolderPath, $"RICS_Settings_Backup_{timestamp}.json");
                        File.WriteAllText(filePath, json);
                        Logger.Message($"[Backup] Settings backup saved to: {filePath}");
                        PruneOldBackups();
                    }
                    else
                    {
                        Logger.Debug("[Backup] Settings JSON unchanged; skipped timestamped backup.");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"[Backup] Error saving settings backup: {ex.Message}");
            }
        }

        /// <summary>
        /// Updates RICS_Settings_LatestBackup.json only (no timestamped rotation).
        /// Used from WriteSettings for token / live-chat persistence.
        /// </summary>
        public static void UpdateLatestSettingsBackup(CAPChatInteractiveSettings settings)
        {
            SaveSettingsBackup(settings, writeTimestamped: false, skipTimestampIfUnchanged: false);
        }

        /// <summary>
        /// Keeps only the 5 most recent timestamped backup files (deletes older ones).
        /// LatestBackup.json is never deleted.
        /// Called automatically after every successful save.
        /// </summary>
        private static void PruneOldBackups()
        {
            try
            {
                if (string.IsNullOrEmpty(BackupFolderPath) || !Directory.Exists(BackupFolderPath))
                    return;

                var backupFiles = Directory.GetFiles(BackupFolderPath, "RICS_Settings_Backup_*.json")
                    .OrderByDescending(f => f)  // Timestamped filenames sort lexicographically descending for newest-first
                    .ToList();

                // Keep only the newest N, delete the rest
                for (int i = MaxTimestampedSettingsBackups; i < backupFiles.Count; i++)
                {
                    try
                    {
                        File.Delete(backupFiles[i]);
                        Logger.Debug($"[Backup] Pruned old backup: {Path.GetFileName(backupFiles[i])}");
                    }
                    catch (Exception delEx)
                    {
                        Logger.Warning($"[Backup] Failed to delete old backup {Path.GetFileName(backupFiles[i])}: {delEx.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Warning($"[Backup] Error pruning old backups: {ex.Message}");
            }
        }

        /// <summary>
        /// Loads the most recent settings backup (RICS_Settings_LatestBackup.json).
        /// Returns null if file missing or corrupted (caller handles UI message).
        /// </summary>
        public static CAPChatInteractiveSettings LoadLatestSettingsBackup()
        {
            try
            {
                JObject root = LoadLatestSettingsBackupJObject();
                if (root == null)
                    return null;

                var backup = SettingsFromSnapshot(root);
                if (backup == null)
                {
                    Logger.Error("[Backup] Deserialization returned null — backup may be corrupted or from incompatible version.");
                    return null;
                }

                Logger.Message("[Backup] Successfully loaded settings from JSON backup.");
                return backup;
            }
            catch (Exception ex)
            {
                Logger.Error($"[Backup] Error loading settings backup: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Reads Latest backup as JObject (no ModSettings allocation). Null if missing/empty.
        /// </summary>
        public static JObject LoadLatestSettingsBackupJObject()
        {
            try
            {
                string latestPath = GetLatestSettingsBackupPath();
                if (!File.Exists(latestPath))
                    return null;

                string jsonContent = File.ReadAllText(latestPath);
                if (string.IsNullOrWhiteSpace(jsonContent))
                {
                    Logger.Warning("[Backup] Latest backup file is empty.");
                    return null;
                }

                return JObject.Parse(jsonContent);
            }
            catch (Exception ex)
            {
                Logger.Error($"[Backup] Error reading latest settings backup JSON: {ex.Message}");
                return null;
            }
        }

        private static CAPChatInteractiveSettings SettingsFromSnapshot(JObject root)
        {
            if (root == null)
                return null;

            var backup = new CAPChatInteractiveSettings();
            backup.TwitchSettings = root["TwitchSettings"]?.ToObject<StreamServiceSettings>() ?? new StreamServiceSettings();
            backup.YouTubeSettings = root["YouTubeSettings"]?.ToObject<StreamServiceSettings>() ?? new StreamServiceSettings();
            backup.KickSettings = root["KickSettings"]?.ToObject<StreamServiceSettings>() ?? new StreamServiceSettings();
            backup.GlobalSettings = root["GlobalSettings"]?.ToObject<CAPGlobalChatSettings>() ?? new CAPGlobalChatSettings();
            return backup;
        }

        /// <summary>
        /// Applies Latest JSON onto <paramref name="target"/> in place. Returns false if missing/corrupt.
        /// </summary>
        public static bool TryApplyLatestBackupInPlace(CAPChatInteractiveSettings target)
        {
            if (target == null)
                return false;

            var backup = LoadLatestSettingsBackup();
            if (backup == null)
                return false;

            ApplyBackupToSettings(target, backup);
            return true;
        }

        /// <summary>
        /// Applies a deserialized backup onto the live mod settings instance (in-place field copy).
        /// </summary>
        public static void ApplyBackupToCurrentSettings(CAPChatInteractiveSettings backup)
        {
            var current = CAPChatInteractiveMod.Instance?.Settings;
            if (current == null)
            {
                Logger.Error("[Backup] Cannot apply backup — CAPChatInteractiveMod.Instance.Settings is null");
                return;
            }

            ApplyBackupToSettings(current, backup);
        }

        private static void ApplyBackupToSettings(CAPChatInteractiveSettings current, CAPChatInteractiveSettings backup)
        {
            if (backup == null || current == null)
                return;

            try
            {
                // Sidecar / live checkbox is the source of truth for this flag (XML/JSON may disagree).
                bool preferJson = current.GlobalSettings?.PreferJsonOnLoad ?? false;
                current.CopyFrom(backup);
                if (current.GlobalSettings != null)
                {
                    current.GlobalSettings.PreferJsonOnLoad = preferJson;
                    // Backup files from 1.50 still contain modVersion. Chat uses the DLL string.
                    current.GlobalSettings.modVersion = CAPGlobalChatSettings.CompiledModVersion;
                }
                Logger.Message("[Backup] Applied JSON backup to live mod settings (in-place).");
            }
            catch (Exception ex)
            {
                Logger.Error($"[Backup] Error applying backup to current settings: {ex.Message}");
            }
        }
    }
}