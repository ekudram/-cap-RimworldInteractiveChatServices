// Dialog_StoreEditor.cs
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
// A dialog window for editing store items in the Chat Interactive mod

/*
============================================================
STORE EDITOR SAVE BEHAVIOR
============================================================

SAVE TRIGGERS:
1. Automatically saves all changes to JSON and dictionary in real time.
2. Window close (PostClose())
3. Bulk operations (Enable/Disable all)
4. Individual item toggles (with debounce)

DESIGN:
• Changes update in-memory Dictionary immediately
• JSON save happens asynchronously (non-blocking)
• Users see instant feedback, save happens in background
============================================================
*/
using CAP_ChatInteractive.Store;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace CAP_ChatInteractive
{
    public enum QuantityLimitMode
    {
        Each,
        OneStack,
        ThreeStacks,
        FiveStacks
    }
    public class Dialog_StoreEditor : Window
    {
        private Vector2 scrollPosition = Vector2.zero;
        private Vector2 categoryScrollPosition = Vector2.zero;
        private string searchQuery = "";
        private string lastSearch = "";
        private StoreSortMethod sortMethod = StoreSortMethod.Name;
        private bool sortAscending = true;
        private string selectedCategory = "All";
        private Dictionary<string, int> categoryCounts = new Dictionary<string, int>();
        private List<StoreItem> filteredItems = new List<StoreItem>();
        private Dictionary<string, int> originalPrices = new Dictionary<string, int>();

        private string selectedModSource = "All";
        private Dictionary<string, int> modSourceCounts = new Dictionary<string, int>();
        private StoreListViewType listViewType = StoreListViewType.Category;

        public override Vector2 InitialSize => new Vector2(1200f, 755f);

        // Constructor
        public Dialog_StoreEditor()
        {
            doCloseButton = true;
            forcePause = true;
            absorbInputAroundWindow = true;

            selectedCategory = "All";
            selectedModSource = "All";
            scrollPosition = Vector2.zero;
            categoryScrollPosition = Vector2.zero;

            BuildCategoryCounts();
            BuildModSourceCounts();
            FilterItems();
            SaveOriginalPrices();
        }
        // DoWindowContents is called every frame to redraw the window
        public override void DoWindowContents(Rect inRect)
        {
            // Update search if query changed
            if (searchQuery != lastSearch || filteredItems.Count == 0)
            {
                FilterItems();
            }

            // Header
            Rect headerRect = new Rect(0f, 0f, inRect.width, 70f); // Increased from 40f to 70f
            DrawHeader(headerRect);

            // Main content area
            Rect contentRect = new Rect(0f, 75f, inRect.width, inRect.height - 75f - CloseButSize.y);
            DrawContent(contentRect);
        }
        // DrawHeader creates the top section of the window with title, search bar, and action buttons
        private void DrawHeader(Rect rect)
        {
            Widgets.BeginGroup(rect);

            // Custom title with larger font and underline effect - similar to PawnQueue
            Text.Font = GameFont.Medium;
            GUI.color = ColorLibrary.HeaderAccent; // Orange accent color for title
            Rect titleRect = new Rect(0f, 0f, 430f, 35f);
            string titleText = "Store Items Editor";

            // Draw title
            Widgets.Label(titleRect, titleText);

            // Draw underline
            Rect underlineRect = new Rect(titleRect.x, titleRect.yMax - 2f, titleRect.width, 2f);
            Widgets.DrawLineHorizontal(underlineRect.x, underlineRect.y, underlineRect.width);

            Text.Font = GameFont.Small;
            GUI.color = Color.white;

            // Second row for controls - positioned below the title
            float controlsY = titleRect.yMax + 5f;
            float controlsHeight = 30f;

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

            // Sort buttons - adjusted position
            Rect sortRect = new Rect(345f, controlsY, 400f, controlsHeight);
            DrawSortButtons(sortRect);

            // Action buttons - adjusted position
            Rect actionsRect = new Rect(695f, controlsY, 430f, controlsHeight);
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
                    Find.WindowStack.Add(new Dialog_StoreEditorHelp());
                }
                TooltipHandler.TipRegion(infoRect, "Events Editor Help");
            }
            else
            {
                // Fallback text button
                if (Widgets.ButtonText(new Rect(rect.width - 110f, 5f, 45f, 24f), "Help"))
                {
                    Find.WindowStack.Add(new Dialog_StoreEditorHelp());
                }
            }

            Widgets.EndGroup();
        }

        private void DrawSortButtons(Rect rect)
        {
            Widgets.BeginGroup(rect);

            float buttonWidth = 90f;   // slightly wider to fit "Mod Source"
            float spacing = 6f;
            float x = 0f;

            // Sort by Name
            if (Widgets.ButtonText(new Rect(x, 0f, buttonWidth, 30f), "Name"))
            {
                if (sortMethod == StoreSortMethod.Name)
                    sortAscending = !sortAscending;
                else
                    sortMethod = StoreSortMethod.Name;
                SortItems();
            }
            x += buttonWidth + spacing;

            // Sort by Price
            if (Widgets.ButtonText(new Rect(x, 0f, buttonWidth, 30f), "Price"))
            {
                if (sortMethod == StoreSortMethod.Price)
                    sortAscending = !sortAscending;
                else
                    sortMethod = StoreSortMethod.Price;
                SortItems();
            }
            x += buttonWidth + spacing;

            // Dynamic Category / Mod Source button
            string secondarySortLabel;
            StoreSortMethod secondarySortMode;

            // Decide which mode the button represents right now
            if (sortMethod == StoreSortMethod.ModSource)
            {
                secondarySortLabel = "Mod Source";
                secondarySortMode = StoreSortMethod.ModSource;
            }
            else
            {
                secondarySortLabel = "Category";
                secondarySortMode = StoreSortMethod.Category;
            }

            // Append arrow if this is the current sort
            if (sortMethod == secondarySortMode)
            {
                secondarySortLabel += sortAscending ? " ↑" : " ↓";
            }

            Rect secondaryRect = new Rect(x, 0f, buttonWidth + 20f, 30f); // a bit wider

            if (Widgets.ButtonText(secondaryRect, secondarySortLabel))
            {
                if (sortMethod == secondarySortMode)
                {
                    // Already sorting by this → just flip direction
                    sortAscending = !sortAscending;
                }
                else
                {
                    // Switch to the other secondary sort (start ascending)
                    sortMethod = secondarySortMode;
                    sortAscending = true;
                }
                SortItems();
            }

            // Optional: show small indicator of what the button will switch to
            string tooltip = sortMethod == StoreSortMethod.Category
                ? "Click to sort by Mod Source"
                : "Click to sort by Category";
            TooltipHandler.TipRegion(secondaryRect, tooltip);

            Widgets.EndGroup();
        }

        // In DrawActionButtons method - replace the Disable All button
        private void DrawActionButtons(Rect rect)
        {
            Widgets.BeginGroup(rect);

            float buttonWidth = 90f;
            float spacing = 5f;
            float x = 0f;

            // Reset All
            if (Widgets.ButtonText(new Rect(x, 0f, buttonWidth, 30f), "Reset All"))
            {
                Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                    "Reset all items to default prices? This cannot be undone.",
                    () => ResetAllPrices()
                ));
            }
            x += buttonWidth + spacing;

            // Enable All
            if (Widgets.ButtonText(new Rect(x, 0f, buttonWidth, 30f), "Enable →"))
            {
                ShowEnableMenu();
            }
            x += buttonWidth + spacing;

            // Disable Dropdown
            if (Widgets.ButtonText(new Rect(x, 0f, buttonWidth, 30f), "Disable →"))
            {
                ShowDisableMenu();
            }
            x += buttonWidth + spacing;

            // Quality & Research Settings button
            if (Widgets.ButtonText(new Rect(x, 0f, buttonWidth + 60f, 30f), "Quality/Research"))
            {
                CAPChatInteractiveMod.OpenQualitySettings();
            }

            Widgets.EndGroup();
        }

        private void ShowEnableMenu()
        {
            var options = new List<FloatMenuOption>();

            // Enable All option
            options.Add(new FloatMenuOption("Enable All Items", () =>
            {
                EnableAllItems();
            }));

            options.Add(new FloatMenuOption("--- Enable by Category ---", null)); // Separator

            // Get all categories and add enable options
            var categories = categoryCounts.Keys
                .Where(cat => cat != "All")
                .OrderBy(cat => cat)
                .ToList();

            foreach (var category in categories)
            {
                options.Add(new FloatMenuOption($"Enable {category} Items", () =>
                {
                    EnableCategoryItems(category);
                }));
            }

            // Enable by type options
            options.Add(new FloatMenuOption("--- Enable by Type ---", null)); // Separator

            options.Add(new FloatMenuOption("Enable All Weapons", () =>
            {
                EnableItemsByPredicate(item => item.IsWeapon, "weapons");
            }));

            options.Add(new FloatMenuOption("Enable All Apparel", () =>
            {
                EnableItemsByPredicate(item => item.IsWearable, "apparel");
            }));

            options.Add(new FloatMenuOption("Enable All Usable Items", () =>
            {
                EnableItemsByPredicate(item => item.IsUsable, "usable items");
            }));

            Find.WindowStack.Add(new FloatMenu(options));
        }

        // Add these helper methods for enable
        private void EnableCategoryItems(string category)
        {
            int enabledCount = 0;
            foreach (var item in StoreInventory.AllStoreItems.Values)
            {
                // Handle null categories
                string itemCategory = item.Category ?? "Uncategorized";
                if (itemCategory == category && !item.Enabled)
                {
                    item.Enabled = true;
                    enabledCount++;
                }
            }
            StoreInventory.SaveStoreToJson();
            Messages.Message($"Enabled {enabledCount} {category} items", MessageTypeDefOf.PositiveEvent);
            FilterItems(); // Refresh the view
        }

        private void EnableItemsByPredicate(System.Func<StoreItem, bool> predicate, string typeDescription)
        {
            int enabledCount = 0;
            foreach (var item in StoreInventory.AllStoreItems.Values)
            {
                if (predicate(item) && !item.Enabled)
                {
                    item.Enabled = true;
                    enabledCount++;
                }
            }
            StoreInventory.SaveStoreToJson();
            Messages.Message($"Enabled {enabledCount} {typeDescription}", MessageTypeDefOf.PositiveEvent);
            FilterItems(); // Refresh the view
        }

        // Add this new method for the disable menu
        private void ShowDisableMenu()
        {
            var options = new List<FloatMenuOption>();

            // Disable All option
            options.Add(new FloatMenuOption("Disable All Items", () =>
            {
                DisableAllItems();
            }));

            options.Add(new FloatMenuOption("--- Disable by Category ---", null)); // Separator

            // Get all categories and add disable options
            var categories = categoryCounts.Keys
                .Where(cat => cat != "All")
                .OrderBy(cat => cat)
                .ToList();

            foreach (var category in categories)
            {
                options.Add(new FloatMenuOption($"Disable {category} Items", () =>
                {
                    DisableCategoryItems(category);
                }));
            }

            // Disable by type options
            options.Add(new FloatMenuOption("--- Disable by Type ---", null)); // Separator

            options.Add(new FloatMenuOption("Disable All Weapons", () =>
            {
                DisableItemsByPredicate(item => item.IsWeapon, "weapons");
            }));

            options.Add(new FloatMenuOption("Disable All Apparel", () =>
            {
                DisableItemsByPredicate(item => item.IsWearable, "apparel");
            }));

            options.Add(new FloatMenuOption("Disable All Usable Items", () =>
            {
                DisableItemsByPredicate(item => item.IsUsable, "usable items");
            }));

            Find.WindowStack.Add(new FloatMenu(options));
        }

        // Add these helper methods
        private void DisableCategoryItems(string category)
        {
            int disabledCount = 0;
            foreach (var item in StoreInventory.AllStoreItems.Values)
            {
                // Handle null categories
                string itemCategory = item.Category ?? "Uncategorized";
                if (itemCategory == category && item.Enabled)
                {
                    item.Enabled = false;
                    disabledCount++;
                }
            }
            StoreInventory.SaveStoreToJson();
            Messages.Message($"Disabled {disabledCount} {category} items", MessageTypeDefOf.NeutralEvent);
            FilterItems(); // Refresh the view
        }

        private void DisableItemsByPredicate(System.Func<StoreItem, bool> predicate, string typeDescription)
        {
            int disabledCount = 0;
            foreach (var item in StoreInventory.AllStoreItems.Values)
            {
                if (predicate(item) && item.Enabled)
                {
                    item.Enabled = false;
                    disabledCount++;
                }
            }
            StoreInventory.SaveStoreToJson();
            Messages.Message($"Disabled {disabledCount} {typeDescription}", MessageTypeDefOf.NeutralEvent);
            FilterItems(); // Refresh the view
        }

        private void EnableModSourceItems()
        {
            int enabledCount = 0;

            foreach (var item in filteredItems)
            {
                if (!item.Enabled)
                {
                    item.Enabled = true;
                    enabledCount++;
                }
            }

            if (enabledCount > 0)
            {
                StoreInventory.SaveStoreToJson();
                string modName = GetDisplayModName(selectedModSource);
                Messages.Message($"Enabled {enabledCount} items from '{modName}'",
                    MessageTypeDefOf.PositiveEvent);
                FilterItems(); // Refresh view
            }
        }

        private void DisableModSourceItems()
        {
            int disabledCount = 0;

            foreach (var item in filteredItems)
            {
                if (item.Enabled)
                {
                    item.Enabled = false;
                    disabledCount++;
                }
            }

            if (disabledCount > 0)
            {
                StoreInventory.SaveStoreToJson();
                string modName = GetDisplayModName(selectedModSource);
                Messages.Message($"Disabled {disabledCount} items from '{modName}'",
                    MessageTypeDefOf.NeutralEvent);
                FilterItems(); // Refresh view
            }
        }

        private void DrawContent(Rect rect)
        {
            // Add 2px padding to the left side
            float padding = 2f;
            rect.x += padding;
            rect.width -= padding;

            // Split into categories (left) and items (right)
            float categoryWidth = 200f;
            float itemsWidth = rect.width - categoryWidth - 10f;

            Rect listRect = new Rect(rect.x, rect.y, categoryWidth, rect.height);
            Rect itemsRect = new Rect(rect.x + categoryWidth + 10f, rect.y, itemsWidth, rect.height);

            if (listViewType == StoreListViewType.Category)
            {
                DrawCategoryList(listRect);
            }
            else
            {
                DrawModSourcesList(listRect);
            }

            DrawItemList(itemsRect);
        }
        // This method draws the category list on the left side when in Category view.
        private void DrawCategoryList(Rect rect)
        {
            // Background
            Widgets.DrawMenuSection(rect);

            // Header Catagory List
            Rect headerRect = new Rect(rect.x, rect.y, rect.width, 30f);
            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.MiddleCenter;
            GUI.color = ColorLibrary.SubHeader;
            Widgets.Label(headerRect, "Categories");
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;
            GUI.color = Color.white;
            TooltipHandler.TipRegion(headerRect, "Click to switch to mod source view");

            // Toggle view on header click
            if (Widgets.ButtonInvisible(headerRect))
            {
                listViewType = StoreListViewType.ModSource;
                selectedCategory = "All";           // reset
                selectedModSource = "All";          // reset both for safety
                scrollPosition = Vector2.zero;      // reset item list scroll
                categoryScrollPosition = Vector2.zero; // reset left panel scroll
                FilterItems();                      // rebuild list immediately
            }

            // Category list area
            Rect listRect = new Rect(rect.x, rect.y + 35f, rect.width, rect.height - 35f);

            // Safety: don't crash if no categories yet
            if (categoryCounts == null || categoryCounts.Count == 0)
            {
                Widgets.Label(listRect, "No categories loaded");
                return;
            }

            Rect viewRect = new Rect(0f, 0f, listRect.width - 20f, categoryCounts.Count * 30f);

            Widgets.BeginScrollView(listRect, ref categoryScrollPosition, viewRect);
            {
                float y = 0f;

                // Custom sort: "All" first → Apparel group → Alphabetical → Uncategorized last
                var orderedCategories = categoryCounts.Keys
                    .OrderBy(cat =>
                    {
                        if (cat == "All") return 0;
                        if (cat == "Apparel") return 1;
                        if (cat == "Children's Apparel") return 2;
                        if (cat == "Uncategorized") return 998;
                        return 3;
                    })
                    .ThenBy(cat => cat)
                    .ToList();

                foreach (var cat in orderedCategories)
                {
                    int count = categoryCounts[cat];
                    Rect categoryButtonRect = new Rect(2f, y, viewRect.width - 4f, 28f);

                    string label = $"{cat} ({count})";

                    // Use the recommended truncation method
                    string displayLabel = UIUtilities.Truncate(label, categoryButtonRect.width - 10f);

                    // Visual feedback
                    if (selectedCategory == cat)
                        Widgets.DrawHighlightSelected(categoryButtonRect);
                    else if (Mouse.IsOver(categoryButtonRect))
                        Widgets.DrawHighlight(categoryButtonRect);

                    // Button action
                    if (Widgets.ButtonText(categoryButtonRect, displayLabel))
                    {
                        selectedCategory = cat;
                        FilterItems();  // Make sure this method exists!
                    }

                    // Tooltip only when actually truncated
                    if (UIUtilities.WouldTruncate(label, categoryButtonRect.width - 10f))
                    {
                        TooltipHandler.TipRegion(categoryButtonRect, label);
                    }

                    y += 30f;
                }
            }
            Widgets.EndScrollView();
        }
        // This method draws the mod source list on the left side when in Mod Source view.
        private void DrawModSourcesList(Rect rect)
        {
            // Background
            Widgets.DrawMenuSection(rect);

            // Header Mod Source List
            Rect headerRect = new Rect(rect.x, rect.y, rect.width, 30f);
            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.MiddleCenter;
            GUI.color = ColorLibrary.SubHeader;
            Widgets.Label(headerRect, "Mod Sources");
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;
            GUI.color = Color.white;
            TooltipHandler.TipRegion(headerRect, "Click to switch to category view");

            // Toggle view on header click
            if (Widgets.ButtonInvisible(headerRect))
            {
                listViewType = StoreListViewType.Category;
                selectedCategory = "All";           // reset
                selectedModSource = "All";          // reset both for safety
                scrollPosition = Vector2.zero;      // reset item list scroll
                categoryScrollPosition = Vector2.zero; // reset left panel scroll
                FilterItems();                      // rebuild list immediately
            }

            // Mod source list area
            Rect listRect = new Rect(rect.x, rect.y + 35f, rect.width, rect.height - 35f);

            // Safety: don't crash if no mod sources yet
            if (modSourceCounts == null || modSourceCounts.Count == 0)
            {
                Widgets.Label(listRect, "No mod sources loaded");
                return;
            }

            Rect viewRect = new Rect(0f, 0f, listRect.width - 20f, modSourceCounts.Count * 30f);

            Widgets.BeginScrollView(listRect, ref categoryScrollPosition, viewRect);
            {
                float y = 0f;

                // Custom sort: "All" first → Alphabetical
                var orderedModSources = modSourceCounts.Keys
                    .OrderBy(source =>
                    {
                        if (source == "All") return 0;
                        return 1;
                    })
                    .ThenBy(source => source)
                    .ToList();

                foreach (var source in orderedModSources)
                {
                    int count = modSourceCounts[source];
                    Rect sourceButtonRect = new Rect(2f, y, viewRect.width - 4f, 28f);

                    string label = $"{source} ({count})";

                    // Use truncation
                    string displayLabel = UIUtilities.Truncate(label, sourceButtonRect.width - 10f);

                    // Visual feedback
                    if (selectedModSource == source)
                        Widgets.DrawHighlightSelected(sourceButtonRect);
                    else if (Mouse.IsOver(sourceButtonRect))
                        Widgets.DrawHighlight(sourceButtonRect);

                    // Button action
                    if (Widgets.ButtonText(sourceButtonRect, displayLabel))
                    {
                        selectedModSource = source;
                        FilterItems();
                    }

                    // Tooltip only when actually truncated
                    if (UIUtilities.WouldTruncate(label, sourceButtonRect.width - 10f))
                    {
                        TooltipHandler.TipRegion(sourceButtonRect, label);
                    }

                    y += 30f;
                }
            }
            Widgets.EndScrollView();
        }
        // This method builds the mod source counts dictionary by
        // iterating through all store items and counting how many items belong to each mod source.
        private void BuildModSourceCounts()
        {
            modSourceCounts.Clear();
            modSourceCounts["All"] = StoreInventory.AllStoreItems.Count;

            foreach (var item in StoreInventory.AllStoreItems.Values)
            {
                // Handle null mod sources
                string modSourceKey = GetDisplayModName(item.ModSource ?? "Unknown");

                if (modSourceCounts.ContainsKey(modSourceKey))
                    modSourceCounts[modSourceKey]++;
                else
                    modSourceCounts[modSourceKey] = 1;
            }
        }
        // This method converts raw mod source names into more user-friendly display names
        private string GetDisplayModName(string modSource)
        {
            if (modSource == "Core") return "RimWorld";
            if (modSource.Contains(".")) return modSource.Split('.')[0];
            return modSource;
        }
        // This method draws the item list on the right side,
        // including the header with count and bulk controls,
        // and then the scrollable list of items.
        // It only draws the visible rows based on the scroll position for performance.
        private void DrawItemList(Rect rect)
        {
            // Background for the entire right panel
            Widgets.DrawMenuSection(rect);

            // Calculate if we need to show bulk controls
            bool showBulkControls = filteredItems.Count > 0;

            // Start with base height for count row
            float currentY = 0f;

            // ────────────────────────────────────────
            // First: calculate final header height (same logic as before)
            float headerHeight = 30f; // count row

            if (showBulkControls)
            {
                headerHeight += 30f; // quantity controls

                bool showPriceControls = false;
                if (listViewType == StoreListViewType.Category && selectedCategory != "All")
                    showPriceControls = true;
                else if (listViewType == StoreListViewType.ModSource && selectedModSource != "All")
                    showPriceControls = true;

                if (showPriceControls)
                {
                    headerHeight += 30f; // price/enable/disable row
                }
            }

            // ────────────────────────────────────────
            // NOW draw the background box FIRST (behind everything)
            Rect headerRect = new Rect(rect.x, rect.y, rect.width, headerHeight);

            // Subtle dark background (adjust alpha if too strong)
            Widgets.DrawBoxSolid(headerRect, new Color(0.18f, 0.18f, 0.18f, 0.85f)); // slightly darker, high opacity

            // ────────────────────────────────────────
            // Now draw content ON TOP of the background
            currentY = 0f;

            // Draw item count header (always shown)
            Rect countRect = new Rect(rect.x, rect.y + currentY, rect.width, 30f);
            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.MiddleCenter;
            GUI.color = ColorLibrary.SubHeader; 
            string headerText = $"Items ({filteredItems.Count})";
            if (listViewType == StoreListViewType.Category && selectedCategory != "All")
                headerText += $" – {selectedCategory}";
            else if (listViewType == StoreListViewType.ModSource && selectedModSource != "All")
                headerText += $" – {GetDisplayModName(selectedModSource)}";

            string displayHeader = UIUtilities.Truncate(headerText, countRect.width - 20f);
            Widgets.Label(countRect, displayHeader);

            if (UIUtilities.WouldTruncate(headerText, countRect.width - 20f))
                TooltipHandler.TipRegion(countRect, headerText);


            Text.Font = GameFont.Small;
            GUI.color = Color.white;

            currentY += 30f;

            // Bulk controls section
            if (showBulkControls)
            {
                // Quantity controls (always when items present)
                Rect qtyControlsRect = new Rect(rect.x, rect.y + currentY, rect.width, 28f);
                DrawBulkQuantityControls(qtyControlsRect);
                currentY += 30f;

                // Price/enable/disable controls (only when specific category or mod source selected)
                bool showPriceControls = false;
                if (listViewType == StoreListViewType.Category && selectedCategory != "All")
                    showPriceControls = true;
                else if (listViewType == StoreListViewType.ModSource && selectedModSource != "All")
                    showPriceControls = true;

                if (showPriceControls)
                {
                    Rect priceControlsRect = new Rect(rect.x, rect.y + currentY, rect.width, 28f);

                    if (listViewType == StoreListViewType.Category)
                    {
                        DrawCategoryPriceControls(priceControlsRect);
                    }
                    else // ModSource
                    {
                        DrawModSourcePriceControls(priceControlsRect);
                    }
                    currentY += 30f;
                }
            }

            Text.Anchor = TextAnchor.UpperLeft;

            // Item list starts after header + small padding
            Rect listRect = new Rect(rect.x, rect.y + headerHeight + 4f, rect.width, rect.height - headerHeight - 8f);

            if (filteredItems.Count == 0)
            {
                Widgets.NoneLabelCenteredVertically(listRect, "No items match the current filter");
                return;
            }

            float rowHeight = 64f;
            float viewHeight = filteredItems.Count * rowHeight;
            Rect viewRect = new Rect(0f, 0f, listRect.width - 16f, viewHeight); // -16f ≈ scrollbar width

            Widgets.BeginScrollView(listRect, ref scrollPosition, viewRect);

            // Only draw visible rows (your existing performant loop)
            float scrollY = scrollPosition.y;
            float visibleTop = scrollY;
            float visibleBottom = scrollY + listRect.height;

            int firstRow = Mathf.Max(0, Mathf.FloorToInt(visibleTop / rowHeight));
            int lastRow = Mathf.Min(filteredItems.Count - 1, Mathf.CeilToInt(visibleBottom / rowHeight));

            firstRow = Mathf.Max(0, firstRow - 2);
            lastRow = Mathf.Min(filteredItems.Count - 1, lastRow + 2);

            for (int i = firstRow; i <= lastRow; i++)
            {
                float y = i * rowHeight;
                Rect rowRect = new Rect(0f, y, viewRect.width, rowHeight);

                if (y + rowHeight < visibleTop || y > visibleBottom)
                    continue;

                StoreItem item = filteredItems[i];
                DrawItemRow(rowRect, item, i);  // assuming it accepts index; remove ,i if not needed
            }

            Widgets.EndScrollView();
        }
        // This method draws a single item row, given the rect and the StoreItem data
        // Only draws the Rows that are visible based on the scroll position and row height
        private void DrawItemRow(Rect rect, StoreItem item, int index)
        {
            Widgets.BeginGroup(rect);
            try
            {
                var thingDef = DefDatabase<ThingDef>.GetNamedSilentFail(item.DefName);

                float centerY = (rect.height - 30f) / 2f; // vertical center
                float x = 5f;

                // === Icon ===
                if (thingDef != null)
                {
                    Rect iconRect = new Rect(x, 5f, 50f, 50f);
                    Widgets.ThingIcon(iconRect, thingDef);

                    // Make the icon itself clickable to show Def info
                    if (Widgets.ButtonInvisible(iconRect))
                    {
                        ShowDefInfoWindow(thingDef, item);
                    }

                    // Add tooltip for the icon
                    TooltipHandler.TipRegion(iconRect, "Click icon for detailed item information and to set custom name");

                    Widgets.InfoCardButton(iconRect.xMax + 2f, iconRect.y, thingDef);
                }
                x += 80f;

                // === Info text === 
                float infoWidth = 210f; // Fixed reasonable width for item info
                Rect infoRect = new Rect(x, 5f, infoWidth, 50f);
                Text.Anchor = TextAnchor.MiddleLeft;

                // Get the LabelCap as string for comparison
                string labelCapString = thingDef?.LabelCap.ToString();

                // Determine if we have a user-set custom name
                // Custom name is considered "user-set" if it exists AND is different from LabelCap
                bool hasUserCustomName = !string.IsNullOrEmpty(item.CustomName) &&
                                         item.CustomName != labelCapString;

                // Determine the display name: User Custom Name first, then LabelCap, then DefName
                string displayName;
                if (hasUserCustomName)
                {
                    displayName = item.CustomName;
                }
                else
                {
                    displayName = labelCapString ?? item.DefName;
                }

                // Truncate the display name if needed
                string truncatedDisplayName = UIUtilities.Truncate(displayName, infoWidth - 5f);
                Widgets.Label(infoRect.TopHalf(), truncatedDisplayName);

                // Add tooltip if truncated OR if there's a user-set custom name
                if (UIUtilities.WouldTruncate(displayName, infoWidth - 5f) || hasUserCustomName)
                {
                    // Build comprehensive tooltip text
                    StringBuilder tooltipBuilder = new StringBuilder();

                    if (hasUserCustomName)
                    {
                        tooltipBuilder.AppendLine($"Custom Name: {item.CustomName}");
                        tooltipBuilder.AppendLine($"Default Name: {labelCapString ?? item.DefName}");
                    }
                    else
                    {
                        tooltipBuilder.AppendLine($"Name: {labelCapString ?? item.DefName}");
                    }

                    tooltipBuilder.AppendLine($"DefName: {item.DefName}");

                    // Only show LabelCap separately if it's different from DefName
                    if (thingDef != null && labelCapString != item.DefName)
                    {
                        tooltipBuilder.AppendLine($"LabelCap: {thingDef.LabelCap}");
                    }

                    if (hasUserCustomName)
                    {
                        tooltipBuilder.AppendLine($"\nClick the icon to edit custom name");
                    }

                    TooltipHandler.TipRegion(infoRect.TopHalf(), tooltipBuilder.ToString());
                }
                // Optional: Add a small indicator for user-set custom names
                if (hasUserCustomName)
                {
                    Rect customIndicatorRect = new Rect(infoRect.x, infoRect.y, 4f, 4f);
                    GUI.color = ColorLibrary.HeaderAccent;
                    Widgets.DrawBox(customIndicatorRect);
                    GUI.color = Color.white;

                    // Add a special tooltip just for the indicator
                    Rect indicatorTooltipRect = new Rect(infoRect.x, infoRect.y, 20f, 20f);
                    TooltipHandler.TipRegion(indicatorTooltipRect, "Custom name is set");
                }

                Text.Font = GameFont.Tiny;
                // Also truncate category info if needed
                string categoryInfo = $"{item.Category} • {item.ModSource}";
                string displayCategory = UIUtilities.Truncate(categoryInfo, infoWidth - 5f);
                Widgets.Label(infoRect.BottomHalf(), displayCategory);

                // Add tooltip if truncated
                if (UIUtilities.WouldTruncate(categoryInfo, infoWidth - 5f))
                {
                    TooltipHandler.TipRegion(infoRect.BottomHalf(), categoryInfo);
                }

                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.UpperLeft;
                x += infoWidth + 10f;

                // === Enabled ===
                Rect enabledRect = new Rect(x, centerY, 80f, 30f);
                DrawEnabledToggle(enabledRect, item);
                x += enabledRect.width + 8f;

                // === Type checkbox === (now only one, so center vertically)
                Rect typeRect = new Rect(x, centerY, 100f, 30f);
                DrawItemTypeCheckboxes(typeRect, item);
                x += typeRect.width + 12f;

                // === Price ===
                Rect priceRect = new Rect(x, centerY, 150f, 30f);
                DrawPriceControls(priceRect, item);
                x += priceRect.width + 8f;

                // === Quantity preset controls ===
                // Use remaining space for quantity controls
                float remainingWidth = rect.width - x - 10f;
                if (remainingWidth > 200f) // Ensure minimum width for quantity controls
                {
                    Rect qtyRect = new Rect(x, centerY, remainingWidth, 30f);
                    DrawQuantityPresetControls(qtyRect, item);
                }
                else
                {
                    // Fallback: if not enough space, use compact layout
                    Rect qtyRect = new Rect(x, centerY, 200f, 30f);
                    DrawQuantityPresetControls(qtyRect, item);
                }
            }
            finally
            {
                Widgets.EndGroup();
                Text.Anchor = TextAnchor.UpperLeft;
                GUI.color = Color.white;
            }
        }

        // This method draws the bulk quantity controls in the header of the item list.
        private void DrawBulkQuantityControls(Rect rect)
        {
            Widgets.BeginGroup(rect);

            float iconSize = 24f;
            float spacing = 4f;
            float centerY = (rect.height - iconSize) / 2f;
            float x = rect.width / 2f - 150f; // Center the controls

            // Label
            Rect labelRect = new Rect(x, centerY, 80f, iconSize);
            Text.Anchor = TextAnchor.MiddleRight;
            string labelText = "Set All Qty:";
            string displayLabel = UIUtilities.Truncate(labelText, labelRect.width);
            Widgets.Label(labelRect, displayLabel);

            // Add tooltip if truncated
            if (UIUtilities.WouldTruncate(labelText, labelRect.width))
            {
                TooltipHandler.TipRegion(labelRect, labelText);
            }
            Text.Anchor = TextAnchor.UpperLeft;
            x += 85f + spacing;

            // Enable/disable toggle
            Rect enableRect = new Rect(x, centerY, 24f, iconSize);
            bool anyHasLimit = filteredItems.Any(item => item.HasQuantityLimit);
            bool allHaveLimit = filteredItems.All(item => item.HasQuantityLimit);

            // Use mixed state if some have limit and some don't
            bool? mixedState = anyHasLimit && !allHaveLimit ? null : (bool?)allHaveLimit;

            if (Widgets.ButtonInvisible(enableRect))
            {
                // If mixed or any disabled, enable all. If all enabled, disable all.
                bool newState = !allHaveLimit;
                EnableQuantityLimitForAllVisible(newState);
            }

            // Draw appropriate checkbox state
            if (mixedState.HasValue)
            {
                bool state = mixedState.Value;
                Widgets.Checkbox(enableRect.position, ref state, 24f);
            }
            else
            {
                // Draw mixed state (partially checked)
                Texture2D mixedTex = ContentFinder<Texture2D>.Get("UI/Widgets/CheckBoxPartial", false);
                if (mixedTex != null)
                {
                    Widgets.DrawTextureFitted(enableRect, mixedTex, 1f);
                }
                else
                {
                    // Fallback: draw empty checkbox with different background
                    Widgets.DrawRectFast(enableRect, new Color(0.5f, 0.5f, 0.5f, 0.3f));
                    Widgets.DrawBox(enableRect);
                }
            }

            TooltipHandler.TipRegion(enableRect, "Enable/disable quantity limits for all visible items");
            x += 28f + spacing;

            // Stack preset buttons (only show if there are items with quantity limits)
            if (anyHasLimit)
            {
                (string icon, string tooltip, int stacks)[] presets =
                {
                    ("Stack1", "Set all visible items to 1 stack limit", 1),
                    ("Stack3", "Set all visible items to 3 stacks limit", 3),
                    ("Stack5", "Set all visible items to 5 stacks limit", 5)
                };

                foreach (var preset in presets)
                {
                    Texture2D icon = null;

                    // Method 1: Try standard path
                    icon = ContentFinder<Texture2D>.Get($"UI/Icons/{preset.icon}", false);
                    Rect iconRect = new Rect(x, centerY, iconSize, iconSize);

                    // Hover highlight
                    if (Mouse.IsOver(iconRect))
                        Widgets.DrawHighlight(iconRect);

                    if (icon == null)
                    {
                        Log.Warning($"Could not load icon: UI/Icons/{preset.icon}");
                        // Fallback to text
                        Widgets.ButtonText(iconRect, $"{preset.stacks}x");
                    }
                    else
                    {
                        Widgets.DrawTextureFitted(iconRect, icon, 1f);
                    }

                    TooltipHandler.TipRegion(iconRect, preset.tooltip);

                    // Click handler
                    if (Widgets.ButtonInvisible(iconRect))
                    {
                        SetAllVisibleItemsQuantityLimit(preset.stacks);
                    }

                    x += iconSize + spacing;
                }
            }

            Widgets.EndGroup();
        }
        // This method draws the item type checkboxes (Usable, Equippable, Wearable) in the item row.
        private void DrawItemTypeCheckboxes(Rect rect, StoreItem item)
        {
            Widgets.BeginGroup(rect);
            float centerY = (rect.height - 18f) / 2f;
            var thingDef = DefDatabase<ThingDef>.GetNamedSilentFail(item.DefName);
            if (thingDef != null)
            {
                string label = null;
                Action<Rect, StoreItem> drawAction = null;

                if (StoreItem.IsItemUsable(thingDef))
                {
                    label = "Usable";
                    drawAction = DrawUsableCheckbox;
                }
                else if (!item.IsUsable && thingDef.IsWeapon)
                {
                    label = "Equippable";
                    drawAction = DrawEquippableCheckbox;
                }
                else if (!item.IsUsable && !item.IsEquippable && thingDef.IsApparel) 
                {
                    label = "Wearable";
                    drawAction = DrawWearableCheckbox;
                }


                if (label != null && drawAction != null)
                {
                    Rect centered = new Rect(0f, centerY, rect.width, 18f);
                    drawAction(centered, item);
                }
            }
            Widgets.EndGroup();
        }
        // This method draws the quantity preset controls in the item row, allowing quick setting of quantity limits.
        private void DrawQuantityPresetControls(Rect rect, StoreItem item)
        {
            Widgets.BeginGroup(rect);

            float x = 0f;
            float iconSize = 32f;              // match your icons
            float spacing = 6f;
            float centerY = (rect.height - iconSize) / 2f;

            // === Enable/disable limit ===
            bool hasLimit = item.HasQuantityLimit;
            Widgets.Checkbox(new Vector2(x, centerY + 4f), ref hasLimit, 24f);
            if (hasLimit != item.HasQuantityLimit)
            {
                item.HasQuantityLimit = hasLimit;
                SoundDefOf.Checkbox_TurnedOn.PlayOneShotOnCamera(); // ✅ RimWorld native sound
                StoreInventory.SaveStoreToJson();
            }
            x += 28f + spacing;

            // === Label ===
            Widgets.Label(new Rect(x, centerY + 6f, 45f, 24f), "Qty:");
            x += 36f + spacing;

            if (item.HasQuantityLimit)
            {
                var thingDef = DefDatabase<ThingDef>.GetNamedSilentFail(item.DefName);
                if (thingDef != null)
                {
                    // === Stack preset buttons ===
                    (string icon, string tooltip, int stacks)[] presets =
                    {
                ("Stack1", "Set to 1 stack", 1),
                ("Stack3", "Set to 3 stacks", 3),
                ("Stack5", "Set to 5 stacks", 5)
            };

                    foreach (var preset in presets)
                    {
                        Texture2D icon = ContentFinder<Texture2D>.Get($"UI/Icons/{preset.icon}", false);
                        Rect iconRect = new Rect(x, centerY, iconSize, iconSize);

                        // Hover highlight
                        if (Mouse.IsOver(iconRect))
                            Widgets.DrawHighlight(iconRect);

                        // Draw icon (fallback to text)
                        if (icon != null)
                            Widgets.DrawTextureFitted(iconRect, icon, 1f);
                        else
                            Widgets.ButtonText(iconRect, $"{preset.stacks}x");

                        TooltipHandler.TipRegion(iconRect, preset.tooltip);

                        // Click handler
                        if (Widgets.ButtonInvisible(iconRect))
                        {
                            int baseStack = Mathf.Max(1, thingDef.stackLimit);
                            item.QuantityLimit = Mathf.Clamp(baseStack * preset.stacks, 1, 9999);

                            // ✅ Play RimWorld click sound safely
                            SoundDefOf.Click.PlayOneShotOnCamera();

                            StoreInventory.SaveStoreToJson();
                        }

                        x += iconSize + spacing;
                    }

                    // === Numeric box (always visible, inside bounds) ===
                    float boxWidth = 60f;
                    Rect numRect = new Rect(x + 2f, centerY + 4f, boxWidth, 24f);

                    int limit = item.QuantityLimit;
                    string buffer = limit.ToString();
                    UIUtilities.TextFieldNumericFlexible(numRect, ref limit, ref buffer, 1, 9999);
                    if (limit != item.QuantityLimit)
                    {
                        item.QuantityLimit = limit;
                        SoundDefOf.Tick_Tiny.PlayOneShotOnCamera(); // ✅ soft feedback for typing
                        StoreInventory.SaveStoreToJson();
                    }

                    TooltipHandler.TipRegion(numRect, $"Manual limit (current: {item.QuantityLimit})");
                }
            }

            Widgets.EndGroup();
        }
        // These methods draw the individual checkboxes for item types (Usable, Wearable, Equippable) in the item row.
        private void DrawUsableCheckbox(Rect rect, StoreItem item)
        {
            bool currentValue = item.IsUsable;
            Widgets.CheckboxLabeled(rect, "Usable", ref currentValue);
            if (currentValue != item.IsUsable)
            {
                item.IsUsable = currentValue;
                SoundDefOf.Checkbox_TurnedOn.PlayOneShotOnCamera(); // ✅ RimWorld native sound
                StoreInventory.SaveStoreToJson();
            }
        }
        // These methods draw the individual checkboxes for item types (Usable, Wearable, Equippable) in the item row.
        private void DrawWearableCheckbox(Rect rect, StoreItem item)
        {
            bool currentValue = item.IsWearable;
            Widgets.CheckboxLabeled(rect, "Wearable", ref currentValue);
            if (currentValue != item.IsWearable)
            {
                item.IsWearable = currentValue;
                SoundDefOf.Checkbox_TurnedOn.PlayOneShotOnCamera(); // ✅ RimWorld native sound
                StoreInventory.SaveStoreToJson();
            }
        }
        // These methods draw the individual checkboxes for item types (Usable, Wearable, Equippable) in the item row.
        private void DrawEquippableCheckbox(Rect rect, StoreItem item)
        {
            bool currentValue = item.IsEquippable;
            Widgets.CheckboxLabeled(rect, "Equippable", ref currentValue);
            if (currentValue != item.IsEquippable)
            {
                item.IsEquippable = currentValue;
                SoundDefOf.Checkbox_TurnedOn.PlayOneShotOnCamera(); // ✅ RimWorld native sound
                StoreInventory.SaveStoreToJson();
            }
        }
        // This method draws the enabled/disabled toggle for an item in the item row.
        private void DrawEnabledToggle(Rect rect, StoreItem item)
        {
            bool wasEnabled = item.Enabled;
            bool currentEnabled = item.Enabled;
            Widgets.CheckboxLabeled(rect, "Enabled", ref currentEnabled);
            if (currentEnabled != wasEnabled)
            {
                item.Enabled = currentEnabled;
                SoundDefOf.Checkbox_TurnedOn.PlayOneShotOnCamera(); // ✅ RimWorld native sound
                StoreInventory.SaveStoreToJson();
            }
        }
        // This method draws the price controls (input and reset button) for an item in the item row.
        private void DrawPriceControls(Rect rect, StoreItem item)
        {
            Widgets.BeginGroup(rect);

            // Price label
            Rect labelRect = new Rect(0f, 0f, 40f, 30f);
            Widgets.Label(labelRect, "Price:");

            // Price input - use local variable instead of property directly
            Rect inputRect = new Rect(45f, 0f, 60f, 30f);
            int currentPrice = item.BasePrice; // Copy to local variable
            string priceBuffer = currentPrice.ToString();

            // Store previous price to detect changes
            int previousPrice = currentPrice;
            UIUtilities.TextFieldNumericFlexible(inputRect, ref currentPrice, ref priceBuffer, 0, 1000000);

            // Check if price changed and update property
            if (currentPrice != previousPrice)
            {
                item.BasePrice = currentPrice;
                SoundDefOf.Checkbox_TurnedOn.PlayOneShotOnCamera(); // ✅ RimWorld native sound
                StoreInventory.SaveStoreToJson(); // Auto-save price changes
            }

            // Reset button
            Rect resetRect = new Rect(110f, 0f, 40f, 30f);
            if (Widgets.ButtonText(resetRect, "Reset"))
            {
                var thingDef = DefDatabase<ThingDef>.GetNamedSilentFail(item.DefName);
                if (thingDef != null)
                {
                    item.BasePrice = (int)(thingDef.BaseMarketValue);  // Rimworld Base Market value
                    SoundDefOf.Checkbox_TurnedOn.PlayOneShotOnCamera(); // ✅ RimWorld native sound
                    StoreInventory.SaveStoreToJson();
                }
            }

            Widgets.EndGroup();
        }
        // This method draws the bulk price controls (Enable/Disable/Reset)
        // in the header of the item list when a specific category is selected.
        private void DrawCategoryPriceControls(Rect rect)
        {
            Widgets.BeginGroup(rect);

            float buttonWidth = 110f;
            float spacing = 8f;
            float x = 12f;

            // Enable button (with dropdown arrow feel)
            Rect enableRect = new Rect(x, 2f, buttonWidth + 20f, 28f);
            Text.Anchor = TextAnchor.MiddleRight;

            if (Widgets.ButtonText(enableRect, "Enable →"))
            {
                ShowCategoryEnableMenu();
            }
            x += buttonWidth + 28f + spacing;

            // Disable button
            Rect disableRect = new Rect(x, 2f, buttonWidth + 20f, 28f);
            if (Widgets.ButtonText(disableRect, "Disable →"))
            {
                ShowCategoryDisableMenu();
            }
            x += buttonWidth + 28f + spacing;

            // Reset All Prices button (with confirmation)
            Rect resetRect = new Rect(x, 2f, buttonWidth + 30f, 28f);
            if (Widgets.ButtonText(resetRect, "Reset All"))
            {
                Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                    $"Reset all prices to default for {filteredItems.Count} items in '{selectedCategory}'?\nThis cannot be undone.",
                    () => ResetCategoryPrices()
                ));
            }
            Text.Anchor = TextAnchor.UpperLeft;
            Widgets.EndGroup();
        }
        // This method draws the bulk price controls (Enable/Disable/Reset)
        private void DrawModSourcePriceControls(Rect rect)
        {
            Widgets.BeginGroup(rect);

            float buttonWidth = 110f;
            float spacing = 8f;
            float x = 12f;

            // Enable button
            Rect enableRect = new Rect(x, 2f, buttonWidth + 20f, 28f);
            Text.Anchor = TextAnchor.MiddleRight;
            if (Widgets.ButtonText(enableRect, "Enable →"))
            {
                // For mod source - we can either reuse category logic or make a mod-specific one
                // Simplest: use the same category-style menu but apply to current mod source items
                ShowModSourceEnableMenu();  // ← we'll define this next
            }
            x += buttonWidth + 28f + spacing;

            // Disable button
            Rect disableRect = new Rect(x, 2f, buttonWidth + 20f, 28f);
            if (Widgets.ButtonText(disableRect, "Disable →"))
            {
                ShowModSourceDisableMenu();
            }
            x += buttonWidth + 28f + spacing;

            // Reset All Prices button
            Rect resetRect = new Rect(x, 2f, buttonWidth + 30f, 28f);
            if (Widgets.ButtonText(resetRect, "Reset All"))
            {
                Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                    $"Reset all prices to default for {filteredItems.Count} items from '{GetDisplayModName(selectedModSource)}'?\nThis cannot be undone.",
                    () => ResetModSourcePrices()
                ));
            }
            Text.Anchor = TextAnchor.UpperLeft;
            Widgets.EndGroup();
        }
        // These methods show the bulk enable/disable menus for mod sources,
        // allowing users to enable/disable all items from the mod source or by specific usage types.
        private void ShowModSourceEnableMenu()
        {
            var options = new List<FloatMenuOption>();

            // Enable all in current mod source
            options.Add(new FloatMenuOption($"Enable All Items from {GetDisplayModName(selectedModSource)}", () =>
            {
                EnableModSourceItems();
            }));

            options.Add(new FloatMenuOption("--- Enable Usage Types ---", null)); // Separator

            options.Add(new FloatMenuOption($"Enable Usable Items from {GetDisplayModName(selectedModSource)}", () =>
            {
                ToggleModSourceItemTypeFlag("Usable", true);
            }));

            options.Add(new FloatMenuOption($"Enable Wearable Items from {GetDisplayModName(selectedModSource)}", () =>
            {
                ToggleModSourceItemTypeFlag("Wearable", true);
            }));

            options.Add(new FloatMenuOption($"Enable Equippable Items from {GetDisplayModName(selectedModSource)}", () =>
            {
                ToggleModSourceItemTypeFlag("Equippable", true);
            }));

            Find.WindowStack.Add(new FloatMenu(options));
        }
        // This method shows the disable menu for mod sources,
        // allowing users to disable all items from the mod source or by specific usage types.
        private void ShowModSourceDisableMenu()
        {
            var options = new List<FloatMenuOption>();

            // Disable all in current mod source
            options.Add(new FloatMenuOption($"Disable All Items from {GetDisplayModName(selectedModSource)}", () =>
            {
                DisableModSourceItems();
            }));

            options.Add(new FloatMenuOption("--- Disable Usage Types ---", null)); // Separator

            options.Add(new FloatMenuOption($"Disable Usable Items from {GetDisplayModName(selectedModSource)}", () =>
            {
                ToggleModSourceItemTypeFlag("Usable", false);
            }));

            options.Add(new FloatMenuOption($"Disable Wearable Items from {GetDisplayModName(selectedModSource)}", () =>
            {
                ToggleModSourceItemTypeFlag("Wearable", false);
            }));

            options.Add(new FloatMenuOption($"Disable Equippable Items from {GetDisplayModName(selectedModSource)}", () =>
            {
                ToggleModSourceItemTypeFlag("Equippable", false);
            }));

            Find.WindowStack.Add(new FloatMenu(options));
        }
        // This method toggles the specified item type flag (Usable, Wearable, Equippable)
        // for all items from the currently selected mod source.
        private void ToggleModSourceItemTypeFlag(string flagType, bool enable)
        {
            int changedCount = 0;

            foreach (var item in filteredItems)  // already filtered to current mod source
            {
                var thingDef = DefDatabase<ThingDef>.GetNamedSilentFail(item.DefName);
                if (thingDef == null) continue;

                bool shouldHaveThisFlag = false;

                switch (flagType)
                {
                    case "Usable":
                        shouldHaveThisFlag = StoreItem.IsItemUsable(thingDef);
                        break;
                    case "Equippable":
                        shouldHaveThisFlag = !StoreItem.IsItemUsable(thingDef) && thingDef.IsWeapon;
                        break;
                    case "Wearable":
                        shouldHaveThisFlag = !StoreItem.IsItemUsable(thingDef) && !thingDef.IsWeapon && thingDef.IsApparel;
                        break;
                }

                if (shouldHaveThisFlag)
                {
                    bool changed = false;

                    switch (flagType)
                    {
                        case "Usable":
                            if (item.IsUsable != enable)
                            {
                                item.IsUsable = enable;
                                changed = true;
                            }
                            break;
                        case "Wearable":
                            if (item.IsWearable != enable)
                            {
                                item.IsWearable = enable;
                                changed = true;
                            }
                            break;
                        case "Equippable":
                            if (item.IsEquippable != enable)
                            {
                                item.IsEquippable = enable;
                                changed = true;
                            }
                            break;
                    }

                    if (changed) changedCount++;
                }
            }

            if (changedCount > 0)
            {
                StoreInventory.SaveStoreToJson();
                Messages.Message($"{(enable ? "Enabled" : "Disabled")} {changedCount} {flagType.ToLower()} items from '{GetDisplayModName(selectedModSource)}'",
                    MessageTypeDefOf.PositiveEvent);
                SoundDefOf.Click.PlayOneShotOnCamera();
                FilterItems(); // Refresh
            }
        }
        // This method shows the bulk enable menu for categories,
        private void ShowCategoryEnableMenu()
        {
            var options = new List<FloatMenuOption>();

            // Enable All in Category (this still uses the Enabled property)
            options.Add(new FloatMenuOption($"Enable All Items in {selectedCategory}", () =>
            {
                EnableCategoryItems(selectedCategory);
            }));

            options.Add(new FloatMenuOption("--- Enable Usage Types ---", null)); // Separator

            // Always show all three options
            options.Add(new FloatMenuOption($"Enable Usable Items in {selectedCategory}", () =>
            {
                ToggleCategoryItemTypeFlag(selectedCategory, "Usable", true);
            }));

            options.Add(new FloatMenuOption($"Enable Wearable Items in {selectedCategory}", () =>
            {
                ToggleCategoryItemTypeFlag(selectedCategory, "Wearable", true);
            }));

            options.Add(new FloatMenuOption($"Enable Equippable Items in {selectedCategory}", () =>
            {
                ToggleCategoryItemTypeFlag(selectedCategory, "Equippable", true);
            }));

            Find.WindowStack.Add(new FloatMenu(options));
        }
        // This method shows the bulk disable menu for categories,
        private void ShowCategoryDisableMenu()
        {
            var options = new List<FloatMenuOption>();

            // Disable All in Category (this still uses the Enabled property)
            options.Add(new FloatMenuOption($"Disable All Items in {selectedCategory}", () =>
            {
                DisableCategoryItems(selectedCategory);
            }));

            options.Add(new FloatMenuOption("--- Disable Usage Types ---", null)); // Separator

            // Always show all three options
            options.Add(new FloatMenuOption($"Disable Usable Items in {selectedCategory}", () =>
            {
                ToggleCategoryItemTypeFlag(selectedCategory, "Usable", false);
            }));

            options.Add(new FloatMenuOption($"Disable Wearable Items in {selectedCategory}", () =>
            {
                ToggleCategoryItemTypeFlag(selectedCategory, "Wearable", false);
            }));

            options.Add(new FloatMenuOption($"Disable Equippable Items in {selectedCategory}", () =>
            {
                ToggleCategoryItemTypeFlag(selectedCategory, "Equippable", false);
            }));

            Find.WindowStack.Add(new FloatMenu(options));
        }
        // This method toggles the specified item type flag (Usable, Wearable, Equippable)
        private void ToggleCategoryItemTypeFlag(string category, string flagType, bool enable)
        {
            int changedCount = 0;

            foreach (var item in StoreInventory.AllStoreItems.Values)
            {
                if (item.Category == category)
                {
                    var thingDef = DefDatabase<ThingDef>.GetNamedSilentFail(item.DefName);
                    if (thingDef == null) continue;

                    bool shouldHaveThisFlag = false;

                    // Use the same logic as DrawItemTypeCheckboxes
                    switch (flagType)
                    {
                        case "Usable":
                            shouldHaveThisFlag = StoreItem.IsItemUsable(thingDef);
                            break;

                        case "Equippable":
                            shouldHaveThisFlag = !StoreItem.IsItemUsable(thingDef) && thingDef.IsWeapon;
                            break;

                        case "Wearable":
                            shouldHaveThisFlag = !StoreItem.IsItemUsable(thingDef) && !thingDef.IsWeapon && thingDef.IsApparel;
                            break;
                    }

                    // Only toggle if this item should have this type of flag
                    if (shouldHaveThisFlag)
                    {
                        bool changed = false;

                        switch (flagType)
                        {
                            case "Usable":
                                if (item.IsUsable != enable)
                                {
                                    item.IsUsable = enable;
                                    changed = true;
                                }
                                break;

                            case "Wearable":
                                if (item.IsWearable != enable)
                                {
                                    item.IsWearable = enable;
                                    changed = true;
                                }
                                break;

                            case "Equippable":
                                if (item.IsEquippable != enable)
                                {
                                    item.IsEquippable = enable;
                                    changed = true;
                                }
                                break;
                        }

                        if (changed) changedCount++;
                    }
                }
            }

            if (changedCount > 0)
            {
                StoreInventory.SaveStoreToJson();
                Messages.Message($"{(enable ? "Enabled" : "Disabled")} {changedCount} {flagType.ToLower()} items in {category}",
                    MessageTypeDefOf.PositiveEvent);
                SoundDefOf.Click.PlayOneShotOnCamera();
                FilterItems(); // Refresh the view
            }
        }
        // This method enables all items in the specified category.
        // Deprecated: now we use the toggle method for specific types, but this can still be used for a general "Enable All".
        private void SetCategoryPrice(int price)
        {
            int changedCount = 0;
            foreach (var item in filteredItems)
            {
                if (item.BasePrice != price)
                {
                    item.BasePrice = price;
                    changedCount++;
                }
            }

            if (changedCount > 0)
            {
                StoreInventory.SaveStoreToJson();
                Messages.Message($"Set price to {price} for {changedCount} items in '{selectedCategory}' category",
                    MessageTypeDefOf.PositiveEvent);
                SoundDefOf.Click.PlayOneShotOnCamera();
            }
        }
        // This method enables all items from the selected mod source.
        // Deprecated: now we use the toggle method for specific types, but this can still be used for a general "Enable All".
        private void SetModSourcePrice(int price)
        {
            if (price <= 0)
            {
                Messages.Message("Price must be positive", MessageTypeDefOf.RejectInput);
                return;
            }

            int changedCount = 0;

            foreach (var item in filteredItems)
            {
                if (item.BasePrice != price)
                {
                    item.BasePrice = price;
                    changedCount++;
                }
            }

            if (changedCount > 0)
            {
                StoreInventory.SaveStoreToJson();
                string modName = GetDisplayModName(selectedModSource);
                Messages.Message($"Set price to {price} silver for {changedCount} items from mod '{modName}'",
                    MessageTypeDefOf.PositiveEvent);
                SoundDefOf.Click.PlayOneShotOnCamera();
            }
        }
        // This method resets all item prices in the selected category to their default values based on ThingDef.BaseMarketValue.
        private void ResetCategoryPrices()
        {
            int changedCount = 0;
            foreach (var item in filteredItems)
            {
                var thingDef = DefDatabase<ThingDef>.GetNamedSilentFail(item.DefName);
                if (thingDef != null)
                {
                    int defaultPrice = (int)thingDef.BaseMarketValue;
                    if (item.BasePrice != defaultPrice)
                    {
                        item.BasePrice = defaultPrice;
                        changedCount++;
                    }
                }
            }

            if (changedCount > 0)
            {
                StoreInventory.SaveStoreToJson();
                Messages.Message($"Reset {changedCount} items in '{selectedCategory}' to default prices",
                    MessageTypeDefOf.PositiveEvent);
                SoundDefOf.Click.PlayOneShotOnCamera();
            }
        }
        // This method resets all item prices from the selected mod source to their default values based on ThingDef.BaseMarketValue.
        private void ResetModSourcePrices()
        {
            int changedCount = 0;

            foreach (var item in filteredItems)
            {
                var thingDef = DefDatabase<ThingDef>.GetNamedSilentFail(item.DefName);
                if (thingDef != null)
                {
                    int defaultPrice = (int)thingDef.BaseMarketValue;
                    // Optional: enforce minimum price of 1
                    defaultPrice = Math.Max(1, defaultPrice);

                    if (item.BasePrice != defaultPrice)
                    {
                        item.BasePrice = defaultPrice;
                        changedCount++;
                    }
                }
            }

            if (changedCount > 0)
            {
                StoreInventory.SaveStoreToJson();
                string modName = GetDisplayModName(selectedModSource);
                Messages.Message($"Reset {changedCount} items from '{modName}' to default prices",
                    MessageTypeDefOf.PositiveEvent);
                SoundDefOf.Click.PlayOneShotOnCamera();
            }
        }
        // This dictionary is used to store temporary string buffers for numeric text fields, keyed by item DefName.
        private Dictionary<string, string> numericBuffers = new Dictionary<string, string>();
        // This method retrieves the string buffer for a given item DefName, creating it if it doesn't exist.
        private void BuildCategoryCounts()
        {
            categoryCounts.Clear();
            categoryCounts["All"] = StoreInventory.AllStoreItems.Count;

            foreach (var item in StoreInventory.AllStoreItems.Values)
            {
                if (item.Category == null)
                {
                    Logger.Warning($"[CAP] Store item '{item.DefName}' from mod '{item.ModSource}' has null category");
                }
                // Handle null categories - this is the fix!
                string categoryKey = item.Category ?? "Uncategorized";

                if (categoryCounts.ContainsKey(categoryKey))
                    categoryCounts[categoryKey]++;
                else
                    categoryCounts[categoryKey] = 1;
            }
        }
        // This method applies the current search query and view filters to the list of store items,
        // updating the filteredItems list accordingly.
        private void FilterItems()
        {
            lastSearch = searchQuery;
            filteredItems.Clear();

            var allItems = StoreInventory.AllStoreItems.Values.AsEnumerable();

            // Apply view-based filter
            if (listViewType == StoreListViewType.Category && selectedCategory != "All")
            {
                allItems = allItems.Where(item => (item.Category ?? "Uncategorized") == selectedCategory);
            }
            else if (listViewType == StoreListViewType.ModSource && selectedModSource != "All")
            {
                allItems = allItems.Where(item => GetDisplayModName(item.ModSource ?? "Unknown") == selectedModSource);
            }

            // Search filter
            if (!string.IsNullOrEmpty(searchQuery))
            {
                string searchLower = searchQuery.ToLower();
                allItems = allItems.Where(item =>
                    item.DefName.ToLower().Contains(searchLower) ||
                    (GetThingDefLabel(item.DefName) ?? "").ToLower().Contains(searchLower) ||
                    (item.Category ?? "").ToLower().Contains(searchLower) ||  // Handle null category
                    item.ModSource.ToLower().Contains(searchLower)
                );
            }

            filteredItems = allItems.ToList();
            SortItems();
        }
        // This method sorts the filteredItems list based on the current sort method and order.
        private void SortItems()
        {
            switch (sortMethod)
            {
                case StoreSortMethod.Name:
                    filteredItems = sortAscending
                        ? filteredItems.OrderBy(item => GetThingDefLabel(item.DefName)).ToList()
                        : filteredItems.OrderByDescending(item => GetThingDefLabel(item.DefName)).ToList();
                    break;

                case StoreSortMethod.Price:
                    filteredItems = sortAscending
                        ? filteredItems.OrderBy(item => item.BasePrice).ToList()
                        : filteredItems.OrderByDescending(item => item.BasePrice).ToList();
                    break;

                case StoreSortMethod.Category:
                    filteredItems = sortAscending
                        ? filteredItems.OrderBy(item => item.Category ?? "Uncategorized")
                                      .ThenBy(item => GetThingDefLabel(item.DefName))
                                      .ToList()
                        : filteredItems.OrderByDescending(item => item.Category ?? "Uncategorized")
                                      .ThenBy(item => GetThingDefLabel(item.DefName))
                                      .ToList();
                    break;

                case StoreSortMethod.ModSource:
                    filteredItems = sortAscending
                        ? filteredItems.OrderBy(item => GetDisplayModName(item.ModSource ?? "Unknown"))
                                      .ThenBy(item => GetThingDefLabel(item.DefName))
                                      .ToList()
                        : filteredItems.OrderByDescending(item => GetDisplayModName(item.ModSource ?? "Unknown"))
                                      .ThenBy(item => GetThingDefLabel(item.DefName))
                                      .ToList();
                    break;
            }
        }
        // This method retrieves the display name for a mod source, handling "RimWorld" and null/unknown cases.
        private string GetThingDefLabel(string defName)
        {
            var thingDef = DefDatabase<ThingDef>.GetNamedSilentFail(defName);
            return thingDef?.LabelCap ?? defName;
        }
        // This method retrieves a user-friendly display name for a mod source, handling "RimWorld" and null/unknown cases.
        private void SaveOriginalPrices()
        {
            originalPrices.Clear();
            foreach (var item in StoreInventory.AllStoreItems.Values)
            {
                originalPrices[item.DefName] = item.BasePrice;
            }
        }
        // This method resets all item prices to their default values based on ThingDef.BaseMarketValue.
        private void ResetAllPrices()
        {
            foreach (var item in StoreInventory.AllStoreItems.Values)
            {
                var thingDef = DefDatabase<ThingDef>.GetNamedSilentFail(item.DefName);
                if (thingDef != null)
                {
                    // Round properly and ensure minimum price of 1
                    item.BasePrice = Math.Max(1, (int)Math.Round(thingDef.BaseMarketValue));
                    // item.Enabled = true;  // Lets just reset the prices not enable them all
                }
            }
            StoreInventory.SaveStoreToJson();
            FilterItems();
        }
        // This method enables all items in the store.
        private void EnableAllItems()
        {
            foreach (var item in StoreInventory.AllStoreItems.Values)
            {
                item.Enabled = true;
            }
            StoreInventory.SaveStoreToJson();
            FilterItems();
        }
        // This method disables all items in the store.
        private void DisableAllItems()
        {
            foreach (var item in StoreInventory.AllStoreItems.Values)
            {
                item.Enabled = false;
            }
            StoreInventory.SaveStoreToJson();
            FilterItems();
        }
        // This method sets the quantity limit for all visible items based on the specified number of stacks.
        private void SetAllVisibleItemsQuantityLimit(int stacks)
        {
            int affectedCount = 0;
            foreach (var item in filteredItems)
            {
                var thingDef = DefDatabase<ThingDef>.GetNamedSilentFail(item.DefName);
                if (thingDef != null)
                {
                    int baseStack = Mathf.Max(1, thingDef.stackLimit);
                    item.QuantityLimit = Mathf.Clamp(baseStack * stacks, 1, 9999);
                    item.HasQuantityLimit = true;
                    affectedCount++;
                }
            }

            if (affectedCount > 0)
            {
                StoreInventory.SaveStoreToJson();
                Messages.Message($"Set quantity limit to {stacks} stacks for {affectedCount} items", MessageTypeDefOf.PositiveEvent);
                SoundDefOf.Click.PlayOneShotOnCamera();
            }
        }
        // This method enables or disables the quantity limit for all visible items based on the specified boolean value.
        private void EnableQuantityLimitForAllVisible(bool enable)
        {
            int affectedCount = 0;
            foreach (var item in filteredItems)
            {
                if (item.HasQuantityLimit != enable)
                {
                    item.HasQuantityLimit = enable;
                    affectedCount++;
                }
            }

            if (affectedCount > 0)
            {
                StoreInventory.SaveStoreToJson();
                Messages.Message($"{(enable ? "Enabled" : "Disabled")} quantity limit for {affectedCount} items",
                    MessageTypeDefOf.PositiveEvent);
                SoundDefOf.Click.PlayOneShotOnCamera();
            }
        }
        // Override PostClose to save store data when the dialog is closed
        public override void PostClose()
        {
            StoreInventory.SaveStoreToJson();
            base.PostClose();
        }
        // This method opens a new window displaying detailed information about the specified ThingDef and StoreItem.
        private void ShowDefInfoWindow(ThingDef thingDef, StoreItem storeItem)
        {
            Find.WindowStack.Add(new DefInfoWindow(thingDef, storeItem));
        }
    }
    // This class represents a window that displays detailed information about a specific ThingDef and its corresponding StoreItem.
    public class DefInfoWindow : Window
    {
        private ThingDef thingDef;
        private StoreItem storeItem;
        private Vector2 scrollPosition = Vector2.zero;
        private string customNameBuffer = "";

        public override Vector2 InitialSize => new Vector2(600f, 700f);

        public DefInfoWindow(ThingDef thingDef, StoreItem storeItem)
        {
            this.thingDef = thingDef;
            this.storeItem = storeItem;
            this.customNameBuffer = storeItem.CustomName ?? "";
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
            Widgets.Label(titleRect, $"Def Information: {thingDef.LabelCap}");
            Text.Font = GameFont.Small;

            // Content area
            Rect contentRect = new Rect(0f, 40f, inRect.width, inRect.height - 40f - CloseButSize.y);
            DrawDefInfo(contentRect);
        }

        private void DrawDefInfo(Rect rect)
        {
            StringBuilder sb = new StringBuilder();

            // Always show at top
            sb.AppendLine($"DefName: {thingDef.defName}");
            sb.AppendLine($"BaseMarketValue: {thingDef.BaseMarketValue}");
            sb.AppendLine($"");

            // ThingDef properties
            sb.AppendLine($"thingClass: {thingDef.thingClass?.Name ?? "null"}");
            sb.AppendLine($"stackLimit: {thingDef.stackLimit}");
            sb.AppendLine($"Size: {thingDef.size}");
            sb.AppendLine($"TechLevel: {thingDef.techLevel}");
            sb.AppendLine($"Tradeability: {thingDef.tradeability}");

            // Boolean properties - only show if true
            if (thingDef.IsIngestible) sb.AppendLine($"IsIngestible: {thingDef.IsIngestible}");
            if (thingDef.IsMedicine) sb.AppendLine($"IsMedicine: {thingDef.IsMedicine}");
            if (thingDef.IsStuff) sb.AppendLine($"IsStuff: {thingDef.IsStuff}");
            if (thingDef.IsDrug) sb.AppendLine($"IsDrug: {thingDef.IsDrug}");
            if (thingDef.IsPleasureDrug) sb.AppendLine($"IsPleasureDrug: {thingDef.IsPleasureDrug}");
            if (thingDef.IsNonMedicalDrug) sb.AppendLine($"IsNonMedicalDrug: {thingDef.IsNonMedicalDrug}");
            if (thingDef.IsApparel) sb.AppendLine($"IsApparel: {thingDef.IsApparel}");
            if (thingDef.Claimable) sb.AppendLine($"Claimable: {thingDef.Claimable}");
            if (thingDef.IsWeapon) sb.AppendLine($"IsWeapon: {thingDef.IsWeapon}");
            if (thingDef.IsBuildingArtificial) sb.AppendLine($"IsBuildingArtificial: {thingDef.IsBuildingArtificial}");

            // MINIFICATION PROPERTIES - Always show these
            sb.AppendLine($"Minifiable: {thingDef.Minifiable}");
            if (thingDef.Minifiable)
            {
                sb.AppendLine($"  - Will be delivered as minified crate");
            }

            if (thingDef.smeltable) sb.AppendLine($"Smeltable: {thingDef.smeltable}");

            sb.AppendLine($"");

            // Delivery Information
            sb.AppendLine($"--- Delivery Information ---");
            sb.AppendLine($"Will Minify: {ShouldMinifyForDelivery(thingDef)}");
            sb.AppendLine($"Stack Limit: {thingDef.stackLimit}");
            sb.AppendLine($"Size: {thingDef.size}");
            sb.AppendLine($"");

            // List comps if the thing has them
            if (thingDef.comps != null && thingDef.comps.Count > 0)
            {
                sb.AppendLine($"--- Comp Properties ({thingDef.comps.Count}) ---");
                foreach (var compProps in thingDef.comps)
                {
                    sb.AppendLine($"• {compProps.compClass?.Name ?? "null"}");

                    // Show detailed CompProperties_UseEffect information
                    if (compProps is CompProperties_UseEffect compUseEffect)
                    {
                        sb.AppendLine($"  - UseEffect Type: {compUseEffect.GetType().Name}");
                        if (compUseEffect.doCameraShake) sb.AppendLine($"  - Camera Shake: Yes");
                        if (compUseEffect.moteOnUsed != null) sb.AppendLine($"  - Mote On Used: {compUseEffect.moteOnUsed.defName}");
                        if (compUseEffect.moteOnUsedScale != 1f) sb.AppendLine($"  - Mote Scale: {compUseEffect.moteOnUsedScale}");
                        if (compUseEffect.fleckOnUsed != null) sb.AppendLine($"  - Fleck On Used: {compUseEffect.fleckOnUsed.defName}");
                        if (compUseEffect.fleckOnUsedScale != 1f) sb.AppendLine($"  - Fleck Scale: {compUseEffect.fleckOnUsedScale}");
                        if (compUseEffect.effecterOnUsed != null) sb.AppendLine($"  - Effecter On Used: {compUseEffect.effecterOnUsed.defName}");
                        if (compUseEffect.warmupEffecter != null) sb.AppendLine($"  - Warmup Effecter: {compUseEffect.warmupEffecter.defName}");
                    }
                    else if (compProps is CompProperties_FoodPoisonable compFoodPoison)
                    {
                        sb.AppendLine($"  - FoodPoisonable");
                    }
                    // Add more comp type checks as needed
                }
                sb.AppendLine($"");
            }

            // Ingestible properties if exists
            if (thingDef.ingestible != null)
            {
                sb.AppendLine($"--- Ingestible Properties ---");
                sb.AppendLine($"Nutrition: {thingDef.ingestible.CachedNutrition}");
                sb.AppendLine($"FoodType: {thingDef.ingestible.foodType}");
                sb.AppendLine($"Preferability: {thingDef.ingestible.preferability}");
                sb.AppendLine($"");
            }

            // Apparel properties if exists
            if (thingDef.apparel != null)
            {
                sb.AppendLine($"--- Apparel Properties ---");
                sb.AppendLine($"Layers: {string.Join(", ", thingDef.apparel.layers)}");
                sb.AppendLine($"BodyPartGroups: {string.Join(", ", thingDef.apparel.bodyPartGroups?.Select(g => g.defName) ?? new List<string>())}");
                sb.AppendLine($"");
            }

            // Weapon properties if exists
            if (thingDef.weaponTags != null && thingDef.weaponTags.Count > 0)
            {
                sb.AppendLine($"--- Weapon Properties ---");
                sb.AppendLine($"WeaponTags: {string.Join(", ", thingDef.weaponTags)}");
                sb.AppendLine($"");
            }

            // StoreItem information
            sb.AppendLine($"--- Store Item Data ---");
            sb.AppendLine($"Custom Name: {storeItem.CustomName}");
            sb.AppendLine($"Base Price: {storeItem.BasePrice}");
            sb.AppendLine($"Enabled: {storeItem.Enabled}");
            sb.AppendLine($"Category: {storeItem.Category}");
            sb.AppendLine($"Mod Source: {storeItem.ModSource}");
            sb.AppendLine($"IsUsable: {storeItem.IsUsable}");
            sb.AppendLine($"IsWearable: {storeItem.IsWearable}");
            sb.AppendLine($"IsEquippable: {storeItem.IsEquippable}");
            sb.AppendLine($"HasQuantityLimit: {storeItem.HasQuantityLimit}");
            sb.AppendLine($"QuantityLimit: {storeItem.QuantityLimit}");

            string fullText = sb.ToString();

            // Custom name editor - positioned at the top
            Rect customNameRect = new Rect(rect.x, rect.y, rect.width, 30f);

            // Label
            Rect labelRect = new Rect(customNameRect.x, customNameRect.y, 100f, 30f);
            Widgets.Label(labelRect, "Custom Name:");

            // Text input
            Rect inputRect = new Rect(customNameRect.x + 105f, customNameRect.y, rect.width - 215f, 30f);
            customNameBuffer = Widgets.TextField(inputRect, customNameBuffer);

            // Clear button (only show if there's a custom name)
            if (!string.IsNullOrEmpty(storeItem.CustomName))
            {
                Rect clearButtonRect = new Rect(customNameRect.x + rect.width - 210f, customNameRect.y, 100f, 30f);
                if (Widgets.ButtonText(clearButtonRect, "Clear"))
                {
                    storeItem.CustomName = null;
                    customNameBuffer = "";
                    StoreInventory.SaveStoreToJson();
                    Messages.Message("Custom name cleared", MessageTypeDefOf.PositiveEvent);
                    SoundDefOf.Click.PlayOneShotOnCamera();
                }
            }

            // Save button
            Rect saveButtonRect = new Rect(customNameRect.x + rect.width - 105f, customNameRect.y, 100f, 30f);
            bool canSave = !string.IsNullOrWhiteSpace(customNameBuffer) && customNameBuffer != storeItem.CustomName;

            // Disable save button if name is duplicate
            if (IsCustomNameDuplicate(customNameBuffer))
            {
                GUI.color = Color.gray;
                if (Widgets.ButtonText(saveButtonRect, "Save"))
                {
                    Messages.Message("Cannot save: Custom name is already in use by another item",
                        MessageTypeDefOf.RejectInput);
                }
                GUI.color = Color.white;
                TooltipHandler.TipRegion(saveButtonRect, "Cannot save: This name is already in use by another item");
            }
            else if (Widgets.ButtonText(saveButtonRect, "Save") && canSave)
            {
                storeItem.CustomName = customNameBuffer.Trim();
                StoreInventory.SaveStoreToJson();
                Messages.Message($"Custom name updated to '{storeItem.CustomName}'", MessageTypeDefOf.PositiveEvent);
                SoundDefOf.Click.PlayOneShotOnCamera();
            }
            else if (!canSave)
            {
                GUI.color = Color.gray;
                Widgets.ButtonText(saveButtonRect, "Save");
                GUI.color = Color.white;
                TooltipHandler.TipRegion(saveButtonRect, "No changes to save");
            }

            // Warning message below the input field (if duplicate)
            if (!string.IsNullOrWhiteSpace(customNameBuffer) && IsCustomNameDuplicate(customNameBuffer))
            {
                Rect warningRect = new Rect(rect.x + 105f, rect.y + 35f, rect.width - 110f, 20f);
                Text.Anchor = TextAnchor.UpperLeft;
                GUI.color = ColorLibrary.HeaderAccent;
                Widgets.Label(warningRect, "⚠ Warning: This name is already in use by another item");
                GUI.color = Color.white;
                Text.Anchor = TextAnchor.UpperLeft;

                // Adjust scroll view position to account for warning message
                Rect scrollRect = new Rect(rect.x, rect.y + 60f, rect.width, rect.height - 60f);

                // Calculate text height
                float textHeight = Text.CalcHeight(fullText, rect.width - 20f);
                Rect viewRect = new Rect(0f, 0f, rect.width - 20f, textHeight);

                // Scroll view
                Widgets.BeginScrollView(scrollRect, ref scrollPosition, viewRect);
                Widgets.Label(new Rect(0f, 0f, viewRect.width, textHeight), fullText);
                Widgets.EndScrollView();
            }
            else
            {
                // Adjust scroll view to account for custom name editor
                Rect scrollRect = new Rect(rect.x, rect.y + 35f, rect.width, rect.height - 35f);

                // Calculate text height
                float textHeight = Text.CalcHeight(fullText, rect.width - 20f);
                Rect viewRect = new Rect(0f, 0f, rect.width - 20f, textHeight);

                // Scroll view
                Widgets.BeginScrollView(scrollRect, ref scrollPosition, viewRect);
                Widgets.Label(new Rect(0f, 0f, viewRect.width, textHeight), fullText);
                Widgets.EndScrollView();
            }
        }

        // Helper method to check minification (same logic as StoreCommandHelper)
        private bool ShouldMinifyForDelivery(ThingDef thingDef)
        {
            return thingDef != null && thingDef.Minifiable;
        }
        // Method to check for duplicate custom names
        private HashSet<string> GetAllExistingNames()
        {
            var existingNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var item in StoreInventory.AllStoreItems.Values)
            {
                // Add custom names
                if (!string.IsNullOrEmpty(item.CustomName))
                    existingNames.Add(item.CustomName);

                // Add def names
                existingNames.Add(item.DefName);

                // Add label caps
                var thingDef = DefDatabase<ThingDef>.GetNamedSilentFail(item.DefName);
                if (thingDef != null)
                    existingNames.Add(thingDef.LabelCap.ToString());
            }

            return existingNames;
        }

        private bool IsCustomNameDuplicate(string customName)
        {
            if (string.IsNullOrWhiteSpace(customName))
                return false;

            var existingNames = GetAllExistingNames();

            // Remove current item's names from the set to allow editing
            if (!string.IsNullOrEmpty(storeItem.CustomName))
                existingNames.Remove(storeItem.CustomName);

            existingNames.Remove(storeItem.DefName);

            var currentThingDef = DefDatabase<ThingDef>.GetNamedSilentFail(storeItem.DefName);
            if (currentThingDef != null)
            {
                string currentLabelCap = currentThingDef.LabelCap.ToString();
                existingNames.Remove(currentLabelCap);

                // Also remove the custom name if it's the same as LabelCap (default assignment)
                if (storeItem.CustomName == currentLabelCap)
                    existingNames.Remove(storeItem.CustomName);
            }

            return existingNames.Contains(customName);
        }
    }
    // Enums for sorting and view types
    public enum StoreSortMethod
    {
        Name,
        Price,
        Category,
        ModSource
    }
    // This enum defines the different ways the store item list can be filtered or grouped in the UI.
    public enum StoreListViewType
    {
        ModSource,
        Category
    }
}