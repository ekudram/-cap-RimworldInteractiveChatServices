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
using CAP_ChatInteractive.Store;
using RimWorld;
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
        }
        // INITIALIZATION RACE SETTINGS
        private void InitializeRaceSettings()
        {
            if (raceSettingsInitialized) return;

            Logger.Debug("=== INITIALIZING RACE SETTINGS ===");

            // This triggers the full load/initialization in RaceSettingsManager
            var settings = RaceSettingsManager.RaceSettings;

            raceSettingsInitialized = true;

            Logger.Debug($"Race settings initialized with {settings.Count} races");
            Logger.Debug("=== FINISHED INITIALIZING RACE SETTINGS ===");
        }

        // INITIALIZATION STORE SETTINGS

        private void InitializeStore()
        {
            if (!storeInitialized)
            {
                StoreInventory.InitializeStore();
                storeInitialized = true;
            }
        }

    }
}