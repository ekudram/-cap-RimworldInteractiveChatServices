// EnhancedInteractionCommandHandler.cs
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;
using Verse.AI;

namespace CAP_ChatInteractive.Commands.CommandHandlers
{
    public static class EnhancedInteractionCommandHandler
    {
        private static readonly Dictionary<InteractionDef, InteractionInfo> InteractionData =
            new Dictionary<InteractionDef, InteractionInfo>
        {
            { InteractionDefOf.Chitchat, new InteractionInfo { IsNegative = false, Cost = 10, KarmaCost = 0 } },
            { InteractionDefOf.DeepTalk, new InteractionInfo { IsNegative = false, Cost = 15, KarmaCost = 0 } },
            { InteractionDefOf.Insult, new InteractionInfo { IsNegative = true, Cost = 5, KarmaCost = 5 } },
            { InteractionDefOf.RomanceAttempt, new InteractionInfo { IsNegative = false, Cost = 20, KarmaCost = 0 } },
            { InteractionDefOf.MarriageProposal, new InteractionInfo { IsNegative = false, Cost = 50, KarmaCost = 10 } },
            { InteractionDefOf.BuildRapport, new InteractionInfo { IsNegative = false, Cost = 25, KarmaCost = 0 } },
            { InteractionDefOf.ConvertIdeoAttempt, new InteractionInfo { IsNegative = false, Cost = 30, KarmaCost = 15 } },
            { InteractionDefOf.Reassure, new InteractionInfo { IsNegative = false, Cost = 12, KarmaCost = 0 } },
            { InteractionDefOf.Nuzzle, new InteractionInfo { IsNegative = false, Cost = 8, KarmaCost = 0 } },
            { InteractionDefOf.AnimalChat, new InteractionInfo { IsNegative = false, Cost = 10, KarmaCost = 0 } }
        };

        public static string HandleInteractionCommand(ChatMessageWrapper user, InteractionDef interaction, string[] args)
        {
            try
            {
                // Get viewer data
                var viewer = Viewers.GetViewer(user);
                if (viewer == null) return "Could not find your viewer data.";

                // Check interaction validity and cost
                if (interaction == null) return "This interaction is not available.";

                if (!InteractionData.TryGetValue(interaction, out var interactionInfo))
                    interactionInfo = new InteractionInfo(); // Default values

                if (viewer.GetCoins() < interactionInfo.Cost)
                {
                    var settings = CAPChatInteractiveMod.Instance.Settings.GlobalSettings;
                    var currencySymbol = settings.CurrencyName?.Trim() ?? "¢";
                    return $"You need {interactionInfo.Cost}{currencySymbol} to use this interaction. You have {viewer.GetCoins()}{currencySymbol}.";
                }

                // Check karma for negative interactions
                if (interactionInfo.IsNegative && viewer.Karma < interactionInfo.KarmaCost)
                    return $"You need at least {interactionInfo.KarmaCost} karma to use negative interactions. You have {viewer.Karma} karma.";

                // Get pawns
                var assignmentManager = CAPChatInteractiveMod.GetPawnAssignmentManager();
                var initiatorPawn = assignmentManager.GetAssignedPawn(user);
                if (initiatorPawn == null) return "You don't have an active pawn. Use !pawn to purchase one!";

                // Find target pawn
                Pawn targetPawn = FindInteractionTarget(initiatorPawn, args);
                if (targetPawn == null) return "No valid target found for interaction.";

                // Check if pawn can interact
                if (!CanPawnsInteract(initiatorPawn, targetPawn))
                    return $"{initiatorPawn.Name} cannot interact with {targetPawn.Name} right now.";

                // Create and assign the social visit job
                Job socialJob = JobMaker.MakeJob(JobDefOf_CAP.CAP_SocialVisit, targetPawn);
                socialJob.interaction = interaction; // Store which interaction to use

                initiatorPawn.jobs.StartJob(socialJob, JobCondition.InterruptForced);

                // Deduct cost immediately
                viewer.TakeCoins(interactionInfo.Cost);

                // Apply karma penalty for negative interactions
                if (interactionInfo.IsNegative)
                    viewer.SetKarma(Math.Max(viewer.Karma - interactionInfo.KarmaCost, 0));

                Viewers.SaveViewers();

                return $"{initiatorPawn.Name} is going to visit {targetPawn.Name} for a {interaction.label}...";
            }
            catch (Exception ex)
            {
                Logger.Error($"Error in enhanced interaction command: {ex}");
                return "An error occurred while processing the interaction.";
            }
        }

        private static Pawn FindInteractionTarget(Pawn initiator, string[] args)
        {
            // If args provided, try to find specific target
            if (args.Length > 0)
            {
                string targetQuery = args[0];

                // Remove @ symbol if present
                if (targetQuery.StartsWith("@"))
                    targetQuery = targetQuery.Substring(1);

                // Try to find by username
                var assignmentManager = CAPChatInteractiveMod.GetPawnAssignmentManager();
                if (assignmentManager != null && assignmentManager.HasAssignedPawn(targetQuery))
                {
                    var targetPawn = assignmentManager.GetAssignedPawn(targetQuery);
                    if (targetPawn != null && targetPawn != initiator) return targetPawn;
                }

                // Try to find by pawn name
                var namedPawn = FindPawnByName(targetQuery);
                if (namedPawn != null && namedPawn != initiator) return namedPawn;

                return null; // Specific target not found
            }

            // No target specified - find random colonist
            return FindRandomColonist(initiator);
        }

        private static Pawn FindPawnByName(string name)
        {
            return PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_FreeColonists
                .FirstOrDefault(p => !p.Dead && p.Name != null &&
                    p.Name.ToString().ToLower().Contains(name.ToLower()));
        }

        private static Pawn FindRandomColonist(Pawn excludePawn)
        {
            var colonists = PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_FreeColonists
                .Where(p => !p.Dead && p != excludePawn)
                .ToList();
            return colonists.Count > 0 ? colonists.RandomElement() : null;
        }

        private static bool CanPawnsInteract(Pawn initiator, Pawn target)
        {
            if (initiator == null || target == null) return false;
            if (initiator.Dead || target.Dead) return false;
            if (!initiator.Spawned || !target.Spawned) return false;
            if (initiator.Downed || target.Downed) return false;

            return true;
        }

        private class InteractionInfo
        {
            public bool IsNegative { get; set; } = false;
            public int Cost { get; set; } = 10;
            public int KarmaCost { get; set; } = 0;
        }
    }
}