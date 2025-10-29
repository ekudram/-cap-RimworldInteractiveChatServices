using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using CAP_ChatInteractive.Incidents;

namespace CAP_ChatInteractive
{
    public class Dialog_WeatherEditor : Window
    {
        private Vector2 scrollPosition = Vector2.zero;
        private Vector2 categoryScrollPosition = Vector2.zero;
        private string searchQuery = "";
        private string lastSearch = "";
        private WeatherSortMethod sortMethod = WeatherSortMethod.Name;
        private bool sortAscending = true;
        private string selectedModSource = "All";
        private Dictionary<string, int> modSourceCounts = new Dictionary<string, int>();
        private List<BuyableWeather> filteredWeather = new List<BuyableWeather>();
        private Dictionary<string, (int baseCost, string karmaType)> originalSettings = new Dictionary<string, (int, string)>();

        public override Vector2 InitialSize => new Vector2(1200f, 700f);

        public Dialog_WeatherEditor()
        {
            doCloseButton = true;
            forcePause = true;
            absorbInputAroundWindow = true;
            optionalTitle = "Weather Editor";

            BuildModSourceCounts();
            FilterWeather();
            SaveOriginalSettings();
        }

        public override void DoWindowContents(Rect inRect)
        {
            if (searchQuery != lastSearch || filteredWeather.Count == 0)
            {
                FilterWeather();
            }

            Rect headerRect = new Rect(0f, 0f, inRect.width, 40f);
            DrawHeader(headerRect);

            Rect contentRect = new Rect(0f, 45f, inRect.width, inRect.height - 45f - CloseButSize.y);
            DrawContent(contentRect);
        }

        private void DrawHeader(Rect rect)
        {
            Widgets.BeginGroup(rect);

            // Title
            Text.Font = GameFont.Medium;
            Rect titleRect = new Rect(0f, 0f, 200f, 30f);
            Widgets.Label(titleRect, "Weather Editor");
            Text.Font = GameFont.Small;

            // Search bar
            Rect searchRect = new Rect(210f, 5f, 250f, 30f);
            searchQuery = Widgets.TextField(searchRect, searchQuery);

            // Sort buttons
            Rect sortRect = new Rect(470f, 5f, 300f, 30f);
            DrawSortButtons(sortRect);

            // Action buttons
            Rect actionsRect = new Rect(780f, 5f, 400f, 30f);
            DrawActionButtons(actionsRect);

            Widgets.EndGroup();
        }

        private void DrawSortButtons(Rect rect)
        {
            Widgets.BeginGroup(rect);

            float buttonWidth = 90f;
            float spacing = 5f;
            float x = 0f;

            if (Widgets.ButtonText(new Rect(x, 0f, buttonWidth, 30f), "Name"))
            {
                if (sortMethod == WeatherSortMethod.Name)
                    sortAscending = !sortAscending;
                else
                    sortMethod = WeatherSortMethod.Name;
                SortWeather();
            }
            x += buttonWidth + spacing;

            if (Widgets.ButtonText(new Rect(x, 0f, buttonWidth, 30f), "Cost"))
            {
                if (sortMethod == WeatherSortMethod.Cost)
                    sortAscending = !sortAscending;
                else
                    sortMethod = WeatherSortMethod.Cost;
                SortWeather();
            }
            x += buttonWidth + spacing;

            if (Widgets.ButtonText(new Rect(x, 0f, buttonWidth, 30f), "Source"))
            {
                if (sortMethod == WeatherSortMethod.ModSource)
                    sortAscending = !sortAscending;
                else
                    sortMethod = WeatherSortMethod.ModSource;
                SortWeather();
            }

            string sortIndicator = sortAscending ? " ↑" : " ↓";
            Rect indicatorRect = new Rect(x + buttonWidth + 10f, 8f, 50f, 20f);
            Widgets.Label(indicatorRect, sortIndicator);

            Widgets.EndGroup();
        }

        private void DrawActionButtons(Rect rect)
        {
            Widgets.BeginGroup(rect);

            float buttonWidth = 100f; // Back to original width since we have fewer buttons
            float spacing = 5f;
            float x = 0f;

            if (Widgets.ButtonText(new Rect(x, 0f, buttonWidth, 30f), "Reset Prices"))
            {
                Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                    "Reset all weather prices to default? This cannot be undone.",
                    () => ResetAllPrices()
                ));
            }
            x += buttonWidth + spacing;

