// Dialog_PawnRaceSettings.cs
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
// A dialog window for configuring pawn races and xenotypes 
using _CAP__Chat_Interactive.Utilities;
using CAP_ChatInteractive;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Verse;

namespace CAP_ChatInteractive
{
    public class Dialog_PawnRaceSettings : Window
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
        private string ageMinBuffer = "";
        private string ageMaxBuffer = "";
        private bool buffersInitialized = false;
        private string lastSelectedRaceDefName = "";

        public override Vector2 InitialSize => new Vector2(1000f, 700f);

        public Dialog_PawnRaceSettings()
        {
            doCloseButton = true;
            forcePause = true;
            absorbInputAroundWindow = true;
            // optionalTitle = "Pawn Race & Xenotype Settings";
            raceSettings = RaceSettingsManager.RaceSettings;
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
            GUI.color = ColorLibrary.HeaderAccent;
            Rect titleRect = new Rect(0f, 0f, 200f, 30f);

            // FIXED: Use enabled races count instead of all humanlike races
            int enabledRacesCount = RaceUtils.GetEnabledRaces().Count;
            int totalRacesCount = DefDatabase<ThingDef>.AllDefs.Count(d => d.race?.Humanlike ?? false);
            // string titleText = $"Pawn Races ({enabledRacesCount}";
            string titleText = $"RICS.Header.PawnRaces".Translate(enabledRacesCount);

            // Show filtered count if search is active
            if (filteredRaces.Count != enabledRacesCount)
                titleText += $"/{filteredRaces.Count}";

            //titleText += $")";

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
                Widgets.Label(new Rect(0f, searchY, 40f, 30f), "RICS.Search".Translate() + ":");
            }

            Rect searchRect = new Rect(30f, searchY, 170f, 24f);
            searchQuery = Widgets.TextField(searchRect, searchQuery);

            // Sort buttons
            Rect sortRect = new Rect(210f, controlsY, 300f, 24f);
            DrawSortButtons(sortRect);

            // In DrawHeader method, after the sort buttons:

            // Reset All Prices button - moved left to make room for help button
            Rect resetAllRect = new Rect(rect.width - 160f, controlsY, 120f, 24f);
            if (Widgets.ButtonText(resetAllRect, "RICS.Button.ResetAllPrices".Translate()))
            {
                if (selectedRace != null)
                {
                    // Show confirmation dialog
                    string message = "RICS.Dialog.ResetXenotypeQuestion".Translate(selectedRace.LabelCap) +
                                     "\n\n" +
                                     "RICS.Dialog.ResetXenotypeWarning".Translate();

                    Find.WindowStack.Add(new Dialog_MessageBox(
                        message,
                        "RICS.Dialog.Button.ResetAll".Translate(),
                        () => ResetAllXenotypePrices(selectedRace),
                        "RICS.Dialog.Button.Cancel".Translate(),
                        null,
                        "RICS.Dialog.Title.ResetXenotypePrices".Translate()
                    ));
                }
                else
                {
                    // Messages.Message("Select a race first to reset prices", MessageTypeDefOf.RejectInput);
                    Messages.Message("RICS.Message.SelectRaceToReset".Translate(), MessageTypeDefOf.RejectInput);
                }
            }
            // TooltipHandler.TipRegion(resetAllRect, "Reset all xenotype prices for selected race to gene-based values");
            TooltipHandler.TipRegion(resetAllRect, "RICS.Tooltip.ResetAllPrices".Translate());

            // Info help icon - next to reset button
            //Rect infoRect = new Rect(rect.width - 190f, controlsY, 24f, 24f);
            Rect infoRect = new Rect(rect.width - 60f, 5f, 24f, 24f); // Positioned left of the gear
            Texture2D infoIcon = ContentFinder<Texture2D>.Get("UI/Buttons/InfoButton", false);
            if (infoIcon != null)
            {
                if (Widgets.ButtonImage(infoRect, infoIcon))
                {
                    Find.WindowStack.Add(new Dialog_PawnRacesHelp());
                }
                // TooltipHandler.TipRegion(infoRect, "Pawn Race Settings Help");
                TooltipHandler.TipRegion(infoRect, "RICS.Tooltip.PawnRacesHelp".Translate());
            }
            else
            {
                // Fallback text button
                if (Widgets.ButtonText(new Rect(rect.width - 190f, 5f, 45f, 24f), "RICS.Help".Translate())) //(Widgets.ButtonText(new Rect(rect.width - 190f, controlsY, 45f, 24f), "Help"))
                {
                    Find.WindowStack.Add(new Dialog_PawnRacesHelp());
                }
            }

