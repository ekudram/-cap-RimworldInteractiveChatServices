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
using UnityEngine;
using Verse;

namespace CAP_ChatInteractive
{
    public class CAPChatInteractive_GameComponent : GameComponent
    {
        private int tickCounter = 0;
        private const int TICKS_PER_REWARD = 120 * 60;

        private int karmaDecayTickCounter = 0;

        private bool versionCheckDone = false;
        private bool raceSettingsInitialized = false;
        private bool storeInitialized = false;
        private bool eventsInitialized = false;
        private bool weatherInitialized = false;
        private bool traitsInitialized = false;

        // AI ChatBot Service
        public AIChatBotService _aiChatBotService;


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
            InitializeAll();
        }

        public override void StartedNewGame()
        {
            base.StartedNewGame();
            InitializeAll();
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

            // === THROTTLED UPDATES (every 120 ticks ≈ 2 seconds) ===
            // This was the main source of the previous 23% spike.
            if (tickCounter % 120 == 0)
            {
                // Raid timer - only if enabled
                if (CAPChatInteractiveMod.Instance?.Settings?.GlobalSettings?.TwitchRaidsEnabled == true)
                {
                    CAPChatInteractiveMod.Instance.TwitchService?.UpdateRaidJoinTimer();
                }

                // AI ChatBot game state cache - only if enabled
                if (CAPChatInteractiveMod.Instance?.Settings?.GlobalSettings?.AIChatBotActive == true)
                {
                    _aiChatBotService?.UpdateGameStateCache();
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
            if (settings?.AIChatBotActive != true) return;

            _aiChatBotService ??= new AIChatBotService();
            _aiChatBotService.Start();
            Logger.Debug("AI ChatBot service initialized");
        }



        /// <summary>
        /// Runs every frame (like Update). Checks Ctrl+V globally to toggle live chat.
        /// Very cheap (only 1 GetKey call) and ignores input when typing.
        /// </summary>
        public override void GameComponentUpdate()
        {
            base.GameComponentUpdate();

            if (ChatUtility.IsToggleHotkeyPressed())
            {
                // Safety: never toggle while user is typing in any text field (prevents closing while pasting)
                string focused = GUI.GetNameOfFocusedControl();
                if (string.IsNullOrEmpty(focused) || !focused.Contains("Input"))
                {
                    Window_LiveChat.ToggleLiveChatWindow();
                }
            }
        }
    }
}