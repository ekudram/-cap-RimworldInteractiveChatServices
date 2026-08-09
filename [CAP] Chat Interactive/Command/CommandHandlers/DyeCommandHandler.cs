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
//
// !dye [hair] [color] — hair or apparel color for viewer pawn
using CAP_ChatInteractive.Helpers;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Verse;

namespace CAP_ChatInteractive.Commands.ViewerCommands
{
    internal static class DyeCommandHandler
    {
        private const string ReturnDivider = " | ";

        private static Dictionary<string, Color> _rimColorCache;

        /// <summary>Cache ColorDefs useful for dye (hair-type + common labels).</summary>
        private static Dictionary<string, Color> GetAllRimColorDefs()
        {
            if (_rimColorCache != null)
                return _rimColorCache;

            _rimColorCache = new Dictionary<string, Color>(StringComparer.OrdinalIgnoreCase);

            foreach (var colorDef in DefDatabase<ColorDef>.AllDefs)
            {
                if (colorDef == null)
                    continue;

                bool isHairColor = colorDef.colorType == ColorType.Hair ||
                                   (colorDef.defName?.IndexOf("Hair", StringComparison.OrdinalIgnoreCase) >= 0) ||
                                   (colorDef.label?.IndexOf("hair", StringComparison.OrdinalIgnoreCase) >= 0);

                if (!isHairColor)
                    continue;

                Color color = colorDef.color;

                if (!string.IsNullOrEmpty(colorDef.defName) && !_rimColorCache.ContainsKey(colorDef.defName))
                    _rimColorCache.Add(colorDef.defName, color);

                if (!string.IsNullOrEmpty(colorDef.label) && !_rimColorCache.ContainsKey(colorDef.label))
                    _rimColorCache.Add(colorDef.label, color);

                string normalizedLabel = colorDef.label?.Replace(" ", "");
                if (!string.IsNullOrEmpty(normalizedLabel) && !_rimColorCache.ContainsKey(normalizedLabel))
                    _rimColorCache.Add(normalizedLabel, color);
            }

            return _rimColorCache;
        }

        internal static string HandleDyeCommand(ChatMessageWrapper messageWrapper, string[] args)
        {
            var assignmentManager = CAPChatInteractiveMod.GetPawnAssignmentManager();
            Verse.Pawn viewerPawn = assignmentManager?.GetAssignedPawn(messageWrapper);

            if (viewerPawn == null)
                return "RICS.Pawn.NoPawn".Translate();

            // Fully destroyed / gone — cannot dye
            if (viewerPawn.Destroyed)
            {
                var deathInfo = GameComponent_PawnAssignmentManager.GetPawnDeathInfo(viewerPawn);
                return "RICS.Pawn.Dead".Translate()
                       + ReturnDivider
                       + "RICS.Return.PawnDeadReason".Translate(deathInfo.ToString());
            }

            args = args ?? Array.Empty<string>();
            bool isHairDye = args.Length > 0 && args[0].Equals("hair", StringComparison.OrdinalIgnoreCase);

            var cmdSettings = CommandSettingsManager.GetSettings("dye");
            if (isHairDye)
            {
                if (!cmdSettings.GetCustom("enableHairDye", true))
                    return "RICS.DyeCommand.HairDisabled".Translate();
            }
            else if (!cmdSettings.GetCustom("enableApparelDye", true))
            {
                return "RICS.DyeCommand.ApparelDisabled".Translate();
            }

            // Dead but still present: allow dye (funeral prep), append note on success
            bool isDead = viewerPawn.Dead;
            string deathMessage = "";
            if (isDead)
            {
                var deathInfo = GameComponent_PawnAssignmentManager.GetPawnDeathInfo(viewerPawn);
                deathMessage = ReturnDivider + "RICS.DyeCommand.DeadPawnNote".Translate(deathInfo.ToString());
            }

            Color? color = null;
            string colorInput = null;
            int startIndex = isHairDye ? 1 : 0;

            if (args.Length > startIndex)
            {
                colorInput = ReconstructColorString(args, startIndex);
                if (!string.IsNullOrEmpty(colorInput))
                {
                    color = ParseColorInput(colorInput);
                    if (!color.HasValue)
                        return "RICS.DyeCommand.InvalidColor".Translate(colorInput);
                }
            }

            if (!color.HasValue)
            {
                if (!ModsConfig.IdeologyActive)
                    return "RICS.DyeCommand.IdeologyRequired".Translate();

                color = viewerPawn.story?.favoriteColor?.color ?? new Color(0.6f, 0.6f, 0.6f);
                colorInput = "favorite color";
            }

            // Force opaque alpha — a=0 from helpers makes CompColorable invisible
            Color c = color.Value;
            if (c.a < 0.99f)
                color = new Color(c.r, c.g, c.b, 1f);

            return isHairDye
                ? HandleHairDye(viewerPawn, color.Value, colorInput, isDead, deathMessage)
                : HandleApparelDye(viewerPawn, color.Value, colorInput, isDead, deathMessage);
        }

        private static string ReconstructColorString(string[] args, int startIndex)
        {
            if (startIndex >= args.Length)
                return null;

            var sb = new StringBuilder();
            for (int i = startIndex; i < args.Length; i++)
            {
                if (i > startIndex)
                    sb.Append(' ');
                sb.Append(args[i]);
            }
            return sb.ToString();
        }

