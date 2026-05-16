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
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace _CAP__Chat_Interactive.Utilities
{
    // Simplified GeneUtils.cs - uses Rimworld's built-in market value calculations
    /// <summary>
    /// Utility class for calculating gene and xenotype market values.
    /// </summary>
    public static class GeneUtils
    {
        /// <summary>
        /// Calculates the total market value of a xenotype, including the base value of the race.
        /// </summary>
        /// <param name="race">The race definition.</param>
        /// <param name="xenotypeName">The name of the xenotype.</param>
        /// <returns>The total market value of the xenotype.</returns>
        public static float CalculateXenotypeMarketValue(ThingDef race, string xenotypeName)
        {
            if (!ModsConfig.BiotechActive)
                return Mathf.Max(race.BaseMarketValue, 1f);

            var xenotypeDef = DefDatabase<XenotypeDef>.AllDefs.FirstOrDefault(x =>
                x.defName.Equals(xenotypeName, StringComparison.OrdinalIgnoreCase));

            if (xenotypeDef == null)
                return Mathf.Max(race.BaseMarketValue, 1f);

            // Start with the race's base market value (RimWorld's canonical baseline)
            float totalValue = race.BaseMarketValue;

            // Add (or subtract) value from each gene
            if (xenotypeDef.genes != null)
            {
                foreach (var geneDef in xenotypeDef.genes)
                {
                    totalValue += GetGeneMarketValue(geneDef, race.BaseMarketValue);
                }
            }

            // NEW: Allow bad genes to make the xenotype cheaper than base price
            // Hard floor of 1 silver (never negative or zero)
            return Mathf.Max(totalValue, 1f);
        }

        /// <summary>
        /// Calculate the market value contribution of a single gene.
        /// </summary>
        /// <param name="geneDef">The gene definition.</param>
        /// <param name="baseRaceValue">The base market value of the race.</param>
        /// <returns>The market value contribution of the gene.</returns>
        public static float GetGeneMarketValue(GeneDef geneDef, float baseRaceValue)
        {
            // RimWorld's marketValueFactor is a multiplier on the pawn's base value.
            // Example: marketValueFactor = 1.1  → +10% value
            //          marketValueFactor = 0.8  → -20% value (bad genes = cheaper xenotype)
            // So the gene's contribution is (factor - 1) * base value.
            float geneValue = (geneDef.marketValueFactor - 1.0f) * baseRaceValue;

            return geneValue;
        }


        /// <summary>
        /// Calculates the total market value contribution of all genes in a xenotype.
        /// </summary>
        /// <param name="xenotypeName">The name of the xenotype.</param>
        /// <param name="baseRaceValue">The base market value of the race.</param>
        /// <returns>The total market value contribution of all genes in the xenotype.</returns>
        public static float GetXenotypeGeneValueOnly(string xenotypeName, float baseRaceValue)
        {
            if (!ModsConfig.BiotechActive) return 0f;

            var xenotypeDef = DefDatabase<XenotypeDef>.AllDefs.FirstOrDefault(x =>
                x.defName.Equals(xenotypeName, StringComparison.OrdinalIgnoreCase));

            if (xenotypeDef == null || xenotypeDef.genes == null) return 0f;

            float totalGeneValue = 0f;
            foreach (var geneDef in xenotypeDef.genes)
            {
                totalGeneValue += GetGeneMarketValue(geneDef, baseRaceValue);
            }

            return totalGeneValue;
        }

        /// <summary>
        /// Retrieves the list of genes for a given xenotype.
        /// </summary>
        /// <param name="xenotypeName">The name of the xenotype.</param>
        /// <returns>A list of gene definitions for the xenotype.</returns>
        public static List<GeneDef> GetXenotypeGenes(string xenotypeName)
        {
            var genes = new List<GeneDef>();

            if (!ModsConfig.BiotechActive) return genes;

            var xenotypeDef = DefDatabase<XenotypeDef>.AllDefs.FirstOrDefault(x =>
                x.defName.Equals(xenotypeName, StringComparison.OrdinalIgnoreCase));

            if (xenotypeDef?.genes != null)
            {
                genes.AddRange(xenotypeDef.genes);
            }

            return genes;
        }

        /// <summary>
        /// Generates a summary of the genes for a given xenotype.
        /// </summary>
        /// <param name="xenotypeName">The name of the xenotype.</param>
        /// <returns>A summary string of the genes in the xenotype.</returns>
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

        /// <summary>
        /// Retrieves detailed gene information for a given xenotype, including market values.
        /// </summary>
        /// <param name="race">The race definition.</param>
        /// <param name="xenotypeName">The name of the xenotype.</param>
        /// <returns>A list of strings containing detailed gene information.</returns>
        public static List<string> GetXenotypeGeneDetails(ThingDef race, string xenotypeName)
        {
            var details = new List<string>();
            var genes = GetXenotypeGenes(xenotypeName);

            float baseRaceValue = race.BaseMarketValue;
            float totalGeneValue = 0f;

            foreach (var gene in genes.OrderBy(g => g.displayCategory?.defName ?? "Unknown"))
            {
                float geneValue = GetGeneMarketValue(gene, baseRaceValue);
                string geneInfo = $"{gene.defName}";
                if (gene.displayCategory != null)
                    geneInfo += $" [{gene.displayCategory.defName}]";
                geneInfo += $" MarketFactor:{gene.marketValueFactor:F2}";
                geneInfo += $" Value:{geneValue:F0}";

                totalGeneValue += geneValue;
                details.Add(geneInfo);
            }

            // Add summary line
            details.Add($"Total Gene Value: {totalGeneValue:F0}");
            details.Add($"Race Base Value: {baseRaceValue:F0}");
            details.Add($"Total Xenotype Value: {baseRaceValue + totalGeneValue:F0}");

            return details;
        }
    }
}