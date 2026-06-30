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
    }
}