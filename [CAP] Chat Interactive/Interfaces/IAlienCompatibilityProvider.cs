// IAlienCompatibilityProvider.cs
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
// Interface for compatibility providers
using RimWorld;
using System.Collections.Generic;
using Verse;

namespace _CAP__Chat_Interactive.Interfaces
{
    public interface ICompatibilityProvider
    {
        string ModId { get;}
    }
    public interface IAlienCompatibilityProvider : ICompatibilityProvider
    {
        bool IsTraitForced(Pawn pawn, string defName, int degree);
        bool IsTraitDisallowed(Pawn pawn, string defName, int degree);
        bool IsTraitAllowed(Pawn pawn, TraitDef traitDef, int degree = -10);
        List<string> GetAllowedXenotypes(ThingDef raceDef);
        bool IsXenotypeAllowed(ThingDef raceDef, XenotypeDef xenotype);

        // Gender restriction methods
        bool IsGenderAllowed(ThingDef raceDef, Gender gender);

        /// <summary>
        /// Checks if a backstory is compatible with the pawn (HAR: uses AlienBackstoryDef.Approved(Pawn) for race/gender/age/xenotype restrictions).
        /// Vanilla backstories always return true (matches RimWorld default behavior).
        /// </summary>
        bool IsBackstoryAllowed(BackstoryDef backstory, Pawn pawn);

        // HAR race restriction support (prevents "coins taken, item disappears" bug)
        /// <summary>
        /// Delegates to HAR's RaceRestrictionSettings.CanWear (verified from provided decompile).
        /// Returns true for humans / non-HAR races.
        /// </summary>
        bool CanWear(ThingDef apparel, ThingDef race);

        /// <summary>
        /// Delegates to HAR's RaceRestrictionSettings.CanEquip.
        /// Returns true for humans / non-HAR races.
        /// </summary>
        bool CanEquip(ThingDef weapon, ThingDef race);
        GenderPossibility GetAllowedGenders(ThingDef raceDef);
        (float maleProbability, float femaleProbability) GetGenderProbabilities(ThingDef raceDef);
    }
}

