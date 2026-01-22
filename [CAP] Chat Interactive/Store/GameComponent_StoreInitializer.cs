// GameComponent_StoreInitializer.cs
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
// Initializes the in-game store when a game is loaded or started new.

/*
============================================================
STORE INITIALIZATION ONLY
============================================================

PURPOSE:
• Initialize store once when game loads
• Ensure AllStoreItems Dictionary is populated
• Handle validation/updates from JSON

NOT FOR:
• Store data saving (use StoreInventory.SaveStoreToJson())
• Store operations (use StoreInventory.AllStoreItems)
• Store UI logic (see Dialog_StoreEditor)
============================================================
*/
using RimWorld;
using Verse;

namespace CAP_ChatInteractive.Store
{
    public class GameComponent_StoreInitializer : GameComponent
    {
        private bool storeInitialized = false;

        public GameComponent_StoreInitializer(Game game) { }

        public override void LoadedGame()
        {
            InitializeStore();
        }

        public override void StartedNewGame()
        {
            InitializeStore();
        }

        public override void GameComponentTick()
        {
            // Initialize on first tick to ensure all defs are loaded
            if (!storeInitialized && Current.ProgramState == ProgramState.Playing)
            {
                InitializeStore();
            }
        }

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