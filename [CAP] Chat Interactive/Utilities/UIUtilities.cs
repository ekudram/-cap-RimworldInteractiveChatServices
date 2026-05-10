// UIUtilities.cs
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
// General UI utility methods for the CAP Chat Interactive mod

using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace CAP_ChatInteractive
{
    /// <summary>
    /// Static utility class for common UI-related formatting and helpers,
    /// especially for building rich-text tooltips, labels, truncation, and input handling.
    /// </summary>
    public static class UIUtilities
    {
        /// <summary>
        /// Recommended truncation method (uses efficient binary search)
        /// 1 reference (primary public API)
        /// </summary>
        public static string Truncate(string text, float maxWidth, string ellipsis = "..")
        {
            return TruncateTextToWidthEfficient(text, maxWidth, ellipsis);
        }

        /// <summary>
        /// Truncates text to fit within a specified width, adding ".." if necessary
        /// 6 references - Used in editors
        /// </summary>
        public static string TruncateTextToWidth(string text, float maxWidth)
        {
            return Truncate(text, maxWidth); // Redirect to efficient version
        }

        /// <summary>
        /// Calculates if text will fit within the specified width
        /// 2 references
        /// </summary>
        public static bool TextFitsWidth(string text, float maxWidth)
        {
            if (string.IsNullOrEmpty(text))
                return true;

            float paddingWidth = Text.CalcSize("M").x * 2f;
            float availableWidth = maxWidth - paddingWidth;
            return Text.CalcSize(text).x <= availableWidth;
        }

        /// <summary>
        /// Efficient binary search truncation (internal core method)
        /// 7 references
        /// </summary>
        private static string TruncateTextToWidthEfficient(string text, float maxWidth, string ellipsis = "..")
        {
            if (string.IsNullOrEmpty(text) || TextFitsWidth(text, maxWidth))
                return text;

            float paddingWidth = Text.CalcSize("M").x * 2f;
            float availableWidth = maxWidth - paddingWidth;

            int min = 1;
            int max = text.Length;
            string result = text + ellipsis;

            while (min <= max)
            {
                int mid = (min + max) / 2;
                string test = text.Substring(0, mid) + ellipsis;

                if (Text.CalcSize(test).x <= availableWidth)
                {
                    result = test;
                    min = mid + 1;
                }
                else
                {
                    max = mid - 1;
                }
            }

            return result;
        }

        /// <summary>
        /// Checks if text would be truncated to fit within specified width
        /// 8 references - used mostly in store editor and traits editor
        /// </summary>
        public static bool WouldTruncate(string text, float maxWidth)
        {
            if (string.IsNullOrEmpty(text))
                return false;

            float paddingWidth = Text.CalcSize("M").x * 2f;
            float availableWidth = maxWidth - paddingWidth;
            return Text.CalcSize(text).x > availableWidth;
        }

        /// <summary>
        /// Draws a button with automatic truncation and tooltip if truncated
        /// 8 References
        /// </summary>
        public static bool ButtonWithTruncation(Rect rect, string text, string tooltip = null, bool active = true)
        {
            string displayText = Truncate(text, rect.width);
            bool clicked;

            if (active)
            {
                clicked = Widgets.ButtonText(rect, displayText);
            }
            else
            {
                Widgets.ButtonText(rect, displayText, active: false);
                clicked = false;
            }

            if (Mouse.IsOver(rect))
            {
                if (!string.IsNullOrEmpty(tooltip))
                    TooltipHandler.TipRegion(rect, tooltip);
                else if (WouldTruncate(text, rect.width))
                    TooltipHandler.TipRegion(rect, text);
            }

            return clicked;
        }

        /// <summary>
        /// Draws a button with truncation and custom TipSignal tooltip
        /// </summary>
        public static bool ButtonWithTruncation(Rect rect, string text, TipSignal tooltip)
        {
            string truncatedText = Truncate(text, rect.width);
            bool result = Widgets.ButtonText(rect, truncatedText);

            if (Mouse.IsOver(rect))
            {
                TooltipHandler.TipRegion(rect, tooltip);
            }

            return result;
        }

        /// <summary>
        /// Flexible numeric text field: allows free typing, clamps only on valid input
        /// 19 references
        /// Keeps invalid input visible so user can correct it
        /// </summary>
        public static void TextFieldNumericFlexible<T>(Rect rect, ref T value, ref string buffer, T min, T max) where T : struct
        {
            string newBuffer = Widgets.TextField(rect, buffer);

            if (newBuffer != buffer)
            {
                buffer = newBuffer;

                if (float.TryParse(buffer, out float floatValue))
                {
                    if (typeof(T) == typeof(int))
                    {
                        int intValue = (int)floatValue;
                        intValue = Mathf.Clamp(intValue, (int)(object)min, (int)(object)max);
                        value = (T)(object)intValue;
                        buffer = intValue.ToString();
                    }
                    else if (typeof(T) == typeof(float))
                    {
                        floatValue = Mathf.Clamp(floatValue, (float)(object)min, (float)(object)max);
                        value = (T)(object)floatValue;
                        buffer = floatValue.ToString("0.##");
                    }
                }
                else if (string.IsNullOrWhiteSpace(buffer))
                {
                    value = min;
                    buffer = min.ToString();
                }
                // Else: keep invalid buffer visible for user correction
            }
        }

        /// <summary>
        /// Draws a label with right-aligned muted description text
        /// 3 references
        /// </summary>
        public static void LabelWithDescription(Rect rect, string label, string description)
        {
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(rect, label);

            if (!string.IsNullOrEmpty(description))
            {
                Color originalColor = GUI.color;
                GUI.color = ColorLibrary.MutedText;

                float descriptionWidth = Text.CalcSize(description).x;
                Rect descriptionRect = new Rect(rect.xMax - descriptionWidth, rect.y, descriptionWidth, rect.height);
                Widgets.Label(descriptionRect, description);

                GUI.color = originalColor;
            }

            Text.Anchor = TextAnchor.UpperLeft;
        }

        /// <summary>
        /// Draws a numeric integer field with label and description
        /// 9 references
        /// </summary>
        public static void NumericField(Listing_Standard listing, string label, string description, ref int value, int min, int max)
        {
            Rect rect = listing.GetRect(Text.LineHeight);
            Rect leftRect = rect.LeftPart(0.7f).Rounded();
            Rect rightRect = rect.RightPart(0.3f).Rounded();

            LabelWithDescription(leftRect, label, description);

            string buffer = value.ToString();
            Widgets.TextFieldNumeric(rightRect, ref value, ref buffer, min, max);

            listing.Gap(2f);
        }

        // Float version commented out as unused (0 references) - keep for future use if needed
        public static void NumericField(Listing_Standard listing, string label, string description, ref float value, float min, float max)
        {
            Rect rect = listing.GetRect(Text.LineHeight);
            Rect leftRect = rect.LeftPart(0.7f).Rounded();
            Rect rightRect = rect.RightPart(0.3f).Rounded();

            LabelWithDescription(leftRect, label, description);

            // Initialize buffer with "0.##" so decimals (0.05, 0.35, etc.) parse correctly
            string buffer = value.ToString("0.##");

            Widgets.TextFieldNumeric(rightRect, ref value, ref buffer, min, max);

            // Always clamp after editing (TextFieldNumeric does not return success/failure)
            value = Mathf.Clamp(value, min, max);

            listing.Gap(2f);
        }

        /// <summary>
        /// Builds a colored header section for tooltips with a header and list of items.
        /// Example: "<b>Settings</b>\n\n" + ColoredBulletSection("MyMod.GoodForHeader", ColorLibrary.Success, "Item1", "Item2")
        /// </summary>
        public static string ColoredSection(string headerKey, string color, params string[] itemKeys)
        {
            string text = "<color=" + color + ">" + headerKey.Translate() + "</color>\n";

            foreach (var key in itemKeys)
            {
                if (!string.IsNullOrEmpty(key))
                    text += key.Translate() + "\n";
            }

            return text.TrimEnd('\n');
        }

        /// <summary>
        /// Colored section with default bullet prefix ("• ")
        /// </summary>
        public static string ColoredBulletSection(string headerKey, string color, params string[] itemKeys)
        {
            string text = "<color=" + color + ">" + headerKey.Translate() + "</color>\n";

            foreach (var key in itemKeys)
            {
                if (!string.IsNullOrEmpty(key))
                    text += "• " + key.Translate() + "\n";
            }

            return text.TrimEnd('\n');
        }

        /// <summary>
        /// Builds a simple bold header + description tooltip string
        /// </summary>
        public static string BasicTooltip(string titleKey, string descriptionKey)
        {
            return "<b>" + titleKey.Translate() + "</b>\n\n" + descriptionKey.Translate();
        }
        /// <summary>
        /// Wraps text in rich-text color tag using a Color struct
        /// </summary>
        public static string Colorize(string text, Color color)
        {
            string hex = ColorUtility.ToHtmlStringRGB(color);
            return $"<color=#{hex}>{text}</color>";
        }
    }

    /// <summary>
    /// Helper for buffered text fields to prevent UI flicker/conflicts in settings windows.
    /// Call ClearAllBuffers() in your settings window's PostClose() to prevent memory buildup.
    /// </summary>
    public static class TextFieldHelper
    {
        private static readonly Dictionary<string, string> textFieldBuffers = new();

        public static string DrawBufferedTextField(Rect rect, string currentValue, string uniqueKey)
        {
            if (!textFieldBuffers.TryGetValue(uniqueKey, out string buffer))
            {
                buffer = currentValue ?? string.Empty;
                textFieldBuffers[uniqueKey] = buffer;
            }

            buffer = Widgets.TextField(rect, buffer);
            textFieldBuffers[uniqueKey] = buffer;
            return buffer;
        }

        public static void UpdateBuffer(string uniqueKey, string value)
        {
            textFieldBuffers[uniqueKey] = value ?? string.Empty;
        }

        public static void ClearBuffer(string uniqueKey)
        {
            textFieldBuffers.Remove(uniqueKey);
        }

        public static void ClearAllBuffers()
        {
            textFieldBuffers.Clear();
        }
    }

    /// <summary>
    /// Centralized color palette (67 references)
    /// Use ColorLibrary.Colorize(text, ColorLibrary.SomeColor) for rich text
    /// </summary>
    public static class ColorLibrary
    {
        // Semantic / theme colors
        public static readonly Color HeaderAccent = new Color(1.0f, 0.5f, 0.1f);   // Orange - headers
        public static readonly Color SubHeader = new Color(0.529f, 0.808f, 0.922f); // SkyBlue - sub-headers
        public static readonly Color PrimaryAction = new Color(0.2f, 0.4f, 0.8f);   // Blue
        public static readonly Color Success = new Color(0.2f, 0.8f, 0.2f);
        public static readonly Color Warning = new Color(1.0f, 0.75f, 0.2f);  // Yellow-Orange  Maybe more yellow?
        public static readonly Color Danger = new Color(0.9f, 0.1f, 0.1f);
        public static readonly Color Info = new Color(0.3f, 0.65f, 0.95f); // Informational / neutral blue for status messages, tips, secondary labels and info sections. Distinct from PrimaryAction (darker action blue) and SubHeader (lighter sky blue) while staying readable on RimWorld dark UI.

        // Text variants
        public static readonly Color MutedText = new Color(0.7f, 0.7f, 0.7f);   // Secondary / descriptions
        public static readonly Color LightText = new Color(0.85f, 0.85f, 0.85f); // Tiny / faint text
        public static readonly Color DefaultText = Color.white;

        // Utility
        public static readonly Color BlackOutline = Color.black;

        /// <summary>
        /// Wraps text in rich-text color tag using a Color struct
        /// </summary>
        /// we are switching to method:  ColorLibrary.Colorize(modeText, modeColor)
        /// 
        public static string Colorize(string text, Color color)
        {
            string hex = ColorUtility.ToHtmlStringRGB(color);
            return $"<color=#{hex}>{text}</color>";
        }
    }

    namespace CAP_ChatInteractive
    {
        public static class Translations
        {
            // Permission levels
            public static string TranslatePermission(string permission)
            {
                return permission switch
                {
                    "broadcaster" => "CAP.Permission.Broadcaster".Translate(),
                    "moderator" => "CAP.Permission.Moderator".Translate(),
                    "vip" => "CAP.Permission.VIP".Translate(),
                    "subscriber" => "CAP.Permission.Subscriber".Translate(),
                    "everyone" => "CAP.Permission.Everyone".Translate(),
                    _ => permission // Fallback
                };
            }
        }
    }
}