// ChatCommand.cs
// Copyright (c) Captolamia. All rights reserved.
// Licensed under the AGPLv3 License. See LICENSE file in the project root for full license information.
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
                var settings = GetCommandSettings();
                return settings?.PermissionLevel ?? "everyone"; // Default to everyone if no settings
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
            Logger.Debug($"Permission check for {Name}: viewer '{message.Username}' needs '{requiredPermission}'");

            return viewer.HasPermission(requiredPermission);
        }

        // Get command settings from the settings manager
        public virtual CommandSettings GetCommandSettings()
        {
            return CommandSettingsManager.GetSettings(Name);
        }

        // Check if command is enabled in settings
        public virtual bool IsEnabled()
        {
            var settings = GetCommandSettings();
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
                //Logger.Debug($"=== Settings lookup for: '{commandName}' ===");

                // Try to get from open dialog first
                var dialog = Find.WindowStack?.WindowOfType<Dialog_CommandManager>();
                if (dialog != null && dialog.commandSettings.ContainsKey(commandName))
                {
                    //Logger.Debug($"  -> Found in dialog settings");
                    return dialog.commandSettings[commandName];
                }

                // Fallback: Load directly from JSON
                //Logger.Debug($"  -> Looking in JSON file");
                return LoadSettingsFromJson(commandName);
            }
            catch (Exception ex)
            {
                Logger.Error($"Error getting settings for {commandName}: {ex}");
                return new CommandSettings(); // Return default settings
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

                    // FIXED: Proper null checking for the debug output
                    if (allSettings != null)
                    {
                       // Logger.Debug($"  -> JSON keys available: {string.Join(", ", allSettings.Keys)}");
                    }
                    else
                    {
                        Logger.Debug($"  -> JSON deserialized to null");
                    }

                    // FIRST: Try exact match
                    if (allSettings != null && allSettings.ContainsKey(commandName))
                    {
                        // Logger.Debug($"  -> Found exact match for '{commandName}'");
                        return allSettings[commandName];
                    }

                    // SECOND: Try to find by defName (case-insensitive)
                    if (allSettings != null)
                    {
                        var matchingKey = allSettings.Keys.FirstOrDefault(k =>
                            string.Equals(k, commandName, StringComparison.OrdinalIgnoreCase));

                        if (matchingKey != null)
                        {
                            // Logger.Debug($"  -> Found case-insensitive match: '{matchingKey}' -> '{commandName}'");
                            return allSettings[matchingKey];
                        }
                    }

                    Logger.Debug($"  -> No match found for '{commandName}'");
                }
                catch (Exception ex)
                {
                    Logger.Error($"Error loading settings from JSON for {commandName}: {ex}");
                }
            }
            else
            {
                Logger.Debug($"  -> JSON file is empty or doesn't exist");
            }

            return new CommandSettings();
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