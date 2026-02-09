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
using CAP_ChatInteractive.Incidents;
using CAP_ChatInteractive.Store;
using CAP_ChatInteractive.Incidents.Weather;
using CAP_ChatInteractive.Traits;
using Verse;

namespace CAP_ChatInteractive
{
    public class CAPChatInteractive_GameComponent : GameComponent
    {
        private int tickCounter = 0;
        private const int TICKS_PER_REWARD = 120 * 60; // 2 minutes in ticks (60 ticks/sec * 120 sec)
        // Flags to ensure certain checks and initializations only happen once
        private bool versionCheckDone = false;
        private bool raceSettingsInitialized = false;
        private bool storeInitialized = false;
        private bool eventsInitialized = false;
        private bool weatherInitialized = false;
        private bool traitsInitialized = false;

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
            // 1st time version check on game load to catch returning players
            PerformVersionCheckIfNeeded();
            // 2nd Initialize Race Settings on game load
            InitializeRaceSettings();
            // 3rd Initialize Store on game load
            InitializeStore();
            // 4th Initialize Events on game load
            InitializeEvents();
            // 5th Initialize Weather on game load
            InitializeWeather();
            // 6th Initialize Traits on game load
            InitializeTraits();
        }

        public override void StartedNewGame()
        {
            base.StartedNewGame();
            // 1st time version check on new game start to catch first-time players
            PerformVersionCheckIfNeeded();
            // 2nd Thing to initialize
            InitializeRaceSettings();
            // 3rd Thing to initialize
            InitializeStore();
            // 4th Initialize Events
            InitializeEvents();
            // 5th Initialize Weather
            InitializeWeather();
            // 6th Initialize Traits
            InitializeTraits();
        }

        public override void FinalizeInit()
        {
            base.FinalizeInit();
            // Logger.Debug("GameComponent FinalizeInit - ensuring store is initialized");
            // Store.StoreInventory.InitializeStore();
        }

        public override void GameComponentTick()
        {
            tickCounter++;
            if (tickCounter >= TICKS_PER_REWARD)
            {
                tickCounter = 0;
                Viewers.AwardActiveViewersCoins();
                Logger.Debug("2-minute coin reward tick executed - awarded coins to active viewers");
            }
        }


        // MOD VERSION CHECKING AND UPDATE NOTIFICATIONS
        private void PerformVersionCheckIfNeeded()
        {
            if (versionCheckDone) return;
            versionCheckDone = true;
            VersionHistory.CheckForVersionUpdate();
            Logger.Debug("Version check performed on game load/new game start");
        }
        // INITIALIZATION RACE SETTINGS
        private void InitializeRaceSettings()
        {
            if (raceSettingsInitialized) return;
            // This will load settings from file or create defaults if missing
            var settings = RaceSettingsManager.RaceSettings;
            raceSettingsInitialized = true;
            Logger.Debug($"Race settings initialized with {settings.Count} races");
        }

        // INITIALIZATION STORE SETTINGS
        private void InitializeStore()
        {
            if (storeInitialized) return;
            StoreInventory.InitializeStore();
            storeInitialized = true;
            Logger.Debug("Store inventory initialized in GameComponent");
        }
        // INITIALIZATION EVENTS
        private void InitializeEvents()
        {
            if (eventsInitialized) return;
            IncidentsManager.InitializeIncidents();
            eventsInitialized = true;
            Logger.Debug("Incidents initialized in GameComponent");
        }
        // INITIALIZATION WEATHER
        private void InitializeWeather()
        {
            if (weatherInitialized) return;
            BuyableWeatherManager.InitializeWeather();
            weatherInitialized = true;
            Logger.Debug("Buyable weather initialized in GameComponent");
        }
        // INITIALIZATION TRAITS
        private void InitializeTraits()
        {
            if (traitsInitialized) return;
            TraitsManager.InitializeTraits();
            traitsInitialized = true;
            Logger.Debug("Traits initialized in GameComponent");
        }

    }
}