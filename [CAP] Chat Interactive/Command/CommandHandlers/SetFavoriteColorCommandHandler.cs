// SetFavoriteColorCommandHandler.cs
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

using _CAP__Chat_Interactive.Command.CommandHelpers;
using CAP_ChatInteractive.Helpers;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace CAP_ChatInteractive.Commands.ViewerCommands
{
    internal static class SetFavoriteColorCommandHandler
    {
        private static readonly Dictionary<string, ColorDef> GeneratedColors = new Dictionary<string, ColorDef>();

        internal static string HandleSetFavoriteColorCommand(ChatMessageWrapper messageWrapper, string[] args)
        {
            Verse.Pawn viewerPawn = PawnItemHelper.GetViewerPawn(messageWrapper);
            if (viewerPawn == null)
            {
                return "RICS.SFCCH.NoPawn".Translate();
            }

            if (viewerPawn.story == null)
            {
                return "RICS.SFCCH.NoStory".Translate();
            }

            // Show current color + usage when no arguments
            if (args.Length == 0 || string.IsNullOrEmpty(args[0]))
            {
                var currentColorDef = viewerPawn.story.favoriteColor;
                string current = currentColorDef != null ? currentColorDef.defName : "none";
                return "RICS.SFCCH.UsageNoArgs".Translate(current);
            }

            // Combine args for multi-word color names (up to 3 words)
            string colorInput = args[0];
            if (args.Length > 1)
            {
                int wordsToTake = Mathf.Min(args.Length, 3);
                colorInput = string.Join(" ", args.Take(wordsToTake));
            }

            // 1. Try exact ColorDef by name
            ColorDef colorDefFromName = FindColorDefByName(colorInput);
            if (colorDefFromName != null)
            {
                bool success = SetPawnFavoriteColor(viewerPawn, colorDefFromName.color);
                if (success)
                {
                    return "RICS.SFCCH.SuccessNamed".Translate(colorDefFromName.label);
                }
            }

            // 2. Try parsing as hex / rgb etc.
            Color? parsedColor = ColorHelper.ParseColor(colorInput);
            if (!parsedColor.HasValue)
            {
                // Try closest match as fallback
                colorDefFromName = FindClosestColorDef(colorInput);
                if (colorDefFromName != null)
                {
                    bool success = SetPawnFavoriteColor(viewerPawn, colorDefFromName.color);
                    if (success)
                    {
                        return "RICS.SFCCH.SuccessHSV".Translate(
                            colorDefFromName.label,
                            colorDefFromName.color.ToString()
                        );
                    }
                }

                return "RICS.SFCCH.InvalidColor".Translate(colorInput);
            }

            // 3. Set parsed color
            bool setSuccess = SetPawnFavoriteColor(viewerPawn, parsedColor.Value);

            if (setSuccess)
            {
                string colorName = GetColorName(parsedColor.Value);
                return "RICS.SFCCH.SuccessGeneric".Translate(colorName);
            }

            return "RICS.SFCCH.FailedToSet".Translate();
        }

        private static bool SetPawnFavoriteColor(Verse.Pawn pawn, Color color)
        {
            try
            {
                ColorDef colorDef = GetColorDef(color);
                pawn.story.favoriteColor = colorDef;
                return true;
            }
            catch (Exception ex)
            {
                Log.Error("RICS.SFCCH.ErrorLogPrefix".Translate(pawn.Name.ToString(), ex.Message));
                return false;
            }
        }

        private static bool SetPawnFavoriteColor(Verse.Pawn pawn, ColorDef colorDef)
        {
            try
            {
                pawn.story.favoriteColor = colorDef;
                return true;
            }
            catch (Exception ex)
            {
                Log.Error("RICS.SFCCH.ErrorLogPrefix".Translate(pawn.Name.ToString(), ex.Message));
                return false;
            }
        }

        private static ColorDef GetColorDef(Color color)
        {
            string colorHex = ColorUtility.ToHtmlStringRGBA(color);

            // Check cache first
            if (GeneratedColors.TryGetValue(colorHex, out ColorDef colorDef))
                return colorDef;

            // Search through ALL ColorDefs, not just Misc and Hair
            colorDef = DefDatabase<ColorDef>.AllDefs
                .OrderBy(def => ColorDistance(def.color, color))
                .FirstOrDefault();

            if (colorDef != null)
            {
                GeneratedColors[colorHex] = colorDef;
                return colorDef;
            }

            // Better fallback - find the closest color from all available ColorDefs
            // This should never happen since DefDatabase should always have colors, but just in case
            Log.Warning($"No ColorDef found for color {colorHex}, using closest available color");

            // Try to find any color def that exists
            var fallback = DefDatabase<ColorDef>.AllDefs.FirstOrDefault();
            if (fallback != null)
            {
                GeneratedColors[colorHex] = fallback;
                return fallback;
            }

            // Absolute last resort
            return DefDatabase<ColorDef>.GetNamedSilentFail("White") ?? new ColorDef() { color = Color.white };
        }

        private static float ColorDistance(Color a, Color b)
        {
            // Use Euclidean distance for better color matching
            float rDiff = a.r - b.r;
            float gDiff = a.g - b.g;
            float bDiff = a.b - b.b;
            return Mathf.Sqrt(rDiff * rDiff + gDiff * gDiff + bDiff * bDiff);
        }



        private static string GetColorName(Color color)
        {
            // Try to find a close match in our color dictionary
            foreach (var kvp in ColorHelper.GetColorDictionary())
            {
                if (ColorsAreSimilar(kvp.Value, color))
                {
                    return kvp.Key;
                }
            }

            // If no close match found, return hex code
            return "#" + ColorUtility.ToHtmlStringRGB(color);
        }

        private static ColorDef FindColorDefByName(string colorName)
        {
            // Clean up the color name for comparison
            string cleanName = colorName.ToLower().Replace(" ", "");

            // Search through ALL ColorDefs for exact or close name match
            foreach (ColorDef def in DefDatabase<ColorDef>.AllDefs)
            {
                // Check exact match (case insensitive, no spaces)
                if (def.defName.ToLower().Replace("_", "").Replace(" ", "") == cleanName)
                    return def;

                // Check if defName contains our color name
                if (def.defName.ToLower().Contains(cleanName) && cleanName.Length > 2)
                    return def;

                // Also check label if available
                if (!def.label.NullOrEmpty() && def.label.ToLower().Replace(" ", "").Contains(cleanName) && cleanName.Length > 2)
                    return def;
            }

            return null;
        }

        private static ColorDef FindClosestColorDef(string colorInput)
        {
            // Try to parse as color first
            Color? parsedColor = ColorHelper.ParseColor(colorInput);
            if (parsedColor.HasValue)
            {
                // Find the closest ColorDef by color value
                return DefDatabase<ColorDef>.AllDefs
                    .OrderBy(def => ColorDistance(def.color, parsedColor.Value))
                    .FirstOrDefault();
            }

            return null;
        }

        private static bool ColorsAreSimilar(Color a, Color b, float tolerance = 0.1f)
        {
            return Math.Abs(a.r - b.r) < tolerance &&
                   Math.Abs(a.g - b.g) < tolerance &&
                   Math.Abs(a.b - b.b) < tolerance;
        }
    }
}