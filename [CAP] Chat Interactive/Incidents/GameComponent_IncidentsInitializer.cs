// GameComponent_IncidentsInitializer.cs
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
//
// Initializes incident and weather systems when a game is loaded or started
using Verse;

// Leave empty to prevent non critical errors related to missing game components during incident initialization.
// Actual initialization logic is handled in CAPChatInteractive_GameComponent to ensure proper sequencing and error handling.

namespace CAP_ChatInteractive.Incidents
{
    public class GameComponent_IncidentsInitializer : GameComponent
    {
        public GameComponent_IncidentsInitializer(Game game) { }

    }
}