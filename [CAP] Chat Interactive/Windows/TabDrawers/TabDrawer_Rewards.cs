// TabDrawer_Rewards.cs
// Copyright (c) Captolamia. All rights reserved.
// Licensed under the AGPLv3 License. See LICENSE file in the project root for full license information.
// Draws the Rewards Settings tab (Channel Points & Lootboxes)
using CAP_ChatInteractive;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace _CAP__Chat_Interactive
{
    public static class TabDrawer_Rewards
    {
        private static Vector2 _scrollPosition = Vector2.zero;
        private static float _lineHeight = 30f;
        private static float _iconWidth = 24f;

        public static void Draw(Rect region)
        {
            var settings = CAPChatInteractiveMod.Instance.Settings.GlobalSettings;
            var view = new Rect(0f, 0f, region.width - 16f, 1200f);

            Widgets.BeginScrollView(region, ref _scrollPosition, view);
            var listing = new Listing_Standard();
            listing.Begin(view);

            // Channel Points Section
            Text.Font = GameFont.Medium;
            listing.Label("Twitch Channel Points");
            Text.Font = GameFont.Small;
            listing.GapLine(6f);

            // Important note about channel points
            Text.Font = GameFont.Tiny;
            GUI.color = Color.yellow;
            listing.Label("Important: Twitch rewards must be set to 'Require Viewer to Enter Text' or they won't be detected!");
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            listing.Gap(8f);

            listing.CheckboxLabeled("Enable Channel Points Rewards", ref settings.ChannelPointsEnabled);
            listing.CheckboxLabeled("Show Channel Points Debug Messages", ref settings.ShowChannelPointsDebugMessages);

            listing.Gap(12f);

            // Channel Points Rewards Table
            DrawChannelPointsTable(listing, settings);

            listing.Gap(24f);

            // Lootbox Section
            Text.Font = GameFont.Medium;
            listing.Label("Loot Box Settings");
            Text.Font = GameFont.Small;
            listing.GapLine(6f);

            // Coin range
            listing.Label("Coin Range (1-10000):");
            listing.Gap(4f);

            // Create a horizontal layout for min/max inputs
            var coinRangeRect = listing.GetRect(Text.LineHeight);
            var coinMinRect = new Rect(coinRangeRect.x, coinRangeRect.y, 80f, Text.LineHeight);
            var coinMinInputRect = new Rect(coinMinRect.xMax + 4f, coinRangeRect.y, 60f, Text.LineHeight);
            var coinMaxLabelRect = new Rect(coinMinInputRect.xMax + 8f, coinRangeRect.y, 80f, Text.LineHeight);
            var coinMaxInputRect = new Rect(coinMaxLabelRect.xMax + 4f, coinRangeRect.y, 60f, Text.LineHeight);

            Widgets.Label(coinMinRect, "Min:");
            string coinMinBuffer = settings.LootBoxRandomCoinRange.min.ToString();
            UIUtilities.TextFieldNumericFlexible(coinMinInputRect, ref settings.LootBoxRandomCoinRange.min, ref coinMinBuffer, 1, 10000);

            Widgets.Label(coinMaxLabelRect, "Max:");
            string coinMaxBuffer = settings.LootBoxRandomCoinRange.max.ToString();
            UIUtilities.TextFieldNumericFlexible(coinMaxInputRect, ref settings.LootBoxRandomCoinRange.max, ref coinMaxBuffer, 1, 10000);

            listing.Gap(8f);

            // Lootboxes per day
            var perDayRect = listing.GetRect(Text.LineHeight);
            var perDayLabelRect = new Rect(perDayRect.x, perDayRect.y, 150f, Text.LineHeight);
            var perDayInputRect = new Rect(perDayLabelRect.xMax + 8f, perDayRect.y, 80f, Text.LineHeight);

            Widgets.Label(perDayLabelRect, "Lootboxes Per Day:");
            string perDayBuffer = settings.LootBoxesPerDay.ToString();
            UIUtilities.TextFieldNumericFlexible(perDayInputRect, ref settings.LootBoxesPerDay, ref perDayBuffer, 1, 20);

            listing.Gap(8f);

            // Show welcome message
            listing.CheckboxLabeled("Show Welcome Message", ref settings.LootBoxShowWelcomeMessage);
            listing.Gap(4f);

            // Force open all at once
            listing.CheckboxLabeled("Force Open All At Once", ref settings.LootBoxForceOpenAllAtOnce);

            listing.End();
            Widgets.EndScrollView();
        }

        private static void DrawChannelPointsTable(Listing_Standard listing, CAPGlobalChatSettings settings)
        {
            // Safety check - ensure RewardSettings is not null
            if (settings.RewardSettings == null)
            {
                settings.RewardSettings = new List<ChannelPoints_RewardSettings>();
            }

            // Table headers
            var headerRect = listing.GetRect(_lineHeight);
            var nameWidth = headerRect.width * 0.25f;
            var uuidWidth = headerRect.width * 0.35f;
            var coinsWidth = headerRect.width * 0.15f;
            var autoWidth = headerRect.width * 0.1f;
            var enabledWidth = headerRect.width * 0.1f;
            var deleteWidth = headerRect.width * 0.05f;

            var headerRow = new WidgetRow(headerRect.x, headerRect.y, UIDirection.RightThenDown);
            headerRow.Label("Reward Name", nameWidth);
            headerRow.Label("Reward UUID", uuidWidth);
            headerRow.Label("Coins", coinsWidth);
            headerRow.Label("Auto", autoWidth);
            headerRow.Label("Enabled", enabledWidth);
            headerRow.Label("", deleteWidth);

            listing.Gap(4f);

            // Safety check for empty list
            if (settings.RewardSettings.Count == 0)
            {
                var emptyRect = listing.GetRect(_lineHeight);
                Widgets.Label(emptyRect, "No rewards configured. Add one below.");
                listing.Gap(8f);
            }
            else
            {
                // Reward rows
                for (int i = 0; i < settings.RewardSettings.Count; i++)
                {
                    var reward = settings.RewardSettings[i];
                    // Safety check for null reward
                    if (reward == null)
                    {
                        settings.RewardSettings[i] = new ChannelPoints_RewardSettings();
                        reward = settings.RewardSettings[i];
                    }

                    var rowRect = listing.GetRect(_lineHeight);

                    // Name field
                    var nameRect = new Rect(rowRect.x, rowRect.y, nameWidth, _lineHeight);
                    reward.RewardName = Widgets.TextField(nameRect, reward.RewardName ?? "");

                    // UUID field
                    var uuidRect = new Rect(nameRect.xMax, rowRect.y, uuidWidth, _lineHeight);
                    reward.RewardUUID = Widgets.TextField(uuidRect, reward.RewardUUID ?? "");

                    // Coins field
                    var coinsRect = new Rect(uuidRect.xMax, rowRect.y, coinsWidth, _lineHeight);
                    reward.CoinsToAward = Widgets.TextField(coinsRect, reward.CoinsToAward ?? "300");

                    // Auto capture toggle
                    var autoRect = new Rect(coinsRect.xMax, rowRect.y, autoWidth, _lineHeight);
                    Widgets.Checkbox(autoRect.position, ref reward.AutomaticallyCaptureUUID);

                    // Enabled toggle
                    var enabledRect = new Rect(autoRect.xMax, rowRect.y, enabledWidth, _lineHeight);
                    Widgets.Checkbox(enabledRect.position, ref reward.Enabled);

                    // Delete button
                    var deleteRect = new Rect(enabledRect.xMax, rowRect.y, deleteWidth, _lineHeight);
                    if (Widgets.ButtonText(deleteRect, "X"))
                    {
                        settings.RewardSettings.RemoveAt(i);
                        i--; // Adjust index after removal
                    }

                    listing.Gap(4f);
                }
            }

            // Add new reward button
            var addButtonRect = listing.GetRect(30f);
            if (Widgets.ButtonText(addButtonRect, "Add New Reward"))
            {
                // Ensure list is initialized
                if (settings.RewardSettings == null)
                {
                    settings.RewardSettings = new List<ChannelPoints_RewardSettings>();
                }

                settings.RewardSettings.Add(new ChannelPoints_RewardSettings(
                    "New Reward",
                    "",
                    "300",
                    false,
                    true
                ));
            }

            listing.Gap(12f);

            // Auto-capture explanation
            Text.Font = GameFont.Tiny;
            GUI.color = new Color(0.7f, 0.7f, 0.7f);
            listing.Label("Auto Capture: When enabled, this reward will automatically capture the next UUID redeemed on Twitch");
            Text.Font = GameFont.Small;
            GUI.color = Color.white;
        }
    }
}