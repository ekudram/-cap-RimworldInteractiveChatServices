// IVPEPsycastProvider.cs
// Copyright (c) Captolamia
// This file is part of CAP Chat Interactive.
// 
// CAP Chat Interactive is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License as published
// by the Free Software Foundation, either version 3 of the License, or
// (at an option) any later version.
// 
// CAP Chat Interactive is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
// GNU Affero General Public License for more details.
// 
// You should have received a copy of the GNU Affero General Public License
// along with CAP Chat Interactive. If not, see <https://www.gnu.org/licenses/>.
//
// Interface for Vanilla Psycasts Expanded (VPE) compatibility provider.
// Kept separate from IAlienCompatibilityProvider (HAR) for readability.

using RimWorld;
using System.Collections.Generic;
using Verse;

namespace _CAP__Chat_Interactive.Interfaces
{
    /// <summary>
    /// Provider interface for Vanilla Psycasts Expanded (VPE) data.
    /// The implementing patch assembly will have direct references to VPE/VEF types
    /// so we get clean access instead of reflection hacks.
    /// </summary>
    public interface IVPEPsycastProvider : ICompatibilityProvider
    {
        /// <summary>
        /// Returns basic psycast information for the given pawn.
        /// Used by !mypawn psycasts (Phase 1).
        /// Returns a sensible default object if the pawn has no VPE psycaster hediff.
        /// </summary>
        VPEBasicPsycastInfo GetBasicPsycastInfo(Pawn pawn);

        /// <summary>
        /// Returns owned psycast abilities for a specific VPE "class" (PsycasterPathDef).
        /// Matches classIdentifier against defName or label (case/punctuation insensitive).
        /// Returns matching class info with owned abilities (filtered by pawn level + HasAbility).
        /// </summary>
        VPEClassInfo GetPsycastsInClass(Pawn pawn, string classIdentifier);
    }

    /// <summary>
    /// Data transfer object for basic VPE psycast stats.
    /// Phase 1 focuses on:
    /// - Psycaster level (from the VPE implant hediff)
    /// - Psyfocus (current + what is needed for next level)
    /// - Heat / Entropy (current + max)
    /// </summary>
    public class VPEBasicPsycastInfo
    {
        /// <summary>
        /// The pawn's current psycaster / psylink level.
        /// 0 means no VPE psycaster implant was found.
        /// </summary>
        public int Level { get; set; } = 0;

        /// <summary>
        /// Current psyfocus (0.0 - 1.0 range).
        /// </summary>
        public float CurrentPsyfocus { get; set; } = 0f;

        /// <summary>
        /// Maximum psyfocus the pawn can hold.
        /// </summary>
        public float MaxPsyfocus { get; set; } = 1f;

        /// <summary>
        /// Approximate psyfocus / experience amount needed to reach the next level.
        /// For Phase 1 we can report a value or 0 if at max / no data.
        /// The patch implementation will try to compute this from the hediff's experience system.
        /// </summary>
        public float PsyfocusNeededForNextLevel { get; set; } = 0f;

        /// <summary>
        /// Current psychic entropy (heat).
        /// </summary>
        public float CurrentHeat { get; set; } = 0f;

        /// <summary>
        /// Maximum psychic entropy the pawn can hold before overload.
        /// </summary>
        public float MaxHeat { get; set; } = 0f;

        /// <summary>
        /// Whether this pawn actually has VPE psycasts active.
        /// </summary>
        public bool HasPsycasts => Level > 0;
    }

    /// <summary>
    /// Result for !mypawn psycast &lt;classname&gt; queries.
    /// Contains the psycaster level at time of query + the list of abilities the pawn has learned in that path/class.
    /// </summary>
    public class VPEClassInfo
    {
        public int Level { get; set; } = 0;
        public string ClassLabel { get; set; } = "";
        public string ClassDefName { get; set; } = "";
        public List<VPEOwnedAbility> Abilities { get; set; } = new List<VPEOwnedAbility>();
        public bool HasMatchingClass { get; set; } = false;
        public string Error { get; set; }

        public bool HasAnyAbilities => Abilities != null && Abilities.Count > 0;
    }

    /// <summary>
    /// A single ability the pawn owns within a VPE class/path.
    /// </summary>
    public class VPEOwnedAbility
    {
        public string Label { get; set; }
        public int RequiredLevel { get; set; }
    }
}
