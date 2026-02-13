// Dialog_EventsEditor.cs
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
// A dialog window for editing and managing chat-interactive events
using _CAP__Chat_Interactive.Windows.Dialogs;
using CAP_ChatInteractive.Incidents;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using Verse;

namespace CAP_ChatInteractive
{
    public class Dialog_EventsEditor : Window
    {
        private Vector2 scrollPosition = Vector2.zero;
        private Vector2 categoryScrollPosition = Vector2.zero;
        private string searchQuery = "";
        private string lastSearch = "";
        private EventSortMethod sortMethod = EventSortMethod.Name;
        private bool sortAscending = true;
        private string selectedModSource = "All";
        private string selectedCategory = "All";
        private Dictionary<string, int> modSourceCounts = new Dictionary<string, int>();
        private Dictionary<string, int> categoryCounts = new Dictionary<string, int>();
        public List<BuyableIncident> filteredEvents = new List<BuyableIncident>();  //
        private Dictionary<string, (int baseCost, string karmaType)> originalSettings = new Dictionary<string, (int, string)>();
        private EventListViewType listViewType = EventListViewType.ModSource;

        //private CAPGlobalChatSettings settingsGlobalChat;
        private Dictionary<string, string> numericBuffers = new Dictionary<string, string>();

        public override Vector2 InitialSize => new Vector2(1200f, 700f);

        public Dialog_EventsEditor()
        {
            doCloseButton = true;
            forcePause = true;
            absorbInputAroundWindow = true;

            BuildModSourceCounts();
            BuildCategoryCounts();
            FilterEvents();
            SaveOriginalSettings();
        }

        public override void DoWindowContents(Rect inRect)
        {
            if (searchQuery != lastSearch || filteredEvents.Count == 0)
            {
                FilterEvents();
            }

            Rect headerRect = new Rect(0f, 0f, inRect.width, 65f);
            DrawHeader(headerRect);

            Rect contentRect = new Rect(0f, 70f, inRect.width, inRect.height - 70f - CloseButSize.y);
            DrawContent(contentRect);
        }

        private void DrawHeader(Rect rect)
        {
            Widgets.BeginGroup(rect);

            // Title row
            Text.Font = GameFont.Medium;
            GUI.color = ColorLibrary.HeaderAccent;
            Rect titleRect = new Rect(0f, 0f, 200f, 30f);
            Widgets.Label(titleRect, "RICS.EventsEditor".Translate());

            // Draw underline
            Rect underlineRect = new Rect(titleRect.x, titleRect.yMax - 2f, titleRect.width, 2f);
            Widgets.DrawLineHorizontal(underlineRect.x, underlineRect.y, underlineRect.width);

            Text.Font = GameFont.Small;
            GUI.color = Color.white;

            // Second row for controls
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
                Widgets.Label(new Rect(0f, searchY, 40f, 30f), "RICS.EventsSearch".Translate());
            }

            Rect searchRect = new Rect(30f, searchY, 170f, 24f); // Adjusted position for icon
            searchQuery = Widgets.TextField(searchRect, searchQuery);

            // Sort buttons
            Rect sortRect = new Rect(270f, controlsY, 320f, 30f);
            DrawSortButtons(sortRect);

            // Action buttons
            Rect actionsRect = new Rect(615f, controlsY, 525f, 30f);
            DrawActionButtons(actionsRect);

