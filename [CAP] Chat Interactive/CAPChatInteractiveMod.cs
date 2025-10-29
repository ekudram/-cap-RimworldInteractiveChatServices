using _CAP__Chat_Interactive.Interfaces;
using Google.Apis.YouTube.v3;
using RimWorld;
using System;   
using System.Collections.Generic;
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

            Logger.Message("[CAP] Chat Interactive mod loaded successfully!");
            // Force viewer loading by accessing the All property
            var viewerCount = Viewers.All.Count; // This triggers static constructor
            Logger.Debug($"Pre-loaded {viewerCount} viewers");

            // Register commands from XML Defs first
            RegisterDefCommands();

            // Then initialize services (which will use the registered commands)
            InitializeServices();

            Logger.Debug("CAPChatInteractiveMod constructor completed");
        }

        private void RegisterDefCommands()
        {
            Logger.Debug("Registering commands from Defs...");

            // Register all ChatCommandDefs with the processor
            foreach (var commandDef in DefDatabase<ChatCommandDef>.AllDefs)
            {
                commandDef.RegisterCommand();
            }

            Logger.Message($"Registered {DefDatabase<ChatCommandDef>.AllDefsListForReading.Count} commands from Defs");
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
        public override string SettingsCategory() => "[CAP] Chat Interactive";

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