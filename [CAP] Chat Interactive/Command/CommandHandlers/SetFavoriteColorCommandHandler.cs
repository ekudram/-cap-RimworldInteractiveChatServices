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
    internal static class SetFavoriteColorCommandHandler
    {
        private static readonly Dictionary<string, ColorDef> GeneratedColors = new Dictionary<string, ColorDef>();

        internal static string HandleSetFavoriteColorCommand(ChatMessageWrapper messageWrapper, string[] args)
        {
            // Get the viewer's pawn
            Verse.Pawn viewerPawn = StoreCommandHelper.GetViewerPawn(messageWrapper);
            if (viewerPawn == null)
            {
                return "You need to have a pawn in the colony to set a favorite color. Use !buy pawn first.";
            }

            // Check if pawn has a story (should always have one, but safety check)
            if (viewerPawn.story == null)
            {
                return "Your pawn doesn't have a background story. This shouldn't happen!";
            }

            // Parse color from arguments
            if (args.Length == 0 || string.IsNullOrEmpty(args[0]))
            {
                return "Please specify a color. Usage: !setfavoritecolor <color> (e.g., !setfavoritecolor blue or !setfavoritecolor #FF0000)";
            }

            Color? color = ColorHelper.ParseColor(args[0]);
            if (!color.HasValue)
            {
                return $"'{args[0]}' is not a valid color. Use color names or hex codes like #FF0000.";
            }

            // Debug: Log what color we're trying to set
            Log.Message($"Setting favorite color: {args[0]} -> RGBA({color.Value.r}, {color.Value.g}, {color.Value.b}, {color.Value.a})");

            // Set the favorite color
            bool success = SetPawnFavoriteColor(viewerPawn, color.Value);

            if (success)
            {
                string colorName = GetColorName(color.Value);
                // Also log what ColorDef was actually set
                if (viewerPawn.story.favoriteColor != null)
                {
                    Log.Message($"Successfully set favorite color to: {viewerPawn.story.favoriteColor.defName} - RGBA({viewerPawn.story.favoriteColor.color.r}, {viewerPawn.story.favoriteColor.color.g}, {viewerPawn.story.favoriteColor.color.b}, {viewerPawn.story.favoriteColor.color.a})");
                }
                return $"Your pawn's favorite color has been set to {colorName}!";
            }
            else
            {
                return "Failed to set favorite color.";
            }
        }

        private static bool SetPawnFavoriteColor(Verse.Pawn pawn, Color color)
        {
            try
            {
                // Create or get a ColorDef for this color
                ColorDef colorDef = GetColorDef(color);
                pawn.story.favoriteColor = colorDef;
                return true;
            }
            catch (Exception ex)
            {
                Log.Error($"Failed to set favorite color for pawn {pawn.Name}: {ex.Message}");
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

        private static bool ColorsAreSimilar(Color a, Color b, float tolerance = 0.1f)
        {
            return Math.Abs(a.r - b.r) < tolerance &&
                   Math.Abs(a.g - b.g) < tolerance &&
                   Math.Abs(a.b - b.b) < tolerance;
        }
    }
}