            // Put Info Icon on the right side next to gear  Use it to open a help dialog

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
                if (Widgets.ButtonText(new Rect(rect.width - 80f, 5f, 75f, 24f), "RICS.EventsSettings".Translate()))
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
                    Find.WindowStack.Add(new Dialog_EventsEditorHelp());
                }
                TooltipHandler.TipRegion(infoRect, "RICS.EventsEditorHelp.tooltip".Translate());
            }
            else
            {
                // Fallback text button
                if (Widgets.ButtonText(new Rect(rect.width - 110f, 5f, 45f, 24f), "<RICS.EventsEditorHelp".Translate()))
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

            if (Widgets.ButtonText(new Rect(x, 0f, buttonWidth, 30f), "RICS.SortByName".Translate()))
            {
                if (sortMethod == EventSortMethod.Name)
                    sortAscending = !sortAscending;
                else
                    sortMethod = EventSortMethod.Name;
                SortEvents();
            }
            x += buttonWidth + spacing;

            if (Widgets.ButtonText(new Rect(x, 0f, buttonWidth, 30f), "RICS.SortByCost".Translate()))
            {
                if (sortMethod == EventSortMethod.Cost)
                    sortAscending = !sortAscending;
                else
                    sortMethod = EventSortMethod.Cost;
                SortEvents();
            }
            x += buttonWidth + spacing;

            if (Widgets.ButtonText(new Rect(x, 0f, buttonWidth, 30f), "RICS.SortByKarma".Translate()))
            {
                if (sortMethod == EventSortMethod.Karma)
                    sortAscending = !sortAscending;
                else
                    sortMethod = EventSortMethod.Karma;
                SortEvents();
            }

            string sortAscIconPath = sortAscending ? "UI/Buttons/ReorderUp" : "UI/Buttons/ReorderDown";
            Texture2D sortIcon = ContentFinder<Texture2D>.Get(sortAscIconPath, false);

            Rect indicatorRect = new Rect(x + buttonWidth + 5f, 5f, 20f, 20f); // Adjusted position
            if (sortIcon != null)
            {
                Widgets.DrawTextureFitted(indicatorRect, sortIcon, 1f);
                // Show tooltip indicating current sort
                string tooltip = "RICS.SortedByX".Translate(sortMethod.ToString()) + "\n" +
                                 "RICS.DirectionX".Translate((sortAscending ? "RICS.Ascending" : "RICS.Descending").Translate());
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

            float buttonWidth = 100f;
            float spacing = 5f;
            float x = 0f;

            if (Widgets.ButtonText(new Rect(x, 0f, buttonWidth, 30f), "RICS.ResetPrices".Translate()))
            {
                Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                    "RICS.EventsEditor.ResetPricesDialog".Translate(),
                    () => ResetAllPrices()
                ));
            }
            x += buttonWidth + spacing;

            // Enable by Mod Source
            if (Widgets.ButtonText(new Rect(x, 0f, buttonWidth, 30f), "RICS.EnableEvents".Translate()))
            {
                ShowEnableByModSourceMenu();
            }
            x += buttonWidth + spacing;

            // Disable by Mod Source
            if (Widgets.ButtonText(new Rect(x, 0f, buttonWidth, 30f), "RICS.DisableEvents".Translate()))
            {
                ShowDisableByModSourceMenu();
            }
            x += buttonWidth + spacing;

            // Set Cooldowns button (includes reset option in menu)
            if (Widgets.ButtonText(new Rect(x, 0f, buttonWidth, 30f), "RICS.Cooldowns".Translate()))
            {
                ShowCooldownMenu();
            }

            Widgets.EndGroup();
        }

        // Show cooldown menu
        private void ShowCooldownMenu()
        {
            List<FloatMenuOption> options = new List<FloatMenuOption>();

            // Build filter description
            string filterDescription = BuildFilterDescription();

            // Add preset options
            options.Add(new FloatMenuOption("RICS.NoCooldown".Translate(), () => SetBulkCooldown(0, false, filterDescription)));
            options.Add(new FloatMenuOption("RICS.oneDay".Translate(), () => SetBulkCooldown(1, false, filterDescription)));
            options.Add(new FloatMenuOption("RICS.threeDays".Translate(), () => SetBulkCooldown(3, false, filterDescription)));
            options.Add(new FloatMenuOption("RICS.fiveDays".Translate(), () => SetBulkCooldown(5, false, filterDescription)));
            options.Add(new FloatMenuOption("RICS.sevenDays".Translate(), () => SetBulkCooldown(7, false, filterDescription)));
            options.Add(new FloatMenuOption("RICS.fourteenDays".Translate(), () => SetBulkCooldown(14, false, filterDescription)));

            // Reset to defaults section
            if (!string.IsNullOrEmpty(filterDescription))
            {
                options.Add(new FloatMenuOption($"RICS.ResetFilteredToDefaults".Translate(), () =>
                {
                    Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                        "RICS.ResetFilteredToDefaults.Dialog".Translate(filterDescription),
                        () => ApplyResetAllCooldowns(true)
                    ));
                }));
            }

            options.Add(new FloatMenuOption("RICS.ResetAllToDefaults".Translate(), () =>
            {
                Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                    "RICS.ResetAllToDefaults.Dialog".Translate(),
                    () => ApplyResetAllCooldowns(false)
                ));
            }));

            // Custom input option
            options.Add(new FloatMenuOption("RICS.Custom".Translate(), () => OpenCustomCooldownDialog()));

            Find.WindowStack.Add(new FloatMenu(options));
        }

        // Helper method to build filter description
        private string BuildFilterDescription()
        {
            string description = "";

            if (listViewType == EventListViewType.Category && selectedCategory != "All")
            {
                description = "RICS.Filter.Category".Translate(GetDisplayCategoryName(selectedCategory));
            }
            else if (listViewType == EventListViewType.ModSource && selectedModSource != "All")
            {
                description = "RICS.Filter.Mod".Translate(GetDisplayModName(selectedModSource));
            }

            // Check if we have a search query
            if (!string.IsNullOrEmpty(searchQuery))
            {
                if (!string.IsNullOrEmpty(description))
                    description += " + ";
                description += "RICS.Filter.Search".Translate(searchQuery);
            }

            return description;
        }

        // NEW: Apply bulk cooldown to all incidents
        // Future: Add filteredOnly parameter to apply only to filtered events
        private void ApplyBulkCooldown(int days)
        {
            int count = 0;
            foreach (var incident in IncidentsManager.AllBuyableIncidents.Values)
            {
                incident.CooldownDays = days;
                // Also update the buffer if it exists
                string bufferKey = $"Cooldown_{incident.DefName}";
                if (numericBuffers.ContainsKey(bufferKey))
                {
                    numericBuffers[bufferKey] = days.ToString();
                }
                count++;
            }

            IncidentsManager.SaveIncidentsToJson();

            string message = days == 0 ?
                "RICS.Messages.SetNoCooldown".Translate(count) :
                "RICS.Messages.SetCooldown".Translate(count, days, (days == 1 ? "RICS.DaysSingular" : "RICS.DaysPlural").Translate());

            Messages.Message(message, MessageTypeDefOf.TaskCompletion);

            // Refresh the view
            FilterEvents();
        }
        // NEW: Open custom cooldown dialog
        private void OpenCustomCooldownDialog()
        {
            // Build filter description
            string filterDescription = BuildFilterDescription();

            Find.WindowStack.Add(new Dialog_EventSetCustomCooldown((customDays, filteredOnly) =>
            {
                SetBulkCooldown(customDays, filteredOnly, filterDescription);
            }, filterDescription));
        }

        private void DrawContent(Rect rect)
        {
            // Split into list view (left) and events (right)
            float listWidth = 200f;
            float eventsWidth = rect.width - listWidth - 10f;

            Rect listRect = new Rect(rect.x + 5f, rect.y, listWidth - 10f, rect.height);
            Rect eventsRect = new Rect(rect.x + listWidth + 5f, rect.y, eventsWidth - 10f, rect.height);

            // Draw the appropriate list based on view type
            if (listViewType == EventListViewType.Category)
            {
                DrawCategoriesList(listRect);
            }
            else
            {
                DrawModSourcesList(listRect);
            }

            DrawEventsList(eventsRect);
        }

        private void DrawCategoriesList(Rect rect)
        {
            Widgets.DrawMenuSection(rect);

            Rect headerRect = new Rect(rect.x, rect.y, rect.width, 30f);

            // Hidden toggle button over the header
            if (Widgets.ButtonInvisible(headerRect))
            {
                listViewType = EventListViewType.ModSource;
            }

            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.UpperCenter;
            GUI.color = ColorLibrary.SubHeader;
            Widgets.Label(headerRect, "RICS.Categories".Translate().Colorize(ColorLibrary.SubHeader));
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;
            GUI.color = Color.white;

            Rect listRect = new Rect(rect.x + 5f, rect.y + 35f, rect.width - 10f, rect.height - 35f);
            Rect viewRect = new Rect(0f, 0f, listRect.width - 20f, categoryCounts.Count * 30f);

            Widgets.BeginScrollView(listRect, ref categoryScrollPosition, viewRect);
            {
                float y = 0f;
                foreach (var category in categoryCounts.OrderByDescending(kvp => kvp.Value))
                {
                    Rect categoryButtonRect = new Rect(0f, y, listRect.xMax - 21f, 28f);

                    if (selectedCategory == category.Key)
                    {
                        Widgets.DrawHighlightSelected(categoryButtonRect);
                    }
                    else if (Mouse.IsOver(categoryButtonRect))
                    {
                        Widgets.DrawHighlight(categoryButtonRect);
                    }

                    string displayName = category.Key == "All" ? "All" : GetDisplayCategoryName(category.Key);
                    // Calculate max width for the text (button width minus some padding for the count)
                    float textMaxWidth = categoryButtonRect.width - Text.CalcSize($" ({category.Value})").x - 10f;
                    string truncatedName = UIUtilities.TruncateTextToWidth(displayName, textMaxWidth);
                    string label = $"{truncatedName} ({category.Value})";

                    Text.Anchor = TextAnchor.MiddleLeft;
                    if (Widgets.ButtonText(categoryButtonRect, label))
                    {
                        selectedCategory = category.Key;
                        FilterEvents();
                    }
                    Text.Anchor = TextAnchor.UpperLeft;

                    y += 30f;
                }
            }
            Widgets.EndScrollView();
        }

        private void DrawModSourcesList(Rect rect)
        {
            Widgets.DrawMenuSection(rect);

            Rect headerRect = new Rect(rect.x, rect.y, rect.width, 30f);

            // Hidden toggle button over the header
            if (Widgets.ButtonInvisible(headerRect))
            {
                listViewType = EventListViewType.Category;
            }

            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.UpperCenter;
            Widgets.Label(headerRect, "RICS.ModSources".Translate().Colorize(ColorLibrary.SubHeader));
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;

            Rect listRect = new Rect(rect.x + 5f, rect.y + 35f, rect.width - 10f, rect.height - 35f);
            Rect viewRect = new Rect(0f, 0f, listRect.width - 20f, modSourceCounts.Count * 30f);

            Widgets.BeginScrollView(listRect, ref categoryScrollPosition, viewRect);
            {
                float y = 0f;
                foreach (var modSource in modSourceCounts.OrderByDescending(kvp => kvp.Value))
                {
                    Rect sourceButtonRect = new Rect(0f, y, listRect.xMax - 21f, 28f);

                    if (selectedModSource == modSource.Key)
                    {
                        Widgets.DrawHighlightSelected(sourceButtonRect);
                    }
                    else if (Mouse.IsOver(sourceButtonRect))
                    {
                        Widgets.DrawHighlight(sourceButtonRect);
                    }

                    string displayName = modSource.Key == "All" ? "All" : GetDisplayModName(modSource.Key);
                    // Calculate max width for the text (button width minus some padding for the count)
                    float textMaxWidth = sourceButtonRect.width - Text.CalcSize($" ({modSource.Value})").x - 10f;
                    string truncatedName = UIUtilities.TruncateTextToWidth(displayName, textMaxWidth);
                    string label = $"{truncatedName} ({modSource.Value})";

                    Text.Anchor = TextAnchor.MiddleLeft;
                    if (Widgets.ButtonText(sourceButtonRect, label))
                    {
                        selectedModSource = modSource.Key;
                        FilterEvents();
                    }
                    Text.Anchor = TextAnchor.UpperLeft;

                    y += 30f;
                }
            }
            Widgets.EndScrollView();
        }

        private void DrawEventsList(Rect rect)
        {
            Widgets.DrawMenuSection(rect);

            Rect headerRect = new Rect(rect.x, rect.y, rect.width, 30f);
            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.UpperCenter;

            string headerText = "RICS.EventsEditor.HeaderWithCount".Translate(filteredEvents.Count);

            if (listViewType == EventListViewType.Category && selectedCategory != "All")
                headerText += " - " + GetDisplayCategoryName(selectedCategory);
            else if (listViewType == EventListViewType.ModSource && selectedModSource != "All")
                headerText += " - " + GetDisplayModName(selectedModSource);

            Widgets.Label(headerRect, headerText);
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;

            // Early return if no events to display
            if (filteredEvents.Count == 0)
            {
                Rect noEventsRect = new Rect(rect.x, rect.y + 35f, rect.width, rect.height - 35f);
                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(noEventsRect, "RICS.NoEventsFound".Translate());
                Text.Anchor = TextAnchor.UpperLeft;
                return;
            }

            Rect listRect = new Rect(rect.x, rect.y + 35f, rect.width, rect.height - 35f);
            float rowHeight = 70f; // Slightly taller for events

            // Calculate visible indices with proper bounds checking
            int firstVisibleIndex = Mathf.FloorToInt(scrollPosition.y / rowHeight);
            int lastVisibleIndex = Mathf.CeilToInt((scrollPosition.y + listRect.height) / rowHeight);

            // Ensure indices are within valid range
            firstVisibleIndex = Mathf.Clamp(firstVisibleIndex, 0, Mathf.Max(0, filteredEvents.Count - 1));
            lastVisibleIndex = Mathf.Clamp(lastVisibleIndex, 0, Mathf.Max(0, filteredEvents.Count - 1));

            Rect viewRect = new Rect(0f, 0f, listRect.width - 20f, filteredEvents.Count * rowHeight);

            Widgets.BeginScrollView(listRect, ref scrollPosition, viewRect);
            {
                float y = firstVisibleIndex * rowHeight;
                for (int i = firstVisibleIndex; i <= lastVisibleIndex; i++)
                {
                    // Additional safety check
                    if (i < 0 || i >= filteredEvents.Count || filteredEvents[i] == null)
                        continue;

                    Rect eventRect = new Rect(0f, y, viewRect.width, rowHeight - 2f);
                    if (i % 2 == 1)
                    {
                        Widgets.DrawLightHighlight(eventRect);
                    }

                    DrawEventRow(eventRect, filteredEvents[i]);
                    y += rowHeight;
                }
            }
            Widgets.EndScrollView();
        }

        private void DrawEventRow(Rect rect, BuyableIncident incident)
        {
            Widgets.BeginGroup(rect);

            try
            {
                // Adjust row height since we're removing description
                // Left section: Name and meta info (mod + category)
                Rect infoRect = new Rect(5f, 5f, rect.width - 400f, 60f); // Reduced height
                DrawEventInfo(infoRect, incident);

                // Middle section: Enable toggle and event type - moved up to align with cost controls
                Rect toggleRect = new Rect(rect.width - 390f, 10f, 150f, 50f); // Moved up from 20f to 10f
                DrawEventToggle(toggleRect, incident);

                // Right section: Cost and Karma controls
                Rect controlsRect = new Rect(rect.width - 230f, 10f, 225f, 70f); // Moved up from 10f to match
                DrawEventControls(controlsRect, incident);
            }
            finally
            {
                Widgets.EndGroup();
                Text.Anchor = TextAnchor.UpperLeft;
                GUI.color = Color.white;
            }
        }

        private void DrawEventInfo(Rect rect, BuyableIncident incident)
        {
            Widgets.BeginGroup(rect);

            // Event name - color based on availability
            Rect nameRect = new Rect(0f, 0f, rect.width, 28f);
            Text.Font = GameFont.Medium;

            if (!incident.IsAvailableForCommands)
            {
                GUI.color = Color.gray; // Gray out unavailable events
            }

            string displayLabel = incident.Label;
            if (!string.IsNullOrEmpty(displayLabel))
            {
                displayLabel = char.ToUpper(displayLabel[0]) + (displayLabel.Length > 1 ? displayLabel.Substring(1) : "");
            }

            // Make the name clickable to show Def info
            if (Widgets.ButtonInvisible(nameRect))
            {
                ShowDefInfoWindow(incident);
            }

            Widgets.Label(nameRect, displayLabel);
            Text.Font = GameFont.Small;
            GUI.color = Color.white;

            // Mod source and category with availability info
            Rect metaRect = new Rect(0f, 28f, rect.width, 30f);
            string metaInfo = $"{GetDisplayModName(incident.ModSource)} - {GetDisplayCategoryName(incident.CategoryName)}";

            if (!incident.IsAvailableForCommands)
            {
                metaInfo += "RICS.EventInfo.UnavailableSuffix".Translate().Colorize(ColorLibrary.Danger);
                GUI.color = Color.red;
            }
            else
            {
                GUI.color = Color.gray;
            }

            Widgets.Label(metaRect, metaInfo);
            GUI.color = Color.white;

            Widgets.EndGroup();
        }

        private void DrawEventToggle(Rect rect, BuyableIncident incident)
        {
            Widgets.BeginGroup(rect);

            // Enable checkbox - disable for unavailable events
            Rect toggleRect = new Rect(0f, 0f, rect.width, 30f);
            bool enabledCurrent = incident.Enabled;

            if (!incident.IsAvailableForCommands)
            {
                GUI.color = Color.gray;
                Widgets.CheckboxLabeled(toggleRect, "RICS.Enabled".Translate(), ref enabledCurrent);
                GUI.color = Color.white;
                TooltipHandler.TipRegion(toggleRect, "RICS.CannotEnableNotAvailable".Translate());
            }
            else
            {
                Widgets.CheckboxLabeled(toggleRect, "RICS.Enabled".Translate(), ref enabledCurrent);
                if (enabledCurrent != incident.Enabled)
                {
                    incident.Enabled = enabledCurrent;
                    IncidentsManager.SaveIncidentsToJson();
                }
            }

            // Create a horizontal layout for Karma Type and Cooldown
            float y = 30f;
            float sectionHeight = 25f;

            // Karma Type (left side)
            Rect karmaRect = new Rect(0f, y, rect.width * 0.4f, sectionHeight);
            string karmaInfo = TranslateKarmaType(incident.KarmaType);
            GUI.color = GetKarmaTypeColor(incident.KarmaType);
            Widgets.Label(karmaRect, karmaInfo);
            GUI.color = Color.white;

            // Add tooltip for karma type
            TooltipHandler.TipRegion(karmaRect, "RICS.EventToggle.KarmaTooltip".Translate(TranslateKarmaType(incident.KarmaType)));
            // Cooldown Days (right side)
            Rect cooldownRect = new Rect(karmaRect.xMax + 5f, y, rect.width * 0.6f - 5f, sectionHeight);
            DrawCooldownControl(cooldownRect, incident);

            Widgets.EndGroup();
        }

        private void DrawCooldownControl(Rect rect, BuyableIncident incident)
        {
            Widgets.BeginGroup(rect);

            // Label
            Rect labelRect = new Rect(0f, 0f, 45f, rect.height);
            Widgets.Label(labelRect, "RICS.CooldownControl.Label".Translate());

            // Cooldown input field
            Rect inputRect = new Rect(50f, 0f, 50f, rect.height);

            // Create a unique buffer key for this incident's cooldown
            string bufferKey = $"Cooldown_{incident.DefName}";
            if (!numericBuffers.ContainsKey(bufferKey))
            {
                numericBuffers[bufferKey] = incident.CooldownDays.ToString();
            }

            // Check if cooldown is 0 (infinite)
            if (incident.CooldownDays == 0)
            {
                // Draw infinity symbol button
                Rect infinityRect = new Rect(inputRect.x, inputRect.y, 30f, 30f);

                // Try to load infinity icon
                Texture2D infinityIcon = ContentFinder<Texture2D>.Get("UI/Buttons/Infinity", false);
                if (infinityIcon != null)
                {
                    if (Widgets.ButtonImage(infinityRect, infinityIcon))
                    {
                        // Toggle to enable input
                        incident.CooldownDays = 1;
                        numericBuffers[bufferKey] = "1";
                        IncidentsManager.SaveIncidentsToJson();
                    }
                    TooltipHandler.TipRegion(infinityRect, "RICS.NoCooldownInfiniteTooltip".Translate());
                }
                else
                {
                    // Fallback: Draw "∞" text
                    if (Widgets.ButtonText(infinityRect, "∞"))
                    {
                        incident.CooldownDays = 1;
                        numericBuffers[bufferKey] = "1";
                        IncidentsManager.SaveIncidentsToJson();
                    }
                    TooltipHandler.TipRegion(infinityRect, "RICS.NoCooldownInfiniteTooltip".Translate());
                }
            }
            else
            {
                // Use numeric input for non-zero cooldown
                int cooldownBuffer = incident.CooldownDays;
                string _numBufferString = numericBuffers[bufferKey];

                // Use TextFieldNumeric with range limits
                Widgets.TextFieldNumeric(inputRect, ref cooldownBuffer, ref _numBufferString, 0f, 1000f);
                numericBuffers[bufferKey] = _numBufferString;

                if (cooldownBuffer != incident.CooldownDays)
                {
                    incident.CooldownDays = cooldownBuffer;
                    IncidentsManager.SaveIncidentsToJson();
                }

                // Add reset to infinity button
                Rect infinityButtonRect = new Rect(inputRect.xMax + 5f, inputRect.y, 25f, rect.height);
                Texture2D infinityIcon = ContentFinder<Texture2D>.Get("UI/Buttons/Infinity", false);
                if (infinityIcon != null)
                {
                    if (Widgets.ButtonImage(infinityButtonRect, infinityIcon))
                    {
                        incident.CooldownDays = 0;
                        numericBuffers[bufferKey] = "0";
                        IncidentsManager.SaveIncidentsToJson();
                    }
                }
                else
                {
                    if (Widgets.ButtonText(infinityButtonRect, "∞"))
                    {
                        incident.CooldownDays = 0;
                        numericBuffers[bufferKey] = "0";
                        IncidentsManager.SaveIncidentsToJson();
                    }
                }
                TooltipHandler.TipRegion(infinityButtonRect, "RICS.ResetToNoCooldown".Translate());
            }

            // Tooltip for the entire cooldown control
            string cooldownTooltip = "RICS.CooldownDaysTooltip".Translate();
            TooltipHandler.TipRegion(new Rect(labelRect.x, labelRect.y, rect.width, rect.height), cooldownTooltip);

            Widgets.EndGroup();
        }

        private void DrawEventControls(Rect rect, BuyableIncident incident)
        {
            Widgets.BeginGroup(rect);

            float controlHeight = 25f;
            float spacing = 5f;
            float y = 0f;

            // Cost control
            Rect costRect = new Rect(0f, y, rect.width, controlHeight);
            DrawCostControl(costRect, incident);
            y += controlHeight + spacing;

            // NEW: Cooldown control - removed since we moved it to toggle section
            // Karma type control remains
            Rect karmaRect = new Rect(0f, y, rect.width, controlHeight);
            DrawKarmaControl(karmaRect, incident);

            Widgets.EndGroup();
        }

        private void DrawCostControl(Rect rect, BuyableIncident incident)
        {
            Widgets.BeginGroup(rect);

            // Label
            Rect labelRect = new Rect(0f, 0f, 60f, 25f);
            Widgets.Label(labelRect, "Cost".Translate() + ":"); ;

            // Cost input
            Rect inputRect = new Rect(65f, 0f, 80f, 25f);

            // Create a unique buffer key for this incident's cost
            string bufferKey = $"Cost_{incident.DefName}";
            if (!numericBuffers.ContainsKey(bufferKey))
            {
                numericBuffers[bufferKey] = incident.BaseCost.ToString();
            }

            // Use standard Widgets.TextFieldNumeric with the buffer from dictionary
            int costBuffer = incident.BaseCost;
            string _numBufferString = numericBuffers[bufferKey];
            Widgets.TextFieldNumeric(inputRect, ref costBuffer, ref _numBufferString, 0f, 1000000f);
            numericBuffers[bufferKey] = _numBufferString; // Store back the updated buffer

            if (costBuffer != incident.BaseCost)
            {
                incident.BaseCost = costBuffer;
                IncidentsManager.SaveIncidentsToJson();
            }

            // Reset button
            Rect resetRect = new Rect(150f, 0f, 60f, 25f);
            if (Widgets.ButtonText(resetRect, "RICS.Reset".Translate()))
            {
                incident.BaseCost = CalculateDefaultCost(incident);
                // Also update the buffer when resetting
                numericBuffers[bufferKey] = incident.BaseCost.ToString();
                IncidentsManager.SaveIncidentsToJson();
            }

            Widgets.EndGroup();
        }

        private void DrawKarmaControl(Rect rect, BuyableIncident incident)
        {
            Widgets.BeginGroup(rect);

            // Label for Karma
            Rect labelRect = new Rect(0f, 0f, 60f, 25f);
            Widgets.Label(labelRect, "RICS.Karma".Translate() + ":");

            // Karma dropdown
            Rect dropdownRect = new Rect(65f, 0f, 100f, 25f);
            if (Widgets.ButtonText(dropdownRect, TranslateKarmaType(incident.KarmaType)))
            {
                List<FloatMenuOption> options = new List<FloatMenuOption>
                {
                    new FloatMenuOption("RICS.Good".Translate(), () => UpdateKarmaType(incident, "Good")),
                    new FloatMenuOption("RICS.Bad".Translate(), () => UpdateKarmaType(incident, "Bad")),
                    new FloatMenuOption("RICS.Neutral".Translate(), () => UpdateKarmaType(incident, "Neutral"))
                };

                Find.WindowStack.Add(new FloatMenu(options));
            }

            Widgets.EndGroup();
        }

        private void UpdateKarmaType(BuyableIncident incident, string karmaType)
        {
            incident.KarmaType = karmaType;
            IncidentsManager.SaveIncidentsToJson();
        }

        private Color GetKarmaTypeColor(string karmaType)
        {
            return karmaType?.ToLower() switch
            {
                "good" => Color.green,
                "bad" => Color.red,
                _ => Color.yellow
            };
        }

        // Bulk operations (similar to weather editor)
        private void ApplyResetAllCooldowns(bool filteredOnly = false)
        {
            int count = 0;

            if (filteredOnly)
            {
                // Reset only filtered events
                foreach (var incident in filteredEvents)
                {
                    if (incident == null) continue;

                    int defaultCooldown = CalculateDefaultCooldown(incident);
                    incident.CooldownDays = defaultCooldown;

                    // Update buffer
                    string bufferKey = $"Cooldown_{incident.DefName}";
                    if (numericBuffers.ContainsKey(bufferKey))
                    {
                        numericBuffers[bufferKey] = defaultCooldown.ToString();
                    }
                    count++;
                }
            }
            else
            {
                // Reset all events
                foreach (var incident in IncidentsManager.AllBuyableIncidents.Values)
                {
                    int defaultCooldown = CalculateDefaultCooldown(incident);
                    incident.CooldownDays = defaultCooldown;

                    // Update buffer
                    string bufferKey = $"Cooldown_{incident.DefName}";
                    if (numericBuffers.ContainsKey(bufferKey))
                    {
                        numericBuffers[bufferKey] = defaultCooldown.ToString();
                    }
                    count++;
                }
            }

            IncidentsManager.SaveIncidentsToJson();
            Messages.Message("RICS.Messages.ResetCooldowns".Translate(count), MessageTypeDefOf.TaskCompletion);
            FilterEvents();
        }
        private void SetBulkCooldown(int days, bool filteredOnly = false, string filterDescription = "")
        {
            string targetDescription = filteredOnly ?
                $"filtered events ({filterDescription})" :
                "ALL events";

            string confirmMessage = days == 0 ?
                "RICS.SetNoCooldownConfirm".Translate(targetDescription) :
                "RICS.SetCooldownConfirm".Translate(targetDescription, days);

            Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                confirmMessage,
                () => ApplyBulkCooldown(days, filteredOnly)
            ));
        }
        private void ApplyBulkCooldown(int days, bool filteredOnly = false)
        {
            int count = 0;

            if (filteredOnly)
            {
                // Apply only to currently filtered events
                foreach (var incident in filteredEvents)
                {
                    if (incident == null) continue;

                    incident.CooldownDays = days;
                    // Also update the buffer if it exists
                    string bufferKey = $"Cooldown_{incident.DefName}";
                    if (numericBuffers.ContainsKey(bufferKey))
                    {
                        numericBuffers[bufferKey] = days.ToString();
                    }
                    count++;
                }
            }
            else
            {
                // Apply to all events
                foreach (var incident in IncidentsManager.AllBuyableIncidents.Values)
                {
                    incident.CooldownDays = days;
                    // Also update the buffer if it exists
                    string bufferKey = $"Cooldown_{incident.DefName}";
                    if (numericBuffers.ContainsKey(bufferKey))
                    {
                        numericBuffers[bufferKey] = days.ToString();
                    }
                    count++;
                }
            }

            IncidentsManager.SaveIncidentsToJson();

            string message = days == 0 ?
                $"Set {count} events to have no cooldown" :
                $"Set {count} events to {days} day{(days == 1 ? "" : "s")} cooldown";

            Messages.Message(message, MessageTypeDefOf.TaskCompletion);

            // Refresh the view
            FilterEvents();
        }

        // Calculate default cooldown based on incident type (similar to your BuyableIncident logic)
        private int CalculateDefaultCooldown(BuyableIncident incident)
        {
            // This should match the logic in BuyableIncident.SetDefaultCooldown
            // Since we don't have that method yet, let's create a simple default

            // You can adjust these defaults based on your preferences
            if (incident.IsRaidIncident)
                return 7;
            else if (incident.IsDiseaseIncident)
                return 5;
            else if (incident.IsWeatherIncident)
                return 3;
            else if (incident.IsQuestIncident)
                return 10;
            else
                return 1;
        }

        private void ShowEnableByModSourceMenu()
        {
            List<FloatMenuOption> options = new List<FloatMenuOption>();

            options.Add(new FloatMenuOption("RICS.FloatingMenu.AllMods".Translate(), EnableAllEvents));

            foreach (var modSource in modSourceCounts.Keys.Where(k => k != "All").OrderBy(k => k))
            {
                string displayName = GetDisplayModName(modSource);
                options.Add(new FloatMenuOption(displayName, () => EnableEventsByModSource(modSource)));
            }

            Find.WindowStack.Add(new FloatMenu(options));
        }

        private void ShowDisableByModSourceMenu()
        {
            List<FloatMenuOption> options = new List<FloatMenuOption>();

            options.Add(new FloatMenuOption("RICS.FloatingMenu.AllMods".Translate(), DisableAllEvents));

            foreach (var modSource in modSourceCounts.Keys.Where(k => k != "All").OrderBy(k => k))
            {
                string displayName = GetDisplayModName(modSource);
                options.Add(new FloatMenuOption(displayName, () => DisableEventsByModSource(modSource)));
            }

            Find.WindowStack.Add(new FloatMenu(options));
        }

        private void EnableEventsByModSource(string modSource)
        {
            int count = 0;
            foreach (var incident in IncidentsManager.AllBuyableIncidents.Values)
            {
                if (GetDisplayModName(incident.ModSource) == modSource)
                {
                    incident.Enabled = true;
                    count++;
                }
            }

            IncidentsManager.SaveIncidentsToJson();
            FilterEvents();
            Messages.Message("RICS.Messages.EventsEnabledFromMod".Translate(count, GetDisplayModName(modSource)), MessageTypeDefOf.TaskCompletion);
        }

        private void DisableEventsByModSource(string modSource)
        {
            int count = 0;
            foreach (var incident in IncidentsManager.AllBuyableIncidents.Values)
            {
                if (GetDisplayModName(incident.ModSource) == modSource)
                {
                    incident.Enabled = false;
                    count++;
                }
            }

            IncidentsManager.SaveIncidentsToJson();
            FilterEvents();
            Messages.Message("RICS.Messages.EventsDisabledFromMod".Translate(count, GetDisplayModName(modSource)), MessageTypeDefOf.TaskCompletion);
        }

        private void EnableAllEvents()
        {
            foreach (var incident in IncidentsManager.AllBuyableIncidents.Values)
            {
                incident.Enabled = true;
            }
            IncidentsManager.SaveIncidentsToJson();
            FilterEvents();
        }

        private void DisableAllEvents()
        {
            foreach (var incident in IncidentsManager.AllBuyableIncidents.Values)
            {
                incident.Enabled = false;
            }
            IncidentsManager.SaveIncidentsToJson();
            FilterEvents();
        }

        private int CalculateDefaultCost(BuyableIncident incident)
        {
            // Use the same logic as in BuyableIncident.SetDefaultPricing
            var incidentDef = DefDatabase<IncidentDef>.GetNamedSilentFail(incident.DefName);
            if (incidentDef != null)
            {
                var tempIncident = new BuyableIncident(incidentDef);
                return tempIncident.BaseCost;
            }
            return 500; // Fallback
        }

        private void BuildModSourceCounts()
        {
            modSourceCounts.Clear();
            modSourceCounts["All"] = IncidentsManager.AllBuyableIncidents.Count;

            foreach (var incident in IncidentsManager.AllBuyableIncidents.Values)
            {
                string displayModSource = GetDisplayModName(incident.ModSource);
                if (modSourceCounts.ContainsKey(displayModSource))
                    modSourceCounts[displayModSource]++;
                else
                    modSourceCounts[displayModSource] = 1;
            }
        }

        private void BuildCategoryCounts()
        {
            categoryCounts.Clear();
            categoryCounts["All"] = IncidentsManager.AllBuyableIncidents.Count;

            foreach (var incident in IncidentsManager.AllBuyableIncidents.Values)
            {
                string displayCategory = GetDisplayCategoryName(incident.CategoryName);
                if (categoryCounts.ContainsKey(displayCategory))
                    categoryCounts[displayCategory]++;
                else
                    categoryCounts[displayCategory] = 1;
            }
        }

        private string GetDisplayModName(string modSource)
        {
            if (modSource == "Core") return "RimWorld";
            if (modSource.Contains(".")) return modSource.Split('.')[0];
            return modSource;
        }

        private string GetDisplayCategoryName(string categoryName)
        {
            if (string.IsNullOrEmpty(categoryName)) return "RICS.Categories.Uncategorized".Translate();
            return categoryName;
        }

        private void FilterEvents()
        {
            lastSearch = searchQuery;
            filteredEvents.Clear();

            var allEvents = IncidentsManager.AllBuyableIncidents.Values.AsEnumerable();

            // Add null check to filter out null incidents
            allEvents = allEvents.Where(incident => incident != null);

            // Apply category/mod source filter based on view type
            if (listViewType == EventListViewType.Category && selectedCategory != "All")
            {
                allEvents = allEvents.Where(incident => GetDisplayCategoryName(incident.CategoryName) == selectedCategory);
            }
            else if (listViewType == EventListViewType.ModSource && selectedModSource != "All")
            {
                allEvents = allEvents.Where(incident => GetDisplayModName(incident.ModSource) == selectedModSource);
            }

            // Filter by search query with null-safe property access
            if (!string.IsNullOrEmpty(searchQuery))
            {
                string searchLower = searchQuery.ToLower();
                allEvents = allEvents.Where(incident =>
                    (incident.Label?.ToLower().Contains(searchLower) ?? false) ||
                    (incident.Description?.ToLower().Contains(searchLower) ?? false) ||
                    (incident.DefName?.ToLower().Contains(searchLower) ?? false) ||
                    (incident.ModSource?.ToLower().Contains(searchLower) ?? false) ||
                    (incident.CategoryName?.ToLower().Contains(searchLower) ?? false)
                );
            }

            // Filter out unavailable events if setting is disabled
            var settings = CAPChatInteractiveMod.Instance.Settings.GlobalSettings;
            if (!settings.ShowUnavailableEvents)
            {
                allEvents = allEvents.Where(incident => incident.IsAvailableForCommands);
            }

            filteredEvents = allEvents.ToList();
            SortEvents();
        }

        private void SortEvents()
        {
            switch (sortMethod)
            {
                case EventSortMethod.Name:
                    filteredEvents = sortAscending ?
                        filteredEvents.OrderBy(incident => incident?.Label ?? "").ToList() :
                        filteredEvents.OrderByDescending(incident => incident?.Label ?? "").ToList();
                    break;
                case EventSortMethod.Cost:
                    filteredEvents = sortAscending ?
                        filteredEvents.OrderBy(incident => incident?.BaseCost ?? 0).ToList() :
                        filteredEvents.OrderByDescending(incident => incident?.BaseCost ?? 0).ToList();
                    break;
                case EventSortMethod.Karma:
                    filteredEvents = sortAscending ?
                        filteredEvents.OrderBy(incident => incident?.KarmaType ?? "").ThenBy(incident => incident?.Label ?? "").ToList() :
                        filteredEvents.OrderByDescending(incident => incident?.KarmaType ?? "").ThenBy(incident => incident?.Label ?? "").ToList();
                    break;
            }
        }
        private void SaveOriginalSettings()
        {
            originalSettings.Clear();
            foreach (var incident in IncidentsManager.AllBuyableIncidents.Values)
            {
                originalSettings[incident.DefName] = (incident.BaseCost, incident.KarmaType);
            }
        }
        private void ResetAllPrices()
        {
            foreach (var incident in IncidentsManager.AllBuyableIncidents.Values)
            {
                incident.BaseCost = CalculateDefaultCost(incident);
            }
            IncidentsManager.SaveIncidentsToJson();
            FilterEvents();
        }
        public override void PostClose()
        {
            IncidentsManager.SaveIncidentsToJsonPostClose();
            base.PostClose();
        }

        private void ShowDefInfoWindow(BuyableIncident incident)
        {
            Find.WindowStack.Add(new EventsDefInfoWindow(incident));
        }

        private string TranslateKarmaType(string karmaType)
        {
            return karmaType?.ToLower() switch
            {
                "good" => "RICS.Good".Translate(),
                "bad" => "RICS.Bad".Translate(),
                _ => "RICS.Neutral".Translate() // default to Neutral for null or any other value
            };
        }
    }

    public enum EventSortMethod
    {
        Name,
        Cost,
        Karma
    }
    public enum EventListViewType
    {
        ModSource,
        Category
    }

}