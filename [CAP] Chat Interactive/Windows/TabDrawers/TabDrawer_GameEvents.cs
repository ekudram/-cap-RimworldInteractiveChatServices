// TabDrawer_GameEvents.cs
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
// Draws the Game Events & Cooldowns tab in the mod settings window
using CAP_ChatInteractive.Incidents;
using CAP_ChatInteractive.Incidents.Weather;
using CAP_ChatInteractive.Store;
using CAP_ChatInteractive.Traits;
using RimWorld;
using System.Linq;
using UnityEngine;
using Verse;
using Verse.Noise;
using ColorLibrary = CAP_ChatInteractive.ColorLibrary;

namespace CAP_ChatInteractive
{
    public static class TabDrawer_GameEvents
    {
        private static Vector2 _scrollPosition = Vector2.zero;

        public static void Draw(Rect region)
        {
            var settings = CAPChatInteractiveMod.Instance.Settings.GlobalSettings;

            // Calculate dynamic height based on content
            float contentHeight = CalculateContentHeight(settings);
            var view = new Rect(0f, 0f, region.width - 16f, contentHeight);

            Widgets.BeginScrollView(region, ref _scrollPosition, view);
            var listing = new Listing_Standard();
            listing.Begin(view);

            // === HEADER ===
            Text.Font = GameFont.Medium;
            GUI.color = ColorLibrary.HeaderAccent;
            // OLD: listing.Label("Game Events & Cooldowns");
            listing.Label("RICS.GameEvents.Header".Translate());           // ← NEW
            Text.Font = GameFont.Small;
            GUI.color = Color.white;
            listing.GapLine(6f);

            // === DESCRIPTION ===
            // OLD: listing.Label("Configure cooldowns for events, traits, and store purchases. Manage all game interactions in one place.");
            listing.Label("RICS.GameEvents.MainDescription".Translate());   // ← NEW
            listing.Gap(12f);

            // COOLDOWN SETTINGS SECTION (Always available - no game required)
            DrawCooldownSettings(listing, settings);

            // Add reset button
            DrawResetButton(listing, settings);

            listing.Gap(24f);

            DrawAnomalySettings(listing, settings);

            listing.Gap(24f);

            // OTHER SETTINGS SECTION
            DrawOtherSettings(listing, settings);

            listing.Gap(24f);

            // STATISTICS AND EDITORS SECTION
            DrawStatisticsAndEditors(listing);

            listing.End();
            Widgets.EndScrollView();
        }

        private static float CalculateContentHeight(CAPGlobalChatSettings settings)
        {
            // Base heights for different sections
            float headerHeight = 60f; // Header + description
            float cooldownHeight = CalculateCooldownSectionHeight(settings);
            float resetButtonHeight = 50f; // Button + gap
            float anomalySettingsHeight = 340f; // Header, two checkboxes, two paragraphs
            float otherSettingsHeight = 40f; // Max traits section
            float statisticsHeight = CalculateStatisticsSectionHeight();

            // Total height with gaps
            return headerHeight + cooldownHeight + resetButtonHeight + anomalySettingsHeight + otherSettingsHeight + statisticsHeight + 200f; // Extra padding
        }

        private static float CalculateCooldownSectionHeight(CAPGlobalChatSettings settings)
        {
            float height = 120f; // Base cooldown section (header + toggle + basic fields)

            if (settings.EventCooldownsEnabled)
            {
                height += 120f; // Cooldown days + events per period

                if (settings.KarmaTypeLimitsEnabled)
                {
                    height += 120f; // Karma type limits (3 fields + descriptions)
                }

                height += 60f; // Store purchase limits
            }
            else
            {
                height += 40f; // Just the disabled message
            }

            return height;
        }

        private static float CalculateStatisticsSectionHeight()
        {
            bool gameLoaded = Current.ProgramState == ProgramState.Playing;

            if (!gameLoaded)
            {
                return 80f; // Just the "load a game" message
            }

            return 160f; // Statistics + editor buttons with proper spacing
        }

