// MyPawnCommandHandler.cs
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
// Handles the !mypawn command and its subcommands to provide detailed information about the viewer's assigned pawn.


using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using Verse;

namespace CAP_ChatInteractive.Commands.CommandHandlers
{
    public static class MyPawnCommandHandler
    {
        public static string HandleMyPawnCommand(ChatMessageWrapper messageWrapper, string subCommand, string[] args)
        {
            try
            {
                // Get the viewer and their pawn
                var viewer = Viewers.GetViewer(messageWrapper);
                if (viewer == null)
                {
                    return "Could not find your viewer data.";
                }

                var assignmentManager = CAPChatInteractiveMod.GetPawnAssignmentManager();

                // Check if viewer already has a pawn assigned using the new manager
                if (assignmentManager != null)
                {
                    Pawn existingPawn = assignmentManager.GetAssignedPawn(messageWrapper);
                    if (existingPawn == null)
                    {
                        return "You don't have an active pawn in the colony. Use !pawn to purchase one!";
                    }
                }

                var pawn = assignmentManager.GetAssignedPawn(messageWrapper);

                // Route to appropriate handler based on subcommand
                switch (subCommand)
                {
                    case "body":
                        return HandleBodyInfo(pawn, args);
                    case "health":
                        return HandlehealthInfo(pawn, args);
                    case "implants":
                        return HandleImplantsInfo(pawn, args);
                    case "gear":
                        return HandleGearInfo(pawn, args);
                    case "kills":
                    case "killcount":
                        return HandleKillInfo(pawn, args);
                    case "needs":
                        return HandleNeedsInfo(pawn, args);
                    case "relations":
                        return HandleRelationsInfo(pawn, viewer, args);
                    case "skills":
                        return HandleSkillsInfo(pawn, args);
                    case "stats":
                        return HandleStatsInfo(pawn, args);
                    case "story":
                        return HandleBackstoriesInfo(pawn, args);
                    case "traits":
                        return HandleTraitsInfo(pawn, args);
                    case "work":
                        return HandleWorkInfo(pawn, args);
                    default:
                        return $"Unknown subcommand: {subCommand}. !mypawn [type]: body, health, implants, gear, kills, needs, relations, skills, stats, story, traits, work";
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error in MyPawn command handler: {ex}");
                return "An error occurred while processing your pawn information.";
            }
        }

        // === inplants ===
        private static string HandleImplantsInfo(Pawn pawn, string[] args)
        {
            if (pawn.health?.hediffSet?.hediffs == null)
            {
                return $"{pawn.Name} has no health records.";
            }

            //Logger.Debug("=== HandleInplantsInfo START ===");
            //Logger.Debug($"Pawn: {pawn.Name}, Total hediffs: {pawn.health.hediffSet.hediffs.Count}");

            var report = new StringBuilder();
            report.AppendLine("🔧 Implants:");

            // Get all visible implants (added parts)
            var allHediffs = pawn.health.hediffSet.hediffs.ToList();
            //Logger.Debug("All hediffs:");
            //foreach (var h in allHediffs)
            //{
            //    Logger.Debug($"  - {h.def.defName}, Visible: {h.Visible}, Part: {h.Part?.def?.label}");
            //}

            var implants = allHediffs
                .Where(h =>
                {
                    bool isVisible = h.Visible;
                    bool isImplant = IsImplantOrAddedPart(h);
                    //Logger.Debug($"Filtering: {h.def.defName}, Visible={isVisible}, IsImplant={isImplant}");
                    return isVisible && isImplant;
                })
                .ToList();

            //Logger.Debug($"Found {implants.Count} implants after filtering");

            if (implants.Count == 0)
            {
                report.AppendLine("No implants or bionic replacements found.");
                //Logger.Debug("=== HandleInplantsInfo END (no implants) ===");
                return report.ToString();
            }

            // Group implants by body part
            var groupedImplants = implants
                .GroupBy(h => h.Part)
                .OrderByDescending(g => g.Key?.height ?? 0f)
                .ThenByDescending(g => g.Key?.coverageAbsWithChildren ?? 0f)
                .ToList();

            foreach (var partGroup in groupedImplants)
            {
                //string partName = partGroup.Key?.LabelCap ?? "Whole Body";
                //eport.AppendLine($"• {partName}:");

                foreach (var implant in partGroup.OrderBy(h => h.def.label))
                {
                    string implantName = StripTags(implant.LabelCap);
                    // string implantQuality = GetImplantQuality(implant);

                    // report.AppendLine($"  ◦ {implantName}{implantQuality}");
                    report.AppendLine($" ◦ {implantName}");
                    //Logger.Debug($"  - Found implant: {implantName} on {partName}");
                }
            }

            // Add summary
            report.AppendLine($"📊 {implants.Count} implant(s)");

            // Logger.Debug("=== HandleInplantsInfo END ===");
            return report.ToString();
        }

        // Remove or comment out most of the debug logs, keeping only important ones
        private static bool IsImplantOrAddedPart(Hediff hediff)
        {
            if (hediff.def == null) return false;

            // Check if it's an added part or implant class
            if (hediff.def.hediffClass != null)
            {
                // Check if it's an added part type
                if (typeof(Hediff_AddedPart).IsAssignableFrom(hediff.def.hediffClass))
                    return true;

                // Check if it's an implant type
                if (typeof(Hediff_Implant).IsAssignableFrom(hediff.def.hediffClass))
                    return true;
            }

            // Check if it spawns an implant/prosthetic when removed
            if (hediff.def.spawnThingOnRemoved != null)
            {
                var thingDef = hediff.def.spawnThingOnRemoved;

                // Check if it's any type of body part (excluding natural)
                if (thingDef.IsWithinCategory(ThingCategoryDefOf.BodyParts))
                {
                    // Check specifically for prosthetic/bionic/ultratech/archotech/mechtech
                    // Exclude natural body parts
                    if (!thingDef.IsWithinCategory(ThingCategoryDef.Named("BodyPartsNatural")))
                    {
                        return true;
                    }
                }
            }

            // Check def name for common implant patterns
            string defName = hediff.def.defName.ToLower();
            if (defName.Contains("bionic") ||
                defName.Contains("archotech") ||
                defName.Contains("prosthetic") ||
                defName.Contains("ultratech") ||
                defName.Contains("mechtech") ||
                defName.Contains("implant"))
            {
                return true;
            }

            return false;
        }

        private static string GetImplantQuality(Hediff implant)
        {
            if (implant.def.spawnThingOnRemoved != null)
            {
                var thingDef = implant.def.spawnThingOnRemoved;

                // Check which type of body part it is
                if (thingDef.IsWithinCategory(ThingCategoryDefOf.BodyParts))
                {
                    // Check for archotech
                    if (thingDef.IsWithinCategory(ThingCategoryDef.Named("BodyPartsArchotech")))
                        return " (Archotech)";

                    // Check for ultratech
                    if (thingDef.IsWithinCategory(ThingCategoryDef.Named("BodyPartsUltra")))
                        return " (Ultratech)";

                    // Check for bionic
                    if (thingDef.IsWithinCategory(ThingCategoryDef.Named("BodyPartsBionic")))
                        return " (Bionic)";

                    // Check for mechtech
                    if (thingDef.IsWithinCategory(ThingCategoryDef.Named("BodyPartsMechtech")))
                        return " (Mechtech)";

                    // Check for prosthetic/simple
                    if (thingDef.IsWithinCategory(ThingCategoryDef.Named("BodyPartsProsthetic")) ||
                        thingDef.IsWithinCategory(ThingCategoryDef.Named("BodyPartsSimple")))
                        return " (Prosthetic)";
                }
            }

            // Check def name for common patterns
            string defName = implant.def.defName.ToLower();
            if (defName.Contains("archotech"))
                return " (Archotech)";
            if (defName.Contains("ultratech") || defName.Contains("ultra"))
                return " (Ultratech)";
            if (defName.Contains("mechtech"))
                return " (Mechtech)";
            if (defName.Contains("bionic"))
                return " (Bionic)";
            if (defName.Contains("prosthetic"))
                return " (Prosthetic)";

            return " (Implant)"; // Default fallback
        }

        // === health ===
        private static string HandlehealthInfo(Pawn pawn, string[] args)
        {
            var report = new StringBuilder();
            report.AppendLine("❤️ Health:");

            try
            {
                // Core health capacities - only the most important ones
                var capacities = new[]
                {
            PawnCapacityDefOf.Consciousness,
            PawnCapacityDefOf.Sight,
            PawnCapacityDefOf.Hearing,
            PawnCapacityDefOf.Moving,
            PawnCapacityDefOf.Manipulation,
            PawnCapacityDefOf.Talking,
            PawnCapacityDefOf.Breathing,
            PawnCapacityDefOf.BloodFiltration,
            PawnCapacityDefOf.BloodPumping,
        };

                foreach (var capacity in capacities)
                {
                    if (capacity == null) continue;

                    var capacityValue = pawn.health.capacities.GetLevel(capacity);
                    // string status = GetCapacityStatus(capacityValue);
                    // string emoji = GetCapacityEmoji(capacityValue);
                    report.AppendLine($"• {capacity.LabelCap}:  ({capacityValue.ToStringPercent()})");
                    //report.AppendLine($"• {capacity.LabelCap}: {emoji} {status} ({capacityValue.ToStringPercent()})");
                    // alt
                    // report.AppendLine($"• {capacity.LabelCap}: {emoji} ({capacityValue.ToStringPercent()})");
                }

                // Add pain
                float pain = pawn.health.hediffSet.PainTotal;
                string painStatus = GetPainStatus(pain);
                string painEmoji = GetPainEmoji(pain);
                var painDef = StatDef.Named("PainShockThreshold");
                float maxPain = 0f;
                if (painDef != null)
                    maxPain = pawn.GetStatValue(painDef);

                report.AppendLine($"• Pain: {painEmoji} {painStatus} ({pain.ToStringPercent()}/{maxPain.ToStringPercent()})");

                // Add health
                string hp = pawn.health.summaryHealth.SummaryHealthPercent.ToStringPercent();
                report.AppendLine($"• Health: {hp}");
            }
            catch (Exception ex)
            {
                Logger.Error($"Error in health info: {ex}");
                return "Error retrieving health information.";
            }

            return report.ToString();
        }

        private static string GetCapacityStatus(float level)
        {
            return level switch
            {
                >= 0.95f => "Perfect",
                >= 0.85f => "Excellent",
                >= 0.70f => "Good",
                >= 0.50f => "Impaired",
                >= 0.30f => "Poor",
                >= 0.10f => "Very Poor",
                > 0f => "Critical",
                _ => "None"
            };
        }

        private static string GetCapacityEmoji(float level)
        {
            return level switch
            {
                >= 0.85f => "🟢",
                >= 0.60f => "🟡",
                >= 0.30f => "🟠",
                > 0f => "🔴",
                _ => "⚫"
            };
        }

        private static string GetPainStatus(float painLevel)
        {
            return painLevel switch
            {
                >= 0.80f => "Extreme",
                >= 0.60f => "Severe",
                >= 0.40f => "Moderate",
                >= 0.20f => "Minor",
                >= 0.05f => "Negligible",
                _ => "None"
            };
        }

        private static string GetPainEmoji(float painLevel)
        {
            return painLevel switch
            {
                >= 0.60f => "😫",
                >= 0.40f => "😣",
                >= 0.20f => "😐",
                >= 0.05f => "🙂",
                _ => "😊"
            };
        }

        private static bool IsChildOf(BodyPartRecord part, BodyPartRecord potentialParent)
        {
            if (part == null || potentialParent == null) return false;
            if (part == potentialParent) return false;

            BodyPartRecord childNode = part;
            BodyPartRecord parentNode = part.parent;

            while (parentNode != null)
            {
                if (parentNode == potentialParent)
                {
                    // === EXCEPTION FOR TORSO ===
                    if (potentialParent.def.defName.Equals("Torso", StringComparison.OrdinalIgnoreCase))
                    {
                        if (childNode.depth != BodyPartDepth.Inside)
                        {
                            return false;
                        }
                    }
                    // ===========================

                    return true;
                }

                childNode = parentNode;
                parentNode = parentNode.parent;
            }

            return false;
        }

        // === Body ===
        private static string HandleBodyInfo(Pawn pawn, string[] args)
        {
            // Add body type at the beginning
            string bodyTypeInfo = "";
            if (pawn.story?.bodyType != null)
            {
                string displayName = GetBodyTypeDisplayName(pawn);
                bodyTypeInfo = $"🧬 Body Type: {displayName} |";
            }

            if (pawn.story?.bodyType != null)
            {
                bodyTypeInfo = $"🧬 Body Type: {pawn.story.bodyType.defName} |";
            }

            if (pawn.health?.hediffSet?.hediffs == null || pawn.health.hediffSet.hediffs.Count == 0)
            {
                return $"{bodyTypeInfo}{pawn.Name} has no health conditions. 🟢";
            }

            var report = new StringBuilder();

            // Add body type at the beginning of the report
            if (!string.IsNullOrEmpty(bodyTypeInfo))
            {
                report.Append(bodyTypeInfo);
            }

            // Get all visible health conditions
            var healthConditions = GetVisibleHealthConditions(pawn);

            // Check if user specified a body part filter
            string bodyPartFilter = args.Length > 0 ? string.Join(" ", args).ToLower() : null;
            BodyPartRecord targetPart = null;

            if (!string.IsNullOrEmpty(bodyPartFilter))
            {
                // Find the body part
                targetPart = pawn.RaceProps.body.AllParts
                    .FirstOrDefault(p => p.def?.label?.ToLower().Contains(bodyPartFilter) == true ||
                                        p.def?.defName?.ToLower().Contains(bodyPartFilter) == true);

                if (targetPart == null)
                {
                    return $"❌ Body part '{bodyPartFilter}' not found. Try: torso, head, arm, leg, etc.";
                }

                // Filter to only show this part and its children
                healthConditions = healthConditions
                    .Where(g => g.Key == targetPart || IsChildOf(g.Key, targetPart))
                    .ToList();
            }

            // Count UNIQUE condition types
            int uniqueConditionCount = 0;
            int totalHediffCount = 0;
            var conditionGroups = new Dictionary<string, int>();

            foreach (var partGroup in healthConditions)
            {
                var hediffsList = partGroup.ToList();
                var groups = hediffsList.GroupBy(h => GetConditionKey(h)).ToList();

                foreach (var group in groups)
                {
                    string conditionName = GetConditionDisplayName(group.First());
                    if (!string.IsNullOrEmpty(conditionName))
                    {
                        string groupKey = $"{partGroup.Key?.def?.defName ?? "WholeBody"}_{GetConditionKey(group.First())}";
                        if (!conditionGroups.ContainsKey(groupKey))
                        {
                            conditionGroups[groupKey] = 0;
                            uniqueConditionCount++;
                        }
                        conditionGroups[groupKey] += group.Count();
                        totalHediffCount += group.Count();
                    }
                }
            }

            // Add summary at the front
            if (targetPart != null)
            {
                report.AppendLine($"🏥 Health Report - {targetPart.LabelCap} ({uniqueConditionCount} conditions):");
            }
            else
            {
                report.AppendLine($"🏥 Health Report ({uniqueConditionCount} conditions):");
            }

            // Add temperature comfort range (only in full report)
            if (targetPart == null)
            {
                float minComfy = pawn.GetStatValue(StatDefOf.ComfyTemperatureMin);
                float maxComfy = pawn.GetStatValue(StatDefOf.ComfyTemperatureMax);
                report.AppendLine($"🌡️ Comfort Range: {minComfy.ToStringTemperature()} ~ {maxComfy.ToStringTemperature()}");
            }

            if (healthConditions.Count == 0)
            {
                if (targetPart != null)
                {
                    report.AppendLine($"No visible issues on {targetPart.LabelCap}. ✅");
                }
                else
                {
                    report.AppendLine("No visible health issues. ✅");
                }
                return report.ToString();
            }

            // Group conditions by body part for display
            report.AppendLine("Health Conditions:");

            // Sort body parts by height (head to toe)
            var sortedPartGroups = healthConditions
                .OrderByDescending(g => g.Key?.height ?? 0f)
                .ThenByDescending(g => g.Key?.coverageAbsWithChildren ?? 0f)
                .ToList();

            int maxPartsToShow = targetPart != null ? 25 : 10; // Show more for specific part, than for full body
            int partsShown = 0;
            int maxHediffsToShow = targetPart != null ? 15 : 20;
            int hediffssShown = 0;

            foreach (var partGroup in sortedPartGroups)
            {
                if (partsShown >= maxPartsToShow)
                    break;

                BodyPartRecord part = partGroup.Key;
                string partName = part?.LabelCap ?? "Whole Body";
                string partEmoji = GetBodyPartEmoji(part);
                string healthStats = "";
                if (part != null)
                {
                    float partHealth = pawn.health.hediffSet.GetPartHealth(part);
                    float partMaxHealth = part.def.GetMaxHealth(pawn);
                    healthStats = $"({partHealth}/{partMaxHealth})";
                }

                var hediffsList = partGroup.ToList();

                // Group by condition type
                var conditionsByType = hediffsList
                    .GroupBy(h => GetConditionKey(h))
                    .OrderByDescending(g => IsCriticalCondition(g.First()))
                    .ThenByDescending(g => g.Sum(h => h.Severity))
                    .ToList();

                // Build conditions list for this body part
                var conditionLines = new List<string>();

                foreach (var group in conditionsByType)
                {
                    if (hediffssShown >= maxHediffsToShow)
                        break;
                    int count = group.Count();
                    Hediff sample = group.First();
                    string conditionName = GetConditionDisplayName(sample);

                    if (string.IsNullOrEmpty(conditionName)) continue;

                    string severityIndicator = GetSeverityIndicator(sample);
                    string display = count > 1 ?
                        $"{severityIndicator}{conditionName} (x{count})" :
                        $"{severityIndicator}{conditionName}";

                    conditionLines.Add(display);
                    hediffssShown++;
                }

                if (conditionLines.Count > 0)
                {
                    // Display body part with all its conditions
                    report.AppendLine($"{partEmoji} {partName}{healthStats}:");
                    foreach (var line in conditionLines)
                    {
                        report.AppendLine($"  • {line}");
                    }

                    partsShown++;
                }
            }

            // Add overflow message if we didn't show everything (only for full body report)
            if (targetPart == null)
            {
                int hiddenParts = Math.Max(0, healthConditions.Count - partsShown);
                if (hiddenParts > 0)
                {
                    report.AppendLine($"... and {hiddenParts} more body parts with conditions");
                }

                if (totalHediffCount > uniqueConditionCount)
                {
                    report.AppendLine($"({totalHediffCount} individual injuries across all body)");
                }
            }

            if (targetPart == null)
            {
                // Add health severity summary
                string severity = GetOverallHealthSeverity(pawn);
                report.AppendLine($"📊 Overall Status: {severity}");

                // Add immediate danger warnings
                // Check for bleeding
                float bleedRate = pawn.health.hediffSet.BleedRateTotal;
                if (bleedRate > 0f)
                {
                    int bleedoutTime = HealthUtility.TicksUntilDeathDueToBloodLoss(pawn);
                    if (bleedoutTime < GenDate.TicksPerDay)
                    {
                        report.AppendLine($"Bleedout in {bleedoutTime.ToStringTicksToPeriod()}!");
                    }
                }
                if (pawn.health.hediffSet.HasTendableHediff() || pawn.health.hediffSet.HasTendableNonInjuryNonMissingPartHediff())
                {
                    report.AppendLine("⚠️ Needs medical attention!");
                }
            }
            return report.ToString();
        }

        private static string GetBodyTypeDisplayName(Pawn pawn, bool includeModdedFlag = true)
        {
            if (pawn?.story?.bodyType == null)
                return "Unknown Body";

            string defName = pawn.story.bodyType.defName;

            // Vanilla body types
            Dictionary<string, string> vanillaMap = new Dictionary<string, string>
    {
        { "Female", "Female" },
        { "Male", "Male" },
        { "Thin", "Thin" },
        { "Hulk", "Hulk" },
        { "Fat", "Fat" },
        { "Standard", "Standard" }
    };

            // Check vanilla first
            if (vanillaMap.ContainsKey(defName))
            {
                return vanillaMap[defName];
            }

            // Common modded patterns with display names
            List<(string pattern, string display)> patterns = new List<(string, string)>
    {
        ("female", "Female"),
        ("male", "Male"),
        ("thin", "Thin"),
        ("slim", "Slim"),
        ("hulk", "Hulk"),
        ("muscular", "Muscular"),
        ("fat", "Fat"),
        ("chubby", "Chubby"),
        ("curvy", "Curvy"),
        ("athletic", "Athletic"),
        ("average", "Average"),
        ("normal", "Normal"),
        ("standard", "Standard"),
        
        // Creature types
        ("dragon", "Dragon"),
        ("lizard", "Lizard"),
        ("reptil", "Reptilian"),
        ("insect", "Insectoid"),
        ("bug", "Insectoid"),
        ("arachnid", "Arachnid"),
        ("spider", "Arachnid"),
        ("avian", "Avian"),
        ("bird", "Avian"),
        ("canine", "Canine"),
        ("wolf", "Canine"),
        ("dog", "Canine"),
        ("feline", "Feline"),
        ("cat", "Feline"),
        ("equine", "Equine"),
        ("horse", "Equine"),
        ("mechanoid", "Mechanoid"),
        ("android", "Android"),
        ("robot", "Robotic"),
        ("synth", "Synthetic"),
        ("demon", "Demonic"),
        ("angel", "Angelic"),
        ("alien", "Alien")
    };

            string lowerDefName = defName.ToLower();

            foreach (var (pattern, display) in patterns)
            {
                if (lowerDefName.Contains(pattern))
                {
                    string result = display;
                    if (includeModdedFlag && !vanillaMap.ContainsValue(display))
                    {
                        result += " (Modded)";
                    }
                    return result;
                }
            }

            // Try to extract a readable name from the defName
            // Remove common prefixes/suffixes
            string cleanedName = defName
                .Replace("BB_", "")
                .Replace("_Female", "")
                .Replace("_Male", "")
                .Replace("_BodyType", "")
                .Replace("BodyType_", "")
                .Replace("_", " ");

            // Capitalize first letter of each word
            if (!string.IsNullOrWhiteSpace(cleanedName))
            {
                cleanedName = System.Globalization.CultureInfo.CurrentCulture.TextInfo
                    .ToTitleCase(cleanedName.ToLower());

                if (includeModdedFlag)
                {
                    return $"{cleanedName} (Modded)";
                }
                return cleanedName;
            }

            // Last resort
            if (includeModdedFlag)
            {
                return "Unknown Modded Body";
            }
            return "Unknown";
        }

        // Updated to prioritize critical conditions
        private static bool IsCriticalCondition(Hediff hediff)
        {
            if (hediff == null) return false;

            // Missing body parts (amputations) - always critical
            if (hediff is Hediff_MissingPart && hediff.Bleeding)
            {
                return true;
            }

            // Bleeding injuries - always critical
            if (hediff.Bleeding && hediff.BleedRateScaled > 2.0f)
            {
                return true;
            }

            // High severity diseases

            if (hediff.def.isInfection && hediff.Severity > 0.5)
            {
                return true;
            }
            // High severity injuries
            //if (hediff.Severity > 0.6f)
            //{
            //    return true;
            //}

            // Infections and serious injuries
            //if (hediff.def.hediffClass == typeof(Hediff_Injury) && hediff.Severity > 0.4f)
            //{
            //    return true;
            //}

            // Check if it's life-threatening
            if (hediff.IsLethal || hediff.IsCurrentlyLifeThreatening)
            {
                return true;
            }

            // Check if it destroys body parts
            if (hediff is Hediff_Injury injury && injury.destroysBodyParts)
            {
                return true;
            }

            // Addictions (these are always important to show)
            if (hediff.def.hediffClass == typeof(Hediff_Addiction) ||
                hediff.def.hediffClass == typeof(Hediff_ChemicalDependency))
            {
                return true;
            }

            // Specific important conditions from your research
            string defName = hediff.def.defName.ToLower();

            // Life-threatening conditions
            if (defName.Contains("heartattack") ||
                defName.Contains("psychiccoma") ||
                defName.Contains("catatonicbreakdown") ||
                defName.Contains("psychicshock") ||
                defName.Contains("brainshock"))
            {
                return true;
            }

            // Serious illnesses/diseases
            if (defName.Contains("bloodloss") ||
                defName.Contains("malnutrition") ||
                defName.Contains("heatstroke") ||
                defName.Contains("hypothermia") ||
                defName.Contains("foodpoisoning") ||
                defName.Contains("drugoverdose") ||
                defName.Contains("cryptosleepsickness") ||
                defName.Contains("resurrectionsickness") ||
                defName.Contains("hypothermicslowdown"))
            {
                return true;
            }

            // Psylink (important for psychic pawns)
            if (hediff.def.hediffClass != null &&
                hediff.def.hediffClass.Name == "Hediff_Psylink")
            {
                return true;
            }

            // Alcohol/hangover if severe
            if ((hediff.def.hediffClass != null &&
                 (hediff.def.hediffClass.Name == "Hediff_Alcohol" ||
                  hediff.def.hediffClass.Name == "Hediff_Hangover")) &&
                hediff.Severity > 0.5f)
            {
                return true;
            }

            // Anesthetic if high severity (could indicate surgery/coma)
            if (defName.Contains("anesthetic") && hediff.Severity > 0.7f)
            {
                return true;
            }

            // Painful conditions that affect functionality
            //if (hediff.PainFactor > 1.5f || hediff.PainOffset > 0.3f)
            //{
            //    return true;
            //}

            // Check summary health impact (conditions that significantly affect health)
            if (hediff.SummaryHealthPercentImpact < -0.2f) // Reduces health by more than 20%
            {
                return true;
            }

            return false;
        }

        private static string GetSeverityIndicator(Hediff hediff)
        {
            if (hediff == null) return "";

            // Missing body parts
            if (hediff is Hediff_MissingPart)
            {
                return "🆘 "; // Emergency/SOS for missing parts
            }

            // Bleeding
            if (hediff.Bleeding)
            {
                return "🩸 ";
            }

            // Life-threatening conditions
            if (hediff.IsLethal || hediff.IsCurrentlyLifeThreatening)
            {
                return "💀 ";
            }

            // Addictions
            if (hediff.def.hediffClass == typeof(Hediff_Addiction) ||
                hediff.def.hediffClass == typeof(Hediff_ChemicalDependency))
            {
                return "💊 ";
            }

            // Heart attacks and similar critical conditions
            string defName = hediff.def.defName.ToLower();
            if (defName.Contains("heartattack") ||
                defName.Contains("psychiccoma") ||
                defName.Contains("catatonicbreakdown"))
            {
                return "🚑 ";
            }

            // Diseases and illnesses
            if (defName.Contains("bloodloss") ||
                defName.Contains("malnutrition") ||
                defName.Contains("heatstroke") ||
                defName.Contains("hypothermia") ||
                defName.Contains("foodpoisoning") ||
                defName.Contains("drugoverdose"))
            {
                return "🤢 ";
            }

            // Psylink/psychic conditions
            if (hediff.def.hediffClass != null &&
                hediff.def.hediffClass.Name == "Hediff_Psylink")
            {
                return "🌀 ";
            }

            bool isDisease = hediff.def.isInfection;

            // General severity indicators
            if (isDisease && hediff.Severity > 0.8f)
            {
                return "⚠️ "; // Warning
            }

            if (isDisease && hediff.Severity > 0.6f)
            {
                return "❗ "; // Exclamation
            }

            if (isDisease && hediff.Severity > 0.4f)
            {
                return "🔸 "; // Orange diamond
            }

            if (isDisease && hediff.Severity > 0.2f)
            {
                return "🔹 "; // Blue diamond
            }

            return "";
        }

        private static string GetConditionKey(Hediff hediff)
        {
            if (hediff == null) return string.Empty;

            // Base key on the hediff def name
            string key = hediff.def.defName;

            // Include sourceDef if available (like an animal or weapon)
            if (hediff.sourceDef != null)
            {
                key += "_" + hediff.sourceDef.defName;
            }
            // If no sourceDef but we have sourceLabel, use that
            else if (!string.IsNullOrEmpty(hediff.sourceLabel))
            {
                // Create a sanitized version of sourceLabel for the key
                string sanitizedLabel = System.Text.RegularExpressions.Regex.Replace(hediff.sourceLabel, @"\s+", "_").ToLower();
                key += "_" + sanitizedLabel;
            }

            return key;
        }

        private static string GetSimpleHediffDisplay(Hediff hediff)
        {
            if (hediff == null) return string.Empty;

            // Skip healthy implants in body report
            if (!hediff.def.isBad && IsImplantOrAddedPart(hediff))
            {
                return string.Empty;
            }

            string display = StripTags(hediff.LabelCap);

            // Remove the source from the display for cleaner grouping
            // This will make "Bruise (noctol claw)" show as just "Bruise"
            // Check if there's a source label in parentheses
            if (!string.IsNullOrEmpty(hediff.sourceLabel) && display.Contains("("))
            {
                int parenIndex = display.IndexOf('(');
                if (parenIndex > 0)
                {
                    display = display.Substring(0, parenIndex).Trim();
                }
            }

            return display;
        }

        private static string GetConditionDisplayName(Hediff hediff)
        {
            string simpleName = GetSimpleHediffDisplay(hediff);

            // For certain important conditions, add more context
            string defName = hediff.def.defName.ToLower();

            if (defName.Contains("heartattack"))
            {
                return "Heart Attack";
            }

            if (defName.Contains("psychiccoma"))
            {
                return "Psychic Coma";
            }

            if (defName.Contains("catatonicbreakdown"))
            {
                return "Catatonic Breakdown";
            }

            if (defName.Contains("bloodloss"))
            {
                return "Blood Loss";
            }

            if (defName.Contains("malnutrition"))
            {
                return "Malnutrition";
            }

            if (defName.Contains("heatstroke"))
            {
                return "Heatstroke";
            }

            if (defName.Contains("hypothermia"))
            {
                return "Hypothermia";
            }

            if (hediff.def.hediffClass == typeof(Hediff_Addiction))
            {
                return simpleName + " (Addiction)";
            }

            return simpleName;
        }

        private static List<IGrouping<BodyPartRecord, Hediff>> GetVisibleHealthConditions(Pawn pawn)
        {
            var finalHediffs = new List<Hediff>();

            // Get all visible hediffs first
            var allVisibleHediffs = pawn.health.hediffSet.hediffs
                .Where(h => h.Visible)
                .ToList();

            // Get all missing parts
            var missingPartsDict = new Dictionary<BodyPartRecord, Hediff_MissingPart>();
            foreach (var hediff in allVisibleHediffs.OfType<Hediff_MissingPart>())
            {
                if (hediff.Part != null)
                {
                    missingPartsDict[hediff.Part] = hediff;
                }
            }

            // Function to check if a part has a missing ancestor
            bool HasMissingAncestor(BodyPartRecord part)
            {
                if (part == null) return false;

                BodyPartRecord parent = part.parent;
                while (parent != null)
                {
                    if (missingPartsDict.ContainsKey(parent))
                        return true;
                    parent = parent.parent;
                }
                return false;
            }

            foreach (var hediff in allVisibleHediffs)
            {
                if (!hediff.Visible) continue;

                // Skip healthy implants
                if (IsImplantOrAddedPart(hediff) && !hediff.def.isBad)
                    continue;

                // Handle missing parts
                if (hediff is Hediff_MissingPart missingPart)
                {
                    // Skip if this part has a missing ancestor (parent, grandparent, etc.)
                    if (HasMissingAncestor(missingPart.Part))
                        continue;

                    // Check if replaced by implant
                    if (HasAnyImplantOnPartOrChildren(pawn, missingPart.Part))
                        continue;

                    finalHediffs.Add(hediff);
                }
                else
                {
                    // For other hediffs on parts with missing ancestors, only include if they're implants
                    if (HasMissingAncestor(hediff.Part))
                    {
                        if (!IsImplantOrAddedPart(hediff) || hediff.def.isBad)
                            continue;
                    }

                    finalHediffs.Add(hediff);
                }
            }

            return finalHediffs
                .GroupBy(h => h.Part)
                .OrderByDescending(g => g.Key?.height ?? 0f)
                .ThenByDescending(g => g.Key?.coverageAbsWithChildren ?? 0f)
                .ToList();
        }

        private static bool HasAnyImplantOnPartOrChildren(Pawn pawn, BodyPartRecord missingPart)
        {
            if (missingPart == null || pawn.health?.hediffSet?.hediffs == null)
                return false;

            Logger.Debug($"=== Checking if missing part {missingPart.def?.label} is replaced by implant ===");

            // Get all visible implants
            var allImplants = pawn.health.hediffSet.hediffs
                .Where(h => h.Visible && IsImplantOrAddedPart(h))
                .ToList();

            foreach (var implant in allImplants)
            {
                var implantPart = implant.Part;
                if (implantPart == null) continue;

                // Get all parts that this implant replaces/affects
                var affectedParts = GetPartsReplacedByImplant(implant, implantPart);

                if (affectedParts.Contains(missingPart))
                {
                    Logger.Debug($"Implant {implant.def.defName} replaces missing part {missingPart.def?.label}");
                    return true;
                }
            }

            return false;
        }

        private static HashSet<BodyPartRecord> GetPartsReplacedByImplant(Hediff implant, BodyPartRecord implantPart)
        {
            var parts = new HashSet<BodyPartRecord> { implantPart };

            // If implant is on a major body part (like leg), include all children
            AddAllChildren(implantPart, parts);

            return parts;
        }

        private static void AddAllChildren(BodyPartRecord part, HashSet<BodyPartRecord> set)
        {
            foreach (var child in part.GetDirectChildParts())
            {
                set.Add(child);
                AddAllChildren(child, set);
            }
        }

        private static string GetBodyPartEmoji(BodyPartRecord part)
        {
            if (part == null) return "❓";

            string partLabel = part.def?.label?.ToLower() ?? "";

            // Detailed emoji mapping for body parts
            return partLabel switch
            {
                string p when p.Contains("arm") || p.Contains("shoulder") || p.Contains("upper arm") => "💪", // Arm
                string p when p.Contains("hand") || p.Contains("palm") => "🖐️", // Hand
                string p when p.Contains("finger") || p.Contains("thumb") => "👉", // Finger
                string p when p.Contains("leg") || p.Contains("thigh") || p.Contains("hip") => "🦵", // Leg
                string p when p.Contains("foot") || p.Contains("ankle") || p.Contains("heel") => "🦶", // Foot
                string p when p.Contains("toe") => "🦶", // Toe (same as foot)
                string p when p.Contains("head") || p.Contains("skull") => "🧑", // Head
                string p when p.Contains("brain") || p.Contains("cerebrum") || p.Contains("cerebellum") => "🧠", // Brain
                string p when p.Contains("eye") || p.Contains("retina") || p.Contains("cornea") => "👁️", // Eye
                string p when p.Contains("ear") || p.Contains("eardrum") => "👂", // Ear
                string p when p.Contains("nose") || p.Contains("nostril") => "👃", // Nose
                string p when p.Contains("mouth") || p.Contains("lip") => "👄", // Mouth
                string p when p.Contains("jaw") || p.Contains("mandible") => "🦷", // Jaw (teeth emoji)
                string p when p.Contains("tooth") => "🦷", // Tooth
                string p when p.Contains("tongue") => "👅", // Tongue
                string p when p.Contains("heart") => "❤️", // Heart
                string p when p.Contains("lung") => "🫁", // Lungs
                string p when p.Contains("rib") || p.Contains("ribcage") => "🦴", // Ribs
                string p when p.Contains("spine") || p.Contains("vertebra") => "🦴", // Spine
                string p when p.Contains("pelvis") || p.Contains("hip bone") => "🦴", // Pelvis
                _ => "🔪" // Default knife for generic amputations/unknown parts
            };
        }

        private static string GetOverallHealthSeverity(Pawn pawn)
        {
            // Check for immediate life-threatening conditions first
            float bleedRate = pawn.health.hediffSet.BleedRateTotal;

            // If bleeding severely, override to Critical
            if (bleedRate > 2.0f) // 200% per hour - very dangerous
            {
                return "Critical 🔴 (Bleeding Out!)";
            }

            // Check for any missing body parts (amputations)
            var missingParts = pawn.health.hediffSet.GetMissingPartsCommonAncestors();
            if (missingParts.Count > 0)
            {
                // Check if any missing part is fresh (untended)
                bool hasFreshAmputation = missingParts.Any(mp => mp.IsFresh);
                if (hasFreshAmputation)
                {
                    return "Critical 🔴 (Amputated!)";
                }
            }

            // Check for untended bleeding wounds
            var bleedingWounds = pawn.health.hediffSet.hediffs
                .Where(h => h.Visible && h.Bleeding && !h.IsTended())
                .ToList();

            if (bleedingWounds.Count > 0 && bleedRate > 0.5f) // 50% per hour
            {
                return "Serious 🟠 (Untended Bleeding)";
            }

            // Check for life-threatening conditions
            if (pawn.health.hediffSet.HasTendedAndHealingInjury() ||
                pawn.health.hediffSet.HasImmunizableNotImmuneHediff())
            {
                return "Poor 🟠 (Needs Bed Rest)";
            }

            // Use RimWorld's summary health as baseline
            float healthPercent = pawn.health.summaryHealth.SummaryHealthPercent;

            // Adjust based on additional factors
            float adjustment = 0f;

            // Reduce rating for bleeding
            if (bleedRate > 0.1f)
            {
                adjustment -= 0.2f;
            }

            // Reduce rating for pain
            float pain = pawn.health.hediffSet.PainTotal;
            if (pain > 0.3f)
            {
                adjustment -= 0.1f;
            }
            if (pain > 0.6f)
            {
                adjustment -= 0.2f;
            }

            // Reduce rating for missing parts
            if (missingParts.Count > 0)
            {
                adjustment -= 0.15f * missingParts.Count;
            }

            // Apply adjustment (clamp between 0 and 1)
            float adjustedHealthPercent = Mathf.Clamp01(healthPercent + adjustment);

            // Return based on adjusted health
            if (adjustedHealthPercent >= 0.85f) return "Excellent 🟢";
            if (adjustedHealthPercent >= 0.65f) return "Good 🟢";
            if (adjustedHealthPercent >= 0.45f) return "Fair 🟡";
            if (adjustedHealthPercent >= 0.25f) return "Poor 🟠";
            return "Critical 🔴";
        }

        // === Gear ===
        private static string HandleGearInfo(Pawn pawn, string[] args)
        {
            var report = new StringBuilder();
            report.AppendLine($"🎒 Gear Report: ");// for {pawn.Name}:

            // Weapons - check for Simple Sidearms first
            var weapons = GetWeaponsList(pawn);
            if (weapons.Count > 0)
            {
                report.Append("⚔️ Weapons: ");
                report.AppendLine(string.Join(", ", weapons));
            }
            else
            {
                report.AppendLine("⚔️ Weapons: None");
            }

            // Apparel - list everything worn
            var apparel = pawn.apparel?.WornApparel;
            if (apparel != null && apparel.Count > 0)
            {
                report.AppendLine("👕 Apparel:");
                foreach (var item in apparel)
                {
                    // Get base name without quality
                    string baseName = StripTags(item.def.LabelCap);

                    // Add quality if it exists
                    string quality = item.TryGetQuality(out QualityCategory qc) ? $" ({qc})" : "";

                    // Add hit points if damaged
                    string hitPoints = item.HitPoints != item.MaxHitPoints ?
                        $" {((float)item.HitPoints / item.MaxHitPoints).ToStringPercent()}" : "";

                    report.AppendLine($"  • {baseName}{quality}{hitPoints}");
                }
            }
            else
            {
                report.AppendLine("👕 Apparel: None");
            }

            // Inventory items - show all notable items
            var inventory = pawn.inventory?.innerContainer;
            if (inventory != null && inventory.Count > 0)
            {
                var notableItems = inventory.Where(item =>
                     item.def.IsMedicine ||
                    item.def.IsDrug ||
                    item.def.IsIngestible 
                );

                if (notableItems.Any())
                {
                    report.AppendLine("🎒 Inventory:");
                    foreach (var item in notableItems)
                    {
                        string stackInfo = item.stackCount > 1 ? $" x{item.stackCount}" : "";
                        report.AppendLine($"  • {StripTags(item.LabelCap)}{stackInfo}");
                    }
                }
            }

            // Armor stats from RimWorld (no complex calculations)
            report.Append(GetArmorSummary(pawn));

            return report.ToString();
        }

        private static List<string> GetWeaponsList(Pawn pawn)
        {
            var weapons = new List<string>();

            // Check for Simple Sidearms mod
            if (ModLister.GetActiveModWithIdentifier("PeteTimesSix.SimpleSidearms") != null)
            {
                try
                {
                    // Use reflection or direct call if you have access to SimpleSidearms API
                    var sidearms = GetSidearmsViaReflection(pawn);
                    if (sidearms != null && sidearms.Count > 0)
                    {
                        weapons.AddRange(sidearms.Select(weapon => StripTags(weapon.LabelCap)));
                        return weapons;
                    }
                }
                catch (Exception ex)
                {
                    Logger.Warning($"Failed to get SimpleSidearms data: {ex.Message}");
                    // Fall through to default equipment
                }
            }

            // Fallback: standard equipment
            var equipment = pawn.equipment?.AllEquipmentListForReading;
            if (equipment != null && equipment.Count > 0)
            {
                weapons.AddRange(equipment.Select(e => StripTags(e.LabelCap)));
            }

            return weapons;
        }

        private static List<Thing> GetSidearmsViaReflection(Pawn pawn)
        {
            try
            {
                // Simple Sidearms integration via reflection
                var sidearmsComp = pawn.TryGetComp<CompEquippable>();
                if (sidearmsComp != null)
                {
                    // Alternative: Check for Simple Sidearms comp
                    var sidearmsField = pawn.GetType().GetField("sidearms", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                    if (sidearmsField != null)
                    {
                        var sidearmsList = sidearmsField.GetValue(pawn) as List<Thing>;
                        return sidearmsList ?? new List<Thing>();
                    }
                }

                // Try the static method approach that the old code used
                var simpleSidearmsType = Type.GetType("SimpleSidearms.SimpleSidearms, SimpleSidearms");
                if (simpleSidearmsType != null)
                {
                    var getSidearmsMethod = simpleSidearmsType.GetMethod("GetSidearms", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
                    if (getSidearmsMethod != null)
                    {
                        var result = getSidearmsMethod.Invoke(null, new object[] { pawn }) as IEnumerable<Thing>;
                        return result?.ToList() ?? new List<Thing>();
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Warning($"Reflection failed for SimpleSidearms: {ex.Message}");
            }

            return new List<Thing>();
        }

        private static string GetArmorSummary(Pawn pawn)
        {
            try
            {
                float sharpArmor = CalculateArmorRating(pawn, StatDefOf.ArmorRating_Sharp);
                float bluntArmor = CalculateArmorRating(pawn, StatDefOf.ArmorRating_Blunt);
                float heatArmor = CalculateArmorRating(pawn, StatDefOf.ArmorRating_Heat);

                Logger.Debug($"Calculated armor: Sharp={sharpArmor:P0}, Blunt={bluntArmor:P0}, Heat={heatArmor:P0}");

                var armorStats = new List<string>();

                if (sharpArmor >= 0.01f)
                    armorStats.Add($"🗡️{sharpArmor.ToStringPercent()}");

                if (bluntArmor >= 0.01f)
                    armorStats.Add($"🔨{bluntArmor.ToStringPercent()}");

                if (heatArmor >= 0.01f)
                    armorStats.Add($"🔥{heatArmor.ToStringPercent()}");

                if (armorStats.Count > 0)
                {
                    return $"🛡️ Armor: {string.Join(" ", armorStats)}\n";
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error calculating armor: {ex}");
            }

            return "🛡️ Armor: None\n";
        }

        private static float CalculateArmorRating(Pawn pawn, StatDef stat)
        {
            if (pawn.apparel?.WornApparel == null || !pawn.apparel.WornApparel.Any())
                return 0f;

            var rating = 0f;
            float baseValue = Mathf.Clamp01(pawn.GetStatValue(stat) / 2f);
            var parts = pawn.RaceProps.body.AllParts;
            var apparel = pawn.apparel.WornApparel;

            foreach (var part in parts)
            {
                float cache = 1f - baseValue;

                if (apparel != null && apparel.Any())
                {
                    cache = apparel.Where(a => a.def.apparel?.CoversBodyPart(part) ?? false)
                       .Select(a => Mathf.Clamp01(a.GetStatValue(stat) / 2f))
                       .Aggregate(cache, (current, v) => current * (1f - v));
                }

                rating += part.coverageAbs * (1f - cache);
            }

            return Mathf.Clamp(rating * 2f, 0f, 2f);
        }

        private static string StripTags(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            return System.Text.RegularExpressions.Regex.Replace(text, @"<[^>]*>", "");
        }

        private static string HandleKillInfo(Pawn pawn, string[] args)
        {
            var report = new StringBuilder();
            report.AppendLine($"💀 Kill Report:"); //  for {pawn.Name}:

            // Get kill counts from pawn records - cast float to int
            int humanlikeKills = (int)pawn.records.GetValue(RecordDefOf.KillsHumanlikes);
            int animalKills = (int)pawn.records.GetValue(RecordDefOf.KillsAnimals);
            int mechanoidKills = (int)pawn.records.GetValue(RecordDefOf.KillsMechanoids);
            int totalKills = humanlikeKills + animalKills + mechanoidKills;

            // Total kills
            report.AppendLine($"Total Kills: {totalKills}");

            // Breakdown by type
            if (humanlikeKills > 0)
                report.AppendLine($"• Humans: {humanlikeKills}");
            if (animalKills > 0)
                report.AppendLine($"• Animals: {animalKills}");
            if (mechanoidKills > 0)
                report.AppendLine($"• Mechanoids: {mechanoidKills}");

            // Check for other kill records that might exist
            var killRecords = DefDatabase<RecordDef>.AllDefs
                .Where(r => r.defName.Contains("Kill") || r.defName.Contains("kill"))
                .Where(r => pawn.records.GetValue(r) > 0)
                .ToList();

            // Add any additional kill types found - cast float to int
            foreach (var record in killRecords)
            {
                if (record != RecordDefOf.KillsHumanlikes &&
                    record != RecordDefOf.KillsAnimals &&
                    record != RecordDefOf.KillsMechanoids)
                {
                    int count = (int)pawn.records.GetValue(record);
                    string recordName = record.LabelCap.ToLower().Replace("kills", "").Trim();
                    report.AppendLine($"• {recordName}: {count}");
                }
            }

            // Check for most recent kill if any kills exist
            if (totalKills > 0)
            {
                // Get damage dealt records - cast float to int
                int damageDealt = (int)pawn.records.GetValue(RecordDefOf.DamageDealt);
                if (damageDealt > 0)
                {
                    report.AppendLine($"Damage Dealt: {damageDealt}");
                }

                // Add some flavor text based on kill count
                if (totalKills >= 100)
                    report.AppendLine("🏆 Legendary Slayer!");
                else if (totalKills >= 50)
                    report.AppendLine("⚔️ Veteran Warrior");
                else if (totalKills >= 10)
                    report.AppendLine("🔪 Experienced Fighter");
                else if (totalKills > 0)
                    report.AppendLine("🎯 Getting Started");
            }
            else
            {
                report.AppendLine("No kills recorded yet. Go get 'em!");
            }

            return report.ToString();
        }

        private static string HandleNeedsInfo(Pawn pawn, string[] args)
        {
            var report = new StringBuilder();
            report.AppendLine($"😊 Needs Report: "); //for {pawn.Name}:

            var needs = pawn.needs?.AllNeeds;
            if (needs == null || needs.Count == 0)
            {
                return $"{pawn.Name} has no needs tracked.";
            }

            foreach (var need in needs.Where(n => n != null && n.def != null))
            {
                if (!need.ShowOnNeedList) continue;

                string needName = StripTags(need.def.LabelCap);
                string needStatus = GetNeedStatus(need);

                report.AppendLine($"• {needName}: {needStatus}");
            }

            // Add mood summary if available
            var moodNeed = pawn.needs?.mood;
            if (moodNeed != null)
            {
                string moodStatus = GetMoodStatus(moodNeed.CurLevel);
                report.AppendLine($"📊 Overall Mood: {moodStatus}");
            }

            return report.ToString();
        }

        private static string GetNeedStatus(Need need)
        {
            if (need == null) return "Unknown";

            float curLevel = need.CurLevel;
            float maxLevel = need.MaxLevel;

            // Get percentage for display
            float percent = curLevel / maxLevel;

            // Determine status and emoji based on need type and level
            string status = GetNeedLevelStatus(need.def.defName, percent, curLevel);
            string emoji = GetNeedEmoji(need.def.defName, percent);

            return $"{emoji} {status} ({curLevel.ToString("F1")}/{maxLevel.ToString("F1")})";
        }

        private static string GetNeedLevelStatus(string needDefName, float percent, float curLevel)
        {
            return percent switch
            {
                >= 0.9f => "Excellent",
                >= 0.7f => "Good",
                >= 0.5f => "Okay",
                >= 0.3f => "Low",
                >= 0.1f => "Very Low",
                _ => "Critical"
            };
        }

        private static string GetNeedEmoji(string needDefName, float percent)
        {
            // Default emoji based on level
            string levelEmoji = percent switch
            {
                >= 0.7f => "🟢",
                >= 0.4f => "🟡",
                >= 0.2f => "🟠",
                _ => "🔴"
            };

            // Specific emojis for common needs
            return needDefName.ToLower() switch
            {
                "food" or "hunger" => percent >= 0.3f ? "🍽️" : "🍴",
                "rest" => percent >= 0.3f ? "😴" : "💤",
                "joy" => percent >= 0.3f ? "😄" : "😞",
                "mood" => percent >= 0.7f ? "😊" : percent >= 0.4f ? "😐" : "😠",
                "beauty" => percent >= 0.5f ? "🎨" : "🏚️",
                "comfort" => percent >= 0.5f ? "🛋️" : "🪑",
                "outdoors" => percent >= 0.5f ? "🌳" : "🏠",
                "room" => percent >= 0.5f ? "🏠" : "⛺",
                _ => levelEmoji
            };
        }

        private static string GetMoodStatus(float moodLevel)
        {
            return moodLevel switch
            {
                >= 0.9f => "Ecstatic 😁",
                >= 0.8f => "Very Happy 😊",
                >= 0.6f => "Content 🙂",
                >= 0.4f => "Neutral 😐",
                >= 0.2f => "Stressed 😟",
                >= 0.1f => "Upset 😠",
                _ => "Breaking 😭"
            };
        }

        private static string HandleRelationsInfo(Pawn pawn, Viewer viewer, string[] args)
        {
            var report = new StringBuilder();
            report.AppendLine($"💕 Relations Report:"); // for {pawn.Name}:

            // Get the assignment manager
            var assignmentManager = Current.Game?.GetComponent<GameComponent_PawnAssignmentManager>();
            if (assignmentManager == null)
            {
                return "Relations system not available.";
            }

            // Handle specific viewer relation request
            if (args.Length > 0)
            {
                string targetViewer = args[0];
                var targetPawn = assignmentManager.GetAssignedPawn(targetViewer);
                if (targetPawn == null)
                {
                    return $"Viewer '{targetViewer}' doesn't have an active pawn.";
                }

                return GetSpecificRelationInfo(pawn, targetPawn, targetViewer, assignmentManager);
            }

            // General relations overview
            return GetRelationsOverview(pawn, assignmentManager);
        }

        private static string GetSpecificRelationInfo(Pawn pawn, Pawn targetPawn, string targetViewer, GameComponent_PawnAssignmentManager assignmentManager)
        {
            var report = new StringBuilder();
            string pawnViewer = GetViewerNameFromPawn(pawn);
            string targetPawnViewer = GetViewerNameFromPawn(targetPawn);

            report.AppendLine($"🤝 Relations between {pawnViewer} and {targetPawnViewer}:");

            // Get direct relation
            var directRelation = pawn.relations.DirectRelationExists(PawnRelationDefOf.Spouse, targetPawn) ? "Spouse 💍" :
                                pawn.relations.DirectRelationExists(PawnRelationDefOf.Lover, targetPawn) ? "Lover ❤️" :
                                pawn.relations.DirectRelationExists(PawnRelationDefOf.Fiance, targetPawn) ? "Fiancé 💑" :
                                pawn.relations.DirectRelationExists(PawnRelationDefOf.ExLover, targetPawn) ? "Ex-Lover 💔" :
                                pawn.relations.DirectRelationExists(PawnRelationDefOf.ExSpouse, targetPawn) ? "Ex-Spouse 💔" :
                                pawn.relations.DirectRelationExists(PawnRelationDefOf.Child, targetPawn) ? "Child 👶" :
                                pawn.relations.DirectRelationExists(PawnRelationDefOf.Parent, targetPawn) ? "Parent 👨‍👦" :
                                pawn.relations.DirectRelationExists(PawnRelationDefOf.Sibling, targetPawn) ? "Sibling 👫" :
                                "No direct relation";

            report.AppendLine($"• Relationship: {directRelation}");

            // Opinion
            int opinion = pawn.relations.OpinionOf(targetPawn);
            string opinionEmoji = opinion >= 50 ? "😍" : opinion >= 25 ? "😊" : opinion >= 0 ? "🙂" : opinion >= -25 ? "😐" : opinion >= -50 ? "😠" : "😡";
            report.AppendLine($"• Opinion: {opinion} {opinionEmoji}");

            // Romance-related info - basic
            if (pawn.relations.DirectRelationExists(PawnRelationDefOf.Lover, targetPawn) ||
                pawn.relations.DirectRelationExists(PawnRelationDefOf.Spouse, targetPawn))
            {
                report.AppendLine($"• Relationship: Active 💑");
            }

            return report.ToString();
        }

        private static string GetRelationsOverview(Pawn pawn, GameComponent_PawnAssignmentManager assignmentManager)
        {
            var report = new StringBuilder();

            // Family relations (always show all)
            var family = pawn.relations.RelatedPawns
                .Where(p => p.relations.DirectRelationExists(PawnRelationDefOf.Spouse, pawn) ||
                           p.relations.DirectRelationExists(PawnRelationDefOf.Lover, pawn) ||
                           p.relations.DirectRelationExists(PawnRelationDefOf.Child, pawn) ||
                           p.relations.DirectRelationExists(PawnRelationDefOf.Parent, pawn) ||
                           p.relations.DirectRelationExists(PawnRelationDefOf.Sibling, pawn))
                .ToList();

            if (family.Count > 0)
            {
                report.AppendLine("👨‍👩‍👧‍👦 Family:");
                foreach (var relative in family)
                {
                    string relation = GetFamilyRelation(pawn, relative);
                    string viewerName = GetViewerNameFromPawn(relative);
                    report.AppendLine($"  • {viewerName}: {relation}");
                }
            }

            // Viewer friends (top 5 by opinion) - EXCLUDE family members
            var viewerFriends = assignmentManager.GetAllViewerPawns()
                .Where(p => p != pawn &&
                       pawn.relations.OpinionOf(p) > 10 &&
                       !family.Contains(p)) // Exclude family members
                .OrderByDescending(p => pawn.relations.OpinionOf(p))
                .Take(5)
                .ToList();

            if (viewerFriends.Count > 0)
            {
                report.AppendLine("🎮 Viewer Friends:");
                foreach (var friend in viewerFriends)
                {
                    int opinion = pawn.relations.OpinionOf(friend);
                    string friendViewerName = GetViewerNameFromPawn(friend);
                    report.AppendLine($"  • {friendViewerName}: +{opinion} 😊");
                }
            }

            // Viewer rivals (top 5 by negative opinion) - EXCLUDE family members
            var viewerRivals = assignmentManager.GetAllViewerPawns()
                .Where(p => p != pawn &&
                       pawn.relations.OpinionOf(p) < -10 &&
                       !family.Contains(p)) // Exclude family members
                .OrderBy(p => pawn.relations.OpinionOf(p))
                .Take(5)
                .ToList();

            if (viewerRivals.Count > 0)
            {
                report.AppendLine("⚔️ Viewer Rivals:");
                foreach (var rival in viewerRivals)
                {
                    int opinion = pawn.relations.OpinionOf(rival);
                    string rivalViewerName = GetViewerNameFromPawn(rival);
                    report.AppendLine($"  • {rivalViewerName}: {opinion} 😠");
                }
            }

            // Overall social summary - EXCLUDE family members from counts
            int totalFriends = assignmentManager.GetAllViewerPawns()
                .Count(p => p != pawn && pawn.relations.OpinionOf(p) > 10 && !family.Contains(p));
            int totalRivals = assignmentManager.GetAllViewerPawns()
                .Count(p => p != pawn && pawn.relations.OpinionOf(p) < -10 && !family.Contains(p));

            report.AppendLine($"📊 Social Summary: {family.Count} family, {totalFriends} viewer friends, {totalRivals} viewer rivals");

            if (family.Count == 0 && viewerFriends.Count == 0 && viewerRivals.Count == 0)
            {
                report.AppendLine("No significant relationships found.");
            }

            return report.ToString();
        }

        private static string GetFamilyRelation(Pawn pawn, Pawn relative)
        {
            if (pawn.relations.DirectRelationExists(PawnRelationDefOf.Spouse, relative)) return "Spouse 💍";
            if (pawn.relations.DirectRelationExists(PawnRelationDefOf.Lover, relative)) return "Lover ❤️";
            if (pawn.relations.DirectRelationExists(PawnRelationDefOf.Fiance, relative)) return "Fiancé 💑";
            if (pawn.relations.DirectRelationExists(PawnRelationDefOf.Child, relative)) return "Child 👶";
            if (pawn.relations.DirectRelationExists(PawnRelationDefOf.Parent, relative)) return "Parent 👨‍👦";
            if (pawn.relations.DirectRelationExists(PawnRelationDefOf.Sibling, relative)) return "Sibling 👫";
            if (pawn.relations.DirectRelationExists(PawnRelationDefOf.ExSpouse, relative)) return "Ex-Spouse 💔";
            if (pawn.relations.DirectRelationExists(PawnRelationDefOf.ExLover, relative)) return "Ex-Lover 💔";

            // Check for indirect relations if no direct relation found
            if (pawn.relations.FamilyByBlood.Contains(relative)) return "Blood Relative 👨‍👩‍👧‍👦";
            if (pawn.GetRelations(relative).Any()) return "Relative";

            return "Relation"; // Fallback
        }

        private static string GetViewerNameFromPawn(Pawn pawn)
        {
            if (pawn?.Name is NameTriple nameTriple)
            {
                // Prefer the Nick (username) if it's not empty
                if (!string.IsNullOrEmpty(nameTriple.Nick))
                    return nameTriple.Nick;

                // Fallback to first name if Nick is empty
                return nameTriple.First;
            }
            return pawn?.Name?.ToString() ?? "Unknown";
        }

        private static string GetDisplayNameForRelations(Pawn pawn, GameComponent_PawnAssignmentManager assignmentManager = null)
        {
            if (pawn?.Name is NameTriple nameTriple)
            {
                // If this is a viewer pawn, always use the Nick (username)
                if (assignmentManager?.IsViewerPawn(pawn) == true)
                    return nameTriple.Nick;

                // For non-viewer pawns, use First name (more natural for family relationships)
                return nameTriple.First;
            }
            return pawn?.Name?.ToString() ?? "Unknown";
        }

        private static string HandleSkillsInfo(Pawn pawn, string[] args)
        {
            var report = new StringBuilder();
            report.AppendLine($"🎯 Skills Report: "); // for {pawn.Name}:

            var skills = pawn.skills?.skills;
            if (skills == null || skills.Count == 0)
            {
                return $"{pawn.Name} has no skills tracked.";
            }

            // Group skills by their display order (matching RimWorld's UI)
            var orderedSkills = skills
                .OrderBy(s => s.def.listOrder)
                .ToList();

            foreach (var skill in orderedSkills)
            {
                if (skill == null || skill.def == null) continue;

                string skillName = StripTags(skill.def.LabelCap);
                string passionEmoji = GetPassionEmoji(skill.passion);
                // Remove extra emote
                // string levelDescription = GetSkillLevelDescriptionDetailed(skill.Level);
                // report.AppendLine($"• {passionEmoji}{skillName}: {skill.Level} {levelDescription}");
                report.AppendLine($"• {passionEmoji}{skillName}: {skill.Level} ");
            }

            //// Add learning summary
            //var burningPassions = skills.Count(s => s.passion == Passion.Major);
            //var minorPassions = skills.Count(s => s.passion == Passion.Minor);

            //if (burningPassions > 0 || minorPassions > 0)
            //{
            //    report.AppendLine($"📚 Passions: {burningPassions} 🔥🔥, {minorPassions} 🔥");
            //}

            // Top 3 skills
            var topSkills = skills.OrderByDescending(s => s.Level).Take(3);
            if (topSkills.Any(s => s.Level >= 10))
            {
                report.Append("🏆 Top Skills: ");
                var topSkillNames = topSkills.Select(s => $"{StripTags(s.def.LabelCap)} ({s.Level})");
                report.AppendLine(string.Join(", ", topSkillNames));
            }

            return report.ToString();
        }

        private static string GetPassionEmoji(Passion passion)
        {
            return passion switch
            {
                Passion.Major => "🔥🔥", // Burning passion
                Passion.Minor => "🔥",   // Minor passion  
                _ => ""                // No passion
            };
        }

        private static string GetSkillLevelDescriptionDetailed(int level)
        {
            //if (level >= 20) return "Legendary 🌟";
            //if (level >= 18) return "Master 🎯";
            //if (level >= 16) return "Expert 💪";
            //if (level >= 14) return "Proficient ✨";
            //if (level >= 12) return "Skilled 👍";
            //if (level >= 10) return "Adept 👌";
            //if (level >= 8) return "Competent ✅";
            //if (level >= 6) return "Experienced 📚";
            //if (level >= 4) return "Novice 👶";
            //if (level >= 2) return "Beginner 🌱";
            //if (level >= 1) return "Awkward 🐣";
            //return "Ignorant ❓";

            if (level >= 20) return "🌟";
            if (level >= 18) return "🎯";
            if (level >= 16) return "💪";
            if (level >= 14) return "✨";
            if (level >= 12) return "👍";
            if (level >= 10) return "👌";
            if (level >= 8) return "✅";
            if (level >= 6) return "📚";
            if (level >= 4) return "👶";
            if (level >= 2) return "🌱";
            if (level >= 1) return "🐣";
            return "";
        }

        private static string HandleStatsInfo(Pawn pawn, string[] args)
        {
            if (args.Length == 0)
            {
                return GetStatsOverview(pawn);
            }

            return GetSpecificStats(pawn, args);
        }

        private static string GetStatsOverview(Pawn pawn)
        {
            var report = new StringBuilder();
            report.AppendLine($"📊 Stats Overview:");  // for { pawn.Name}:

            // Show a few key stats as examples
            var keyStats = new[]
            {
                StatDefOf.MoveSpeed,
                StatDefOf.ShootingAccuracyPawn,
                StatDefOf.MeleeHitChance,
                StatDefOf.MeleeDPS,
                StatDefOf.WorkSpeedGlobal,
                StatDefOf.MedicalTendQuality,
                StatDefOf.SocialImpact,
                StatDefOf.TradePriceImprovement
            };

            foreach (var statDef in keyStats)
            {
                if (statDef == null) continue;

                float value = pawn.GetStatValue(statDef);
                string formattedValue = FormatStatValue(statDef, value);

                report.AppendLine($"• {StripTags(statDef.LabelCap)}: {formattedValue}");
            }

            report.AppendLine();
            report.AppendLine("💡 Usage: !mypawn stats <stat1> <stat2> ...");
            report.AppendLine("Examples: !mypawn stats movespeed shootingaccuracy meleedps");
            // reduced for brevity
            //report.AppendLine("Available: movespeed, shootingaccuracy, meleehitchance, meleedps, workspeed, medicaltend, socialimpact, tradeprice, etc.");

            return report.ToString();
        }

        private static string GetSpecificStats(Pawn pawn, string[] args)
        {
            var report = new StringBuilder();
            report.AppendLine($"📊 Stats: "); // for {pawn.Name}:

            var foundStats = new List<string>();
            var notFoundStats = new List<string>();

            foreach (var statName in args)
            {
                var statDef = FindStatDef(statName);
                if (statDef != null)
                {
                    float value = pawn.GetStatValue(statDef);
                    string formattedValue = FormatStatValue(statDef, value);
                    string description = StripTags(statDef.description) ?? "";

                    // Truncate long descriptions
                    if (description.Length > 80)
                    {
                        description = description.Substring(0, 77) + "...";
                    }

                    report.AppendLine($"• {StripTags(statDef.LabelCap)}: {formattedValue}");
                    if (!string.IsNullOrEmpty(description))
                    {
                        report.AppendLine($"  {description}");
                    }

                    foundStats.Add(statName);
                }
                else
                {
                    notFoundStats.Add(statName);
                }
            }

            // Add not found stats at the end
            if (notFoundStats.Count > 0)
            {
                report.AppendLine();
                report.AppendLine($"❌ Unknown stats: {string.Join(", ", notFoundStats)}");
                report.AppendLine("Use !mypawn stats to see available stats.");
            }

            return report.ToString();
        }

        private static StatDef FindStatDef(string statName)
        {
            string searchName = statName.ToLower().Replace("_", "").Replace(" ", "");

            return DefDatabase<StatDef>.AllDefs
                .FirstOrDefault(stat =>
                    stat.defName.ToLower().Replace("_", "").Contains(searchName) ||
                    stat.label.ToLower().Replace(" ", "").Contains(searchName));
        }

        private static string FormatStatValue(StatDef statDef, float value)
        {
            if (statDef == null) return "N/A";

            try
            {
                // Let RimWorld handle the formatting - it knows best how to display each stat
                return statDef.ValueToString(value, statDef.toStringNumberSense);
            }
            catch (Exception ex)
            {
                Logger.Warning($"Error formatting stat {statDef.defName}: {ex.Message}");
                return value.ToString("F1"); // Simple fallback
            }
        }

        private static string HandleBackstoriesInfo(Pawn pawn, string[] args)
        {
            var report = new StringBuilder();
            report.Append($"Age:🧬{pawn.ageTracker.AgeBiologicalYears}/⏳{pawn.ageTracker.AgeChronologicalYears} | ");
            report.AppendLine($"👤 Backstories:");  // for {pawn.Name}:

            // Childhood backstory - truncated to fit in one message
            if (pawn.story?.Childhood != null)
            {
                var childhoodDesc = StripTags(pawn.story.Childhood.FullDescriptionFor(pawn));
                // var truncatedChildhood = TruncateDescription(childhoodDesc, 183); // Limit to 188 chars

                report.AppendLine($"🎒 Childhood: {StripTags(pawn.story.Childhood.title)}");
                // report.AppendLine($"   {truncatedChildhood}");
            }
            else
            {
                report.AppendLine("🎒 Childhood: No childhood backstory");
            }

            report.AppendLine(); // Spacing

            // Adulthood backstory - truncated to fit in one message
            if (pawn.story?.Adulthood != null)
            {
                var adulthoodDesc = StripTags(pawn.story.Adulthood.FullDescriptionFor(pawn));
                // var truncatedAdulthood = TruncateDescription(adulthoodDesc, 183); // Limit to 183 chars

                report.AppendLine($"🧑 Adulthood: {StripTags(pawn.story.Adulthood.title)}");
                // report.AppendLine($"   {truncatedAdulthood}");
            }
            else if (pawn.ageTracker.AgeBiologicalYears >= 18)
            {
                report.AppendLine("🧑 Adulthood: No adulthood backstory");
            }
            else
            {
                report.AppendLine("🧑 Adulthood: Too young for adulthood backstory");
            }

            return report.ToString();
        }

        private static string HandleTraitsInfo(Pawn pawn, string[] args)
        {
            var report = new StringBuilder();
            report.AppendLine($"🎭 Traits:"); // for { pawn.Name}

            if (pawn.story?.traits == null || pawn.story.traits.allTraits.Count == 0)
            {
                return $"{pawn.Name} has no traits.";
            }

            foreach (var trait in pawn.story.traits.allTraits)
            {
                if (trait == null) continue;

                string traitName = StripTags(trait.LabelCap);
                string traitDesc = StripTags(trait.def.description);

                report.AppendLine($"• {traitName}");
                report.AppendLine($"  {traitDesc}");

                // Add spacing between traits
                if (trait != pawn.story.traits.allTraits.Last())
                {
                    report.AppendLine();
                }
            }

            return report.ToString();
        }

        // === Work ===
        private static string HandleWorkInfo(Pawn pawn, string[] args)
        {
            // Check if pawn can work
            if (pawn.workSettings == null || !pawn.workSettings.EverWork)
            {
                return $"{pawn.Name} is not capable of work.";
            }

            // Ensure work settings are initialized using RimWorld's proper method
            if (!pawn.workSettings.Initialized)
            {
                pawn.workSettings.EnableAndInitialize();
            }

            // If no args, show top 5 highest priority work types
            if (args.Length == 0)
            {
                return GetTopWorkPriorities(pawn);
            }

            // Handle individual work type lookups and changes
            return HandleIndividualWorkCommands(pawn, args);
        }

        private static string GetTopWorkPriorities(Pawn pawn)
        {
            var report = new StringBuilder();
            report.AppendLine("💼 Top Work Priorities:");

            // Get work types with priority 1 (highest)
            var topPriorityWork = WorkTypeDefsUtility.WorkTypeDefsInPriorityOrder
                .Where(w => !pawn.WorkTypeIsDisabled(w) && pawn.workSettings.GetPriority(w) == 1)
                .Take(5)
                .ToList();

            if (topPriorityWork.Count > 0)
            {
                report.AppendLine("🔥 Highest (1):");
                foreach (var workType in topPriorityWork)
                {
                    string label = StripTags(workType.pawnLabel);
                    report.AppendLine($"  • {label}");
                }
            }
            else
            {
                report.AppendLine("No work set to highest priority");
            }

            report.AppendLine();
            report.AppendLine("💡 Usage: !mypawn work <worktype> [1-4]");
            // report.AppendLine("Examples: !mypawn work doctor | !mypawn work firefight 1 | !mypawn work growing 2 cleaning 1");

            return report.ToString();
        }

        private static string HandleIndividualWorkCommands(Pawn pawn, string[] args)
        {
            var results = new List<string>();

            // Process args in pairs (worktype, priority) or single (worktype lookup)
            for (int i = 0; i < args.Length; i++)
            {
                string workTypeName = args[i];

                // Find the work type
                var workType = FindWorkType(workTypeName);
                if (workType == null)
                {
                    results.Add($"❌ Unknown work: {workTypeName}");
                    continue;
                }

                // Check if work type is disabled for this pawn
                if (pawn.WorkTypeIsDisabled(workType))
                {
                    results.Add($"❌ {workType.label} disabled");
                    continue;
                }

                // If next arg exists and is a number 1-4, set priority
                if (i + 1 < args.Length && int.TryParse(args[i + 1], out int newPriority) && newPriority >= 0 && newPriority <= 4)
                {
                    int oldPriority = pawn.workSettings.GetPriority(workType);
                    pawn.workSettings.SetPriority(workType, newPriority);

                    string priorityName = GetPriorityName(newPriority);
                    results.Add($"✅ {workType.label}: {oldPriority}→{newPriority} ({priorityName})");
                    i++; // Skip the priority arg since we used it
                }
                else
                {
                    // Just show current priority
                    int currentPriority = pawn.workSettings.GetPriority(workType);
                    string priorityName = GetPriorityName(currentPriority);
                    results.Add($"📋 {workType.label}: {currentPriority} ({priorityName})");
                }
            }

            return string.Join(" | ", results);
        }

        private static WorkTypeDef FindWorkType(string workTypeName)
        {
            string searchName = workTypeName.ToLower().Replace("_", "").Replace(" ", "");

            return DefDatabase<WorkTypeDef>.AllDefs
                .FirstOrDefault(w => w != null &&
                    (!string.IsNullOrEmpty(w.defName) && w.defName.ToLower().Replace("_", "").Contains(searchName)) ||
                    (!string.IsNullOrEmpty(w.label) && w.label.ToLower().Replace(" ", "").Contains(searchName)));
        }

        private static string GetPriorityName(int priority)
        {
            return priority switch
            {
                1 => "🔥 Highest",
                2 => "💪 High",
                3 => "👍 Medium",
                4 => "👌 Low",
                _ => "❌ Disabled"
            };
        }
    }
}