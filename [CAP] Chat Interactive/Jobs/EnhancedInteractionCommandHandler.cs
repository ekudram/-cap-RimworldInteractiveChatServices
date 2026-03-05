// EnhancedInteractionCommandHandler.cs

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

        public class FlirtSettings
        {
            public float MinMoodThreshold { get; set; } = 0.30f;
            public float StressedMoodThreshold { get; set; } = 0.40f;
            public int MinOpinionForAutoSuccess { get; set; } = 20;
            public int NegativeOpinionRefuseChance { get; set; } = 80; // 80%
            public bool CheckExistingRelationships { get; set; } = true;

            // You can make this configurable via your mod settings
            public static FlirtSettings Instance = new FlirtSettings();
        }

        public static string HandleInteractionCommand(ChatMessageWrapper messageWrapper, InteractionDef interaction, string[] args)
        {
            try
            {
                // Get viewer data
                var viewer = Viewers.GetViewer(messageWrapper);
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
                var initiatorPawn = assignmentManager.GetAssignedPawn(messageWrapper);
                if (initiatorPawn == null) return "You don't have an active pawn. Use !pawn to purchase one!";

                // Find target pawn - now passes interaction type so !nuzzle/!animalchat can target named colony animals
                Pawn targetPawn = FindInteractionTarget(initiatorPawn, interaction, args);
                if (targetPawn == null) return "No valid target found for interaction.";

                if (!CanPawnsInteract(initiatorPawn, targetPawn))
                    return $"{initiatorPawn.Name} cannot interact with {targetPawn.Name} right now.";

                // Special handling for romance/flirt interactions
                if (interaction == InteractionDefOf.RomanceAttempt ||
                    (interaction.defName?.ToLower().Contains("flirt") == true))
                {
                    if (!CanFlirt(initiatorPawn, targetPawn, out string refusalMessage))
                    {
                        // Refund cost since interaction won't happen
                        viewer.GiveCoins(interactionInfo.Cost);
                        Viewers.SaveViewers();

                        return refusalMessage;
                    }
                }

                // Create and assign the social visit job
                Job socialJob = JobMaker.MakeJob(JobDefOf_CAP.CAP_SocialVisit, targetPawn);
                socialJob.interaction = interaction; // Store which interaction to useI coul

                initiatorPawn.jobs.StartJob(socialJob, JobCondition.InterruptForced);

                // Deduct cost immediately
                viewer.TakeCoins(interactionInfo.Cost);

                // Apply karma penalty for negative interactions
                if (interactionInfo.IsNegative)
                    viewer.SetKarma(Math.Max(viewer.Karma - interactionInfo.KarmaCost, 0));

                Viewers.SaveViewers();

                return $" is going to visit {targetPawn.Name} for a {interaction.label}...";
            }
            catch (Exception ex)
            {
                Logger.Error($"Error in enhanced interaction command: {ex}");
                return "An error occurred while processing the interaction.";
            }
        }

        private static Pawn FindInteractionTarget(Pawn initiator, InteractionDef interaction, string[] args)
        {
            // Animal-specific commands now search colony animals (named only)
            bool isAnimalInteraction = interaction != null &&
                (interaction == InteractionDefOf.Nuzzle ||
                 interaction.defName?.ToLowerInvariant() == "animalchat" ||
                 interaction.defName?.ToLowerInvariant().Contains("animalchat") == true);

            // If args provided, try to find specific target
            if (args.Length > 0)
            {
                string targetQuery = args[0];

                // Remove @ symbol if present
                if (targetQuery.StartsWith("@"))
                    targetQuery = targetQuery.Substring(1);

                if (isAnimalInteraction)
                {
                    // New: colony animals by name (e.g. !nuzzle Fluffy)
                    var targetAnimal = FindAnimalByName(targetQuery);
                    if (targetAnimal != null && targetAnimal != initiator) return targetAnimal;
                }
                else
                {
                    // Existing human/colonist logic (unchanged)
                    var assignmentManager = CAPChatInteractiveMod.GetPawnAssignmentManager();
                    if (assignmentManager != null && assignmentManager.HasAssignedPawn(targetQuery))
                    {
                        var targetPawn = assignmentManager.GetAssignedPawn(targetQuery);
                        if (targetPawn != null && targetPawn != initiator) return targetPawn;
                    }

                    // Try to find by pawn name
                    var namedPawn = FindPawnByName(targetQuery);
                    if (namedPawn != null && namedPawn != initiator) return namedPawn;
                }

                return null; // Specific target not found
            }

            // No target specified - use random animal for nuzzle/animalchat, random colonist otherwise
            return isAnimalInteraction
                ? FindRandomColonistAnimal(initiator)
                : FindRandomColonist(initiator);
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

        // NEW: Named colony animals only (Faction.OfPlayer + has name) - used by !nuzzle and !animalchat
        private static Pawn FindAnimalByName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;

            return PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive
                .FirstOrDefault(p => p.RaceProps != null && p.RaceProps.Animal &&
                                     p.Faction == Faction.OfPlayer &&
                                     !p.Dead &&
                                     p.Name != null &&
                                     p.Name.ToString().ToLowerInvariant().Contains(name.ToLowerInvariant()));
        }

        // NEW: Random named colony animal (fallback when no target name given)
        private static Pawn FindRandomColonistAnimal(Pawn excludePawn)
        {
            var animals = PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive
                .Where(p => p.RaceProps != null && p.RaceProps.Animal &&
                            p.Faction == Faction.OfPlayer &&
                            !p.Dead &&
                            p != excludePawn &&
                            p.Name != null)
                .ToList();

            return animals.Count > 0 ? animals.RandomElement() : null;
        }

        private static bool CanPawnsInteract(Pawn initiator, Pawn target)
        {
            if (initiator == null || target == null) return false;
            if (initiator.Dead || target.Dead) return false;
            if (!initiator.Spawned || !target.Spawned) return false;
            if (initiator.Downed || target.Downed) return false;

            return true;
        }

        // Add this method inside the EnhancedInteractionCommandHandler class
        private static bool CanFlirt(Pawn initiator, Pawn target, out string refusalMessage)
        {
            refusalMessage = null;

            if (target == null || initiator == null)
            {
                refusalMessage = "No valid target found.";
                return false;
            }

            if (initiator == target)
            {
                refusalMessage = "You can't flirt with yourself!";
                return false;
            }

            // Step 1: Basic checks (already partially in CanPawnsInteract, but add more)
            if (initiator.Dead || target.Dead)
            {
                refusalMessage = $"{target.Name} is not available.";
                return false;
            }

            if (initiator.Downed || target.Downed)
            {
                refusalMessage = $"{target.Name} is incapacitated.";
                return false;
            }

            // Step 2: Mood check on target (the one being flirted with)
            var targetMood = target.needs.mood;
            if (targetMood != null)
            {
                float targetMoodPct = targetMood.CurLevelPercentage;

                // Hard refuse if target is in a very bad mood
                if (targetMoodPct < 0.30f)  // Below 30% - near mental break
                {
                    refusalMessage = $"{target.Name} is feeling too down for this.";

                    // Small mood hit to initiator for insensitive timing
                    var initiatorMood = initiator.needs.mood;
                    if (initiatorMood != null && Rand.Value < 0.5f)
                    {
                        initiatorMood.thoughts.memories.TryGainMemory(ThoughtDefOf.RebuffedMyRomanceAttempt);
                    }
                    return false;
                }
                // Soft refuse if target is stressed
                else if (targetMoodPct < 0.40f && Rand.Value < 0.7f)  // 70% chance to refuse when stressed
                {
                    refusalMessage = $"{target.Name} isn't in the mood right now.";
                    return false;
                }
            }

            // Step 3: Opinion check (target's opinion of initiator)
            int opinion = target.relations.OpinionOf(initiator);

            // Hard refuse if target dislikes initiator
            if (opinion < -10)  // Strong dislike
            {
                refusalMessage = $"{target.Name} wants nothing to do with {initiator.Name}.";
                return false;
            }

            // Likely refuse if neutral-negative
            if (opinion < 0 && Rand.Value < 0.8f)  // 80% chance to refuse
            {
                refusalMessage = $"{target.Name} isn't interested.";
                return false;
            }


            // Unlikely but possible refusal for neutral opinion
            if (opinion < 10 && Rand.Value < 0.3f)  // 30% chance to refuse
            {
                refusalMessage = $"{target.Name} seems unsure about this.";
                return false;
            }

            // Step 4: Check for existing relationships (optional but recommended)
            if (LovePartnerRelationUtility.HasAnyLovePartner(target))
            {
                Pawn existingPartner = LovePartnerRelationUtility.ExistingMostLikedLovePartner(target, false);
                if (existingPartner != null)
                {
                    int partnerOpinion = target.relations.OpinionOf(existingPartner);
                    if (partnerOpinion >= 15 && Rand.Value < 0.6f)  // 60% chance to refuse if happy with partner
                    {
                        refusalMessage = $"{target.Name} is committed to {existingPartner.Name}.";
                        return false;
                    }
                }
            }

            // Step 5: Trait-based checks
            if (target.story != null && target.story.traits != null)
            {
                // Psychopaths are less receptive to romance
                if (target.story.traits.HasTrait(TraitDefOf.Psychopath) && Rand.Value < 0.4f)
                {
                    refusalMessage = $"{target.Name} coldly rejects the advance.";
                    return false;
                }

                // Abrasive pawns more likely to be rude
                if (target.story.traits.HasTrait(TraitDefOf.Abrasive) && Rand.Value < 0.3f)
                {
                    refusalMessage = $"{target.Name} snaps: \"Leave me alone!\"";
                    return false;
                }
            }

            return true; // All checks passed
        }

        private class InteractionInfo
        {
            public bool IsNegative { get; set; } = false;
            public int Cost { get; set; } = 10;
            public int KarmaCost { get; set; } = 0;
        }
    }
}