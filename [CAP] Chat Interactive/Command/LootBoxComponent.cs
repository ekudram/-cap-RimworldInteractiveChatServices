// LootBoxComponent.cs
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

using System;
using System.Collections.Generic;
using Verse;

namespace CAP_ChatInteractive
{
    public class LootBoxComponent : GameComponent
    {
        public DateTime today = DateTime.Now;
        public long todayFileTime;
        public List<string> ViewersWhoHaveReceivedLootboxesToday = new List<string>();
        public Dictionary<string, long> ViewersLastSeenDate = new Dictionary<string, long>();
        public Dictionary<string, int> ViewersLootboxes = new Dictionary<string, int>();

        public LootBoxComponent(Game game)
        {
            if (this.ViewersWhoHaveReceivedLootboxesToday == null)
                this.ViewersWhoHaveReceivedLootboxesToday = new List<string>();
        }

        public override void GameComponentTick()
        {
            // Check every ~6.67 minutes (20000 ticks) if it's a new day
            if (Find.TickManager.TicksGame % 20000 != 0)
                return;

            DateTime dateTime = DateTime.FromFileTime(this.todayFileTime);
            if (dateTime.DayOfYear == DateTime.Now.DayOfYear)
                return;

            // New day - reset daily tracking
            this.ViewersWhoHaveReceivedLootboxesToday = new List<string>();
            this.today = DateTime.Now;
            this.todayFileTime = this.today.ToFileTime();
        }

        public void ProcessViewerMessage(ChatMessageWrapper message)
        {
            var viewer = Viewers.GetViewer(message);
            if (this.IsViewerOwedLootboxesToday(viewer.Username.ToLower()))
                this.AwardViewerDailyLootboxes(viewer.Username.ToLower());
        }

        public void WelcomeMessage(string username)
        {
            var messageService = CAPChatInteractiveMod.Instance?.TwitchService;
            if (messageService != null)
            {
                messageService.SendMessage($"@{username} Welcome to the stream! You currently have {this.HowManyLootboxesDoesViewerHave(username)} Lootbox(es) to open. Use !openlootbox");
            }
        }

        public void AwardViewerDailyLootboxes(string username)
        {
            var settings = CAPChatInteractiveMod.Instance?.Settings?.GlobalSettings;
            if (settings == null) return;

            this.ViewersWhoHaveReceivedLootboxesToday.Add(username);
            this.LogViewerLastSeen(username);
            this.GiveViewerLootbox(username, settings.LootBoxesPerDay);

            if (settings.LootBoxShowWelcomeMessage)
                this.WelcomeMessage(username);
        }

        public void GiveViewerLootbox(string username, int amount = 1)
        {
            if (this.ViewersLootboxes.ContainsKey(username))
                this.ViewersLootboxes[username] += amount;
            else
                this.ViewersLootboxes.Add(username, amount);
        }

        private bool IsViewerOwedLootboxesToday(string username)
        {
            if (this.ViewersWhoHaveReceivedLootboxesToday == null)
                this.ViewersWhoHaveReceivedLootboxesToday = new List<string>();

            return !this.ViewersWhoHaveReceivedLootboxesToday.Contains(username) &&
                   this.IsViewerOwedLootboxesLookup(username);
        }

        private bool IsViewerOwedLootboxesLookup(string username)
        {
            if (this.ViewersLastSeenDate == null)
                this.ViewersLastSeenDate = new Dictionary<string, long>();
            if (this.ViewersLootboxes == null)
                this.ViewersLootboxes = new Dictionary<string, int>();

            return !this.IsViewerInLastSeenList(username) ||
                   this.ViewerLastSeenAt(username).DayOfYear != DateTime.Now.DayOfYear;
        }

        public void LogViewerLastSeen(string username)
        {
            if (this.ViewersLastSeenDate.ContainsKey(username))
                this.ViewersLastSeenDate[username] = DateTime.Now.ToFileTime();
            else
                this.ViewersLastSeenDate.Add(username, DateTime.Now.ToFileTime());
        }

        public bool IsViewerInLastSeenList(string username) =>
            this.ViewersLastSeenDate.ContainsKey(username);

        private DateTime ViewerLastSeenAt(string username) =>
            DateTime.FromFileTime(this.ViewersLastSeenDate[username]);

        public bool DoesViewerHaveLootboxes(string username) =>
            this.ViewersLootboxes.ContainsKey(username) && this.ViewersLootboxes[username] > 0;

        public int HowManyLootboxesDoesViewerHave(string username) =>
            this.ViewersLootboxes.ContainsKey(username) ? this.ViewersLootboxes[username] : 0;

        public override void ExposeData()
        {
            Scribe_Collections.Look(ref this.ViewersWhoHaveReceivedLootboxesToday, "ViewersWhoHaveReceivedLootboxesToday", LookMode.Value);
            Scribe_Collections.Look(ref this.ViewersLastSeenDate, "ViewersLastSeenDate", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref this.ViewersLootboxes, "ViewersLootboxes", LookMode.Value, LookMode.Value);
            Scribe_Values.Look(ref this.todayFileTime, "todayFileTime");
        }
    }
}