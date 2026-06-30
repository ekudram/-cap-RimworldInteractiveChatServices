// TabDrawer_AI.cs
// Copyright (c) Captolamia 
// This file is part of CAP Chat Interactive.  Aka RICS, Rimworld Interactive Chat Service
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
// A game component that handles periodic tasks such as awarding coins to active viewers and managing storyteller ticks.
// Uses an efficient tick system to minimize performance impact.

/// <summary>
/// This file creates the Window that is used to change the AI Chat Bot Settings.
/// </summary>


using CAP_ChatInteractive;
using RimWorld;
using System;
using UnityEngine;
using Verse;
using ColorLibrary = CAP_ChatInteractive.ColorLibrary;

namespace _CAP__Chat_Interactive
{
    public static class TabDrawer_AI
    {
        private static Vector2 _scrollPosition = Vector2.zero;

        public static void Draw(Rect region)
        {
            var settings = CAPChatInteractiveMod.Instance?.Settings?.GlobalSettings;
            if (settings == null)
            {
                Widgets.Label(region, "Settings not loaded.");
                return;
            }

            var view = new Rect(0f, 0f, region.width - 16f, 1600f);

            Widgets.BeginScrollView(region, ref _scrollPosition, view);
            var listing = new Listing_Standard();
            listing.Begin(view);

            // Header
            Text.Font = GameFont.Medium;
            GUI.color = ColorLibrary.HeaderAccent;
            listing.Label("RICS.AI.Title".Translate());
            Text.Font = GameFont.Small;
            GUI.color = Color.white;

            listing.GapLine();

            // Cleanup button always visible (useful even if AI feature is off)
            Text.Font = GameFont.Tiny;
            GUI.color = Color.gray;
            listing.Label("RICS.AI.ClearLeftoverLabel".Translate());
            GUI.color = Color.white;
            Text.Font = GameFont.Small;

            if (listing.ButtonText("RICS.AI.ClearLeftoverButton".Translate()))
            {
                try
                {
                    int count = CAP_ChatInteractive.AI.AIChatBotService.CleanupLeftoverAICommandFiles();
                    Messages.Message($"Cleared {count} leftover AI command/event files.", MessageTypeDefOf.PositiveEvent);
                }
                catch (Exception ex)
                {
                    Messages.Message($"Error clearing AI files: {ex.Message}", MessageTypeDefOf.RejectInput);
                }
            }

            listing.GapLine(8f);

            // Master toggle
            listing.CheckboxLabeled("RICS.AI.Enable".Translate(), ref settings.AIChatBotActive);

            if (settings.AIChatBotActive)
            {
                listing.Gap(8f);

                // Help / setup description
                Text.Font = GameFont.Tiny;
                GUI.color = Color.gray;
                listing.Label("RICS.AI.Description".Translate());
                GUI.color = Color.white;
                Text.Font = GameFont.Small;

                listing.GapLine(4f);

                // Core endpoints
                listing.Label("RICS.AI.ListenerURL".Translate());
                settings.AIChatBotEndpoint = listing.TextEntry(settings.AIChatBotEndpoint ?? "http://127.0.0.1:17888").Trim();

                listing.Label("RICS.AI.BotResponseURL".Translate());
                settings.AIChatBotListenUrl = listing.TextEntry(settings.AIChatBotListenUrl ?? "http://127.0.0.1:5000/chat").Trim();

                listing.Label("RICS.AI.BotName".Translate());
                settings.AIChatBotName = listing.TextEntry(settings.AIChatBotName ?? "Masie Lamia");

                listing.Gap(10f);

                // Game state & context
                listing.CheckboxLabeled("RICS.AI.SendGameState".Translate(), ref settings.AIChatBotSendGameState);
                listing.CheckboxLabeled("RICS.AI.SendChatHistory".Translate(), ref settings.AIChatBotSendChatHistory);

                listing.Gap(6f);

                // Interval slider
                listing.Label(string.Format("RICS.AI.UpdateInterval".Translate() + ": {0} min", settings.AIChatBotGameStateUpdateIntervalMinutes));
                settings.AIChatBotGameStateUpdateIntervalMinutes = (int)listing.Slider(settings.AIChatBotGameStateUpdateIntervalMinutes, 0, 60);

                // Timeout
                listing.Label(string.Format("RICS.AI.Timeout".Translate() + ": {0} ms", settings.AIChatBotTimeoutMs));
                settings.AIChatBotTimeoutMs = (int)listing.Slider(settings.AIChatBotTimeoutMs, 1000, 30000);

                listing.Gap(6f);

                // Push endpoint
                listing.Label("RICS.AI.PushEndpoint".Translate());
                settings.AIChatBotGameStatePushEndpoint = listing.TextEntry(settings.AIChatBotGameStatePushEndpoint ?? "http://127.0.0.1:5000/gamestate_update").Trim();

                listing.Gap(10f);

                // Command execution (sensitive)
                listing.CheckboxLabeled("RICS.AI.CanExecuteCommands".Translate(), ref settings.AIChatBotCanExecuteCommands);

                Text.Font = GameFont.Tiny;
                GUI.color = Color.yellow;
                listing.Label("RICS.AI.WarningExecute".Translate());
                GUI.color = Color.white;
                Text.Font = GameFont.Small;

                listing.Gap(12f);

                // Buttons row
                if (listing.ButtonText("RICS.AI.TestButton".Translate()))
                {
                    TestAIConnection();
                }

                listing.Gap(4f);

                if (listing.ButtonText("RICS.AI.ResetButton".Translate()))
                {
                    settings.AIChatBotEndpoint = "http://127.0.0.1:17888";
                    settings.AIChatBotListenUrl = "http://127.0.0.1:5000/chat";
                    settings.AIChatBotName = "Masie Lamia";
                    settings.AIChatBotGameStatePushEndpoint = "http://127.0.0.1:5000/gamestate_update";
                    settings.AIChatBotTimeoutMs = 8000;
                    settings.AIChatBotGameStateUpdateIntervalMinutes = 15;
                    settings.AIChatBotSendGameState = true;
                    settings.AIChatBotSendChatHistory = true;
                    settings.AIChatBotCanExecuteCommands = true;

                    Messages.Message("AI Chatbot settings reset to local Python defaults.", MessageTypeDefOf.PositiveEvent);
                }

                listing.Gap(8f);
                Text.Font = GameFont.Tiny;
                GUI.color = Color.gray;
                listing.Label("RICS.AI.SetupHelp".Translate());
                GUI.color = Color.white;
                Text.Font = GameFont.Small;
            }

            listing.End();
            Widgets.EndScrollView();
        }

        private static void TestAIConnection()
        {
            var s = CAPChatInteractiveMod.Instance?.Settings?.GlobalSettings;
            if (s == null) return;

            try
            {
                using (var client = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(5) })
                {
                    var url = s.AIChatBotEndpoint.TrimEnd('/') + "/gamestate";
                    var response = client.GetAsync(url).GetAwaiter().GetResult();

                    if (response.IsSuccessStatusCode)
                        Messages.Message($"✅ RICS AI listener responding at {url}", MessageTypeDefOf.PositiveEvent);
                    else
                        Messages.Message($"⚠️ Connected but got status {response.StatusCode}", MessageTypeDefOf.CautionInput);
                }
            }
            catch (Exception ex)
            {
                Messages.Message($"❌ Could not connect to RICS AI listener:\n{ex.Message}", MessageTypeDefOf.NegativeEvent);
            }
        }
    }
}