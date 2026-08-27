// Source/RICS/UI/TabDrawer_Kick.cs
// Copyright (c) Captolamia
// This file is part of RICS (Rimworld Interactive Chat Services).
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
// Simple, streamer-friendly Kick.com settings tab.
// Read: public Pusher. Write: OAuth 2.1 authorization-code + PKCE (chat:write).

using CAP_ChatInteractive;
using RimWorld;
using UnityEngine;
using Verse;
using ColorLibrary = CAP_ChatInteractive.ColorLibrary;

namespace _CAP__Chat_Interactive
{
    public static class TabDrawer_Kick
    {
        private static Vector2 _scrollPosition = Vector2.zero;

        public static void Draw(Rect region)
        {
            var settings = CAPChatInteractiveMod.Instance.Settings.KickSettings;
            var kick = CAPChatInteractiveMod.Instance.KickService;
            var view = new Rect(0f, 0f, region.width - 16f, 860f);

            Widgets.BeginScrollView(region, ref _scrollPosition, view);
            var listing = new Listing_Standard();
            listing.Begin(view);

            Text.Font = GameFont.Medium;
            GUI.color = ColorLibrary.HeaderAccent;
            listing.Label("RICS.Kick.KickIntegrationHeader".Translate());
            GUI.color = Color.white;
            Text.Font = GameFont.Small;

            listing.Gap(8f);
            listing.CheckboxLabeled("RICS.Kick.EnableIntegrationLabel".Translate(), ref settings.Enabled);
            TooltipHandler.TipRegion(listing.GetRect(0f), "RICS.Kick.EnableIntegrationTooltip".Translate());

            listing.Gap(8f);
            GUI.color = Color.yellow;
            listing.Label("RICS.Kick.LiveWarning".Translate());
            GUI.color = Color.white;
            listing.Gap(4f);
            listing.Label("RICS.Kick.ReqActive".Translate());
            listing.Gap(12f);

            Text.Font = GameFont.Medium;
            GUI.color = ColorLibrary.SubHeader;
            listing.Label("RICS.Kick.ChannelInformationHeader".Translate());
            Text.Font = GameFont.Small;
            GUI.color = Color.white;
            listing.GapLine(6f);

            Rect channelLabelRect = listing.GetRect(24f);
            Widgets.Label(channelLabelRect, "RICS.Kick.ChannelSlugLabel".Translate());
            TooltipHandler.TipRegion(channelLabelRect, "RICS.Kick.ChannelSlugTooltip".Translate());

            Rect channelFieldRect = listing.GetRect(30f);
            settings.ChannelName = Widgets.TextField(channelFieldRect, settings.ChannelName);
            TooltipHandler.TipRegion(channelFieldRect, "RICS.Kick.ChannelSlugFieldTooltip".Translate());

            listing.Gap(16f);

            Text.Font = GameFont.Medium;
            GUI.color = ColorLibrary.SubHeader;
            listing.Label("RICS.Kick.OAuthCredentialsHeader".Translate());
            Text.Font = GameFont.Small;
            GUI.color = Color.white;
            listing.GapLine(6f);

            Rect clientIdLabelRect = listing.GetRect(24f);
            Widgets.Label(clientIdLabelRect, "RICS.Kick.ClientIdLabel".Translate());
            TooltipHandler.TipRegion(clientIdLabelRect, "RICS.Kick.ClientIdTooltip".Translate());
            Rect clientIdFieldRect = listing.GetRect(30f);
            settings.ClientId = Widgets.TextField(clientIdFieldRect, settings.ClientId);

            listing.Gap(8f);

            Rect secretLabelRect = listing.GetRect(24f);
            Widgets.Label(secretLabelRect, "RICS.Kick.ClientSecretLabel".Translate());
            TooltipHandler.TipRegion(secretLabelRect, "RICS.Kick.ClientSecretTooltip".Translate());

            Rect secretFieldRect = listing.GetRect(30f);
            string secretDisplay = string.IsNullOrEmpty(settings.ClientSecret)
                ? "RICS.Kick.ClientSecretPlaceholder".Translate()
                : "••••••••••••••••";
            Widgets.TextField(secretFieldRect, secretDisplay);

            Rect pasteSecretRect = listing.GetRect(30f);
            if (Widgets.ButtonText(pasteSecretRect, "RICS.Kick.PasteSecretButton".Translate()))
            {
                string clipboard = GUIUtility.systemCopyBuffer?.Trim();
                if (!string.IsNullOrEmpty(clipboard))
                {
                    settings.ClientSecret = clipboard;
                    Messages.Message("RICS.Kick.SecretPastedSuccess".Translate(), MessageTypeDefOf.PositiveEvent);
                }
                else
                {
                    Messages.Message("RICS.Kick.ClipboardEmpty".Translate(), MessageTypeDefOf.NegativeEvent);
                }
            }
            TooltipHandler.TipRegion(pasteSecretRect, "RICS.Kick.PasteSecretTooltip".Translate());

            listing.Gap(8f);
            Rect redirectLabelRect = listing.GetRect(24f);
            Widgets.Label(redirectLabelRect, "RICS.Kick.RedirectUriLabel".Translate());
            TooltipHandler.TipRegion(redirectLabelRect, "RICS.Kick.RedirectUriTooltip".Translate());

            Rect redirectFieldRect = listing.GetRect(30f);
            string redirectShown = string.IsNullOrWhiteSpace(settings.RedirectUri)
                ? KickService.DefaultRedirectUri
                : settings.RedirectUri;
            string redirectEdited = Widgets.TextField(redirectFieldRect, redirectShown);
            if (redirectEdited != KickService.DefaultRedirectUri || !string.IsNullOrWhiteSpace(settings.RedirectUri))
                settings.RedirectUri = redirectEdited == KickService.DefaultRedirectUri ? "" : redirectEdited;

            listing.Gap(8f);
            bool canAuthorize = !string.IsNullOrEmpty(settings.ClientId) && !string.IsNullOrEmpty(settings.ClientSecret);
            if (canAuthorize)
            {
                if (listing.ButtonText("RICS.Kick.AuthorizeButton".Translate()))
                    kick?.BeginUserAuthorization();
            }
            else
            {
                GUI.color = Color.gray;
                listing.ButtonText("RICS.Kick.AuthorizeDisabledButton".Translate());
                GUI.color = Color.white;
            }
            TooltipHandler.TipRegion(listing.GetRect(0f), "RICS.Kick.AuthorizeTooltip".Translate());

            listing.Gap(8f);
            bool writeReady = !string.IsNullOrEmpty(settings.AccessToken);
            if (writeReady)
            {
                GUI.color = Color.green;
                string who = string.IsNullOrEmpty(settings.BotUsername) ? "Kick user" : settings.BotUsername;
                listing.Label("RICS.Kick.WriteReady".Translate(who));
                GUI.color = Color.white;
            }
            else
            {
                GUI.color = Color.yellow;
                listing.Label("RICS.Kick.WriteNotReady".Translate());
                GUI.color = Color.white;
            }

            listing.Gap(16f);

            Text.Font = GameFont.Medium;
            GUI.color = ColorLibrary.SubHeader;
            listing.Label("RICS.Kick.ConnectionSettingsHeader".Translate());
            Text.Font = GameFont.Small;
            GUI.color = Color.white;
            listing.GapLine(6f);

            listing.CheckboxLabeled("RICS.Kick.AutoConnectLabel".Translate(), ref settings.AutoConnect);
            TooltipHandler.TipRegion(listing.GetRect(0f), "RICS.Kick.AutoConnectTooltip".Translate());

            listing.Gap(12f);

            string status = "RICS.Kick.Status".Translate(
                ColorLibrary.Colorize(settings.IsConnected ? "RICS.Kick.Connected".Translate() : "RICS.Kick.Disconnected".Translate(),
                    settings.IsConnected ? Color.green : Color.red)
            );
            listing.Label(status);

            bool canConnect = KickService.HasKickConnectCredentials(settings);

            if (canConnect)
            {
                if (listing.ButtonText("RICS.Kick.ConnectButton".Translate()))
                {
                    CAPChatInteractiveMod.Instance.KickService.Connect();
                    Messages.Message("RICS.Kick.ConnectingMessage".Translate(), MessageTypeDefOf.SilentInput);
                }
            }
            else
            {
                GUI.color = Color.gray;
                listing.ButtonText("RICS.Kick.CannotConnectButton".Translate());
                GUI.color = Color.white;
                TooltipHandler.TipRegion(listing.GetRect(0f), "RICS.Kick.CannotConnectTooltip".Translate());
            }

            listing.Gap(20f);
            GUI.color = ColorLibrary.MutedText;
            listing.Label("RICS.Kick.QuickTip".Translate());
            GUI.color = Color.white;

            listing.End();
            Widgets.EndScrollView();
        }
    }
}
