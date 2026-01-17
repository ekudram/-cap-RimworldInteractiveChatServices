// ChatCommand.cs
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
// Base class and utilities for chat commands
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace CAP_ChatInteractive
{
    // In ChatCommand.cs - Add these methods to the ChatCommand class

    public abstract class ChatCommand
    {
        public abstract string Name { get; }
        public virtual string Alias
        {
            get
            {
                var settings = GetCommandSettings();
                //Logger.Debug($"  -> Alias lookup for {Name}: settings.CommandAlias = '{settings.CommandAlias}'");

                if (!string.IsNullOrEmpty(settings.CommandAlias))
                {
                    string alias = settings.CommandAlias.Trim().ToLowerInvariant();
                    //Logger.Debug($"  -> Returning alias: '{alias}'");
                    return alias;
                }

                //Logger.Debug($"  -> No alias found, returning null");
                return null;
            }
        }
        public virtual string Description => "No description available";
        // Make PermissionLevel virtual so it can use settings
        public virtual string PermissionLevel
        {
            get
            {
                // First check if this is a Def-based command
                if (this is DefBasedChatCommand defCommand)
                {
                    return defCommand.PermissionLevel;
                }

                // Fall back to JSON settings for non-Def commands
                var settings = GetCommandSettings();
                return settings?.PermissionLevel ?? "everyone";
            }
        }
        public virtual int CooldownSeconds => 0;

        public abstract string Execute(ChatMessageWrapper user, string[] args);

        public virtual bool CanExecute(ChatMessageWrapper message)
        {
            // Get the viewer from database for permission checking
            var viewer = Viewers.GetViewer(message);
            if (viewer == null) return false;

            // Use the PermissionLevel property which now gets from JSON settings
            string requiredPermission = PermissionLevel;
            // Logger.Debug($"Permission check for {Name}: viewer '{message.Username}' needs '{requiredPermission}'");

            return viewer.HasPermission(requiredPermission);
        }

        // Get command settings from the settings manager
        public virtual CommandSettings GetCommandSettings()
        {
            return CommandSettingsManager.GetSettings(Name);
        }

        // Check if command is enabled in settings
        public bool IsEnabled()
        {

            var settings = GetCommandSettings();
            Logger.Debug($"=== Command '{Name}' Enabled Check ===");
            Logger.Debug($"Settings found: {settings != null}");
            Logger.Debug($"Settings.Enabled: {settings?.Enabled}");
            Logger.Debug($"Final result: {settings?.Enabled ?? true}");

            return settings?.Enabled ?? true;
        }

        // Public method to get karma emoji - can be used anywhere
        public static string GetKarmaEmoji(int karma)
        {
            if (karma >= 200) return "🦄"; // Legendary good - Unicorn
            if (karma >= 150) return "😇"; // Very high karma - Angel
            if (karma >= 120) return "😊"; // High karma - Happy
            if (karma >= 90) return "🙂";  // Good karma - Smiley
            if (karma >= 80) return "☺️";  // Neutral to good - Smiling
            if (karma >= 70) return "😐";  // Slightly low - Neutral
            if (karma >= 50) return "😕";  // Low - Confused/Unsure
            if (karma >= 30) return "😠";  // Quite low - Angry
            if (karma >= 10) return "👿";  // Very low - Angry devil
            return "💀";                   // Rock bottom - Skull
        }

        // Get karma description along with emoji
        public static string GetKarmaDescription(int karma)
        {
            if (karma >= 200) return "Legendary Good 🦄";
            if (karma >= 150) return "Very High Karma 😇";
            if (karma >= 120) return "High Karma 😊";
            if (karma >= 90) return "Good Karma 🙂";
            if (karma >= 80) return "Neutral to Good ☺️";
            if (karma >= 70) return "Slightly Low 😐";
            if (karma >= 50) return "Low Karma 😕";
            if (karma >= 30) return "Quite Low 😠";
            if (karma >= 10) return "Very Low 👿";
            return "Rock Bottom 💀";
        }
    }

    // Add this static class to manage command settings
    public static class CommandSettingsManager
    {
        public static CommandSettings GetSettings(string commandName)
        {
            try
            {
                //Logger.Debug($"=== GET SETTINGS FOR: '{commandName}' ===");

                // Try to get from open dialog first
                var dialog = Find.WindowStack?.WindowOfType<Dialog_CommandManager>();
                if (dialog != null && dialog.commandSettings.ContainsKey(commandName))
                {
                    //Logger.Debug($"  -> Found in dialog settings: Enabled={dialog.commandSettings[commandName].Enabled}");
                    return dialog.commandSettings[commandName];
                }

                // Fallback: Load directly from JSON
                //Logger.Debug($"  -> Looking in JSON file");
                var jsonSettings = LoadSettingsFromJson(commandName);
                //Logger.Debug($"  -> JSON result: Enabled={jsonSettings.Enabled}");
                return jsonSettings;
            }
            catch (Exception ex)
            {
                Logger.Error($"Error getting settings for {commandName}: {ex}");
                return new CommandSettings();
            }
        }

        // In CommandSettingsManager.cs (add this public static method)

        public static void SaveSettings()
        {
            try
            {
                // Load existing JSON to modify (or create new)
                string json = JsonFileManager.LoadFile("CommandSettings.json");
                var allSettings = string.IsNullOrEmpty(json)
                    ? new Dictionary<string, CommandSettings>()
                    : JsonConvert.DeserializeObject<Dictionary<string, CommandSettings>>(json);

                // Update togglestore (or add if missing)
                string key = "togglestore".ToLowerInvariant();
                if (!allSettings.ContainsKey(key))
                    allSettings[key] = new CommandSettings();

                // No need to copy everything - but since we modified GetSettings() cache/dialog, assume it's in-memory
                // For simplicity: re-save the whole thing (small file)

                string newJson = JsonConvert.SerializeObject(allSettings, Formatting.Indented);
                JsonFileManager.SaveFile("CommandSettings.json", newJson);

                Logger.Debug("[CAP] CommandSettings.json saved (store toggle updated)");
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to save CommandSettings: {ex}");
            }
        }

        private static CommandSettings LoadSettingsFromJson(string commandName)
        {
            // Load from JSON file directly
            string json = JsonFileManager.LoadFile("CommandSettings.json");
            if (!string.IsNullOrEmpty(json))
            {
                try
                {
                    var allSettings = JsonConvert.DeserializeObject<Dictionary<string, CommandSettings>>(json);

                    if (allSettings != null)
                    {
                        // Try multiple lookup strategies
                        string commandNameLower = commandName.ToLowerInvariant();

                        // 1. Exact match
                        if (allSettings.ContainsKey(commandName))
                        {
                            return allSettings[commandName];
                        }

                        // 2. Case-insensitive match
                        var matchingKey = allSettings.Keys.FirstOrDefault(k =>
                            k.ToLowerInvariant() == commandNameLower);

                        if (matchingKey != null)
                        {
                            return allSettings[matchingKey];
                        }

                        Logger.Debug($"No settings found for '{commandName}' (tried: '{commandName}', case-insensitive)");
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error($"Error loading settings from JSON for {commandName}: {ex}");
                }
            }

            // Return default settings (Enabled = true)
            return new CommandSettings();
        }
    }

    public static class CommandUtility
    {
        public static bool AreStoreCommandsEnabled()
        {
            // We use the settings of any well-known command (e.g. "buy") as the global holder
            // This is simple and reuses your existing JSON infrastructure
            var settings = CAPChatInteractiveMod.Instance.Settings.GlobalSettings; // or "raid", "backpack" etc.
            return settings?.StoreCommandsEnabled ?? true;
        }

        public static string GetStoreStatusText()
        {
            return AreStoreCommandsEnabled()
                ? "Store & interaction commands are **ENABLED**"
                : "Store & interaction commands are **DISABLED** (emergency mode)";
        }
    }

    // Example commands
    public class HelpCommand : ChatCommand
    {
        public override string Name => "help";

        public override string Execute(ChatMessageWrapper messageWrapper, string[] args)
        {
            return $"Github Wiki: https://github.com/ekudram/-cap-RimworldInteractiveChatServices/wiki";
        }
    }
}