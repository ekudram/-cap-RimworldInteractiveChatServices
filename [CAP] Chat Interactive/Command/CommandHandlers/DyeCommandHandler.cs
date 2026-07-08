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

// This class handles the !dye command for changing hair color or apparel color.

using _CAP__Chat_Interactive.Command.CommandHelpers;
using CAP_ChatInteractive.Helpers;
using RimWorld;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Verse;

namespace CAP_ChatInteractive.Commands.ViewerCommands
{
    internal static class DyeCommandHandler
    {
        // Cache of RimWorld hair color definitions for quick lookup
        private static Dictionary<string, Color> _rimColorCache;

        /// <summary>
        /// Caches all RimWorld ColorDefs that are likely to be used for all colors, using defName and label for lookup.
        /// </summary>
        /// <returns></returns>
        private static Dictionary<string, Color> GetAllRimColorDefs()
        {
            if (_rimColorCache != null) return _rimColorCache;

            _rimColorCache = new Dictionary<string, Color>(System.StringComparer.OrdinalIgnoreCase);

            // Get all ColorDefs from RimWorld
            var allColorDefs = DefDatabase<ColorDef>.AllDefs;

            foreach (var colorDef in allColorDefs)
            {
                // Check if it's a hair color by colorType or naming convention
                bool isHairColor = colorDef.colorType == ColorType.Hair ||
                                  colorDef.defName.Contains("Hair") ||
                                  (colorDef.label?.Contains("hair") ?? false);

                if (isHairColor)
                {
                    // Get the color directly - ColorDef.color is already a Unity Color
                    // RimWorld stores it as 0-255 but Unity handles the conversion automatically
                    Color color = colorDef.color;

                    // Add by defName
                    if (!_rimColorCache.ContainsKey(colorDef.defName))
                        _rimColorCache.Add(colorDef.defName, color);

                    // Add by label if it exists and is different
                    if (!string.IsNullOrEmpty(colorDef.label) && !_rimColorCache.ContainsKey(colorDef.label))
                        _rimColorCache.Add(colorDef.label, color);

                    // Also add a lowercase version without spaces for easier matching
                    string normalizedLabel = colorDef.label?.Replace(" ", "").ToLower();
                    if (!string.IsNullOrEmpty(normalizedLabel) && !_rimColorCache.ContainsKey(normalizedLabel))
                        _rimColorCache.Add(normalizedLabel, color);
                }
            }

            return _rimColorCache;
        }