            // Debug gear icon - top right corner (unchanged position)
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
            // TooltipHandler.TipRegion(debugRect, "Open Race Debug Information");
            TooltipHandler.TipRegion(debugRect, "RICS.Tooltip.DebugInfo".Translate());
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
            if (Widgets.ButtonText(new Rect(x, y, buttonWidth, 24f), "RICS.Name".Translate()))
            {
                if (sortMethod == PawnSortMethod.Name)
                    sortAscending = !sortAscending;
                else
                    sortMethod = PawnSortMethod.Name;
                SortRaces();
            }
            x += buttonWidth + spacing;

            // Sort by Category
            if (Widgets.ButtonText(new Rect(x, y, buttonWidth, 24f), "RICS.Category".Translate()))
            {
                if (sortMethod == PawnSortMethod.Category)
                    sortAscending = !sortAscending;
                else
                    sortMethod = PawnSortMethod.Category;
                SortRaces();
            }
            x += buttonWidth + spacing;

            // Sort by Status
            if (Widgets.ButtonText(new Rect(x, y, buttonWidth, 24f), "RICS.Status".Translate()))
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
            GUI.color = ColorLibrary.SubHeader;
            Widgets.Label(headerRect, "RICS.Header.Races".Translate());
            GUI.color = Color.white;
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
                        //Logger.Warning($"Race {race.defName} not found in raceSettings, skipping");
                        continue;
                    }

                    Rect buttonRect = new Rect(5f, y, viewRect.width - 10f, rowHeight - 2f);

                    // Race name with status indicator
                    string displayName = race.LabelCap;
                    if (!settings.Enabled)
                        displayName += " [" + "RICS.DISABLED".Translate() + "]";

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
        // Draws the details panel for the selected Race, including settings and xenotype configuration
        private void DrawRaceDetails(Rect rect)
        {
            Widgets.DrawMenuSection(rect);

            var fontRestore = Text.Font;      // capture
            TextAnchor anchorRestore = Text.Anchor;
            Color colorRestore = GUI.color;

            try
            {
                if (selectedRace == null)
                {
                    Rect messageRect = new Rect(rect.x, rect.y, rect.width, rect.height);
                    Text.Anchor = TextAnchor.MiddleCenter;
                    Widgets.Label(messageRect, "RICS.Label.SelectRace".Translate());
                    return;
                }

                // SAFELY get settings
                if (!raceSettings.TryGetValue(selectedRace.defName, out var settings))
                {
                    Rect messageRect = new Rect(rect.x, rect.y, rect.width, rect.height);
                    Text.Anchor = TextAnchor.MiddleCenter;
                    Widgets.Label(messageRect, "RICS.Label.RaceNotFound".Translate(selectedRace.LabelCap));
                    return;
                }

                // Compact header
                Rect headerRect = new Rect(rect.x, rect.y, rect.width, 30f);
                Text.Font = GameFont.Medium;
                Text.Anchor = TextAnchor.MiddleCenter;
                string headerText = $"{selectedRace.LabelCap}";
                GUI.color = ColorLibrary.Danger;
                if (!settings.Enabled)
                    headerText += " 🚫 " + "RICS.DISABLED".Translate();
                GUI.color = colorRestore;           // restore immediately after use

                Widgets.Label(headerRect, headerText);

                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.UpperLeft;

                // Details content with scrolling
                Rect contentRect = new Rect(rect.x, rect.y + 35f, rect.width, rect.height - 40f);
                DrawRaceDetailsContent(contentRect, settings);
            }
            finally
            {
                // Always restore, no matter what
                Text.Font = fontRestore;
                Text.Anchor = anchorRestore;
                GUI.color = colorRestore;
            }
        }

        private void DrawRaceDetailsContent(Rect rect, RaceSettings settings)
        {
            float contentWidth = rect.width - 30f;
            float viewHeight = CalculateDetailsHeight(settings);

            Rect viewRect = new Rect(0f, 0f, contentWidth, Mathf.Max(viewHeight, rect.height));

            Widgets.BeginScrollView(rect, ref detailsScrollPosition, viewRect);
            {
                if (!buffersInitialized || lastSelectedRaceDefName != selectedRace.defName)
                {
                    ageMinBuffer = settings.MinAge.ToString();
                    ageMaxBuffer = settings.MaxAge.ToString();
                    buffersInitialized = true;
                    lastSelectedRaceDefName = selectedRace.defName;
                }

                float y = 0f;
                float sectionHeight = 32f;
                float leftPadding = 15f;
                float columnWidth = (viewRect.width - leftPadding - 20f) / 2f;

                // Basic Info section
                Rect basicLabelRect = new Rect(leftPadding, y, viewRect.width, sectionHeight);
                Text.Font = GameFont.Medium;
                Widgets.Label(basicLabelRect, "Basic Information");
                Text.Font = GameFont.Small;
                y += sectionHeight;

                // Show inherent gender restrictions from RaceSettings (read-only)
                var raceSettings = RaceSettingsManager.GetRaceSettings(selectedRace.defName);
                if (raceSettings != null)
                {
                    string genderRestrictionText = "";

                    if (!raceSettings.AllowedGenders.AllowMale && !raceSettings.AllowedGenders.AllowFemale)
                    {
                        genderRestrictionText = "No genders allowed (custom race)";
                    }
                    else if (!raceSettings.AllowedGenders.AllowMale)
                    {
                        genderRestrictionText = "Female only";
                    }
                    else if (!raceSettings.AllowedGenders.AllowFemale)
                    {
                        genderRestrictionText = "Male only";
                    }
                    else if (!raceSettings.AllowedGenders.AllowOther)
                    {
                        genderRestrictionText = "Male/Female only (no other)";
                    }

                    if (!string.IsNullOrEmpty(genderRestrictionText))
                    {
                        Rect inherentGenderRect = new Rect(leftPadding, y, viewRect.width - leftPadding, sectionHeight);
                        Widgets.Label(inherentGenderRect, $"Inherent gender restriction: {genderRestrictionText}");
                        y += sectionHeight;
                    }
                }

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

                UIUtilities.TextFieldNumericFlexible(priceInputRect, ref currentPrice, ref priceBuffer, 0, 100000);
                if (currentPrice != settings.BasePrice)
                {
                    settings.BasePrice = currentPrice;
                    SaveRaceSettings();
                }
                y += sectionHeight + 4f;

                // Age settings - same row with sliders
                Rect ageMinLabelRect = new Rect(leftPadding, y, 100f, sectionHeight);
                Widgets.Label(ageMinLabelRect, $"Min Age: {settings.MinAge}  ");
                Rect ageMinInputRect = new Rect(leftPadding + 100f, y, 50f, sectionHeight);

                // Min Age text field with persistent buffer
                string newAgeMinBuffer = Widgets.TextField(ageMinInputRect, ageMinBuffer);
                if (newAgeMinBuffer != ageMinBuffer)
                {
                    ageMinBuffer = newAgeMinBuffer;

                    // Validate and commit if it's a valid number
                    if (int.TryParse(ageMinBuffer, out int parsedMinAge))
                    {
                        parsedMinAge = Mathf.Clamp(parsedMinAge, 4, 120);
                        settings.MinAge = parsedMinAge;
                        if (settings.MinAge > settings.MaxAge)
                            settings.MaxAge = settings.MinAge;
                        SaveRaceSettings();
                    }
                }

                // Reset buffer if field loses focus and contains invalid data
                string ageMinControlName = "AgeMinInput_" + selectedRace.defName;
                GUI.SetNextControlName(ageMinControlName);
                bool ageMinHasFocus = GUI.GetNameOfFocusedControl() == ageMinControlName;

                if (!ageMinHasFocus && !int.TryParse(ageMinBuffer, out _))
                {
                    ageMinBuffer = settings.MinAge.ToString();
                }

                // Min Age slider
                Rect ageMinSliderRect = new Rect(leftPadding + 160f, y, 100f, sectionHeight);
                int newMinAge = (int)Widgets.HorizontalSlider(ageMinSliderRect, settings.MinAge, 4, 120, middleAlignment: true, label: "", leftAlignedLabel: "4", rightAlignedLabel: "120");
                if (newMinAge != settings.MinAge)
                {
                    settings.MinAge = newMinAge;
                    if (settings.MinAge > settings.MaxAge)
                        settings.MaxAge = settings.MinAge;
                    ageMinBuffer = settings.MinAge.ToString(); // Update buffer to match
                    SaveRaceSettings();
                }

                Rect ageMaxLabelRect = new Rect(leftPadding + 300f, y, 100f, sectionHeight);
                Widgets.Label(ageMaxLabelRect, $"Max Age: {settings.MaxAge}  ");
                Rect ageMaxInputRect = new Rect(leftPadding + 400f, y, 50f, sectionHeight);

                // Max Age text field with persistent buffer
                string newAgeMaxBuffer = Widgets.TextField(ageMaxInputRect, ageMaxBuffer);
                if (newAgeMaxBuffer != ageMaxBuffer)
                {
                    ageMaxBuffer = newAgeMaxBuffer;

                    // Validate and commit if it's a valid number
                    if (int.TryParse(ageMaxBuffer, out int parsedMaxAge))
                    {
                        parsedMaxAge = Mathf.Clamp(parsedMaxAge, settings.MinAge, 120);
                        settings.MaxAge = parsedMaxAge;
                        SaveRaceSettings();
                    }
                }

                // Reset buffer if field loses focus and contains invalid data
                string ageMaxControlName = "AgeMaxInput_" + selectedRace.defName;
                GUI.SetNextControlName(ageMaxControlName);
                bool ageMaxHasFocus = GUI.GetNameOfFocusedControl() == ageMaxControlName;

                if (!ageMaxHasFocus && !int.TryParse(ageMaxBuffer, out _))
                {
                    ageMaxBuffer = settings.MaxAge.ToString();
                }

                // Max Age slider
                Rect ageMaxSliderRect = new Rect(leftPadding + 470f, y, 100f, sectionHeight);
                int newMaxAge = (int)Widgets.HorizontalSlider(ageMaxSliderRect, settings.MaxAge, settings.MinAge, 120, middleAlignment: true, label: "", leftAlignedLabel: settings.MinAge.ToString(), rightAlignedLabel: "120");
                if (newMaxAge != settings.MaxAge)
                {
                    settings.MaxAge = newMaxAge;
                    ageMaxBuffer = settings.MaxAge.ToString(); // Update buffer to match
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
                // Xenotype Settings section (only if Biotech is active)
                if (ModsConfig.BiotechActive)
                {
                    Rect xenotypeLabelRect = new Rect(leftPadding, y, viewRect.width, sectionHeight);
                    Text.Font = GameFont.Medium;
                    Widgets.Label(xenotypeLabelRect, "Xenotype Prices");
                    Text.Font = GameFont.Small;
                    y += sectionHeight;

                    var allowedXenotypes = GetAllowedXenotypes(selectedRace)
                        .OrderBy(x => x) // Ensure sorted
                        .ToList();
                    Logger.Debug($"HAR allows {allowedXenotypes.Count} xenotypes for {selectedRace.defName}: {string.Join(", ", allowedXenotypes)}");

                    if (allowedXenotypes.Count > 0)
                    {
                        // Column headers - UPDATED
                        Rect xenotypeHeaderRect = new Rect(leftPadding, y, columnWidth, sectionHeight);
                        Rect enabledHeaderRect = new Rect(leftPadding + columnWidth, y, 80f, sectionHeight);
                        Rect priceHeaderRect = new Rect(leftPadding + columnWidth + 90f, y, 120f, sectionHeight);

                        Text.Font = GameFont.Tiny;
                        Widgets.Label(xenotypeHeaderRect, "Xenotype");
                        Widgets.Label(enabledHeaderRect, "Enabled");
                        Widgets.Label(priceHeaderRect, "Price (silver)");
                        Text.Font = GameFont.Small;
                        y += sectionHeight;

                        // Xenotype rows - now only allowed/spawnable ones
                        foreach (var xenotype in allowedXenotypes)
                        {
                            // Initialize if not exists
                            if (!settings.EnabledXenotypes.ContainsKey(xenotype))
                            {
                                // Default enabled: true for all allowed (Baseliner special not needed since if allowed, enable)
                                bool defaultEnabled = true; // Or keep your original: xenotype == "Baseliner" || allowedXenotypes.Contains(xenotype); but Contains always true here
                                settings.EnabledXenotypes[xenotype] = defaultEnabled;
                                Logger.Debug($"Default enabled for {xenotype}: {defaultEnabled}");
                            }
                            if (!settings.XenotypePrices.ContainsKey(xenotype))
                            {
                                // Get price from settings manager instead of calculating it
                                float defaultPrice = RaceSettingsManager.GetRaceSettings(selectedRace.defName)?.BasePrice ?? settings.BasePrice;
                                settings.XenotypePrices[xenotype] = defaultPrice;
                            }

                            // Xenotype name
                            Rect xenotypeNameRect = new Rect(leftPadding, y, columnWidth - 10f, sectionHeight);
                            Widgets.Label(xenotypeNameRect, xenotype);

                            // Enabled checkbox 
                            Rect xenotypeEnabledRect = new Rect(leftPadding + columnWidth, y, 30f, sectionHeight);
                            bool currentXenoEnabled = settings.EnabledXenotypes[xenotype];
                            Widgets.Checkbox(xenotypeEnabledRect.position, ref currentXenoEnabled, 24f);
                            if (currentXenoEnabled != settings.EnabledXenotypes[xenotype])
                            {
                                settings.EnabledXenotypes[xenotype] = currentXenoEnabled;
                                SaveRaceSettings();
                            }

                            // Price input - CHANGED: from multiplier to price
                            Rect priceRect = new Rect(leftPadding + columnWidth + 90f, y, 120f, sectionHeight);
                            float currentPriceValue = settings.XenotypePrices[xenotype];
                            string xenotypePriceBuffer = currentPriceValue.ToString("F0");
                            string newPriceBuffer = Widgets.TextField(priceRect, xenotypePriceBuffer);

                            if (newPriceBuffer != xenotypePriceBuffer && float.TryParse(newPriceBuffer, out float parsedPrice))
                            {
                                parsedPrice = Mathf.Clamp(parsedPrice, 0f, 1000000f);
                                settings.XenotypePrices[xenotype] = parsedPrice;
                                SaveRaceSettings();
                            }

                            // Reset button with tooltip
                            Rect resetButtonRect = new Rect(leftPadding + columnWidth + 220f, y, 60f, sectionHeight);
                            if (Widgets.ButtonText(resetButtonRect, "Reset"))
                            {
                                float geneBasedPrice = GeneUtils.CalculateXenotypeMarketValue(selectedRace, xenotype);
                                settings.XenotypePrices[xenotype] = geneBasedPrice;
                                SaveRaceSettings();
                                Messages.Message($"Reset {xenotype} price to {geneBasedPrice:F0} silver", MessageTypeDefOf.NeutralEvent);
                            }

                            string resetTooltip = $"Reset {xenotype} price to gene-based value:\n" +
                                                  $"• Race base value: {selectedRace.BaseMarketValue:F0} silver\n" +
                                                  $"• Gene contribution: {GeneUtils.GetXenotypeGeneValueOnly(xenotype, selectedRace.BaseMarketValue):F0} silver\n" +
                                                  $"• Total: {GeneUtils.CalculateXenotypeMarketValue(selectedRace, xenotype):F0} silver\n" +
                                                  "\nClick to reset to Rimworld's calculated market value based on gene marketValueFactor";
                            TooltipHandler.TipRegion(resetButtonRect, new TipSignal(resetTooltip, xenotype.GetHashCode() + 1000));

                            y += sectionHeight;
                        }
                    }
                    else
                    {
                        // No allowed xenotypes
                        Rect noXenotypeRect = new Rect(leftPadding, y, viewRect.width - leftPadding, sectionHeight);
                        Widgets.Label(noXenotypeRect, "No xenotypes allowed for this race (HAR restrictions apply)");
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
            height += 32f; // Header
            height += 32f; // Enabled + Price row
            height += 40f; // Age settings row (now includes sliders)
            height += 32f; // Custom xenotypes
            height += 32f; // Gender settings row
            height += 10f; // Spacing

            // Xenotype section
            if (ModsConfig.BiotechActive)
            {
                height += 32f; // Header

                // Get ALL xenotypes for height calculation, not just allowed ones
                var allXenotypes = DefDatabase<XenotypeDef>.AllDefs
                    .Where(x => !string.IsNullOrEmpty(x.defName))
                    .Select(x => x.defName)
                    .ToList();

                if (allXenotypes.Count > 0)
                {
                    height += 30f; // Column headers
                    height += 30f * allXenotypes.Count; // Xenotype rows
                }
                else
                {
                    height += 30f; // No xenotypes message
                }
            }

            return height + 30f; // Extra padding
        }

        public static List<string> GetAllowedXenotypes(ThingDef raceDef)
        {
            if (!ModsConfig.BiotechActive)
                return new List<string>();

            if (raceDef == ThingDefOf.Human)
                return DefDatabase<XenotypeDef>.AllDefs
                    .Where(x => !string.IsNullOrEmpty(x.defName))
                    .Select(x => x.defName)
                    .OrderBy(x => x)
                    .ToList();

            // Check if HAR is active
            const string harModId = "erdelf.HumanoidAlienRaces";
            if (!ModsConfig.IsActive(harModId))
            {
                // No HAR: allow all
                return DefDatabase<XenotypeDef>.AllDefs
                    .Where(x => !string.IsNullOrEmpty(x.defName))
                    .Select(x => x.defName)
                    .OrderBy(x => x)
                    .ToList();
            }

            try
            {
                // Reflection to get alienRace field (object)
                var alienRaceField = raceDef.GetType().GetField("alienRace", BindingFlags.Public | BindingFlags.Instance);
                if (alienRaceField == null)
                {
                    Logger.Debug($"[RICS] No alienRace field found for {raceDef.defName} - treating as non-HAR race");
                    return DefDatabase<XenotypeDef>.AllDefs
                        .Where(x => !string.IsNullOrEmpty(x.defName))
                        .Select(x => x.defName)
                        .OrderBy(x => x)
                        .ToList();
                }

                var alienRaceObj = alienRaceField.GetValue(raceDef);
                if (alienRaceObj == null)
                    return new List<string>(); // No settings: none allowed (edge case)

                // Get raceRestriction
                var raceRestrictionField = alienRaceObj.GetType().GetField("raceRestriction", BindingFlags.Public | BindingFlags.Instance);
                if (raceRestrictionField == null)
                    return new List<string>(); // No restrictions: none? Or all? Adjust to all if preferred

                var restrictionObj = raceRestrictionField.GetValue(alienRaceObj);
                if (restrictionObj == null)
                    return DefDatabase<XenotypeDef>.AllDefs
                        .Where(x => !string.IsNullOrEmpty(x.defName))
                        .Select(x => x.defName)
                        .OrderBy(x => x)
                        .ToList();

                // Extract onlyUseRaceRestrictedXenotypes (bool)
                var onlyRestrictedField = restrictionObj.GetType().GetField("onlyUseRaceRestrictedXenotypes", BindingFlags.Public | BindingFlags.Instance);
                Logger.Debug($"[RICS] HAR restriction object for {raceDef.defName}: onlyUseRaceRestrictedXenotypes field found: {onlyRestrictedField != null}");
                bool onlyRestricted = onlyRestrictedField != null && (bool)onlyRestrictedField.GetValue(restrictionObj);

                // Extract lists (as IEnumerable<object>, then get defNames)
                var xenoListField = restrictionObj.GetType().GetField("xenotypeList", BindingFlags.Public | BindingFlags.Instance);
                var whiteListField = restrictionObj.GetType().GetField("whiteXenotypeList", BindingFlags.Public | BindingFlags.Instance);
                var blackListField = restrictionObj.GetType().GetField("blackXenotypeList", BindingFlags.Public | BindingFlags.Instance);

                var xenoList = (xenoListField?.GetValue(restrictionObj) as IEnumerable<object>)?.Select(x => GetDefName(x)).Where(n => n != null).ToList() ?? new List<string>();
                var whiteList = (whiteListField?.GetValue(restrictionObj) as IEnumerable<object>)?.Select(x => GetDefName(x)).Where(n => n != null).ToList() ?? new List<string>();
                var blackList = (blackListField?.GetValue(restrictionObj) as IEnumerable<object>)?.Select(x => GetDefName(x)).Where(n => n != null).ToList() ?? new List<string>();

                Logger.Debug($"[RICS] HAR restrictions for {raceDef.defName}: onlyRestricted={onlyRestricted}, xenoList={xenoList.Count}, white={whiteList.Count}, black={blackList.Count}");

                // Build allowed list per HAR logic
                var result = new HashSet<string>(); // Use set to avoid dups
                foreach (var xenDef in DefDatabase<XenotypeDef>.AllDefs)
                {
                    string defName = xenDef.defName;
                    if (string.IsNullOrEmpty(defName)) continue;

                    // Always exclude blacklist
                    if (blackList.Contains(defName)) continue;

                    // If whitelist present, only allow from whitelist
                    if (whiteList.Count > 0)
                    {
                        if (whiteList.Contains(defName)) result.Add(defName);
                        continue;
                    }

                    // If only restricted, only allow from xenotypeList
                    if (onlyRestricted)
                    {
                        if (xenoList.Contains(defName)) result.Add(defName);
                        continue;
                    }

                    // If xenotypeList present (without onlyRestricted), prefer it but allow others? Wait, per wiki: xenotypeList is exclusive only if onlyRestricted=true
                    // But wiki says xenotypeList is for "only members of your race can have" (i.e., race-exclusive), but for allowed, it's combined with white when onlyRestricted.
                    // To match: If onlyRestricted, allow xenotypeList + whiteList; else allow all except black (but xenotypeList marks race-exclusive, not allowance filter)

                    // Correct per wiki/tool: When onlyRestricted=true, allow only xenotypeList + whiteList; else allow all except black (xenotypeList/white are additives? But wiki says white is for non-exclusive allowance)
                    // Adjust: Always allow all except black, but if onlyRestricted, restrict to (xenotypeList + whiteList) minus black

                    if (onlyRestricted)
                    {
                        if (xenoList.Contains(defName) || whiteList.Contains(defName))
                            result.Add(defName);
                    }
                    else
                    {
                        result.Add(defName); // Allow all minus black (already skipped)
                    }
                }

                return result.OrderBy(n => n).ToList();
            }
            catch (Exception ex)
            {
                Logger.Warning($"[RICS] Failed to apply HAR xenotype restrictions for {raceDef.defName}: {ex.Message}");
                return DefDatabase<XenotypeDef>.AllDefs
                    .Where(x => !string.IsNullOrEmpty(x.defName))
                    .Select(x => x.defName)
                    .OrderBy(x => x)
                    .ToList();
            }
        }

        // Helper method (add this new private method in the class)
        private static string GetDefName(object defObj)
        {
            if (defObj == null) return null;
            var defNameField = defObj.GetType().GetField("defName", BindingFlags.Public | BindingFlags.Instance);
            return defNameField?.GetValue(defObj) as string;
        }

        private void SaveRaceSettings()
        {
            try
            {
                RaceSettingsManager.SaveSettings();
                Logger.Debug($"Saved race settings for {raceSettings.Count} races");
            }
            catch (Exception ex)
            {
                Logger.Error($"Error saving race settings: {ex}");
            }
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

            // Logger.Debug($"Filtered races: {filteredRaces.Count} races after filtering");
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
                        filteredRaces.OrderBy(r => r.modContentPack?.Name ?? "Rimworld").ToList() :
                        filteredRaces.OrderByDescending(r => r.modContentPack?.Name ?? "Rimworld").ToList();
                    break;
                case PawnSortMethod.Status:
                    filteredRaces = sortAscending ?
                        filteredRaces.OrderBy(r => raceSettings[r.defName].Enabled).ToList() :
                        filteredRaces.OrderByDescending(r => raceSettings[r.defName].Enabled).ToList();
                    break;
            }
        }

        public override void Close(bool doCloseSound = true)
        {
            // Force validation of any active numeric fields before closing
            ValidateAllNumericFields();
            base.Close(doCloseSound);
        }

        private void ResetAllXenotypePrices(ThingDef race)
        {
            if (race == null || !raceSettings.TryGetValue(race.defName, out var settings))
                return;

            int resetCount = 0;

            // Reset all xenotype prices for this race
            foreach (var xenotype in settings.XenotypePrices.Keys.ToList())
            {
                float geneBasedPrice = GeneUtils.CalculateXenotypeMarketValue(race, xenotype);
                settings.XenotypePrices[xenotype] = geneBasedPrice;
                resetCount++;
            }

            SaveRaceSettings();

            // Show feedback
            string message = $"RICS.Reset".Translate() + $" {resetCount} " + "RICS.Message.xenotypepricesfor".Translate() + $" {race.LabelCap}";
            Messages.Message(message, MessageTypeDefOf.PositiveEvent);

            // Optional: Log details
            Logger.Debug(message);
        }

        private void ValidateAllNumericFields()
        {
            // This would validate any pending numeric inputs
            // For now, we'll just ensure the age settings are valid
            if (selectedRace != null && raceSettings.TryGetValue(selectedRace.defName, out var settings))
            {
                // Ensure min/max age are valid
                if (settings.MinAge > settings.MaxAge)
                {
                    settings.MaxAge = settings.MinAge;
                }
                SaveRaceSettings();
            }
        }
    }
}