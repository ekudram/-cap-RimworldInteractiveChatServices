// SetFavoriteColorCommandHandler.cs
// Copyright (c) Captolamia. All rights reserved.
// Licensed under the AGPLv3 License. See LICENSE file in the project root for full license information.

using CAP_ChatInteractive.Commands.CommandHandlers;
using CAP_ChatInteractive.Helpers;
using RimWorld;
using System;
using System.Collections.Generic;
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

            // Set the favorite color
            bool success = SetPawnFavoriteColor(viewerPawn, color.Value);

            if (success)
            {
                string colorName = GetColorName(color.Value);
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

            if (GeneratedColors.TryGetValue(colorHex, out ColorDef colorDef))
                return colorDef;

            // Create a new dynamic ColorDef
            colorDef = new ColorDef
            {
                defName = $"RICS_Color_{colorHex}",
                label = colorHex,
                color = color,
                colorType = ColorType.Misc,
                displayInStylingStationUI = false,
                randomlyPickable = false,
                displayOrder = -1
            };

            GeneratedColors[colorHex] = colorDef;
            return colorDef;
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