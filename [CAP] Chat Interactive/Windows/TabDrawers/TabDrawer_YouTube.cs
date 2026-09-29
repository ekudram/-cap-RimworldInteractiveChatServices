// TabDrawer_YouTube.cs
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
// Draws the YouTube settings tab in the Chat Interactive settings window
using CAP_ChatInteractive;
using RimWorld;
using System.Text;
using UnityEngine;
using Verse;
using ColorLibrary = CAP_ChatInteractive.ColorLibrary;

namespace _CAP__Chat_Interactive
{
    public static class TabDrawer_YouTube
    {
        private static Vector2 _scrollPosition = Vector2.zero;

        public static void Draw(Rect region)
        {
            var settings = CAPChatInteractiveMod.Instance.Settings.YouTubeSettings;
            var view = new Rect(0f, 0f, region.width - 16f, 842f);

            Widgets.BeginScrollView(region, ref _scrollPosition, view);
            var listing = new Listing_Standard();
            listing.Begin(view);

            // Header with connection type explanation
            Text.Font = GameFont.Medium;
            GUI.color = ColorLibrary.HeaderAccent;
            listing.Label("TabDrawer_YouTube.YouTubeChatIntegration".Translate());
            GUI.color = Color.white;
            Text.Font = GameFont.Small;

            // Connection type explanation
            GUI.color = ColorLibrary.SubHeader;
            listing.Label("TabDrawer_YouTube.ConnectionTypeExplanation".Translate());
            GUI.color = Color.white;

            var explanationRect = listing.GetRect(60f);
            StringBuilder explanation = new StringBuilder();
            explanation.AppendLine("TabDrawer_YouTube.ConnectionTypeReadOnly".Translate());
            explanation.AppendLine("TabDrawer_YouTube.ConnectionTypeReadWrite".Translate());
            Widgets.Label(explanationRect, explanation.ToString());
            listing.Gap(12f);

            listing.CheckboxLabeled("TabDrawer_YouTube.EnableYouTubeIntegration".Translate(), ref settings.Enabled);
            listing.Gap(6f);
            listing.CheckboxLabeled(
                "RICS.Alert.SuppressDisconnect".Translate(),
                ref settings.SuppressDisconnectAlert,
                "RICS.Alert.SuppressDisconnectTooltip".Translate());
            listing.Gap(12f);

            // Basic Settings Section
            Text.Font = GameFont.Medium;
            GUI.color = ColorLibrary.SubHeader;
            listing.Label("TabDrawer_YouTube.BasicSettingsHeader".Translate());
            Text.Font = GameFont.Small;
            GUI.color = Color.white;
            listing.GapLine(6f);

            // Channel ID with better tooltip
            Rect channelRect = listing.GetRect(30f);
            Rect channelLabelRect = new Rect(channelRect.x, channelRect.y, 120f, 30f);
            Rect channelFieldRect = new Rect(channelLabelRect.xMax + 10f, channelRect.y, 200f, 30f);
            Rect channelButtonRect = new Rect(channelFieldRect.xMax + 10f, channelRect.y, 120f, 30f);

            Widgets.Label(channelLabelRect, "TabDrawer_YouTube.ChannelIDLabel".Translate());
            settings.ChannelName = Widgets.TextField(channelFieldRect, settings.ChannelName);
            if (Widgets.ButtonText(channelButtonRect, "TabDrawer_YouTube.FindIDButton".Translate()))
            {
                Find.WindowStack.Add(new Dialog_MessageBox(
                    "TabDrawer_YouTube.ChannelIDDialogMessage".Translate(),
                    "TabDrawer_YouTube.ChannelIDDialogOpenTool".Translate(),
                    () => Application.OpenURL("https://commentpicker.com/youtube-channel-id.php"),
                    "TabDrawer_YouTube.ChannelIDDialogCancel".Translate(), null, null, true
                ));
            }
            TooltipHandler.TipRegion(channelRect, "TabDrawer_YouTube.ChannelIDTooltip".Translate());

            listing.Gap(12f);

            // API Key Section with better explanation
            Text.Font = GameFont.Medium;
            GUI.color = ColorLibrary.SubHeader; 
            listing.Label("TabDrawer_YouTube.APIKeyHeader".Translate());
            Text.Font = GameFont.Small;
            GUI.color = Color.white;
            listing.GapLine(6f);

            Rect apiKeyHelpRect = listing.GetRect(30f);
            GUI.color = Color.yellow;
            Widgets.Label(apiKeyHelpRect, "TabDrawer_YouTube.APIKeyHelp".Translate());
            GUI.color = Color.white;
            listing.Gap(4f);

            Rect apiKeyRect = listing.GetRect(30f);
            Rect apiKeyLabelRect = new Rect(apiKeyRect.x, apiKeyRect.y, 150f, 30f);
            Rect apiKeyFieldRect = new Rect(apiKeyLabelRect.xMax + 10f, apiKeyRect.y, 200f, 30f);
            Rect apiKeyPasteRect = new Rect(apiKeyFieldRect.xMax + 10f, apiKeyRect.y, 80f, 30f);
            Rect apiKeyGetRect = new Rect(apiKeyPasteRect.xMax + 10f, apiKeyRect.y, 80f, 30f);

            Widgets.Label(apiKeyLabelRect, "TabDrawer_YouTube.APIKeyLabel".Translate());

            // Show masked API key for security
            string displayText = string.IsNullOrEmpty(settings.AccessToken) ?
                "TabDrawer_YouTube.APIKeyPlaceholder".Translate() :
                "TabDrawer_YouTube.APIKeyMasked".Translate();
            Widgets.TextField(apiKeyFieldRect, displayText);

            if (Widgets.ButtonText(apiKeyPasteRect, "TabDrawer_YouTube.APIKeyPasteButton".Translate()))
            {
                string clipboardText = GUIUtility.systemCopyBuffer?.Trim();
                if (!string.IsNullOrEmpty(clipboardText) && clipboardText.Length > 10)
                {
                    settings.AccessToken = clipboardText;
                    Messages.Message("TabDrawer_YouTube.APIKeyPasteSuccess".Translate(), MessageTypeDefOf.PositiveEvent);
                }
                else
                {
                    Messages.Message("TabDrawer_YouTube.APIKeyPasteFailed".Translate(), MessageTypeDefOf.NegativeEvent);
                }
            }

            if (Widgets.ButtonText(apiKeyGetRect, "TabDrawer_YouTube.APIKeyGetButton".Translate()))
            {
                StringBuilder dialogMessage = new StringBuilder();
                dialogMessage.AppendLine("TabDrawer_YouTube.APIKeyDialogTitle".Translate());
                dialogMessage.AppendLine();
                dialogMessage.Append("TabDrawer_YouTube.APIKeyDialogInstructions".Translate());

                Find.WindowStack.Add(new Dialog_MessageBox(
                    dialogMessage.ToString(),
                    "TabDrawer_YouTube.APIKeyDialogOpenConsole".Translate(),
                    () => Application.OpenURL("https://console.cloud.google.com/"),
                    "TabDrawer_YouTube.ChannelIDDialogCancel".Translate(), null, null, true
                ));
            }

            // API Key status
            if (!string.IsNullOrEmpty(settings.AccessToken))
            {
                Rect statusRect = listing.GetRect(20f);
                GUI.color = Color.green;
                Widgets.Label(statusRect, "TabDrawer_YouTube.APIKeyConfigured".Translate());
                GUI.color = Color.white;
            }
            else
            {
                Rect statusRect = listing.GetRect(20f);
                GUI.color = Color.yellow;
                Widgets.Label(statusRect, "TabDrawer_YouTube.NoAPIKey".Translate());
                GUI.color = Color.white;
            }

            TooltipHandler.TipRegion(apiKeyRect, "TabDrawer_YouTube.APIKeyTooltip".Translate());

            listing.Gap(24f);

            // Connection status and controls
            listing.Gap(12f);

            if (settings.IsConnected)
            {
                // Status: Connected (green)
                string connectedStatus = "TabDrawer_YouTube.Status".Translate(
                    ColorLibrary.Colorize("TabDrawer_YouTube.Connected".Translate(), Color.green)
                );
                listing.Label(connectedStatus);

                if (listing.ButtonText("TabDrawer_YouTube.DisconnectButton".Translate()))
                {
                    CAPChatInteractiveMod.Instance.YouTubeService.Disconnect();
                    Messages.Message("TabDrawer_YouTube.DisconnectedMessage".Translate(), MessageTypeDefOf.SilentInput);
                }

                // Show connection type
                var youtubeService = CAPChatInteractiveMod.Instance?.YouTubeService;
                if (youtubeService != null)
                {
                    string modeText = youtubeService.CanSendMessages ?
                        "TabDrawer_YouTube.ModeReadWrite".Translate() :
                        "TabDrawer_YouTube.ModeReadOnly".Translate();

                    Color modeColor = youtubeService.CanSendMessages ? Color.green : Color.yellow;

                    string modeDisplay = "TabDrawer_YouTube.ConnectionMode".Translate(
                        ColorLibrary.Colorize(modeText, modeColor)
                    );
                    listing.Label(modeDisplay);
                }
            }
            else
            {
                // Status: Disconnected (red)
                string disconnectedStatus = "TabDrawer_YouTube.Status".Translate(
                    ColorLibrary.Colorize("TabDrawer_YouTube.Disconnected".Translate(), Color.red)
                );
                listing.Label(disconnectedStatus);

                bool canConnectReadOnly = !string.IsNullOrEmpty(settings.ChannelName) &&
                                        !string.IsNullOrEmpty(settings.AccessToken);

                if (canConnectReadOnly)
                {
                    if (listing.ButtonText("TabDrawer_YouTube.ConnectReadOnlyButton".Translate()))
                    {
                        CAPChatInteractiveMod.Instance.YouTubeService.Connect();
                        Messages.Message("TabDrawer_YouTube.ConnectingMessage".Translate(), MessageTypeDefOf.SilentInput);
                    }
                    TooltipHandler.TipRegion(listing.GetRect(0f), "TabDrawer_YouTube.ConnectReadOnlyTooltip".Translate());
                }
                else
                {
                    GUI.color = Color.gray;
                    listing.ButtonText("TabDrawer_YouTube.CannotConnectButton".Translate());
                    GUI.color = Color.white;
                    TooltipHandler.TipRegion(listing.GetRect(0f), "TabDrawer_YouTube.CannotConnectTooltip".Translate());
                }
            }

            // Quota information if connected
            var youtubeServiceQuota = CAPChatInteractiveMod.Instance?.YouTubeService;
            if (youtubeServiceQuota != null && settings.IsConnected)
            {
                listing.Gap(12f);
                Text.Font = GameFont.Medium;
                listing.Label("TabDrawer_YouTube.APIQuotaHeader".Translate());
                Text.Font = GameFont.Small;
                listing.GapLine(6f);

                listing.Label(string.Format("TabDrawer_YouTube.QuotaStatus".Translate(), youtubeServiceQuota.QuotaStatus));

                Rect quotaRect = listing.GetRect(22f);
                Widgets.FillableBar(quotaRect, youtubeServiceQuota.QuotaPercentage / 100f,
                    SolidColorMaterials.NewSolidColorTexture(youtubeServiceQuota.QuotaColor));

                if (youtubeServiceQuota.QuotaPercentage >= 80)
                {
                    listing.Gap(4f);
                    GUI.color = Color.yellow;
                    listing.Label("TabDrawer_YouTube.HighQuotaWarning".Translate());
                    GUI.color = Color.white;
                }
            }

            listing.End();
            Widgets.EndScrollView();
        }
    }
}

