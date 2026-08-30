// File: Building_RimazonLocker.cs
//
// Copyright (c) Captolamia
// This file is part of CAP Chat Interactive (RICS).
// Licensed under the GNU Affero General Public License v3.0 or later.
// See LICENSE.txt in the project root for full license text.
//
// This file defines a Rimazon locker building for RimWorld that can be renamed by the player.

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