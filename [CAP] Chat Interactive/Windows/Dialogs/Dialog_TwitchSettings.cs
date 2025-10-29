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
            optionalTitle = "Twitch Settings";
            forceCatchAcceptAndCancelEventEvenIfUnfocused = true;
        }

        public override void DoWindowContents(Rect inRect)
        {
            var listing = new Listing_Standard();
            listing.Begin(inRect);

            listing.CheckboxLabeled("Enable Twitch Integration", ref _settings.Enabled);
            listing.Gap(12f);

            listing.Label("Channel Name:");
            TooltipHandler.TipRegion(listing.GetRect(0f), "Your Twitch channel name\nThis is what appears in your stream URL");
            _settings.ChannelName = listing.TextEntry(_settings.ChannelName);
            listing.Gap(12f);

            listing.Label("Bot Username:");
            TooltipHandler.TipRegion(listing.GetRect(0f), "Your Twitch bot account username\nCan be same as your channel");
            _settings.BotUsername = listing.TextEntry(_settings.BotUsername);
            listing.Gap(12f);
            // Secure token field with tooltip
            listing.Label("Access Token:");
            TooltipHandler.TipRegion(listing.GetRect(0f), "Your Twitch OAuth token\nGet this from Twitch Token Generator");

            // Use a separate variable for the input field
            string tokenInput = listing.TextEntry(_settings.AccessToken);
            if (tokenInput != new string('*', 16) && tokenInput != _settings.AccessToken)
            {
                // Only update if the user actually entered something new
                _settings.AccessToken = tokenInput;
            }

            if (listing.ButtonText("Paste Token from Clipboard"))
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
                    Messages.Message("Twitch token pasted from clipboard!", MessageTypeDefOf.PositiveEvent);
                }
                else
                {
                    Messages.Message("Clipboard is empty!", MessageTypeDefOf.NegativeEvent);
                }
            }

            listing.Gap(12f);

            // Help button for token generation
            if (listing.ButtonText("Get Twitch Access Token"))
            {
                string message = "This will open Twitch Token Generator in your browser.\n\n" +
                                "<b>Instructions:</b>\n" +
                                "• Login with your Twitch account\n" +
                                "• Select 'Bot Chat Token'\n" +
                                "• Copy the generated token\n" +
                                "• Paste it in your settings file manually\n\n" +
                                "Open Twitch Token Generator?";

                Find.WindowStack.Add(new Dialog_MessageBox(message, "Open Browser",
                    () => Application.OpenURL("https://twitchtokengenerator.com/"),
                    "Cancel", null, null, true));
            }

            listing.Gap(12f);
            listing.CheckboxLabeled("Auto-connect on startup", ref _settings.AutoConnect);

            // Connection status and controls
            listing.Gap();
            if (_settings.IsConnected)
            {
                listing.Label("Status: <color=green>Connected</color>");
                if (listing.ButtonText("Disconnect"))
                {
                    CAPChatInteractiveMod.Instance.TwitchService.Disconnect();
                }
            }
            else
            {
                listing.Label("Status: <color=red>Disconnected</color>");
                if (listing.ButtonText("Connect") && _settings.CanConnect)
                {
                    CAPChatInteractiveMod.Instance.TwitchService.Connect();
                    Messages.Message("Attempting to connect to Twitch...", MessageTypeDefOf.SilentInput);
                }
            }

            listing.End();
        }
    }
}