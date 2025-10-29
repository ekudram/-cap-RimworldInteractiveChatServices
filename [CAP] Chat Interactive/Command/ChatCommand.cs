// ChatCommand.cs
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
        public virtual string[] Aliases => Array.Empty<string>();
        public virtual string Description => "No description available";
        public virtual string PermissionLevel => "everyone";
        public virtual int CooldownSeconds => 0;

        public abstract string Execute(ChatMessageWrapper user, string[] args);

        public virtual bool CanExecute(ChatMessageWrapper message)
        {
            // Get the viewer from database for permission checking
            var viewer = Viewers.GetViewer(message.Username);
            if (viewer == null) return false;

            return viewer.HasPermission(PermissionLevel);
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
    // In ChatCommand.cs - Update the CommandSettingsManager class
    public static class CommandSettingsManager
    {
        public static CommandSettings GetSettings(string commandName)
        {
            try
            {
                // Try to get from open dialog first
                var dialog = Find.WindowStack?.WindowOfType<Dialog_CommandManager>();
                if (dialog != null && dialog.commandSettings.ContainsKey(commandName))
                {
                    return dialog.commandSettings[commandName];
                }

                // Fallback: Load directly from JSON
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
                    if (allSettings != null && allSettings.ContainsKey(commandName))
                    {
                        return allSettings[commandName];
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error($"Error loading settings from JSON for {commandName}: {ex}");
                }
            }

            return new CommandSettings();
        }
    }

    // Example commands
    public class HelpCommand : ChatCommand
    {
        public override string Name => "help";
        public override string Description => "Shows available commands";

        public override string Execute(ChatMessageWrapper user, string[] args)
        {
            var availableCommands = ChatCommandProcessor.GetAvailableCommands(user);
            var commandList = string.Join(", ", availableCommands.Select(cmd => $"!{cmd.Name}"));

            return $"Available commands: {commandList}. Use !help <command> for more info.";
        }
    }

    public class PointsCommand : ChatCommand
    {
        public override string Name => "points";
        public override string[] Aliases => new[] { "balance", "coins" };
        public override string Description => "Check your point balance";

        public override string Execute(ChatMessageWrapper user, string[] args)
        {
            // TODO: Integrate with points system
            var points = 100; // Placeholder
            return $"You have {points} points!";
        }
    }
}