            // Enable by Mod Source
            if (Widgets.ButtonText(new Rect(x, 0f, buttonWidth, 30f), "Enable →"))
            {
                ShowEnableByModSourceMenu();
            }
            x += buttonWidth + spacing;

            // Disable by Mod Source
            if (Widgets.ButtonText(new Rect(x, 0f, buttonWidth, 30f), "Disable →"))
            {
                ShowDisableByModSourceMenu();
            }

            Widgets.EndGroup();
        }

        private void DrawContent(Rect rect)
        {
            // Split into mod sources (left) and weather (right)
            float sourcesWidth = 200f;
            float weatherWidth = rect.width - sourcesWidth - 10f;

            Rect sourcesRect = new Rect(rect.x + 5f, rect.y, sourcesWidth - 10f, rect.height);
            Rect weatherRect = new Rect(rect.x + sourcesWidth + 5f, rect.y, weatherWidth - 10f, rect.height);

            DrawModSourcesList(sourcesRect);
            DrawWeatherList(weatherRect);
        }

        private void DrawModSourcesList(Rect rect)
        {
            Widgets.DrawMenuSection(rect);

            Rect headerRect = new Rect(rect.x, rect.y, rect.width, 30f);
            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.UpperCenter;
            Widgets.Label(headerRect, "Mod Sources");
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;

            Rect listRect = new Rect(rect.x + 5f, rect.y + 35f, rect.width - 10f, rect.height - 35f);
            Rect viewRect = new Rect(0f, 0f, listRect.width - 20f, modSourceCounts.Count * 30f);

            Widgets.BeginScrollView(listRect, ref categoryScrollPosition, viewRect);
            {
                float y = 0f;
                foreach (var modSource in modSourceCounts.OrderByDescending(kvp => kvp.Value))
                {
                    // Increased button width to fill available space better
                    Rect sourceButtonRect = new Rect(0f, y, listRect.xMax - 21f, 28f); // Reduced width slightly for padding

                    if (selectedModSource == modSource.Key)
                    {
                        Widgets.DrawHighlightSelected(sourceButtonRect);
                    }
                    else if (Mouse.IsOver(sourceButtonRect))
                    {
                        Widgets.DrawHighlight(sourceButtonRect);
                    }

                    string displayName = modSource.Key == "All" ? "All" : GetDisplayModName(modSource.Key);
                    string label = $"{displayName} ({modSource.Value})";

                    Text.Anchor = TextAnchor.MiddleLeft;
                    if (Widgets.ButtonText(sourceButtonRect, label))
                    {
                        selectedModSource = modSource.Key;
                        FilterWeather();
                    }
                    Text.Anchor = TextAnchor.UpperLeft;

                    y += 30f;
                }
            }
            Widgets.EndScrollView();
        }

        private void DrawWeatherList(Rect rect)
        {
            Widgets.DrawMenuSection(rect);

            Rect headerRect = new Rect(rect.x, rect.y, rect.width, 30f);
            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.UpperCenter;
            string headerText = $"Weather Types ({filteredWeather.Count})";
            if (selectedModSource != "All")
                headerText += $" - {GetDisplayModName(selectedModSource)}";
            Widgets.Label(headerRect, headerText);
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;

            Rect listRect = new Rect(rect.x, rect.y + 35f, rect.width, rect.height - 35f);
            float rowHeight = 80f;

            int firstVisibleIndex = Mathf.FloorToInt(scrollPosition.y / rowHeight);
            int lastVisibleIndex = Mathf.CeilToInt((scrollPosition.y + listRect.height) / rowHeight);
            firstVisibleIndex = Mathf.Clamp(firstVisibleIndex, 0, filteredWeather.Count - 1);
            lastVisibleIndex = Mathf.Clamp(lastVisibleIndex, 0, filteredWeather.Count - 1);

            Rect viewRect = new Rect(0f, 0f, listRect.width - 20f, filteredWeather.Count * rowHeight);

            Widgets.BeginScrollView(listRect, ref scrollPosition, viewRect);
            {
                float y = firstVisibleIndex * rowHeight;
                for (int i = firstVisibleIndex; i <= lastVisibleIndex; i++)
                {
                    Rect weatherRect = new Rect(0f, y, viewRect.width, rowHeight - 2f);
                    if (i % 2 == 1)
                    {
                        Widgets.DrawLightHighlight(weatherRect);
                    }

                    DrawWeatherRow(weatherRect, filteredWeather[i]);
                    y += rowHeight;
                }
            }
            Widgets.EndScrollView();
        }

        private void DrawWeatherRow(Rect rect, BuyableWeather weather)
        {
            Widgets.BeginGroup(rect);

            try
            {
                // Left section: Name and description
                Rect infoRect = new Rect(5f, 5f, rect.width - 400f, 70f);
                DrawWeatherInfo(infoRect, weather);

                // Middle section: Enable toggle
                Rect toggleRect = new Rect(rect.width - 390f, 20f, 100f, 40f);
                DrawWeatherToggle(toggleRect, weather);

                // Right section: Cost and Karma controls
                Rect controlsRect = new Rect(rect.width - 280f, 10f, 275f, 60f);
                DrawWeatherControls(controlsRect, weather);
            }
            finally
            {
                Widgets.EndGroup();
                Text.Anchor = TextAnchor.UpperLeft;
                GUI.color = Color.white;
            }
        }

        private void DrawWeatherInfo(Rect rect, BuyableWeather weather)
        {
            Widgets.BeginGroup(rect);

            // Weather name - increased height to prevent cutting off descending characters
            Rect nameRect = new Rect(0f, 0f, rect.width, 28f); // Increased from 24f to 28f
            Text.Font = GameFont.Medium;

            // Capitalize first letter of the label
            string displayLabel = weather.Label;
            if (!string.IsNullOrEmpty(displayLabel))
            {
                displayLabel = char.ToUpper(displayLabel[0]) + (displayLabel.Length > 1 ? displayLabel.Substring(1) : "");
            }

            Widgets.Label(nameRect, displayLabel);
            Text.Font = GameFont.Small;

            // Description
            Rect descRect = new Rect(0f, 28f, rect.width, 40f); // Adjusted Y position due to increased name height
            string description = weather.Description;
            if (description.Length > 160)
            {
                description = description.Substring(0, 157) + "...";
            }
            Widgets.Label(descRect, description);

            Widgets.EndGroup();
        }

        private void DrawWeatherToggle(Rect rect, BuyableWeather weather)
        {
            Widgets.BeginGroup(rect);

            Rect toggleRect = new Rect(0f, 0f, rect.width, 30f);
            bool enabledCurrent = weather.Enabled;
            Widgets.CheckboxLabeled(toggleRect, "Enabled", ref enabledCurrent);
            if (enabledCurrent != weather.Enabled)
            {
                weather.Enabled = enabledCurrent;
                Incidents.Weather.BuyableWeatherManager.SaveWeatherToJson();
            }

            Widgets.EndGroup();
        }

        private void DrawWeatherControls(Rect rect, BuyableWeather weather)
        {
            Widgets.BeginGroup(rect);

            float controlHeight = 25f;
            float spacing = 5f;
            float y = 0f;

            // Cost control
            Rect costRect = new Rect(0f, y, rect.width, controlHeight);
            DrawCostControl(costRect, weather);
            y += controlHeight + spacing;

            // Karma type control
            Rect karmaRect = new Rect(0f, y, rect.width, controlHeight);
            DrawKarmaControl(karmaRect, weather);

            Widgets.EndGroup();
        }

        private void DrawCostControl(Rect rect, BuyableWeather weather)
        {
            Widgets.BeginGroup(rect);

            // Label
            Rect labelRect = new Rect(0f, 0f, 60f, 25f);
            Widgets.Label(labelRect, "Cost:");

            // Cost input
            Rect inputRect = new Rect(65f, 0f, 80f, 25f);
            int costBuffer = weather.BaseCost;
            string stringBuffer = costBuffer.ToString();
            Widgets.TextFieldNumeric(inputRect, ref costBuffer, ref stringBuffer, 0, 1000000);

            if (costBuffer != weather.BaseCost)
            {
                weather.BaseCost = costBuffer;
                Incidents.Weather.BuyableWeatherManager.SaveWeatherToJson();
            }

            // Reset button
            Rect resetRect = new Rect(150f, 0f, 60f, 25f);
            if (Widgets.ButtonText(resetRect, "Reset"))
            {
                weather.BaseCost = CalculateDefaultCost(weather);
                Incidents.Weather.BuyableWeatherManager.SaveWeatherToJson();
            }

            Widgets.EndGroup();
        }

        private void DrawKarmaControl(Rect rect, BuyableWeather weather)
        {
            Widgets.BeginGroup(rect);

            // Label for Karma
            Rect labelRect = new Rect(0f, 0f, 60f, 25f);
            Widgets.Label(labelRect, "Karma:");

            // Karma dropdown
            Rect dropdownRect = new Rect(65f, 0f, 100f, 25f);
            if (Widgets.ButtonText(dropdownRect, weather.KarmaType))
            {
                List<FloatMenuOption> options = new List<FloatMenuOption>();

                foreach (KarmaType karmaType in System.Enum.GetValues(typeof(KarmaType)))
                {
                    string karmaName = karmaType.ToString();
                    options.Add(new FloatMenuOption(karmaName, () =>
                    {
                        weather.KarmaType = karmaName;
                        Incidents.Weather.BuyableWeatherManager.SaveWeatherToJson();
                    }));
                }

                Find.WindowStack.Add(new FloatMenu(options));
            }

            Widgets.EndGroup();
        }

        private void ShowEnableByModSourceMenu()
        {
            List<FloatMenuOption> options = new List<FloatMenuOption>();

            // Add "All" option
            options.Add(new FloatMenuOption("All Mods", () =>
            {
                EnableAllWeather();
            }));

            // Add options for each mod source
            foreach (var modSource in modSourceCounts.Keys.Where(k => k != "All").OrderBy(k => k))
            {
                string displayName = GetDisplayModName(modSource);
                options.Add(new FloatMenuOption(displayName, () =>
                {
                    EnableWeatherByModSource(modSource);
                }));
            }

            Find.WindowStack.Add(new FloatMenu(options));
        }

        private void ShowDisableByModSourceMenu()
        {
            List<FloatMenuOption> options = new List<FloatMenuOption>();

            // Add "All" option
            options.Add(new FloatMenuOption("All Mods", () =>
            {
                DisableAllWeather();
            }));

            // Add options for each mod source
            foreach (var modSource in modSourceCounts.Keys.Where(k => k != "All").OrderBy(k => k))
            {
                string displayName = GetDisplayModName(modSource);
                options.Add(new FloatMenuOption(displayName, () =>
                {
                    DisableWeatherByModSource(modSource);
                }));
            }

            Find.WindowStack.Add(new FloatMenu(options));
        }

        private void EnableWeatherByModSource(string modSource)
        {
            int count = 0;
            foreach (var weather in Incidents.Weather.BuyableWeatherManager.AllBuyableWeather.Values)
            {
                if (GetDisplayModName(weather.ModSource) == modSource)
                {
                    weather.Enabled = true;
                    count++;
                }
            }

            Incidents.Weather.BuyableWeatherManager.SaveWeatherToJson();
            FilterWeather();

            Messages.Message($"Enabled {count} weather types from {GetDisplayModName(modSource)}", MessageTypeDefOf.TaskCompletion);
        }

        private void DisableWeatherByModSource(string modSource)
        {
            int count = 0;
            foreach (var weather in Incidents.Weather.BuyableWeatherManager.AllBuyableWeather.Values)
            {
                if (GetDisplayModName(weather.ModSource) == modSource)
                {
                    weather.Enabled = false;
                    count++;
                }
            }

            Incidents.Weather.BuyableWeatherManager.SaveWeatherToJson();
            FilterWeather();

            Messages.Message($"Disabled {count} weather types from {GetDisplayModName(modSource)}", MessageTypeDefOf.TaskCompletion);
        }

        private int CalculateDefaultCost(BuyableWeather weather)
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

        private void BuildModSourceCounts()
        {
            modSourceCounts.Clear();
            modSourceCounts["All"] = Incidents.Weather.BuyableWeatherManager.AllBuyableWeather.Count;

            foreach (var weather in Incidents.Weather.BuyableWeatherManager.AllBuyableWeather.Values)
            {
                string displayModSource = GetDisplayModName(weather.ModSource);
                if (modSourceCounts.ContainsKey(displayModSource))
                    modSourceCounts[displayModSource]++;
                else
                    modSourceCounts[displayModSource] = 1;
            }
        }

        private string GetDisplayModName(string modSource)
        {
            if (modSource == "Core")
                return "RimWorld";

            if (modSource.Contains("."))
            {
                return modSource.Split('.')[0];
            }

            return modSource;
        }

        private void FilterWeather()
        {
            lastSearch = searchQuery;
            filteredWeather.Clear();

            var allWeather = Incidents.Weather.BuyableWeatherManager.AllBuyableWeather.Values.AsEnumerable();

            if (selectedModSource != "All")
            {
                allWeather = allWeather.Where(weather => GetDisplayModName(weather.ModSource) == selectedModSource);
            }

            if (!string.IsNullOrEmpty(searchQuery))
            {
                string searchLower = searchQuery.ToLower();
                allWeather = allWeather.Where(weather =>
                    weather.Label.ToLower().Contains(searchLower) ||
                    weather.Description.ToLower().Contains(searchLower) ||
                    weather.DefName.ToLower().Contains(searchLower) ||
                    weather.ModSource.ToLower().Contains(searchLower)
                );
            }

            filteredWeather = allWeather.ToList();
            SortWeather();
        }

        private void SortWeather()
        {
            switch (sortMethod)
            {
                case WeatherSortMethod.Name:
                    filteredWeather = sortAscending ?
                        filteredWeather.OrderBy(weather => weather.Label).ToList() :
                        filteredWeather.OrderByDescending(weather => weather.Label).ToList();
                    break;
                case WeatherSortMethod.Cost:
                    filteredWeather = sortAscending ?
                        filteredWeather.OrderBy(weather => weather.BaseCost).ToList() :
                        filteredWeather.OrderByDescending(weather => weather.BaseCost).ToList();
                    break;
                case WeatherSortMethod.ModSource:
                    filteredWeather = sortAscending ?
                        filteredWeather.OrderBy(weather => GetDisplayModName(weather.ModSource)).ThenBy(weather => weather.Label).ToList() :
                        filteredWeather.OrderByDescending(weather => GetDisplayModName(weather.ModSource)).ThenBy(weather => weather.Label).ToList();
                    break;
            }
        }

        private void SaveOriginalSettings()
        {
            originalSettings.Clear();
            foreach (var weather in Incidents.Weather.BuyableWeatherManager.AllBuyableWeather.Values)
            {
                originalSettings[weather.DefName] = (weather.BaseCost, weather.KarmaType);
            }
        }

        private void ResetAllPrices()
        {
            foreach (var weather in Incidents.Weather.BuyableWeatherManager.AllBuyableWeather.Values)
            {
                weather.BaseCost = CalculateDefaultCost(weather);
            }
            Incidents.Weather.BuyableWeatherManager.SaveWeatherToJson();
            FilterWeather();
        }

        private void EnableAllWeather()
        {
            foreach (var weather in Incidents.Weather.BuyableWeatherManager.AllBuyableWeather.Values)
            {
                weather.Enabled = true;
            }
            Incidents.Weather.BuyableWeatherManager.SaveWeatherToJson();
            FilterWeather();
        }

        private void DisableAllWeather()
        {
            foreach (var weather in Incidents.Weather.BuyableWeatherManager.AllBuyableWeather.Values)
            {
                weather.Enabled = false;
            }
            Incidents.Weather.BuyableWeatherManager.SaveWeatherToJson();
            FilterWeather();
        }

        public override void PostClose()
        {
            Incidents.Weather.BuyableWeatherManager.SaveWeatherToJson();
            base.PostClose();
        }
    }

    public enum WeatherSortMethod
    {
        Name,
        Cost,
        ModSource
    }
}