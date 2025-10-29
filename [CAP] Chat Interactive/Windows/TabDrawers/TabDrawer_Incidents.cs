using CAP_ChatInteractive.Incidents;
using CAP_ChatInteractive.Incidents.Weather;
using RimWorld;
using System.Linq;
using UnityEngine;
using Verse;

namespace CAP_ChatInteractive
{
    public static class TabDrawer_Incidents
    {
        private static Vector2 _scrollPosition = Vector2.zero;

        public static void Draw(Rect region)
        {
            var view = new Rect(0f, 0f, region.width - 16f, 400f);

            Widgets.BeginScrollView(region, ref _scrollPosition, view);
            var listing = new Listing_Standard();
            listing.Begin(view);

            // Header
            Text.Font = GameFont.Medium;
            listing.Label("Incidents Management");
            Text.Font = GameFont.Small;
            listing.GapLine(6f);

            // Description
            listing.Label("Manage game incidents and events that can be purchased. Set prices, enable/disable incidents, and configure karma types.");
            listing.Gap(12f);

            // Incidents Statistics
            bool gameLoaded = Current.ProgramState == ProgramState.Playing;
            int totalWeather = gameLoaded ? BuyableWeatherManager.AllBuyableWeather.Count : 0;
            int enabledWeather = gameLoaded ? BuyableWeatherManager.AllBuyableWeather.Values.Count(w => w.Enabled) : 0;
            int disabledWeather = totalWeather - enabledWeather;

            listing.Label($"Weather Incidents Statistics:");
            Text.Font = GameFont.Tiny;
            if (gameLoaded)
            {
                listing.Label($"  • Total Weather Types: {totalWeather}");
                listing.Label($"  • Enabled: {enabledWeather}");
                listing.Label($"  • Disabled: {disabledWeather}");
            }
            else
            {
                listing.Label($"  • Load a game to view incidents statistics");
            }
            Text.Font = GameFont.Small;
            listing.Gap(12f);

            // Open Weather Editor Button
            Rect weatherButtonRect = listing.GetRect(30f);
            if (!gameLoaded)
            {
                GUI.color = Color.gray;
                Widgets.ButtonText(weatherButtonRect, "Open Weather Editor");
                GUI.color = Color.white;

                Rect warningRect = listing.GetRect(Text.LineHeight);
                Text.Anchor = TextAnchor.MiddleLeft;
                Widgets.Label(warningRect, "Weather editor requires a loaded game");
                Text.Anchor = TextAnchor.UpperLeft;
            }
            else
            {
                if (Widgets.ButtonText(weatherButtonRect, "Open Weather Editor"))
                {
                    Find.WindowStack.Add(new Dialog_WeatherEditor());
                }
            }

            listing.Gap(24f);

            // Quick Actions Section
            Text.Font = GameFont.Medium;
            listing.Label("Quick Actions");
            Text.Font = GameFont.Small;
            listing.GapLine(6f);

            if (!gameLoaded)
            {
                Rect warningRect = listing.GetRect(Text.LineHeight * 2f);
                Text.Anchor = TextAnchor.MiddleCenter;
                GUI.color = Color.yellow;
                Widgets.Label(warningRect, "Quick actions require a loaded game");
                GUI.color = Color.white;
                Text.Anchor = TextAnchor.UpperLeft;
                listing.Gap(12f);
            }
            else
            {
                // Reset All Weather Prices - with label on left
                Rect resetRow = listing.GetRect(30f);
                Rect resetLabelRect = resetRow.LeftPart(0.7f).Rounded();
                Rect resetButtonRect = resetRow.RightPart(0.3f).Rounded();

                Widgets.Label(resetLabelRect, "Reset weather prices to default");
                if (Widgets.ButtonText(resetButtonRect, "Reset"))
                {
                    Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                        "Reset all weather prices to default values?",
                        () => {
                            foreach (var weather in BuyableWeatherManager.AllBuyableWeather.Values)
                            {
                                weather.BaseCost = CalculateDefaultWeatherCost(weather);
                            }
                            BuyableWeatherManager.SaveWeatherToJson();
                            Messages.Message("Weather prices reset to default", MessageTypeDefOf.NeutralEvent);
                        }
                    ));
                }

                // Enable All Weather - with label on left  
                Rect enableRow = listing.GetRect(30f);
                Rect enableLabelRect = enableRow.LeftPart(0.7f).Rounded();
                Rect enableButtonRect = enableRow.RightPart(0.3f).Rounded();

                Widgets.Label(enableLabelRect, "Enable all weather types");
                if (Widgets.ButtonText(enableButtonRect, "Enable All"))
                {
                    foreach (var weather in BuyableWeatherManager.AllBuyableWeather.Values)
                    {
                        weather.Enabled = true;
                    }
                    BuyableWeatherManager.SaveWeatherToJson();
                    Messages.Message($"Enabled all {BuyableWeatherManager.AllBuyableWeather.Count} weather types", MessageTypeDefOf.PositiveEvent);
                }

                // Disable All Weather - with label on left
                Rect disableRow = listing.GetRect(30f);
                Rect disableLabelRect = disableRow.LeftPart(0.7f).Rounded();
                Rect disableButtonRect = disableRow.RightPart(0.3f).Rounded();

                Widgets.Label(disableLabelRect, "Disable all weather types");
                if (Widgets.ButtonText(disableButtonRect, "Disable All"))
                {
                    Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                        "Disable all weather types from being purchased?",
                        () => {
                            foreach (var weather in BuyableWeatherManager.AllBuyableWeather.Values)
                            {
                                weather.Enabled = false;
                            }
                            BuyableWeatherManager.SaveWeatherToJson();
                            Messages.Message($"Disabled all {BuyableWeatherManager.AllBuyableWeather.Count} weather types", MessageTypeDefOf.NeutralEvent);
                        }
                    ));
                }
            }

            // Future Incidents Section (Placeholder)
            listing.Gap(24f);
            Text.Font = GameFont.Medium;
            listing.Label("Other Incident Types");
            Text.Font = GameFont.Small;
            listing.GapLine(6f);

            listing.Label("More incident types (raids, quests, events) will be available here in future updates.");
            Text.Font = GameFont.Tiny;
            listing.Label("Check back for updates!");
            Text.Font = GameFont.Small;

            listing.End();
            Widgets.EndScrollView();
        }

        private static int CalculateDefaultWeatherCost(BuyableWeather weather)
        {
            // Use the same logic as in BuyableWeather.SetDefaultPricing
            var weatherDef = DefDatabase<WeatherDef>.GetNamedSilentFail(weather.DefName);
            if (weatherDef != null)
            {
                var tempWeather = new BuyableWeather(weatherDef);
                return tempWeather.BaseCost;
            }
            return 200; // Fallback
        }
    }
}