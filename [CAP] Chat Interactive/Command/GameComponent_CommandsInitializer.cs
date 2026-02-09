// GameComponent_CommandsInitializer.cs
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
// Initializes chat commands when a game is loaded or started.
using LudeonTK;
using Newtonsoft.Json;
using RimWorld;
using System;
using System.Collections.Generic;
using Verse;

namespace CAP_ChatInteractive
{
    public class GameComponent_CommandsInitializer : GameComponent
    {
        public bool commandsInitialized = false;

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
        public void InitializeCommands()
        {
            if (!commandsInitialized)
            {
                Logger.Debug("Initializing commands via GameComponent...");

                // Validate and fix JSON permissions BEFORE initialization
                ValidateAndFixJsonPermissions();

                // Initialize settings
                CAP_InitializeCommandSettings();

                // Then register commands
                RegisterDefCommands();

                // Ensure raid settings are properly initialized
                EnsureRaidSettingsInitialized();

                commandsInitialized = true;
                Logger.Message("[CAP] Commands initialized successfully");
            }
        }

        public void ResetCommands()
        {
            commandsInitialized = false;
            InitializeCommands();
        }

        private void CAP_InitializeCommandSettings()
        {
            Logger.Message("=== CAP_InitializeCommandSettings called ===");

            // FORCE check for any missing commands and add them
            ForceAddMissingCommands();

            Logger.Message($"=== [CAP] Command settings initialized ===");
        }

        private void ForceAddMissingCommands()
        {
            try
            {
                // Load current settings
                string jsonContent = JsonFileManager.LoadFile("CommandSettings.json");
                var currentSettings = new Dictionary<string, CommandSettings>();

                if (!string.IsNullOrEmpty(jsonContent))
                {
                    currentSettings = JsonConvert.DeserializeObject<Dictionary<string, CommandSettings>>(jsonContent) ?? new Dictionary<string, CommandSettings>();
                }

                bool settingsChanged = false;
                var commandDefs = DefDatabase<ChatCommandDef>.AllDefsListForReading;

                // Check every command def and ensure it exists in settings
                foreach (var def in commandDefs)
                {
                    if (!string.IsNullOrEmpty(def.commandText))
                    {
                        // FIX: Use lowercase consistently
                        string commandName = def.commandText.ToLowerInvariant();
                        if (!currentSettings.ContainsKey(commandName))
                        {
                            currentSettings[commandName] = new CommandSettings
                            {
                                Enabled = def.enabled,
                                CooldownSeconds = def.cooldownSeconds,
                                PermissionLevel = def.permissionLevel,
                                useCommandCooldown = def.useCommandCooldown
                            };
                            settingsChanged = true;
                            Logger.Debug($"FORCE ADDED missing command: '{commandName}'");
                        }
                    }
                }

                // Save if changes were made
                if (settingsChanged)
                {
                    string newJson = JsonConvert.SerializeObject(currentSettings, Formatting.Indented);
                    JsonFileManager.SaveFile("CommandSettings.json", newJson);
                    Logger.Message("[CAP] Added missing commands to settings");
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error in ForceAddMissingCommands: {ex}");
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

        
        private void ValidateAndFixJsonPermissions()
        {
            try
            {
                Logger.Message("[CAP] Validating JSON permissions against XML Defs...");

                // Load current JSON
                string jsonContent = JsonFileManager.LoadFile("CommandSettings.json");
                if (string.IsNullOrEmpty(jsonContent))
                {
                    Logger.Debug("No CommandSettings.json found, will be created from XML");
                    return;
                }

                var currentSettings = JsonConvert.DeserializeObject<Dictionary<string, CommandSettings>>(jsonContent);
                if (currentSettings == null)
                {
                    Logger.Debug("CommandSettings.json is empty or invalid");
                    return;
                }

                bool fixedAny = false;
                var commandDefs = DefDatabase<ChatCommandDef>.AllDefsListForReading;

                foreach (var def in commandDefs)
                {
                    if (string.IsNullOrEmpty(def.commandText))
                        continue;

                    string commandKey = def.commandText.ToLowerInvariant();

                    if (currentSettings.TryGetValue(commandKey, out var settings))
                    {
                        // Check if JSON permission matches XML
                        if (settings.PermissionLevel != def.permissionLevel)
                        {
                            Logger.Debug($"Fixing permission for '{commandKey}': JSON='{settings.PermissionLevel}' -> XML='{def.permissionLevel}'");
                            settings.PermissionLevel = def.permissionLevel;
                            fixedAny = true;
                        }

                        // Also ensure other XML values are set
                        if (settings.CooldownSeconds == 0 && def.cooldownSeconds > 0)
                        {
                            settings.CooldownSeconds = def.cooldownSeconds;
                            fixedAny = true;
                        }
                    }
                }

                if (fixedAny)
                {
                    // Save the fixed JSON
                    string fixedJson = JsonConvert.SerializeObject(currentSettings, Formatting.Indented);
                    JsonFileManager.SaveFile("CommandSettings.json", fixedJson);
                    Logger.Message("[CAP] Fixed JSON permissions to match XML Defs");
                }
                else
                {
                    Logger.Debug("[CAP] All JSON permissions match XML Defs");
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error validating JSON permissions: {ex}");
            }
        }


        [DebugAction("CAP", "Fix JSON Permissions", allowedGameStates = AllowedGameStates.Playing)]
        public static void DebugFixJsonPermissions()
        {
            try
            {
                var comp = Current.Game.GetComponent<GameComponent_CommandsInitializer>();
                if (comp != null)
                {
                    // Call the validation method directly
                    typeof(GameComponent_CommandsInitializer)
                        .GetMethod("ValidateAndFixJsonPermissions",
                            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                        ?.Invoke(comp, null);

                    Messages.Message("JSON permissions fixed to match XML Defs", MessageTypeDefOf.TaskCompletion);
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error in debug action: {ex}");
                Messages.Message($"Error fixing permissions: {ex.Message}", MessageTypeDefOf.NegativeEvent);
            }
        }
    }

}