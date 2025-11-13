// BuyableTrait.cs
// Copyright (c) Captolamia. All rights reserved.
// Licensed under the AGPLv3 License. See LICENSE file in the project root for full license information.
// A class representing a trait that can be bought or sold in the chat interaction system.
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace CAP_ChatInteractive.Traits
{
    public class BuyableTrait
    {
        // Core settings
        public string DefName { get; set; }
        public string Name { get; set; }
        public int Degree { get; set; }
        public string Description { get; set; }
        public List<string> Stats { get; set; }
        public List<string> Conflicts { get; set; }

        // Purchase settings
        public bool CanAdd { get; set; } = true;
        public bool CanRemove { get; set; } = true;
        public int AddPrice { get; set; } = 3500;
        public int RemovePrice { get; set; } = 5500;
        public bool BypassLimit { get; set; } = false;

        // Additional data
        public bool CustomName { get; set; } = false;
        public string KarmaTypeForAdding { get; set; } = null;
        public string KarmaTypeForRemoving { get; set; } = null;
        public string ModSource { get; set; } = "RimWorld";
        public int Version { get; set; } = 1;

        public BuyableTrait() { }

        public BuyableTrait(TraitDef traitDef, TraitDegreeData degreeData = null)
        {
            DefName = traitDef.defName;
            Degree = degreeData?.degree ?? 0;

            // Use the degree-specific label if available, otherwise use trait def label
            Name = degreeData?.label?.CapitalizeFirst() ?? traitDef.LabelCap;

            // Replace [PAWN_nameDef] with [PAWN_name] in description
            Description = (degreeData?.description ?? traitDef.description)?.Replace("[PAWN_nameDef]", "[PAWN_name]");

            // Extract stat modifiers
            Stats = new List<string>();
            if (degreeData?.statOffsets != null)
            {
                foreach (var statOffset in degreeData.statOffsets)
                {
                    string sign = statOffset.value > 0 ? "+" : "";

                    // Check if this stat is typically displayed as a percentage
                    if (statOffset.stat.formatString == "F1" ||
                        statOffset.stat.ToString().Contains("Factor") ||
                        statOffset.stat.ToString().Contains("Percent") )
                    {
                        // Display as percentage for stats that are normally percentages
                        Stats.Add($"{sign}{statOffset.value * 100:f1}% {statOffset.stat.LabelCap}");
                    }
                    else
                    {
                        // Display as raw value for other stats
                        Stats.Add($"{sign}{statOffset.value} {statOffset.stat.LabelCap}");
                    }
                }
            }

            // Extract conflicts
            Conflicts = new List<string>();
            if (traitDef.conflictingTraits != null)
            {
                foreach (var conflict in traitDef.conflictingTraits)
                {
                    if (conflict != null && !string.IsNullOrEmpty(conflict.LabelCap))
                    {
                        Conflicts.Add(conflict.LabelCap);
                    }
                }
            }
            ModSource = traitDef.modContentPack?.Name ?? "RimWorld";

            // Set default prices based on trait impact
            SetDefaultPrices(traitDef, degreeData);
        }

        private void SetDefaultPrices(TraitDef traitDef, TraitDegreeData degreeData)
        {
            // Base prices for a typical trait - FURTHER REDUCED for chat economy
            int baseAddPrice = 150;  // Reduced from 300
            int baseRemovePrice = 250; // Reduced from 500

            float impactFactor = 1.0f;

            // Adjust based on stat offsets (positive or negative)
            if (degreeData?.statOffsets != null)
            {
                foreach (var statOffset in degreeData.statOffsets)
                {
                    float statImpact = Math.Abs(statOffset.value);

                    // Apply different weights based on stat importance
                    float weight = GetStatWeight(statOffset.stat);
                    impactFactor += statImpact * weight;
                }
            }

            // Adjust for degree (higher absolute degree = more impact)
            impactFactor += Math.Abs(Degree) * 0.3f;

            // Negative traits should be cheaper to add and more expensive to remove
            float addMultiplier = impactFactor;
            float removeMultiplier = impactFactor;

            // If it's generally a negative trait, adjust prices
            if (IsGenerallyNegativeTrait(traitDef, degreeData))
            {
                addMultiplier *= 0.3f;  // Much cheaper to add negative traits
                removeMultiplier *= 1.5f; // More expensive to remove negative traits
            }

            AddPrice = (int)(baseAddPrice * addMultiplier);
            RemovePrice = (int)(baseRemovePrice * removeMultiplier);

            // Ensure minimum prices - REDUCED
            AddPrice = Math.Max(30, AddPrice);  // Reduced from 50
            RemovePrice = Math.Max(50, RemovePrice); // Reduced from 80
        }

        private float GetStatWeight(StatDef stat)
        {
            // High importance stats - REDUCED WEIGHTS
            if (stat.defName.Contains("GlobalLearningFactor") ||
                stat.defName.Contains("WorkSpeedGlobal") ||
                stat.defName.Contains("MoveSpeed") ||
                stat.defName.Contains("MeleeHitChance") ||
                stat.defName.Contains("AimingDelayFactor") ||
                stat.defName.Contains("ShootingAccuracyPawn"))
            {
                return 2.5f; // REDUCED from 25f to 2.5f (10x reduction)
            }

            // Medium importance stats - REDUCED WEIGHTS  
            if (stat.defName.Contains("HungerRate") ||
                stat.defName.Contains("RestRate") ||
                stat.defName.Contains("PsychicSensitivity") ||
                stat.defName.Contains("ToxicResistance") ||
                stat.defName.Contains("PainShockThreshold"))
            {
                return 1.5f; // REDUCED from 15f to 1.5f (10x reduction)
            }

            // Low importance stats - REDUCED WEIGHTS
            if (stat.defName.Contains("Beauty") ||
                stat.defName.Contains("ComfyTemperatureMin") ||
                stat.defName.Contains("ComfyTemperatureMax"))
            {
                return 0.5f; // REDUCED from 5f to 0.5f (10x reduction)
            }

            // Default weight - REDUCED
            return 1.0f; // REDUCED from 10f to 1.0f (10x reduction)
        }

        private bool IsGenerallyNegativeTrait(TraitDef traitDef, TraitDegreeData degreeData)
        {
            // Check if this is likely a negative trait
            if (degreeData?.statOffsets != null)
            {
                float netStatImpact = 0f;
                foreach (var statOffset in degreeData.statOffsets)
                {
                    // Some stats are more important than others
                    float weight = GetStatWeight(statOffset.stat);
                    netStatImpact += statOffset.value * weight;
                }
                return netStatImpact < 0;
            }

            // Check trait name for common negative indicators
            string traitName = traitDef.defName.ToLower();
            return traitName.Contains("ugly") || traitName.Contains("slow") ||
                   traitName.Contains("weak") || traitName.Contains("stupid") ||
                   traitName.Contains("annoying") || traitName.Contains("creep");
        }

        public string GetDisplayName()
        {
            if (CustomName && !string.IsNullOrEmpty(Name))
                return Name;
            return DefName;
        }

        public string GetFullDescription()
        {
            var description = Description ?? "";

            if (Stats.Count > 0)
            {
                description += "\n\n" + string.Join("\n", Stats);
            }

            if (Conflicts.Count > 0)
            {
                description += $"\n\nConflicts with: {string.Join(", ", Conflicts)}";
            }

            return description;
        }
    }
}