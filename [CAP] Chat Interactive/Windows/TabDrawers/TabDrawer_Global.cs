using CAP_ChatInteractive;
using UnityEngine;
using Verse;

namespace _CAP__Chat_Interactive
{
    public static class TabDrawer_Global
    {
        private static Vector2 _scrollPosition = Vector2.zero;

        public static void Draw(Rect region)
        {
            var settings = CAPChatInteractiveMod.Instance.Settings.GlobalSettings;
            var view = new Rect(0f, 0f, region.width - 16f, 800f);

            Widgets.BeginScrollView(region, ref _scrollPosition, view);
            var listing = new Listing_Standard();
            listing.Begin(view);

            // Debug and Logging Section
            Text.Font = GameFont.Medium;
            listing.Label("Debug & Logging");
            Text.Font = GameFont.Small;
            listing.GapLine(6f);
            listing.CheckboxLabeled("Enable Debug Logging", ref settings.EnableDebugLogging);
            listing.CheckboxLabeled("Log All Chat Messages", ref settings.LogAllMessages);

            // Cooldown setting with slider
            listing.Label($"Message Cooldown: {settings.MessageCooldownSeconds} seconds");
            settings.MessageCooldownSeconds = (int)listing.Slider(settings.MessageCooldownSeconds, 1, 10);

            listing.Gap(24f);

            // Quick Status Section
            Text.Font = GameFont.Medium;
            listing.Label("Quick Status");
            Text.Font = GameFont.Small;
            listing.GapLine(6f);

            var twitchStatus = CAPChatInteractiveMod.Instance.Settings.TwitchSettings.IsConnected ? "Connected" : "Disconnected";
            var youtubeStatus = CAPChatInteractiveMod.Instance.Settings.YouTubeSettings.IsConnected ? "Connected" : "Disconnected";

            listing.Label($"Twitch: {twitchStatus}");
            listing.Label($"YouTube: {youtubeStatus}");

            listing.Gap(24f);

            // Command Prefixes Section
            Text.Font = GameFont.Medium;
            listing.Label("Command Prefixes");
            Text.Font = GameFont.Small;
            listing.GapLine(6f);

            listing.Label("Command Prefix - The prefix to use for all chat commands.");
            Text.Font = GameFont.Tiny;
            listing.Label("Prefixes cannot start with: / . \\ or contain spaces");
            Text.Font = GameFont.Small;

            string commandPrefix = listing.TextEntryLabeled("Command Prefix:", settings.Prefix);
            if (IsValidPrefix(commandPrefix))
            {
                settings.Prefix = commandPrefix;
            }
            else if (!string.IsNullOrEmpty(commandPrefix))
            {
                GUI.color = Color.red;
                listing.Label("Invalid prefix! Must not start with / . \\ or contain spaces");
                GUI.color = Color.white;
            }

            listing.Gap(12f);

            listing.Label("Purchase Prefix - The prefix to use as a substitute for !buy.");
            Text.Font = GameFont.Tiny;
            listing.Label("Prefixes cannot start with: / . \\ or contain spaces");
            Text.Font = GameFont.Small;

            string buyPrefix = listing.TextEntryLabeled("Purchase Prefix:", settings.BuyPrefix);
            if (IsValidPrefix(buyPrefix))
            {
                settings.BuyPrefix = buyPrefix;
            }
            else if (!string.IsNullOrEmpty(buyPrefix))
            {
                GUI.color = Color.red;
                listing.Label("Invalid prefix! Must not start with / . \\ or contain spaces");
                GUI.color = Color.white;
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

        private static void NumericField(Listing_Standard listing, string label, ref int value, int min, int max)
        {
            Rect rect = listing.GetRect(Text.LineHeight);
            Rect leftRect = rect.LeftPart(0.6f).Rounded();
            Rect rightRect = rect.RightPart(0.4f).Rounded();

            Widgets.Label(leftRect, label);
            string buffer = value.ToString();
            Widgets.TextFieldNumeric(rightRect, ref value, ref buffer, min, max);
        }
    }
}