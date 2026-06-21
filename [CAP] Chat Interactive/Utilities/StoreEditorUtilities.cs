// StoreEditorUtilities.cs
// Copyright (c) Captolamia
// This file is part of CAP Chat Interactive (RICS - Rimworld Interactive Chat Services).
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

/*
 * REFACTOR NOTE (2026-06-19):
 * This static utility class was created to reduce the size of Dialog_StoreEditor.cs
 * (which had grown to >80k bytes and become difficult to maintain).
 * 
 * Design goals for safe incremental extraction:
 * - Pure drawing / calculation helpers that do not directly mutate Dialog_StoreEditor instance state
 *   are moved here.
 * - Methods that need to trigger instance actions (e.g. bulk quantity changes, saves) return a
 *   simple signal (bool clicked) so the Dialog retains full control over when and how state changes.
 * - All existing behavior, sounds, tooltips, fallbacks, and translation keys are preserved exactly.
 * - Future extractions (mixed checkboxes, price formatting, category sorting helpers, etc.) can be
 *   added here following the same pattern.
 * 
 * This keeps the main Dialog class focused on orchestration and RimWorld Window lifecycle while
 * making the helper logic reusable and easier to unit-test or extend.
 */

using CAP_ChatInteractive.Store;
using RimWorld;
using System;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace CAP_ChatInteractive
{
    /// <summary>
    /// Static helper methods for the Store Editor dialog and related UI.
    /// Extracted from Dialog_StoreEditor to keep that class maintainable.
    /// All methods are thread-safe for UI drawing (called on main thread only).
    /// </summary>
    public static class StoreEditorUtilities
    {
        /// <summary>
        /// Draws a single quantity preset icon (Stack1 / Stack3 / Stack5 style) at the given position.
        /// 
        /// WHY extracted: This is a pure drawing + input helper with no dependency on Dialog_StoreEditor
        /// instance fields. It only needs position, stacks count, icon resource name, and tooltip key.
        /// 
        /// Returns true if the invisible button was clicked this frame. The caller is responsible
        /// for performing the actual quantity change (SetAllVisibleItemsQuantityLimit) so that
        /// all side-effects (sound already played here, save, message, FilterItems refresh) stay
        /// inside the Dialog where they have access to filteredItems and other state.
        /// 
        /// Preserves original behavior:
        /// - Hover highlight
        /// - Icon or fallback text button
        /// - Click sound (Click)
        /// - Tooltip via translation key
        /// </summary>
        public static bool DrawQuantityPresetIcon(float x, float y, int stacks, string iconName, string tooltipKey)
        {
            float iconSize = 24f;
            Rect iconRect = new Rect(x, y + 3f, iconSize, iconSize);

            Texture2D iconTex = ContentFinder<Texture2D>.Get($"UI/Icons/{iconName}", false);

            if (iconTex == null)
            {
                Log.Warning($"[CAP] Could not load quantity icon: UI/Icons/{iconName}");
                // Fallback: draw number as button text (original behavior)
                if (Widgets.ButtonText(iconRect, $"{stacks}×"))
                {
                    SoundDefOf.Click.PlayOneShotOnCamera();
                    return true;
                }
                return false;
            }

            // Hover effect (original)
            if (Mouse.IsOver(iconRect))
            {
                Widgets.DrawHighlight(iconRect);
            }

            Widgets.DrawTextureFitted(iconRect, iconTex, 1f);

            if (Widgets.ButtonInvisible(iconRect))
            {
                SoundDefOf.Click.PlayOneShotOnCamera();  // Optional: feedback (kept from original)
                return true;
            }

            TooltipHandler.TipRegion(iconRect, tooltipKey.Translate());
            return false;
        }



        // =====================================================================
        // FUTURE EXTRACTION CANDIDATES (to be moved incrementally in later passes)
        // =====================================================================
        //
        // - DrawMixedCheckbox(Rect rect, bool? state, Action<bool> onChanged)
        //   (Currently referenced in bulk row; needs to be located/confirmed in the truncated section)
        //
        // - General price formatting / reset logic helpers
        // - Category / ModSource filtering helpers that do not require the full Dialog instance
        // - Bulk enable/disable by predicate (can become static if we pass the collection + save callback)
        //
        // Each new helper will follow the same safety rules:
        //   • No direct mutation of Dialog_StoreEditor fields
        //   • Return values or Action delegates for any state-changing side effects
        //   • Full preservation of sounds, tooltips, translations, and edge-case handling
        // =====================================================================
    }
}