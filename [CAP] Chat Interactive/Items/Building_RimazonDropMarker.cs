// Building_RimazonDropMarker.cs
// Copyright (c) Captolamia
// This file is part of CAP Chat Interactive (RICS).
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

using RimWorld;
using UnityEngine;
using Verse;

namespace CAP_ChatInteractive
{
    /// <summary>
    /// A simple ground marker that tells RICS drop pod delivery system where to land
    /// when lockers are full or cannot accept more items.
    /// When selected, draws a radius ring showing the target drop area.
    /// Free to place, no resources, no power, misc building.
    /// </summary>
    public class Building_RimazonDropMarker : Building
    {
        /// <summary>
        /// The radius (in cells) around the marker where drop pods will try to land.
        /// Matches the area used by DropCellFinder.TryFindDropSpotNear etc.
        /// </summary>
        public const float DropRadius = 5f;

        public override void DrawExtraSelectionOverlays()
        {
            base.DrawExtraSelectionOverlays();

            // Draw the radius only when this marker is selected (standard RimWorld pattern)
            GenDraw.DrawRadiusRing(this.Position, DropRadius);
        }

        public override string GetInspectString()
        {
            string text = base.GetInspectString();
            if (!string.IsNullOrEmpty(text))
            {
                text += "\n";
            }
            text += "Delivery pods will attempt to land within the highlighted radius.";
            return text;
        }

        // No special storage or power needed. Pure marker.
    }
}
