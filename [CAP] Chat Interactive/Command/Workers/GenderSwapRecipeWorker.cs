// File: GenderSwapRecipeWorker.cs
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
// Gender swap surgery worker for RimWorld mod CAP Chat Interactive
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace CAP_ChatInteractive.Commands.CommandHandlers
{

    public class GenderSwapRecipeWorker : Recipe_InstallArtificialBodyPart
    {
        /// <summary>
        /// Surgery for changing gender on pawn
        /// </summary>
        /// <param name="pawn"></param>
        /// <param name="part"></param>
        /// <param name="billDoer"></param>
        /// <param name="ingredients"></param>
        /// <param name="bill"></param>
        public override void ApplyOnPawn(
            Pawn pawn,
            BodyPartRecord part,
            Pawn billDoer,
            List<Thing> ingredients,
            Bill bill)
        {
            Logger.Debug($"GenderSwap ApplyOnPawn called - Pawn: {pawn?.Name}, Doctor: {billDoer?.Name}");

            if (pawn == null || pawn.Dead)
            {
                Logger.Debug("Surgery Aborted: Pawn null or dead.");
                return;
            }

            if (billDoer == null || CheckSurgeryFail(billDoer, pawn, ingredients, part, bill))
            {
                Logger.Debug("Surgery fail check - aborting.");
                return;
            }

            Gender originalGender = pawn.gender;  // Capture BEFORE change

            TaleRecorder.RecordTale(TaleDefOf.DidSurgery, billDoer, pawn);
            pawn.gender = originalGender == Gender.Male ? Gender.Female : Gender.Male;

            pawn.Drawer.renderer.SetAllGraphicsDirty();
            PortraitsCache.SetDirty(pawn);

            Messages.Message($"Gender swap completed on {pawn.LabelShort}. Now {pawn.gender}.", pawn, MessageTypeDefOf.PositiveEvent);
            Logger.Debug($"Swapped {pawn.Name} from {originalGender} to {pawn.gender}");
        }

        public override IEnumerable<BodyPartRecord> GetPartsToApplyOn(Pawn pawn, RecipeDef recipe)
        {
            // Unconditional return of Torso (matches your XML <appliedOnFixedBodyParts>)
            //BodyPartRecord torso = pawn.health.hediffSet.GetFirstPartOfDef(BodyPartDefOf.Torso, mustBeVisible: false);
            BodyPartRecord torso = pawn.health.hediffSet.GetNotMissingParts(BodyPartHeight.Undefined, BodyPartDepth.Undefined)
    .FirstOrDefault(p => p.def == BodyPartDefOf.Torso);
            if (torso != null)
            {
                yield return torso;
            }
            else
            {
                Logger.Warning($"[RICS] No Torso found for {pawn.Name} in GetPartsToApplyOn - surgery unavailable.");
            }
        }
        
        public override bool CompletableEver(Pawn surgeryTarget)
        {
            // Explicitly true if Torso exists (prevents bill removal)
            return GetPartsToApplyOn(surgeryTarget, recipe).Any();
        }

        public override bool AvailableOnNow(Thing thing, BodyPartRecord part = null)
        {
            // Available if pawn has Torso
            if (thing is Pawn pawn)
            {
                return GetPartsToApplyOn(pawn, recipe).Any();
            }
            return false;
        }

        public override bool IsViolationOnPawn(Pawn pawn, BodyPartRecord part, Faction billDoerFaction)
        {
            // No violation for cosmetic surgeries
            return false;
        }
    }

    public class FatBodyRecipeWorker : Recipe_InstallArtificialBodyPart
    {
        public override void ApplyOnPawn(Pawn pawn, BodyPartRecord part, Pawn billDoer, List<Thing> ingredients, Bill bill)
        {
            Logger.Debug($"FatBody ApplyOnPawn called - Pawn: {pawn?.Name}, Doctor: {billDoer?.Name}");

            if (pawn == null || pawn.Dead)
            {
                Logger.Debug("FatBody surgery aborted: Pawn null or dead.");
                return;
            }

            // Fail check + XP/tale
            if (billDoer != null && CheckSurgeryFail(billDoer, pawn, ingredients, part, bill))
            {
                Logger.Debug("FatBody surgery failed check - aborting.");
                return;
            }

            if (billDoer != null)
            {
                TaleRecorder.RecordTale(TaleDefOf.DidSurgery, billDoer, pawn);
                billDoer.skills.Learn(SkillDefOf.Medicine, 300f);
            }

            // Core effect
            pawn.story.bodyType = BodyTypeDefOf.Fat;

            // Visual refresh
            pawn.Drawer.renderer.SetAllGraphicsDirty();
            PortraitsCache.SetDirty(pawn);

            // Feedback
            Messages.Message($"Fat body surgery completed on {pawn.LabelShort}.", pawn, MessageTypeDefOf.PositiveEvent);
            Logger.Message($"Body type changed to Fat for {pawn.Name}");
        }

        public override IEnumerable<BodyPartRecord> GetPartsToApplyOn(Pawn pawn, RecipeDef recipe)
        {
            if (pawn == null || pawn.health?.hediffSet == null)
            {
                Logger.Warning($"[RICS] Pawn or HediffSet null in GetPartsToApplyOn for FatBody");
                yield break;
            }

            // Safe lookup: non-missing Torso
            var torso = pawn.health.hediffSet.GetNotMissingParts(BodyPartHeight.Undefined, BodyPartDepth.Undefined)
                .FirstOrDefault(p => p.def == BodyPartDefOf.Torso);

            if (torso != null)
            {
                yield return torso;
            }
            else
            {
                Logger.Warning($"[RICS] No valid Torso found for {pawn.Name} - FatBody surgery unavailable.");
            }
        }

        public override bool CompletableEver(Pawn surgeryTarget)
        {
            bool canComplete = GetPartsToApplyOn(surgeryTarget, recipe).Any();
            // Optional: log for debugging
            // Logger.Debug($"[RICS] FatBody CompletableEver for {surgeryTarget.Name}: {canComplete}");
            return canComplete;
        }

        public override bool AvailableOnNow(Thing thing, BodyPartRecord part = null)
        {
            if (thing is Pawn pawn)
            {
                return GetPartsToApplyOn(pawn, recipe).Any();
            }
            return false;
        }

        public override bool IsViolationOnPawn(Pawn pawn, BodyPartRecord part, Faction billDoerFaction)
        {
            // Cosmetic → no violation
            return false;
        }
    }

    public class ThinBodyRecipeWorker : Recipe_InstallArtificialBodyPart
    {
        public override void ApplyOnPawn(Pawn pawn, BodyPartRecord part, Pawn billDoer, List<Thing> ingredients, Bill bill)
        {
            Logger.Debug($"ThinBody ApplyOnPawn called - Pawn: {pawn?.Name}, Doctor: {billDoer?.Name}");

            if (pawn == null || pawn.Dead)
            {
                Logger.Debug("ThinBody surgery aborted: Pawn null or dead.");
                return;
            }

            if (billDoer != null && CheckSurgeryFail(billDoer, pawn, ingredients, part, bill)) return;

            if (billDoer != null)
            {
                TaleRecorder.RecordTale(TaleDefOf.DidSurgery, billDoer, pawn);
                billDoer.skills.Learn(SkillDefOf.Medicine, 300f);
            }

            // Set body type
            pawn.story.bodyType = BodyTypeDefOf.Thin;

            // Force graphics update
            pawn.Drawer.renderer.SetAllGraphicsDirty();
            PortraitsCache.SetDirty(pawn);

            // Record tale
            TaleRecorder.RecordTale(TaleDefOf.DidSurgery, billDoer, pawn);
            Logger.Message($"Body type changed for {pawn.Name} to Thin");
        }

        public override IEnumerable<BodyPartRecord> GetPartsToApplyOn(Pawn pawn, RecipeDef recipe)
        {
            if (pawn == null || pawn.health?.hediffSet == null)
            {
                Logger.Warning($"[RICS] Pawn or HediffSet null in GetPartsToApplyOn for FatBody");
                yield break;
            }

            // Safe lookup: non-missing Torso
            var torso = pawn.health.hediffSet.GetNotMissingParts(BodyPartHeight.Undefined, BodyPartDepth.Undefined)
                .FirstOrDefault(p => p.def == BodyPartDefOf.Torso);

            if (torso != null)
            {
                yield return torso;
            }
            else
            {
                Logger.Warning($"[RICS] No valid Torso found for {pawn.Name} - FatBody surgery unavailable.");
            }
        }

        public override bool CompletableEver(Pawn surgeryTarget)
        {
            bool canComplete = GetPartsToApplyOn(surgeryTarget, recipe).Any();
            // Optional: log for debugging
            // Logger.Debug($"[RICS] FatBody CompletableEver for {surgeryTarget.Name}: {canComplete}");
            return canComplete;
        }

        public override bool AvailableOnNow(Thing thing, BodyPartRecord part = null)
        {
            if (thing is Pawn pawn)
            {
                return GetPartsToApplyOn(pawn, recipe).Any();
            }
            return false;
        }

        public override bool IsViolationOnPawn(Pawn pawn, BodyPartRecord part, Faction billDoerFaction)
        {
            // Cosmetic → no violation
            return false;
        }

    }

    public class HulkBodyRecipeWorker : Recipe_InstallArtificialBodyPart
    {
        public override void ApplyOnPawn(Pawn pawn, BodyPartRecord part, Pawn billDoer, List<Thing> ingredients, Bill bill)
        {
            base.ApplyOnPawn(pawn, part, billDoer, ingredients, bill);
            if (pawn == null || pawn.Dead) return;


            if (billDoer != null && CheckSurgeryFail(billDoer, pawn, ingredients, part, bill)) return;

            if (billDoer != null)
            {
                TaleRecorder.RecordTale(TaleDefOf.DidSurgery, billDoer, pawn);
                billDoer.skills.Learn(SkillDefOf.Medicine, 300f);
            }
            // Set body type
            pawn.story.bodyType = BodyTypeDefOf.Hulk;

            // Force graphics update
            pawn.Drawer.renderer.SetAllGraphicsDirty();
            PortraitsCache.SetDirty(pawn);

            // Record tale
            TaleRecorder.RecordTale(TaleDefOf.DidSurgery, billDoer, pawn);
            Logger.Message($"Body type changed for {pawn.Name} to Hulk");
        }

        public override IEnumerable<BodyPartRecord> GetPartsToApplyOn(Pawn pawn, RecipeDef recipe)
        {
            if (pawn == null || pawn.health?.hediffSet == null)
            {
                Logger.Warning($"[RICS] Pawn or HediffSet null in GetPartsToApplyOn for FatBody");
                yield break;
            }

            // Safe lookup: non-missing Torso
            var torso = pawn.health.hediffSet.GetNotMissingParts(BodyPartHeight.Undefined, BodyPartDepth.Undefined)
                .FirstOrDefault(p => p.def == BodyPartDefOf.Torso);

            if (torso != null)
            {
                yield return torso;
            }
            else
            {
                Logger.Warning($"[RICS] No valid Torso found for {pawn.Name} - FatBody surgery unavailable.");
            }
        }

        public override bool CompletableEver(Pawn surgeryTarget)
        {
            bool canComplete = GetPartsToApplyOn(surgeryTarget, recipe).Any();
            // Optional: log for debugging
            // Logger.Debug($"[RICS] FatBody CompletableEver for {surgeryTarget.Name}: {canComplete}");
            return canComplete;
        }

        public override bool AvailableOnNow(Thing thing, BodyPartRecord part = null)
        {
            if (thing is Pawn pawn)
            {
                return GetPartsToApplyOn(pawn, recipe).Any();
            }
            return false;
        }

        public override bool IsViolationOnPawn(Pawn pawn, BodyPartRecord part, Faction billDoerFaction)
        {
            // Cosmetic → no violation
            return false;
        }

    }

    public class MasculineBodyRecipeWorker : Recipe_InstallArtificialBodyPart
    {
        public override void ApplyOnPawn(Pawn pawn, BodyPartRecord part, Pawn billDoer, List<Thing> ingredients, Bill bill)
        {
            base.ApplyOnPawn(pawn, part, billDoer, ingredients, bill);
            if (pawn == null || pawn.Dead) return;

            if (billDoer != null && CheckSurgeryFail(billDoer, pawn, ingredients, part, bill)) return;

            if (billDoer != null)
            {
                TaleRecorder.RecordTale(TaleDefOf.DidSurgery, billDoer, pawn);
                billDoer.skills.Learn(SkillDefOf.Medicine, 300f);
            }
            // Set body type
            pawn.story.bodyType = BodyTypeDefOf.Male;

            // Force graphics update
            pawn.Drawer.renderer.SetAllGraphicsDirty();
            PortraitsCache.SetDirty(pawn);

            // Record tale
            TaleRecorder.RecordTale(TaleDefOf.DidSurgery, billDoer, pawn);
            Logger.Message($"Body type changed for {pawn.Name} to Masculine");
        }
        public override IEnumerable<BodyPartRecord> GetPartsToApplyOn(Pawn pawn, RecipeDef recipe)
        {
            if (pawn == null || pawn.health?.hediffSet == null)
            {
                Logger.Warning($"[RICS] Pawn or HediffSet null in GetPartsToApplyOn for FatBody");
                yield break;
            }

            // Safe lookup: non-missing Torso
            var torso = pawn.health.hediffSet.GetNotMissingParts(BodyPartHeight.Undefined, BodyPartDepth.Undefined)
                .FirstOrDefault(p => p.def == BodyPartDefOf.Torso);

            if (torso != null)
            {
                yield return torso;
            }
            else
            {
                Logger.Warning($"[RICS] No valid Torso found for {pawn.Name} - FatBody surgery unavailable.");
            }
        }

        public override bool CompletableEver(Pawn surgeryTarget)
        {
            bool canComplete = GetPartsToApplyOn(surgeryTarget, recipe).Any();
            // Optional: log for debugging
            // Logger.Debug($"[RICS] FatBody CompletableEver for {surgeryTarget.Name}: {canComplete}");
            return canComplete;
        }

        public override bool AvailableOnNow(Thing thing, BodyPartRecord part = null)
        {
            if (thing is Pawn pawn)
            {
                return GetPartsToApplyOn(pawn, recipe).Any();
            }
            return false;
        }

        public override bool IsViolationOnPawn(Pawn pawn, BodyPartRecord part, Faction billDoerFaction)
        {
            // Cosmetic → no violation
            return false;
        }

    }

    public class FeminineBodyRecipeWorker : Recipe_Surgery
    {
        public override void ApplyOnPawn(Pawn pawn, BodyPartRecord part, Pawn billDoer, List<Thing> ingredients, Bill bill)
        {
            base.ApplyOnPawn(pawn, part, billDoer, ingredients, bill);
            if (pawn == null || pawn.Dead) return;

            if (billDoer != null && CheckSurgeryFail(billDoer, pawn, ingredients, part, bill)) return;

            if (billDoer != null)
            {
                TaleRecorder.RecordTale(TaleDefOf.DidSurgery, billDoer, pawn);
                billDoer.skills.Learn(SkillDefOf.Medicine, 300f);
            }

            // Set body type
            pawn.story.bodyType = BodyTypeDefOf.Female;

            // Force graphics update
            pawn.Drawer.renderer.SetAllGraphicsDirty();
            PortraitsCache.SetDirty(pawn);

            // Record tale
            TaleRecorder.RecordTale(TaleDefOf.DidSurgery, billDoer, pawn);
            Logger.Message($"Body type changed for {pawn.Name} to Feminine");
        }
        public override IEnumerable<BodyPartRecord> GetPartsToApplyOn(Pawn pawn, RecipeDef recipe)
        {
            if (pawn == null || pawn.health?.hediffSet == null)
            {
                Logger.Warning($"[RICS] Pawn or HediffSet null in GetPartsToApplyOn for FatBody");
                yield break;
            }

            // Safe lookup: non-missing Torso
            var torso = pawn.health.hediffSet.GetNotMissingParts(BodyPartHeight.Undefined, BodyPartDepth.Undefined)
                .FirstOrDefault(p => p.def == BodyPartDefOf.Torso);

            if (torso != null)
            {
                yield return torso;
            }
            else
            {
                Logger.Warning($"[RICS] No valid Torso found for {pawn.Name} - FatBody surgery unavailable.");
            }
        }

        public override bool CompletableEver(Pawn surgeryTarget)
        {
            bool canComplete = GetPartsToApplyOn(surgeryTarget, recipe).Any();
            // Optional: log for debugging
            // Logger.Debug($"[RICS] FatBody CompletableEver for {surgeryTarget.Name}: {canComplete}");
            return canComplete;
        }

        public override bool AvailableOnNow(Thing thing, BodyPartRecord part = null)
        {
            if (thing is Pawn pawn)
            {
                return GetPartsToApplyOn(pawn, recipe).Any();
            }
            return false;
        }

        public override bool IsViolationOnPawn(Pawn pawn, BodyPartRecord part, Faction billDoerFaction)
        {
            // Cosmetic → no violation
            return false;
        }
    }
}