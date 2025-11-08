using CAP_ChatInteractive;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using Verse;

public static class CommandSettingsValidator
{
    public static void ValidateAndMigrateCommandSettings()
    {
        try
        {
            string jsonContent = JsonFileManager.LoadFile("CommandSettings.json");
            if (string.IsNullOrEmpty(jsonContent)) return;

            var oldSettings = JsonConvert.DeserializeObject<Dictionary<string, CommandSettings>>(jsonContent);
            if (oldSettings == null) return;

            var newSettings = MigrateOldKeysToCommandNames(oldSettings);

            // Only save if changes were made
            if (SettingsWereMigrated(oldSettings, newSettings))
            {
                string newJson = JsonConvert.SerializeObject(newSettings, Formatting.Indented);
                JsonFileManager.SaveFile("CommandSettings.json", newJson);
                Logger.Message("[CAP] Migrated command settings to use command names as keys");
            }
        }
        catch (Exception ex)
        {
            Logger.Error($"Error validating command settings: {ex}");
        }
    }

    private static Dictionary<string, CommandSettings> MigrateOldKeysToCommandNames(Dictionary<string, CommandSettings> oldSettings)
    {
        var newSettings = new Dictionary<string, CommandSettings>();
        var commandDefs = DefDatabase<ChatCommandDef>.AllDefsListForReading;

        // Create mapping from defName to commandText
        var defNameToCommandName = new Dictionary<string, string>();
        foreach (var def in commandDefs)
        {
            if (!string.IsNullOrEmpty(def.commandText))
            {
                defNameToCommandName[def.defName] = def.commandText.ToLower();
            }
        }

        foreach (var kvp in oldSettings)
        {
            string oldKey = kvp.Key;
            CommandSettings settings = kvp.Value;

            // Check if this is a defName that should be migrated to command name
            if (defNameToCommandName.ContainsKey(oldKey))
            {
                string newKey = defNameToCommandName[oldKey];
                newSettings[newKey] = settings;
                Logger.Debug($"Migrated settings key: '{oldKey}' -> '{newKey}'");
            }
            else if (commandDefs.Any(def => def.commandText?.ToLower() == oldKey.ToLower()))
            {
                // Key is already a command name, keep it
                newSettings[oldKey] = settings;
            }
            else
            {
                // Unknown key - might be from a removed mod, but keep it for safety
                newSettings[oldKey] = settings;
                Logger.Debug($"Keeping unknown settings key: '{oldKey}'");
            }
        }

        // Add any missing commands with default settings
        foreach (var def in commandDefs)
        {
            if (!string.IsNullOrEmpty(def.commandText))
            {
                string commandName = def.commandText.ToLower();
                if (!newSettings.ContainsKey(commandName))
                {
                    newSettings[commandName] = CreateDefaultSettingsForCommand(def);
                    Logger.Debug($"Added missing command settings for: '{commandName}'");
                }
            }
        }

        return newSettings;
    }

    private static bool SettingsWereMigrated(Dictionary<string, CommandSettings> oldSettings, Dictionary<string, CommandSettings> newSettings)
    {
        if (oldSettings.Count != newSettings.Count) return true;

        foreach (var key in oldSettings.Keys)
        {
            if (!newSettings.ContainsKey(key)) return true;

            // Also check if key case changed (HelloWorld -> hello)
            var commandDef = DefDatabase<ChatCommandDef>.AllDefsListForReading
                .FirstOrDefault(def => def.defName == key);

            if (commandDef != null && newSettings.ContainsKey(commandDef.commandText?.ToLower()))
                return true;
        }

        return false;
    }

    private static CommandSettings CreateDefaultSettingsForCommand(ChatCommandDef def)
    {
        return new CommandSettings
        {
            Enabled = def.enabled,
            CooldownSeconds = def.cooldownSeconds,
            PermissionLevel = def.permissionLevel
        };
    }
}