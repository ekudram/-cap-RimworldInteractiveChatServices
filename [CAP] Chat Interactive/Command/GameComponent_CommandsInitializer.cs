// GameComponent_CommandsInitializer.cs
// Copyright (c) Captolamia. All rights reserved.
// Licensed under the AGPLv3 License. See LICENSE file in the project root for full license information.
//
// Initializes chat commands when a game is loaded or started.
using Newtonsoft.Json;
using RimWorld;
using System;
using System.Collections.Generic;
using Verse;

namespace CAP_ChatInteractive
{
    public class GameComponent_CommandsInitializer : GameComponent
    {
        private bool commandsInitialized = false;

        public GameComponent_CommandsInitializer(Game game) { }

        public override void LoadedGame()
        {
            InitializeCommands();
        }

        public override void StartedNewGame()
        {
            InitializeCommands();
        }

        public override void GameComponentTick()
        {
            // Initialize on first tick to ensure all defs are loaded
            if (!commandsInitialized && Current.ProgramState == ProgramState.Playing)
            {
                InitializeCommands();
            }
        }
        private void InitializeCommands()
        {
            if (!commandsInitialized)
            {
                Logger.Debug("Initializing commands via GameComponent...");

                // Add debug logging to see defs
                var totalDefs = DefDatabase<Def>.AllDefsListForReading.Count;
                var commandDefs = DefDatabase<ChatCommandDef>.AllDefsListForReading;
                Logger.Debug($"Total defs: {totalDefs}, ChatCommandDefs: {commandDefs.Count}");

                // Initialize settings first
                InitializeCommandSettings();

                // Then register commands
                RegisterDefCommands();

                // Ensure raid settings are properly initialized
                EnsureRaidSettingsInitialized();

                commandsInitialized = true;
                Logger.Message("[CAP] Commands initialized successfully");
            }
        }

        private void InitializeCommandSettings()
        {
            Logger.Debug("Initializing command settings...");

            // Validate and migrate existing settings first
            CommandSettingsValidator.ValidateAndMigrateCommandSettings();

            // Then proceed with normal loading
            if (!LoadCommandSettingsFromJson())
            {
                CreateDefaultCommandSettings();
                SaveCommandSettingsToJson();
            }

            Logger.Message($"[CAP] Command settings initialized");
        }

        private void CreateDefaultCommandSettings()
        {
            var commandDefs = DefDatabase<ChatCommandDef>.AllDefsListForReading;
            var defaultSettings = new Dictionary<string, CommandSettings>();

            foreach (var commandDef in commandDefs)
            {
                string commandName = commandDef.commandText?.ToLower() ?? commandDef.defName.ToLower();
                defaultSettings[commandName] = new CommandSettings
                {
                    Enabled = commandDef.enabled,
                    CooldownSeconds = commandDef.cooldownSeconds,
                    PermissionLevel = commandDef.permissionLevel
                };
            }

            // Save the default settings
            string json = JsonConvert.SerializeObject(defaultSettings, Formatting.Indented);
            JsonFileManager.SaveFile("CommandSettings.json", json);
        }


        private bool LoadCommandSettingsFromJson()
        {
            string jsonContent = JsonFileManager.LoadFile("CommandSettings.json");
            if (string.IsNullOrEmpty(jsonContent))
                return false;

            try
            {
                var loadedSettings = JsonConvert.DeserializeObject<Dictionary<string, CommandSettings>>(jsonContent);
                // Store settings for later use
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error($"Error loading command settings JSON: {ex.Message}");
                return false;
            }
        }

        private void SaveCommandSettingsToJson()
        {
            try
            {
                var commandDefs = DefDatabase<ChatCommandDef>.AllDefsListForReading;
                var settingsToSave = new Dictionary<string, CommandSettings>();

                foreach (var commandDef in commandDefs)
                {
                    string commandName = commandDef.commandText?.ToLower() ?? commandDef.defName.ToLower();
                    settingsToSave[commandName] = new CommandSettings
                    {
                        Enabled = commandDef.enabled,
                        CooldownSeconds = commandDef.cooldownSeconds,
                        PermissionLevel = commandDef.permissionLevel
                        // Add other default settings as needed
                    };
                }

                string json = JsonConvert.SerializeObject(settingsToSave, Formatting.Indented);
                JsonFileManager.SaveFile("CommandSettings.json", json);
                Logger.Debug("Saved default command settings to JSON");
            }
            catch (Exception ex)
            {
                Logger.Error($"Error saving command settings: {ex}");
            }
        }

        private void RegisterDefCommands()
        {
            var defs = DefDatabase<ChatCommandDef>.AllDefsListForReading;
            Logger.Debug($"Registering {defs.Count} commands from Defs...");

            foreach (var commandDef in defs)
            {
                commandDef.RegisterCommand();
            }
        }

        private void EnsureRaidSettingsInitialized()
        {
            try
            {
                var raidSettings = CommandSettingsManager.GetSettings("raid"); // CORRECT

                // Initialize raid-specific lists if they're null or empty
                if (raidSettings.AllowedRaidTypes == null || raidSettings.AllowedRaidTypes.Count == 0)
                {
                    raidSettings.AllowedRaidTypes = new List<string> {
                "standard", "drop", "dropcenter", "dropedge", "dropchaos",
                "dropgroups", "mech", "mechcluster", "manhunter", "infestation",
                "water", "wateredge"
            };
                }

                if (raidSettings.AllowedRaidStrategies == null || raidSettings.AllowedRaidStrategies.Count == 0)
                {
                    raidSettings.AllowedRaidStrategies = new List<string> {
                "default", "immediate", "smart", "sappers", "breach",
                "breachsmart", "stage", "siege"
            };
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error ensuring raid settings are initialized: {ex}");
            }
        }
    }
}