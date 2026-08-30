// File: Building_RimazonDropMarker.cs
//
// Copyright (c) Captolamia
// This file is part of CAP Chat Interactive (RICS).
// Licensed under the GNU Affero General Public License v3.0 or later.
// See LICENSE.txt in the project root for full license text.
//
// This file defines a simple ground marker building for RimWorld that indicates where drop pods should land.

using RimWorld;
using System;
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
        /// This is what gets visualized when the marker is selected.
        /// </summary>
        public const float DropRadius = 6f;

        // Orange to match the Rimazon (Amazon-style) graphic the user provided
        private static readonly Color RimazonOrange = new Color(1f, 0.6f, 0f);

        public override void DrawExtraSelectionOverlays()
        {
            base.DrawExtraSelectionOverlays();

            if (this.Map == null)
                return;

            // Predicate: only draw ring segments over cells that are valid drop locations
            // (standable, not fogged, not impassable). Makes the preview more accurate.
            Func<IntVec3, bool> validForDrop = (IntVec3 c) =>
                c.InBounds(this.Map) &&
                c.Standable(this.Map) &&
                !c.Fogged(this.Map) &&
                !c.Impassable(this.Map);

            // Use the full overload with orange color matching the graphic
            GenDraw.DrawRadiusRing(this.Position, DropRadius, RimazonOrange, validForDrop);
        }

        public override string GetInspectString()
        {
            string baseText = base.GetInspectString();
            string radiusText = "RICS.DropMarker.Inspect".Translate();

            if (!string.IsNullOrEmpty(baseText))
                return baseText + "\n" + radiusText;

            return radiusText;
        }

        // No special storage or power needed. Pure marker.
    }
}
