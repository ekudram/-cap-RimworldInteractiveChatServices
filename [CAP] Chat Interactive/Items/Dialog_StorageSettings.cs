// File: Dialog_StorageSettings.cs
//
// Copyright (c) Captolamia
// This file is part of CAP Chat Interactive (RICS).
// Licensed under the GNU Affero General Public License v3.0 or later.
// See LICENSE.txt in the project root for full license text.
//
// This file defines a dialog window that allows the player to configure the storage settings of a Rimazon locker.

using RimWorld;
using System;
using UnityEngine;
using Verse;

namespace CAP_ChatInteractive
{
    public class Dialog_StorageSettings : Window
    {
        private Building_RimazonLocker locker;
        private ThingFilterUI.UIState uiState = new ThingFilterUI.UIState();

        public Dialog_StorageSettings(Building_RimazonLocker locker)
        {
            this.locker = locker;
            this.forcePause = true;
            this.doCloseX = true;
            this.absorbInputAroundWindow = true;
            this.closeOnClickedOutside = true;
            this.closeOnAccept = false;
        }

        public override Vector2 InitialSize => new Vector2(420f, 480f);

        public override void DoWindowContents(Rect inRect)
        {
            try
            {
                if (locker?.settings == null)
                    locker.GetStoreSettings();

                if (locker?.settings == null || locker.settings.filter == null)
                {
                    Widgets.Label(inRect, "RICS.Locker.StorageSettingsUnavailable".Translate());
                    return;
                }

                GUI.color = Color.white;
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.UpperLeft;

                Rect mainRect = new Rect(0f, 0f, inRect.width, inRect.height).ContractedBy(10f);

                DrawPriority(new Rect(mainRect.x, mainRect.y, mainRect.width, 30f), locker.settings);

                Rect filterRect = new Rect(mainRect.x, mainRect.y + 35f, mainRect.width, mainRect.height - 35f);
                ThingFilter parentFilter = locker.def?.building?.defaultStorageSettings?.filter ?? locker.settings.filter;
                DrawFilter(filterRect, locker.settings.filter, parentFilter);
            }
            catch (Exception ex)
            {
                Logger.Error($"Error in storage settings window: {ex}");
            }
        }

        private void DrawPriority(Rect rect, StorageSettings settings)
        {
            // Priority button removed on purpose: lockers are chat delivery, not stockpiles.
            Widgets.Label(rect.LeftHalf(), "RICS.Priority".Translate() + ":");
            Widgets.Label(rect.RightHalf(), "RICS.Unstored".Translate());
        }

        private void DrawFilter(Rect rect, ThingFilter filter, ThingFilter parentFilter)
        {
            ThingFilterUI.DoThingFilterConfigWindow(
                rect: rect,
                state: uiState,
                filter: filter,
                parentFilter: parentFilter,
                openMask: 1,
                forceHiddenDefs: null,
                forceHiddenFilters: null,
                forceHideHitPointsConfig: false,
                forceHideQualityConfig: false,
                showMentalBreakChanceRange: false,
                suppressSmallVolumeTags: null,
                map: Find.CurrentMap
            );
        }
    }
}
