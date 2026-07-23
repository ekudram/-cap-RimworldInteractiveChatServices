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
using CAP_ChatInteractive.Utilities;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
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
            // KarmaCost below is a base for mood-scaled social hits (see ComputeSocialKarmaHit).
            AddIfNotNull(InteractionDefOf.Chitchat, new InteractionInfo { IsNegative = false, Cost = 10, KarmaBaseHit = 0 });
            AddIfNotNull(InteractionDefOf.DeepTalk, new InteractionInfo { IsNegative = false, Cost = 15, KarmaBaseHit = 0 });
            AddIfNotNull(InteractionDefOf.Insult, new InteractionInfo { IsNegative = true, Cost = 5, KarmaBaseHit = 10, AlwaysSocialKarma = true });
            AddIfNotNull(InteractionDefOf.RomanceAttempt, new InteractionInfo { IsNegative = false, Cost = 20, KarmaBaseHit = 8, RomanceRisk = true });
            AddIfNotNull(InteractionDefOf.MarriageProposal, new InteractionInfo { IsNegative = false, Cost = 50, KarmaBaseHit = 12, RomanceRisk = true });
            AddIfNotNull(InteractionDefOf.BuildRapport, new InteractionInfo { IsNegative = false, Cost = 25, KarmaBaseHit = 0 });
            AddIfNotNull(InteractionDefOf.ConvertIdeoAttempt, new InteractionInfo { IsNegative = false, Cost = 30, KarmaBaseHit = 12, AlwaysSocialKarma = true });
            AddIfNotNull(InteractionDefOf.Reassure, new InteractionInfo { IsNegative = false, Cost = 12, KarmaBaseHit = 0 });
            AddIfNotNull(InteractionDefOf.Nuzzle, new InteractionInfo { IsNegative = false, Cost = 8, KarmaBaseHit = 0 });
            AddIfNotNull(InteractionDefOf.AnimalChat, new InteractionInfo { IsNegative = false, Cost = 10, KarmaBaseHit = 0 });
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

                bool isRomance = interaction == InteractionDefOf.RomanceAttempt ||
                                 interaction == InteractionDefOf.MarriageProposal ||
                                 (interaction.defName?.ToLower().Contains("flirt") == true);

                // Romance pre-checks (mood / opinion / monogamy vs multi-spouse ideo)
                if (isRomance)
                {
                    bool canProceed;
                    string refusalMessage;

                    if (interaction == InteractionDefOf.MarriageProposal)
                        canProceed = CanProposeMarriage(initiatorPawn, targetPawn, out refusalMessage);
                    else
                        canProceed = CanFlirt(initiatorPawn, targetPawn, out refusalMessage);

                    if (!canProceed)
                    {
                        // Harassment / bad-timing attempt: karma hit even when job does not run
                        float refuseHit = ComputeSocialKarmaHit(
                            interactionInfo.KarmaBaseHit > 0 ? interactionInfo.KarmaBaseHit : 8f,
                            targetPawn, initiatorPawn, riskBoost: 1.25f);

                        if (refuseHit > 0.5f)
                        {
                            if (!TryApplySocialKarmaHit(viewer, refuseHit, out string denyMsg))
                                return denyMsg; // would go below 0 — block entirely

                            Viewers.SaveViewers();
                            return $"{refusalMessage} (−{refuseHit:F0} karma)";
                        }

                        return refusalMessage;
                    }
                }

                // Social karma for insult / convert / unwelcome romance attempts that still proceed
                float socialHit = 0f;
                if (ShouldChargeSocialKarma(interactionInfo, initiatorPawn, targetPawn, isRomance, out float computedHit))
                {
                    socialHit = computedHit;
                    if (!CanAffordSocialKarmaHit(viewer, socialHit, out string cannotAfford))
                        return cannotAfford;
                }

                // Create and assign the social visit job
                var socialVisitDef = JobDefOf_CAP.CAP_SocialVisit;
                if (socialVisitDef == null)
                    return "The social visit job definition is missing.";

                Job socialJob = JobMaker.MakeJob(socialVisitDef, targetPawn);
                socialJob.interaction = interaction;

                initiatorPawn.jobs.StartJob(socialJob, JobCondition.InterruptForced);

                // Deduct cost only after job is queued
                viewer.TakeCoins(interactionInfo.Cost);

                string karmaNote = "";
                if (socialHit > 0.5f)
                {
                    ApplySocialKarmaHit(viewer, socialHit);
                    karmaNote = $" (−{socialHit:F0} karma)";
                }

                Viewers.SaveViewers();

                return $"{initiatorPawn.Name} is going to visit {targetPawn.Name} for a {interaction.label}...{karmaNote}";
            }
            catch (Exception ex)
            {
                Logger.Error($"Error in enhanced interaction command: {ex}");
                return "An error occurred while processing the interaction.";
            }
        }

        // === Social karma (mood-scaled; never below MinKarma) ===

        private static bool ShouldChargeSocialKarma(
            InteractionInfo info, Pawn initiator, Pawn target, bool isRomance, out float hit)
        {
            hit = 0f;
            var settings = CAPChatInteractiveMod.Instance?.Settings?.GlobalSettings;
            if (settings != null && !settings.SocialKarmaEnabled)
                return false;

            if (info.AlwaysSocialKarma)
            {
                hit = ComputeSocialKarmaHit(info.KarmaBaseHit, target, initiator, riskBoost: 1f);
                return hit > 0.5f;
            }

            if (isRomance && info.RomanceRisk)
            {
                // Partial hit when attempt is unwelcome but still allowed (low opinion / stressed)
                float mood = target?.needs?.mood?.CurLevelPercentage ?? 0.5f;
                int opinion = target?.relations != null && initiator != null
                    ? target.relations.OpinionOf(initiator) : 0;

                bool unwelcome = mood < 0.45f || opinion < 15;
                if (!unwelcome)
                    return false;

                float boost = mood < 0.40f ? 1.15f : 0.85f;
                if (opinion < 0) boost += 0.35f;
                hit = ComputeSocialKarmaHit(info.KarmaBaseHit, target, initiator, riskBoost: boost);
                return hit > 0.5f;
            }

            return false;
        }

        /// <summary>
        /// Lower target mood → higher hit. Dislike of initiator also scales up.
        /// </summary>
        private static float ComputeSocialKarmaHit(float baseHit, Pawn target, Pawn initiator, float riskBoost)
        {
            if (baseHit <= 0f) return 0f;

            var settings = CAPChatInteractiveMod.Instance?.Settings?.GlobalSettings;
            float scale = settings?.SocialKarmaMoodScale ?? 1f;
            if (scale <= 0f) return 0f;

            float moodPct = target?.needs?.mood?.CurLevelPercentage ?? 0.5f;
            moodPct = Mathf.Clamp01(moodPct);
            // mood 0 → factor 2.0, mood 1 → factor 0.5
            float moodFactor = Mathf.Lerp(2.0f, 0.5f, moodPct);

            int opinion = 0;
            if (target?.relations != null && initiator != null)
                opinion = target.relations.OpinionOf(initiator);

            float opinionFactor = 1f;
            if (opinion < 0)
            {
                float t = Mathf.Clamp01((-opinion) / 50f);
                opinionFactor = Mathf.Lerp(1f, 1.5f, t);
            }

            float hit = baseHit * moodFactor * opinionFactor * riskBoost * scale;
            return Mathf.Clamp(hit, 3f, 40f);
        }

        private static bool CanAffordSocialKarmaHit(Viewer viewer, float hit, out string message)
        {
            message = null;
            if (viewer == null || hit <= 0f) return true;

            float minK = CAPChatInteractiveMod.Instance?.Settings?.GlobalSettings?.MinKarma ?? 0f;
            if (viewer.Karma - hit < minK - 0.01f)
            {
                message =
                    $"That social action would cost {hit:F0} karma (target mood makes it worse). " +
                    $"You have {viewer.Karma:F0}; it would go below {minK:F0}. Earn karma first (0 karma earns no coins).";
                return false;
            }
            return true;
        }

        private static bool TryApplySocialKarmaHit(Viewer viewer, float hit, out string denyMessage)
        {
            denyMessage = null;
            if (!CanAffordSocialKarmaHit(viewer, hit, out denyMessage))
                return false;
            ApplySocialKarmaHit(viewer, hit);
            return true;
        }

        private static void ApplySocialKarmaHit(Viewer viewer, float hit)
        {
            if (viewer == null || hit <= 0f) return;
            viewer.TakeKarma(hit);
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

            // Step 4: Existing relationships — monogamy friction only.
            // Multi-spouse ideos (SpouseCount MaxTwo+) may welcome additional partners; do not hard-block.
            if (LovePartnerRelationUtility.HasAnyLovePartner(target))
            {
                Pawn existingPartner = LovePartnerRelationUtility.ExistingMostLikedLovePartner(target, false);
                if (existingPartner != null && existingPartner != initiator)
                {
                    bool multiOk = IdeoSpouseUtility.CultureAllowsAdditionalPartners(initiator, target);
                    if (!multiOk)
                    {
                        int partnerOpinion = target.relations.OpinionOf(existingPartner);
                        // Monogamy: 60% refuse if happy with someone else
                        if (partnerOpinion >= 15 && Rand.Value < 0.6f)
                        {
                            refusalMessage = $"{target.Name} is committed to {existingPartner.Name}.";
                            return false;
                        }
                    }
                    else
                    {
                        // Multi-spouse culture: rare soft "busy" only (still allow most advances)
                        int partnerOpinion = target.relations.OpinionOf(existingPartner);
                        if (partnerOpinion >= 40 && Rand.Value < 0.12f)
                        {
                            refusalMessage = $"{target.Name} is busy with {existingPartner.Name} right now.";
                            return false;
                        }
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

            // Step 4: Existing relationships
            if (LovePartnerRelationUtility.HasAnyLovePartner(target))
            {
                Pawn existingPartner = LovePartnerRelationUtility.ExistingMostLikedLovePartner(target, false);
                if (existingPartner != null && existingPartner != initiator)
                {
                    bool multiOk = IdeoSpouseUtility.CultureAllowsAdditionalPartners(initiator, target);
                    if (!multiOk)
                    {
                        int partnerOpinion = target.relations?.OpinionOf(existingPartner) ?? 0;
                        // Monogamy: high chance to refuse if reasonably happy with someone else
                        if (partnerOpinion >= 10 && Rand.Value < 0.75f)
                        {
                            refusalMessage = $"{target.Name} is already committed to {existingPartner.Name}.";
                            return false;
                        }
                    }
                    // Multi-spouse: allow proposal attempts; vanilla/ideo still decides outcome
                }
                else if (existingPartner == initiator)
                {
                    // Already in a romantic relationship with initiator — check if already married
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
            /// <summary>Base karma for mood-scaled social hits (0 = none).</summary>
            public float KarmaBaseHit { get; set; } = 0f;
            /// <summary>Always charge social karma when used (insult, convert).</summary>
            public bool AlwaysSocialKarma { get; set; } = false;
            /// <summary>Romance: charge when attempt is unwelcome (low mood/opinion) or on refuse.</summary>
            public bool RomanceRisk { get; set; } = false;
        }
    }
}