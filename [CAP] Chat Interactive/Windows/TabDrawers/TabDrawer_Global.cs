// TabDrawer_Global.cs
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
// Draws the Global Settings tab in the mod settings window
using CAP_ChatInteractive;
using RimWorld;
using System;
using UnityEngine;
using Verse;
using ColorLibrary = CAP_ChatInteractive.ColorLibrary;

namespace _CAP__Chat_Interactive
{
    public static class TabDrawer_Global
    {
        private static Vector2 _scrollPosition = Vector2.zero;

        public static void Draw(Rect region)
        {
            var settings = CAPChatInteractiveMod.Instance.Settings.GlobalSettings;
            var view = new Rect(0f, 0f, region.width - 16f, 1100f);

            Widgets.BeginScrollView(region, ref _scrollPosition, view);
            var listing = new Listing_Standard();
            listing.Begin(view);

            // Debug and Logging Section
            Text.Font = GameFont.Medium;
            GUI.color = ColorLibrary.HeaderAccent;
            listing.Label("RICS.Global.DebugHeader".Translate());
            Text.Font = GameFont.Small;
            GUI.color = Color.white;

            listing.GapLine(6f);
            listing.CheckboxLabeled("RICS.Global.EnableDebugLogging".Translate(), ref settings.EnableDebugLogging);
            listing.CheckboxLabeled("RICS.Global.LogAllChatMessages".Translate(), ref settings.LogAllMessages);

            // Cooldown setting with slider
            listing.Label(string.Format("RICS.Global.MessageCooldown".Translate(), settings.MessageCooldownSeconds));
            settings.MessageCooldownSeconds = (int)listing.Slider(settings.MessageCooldownSeconds, 1, 10);

            listing.Gap(24f);

            // Quick Status Section
            Text.Font = GameFont.Medium;
            GUI.color = ColorLibrary.HeaderAccent;
            listing.Label("RICS.Global.QuickStatusHeader".Translate());
            Text.Font = GameFont.Small;
            GUI.color = Color.white;

            listing.GapLine(6f);

            var twitchStatus = CAPChatInteractiveMod.Instance.Settings.TwitchSettings.IsConnected ? "RICS.Global.Connected".Translate() : "RICS.Global.Disconnected".Translate();
            var youtubeStatus = CAPChatInteractiveMod.Instance.Settings.YouTubeSettings.IsConnected ? "RICS.Global.Connected".Translate() : "RICS.Global.Disconnected".Translate();

            listing.Label(string.Format("RICS.Global.TwitchStatus".Translate(), twitchStatus));
            listing.Label(string.Format("RICS.Global.YouTubeStatus".Translate(), youtubeStatus));

            listing.Gap(24f);

            // Command Prefixes Section
            Text.Font = GameFont.Medium;
            GUI.color = ColorLibrary.HeaderAccent;
            listing.Label("RICS.Global.CommandPrefixesHeader".Translate());
            Text.Font = GameFont.Small;
            GUI.color = Color.white;
            listing.GapLine(6f);

            listing.Label("RICS.Global.CommandPrefixDescription".Translate());
            Text.Font = GameFont.Tiny;
            GUI.color = ColorLibrary.LightText;
            listing.Label("RICS.Global.PrefixRestrictions".Translate());
            Text.Font = GameFont.Small;
            GUI.color = Color.white;

            string commandPrefix = listing.TextEntryLabeled("RICS.Global.CommandPrefixLabel".Translate(), settings.Prefix);
            if (IsValidPrefix(commandPrefix))
            {
                settings.Prefix = commandPrefix;
            }
            else if (!string.IsNullOrEmpty(commandPrefix))
            {
                GUI.color = Verse.ColorLibrary.RedReadable;
                listing.Label("RICS.Global.InvalidPrefix".Translate());
                GUI.color = Color.white;
            }

            listing.Gap(12f);

            listing.Label("RICS.Global.PurchasePrefixDescription".Translate());
            Text.Font = GameFont.Tiny;
            listing.Label("RICS.Global.PrefixRestrictions".Translate());
            Text.Font = GameFont.Small;

            string buyPrefix = listing.TextEntryLabeled("RICS.Global.PurchasePrefixLabel".Translate(), settings.BuyPrefix);
            if (IsValidPrefix(buyPrefix))
            {
                settings.BuyPrefix = buyPrefix;
            }
            else if (!string.IsNullOrEmpty(buyPrefix))
            {
                GUI.color = Verse.ColorLibrary.RedReadable;
                listing.Label("RICS.Global.InvalidPrefix".Translate());
                GUI.color = Color.white;
            }

            listing.Gap(24f);

            // Price List URL setting
            listing.Label("RICS.Global.PriceListUrlDescription".Translate());
            string newPriceListUrl = listing.TextEntryLabeled("RICS.Global.PriceListUrlLabel".Translate(), settings.priceListUrl);
            if (!string.IsNullOrEmpty(newPriceListUrl))
            {
                settings.priceListUrl = newPriceListUrl;
            }

            listing.Gap(24f);

            // === AI Chatbot Integration ===
            Text.Font = GameFont.Medium;
            GUI.color = ColorLibrary.HeaderAccent;
            listing.Label("AI Chatbot Integration");
            Text.Font = GameFont.Small;
            GUI.color = Color.white;

            listing.GapLine(6f);

            // TODO: Add validation for URLs and display warnings if they don't look correct <-- this is important to help users avoid common mistakes when setting up the AI integration 
            // TODO: Add clarification message that RICS does not provide the AI chatbot itself, just the integration points for users to connect their own bots
            // Note:  Must be in a game (have game loaded) for AI connection to work <-- consider adding a warning message or disabling the test connection button if not in a game to prevent confusion and error messages when users try to test the connection without having a game loaded.

            // Semantic / theme colors from RICS Color Library - consider using these for section headers and important labels in the settings UI to create a more visually engaging and organized layout that ties into the overall RICS branding and design language.
            //public static readonly Color HeaderAccent = new Color(1.0f, 0.5f, 0.1f);   // Orange - headers
            //public static readonly Color SubHeader = new Color(0.529f, 0.808f, 0.922f); // SkyBlue - sub-headers
            //public static readonly Color PrimaryAction = new Color(0.2f, 0.4f, 0.8f);   // Blue
            //public static readonly Color Success = new Color(0.2f, 0.8f, 0.2f);
            //public static readonly Color Warning = new Color(1.0f, 0.75f, 0.2f);  // Yellow-Orange  Maybe more yellow?
            //public static readonly Color Danger = new Color(0.9f, 0.1f, 0.1f);
            //public static readonly Color Info = new Color(0.3f, 0.65f, 0.95f); // Informational / neutral blue for status messages, tips, secondary labels and info sections. Distinct from PrimaryAction (darker action blue) and SubHeader (lighter sky blue) while staying readable on RimWorld dark UI.

            // Remove comments after implementing the above TODOs to clean up the code and avoid confusion for future maintainers. The comments are meant to guide the implementation of the AI chatbot settings section, but once it's implemented and the UI is polished, the comments should be removed to keep the codebase clean and maintainable.


            listing.CheckboxLabeled("Enable AI Chatbot", ref settings.AIChatBotActive);

            if (settings.AIChatBotActive)
            {
                listing.GapLine(6f);
                listing.Label("To use the AI chatbot, set up your own bot that can receive game state data from RICS and respond to chat messages. Then enter the appropriate URLs below to connect RICS to your bot.");
                listing.Label("RICS will send game state data to the AIChatBotEndpoint URL, and your bot should respond to these requests with chat messages that RICS will relay back to Twitch/YouTube chat. When you use the !ricsaichatbot command in chat, RICS will send the message content and optionally recent chat history and game state to the AIChatBotListenUrl, and display the response in chat as if it came from the bot.");
                listing.Label("This is experimental and advanced functionality that requires you to set up and host your own AI chatbot that can interface with RICS. It is not required for basic RICS functionality and is intended for users who want to create a more interactive experience by connecting an AI bot to their stream.");

                listing.GapLine(6f);

                listing.Label("RICS Listener URL (Python bot calls this for game state):");
                settings.AIChatBotEndpoint = listing.TextEntry(settings.AIChatBotEndpoint);

                listing.Label("AI Bot URL (RICS calls this when using !ricsaichatbot):");
                settings.AIChatBotListenUrl = listing.TextEntry(settings.AIChatBotListenUrl);

                listing.Label("Bot Display Name (shown in chat):");
                settings.AIChatBotName = listing.TextEntry(settings.AIChatBotName);

                listing.CheckboxLabeled("Include Game State in prompts", ref settings.AIChatBotSendGameState);
                listing.CheckboxLabeled("Include Recent Chat History", ref settings.AIChatBotSendChatHistory);

                listing.Gap(8f);
                if (listing.ButtonText("Test Connection to AI Bot"))
                {
                    TestAIConnection();
                }

                if (listing.ButtonText("Reset AI Settings to Defaults"))
                {
                    settings.AIChatBotEndpoint = "http://127.0.0.1:17888";
                    settings.AIChatBotListenUrl = "http://127.0.0.1:5000/chat";
                    settings.AIChatBotName = "AI Storyteller";
                    Messages.Message("AI Chatbot settings reset to defaults.", MessageTypeDefOf.PositiveEvent);
                }
            }

            listing.Gap(24f);
            listing.End();
            Widgets.EndScrollView();
        }

        private static bool IsValidPrefix(string prefix)
        {
            if (string.IsNullOrEmpty(prefix)) return false;
            if (prefix.Contains(" ")) return false;
            if (prefix.StartsWith("/") || prefix.StartsWith(".") || prefix.StartsWith("\\")) return false;
            return true;
        }


        /// <summary>
        /// Sends a test request to the AI chatbot endpoint to verify connectivity and displays the result in a message.
        /// </summary>
        private static void TestAIConnection()
        {
            var settings = CAPChatInteractiveMod.Instance?.Settings?.GlobalSettings;
            if (settings == null) return;

            try
            {
                using (var client = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(5) })
                {
                    var url = settings.AIChatBotEndpoint.TrimEnd('/') + "/gamestate";
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