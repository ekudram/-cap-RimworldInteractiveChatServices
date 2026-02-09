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
        private const int TICKS_PER_REWARD = 120 * 60;

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
            if (tickCounter >= TICKS_PER_REWARD)
            {
                tickCounter = 0;
                Viewers.AwardActiveViewersCoins();
                Logger.Debug("2-minute coin reward tick executed");
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
    }
}