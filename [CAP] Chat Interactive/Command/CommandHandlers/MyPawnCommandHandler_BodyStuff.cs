
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
    public static class MyPawnCommandHandler_Body
    {
        // === health ===
        public static string HandlehealthInfo(Pawn pawn, string[] args)
        {
            var report = new StringBuilder();
            // report.AppendLine("❤️ Health:");
            report.AppendLine("RICS.MPCH.HealthHeader".Translate());
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

                //report.AppendLine($"• Pain: {painEmoji} {painStatus} ({pain.ToStringPercent()}/{maxPain.ToStringPercent()})");
                report.AppendLine("RICS.MPCH.Pain".Translate(painEmoji, painStatus, pain.ToStringPercent(), maxPain.ToStringPercent()));

                // Add health
                string hp = pawn.health.summaryHealth.SummaryHealthPercent.ToStringPercent();
                // report.AppendLine($"• Health: {hp}");
                report.AppendLine("RICS.MPCH.OverallHealth".Translate(hp));
            }
            catch (Exception ex)
            {
                Logger.Error($"Error in health info: {ex}");
                // return "Error retrieving health information.";
                return "RICS.MPCH.HealthError".Translate();
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

        // === Body ===
        public static string HandleBodyInfo(Pawn pawn, string[] args)
        {
            // Add body type at the beginning
            string bodyTypeInfo = "";
            if (pawn.story?.bodyType != null)
            {
                bodyTypeInfo = $"🧬 Body Type: {pawn.story.bodyType.label} |";
                string displayName = GetBodyTypeDisplayName(pawn);
                bodyTypeInfo = $"🧬 Body Type: {displayName} |";
            }


            if (pawn.health?.hediffSet?.hediffs == null || pawn.health.hediffSet.hediffs.Count == 0)
            {
                // return $"{bodyTypeInfo}{pawn.Name} has no health conditions. 🟢";
                return "RICS.MPCH.BodyNoConditions".Translate(pawn.LabelShortCap);
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
                    // return $"❌ Body part '{bodyPartFilter}' not found. Try: torso, head, arm, leg, etc.";
                    return "RICS.MPCH.BodyPartNotFound".Translate(bodyPartFilter);
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
                // report.AppendLine($"🏥 Health Report - {targetPart.LabelCap} ({uniqueConditionCount} conditions):");
                report.AppendLine("RICS.MPCH.BodyReportSpecific".Translate(targetPart.LabelCap, uniqueConditionCount));
            }
            else
            {
                // report.AppendLine($"🏥 Health Report ({uniqueConditionCount} conditions):");
                report.AppendLine("RICS.MPCH.BodyReportFull".Translate(uniqueConditionCount));
            }

            // Add temperature comfort range (only in full report)
            if (targetPart == null)
            {
                float minComfy = pawn.GetStatValue(StatDefOf.ComfyTemperatureMin);
                float maxComfy = pawn.GetStatValue(StatDefOf.ComfyTemperatureMax);
                // report.AppendLine($"🌡️ Comfort Range: {minComfy.ToStringTemperature()} ~ {maxComfy.ToStringTemperature()}");
                report.AppendLine("RICS.MPCH.ComfortRange".Translate(minComfy.ToStringTemperature(), maxComfy.ToStringTemperature()));
            }

            if (healthConditions.Count == 0)
            {
                return targetPart != null
                    ? "RICS.MPCH.NoIssuesOnPart".Translate(targetPart.LabelCap)
                    : "RICS.MPCH.NoHealthIssues".Translate();
                //if (targetPart != null)
                //{
                //    report.AppendLine($"No visible issues on {targetPart.LabelCap}. ✅");
                //}
                //else
                //{
                //    report.AppendLine("No visible health issues. ✅");
                //}
                //return report.ToString();
            }

            // Group conditions by body part for display
            // report.AppendLine("Health Conditions:");
            report.AppendLine("RICS.MPCH.HealthConditionsHeader".Translate());

            // Sort body parts by height (head to toe)
            var sortedPartGroups = healthConditions
                .OrderBy(g => g.Key != null)
                .ThenByDescending(g => g.Key?.height ?? 0f)
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
                    // report.AppendLine($"... and {hiddenParts} more body parts with conditions");
                    report.AppendLine("RICS.MPCH.HiddenParts".Translate(hiddenParts));
                }

                if (totalHediffCount > uniqueConditionCount)
                {
                    // report.AppendLine($"({totalHediffCount} individual injuries across all body)");
                    report.AppendLine("RICS.MPCH.TotalInjuries".Translate(totalHediffCount));
                }
            }

            if (targetPart == null)
            {
                // Add health severity summary
                string severity = GetOverallHealthSeverity(pawn);
                // report.AppendLine($"📊 Overall Status: {severity}");
                report.AppendLine("RICS.MPCH.OverallStatus".Translate(severity));
                // Add immediate danger warnings
                // Check for bleeding
                float bleedRate = pawn.health.hediffSet.BleedRateTotal;
                if (bleedRate > 0f)
                {
                    int bleedoutTime = HealthUtility.TicksUntilDeathDueToBloodLoss(pawn);
                    if (bleedoutTime < GenDate.TicksPerDay)
                    {
                        // report.AppendLine($"Bleedout in {bleedoutTime.ToStringTicksToPeriod()}!");
                        report.AppendLine("RICS.MPCH.BleedoutIn".Translate(bleedoutTime.ToStringTicksToPeriod()));
                    }
                }
                if (pawn.health.hediffSet.HasTendableHediff() || pawn.health.hediffSet.HasTendableNonInjuryNonMissingPartHediff())
                {
                    // report.AppendLine("⚠️ Needs medical attention!");
                    report.AppendLine("RICS.MPCH.NeedsMedical".Translate());
                }
            }
            return report.ToString();
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

            string display = MyPawnCommandHandler.StripTags(hediff.LabelCap);

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


        // === implants ===
        public static string HandleImplantsInfo(Pawn pawn, string[] args)
        {
            if (pawn.health?.hediffSet?.hediffs == null)
            {
                // return $"{pawn.Name} has no health records.";
                return "RICS.MPCH.NoHealthRecords".Translate(pawn.LabelShortCap);
            }

            var report = new StringBuilder();
            // report.AppendLine("🔧 Implants:");
            report.AppendLine("RICS.MPCH.ImplantsHeader".Translate());

            // Get all visible implants (added parts)
            var allHediffs = pawn.health.hediffSet.hediffs.ToList();

            var implants = allHediffs
                .Where(h =>
                {
                    bool isVisible = h.Visible;
                    bool isImplant = IsImplantOrAddedPart(h);
                    //Logger.Debug($"Filtering: {h.def.defName}, Visible={isVisible}, IsImplant={isImplant}");
                    return isVisible && isImplant;
                })
                .ToList();

            if (implants.Count == 0)
            {
                // report.AppendLine("No implants or bionic replacements found.");
                report.AppendLine("RICS.MPCH.NoImplants".Translate());
                return report.ToString();
            }

            // Group identical implants directly to show count (e.g., "Bionic arm x2")
            var groupedByImplant = implants
                .GroupBy(h => MyPawnCommandHandler.StripTags(h.LabelCap))
                .OrderBy(g => g.Key);

            foreach (var implantGroup in groupedByImplant)
            {
                string implantName = implantGroup.Key;
                int count = implantGroup.Count();

                if (count > 1)
                    report.AppendLine($" ◦ {implantName} x{count}");
                else
                    report.AppendLine($" ◦ {implantName}");
            }
            // report.AppendLine($"📊 {implants.Count} implant(s)");
            report.AppendLine("RICS.MPCH.ImplantsSummary".Translate(implants.Count));
            return report.ToString();
        }

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
    }
}