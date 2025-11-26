// TabDrawer_GameEvents.cs
// Copyright (c) Captolamia. All rights reserved.
// Licensed under the AGPLv3 License. See LICENSE file in the project root for full license information.
// Draws the Game Events & Cooldowns tab in the mod settings window
using CAP_ChatInteractive.Incidents;
using CAP_ChatInteractive.Incidents.Weather;
using CAP_ChatInteractive.Store;
using CAP_ChatInteractive.Traits;
using RimWorld;
using System.Linq;
using UnityEngine;
using Verse;
using ColorLibrary = CAP_ChatInteractive.ColorLibrary;

namespace CAP_ChatInteractive
{
    public static class TabDrawer_GameEvents
    {
        private static Vector2 _scrollPosition = Vector2.zero;

        public static void Draw(Rect region)
        {
            var settings = CAPChatInteractiveMod.Instance.Settings.GlobalSettings;
            var view = new Rect(0f, 0f, region.width - 16f, 800f);

            Widgets.BeginScrollView(region, ref _scrollPosition, view);
            var listing = new Listing_Standard();
            listing.Begin(view);

            // Header
            Text.Font = GameFont.Medium;
            GUI.color = ColorLibrary.Orange;
            listing.Label("Game Events & Cooldowns");
            Text.Font = GameFont.Small;
            GUI.color = ColorLibrary.White;
            listing.GapLine(6f);

            // Description
            listing.Label("Configure cooldowns for events, traits, and store purchases. Manage all game interactions in one place.");
            listing.Gap(12f);

            // COOLDOWN SETTINGS SECTION (Always available - no game required)
            DrawCooldownSettings(listing, settings);

            // Add reset button
            DrawResetButton(listing, settings);

            listing.Gap(24f);

            // OTHER SETTINGS SECTION
            DrawOtherSettings(listing, settings);

            listing.Gap(24f);

            // STATISTICS AND EDITORS SECTION
            DrawStatisticsAndEditors(listing);

            listing.End();
            Widgets.EndScrollView();
        }

        private static void DrawCooldownSettings(Listing_Standard listing, CAPGlobalChatSettings settings)
        {
            Text.Font = GameFont.Medium;
            GUI.color = ColorLibrary.Orange;
            listing.Label("Global Cooldown Settings");
            Text.Font = GameFont.Small;
            GUI.color = ColorLibrary.White;
            listing.GapLine(6f);

            // Event cooldown toggle
            listing.CheckboxLabeled("Enable event cooldowns", ref settings.EventCooldownsEnabled,
                "Turn on/off all event cooldowns. When off, events can be purchased without limits.");

            // Only show the rest if event cooldowns are enabled
            if (settings.EventCooldownsEnabled)
            {
                // Cooldown days
                NumericField(listing, "Event cooldown duration in game days:", ref settings.EventCooldownDays, 1, 90);
                GUI.color = ColorLibrary.LightGray;
                listing.Label($"How many in-game days to count events. Affects: !event, !raid, !militaryaid, !weather");
                GUI.color = ColorLibrary.White;

                // Events per cooldown period
                NumericField(listing, "Events per cooldown period:", ref settings.EventsperCooldown, 1, 1000);
                GUI.color = ColorLibrary.LightGray;
                listing.Label($"Maximum events allowed in {settings.EventCooldownDays} days. 0 = unlimited");
                GUI.color = ColorLibrary.White;

                listing.Gap(12f);

                // Karma type limits toggle
                listing.CheckboxLabeled("Limit events by karma type", ref settings.KarmaTypeLimitsEnabled,
                    "Set different limits for good, bad, and neutral events");

                if (settings.KarmaTypeLimitsEnabled)
                {
                    listing.Gap(4f);
                    NumericField(listing, "Maximum bad event purchases:", ref settings.MaxBadEvents, 1, 100);
                    GUI.color = ColorLibrary.LightGray;
                    listing.Label($"Bad events: !raid and other harmful events");
                    GUI.color = ColorLibrary.White;

                    NumericField(listing, "Maximum good event purchases:", ref settings.MaxGoodEvents, 1, 100);
                    GUI.color = ColorLibrary.LightGray;
                    listing.Label($"Good events: !militaryaid and other helpful events");
                    GUI.color = ColorLibrary.White;

                    NumericField(listing, "Maximum neutral event purchases:", ref settings.MaxNeutralEvents, 1, 100);
                    GUI.color = ColorLibrary.LightGray;
                    listing.Label($"Neutral events: !weather and other neutral events");
                    GUI.color = ColorLibrary.White;
                }

                listing.Gap(12f);

                // Store purchase limits
                NumericField(listing, "Maximum item purchases per period:", ref settings.MaxItemPurchases, 1, 1000);
                GUI.color = ColorLibrary.LightGray;
                listing.Label($"Maximum !buy, !equip, !wear, !healpawn, !revivepawn commands in {settings.EventCooldownDays} days");
                GUI.color = ColorLibrary.White;
            }
            else
            {
                // Show a message when cooldowns are disabled
                listing.Gap(8f);
                GUI.color = ColorLibrary.LightGray;
                listing.Label("Event cooldowns are disabled. All events can be purchased without limits.");
                GUI.color = ColorLibrary.White;
            }
        }

        private static void DrawOtherSettings(Listing_Standard listing, CAPGlobalChatSettings settings)
        {
            Text.Font = GameFont.Medium;
            GUI.color = ColorLibrary.Orange;
            listing.Label("Other Settings");
            Text.Font = GameFont.Small;
            GUI.color = ColorLibrary.White;
            listing.GapLine(6f);

            // Max Traits setting
            NumericField(listing, "Max traits for a pawn:", ref settings.MaxTraits, 1, 20);
            Text.Font = GameFont.Tiny;
            listing.Label($"Maximum number of traits a single pawn can have");
            Text.Font = GameFont.Small;
        }

