// GameComponent_CustomColorDefs.cs
// Copyright (c) Captolamia
// This file is part of CAP Chat Interactive.
//
// Persists RICS-created favorite ColorDefs across save/load.
// Runtime DefDatabase.Add alone is lost when the process restarts; save game
// only stores the defName reference on the pawn, so we must re-register.

using RimWorld;
using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace CAP_ChatInteractive
{
    /// <summary>Serializable RGB + defName for a custom favorite color.</summary>
    public class SavedCustomColor : IExposable
    {
        public string defName;
        public string label;
        public float r;
        public float g;
        public float b;

        public void ExposeData()
        {
            Scribe_Values.Look(ref defName, "defName");
            Scribe_Values.Look(ref label, "label");
            Scribe_Values.Look(ref r, "r");
            Scribe_Values.Look(ref g, "g");
            Scribe_Values.Look(ref b, "b");
        }

        public Color ToColor() => new Color(r, g, b, 1f);
    }

    public class GameComponent_CustomColorDefs : GameComponent
    {
        public List<SavedCustomColor> customColors = new List<SavedCustomColor>();

        public GameComponent_CustomColorDefs(Game game)
        {
            if (customColors == null)
                customColors = new List<SavedCustomColor>();
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref customColors, "ricsCustomFavoriteColors", LookMode.Deep);
            if (customColors == null)
                customColors = new List<SavedCustomColor>();

            // LoadingVars: re-inject ASAP so pawn favoriteColor CrossRef (LookMode.Def) can resolve.
            // PostLoadInit: belt-and-suspenders if anything still missing.
            if (Scribe.mode == LoadSaveMode.LoadingVars || Scribe.mode == LoadSaveMode.PostLoadInit)
                ReRegisterAll();
        }

        public override void FinalizeInit()
        {
            base.FinalizeInit();
            ReRegisterAll();
        }

        public override void LoadedGame()
        {
            base.LoadedGame();
            ReRegisterAll();
        }

        public override void StartedNewGame()
        {
            base.StartedNewGame();
            // Keep list; new games start empty unless somehow prefilled
            ReRegisterAll();
        }

        public static GameComponent_CustomColorDefs GetOrCreate()
        {
            if (Current.Game == null)
                return null;

            var comp = Current.Game.GetComponent<GameComponent_CustomColorDefs>();
            if (comp != null)
                return comp;

            comp = new GameComponent_CustomColorDefs(Current.Game);
            Current.Game.components.Add(comp);
            return comp;
        }

        /// <summary>
        /// Return existing or create+persist a ColorDef for this RGB (alpha forced to 1).
        /// </summary>
        public ColorDef GetOrCreateColorDef(Color color)
        {
            Color safe = new Color(color.r, color.g, color.b, 1f);
            string hex = ColorUtility.ToHtmlStringRGB(safe);
            string defName = "RICS_Custom_" + hex;

            // Already in DefDatabase (this session or re-registered from save)
            var existing = DefDatabase<ColorDef>.GetNamedSilentFail(defName);
            if (existing != null)
            {
                EnsureTracked(defName, existing.label, safe);
                return existing;
            }

            // Prefer official ColorDef if nearly identical (no need for custom def)
            ColorDef closest = null;
            float best = float.MaxValue;
            foreach (var d in DefDatabase<ColorDef>.AllDefs)
            {
                if (d == null) continue;
                float dist = ColorDistance(d.color, safe);
                if (dist < best)
                {
                    best = dist;
                    closest = d;
                }
            }
            if (closest != null && best < 0.02f)
                return closest;

            // New custom def — register + persist
            var custom = new ColorDef
            {
                defName = defName,
                label = "#" + hex,
                description = "Custom favorite color set by RICS viewer",
                color = safe,
                colorType = ColorType.Misc,
                displayOrder = 9999
            };

            try
            {
                DefDatabase<ColorDef>.Add(custom);
            }
            catch (Exception ex)
            {
                Logger.Warning($"[CustomColor] DefDatabase.Add failed for {defName}: {ex.Message}");
            }

            EnsureTracked(defName, custom.label, safe);
            return custom;
        }

        private void EnsureTracked(string defName, string label, Color safe)
        {
            if (customColors == null)
                customColors = new List<SavedCustomColor>();

            for (int i = 0; i < customColors.Count; i++)
            {
                if (string.Equals(customColors[i].defName, defName, StringComparison.OrdinalIgnoreCase))
                {
                    customColors[i].label = label;
                    customColors[i].r = safe.r;
                    customColors[i].g = safe.g;
                    customColors[i].b = safe.b;
                    return;
                }
            }

            customColors.Add(new SavedCustomColor
            {
                defName = defName,
                label = label ?? defName,
                r = safe.r,
                g = safe.g,
                b = safe.b
            });
        }

        public void ReRegisterAll()
        {
            if (customColors == null || customColors.Count == 0)
                return;

            int added = 0;
            foreach (var entry in customColors)
            {
                if (entry == null || string.IsNullOrEmpty(entry.defName))
                    continue;

                if (DefDatabase<ColorDef>.GetNamedSilentFail(entry.defName) != null)
                    continue;

                var def = new ColorDef
                {
                    defName = entry.defName,
                    label = string.IsNullOrEmpty(entry.label) ? entry.defName : entry.label,
                    description = "Custom favorite color set by RICS viewer (restored from save)",
                    color = entry.ToColor(),
                    colorType = ColorType.Misc,
                    displayOrder = 9999
                };

                try
                {
                    DefDatabase<ColorDef>.Add(def);
                    added++;
                }
                catch (Exception ex)
                {
                    Logger.Warning($"[CustomColor] Re-register failed for {entry.defName}: {ex.Message}");
                }
            }

            if (added > 0)
                Logger.Message($"[CustomColor] Re-registered {added} custom favorite ColorDef(s) from save");
        }

        private static float ColorDistance(Color a, Color b)
        {
            float rDiff = a.r - b.r;
            float gDiff = a.g - b.g;
            float bDiff = a.b - b.b;
            return Mathf.Sqrt(rDiff * rDiff + gDiff * gDiff + bDiff * bDiff);
        }
    }
}
