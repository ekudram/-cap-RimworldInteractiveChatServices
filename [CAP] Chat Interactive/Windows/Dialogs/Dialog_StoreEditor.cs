// Dialog_StoreEditor.cs
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

        public override Vector2 InitialSize => new Vector2(1200f, 755f);

        public Dialog_StoreEditor()
        {
            doCloseButton = true;
            forcePause = true;
            absorbInputAroundWindow = true;

            BuildCategoryCounts();
            FilterItems();
            SaveOriginalPrices();
        }

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

        private void DrawHeader(Rect rect)
        {
            Widgets.BeginGroup(rect);

            // Custom title with larger font and underline effect - similar to PawnQueue
            Text.Font = GameFont.Medium;
            GUI.color = ColorLibrary.Orange;
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

            // Search bar with label - similar to PawnQueue
            Rect searchLabelRect = new Rect(0f, controlsY, 80f, controlsHeight);
            Text.Font = GameFont.Medium; // Medium font for the label
            Widgets.Label(searchLabelRect, "Search:");
            Text.Font = GameFont.Small;

            Rect searchRect = new Rect(85f, controlsY, 250f, controlsHeight);
            searchQuery = Widgets.TextField(searchRect, searchQuery);

            // Sort buttons - adjusted position
            Rect sortRect = new Rect(345f, controlsY, 400f, controlsHeight);
            DrawSortButtons(sortRect);

            // Action buttons - adjusted position
            Rect actionsRect = new Rect(695f, controlsY, 430f, controlsHeight);
            DrawActionButtons(actionsRect);

            Widgets.EndGroup();
        }

        private void DrawSortButtons(Rect rect)
        {
            Widgets.BeginGroup(rect);

            float buttonWidth = 80f;
            float spacing = 5f;
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

            // Sort by Category
            if (Widgets.ButtonText(new Rect(x, 0f, buttonWidth, 30f), "Category"))
            {
                if (sortMethod == StoreSortMethod.Category)
                    sortAscending = !sortAscending;
                else
                    sortMethod = StoreSortMethod.Category;
                SortItems();
            }

            // Sort indicator
            string sortIndicator = sortAscending ? " ↑" : " ↓";
            Rect indicatorRect = new Rect(x + buttonWidth + 10f, 8f, 50f, 20f);
            Widgets.Label(indicatorRect, sortIndicator);

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
                StoreInventory.OpenQualitySettings();
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
                if (item.Category == category && !item.Enabled)
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
                if (item.Category == category && item.Enabled)
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

        private void DrawContent(Rect rect)
        {
            // Add 2px padding to the left side
            float padding = 2f;
            rect.x += padding;
            rect.width -= padding;

            // Split into categories (left) and items (right)
            float categoryWidth = 200f;
            float itemsWidth = rect.width - categoryWidth - 10f;

            Rect categoryRect = new Rect(rect.x, rect.y, categoryWidth, rect.height);
            Rect itemsRect = new Rect(rect.x + categoryWidth + 10f, rect.y, itemsWidth, rect.height);

            DrawCategoryList(categoryRect);
            DrawItemList(itemsRect);
        }

        private void DrawCategoryList(Rect rect)
        {
            // Background
            Widgets.DrawMenuSection(rect);

            // Header
            Rect headerRect = new Rect(rect.x, rect.y, rect.width, 30f);
            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(headerRect, "Categories");
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;

            // Category list
            Rect listRect = new Rect(rect.x, rect.y + 35f, rect.width, rect.height - 35f);
            Rect viewRect = new Rect(0f, 0f, listRect.width - 20f, categoryCounts.Count * 30f);

            Widgets.BeginScrollView(listRect, ref categoryScrollPosition, viewRect);
            {
                float y = 0f;
                // Custom sort order for cleaner sidebar
                var orderedCategories = categoryCounts.Keys
                    .OrderBy(cat =>
                    {
                        // Keep "All" at the very top
                        if (cat == "All") return 0;
                        // Group Apparel categories together
                        if (cat == "Apparel") return 1;
                        if (cat == "Children's Apparel") return 2;
                        // Everything else alphabetically after
                        return 3;
                    })
                    .ThenBy(cat => cat)
                    .ToList();

                foreach (var cat in orderedCategories)
                {
                    var count = categoryCounts[cat];
                    Rect categoryButtonRect = new Rect(2f, y, viewRect.width-4f, 28f);

                    // Highlight selected category
                    if (selectedCategory == cat)
                        Widgets.DrawHighlightSelected(categoryButtonRect);
                    else if (Mouse.IsOver(categoryButtonRect))
                        Widgets.DrawHighlight(categoryButtonRect);

                    string label = $"{cat} ({count})";
                    if (Widgets.ButtonText(categoryButtonRect, label))
                    {
                        selectedCategory = cat;
                        FilterItems();
                    }

                    y += 30f;
                }
            }
            Widgets.EndScrollView();
        }

        private void DrawItemList(Rect rect)
        {
            // Background
            Widgets.DrawMenuSection(rect);

            // Header with item count
            Rect headerRect = new Rect(rect.x, rect.y, rect.width, 30f);
            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.MiddleCenter; // Add this to center the text
            string headerText = $"Items ({filteredItems.Count})";
            if (selectedCategory != "All") headerText += $" - {selectedCategory}";
            Widgets.Label(headerRect, headerText);
            Text.Anchor = TextAnchor.UpperLeft; // Reset anchor
            Text.Font = GameFont.Small;

            // Item list with virtual scrolling
            Rect listRect = new Rect(rect.x, rect.y + 35f, rect.width, rect.height - 35f);
            float rowHeight = 60f;

            // Handle empty filtered items case
            if (filteredItems.Count == 0)
            {
                Rect emptyRect = new Rect(listRect.x, listRect.y, listRect.width, 30f);
                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(emptyRect, "No items found");
                Text.Anchor = TextAnchor.UpperLeft;
                return;
            }

            // Calculate visible range
            int firstVisibleIndex = Mathf.FloorToInt(scrollPosition.y / rowHeight);
            int lastVisibleIndex = Mathf.CeilToInt((scrollPosition.y + listRect.height) / rowHeight);
            firstVisibleIndex = Mathf.Clamp(firstVisibleIndex, 0, filteredItems.Count - 1);
            lastVisibleIndex = Mathf.Clamp(lastVisibleIndex, 0, filteredItems.Count - 1);

            // Only create viewRect for visible items
            Rect viewRect = new Rect(0f, 0f, listRect.width - 20f, filteredItems.Count * rowHeight);

            Widgets.BeginScrollView(listRect, ref scrollPosition, viewRect);
            {
                float y = firstVisibleIndex * rowHeight;
                for (int i = firstVisibleIndex; i <= lastVisibleIndex; i++)
                {
                    Rect itemRect = new Rect(0f, y, viewRect.width, rowHeight - 2f);
                    if (i % 2 == 1)
                    {
                        Widgets.DrawLightHighlight(itemRect);
                    }

                    DrawItemRow(itemRect, filteredItems[i], i);
                    y += rowHeight;
                }
            }
            Widgets.EndScrollView();
        }

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

                    Widgets.InfoCardButton(iconRect.xMax + 2f, iconRect.y, thingDef);
                }
                x += 80f;

                // === Info text === 
                float infoWidth = 210f; // Fixed reasonable width for item info
                Rect infoRect = new Rect(x, 5f, infoWidth, 50f);
                Text.Anchor = TextAnchor.MiddleLeft;
                string itemName = thingDef?.LabelCap ?? item.DefName;
                string categoryInfo = $"{item.Category} • {item.ModSource}";
                Widgets.Label(infoRect.TopHalf(), itemName);
                Text.Font = GameFont.Tiny;
                Widgets.Label(infoRect.BottomHalf(), categoryInfo);
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
                    Widgets.TextFieldNumeric(numRect, ref limit, ref buffer, 1, 9999);
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
            Widgets.TextFieldNumeric(inputRect, ref currentPrice, ref priceBuffer, 0, 1000000);

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
                    item.BasePrice = (int)(thingDef.BaseMarketValue * 1.67f);
                    SoundDefOf.Checkbox_TurnedOn.PlayOneShotOnCamera(); // ✅ RimWorld native sound
                    StoreInventory.SaveStoreToJson();
                }
            }

            Widgets.EndGroup();
        }

        private void DrawQuantityLimitControls(Rect rect, StoreItem item)
        {
            Widgets.BeginGroup(rect);

            float labelWidth = 100f;
            float checkboxWidth = 20f;
            float numericWidth = 50f;
            float spacing = 5f;

            // Checkbox for enabling/disabling quantity limit
            Rect checkboxRect = new Rect(0f, 0f, checkboxWidth, 30f);
            bool hasLimit = item.HasQuantityLimit;
            Widgets.Checkbox(checkboxRect.position, ref hasLimit, 24f);
            if (hasLimit != item.HasQuantityLimit)
            {
                item.HasQuantityLimit = hasLimit;
                StoreInventory.SaveStoreToJson();
            }

            // Label next to the checkbox
            Rect labelRect = new Rect(checkboxRect.xMax + spacing, 0f, labelWidth, 30f);
            Widgets.Label(labelRect, "Qty Limit");

            // Numeric field (active only if limit is on)
            if (item.HasQuantityLimit)
            {
                Rect inputRect = new Rect(labelRect.xMax + spacing, 0f, numericWidth, 30f);
                int limit = item.QuantityLimit;
                string buffer = limit.ToString();

                Widgets.TextFieldNumeric(inputRect, ref limit, ref buffer, 1, 9999); // clamp between 1 and 9999
                if (limit != item.QuantityLimit)
                {
                    item.QuantityLimit = limit;
                    StoreInventory.SaveStoreToJson();
                }

                // Optional: small "+" and "–" buttons for quick adjustment
                float buttonSize = 20f;
                Rect minusRect = new Rect(inputRect.xMax + 2f, 5f, buttonSize, buttonSize);
                Rect plusRect = new Rect(minusRect.xMax + 2f, 5f, buttonSize, buttonSize);
                if (Widgets.ButtonText(minusRect, "-"))
                {
                    item.QuantityLimit = Mathf.Max(1, item.QuantityLimit - 1);
                    StoreInventory.SaveStoreToJson();
                }
                if (Widgets.ButtonText(plusRect, "+"))
                {
                    item.QuantityLimit = Mathf.Min(9999, item.QuantityLimit + 1);
                    StoreInventory.SaveStoreToJson();
                }
            }

            Widgets.EndGroup();
        }

        private void BuildCategoryCounts()
        {
            categoryCounts.Clear();
            categoryCounts["All"] = StoreInventory.AllStoreItems.Count;

            foreach (var item in StoreInventory.AllStoreItems.Values)
            {
                if (categoryCounts.ContainsKey(item.Category))
                    categoryCounts[item.Category]++;
                else
                    categoryCounts[item.Category] = 1;
            }
        }

        private void FilterItems()
        {
            lastSearch = searchQuery;
            filteredItems.Clear();

            var allItems = StoreInventory.AllStoreItems.Values.AsEnumerable();

            // Category filter
            if (selectedCategory != "All")
            {
                allItems = allItems.Where(item => item.Category == selectedCategory);
            }

            // Search filter
            if (!string.IsNullOrEmpty(searchQuery))
            {
                string searchLower = searchQuery.ToLower();
                allItems = allItems.Where(item =>
                    item.DefName.ToLower().Contains(searchLower) ||
                    (GetThingDefLabel(item.DefName) ?? "").ToLower().Contains(searchLower) ||
                    item.Category.ToLower().Contains(searchLower) ||
                    item.ModSource.ToLower().Contains(searchLower)
                );
            }

            filteredItems = allItems.ToList();
            SortItems();
        }

        private void SortItems()
        {
            switch (sortMethod)
            {
                case StoreSortMethod.Name:
                    filteredItems = sortAscending ?
                        filteredItems.OrderBy(item => GetThingDefLabel(item.DefName)).ToList() :
                        filteredItems.OrderByDescending(item => GetThingDefLabel(item.DefName)).ToList();
                    break;
                case StoreSortMethod.Price:
                    filteredItems = sortAscending ?
                        filteredItems.OrderBy(item => item.BasePrice).ToList() :
                        filteredItems.OrderByDescending(item => item.BasePrice).ToList();
                    break;
                case StoreSortMethod.Category:
                    filteredItems = sortAscending ?
                        filteredItems.OrderBy(item => item.Category).ThenBy(item => GetThingDefLabel(item.DefName)).ToList() :
                        filteredItems.OrderByDescending(item => item.Category).ThenBy(item => GetThingDefLabel(item.DefName)).ToList();
                    break;
            }
        }

        private string GetThingDefLabel(string defName)
        {
            var thingDef = DefDatabase<ThingDef>.GetNamedSilentFail(defName);
            return thingDef?.LabelCap ?? defName;
        }

        private void SaveOriginalPrices()
        {
            originalPrices.Clear();
            foreach (var item in StoreInventory.AllStoreItems.Values)
            {
                originalPrices[item.DefName] = item.BasePrice;
            }
        }

        private void ResetAllPrices()
        {
            foreach (var item in StoreInventory.AllStoreItems.Values)
            {
                var thingDef = DefDatabase<ThingDef>.GetNamedSilentFail(item.DefName);
                if (thingDef != null)
                {
                    item.BasePrice = (int)(thingDef.BaseMarketValue * 1.67f);
                    item.Enabled = true;
                }
            }
            StoreInventory.SaveStoreToJson();
            FilterItems();
        }

        private void EnableAllItems()
        {
            foreach (var item in StoreInventory.AllStoreItems.Values)
            {
                item.Enabled = true;
            }
            StoreInventory.SaveStoreToJson();
            FilterItems();
        }

        private void DisableAllItems()
        {
            foreach (var item in StoreInventory.AllStoreItems.Values)
            {
                item.Enabled = false;
            }
            StoreInventory.SaveStoreToJson();
            FilterItems();
        }

        public override void PostClose()
        {
            // Auto-save any changes when window closes
            StoreInventory.SaveStoreToJson();
            base.PostClose();
        }

        private void ShowDefInfoWindow(ThingDef thingDef, StoreItem storeItem)
        {
            Find.WindowStack.Add(new DefInfoWindow(thingDef, storeItem));
        }
    }

    public class DefInfoWindow : Window
    {
        private ThingDef thingDef;
        private StoreItem storeItem;
        private Vector2 scrollPosition = Vector2.zero;

        public override Vector2 InitialSize => new Vector2(600f, 700f);

        public DefInfoWindow(ThingDef thingDef, StoreItem storeItem)
        {
            this.thingDef = thingDef;
            this.storeItem = storeItem;
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
            if (thingDef.Minifiable) sb.AppendLine($"Minifiable: {thingDef.Minifiable}");
            if (thingDef.smeltable) sb.AppendLine($"Smeltable: {thingDef.smeltable}");

            sb.AppendLine($"");

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

            // StoreItem information
            sb.AppendLine($"--- Store Item Data ---");
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

            // Calculate text height
            float textHeight = Text.CalcHeight(fullText, rect.width - 20f);
            Rect viewRect = new Rect(0f, 0f, rect.width - 20f, textHeight);

            // Scroll view
            Widgets.BeginScrollView(rect, ref scrollPosition, viewRect);
            Widgets.Label(new Rect(0f, 0f, viewRect.width, textHeight), fullText);
            Widgets.EndScrollView();
        }
    }

    public enum StoreSortMethod
    {
        Name,
        Price,
        Category
    }
}