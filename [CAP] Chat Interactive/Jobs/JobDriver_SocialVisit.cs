// JobDriver_SocialVisit.cs - Enhanced version
using RimWorld;
using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace CAP_ChatInteractive
{
    public class JobDriver_SocialVisit : JobDriver
    {
        private const int WaitTimeoutTicks = 300; // 5 seconds real-time
        private const int CheckInterval = 60; // Check every second

        private Pawn TargetPawn => job.targetA.Thing as Pawn;
        private InteractionDef Interaction => job.interaction;
        private int waitTicks;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(job.targetA, job, 1, -1, null, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            // Fail conditions
            this.AddFailCondition(() => TargetPawn == null || TargetPawn.Dead || TargetPawn.Destroyed);
            this.AddFailCondition(() => pawn == null || pawn.Dead || pawn.Destroyed);

            // Stage 1: Go to target pawn
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch)
                .FailOn(() => !CanInteractWithTarget(TargetPawn));

            // Stage 2: Wait for target to be available
            Toil waitToil = new Toil();
            waitToil.initAction = () => waitTicks = 0;
            waitToil.tickAction = () =>
            {
                waitTicks++;
                if (waitTicks % CheckInterval == 0)
                {
                    if (CanInteractNow(TargetPawn))
                    {
                        ReadyForNextToil();
                    }
                    else if (waitTicks >= WaitTimeoutTicks)
                    {
                        EndJobWith(JobCondition.Incompletable);
                    }
                }
            };
            waitToil.defaultCompleteMode = ToilCompleteMode.Never;
            waitToil.WithProgressBar(TargetIndex.A, () => waitTicks / (float)WaitTimeoutTicks);
            yield return waitToil;

            // Stage 3: Execute interaction
            yield return new Toil
            {
                initAction = () =>
                {
                    if (CanInteractNow(TargetPawn) && Interaction != null)
                    {
                        bool success = pawn.interactions.TryInteractWith(TargetPawn, Interaction);
                        Logger.Debug($"Social interaction {Interaction.defName} result: {success}");
                    }
                },
                defaultCompleteMode = ToilCompleteMode.Instant
            };
        }

        private bool CanInteractWithTarget(Pawn target)
        {
            return target != null &&
                   !target.Dead &&
                   !target.Destroyed &&
                   target.Spawned &&
                   !target.Downed;
        }

        private bool CanInteractNow(Pawn target)
        {
            if (!CanInteractWithTarget(target)) return false;

            // Check if target is sleeping, in mental break, etc.
            if (target.jobs.curDriver != null && target.jobs.curDriver.asleep) return false;
            if (target.CurJob != null && target.CurJob.def == JobDefOf.LayDown) return false;
            if (target.InMentalState) return false;
            if (target.Drafted && target.CurJob != null && target.CurJob.def == JobDefOf.AttackStatic) return false;

            return true;
        }

        public override string GetReport()
        {
            if (Interaction != null)
            {
                return Interaction.defName switch
                {
                    "Chitchat" => "Having a chat with " + TargetPawn?.Name,
                    "DeepTalk" => "Having a deep talk with " + TargetPawn?.Name,
                    "Insult" => "Insulting " + TargetPawn?.Name,
                    "RomanceAttempt" => "Flirting with " + TargetPawn?.Name,
                    "MarriageProposal" => "Proposing to " + TargetPawn?.Name,
                    "BuildRapport" => "Building rapport with " + TargetPawn?.Name,
                    "ConvertIdeoAttempt" => "Converting " + TargetPawn?.Name,
                    "Reassure" => "Reassuring " + TargetPawn?.Name,
                    "Nuzzle" => "Nuzzling with " + TargetPawn?.Name,
                    "AnimalChat" => "Chatting with animal " + TargetPawn?.Name,
                    _ => "Visiting " + TargetPawn?.Name
                };
            }
            return base.GetReport();
        }
    }
}