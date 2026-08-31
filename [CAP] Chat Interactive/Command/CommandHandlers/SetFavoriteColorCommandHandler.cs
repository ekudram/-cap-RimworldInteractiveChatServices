// File: SetFavoriteColorCommandHandler.cs
//
// Copyright (c) Captolamia
// This file is part of CAP Chat Interactive (RICS).
// Licensed under the GNU Affero General Public License v3.0 or later.
// See LICENSE.txt in the project root for full license text.
//
// !setfavoritecolor — set Ideology favorite color (named ColorDef or hex/RGB).
// Custom colors are persisted via GameComponent_CustomColorDefs so saves survive reload.
using _CAP__Chat_Interactive.Command.CommandHelpers;
using CAP_ChatInteractive.Helpers;
using RimWorld;
using System;
using System.Linq;
using UnityEngine;
using Verse;

namespace CAP_ChatInteractive.Commands.ViewerCommands
{
    internal static class SetFavoriteColorCommandHandler
    {
        private const string ReturnDivider = " | ";

        internal static string HandleSetFavoriteColorCommand(ChatMessageWrapper messageWrapper, string[] args)
        {
            try
            {
                if (!ModsConfig.IdeologyActive)
                    return "RICS.SFCCH.IdeologyRequired".Translate();

                Verse.Pawn viewerPawn = PawnItemHelper.GetViewerPawn(messageWrapper);
                if (viewerPawn == null)
                    return "RICS.SFCCH.NoPawn".Translate();

                if (viewerPawn.Destroyed || viewerPawn.Dead)
                {
                    var deathInfo = GameComponent_PawnAssignmentManager.GetPawnDeathInfo(viewerPawn);
                    return "RICS.Return.PawnDead".Translate()
                           + ReturnDivider
                           + "RICS.Return.PawnDeadReason".Translate(deathInfo.ToString());
                }

                if (viewerPawn.story == null)
                    return "RICS.SFCCH.NoStory".Translate();

                args = args ?? Array.Empty<string>();
                if (args.Length == 0 || string.IsNullOrWhiteSpace(args[0]))
                {
                    var currentColorDef = viewerPawn.story.favoriteColor;
                    string current = currentColorDef != null
                        ? (currentColorDef.label ?? currentColorDef.defName)
                        : "none";
                    return "RICS.SFCCH.UsageNoArgs".Translate(current);
                }

                // Multi-word color names (up to 3 words)
                int wordsToTake = Mathf.Min(args.Length, 3);
                string colorInput = string.Join(" ", args.Take(wordsToTake)).Trim();

                // 1. Exact / named ColorDef — use the def itself (do not rebuild as custom)
                ColorDef namedDef = FindColorDefByName(colorInput);
                if (namedDef != null)
                {
                    if (SetPawnFavoriteColor(viewerPawn, namedDef))
                        return "RICS.SFCCH.SuccessNamed".Translate(namedDef.label ?? namedDef.defName);
                    return "RICS.SFCCH.FailedToSet".Translate();
                }

                // 2. Parse hex / named helper colors
                Color? parsedColor = ColorHelper.ParseColor(colorInput);
                if (!parsedColor.HasValue)
                {
                    // Closest ColorDef by name-as-color fallback
                    namedDef = FindClosestColorDef(colorInput);
                    if (namedDef != null && SetPawnFavoriteColor(viewerPawn, namedDef))
                    {
                        return "RICS.SFCCH.SuccessHSV".Translate(
                            namedDef.label ?? namedDef.defName,
                            namedDef.color.ToString());
                    }

                    return "RICS.SFCCH.InvalidColor".Translate(colorInput);
                }

                // 3. Custom / parsed color — persist ColorDef so save/load keeps it
                if (SetPawnFavoriteColor(viewerPawn, parsedColor.Value))
                {
                    string colorName = GetColorName(parsedColor.Value);
                    return "RICS.SFCCH.SuccessGeneric".Translate(colorName);
                }

                return "RICS.SFCCH.FailedToSet".Translate();
            }
            catch (Exception ex)
            {
                Logger.Error($"[FavoriteColor] Error: {ex}");
                return "RICS.SFCCH.FailedToSet".Translate();
            }
        }