        private static void DrawCooldownSettings(Listing_Standard listing, CAPGlobalChatSettings settings)
        {
            // === SECTION TITLE ===
            Text.Font = GameFont.Medium;
            GUI.color = ColorLibrary.HeaderAccent;
            // OLD: listing.Label("Global Cooldown Settings");
            listing.Label("RICS.GameEvents.GlobalCooldownSettings".Translate());   // ← NEW
            Text.Font = GameFont.Small;
            GUI.color = Color.white;
            listing.GapLine(6f);

            // === MAIN TOGGLE ===
            // OLD:
            // listing.CheckboxLabeled("Enable event cooldowns", ref settings.EventCooldownsEnabled,
            //     "Turn on/off all event cooldowns. When off, events can be purchased without limits.");
            listing.CheckboxLabeled(
                "RICS.GameEvents.EnableEventCooldowns".Translate(),
                ref settings.EventCooldownsEnabled,
                "RICS.GameEvents.EnableEventCooldownsDesc".Translate()
            );

            // Only show the rest if event cooldowns are enabled
            if (settings.EventCooldownsEnabled)
            {
                // Cooldown days
                NumericField(listing,
                        "RICS.GameEvents.EventCooldownDays".Translate(),
                        ref settings.EventCooldownDays, 1, 90);

                GUI.color = ColorLibrary.LightText;
// OLD: listing.Label($"How many in-game days to count events. Affects: !event, !raid, !militaryaid, !weather");
    listing.Label("RICS.GameEvents.EventCooldownDaysDesc".Translate()); // ← NEW
                GUI.color = Color.white;

                // Events per cooldown period
                // OLD: NumericField(listing, "Events per cooldown period:", ref settings.EventsperCooldown, 1, 1000);
                NumericField(listing,
                    "RICS.GameEvents.EventsPerCooldown".Translate(),
                    ref settings.EventsperCooldown, 1, 1000);
                GUI.color = ColorLibrary.LightText;
                // OLD: listing.Label($"Maximum events allowed in {settings.EventCooldownDays} days. 0 = unlimited");
                listing.Label("RICS.GameEvents.EventsPerCooldownDesc".Translate(settings.EventCooldownDays)); // ← NEW with param
                GUI.color = Color.white;

                listing.Gap(12f);

                // Karma type limits toggle
                // OLD: listing.CheckboxLabeled("Limit events by karma type", ref settings.KarmaTypeLimitsEnabled,"Set different limits for good, bad, and neutral events");
                listing.CheckboxLabeled("RICS.GameEvents.LimitEventsByKarmaType".Translate(), ref settings.KarmaTypeLimitsEnabled, "RICS.GameEvents.LimitEventsByKarmaTypeDesc".Translate());
                if (settings.KarmaTypeLimitsEnabled)
                {
                    listing.Gap(4f);
                    // OLD (error): NumericField(listing, "RICS.GameEvents.MaxBadEvents:".Translate(), ref settings.MaxBadEvents, 1, 1000);
                    NumericField(listing, "RICS.GameEvents.MaxBadEvents".Translate(), ref settings.MaxBadEvents, 1, 1000);  // ← FIXED (no extra :)
                    GUI.color = ColorLibrary.LightText;
                    listing.Label("RICS.GameEvents.MaxBadEventsDesc".Translate());
                    GUI.color = Color.white;

                    NumericField(listing, "RICS.GameEvents.MaxGoodEvents".Translate(), ref settings.MaxGoodEvents, 1, 1000);  // ← FIXED
                    GUI.color = ColorLibrary.LightText;
                    listing.Label("RICS.GameEvents.MaxGoodEventsDesc".Translate());
                    GUI.color = Color.white;

                    NumericField(listing, "RICS.GameEvents.MaxNeutralEvents".Translate(), ref settings.MaxNeutralEvents, 1, 1000);  // ← FIXED
                    GUI.color = ColorLibrary.LightText;
                    listing.Label("RICS.GameEvents.MaxNeutralEventsDesc".Translate());
                    GUI.color = Color.white;
                }

                listing.Gap(12f);

                // Store purchase limits
                // OLD: NumericField(listing, "Maximum item purchases per period:", ref settings.MaxItemPurchases, 1, 1000);
                NumericField(listing, "RICS.GameEvents.MaxItemPurchases".Translate(), ref settings.MaxItemPurchases, 1, 1000);
                GUI.color = ColorLibrary.LightText;
                // OLD: listing.Label($"Maximum !buy, !equip, !wear, !healpawn, !revivepawn commands in {settings.EventCooldownDays} days");
                listing.Label("RICS.GameEvents.MaxItemPurchasesDesc".Translate(settings.EventCooldownDays)); // ← NEW
                GUI.color = Color.white;
            }
            else
            {
                // Show a message when cooldowns are disabled
                listing.Gap(8f);
                GUI.color = ColorLibrary.LightText;
                // OLD (error): listing.Label("RICS.GameEvents.EventCooldownDaysDesc".Translate());
                listing.Label("RICS.GameEvents.CooldownsDisabledMessage".Translate());  // ← FIXED
                GUI.color = Color.white;
            }
        }

