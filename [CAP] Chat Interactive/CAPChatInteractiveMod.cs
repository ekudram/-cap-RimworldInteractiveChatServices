// CAPChatInteractiveMod.cs
// Copyright (c) Captolamia. All rights reserved.
// Licensed under the AGPLv3 License. See LICENSE file in the project root for full license information.
// Main mod class for [CAP] Chat Interactive RimWorld mod
// Handles initialization, settings, and service management.
// Store, Traits, Weather, and other systems will be initialized when the game starts.
using _CAP__Chat_Interactive.Interfaces;
using _CAP__Chat_Interactive.Utilities;
using Google.Apis.YouTube.v3;
using LudeonTK;
using Newtonsoft.Json;
using RimWorld;
using System;   
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using Verse;

namespace CAP_ChatInteractive
{
    public class CAPChatInteractiveMod : Mod
    {
        public static CAPChatInteractiveMod Instance { get; private set; }
        public CAPChatInteractiveSettings Settings { get; private set; }
        public IAlienCompatibilityProvider AlienProvider { get; private set; }

        // Service managers (we'll create these later)
        private TwitchService _twitchService;
        private YouTubeChatService _youTubeService;


        public CAPChatInteractiveMod(ModContentPack content) : base(content)
        {
            Instance = this;
            Logger.Debug("CAPChatInteractiveMod constructor started");

            Settings = GetSettings<CAPChatInteractiveSettings>();

            // Force GameComponent creation if a game is already running
            if (Current.Game != null && Current.Game.components != null)
            {
                var existingComponent = Current.Game.GetComponent<CAPChatInteractive_GameComponent>();
                if (existingComponent == null)
                {
                    Current.Game.components.Add(new CAPChatInteractive_GameComponent(Current.Game));
                    Logger.Debug("GameComponent added to existing game");
                }
            }

            Logger.Message("[CAP] RICS mod loaded successfully!");
            // Force viewer loading by accessing the All property
            var viewerCount = Viewers.All.Count; // This triggers static constructor
            Logger.Debug($"Pre-loaded {viewerCount} viewers");

            // Register commands from game constructor doesn't work reliably due to def loading order
            // RegisterAllCommands();
            // InitializeCommandSettings();

            // Then initialize services (which will use the registered commands)
            InitializeServices();

            Logger.Debug("CAPChatInteractiveMod constructor completed");
        }

        private void InitializeCommandSettings()
        {
            Logger.Debug("Initializing command settings...");

            // Try to load existing settings
            if (!LoadCommandSettingsFromJson())
            {
                // If no JSON exists, create default settings from XML defs
                CreateDefaultCommandSettings();
                SaveCommandSettingsToJson();
            }

            Logger.Message($"[CAP] Command settings initialized");
        }

