// GeneUtils.cs
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
using CAP_ChatInteractive;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;

namespace _CAP__Chat_Interactive.Utilities
{
    // Updated GeneUtils.cs with correct field names
    public static class GeneUtils
    {
        public static float CalculateXenotypeGeneCost(string xenotypeName)
        {
            if (!ModsConfig.BiotechActive) return 1.0f;

            var xenotypeDef = DefDatabase<XenotypeDef>.AllDefs.FirstOrDefault(x =>
                x.defName.Equals(xenotypeName, StringComparison.OrdinalIgnoreCase));

            if (xenotypeDef == null) return 1.0f;

            float totalCost = 0f;
            int geneCount = 0;

            // Calculate cost from genes - CORRECTED FIELD NAME
            if (xenotypeDef.genes != null)
            {
                foreach (var geneDef in xenotypeDef.genes)
                {
                    totalCost += GetGeneMarketValue(geneDef);
                    geneCount++;
                }
            }

            // If no genes found, return base multiplier
            if (geneCount == 0) return 1.0f;

            // Calculate average gene value and scale to reasonable multiplier
            float averageGeneValue = totalCost / geneCount;
            float baseMultiplier = Mathf.Clamp(averageGeneValue / 1000f, 0.5f, 5.0f);

            // Apply xenotype-specific adjustments
            return ApplyXenotypeScalingFactors(xenotypeDef, baseMultiplier);
        }

        private static float GetGeneMarketValue(GeneDef geneDef)
        {
            // Genes have marketValueFactor, biostatCpx, and other properties
            float baseValue = geneDef.marketValueFactor * 1000f; // marketValueFactor is a multiplier

            // If no direct market value factor, calculate based on complexity and impact
            if (geneDef.marketValueFactor <= 1.0f) // Default is 1.0
            {
                baseValue = geneDef.biostatCpx * 150f; // Complexity contributes to value
                baseValue += Mathf.Abs(geneDef.biostatMet) * 75f; // Metabolic impact
                baseValue += Mathf.Abs(geneDef.biostatArc) * 200f; // Archite requirement (very valuable)
            }

            // Adjust for gene category
            if (geneDef.displayCategory != null)
            {
                switch (geneDef.displayCategory.defName)
                {
                    case "Archite": baseValue *= 2.5f; break;
                    case "Metabolic": baseValue *= 1.3f; break;
                    case "Special": baseValue *= 1.8f; break;
                    default: baseValue *= 1.0f; break;
                }
            }

            // Adjust for specific high-value genes
            if (geneDef.defName.Contains("Deathless") || geneDef.defName.Contains("Ageless"))
                baseValue *= 3.0f;
            else if (geneDef.defName.Contains("Robust"))
                baseValue *= 2.0f;
            else if (geneDef.defName.Contains("Weak") || geneDef.defName.Contains("Feeble"))
                baseValue *= 0.3f;

            return Mathf.Max(baseValue, 25f); // Minimum value
        }
        /// <summary>
        /// Applies scaling factors to xenotype pricing based on overall power and balance considerations.
        /// This method is public to allow other mods to extend or override xenotype pricing logic.
        /// </summary>
        /// <param name="xenotypeDef">The xenotype definition to evaluate</param>
        /// <param name="baseMultiplier">The base multiplier calculated from gene values</param>
        /// <returns>Final price multiplier for the xenotype</returns>

