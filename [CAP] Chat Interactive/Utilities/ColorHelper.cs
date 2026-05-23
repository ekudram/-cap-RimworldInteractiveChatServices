// ColorHelper.cs
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
using UnityEngine;

namespace CAP_ChatInteractive.Helpers
{
    internal static class ColorHelper
    {
        /// <summary>
        /// Dictionary of common color names to their corresponding Unity Color values.
        /// </summary>
        private static readonly Dictionary<string, Color> ColorDictionary = new Dictionary<string, Color>(StringComparer.OrdinalIgnoreCase)
        {
            {"red", Color.red},
            {"green", Color.green},
            {"blue", Color.blue},
            {"yellow", Color.yellow},
            {"orange", new Color(1f, 0.5f, 0f)},
            {"purple", new Color(0.5f, 0f, 0.5f)},
            {"pink", new Color(1f, 0.75f, 0.8f)},
            {"brown", new Color(0.65f, 0.16f, 0.16f)},
            {"black", Color.black},
            {"white", Color.white},
            {"gray", Color.gray},
            {"cyan", Color.cyan},
            {"magenta", Color.magenta},
            {"silver", new Color(0.75f, 0.75f, 0.75f)},
            {"gold", new Color(1f, 0.84f, 0f)},
            {"maroon", new Color(0.5f, 0f, 0f)},
            {"navy", new Color(0f, 0f, 0.5f)},
            {"teal", new Color(0f, 0.5f, 0.5f)},
            {"lime", new Color(0f, 1f, 0f)},
            {"olive", new Color(0.5f, 0.5f, 0f)},
            {"darkred", new Color(0.5f, 0f, 0f)},
            {"darkgreen", new Color(0f, 0.5f, 0f)},
            {"darkblue", new Color(0f, 0f, 0.5f)},
            {"lightblue", new Color(0.68f, 0.85f, 0.9f)},
            {"skyblue", new Color(0.53f, 0.81f, 0.92f)},
            {"forestgreen", new Color(0.13f, 0.55f, 0.13f)},
            {"royalblue", new Color(0.25f, 0.41f, 0.88f)},
            {"hotpink", new Color(1f, 0.41f, 0.71f)},
            {"darkgray", new Color(0.66f, 0.66f, 0.66f)},
            {"lightgray", new Color(0.83f, 0.83f, 0.83f)},
        };

        /// <summary>
        /// Parses a color from a string input. Supports named colors, hex codes, and rgb() format.
        /// </summary>
        /// <param name="colorInput">The input string representing the color.</param>
        /// <returns>A Unity Color if parsing is successful; otherwise, null.</returns>
        public static Color? ParseColor(string colorInput)
        {
            if (string.IsNullOrEmpty(colorInput))
                return null;

            string cleanInput = colorInput.Trim().TrimStart('#');

            // Named colors first
            if (ColorDictionary.TryGetValue(cleanInput, out Color namedColor))
            {
                return namedColor;
            }

            // Hex
            if (ColorUtility.TryParseHtmlString("#" + cleanInput, out Color hexColor))
            {
                return hexColor;
            }

            // NEW: Support "rgb(255,0,0)" style if users paste from tools
            if (cleanInput.StartsWith("rgb"))
            {
                // Simple parser for rgb/rgba - can be expanded if needed
                var match = System.Text.RegularExpressions.Regex.Match(cleanInput, @"rgb\((\d+),\s*(\d+),\s*(\d+)\)");
                if (match.Success &&
                    int.TryParse(match.Groups[1].Value, out int r) &&
                    int.TryParse(match.Groups[2].Value, out int g) &&
                    int.TryParse(match.Groups[3].Value, out int b))
                {
                    return new Color(r / 255f, g / 255f, b / 255f);
                }
            }

            return null;
        }

        /// <summary>
        /// Returns the dictionary of named colors. This can be used for auto-completion or reference in other parts of the code.
        /// </summary>
        /// <returns></returns>
        public static Dictionary<string, Color> GetColorDictionary()
        {
            return ColorDictionary;
        }
    }


}