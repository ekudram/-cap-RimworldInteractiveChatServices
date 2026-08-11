// LootBoxComponent.cs
// Copyright (c) Captolamia
// This file is part of CAP Chat Interactive (RICS).
// Licensed under the GNU Affero General Public License v3.0 or later.
// See LICENSE.txt in the project root for full license text.
//
// Daily lootbox grants and per-viewer inventory (save-backed).
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

        public LootBoxComponent(Game game) { }

        public override void GameComponentTick()
        {
            // ~6.67 minutes (20000 ticks)
            if (Find.TickManager.TicksGame % 20000 != 0)
                return;

            EnsureCollections();

            if (todayFileTime != 0)
            {
                var stored = DateTime.FromFileTime(todayFileTime);
                if (stored.DayOfYear == DateTime.Now.DayOfYear)
                    return;
            }

            ViewersWhoHaveReceivedLootboxesToday = new List<string>();
            today = DateTime.Now;
            todayFileTime = today.ToFileTime();
        }

        public void ProcessViewerMessage(ChatMessageWrapper message)
        {
            if (message == null)
                return;

            var viewer = Viewers.GetViewer(message);
            if (viewer == null || string.IsNullOrEmpty(viewer.Username))
                return;

            string username = viewer.Username.ToLowerInvariant();
            if (IsViewerOwedLootboxesToday(username))
                AwardViewerDailyLootboxes(username);
        }

        public void WelcomeMessage(string username)
        {
            var messageService = CAPChatInteractiveMod.Instance?.TwitchService;
            if (messageService == null || string.IsNullOrEmpty(username))
                return;

            int count = HowManyLootboxesDoesViewerHave(username);
            messageService.SendMessage(
                $"@{username} Welcome to the stream! You currently have {count} Lootbox(es) to open. Use !openlootbox");
        }

        public void AwardViewerDailyLootboxes(string username)
        {
            var settings = CAPChatInteractiveMod.Instance?.Settings?.GlobalSettings;
            if (settings == null || string.IsNullOrEmpty(username))
                return;

            EnsureCollections();
            ViewersWhoHaveReceivedLootboxesToday.Add(username);
            LogViewerLastSeen(username);
            GiveViewerLootbox(username, settings.LootBoxesPerDay);

            if (settings.LootBoxShowWelcomeMessage)
                WelcomeMessage(username);
        }

        public void GiveViewerLootbox(string username, int amount = 1)
        {
            if (string.IsNullOrEmpty(username) || amount == 0)
                return;

            EnsureCollections();
            if (ViewersLootboxes.TryGetValue(username, out int current))
                ViewersLootboxes[username] = current + amount;
            else
                ViewersLootboxes[username] = amount;
        }

        private bool IsViewerOwedLootboxesToday(string username)
        {
            EnsureCollections();
            return !ViewersWhoHaveReceivedLootboxesToday.Contains(username)
                   && IsViewerOwedLootboxesLookup(username);
        }

        private bool IsViewerOwedLootboxesLookup(string username)
        {
            EnsureCollections();
            return !IsViewerInLastSeenList(username)
                   || ViewerLastSeenAt(username).DayOfYear != DateTime.Now.DayOfYear;
        }

        public void LogViewerLastSeen(string username)
        {
            if (string.IsNullOrEmpty(username))
                return;

            EnsureCollections();
            ViewersLastSeenDate[username] = DateTime.Now.ToFileTime();
        }

        public bool IsViewerInLastSeenList(string username) =>
            !string.IsNullOrEmpty(username)
            && ViewersLastSeenDate != null
            && ViewersLastSeenDate.ContainsKey(username);

        private DateTime ViewerLastSeenAt(string username) =>
            DateTime.FromFileTime(ViewersLastSeenDate[username]);

        public bool DoesViewerHaveLootboxes(string username) =>
            HowManyLootboxesDoesViewerHave(username) > 0;

        public int HowManyLootboxesDoesViewerHave(string username)
        {
            if (string.IsNullOrEmpty(username) || ViewersLootboxes == null)
                return 0;
            return ViewersLootboxes.TryGetValue(username, out int n) ? n : 0;
        }

        public override void ExposeData()
        {
            Scribe_Collections.Look(ref ViewersWhoHaveReceivedLootboxesToday, "ViewersWhoHaveReceivedLootboxesToday", LookMode.Value);
            Scribe_Collections.Look(ref ViewersLastSeenDate, "ViewersLastSeenDate", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref ViewersLootboxes, "ViewersLootboxes", LookMode.Value, LookMode.Value);
            Scribe_Values.Look(ref todayFileTime, "todayFileTime");

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
                EnsureCollections();
        }

        private void EnsureCollections()
        {
            if (ViewersWhoHaveReceivedLootboxesToday == null)
                ViewersWhoHaveReceivedLootboxesToday = new List<string>();
            if (ViewersLastSeenDate == null)
                ViewersLastSeenDate = new Dictionary<string, long>();
            if (ViewersLootboxes == null)
                ViewersLootboxes = new Dictionary<string, int>();
        }
    }
}
