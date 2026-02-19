// CommandParserUtility.cs
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

using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace CAP_ChatInteractive.Utilities
{
    public class ParsedCommand
    {
        public string ItemName { get; set; } = "";
        public string Quality { get; set; } = "random";
        public string Material { get; set; } = "random";
        public string Side { get; set; } = null;
        public int Quantity { get; set; } = 1;
        public string Error { get; set; } = null;

        public bool HasError => Error != null;
    }

    public static class CommandParserUtility
    {
        private static HashSet<string> _materialKeywords = null;

        private static List<ThingDef> _stuffDefs = null;

        public static ParsedCommand ParseCommandArguments(string[] args, bool allowQuality = true, bool allowMaterial = true, bool allowSide = false, bool allowQuantity = true)
        {
            var result = new ParsedCommand();

            if (args.Length == 0)
            {
                result.Error = "Usage: ![buy, use, equip, wear, surgery] [item] [quality] [material] [side] [quantity]";
                return result;
            }

            var cleanedArgs = CleanArguments(args);
            var remainingArgs = new List<string>(cleanedArgs);

            // Quantity from end only (per your spec: always integer with spaces on both sides)
            if (allowQuantity && remainingArgs.Count > 0 && int.TryParse(remainingArgs[remainingArgs.Count - 1], out int quantity))
            {
                result.Quantity = quantity;
                remainingArgs.RemoveAt(remainingArgs.Count - 1);
            }

            // Flexible trailing qualifier peeling (any order, multi-word material, partial support)
            string parsedMaterial = null;
            string parsedQuality = "random";
            string parsedSide = null;
            bool changed;
            do
            {
                changed = false;

                // Side
                if (allowSide && parsedSide == null && remainingArgs.Count > 0 && IsSideKeyword(remainingArgs[remainingArgs.Count - 1]))
                {
                    parsedSide = remainingArgs[remainingArgs.Count - 1];
                    remainingArgs.RemoveAt(remainingArgs.Count - 1);
                    changed = true;
                }

                // Material (multi-word from end, partial support)
                if (allowMaterial && parsedMaterial == null && remainingArgs.Count > 0)
                {
                    for (int wordCount = 1; wordCount <= 3; wordCount++)  // CHANGED: 1 -> 2 -> 3 instead of 3->2->1
                    {
                        if (remainingArgs.Count >= wordCount)
                        {
                            var materialWords = remainingArgs.Skip(remainingArgs.Count - wordCount).Take(wordCount).ToArray();
                            string potential = string.Join(" ", materialWords);
                            string matched = TryFindMaterial(potential);
                            if (matched != null)
                            {
                                parsedMaterial = matched;
                                remainingArgs.RemoveRange(remainingArgs.Count - wordCount, wordCount);
                                changed = true;
                                break;
                            }
                        }
                    }
                }

                // Quality
                if (allowQuality && parsedQuality == "random" && remainingArgs.Count > 0 && IsQualityKeyword(remainingArgs[remainingArgs.Count - 1]))
                {
                    parsedQuality = remainingArgs[remainingArgs.Count - 1];  // preserve user casing like original
                    remainingArgs.RemoveAt(remainingArgs.Count - 1);
                    changed = true;
                }
            } while (changed && remainingArgs.Count > 0);

            // Final assignment + special case (material-as-item)
            result.Quality = parsedQuality;
            result.Side = parsedSide;

            if (remainingArgs.Count == 0)
            {
                if (parsedMaterial != null)
                {
                    result.ItemName = parsedMaterial;
                    result.Material = "random";
                }
                else
                {
                    result.Error = "No item name specified.";
                    return result;
                }
            }
            else
            {
                result.ItemName = string.Join(" ", remainingArgs).Trim();
                result.Material = parsedMaterial ?? "random";
            }

            if (string.IsNullOrWhiteSpace(result.ItemName))
            {
                result.Error = "Invalid item name after parsing arguments.";
                return result;
            }

            Logger.Debug($"Parsed - Item: '{result.ItemName}', Quality: '{result.Quality}', Material: '{result.Material}', Side: '{result.Side}', Quantity: {result.Quantity}");
            return result;
        }

        private static string[] CleanArguments(string[] args)
        {
            var cleaned = new List<string>();

            foreach (string arg in args)
            {
                // Remove/replace problematic characters with spaces
                string cleanArg = arg.Replace("[", " ")
                                   .Replace("]", " ")
                                   //  .Replace("(", " ")
                                   //  .Replace(")", " ")
                                   .Replace(",", " ")
                                   //  .Replace(".", " ")
                                   .Replace(";", " ")
                                   .Trim();

                // Split if cleaning created multiple words (e.g., "axe[awful" -> "axe awful")
                var words = cleanArg.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                cleaned.AddRange(words);
            }

            return cleaned.ToArray();
        }

        private static bool IsQualityKeyword(string arg)
        {
            return arg.ToLower() switch
            {
                "awful" or "poor" or "normal" or "good" or "excellent" or "masterwork" or "legendary" => true,
                _ => false
            };
        }

        public static bool IsMaterialKeyword(string arg)
        {
            InitializeMaterialKeywords();
            return _materialKeywords.Contains(arg);
        }

        private static bool IsSideKeyword(string arg)
        {
            return arg.ToLower() switch
            {
                "left" or "right" or "l" or "r" => true,
                _ => false
            };
        }

        private static void InitializeMaterialKeywords()
        {
            if (_materialKeywords != null) return;

            _materialKeywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            try
            {
                var allStuffDefs = DefDatabase<ThingDef>.AllDefs.Where(def => def.IsStuff);
                foreach (var stuffDef in allStuffDefs)
                {
                    // Add def name (with underscores as spaces)
                    string defNameWithSpaces = stuffDef.defName.Replace("_", " ");
                    _materialKeywords.Add(defNameWithSpaces);

                    // Add def name (original with underscores)
                    _materialKeywords.Add(stuffDef.defName);

                    // Add label (this is the display name with spaces)
                    if (!string.IsNullOrEmpty(stuffDef.label))
                    {
                        _materialKeywords.Add(stuffDef.label);

                        // Also add label without spaces for backward compatibility
                        _materialKeywords.Add(stuffDef.label.Replace(" ", ""));
                    }
                }

                Logger.Debug($"Initialized material keywords with {_materialKeywords.Count} entries");

                // Log some examples for debugging
                var blockMaterials = _materialKeywords.Where(k => k.Contains("block")).Take(5).ToList();
                Logger.Debug($"Example block materials: {string.Join(", ", blockMaterials)}");
            }
            catch (Exception ex)
            {
                Logger.Error($"Error initializing material keywords: {ex}");
                // Fallback to common materials including multi-word ones
                _materialKeywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "wood", "steel", "plasteel", "cloth", "leather", "synthread", "hyperweave",
            "gold", "silver", "uranium", "jade", "component", "components",
            "marble blocks", "granite blocks", "limestone blocks", "sandstone blocks",
            "slate blocks", "steel blocks", "plasteel blocks", "wood logs"
        };
            }
        }

        // --- GROK's Helpers ---

        private static void InitializeStuffDefs()
        {
            if (_stuffDefs != null) return;
            _stuffDefs = DefDatabase<ThingDef>.AllDefs
                .Where(def => def.IsStuff)
                .OrderBy(def => def.label ?? def.defName)
                .ToList();
            Logger.Debug($"Initialized {_stuffDefs.Count} stuff definitions for material parsing.");
        }

        private static bool IsExactMaterialMatch(ThingDef stuff, string lowerPot)
        {
            if (string.IsNullOrEmpty(stuff.label)) return false;
            string lowerLabel = stuff.label.ToLowerInvariant();
            string lowerDef = stuff.defName.ToLowerInvariant();
            string lowerDefSpaces = stuff.defName.Replace("_", " ").ToLowerInvariant();
            string lowerLabelNoSpace = lowerLabel.Replace(" ", "");
            return lowerLabel == lowerPot ||
                   lowerDef == lowerPot ||
                   lowerDefSpaces == lowerPot ||
                   lowerLabelNoSpace == lowerPot;
        }

        private static int GetPartialMatchScore(ThingDef stuff, string lowerPot)
        {
            if (string.IsNullOrEmpty(stuff.label)) return 0;
            string lowerLabel = stuff.label.ToLowerInvariant();
            string lowerDefSpaces = stuff.defName.Replace("_", " ").ToLowerInvariant();

            // Require the material to be a trailing substring (common user pattern: item ... material)
            // This avoids consuming the whole phrase when item contains material word
            bool isTrailing =
                lowerPot.EndsWith(lowerLabel) ||
                lowerPot.EndsWith(lowerDefSpaces) ||
                lowerPot.EndsWith(lowerLabel.Replace(" ", "")); // rare

            if (!isTrailing) return 0;

            // Bonus: closer length = better
            int lenDiff = Math.Abs(lowerLabel.Length - lowerPot.Length);
            return 100 - lenDiff * 2; // penalize length diff more
        }

        public static string TryFindMaterial(string potential)
        {
            if (string.IsNullOrWhiteSpace(potential)) return null;
            InitializeStuffDefs();
            string lowerPot = potential.ToLowerInvariant().Trim();

            // Exact first (preserves 100% old behavior)
            foreach (var stuff in _stuffDefs)
            {
                if (IsExactMaterialMatch(stuff, lowerPot))
                {
                    return stuff.label ?? stuff.defName.Replace("_", " ");
                }
            }

            // Partial with tie-breaker (what-if-multiple resolved)
            ThingDef bestMatch = null;
            int bestScore = 0;
            foreach (var stuff in _stuffDefs)
            {
                int score = GetPartialMatchScore(stuff, lowerPot);
                if (score > bestScore ||
                    (score == bestScore && bestMatch != null &&
                     (stuff.label?.Length ?? 999) < (bestMatch.label?.Length ?? 999)))
                {
                    bestScore = score;
                    bestMatch = stuff;
                }
            }
            if (bestScore > 0 && bestMatch != null)
            {
                Logger.Debug($"Partial material match: '{potential}' → '{bestMatch.label}'");
                return bestMatch.label ?? bestMatch.defName.Replace("_", " ");
            }
            return null;
        }
    }
}