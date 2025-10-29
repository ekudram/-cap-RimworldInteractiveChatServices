// BuyableTrait.cs
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

            Logger.Debug($"Creating trait: DefName={traitDef.defName}, Degree={degreeData?.degree}, Name={Name}");

            // Replace [PAWN_nameDef] with [PAWN_name] in description
            Description = (degreeData?.description ?? traitDef.description)?.Replace("[PAWN_nameDef]", "[PAWN_name]");

            // Extract stat modifiers
            Stats = new List<string>();
            if (degreeData?.statOffsets != null)
            {
                foreach (var statOffset in degreeData.statOffsets)
                {
                    string sign = statOffset.value > 0 ? "+" : "";
                    Stats.Add($"{sign}{statOffset.value * 100}% {statOffset.stat.LabelCap}");
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

            DebugTraits(); // Call debug method to log specific traits
        }

        private void SetDefaultPrices(TraitDef traitDef, TraitDegreeData degreeData)
        {
            // Base prices for a typical trait
            int baseAddPrice = 500;  // ~5 minutes of earning
            int baseRemovePrice = 800; // ~8 minutes of earning

            float impactFactor = 1.0f;

            // Adjust based on stat offsets (positive or negative)
            if (degreeData?.statOffsets != null)
            {
                foreach (var statOffset in degreeData.statOffsets)
                {
                    // Absolute value so both positive and negative traits have value
                    impactFactor += Math.Abs(statOffset.value) * 5f; // Reduced multiplier
                }
            }

            // Adjust for degree (higher absolute degree = more impact)
            impactFactor += Math.Abs(Degree) * 0.5f;

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

            // Ensure minimum prices
            AddPrice = Math.Max(100, AddPrice);
            RemovePrice = Math.Max(150, RemovePrice);
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
                    float weight = 1.0f;
                    if (statOffset.stat.defName.Contains("GlobalLearningFactor") ||
                        statOffset.stat.defName.Contains("WorkSpeedGlobal") ||
                        statOffset.stat.defName.Contains("MoveSpeed"))
                    {
                        weight = 2.0f;
                    }

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

        private void DebugTraits()
        {
            var beautyTraits = TraitsManager.AllBuyableTraits.Values
                .Where(t => t.DefName.Contains("Beauty"))
                .ToList();

            foreach (var trait in beautyTraits)
            {
                Logger.Debug($"Beauty Trait - Key: {GetTraitKey(trait)}, DefName: {trait.DefName}, Name: {trait.Name}, Degree: {trait.Degree}");
            }
        }

        private string GetTraitKey(BuyableTrait trait)
        {
            return TraitsManager.AllBuyableTraits.FirstOrDefault(x => x.Value == trait).Key;
        }

    }
}