        private static void DrawAnomalySettings(Listing_Standard listing, CAPGlobalChatSettings settings)
        {
            Text.Font = GameFont.Medium;
            GUI.color = ColorLibrary.HeaderAccent;
            listing.Label("RICS.GameEvents.AnomalyHeader".Translate());
            Text.Font = GameFont.Small;
            GUI.color = Color.white;
            listing.GapLine(6f);

            listing.CheckboxLabeled(
                "RICS.GameEvents.AnomalyStaging".Translate(),
                ref settings.AnomalyStagingEnabled,
                "RICS.GameEvents.AnomalyStagingDesc".Translate());
            Text.Font = GameFont.Small;
            GUI.color = ColorLibrary.LightText;
            listing.Label("RICS.GameEvents.AnomalyStagingDesc".Translate());
            Text.Font = GameFont.Small;
            GUI.color = Color.white;
            listing.Gap(8f);

            listing.CheckboxLabeled(
                "RICS.GameEvents.AnomalyBlockWhenDisabled".Translate(),
                ref settings.AnomalyBlockEventsWhenDisabled,
                "RICS.GameEvents.AnomalyBlockWhenDisabledDesc".Translate());
            Text.Font = GameFont.Small;
            GUI.color = ColorLibrary.LightText;
            listing.Label("RICS.GameEvents.AnomalyBlockWhenDisabledDesc".Translate());
            Text.Font = GameFont.Small;
            GUI.color = Color.white;

            if (!ModsConfig.AnomalyActive)
            {
                listing.Gap(6f);
                GUI.color = ColorLibrary.LightText;
                listing.Label("RICS.GameEvents.AnomalyNotActive".Translate());
                GUI.color = Color.white;
            }
        }

        private static void DrawOtherSettings(Listing_Standard listing, CAPGlobalChatSettings settings)
        {
            Text.Font = GameFont.Medium;
            GUI.color = ColorLibrary.HeaderAccent;
            // OLD: listing.Label("Other Settings");
            listing.Label("RICS.GameEvents.OtherSettings".Translate());
            Text.Font = GameFont.Small;
            GUI.color = Color.white;
            listing.GapLine(6f);

            // Max Traits setting

            // OLD: NumericField(listing, "Max traits for a pawn:", ref settings.MaxTraits, 1, 20);
            NumericField(listing, "RICS.GameEvents.MaxTraits".Translate(), ref settings.MaxTraits, 1, 20);
            Text.Font = GameFont.Tiny;
            // OLD: listing.Label($"Maximum number of traits a single pawn can have");
            listing.Label("RICS.GameEvents.MaxTraitsDesc".Translate()); // ← NEW)
            Text.Font = GameFont.Small;
        }

