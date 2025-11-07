// Dialog_PawnSettings.cs
// Copyright (c) Captolamia. All rights reserved.
// Licensed under the AGPLv3 License. See LICENSE file in the project root for full license information.
// A dialog window for configuring pawn races and xenotypes 
using _CAP__Chat_Interactive.Interfaces;
using _CAP__Chat_Interactive.Utilities;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace CAP_ChatInteractive
{
    public class Dialog_PawnSettings : Window
    {
        private Vector2 raceScrollPosition = Vector2.zero;
        private Vector2 detailsScrollPosition = Vector2.zero;
        private string searchQuery = "";
        private string lastSearch = "";
        private PawnSortMethod sortMethod = PawnSortMethod.Name;
        private bool sortAscending = true;
        private ThingDef selectedRace = null;
        private List<ThingDef> filteredRaces = new List<ThingDef>();
        private Dictionary<string, RaceSettings> raceSettings = new Dictionary<string, RaceSettings>();

        public override Vector2 InitialSize => new Vector2(1000f, 700f);

        public Dialog_PawnSettings()
        {
            doCloseButton = true;
            forcePause = true;
            absorbInputAroundWindow = true;
            // optionalTitle = "Pawn Race & Xenotype Settings";

            LoadRaceSettings();
            FilterRaces();
        }

        public override void DoWindowContents(Rect inRect)
        {
            // Update search if query changed
            if (searchQuery != lastSearch || filteredRaces.Count == 0)
            {
                FilterRaces();
            }

            // Header - INCREASED HEIGHT to accommodate two rows
            Rect headerRect = new Rect(0f, 0f, inRect.width, 60f); // Changed from 40f to 60f
            DrawHeader(headerRect);

            // Main content area - ADJUSTED START POSITION
            Rect contentRect = new Rect(0f, 65f, inRect.width, inRect.height - 65f - CloseButSize.y); // Changed from 45f to 65f
            DrawContent(contentRect);
        }

        private void DrawHeader(Rect rect)
        {
            Widgets.BeginGroup(rect);

            // Title row
            Text.Font = GameFont.Medium;
            GUI.color = ColorLibrary.Orange;
            Rect titleRect = new Rect(0f, 0f, 200f, 30f);

            // FIXED: Use enabled races count instead of all humanlike races
            int enabledRacesCount = RaceUtils.GetEnabledRaces().Count;
            int totalRacesCount = DefDatabase<ThingDef>.AllDefs.Count(d => d.race?.Humanlike ?? false);
            string titleText = $"Pawn Races ({enabledRacesCount}";

            // Show filtered count if search is active
            if (filteredRaces.Count != enabledRacesCount)
                titleText += $"/{filteredRaces.Count}";

            titleText += $")";

            Widgets.Label(titleRect, titleText);

            // Draw underline
            Rect underlineRect = new Rect(titleRect.x, titleRect.yMax - 2f, titleRect.width, 2f);
            Widgets.DrawLineHorizontal(underlineRect.x, underlineRect.y, underlineRect.width);

            Text.Font = GameFont.Small;
            GUI.color = Color.white;

            // Second row for controls - ADJUSTED POSITION
            float controlsY = 35f;

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

            Rect searchRect = new Rect(30f, searchY, 170f, 24f);
            searchQuery = Widgets.TextField(searchRect, searchQuery);

            // Sort buttons
            Rect sortRect = new Rect(210f, controlsY, 300f, 24f);
            DrawSortButtons(sortRect);

            // Debug gear icon - top right corner
            Rect debugRect = new Rect(rect.width - 30f, 5f, 24f, 24f);
            Texture2D gearIcon = ContentFinder<Texture2D>.Get("UI/Icons/Options/OptionsGeneral", false);
            if (gearIcon != null)
            {
                if (Widgets.ButtonImage(debugRect, gearIcon))
                {
                    Find.WindowStack.Add(new Dialog_DebugRaces());
                }
            }
            else
            {
                // Fallback to the original gear icon
                if (Widgets.ButtonImage(debugRect, TexButton.OpenInspector))
                {
                    Find.WindowStack.Add(new Dialog_DebugRaces());
                }
            }
            TooltipHandler.TipRegion(debugRect, "Open Race Debug Information");

            Widgets.EndGroup();
        }

        private void DrawSortButtons(Rect rect)
        {
            Widgets.BeginGroup(rect);

            float buttonWidth = 90f;
            float spacing = 5f;
            float x = 0f;
            float y = 0f; // Draw at top of the group, not at absolute top

            // Sort by Name
            if (Widgets.ButtonText(new Rect(x, y, buttonWidth, 24f), "Name"))
            {
                if (sortMethod == PawnSortMethod.Name)
                    sortAscending = !sortAscending;
                else
                    sortMethod = PawnSortMethod.Name;
                SortRaces();
            }
            x += buttonWidth + spacing;

            // Sort by Category
            if (Widgets.ButtonText(new Rect(x, y, buttonWidth, 24f), "Category"))
            {
                if (sortMethod == PawnSortMethod.Category)
                    sortAscending = !sortAscending;
                else
                    sortMethod = PawnSortMethod.Category;
                SortRaces();
            }
            x += buttonWidth + spacing;

            // Sort by Status
            if (Widgets.ButtonText(new Rect(x, y, buttonWidth, 24f), "Status"))
            {
                if (sortMethod == PawnSortMethod.Status)
                    sortAscending = !sortAscending;
                else
                    sortMethod = PawnSortMethod.Status;
                SortRaces();
            }

            Widgets.EndGroup();
        }

        private void DrawContent(Rect rect)
        {
            float listWidth = 250f;
            float detailsWidth = rect.width - listWidth - 10f;

            Rect listRect = new Rect(rect.x, rect.y, listWidth, rect.height);
            Rect detailsRect = new Rect(rect.x + listWidth + 10f, rect.y, detailsWidth, rect.height);

            DrawRaceList(listRect);
            DrawRaceDetails(detailsRect);
        }

        // In Dialog_PawnSettings.cs - Update the DrawRaceList method
        private void DrawRaceList(Rect rect)
        {
            Widgets.DrawMenuSection(rect);

            // Header
            Rect headerRect = new Rect(rect.x, rect.y, rect.width, 30f);
            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(headerRect, "Races");
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;

            // Race list
            Rect listRect = new Rect(rect.x, rect.y + 35f, rect.width, rect.height - 35f);
            float rowHeight = 35f;
            Rect viewRect = new Rect(0f, 0f, listRect.width - 20f, filteredRaces.Count * rowHeight);

            Widgets.BeginScrollView(listRect, ref raceScrollPosition, viewRect);
            {
                float y = 0f;
                for (int i = 0; i < filteredRaces.Count; i++)
                {
                    var race = filteredRaces[i];

                    // SAFELY get settings - don't crash if race was removed
                    RaceSettings settings = null;
                    if (!raceSettings.TryGetValue(race.defName, out settings))
                    {
                        // Race was excluded or not in settings - skip it
                        Logger.Warning($"Race {race.defName} not found in raceSettings, skipping");
                        continue;
                    }

                    Rect buttonRect = new Rect(5f, y, viewRect.width - 10f, rowHeight - 2f);

                    // Race name with status indicator
                    string displayName = race.LabelCap;
                    if (!settings.Enabled)
                        displayName += " [DISABLED]";

                    // Color coding based on enabled status
                    Color buttonColor = settings.Enabled ? Color.white : Color.gray;
                    bool isSelected = selectedRace == race;

                    if (isSelected)
                    {
                        GUI.color = buttonColor * 1.3f;
                    }
                    else
                    {
                        GUI.color = buttonColor;
                    }

                    if (Widgets.ButtonText(buttonRect, displayName))
                    {
                        selectedRace = race;
                    }
                    GUI.color = Color.white;

                    y += rowHeight;
                }
            }
            Widgets.EndScrollView();
        }

        private void DrawRaceDetails(Rect rect)
        {
            Widgets.DrawMenuSection(rect);

            if (selectedRace == null)
            {
                Rect messageRect = new Rect(rect.x, rect.y, rect.width, rect.height);
                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(messageRect, "Select a race to see details");
                Text.Anchor = TextAnchor.UpperLeft;
                return;
            }

            // SAFELY get settings
            RaceSettings settings = null;
            if (!raceSettings.TryGetValue(selectedRace.defName, out settings))
            {
                Rect messageRect = new Rect(rect.x, rect.y, rect.width, rect.height);
                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(messageRect, $"Race {selectedRace.defName} not found in settings");
                Text.Anchor = TextAnchor.UpperLeft;
                return;
            }

            // Compact header
            Rect headerRect = new Rect(rect.x, rect.y, rect.width, 30f);
            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.MiddleCenter;

            string headerText = $"{selectedRace.LabelCap}";
            if (!settings.Enabled)
                headerText += " 🚫 DISABLED";

            Widgets.Label(headerRect, headerText);
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperLeft;

            // Details content with scrolling
            Rect contentRect = new Rect(rect.x, rect.y + 35f, rect.width, rect.height - 40f);
            DrawRaceDetailsContent(contentRect, settings);
        }

        private void DrawRaceDetailsContent(Rect rect, RaceSettings settings)
        {
            float contentWidth = rect.width - 30f;
            float viewHeight = CalculateDetailsHeight(settings);
            Rect viewRect = new Rect(0f, 0f, contentWidth, Mathf.Max(viewHeight, rect.height));

            Widgets.BeginScrollView(rect, ref detailsScrollPosition, viewRect);
            {
                float y = 0f;
                float sectionHeight = 28f;
                float leftPadding = 15f;
                float columnWidth = (viewRect.width - leftPadding - 20f) / 2f;

                // Basic Info section
                Rect basicLabelRect = new Rect(leftPadding, y, viewRect.width, sectionHeight);
                Text.Font = GameFont.Medium;
                Widgets.Label(basicLabelRect, "Basic Information");
                Text.Font = GameFont.Small;
                y += sectionHeight;

                // Race description (compact)
                Rect descRect = new Rect(leftPadding, y, viewRect.width - leftPadding, sectionHeight * 1.5f);
                string desc = string.IsNullOrEmpty(selectedRace.description) ?
                    "No description available" : selectedRace.description;
                Widgets.Label(descRect, desc);
                y += sectionHeight * 1.5f + 10f;

                // Settings section
                Rect settingsLabelRect = new Rect(leftPadding, y, viewRect.width, sectionHeight);
                Text.Font = GameFont.Medium;
                Widgets.Label(settingsLabelRect, "Settings");
                Text.Font = GameFont.Small;
                y += sectionHeight;

                // Enabled checkbox and Base Price - same row
                Rect enabledRect = new Rect(leftPadding, y, columnWidth - 10f, sectionHeight);
                bool currentEnabled = settings.Enabled;
                Widgets.CheckboxLabeled(enabledRect, "Enabled", ref currentEnabled);
                if (currentEnabled != settings.Enabled)
                {
                    settings.Enabled = currentEnabled;
                    SaveRaceSettings();
                }

                Rect priceLabelRect = new Rect(leftPadding + columnWidth, y, 100f, sectionHeight);
                Widgets.Label(priceLabelRect, "Base Price:");
                Rect priceInputRect = new Rect(leftPadding + columnWidth + 100f, y, 80f, sectionHeight);
                int currentPrice = settings.BasePrice;
                string priceBuffer = currentPrice.ToString();
                // Use TextFieldNumeric for price since it's better for numbers
                Widgets.TextFieldNumeric(priceInputRect, ref currentPrice, ref priceBuffer, 0, 100000);
                if (currentPrice != settings.BasePrice)
                {
                    settings.BasePrice = currentPrice;
                    SaveRaceSettings();
                }
                y += sectionHeight;

                // Age settings - same row with sliders
                Rect ageMinLabelRect = new Rect(leftPadding, y, 70f, sectionHeight);
                Widgets.Label(ageMinLabelRect, "Min Age:");
                Rect ageMinInputRect = new Rect(leftPadding + 70f, y, 50f, sectionHeight);
                int currentMinAge = settings.MinAge;
                string ageMinBuffer = currentMinAge.ToString();
                string newAgeMin = Widgets.TextField(ageMinInputRect, ageMinBuffer);
                if (newAgeMin != ageMinBuffer && int.TryParse(newAgeMin, out int parsedMinAge) && parsedMinAge >= 4 && parsedMinAge <= 120)
                {
                    settings.MinAge = parsedMinAge;
                    if (settings.MinAge > settings.MaxAge)
                        settings.MaxAge = settings.MinAge;
                    SaveRaceSettings();
                }

                // Min Age slider
                Rect ageMinSliderRect = new Rect(leftPadding + 130f, y, 100f, sectionHeight);
                int newMinAge = (int)Widgets.HorizontalSlider(ageMinSliderRect, settings.MinAge, 4, 120, middleAlignment: true, label: settings.MinAge.ToString(), leftAlignedLabel: "4", rightAlignedLabel: "120");
                if (newMinAge != settings.MinAge)
                {
                    settings.MinAge = newMinAge;
                    if (settings.MinAge > settings.MaxAge)
                        settings.MaxAge = settings.MinAge;
                    SaveRaceSettings();
                }

                Rect ageMaxLabelRect = new Rect(leftPadding + 240f, y, 70f, sectionHeight);
                Widgets.Label(ageMaxLabelRect, "Max Age:");
                Rect ageMaxInputRect = new Rect(leftPadding + 310f, y, 50f, sectionHeight);
                int currentMaxAge = settings.MaxAge;
                string ageMaxBuffer = currentMaxAge.ToString();
                string newAgeMax = Widgets.TextField(ageMaxInputRect, ageMaxBuffer);
                if (newAgeMax != ageMaxBuffer && int.TryParse(newAgeMax, out int parsedMaxAge) && parsedMaxAge >= settings.MinAge && parsedMaxAge <= 120)
                {
                    settings.MaxAge = parsedMaxAge;
                    SaveRaceSettings();
                }

                // Max Age slider
                Rect ageMaxSliderRect = new Rect(leftPadding + 370f, y, 100f, sectionHeight);
                int newMaxAge = (int)Widgets.HorizontalSlider(ageMaxSliderRect, settings.MaxAge, settings.MinAge, 120, middleAlignment: true, label: settings.MaxAge.ToString(), leftAlignedLabel: settings.MinAge.ToString(), rightAlignedLabel: "120");
                if (newMaxAge != settings.MaxAge)
                {
                    settings.MaxAge = newMaxAge;
                    SaveRaceSettings();
                }
                y += sectionHeight;

                // Allow Custom Xenotypes
                Rect customXenoRect = new Rect(leftPadding, y, viewRect.width - leftPadding, sectionHeight);
                bool currentAllowCustom = settings.AllowCustomXenotypes;
                Widgets.CheckboxLabeled(customXenoRect, "Allow Custom Xenotypes for this Race", ref currentAllowCustom);
                if (currentAllowCustom != settings.AllowCustomXenotypes)
                {
                    settings.AllowCustomXenotypes = currentAllowCustom;
                    SaveRaceSettings();
                }
                y += sectionHeight + 10f;

                // Xenotype Settings section (only if Biotech is active)
                if (ModsConfig.BiotechActive)
                {
                    Rect xenotypeLabelRect = new Rect(leftPadding, y, viewRect.width, sectionHeight);
                    Text.Font = GameFont.Medium;
                    Widgets.Label(xenotypeLabelRect, "Xenotype Settings");
                    Text.Font = GameFont.Small;
                    y += sectionHeight;

                    // Get allowed xenotypes
                    var allowedXenotypes = GetAllowedXenotypes(selectedRace);

                    if (allowedXenotypes.Count > 0)
                    {
                        // Column headers
                        Rect xenotypeHeaderRect = new Rect(leftPadding, y, columnWidth, sectionHeight);
                        Rect enabledHeaderRect = new Rect(leftPadding + columnWidth, y, 80f, sectionHeight);
                        Rect multiplierHeaderRect = new Rect(leftPadding + columnWidth + 90f, y, 100f, sectionHeight);

                        Text.Font = GameFont.Tiny;
                        Widgets.Label(xenotypeHeaderRect, "Xenotype");
                        Widgets.Label(enabledHeaderRect, "Enabled");
                        Widgets.Label(multiplierHeaderRect, "Multiplier");
                        Text.Font = GameFont.Small;
                        y += sectionHeight;

                        // Xenotype rows
                        foreach (var xenotype in allowedXenotypes)
                        {
                            // Initialize if not exists
                            if (!settings.EnabledXenotypes.ContainsKey(xenotype))
                            {
                                bool defaultEnabled = xenotype == "Baseliner" ||
                                                    xenotype.ToLower() == selectedRace.defName.ToLower() ||
                                                    xenotype.ToLower() == selectedRace.LabelCap.RawText.ToLower();
                                settings.EnabledXenotypes[xenotype] = defaultEnabled;
                            }
                            if (!settings.XenotypePrices.ContainsKey(xenotype))
                            {
                                settings.XenotypePrices[xenotype] = GetDefaultXenotypeMultiplier(xenotype);
                            }

                            // Xenotype name
                            Rect xenotypeNameRect = new Rect(leftPadding, y, columnWidth - 10f, sectionHeight);
                            Widgets.Label(xenotypeNameRect, xenotype);

                            // Enabled checkbox 
                            Rect xenotypeEnabledRect = new Rect(leftPadding + columnWidth, y, 30f, sectionHeight);
                            bool currentXenoEnabled = settings.EnabledXenotypes[xenotype];
                            // Use the top-left position of the rect instead of the rect itself
                            Widgets.Checkbox(xenotypeEnabledRect.position, ref currentXenoEnabled, 24f);
                            if (currentXenoEnabled != settings.EnabledXenotypes[xenotype])
                            {
                                settings.EnabledXenotypes[xenotype] = currentXenoEnabled;
                                SaveRaceSettings();
                            }

                            // Multiplier input
                            Rect multiplierRect = new Rect(leftPadding + columnWidth + 90f, y, 80f, sectionHeight);
                            float multiplier = settings.XenotypePrices[xenotype];
                            string multiplierBuffer = multiplier.ToString("F2");
                            // Use simple TextField for multiplier
                            string newMultiplier = Widgets.TextField(multiplierRect, multiplierBuffer);
                            if (newMultiplier != multiplierBuffer && float.TryParse(newMultiplier, out float parsedMultiplier) &&
                                parsedMultiplier >= 0.1f && parsedMultiplier <= 10f)
                            {
                                settings.XenotypePrices[xenotype] = parsedMultiplier;
                                SaveRaceSettings();
                            }

                            y += sectionHeight;
                        }
                    }
                    else
                    {
                        // No xenotype restrictions
                        Rect noXenotypeRect = new Rect(leftPadding, y, viewRect.width - leftPadding, sectionHeight);
                        Widgets.Label(noXenotypeRect, "No xenotype restrictions - all xenotypes allowed");
                        y += sectionHeight;
                    }
                }
            }
            Widgets.EndScrollView();
        }

        private float CalculateDetailsHeight(RaceSettings settings)
        {
            float height = 0f;

            // Basic Info section
            height += 28f; // Header
            height += 28f * 1.5f; // Description
            height += 10f; // Spacing

            // Settings section
            height += 28f; // Header
            height += 28f; // Enabled + Price row
            height += 28f; // Age settings row (now includes sliders)
            height += 28f; // Custom xenotypes
            height += 10f; // Spacing

            // Xenotype section
            if (ModsConfig.BiotechActive)
            {
                height += 28f; // Header
                var allowedXenotypes = GetAllowedXenotypes(selectedRace);
                if (allowedXenotypes.Count > 0)
                {
                    height += 28f; // Column headers
                    height += 28f * allowedXenotypes.Count; // Xenotype rows
                }
                else
                {
                    height += 28f; // No restrictions message
                }
            }

            return height + 20f; // Extra padding
        }

        private List<string> GetAllowedXenotypes(ThingDef raceDef)
        {
            // Use HAR patch if available, otherwise return all xenotypes
            if (CAPChatInteractiveMod.Instance?.AlienProvider != null)
            {
                return CAPChatInteractiveMod.Instance.AlienProvider.GetAllowedXenotypes(raceDef);
            }

            // Fallback: return all xenotypes if no restrictions
            if (ModsConfig.BiotechActive)
            {
                return DefDatabase<XenotypeDef>.AllDefs.Select(x => x.defName).ToList();
            }

            return new List<string>();
        }

        // In Dialog_PawnSettings.cs - Update the LoadRaceSettings method
        private void LoadRaceSettings()
        {
            // Load from JSON file using JsonFileManager
            raceSettings = JsonFileManager.LoadRaceSettings();

            // Initialize defaults for any missing races - BUT ONLY FOR NON-EXCLUDED RACES
            foreach (var race in GetHumanlikeRaces())
            {
                // Skip excluded races entirely - don't even add them to settings
                if (RaceUtils.IsRaceExcluded(race))
                {
                    Logger.Debug($"Skipping excluded race in settings: {race.defName}");
                    continue;
                }

                if (!raceSettings.ContainsKey(race.defName))
                {
                    raceSettings[race.defName] = new RaceSettings
                    {
                        Enabled = true, // Enable by default for non-excluded races
                        BasePrice = CalculateDefaultPrice(race),
                        MinAge = 16,
                        MaxAge = 65,
                        AllowCustomXenotypes = true,
                        XenotypePrices = new Dictionary<string, float>(),
                        EnabledXenotypes = new Dictionary<string, bool>()
                    };

                    // Initialize default xenotype settings if Biotech is active
                    if (ModsConfig.BiotechActive)
                    {
                        var allowedXenotypes = GetAllowedXenotypes(race);
                        foreach (var xenotype in allowedXenotypes)
                        {
                            // Default rules: always enable Baseliner, enable xenotypes with same name as race
                            bool defaultEnabled = xenotype == "Baseliner" ||
                                                xenotype.ToLower() == race.defName.ToLower() ||
                                                xenotype.ToLower() == race.LabelCap.RawText.ToLower();
                            raceSettings[race.defName].EnabledXenotypes[xenotype] = defaultEnabled;

                            // Set default price multipliers
                            raceSettings[race.defName].XenotypePrices[xenotype] = GetDefaultXenotypeMultiplier(xenotype);
                        }
                    }
                }
            }

            // Remove any excluded races that might have been in the saved settings
            var excludedRaces = raceSettings.Keys.Where(key =>
            {
                var raceDef = DefDatabase<ThingDef>.AllDefs.FirstOrDefault(d => d.defName == key);
                return raceDef != null && RaceUtils.IsRaceExcluded(raceDef);
            }).ToList();

            foreach (var excludedKey in excludedRaces)
            {
                Logger.Debug($"Removing excluded race from settings: {excludedKey}");
                raceSettings.Remove(excludedKey);
            }

            // Save any newly initialized settings
            SaveRaceSettings();
        }

        private static float GetDefaultXenotypeMultiplier(string xenotypeName)
        {
            // Use gene-based pricing if available, otherwise fall back to defaults
            return GeneUtils.CalculateXenotypeGeneCost(xenotypeName);
        }

        private void SaveRaceSettings()
        {
            try
            {
                string json = JsonFileManager.SerializeRaceSettings(raceSettings);
                JsonFileManager.SaveFile("RaceSettings.json", json);
                Logger.Debug($"Saved race settings for {raceSettings.Count} races");
            }
            catch (Exception ex)
            {
                Logger.Error($"Error saving race settings: {ex}");
            }
        }

        private int CalculateDefaultPrice(ThingDef race)
        {
            // Use BaseMarketValue if available, otherwise use default pricing
            if (race.BaseMarketValue > 0)
            {
                return (int)(race.BaseMarketValue * 1.5f); // Adjust multiplier as needed
            }

            // Fallback pricing
            return race == ThingDefOf.Human ? 1000 : 1500;
        }

        private IEnumerable<ThingDef> GetHumanlikeRaces()
        {
            return RaceUtils.GetAllHumanlikeRaces(); // Use the filtered version
        }

        private void FilterRaces()
        {
            lastSearch = searchQuery;
            filteredRaces.Clear();

            // Use the filtered races from RaceUtils, but also filter by what's in our settings
            var allRaces = GetHumanlikeRaces().Where(race => raceSettings.ContainsKey(race.defName)).AsEnumerable();

            // Search filter
            if (!string.IsNullOrEmpty(searchQuery))
            {
                string searchLower = searchQuery.ToLower();
                allRaces = allRaces.Where(race =>
                    race.LabelCap.RawText.ToLower().Contains(searchLower) ||
                    race.defName.ToLower().Contains(searchLower) ||
                    (race.description ?? "").ToLower().Contains(searchLower)
                );
            }

            filteredRaces = allRaces.ToList();
            SortRaces();

            Logger.Debug($"Filtered races: {filteredRaces.Count} races after filtering");
        }

        private void SortRaces()
        {
            switch (sortMethod)
            {
                case PawnSortMethod.Name:
                    filteredRaces = sortAscending ?
                        filteredRaces.OrderBy(r => r.LabelCap.RawText).ToList() :
                        filteredRaces.OrderByDescending(r => r.LabelCap.RawText).ToList();
                    break;
                case PawnSortMethod.Category:
                    // Group by mod source
                    filteredRaces = sortAscending ?
                        filteredRaces.OrderBy(r => r.modContentPack?.Name ?? "Core").ToList() :
                        filteredRaces.OrderByDescending(r => r.modContentPack?.Name ?? "Core").ToList();
                    break;
                case PawnSortMethod.Status:
                    filteredRaces = sortAscending ?
                        filteredRaces.OrderBy(r => raceSettings[r.defName].Enabled).ToList() :
                        filteredRaces.OrderByDescending(r => raceSettings[r.defName].Enabled).ToList();
                    break;
            }
        }
    }

    public class RaceSettings
    {
        public bool Enabled { get; set; } = true;
        public int BasePrice { get; set; } = 1000;
        public int MinAge { get; set; } = 16;
        public int MaxAge { get; set; } = 65;
        public bool AllowCustomXenotypes { get; set; } = true;
        public Dictionary<string, float> XenotypePrices { get; set; } = new Dictionary<string, float>();
        public Dictionary<string, bool> EnabledXenotypes { get; set; } = new Dictionary<string, bool>();
    }

    public enum PawnSortMethod
    {
        Name,
        Category,
        Status
    }
}