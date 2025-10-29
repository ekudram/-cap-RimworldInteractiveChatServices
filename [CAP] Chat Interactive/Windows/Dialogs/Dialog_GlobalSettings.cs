using UnityEngine;
using Verse;

namespace CAP_ChatInteractive
{
    public class Dialog_GlobalSettings : Window
    {
        private CAPGlobalChatSettings _settings;
        public override Vector2 InitialSize => new Vector2(600f, 700f);

        public Dialog_GlobalSettings(CAPGlobalChatSettings settings)
        {
            _settings = settings;
            doCloseButton = true;
            forcePause = false;
            absorbInputAroundWindow = true;
            closeOnAccept = false;
            closeOnCancel = true;

            optionalTitle = "Global Chat Settings";
            forceCatchAcceptAndCancelEventEvenIfUnfocused = true;
        }

        public override void DoWindowContents(Rect inRect)
        {
            var listing = new Listing_Standard();
            listing.Begin(inRect);

            // Debug and Logging Section
            Text.Font = GameFont.Medium;
            listing.Label("Debug & Logging");
            Text.Font = GameFont.Small;
            listing.GapLine(6f);
            listing.CheckboxLabeled("Enable Debug Logging", ref _settings.EnableDebugLogging);
            listing.CheckboxLabeled("Log All Chat Messages", ref _settings.LogAllMessages);

            // Cooldown setting with slider
            listing.Label($"Message Cooldown: {_settings.MessageCooldownSeconds} seconds");
            _settings.MessageCooldownSeconds = (int)listing.Slider(_settings.MessageCooldownSeconds, 1, 10);

            listing.Gap(24f);

            // Command Prefixes Section
            Text.Font = GameFont.Medium;
            listing.Label("Command Prefixes");
            Text.Font = GameFont.Small;
            listing.GapLine(6f);

            // Command prefix
            listing.Label("Command Prefix - The prefix to use for all chat commands.");
            Text.Font = GameFont.Tiny;
            listing.Label("Prefixes cannot start with: / . \\ or contain spaces");
            Text.Font = GameFont.Small;

            string commandPrefix = listing.TextEntryLabeled("Command Prefix:", _settings.Prefix);
            if (IsValidPrefix(commandPrefix))
            {
                _settings.Prefix = commandPrefix;
            }
            else if (!string.IsNullOrEmpty(commandPrefix))
            {
                GUI.color = Color.red;
                listing.Label("Invalid prefix! Must not start with / . \\ or contain spaces");
                GUI.color = Color.white;
            }

            listing.Gap(12f);

            // Purchase prefix
            listing.Label("Purchase Prefix - The prefix to use as a substitute for !buy.");
            Text.Font = GameFont.Tiny;
            listing.Label("Prefixes cannot start with: / . \\ or contain spaces");
            Text.Font = GameFont.Small;

            string buyPrefix = listing.TextEntryLabeled("Purchase Prefix:", _settings.BuyPrefix);
            if (IsValidPrefix(buyPrefix))
            {
                _settings.BuyPrefix = buyPrefix;
            }
            else if (!string.IsNullOrEmpty(buyPrefix))
            {
                GUI.color = Color.red;
                listing.Label("Invalid prefix! Must not start with / . \\ or contain spaces");
                GUI.color = Color.white;
            }

            listing.Gap(24f);

            // Economy Settings Section - Let's start with just a few to test
            Text.Font = GameFont.Medium;
            listing.Label("Economy Settings");
            Text.Font = GameFont.Small;
            listing.GapLine(6f);

            // Just test with a couple of fields first
            NumericField(listing, "Starting Coins:", ref _settings.StartingCoins, 0, 10000);
            NumericField(listing, "Base Coin Reward:", ref _settings.BaseCoinReward, 1, 100);

            listing.End();
        }

        private bool IsValidPrefix(string prefix)
        {
            if (string.IsNullOrEmpty(prefix)) return false;
            if (prefix.Contains(" ")) return false;
            if (prefix.StartsWith("/") || prefix.StartsWith(".") || prefix.StartsWith("\\")) return false;
            return true;
        }

        private void NumericField(Listing_Standard listing, string label, ref int value, int min, int max)
        {
            // Get a rect for this control
            Rect rect = listing.GetRect(Text.LineHeight);

            // Split the rect for label and input
            Rect leftRect = rect.LeftPart(0.6f).Rounded();
            Rect rightRect = rect.RightPart(0.4f).Rounded();

            // Label
            Widgets.Label(leftRect, label);

            // Text field for numeric input
            string buffer = value.ToString();
            Widgets.TextFieldNumeric(rightRect, ref value, ref buffer, min, max);
        }
    }
}