        /// <summary>
        /// Handles the !dye command, allowing viewers to change their pawn's hair color or apparel color.
        /// </summary>
        /// <param name="messageWrapper">The wrapper containing the chat message information.</param>
        /// <param name="args">The arguments passed with the !dye command.</param>
        /// <returns>A string message indicating the result of the command.</returns>
        internal static string HandleDyeCommand(ChatMessageWrapper messageWrapper, string[] args)
        {
            var assignmentManager = CAPChatInteractiveMod.GetPawnAssignmentManager();
            Verse.Pawn viewerPawn = assignmentManager.GetAssignedPawn(messageWrapper);

            if (viewerPawn == null)
            {
                return "RICS.Pawn.NoPawn".Translate();
            }
            if (viewerPawn.Destroyed)
            {
                // This gives much better player experience than a generic "your pawn is dead" message.
                var deathInfo = GameComponent_PawnAssignmentManager.GetPawnDeathInfo(viewerPawn);

                string deathDetails = deathInfo.ToString(); // e.g. "Deceased (body remains) — bullet wound caused by Assault Rifle"

                return "RICS.Pawn.Dead".Translate() + "RICS.Return.PawnDeadReason".Translate(deathDetails);
            }

            // Check sub enable
            var cmdSettings = CommandSettingsManager.GetSettings("dye");
            bool isHairDyeCheck = args.Length > 0 && args[0].ToLower() == "hair";
            if (isHairDyeCheck)
            {
                if (!cmdSettings.GetCustom<bool>("enableHairDye", true))
                    return "Sub Command hair is disabled.";
            }
            else
            {
                if (!cmdSettings.GetCustom<bool>("enableApparelDye", true))
                    return "Sub Command apparel is disabled.";
            }

            // Capture death state early (we still allow dyeing)
            bool isDead = viewerPawn.Dead;
            string deathMessage = "";

            if (isDead)
            {
                var deathInfo = GameComponent_PawnAssignmentManager.GetPawnDeathInfo(viewerPawn);
                deathMessage = " " + "RICS.DyeCommand.DeadPawnNote".Translate(deathInfo.ToString());
            }


            // Check for subcommand
            bool isHairDye = args.Length > 0 && args[0].ToLower() == "hair";

            // Parse color from arguments (handle multi-word colors)
            Color? color = null;
            string colorInput = null;
            int startIndex = isHairDye ? 1 : 0;

            if (args.Length > startIndex)
            {
                // Reconstruct multi-word color from remaining arguments
                colorInput = ReconstructColorString(args, startIndex);

                if (!string.IsNullOrEmpty(colorInput))
                {
                    color = ParseColorInput(colorInput);

                    if (!color.HasValue)
                    {
                        // return $"'{colorInput}' is not a valid color. Try using RimWorld hair color names (like 'PitchBlack', 'DarkReddish', 'SandyBlonde'), common color names (like 'red', 'blue'), or hex codes like #FF0000.";
                        return "RICS.DyeCommand.InvalidColor".Translate(colorInput);    
                    }
                }
            }



            // Use favorite color if no color specified
            if (!color.HasValue)
            {
                if (!ModsConfig.IdeologyActive)
                {
                    // return "Please specify a color. The favorite color system requires the Ideology DLC.";
                    return "RICS.DyeCommand.IdeologyRequired".Translate();
                }

                color = viewerPawn.story?.favoriteColor?.color ?? new Color(0.6f, 0.6f, 0.6f);
                colorInput = "favorite color";
            }

            if (color.HasValue)
            {
                Color c = color.Value;
                if (c.a < 0.99f)
                {
                    // WHY: User-provided colors via ColorHelper.ParseColor (named "cyan", hex without alpha, etc.)
                    // or certain closest-match paths can arrive with a=0, which makes CompColorable render
                    // the apparel completely transparent/invisible. We force opaque here because apparel dye
                    // and hair color are always meant to be solid. This is defensive and does not change hue.
                    color = new Color(c.r, c.g, c.b, 1f);
                    Logger.Debug($"[RICS Dye] Forced alpha=1f for color '{colorInput ?? "favorite color"}' to prevent invisible clothing.");
                }
            }

            if (isHairDye)
            {
                return HandleHairDye(viewerPawn, color.Value, colorInput, isDead, deathMessage);
            }
            else
            {
                return HandleApparelDye(viewerPawn, color.Value, colorInput, isDead, deathMessage);
            }
        }

        /// <summary>
        /// String reconstruction helper to combine multiple arguments into a single color string, allowing for multi-word color names.
        /// </summary>
        /// <param name="args">The array of arguments passed to the command.</param>
        /// <param name="startIndex">The index in the arguments array to start combining from.</param>
        /// <returns>A single string representing the combined color name.</returns>
        private static string ReconstructColorString(string[] args, int startIndex)
        {
            if (startIndex >= args.Length) return null;

            StringBuilder sb = new StringBuilder();
            for (int i = startIndex; i < args.Length; i++)
            {
                if (i > startIndex) sb.Append(" ");
                sb.Append(args[i]);
            }
            return sb.ToString();
        }

