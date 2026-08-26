// IAddonMenu.cs
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
// Contract for MenuButton / SubmenuButton menuClass types.
using System.Collections.Generic;
using Verse;

namespace CAP_ChatInteractive.Interfaces
{
    /// <summary>
    /// Implement this on a public parameterless class and point
    /// <c>EnhancedChatInteractiveAddonDef.menuClass</c> at it for a MenuButton.
    /// Return FloatMenuOption entries; RICS shows them in a FloatMenu.
    /// </summary>
    public interface IAddonMenu
    {
        /// <summary>Options shown when the MenuButton is activated.</summary>
        List<FloatMenuOption> MenuOptions();
    }
}
