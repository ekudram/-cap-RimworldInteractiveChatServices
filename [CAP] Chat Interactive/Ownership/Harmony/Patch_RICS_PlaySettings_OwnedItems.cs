// Patch_RICS_PlaySettings_OwnedItems.cs
// Copyright (c) Captolamia
// Part of CAP Chat Interactive (RICS) — AGPLv3
//
// Adds a Play Settings (bottom-right display toggles) button that opens the RICS owned-items browser.
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace CAP_ChatInteractive.Ownership.Harmony
{
    [HarmonyPatch(typeof(PlaySettings), "DoPlaySettingsGlobalControls")]
    public static class Patch_RICS_PlaySettings_OwnedItems
    {
        private static Texture2D _icon;

        private static Texture2D Icon
        {
            get
            {
                if (_icon == null)
                {
                    _icon = ContentFinder<Texture2D>.Get("UI/RICS_OwnedItemsIcon", reportFailure: false)
                            ?? ContentFinder<Texture2D>.Get("UI/Commands/ForbidOff", reportFailure: false)
                            ?? TexCommand.ForbidOff;
                }
                return _icon;
            }
        }

        [HarmonyPostfix]
        public static void Postfix(WidgetRow row, bool worldView)
        {
            try
            {
                if (worldView || row == null)
                    return;

                // Always show the icon; window explains if ownership is off.
                if (!row.ButtonIcon(Icon, "RICS.Ownership.Browser.Tooltip".Translate()))
                    return;

                Find.WindowStack.Add(new Dialog_RICS_OwnedItemsBrowser());
            }
            catch (System.Exception ex)
            {
                Logger.Warning($"[RICS Ownership] PlaySettings button failed: {ex.Message}");
            }
        }
    }
}
