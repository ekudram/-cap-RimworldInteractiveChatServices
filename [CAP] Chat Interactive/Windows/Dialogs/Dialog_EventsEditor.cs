// Dialog_EventsEditor.cs
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using CAP_ChatInteractive.Incidents;
using System;
using System.Text;

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
        private List<BuyableIncident> filteredEvents = new List<BuyableIncident>();
        private Dictionary<string, (int baseCost, string karmaType)> originalSettings = new Dictionary<string, (int, string)>();
        private EventListViewType listViewType = EventListViewType.Category;

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
            GUI.color = ColorLibrary.Orange;
            Rect titleRect = new Rect(0f, 0f, 200f, 30f);
            Widgets.Label(titleRect, "Events Editor");

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
                Widgets.Label(new Rect(0f, searchY, 40f, 30f), "Search:");
            }

            Rect searchRect = new Rect(30f, searchY, 170f, 24f); // Adjusted position for icon
            searchQuery = Widgets.TextField(searchRect, searchQuery);

            // Sort buttons
            Rect sortRect = new Rect(270f, controlsY, 300f, 30f);
            DrawSortButtons(sortRect);

            // Action buttons
            Rect actionsRect = new Rect(575f, controlsY, 400f, 30f);
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
                if (sortMethod == EventSortMethod.Name)
                    sortAscending = !sortAscending;
                else
                    sortMethod = EventSortMethod.Name;
                SortEvents();
            }
            x += buttonWidth + spacing;

            if (Widgets.ButtonText(new Rect(x, 0f, buttonWidth, 30f), "Cost"))
            {
                if (sortMethod == EventSortMethod.Cost)
                    sortAscending = !sortAscending;
                else
                    sortMethod = EventSortMethod.Cost;
                SortEvents();
            }
            x += buttonWidth + spacing;

            if (Widgets.ButtonText(new Rect(x, 0f, buttonWidth, 30f), "Karma"))
            {
                if (sortMethod == EventSortMethod.Karma)
                    sortAscending = !sortAscending;
                else
                    sortMethod = EventSortMethod.Karma;
                SortEvents();
            }

            string sortIndicator = sortAscending ? " ↑" : " ↓";
            Rect indicatorRect = new Rect(x + buttonWidth + 10f, 8f, 50f, 20f);
            Widgets.Label(indicatorRect, sortIndicator);

            Widgets.EndGroup();
        }

        private void DrawActionButtons(Rect rect)
        {
            Widgets.BeginGroup(rect);

            float buttonWidth = 100f;
            float spacing = 5f;
            float x = 0f;

            if (Widgets.ButtonText(new Rect(x, 0f, buttonWidth, 30f), "Reset Prices"))
            {
                Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                    "Reset all event prices to default? This cannot be undone.",
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
            Widgets.Label(headerRect, "Categories");
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;

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
                    string label = $"{displayName} ({category.Value})";

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
                    string label = $"{displayName} ({modSource.Value})";

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

            string headerText = $"Events ({filteredEvents.Count})";
            if (listViewType == EventListViewType.Category && selectedCategory != "All")
                headerText += $" - {GetDisplayCategoryName(selectedCategory)}";
            else if (listViewType == EventListViewType.ModSource && selectedModSource != "All")
                headerText += $" - {GetDisplayModName(selectedModSource)}";

            Widgets.Label(headerRect, headerText);
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;

            Rect listRect = new Rect(rect.x, rect.y + 35f, rect.width, rect.height - 35f);
            float rowHeight = 70f; // Slightly taller for events

            int firstVisibleIndex = Mathf.FloorToInt(scrollPosition.y / rowHeight);
            int lastVisibleIndex = Mathf.CeilToInt((scrollPosition.y + listRect.height) / rowHeight);
            firstVisibleIndex = Mathf.Clamp(firstVisibleIndex, 0, filteredEvents.Count - 1);
            lastVisibleIndex = Mathf.Clamp(lastVisibleIndex, 0, filteredEvents.Count - 1);

            Rect viewRect = new Rect(0f, 0f, listRect.width - 20f, filteredEvents.Count * rowHeight);

            Widgets.BeginScrollView(listRect, ref scrollPosition, viewRect);
            {
                float y = firstVisibleIndex * rowHeight;
                for (int i = firstVisibleIndex; i <= lastVisibleIndex; i++)
                {
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
                metaInfo += " - [UNAVAILABLE]";
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
                Widgets.CheckboxLabeled(toggleRect, "Enabled", ref enabledCurrent);
                GUI.color = Color.white;
                TooltipHandler.TipRegion(toggleRect, "Cannot enable - not available via commands");
            }
            else
            {
                Widgets.CheckboxLabeled(toggleRect, "Enabled", ref enabledCurrent);
                if (enabledCurrent != incident.Enabled)
                {
                    incident.Enabled = enabledCurrent;
                    IncidentsManager.SaveIncidentsToJson();
                }
            }

            // Event type indicator - moved up and adjusted spacing
            Rect typeRect = new Rect(0f, 30f, rect.width, 25f); // Increased height from 20f to 25f
            string typeInfo = $"Type: {incident.KarmaType}";
            GUI.color = GetKarmaTypeColor(incident.KarmaType);
            Widgets.Label(typeRect, typeInfo);
            GUI.color = Color.white;

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

            // Karma type control
            Rect karmaRect = new Rect(0f, y, rect.width, controlHeight);
            DrawKarmaControl(karmaRect, incident);

            Widgets.EndGroup();
        }

        private void DrawCostControl(Rect rect, BuyableIncident incident)
        {
            Widgets.BeginGroup(rect);

            // Label
            Rect labelRect = new Rect(0f, 0f, 60f, 25f);
            Widgets.Label(labelRect, "Cost:");

            // Cost input
            Rect inputRect = new Rect(65f, 0f, 80f, 25f);
            int costBuffer = incident.BaseCost;
            string stringBuffer = costBuffer.ToString();
            Widgets.TextFieldNumeric(inputRect, ref costBuffer, ref stringBuffer, 0, 1000000);

            if (costBuffer != incident.BaseCost)
            {
                incident.BaseCost = costBuffer;
                IncidentsManager.SaveIncidentsToJson();
            }

            // Reset button
            Rect resetRect = new Rect(150f, 0f, 60f, 25f);
            if (Widgets.ButtonText(resetRect, "Reset"))
            {
                incident.BaseCost = CalculateDefaultCost(incident);
                IncidentsManager.SaveIncidentsToJson();
            }

            Widgets.EndGroup();
        }

        private void DrawKarmaControl(Rect rect, BuyableIncident incident)
        {
            Widgets.BeginGroup(rect);

            // Label for Karma
            Rect labelRect = new Rect(0f, 0f, 60f, 25f);
            Widgets.Label(labelRect, "Karma:");

            // Karma dropdown
            Rect dropdownRect = new Rect(65f, 0f, 100f, 25f);
            if (Widgets.ButtonText(dropdownRect, incident.KarmaType))
            {
                List<FloatMenuOption> options = new List<FloatMenuOption>
                {
                    new FloatMenuOption("Good", () => UpdateKarmaType(incident, "Good")),
                    new FloatMenuOption("Bad", () => UpdateKarmaType(incident, "Bad")),
                    new FloatMenuOption("Neutral", () => UpdateKarmaType(incident, "Neutral"))
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
        private void ShowEnableByModSourceMenu()
        {
            List<FloatMenuOption> options = new List<FloatMenuOption>();

            options.Add(new FloatMenuOption("All Mods", EnableAllEvents));

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

            options.Add(new FloatMenuOption("All Mods", DisableAllEvents));

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
            Messages.Message($"Enabled {count} events from {GetDisplayModName(modSource)}", MessageTypeDefOf.TaskCompletion);
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
            Messages.Message($"Disabled {count} events from {GetDisplayModName(modSource)}", MessageTypeDefOf.TaskCompletion);
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
            if (string.IsNullOrEmpty(categoryName)) return "Uncategorized";
            return categoryName;
        }

        private void FilterEvents()
        {
            lastSearch = searchQuery;
            filteredEvents.Clear();

            var allEvents = IncidentsManager.AllBuyableIncidents.Values.AsEnumerable();

            // Apply category/mod source filter based on view type
            if (listViewType == EventListViewType.Category && selectedCategory != "All")
            {
                allEvents = allEvents.Where(incident => GetDisplayCategoryName(incident.CategoryName) == selectedCategory);
            }
            else if (listViewType == EventListViewType.ModSource && selectedModSource != "All")
            {
                allEvents = allEvents.Where(incident => GetDisplayModName(incident.ModSource) == selectedModSource);
            }

            // Filter by search query
            if (!string.IsNullOrEmpty(searchQuery))
            {
                string searchLower = searchQuery.ToLower();
                allEvents = allEvents.Where(incident =>
                    incident.Label.ToLower().Contains(searchLower) ||
                    incident.Description.ToLower().Contains(searchLower) ||
                    incident.DefName.ToLower().Contains(searchLower) ||
                    incident.ModSource.ToLower().Contains(searchLower) ||
                    incident.CategoryName.ToLower().Contains(searchLower)
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
                        filteredEvents.OrderBy(incident => incident.Label).ToList() :
                        filteredEvents.OrderByDescending(incident => incident.Label).ToList();
                    break;
                case EventSortMethod.Cost:
                    filteredEvents = sortAscending ?
                        filteredEvents.OrderBy(incident => incident.BaseCost).ToList() :
                        filteredEvents.OrderByDescending(incident => incident.BaseCost).ToList();
                    break;
                case EventSortMethod.Karma:
                    filteredEvents = sortAscending ?
                        filteredEvents.OrderBy(incident => incident.KarmaType).ThenBy(incident => incident.Label).ToList() :
                        filteredEvents.OrderByDescending(incident => incident.KarmaType).ThenBy(incident => incident.Label).ToList();
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
            IncidentsManager.SaveIncidentsToJson();
            base.PostClose();
        }

        private void ShowDefInfoWindow(BuyableIncident incident)
        {
            Find.WindowStack.Add(new EventsDefInfoWindow(incident));
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
        Category,
        ModSource
    }

    public class EventsDefInfoWindow : Window
    {
        private BuyableIncident incident;
        private Vector2 scrollPosition = Vector2.zero;

        public override Vector2 InitialSize => new Vector2(600f, 700f);

        public EventsDefInfoWindow(BuyableIncident incident)
        {
            this.incident = incident;
            doCloseButton = true;
            forcePause = true;
            absorbInputAroundWindow = true;
            resizeable = true;
        }

        public override void DoWindowContents(Rect inRect)
        {
            // Title
            Rect titleRect = new Rect(0f, 0f, inRect.width, 35f);
            Text.Font = GameFont.Medium;
            Widgets.Label(titleRect, $"Incident Information: {incident.Label}");
            Text.Font = GameFont.Small;

            // Content area
            Rect contentRect = new Rect(0f, 40f, inRect.width, inRect.height - 40f - CloseButSize.y);
            DrawIncidentInfo(contentRect);
        }

        private void DrawIncidentInfo(Rect rect)
        {
            StringBuilder sb = new StringBuilder();

            // Always show at top
            sb.AppendLine($"DefName: {incident.DefName}");
            sb.AppendLine($"Label: {incident.Label}");
            sb.AppendLine($"Mod Source: {GetDisplayModName(incident.ModSource)}");
            sb.AppendLine($"Category: {GetDisplayCategoryName(incident.CategoryName)}");
            sb.AppendLine($"");

            // BuyableIncident properties
            sb.AppendLine($"--- Buyable Incident Properties ---");
            sb.AppendLine($"Base Cost: {incident.BaseCost}");
            sb.AppendLine($"Karma Type: {incident.KarmaType}");
            sb.AppendLine($"Enabled: {incident.Enabled}");
            sb.AppendLine($"Event Cap: {incident.EventCap}");
            sb.AppendLine($"Is Available For Commands: {incident.IsAvailableForCommands}");
            sb.AppendLine($"Should Be In Store: {incident.ShouldBeInStore}");
            sb.AppendLine($"Worker Class: {incident.WorkerClassName}");
            sb.AppendLine($"");

            // Incident type analysis
            sb.AppendLine($"--- Incident Type Analysis ---");
            sb.AppendLine($"Is Weather Incident: {incident.IsWeatherIncident}");
            sb.AppendLine($"Is Raid Incident: {incident.IsRaidIncident}");
            sb.AppendLine($"Is Disease Incident: {incident.IsDiseaseIncident}");
            sb.AppendLine($"Is Quest Incident: {incident.IsQuestIncident}");
            sb.AppendLine($"Points Scaleable: {incident.PointsScaleable}");
            sb.AppendLine($"Base Chance: {incident.BaseChance}");
            sb.AppendLine($"Min Threat Points: {incident.MinThreatPoints}");
            sb.AppendLine($"Max Threat Points: {incident.MaxThreatPoints}");
            sb.AppendLine($"");

            // Get the actual IncidentDef for more detailed information
            IncidentDef incidentDef = DefDatabase<IncidentDef>.GetNamedSilentFail(incident.DefName);
            if (incidentDef != null)
            {
                sb.AppendLine($"--- IncidentDef Properties ---");
                sb.AppendLine($"Category: {incidentDef.category?.defName ?? "null"}");
                sb.AppendLine($"Category Label: {incidentDef.category?.LabelCap ?? "null"}");
                sb.AppendLine($"Base Chance: {incidentDef.baseChance}");
                sb.AppendLine($"Base Chance With Royalty: {incidentDef.baseChanceWithRoyalty}");
                sb.AppendLine($"Earliest Day: {incidentDef.earliestDay}");
                sb.AppendLine($"Min Population: {incidentDef.minPopulation}");
                sb.AppendLine($"Points Scaleable: {incidentDef.pointsScaleable}");
                sb.AppendLine($"Min Threat Points: {incidentDef.minThreatPoints}");
                sb.AppendLine($"Max Threat Points: {incidentDef.maxThreatPoints}");
                sb.AppendLine($"Min Refire Days: {incidentDef.minRefireDays}");
                sb.AppendLine($"Hidden: {incidentDef.hidden}");
                sb.AppendLine($"Is Anomaly Incident: {incidentDef.IsAnomalyIncident}");

                // Target tags
                if (incidentDef.targetTags != null && incidentDef.targetTags.Count > 0)
                {
                    sb.AppendLine($"Target Tags: {string.Join(", ", incidentDef.targetTags.Select(t => t.defName))}");
                }

                // Letter information with karma analysis
                if (incidentDef.letterDef != null)
                {
                    sb.AppendLine($"Letter Type: {incidentDef.letterDef.defName}");
                    string letterDefName = incidentDef.letterDef.defName.ToLower();
                    if (letterDefName.Contains("positive") || letterDefName.Contains("good"))
                        sb.AppendLine($"Letter Karma: Good (based on letter type)");
                    else if (letterDefName.Contains("negative") || letterDefName.Contains("bad") || letterDefName.Contains("threat"))
                        sb.AppendLine($"Letter Karma: Bad (based on letter type)");
                    else
                        sb.AppendLine($"Letter Karma: Neutral (based on letter type)");
                }

                // Game condition if applicable
                if (incidentDef.gameCondition != null)
                {
                    sb.AppendLine($"Game Condition: {incidentDef.gameCondition.defName}");
                    sb.AppendLine($"Duration Days: {incidentDef.durationDays}");
                }

                // Disease incident if applicable
                if (incidentDef.diseaseIncident != null)
                {
                    sb.AppendLine($"Disease: {incidentDef.diseaseIncident.defName}");
                    sb.AppendLine($"Disease Max Victims: {incidentDef.diseaseMaxVictims}");
                }

                // Quest incident if applicable
                if (incidentDef.questScriptDef != null)
                {
                    sb.AppendLine($"--- Quest Information ---");
                    sb.AppendLine($"Quest Script: {incidentDef.questScriptDef.defName}");
                    sb.AppendLine($"Auto Accept: {incidentDef.questScriptDef.autoAccept}");
                    sb.AppendLine($"Randomly Selectable: {incidentDef.questScriptDef.randomlySelectable}");
                    sb.AppendLine($"Root Min Points: {incidentDef.questScriptDef.rootMinPoints}");
                    sb.AppendLine($"Root Earliest Day: {incidentDef.questScriptDef.rootEarliestDay}");
                }
            }
            else
            {
                sb.AppendLine($"--- IncidentDef Not Found ---");
                sb.AppendLine($"Could not find IncidentDef with name: {incident.DefName}");
            }

            string fullText = sb.ToString();

            // Calculate text height
            float textHeight = Text.CalcHeight(fullText, rect.width - 20f);
            Rect viewRect = new Rect(0f, 0f, rect.width - 20f, textHeight);

            // Scroll view
            Widgets.BeginScrollView(rect, ref scrollPosition, viewRect);
            Widgets.Label(new Rect(0f, 0f, viewRect.width, textHeight), fullText);
            Widgets.EndScrollView();
        }

        // Helper methods to match the main dialog
        private string GetDisplayModName(string modSource)
        {
            if (modSource == "Core") return "RimWorld";
            if (modSource.Contains(".")) return modSource.Split('.')[0];
            return modSource;
        }

        private string GetDisplayCategoryName(string categoryName)
        {
            if (string.IsNullOrEmpty(categoryName)) return "Uncategorized";
            return categoryName;
        }
    }

    public class Dialog_EventSettings : Window
{
    private CAPGlobalChatSettings settings;
    private Vector2 scrollPosition = Vector2.zero;

    public override Vector2 InitialSize => new Vector2(500f, 600f);

    public Dialog_EventSettings()
    {
        settings = CAPChatInteractiveMod.Instance.Settings.GlobalSettings;

        doCloseButton = true;
        forcePause = true;
        absorbInputAroundWindow = true;
        resizeable = true;
    }

    public override void DoWindowContents(Rect inRect)
    {
        // Title
        Rect titleRect = new Rect(0f, 0f, inRect.width, 35f);
        Text.Font = GameFont.Medium;
        Widgets.Label(titleRect, "Event Settings");
        Text.Font = GameFont.Small;

        // Content area
        Rect contentRect = new Rect(0f, 40f, inRect.width, inRect.height - 40f - CloseButSize.y);
        DrawSettings(contentRect);
    }

    private void DrawSettings(Rect rect)
    {
        Rect viewRect = new Rect(0f, 0f, rect.width - 20f, 800f); // Enough height for all content

        Widgets.BeginScrollView(rect, ref scrollPosition, viewRect);
        Listing_Standard listing = new Listing_Standard();
        listing.Begin(viewRect);

        // Event Statistics
        DrawEventStatistics(listing);

        listing.Gap(12f);

        // Display Settings
        DrawDisplaySettings(listing);

        listing.Gap(12f);

        // Cooldown Settings
        DrawCooldownSettings(listing);

        listing.End();
        Widgets.EndScrollView();
    }

    private void DrawEventStatistics(Listing_Standard listing)
    {
        Text.Font = GameFont.Medium;
        listing.Label("Event Statistics");
        Text.Font = GameFont.Small;
        listing.GapLine(6f);

        int totalEvents = IncidentsManager.AllBuyableIncidents?.Count ?? 0;
        int enabledEvents = IncidentsManager.AllBuyableIncidents?.Values.Count(e => e.Enabled) ?? 0;
        int availableEvents = IncidentsManager.AllBuyableIncidents?.Values.Count(e => e.IsAvailableForCommands) ?? 0;

        listing.Label($"Total Events: {totalEvents}");
        listing.Label($"Enabled Events: {enabledEvents}");
        listing.Label($"Available for Commands: {availableEvents}");
        listing.Label($"Unavailable Events: {totalEvents - availableEvents}");

        // Breakdown by karma type
        if (IncidentsManager.AllBuyableIncidents != null)
        {
            var karmaGroups = IncidentsManager.AllBuyableIncidents.Values
                .Where(e => e.IsAvailableForCommands)
                .GroupBy(e => e.KarmaType)
                .OrderByDescending(g => g.Count());

            listing.Gap(4f);
            Text.Font = GameFont.Tiny;
            listing.Label("Available events by karma type:");
            foreach (var group in karmaGroups)
            {
                listing.Label($"  • {group.Key}: {group.Count()} events");
            }
            Text.Font = GameFont.Small;
        }
    }

    private void DrawDisplaySettings(Listing_Standard listing)
    {
        Text.Font = GameFont.Medium;
        listing.Label("Display Settings");
        Text.Font = GameFont.Small;
        listing.GapLine(6f);

        // Show unavailable events setting
        bool showUnavailable = settings.GetType().GetField("ShowUnavailableEvents")?.GetValue(settings) as bool? ?? true;
        bool newShowUnavailable = showUnavailable;
        
        listing.CheckboxLabeled("Show unavailable events", ref newShowUnavailable,
            "When enabled, events that are not available via commands will still be visible in the editor");

        if (newShowUnavailable != showUnavailable)
        {
            var field = settings.GetType().GetField("ShowUnavailableEvents");
            if (field != null)
            {
                field.SetValue(settings, newShowUnavailable);
            }
            else
            {
                // If the field doesn't exist, we need to add it to the settings class
                Logger.Warning("ShowUnavailableEvents field not found in settings");
            }
        }

        listing.Gap(4f);
        Text.Font = GameFont.Tiny;
        listing.Label("Unavailable events are grayed out and cannot be enabled");
        Text.Font = GameFont.Small;
    }

    private void DrawCooldownSettings(Listing_Standard listing)
    {
        Text.Font = GameFont.Medium;
        listing.Label("Cooldown Settings");
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
            NumericField(listing, "Max bad event purchases:", ref settings.MaxBadEvents, 1, 20);
            NumericField(listing, "Max good event purchases:", ref settings.MaxGoodEvents, 1, 20);
            NumericField(listing, "Max neutral event purchases:", ref settings.MaxNeutralEvents, 1, 20);
        }

        listing.Gap(12f);

        // Store purchase limits
        NumericField(listing, "Max item purchases per day:", ref settings.MaxItemPurchases, 1, 50);
        Text.Font = GameFont.Tiny;
        listing.Label($"Viewers can purchase up to {settings.MaxItemPurchases} items per game day before cooldown");
        Text.Font = GameFont.Small;
    }

    private void NumericField(Listing_Standard listing, string label, ref int value, int min, int max)
    {
        Rect rect = listing.GetRect(30f);
        Rect leftRect = rect.LeftHalf().Rounded();
        Rect rightRect = rect.RightHalf().Rounded();

        Widgets.Label(leftRect, label);
        string buffer = value.ToString();
        Widgets.TextFieldNumeric(rightRect, ref value, ref buffer, min, max);
        listing.Gap(2f);
    }
}
}