// CAPChatInteractiveMod.cs
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
// Handles initialization, settings, and service management.
// Store, Traits, Weather, and other systems will be initialized when the game starts.

using _CAP__Chat_Interactive.Interfaces;
using RimWorld;
using System;   
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Verse;
using HarmonyLib;

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

            // ────────────────────────────────────────────────
            // Harmony initialization — put this early!
            var harmony = new Harmony("com.capto.rimazonlocker");  // Unique ID (reverse-domain style is best)
            harmony.PatchAll();                                    // Applies ALL [HarmonyPatch] classes in your assembly
            Logger.Message("[RICS] Harmony patches applied successfully.");  // Confirm in log
            // ────────────────────────────────────────────────

            // Ensure modVersion is set in saved settings if it's empty
            if (string.IsNullOrEmpty(Settings.GlobalSettings.modVersionSaved))
            {
                Settings.GlobalSettings.modVersionSaved = Settings.GlobalSettings.modVersion;
                Logger.Debug($"Initialized modVersionSaved to {Settings.GlobalSettings.modVersion}");
            }

            // INITIALIZE ALIEN PROVIDER HERE - AT MOD STARTUP
            Logger.Debug("=== INITIALIZING ALIEN COMPATIBILITY AT MOD STARTUP ===");
            InitializeAlienCompatibilityProvider();

            if (Current.Game != null && Current.Game.components != null)
            {
                var existingComponent = Current.Game.GetComponent<CAPChatInteractive_GameComponent>();
                if (existingComponent == null)
                {
                    Current.Game.components.Add(new CAPChatInteractive_GameComponent(Current.Game));
                    Logger.Debug("GameComponent added to existing game");
                }
            }

            Logger.Message("RICS mod loaded successfully!");

            // Force viewer loading by accessing the All property
            var viewerCount = Viewers.All.Count; // This triggers static constructor
            Logger.Debug($"Pre-loaded {viewerCount} viewers");

            if (Current.Game != null)
            {
                Current.Game.GetComponent<GameComponent_RaceSettingsInitializer>();
            }

            // Then initialize services (which will use the registered commands)
            InitializeServices();
            InitializeAlienCompatibilityProvider(); // HAR



            Logger.Debug("CAPChatInteractiveMod constructor completed");
        }

        public void InitializeAlienCompatibilityProvider()
        {
            try
            {
                Logger.Debug("=== INITIALIZING ALIEN COMPATIBILITY PROVIDER ===");

                // Check if HAR mod is loaded AND if our patch assembly is available
                if (ModLister.GetActiveModWithIdentifier("erdelf.HumanoidAlienRaces") != null)
                {
                    Logger.Debug("HAR mod detected, checking for patch assembly");

                    // Try to load the patch assembly dynamically
                    AlienProvider = LoadHARPatchConditionally();
                }
                else
                {
                    Logger.Debug("HAR mod not detected, alien compatibility disabled");
                    AlienProvider = null;
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error initializing alien compatibility provider: {ex}");
                AlienProvider = null;
            }
        }

        private IAlienCompatibilityProvider LoadHARPatchConditionally()
        {
            try
            {
                // This will only succeed if the assembly is in the Optional folder
                Assembly harPatchAssembly = AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == "[CAP] HAR Patch");

                if (harPatchAssembly == null)
                {
                    Logger.Debug("HAR Patch assembly not found (not in Optional folder)");
                    return null;
                }

                Type harPatchType = harPatchAssembly.GetType("CAP_ChatInteractive.Patch.HAR.HARPatch");
                if (harPatchType != null)
                {
                    return Activator.CreateInstance(harPatchType) as IAlienCompatibilityProvider;
                }
            }
            catch (FileNotFoundException)
            {
                // This is expected if the patch isn't installed
                Logger.Debug("HAR Patch assembly not found (optional dependency)");
            }
            catch (Exception ex)
            {
                Logger.Error($"Error loading HAR patch: {ex}");
            }

            return null;
        }

        public IAlienCompatibilityProvider FindAnyAlienCompatibilityProvider()
        {
            try
            {
                Logger.Debug("=== DIRECT HAR PATCH SEARCH ===");

                // Method 1: Direct assembly name search
                Assembly harPatchAssembly = AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == "[CAP] HAR Patch");

                if (harPatchAssembly != null)
                {
                    Logger.Debug($"Found HAR Patch assembly: {harPatchAssembly.FullName}");
                    Type harPatchType = harPatchAssembly.GetType("CAP_ChatInteractive.Patch.HAR.HARPatch");
                    if (harPatchType != null)
                    {
                        Logger.Debug($"Found HARPatch type, creating instance...");
                        var instance = Activator.CreateInstance(harPatchType) as IAlienCompatibilityProvider;
                        if (instance != null)
                        {
                            Logger.Debug($"SUCCESS: Created HARPatch instance with ModId: {instance.ModId}");
                            return instance;
                        }
                    }
                    else
                    {
                        Logger.Error($"HARPatch type not found in assembly");
                    }
                }
                else
                {
                    Logger.Error("[CAP] HAR Patch assembly not found in loaded assemblies");

                    // Log all loaded assemblies for debugging
                    Logger.Debug("=== LOADED ASSEMBLIES ===");
                    foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                    {
                        Logger.Debug($"Assembly: {assembly.GetName().Name}");
                    }
                    Logger.Debug("=== END LOADED ASSEMBLIES ===");
                }

                return null;
            }
            catch (Exception ex)
            {
                Logger.Error($"Error finding HAR Patch: {ex}");
                return null;
            }
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
        public static void OpenQualitySettings()
        {
            if (Instance != null && Instance.Settings != null)
            {
                Find.WindowStack.Add(new Dialog_QualityResearchSettings(Instance.Settings));
            }
            else
            {
                Logger.Error("Cannot open quality settings - mod instance or settings not available");
            }
        }
    }

}