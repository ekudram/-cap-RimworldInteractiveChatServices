// GameComponent_CommandsInitializer.cs
// Copyright (c) Captolamia
// This file is part of CAP Chat Interactive (RICS).
// Licensed under the GNU Affero General Public License v3.0 or later.
// See LICENSE.txt in the project root for full license text.
//
// Loads / migrates CommandSettings.json and registers ChatCommandDefs when a game starts.
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
        public bool commandsInitialized;

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
            // First playing tick: ensure all defs are loaded before register.
            if (!commandsInitialized && Current.ProgramState == ProgramState.Playing)
                InitializeCommands();
        }

        public void InitializeCommands()
        {
            if (commandsInitialized)
                return;

            ValidateAndFixJsonPermissions();
            CAP_InitializeCommandSettings();
            EnsureCustomSettingsDefaults();
            RegisterDefCommands();
            EnsureRaidSettingsInitialized();

            // Migrations: global CAP settings → per-command CustomData in CommandSettings.json.
            // Keep until older installs have had time to upgrade (do not remove yet).
            EnsurePassionSettingsMigrated();
            EnsureSurgerySettingsMigrated();
            EnsureShuffleChildhoodSettingsMigrated();
            EnsureShuffleAdulthoodSettingsMigrated();

            commandsInitialized = true;
        }

        public void ResetCommands()
        {
            commandsInitialized = false;
            InitializeCommands();
        }

        private void CAP_InitializeCommandSettings()
        {
            ForceAddMissingCommands();
        }

        /// <summary>
        /// For every ChatCommandDef with CustomData / metadata, ensure CommandSettings.json has keys
        /// and refreshed label/description/pricelist flags. Does not overwrite existing custom values.
        /// </summary>
        private void EnsureCustomSettingsDefaults()
        {
            try
            {
                string jsonContent = JsonFileManager.LoadFile("CommandSettings.json");
                var current = string.IsNullOrEmpty(jsonContent)
                    ? new Dictionary<string, CommandSettings>()
                    : (JsonConvert.DeserializeObject<Dictionary<string, CommandSettings>>(jsonContent)
                       ?? new Dictionary<string, CommandSettings>());

                bool changed = false;
                foreach (var def in DefDatabase<ChatCommandDef>.AllDefsListForReading)
                {
                    if (string.IsNullOrEmpty(def.commandText))
                        continue;

                    string key = def.commandText.ToLowerInvariant();
                    if (!current.TryGetValue(key, out var s))
                    {
                        s = new CommandSettings
                        {
                            Enabled = def.enabled,
                            CooldownSeconds = def.cooldownSeconds,
                            PermissionLevel = def.permissionLevel,
                            useCommandCooldown = def.useCommandCooldown
                        };
                        current[key] = s;
                        changed = true;
                    }

                    string prevLabel = s.Label;
                    string prevDesc = s.CommandDescription;
                    bool prevExclude = s.ExcludeFromPricelist;
                    s.ApplyDefMetadata(def);
                    if (prevLabel != s.Label || prevDesc != s.CommandDescription || prevExclude != s.ExcludeFromPricelist)
                        changed = true;

                    if (def.CustomData != null && def.CustomData.Count > 0)
                    {
                        s.EnsureCustomDefaults(def.CustomData);
                        changed = true;
                    }
                }

                if (changed)
                {
                    JsonFileManager.SaveFile(
                        "CommandSettings.json",
                        JsonConvert.SerializeObject(current, Formatting.Indented));
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"[CommandsInitializer] Error ensuring custom settings defaults: {ex}");
            }
        }

        private void ForceAddMissingCommands()
        {
            try
            {
                string jsonContent = JsonFileManager.LoadFile("CommandSettings.json");
                var currentSettings = string.IsNullOrEmpty(jsonContent)
                    ? new Dictionary<string, CommandSettings>()
                    : (JsonConvert.DeserializeObject<Dictionary<string, CommandSettings>>(jsonContent)
                       ?? new Dictionary<string, CommandSettings>());

                bool settingsChanged = false;

                foreach (var def in DefDatabase<ChatCommandDef>.AllDefsListForReading)
                {
                    if (string.IsNullOrEmpty(def.commandText))
                        continue;

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
                    }

                    var settings = currentSettings[commandName];

                    string prevLabel = settings.Label;
                    string prevDesc = settings.CommandDescription;
                    bool prevExclude = settings.ExcludeFromPricelist;
                    settings.ApplyDefMetadata(def);
                    if (prevLabel != settings.Label
                        || prevDesc != settings.CommandDescription
                        || prevExclude != settings.ExcludeFromPricelist)
                        settingsChanged = true;

                    if (def.CustomData != null && def.CustomData.Count > 0)
                    {
                        settings.EnsureCustomDefaults(def.CustomData);
                        if (isNew)
                            settingsChanged = true;
                    }
                }

                if (settingsChanged)
                {
                    JsonFileManager.SaveFile(
                        "CommandSettings.json",
                        JsonConvert.SerializeObject(currentSettings, Formatting.Indented));
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"[CommandsInitializer] Error in ForceAddMissingCommands: {ex}");
            }
        }

        private void RegisterDefCommands()
        {
            foreach (var commandDef in DefDatabase<ChatCommandDef>.AllDefsListForReading)
                commandDef.RegisterCommand();
        }

        private void EnsureRaidSettingsInitialized()
        {
            try
            {
                var raidSettings = CommandSettingsManager.GetSettings("raid");
                if (raidSettings == null)
                    return;

                if (raidSettings.AllowedRaidTypes == null || raidSettings.AllowedRaidTypes.Count == 0)
                {
                    raidSettings.AllowedRaidTypes = new List<string>
                    {
                        "standard", "drop", "dropcenter", "dropedge", "dropchaos",
                        "dropgroups", "mech", "mechcluster", "manhunter", "infestation",
                        "water", "wateredge"
                    };
                }

                if (raidSettings.AllowedRaidStrategies == null || raidSettings.AllowedRaidStrategies.Count == 0)
                {
                    raidSettings.AllowedRaidStrategies = new List<string>
                    {
                        "default", "immediate", "smart", "sappers", "breach",
                        "breachsmart", "stage", "siege"
                    };
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"[CommandsInitializer] Error ensuring raid settings: {ex}");
            }
        }

        /// <summary>
        /// Migration: copy global passion wager/chance values into passion command CustomData
        /// when keys are missing or still at XML defaults. Keeps tuned numbers for older installs
        /// after settings moved out of CAPGlobalChatSettings.
        /// </summary>
        private void EnsurePassionSettingsMigrated()
        {
            try
            {
                var global = CAPChatInteractiveMod.Instance?.Settings?.GlobalSettings;
                if (global == null)
                    return;

                var passionSettings = CommandSettingsManager.GetSettings("passion");
                if (passionSettings == null)
                    return;

                passionSettings.EnsureCustomDefaults(
                    DefDatabase<ChatCommandDef>.GetNamed("Passion", false)?.CustomData);

                // Seed only when still at default / empty so user edits in the new UI are kept.
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
                Logger.Error($"[CommandsInitializer] Error migrating passion settings: {ex.Message}");
            }
        }

        /// <summary>
        /// Migration: copy global surgery costs/allows into surgery command CustomData when still default.
        /// </summary>
        private void EnsureSurgerySettingsMigrated()
        {
            try
            {
                var global = CAPChatInteractiveMod.Instance?.Settings?.GlobalSettings;
                if (global == null)
                    return;

                var s = CommandSettingsManager.GetSettings("surgery");
                if (s == null)
                    return;

                var def = DefDatabase<ChatCommandDef>.GetNamed("Surgery", false);
                if (def?.CustomData != null && def.CustomData.Count > 0)
                    s.EnsureCustomDefaults(def.CustomData);

                if (s.GetCustom<string>("genderSwapCost", "") == "1000"
                    || string.IsNullOrEmpty(s.GetCustom<string>("genderSwapCost", "")))
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
                Logger.Error($"[CommandsInitializer] Error migrating surgery settings: {ex.Message}");
            }
        }

        /// <summary>
        /// Migration: copy global ChildhoodWager into shufflechildhood CustomData when still default.
        /// </summary>
        private void EnsureShuffleChildhoodSettingsMigrated()
        {
            try
            {
                var global = CAPChatInteractiveMod.Instance?.Settings?.GlobalSettings;
                if (global == null)
                    return;

                var s = CommandSettingsManager.GetSettings("shufflechildhood");
                if (s == null)
                    return;

                var def = DefDatabase<ChatCommandDef>.GetNamed("ShuffleChildhood", false);
                if (def?.CustomData != null && def.CustomData.Count > 0)
                    s.EnsureCustomDefaults(def.CustomData);

                if (s.GetCustom<string>("childhoodWager", "") == "1000"
                    || string.IsNullOrEmpty(s.GetCustom<string>("childhoodWager", "")))
                {
                    s.SetCustom("childhoodWager", global.ChildhoodWager);
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"[CommandsInitializer] Error migrating shuffle childhood settings: {ex.Message}");
            }
        }

        /// <summary>
        /// Migration: copy global AdulthoodWager into shuffleadulthood CustomData when still default.
        /// </summary>
        private void EnsureShuffleAdulthoodSettingsMigrated()
        {
            try
            {
                var global = CAPChatInteractiveMod.Instance?.Settings?.GlobalSettings;
                if (global == null)
                    return;

                var s = CommandSettingsManager.GetSettings("shuffleadulthood");
                if (s == null)
                    return;

                var def = DefDatabase<ChatCommandDef>.GetNamed("ShuffleAdulthood", false);
                if (def?.CustomData != null && def.CustomData.Count > 0)
                    s.EnsureCustomDefaults(def.CustomData);

                if (s.GetCustom<string>("adulthoodWager", "") == "1000"
                    || string.IsNullOrEmpty(s.GetCustom<string>("adulthoodWager", "")))
                {
                    s.SetCustom("adulthoodWager", global.AdulthoodWager);
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"[CommandsInitializer] Error migrating shuffle adulthood settings: {ex.Message}");
            }
        }

        /// <summary>
        /// Light JSON pass: fill missing cooldowns from Def when JSON has 0.
        /// Does not overwrite user PermissionLevel (Command Editor owns that).
        /// </summary>
        private void ValidateAndFixJsonPermissions()
        {
            try
            {
                string jsonContent = JsonFileManager.LoadFile("CommandSettings.json");
                if (string.IsNullOrEmpty(jsonContent))
                    return;

                var currentSettings = JsonConvert.DeserializeObject<Dictionary<string, CommandSettings>>(jsonContent);
                if (currentSettings == null)
                {
                    Logger.Error("[CommandsInitializer] CommandSettings.json is empty or invalid");
                    return;
                }

                bool fixedAny = false;
                foreach (var def in DefDatabase<ChatCommandDef>.AllDefsListForReading)
                {
                    if (string.IsNullOrEmpty(def.commandText))
                        continue;

                    string commandKey = def.commandText.ToLowerInvariant();
                    if (!currentSettings.TryGetValue(commandKey, out var settings))
                        continue;

                    // PermissionLevel is user-editable; only backfill cooldown from XML when unset.
                    if (settings.CooldownSeconds == 0 && def.cooldownSeconds > 0)
                    {
                        settings.CooldownSeconds = def.cooldownSeconds;
                        fixedAny = true;
                    }
                }

                if (fixedAny)
                {
                    JsonFileManager.SaveFile(
                        "CommandSettings.json",
                        JsonConvert.SerializeObject(currentSettings, Formatting.Indented));
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"[CommandsInitializer] Error validating JSON permissions: {ex}");
            }
        }

        [DebugAction("CAP", "Fix JSON Permissions", allowedGameStates = AllowedGameStates.Playing)]
        public static void DebugFixJsonPermissions()
        {
            try
            {
                var comp = Current.Game?.GetComponent<GameComponent_CommandsInitializer>();
                if (comp == null)
                    return;

                typeof(GameComponent_CommandsInitializer)
                    .GetMethod(
                        "ValidateAndFixJsonPermissions",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                    ?.Invoke(comp, null);

                Messages.Message("JSON permissions fixed to match XML Defs", MessageTypeDefOf.TaskCompletion);
            }
            catch (Exception ex)
            {
                Logger.Error($"[CommandsInitializer] Error in debug action: {ex}");
                Messages.Message($"Error fixing permissions: {ex.Message}", MessageTypeDefOf.NegativeEvent);
            }
        }
    }
}
