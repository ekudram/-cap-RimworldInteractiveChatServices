using RimWorld;
using System;
using System.IO;
using UnityEngine;
using Verse;

namespace CAP_ChatInteractive
{
    public class Dialog_YouTubeSettings : Window
    {
        private StreamServiceSettings _settings;
        public override Vector2 InitialSize => new Vector2(600f, 700f);
        public Dialog_YouTubeSettings(StreamServiceSettings settings)
        {
            _settings = settings;
            doCloseButton = true;
            forcePause = false;
            absorbInputAroundWindow = true;
            closeOnAccept = false;
            closeOnCancel = true;

            // Set proper window size
            optionalTitle = "YouTube Settings";
            forceCatchAcceptAndCancelEventEvenIfUnfocused = true;
        }

        public override void DoWindowContents(Rect inRect)
        {
            var listing = new Listing_Standard();
            listing.Begin(inRect);

            // Enable/Disable toggle
            listing.CheckboxLabeled("Enable YouTube Integration", ref _settings.Enabled);
            listing.Gap(12f);

            // Channel ID with Find button on same line
            Rect channelRect = listing.GetRect(30f);
            Rect channelLabelRect = new Rect(channelRect.x, channelRect.y, 120f, 30f);
            Rect channelFieldRect = new Rect(channelLabelRect.xMax + 10f, channelRect.y, 200f, 30f);
            Rect channelButtonRect = new Rect(channelFieldRect.xMax + 10f, channelRect.y, 120f, 30f);

            Widgets.Label(channelLabelRect, "Channel ID:");
            _settings.ChannelName = Widgets.TextField(channelFieldRect, _settings.ChannelName);
            if (Widgets.ButtonText(channelButtonRect, "Find ID"))
            {
                Find.WindowStack.Add(new Dialog_MessageBox(
                    "This will open a tool to find your YouTube Channel ID.\n\n" +
                    "Open Channel ID Finder?",
                    "Open Tool", () => Application.OpenURL("https://commentpicker.com/youtube-channel-id.php"),
                    "Cancel", null, null, true
                ));
            }
            TooltipHandler.TipRegion(channelRect, "Your unique YouTube Channel ID (starts with UC...)");

            listing.Gap(12f);

            // Bot Username
            listing.Label("Bot Username:");
            _settings.BotUsername = listing.TextEntry(_settings.BotUsername);
            TooltipHandler.TipRegion(listing.GetRect(0f), "Your YouTube channel display name");
            listing.Gap(12f);

            // API Key with buttons on same line - FIXED VERSION
            Rect apiKeyRect = listing.GetRect(30f);
            Rect apiKeyLabelRect = new Rect(apiKeyRect.x, apiKeyRect.y, 150f, 30f);
            Rect apiKeyFieldRect = new Rect(apiKeyLabelRect.xMax + 10f, apiKeyRect.y, 200f, 30f);
            Rect apiKeyPasteRect = new Rect(apiKeyFieldRect.xMax + 10f, apiKeyRect.y, 80f, 30f);
            Rect apiKeyGetRect = new Rect(apiKeyPasteRect.xMax + 10f, apiKeyRect.y, 80f, 30f);

            Widgets.Label(apiKeyLabelRect, "API Key:");

            // Display asterisks but don't overwrite the actual token
            string displayText = string.IsNullOrEmpty(_settings.AccessToken) ? "" : new string('*', 16);
            Widgets.TextField(apiKeyFieldRect, displayText); // Just display, don't assign

            if (Widgets.ButtonText(apiKeyPasteRect, "Paste"))
            {
                string clipboardText = GUIUtility.systemCopyBuffer?.Trim();
                if (!string.IsNullOrEmpty(clipboardText))
                {
                    _settings.AccessToken = clipboardText;
                    Messages.Message("API key pasted!", MessageTypeDefOf.PositiveEvent);
                }
                else
                {
                    Messages.Message("Clipboard empty!", MessageTypeDefOf.NegativeEvent);
                }
            }
            if (Widgets.ButtonText(apiKeyGetRect, "Get"))
            {
                Find.WindowStack.Add(new Dialog_MessageBox(
                    "This will open Google Cloud Console.\n\nOpen console?",
                    "Open", () => Application.OpenURL("https://console.cloud.google.com/"),
                    "Cancel", null, null, true
                ));
            }

            // Show API key preview for verification
            if (!string.IsNullOrEmpty(_settings.AccessToken))
            {
                Rect previewRect = listing.GetRect(20f);
                string preview = _settings.AccessToken.Length > 10 ?
                    _settings.AccessToken.Substring(0, 10) + "..." :
                    _settings.AccessToken;
                Widgets.Label(previewRect, $"Key: {preview}");
            }

            TooltipHandler.TipRegion(apiKeyRect, "Your YouTube Data API v3 key");

            listing.Gap(12f);

            // OAuth Client Secrets - with reality check
            bool clientSecretsExists = JsonFileManager.FileExists("client_secrets.json");

            Rect oauthRect = listing.GetRect(50f); // More height for warning
            Rect oauthLabelRect = new Rect(oauthRect.x, oauthRect.y, 180f, 30f);
            Rect oauthButtonRect = new Rect(oauthLabelRect.xMax + 10f, oauthRect.y, 120f, 30f);
            Rect oauthStatusRect = new Rect(oauthButtonRect.xMax + 10f, oauthRect.y, 120f, 30f);
            Rect oauthWarningRect = new Rect(oauthRect.x, oauthRect.y + 25f, oauthRect.width, 20f);

            Widgets.Label(oauthLabelRect, "OAuth Config:");
            if (Widgets.ButtonText(oauthButtonRect, clientSecretsExists ? "Edit" : "Create"))
            {
                Find.WindowStack.Add(new Dialog_EditClientSecrets());
            }

            if (clientSecretsExists)
            {
                GUI.color = Color.green;
                Widgets.Label(oauthStatusRect, "✓ Ready");
                GUI.color = Color.white;

                // Warning about verification
                GUI.color = Color.yellow;
                Widgets.Label(oauthWarningRect, "⚠ May require Google verification");
                GUI.color = Color.white;
            }
            else
            {
                GUI.color = Color.yellow;
                Widgets.Label(oauthStatusRect, "✗ No OAuth");
                GUI.color = Color.white;

                // Info about fallback
                Widgets.Label(oauthWarningRect, "Chat reading still works without OAuth");
            }
            TooltipHandler.TipRegion(oauthRect, "OAuth 2.0 for sending messages (may require Google verification)");

            listing.Gap(12f);

            // Quota Usage Display (only when connected)
            var youtubeService = CAPChatInteractiveMod.Instance?.YouTubeService;
            if (youtubeService != null && _settings.IsConnected)
            {
                listing.Gap(12f);
                listing.Label($"API Quota: {youtubeService.QuotaStatus}");

                Rect quotaRect = listing.GetRect(22f);
                Widgets.FillableBar(quotaRect, youtubeService.QuotaPercentage / 100f,
                    SolidColorMaterials.NewSolidColorTexture(youtubeService.QuotaColor));

                if (youtubeService.QuotaPercentage >= 80)
                {
                    listing.Gap(4f);
                    GUI.color = Color.yellow;
                    listing.Label("High usage - reduce polling");
                    GUI.color = Color.white;
                }

                listing.Gap(12f);
            }

            // Auto-connect option
            listing.CheckboxLabeled("Auto-connect on startup", ref _settings.AutoConnect);

            // Connection status and controls
            listing.Gap();
            if (_settings.IsConnected)
            {
                listing.Label("Status: <color=green>Connected</color>");
                if (listing.ButtonText("Disconnect"))
                {
                    CAPChatInteractiveMod.Instance.YouTubeService.Disconnect();
                }
            }
            else
            {
                listing.Label("Status: <color=red>Disconnected</color>");
                if (listing.ButtonText("Connect") && _settings.CanConnect)
                {
                    CAPChatInteractiveMod.Instance.YouTubeService.Connect();
                    Messages.Message("Connecting to YouTube...", MessageTypeDefOf.SilentInput);
                }
            }

            listing.End();
        }
    }
}