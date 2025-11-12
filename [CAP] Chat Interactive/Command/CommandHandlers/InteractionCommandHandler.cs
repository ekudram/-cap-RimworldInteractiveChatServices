// InteractionCommandHandler.cs
// Copyright (c) Captolamia. All rights reserved.
// Licensed under the AGPLv3 License. See LICENSE file in the project root for full license information.

using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace CAP_ChatInteractive.Commands.CommandHandlers
{
    public static class InteractionCommandHandler
    {
        private static readonly Dictionary<InteractionDef, InteractionInfo> InteractionData = new Dictionary<InteractionDef, InteractionInfo>
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
                var viewer = Viewers.GetViewer(user);  // This is correct - uses platform ID lookup
                if (viewer == null)
                {
                    return "Could not find your viewer data.";
                }

                if (viewer == null)
                {
                    return "Could not find your viewer data.";
                }

                // Check if interaction is valid
                if (interaction == null)
                {
                    return "This interaction is not available.";
                }

                // Get interaction info
                if (!InteractionData.TryGetValue(interaction, out var interactionInfo))
                {
                    interactionInfo = new InteractionInfo(); // Default values
                }

                // Check if viewer has enough coins
                if (viewer.GetCoins() < interactionInfo.Cost)
                {
                    var settings = CAPChatInteractiveMod.Instance.Settings.GlobalSettings;
                    var currencySymbol = settings.CurrencyName?.Trim() ?? "¢";
                    return $"You need {interactionInfo.Cost}{currencySymbol} to use this interaction. You have {viewer.GetCoins()}{currencySymbol}.";
                }

                // Check karma for negative interactions
                if (interactionInfo.IsNegative && viewer.Karma < interactionInfo.KarmaCost)
                {
                    return $"You need at least {interactionInfo.KarmaCost} karma to use negative interactions. You have {viewer.Karma} karma.";
                }

                // Get the viewer's pawn
                var assignmentManager = CAPChatInteractiveMod.GetPawnAssignmentManager();
                if (assignmentManager == null)
                {
                    return "Pawn assignment system not available.";
                }

                var pawn = assignmentManager.GetAssignedPawn(user);  // CHANGED: user instead of user.Username
                if (pawn == null)
                {
                    return "You don't have an active pawn. Use !pawn to purchase one!";
                }

                // Find target pawn
                Pawn targetPawn = FindInteractionTarget(pawn, args);
                if (targetPawn == null)
                {
                    return "No valid target found for interaction. Usage: !" + interaction.defName.ToLower() + " [@username|random]";
                }

                // Check if pawn can interact
                if (!CanPawnInteract(pawn, targetPawn))
                {
                    return $"{pawn.Name} cannot interact with {targetPawn.Name} right now.";
                }

                // Execute the interaction
                string result = ExecuteInteraction(pawn, targetPawn, interaction, interactionInfo);

                // Deduct cost
                if (result.StartsWith("Success:"))
                {
                    viewer.TakeCoins(interactionInfo.Cost);

                    // Apply karma penalty for negative interactions
                    if (interactionInfo.IsNegative)
                    {
                        viewer.SetKarma(Math.Max(viewer.Karma - interactionInfo.KarmaCost, 0));
                    }

                    Viewers.SaveViewers();
                }

                return result;
            }
            catch (Exception ex)
            {
                Logger.Error($"Error in interaction command: {ex}");
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
                {
                    targetQuery = targetQuery.Substring(1);
                }

                // Try to find by username - FIXED: Only use assignment manager, don't create new viewers
                var assignmentManager = CAPChatInteractiveMod.GetPawnAssignmentManager();
                if (assignmentManager != null)
                {
                    // Check if the target actually has an assigned pawn before creating a viewer
                    if (assignmentManager.HasAssignedPawn(targetQuery))
                    {
                        var targetPawn = assignmentManager.GetAssignedPawn(targetQuery);
                        if (targetPawn != null && targetPawn != initiator)
                        {
                            return targetPawn;
                        }
                    }
                }

                // Try to find by pawn name (existing colonists only, no viewer creation)
                var namedPawn = FindPawnByName(targetQuery);
                if (namedPawn != null && namedPawn != initiator)
                {
                    return namedPawn;
                }

                return null; // Specific target not found
            }

            // No target specified - find random colonist
            return FindRandomColonist(initiator);
        }

        private static Pawn FindPawnByName(string name)
        {
            return PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_FreeColonists
                .Where(p => !p.Dead && p.Name != null)
                .FirstOrDefault(p => p.Name.ToString().ToLower().Contains(name.ToLower()));
        }

        private static Pawn FindRandomColonist(Pawn excludePawn)
        {
            var colonists = PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_FreeColonists
                .Where(p => !p.Dead && p != excludePawn)
                .ToList();

            return colonists.Count > 0 ? colonists.RandomElement() : null;
        }

        private static bool CanPawnInteract(Pawn initiator, Pawn target)
        {
            if (initiator == null || target == null) return false;
            if (initiator.Dead || target.Dead) return false;
            if (!initiator.Spawned || !target.Spawned) return false;
            if (initiator.Downed || target.Downed) return false;

            // Check if they're capable of social interaction
            if (initiator.story != null && initiator.story.traits != null)
            {
                if (initiator.story.traits.HasTrait(TraitDefOf.AnnoyingVoice) ||
                    initiator.story.traits.HasTrait(TraitDefOf.CreepyBreathing))
                {
                    // These traits might affect social ability, but don't prevent it entirely
                }
            }

            return true;
        }

        private static string ExecuteInteraction(Pawn initiator, Pawn target, InteractionDef interaction, InteractionInfo interactionInfo)
        {
            try
            {
                // Use the interaction worker to execute the interaction
                var result = initiator.interactions.TryInteractWith(target, interaction);

                if (result)
                {
                    string successMessage = GetSuccessMessage(initiator, target, interaction);
                    return $"Success: {successMessage}";
                }
                else
                {
                    return $"{initiator.Name} tried to {interaction.label.ToLower()} with {target.Name}, but it was unable.";
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error executing interaction: {ex}");
                return $"Failed to execute {interaction.label} interaction.";
            }
        }

        private static bool HasAssignedPawn(string username)
        {
            var assignmentManager = CAPChatInteractiveMod.GetPawnAssignmentManager();
            return assignmentManager != null && assignmentManager.HasAssignedPawn(username);
        }

        private static string GetSuccessMessage(Pawn initiator, Pawn target, InteractionDef interaction)
        {
            // Generate appropriate success messages based on interaction type
            return interaction.defName switch
            {
                "Chitchat" => $"{initiator.Name} had a pleasant chat with {target.Name} 💬",
                "DeepTalk" => $"{initiator.Name} had a deep conversation with {target.Name} 🗣️",
                "Insult" => $"{initiator.Name} insulted {target.Name} 😠",
                "RomanceAttempt" => $"{initiator.Name} flirted with {target.Name} 😘",
                "MarriageProposal" => $"{initiator.Name} proposed marriage to {target.Name} 💍",
                "BuildRapport" => $"{initiator.Name} built rapport with {target.Name} 🤝",
                "ConvertIdeoAttempt" => $"{initiator.Name} attempted to convert {target.Name} to their ideology ⛪",
                "Reassure" => $"{initiator.Name} reassured {target.Name} 🛡️",
                "Nuzzle" => $"{initiator.Name} nuzzled with {target.Name} 🐾",
                "AnimalChat" => $"{initiator.Name} chatted with {target.Name} 🐶",
                _ => $"{initiator.Name} interacted with {target.Name}"
            };
        }

        private class InteractionInfo
        {
            public bool IsNegative { get; set; } = false;
            public int Cost { get; set; } = 10;
            public int KarmaCost { get; set; } = 0;
        }
    }
}