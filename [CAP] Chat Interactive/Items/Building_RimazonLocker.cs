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

using RimWorld;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using Verse;


// This should be unused.
namespace CAP_ChatInteractive
{
    public class Comp_RimazonLocker : ThingComp
    {
        public string customName = null;  // null = use default label

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref customName, "customName");
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            // Rename button (only visible if player owns it / in god mode etc.)
            yield return new Command_Action
            {
                defaultLabel = "Rename locker",
                defaultDesc = "Give this Rimazon locker a unique name for chat deliveries (e.g. 'lipstick').",
                icon = ContentFinder<Texture2D>.Get("UI/Commands/Rename", true),  // Reuse vanilla rename icon if exists, or your own
                action = () =>
                {
                    Find.WindowStack.Add(new Dialog_RenameLocker((Building_RimazonLocker)parent));
                }
            };
        }

        public override string CompInspectStringExtra()
        {
            if (!customName.NullOrEmpty())
            {
                return "Locker name: " + customName;
            }
            return null;
        }
    }

    // Simple rename dialog (like stockpile rename)
}