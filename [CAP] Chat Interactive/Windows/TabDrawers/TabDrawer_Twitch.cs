using CAP_ChatInteractive;
using RimWorld;
using UnityEngine;
using Verse;

namespace _CAP__Chat_Interactive
{
    public static class TabDrawer_Twitch
    {
        private static Vector2 _scrollPosition = Vector2.zero;

        public static void Draw(Rect region)
        {
            var settings = CAPChatInteractiveMod.Instance.Settings.TwitchSettings;
            var view = new Rect(0f, 0f, region.width - 16f, 750f); // Increased height

            Widgets.BeginScrollView(region, ref _scrollPosition, view);
            var listing = new Listing_Standard();
            listing.Begin(view);

            // Header with quick explanation
            Text.Font = GameFont.Medium;
            listing.Label("Twitch Integration");
            Text.Font = GameFont.Small;

            // Quick start guide - fixed spacing
            listing.Gap(8f);
            Rect quickGuideRect = listing.GetRect(55f); // More height for the text
            string quickGuide = @"<b>Quick Setup:</b>
1. Enter your channel name
2. Get OAuth token from Twitch Token Generator  
3. Paste token and connect!";
            Widgets.Label(quickGuideRect, quickGuide);
            TooltipHandler.TipRegion(quickGuideRect, "Simple 3-step setup guide for Twitch integration");
            listing.Gap(12f);

            // Enable checkbox with proper tooltip
            Rect enableRect = listing.GetRect(30f);
            Widgets.CheckboxLabeled(enableRect, "Enable Twitch Integration", ref settings.Enabled);
            TooltipHandler.TipRegion(enableRect,
                "<b>Enable/Disable Twitch Integration</b>\n\n" +
                "When enabled, the mod will connect to Twitch chat\n" +
                "and process viewer commands and messages.");

            listing.Gap(16f);

            // Channel Name Section
            Text.Font = GameFont.Medium;
            listing.Label("Channel Information");
            Text.Font = GameFont.Small;
            listing.GapLine(6f);
            listing.Gap(4f);

            // Channel name with proper tooltip on the label
            Rect channelLabelRect = listing.GetRect(24f);
            Widgets.Label(channelLabelRect, "Channel Name:");
            TooltipHandler.TipRegion(channelLabelRect,
                "<b>Your Twitch Channel Name</b>\n\n" +
                "This is the name that appears in your stream URL:\n" +
                "• https://twitch.tv/<color=orange>YOUR_CHANNEL_NAME</color>\n\n" +
                "<b>Examples:</b>\n" +
                "• If your URL is twitch.tv/superstreamer → enter 'superstreamer'\n" +
                "• Case insensitive - 'SuperStreamer' same as 'superstreamer'\n\n" +
                "🔍 <i>This is NOT your display name with capitals!</i>");

            Rect channelFieldRect = listing.GetRect(30f);
            settings.ChannelName = Widgets.TextField(channelFieldRect, settings.ChannelName);
            TooltipHandler.TipRegion(channelFieldRect, "Enter your Twitch channel name here");
            listing.Gap(12f);

            // Bot Account Section
            Text.Font = GameFont.Medium;
            listing.Label("Bot Account (Optional)");
            Text.Font = GameFont.Small;
            listing.GapLine(6f);
            listing.Gap(4f);

            // Bot username with proper tooltip
            Rect botLabelRect = listing.GetRect(24f);
            Widgets.Label(botLabelRect, "Bot Username:");
            TooltipHandler.TipRegion(botLabelRect,
                "<b>Bot Account Username</b>\n\n" +
                "🤖 <b>Recommended:</b> Create a separate bot account\n" +
                "👤 <b>Alternative:</b> Use your main streamer account\n\n" +
                "<b>Why use a bot account?</b>\n" +
                "• Keeps chat clean (bot messages separate)\n" +
                "• Prevents accidental commands from your own account\n" +
                "• Better moderation control\n" +
                "• Professional appearance\n\n" +
                "<b>Using main account?</b>\n" +
                "• Just enter your channel name again\n" +
                "• You'll see your own messages in chat");

            Rect botFieldRect = listing.GetRect(30f);
            settings.BotUsername = Widgets.TextField(botFieldRect, settings.BotUsername);
            TooltipHandler.TipRegion(botFieldRect, "Enter bot account username (or your main account)");

            // Bot account status - fixed to not get cut off
            if (!string.IsNullOrEmpty(settings.BotUsername) &&
                !string.IsNullOrEmpty(settings.ChannelName))
            {
                Rect botStatusRect = listing.GetRect(20f);
                if (settings.BotUsername.ToLower() != settings.ChannelName.ToLower())
                {
                    GUI.color = Color.green;
                    Widgets.Label(botStatusRect, "✓ Using separate bot account");
                    GUI.color = Color.white;
                }
                else
                {
                    GUI.color = Color.yellow;
                    Widgets.Label(botStatusRect, "⚠ Using main streamer account as bot");
                    GUI.color = Color.white;
                }
            }

            listing.Gap(16f);

            // OAuth Token Section
            Text.Font = GameFont.Medium;
            listing.Label("Authentication");
            Text.Font = GameFont.Small;
            listing.GapLine(6f);
            listing.Gap(4f);

            // Access Token label with tooltip
            Rect tokenLabelRect = listing.GetRect(24f);
            Widgets.Label(tokenLabelRect, "Access Token:");
            TooltipHandler.TipRegion(tokenLabelRect,
                "<b>Twitch OAuth Token</b>\n\n" +
                "🔐 This is like a password for the bot account\n" +
                "• Get it from Twitch Token Generator (button below)\n" +
                "• Token starts with 'oauth:'\n" +
                "• Keep this secret - never share it!\n\n" +
                "<b>How it works:</b>\n" +
                "1. Click 'Get Twitch Access Token' below\n" +
                "2. Login with your bot account (or main account)\n" +
                "3. Select 'Bot Chat Token'\n" +
                "4. Copy the generated token\n" +
                "5. Paste it here or use the Paste button\n\n" +
                "🛡️ <i>This token only allows chat access, not stream control</i>");

            // Token field - show masked for security
            Rect tokenFieldRect = listing.GetRect(30f);
            string tokenDisplay = string.IsNullOrEmpty(settings.AccessToken) ?
                "[Click Paste or Get Token below]" :
                "oauth:••••••••••••••••";
            // Just display, don't allow editing of masked field
            Widgets.TextField(tokenFieldRect, tokenDisplay);
            TooltipHandler.TipRegion(tokenFieldRect, "Twitch OAuth token (click buttons below to set)");

            // Token action buttons
            Rect tokenButtonRect = listing.GetRect(35f); // More height for buttons
            Rect pasteRect = new Rect(tokenButtonRect.x, tokenButtonRect.y, 140f, 30f);
            Rect getTokenRect = new Rect(pasteRect.xMax + 10f, tokenButtonRect.y, 160f, 30f);

            if (Widgets.ButtonText(pasteRect, "Paste Token"))
            {
                string clipboardText = GUIUtility.systemCopyBuffer?.Trim();
                if (!string.IsNullOrEmpty(clipboardText))
                {
                    // Auto-add "oauth:" prefix if missing
                    if (!clipboardText.StartsWith("oauth:") && !clipboardText.Contains(" "))
                    {
                        clipboardText = "oauth:" + clipboardText;
                        Messages.Message("Added 'oauth:' prefix to token", MessageTypeDefOf.SilentInput);
                    }
                    settings.AccessToken = clipboardText;
                    Messages.Message("Twitch token pasted successfully!", MessageTypeDefOf.PositiveEvent);
                }
                else
                {
                    Messages.Message("Clipboard is empty!", MessageTypeDefOf.NegativeEvent);
                }
            }
            TooltipHandler.TipRegion(pasteRect, "Paste OAuth token from clipboard");

            if (Widgets.ButtonText(getTokenRect, "Get Twitch Token"))
            {
                string message =
                    "<b>Twitch Token Generator Instructions</b>\n\n" +
                    "🔐 <b>Step-by-Step:</b>\n" +
                    "1. Click 'Open Browser' below\n" +
                    "2. Login with your <b>Twitch account</b>\n" +
                    "   • Use your BOT account if you have one\n" +
                    "   • Or use your MAIN streamer account\n" +
                    "3. Select <b>'Bot Chat Token'</b>\n" +
                    "4. Copy the generated token\n" +
                    "5. Return here and click <b>'Paste Token'</b>\n\n" +
                    "🔒 <b>Security Note:</b>\n" +
                    "• This token only allows chat access\n" +
                    "• It cannot control your stream\n" +
                    "• Keep it private like a password\n\n" +
                    "Open Twitch Token Generator in your browser?";

                Find.WindowStack.Add(new Dialog_MessageBox(message, "Open Browser",
                    () => Application.OpenURL("https://twitchtokengenerator.com/"),
                    "Cancel", null, null, true));
            }
            TooltipHandler.TipRegion(getTokenRect, "Open Twitch Token Generator to get OAuth token");

            // Token status indicator - fixed spacing
            listing.Gap(8f);
            if (!string.IsNullOrEmpty(settings.AccessToken))
            {
                Rect tokenStatusRect = listing.GetRect(20f);
                GUI.color = Color.green;
                Widgets.Label(tokenStatusRect, "✓ Token configured - Ready to connect");
                GUI.color = Color.white;

                // Show token type - ensure enough space
                Rect tokenTypeRect = listing.GetRect(18f);
                bool isBotToken = !string.IsNullOrEmpty(settings.BotUsername) &&
                                  settings.BotUsername.ToLower() != settings.ChannelName.ToLower();
                string tokenType = isBotToken ? "Bot account token" : "Main account token";
                Widgets.Label(tokenTypeRect, $"Token type: {tokenType}");
                TooltipHandler.TipRegion(tokenTypeRect, "Shows which account this token belongs to");
            }
            else
            {
                Rect tokenStatusRect = listing.GetRect(20f);
                GUI.color = Color.red;
                Widgets.Label(tokenStatusRect, "❌ No token - Cannot connect to Twitch");
                GUI.color = Color.white;
            }

            listing.Gap(20f);

            // Connection Settings
            Text.Font = GameFont.Medium;
            listing.Label("Connection Settings");
            Text.Font = GameFont.Small;
            listing.GapLine(6f);
            listing.Gap(4f);

            // Auto-connect with proper tooltip
            Rect autoConnectRect = listing.GetRect(30f);
            Widgets.CheckboxLabeled(autoConnectRect, "Auto-connect on startup", ref settings.AutoConnect);
            TooltipHandler.TipRegion(autoConnectRect,
                "<b>Auto-connect on Startup</b>\n\n" +
                "When enabled, the mod will automatically connect to\n" +
                "Twitch when RimWorld loads.\n\n" +
                "✅ <b>Good for:</b>\n" +
                "• Regular streamers\n" +
                "• Set-and-forget setup\n\n" +
                "❌ <b>Disable if:</b>\n" +
                "• You only stream occasionally\n" +
                "• Testing different configurations\n" +
                "• Using multiple streaming platforms");

            // Connection status and controls
            listing.Gap(12f);

            if (settings.IsConnected)
            {
                Rect statusRect = listing.GetRect(24f);
                Widgets.Label(statusRect, "Status: <color=green>Connected to Twitch</color>");
                TooltipHandler.TipRegion(statusRect,
                    "<b>Connected to Twitch</b>\n\n" +
                    "✅ Successfully connected to:\n" +
                    $"• Channel: {settings.ChannelName}\n" +
                    $"• Bot: {settings.BotUsername}\n\n" +
                    "The mod is now listening for chat commands\n" +
                    "and processing viewer interactions.");

                Rect disconnectRect = listing.GetRect(30f);
                if (Widgets.ButtonText(disconnectRect, "Disconnect from Twitch"))
                {
                    CAPChatInteractiveMod.Instance.TwitchService.Disconnect();
                    Messages.Message("Disconnected from Twitch chat", MessageTypeDefOf.SilentInput);
                }
                TooltipHandler.TipRegion(disconnectRect, "Disconnect from Twitch chat");
            }
            else
            {
                Rect statusRect = listing.GetRect(24f);
                Widgets.Label(statusRect, "Status: <color=red>Disconnected</color>");
                TooltipHandler.TipRegion(statusRect,
                    "<b>Disconnected from Twitch</b>\n\n" +
                    "The mod is not currently connected to Twitch chat.\n\n" +
                    "✅ <b>Ready to connect?</b>\n" +
                    "Make sure you have:\n" +
                    "• Channel name entered\n" +
                    "• Valid OAuth token\n" +
                    "• Bot username (optional)\n\n" +
                    "Then click 'Connect to Twitch' below.");

                bool canConnect = settings.CanConnect;
                Rect connectRect = listing.GetRect(30f);

                if (canConnect)
                {
                    if (Widgets.ButtonText(connectRect, "Connect to Twitch"))
                    {
                        CAPChatInteractiveMod.Instance.TwitchService.Connect();
                        Messages.Message("Connecting to Twitch...", MessageTypeDefOf.SilentInput);
                    }
                    TooltipHandler.TipRegion(connectRect, "Connect to Twitch chat");
                }
                else
                {
                    GUI.color = Color.gray;
                    Widgets.ButtonText(connectRect, "Connect to Twitch (Missing Settings)");
                    GUI.color = Color.white;

                    // Show what's missing - with proper spacing
                    listing.Gap(4f);
                    Rect missingRect = listing.GetRect(45f);
                    string missing = "";
                    if (string.IsNullOrEmpty(settings.ChannelName)) missing += "• Channel name\n";
                    if (string.IsNullOrEmpty(settings.AccessToken)) missing += "• OAuth token\n";
                    if (string.IsNullOrEmpty(settings.BotUsername)) missing += "• Bot username\n";

                    if (!string.IsNullOrEmpty(missing))
                    {
                        GUI.color = Color.yellow;
                        Widgets.Label(missingRect, $"Missing requirements:\n{missing}");
                        GUI.color = Color.white;
                        TooltipHandler.TipRegion(missingRect, "Fix these missing settings to connect");
                    }
                }
            }

            // Quick Tips Section
            listing.Gap(24f);
            Text.Font = GameFont.Medium;
            listing.Label("Quick Tips");
            Text.Font = GameFont.Small;
            listing.GapLine(6f);
            listing.Gap(4f);

            Rect tipsRect = listing.GetRect(85f); // More height for tips
            string tips =
                "💡 <b>Common Issues & Solutions:</b>\n" +
                "• <b>Token not working?</b> Regenerate at Twitch Token Generator\n" +
                "• <b>Bot not joining?</b> Check channel name spelling\n" +
                "• <b>Connection drops?</b> Check internet stability\n" +
                "• <b>See your own messages?</b> That's normal with main account";
            Widgets.Label(tipsRect, tips);
            TooltipHandler.TipRegion(tipsRect, "Helpful tips for troubleshooting Twitch connection");

            listing.End();
            Widgets.EndScrollView();
        }
    }
}