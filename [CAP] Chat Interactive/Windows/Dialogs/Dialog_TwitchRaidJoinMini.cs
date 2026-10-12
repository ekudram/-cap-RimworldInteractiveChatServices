// File: Dialog_TwitchRaidJoinMini.cs
//
// Copyright (c) Captolamia
// This file is part of CAP Chat Interactive (RICS).
// Licensed under the GNU Affero General Public License v3.0 or later.
// See LICENSE.txt in the project root for full license text.
//

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
            Widgets.Label(new Rect(0f, 6f, inRect.width, 26f), "RICS.TwitchRaidJoin.Title".Translate());

            // Subtitle
            Text.Font = GameFont.Small;
            GUI.color = Color.white;
            Widgets.Label(new Rect(0f, 32f, inRect.width, 20f), "RICS.TwitchRaidJoin.RaiderInfo".Translate(raiderName, viewerCount));

            // Countdown (prominent, color coded)
            Text.Font = GameFont.Medium;
            GUI.color = timeLeft > 90f ? ColorLibrary.Success : (timeLeft > 30f ? Color.yellow : ColorLibrary.Danger);
            // Whole seconds in C#. Keyed XML must stay "{0}" — a colon is a grammar role and drops the number.
            int secondsLeft = Mathf.CeilToInt(timeLeft);
            Widgets.Label(new Rect(0f, 55f, inRect.width, 28f), "RICS.TwitchRaidJoin.TimeLeft".Translate(secondsLeft));

            // Joiner count
            Text.Font = GameFont.Small;
            GUI.color = Color.white;
            int joined = GetJoinedCount();
            Widgets.Label(new Rect(0f, 85f, inRect.width, 22f), "RICS.TwitchRaidJoin.RaidersSoFar".Translate(joined));

            // Buttons row
            float elapsed = 180f - timeLeft;
            bool buttonEnabled = elapsed >= ButtonDelay;

            Rect startRect = new Rect(8f, inRect.height - 28f, 145f, 30f);
            Rect expandRect = new Rect(inRect.width - 153f, inRect.height - 28f, 145f, 30f);

            GUI.color = buttonEnabled ? ColorLibrary.Danger : Color.gray;
            if (Widgets.ButtonText(startRect, "RICS.TwitchRaidJoin.StartRaidNow".Translate(), active: buttonEnabled))
            {
                if (buttonEnabled)
                    TriggerAndClose();
            }

            GUI.color = ColorLibrary.Info;
            if (Widgets.ButtonText(expandRect, "RICS.TwitchRaidJoin.Expand".Translate()))
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