        private bool LoadCommandSettingsFromJson()
        {
            string jsonContent = JsonFileManager.LoadFile("CommandSettings.json");
            if (string.IsNullOrEmpty(jsonContent))
                return false;

            try
            {
                var loadedSettings = JsonConvert.DeserializeObject<Dictionary<string, CommandSettings>>(jsonContent);

                // Update the command settings in the dialog (if it exists later)
                var dialog = Find.WindowStack?.WindowOfType<Dialog_CommandManager>();
                if (dialog != null)
                {
                    foreach (var kvp in loadedSettings)
                    {
                        dialog.commandSettings[kvp.Key] = kvp.Value;
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                Logger.Error($"Error loading command settings JSON: {ex.Message}");
                return false;
            }
        }

        private void CreateDefaultCommandSettings()
        {
            // Get all command defs and create default settings for them
            var commandDefs = DefDatabase<ChatCommandDef>.AllDefsListForReading;

            foreach (var commandDef in commandDefs)
            {
                var settings = new CommandSettings
                {
                    Enabled = commandDef.enabled,
                    CooldownSeconds = commandDef.cooldownSeconds,
                    PermissionLevel = commandDef.permissionLevel,
                    // Set other defaults as needed
                };

                // Store by command name, not defName
                string commandName = commandDef.commandText?.ToLower() ?? commandDef.defName.ToLower();

                // Update dialog if it exists
                var dialog = Find.WindowStack?.WindowOfType<Dialog_CommandManager>();
                if (dialog != null)
                {
                    dialog.commandSettings[commandName] = settings;
                }
            }
        }

        private void SaveCommandSettingsToJson()
        {
            try
            {
                // Get current settings from dialog or create empty dict
                var settingsToSave = new Dictionary<string, CommandSettings>();
                var dialog = Find.WindowStack?.WindowOfType<Dialog_CommandManager>();

                if (dialog != null)
                {
                    settingsToSave = dialog.commandSettings;
                }
                else
                {
                    // Create from command defs if dialog doesn't exist yet
                    foreach (var commandDef in DefDatabase<ChatCommandDef>.AllDefsListForReading)
                    {
                        string commandName = commandDef.commandText?.ToLower() ?? commandDef.defName.ToLower();
                        settingsToSave[commandName] = new CommandSettings
                        {
                            Enabled = commandDef.enabled,
                            CooldownSeconds = commandDef.cooldownSeconds,
                            PermissionLevel = commandDef.permissionLevel
                        };
                    }
                }

                string json = JsonConvert.SerializeObject(settingsToSave, Formatting.Indented);
                JsonFileManager.SaveFile("CommandSettings.json", json);
            }
            catch (Exception ex)
            {
                Logger.Error($"Error saving command settings: {ex}");
            }
        }

        private void RegisterAllCommands()
        {
            Logger.Debug("Registering all commands from Mod constructor...");

            // Debug: Check all defs first
            var allDefsCount = DefDatabase<Def>.AllDefsListForReading.Count;
            Logger.Debug($"Total defs in database: {allDefsCount}");

            // Debug: Check ChatCommandDefs specifically
            var commandDefs = DefDatabase<ChatCommandDef>.AllDefsListForReading;
            Logger.Debug($"Found {commandDefs.Count} ChatCommandDefs");

            // Log each def we find
            foreach (var def in commandDefs)
            {
                Logger.Debug($"  -> Def: {def.defName}, CommandText: {def.commandText}, Class: {def.commandClass?.Name}");
            }

            // Register commands from XML Defs
            foreach (var commandDef in commandDefs)
            {
                commandDef.RegisterCommand();
            }

            Logger.Message($"[CAP] Registered {commandDefs.Count} commands successfully");
        }

        private void InitializeServices()
        {
            Logger.Debug("InitializeServices started");

            _twitchService = new TwitchService(Settings.TwitchSettings);
            // Logger.Debug($"TwitchService created. AutoConnect: {Settings.TwitchSettings.AutoConnect}, CanConnect: {Settings.TwitchSettings.CanConnect}");
            _youTubeService = new YouTubeChatService(Settings.YouTubeSettings);

            // Auto-connect if configured
            if (Settings.TwitchSettings.AutoConnect && Settings.TwitchSettings.CanConnect)
            {
                Logger.Debug("Auto-connecting to Twitch at startup");
                _twitchService.Connect();
            }
            else
            {
                Logger.Debug($"Skipping auto-connect - AutoConnect: {Settings.TwitchSettings.AutoConnect}, CanConnect: {Settings.TwitchSettings.CanConnect}");
            }

            if (Settings.YouTubeSettings.AutoConnect && Settings.YouTubeSettings.CanConnect)
            {
                _youTubeService.Connect();
            }

            Logger.Debug("InitializeServices completed");
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            // Close the original mod settings window and open our custom one
            Find.WindowStack.TryRemove(typeof(Dialog_ModSettings), true);
            Find.WindowStack.Add(new Dialog_ChatInteractiveSettings());
        }

        public object GetChatService(string platform)
        {
            return platform?.ToLowerInvariant() switch
            {
                "twitch" => _twitchService,
                "youtube" => _youTubeService,
                _ => null
            };
        }
        public override string SettingsCategory() => "[CAP] RICS";

        // Public access to services for other parts of your mod
        public TwitchService TwitchService => _twitchService;
        public YouTubeChatService YouTubeService => _youTubeService;

        public override void WriteSettings()
        {
            base.WriteSettings();
            // Store will be initialized when game starts
        }

        public static GameComponent_PawnAssignmentManager GetPawnAssignmentManager()
        {
            return Current.Game?.GetComponent<GameComponent_PawnAssignmentManager>();
        }
    }

}