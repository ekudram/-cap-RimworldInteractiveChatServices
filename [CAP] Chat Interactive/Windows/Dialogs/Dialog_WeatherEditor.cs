// Dialog_WeatherEditor.cs
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
// A dialog window for editing buyable weather settings in the game
using CAP_ChatInteractive.Incidents;
using CAP_ChatInteractive.Incidents.Weather;
using Newtonsoft.Json;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

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
            doCloseButton = false;
            forcePause = true;
            absorbInputAroundWindow = true;
            // optionalTitle = "Weather Editor";

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

            float bottomBarHeight = 50f; // Space for the 6-button bar

            Rect headerRect = new Rect(0f, 0f, inRect.width, 65f);
            DrawHeader(headerRect);

            // Main content area — leave room at bottom for button bar
            Rect contentRect = new Rect(0f, 70f, inRect.width, inRect.height - 70f - bottomBarHeight);
            DrawContent(contentRect);

            // ========== BOTTOM BUTTON BAR (Save Backup | Load Backup | Save As... | Load file | Delete file | Close) ==========
            // WHY: Consistent with Command Manager, Events, Store, and Traits editors. Uses reusable BackupUtility + Dialog_TextInput.
            float btnH = 38f;
            float btnW = 130f;
            float gap = 6f;
            float padding = 10f;
            float currentY = inRect.yMax - bottomBarHeight + (bottomBarHeight - btnH) / 2f;

            // Save Backup (quick timestamped)
            Rect saveRect = new Rect(padding, currentY, btnW, btnH);
            if (Widgets.ButtonText(saveRect, "Save Backup"))
            {
                string json = JsonConvert.SerializeObject(Incidents.Weather.BuyableWeatherManager.AllBuyableWeather, Formatting.Indented);
                BackupUtility.SaveQuickBackup("WeatherEditor", json);
                Messages.Message("Weather backup saved (timestamped).", MessageTypeDefOf.NeutralEvent);
            }

            // Load Backup (latest timestamped)
            float loadX = padding + btnW + gap;
            Rect loadRect = new Rect(loadX, currentY, btnW, btnH);
            if (Widgets.ButtonText(loadRect, "Load Backup"))
            {
                string json = BackupUtility.LoadLatestTimestampedBackup("WeatherEditor");
                if (!string.IsNullOrEmpty(json))
                {
                    try
                    {
                        var loaded = JsonConvert.DeserializeObject<Dictionary<string, BuyableWeather>>(json);
                        if (loaded != null)
                        {
                            Incidents.Weather.BuyableWeatherManager.AllBuyableWeather.Clear();
                            foreach (var kvp in loaded)
                                Incidents.Weather.BuyableWeatherManager.AllBuyableWeather[kvp.Key] = kvp.Value;

                            Incidents.Weather.BuyableWeatherManager.SaveWeatherToJson();
                            BuildModSourceCounts();
                            FilterWeather();

                            Messages.Message("Weather loaded from latest backup.", MessageTypeDefOf.NeutralEvent);
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Error($"Failed to apply weather backup: {ex.Message}");
                        Messages.Message("Failed to load backup (invalid data).", MessageTypeDefOf.RejectInput);
                    }
                }
                else
                {
                    Messages.Message("No timestamped backups found for Weather Editor.", MessageTypeDefOf.RejectInput);
                }
            }

            // Save As... (uses Dialog_TextInput for custom name)
            float saveAsX = loadX + btnW + gap;
            Rect saveAsRect = new Rect(saveAsX, currentY, btnW, btnH);
            if (Widgets.ButtonText(saveAsRect, "Save As..."))
            {
                ShowSaveAsMenu();
            }

            // Load file
            float loadFileX = saveAsX + btnW + gap;
            Rect loadFileRect = new Rect(loadFileX, currentY, btnW, btnH);
            if (Widgets.ButtonText(loadFileRect, "Load file"))
            {
                ShowLoadFileMenu();
            }

            // Delete file (right next to Load file)
            float deleteX = loadFileX + btnW + gap;
            Rect deleteRect = new Rect(deleteX, currentY, btnW, btnH);
            if (Widgets.ButtonText(deleteRect, "Delete file"))
            {
                ShowDeleteFileMenu();
            }

            // Close (right-aligned)
            float closeX = inRect.xMax - btnW - padding;
            Rect closeRect = new Rect(closeX, currentY, btnW, btnH);
            if (Widgets.ButtonText(closeRect, "Close"))
            {
                this.Close();
            }
        }

        private void DrawHeader(Rect rect)
        {
            Widgets.BeginGroup(rect);

            // Make the header taller to accommodate both rows
            // float headerHeight = 65f; // Increased from 40f to 65f

            // Title row
            Text.Font = GameFont.Medium;
            GUI.color = ColorLibrary.HeaderAccent;
            Rect titleRect = new Rect(0f, 0f, 200f, 30f);
            Widgets.Label(titleRect, "Weather Editor");

            // Draw underline
            Rect underlineRect = new Rect(titleRect.x, titleRect.yMax - 2f, titleRect.width, 2f);
            Widgets.DrawLineHorizontal(underlineRect.x, underlineRect.y, underlineRect.width);

            Text.Font = GameFont.Small;
            GUI.color = Color.white;

            // Second row for controls - positioned below the title
            float controlsY = 35f; // Position below title with some spacing

            // Search bar with icon
            float searchY = controlsY;
            Rect searchIconRect = new Rect(0f, searchY, 24f, 24f);
            Texture2D searchIcon = ContentFinder<Texture2D>.Get("UI/Widgets/Search", false);
            if (searchIcon != null)
            {
                Widgets.DrawTextureFitted(searchIconRect, searchIcon, 1f);
            }
            else
            {
                // Fallback to text if icon not found
                Widgets.Label(new Rect(0f, searchY, 40f, 30f), "Search:");
            }

            Rect searchRect = new Rect(65f, controlsY, 200f, 30f);
            searchQuery = Widgets.TextField(searchRect, searchQuery);

            // Sort buttons
            Rect sortRect = new Rect(270f, controlsY, 320f, 30f);
            DrawSortButtons(sortRect);

            // Action buttons
            Rect actionsRect = new Rect(615f, controlsY, 400f, 30f);
            DrawActionButtons(actionsRect);

            // Settings gear icon - top right corner
            Rect settingsRect = new Rect(rect.width - 30f, 5f, 24f, 24f);
            Texture2D gearIcon = ContentFinder<Texture2D>.Get("UI/Icons/Options/OptionsGeneral", false);
            if (gearIcon != null)
            {
                if (Widgets.ButtonImage(settingsRect, gearIcon))
                {
                    Find.WindowStack.Add(new Dialog_EventSettings());
                }
            }
            else
            {
                // Fallback text button
                if (Widgets.ButtonText(new Rect(rect.width - 80f, 5f, 75f, 24f), "Settings"))
                {
                    Find.WindowStack.Add(new Dialog_EventSettings());
                }
            }

            // Info help icon - next to settings gear
            Rect infoRect = new Rect(rect.width - 60f, 5f, 24f, 24f); // Positioned left of the gear
            Texture2D infoIcon = ContentFinder<Texture2D>.Get("UI/Buttons/InfoButton", false);
            if (infoIcon != null)
            {
                if (Widgets.ButtonImage(infoRect, infoIcon))
                {
                    Find.WindowStack.Add(new Dialog_WeatherEditorHelp());
                }
                TooltipHandler.TipRegion(infoRect, "Events Editor Help");
            }
            else
            {
                // Fallback text button
                if (Widgets.ButtonText(new Rect(rect.width - 110f, 5f, 45f, 24f), "Help"))
                {
                    Find.WindowStack.Add(new Dialog_EventsEditorHelp());
                }
            }

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

            string sortAscIconPath = sortAscending ? "UI/Buttons/ReorderUp" : "UI/Buttons/ReorderDown";
            Texture2D sortIcon = ContentFinder<Texture2D>.Get(sortAscIconPath, false);

            Rect indicatorRect = new Rect(x + buttonWidth + 5f, 5f, 20f, 20f); // Adjusted position
            if (sortIcon != null)
            {
                Widgets.DrawTextureFitted(indicatorRect, sortIcon, 1f);
                // Show tooltip indicating current sort
                string tooltip = $"Sorted by: {sortMethod}\nDirection: {(sortAscending ? "Ascending" : "Descending")}";
                TooltipHandler.TipRegion(indicatorRect, tooltip);
            }
            else
            {
                // Fallback to text arrows
                string sortIndicator = sortAscending ? " ↑" : " ↓";
                Widgets.Label(indicatorRect, sortIndicator);
            }

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

                    // Use truncation for long mod names - ONLY USE THE TRUNCATED VERSION
                    string truncatedLabel = UIUtilities.TruncateTextToWidth(label, sourceButtonRect.width - 10f);

                    Text.Anchor = TextAnchor.MiddleLeft;
                    if (Widgets.ButtonText(sourceButtonRect, truncatedLabel))
                    {
                        selectedModSource = modSource.Key;
                        FilterWeather();
                    }
                    if (truncatedLabel != label)
                    {
                        TooltipHandler.TipRegion(sourceButtonRect, label);
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
            {
                string modText = $" - {GetDisplayModName(selectedModSource)}";
                // Check if full header fits, otherwise shorten
                if (!UIUtilities.TextFitsWidth(headerText + modText, headerRect.width))
                {
                    headerText = $"Weather ({filteredWeather.Count}){modText}";
                }
                else
                {
                    headerText += modText;
                }
            }
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

            displayLabel = UIUtilities.TruncateTextToWidth(displayLabel, nameRect.width);
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
            UIUtilities.TextFieldNumericFlexible(inputRect, ref costBuffer, ref stringBuffer, 0, 1000000);

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

        private void ShowSaveAsMenu()
        {
            List<FloatMenuOption> options = new List<FloatMenuOption>
            {
                new FloatMenuOption("Quick Timestamped Backup", () =>
                {
                    string json = JsonConvert.SerializeObject(Incidents.Weather.BuyableWeatherManager.AllBuyableWeather, Formatting.Indented);
                    BackupUtility.SaveQuickBackup("WeatherEditor", json);
                    Messages.Message("Quick timestamped backup saved.", MessageTypeDefOf.NeutralEvent);
                }),
                new FloatMenuOption("Save as Named Theme (custom name)", () =>
                {
                    string json = JsonConvert.SerializeObject(Incidents.Weather.BuyableWeatherManager.AllBuyableWeather, Formatting.Indented);

                    Find.WindowStack.Add(new Dialog_TextInput(
                        "Enter backup name (e.g. GrimwarWeather, RimMagic)",
                        name =>
                        {
                            if (!string.IsNullOrWhiteSpace(name))
                            {
                                BackupUtility.SaveNamedBackup("WeatherEditor", name, json);
                                Messages.Message($"Named backup saved as {name}.json", MessageTypeDefOf.NeutralEvent);
                            }
                            else
                            {
                                string fallback = "CustomWeather_" + DateTime.Now.ToString("yyyyMMdd_HHmm");
                                BackupUtility.SaveNamedBackup("WeatherEditor", fallback, json);
                                Messages.Message($"Saved with fallback name: {fallback}.json", MessageTypeDefOf.NeutralEvent);
                            }
                        }));
                })
            };

            Find.WindowStack.Add(new FloatMenu(options));
        }

        private void ShowLoadFileMenu()
        {
            var files = BackupUtility.GetAllBackupFiles("WeatherEditor");
            if (files.Count == 0)
            {
                Messages.Message("No backup files found for Weather Editor.", MessageTypeDefOf.RejectInput);
                return;
            }

            List<FloatMenuOption> options = new List<FloatMenuOption>();
            foreach (var file in files)
            {
                options.Add(new FloatMenuOption(file, () =>
                {
                    string json = BackupUtility.LoadBackupFile("WeatherEditor", file);
                    if (!string.IsNullOrEmpty(json))
                    {
                        try
                        {
                            var loaded = JsonConvert.DeserializeObject<Dictionary<string, BuyableWeather>>(json);
                            if (loaded != null)
                            {
                                Incidents.Weather.BuyableWeatherManager.AllBuyableWeather.Clear();
                                foreach (var kvp in loaded)
                                    Incidents.Weather.BuyableWeatherManager.AllBuyableWeather[kvp.Key] = kvp.Value;

                                Incidents.Weather.BuyableWeatherManager.SaveWeatherToJson();
                                BuildModSourceCounts();
                                FilterWeather();

                                Messages.Message($"Loaded backup: {file}", MessageTypeDefOf.NeutralEvent);
                            }
                        }
                        catch (Exception ex)
                        {
                            Logger.Error($"Failed to load {file}: {ex.Message}");
                            Messages.Message("Failed to load selected backup.", MessageTypeDefOf.RejectInput);
                        }
                    }
                }));
            }

            Find.WindowStack.Add(new FloatMenu(options));
        }

        private void ShowDeleteFileMenu()
        {
            var files = BackupUtility.GetAllBackupFiles("WeatherEditor");
            if (files.Count == 0)
            {
                Messages.Message("No backup files found for Weather Editor.", MessageTypeDefOf.RejectInput);
                return;
            }

            List<FloatMenuOption> options = new List<FloatMenuOption>();
            foreach (var file in files)
            {
                options.Add(new FloatMenuOption(file, () =>
                {
                    Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                        $"Delete backup file?\n{file}\n\nThis cannot be undone.",
                        () =>
                        {
                            bool deleted = BackupUtility.DeleteBackupFile("WeatherEditor", file);
                            if (deleted)
                            {
                                Messages.Message($"Deleted: {file}", MessageTypeDefOf.NeutralEvent);
                            }
                        },
                        true,
                        "Delete Backup"
                    ));
                }));
            }

            Find.WindowStack.Add(new FloatMenu(options));
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