//            // OAuth Section (Optional - for sending messages)
//            Text.Font = GameFont.Medium;
//            listing.Label("OAuth 2.0 (Optional - For Sending Messages)");
//            Text.Font = GameFont.Small;
//            listing.GapLine(6f);

//            Rect oauthWarningRect = listing.GetRect(40f);
//            GUI.color = Color.red;
//            Widgets.Label(oauthWarningRect, "⚠ WARNING: OAuth often requires Google verification!");
//            GUI.color = Color.white;
//            listing.Gap(4f);

//            Rect oauthHelpRect = listing.GetRect(50f);
//            string oauthHelp = @"<b>OAuth 2.0 is ONLY needed if you want to:</b>
//• Send messages to chat
//• Reply to viewers
//• Moderate chat

//<b>For just reading chat, skip OAuth!</b>
//Use API Key + Channel ID above.";
//            Widgets.Label(oauthHelpRect, oauthHelp);

//            if (listing.ButtonText("Configure OAuth 2.0 (Advanced)"))
//            {
//                // This would open your OAuth configuration tab or dialog
//                var mainWindow = Find.WindowStack.WindowOfType<Dialog_ChatInteractiveSettings>();
//                if (mainWindow != null)
//                {
//                    // Switch to OAuth tab - you'll need to implement tab switching
//                    Messages.Message("Switch to OAuth tab to configure OAuth 2.0", MessageTypeDefOf.SilentInput);
//                }
//                else
//                {
//                    Find.WindowStack.Add(new Dialog_EditClientSecrets());
//                }
//            }

//            listing.Gap(24f);

//            // Connection Settings
//            Text.Font = GameFont.Medium;
//            listing.Label("Connection Settings");
//            Text.Font = GameFont.Small;
//            listing.GapLine(6f);

//            listing.CheckboxLabeled("Auto-connect on startup", ref settings.AutoConnect);
//            TooltipHandler.TipRegion(listing.GetRect(0f),
//                "Automatically connect to YouTube when RimWorld starts\n\n" +
//                "Requires valid API Key and Channel ID");