        /// <summary>
        /// Parses the color input string, trying multiple methods to find a matching Color.
        /// It checks RimWorld hair colors, common color names, hex codes, and custom ChatColorDefs.
        /// </summary>
        /// <param name="colorInput">The input string representing the color.</param>
        /// <returns>A Unity Color if parsing is successful; otherwise, null.</returns>
        private static Color? ParseColorInput(string colorInput)
        {
            if (string.IsNullOrEmpty(colorInput))
                return null;

            // First check RimWorld hair colors (including multi-word)
            var rimColors = GetAllRimColorDefs();

            // Try exact match first (preserve case for defNames like "PitchBlack")
            if (rimColors.TryGetValue(colorInput, out Color rimColor))
            {
                Logger.Debug($"[CAP] Found exact match: {colorInput} -> R:{rimColor.r} G:{rimColor.g} B:{rimColor.b}");
                return rimColor;
            }

            // Try case-insensitive match
            string lowerInput = colorInput.ToLower();
            foreach (var kvp in rimColors)
            {
                if (kvp.Key.ToLower() == lowerInput)
                {
                    Logger.Debug($"[CAP] Found case-insensitive match: {kvp.Key} -> R:{kvp.Value.r} G:{kvp.Value.g} B:{kvp.Value.b}");
                    return kvp.Value;
                }
            }

            // Try without spaces
            string noSpaces = colorInput.Replace(" ", "");
            foreach (var kvp in rimColors)
            {
                if (string.Equals(kvp.Key.Replace(" ", ""), noSpaces, System.StringComparison.OrdinalIgnoreCase))
                {
                    Logger.Debug($"[CAP] Found no-spaces match: {kvp.Key} -> R:{kvp.Value.r} G:{kvp.Value.g} B:{kvp.Value.b}");
                    return kvp.Value;
                }
            }

            // NEW: ChatColorDef lookup (your expanded colors)
            var chatColor = FindChatColorDef(colorInput);
            if (chatColor.HasValue)
            {
                Logger.Debug($"[CAP] Found ChatColorDef match: {colorInput}");
                return chatColor.Value;
            }

            // NEW: Custom hash lookup (e.g. from favorite color or direct hex)
            var hashColor = TryGetColorByHash(colorInput);
            if (hashColor.HasValue)
            {
                Logger.Debug($"[CAP] Found color by hash: {colorInput}");
                return hashColor.Value;
            }

            // Fall back to ColorHelper
            Color? helperColor = ColorHelper.ParseColor(colorInput);
            if (helperColor.HasValue)
            {
                Logger.Debug($"[CAP] Using ColorHelper: {colorInput} -> R:{helperColor.Value.r} G:{helperColor.Value.g} B:{helperColor.Value.b}");
            }
            return helperColor;
        }
        /// <summary>
        /// Checks for a matching ChatColorDef based on the input string, allowing for both "Chat_ColorName" and just "ColorName" lookups.
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        private static Color? FindChatColorDef(string input)
        {
            if (string.IsNullOrEmpty(input)) return null;

            var def = DefDatabase<ColorDef>.GetNamedSilentFail("Chat_" + input.Replace(" ", ""))
                   ?? DefDatabase<ColorDef>.GetNamedSilentFail(input);

            return def?.color;
        }

        /// <summary>
        /// Tries to parse a color from a hex string (e.g. "#FF0000").
        /// Returns null if parsing fails or if the input is not a valid hex code.
        /// </summary>
        /// <param name="input"></param>
        /// <returns>Returns a Color if parsing is successful; otherwise, null.</returns>
        private static Color? TryGetColorByHash(string input)
        {
            if (string.IsNullOrEmpty(input) || !input.StartsWith("#")) return null;

            if (ColorUtility.TryParseHtmlString(input, out Color c))
                return c;

            return null;
        }

        /// <summary>
        /// Ver 1.37 Removed Styling Station support, so we set hair color directly on the pawn's story.
        /// This should work with all hair types and mods that use the standard hair system.
        /// If a mod uses a completely custom hair system that doesn't rely on the pawn's story.HairColor,
        /// then it may not work, but most should be compatible with this approach.
        /// </summary>
        /// <param name="pawn"></param>
        /// <param name="color"></param>
        /// <param name="colorInput"></param>
        /// <returns></returns>
        // In DyeCommandHandler.cs, inside DyeCommandHandler.HandleHairDye(Verse.Pawn pawn, Color color, string colorInput = null)
        private static string HandleHairDye(Verse.Pawn pawn, Color color, string colorInput, bool isDead, string deathMessage = "")
        {
            if (pawn.story == null || pawn.story.hairDef == null)
            {
                return "RICS.DyeCommand.NoHair".Translate();
            }

            pawn.story.HairColor = color;
            ForceHairGraphicsUpdate(pawn);

            string colorName = GetColorNameForResponse(color, colorInput);

            if (isDead)
            {
                return "RICS.DyeCommand.HairSuccessDead".Translate(colorName) + deathMessage;
            }

            return "RICS.DyeCommand.HairSuccess".Translate(colorName);
        }

