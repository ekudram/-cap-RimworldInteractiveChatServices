using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using _CAP__Chat_Interactive.Interfaces;

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

            // Header
            Rect headerRect = new Rect(0f, 0f, inRect.width, 40f);
            DrawHeader(headerRect);

            // Main content area
            Rect contentRect = new Rect(0f, 45f, inRect.width, inRect.height - 45f - CloseButSize.y);
            DrawContent(contentRect);
        }

        private void DrawHeader(Rect rect)
        {
            Widgets.BeginGroup(rect);

            // Title with counts
            Text.Font = GameFont.Medium;
            Rect titleRect = new Rect(0f, 0f, 200f, 30f);
            string titleText = $"Pawn Races ({DefDatabase<ThingDef>.AllDefs.Count(d => d.race?.Humanlike ?? false)})";
            if (filteredRaces.Count != GetHumanlikeRaces().Count())
                titleText += $" - Filtered: {filteredRaces.Count}";
            Widgets.Label(titleRect, titleText);
            Text.Font = GameFont.Small;

            // Search bar
            Rect searchRect = new Rect(210f, 5f, 250f, 30f);
            searchQuery = Widgets.TextField(searchRect, searchQuery);

            // Sort buttons
            Rect sortRect = new Rect(470f, 5f, 300f, 30f);
            DrawSortButtons(sortRect);

            Widgets.EndGroup();
        }

        private void DrawSortButtons(Rect rect)
        {
            Widgets.BeginGroup(rect);

            float buttonWidth = 90f;
            float spacing = 5f;
            float x = 0f;

            // Sort by Name
            if (Widgets.ButtonText(new Rect(x, 0f, buttonWidth, 30f), "Name"))
            {
                if (sortMethod == PawnSortMethod.Name)
                    sortAscending = !sortAscending;
                else
                    sortMethod = PawnSortMethod.Name;
                SortRaces();
            }
            x += buttonWidth + spacing;

            // Sort by Category
            if (Widgets.ButtonText(new Rect(x, 0f, buttonWidth, 30f), "Category"))
            {
                if (sortMethod == PawnSortMethod.Category)
                    sortAscending = !sortAscending;
                else
                    sortMethod = PawnSortMethod.Category;
                SortRaces();
            }
            x += buttonWidth + spacing;

            // Sort by Status
            if (Widgets.ButtonText(new Rect(x, 0f, buttonWidth, 30f), "Status"))
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
                    var settings = raceSettings[race.defName];

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

            var settings = raceSettings[selectedRace.defName];

            // Header with race name
            Rect headerRect = new Rect(rect.x, rect.y, rect.width, 40f);
            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.MiddleCenter;

            string headerText = $"{selectedRace.LabelCap}";
            if (!settings.Enabled)
                headerText += " 🚫 DISABLED";

            Widgets.Label(headerRect, headerText);
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperLeft;

            // Details content with scrolling
            Rect contentRect = new Rect(rect.x, rect.y + 50f, rect.width, rect.height - 60f);
            DrawRaceDetailsContent(contentRect, settings);
        }

        // In Dialog_PawnSettings.cs - Replace the DrawRaceDetailsContent method
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

                // Basic Info section
                Rect basicLabelRect = new Rect(leftPadding, y, viewRect.width, sectionHeight);
                Widgets.Label(basicLabelRect, "Basic Information:");
                y += sectionHeight;

                // Race description
                Rect descRect = new Rect(leftPadding + 10f, y, viewRect.width - leftPadding, sectionHeight * 2);
                string desc = string.IsNullOrEmpty(selectedRace.description) ?
                    "No description available" : selectedRace.description;
                Widgets.Label(descRect, $"Description: {desc}");
                y += sectionHeight * 2;

                // Race def name
                Rect defNameRect = new Rect(leftPadding + 10f, y, viewRect.width - leftPadding, sectionHeight);
                Widgets.Label(defNameRect, $"Def Name: {selectedRace.defName}");
                y += sectionHeight;

                y += 10f;

                // Basic Settings section
                Rect settingsLabelRect = new Rect(leftPadding, y, viewRect.width, sectionHeight);
                Widgets.Label(settingsLabelRect, "Basic Settings:");
                y += sectionHeight;

                // Enabled toggle
                Rect enabledRect = new Rect(leftPadding + 10f, y, viewRect.width - leftPadding - 100f, sectionHeight);
                Widgets.Label(enabledRect, "Enabled:");
                Rect enabledToggleRect = new Rect(viewRect.width - 90f, y, 80f, sectionHeight);
                if (Widgets.ButtonText(enabledToggleRect, settings.Enabled ? "ON" : "OFF"))
                {
                    settings.Enabled = !settings.Enabled;
                    SaveRaceSettings();
                }
                y += sectionHeight;

                // Base price setting
                Rect priceLabelRect = new Rect(leftPadding + 10f, y, viewRect.width - leftPadding - 100f, sectionHeight);
                Widgets.Label(priceLabelRect, "Base Price (coins):");
                Rect priceInputRect = new Rect(viewRect.width - 90f, y, 80f, sectionHeight);
                int currentPrice = settings.BasePrice;
                string priceBuffer = currentPrice.ToString();
                Widgets.TextFieldNumeric(priceInputRect, ref currentPrice, ref priceBuffer, 0, 100000);
                if (currentPrice != settings.BasePrice)
                {
                    settings.BasePrice = currentPrice;
                    SaveRaceSettings();
                }
                y += sectionHeight;

                // Description for price
                Rect priceDescRect = new Rect(leftPadding + 10f, y, viewRect.width - leftPadding, 14f);
                Text.Font = GameFont.Tiny;
                GUI.color = new Color(0.7f, 0.7f, 0.7f);
                Widgets.Label(priceDescRect, "Base cost for purchasing a pawn of this race");
                Text.Font = GameFont.Small;
                GUI.color = Color.white;
                y += 20f;

                // Xenotype Settings section (only if Biotech is active)
                if (ModsConfig.BiotechActive)
                {
                    Rect xenotypeLabelRect = new Rect(leftPadding, y, viewRect.width, sectionHeight);
                    Widgets.Label(xenotypeLabelRect, "Xenotype Settings:");
                    y += sectionHeight;

                    // Get allowed xenotypes from HAR patch if available
                    var allowedXenotypes = GetAllowedXenotypes(selectedRace);

                    if (allowedXenotypes.Count > 0)
                    {
                        // Xenotype enable/disable toggles
                        Rect xenotypeEnableLabelRect = new Rect(leftPadding + 10f, y, viewRect.width - leftPadding, sectionHeight);
                        Widgets.Label(xenotypeEnableLabelRect, "Enable/Disable Xenotypes:");
                        y += sectionHeight;

                        foreach (var xenotype in allowedXenotypes)
                        {
                            // Initialize if not exists
                            if (!settings.EnabledXenotypes.ContainsKey(xenotype))
                            {
                                // Default rules: always enable Baseliner, enable xenotypes with same name as race
                                bool defaultEnabled = xenotype == "Baseliner" ||
                                                    xenotype.ToLower() == selectedRace.defName.ToLower() ||
                                                    xenotype.ToLower() == selectedRace.LabelCap.RawText.ToLower();
                                settings.EnabledXenotypes[xenotype] = defaultEnabled;
                            }

                            Rect xenotypeToggleRect = new Rect(leftPadding + 20f, y, viewRect.width - leftPadding - 100f, sectionHeight);
                            bool currentEnabled = settings.EnabledXenotypes[xenotype];
                            Widgets.CheckboxLabeled(xenotypeToggleRect, xenotype, ref currentEnabled);
                            if (currentEnabled != settings.EnabledXenotypes[xenotype])
                            {
                                settings.EnabledXenotypes[xenotype] = currentEnabled;
                                SaveRaceSettings();
                            }
                            y += sectionHeight;
                        }

                        y += 5f;

                        // Xenotype price multipliers
                        Rect priceMultiplierLabelRect = new Rect(leftPadding + 10f, y, viewRect.width - leftPadding, sectionHeight);
                        Widgets.Label(priceMultiplierLabelRect, "Xenotype Price Multipliers:");
                        y += sectionHeight;

                        foreach (var xenotype in allowedXenotypes)
                        {
                            if (!settings.XenotypePrices.ContainsKey(xenotype))
                            {
                                settings.XenotypePrices[xenotype] = 1.0f;
                            }

                            Rect xenotypeRect = new Rect(leftPadding + 20f, y, viewRect.width - leftPadding - 100f, sectionHeight);
                            Widgets.Label(xenotypeRect, $"{xenotype}:");

                            Rect multiplierRect = new Rect(viewRect.width - 90f, y, 80f, sectionHeight);
                            float multiplier = settings.XenotypePrices[xenotype];
                            string multiplierBuffer = multiplier.ToString("F2");
                            Widgets.TextFieldNumeric(multiplierRect, ref multiplier, ref multiplierBuffer, 0.1f, 10f);

                            if (multiplier != settings.XenotypePrices[xenotype])
                            {
                                settings.XenotypePrices[xenotype] = multiplier;
                                SaveRaceSettings();
                            }
                            y += sectionHeight;
                        }
                    }
                    else
                    {
                        // No xenotype restrictions
                        Rect noXenotypeRect = new Rect(leftPadding + 10f, y, viewRect.width - leftPadding, sectionHeight);
                        Widgets.Label(noXenotypeRect, "No xenotype restrictions - all xenotypes allowed");
                        y += sectionHeight;
                    }

                    // Xenotype enabled toggle
                    Rect xenotypeEnabledRect = new Rect(leftPadding + 10f, y, viewRect.width - leftPadding - 100f, sectionHeight);
                    bool currentAllowCustom = settings.AllowCustomXenotypes;
                    Widgets.CheckboxLabeled(xenotypeEnabledRect, "Allow custom xenotypes for this race", ref currentAllowCustom);
                    if (currentAllowCustom != settings.AllowCustomXenotypes)
                    {
                        settings.AllowCustomXenotypes = currentAllowCustom;
                        SaveRaceSettings();
                    }
                    y += sectionHeight;
                }

                // Advanced Settings section
                Rect advancedLabelRect = new Rect(leftPadding, y, viewRect.width, sectionHeight);
                Widgets.Label(advancedLabelRect, "Advanced Settings:");
                y += sectionHeight;

                // Age range settings with validation
                Rect ageMinRect = new Rect(leftPadding + 10f, y, viewRect.width - leftPadding - 100f, sectionHeight);
                Widgets.Label(ageMinRect, "Minimum Age:");
                Rect ageMinInputRect = new Rect(viewRect.width - 90f, y, 80f, sectionHeight);
                int currentMinAge = settings.MinAge;
                string ageMinBuffer = currentMinAge.ToString();
                // CHANGED: Minimum age clamped at 4, maximum at 120
                Widgets.TextFieldNumeric(ageMinInputRect, ref currentMinAge, ref ageMinBuffer, 4, 120);
                if (currentMinAge != settings.MinAge)
                {
                    settings.MinAge = currentMinAge;
                    // Ensure min age doesn't exceed max age
                    if (settings.MinAge > settings.MaxAge)
                        settings.MaxAge = settings.MinAge;
                    SaveRaceSettings();
                }
                y += sectionHeight;

                Rect ageMaxRect = new Rect(leftPadding + 10f, y, viewRect.width - leftPadding - 100f, sectionHeight);
                Widgets.Label(ageMaxRect, "Maximum Age:");
                Rect ageMaxInputRect = new Rect(viewRect.width - 90f, y, 80f, sectionHeight);
                int currentMaxAge = settings.MaxAge;
                string ageMaxBuffer = currentMaxAge.ToString();
                // CHANGED: Maximum age clamped at 120
                Widgets.TextFieldNumeric(ageMaxInputRect, ref currentMaxAge, ref ageMaxBuffer, settings.MinAge, 120);
                if (currentMaxAge != settings.MaxAge)
                {
                    settings.MaxAge = currentMaxAge;
                    SaveRaceSettings();
                }
                y += sectionHeight;

            }
            Widgets.EndScrollView();
        }

        // In Dialog_PawnSettings.cs - Update CalculateDetailsHeight method
        private float CalculateDetailsHeight(RaceSettings settings)
        {
            float height = 50f; // Header space
            height += 28f * 4; // Basic info
            height += 38f; // Basic settings label + spacing
            height += 28f; // Enabled
            height += 28f; // Base price
            height += 14f; // Price description
            height += 20f; // Extra spacing

            // Xenotype section
            if (ModsConfig.BiotechActive)
            {
                height += 28f; // Xenotype header
                var allowedXenotypes = GetAllowedXenotypes(selectedRace);
                if (allowedXenotypes.Count > 0)
                {
                    height += 28f; // Enable/disable header
                    height += 28f * allowedXenotypes.Count; // Xenotype enable toggles
                    height += 5f; // Spacing
                    height += 28f; // Price multiplier header
                    height += 28f * allowedXenotypes.Count; // Xenotype multipliers
                }
                else
                {
                    height += 28f; // No restrictions message
                }
                height += 28f; // Custom xenotypes toggle
            }

            height += 38f; // Advanced settings label + spacing
            height += 28f; // Min age
            height += 28f; // Max age

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
        // In Dialog_PawnSettings.cs - Replace the LoadRaceSettings method
        private void LoadRaceSettings()
        {
            // Load from JSON file using JsonFileManager
            raceSettings = JsonFileManager.LoadRaceSettings();

            // Initialize defaults for any missing races
            foreach (var race in GetHumanlikeRaces())
            {
                if (!raceSettings.ContainsKey(race.defName))
                {
                    raceSettings[race.defName] = new RaceSettings
                    {
                        Enabled = true,
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
                        }
                    }
                }
            }

            // Save any newly initialized settings
            SaveRaceSettings();
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
            return DefDatabase<ThingDef>.AllDefs.Where(d => d.race?.Humanlike ?? false);
        }

        private void FilterRaces()
        {
            lastSearch = searchQuery;
            filteredRaces.Clear();

            var allRaces = GetHumanlikeRaces().AsEnumerable();

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
        public int MinAge { get; set; } = 13;
        public int MaxAge { get; set; } = 120;
        public bool AllowCustomXenotypes { get; set; } = true;
        public Dictionary<string, float> XenotypePrices { get; set; } = new Dictionary<string, float>();
        // ADD THIS: Xenotype enable/disable settings
        public Dictionary<string, bool> EnabledXenotypes { get; set; } = new Dictionary<string, bool>();
    }

    public enum PawnSortMethod
    {
        Name,
        Category,
        Status
    }
}