        public static float ApplyXenotypeScalingFactors(XenotypeDef xenotypeDef, float baseMultiplier)
        {
            float multiplier = baseMultiplier;

            // Apply special xenotype adjustments based on overall power
            switch (xenotypeDef.defName)
            {
                case "Sanguophage":
                    multiplier *= 2.2f; // Very valuable - deathless, hemogen, etc.
                    break;
                case "Hussar":
                    multiplier *= 1.8f; // Combat powerhouse but with dependencies
                    break;
                case "Genie":
                    multiplier *= 1.5f; // Great for research and intellectual
                    break;
                case "Highmate":
                    multiplier *= 1.4f; // Social benefits
                    break;
                case "Waster":
                    multiplier *= 1.1f; // Mild benefits with pollution immunity
                    break;
                case "Pigskin":
                    multiplier *= 0.8f; // Mostly negative traits
                    break;
                case "Dirtmole":
                    multiplier *= 1.2f; // Mining benefits
                    break;
                case "Impid":
                    multiplier *= 1.3f; // Fire-based abilities
                    break;
                case "Yttakin":
                    multiplier *= 1.1f; // Cold resistance
                    break;
                case "Neanderthal":
                    multiplier *= 0.9f; // Strong but slow
                    break;
                    // Add more as needed
            }

            return Mathf.Clamp(multiplier, 0.3f, 8f);
        }

        public static List<GeneDef> GetXenotypeGenes(string xenotypeName)
        {
            var genes = new List<GeneDef>();

            if (!ModsConfig.BiotechActive) return genes;

            var xenotypeDef = DefDatabase<XenotypeDef>.AllDefs.FirstOrDefault(x =>
                x.defName.Equals(xenotypeName, StringComparison.OrdinalIgnoreCase));

            if (xenotypeDef?.genes != null) // CORRECTED FIELD NAME
            {
                genes.AddRange(xenotypeDef.genes);
            }

            return genes;
        }

        public static string GetXenotypeGeneSummary(string xenotypeName)
        {
            var genes = GetXenotypeGenes(xenotypeName);
            if (genes.Count == 0) return "No specific genes";

            var geneGroups = genes.GroupBy(g => g.displayCategory?.defName ?? "Unknown")
                                 .OrderBy(g => g.Key);

            var summary = new List<string>();
            foreach (var group in geneGroups)
            {
                summary.Add($"{group.Key}: {group.Count()} genes");
            }

            return string.Join(", ", summary);
        }

        // In GeneUtils.cs - add this for modder extensibility
        public static class XenotypePricingExtensions
        {
            /// <summary>
            /// Event that allows other mods to modify xenotype pricing calculations
            /// First parameter: XenotypeDef, Second parameter: current multiplier, Returns: new multiplier
            /// </summary>
            public static event Func<XenotypeDef, float, float> OnCalculateXenotypePrice;

            /// <summary>
            /// Public method that applies all scaling factors including mod extensions
            /// </summary>
            public static float CalculateFinalXenotypeMultiplier(XenotypeDef xenotypeDef, float baseMultiplier)
            {
                float multiplier = ApplyXenotypeScalingFactors(xenotypeDef, baseMultiplier);

                // Allow other mods to modify the price
                if (OnCalculateXenotypePrice != null)
                {
                    foreach (Func<XenotypeDef, float, float> handler in OnCalculateXenotypePrice.GetInvocationList())
                    {
                        try
                        {
                            multiplier = handler(xenotypeDef, multiplier);
                        }
                        catch (Exception ex)
                        {
                            CAP_ChatInteractive.Logger.Error($"Error in xenotype price handler: {ex}");
                        }
                    }
                }

                return Mathf.Clamp(multiplier, 0.1f, 10f);
            }
        }

        // New method to get detailed gene information for debugging
        public static List<string> GetXenotypeGeneDetails(string xenotypeName)
        {
            var details = new List<string>();
            var genes = GetXenotypeGenes(xenotypeName);

            foreach (var gene in genes.OrderBy(g => g.displayCategory?.defName ?? "Unknown"))
            {
                string geneInfo = $"{gene.defName}";
                if (gene.displayCategory != null)
                    geneInfo += $" [{gene.displayCategory.defName}]";
                geneInfo += $" - Cpx:{gene.biostatCpx} Met:{gene.biostatMet} Arc:{gene.biostatArc}";
                geneInfo += $" Value:{GetGeneMarketValue(gene):F0}";

                details.Add(geneInfo);
            }

            return details;
        }
    }
}
