using CAP_ChatInteractive.Incidents.Weather;
using CAP_ChatInteractive.Store;
using CAP_ChatInteractive.Traits;
using System.Linq;
using UnityEngine;
using Verse;

namespace CAP_ChatInteractive
{
    public static class TabDrawer_GameEvents
    {
        private static Vector2 _scrollPosition = Vector2.zero;

        public static void Draw(Rect region)
        {
            var settings = CAPChatInteractiveMod.Instance.Settings.GlobalSettings;
            var view = new Rect(0f, 0f, region.width - 16f, 600f);

            Widgets.BeginScrollView(region, ref _scrollPosition, view);
            var listing = new Listing_Standard();
            listing.Begin(view);

            // Header
            Text.Font = GameFont.Medium;
            listing.Label("Game Events & Cooldowns");
            Text.Font = GameFont.Small;
            listing.GapLine(6f);

            // Description
            listing.Label("Configure cooldowns for events, traits, and store purchases. Manage all game interactions in one place.");
            listing.Gap(12f);

            // COOLDOWN SETTINGS SECTION (Always available - no game required)
            DrawCooldownSettings(listing, settings);

            listing.Gap(24f);

            // STATISTICS AND EDITORS SECTION
            DrawStatisticsAndEditors(listing);

            listing.End();
            Widgets.EndScrollView();
        }

        private static void DrawCooldownSettings(Listing_Standard listing, CAPGlobalChatSettings settings)
        {
            Text.Font = GameFont.Medium;
            listing.Label("Global Cooldown Settings");
            Text.Font = GameFont.Small;
            listing.GapLine(6f);

            // Event cooldown toggle
            listing.CheckboxLabeled("Enable event cooldowns", ref settings.EventCooldownsEnabled,
                "When enabled, events will go on cooldown after being purchased");

            // Cooldown days
            NumericField(listing, "Event cooldown duration (days):", ref settings.EventCooldownDays, 1, 30);
            Text.Font = GameFont.Tiny;
            listing.Label($"Events will be unavailable for {settings.EventCooldownDays} in-game days after purchase");
            Text.Font = GameFont.Small;

            // Events per cooldown period
            NumericField(listing, "Events per cooldown period:", ref settings.EventsperCooldown, 1, 50);
            Text.Font = GameFont.Tiny;
            listing.Label($"Limit of {settings.EventsperCooldown} event purchases per cooldown period");
            Text.Font = GameFont.Small;

            listing.Gap(12f);

            // Karma type limits toggle
            listing.CheckboxLabeled("Limit events by karma type", ref settings.KarmaTypeLimitsEnabled,
                "Restrict how many events of each karma type can be purchased within a period");

            if (settings.KarmaTypeLimitsEnabled)
            {
                listing.Gap(4f);
                NumericField(listing, "Maximum bad event purchases:", ref settings.MaxBadEvents, 1, 20);
                NumericField(listing, "Maximum good event purchases:", ref settings.MaxGoodEvents, 1, 20);
                NumericField(listing, "Maximum neutral event purchases:", ref settings.MaxNeutralEvents, 1, 20);
            }

            listing.Gap(12f);

            // Store purchase limits
            NumericField(listing, "Maximum item purchases per period:", ref settings.MaxItemPurchases, 1, 50);
            Text.Font = GameFont.Tiny;
            listing.Label($"Viewers can purchase up to {settings.MaxItemPurchases} items before cooldown");
            Text.Font = GameFont.Small;
        }

        private static void DrawStatisticsAndEditors(Listing_Standard listing)
        {
            bool gameLoaded = Current.ProgramState == ProgramState.Playing;

            Text.Font = GameFont.Medium;
            listing.Label("Event Management");
            Text.Font = GameFont.Small;
            listing.GapLine(6f);

            if (!gameLoaded)
            {
                listing.Label("Load a game to access event editors and statistics");
                listing.Gap(12f);
                return;
            }

            // Statistics row
            DrawStatisticsRow(listing);

            listing.Gap(12f);

            // Editor buttons row
            DrawEditorButtons(listing);
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

            Text.Font = GameFont.Small;
            listing.Label("Current Statistics:");
            Text.Font = GameFont.Tiny;

            listing.Label($"  • Store: {enabledStoreItems}/{totalStoreItems} items enabled");
            listing.Label($"  • Traits: {enabledTraits}/{totalTraits} traits enabled");
            listing.Label($"  • Weather: {enabledWeather}/{totalWeather} types enabled");

            Text.Font = GameFont.Small;
        }

        private static void DrawEditorButtons(Listing_Standard listing)
        {
            // Create a rect for the button row
            Rect buttonRow = listing.GetRect(30f);
            float buttonWidth = (buttonRow.width - 20f) / 3f;

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