        private static void DrawStatisticsAndEditors(Listing_Standard listing)
        {
            bool gameLoaded = Current.ProgramState == ProgramState.Playing;

            Text.Font = GameFont.Medium;
            GUI.color = ColorLibrary.Orange;
            listing.Label("Event Management");
            Text.Font = GameFont.Small;
            GUI.color = ColorLibrary.White;

            listing.GapLine(6f);
            GUI.color = Color.white;

            if (!gameLoaded)
            {
                GUI.color = Color.white;
                listing.Label("Load a game to access event editors and statistics");
                listing.Gap(12f);
                return;
            }

            // Statistics row
            GUI.color = Color.white;
            DrawStatisticsRow(listing);

            listing.Gap(12f);

            // Editor buttons row
            GUI.color = Color.white;
            DrawEditorButtons(listing);

            listing.Gap(12f); // Add extra space after buttons
        }

        private static void DrawStatisticsRow(Listing_Standard listing)
        {
            // Calculate statistics
            int totalStoreItems = StoreInventory.AllStoreItems.Count;
            int enabledStoreItems = StoreInventory.GetEnabledItems().Count();

            int totalTraits = TraitsManager.AllBuyableTraits.Count;
            int enabledTraits = TraitsManager.GetEnabledTraits().Count();

            int totalWeather = BuyableWeatherManager.AllBuyableWeather.Count;
            int enabledWeather = BuyableWeatherManager.AllBuyableWeather.Values.Count(w => w.Enabled);

            // ADD EVENTS STATISTICS
            int totalEvents = IncidentsManager.AllBuyableIncidents?.Count ?? 0;
            int enabledEvents = IncidentsManager.AllBuyableIncidents?.Values.Count(e => e.Enabled) ?? 0;

            Text.Font = GameFont.Small;
            GUI.color = ColorLibrary.SkyBlue;
            listing.Label("Current Statistics:");
            Text.Font = GameFont.Tiny;

            listing.Label($"  • Store: {enabledStoreItems}/{totalStoreItems} items enabled");
            listing.Label($"  • Traits: {enabledTraits}/{totalTraits} traits enabled");
            listing.Label($"  • Weather: {enabledWeather}/{totalWeather} types enabled");
            listing.Label($"  • Events: {enabledEvents}/{totalEvents} events enabled"); // ADD THIS LINE

            Text.Font = GameFont.Small;
        }

        private static void DrawEditorButtons(Listing_Standard listing)
        {
            // Create a rect for the button row
            Rect buttonRow = listing.GetRect(30f);
            float buttonWidth = (buttonRow.width - 30f) / 4f; // Changed from 20f/3f to 30f/4f for 4 buttons

            // Store Editor Button
            Rect storeRect = new Rect(buttonRow.x, buttonRow.y, buttonWidth, 30f);
            if (Widgets.ButtonText(storeRect, "Store Editor"))
            {
                Find.WindowStack.Add(new Dialog_StoreEditor());
            }

            // Traits Editor Button  
            Rect traitsRect = new Rect(buttonRow.x + buttonWidth + 10f, buttonRow.y, buttonWidth, 30f);
            if (Widgets.ButtonText(traitsRect, "Traits Editor"))
            {
                Find.WindowStack.Add(new Dialog_TraitsEditor());
            }

            // Weather Editor Button
            Rect weatherRect = new Rect(buttonRow.x + (buttonWidth + 10f) * 2, buttonRow.y, buttonWidth, 30f);
            if (Widgets.ButtonText(weatherRect, "Weather Editor"))
            {
                Find.WindowStack.Add(new Dialog_WeatherEditor());
            }

            // Events Editor Button - NEW
            Rect eventsRect = new Rect(buttonRow.x + (buttonWidth + 10f) * 3, buttonRow.y, buttonWidth, 30f);
            if (Widgets.ButtonText(eventsRect, "Events Editor"))
            {
                Find.WindowStack.Add(new Dialog_EventsEditor());
            }
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

        private static void DrawResetButton(Listing_Standard listing, CAPGlobalChatSettings settings)
        {
            listing.Gap(12f);

            Rect buttonRect = listing.GetRect(30f);
            if (Widgets.ButtonText(buttonRect, "Reset to Default Values"))
            {
                // Create confirmation dialog
                Find.WindowStack.Add(new Dialog_MessageBox(
                    "Reset all cooldown settings to default values?\n\nThis will reset:\n• Event cooldown days\n• Event limits\n• Karma type limits\n• Purchase limits",
                    "Reset",
                    () => ResetToDefaults(settings),
                    "Cancel"
                ));
            }

            GUI.color = ColorLibrary.LightGray;
            listing.Label("Reset all cooldown settings back to default values");
            GUI.color = ColorLibrary.White;
        }

        private static void ResetToDefaults(CAPGlobalChatSettings settings)
        {
            // Reset all cooldown-related settings to defaults
            settings.EventCooldownsEnabled = true;
            settings.EventCooldownDays = 5;
            settings.EventsperCooldown = 25;
            settings.KarmaTypeLimitsEnabled = false;
            settings.MaxBadEvents = 3;
            settings.MaxGoodEvents = 10;
            settings.MaxNeutralEvents = 10;
            settings.MaxItemPurchases = 50;

            // Optional: Also reset MaxTraits if you want
            settings.MaxTraits = 4;

            // Save the changes
            CAPChatInteractiveMod.Instance.Settings.Write();

            Messages.Message("Cooldown settings reset to defaults", MessageTypeDefOf.PositiveEvent);
        }
    }
}