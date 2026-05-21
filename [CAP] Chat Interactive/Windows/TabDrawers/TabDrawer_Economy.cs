// TabDrawer_Economy.cs
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
// Draws the Economy settings tab in the mod settings window
using CAP_ChatInteractive;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using ColorLibrary = CAP_ChatInteractive.ColorLibrary;

namespace _CAP__Chat_Interactive
{
    public static class TabDrawer_Economy
    {

        private static Vector2 _scrollPosition = Vector2.zero;

        public static void Draw(Rect rect)
        {
            var settings = CAPChatInteractiveMod.Instance.Settings.GlobalSettings;

            // Tall virtual view for full scrolling support
            var view = new Rect(0f, 0f, rect.width - 16f, 1580f);
            Widgets.BeginScrollView(rect, ref _scrollPosition, view);

            var listing = new Listing_Standard();
            listing.Begin(view);

            Text.Font = GameFont.Medium;
            GUI.color = ColorLibrary.HeaderAccent;
            listing.Label("RICS.Economy.EconomySettingsHeader".Translate());
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            listing.GapLine(12f);

            // Coin Settings
            GUI.color = ColorLibrary.SubHeader;
            listing.Label("RICS.Economy.CoinEconomyHeader".Translate());
            GUI.color = Color.white;
            UIUtilities.NumericField(listing, "RICS.Economy.StartingCoins".Translate(), "RICS.Economy.StartingCoinsDesc".Translate(), ref settings.StartingCoins, 0, 10000, "startingCoins");
            UIUtilities.NumericField(listing, "RICS.Economy.BaseCoinReward".Translate(), "RICS.Economy.BaseCoinRewardDesc".Translate(), ref settings.BaseCoinReward, 1, 100, "baseCoinReward");
            UIUtilities.NumericField(listing, "RICS.Economy.ActiveViewerMinutes".Translate(), "RICS.Economy.ActiveViewerMinutesDesc".Translate(), ref settings.MinutesForActive, 1, 480, "activeViewerMinutes");
            UIUtilities.NumericField(listing, "RICS.Economy.SubscriberExtraCoins".Translate(), "RICS.Economy.SubscriberExtraCoinsDesc".Translate(), ref settings.SubscriberExtraCoins, 0, 50, "subscriberExtraCoins");
            UIUtilities.NumericField(listing, "RICS.Economy.VIPExtraCoins".Translate(), "RICS.Economy.VIPExtraCoinsDesc".Translate(), ref settings.VipExtraCoins, 0, 50, "vipExtraCoins");
            UIUtilities.NumericField(listing, "RICS.Economy.ModExtraCoins".Translate(), "RICS.Economy.ModExtraCoinsDesc".Translate(), ref settings.ModExtraCoins, 0, 50, "modExtraCoins");

            listing.Gap(20f);

            // Karma Settings
            GUI.color = ColorLibrary.SubHeader;
            listing.Label("RICS.Economy.KarmaSystemHeader".Translate());
            GUI.color = Color.white;

            // === Basic Karma Bounds (float, 2-decimal via helper) ===
            UIUtilities.NumericField(listing, "RICS.Economy.StartingKarma".Translate(), "RICS.Economy.StartingKarmaDesc".Translate(), ref settings.StartingKarma, 0f, 1000f, "startingKarma");
            if (settings.StartingKarma < settings.MinKarma) settings.StartingKarma = settings.MinKarma;
            if (settings.StartingKarma > settings.MaxKarma) settings.StartingKarma = settings.MaxKarma;

            float originalMinKarma = settings.MinKarma;
            UIUtilities.NumericField(listing, "RICS.Economy.MinimumKarma".Translate(), "RICS.Economy.MinimumKarmaDesc".Translate(), ref settings.MinKarma, 0f, 1000f, "minimumKarma");
            if (settings.MinKarma != originalMinKarma && settings.MinKarma > settings.MaxKarma)
            {
                settings.MinKarma = settings.MaxKarma;
            }

            float originalMaxKarma = settings.MaxKarma;
            UIUtilities.NumericField(listing, "RICS.Economy.MaximumKarma".Translate(), "RICS.Economy.MaximumKarmaDesc".Translate(), ref settings.MaxKarma, 0f, 1000f, "maximumKarma");
            if (settings.MaxKarma != originalMaxKarma && settings.MaxKarma < settings.MinKarma)
            {
                settings.MaxKarma = settings.MinKarma;
            }

            // Re-clamp Starting after possible Min/Max change
            if (settings.StartingKarma < settings.MinKarma) settings.StartingKarma = settings.MinKarma;
            if (settings.StartingKarma > settings.MaxKarma) settings.StartingKarma = settings.MaxKarma;

            listing.Gap(20f);

            // === Karma Decay System (prevents permanent 200 karma + store abuse) ===
            GUI.color = ColorLibrary.SubHeader;
            listing.Label("RICS.Economy.KarmaDecayHeader".Translate());
            GUI.color = Color.white;

            UIUtilities.NumericField(listing, "RICS.Economy.KarmaDecayRate".Translate(), "RICS.Economy.KarmaDecayRateDesc".Translate(), ref settings.KarmaDecayRate, 0f, 100f, "karmaDecayRate");
            UIUtilities.NumericField(listing, "RICS.Economy.KarmaDecayInterval".Translate(), "RICS.Economy.KarmaDecayIntervalDesc".Translate(), ref settings.KarmaDecayIntervalMinutes, 1, 60, "karmaDecayInterval");
            UIUtilities.NumericField(listing, "RICS.Economy.KarmaMinDecay".Translate(), "RICS.Economy.KarmaMinDecayDesc".Translate(), ref settings.KarmaMinDecay, 0f, 50f, "karmaMinDecay");
            UIUtilities.NumericField(listing, "RICS.Economy.MinDecayKarma".Translate(), "RICS.Economy.MinDecayKarmaDesc".Translate(), ref settings.KarmaMinDecayFloor, 0f, 1000f, "karmaMinDecayFloor");

            listing.Gap(20f);

            // === Karma from Store Purchases & Events ===
            GUI.color = ColorLibrary.SubHeader;
            listing.Label("RICS.Economy.KarmaStoreEventHeader".Translate());
            GUI.color = Color.white;

            UIUtilities.NumericField(listing, "RICS.Economy.KarmaPerStoreItem".Translate(), "RICS.Economy.KarmaPerStoreItemDesc".Translate(), ref settings.KarmaPerStoreItem, 0f, 100f, "karmaPerStoreItem");
            UIUtilities.NumericField(listing, "RICS.Economy.KarmaGainPerGoodEvent".Translate(), "RICS.Economy.KarmaGainPerGoodEventDesc".Translate(), ref settings.KarmaGainPerGoodEvent, 0f, 200f, "karmaGainPerGoodEvent");
            UIUtilities.NumericField(listing, "RICS.Economy.KarmaGainPerNeutralEvent".Translate(), "RICS.Economy.KarmaGainPerNeutralEventDesc".Translate(), ref settings.KarmaGainPerNeutralEvent, 0f, 200f, "karmaGainPerNeutralEvent");
            UIUtilities.NumericField(listing, "RICS.Economy.KarmaLossPerBadEvent".Translate(), "RICS.Economy.KarmaLossPerBadEventDesc".Translate(), ref settings.KarmaLossPerBadEvent, 0f, 200f, "karmaLossPerBadEvent");
            UIUtilities.NumericField(listing, "RICS.Economy.KarmaLossPerDoomEvent".Translate(), "RICS.Economy.KarmaLossPerDoomEventDesc".Translate(), ref settings.KarmaLossPerDoomEvent, 0f, 200f, "karmaLossPerDoomEvent");
            UIUtilities.NumericField(listing, "RICS.Economy.KarmaPerEventPriceMultiplier".Translate(), "RICS.Economy.KarmaPerEventPriceMultiplierDesc".Translate(), ref settings.KarmaEventPriceMultiplier, 0f, 5f, "karmaEventPriceMultiplier");
            listing.Label("Good/Neutral → + (price × multiplier) | Bad/Doom → - (price × multiplier) | 0.05 = +5 karma per 100 coins");

            listing.Gap(20f);

            // Reset button for the entire Karma section
            Rect resetRect = listing.GetRect(28f);
            //    if (Widgets.ButtonText(resetRect, "Reset Karma Settings to Defaults"))
            if (Widgets.ButtonText(resetRect, "RICS.Economy.ResetKarmaSettings".Translate()))
            {
                // Reset to current CAPGlobalChatSettings field defaults (gentle 1f decay + ultra-low store karma to prevent spam abuse)
                // This now perfectly matches the class field initializers + ExposeData() Scribe_Values defaults
                // We now use 1 = 1% and not 0.01 = 1% to make it more intuitive for users and prevent confusion, so the default is 1f and not 0.01f

                settings.StartingKarma = 100f;
                settings.MinKarma = 0f;
                settings.MaxKarma = 200f;

                settings.KarmaDecayRate = 1f;
                settings.KarmaDecayIntervalMinutes = 30;
                settings.KarmaMinDecay = 0f;
                settings.KarmaMinDecayFloor = 100f;

                settings.KarmaPerStoreItem = 5f;
                settings.KarmaLossPerBadEvent = 12f;
                settings.KarmaGainPerGoodEvent = 5f;
                settings.KarmaGainPerNeutralEvent = 1f;
                settings.KarmaLossPerDoomEvent = 25f;
                settings.KarmaEventPriceMultiplier = 5f;

                // CRITICAL FIX: The NumericField helper in UIUtilities.cs uses a static
                // numericBuffers dictionary to prevent typing flicker.  When we mutate
                // settings values directly (as a reset does), the buffers become stale.
                // Clearing them forces every NumericField to re-read the live settings
                // values on the next frame → instant visible reset.
                UIUtilities.ClearNumericBuffers();

                CAP_ChatInteractive.Logger.Message("[RICS Economy] Karma settings reset to defaults and UI buffers cleared");
            }

            listing.Gap(20f);
            // Currency

            GUI.color = ColorLibrary.SubHeader;
            listing.Label("RICS.Economy.CurrencyNameHeader".Translate());
            GUI.color = Color.white;
            listing.Gap(12f);

            Rect currencyLabelRect = listing.GetRect(Text.LineHeight);
            UIUtilities.LabelWithDescription(currencyLabelRect, "RICS.Economy.CurrencyNameDesc".Translate(), "RICS.Economy.CurrencyNameExample".Translate());
            // Current value 
            listing.Gap(6f);
            listing.Label(string.Format("RICS.Economy.CurrentCurrencyDisplay".Translate(), settings.CurrencyName));
            listing.Label(string.Format("RICS.Economy.CurrencyRawString".Translate(), settings.CurrencyName));
            listing.Label(string.Format("RICS.Economy.CurrencyEmojiSupport".Translate(), settings.CurrencyName));

            // Text entry field
            settings.CurrencyName = listing.TextEntry(settings.CurrencyName).Trim();
            listing.Gap(12f);

            listing.End();
            Widgets.EndScrollView();

            // Optional: clear buffers when user scrolls far away or closes tab (prevents tiny memory growth)
            // Not strictly required but good hygiene
            if (_scrollPosition.y > 1000f) // arbitrary large scroll = user is done editing
                UIUtilities.ClearNumericBuffers();
        }
    }
}