        /// <summary>
        /// Forces the pawn's hair and overall graphics to refresh immediately after changing HairColor.
        /// This replicates what the Styling Station does internally.
        /// </summary>
        private static void ForceHairGraphicsUpdate(Verse.Pawn pawn)
        {
            if (pawn == null || pawn.Drawer?.renderer == null)
                return;

            try
            {
                // Primary method - marks all render nodes dirty
                pawn.Drawer.renderer.SetAllGraphicsDirty();

                // Optional: Also notify style tracker (helps with Ideology/Style system)
                if (pawn.style != null)
                {
                    pawn.style.Notify_StyleItemChanged();
                    // If nextHairColor was somehow involved
                    if (pawn.style.nextHairColor.HasValue)
                        pawn.style.FinalizeHairColor();
                }

                // Extra safety: dirty the hair render node specifically
                // pawn.Drawer.renderer.SetDirtyFor(PawnRenderNodeTagDefOf.Hair);

            }
            catch (System.Exception ex)
            {
                Logger.Warning($"[CAP] Failed to force hair graphics update for {pawn.LabelShort}: {ex.Message}");
            }
        }

        /// <summary>
        /// Applies the specified color to all dyeable apparel worn by the pawn, excluding jewelry and utility items.
        /// </summary>
        /// <param name="pawn"></param>
        /// <param name="color"></param>
        /// <param name="colorInput"></param>
        /// <returns>Returns a string message indicating the result of the dye operation.</returns>
        private static string HandleApparelDye(Verse.Pawn pawn, Color color, string colorInput, bool isDead, string deathMessage = "")
        {
            int dyedCount = ApplyDyeToApparel(pawn, color);

            if (dyedCount == 0)
            {
                return "RICS.DyeCommand.NoDyeableClothing".Translate();
            }

            string colorName = GetColorNameForResponse(color, colorInput);

            if (isDead)
            {
                return "RICS.DyeCommand.ApparelSuccessDead".Translate(dyedCount, colorName) + deathMessage;
            }

            return "RICS.DyeCommand.ApparelSuccess".Translate(dyedCount, colorName);
        }

        /// <summary>
        /// Applies the specified color to all dyeable apparel worn by the pawn, excluding jewelry and utility items.
        /// </summary>
        /// <param name="pawn"></param>
        /// <param name="color"></param>
        /// <returns>Returns the number of apparel items successfully dyed.</returns>
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

        /// <summary>
        /// Attempts to find a user-friendly name for the color to include in the response message.
        /// </summary>
        /// <param name="color">The color to find a name for.</param>
        /// <param name="colorInput">Optional user input for the color name.</param>
        /// <returns>A string representing the color name.</returns>
        private static string GetColorNameForResponse(Color color, string colorInput = null)
        {
            if (!string.IsNullOrEmpty(colorInput) && !colorInput.StartsWith("#") && colorInput != "favorite color")
            {
                return colorInput;
            }

            // RimWorld defs
            var rimColors = GetAllRimColorDefs();
            foreach (var kvp in rimColors)
            {
                if (ColorsApproximatelyEqual(kvp.Value, color))
                    return kvp.Key;
            }

            // ColorHelper named colors
            foreach (var kvp in ColorHelper.GetColorDictionary())
            {
                if (ColorsApproximatelyEqual(kvp.Value, color))
                    return kvp.Key;
            }

            // NEW: Try ChatColorDef labels
            foreach (var colorDef in DefDatabase<ColorDef>.AllDefs)
            {
                if (colorDef.defName.StartsWith("Chat_") && ColorsApproximatelyEqual(colorDef.color, color))
                {
                    return colorDef.label.CapitalizeFirst();
                }
            }

            // Fallback: Return hex hash for custom colors (user-friendly)
            return $"#{ColorUtility.ToHtmlStringRGB(color)}";
        }


        private static bool ColorsApproximatelyEqual(Color a, Color b, float tolerance = 0.02f)
        {
            return Mathf.Abs(a.r - b.r) < tolerance &&
                   Mathf.Abs(a.g - b.g) < tolerance &&
                   Mathf.Abs(a.b - b.b) < tolerance;
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