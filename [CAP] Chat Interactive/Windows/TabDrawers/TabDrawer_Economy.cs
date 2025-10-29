using CAP_ChatInteractive;
using UnityEngine;
using Verse;

namespace _CAP__Chat_Interactive
{
    public static class TabDrawer_Economy
    {
        public static void Draw(Rect rect)
        {
            var settings = CAPChatInteractiveMod.Instance.Settings.GlobalSettings;
            var listing = new Listing_Standard();
            listing.Begin(rect);

            Text.Font = GameFont.Medium;
            listing.Label("Economy Settings");
            Text.Font = GameFont.Small;
            listing.GapLine(6f);

            // Coin Settings
            listing.Label("Coin Economy");
            NumericField(listing, "Starting Coins:", ref settings.StartingCoins, 0, 10000);
            NumericField(listing, "Base Coin Reward:", ref settings.BaseCoinReward, 1, 100);
            NumericField(listing, "Subscriber Extra Coins:", ref settings.SubscriberExtraCoins, 0, 50);
            NumericField(listing, "VIP Extra Coins:", ref settings.VipExtraCoins, 0, 50);
            NumericField(listing, "Mod Extra Coins:", ref settings.ModExtraCoins, 0, 50);

            listing.Gap(12f);

            // Karma Settings  
            listing.Label("Karma System");
            NumericField(listing, "Starting Karma:", ref settings.StartingKarma, 0, 1000);
            NumericField(listing, "Minimum Karma:", ref settings.MinKarma, -1000, 1000);
            NumericField(listing, "Maximum Karma:", ref settings.MaxKarma, 0, 10000);
            NumericField(listing, "Active Viewer Minutes:", ref settings.MinutesForActive, 1, 1440);

            listing.Gap(12f);

            // Currency
            listing.Label("Currency Name:");
            settings.CurrencyName = listing.TextEntry(settings.CurrencyName);

            listing.End();
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