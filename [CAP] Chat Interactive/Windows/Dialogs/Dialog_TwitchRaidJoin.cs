// Source/RICS/Windows/Dialog_TwitchRaidJoin.cs
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

// Source/RICS/Windows/Dialog_TwitchRaidJoin.cs
// Source/RICS/Windows/Dialog_TwitchRaidJoin.cs
using CAP_ChatInteractive.Incidents;
using RimWorld;
using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace CAP_ChatInteractive.Windows
{
    public class Dialog_TwitchRaidJoin : Window
    {
        private readonly string raiderName;
        private readonly int viewerCount;
        private readonly float startTime;

        private Vector2 _scrollPosition = Vector2.zero;

        private const float WindowDuration = 45f;
        private const float ButtonDelay = 2f;

        public override Vector2 InitialSize => new Vector2(460f, 420f);

        public Dialog_TwitchRaidJoin(string raiderName, int viewerCount)
        {
            this.raiderName = raiderName;
            this.viewerCount = viewerCount;
            this.startTime = Time.time;

            this.draggable = true;
            this.closeOnClickedOutside = false;
            this.doCloseX = false;
            this.forcePause = false;
            this.focusWhenOpened = true;
            this.preventCameraMotion = false;
        }

        public override void DoWindowContents(Rect inRect)
        {
            float timeLeft = CAPChatInteractiveMod.Instance.TwitchService.GetRaidJoinTimeLeft();

            if (timeLeft <= 0f)
            {
                CloseAndTriggerRaid();
                return;
            }

            // Title - leave space for Compact button on the right
            Text.Font = GameFont.Medium;
            GUI.color = ColorLibrary.HeaderAccent;
            Widgets.Label(new Rect(0f, 10f, inRect.width - 125f, 35f), "TWITCH RAID INCOMING!");

            // Compact View button (top-right)

            GUI.color = Color.white;
            Rect compactRect = new Rect(inRect.width - 150f, 8f, 150f, 30f);
            if (Widgets.ButtonText(compactRect, "Compact View"))
            {
                Find.WindowStack.Add(new Dialog_TwitchRaidJoinMini(raiderName, viewerCount));
                Close();
            }

            // Subtitle
            Text.Font = GameFont.Small;
            GUI.color = Color.white;
            Widgets.Label(new Rect(0f, 48f, inRect.width, 25f), $"@{raiderName} raided with ~{viewerCount} viewers!");

            // Countdown
            Text.Font = GameFont.Medium;
            GUI.color = timeLeft > 60f ? ColorLibrary.Success : ColorLibrary.Danger;
            Widgets.Label(new Rect(0f, 78f, inRect.width, 32f), $"!JoinRaid in the next {timeLeft:F0} seconds!");

            var currentRaiders = GetJoinedNames();

            // Note about Twitch delay (brief, readable, normal size)
            Text.Font = GameFont.Small;
            GUI.color = Color.gray;
            Widgets.Label(new Rect(0f, 110f, inRect.width, 18f), "Note: Twitch can take up to 3 minutes for all raiders to join.");
            GUI.color = Color.white;

            // Live list header (shifted down for note)
            Text.Font = GameFont.Small;
            GUI.color = Color.white;
            Widgets.Label(new Rect(12f, 130f, inRect.width - 24f, 25f), $"Raiders so far ({currentRaiders.Count}):");

            // === SCROLLABLE LIST (height reduced to fit note) ===
            Rect listRect = new Rect(12f, 156f, inRect.width - 24f, 155f);
            Rect viewRect = new Rect(0f, 0f, listRect.width - 16f, currentRaiders.Count * 24f + 8f);

            Widgets.BeginScrollView(listRect, ref _scrollPosition, viewRect);

            Listing_Standard listing = new Listing_Standard();
            listing.Begin(viewRect);

            foreach (string name in currentRaiders)
            {
                listing.Label($"• {name}");
            }

            listing.End();

            Widgets.EndScrollView();

            // Start Raid Now button
            float elapsed = 180f - timeLeft;
            bool buttonEnabled = elapsed >= ButtonDelay;
            GUI.color = buttonEnabled ? ColorLibrary.Danger : Color.gray;

            Rect buttonRect = new Rect(inRect.width / 2 - 100f, inRect.height - 48f, 200f, 38f);

            if (Widgets.ButtonText(buttonRect, "START RAID NOW!", active: buttonEnabled))
            {
                if (buttonEnabled)
                    CloseAndTriggerRaid();
            }

            GUI.color = Color.white;
        }

        private List<string> GetJoinedNames()
        {
            return CAPChatInteractiveMod.Instance.TwitchService.GetCurrentRaidJoinList();
        }

        private int GetJoinedCount()
        {
            return GetJoinedNames().Count;
        }

        private void CloseAndTriggerRaid()
        {
            IncidentWorker_TwitchRaid.CurrentRaidUsernames.Clear();
            IncidentWorker_TwitchRaid.CurrentRaidUsernames.AddRange(GetJoinedNames());

            CAPChatInteractiveMod.Instance.TwitchService.TriggerRaidNow(raiderName, GetJoinedCount());

            Close();
        }
    }
}