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

// Dummy game component to handle store initialization separately from the main game component
// ensuring better modularity and separation of concerns.

// Left in place to prevent Error showing in older game saves.

using Verse;

namespace CAP_ChatInteractive.Store
{
    public class GameComponent_StoreInitializer : GameComponent
    {
        public GameComponent_StoreInitializer(Game game) { }

    }
}