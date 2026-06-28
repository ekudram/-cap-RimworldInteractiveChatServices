// Dialog_TwitchSettings.cs
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
// A dialog window for configuring Twitch integration settings
using RimWorld;
using UnityEngine;
using Verse;

namespace CAP_ChatInteractive
{
    public class Dialog_TwitchSettings : Window
    {
        private StreamServiceSettings _settings;

        public override Vector2 InitialSize => new Vector2(600f, 700f);
        public Dialog_TwitchSettings(StreamServiceSettings settings)
        {
            _settings = settings;
            doCloseButton = true;
            forcePause = false;
            absorbInputAroundWindow = true;
            closeOnAccept = false;
            closeOnCancel = true;

            // Set proper window size
            optionalTitle = "RICS.TwitchSettings.Title".Translate();
            forceCatchAcceptAndCancelEventEvenIfUnfocused = true;
        }

        public override void DoWindowContents(Rect inRect)
        {
            var listing = new Listing_Standard();
            listing.Begin(inRect);

            listing.CheckboxLabeled("RICS.TwitchSettings.EnableIntegration".Translate(), ref _settings.Enabled);
            listing.Gap(12f);

            listing.Label("RICS.TwitchSettings.ChannelName".Translate());
            TooltipHandler.TipRegion(listing.GetRect(0f), "RICS.TwitchSettings.ChannelNameTooltip".Translate());
            _settings.ChannelName = listing.TextEntry(_settings.ChannelName);
            listing.Gap(12f);

            listing.Label("RICS.TwitchSettings.BotUsername".Translate());
            TooltipHandler.TipRegion(listing.GetRect(0f), "RICS.TwitchSettings.BotUsernameTooltip".Translate());
            _settings.BotUsername = listing.TextEntry(_settings.BotUsername);
            listing.Gap(12f);
            // Secure token field with tooltip
            listing.Label("RICS.TwitchSettings.AccessToken".Translate());
            TooltipHandler.TipRegion(listing.GetRect(0f), "RICS.TwitchSettings.AccessTokenTooltip".Translate());

            // Use a separate variable for the input field
            string tokenInput = listing.TextEntry(_settings.AccessToken);
            if (tokenInput != new string('*', 16) && tokenInput != _settings.AccessToken)
            {
                // Only update if the user actually entered something new
                _settings.AccessToken = tokenInput;
            }

            if (listing.ButtonText("RICS.TwitchSettings.PasteToken".Translate()))
            {
                string clipboardText = GUIUtility.systemCopyBuffer?.Trim();
                if (!string.IsNullOrEmpty(clipboardText))
                {
                    // Auto-add "oauth:" prefix if missing
                    if (!clipboardText.StartsWith("oauth:") && !clipboardText.Contains(" "))
                    {
                        clipboardText = "oauth:" + clipboardText;
                    }
                    _settings.AccessToken = clipboardText;
                    Messages.Message("RICS.TwitchSettings.TokenPasted".Translate(), MessageTypeDefOf.PositiveEvent);
                }
                else
                {
                    Messages.Message("RICS.TwitchSettings.ClipboardEmpty".Translate(), MessageTypeDefOf.NegativeEvent);
                }
            }

            listing.Gap(12f);

            // Help button for token generation
            if (listing.ButtonText("RICS.TwitchSettings.GetToken".Translate()))
            {
                string message = "RICS.TwitchSettings.GetTokenMessage".Translate();

                Find.WindowStack.Add(new Dialog_MessageBox(message, "RICS.TwitchSettings.OpenBrowser".Translate(),
                    () => Application.OpenURL("https://twitchtokengenerator.com/"),
                    "RICS.TwitchSettings.Cancel".Translate(), null, null, true));
            }

            listing.Gap(12f);
            listing.CheckboxLabeled("RICS.TwitchSettings.AutoConnect".Translate(), ref _settings.AutoConnect);

            // Connection status and controls
            listing.Gap();
            if (_settings.IsConnected)
            {
                listing.Label("RICS.TwitchSettings.StatusConnected".Translate());
                if (listing.ButtonText("RICS.TwitchSettings.Disconnect".Translate()))
                {
                    CAPChatInteractiveMod.Instance.TwitchService.Disconnect();
                }
            }
            else
            {
                listing.Label("RICS.TwitchSettings.StatusDisconnected".Translate());
                if (listing.ButtonText("RICS.TwitchSettings.Connect".Translate()) && _settings.CanConnect)
                {
                    CAPChatInteractiveMod.Instance.TwitchService.Connect();
                    Messages.Message("RICS.TwitchSettings.Connecting".Translate(), MessageTypeDefOf.SilentInput);
                }
            }

            listing.End();
        }
    }
}