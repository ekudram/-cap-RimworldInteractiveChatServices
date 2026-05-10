// Source/RICS/Windows/Dialog_TwitchRaidJoinMini.cs
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

using CAP_ChatInteractive.Incidents;
using RimWorld;
using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace CAP_ChatInteractive.Windows
{
    /// <summary>
    /// Minimalist compact view for Twitch raid join window.
    /// Shows only essential info + Start/Expand buttons. Timer and join list
    /// come from TwitchService so it stays accurate even when opened late.
    /// </summary>
    public class Dialog_TwitchRaidJoinMini : Window
    {
        private readonly string raiderName;
        private readonly int viewerCount;

        private const float ButtonDelay = 2f;

        public override Vector2 InitialSize => new Vector2(340f, 170f);

        public Dialog_TwitchRaidJoinMini(string raiderName, int viewerCount)
        {
            this.raiderName = raiderName;
            this.viewerCount = viewerCount;

            this.draggable = true;
            this.closeOnClickedOutside = false;
            this.doCloseX = false;
            this.forcePause = false;
            this.focusWhenOpened = true;
            this.preventCameraMotion = false;
        }

        public override void DoWindowContents(Rect inRect)
        {
            var service = CAPChatInteractiveMod.Instance.TwitchService;
            float timeLeft = service.GetRaidJoinTimeLeft();

            if (timeLeft <= 0f)
            {
                TriggerAndClose();
                return;
            }

            // Title
            Text.Font = GameFont.Medium;
            GUI.color = ColorLibrary.HeaderAccent;
            Widgets.Label(new Rect(0f, 6f, inRect.width, 26f), "TWITCH RAID");

            // Subtitle
            Text.Font = GameFont.Small;
            GUI.color = Color.white;
            Widgets.Label(new Rect(0f, 32f, inRect.width, 20f), $"@{raiderName} raided with ~{viewerCount} viewers");

            // Countdown (prominent, color coded)
            Text.Font = GameFont.Medium;
            GUI.color = timeLeft > 90f ? ColorLibrary.Success : (timeLeft > 30f ? Color.yellow : ColorLibrary.Danger);
            Widgets.Label(new Rect(0f, 55f, inRect.width, 28f), $"{timeLeft:F0} seconds left to join!");

            // Joiner count
            Text.Font = GameFont.Small;
            GUI.color = Color.white;
            int joined = GetJoinedCount();
            Widgets.Label(new Rect(0f, 85f, inRect.width, 22f), $"{joined} raiders have joined so far");

            // Buttons row
            float elapsed = 180f - timeLeft;
            bool buttonEnabled = elapsed >= ButtonDelay;

            Rect startRect = new Rect(8f, inRect.height - 28f, 145f, 30f);
            Rect expandRect = new Rect(inRect.width - 153f, inRect.height - 28f, 145f, 30f);

            GUI.color = buttonEnabled ? ColorLibrary.Danger : Color.gray;
            if (Widgets.ButtonText(startRect, "START RAID!", active: buttonEnabled))
            {
                if (buttonEnabled)
                    TriggerAndClose();
            }

            GUI.color = ColorLibrary.Info;
            if (Widgets.ButtonText(expandRect, "Expand"))
            {
                Find.WindowStack.Add(new Dialog_TwitchRaidJoin(raiderName, viewerCount));
                Close();
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

        private void TriggerAndClose()
        {
            IncidentWorker_TwitchRaid.CurrentRaidUsernames.Clear();
            IncidentWorker_TwitchRaid.CurrentRaidUsernames.AddRange(GetJoinedNames());

            CAPChatInteractiveMod.Instance.TwitchService.TriggerRaidNow(raiderName, GetJoinedCount());

            Close();
        }
    }
}