        private static Color? ParseColorInput(string colorInput)
        {
            if (string.IsNullOrEmpty(colorInput))
                return null;

            var rimColors = GetAllRimColorDefs();

            // Dictionary is OrdinalIgnoreCase — single lookup covers case variants
            if (rimColors.TryGetValue(colorInput, out Color rimColor))
                return rimColor;

            string noSpaces = colorInput.Replace(" ", "");
            if (rimColors.TryGetValue(noSpaces, out rimColor))
                return rimColor;

            var chatColor = FindChatColorDef(colorInput);
            if (chatColor.HasValue)
                return chatColor.Value;

            var hashColor = TryGetColorByHash(colorInput);
            if (hashColor.HasValue)
                return hashColor.Value;

            return ColorHelper.ParseColor(colorInput);
        }

        private static Color? FindChatColorDef(string input)
        {
            if (string.IsNullOrEmpty(input))
                return null;

            var def = DefDatabase<ColorDef>.GetNamedSilentFail("Chat_" + input.Replace(" ", ""))
                      ?? DefDatabase<ColorDef>.GetNamedSilentFail(input);
            return def?.color;
        }

        private static Color? TryGetColorByHash(string input)
        {
            if (string.IsNullOrEmpty(input) || !input.StartsWith("#"))
                return null;
            return ColorUtility.TryParseHtmlString(input, out Color c) ? c : (Color?)null;
        }

        private static string HandleHairDye(Verse.Pawn pawn, Color color, string colorInput, bool isDead, string deathMessage = "")
        {
            if (pawn.story == null || pawn.story.hairDef == null)
                return "RICS.DyeCommand.NoHair".Translate();

            pawn.story.HairColor = color;
            ForceHairGraphicsUpdate(pawn);

            string colorName = GetColorNameForResponse(color, colorInput);
            if (isDead)
                return "RICS.DyeCommand.HairSuccessDead".Translate(colorName) + deathMessage;
            return "RICS.DyeCommand.HairSuccess".Translate(colorName);
        }

        private static void ForceHairGraphicsUpdate(Verse.Pawn pawn)
        {
            if (pawn?.Drawer?.renderer == null)
                return;

            try
            {
                pawn.Drawer.renderer.SetAllGraphicsDirty();

                if (pawn.style != null)
                {
                    pawn.style.Notify_StyleItemChanged();
                    if (pawn.style.nextHairColor.HasValue)
                        pawn.style.FinalizeHairColor();
                }
            }
            catch (Exception ex)
            {
                Logger.Warning($"[Dye] Failed hair graphics update for {pawn.LabelShort}: {ex.Message}");
            }
        }

        private static string HandleApparelDye(Verse.Pawn pawn, Color color, string colorInput, bool isDead, string deathMessage = "")
        {
            int dyedCount = ApplyDyeToApparel(pawn, color);
            if (dyedCount == 0)
                return "RICS.DyeCommand.NoDyeableClothing".Translate();

            string colorName = GetColorNameForResponse(color, colorInput);
            if (isDead)
                return "RICS.DyeCommand.ApparelSuccessDead".Translate(dyedCount, colorName) + deathMessage;
            return "RICS.DyeCommand.ApparelSuccess".Translate(dyedCount, colorName);
        }

        private static int ApplyDyeToApparel(Verse.Pawn pawn, Color color)
        {
            int count = 0;
            var apparel = pawn.apparel?.WornApparel;
            if (apparel == null)
                return 0;

            foreach (var item in apparel)
            {
                if (!IsDyeableApparel(item))
                    continue;

                var comp = item.TryGetComp<CompColorable>();
                if (comp == null)
                    continue;

                comp.SetColor(color);
                count++;
            }

            return count;
        }

        private static string GetColorNameForResponse(Color color, string colorInput = null)
        {
            if (!string.IsNullOrEmpty(colorInput) && !colorInput.StartsWith("#") && colorInput != "favorite color")
                return colorInput;

            foreach (var kvp in GetAllRimColorDefs())
            {
                if (ColorsApproximatelyEqual(kvp.Value, color))
                    return kvp.Key;
            }

            foreach (var kvp in ColorHelper.GetColorDictionary())
            {
                if (ColorsApproximatelyEqual(kvp.Value, color))
                    return kvp.Key;
            }

            foreach (var colorDef in DefDatabase<ColorDef>.AllDefs)
            {
                if (colorDef.defName != null &&
                    colorDef.defName.StartsWith("Chat_") &&
                    ColorsApproximatelyEqual(colorDef.color, color))
                {
                    return colorDef.label.CapitalizeFirst();
                }
            }

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
            if (apparel?.def == null)
                return false;

            string defName = apparel.def.defName?.ToLower() ?? "";
            string label = apparel.def.label?.ToLower() ?? "";

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

            if (apparel.def.apparel?.tags != null)
            {
                var tags = apparel.def.apparel.tags;
                if (tags.Contains("Jewelry") || tags.Contains("Accessory") || tags.Contains("Utility"))
                    return false;
            }

            return !IsUtilitySlotItem(apparel);
        }

        private static bool IsUtilitySlotItem(Apparel apparel)
        {
            var layers = apparel.def.apparel?.layers;
            if (layers != null && layers.Contains(ApparelLayerDefOf.Belt))
                return true;

            if (apparel.def.apparel?.tags != null)
            {
                var tags = apparel.def.apparel.tags;
                if (tags.Contains("Utility") || tags.Contains("Belt") || tags.Contains("Holster"))
                    return true;
            }

            string defName = apparel.def.defName?.ToLower() ?? "";
            return defName.Contains("utility") || defName.Contains("belt") || defName.Contains("holster") ||
                   defName.Contains("tool") || defName.Contains("pouch");
        }
    }
}
