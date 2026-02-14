// DyeCommandHandler.cs
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

using CAP_ChatInteractive.Commands.CommandHandlers;
using CAP_ChatInteractive.Helpers;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace CAP_ChatInteractive.Commands.ViewerCommands
{
    internal static class DyeCommandHandler
    {
        private static Dictionary<string, Color> _rimColorCache;

        private static Dictionary<string, Color> GetAllRimColorDefs()
        {
            if (_rimColorCache != null) return _rimColorCache;

            _rimColorCache = new Dictionary<string, Color>(StringComparer.OrdinalIgnoreCase);

            // Get all ColorDefs from RimWorld
            var allColorDefs = DefDatabase<ColorDef>.AllDefs;

            foreach (var colorDef in allColorDefs)
            {
                if (colorDef.colorType == ColorType.Hair || colorDef.defName.Contains("Hair"))
                {
                    // Convert RGB values (0-255) to Unity Color (0-1)
                    Color color = new Color(
                        colorDef.color.r / 255f,
                        colorDef.color.g / 255f,
                        colorDef.color.b / 255f
                    );

                    // Add by defName
                    if (!_rimColorCache.ContainsKey(colorDef.defName))
                        _rimColorCache.Add(colorDef.defName, color);

                    // Add by label if it exists and is different
                    if (!string.IsNullOrEmpty(colorDef.label) && !_rimColorCache.ContainsKey(colorDef.label))
                        _rimColorCache.Add(colorDef.label, color);
                }
            }

            return _rimColorCache;
        }

        internal static string HandleDyeCommand(ChatMessageWrapper messageWrapper, string[] args)
        {
            // Get the viewer's pawn
            Verse.Pawn viewerPawn = PawnItemHelper.GetViewerPawn(messageWrapper);
            if (viewerPawn == null)
            {
                return "You need to have a pawn in the colony to dye their clothing or hair. Use !buy pawn first.";
            }

            // Check for subcommand
            bool isHairDye = args.Length > 0 && args[0].ToLower() == "hair";

            // Parse color from arguments
            Color? color = null;
            string colorInput = null;
            int colorArgIndex = isHairDye ? 1 : 0;

            if (args.Length > colorArgIndex && !string.IsNullOrEmpty(args[colorArgIndex]))
            {
                colorInput = args[colorArgIndex];

                // First check RimWorld hair colors
                var rimColors = GetAllRimColorDefs();
                if (rimColors.TryGetValue(colorInput, out Color rimColor))
                {
                    color = rimColor;
                }
                else
                {
                    // Fall back to ColorHelper
                    color = ColorHelper.ParseColor(colorInput);
                }

                if (!color.HasValue)
                {
                    return $"'{args[colorArgIndex]}' is not a valid color. Use color names (like 'PitchBlack' or 'Blonde'), hex codes like #FF0000, or common color names.";
                }
            }

            // Use favorite color if no color specified
            if (!color.HasValue)
            {
                if (!ModsConfig.IdeologyActive)
                {
                    return "Please specify a color. The favorite color system requires the Ideology DLC.";
                }

                color = viewerPawn.story?.favoriteColor?.color ?? new Color(0.6f, 0.6f, 0.6f);
                colorInput = "favorite color";
            }

            if (isHairDye)
            {
                return HandleHairDye(viewerPawn, color.Value, colorInput);
            }
            else
            {
                return HandleApparelDye(viewerPawn, color.Value, colorInput);
            }
        }

        private static string HandleHairDye(Verse.Pawn pawn, Color color, string colorInput = null)
        {
            if (pawn.story == null || pawn.story.hairDef == null)
            {
                return "Your pawn doesn't have hair to dye.";
            }

            // Check if the pawn has a hair color tracker (for mod compatibility)
            //var hairComp = pawn.GetComp<CompHairColor>();
            //if (hairComp != null)
            //{
            //    hairComp.SetColor(color);
            //    string colorName = GetColorNameForResponse(color, colorInput);
            //    return $"Successfully dyed hair to {colorName}.";
            //}

            // For vanilla, we can try to set the hair color through story
            if (pawn.story != null)
            {
                // Note: This uses reflection or a Harmony patch in practice
                // as vanilla doesn't have a direct SetHairColor method
                pawn.story.HairColor = color;
                string colorName = GetColorNameForResponse(color, colorInput);
                return $"Successfully dyed hair to {colorName}.";
            }

            return "Could not dye hair. Your pawn might not support hair dyeing.";
        }

        private static string HandleApparelDye(Verse.Pawn pawn, Color color, string colorInput = null)
        {
            // Apply dye to appropriate apparel
            int dyedCount = ApplyDyeToApparel(pawn, color);

            if (dyedCount == 0)
            {
                return "No dyeable clothing found on your pawn.";
            }

            string colorName = GetColorNameForResponse(color, colorInput);
            return $"Successfully dyed {dyedCount} piece(s) of clothing to {colorName}.";
        }

        private static int ApplyDyeToApparel(Verse.Pawn pawn, Color color)
        {
            int count = 0;
            var apparel = pawn.apparel?.WornApparel;

            if (apparel == null) return 0;

            foreach (var item in apparel)
            {
                if (IsDyeableApparel(item))
                {
                    var comp = item.TryGetComp<CompColorable>();
                    if (comp != null)
                    {
                        comp.SetColor(color);
                        count++;
                    }
                }
            }

            return count;
        }

        private static string GetColorNameForResponse(Color color, string colorInput = null)
        {
            // If we have the original input and it's not just a hex code, use it
            if (!string.IsNullOrEmpty(colorInput) && !colorInput.StartsWith("#"))
            {
                return colorInput;
            }

            // Try to find in RimWorld color defs first
            var rimColors = GetAllRimColorDefs();
            foreach (var kvp in rimColors)
            {
                if (kvp.Value.r == color.r && kvp.Value.g == color.g && kvp.Value.b == color.b)
                {
                    return kvp.Key;
                }
            }

            // Then try ColorHelper dictionary
            foreach (var kvp in ColorHelper.GetColorDictionary())
            {
                if (kvp.Value.r == color.r && kvp.Value.g == color.g && kvp.Value.b == color.b)
                {
                    return kvp.Key;
                }
            }

            // Otherwise approximate
            return ApproximateColorName(color);
        }

        private static string ApproximateColorName(Color color)
        {
            if (color.r > 0.8f && color.g < 0.3f && color.b < 0.3f) return "red";
            if (color.r > 0.8f && color.g > 0.8f && color.b < 0.3f) return "yellow";
            if (color.r < 0.3f && color.g > 0.8f && color.b < 0.3f) return "green";
            if (color.r < 0.3f && color.g < 0.3f && color.b > 0.8f) return "blue";
            if (color.r > 0.8f && color.g < 0.3f && color.b > 0.8f) return "purple";
            if (color.r > 0.8f && color.g > 0.5f && color.b < 0.3f) return "orange";
            if (color.r > 0.9f && color.g > 0.9f && color.b > 0.9f) return "white";
            if (color.r < 0.2f && color.g < 0.2f && color.b < 0.2f) return "black";
            if (Mathf.Abs(color.r - color.g) < 0.1f && Mathf.Abs(color.g - color.b) < 0.1f) return "gray";
            return "custom";
        }

        private static bool IsDyeableApparel(Apparel apparel)
        {
            // Exclude jewelry, accessories, and utility items
            if (apparel.def == null) return false;

            // Check for jewelry by defName or label
            string defName = apparel.def.defName?.ToLower() ?? "";
            string label = apparel.def.label?.ToLower() ?? "";

            // Exclude common jewelry and accessory types
            if (defName.Contains("jewelry") || label.Contains("jewelry") ||
                defName.Contains("earring") || label.Contains("earring") ||
                defName.Contains("necklace") || label.Contains("necklace") ||
                defName.Contains("ring") || label.Contains("ring") ||
                defName.Contains("bracelet") || label.Contains("bracelet") ||
                defName.Contains("crown") || label.Contains("crown") ||
                defName.Contains("tiara") || label.Contains("tiara"))
            {
                return false;
            }

            // Check apparel tags for exclusion
            if (apparel.def.apparel?.tags != null)
            {
                var tags = apparel.def.apparel.tags;
                if (tags.Contains("Jewelry") || tags.Contains("Accessory") || tags.Contains("Utility"))
                {
                    return false;
                }
            }

            // Check for utility slot items
            if (IsUtilitySlotItem(apparel))
            {
                return false;
            }

            return true;
        }

        private static bool IsUtilitySlotItem(Apparel apparel)
        {
            // Check if this apparel goes in utility slots
            var layers = apparel.def.apparel?.layers;
            if (layers == null) return false;

            // Utility items are typically in the Belt layer
            if (layers.Contains(ApparelLayerDefOf.Belt))
            {
                return true;
            }

            // Check apparel tags for utility items
            if (apparel.def.apparel?.tags != null)
            {
                var tags = apparel.def.apparel.tags;
                if (tags.Contains("Utility") || tags.Contains("Belt") || tags.Contains("Holster"))
                {
                    return true;
                }
            }

            // Additional check for utility-related defNames
            string defName = apparel.def.defName?.ToLower() ?? "";
            return defName.Contains("utility") || defName.Contains("belt") || defName.Contains("holster") ||
                   defName.Contains("tool") || defName.Contains("pouch");
        }
    }
}