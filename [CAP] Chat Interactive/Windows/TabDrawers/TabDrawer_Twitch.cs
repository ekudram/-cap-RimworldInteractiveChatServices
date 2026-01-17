// TabDrawer_Twitch.cs
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
// Draws the Twitch settings tab in the mod settings window
using CAP_ChatInteractive;
using RimWorld;
using System;
using UnityEngine;
using Verse;
using ColorLibrary = CAP_ChatInteractive.ColorLibrary;

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

            // === Twitch Tab Header ===
            Text.Font = GameFont.Medium;
            GUI.color = ColorLibrary.HeaderAccent;
            listing.Label("RICS.Twitch.TwitchIntegrationHeader".Translate());
            Text.Font = GameFont.Small;
            GUI.color = Color.white;

            // Quick start guid
            listing.Gap(8f);

            string quickGuide =
                "<b>" + "RICS.Twitch.QuickGuide.Title".Translate() + "</b>\n" +
                "RICS.Twitch.QuickGuide.Step1".Translate() + "\n" +
                "RICS.Twitch.QuickGuide.Step2".Translate() + "\n" +
                "RICS.Twitch.QuickGuide.Step3".Translate();

            float textHeight = Text.CalcHeight(quickGuide, listing.ColumnWidth) + 8f;
            Rect quickGuideRect = listing.GetRect(textHeight);
            Widgets.Label(quickGuideRect, quickGuide);
            TooltipHandler.TipRegion(quickGuideRect, "RICS.Twitch.QuickGuide.Tooltip".Translate()
            );

            listing.Gap(12f);

            // === Enable/Disable Integration ===
            Rect enableRect = listing.GetRect(30f);
            Widgets.CheckboxLabeled(enableRect,
                "RICS.Twitch.EnableIntegrationLabel".Translate(),
                ref settings.Enabled);

            string quickGuideTooltip =
                "RICS.Twitch.EnableIntegrationTooltip1".Translate() +
                "RICS.Twitch.EnableIntegrationTooltip2".Translate() +
                "RICS.Twitch.EnableIntegrationTooltip3".Translate();
            TooltipHandler.TipRegion(enableRect, quickGuideTooltip );

            listing.Gap(16f);

            // === Channel Name Section ===
            Text.Font = GameFont.Medium;
            GUI.color = ColorLibrary.SubHeader;
            listing.Label("RICS.Twitch.ChannelInformationHeader".Translate());
            Text.Font = GameFont.Small;
            GUI.color = Color.white;

            listing.GapLine(6f);
            listing.Gap(4f);

            // Channel name with proper tooltip on the label
            Rect channelLabelRect = listing.GetRect(24f);
            // OLD: Widgets.Label(channelLabelRect, "Channel Name:");
            Widgets.Label(channelLabelRect,
                "RICS.Twitch.ChannelNameLabel".Translate());
            TooltipHandler.TipRegion(channelLabelRect,
                "<b>" + UIUtilities.Colorize("RICS.Twitch.ChannelNameTooltip.Title".Translate(),ColorLibrary.HeaderAccent) // orange
                + "</b>\n\n" +
                "RICS.Twitch.ChannelNameTooltip.Desc".Translate() + "\n\n" +
                UIUtilities.Colorize(
                    "RICS.Twitch.ChannelNameTooltip.UrlExample".Translate(),
                    ColorLibrary.SubHeader  // sky blue
                ) + "\n\n" +
                "<b>" + "RICS.Twitch.ChannelNameTooltip.ExamplesHeader".Translate() + "</b>\n" +
                "• " + "RICS.Twitch.ChannelNameTooltip.Example1".Translate() + "\n" +
                "• " + "RICS.Twitch.ChannelNameTooltip.CaseNote".Translate() + "\n\n" +
                UIUtilities.Colorize(
                    "RICS.Twitch.ChannelNameTooltip.Warning".Translate(),
                    ColorLibrary.Warning       // subtle orange/red
                )
            );

            Rect channelFieldRect = listing.GetRect(30f);
            settings.ChannelName = Widgets.TextField(channelFieldRect, settings.ChannelName);
            // OLD: TooltipHandler.TipRegion(channelFieldRect, "Enter your Twitch channel name here");
            TooltipHandler.TipRegion(channelFieldRect,
                "RICS.Twitch.ChannelNameFieldTooltip".Translate()
            );
            listing.Gap(12f);

            // Bot Account Section
            Text.Font = GameFont.Medium;
            // OLD: listing.Label("Bot Account (Optional)");
            GUI.color = ColorLibrary.SubHeader;
            listing.Label("RICS.Twitch.BotAccountHeader".Translate());
            Text.Font = GameFont.Small;
            GUI.color = Color.white;
            listing.GapLine(6f);
            listing.Gap(4f);

            // Bot username with proper tooltip
            Rect botLabelRect = listing.GetRect(24f);
            // OLD: Widgets.Label(botLabelRect, "Bot Username:");
            Widgets.Label(botLabelRect,
                "RICS.Twitch.BotUsernameLabel".Translate());
            TooltipHandler.TipRegion(botLabelRect,
                UIUtilities.Colorize(
                    "RICS.Twitch.BotUsernameTooltip.Title".Translate(),
                    ColorLibrary.HeaderAccent      // orange for main title
                ) + "\n\n" +

                UIUtilities.Colorize(
                    "RICS.Twitch.BotUsernameTooltip.Recommended".Translate(),
                    ColorLibrary.Success           // green – positive emphasis
                ) + "\n" +
                UIUtilities.Colorize(
                    "RICS.Twitch.BotUsernameTooltip.Alternative".Translate(),
                    ColorLibrary.MutedText         // gray – secondary option
                ) + "\n\n" +

                UIUtilities.Colorize(
                    "RICS.Twitch.BotUsernameTooltip.WhyHeader".Translate(),
                    ColorLibrary.SubHeader         // sky blue for sub-header
                ) + "\n" +
                "• " + "RICS.Twitch.BotUsernameTooltip.Why1".Translate() + "\n" +
                "• " + "RICS.Twitch.BotUsernameTooltip.Why2".Translate() + "\n" +
                "• " + "RICS.Twitch.BotUsernameTooltip.Why3".Translate() + "\n" +
                "• " + "RICS.Twitch.BotUsernameTooltip.Why4".Translate() + "\n\n" +

                UIUtilities.Colorize(
                    "RICS.Twitch.BotUsernameTooltip.MainAccountHeader".Translate(),
                    ColorLibrary.SubHeader         // sky blue again for consistency
                ) + "\n" +
                "• " + "RICS.Twitch.BotUsernameTooltip.Main1".Translate() + "\n" +
                "• " + "RICS.Twitch.BotUsernameTooltip.Main2".Translate()
            );

            Rect botFieldRect = listing.GetRect(30f);
            settings.BotUsername = Widgets.TextField(botFieldRect, settings.BotUsername);
            TooltipHandler.TipRegion(botFieldRect,
                "RICS.Twitch.BotUsernameFieldTooltip".Translate()
            );

            // Bot account status - fixed to not get cut off

            bool usingSeparateBot =
                !string.IsNullOrEmpty(settings.BotUsername) &&
                string.Equals(settings.BotUsername, settings.ChannelName, StringComparison.OrdinalIgnoreCase) == false;
            Rect botStatusRect = listing.GetRect(20f);

            if (usingSeparateBot)
            {
                GUI.color = Color.green;
                Widgets.Label(botStatusRect, "RICS.Twitch.BotAccountStatus.Separate".Translate());
            }
            else
            {
                GUI.color = Color.yellow;
                Widgets.Label(botStatusRect, "RICS.Twitch.BotAccountStatus.Main".Translate());
            }
            GUI.color = Color.white;

            listing.Gap(16f);

            // OAuth Token Section
            Text.Font = GameFont.Medium;
            GUI.color = ColorLibrary.SubHeader;
            listing.Label("RICS.Twitch.AuthenticationHeader".Translate());
            Text.Font = GameFont.Small;
            GUI.color = Color.white;
            listing.GapLine(6f);
            listing.Gap(4f);

            // Access Token label with tooltip
            Rect tokenLabelRect = listing.GetRect(24f);
            Widgets.Label(tokenLabelRect,
                "RICS.Twitch.AccessTokenLabel".Translate());
            TooltipHandler.TipRegion(tokenLabelRect,
                "RICS.Twitch.AccessTokenTooltip".Translate()
            );

            // Token display field - read-only, masked for security
            Rect tokenFieldRect = listing.GetRect(30f);

            // Determine display text
            string tokenDisplay;
            if (string.IsNullOrEmpty(settings.AccessToken))
            {
                tokenDisplay = "RICS.Twitch.AccessTokenEmpty".Translate();  // e.g. "[Click Paste or Get Token below]"
            }
            else
            {
                tokenDisplay = "RICS.Twitch.AccessTokenMasked".Translate(); // e.g. "oauth:••••••••••••••••"
            }

            // Draw the read-only field
            Widgets.TextField(tokenFieldRect, tokenDisplay);  

            // Tooltip with translatable text
            TooltipHandler.TipRegion(tokenFieldRect,
                "RICS.Twitch.AccessTokenFieldTooltip".Translate()  // e.g. "Twitch OAuth token (click buttons below to set)"
            );

            // Token action buttons
            Rect tokenButtonRect = listing.GetRect(35f);
            Rect pasteRect = new Rect(tokenButtonRect.x, tokenButtonRect.y, 140f, 30f);
            Rect getTokenRect = new Rect(pasteRect.xMax + 10f, tokenButtonRect.y, 160f, 30f);

            if (Widgets.ButtonText(pasteRect, "RICS.Twitch.PasteTokenButton".Translate()))
            {
                string clipboardText = GUIUtility.systemCopyBuffer?.Trim();
                if (!string.IsNullOrEmpty(clipboardText))
                {
                    // Auto-add "oauth:" prefix if missing
                    if (!clipboardText.StartsWith("oauth:") && !clipboardText.Contains(" "))
                    {
                        clipboardText = "oauth:" + clipboardText;
                        Messages.Message("RICS.Twitch.AddedOAuthPrefix".Translate(), MessageTypeDefOf.SilentInput);
                    }
                    settings.AccessToken = clipboardText;
                    Messages.Message("RICS.Twitch.TokenPastedSuccess".Translate(), MessageTypeDefOf.PositiveEvent);
                }
                else
                {
                    Messages.Message("RICS.Twitch.ClipboardEmpty".Translate(), MessageTypeDefOf.NegativeEvent);
                }
            }
            TooltipHandler.TipRegion(pasteRect, "RICS.Twitch.PasteTokenTooltip".Translate());

            if (Widgets.ButtonText(getTokenRect, "RICS.Twitch.GetTokenButton".Translate()))
            {
                string message =
                    "<b>" + UIUtilities.Colorize("RICS.Twitch.TokenGeneratorTitle".Translate(), ColorLibrary.HeaderAccent) + "</b>\n\n" +
                    "🔐 <b>" + UIUtilities.Colorize("RICS.Twitch.TokenGeneratorStepsHeader".Translate(), ColorLibrary.SubHeader) + "</b>\n" +
                    "RICS.Twitch.TokenGeneratorStep1".Translate() + "\n" +
                    "RICS.Twitch.TokenGeneratorStep2".Translate() + "\n" +
                    "RICS.Twitch.TokenGeneratorStep3".Translate() + "\n" +
                    "RICS.Twitch.TokenGeneratorStep4".Translate() + "\n" +
                    "RICS.Twitch.TokenGeneratorStep5".Translate() + "\n\n" +
                    "🔒 <b>" + UIUtilities.Colorize("RICS.Twitch.TokenGeneratorSecurityHeader".Translate(), ColorLibrary.SubHeader) + "</b>\n" +
                    "RICS.Twitch.TokenGeneratorSecurityNote1".Translate() + "\n" +
                    "RICS.Twitch.TokenGeneratorSecurityNote2".Translate() + "\n" +
                    "RICS.Twitch.TokenGeneratorSecurityNote3".Translate() + "\n\n" +
                    "RICS.Twitch.TokenGeneratorConfirmation".Translate();  // The final question

                Find.WindowStack.Add(new Dialog_MessageBox(
                    message,
                    "RICS.Twitch.OpenBrowserButton".Translate(),  // "Open Browser"
                    () => Application.OpenURL("https://twitchtokengenerator.com/"),
                    "RICS.Twitch.CancelButton".Translate(),       // "Cancel"
                    null, null, true  // Make it dismissible with Esc, etc.
                ));
            }
            TooltipHandler.TipRegion(getTokenRect, "RICS.Twitch.GetTokenButtonTooltip".Translate());

            // Token status indicator
            listing.Gap(8f);
            if (!string.IsNullOrEmpty(settings.AccessToken))
            {
                Rect tokenStatusRect = listing.GetRect(20f);
                GUI.color = Color.green;
                // OLD: Widgets.Label(tokenStatusRect, "✓ Token configured - Ready to connect");
                Widgets.Label(tokenStatusRect,
                    "RICS.Twitch.TokenStatus.Ready".Translate());
                GUI.color = Color.white;

                // Show token type - ensure enough space
                Rect tokenTypeRect = listing.GetRect(18f);
                bool isBotToken = !string.IsNullOrEmpty(settings.BotUsername) &&
                                  settings.BotUsername.ToLower() != settings.ChannelName.ToLower();

                // OLD: string tokenType = isBotToken ? "Bot account token" : "Main account token";
                string tokenTypeKey = isBotToken ? "RICS.Twitch.TokenType.Bot" : "RICS.Twitch.TokenType.Main";

                // OLD: Widgets.Label(tokenTypeRect, $"Token type: {tokenType}");
                Widgets.Label(tokenTypeRect, "RICS.Twitch.TokenTypePrefix".Translate() + " " + tokenTypeKey.Translate());

                // OLD: TooltipHandler.TipRegion(tokenTypeRect, "Shows which account this token belongs to");
                TooltipHandler.TipRegion(tokenTypeRect,
                    "RICS.Twitch.TokenTypeTooltip".Translate()
                );
            }
            else
            {
                Rect tokenStatusRect = listing.GetRect(20f);
                GUI.color = Color.red;
                // OLD: Widgets.Label(tokenStatusRect, "❌ No token - Cannot connect to Twitch");
                Widgets.Label(tokenStatusRect,
                    "RICS.Twitch.TokenStatus.Missing".Translate());
                GUI.color = Color.white;
            }

            listing.Gap(20f);

            // === Connection Settings ===
            Text.Font = GameFont.Medium;
            GUI.color = ColorLibrary.SubHeader;
            listing.Label("RICS.Twitch.ConnectionSettingsHeader".Translate());  
            Text.Font = GameFont.Small;
            GUI.color = Color.white;
            listing.GapLine(6f);
            listing.Gap(4f);

            // Auto-connect checkbox 
            Rect autoConnectRect = listing.GetRect(30f);
            Widgets.CheckboxLabeled(autoConnectRect, "RICS.Twitch.AutoConnectLabel".Translate(), ref settings.AutoConnect);
            TooltipHandler.TipRegion(autoConnectRect, "RICS.Twitch.AutoConnectTooltip".Translate()
            );

            // Connection status and controls
            listing.Gap(12f);

            if (settings.IsConnected)
            {
                Rect statusRect = listing.GetRect(24f);
                // OLD: Widgets.Label(statusRect, "Status: <color=green>Connected to Twitch</color>");
                string connectedLabel = "RICS.Twitch.ConnectionStatus".Translate() +
                    "<color=green>" + "RICS.Twitch.ConnectedLabel".Translate() + "</color>"; // "Connected to Twitch"
                Widgets.Label(statusRect, connectedLabel); // "Connected to Twitch"
                TooltipHandler.TipRegion(statusRect,
                    UIUtilities.Colorize("RICS.Twitch.ConnectedTooltip1".Translate(), ColorLibrary.Success) + "\n" +
                    "RICS.Twitch.ConnectedTooltipChannel".Translate(settings.ChannelName) +
                    "RICS.Twitch.ConnectedTooltipBot".Translate(settings.BotUsername) +
                    "RICS.Twitch.ConnectedTooltip4".Translate()
                );

                Rect disconnectRect = listing.GetRect(30f);
                //if (Widgets.ButtonText(disconnectRect, "Disconnect from Twitch"))
                if (Widgets.ButtonText(disconnectRect, "RICS.Twitch.DisconnectButton".Translate()))
                {
                    CAPChatInteractiveMod.Instance.TwitchService.Disconnect();
                    // Messages.Message("Disconnected from Twitch chat", MessageTypeDefOf.SilentInput);
                    Messages.Message("RICS.Twitch.DisconnectedMessage".Translate(), MessageTypeDefOf.SilentInput);
                }
                // TooltipHandler.TipRegion(disconnectRect, "Disconnect from Twitch chat");
                TooltipHandler.TipRegion(disconnectRect,"RICS.Twitch.DisconnectButtonTooltip".Translate());
            }
            else
            {
                Rect statusRect = listing.GetRect(24f);
                // Widgets.Label(statusRect, "Status: <color=red>Disconnected</color>");
                Widgets.Label(statusRect,
                    "RICS.Twitch.ConnectionStatus".Translate() +
                    "<color=red>" + "RICS.Twitch.DisconnectedLabel".Translate() + "</color>"); // "Disconnected"


                TooltipHandler.TipRegion(statusRect,
                    "<b>Disconnected from Twitch</b>\n\n" +
                    "RICS is not currently connected to Twitch chat.\n\n" +
                    "Make sure you have:\n" +
                    "• Channel name entered\n" +
                    "• Valid OAuth token\n" +
                    "• Bot username (optional)\n\n" +
                    "Then click 'Connect to Twitch' below.");

                TooltipHandler.TipRegion(statusRect,
                    UIUtilities.Colorize("RICS.Twitch.DisconnectedTooltipTitle".Translate(), ColorLibrary.Danger) + "\n\n" +
                    "RICS.Twitch.DisconnectedTooltipStatus".Translate() + "\n\n" +
                    "RICS.Twitch.DisconnectedTooltipIntro".Translate() + "\n" +
                    "RICS.Twitch.DisconnectedTooltipReq1".Translate() + "\n" +
                    "RICS.Twitch.DisconnectedTooltipReq2".Translate() + "\n" +
                    "RICS.Twitch.DisconnectedTooltipReq3".Translate()
                );

                bool canConnect = settings.CanConnect;
                Rect connectRect = listing.GetRect(30f);

                if (canConnect)
                {
                    //if (Widgets.ButtonText(connectRect, "Connect to Twitch"))
                    if (Widgets.ButtonText(connectRect, "RICS.Twitch.ConnectButton".Translate()))
                    {
                        CAPChatInteractiveMod.Instance.TwitchService.Connect();
                        // Messages.Message("Connecting to Twitch...", MessageTypeDefOf.SilentInput);
                        Messages.Message("RICS.Twitch.ConnectingMessage".Translate(), MessageTypeDefOf.SilentInput);
                    }
                    // TooltipHandler.TipRegion(connectRect, "Connect to Twitch chat");
                    TooltipHandler.TipRegion(connectRect,
                        "RICS.Twitch.ConnectButtonTooltip".Translate()
                    );
                }
                else
                {
                    GUI.color = Color.gray;
                    // Widgets.ButtonText(connectRect, "Connect to Twitch (Missing Settings)");
                    Widgets.ButtonText(connectRect,
                        "RICS.Twitch.ConnectButtonDisabled".Translate()
                    );
                    GUI.color = Color.white;

                    // Show what's missing - with proper spacing
                    listing.Gap(4f);
                    Rect missingRect = listing.GetRect(45f);
                    string missing = "";
                    //if (string.IsNullOrEmpty(settings.ChannelName)) missing += "• Channel name\n";
                    if (string.IsNullOrEmpty(settings.ChannelName)) missing += "RICS.Twitch.MissingChannelName".Translate() + "\n";
                    //if (string.IsNullOrEmpty(settings.AccessToken)) missing += "• OAuth token\n";
                    if (string.IsNullOrEmpty(settings.AccessToken)) missing += "RICS.Twitch.MissingOAuthToken".Translate() + "\n";
                    //if (string.IsNullOrEmpty(settings.BotUsername)) missing += "• Bot username\n";
                    if (string.IsNullOrEmpty(settings.BotUsername)) missing += "RICS.Twitch.MissingBotUsername".Translate() + "\n";

                    if (!string.IsNullOrEmpty(missing))
                    {
                        GUI.color = Color.yellow;
                        string missingnote = "RICS.Twitch.MissingSettingsNote".Translate() + missing; // e.g. "Please fill in the above to connect."
                        Widgets.Label(missingRect, $"{missingnote}");
                        GUI.color = Color.white;
                        // TooltipHandler.TipRegion(missingRect, "Fix these missing settings to connect");
                        TooltipHandler.TipRegion(missingRect,
                            "RICS.Twitch.MissingSettingsTooltip".Translate()
                        );
                    }
                }
            }

            // === Quick Tips Section ===
            listing.Gap(24f);
            Text.Font = GameFont.Medium;
            // listing.Label("Quick Tips");
            listing.Label("RICS.Twitch.QuickTipsHeader".Translate());
            Text.Font = GameFont.Small;
            listing.GapLine(6f);
            listing.Gap(4f);

            Rect tipsRect = listing.GetRect(85f); // More height for tips
            //string tips =
            //    "💡 <b>Common Issues & Solutions:</b>\n" +
            //    "• <b>Token not working?</b> Regenerate at Twitch Token Generator\n" +
            //    "• <b>Bot not joining?</b> Check channel name spelling\n" +
            //    "• <b>Connection drops?</b> Check internet stability\n" +
            //    "• <b>See your own messages?</b> That's normal with main account";
            string tips =
                UIUtilities.Colorize("RICS.Twitch.TipsTitle".Translate(), Color.yellow) + "\n" +  // or any color you like for the header
                "RICS.Twitch.TipsTokenIssue".Translate() + "\n" +
                "RICS.Twitch.TipsBotIssue".Translate() + "\n" +
                "RICS.Twitch.TipsConnectionDrops".Translate() + "\n" +
                "RICS.Twitch.TipsOwnMessages".Translate();
            Widgets.Label(tipsRect, tips);
            // TooltipHandler.TipRegion(tipsRect, "Helpful tips for troubleshooting Twitch connection");
            TooltipHandler.TipRegion(tipsRect,"RICS.Twitch.QuickTipsTooltip".Translate());
            listing.End();
            Widgets.EndScrollView();
        }
    }
}