        private static void DrawStatisticsAndEditors(Listing_Standard listing)
        {
            bool gameLoaded = Current.ProgramState == ProgramState.Playing;

            Text.Font = GameFont.Medium;
            GUI.color = ColorLibrary.HeaderAccent;
            // OLD: listing.Label("Event Management");
            listing.Label("RICS.GameEvents.EventManagement".Translate()); // ← NEW
            Text.Font = GameFont.Small;
            GUI.color = Color.white;

            listing.GapLine(6f);
            GUI.color = Color.white;

            if (!gameLoaded)
            {
                GUI.color = Color.white;
                // OLD: listing.Label("Load a game to access event editors and statistics");
                listing.Label("RICS.GameEvents.LoadGameToAccessEditors".Translate()); // ← NEW)
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
            GUI.color = ColorLibrary.SubHeader;
            // OLD: listing.Label("Current Statistics:");
            listing.Label("RICS.GameEvents.CurrentStatistics".Translate()); // ← NEW)
            Text.Font = GameFont.Tiny;

            //listing.Label($"  • Store: {enabledStoreItems}/{totalStoreItems} items enabled");
            //listing.Label($"  • Traits: {enabledTraits}/{totalTraits} traits enabled");
            //listing.Label($"  • Weather: {enabledWeather}/{totalWeather} types enabled");
            //listing.Label($"  • Events: {enabledEvents}/{totalEvents} events enabled");

            listing.Label("RICS.GameEvents.StoreStats".Translate(enabledStoreItems, totalStoreItems)); // ← NEW)
            listing.Label("RICS.GameEvents.TraitsStats".Translate(enabledTraits, totalTraits)); // ← NEW)   
            listing.Label("RICS.GameEvents.WeatherStats".Translate(enabledWeather, totalWeather)); // ← NEW)    
            listing.Label("RICS.GameEvents.EventsStats".Translate(enabledEvents, totalEvents)); // ← NEW)

            Text.Font = GameFont.Small;
        }

        private static void DrawEditorButtons(Listing_Standard listing)
        {
            // Create a rect for the button row
            Rect buttonRow = listing.GetRect(30f);
            float buttonWidth = (buttonRow.width - 30f) / 4f;

            // Store Editor Button
            Rect storeRect = new Rect(buttonRow.x, buttonRow.y, buttonWidth, 30f);
            // OLD (error): if (Widgets.ButtonText(storeRect, "RICS.GameEvents.StoreEditor".Translate()))
            if (Widgets.ButtonText(storeRect, "RICS.GameEvents.StoreEditorButton".Translate()))  // ← FIXED
            {
                Find.WindowStack.Add(new Dialog_StoreEditor());
            }

            // Traits Editor Button  
            Rect traitsRect = new Rect(buttonRow.x + buttonWidth + 10f, buttonRow.y, buttonWidth, 30f);
            if (Widgets.ButtonText(traitsRect, "RICS.GameEvents.TraitsEditorButton".Translate()))  // ← FIXED
            {
                Find.WindowStack.Add(new Dialog_TraitsEditor());
            }

            // Weather Editor Button
            Rect weatherRect = new Rect(buttonRow.x + (buttonWidth + 10f) * 2, buttonRow.y, buttonWidth, 30f);
            if (Widgets.ButtonText(weatherRect, "RICS.GameEvents.WeatherEditorButton".Translate()))  // ← FIXED
            {
                Find.WindowStack.Add(new Dialog_WeatherEditor());
            }

            // Events Editor Button
            Rect eventsRect = new Rect(buttonRow.x + (buttonWidth + 10f) * 3, buttonRow.y, buttonWidth, 30f);
            if (Widgets.ButtonText(eventsRect, "RICS.GameEvents.EventsEditorButton".Translate()))  // ← FIXED
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
            // OLD: if (Widgets.ButtonText(buttonRect, "Reset to Default Values"))
            if (Widgets.ButtonText(buttonRect, "RICS.GameEvents.ResetButton".Translate()))
            {
                // Create confirmation dialog
                //Find.WindowStack.Add(new Dialog_MessageBox(
                //    "Reset all cooldown settings to default values?\n\nThis will reset:\n• Event cooldown days\n• Event limits\n• Karma type limits\n• Purchase limits",
                //    "Reset",
                //    () => ResetToDefaults(settings),
                //    "Cancel"
                //));

                Find.WindowStack.Add(new Dialog_MessageBox(
                    "RICS.GameEvents.ResetConfirmDesc".Translate(), // ← NEW
                    "RICS.GameEvents.Reset".Translate(),       // ← NEW
                    () => ResetToDefaults(settings),
                    "RICS.GameEvents.Cancel".Translate()         // ← NEW
                ));
            }

            GUI.color = ColorLibrary.LightText;
            // OLD: listing.Label("Reset all cooldown settings back to default values");
            listing.Label("RICS.GameEvents.ResetAllCooldowns".Translate()); // ← NEW
            GUI.color = Color.white;
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

            // Save the changes
            CAPChatInteractiveMod.Instance.Settings.Write();

            // OLD: Messages.Message("Cooldown settings reset to defaults", MessageTypeDefOf.PositiveEvent);
            Messages.Message("RICS.GameEvents.ResetMessage".Translate(), MessageTypeDefOf.PositiveEvent); // ← NEW
        }
    }
}