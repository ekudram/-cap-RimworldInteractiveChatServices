using CAP_ChatInteractive;
using RimWorld;
using UnityEngine;
using Verse;

namespace _CAP__Chat_Interactive
{
    public static class TabDrawer_YouTube
    {
        private static Vector2 _scrollPosition = Vector2.zero;

        public static void Draw(Rect region)
        {
            var settings = CAPChatInteractiveMod.Instance.Settings.YouTubeSettings;
            var view = new Rect(0f, 0f, region.width - 16f, 800f);

            Widgets.BeginScrollView(region, ref _scrollPosition, view);
            var listing = new Listing_Standard();
            listing.Begin(view);

            // Header with connection type explanation
            Text.Font = GameFont.Medium;
            listing.Label("YouTube Chat Integration");
            Text.Font = GameFont.Small;

            // Connection type explanation
            GUI.color = ColorLibrary.Orange;
            listing.Label("💡 Choose your connection type:");
            GUI.color = Color.white;

            var explanationRect = listing.GetRect(60f);
            string explanation = @"<b>Read-Only</b>: Just API Key + Channel ID (easy, reads chat only)
<b>Read+Write</b>: API Key + OAuth (hard, can send messages, may require verification)";
            Widgets.Label(explanationRect, explanation);
            listing.Gap(12f);

            listing.CheckboxLabeled("Enable YouTube Integration", ref settings.Enabled);
            listing.Gap(12f);

            // Basic Settings Section
            Text.Font = GameFont.Medium;
            listing.Label("Basic Settings (Required for Read-Only)");
            Text.Font = GameFont.Small;
            listing.GapLine(6f);

            // Channel ID with better tooltip
            Rect channelRect = listing.GetRect(30f);
            Rect channelLabelRect = new Rect(channelRect.x, channelRect.y, 120f, 30f);
            Rect channelFieldRect = new Rect(channelLabelRect.xMax + 10f, channelRect.y, 200f, 30f);
            Rect channelButtonRect = new Rect(channelFieldRect.xMax + 10f, channelRect.y, 120f, 30f);

            Widgets.Label(channelLabelRect, "Channel ID:");
            settings.ChannelName = Widgets.TextField(channelFieldRect, settings.ChannelName);
            if (Widgets.ButtonText(channelButtonRect, "Find ID"))
            {
                Find.WindowStack.Add(new Dialog_MessageBox(
                    "This will open a tool to find your YouTube Channel ID.\n\n" +
                    "Your Channel ID starts with 'UC' and is different from your channel name.\n\n" +
                    "Open Channel ID Finder?",
                    "Open Tool", () => Application.OpenURL("https://commentpicker.com/youtube-channel-id.php"),
                    "Cancel", null, null, true
                ));
            }
            TooltipHandler.TipRegion(channelRect,
                "<b>Required for both read-only and read+write modes</b>\n\n" +
                "Your unique YouTube Channel ID (starts with UC...)\n" +
                "This is NOT your channel name or URL!\n\n" +
                "Example: UC_x123y456z789 (24 characters)");

            listing.Gap(12f);

            // API Key Section with better explanation
            Text.Font = GameFont.Medium;
            listing.Label("API Key (Required for Read-Only)");
            Text.Font = GameFont.Small;
            listing.GapLine(6f);

            Rect apiKeyHelpRect = listing.GetRect(30f);
            GUI.color = Color.yellow;
            Widgets.Label(apiKeyHelpRect, "💡 API Key enables READ-ONLY chat reading");
            GUI.color = Color.white;
            listing.Gap(4f);

            Rect apiKeyRect = listing.GetRect(30f);
            Rect apiKeyLabelRect = new Rect(apiKeyRect.x, apiKeyRect.y, 150f, 30f);
            Rect apiKeyFieldRect = new Rect(apiKeyLabelRect.xMax + 10f, apiKeyRect.y, 200f, 30f);
            Rect apiKeyPasteRect = new Rect(apiKeyFieldRect.xMax + 10f, apiKeyRect.y, 80f, 30f);
            Rect apiKeyGetRect = new Rect(apiKeyPasteRect.xMax + 10f, apiKeyRect.y, 80f, 30f);

            Widgets.Label(apiKeyLabelRect, "API Key:");

            // Show masked API key for security
            string displayText = string.IsNullOrEmpty(settings.AccessToken) ?
                "[Click Paste or Get]" :
                "••••••••••••••••";
            Widgets.TextField(apiKeyFieldRect, displayText);

            if (Widgets.ButtonText(apiKeyPasteRect, "Paste"))
            {
                string clipboardText = GUIUtility.systemCopyBuffer?.Trim();
                if (!string.IsNullOrEmpty(clipboardText) && clipboardText.Length > 10)
                {
                    settings.AccessToken = clipboardText;
                    Messages.Message("API key pasted! You can now try read-only connection.", MessageTypeDefOf.PositiveEvent);
                }
                else
                {
                    Messages.Message("Clipboard empty or invalid!", MessageTypeDefOf.NegativeEvent);
                }
            }

            if (Widgets.ButtonText(apiKeyGetRect, "Get Key"))
            {
                Find.WindowStack.Add(new Dialog_MessageBox(
                    "<b>YouTube Data API v3 Key Setup</b>\n\n" +
                    "1. Go to Google Cloud Console\n" +
                    "2. Create a project (or select existing)\n" +
                    "3. Enable 'YouTube Data API v3'\n" +
                    "4. Create credentials → API Key\n" +
                    "5. Copy the generated key\n" +
                    "6. Paste it here\n\n" +
                    "This enables <b>read-only</b> chat access.\n\n" +
                    "Open Google Cloud Console?",
                    "Open Console", () => Application.OpenURL("https://console.cloud.google.com/"),
                    "Cancel", null, null, true
                ));
            }

            // API Key status
            if (!string.IsNullOrEmpty(settings.AccessToken))
            {
                Rect statusRect = listing.GetRect(20f);
                GUI.color = Color.green;
                Widgets.Label(statusRect, "✓ API Key configured - Ready for read-only mode");
                GUI.color = Color.white;
            }
            else
            {
                Rect statusRect = listing.GetRect(20f);
                GUI.color = Color.yellow;
                Widgets.Label(statusRect, "⚠ No API Key - Cannot connect to YouTube");
                GUI.color = Color.white;
            }

            TooltipHandler.TipRegion(apiKeyRect,
                "<b>YouTube Data API v3 Key</b>\n\n" +
                "Required for reading chat messages.\n" +
                "Get this from Google Cloud Console.\n\n" +
                "This is different from OAuth tokens!\n\n" +
                "🔒 This key only allows reading chat,\nnot sending messages.");

            listing.Gap(24f);

            // OAuth Section (Optional - for sending messages)
            Text.Font = GameFont.Medium;
            listing.Label("OAuth 2.0 (Optional - For Sending Messages)");
            Text.Font = GameFont.Small;
            listing.GapLine(6f);

            Rect oauthWarningRect = listing.GetRect(40f);
            GUI.color = Color.red;
            Widgets.Label(oauthWarningRect, "⚠ WARNING: OAuth often requires Google verification!");
            GUI.color = Color.white;
            listing.Gap(4f);

            Rect oauthHelpRect = listing.GetRect(50f);
            string oauthHelp = @"<b>OAuth 2.0 is ONLY needed if you want to:</b>
• Send messages to chat
• Reply to viewers
• Moderate chat

<b>For just reading chat, skip OAuth!</b>
Use API Key + Channel ID above.";
            Widgets.Label(oauthHelpRect, oauthHelp);

            if (listing.ButtonText("Configure OAuth 2.0 (Advanced)"))
            {
                // This would open your OAuth configuration tab or dialog
                var mainWindow = Find.WindowStack.WindowOfType<Dialog_ChatInteractiveSettings>();
                if (mainWindow != null)
                {
                    // Switch to OAuth tab - you'll need to implement tab switching
                    Messages.Message("Switch to OAuth tab to configure OAuth 2.0", MessageTypeDefOf.SilentInput);
                }
                else
                {
                    Find.WindowStack.Add(new Dialog_EditClientSecrets());
                }
            }

            listing.Gap(24f);

            // Connection Settings
            Text.Font = GameFont.Medium;
            listing.Label("Connection Settings");
            Text.Font = GameFont.Small;
            listing.GapLine(6f);

            listing.CheckboxLabeled("Auto-connect on startup", ref settings.AutoConnect);
            TooltipHandler.TipRegion(listing.GetRect(0f),
                "Automatically connect to YouTube when RimWorld starts\n\n" +
                "Requires valid API Key and Channel ID");

            // Connection status and controls
            listing.Gap(12f);

            if (settings.IsConnected)
            {
                listing.Label("Status: <color=green>Connected</color>");
                if (listing.ButtonText("Disconnect"))
                {
                    CAPChatInteractiveMod.Instance.YouTubeService.Disconnect();
                    Messages.Message("Disconnected from YouTube", MessageTypeDefOf.SilentInput);
                }

                // Show connection type
                var youtubeService = CAPChatInteractiveMod.Instance?.YouTubeService;
                if (youtubeService != null)
                {
                    string connectionType = youtubeService.CanSendMessages ?
                        "<color=green>Read+Write</color>" :
                        "<color=yellow>Read-Only</color>";
                    listing.Label($"Mode: {connectionType}");
                }
            }
            else
            {
                listing.Label("Status: <color=red>Disconnected</color>");

                bool canConnectReadOnly = !string.IsNullOrEmpty(settings.ChannelName) &&
                                        !string.IsNullOrEmpty(settings.AccessToken);

                if (canConnectReadOnly)
                {
                    if (listing.ButtonText("Connect (Read-Only)"))
                    {
                        CAPChatInteractiveMod.Instance.YouTubeService.Connect();
                        Messages.Message("Connecting to YouTube in read-only mode...", MessageTypeDefOf.SilentInput);
                    }
                    TooltipHandler.TipRegion(listing.GetRect(0f),
                        "Connect to YouTube chat in read-only mode\n\n" +
                        "You will be able to see chat messages but cannot reply");
                }
                else
                {
                    GUI.color = Color.gray;
                    listing.ButtonText("Connect (Missing API Key or Channel ID)");
                    GUI.color = Color.white;
                    TooltipHandler.TipRegion(listing.GetRect(0f),
                        "Cannot connect - missing API Key or Channel ID\n\n" +
                        "Configure basic settings above first");
                }
            }

            // Quota information if connected
            var youtubeServiceQuota = CAPChatInteractiveMod.Instance?.YouTubeService;
            if (youtubeServiceQuota != null && settings.IsConnected)
            {
                listing.Gap(12f);
                Text.Font = GameFont.Medium;
                listing.Label("API Quota Usage");
                Text.Font = GameFont.Small;
                listing.GapLine(6f);

                listing.Label($"Status: {youtubeServiceQuota.QuotaStatus}");

                Rect quotaRect = listing.GetRect(22f);
                Widgets.FillableBar(quotaRect, youtubeServiceQuota.QuotaPercentage / 100f,
                    SolidColorMaterials.NewSolidColorTexture(youtubeServiceQuota.QuotaColor));

                if (youtubeServiceQuota.QuotaPercentage >= 80)
                {
                    listing.Gap(4f);
                    GUI.color = Color.yellow;
                    listing.Label("High usage - consider reducing polling frequency");
                    GUI.color = Color.white;
                }
            }

            listing.End();
            Widgets.EndScrollView();
        }
    }
}