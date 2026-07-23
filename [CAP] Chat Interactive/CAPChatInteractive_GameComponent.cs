// CAPChatInteractive_GameComponent.cs (updated with improved null safety)
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
// A game component that handles periodic tasks such as awarding coins to active viewers and managing storyteller ticks.
// Uses an efficient tick system to minimize performance impact.
// Storyteller tick logic can be expanded as needed.


using _CAP__Chat_Interactive.Utilities;
using CAP_ChatInteractive.AI;
using CAP_ChatInteractive.Incidents;
using CAP_ChatInteractive.Incidents.Weather;
using CAP_ChatInteractive.Store;
using CAP_ChatInteractive.Traits;
using CAP_ChatInteractive.Utilities;
using CAP_ChatInteractive.Windows;
using RimWorld;
using UnityEngine;
using Verse;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CAP_ChatInteractive
{
    public class CAPChatInteractive_GameComponent : GameComponent
    {
        private int tickCounter = 0;
        private const int TICKS_PER_REWARD = 120 * 60;

        private int karmaDecayTickCounter = 0;
        private int aiChatBotStateUpdateTickCounter = 0;
        private int aiChatBotCommandProcessTickCounter = 0;

        private bool versionCheckDone = false;
        private bool raceSettingsInitialized = false;
        private bool storeInitialized = false;
        private bool eventsInitialized = false;
        private bool weatherInitialized = false;
        private bool traitsInitialized = false;

        // AI ChatBot Service
        public AIChatBotService _aiChatBotService;

        // For batched death reports to AI bot (throttled, not every tick)
        private readonly List<string> recentDeaths = new List<string>();
        private int lastDeathFlushTick = 0;
        private const int DEATH_FLUSH_INTERVAL = 300; // ~5 real seconds, reasonable throttle

        // Static reference to the active AI service so we can properly stop the HttpListener
        // when loading a new game (prevents "another listener for port 17888" error).
        private static AIChatBotService _activeAIChatBotService;


        public CAPChatInteractive_GameComponent(Game game)
        {
            if (game?.components == null) return;

            if (game.GetComponent<LootBoxComponent>() == null)
            {
                game.components.Add(new LootBoxComponent(game));
                Logger.Debug("LootBoxComponent created by GameComponent");
            }
        }

        public override void LoadedGame()
        {
            base.LoadedGame();
            // Drop any Twitch raid join window that was armed on the main menu / previous session
            // so load never immediately spawns a raid or creates factions mid-init.
            CAPChatInteractiveMod.Instance?.TwitchService?.CancelRaidJoinCollection("LoadedGame — clear stale join window");
            InitializeAll();

            // If AI ChatBot is active, make sure it has fresh colony data as soon as the save finishes loading.
            var settings = CAPChatInteractiveMod.Instance?.Settings?.GlobalSettings;
            if (settings?.AIChatBotActive == true && _aiChatBotService != null)
            {
                _aiChatBotService.UpdateGameStateCache();
            }
        }

        public override void StartedNewGame()
        {
            base.StartedNewGame();
            CAPChatInteractiveMod.Instance?.TwitchService?.CancelRaidJoinCollection("StartedNewGame — clear stale join window");
            InitializeAll();

            // Same immediate cache population for new games
            var settings = CAPChatInteractiveMod.Instance?.Settings?.GlobalSettings;
            if (settings?.AIChatBotActive == true && _aiChatBotService != null)
            {
                _aiChatBotService.UpdateGameStateCache();
            }
        }

        public override void FinalizeInit()
        {
            base.FinalizeInit();
            // Can be used later for very-late setup if needed
        }

        public override void GameComponentTick()
        {
            tickCounter++;

            // === 2-MINUTE COIN REWARD (unchanged - already efficient) ===
            if (tickCounter >= TICKS_PER_REWARD)
            {
                tickCounter = 0;
                Viewers.AwardActiveViewersCoins();
                Logger.Debug("2-minute coin reward tick executed");
            }

            // === KARMA DECAY TIMER (already counter-based) ===
            var settings = CAPChatInteractiveMod.Instance?.Settings?.GlobalSettings;
            if (settings != null && settings.KarmaDecayRate > 0f && settings.KarmaDecayIntervalMinutes > 0)
            {
                karmaDecayTickCounter++;

                int ticksPerDecayInterval = settings.KarmaDecayIntervalMinutes * 2500;

                if (karmaDecayTickCounter >= ticksPerDecayInterval)
                {
                    karmaDecayTickCounter = 0;
                    Viewers.ApplyKarmaDecayToAll(settings);
                    Logger.Debug($"Karma decay tick executed (interval: {settings.KarmaDecayIntervalMinutes} min, rate: {settings.KarmaDecayRate})");
                }
            }

            // === RAID TIMER (only runs when actually needed — keep this gated) ===
            // This block is intentionally throttled and conditional so it adds zero overhead
            // when Twitch Raids are enabled but no raid is currently happening.
            if (tickCounter % 60 == 0)
            {
                var twitch = CAPChatInteractiveMod.Instance?.TwitchService;
                if (CAPChatInteractiveMod.Instance?.Settings?.GlobalSettings?.TwitchRaidsEnabled == true &&
                    twitch?.IsRaidJoinWindowActive == true)
                {
                    twitch.UpdateRaidJoinTimer();
                }
            }

            // === AI CHATBOT FILE COMMAND PROCESSING (independent fast loop) ===
            // Separate block so we can tune its frequency without affecting raid timing,
            // and vice versa. Runs whenever AIChatBotActive is true.
            var aiSettings = CAPChatInteractiveMod.Instance?.Settings?.GlobalSettings;
            if (aiSettings?.AIChatBotActive == true && _aiChatBotService != null)
            {
                // === Process file-based AI commands (main thread only - stable) ===
                // This runs every ~2 seconds (cheap) so the Python bot can drop command files and get quick results.
                _aiChatBotService.ProcessFileBasedAICommands();

                // Game state cache refresh + push to external bot (every X minutes, user configurable)
                // WHY: V9 Masie no longer has its own timer. RICS now controls when fresh colony reports are generated.
                // UpdateGameStateCache + Push are safe here (main thread after LoadedGame/StartedNewGame, already wrapped in try/catch inside the service).
                if (aiSettings.AIChatBotGameStateUpdateIntervalMinutes > 0)
                {
                    aiChatBotStateUpdateTickCounter++;

                    int ticksPerUpdate = aiSettings.AIChatBotGameStateUpdateIntervalMinutes * 2500;

                    if (aiChatBotStateUpdateTickCounter >= ticksPerUpdate)
                    {
                        aiChatBotStateUpdateTickCounter = 0;

                        _aiChatBotService.UpdateGameStateCache();
                        _aiChatBotService.PushCurrentGameStateToBot();
                    }
                }
            }

            // === THROTTLED DEATH REPORTS TO AI BOT ===
            // Not every tick. Flush every ~5 seconds (300 ticks) if there are deaths.
            // The per-death strings (from patch) now include: animal/pawn, faction, killer (who + how), map, + slaughter note.
            // Volume + home-map triggers simple raid inference.
            if (aiSettings?.AIChatBotActive == true && _aiChatBotService != null)
            {
                int currentTick = Find.TickManager?.TicksGame ?? 0;
                if (currentTick - lastDeathFlushTick >= DEATH_FLUSH_INTERVAL && recentDeaths.Count > 0)
                {
                    lastDeathFlushTick = currentTick;

                    var deaths = recentDeaths.ToList();
                    recentDeaths.Clear();

                    string summary = string.Join(" | ", deaths.Take(8)); // limit spam
                    if (deaths.Count >= 4)
                    {
                        summary += " — This looks like a major raid or combat situation!";
                    }

                    // Check if any deaths on home map (substring still present in new richer messages)
                    bool onHome = deaths.Any(d => d.Contains("home colony map"));

                    if (onHome && deaths.Count >= 3)
                    {
                        summary += ". The colony is being raided!";
                    }
                    else if (deaths.Count >= 3)
                    {
                        summary += ". Heavy losses — we may be raiding or in combat on another map.";
                    }

                    string botName = aiSettings.AIChatBotName ?? "Masie";
                    _aiChatBotService.NotifyColonyEvent($"{botName}, deaths reported: {summary}");
                }
            }
        }

        private void InitializeAll()
        {
            PerformVersionCheckIfNeeded();
            InitializeRaceSettings();
            InitializeStore();
            InitializeEvents();
            InitializeWeather();
            InitializeTraits();

            // Initialize AI ChatBot listener if enabled
            InitializeAIChatBot();

            Logger.Message("All core systems initialized");
        }

        private void PerformVersionCheckIfNeeded()
        {
            if (versionCheckDone) return;
            versionCheckDone = true;
            VersionHistory.CheckForVersionUpdate();
            Logger.Debug("Version check performed");
        }

        private void InitializeRaceSettings()
        {
            if (raceSettingsInitialized) return;
            var settings = RaceSettingsManager.RaceSettings;
            raceSettingsInitialized = true;
            Logger.Debug($"Race settings initialized ({settings.Count} races)");
        }

        private void InitializeStore()
        {
            if (storeInitialized) return;
            StoreInventory.InitializeStore();
            storeInitialized = true;
            Logger.Debug("Store inventory initialized");
        }

        private void InitializeEvents()
        {
            if (eventsInitialized) return;
            IncidentsManager.InitializeIncidents();
            eventsInitialized = true;
            Logger.Debug("Incidents initialized");
        }

        private void InitializeWeather()
        {
            if (weatherInitialized) return;
            BuyableWeatherManager.InitializeWeather();
            weatherInitialized = true;
            Logger.Debug("Buyable weather initialized");
        }

        private void InitializeTraits()
        {
            if (traitsInitialized) return;
            TraitsManager.InitializeTraits();
            traitsInitialized = true;
            Logger.Debug("Traits initialized");
        }

        private void InitializeAIChatBot()
        {
            var settings = CAPChatInteractiveMod.Instance?.Settings?.GlobalSettings;
            if (settings?.AIChatBotActive != true)
            {
                // AI is off — make sure any previous listener is stopped
                _activeAIChatBotService?.Stop();
                _activeAIChatBotService = null;
                _aiChatBotService = null;
                return;
            }

            // Always stop any previous listener first. This releases the http://127.0.0.1:17888/ prefix
            // so a fresh HttpListener can bind. Prevents the "another listener for ..." error when
            // loading a new save or restarting the game while AIChatBot is enabled.
            _activeAIChatBotService?.Stop();

            _aiChatBotService = new AIChatBotService();
            _activeAIChatBotService = _aiChatBotService;
            _aiChatBotService.Start();

            // Force an immediate game state cache update now that we know a game is loaded.
            // This guarantees the Python bot gets real colony data (colonists, food, medicine, threat, etc.)
            // on its very first /gamestate poll instead of waiting for the 15-minute timer.
            _aiChatBotService?.UpdateGameStateCache();
            _aiChatBotService.PushCurrentGameStateToBot();

            Logger.Debug("AI ChatBot service initialized + initial game state cached");
        }

        public void RecordDeath(string deathMessage)
        {
            if (string.IsNullOrWhiteSpace(deathMessage)) return;
            recentDeaths.Add(deathMessage);
            // Cap the list to prevent memory bloat during massive raids
            if (recentDeaths.Count > 50)
                recentDeaths.RemoveAt(0);
        }



        /// <summary>
        /// Runs every frame (like Update). Checks Ctrl+V globally to toggle live chat.
        /// Very cheap (only 1 GetKey call) and ignores input when typing.
        /// </summary>
        /// <summary>
        /// Runs every frame (like Update). Checks Ctrl+V globally to toggle live chat.
        /// Also handles AI file commands when the game is paused (Tick() stops during pause).
        /// </summary>
        public override void GameComponentUpdate()
        {
            base.GameComponentUpdate();

            if (ChatUtility.IsToggleHotkeyPressed())
            {
                string focused = GUI.GetNameOfFocusedControl();
                if (string.IsNullOrEmpty(focused) || !focused.Contains("Input"))
                {
                    Window_LiveChat.ToggleLiveChatWindow();
                }
            }

            // === AI CHATBOT FILE COMMANDS - only when paused ===
            // We keep the main processing loop in GameComponentTick() for normal gameplay.
            // This fallback only runs when the game is paused so Masie can still react to commands/chat.
            // The 120ms internal throttle in ProcessFileBasedAICommands() prevents busy work.
            var aiSettings = CAPChatInteractiveMod.Instance?.Settings?.GlobalSettings;
            if (aiSettings?.AIChatBotActive == true && _aiChatBotService != null)
            {
                // Only process from Update when we're paused or not in a normal playing state
                if (Current.ProgramState != ProgramState.Playing || Find.TickManager?.Paused == true)
                {
                    _aiChatBotService.ProcessFileBasedAICommands();
                }
            }
        }
    }
}