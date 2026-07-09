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

                // Ensure custom per-command settings defaults from Defs (populates CustomData)
                EnsureCustomSettingsDefaults();

                // Then register commands
                RegisterDefCommands();

                // Ensure raid settings are properly initialized
                EnsureRaidSettingsInitialized();

                // Seed passion command CustomData from global values on first use / migration (one-time copy of tuned numbers).
                EnsurePassionSettingsMigrated();

                // Same for surgery (checkboxes + costs).
                EnsureSurgerySettingsMigrated();

                // Shuffle Childhood (single wager cost)
                EnsureShuffleChildhoodSettingsMigrated();

                // Shuffle Adulthood (single wager cost)
                EnsureShuffleAdulthoodSettingsMigrated();

                commandsInitialized = true;
                Logger.Message("Commands initialized successfully");
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

            Logger.Message($"=== Command settings initialized ===");
        }

        /// <summary>
        /// For every ChatCommandDef that declares &lt;customSettings&gt;, ensure the corresponding
        /// CommandSettings has the keys populated with schema defaults (stored in CustomData).
        /// Safe to call multiple times; does not overwrite existing values.
        /// </summary>
        private void EnsureCustomSettingsDefaults()
        {
            try
            {
                string jsonContent = JsonFileManager.LoadFile("CommandSettings.json");
                var current = string.IsNullOrEmpty(jsonContent)
                    ? new Dictionary<string, CommandSettings>()
                    : (JsonConvert.DeserializeObject<Dictionary<string, CommandSettings>>(jsonContent) ?? new Dictionary<string, CommandSettings>());

                bool changed = false;
                foreach (var def in DefDatabase<ChatCommandDef>.AllDefsListForReading)
                {
                    if (string.IsNullOrEmpty(def.commandText) || def.CustomData == null || def.CustomData.Count == 0)
                        continue;

                    string key = def.commandText.ToLowerInvariant();
                    if (!current.TryGetValue(key, out var s))
                    {
                        s = new CommandSettings { Enabled = def.enabled, CooldownSeconds = def.cooldownSeconds, PermissionLevel = def.permissionLevel, useCommandCooldown = def.useCommandCooldown };
                        // PermissionLevel starts from Def but can be overridden later by user in Command Editor
                        current[key] = s;
                    }
                    s.EnsureCustomDefaults(def.CustomData);
                    changed = true;
                }

                if (changed)
                {
                    JsonFileManager.SaveFile("CommandSettings.json", JsonConvert.SerializeObject(current, Formatting.Indented));
                    Logger.Message("Ensured custom settings defaults for commands declaring them");
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error ensuring custom settings defaults: {ex}");
            }
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
                        bool isNew = !currentSettings.ContainsKey(commandName);
                        if (isNew)
                        {
                            currentSettings[commandName] = new CommandSettings
                            {
                                Enabled = def.enabled,
                                CooldownSeconds = def.cooldownSeconds,
                                PermissionLevel = def.permissionLevel,
                                useCommandCooldown = def.useCommandCooldown
                            };
                            settingsChanged = true;
                            Logger.Message($"FORCE ADDED missing command: '{commandName}'");
                        }

                        // Ensure custom settings defaults (from XML <CustomData>) are present
                        var settings = currentSettings[commandName];
                        if (def.CustomData != null && def.CustomData.Count > 0)
                        {
                            settings.EnsureCustomDefaults(def.CustomData);
                            if (isNew) settingsChanged = true;
                        }
                    }
                }

                // Save if changes were made
                if (settingsChanged)
                {
                    string newJson = JsonConvert.SerializeObject(currentSettings, Formatting.Indented);
                    JsonFileManager.SaveFile("CommandSettings.json", newJson);
                    Logger.Message("Added missing commands to settings");
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
            // Logger.Debug($"Registering {defs.Count} commands from Defs...");

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

        /// <summary>
        /// One-time migration helper: copy current global passion values into the "passion" command's CustomData
        /// (if the keys are not yet present or still at XML defaults). This lets users keep their tuned numbers
        /// after the passion settings were moved out of CAPGlobalChatSettings.
        /// </summary>
        private void EnsurePassionSettingsMigrated()
        {
            try
            {
                var global = CAPChatInteractiveMod.Instance.Settings.GlobalSettings;
                var passionSettings = CommandSettingsManager.GetSettings("passion");
                if (passionSettings == null) return;

                passionSettings.EnsureCustomDefaults(DefDatabase<ChatCommandDef>.GetNamed("Passion", false)?.CustomData);

                // Only overwrite if the value is still the XML default (heuristic: compare to known defaults or empty).
                // Simpler: always set from global on init if the custom value matches the schema default (first run after migration).
                // To keep it non-destructive for people who already edited in the new UI, only seed when the key is missing or default.
                var cd = passionSettings.GetCustom<string>("minPassionWager", "");
                if (string.IsNullOrEmpty(cd) || cd == "500")
                {
                    passionSettings.SetCustom("minPassionWager", global.MinPassionWager);
                    passionSettings.SetCustom("maxPassionWager", global.MaxPassionWager);
                    passionSettings.SetCustom("passionWagerBonusPer100", global.PassionWagerBonusPer100);
                    passionSettings.SetCustom("maxPassionWagerBonus", global.MaxPassionWagerBonus);
                    passionSettings.SetCustom("basePassionSuccessChance", global.BasePassionSuccessChance);
                    passionSettings.SetCustom("maxPassionSuccessChance", global.MaxPassionSuccessChance);
                    passionSettings.SetCustom("criticalSuccessRatio", global.CriticalSuccessRatio);
                    passionSettings.SetCustom("maxCriticalSuccessChance", global.MaxCriticalSuccessChance);
                    passionSettings.SetCustom("criticalFailBaseChance", global.CriticalFailBaseChance);
                    passionSettings.SetCustom("criticalFailReductionFactor", global.CriticalFailReductionFactor);
                    passionSettings.SetCustom("minCriticalFailChance", global.MinCriticalFailChance);
                    passionSettings.SetCustom("critSuccessUpgradeVsNewChance", global.CritSuccessUpgradeVsNewChance);
                    passionSettings.SetCustom("critFailLoseVsWrongChance", global.CritFailLoseVsWrongChance);
                    passionSettings.SetCustom("targetedCritFailAffectTargetChance", global.TargetedCritFailAffectTargetChance);
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error migrating passion settings from global: {ex.Message}");
            }
        }

        /// <summary>
        /// One-time migration for surgery: copy global surgery costs/allows into the "surgery" command's CustomData.
        /// </summary>
        private void EnsureSurgerySettingsMigrated()
        {
            try
            {
                var global = CAPChatInteractiveMod.Instance.Settings.GlobalSettings;
                var s = CommandSettingsManager.GetSettings("surgery");
                if (s == null) return;

                var def = DefDatabase<ChatCommandDef>.GetNamed("Surgery", false);
                if (def?.CustomData != null && def.CustomData.Count > 0)
                    s.EnsureCustomDefaults(def.CustomData);

                // Seed if still at default values (first migration)
                if (s.GetCustom<string>("genderSwapCost", "") == "1000" || string.IsNullOrEmpty(s.GetCustom<string>("genderSwapCost", "")))
                {
                    s.SetCustom("allowGenderSwap", global.SurgeryAllowGenderSwap);
                    s.SetCustom("genderSwapCost", global.SurgeryGenderSwapCost);
                    s.SetCustom("allowBodyChange", global.SurgeryAllowBodyChange);
                    s.SetCustom("bodyChangeCost", global.SurgeryBodyChangeCost);
                    s.SetCustom("allowSterilize", global.SurgeryAllowSterilize);
                    s.SetCustom("sterilizeCost", global.SurgerySterilizeCost);
                    s.SetCustom("allowIUD", global.SurgeryAllowIUD);
                    s.SetCustom("iudCost", global.SurgeryIUDCost);
                    s.SetCustom("allowVasReverse", global.SurgeryAllowVasReverse);
                    s.SetCustom("vasReverseCost", global.SurgeryVasReverseCost);
                    s.SetCustom("allowTerminate", global.SurgeryAllowTerminate);
                    s.SetCustom("terminateCost", global.SurgeryTerminateCost);
                    s.SetCustom("allowHemogen", global.SurgeryAllowHemogen);
                    s.SetCustom("hemogenCost", global.SurgeryHemogenCost);
                    s.SetCustom("allowTransfusion", global.SurgeryAllowTransfusion);
                    s.SetCustom("transfusionCost", global.SurgeryTransfusionCost);
                    s.SetCustom("allowMiscBiotech", global.SurgeryAllowMiscBiotech);
                    s.SetCustom("miscBiotechCost", global.SurgeryMiscBiotechCost);
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error migrating surgery settings from global: {ex.Message}");
            }
        }

        /// <summary>
        /// One-time migration for Shuffle Childhood: copy global ChildhoodWager into the command's CustomData.
        /// </summary>
        private void EnsureShuffleChildhoodSettingsMigrated()
        {
            try
            {
                var global = CAPChatInteractiveMod.Instance.Settings.GlobalSettings;
                var s = CommandSettingsManager.GetSettings("shufflechildhood");
                if (s == null) return;

                var def = DefDatabase<ChatCommandDef>.GetNamed("ShuffleChildhood", false);
                if (def?.CustomData != null && def.CustomData.Count > 0)
                    s.EnsureCustomDefaults(def.CustomData);

                // Seed if still at default
                if (s.GetCustom<string>("childhoodWager", "") == "1000" || string.IsNullOrEmpty(s.GetCustom<string>("childhoodWager", "")))
                {
                    s.SetCustom("childhoodWager", global.ChildhoodWager);
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error migrating shuffle childhood settings from global: {ex.Message}");
            }
        }

        /// <summary>
        /// One-time migration for Shuffle Adulthood: copy global AdulthoodWager into the command's CustomData.
        /// </summary>
        private void EnsureShuffleAdulthoodSettingsMigrated()
        {
            try
            {
                var global = CAPChatInteractiveMod.Instance.Settings.GlobalSettings;
                var s = CommandSettingsManager.GetSettings("shuffleadulthood");
                if (s == null) return;

                var def = DefDatabase<ChatCommandDef>.GetNamed("ShuffleAdulthood", false);
                if (def?.CustomData != null && def.CustomData.Count > 0)
                    s.EnsureCustomDefaults(def.CustomData);

                // Seed if still at default
                if (s.GetCustom<string>("adulthoodWager", "") == "1000" || string.IsNullOrEmpty(s.GetCustom<string>("adulthoodWager", "")))
                {
                    s.SetCustom("adulthoodWager", global.AdulthoodWager);
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error migrating shuffle adulthood settings from global: {ex.Message}");
            }
        }

        
        private void ValidateAndFixJsonPermissions()
        {
            try
            {
                Logger.Message("Validating JSON permissions against XML Defs...");

                // Load current JSON
                string jsonContent = JsonFileManager.LoadFile("CommandSettings.json");
                if (string.IsNullOrEmpty(jsonContent))
                {
                    Logger.Warning("No CommandSettings.json found, will be created from XML");
                    return;
                }

                var currentSettings = JsonConvert.DeserializeObject<Dictionary<string, CommandSettings>>(jsonContent);
                if (currentSettings == null)
                {
                    Logger.Error("CommandSettings.json is empty or invalid");
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
                        // Do NOT force PermissionLevel from Def here anymore.
                        // This allows users to change a command's permission level (e.g. subscriber-only / paid access)
                        // via the Command Editor. Defaults from XML are still applied at first creation.

                        // Only ensure other XML values like cooldown if not set
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
                    Logger.Message("Fixed JSON permissions to match XML Defs");
                }
                else
                {
                    Logger.Message("All JSON permissions match XML Defs");
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