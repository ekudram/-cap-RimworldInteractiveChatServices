// JobDriver_EmptyLocker.cs
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

using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace CAP_ChatInteractive
{
    /// <summary>
    /// Basic job for a pawn to go "push the button" on a Rimazon Locker to empty it.
    /// Modeled after vanilla JobDriver_Flick (go + short wait + action + clean designation).
    /// </summary>
    public class JobDriver_EmptyLocker : JobDriver
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return this.pawn.Reserve(this.job.targetA, this.job, errorOnFailed: errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(TargetIndex.A);

            // Fail if the designation is gone or the locker no longer allows emptying.
            this.FailOn(() =>
            {
                var locker = this.TargetThingA as Building_RimazonLocker;
                if (locker == null || !locker.allowPawnsToEmpty)
                    return true;
                return this.Map.designationManager.DesignationOn(this.TargetThingA, DesignationDefOf.Flick) == null;
            });

            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);

            // Short "push button" animation time, like flick switches.
            yield return Toils_General.Wait(15).FailOnCannotTouch(TargetIndex.A, PathEndMode.Touch);

            Toil doEmpty = ToilMaker.MakeToil(nameof(MakeNewToils));
            doEmpty.initAction = () =>
            {
                Pawn actor = doEmpty.actor;
                if (this.TargetThingA is Building_RimazonLocker locker && locker.Spawned)
                {
                    locker.SafeEjectAllContents();
                    actor.records.Increment(RecordDefOf.SwitchesFlicked);
                }

                // Clean up the flick designation (we reused it as the trigger signal).
                this.Map.designationManager.DesignationOn(this.TargetThingA, DesignationDefOf.Flick)?.Delete();
            };
            doEmpty.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return doEmpty;
        }
    }
}
