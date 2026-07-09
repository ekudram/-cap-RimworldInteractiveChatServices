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
        private static readonly Dictionary<InteractionDef, InteractionInfo> InteractionData = new Dictionary<InteractionDef, InteractionInfo>();

        static EnhancedInteractionCommandHandler()
        {
            // Safely add only non-null InteractionDefs (some require DLC like Ideology)
            AddIfNotNull(InteractionDefOf.Chitchat, new InteractionInfo { IsNegative = false, Cost = 10, KarmaCost = 0 });
            AddIfNotNull(InteractionDefOf.DeepTalk, new InteractionInfo { IsNegative = false, Cost = 15, KarmaCost = 0 });
            AddIfNotNull(InteractionDefOf.Insult, new InteractionInfo { IsNegative = true, Cost = 5, KarmaCost = 5 });
            AddIfNotNull(InteractionDefOf.RomanceAttempt, new InteractionInfo { IsNegative = false, Cost = 20, KarmaCost = 0 });
            AddIfNotNull(InteractionDefOf.MarriageProposal, new InteractionInfo { IsNegative = false, Cost = 50, KarmaCost = 10 });
            AddIfNotNull(InteractionDefOf.BuildRapport, new InteractionInfo { IsNegative = false, Cost = 25, KarmaCost = 0 });
            AddIfNotNull(InteractionDefOf.ConvertIdeoAttempt, new InteractionInfo { IsNegative = false, Cost = 30, KarmaCost = 15 });
            AddIfNotNull(InteractionDefOf.Reassure, new InteractionInfo { IsNegative = false, Cost = 12, KarmaCost = 0 });
            AddIfNotNull(InteractionDefOf.Nuzzle, new InteractionInfo { IsNegative = false, Cost = 8, KarmaCost = 0 });
            AddIfNotNull(InteractionDefOf.AnimalChat, new InteractionInfo { IsNegative = false, Cost = 10, KarmaCost = 0 });
        }

        private static void AddIfNotNull(InteractionDef def, InteractionInfo info)
        {
            if (def != null && info != null)
            {
                InteractionData[def] = info;
            }
        }

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

                // Check if this sub-command is enabled (CustomData added for streamer control)
                string cmdName = null;
                if (interaction == InteractionDefOf.Chitchat) cmdName = "chitchat";
                else if (interaction == InteractionDefOf.DeepTalk) cmdName = "deeptalk";
                else if (interaction == InteractionDefOf.Insult) cmdName = "insult";
                else if (interaction == InteractionDefOf.RomanceAttempt) cmdName = "flirt";
                else if (interaction == InteractionDefOf.Reassure) cmdName = "reassure";
                else if (interaction == InteractionDefOf.Nuzzle) cmdName = "nuzzle";
                else if (interaction == InteractionDefOf.AnimalChat) cmdName = "animalchat";
                else if (interaction == InteractionDefOf.MarriageProposal) cmdName = "marry";
                else if (interaction == InteractionDefOf.BuildRapport) cmdName = "buildrapport";
                else if (interaction == InteractionDefOf.ConvertIdeoAttempt) cmdName = "convert";
                if (cmdName != null)
                {
                    var cmdSettings = CommandSettingsManager.GetSettings(cmdName);
                    if (!cmdSettings.GetCustom<bool>("enabled", true))
                    {
                        return $"Sub Command {cmdName} is disabled.";
                    }
                }

                InteractionInfo interactionInfo;
                if (InteractionData == null || !InteractionData.TryGetValue(interaction, out interactionInfo))
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
                if (assignmentManager == null)
                    return "Pawn assignment system is not available (load a game first).";
                var initiatorPawn = assignmentManager.GetAssignedPawn(messageWrapper);
                if (initiatorPawn == null) return "You don't have an active pawn. Use !pawn to purchase one!";

                // Find target pawn - now passes interaction type so !nuzzle/!animalchat can target named colony animals
                Pawn targetPawn = FindInteractionTarget(initiatorPawn, interaction, args);
                if (targetPawn == null) return "No valid target found for interaction.";

                if (!CanPawnsInteract(initiatorPawn, targetPawn))
                    return $"{initiatorPawn.Name} cannot interact with {targetPawn.Name} right now.";

                // Special handling for romance/flirt and marriage proposal interactions
                if (interaction == InteractionDefOf.RomanceAttempt ||
                    interaction == InteractionDefOf.MarriageProposal ||
                    (interaction.defName?.ToLower().Contains("flirt") == true))
                {
                    bool canProceed;
                    string refusalMessage;

                    if (interaction == InteractionDefOf.MarriageProposal)
                    {
                        canProceed = CanProposeMarriage(initiatorPawn, targetPawn, out refusalMessage);
                    }
                    else
                    {
                        canProceed = CanFlirt(initiatorPawn, targetPawn, out refusalMessage);
                    }

                    if (!canProceed)
                    {
                        // Refund cost since interaction won't happen
                        viewer.GiveCoins(interactionInfo.Cost);
                        Viewers.SaveViewers();

                        return refusalMessage;
                    }
                }

                // Create and assign the social visit job
                var socialVisitDef = JobDefOf_CAP.CAP_SocialVisit;
                if (socialVisitDef == null)
                    return "The social visit job definition is missing.";

                Job socialJob = JobMaker.MakeJob(socialVisitDef, targetPawn);
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

        private static Pawn FindInteractionTarget(Pawn initiator, InteractionDef interaction, string[] args)
        {
            // Animal-specific commands now search colony animals (named only)
            bool isAnimalInteraction = interaction != null &&
                (interaction == InteractionDefOf.Nuzzle ||
                 interaction.defName?.ToLowerInvariant() == "animalchat" ||
                 interaction.defName?.ToLowerInvariant().Contains("animalchat") == true);

            // If args provided, try to find specific target (unchanged)
            if (args.Length > 0)
            {
                string targetQuery = args[0];

                // Remove @ symbol if present
                if (targetQuery != null && targetQuery.StartsWith("@"))
                    targetQuery = targetQuery.Substring(1);

                if (string.IsNullOrWhiteSpace(targetQuery))
                    return null;

                if (isAnimalInteraction)
                {
                    var targetAnimal = FindAnimalByName(targetQuery);
                    if (targetAnimal != null && targetAnimal != initiator) return targetAnimal;
                }
                else
                {
                    var assignmentManager = CAPChatInteractiveMod.GetPawnAssignmentManager();
                    if (assignmentManager != null && !string.IsNullOrEmpty(targetQuery))
                    {
                        // Use GetAssignedPawn (not HasAssignedPawn) which safely handles cases where the name has no assignment (FindViewerIdentifier returns null)
                        var targetPawn = assignmentManager.GetAssignedPawn(targetQuery);
                        if (targetPawn != null && targetPawn != initiator) return targetPawn;
                    }

                    var namedPawn = FindPawnByName(targetQuery);
                    if (namedPawn != null && namedPawn != initiator) return namedPawn;
                }

                return null; // Specific target not found
            }

            // === PREFERENCE LOGIC ===

            // 1. Human social commands: prefer romantic partner (spouse > lover > fiancé)
            if (!isAnimalInteraction)
            {
                Pawn preferredPartner = GetPreferredRomanticPartner(initiator);
                if (preferredPartner != null && preferredPartner != initiator)
                {
                    if (CanPawnsInteract(initiator, preferredPartner))
                    {
                        Logger.Debug($"[Social] No target specified for {initiator.Name} - preferring romantic partner {preferredPartner.Name}");
                        return preferredPartner;
                    }
                }
            }
            // 2. Animal commands: prefer bonded animal if any
            else
            {
                Pawn bondedAnimal = GetPreferredBondedAnimal(initiator);
                if (bondedAnimal != null && bondedAnimal != initiator)
                {
                    if (CanPawnsInteract(initiator, bondedAnimal))
                    {
                        Logger.Debug($"[Social] No target specified for {initiator.Name} - preferring bonded animal {bondedAnimal.Name}");
                        return bondedAnimal;
                    }
                }
            }

            // Fallback: random animal for animal commands, random colonist otherwise
            return isAnimalInteraction
                ? FindRandomColonistAnimal(initiator)
                : FindRandomColonist(initiator);
        }

        private static Pawn GetPreferredRomanticPartner(Pawn initiator)
        {
            if (initiator == null || initiator.relations == null) return null;

            Pawn bestPartner = null;
            int bestPriority = -1;

            // Check direct relations for spouse / lover / fiancé)
            foreach (var directRelation in initiator.relations.DirectRelations)
            {
                if (directRelation.otherPawn == null ||
                    directRelation.otherPawn.Dead ||
                    !directRelation.otherPawn.Spawned)
                    continue;

                int priority = GetRelationPriority(directRelation.def);
                if (priority > bestPriority)
                {
                    bestPriority = priority;
                    bestPartner = directRelation.otherPawn;
                }
            }

            return bestPartner;
        }

        private static int GetRelationPriority(PawnRelationDef def)
        {
            if (def == null) return 0;

            // Higher number = stronger preference
            if (def == PawnRelationDefOf.Spouse) return 3;
            if (def == PawnRelationDefOf.Lover) return 2;
            if (def == PawnRelationDefOf.Fiance) return 1;

            return 0;
        }

        private static Pawn GetPreferredBondedAnimal(Pawn initiator)
        {
            if (initiator == null || initiator.relations == null)
                return null;

            Pawn bestBonded = null;

            // Follow vanilla bonded animal logic exactly (from ThoughtWorker_BondedAnimalMaster)
            foreach (var relation in initiator.relations.DirectRelations)
            {
                Pawn animal = relation.otherPawn;
                if (animal == null) continue;

                if (relation.def == PawnRelationDefOf.Bond &&
                    !animal.Dead &&
                    animal.Spawned &&
                    animal.Faction == Faction.OfPlayer &&
                    animal.RaceProps != null &&
                    animal.RaceProps.Animal &&
                    animal.training != null &&
                    animal.training.HasLearned(TrainableDefOf.Obedience))
                {
                    // Prefer the first valid bonded animal (usually the strongest bond)
                    // We can expand this later to sort by relation age or thought strength if desired
                    bestBonded = animal;
                    break; // Take the first one found (order in DirectRelations is usually fine)
                }
            }

            return bestBonded;
        }

        private static Pawn FindPawnByName(string name)
        {
            if (string.IsNullOrEmpty(name))
                return null;
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

            // Step 1: Basic checks
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

                // Hard refuse if target is in a very bad mood (near mental break)
                if (targetMoodPct < 0.30f)
                {
                    refusalMessage = $"{target.Name} is feeling too down for this.";

                    // Small mood hit to initiator for insensitive timing (50% chance)
                    var initiatorMood = initiator.needs.mood;
                    if (initiatorMood != null && Rand.Value < 0.5f)
                    {
                        initiatorMood.thoughts.memories.TryGainMemory(ThoughtDefOf.RebuffedMyRomanceAttempt);
                    }
                    return false;
                }
                // Soft refuse if target is stressed (70% chance)
                else if (targetMoodPct < 0.40f && Rand.Value < 0.7f)
                {
                    refusalMessage = $"{target.Name} isn't in the mood right now.";
                    return false;
                }
            }

            // Step 3: Opinion check (target's opinion of initiator)
            int opinion = target.relations.OpinionOf(initiator);

            if (opinion < -10) // Strong dislike → hard refuse
            {
                refusalMessage = $"{target.Name} wants nothing to do with {initiator.Name}.";
                return false;
            }

            if (opinion < 0 && Rand.Value < 0.8f) // Neutral-negative → likely refuse
            {
                refusalMessage = $"{target.Name} isn't interested.";
                return false;
            }

            if (opinion < 10 && Rand.Value < 0.3f) // Neutral → unlikely but possible refuse
            {
                refusalMessage = $"{target.Name} seems unsure about this.";
                return false;
            }

            // Step 4: Check for existing relationships
            // WHY: We only want to block "homewrecking" attempts on someone who is happily committed to ANOTHER person.
            // If the initiator IS the target's current love partner (spouse/lover/fiancé), we must allow the interaction.
            // Previously this block would incorrectly refuse ~60% of the time with "committed to {initiator.Name}",
            // which is why !flirt on lover/spouse was failing even though FindInteractionTarget correctly preferred them.
            if (LovePartnerRelationUtility.HasAnyLovePartner(target))
            {
                Pawn existingPartner = LovePartnerRelationUtility.ExistingMostLikedLovePartner(target, false);
                if (existingPartner != null && existingPartner != initiator) // <-- KEY FIX: skip if we are the partner
                {
                    int partnerOpinion = target.relations.OpinionOf(existingPartner);
                    if (partnerOpinion >= 15 && Rand.Value < 0.6f) // 60% chance to refuse if happy with someone else
                    {
                        refusalMessage = $"{target.Name} is committed to {existingPartner.Name}.";
                        return false;
                    }
                }
            }

            // Step 5: Trait-based checks (still apply even to current partners)
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

            return true; // All checks passed — safe to queue the CAP_SocialVisit job
        }

        private static bool CanProposeMarriage(Pawn initiator, Pawn target, out string refusalMessage)
        {
            refusalMessage = null;

            if (target == null || initiator == null)
            {
                refusalMessage = "No valid target found.";
                return false;
            }

            if (initiator == target)
            {
                refusalMessage = "You can't propose marriage to yourself!";
                return false;
            }

            // Step 1: Basic checks
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

            // Step 2: Mood check on target — marriage is a serious conversation, require decent mood
            var targetMood = target.needs?.mood;
            if (targetMood != null)
            {
                float targetMoodPct = targetMood.CurLevelPercentage;

                // Hard refuse if target is in a very bad mood (near mental break)
                if (targetMoodPct < 0.35f)
                {
                    refusalMessage = $"{target.Name} is feeling too down to consider such a serious conversation.";

                    // Small mood hit to initiator for insensitive timing (40% chance)
                    var initiatorMood = initiator.needs?.mood;
                    if (initiatorMood != null && Rand.Value < 0.4f)
                    {
                        initiatorMood.thoughts?.memories?.TryGainMemory(ThoughtDefOf.RebuffedMyRomanceAttempt);
                    }
                    return false;
                }
                // Soft refuse if target is stressed (60% chance) — higher bar than casual flirt
                else if (targetMoodPct < 0.50f && Rand.Value < 0.6f)
                {
                    refusalMessage = $"{target.Name} isn't in the right headspace for this right now.";
                    return false;
                }
            }

            // Step 3: Opinion check (target's opinion of initiator) — marriage requires stronger foundation than flirting
            int opinion = target.relations?.OpinionOf(initiator) ?? 0;

            if (opinion < 5)
            {
                refusalMessage = $"{target.Name} doesn't think highly enough of {initiator.Name} to even consider marriage.";
                return false;
            }

            if (opinion < 25 && Rand.Value < 0.55f) // Mediocre opinion → likely refuse
            {
                refusalMessage = $"{target.Name} isn't ready to take that step with {initiator.Name} yet.";
                return false;
            }

            // Step 4: Check for existing relationships (stricter than flirt)
            // Block homewrecking attempts on happy couples. Allow if targeting your own current partner (lover/fiancé/spouse).
            if (LovePartnerRelationUtility.HasAnyLovePartner(target))
            {
                Pawn existingPartner = LovePartnerRelationUtility.ExistingMostLikedLovePartner(target, false);
                if (existingPartner != null && existingPartner != initiator)
                {
                    int partnerOpinion = target.relations?.OpinionOf(existingPartner) ?? 0;
                    if (partnerOpinion >= 10 && Rand.Value < 0.75f) // High chance to refuse if reasonably happy with someone else
                    {
                        refusalMessage = $"{target.Name} is already committed to {existingPartner.Name}.";
                        return false;
                    }
                    // If low opinion with current partner, we still allow the attempt (risky "homewrecker" play) — vanilla outcome will decide
                }
                else if (existingPartner == initiator)
                {
                    // Already in a romantic relationship with initiator — check if already married to each other
                    bool alreadyMarried = false;
                    if (initiator.relations != null)
                    {
                        foreach (var rel in initiator.relations.DirectRelations)
                        {
                            if (rel.def == PawnRelationDefOf.Spouse && rel.otherPawn == target)
                            {
                                alreadyMarried = true;
                                break;
                            }
                        }
                    }
                    if (alreadyMarried)
                    {
                        refusalMessage = $"You are already married to {target.Name}!";
                        return false;
                    }
                    // Lovers or fiancés proposing marriage is perfectly valid — proceed
                }
            }

            // Step 5: Trait-based checks (apply even if current partners)
            if (target.story != null && target.story.traits != null)
            {
                // Psychopaths are much less receptive to marriage proposals
                if (target.story.traits.HasTrait(TraitDefOf.Psychopath) && Rand.Value < 0.65f)
                {
                    refusalMessage = $"{target.Name} has no interest in such emotional commitments.";
                    return false;
                }

                // Abrasive pawns more likely to be rude about it
                if (target.story.traits.HasTrait(TraitDefOf.Abrasive) && Rand.Value < 0.35f)
                {
                    refusalMessage = $"{target.Name} snaps: \"Marriage? With you? Don't make me laugh!\"";
                    return false;
                }
            }

            // Note: We intentionally do NOT hard-block proposals to non-partners.
            // High-opinion strangers or friends can still attempt (vanilla success chance may be low but possible).
            // This encourages using !flirt / !buildrapport first for better odds, while still allowing bold plays.
            // The pre-checks above prevent obvious disasters (bad mood, hated, already happily married to someone else).

            return true; // Checks passed — queue the CAP_SocialVisit job; vanilla MarriageProposal interaction will determine final outcome (and possible negative effects)
        }

        private class InteractionInfo
        {
            public bool IsNegative { get; set; } = false;
            public int Cost { get; set; } = 10;
            public int KarmaCost { get; set; } = 0;
        }
    }
}