        private static bool SetPawnFavoriteColor(Verse.Pawn pawn, Color color)
        {
            try
            {
                Color safeColor = new Color(color.r, color.g, color.b, 1f);
                var comp = GameComponent_CustomColorDefs.GetOrCreate();
                ColorDef colorDef = comp != null
                    ? comp.GetOrCreateColorDef(safeColor)
                    : CreateEphemeralColorDef(safeColor); // no game (shouldn't happen in play)

                pawn.story.favoriteColor = colorDef;
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error($"[FavoriteColor] Failed to set color for {pawn?.Name}: {ex}");
                return false;
            }
        }

        private static bool SetPawnFavoriteColor(Verse.Pawn pawn, ColorDef colorDef)
        {
            try
            {
                if (colorDef == null)
                    return false;
                pawn.story.favoriteColor = colorDef;
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error($"[FavoriteColor] Failed to set ColorDef for {pawn?.Name}: {ex}");
                return false;
            }
        }

        /// <summary>Fallback if no GameComponent (should not run during normal play).</summary>
        private static ColorDef CreateEphemeralColorDef(Color safeColor)
        {
            string hex = ColorUtility.ToHtmlStringRGB(safeColor);
            string defName = "RICS_Custom_" + hex;
            var existing = DefDatabase<ColorDef>.GetNamedSilentFail(defName);
            if (existing != null)
            {
                GameComponent_CustomColorDefs.EnsureColorDefShortHash(existing);
                return existing;
            }

            var custom = new ColorDef
            {
                defName = defName,
                label = "#" + hex,
                description = "Custom color set by RICS viewer",
                color = safeColor,
                colorType = ColorType.Misc,
                displayOrder = 9999
            };
            return GameComponent_CustomColorDefs.RegisterColorDef(custom);
        }

        private static string GetColorName(Color color)
        {
            foreach (var kvp in ColorHelper.GetColorDictionary())
            {
                if (ColorsAreSimilar(kvp.Value, color))
                    return kvp.Key;
            }
            return "#" + ColorUtility.ToHtmlStringRGB(color);
        }

        private static ColorDef FindColorDefByName(string colorName)
        {
            if (string.IsNullOrWhiteSpace(colorName))
                return null;

            string cleanName = colorName.ToLowerInvariant().Replace(" ", "").Replace("_", "");

            // Exact defName
            var byName = DefDatabase<ColorDef>.GetNamedSilentFail(colorName)
                         ?? DefDatabase<ColorDef>.GetNamedSilentFail(colorName.Replace(" ", ""));
            if (byName != null)
                return byName;

            foreach (ColorDef def in DefDatabase<ColorDef>.AllDefs)
            {
                if (def == null) continue;

                string dn = (def.defName ?? "").ToLowerInvariant().Replace("_", "").Replace(" ", "");
                if (dn == cleanName)
                    return def;

                if (cleanName.Length > 2 && dn.Contains(cleanName))
                    return def;

                if (!def.label.NullOrEmpty())
                {
                    string lab = def.label.ToLowerInvariant().Replace(" ", "");
                    if (lab == cleanName || (cleanName.Length > 2 && lab.Contains(cleanName)))
                        return def;
                }
            }

            return null;
        }

        private static ColorDef FindClosestColorDef(string colorInput)
        {
            Color? parsedColor = ColorHelper.ParseColor(colorInput);
            if (!parsedColor.HasValue)
                return null;

            return DefDatabase<ColorDef>.AllDefs
                .OrderBy(def => ColorDistance(def.color, parsedColor.Value))
                .FirstOrDefault();
        }

        private static float ColorDistance(Color a, Color b)
        {
            float rDiff = a.r - b.r;
            float gDiff = a.g - b.g;
            float bDiff = a.b - b.b;
            return Mathf.Sqrt(rDiff * rDiff + gDiff * gDiff + bDiff * bDiff);
        }

        private static bool ColorsAreSimilar(Color a, Color b, float tolerance = 0.1f)
        {
            return Math.Abs(a.r - b.r) < tolerance &&
                   Math.Abs(a.g - b.g) < tolerance &&
                   Math.Abs(a.b - b.b) < tolerance;
        }
    }
}
