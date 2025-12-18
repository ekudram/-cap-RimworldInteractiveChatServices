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

        public static Color? ParseColor(string colorInput)
        {
            if (string.IsNullOrEmpty(colorInput))
                return null;

            // Remove # if present for hex parsing
            string cleanInput = colorInput.Trim().TrimStart('#');

            // Check named colors first
            if (ColorDictionary.TryGetValue(cleanInput, out Color namedColor))
            {
                return namedColor;
            }

            // Try parsing as hex color
            if (ColorUtility.TryParseHtmlString("#" + cleanInput, out Color hexColor))
            {
                return hexColor;
            }

            return null;
        }

        public static Dictionary<string, Color> GetColorDictionary()
        {
            return ColorDictionary;
        }
    }


}