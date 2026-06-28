// Dialog_TraitsEditor.cs
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
//  A dialog window for editing buyable traits in the game

using CAP_ChatInteractive.Traits;
using Newtonsoft.Json;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace CAP_ChatInteractive
{
    public class Dialog_TraitsEditor : Window
    {
        private Vector2 scrollPosition = Vector2.zero;
        private Vector2 categoryScrollPosition = Vector2.zero;
        private string searchQuery = "";
        private string lastSearch = "";
        private TraitsSortMethod sortMethod = TraitsSortMethod.Name;
        private bool sortAscending = true;
        private string selectedModSource = "All";
        private Dictionary<string, int> modSourceCounts = new Dictionary<string, int>();
        private List<BuyableTrait> filteredTraits = new List<BuyableTrait>();
        private Dictionary<string, (int addPrice, int removePrice)> originalPrices = new Dictionary<string, (int, int)>();
        private string addAllPriceBuffer = "";
        private int addAllPriceValue = 0;
        private string removeAllPriceBuffer = "";
        private int removeAllPriceValue = 0;

        public override Vector2 InitialSize => new Vector2(1300f, 800f);

        public Dialog_TraitsEditor()
        {
            doCloseButton = false;
            forcePause = true;
            absorbInputAroundWindow = true;
            ///optionalTitle = "Traits Editor";

            BuildModSourceCounts();
            FilterTraits();
            SaveOriginalPrices();
        }

        public override void DoWindowContents(Rect inRect)
        {
            if (searchQuery != lastSearch || filteredTraits.Count == 0)
            {
                FilterTraits();
            }

            float bottomBarHeight = 50f; // Space for the 6-button bar

            // Header - increased height to accommodate two rows (matching other dialogs)
            Rect headerRect = new Rect(0f, 0f, inRect.width, 70f);
            DrawHeader(headerRect);

            // Main content area — leave room at bottom for button bar
            Rect contentRect = new Rect(0f, 75f, inRect.width, inRect.height - 75f - bottomBarHeight);
            DrawContent(contentRect);

            // ========== BOTTOM BUTTON BAR (Save Backup | Load Backup | Save As... | Load file | Delete file | Close) ==========
            // WHY: Consistent with Command Manager, Events Editor, and Store Editor. Uses reusable BackupUtility + Dialog_TextInput.
            float btnH = 38f;
            float btnW = 130f;
            float gap = 6f;
            float padding = 10f;
            float currentY = inRect.yMax - bottomBarHeight + (bottomBarHeight - btnH) / 2f;

            // Save Backup (quick timestamped)
            Rect saveRect = new Rect(padding, currentY, btnW, btnH);
            if (Widgets.ButtonText(saveRect, "RICS.Editor.SaveBackup".Translate()))
            {
                string json = JsonConvert.SerializeObject(TraitsManager.AllBuyableTraits, Formatting.Indented);
                BackupUtility.SaveQuickBackup("TraitsEditor", json);
                Messages.Message("RICS.TraitsEditor.BackupSaved".Translate(), MessageTypeDefOf.NeutralEvent);
            }

            // Load Backup (latest timestamped)
            float loadX = padding + btnW + gap;
            Rect loadRect = new Rect(loadX, currentY, btnW, btnH);
            if (Widgets.ButtonText(loadRect, "RICS.Editor.LoadBackup".Translate()))
            {
                string json = BackupUtility.LoadLatestTimestampedBackup("TraitsEditor");
                if (!string.IsNullOrEmpty(json))
                {
                    try
                    {
                        var loaded = JsonConvert.DeserializeObject<Dictionary<string, BuyableTrait>>(json);
                        if (loaded != null)
                        {
                            TraitsManager.AllBuyableTraits.Clear();
                            foreach (var kvp in loaded)
                                TraitsManager.AllBuyableTraits[kvp.Key] = kvp.Value;

                            TraitsManager.SaveTraitsToJson();
                            BuildModSourceCounts();
                            FilterTraits();

                            Messages.Message("RICS.TraitsEditor.Loaded".Translate(), MessageTypeDefOf.NeutralEvent);
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Error($"Failed to apply traits backup: {ex.Message}");
                        Messages.Message("RICS.TraitsEditor.FailedLoad".Translate(), MessageTypeDefOf.RejectInput);
                    }
                }
                else
                {
                    Messages.Message("RICS.TraitsEditor.NoBackups".Translate(), MessageTypeDefOf.RejectInput);
                }
            }

            // Save As... (uses Dialog_TextInput for custom name)
            float saveAsX = loadX + btnW + gap;
            Rect saveAsRect = new Rect(saveAsX, currentY, btnW, btnH);
            if (Widgets.ButtonText(saveAsRect, "RICS.Editor.SaveAs".Translate()))
            {
                ShowSaveAsMenu();
            }

            // Load file
            float loadFileX = saveAsX + btnW + gap;
            Rect loadFileRect = new Rect(loadFileX, currentY, btnW, btnH);
            if (Widgets.ButtonText(loadFileRect, "RICS.Editor.LoadFile".Translate()))
            {
                ShowLoadFileMenu();
            }

            // Delete file (right next to Load file)
            float deleteX = loadFileX + btnW + gap;
            Rect deleteRect = new Rect(deleteX, currentY, btnW, btnH);
            if (Widgets.ButtonText(deleteRect, "RICS.Editor.DeleteFile".Translate()))
            {
                ShowDeleteFileMenu();
            }

            // Close (right-aligned)
            float closeX = inRect.xMax - btnW - padding;
            Rect closeRect = new Rect(closeX, currentY, btnW, btnH);
            if (Widgets.ButtonText(closeRect, "RICS.Editor.Close".Translate()))
            {
                this.Close();
            }
        }

        private void DrawHeader(Rect rect)
        {
            Widgets.BeginGroup(rect);

            // Custom title with larger font and underline effect - matching other dialogs
            Text.Font = GameFont.Medium;
            GUI.color = ColorLibrary.HeaderAccent;
            Rect titleRect = new Rect(0f, 0f, 400f, 35f);
            Widgets.Label(titleRect, "RICS.TraitsEditor.Title".Translate());

            // Draw underline
            Rect underlineRect = new Rect(titleRect.x, titleRect.yMax - 2f, titleRect.width, 2f);
            Widgets.DrawLineHorizontal(underlineRect.x, underlineRect.y, underlineRect.width);

            Text.Font = GameFont.Small;
            GUI.color = Color.white;

            // Second row for controls - positioned below the title
            float controlsY = titleRect.yMax + 5f;
            float controlsHeight = 30f;

            // Search bar with label - matching other dialogs
            Rect searchLabelRect = new Rect(0f, controlsY, 80f, controlsHeight);
            Text.Font = GameFont.Medium; // Medium font for the label
            Widgets.Label(searchLabelRect, "RICS.TraitsEditor.Search".Translate());
            Text.Font = GameFont.Small;

            Rect searchRect = new Rect(85f, controlsY, 250f, controlsHeight);
            searchQuery = Widgets.TextField(searchRect, searchQuery);

            // Sort buttons - adjusted position
            Rect sortRect = new Rect(345f, controlsY, 420f, controlsHeight);
            DrawSortButtons(sortRect);

            // Action buttons - adjusted position
            Rect actionsRect = new Rect(775f, controlsY, 400f, controlsHeight); // Moved left from 900f to 775f
            DrawActionButtons(actionsRect);

            Widgets.EndGroup();
        }

        private void DrawSortButtons(Rect rect)
        {
            Widgets.BeginGroup(rect);

            float buttonWidth = 100f;
            float spacing = 5f;
            float x = 0f;

            // Use UIUtilities for buttons that might need truncation
            if (UIUtilities.ButtonWithTruncation(new Rect(x, 0f, buttonWidth, 30f), "RICS.TraitsEditor.Name".Translate()))
            {
                if (sortMethod == TraitsSortMethod.Name)
                    sortAscending = !sortAscending;
                else
                    sortMethod = TraitsSortMethod.Name;
                SortTraits();
            }
            x += buttonWidth + spacing;

            if (UIUtilities.ButtonWithTruncation(new Rect(x, 0f, buttonWidth, 30f), "RICS.TraitsEditor.AddPrice".Translate()))
            {
                if (sortMethod == TraitsSortMethod.AddPrice)
                    sortAscending = !sortAscending;
                else
                    sortMethod = TraitsSortMethod.AddPrice;
                SortTraits();
            }
            x += buttonWidth + spacing;

            if (UIUtilities.ButtonWithTruncation(new Rect(x, 0f, buttonWidth, 30f), "RICS.TraitsEditor.RemovePrice".Translate()))
            {
                if (sortMethod == TraitsSortMethod.RemovePrice)
                    sortAscending = !sortAscending;
                else
                    sortMethod = TraitsSortMethod.RemovePrice;
                SortTraits();
            }
            x += buttonWidth + spacing;

            if (UIUtilities.ButtonWithTruncation(new Rect(x, 0f, buttonWidth, 30f), "RICS.TraitsEditor.Source".Translate()))
            {
                if (sortMethod == TraitsSortMethod.ModSource)
                    sortAscending = !sortAscending;
                else
                    sortMethod = TraitsSortMethod.ModSource;
                SortTraits();
            }

            string sortIndicator = sortAscending ? " ↑" : " ↓";
            Rect indicatorRect = new Rect(x + buttonWidth + 10f, 8f, 50f, 20f);
            Widgets.Label(indicatorRect, sortIndicator);

            Widgets.EndGroup();
        }

        private void DrawActionButtons(Rect rect)
        {
            Widgets.BeginGroup(rect);

            float buttonWidth = 90f;
            float spacing = 5f;
            float x = 0f;

            if (UIUtilities.ButtonWithTruncation(new Rect(x, 0f, buttonWidth, 30f), "RICS.TraitsEditor.ResetPrices".Translate()))
            {
                Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                    "RICS.TraitsEditor.ResetConfirm".Translate(),
                    () => ResetAllPrices()
                ));
            }
            x += buttonWidth + spacing;

            if (UIUtilities.ButtonWithTruncation(new Rect(x, 0f, buttonWidth, 30f), "RICS.TraitsEditor.EnableAll".Translate()))
            {
                ShowEnableMenu();
            }
            x += buttonWidth + spacing;

            if (UIUtilities.ButtonWithTruncation(new Rect(x, 0f, buttonWidth, 30f), "RICS.TraitsEditor.DisableAll".Translate()))
            {
                ShowDisableMenu();
            }

            Widgets.EndGroup();
        }

        private void ShowEnableMenu()
        {
            var options = new List<FloatMenuOption>();

            options.Add(new FloatMenuOption("Enable All Traits", () => EnableAllTraits()));
            options.Add(new FloatMenuOption("--- Enable by Source ---", null));

            var modSources = modSourceCounts.Keys
                .Where(source => source != "All")
                .OrderBy(source => source)
                .ToList();

            foreach (var modSource in modSources)
            {
                string displayName = GetDisplayModName(modSource);
                options.Add(new FloatMenuOption($"Enable {displayName} Traits", () =>
                {
                    EnableModSourceTraits(modSource);
                }));
            }

            Find.WindowStack.Add(new FloatMenu(options));
        }

        private void ShowDisableMenu()
        {
            var options = new List<FloatMenuOption>();

            options.Add(new FloatMenuOption("Disable All Traits", () => DisableAllTraits()));
            options.Add(new FloatMenuOption("--- Disable by Source ---", null));

            var modSources = modSourceCounts.Keys
                .Where(source => source != "All")
                .OrderBy(source => source)
                .ToList();

            foreach (var modSource in modSources)
            {
                string displayName = GetDisplayModName(modSource);
                options.Add(new FloatMenuOption($"Disable {displayName} Traits", () =>
                {
                    DisableModSourceTraits(modSource);
                }));
            }

            Find.WindowStack.Add(new FloatMenu(options));
        }

        private void DrawContent(Rect rect)
        {
            // Split into mod sources (left) and traits (right)
            float sourcesWidth = 220f;
            float traitsWidth = rect.width - sourcesWidth - 10f;

            // Center the content by adding some margin
            Rect sourcesRect = new Rect(rect.x + 5f, rect.y, sourcesWidth - 10f, rect.height);
            Rect traitsRect = new Rect(rect.x + sourcesWidth + 15f, rect.y, traitsWidth - 10f, rect.height);

            DrawModSourcesList(sourcesRect);
            DrawTraitsList(traitsRect);
        }

        // In Dialog_TraitsEditor.cs — replace the entire DrawModSourcesList() method with this:
        private void DrawModSourcesList(Rect rect)
        {
            // Background with centered content
            Widgets.DrawMenuSection(rect);

            // Centered header with proper padding
            Rect headerRect = new Rect(rect.x, rect.y, rect.width, 30f);
            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.UpperCenter;
            Widgets.Label(headerRect, "RICS.TraitsEditor.ModSources".Translate());
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;

            // Mod sources list with margins
            Rect listRect = new Rect(rect.x + 10f, rect.y + 35f, rect.width - 20f, rect.height - 35f - 4f);
            Rect viewRect = new Rect(0f, 0f, listRect.width, modSourceCounts.Count * 30f);

            Widgets.BeginScrollView(listRect, ref categoryScrollPosition, viewRect);
            {
                float y = 0f;

                // === Use shared utility for consistent official Ludeon ordering (Core + DLCs first) ===
                var orderedModSources = UIUtilities.GetSortedModSourceKeys(modSourceCounts);

                foreach (var key in orderedModSources)
                {
                    if (!modSourceCounts.TryGetValue(key, out int count))
                        continue;

                    Rect sourceButtonRect = new Rect(5f, y, viewRect.width - 10f, 28f);

                    if (selectedModSource == key)
                    {
                        Widgets.DrawHighlightSelected(sourceButtonRect);
                    }
                    else if (Mouse.IsOver(sourceButtonRect))
                    {
                        Widgets.DrawHighlight(sourceButtonRect);
                    }

                    string displayName = key == "All" ? "All" : GetDisplayModName(key);
                    string label = $"{displayName} ({count})";

                    // === Official Ludeon content highlight (Core + DLCs) ===
                    if (UIUtilities.IsOfficialLudeonContent(key) && key != "All")
                    {
                        label = label.Colorize(ColorLibrary.SubHeader);
                    }

                    Text.Anchor = TextAnchor.MiddleCenter;

                    // Use truncation for mod source buttons
                    if (UIUtilities.ButtonWithTruncation(sourceButtonRect, label))
                    {
                        selectedModSource = key;
                        FilterTraits();
                    }

                    Text.Anchor = TextAnchor.UpperLeft;
                    y += 30f;
                }
            }
            Widgets.EndScrollView();
        }

        private void DrawTraitsList(Rect rect)
        {
            // Background
            Widgets.DrawMenuSection(rect);

            // Header with trait count
            Rect headerRect = new Rect(rect.x, rect.y, rect.width, 30f);
            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.UpperCenter;
            string headerText = $"Traits ({filteredTraits.Count})";
            if (selectedModSource != "All")
                headerText += $" - {GetDisplayModName(selectedModSource)}";
            Widgets.Label(headerRect, headerText);
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;

            // Price setting controls under the header
            Rect priceControlsRect = new Rect(rect.x + 10f, rect.y + 35f, rect.width - 20f, 40f);
            DrawGlobalPriceControls(priceControlsRect);

            // Traits list with increased row height
            Rect listRect = new Rect(rect.x, rect.y + 80f, rect.width, rect.height - 80f - 4f); // Adjusted y position to account for price controls
            float rowHeight = 130f; // Increased from 120f to 130f for better text display

            // FIX: Check if filteredTraits is empty to prevent index out of range
            if (filteredTraits.Count == 0)
            {
                Rect noResultsRect = new Rect(listRect.x, listRect.y, listRect.width, 50f);
                Text.Anchor = TextAnchor.MiddleCenter;
                GUI.color = Color.gray;
                Widgets.Label(noResultsRect, "RICS.TraitsEditor.NoTraitsMatch".Translate());
                GUI.color = Color.white;
                Text.Anchor = TextAnchor.UpperLeft;
                return;
            }

            // FIX: Only calculate visible indices when we actually have items
            int firstVisibleIndex = Mathf.FloorToInt(scrollPosition.y / rowHeight);
            int lastVisibleIndex = Mathf.CeilToInt((scrollPosition.y + listRect.height) / rowHeight);
            firstVisibleIndex = Mathf.Clamp(firstVisibleIndex, 0, filteredTraits.Count - 1);
            lastVisibleIndex = Mathf.Clamp(lastVisibleIndex, 0, filteredTraits.Count - 1);

            Rect viewRect = new Rect(0f, 0f, listRect.width - 20f, filteredTraits.Count * rowHeight);

            Widgets.BeginScrollView(listRect, ref scrollPosition, viewRect);
            {
                float y = firstVisibleIndex * rowHeight;
                for (int i = firstVisibleIndex; i <= lastVisibleIndex; i++)
                {
                    // FIX: Additional safety check
                    if (i < 0 || i >= filteredTraits.Count)
                        continue;

                    Rect traitRect = new Rect(0f, y, viewRect.width, rowHeight - 2f);
                    if (i % 2 == 1)
                    {
                        Widgets.DrawLightHighlight(traitRect);
                    }

                    DrawTraitRow(traitRect, filteredTraits[i]);
                    y += rowHeight;
                }
            }
            Widgets.EndScrollView();
        }

        private void DrawGlobalPriceControls(Rect rect)
        {
            Widgets.BeginGroup(rect);

            try
            {
                // Center the controls horizontally - increased total width for wider buttons
                float totalWidth = 660f; // Increased from 600f to accommodate wider buttons
                float startX = (rect.width - totalWidth) / 2f;

                // Add Price Controls
                Rect addLabelRect = new Rect(startX, 0f, 80f, 30f);
                Widgets.Label(addLabelRect, "RICS.TraitsEditor.AddAll".Translate());

                Rect addInputRect = new Rect(startX + 85f, 0f, 80f, 30f);
                UIUtilities.TextFieldNumericFlexible(addInputRect, ref addAllPriceValue, ref addAllPriceBuffer, 0, 1000000);

                Rect addButtonRect = new Rect(startX + 170f, 0f, 110f, 30f); // Width: 110f for "Set Add Price"
                if (Widgets.ButtonText(addButtonRect, "RICS.TraitsEditor.SetAddPrice".Translate()))
                {
                    if (addAllPriceValue >= 0)
                    {
                        Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                            $"Set Add Price to {addAllPriceValue} for all {filteredTraits.Count} displayed traits?",
                            () => SetAllAddPrices(addAllPriceValue)
                        ));
                    }
                    else
                    {
                        Messages.Message("Price cannot be negative", MessageTypeDefOf.RejectInput);
                    }
                }

                // Remove Price Controls (positioned to the right of Add controls with more spacing)
                Rect removeLabelRect = new Rect(startX + 295f, 0f, 95f, 30f); // Moved right by 5px
                Widgets.Label(removeLabelRect, "Remove All:");

                Rect removeInputRect = new Rect(startX + 395f, 0f, 80f, 30f); // Moved right by 5px
                UIUtilities.TextFieldNumericFlexible(removeInputRect, ref removeAllPriceValue, ref removeAllPriceBuffer, 0, 1000000);

                Rect removeButtonRect = new Rect(startX + 480f, 0f, 140f, 30f); // Width: 120f for "Set Remove Price" (moved right by 5px)
                if (Widgets.ButtonText(removeButtonRect, "RICS.TraitsEditor.SetRemovePrice".Translate()))
                {
                    if (removeAllPriceValue >= 0)
                    {
                        Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                            $"Set Remove Price to {removeAllPriceValue} for all {filteredTraits.Count} displayed traits?",
                            () => SetAllRemovePrices(removeAllPriceValue)
                        ));
                    }
                    else
                    {
                        Messages.Message("Price cannot be negative", MessageTypeDefOf.RejectInput);
                    }
                }

                // Optional: Add a reset button for both - commented out as requested
                // Rect resetBothRect = new Rect(startX + 610f, 0f, 80f, 30f);
                // if (Widgets.ButtonText(resetBothRect, "Reset Both"))
                // {
                //    Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                //        $"Reset all displayed traits to their default prices?",
                //        () => ResetDisplayedTraitsPrices()
                //    ));
                // }
            }
            finally
            {
                Widgets.EndGroup();
            }
        }

        private void SetAllAddPrices(int price)
        {
            int changedCount = 0;
            foreach (var trait in filteredTraits)
            {
                if (trait.AddPrice != price)
                {
                    trait.AddPrice = price;
                    changedCount++;
                }
            }

            TraitsManager.SaveTraitsToJson();
            Messages.Message($"Set Add Price to {price} for {changedCount} traits", MessageTypeDefOf.PositiveEvent);
        }

        private void SetAllRemovePrices(int price)
        {
            int changedCount = 0;
            foreach (var trait in filteredTraits)
            {
                if (trait.RemovePrice != price)
                {
                    trait.RemovePrice = price;
                    changedCount++;
                }
            }

            TraitsManager.SaveTraitsToJson();
            Messages.Message($"Set Remove Price to {price} for {changedCount} traits", MessageTypeDefOf.PositiveEvent);
        }

        private void DrawTraitRow(Rect rect, BuyableTrait trait)
        {
            Widgets.BeginGroup(rect);

            try
            {
                // Left section: Name and basic info (adjusted for taller content)
                Rect infoRect = new Rect(5f, 5f, rect.width - 480f, 120f); // Reduced width by 30px for price section
                DrawTraitInfo(infoRect, trait);

                // Middle section: Enable/disable toggles
                Rect toggleRect = new Rect(rect.width - 470f, 20f, 120f, 90f); // Moved left by 30px
                DrawTraitToggles(toggleRect, trait);

                // Right section: Price controls - WIDER
                Rect priceRect = new Rect(rect.width - 340f, 20f, 345f, 90f); // Increased width from 300f to 330f
                DrawPriceControls(priceRect, trait);
            }
            finally
            {
                Widgets.EndGroup();
                Text.Anchor = TextAnchor.UpperLeft;
                GUI.color = Color.white;
            }
        }

        private void DrawTraitInfo(Rect rect, BuyableTrait trait)
        {
            Widgets.BeginGroup(rect);

            // Trait name with truncation and tooltip
            Rect nameRect = new Rect(0f, 0f, rect.width, 30f);
            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.UpperLeft;

            string name = trait.Name;
            string truncatedName = UIUtilities.TruncateTextToWidth(name, nameRect.width);
            Widgets.Label(nameRect, truncatedName);

            // Add tooltip if name was truncated
            if (UIUtilities.WouldTruncate(name, nameRect.width))
            {
                TooltipHandler.TipRegion(nameRect, name);
            }

            Text.Font = GameFont.Small;

            // Description - increased height and better pawn variable replacement
            Rect descRect = new Rect(0f, 32f, rect.width, 45f);
            Text.Anchor = TextAnchor.UpperLeft;
            string description = ReplacePawnVariables(trait.Description);

            // Use efficient truncation for description
            string displayDescription = UIUtilities.Truncate(description, descRect.width, "...");
            Widgets.Label(descRect, displayDescription);

            // Add tooltip if description was truncated
            if (UIUtilities.WouldTruncate(description, descRect.width))
            {
                TooltipHandler.TipRegion(descRect, description);
            }

            // Stats (if any) - adjust position due to increased heights
            if (trait.Stats.Count > 0)
            {
                Rect statsRect = new Rect(0f, 77f, rect.width, 25f); // Adjusted position
                string statsText = string.Join(", ", trait.Stats.Take(3));
                if (trait.Stats.Count > 3)
                {
                    statsText += "...";
                }
                Text.Font = GameFont.Tiny;
                GUI.color = Color.gray;
                Widgets.Label(statsRect, statsText);
                GUI.color = Color.white;
                Text.Font = GameFont.Small;
            }

            // Conflicts (if any) - adjust position
            if (trait.Conflicts.Count > 0)
            {
                Rect conflictsRect = new Rect(0f, 100f, rect.width, 15f); // Adjusted position
                string conflictsText = "Conflicts: " + string.Join(", ", trait.Conflicts.Take(2));
                if (trait.Conflicts.Count > 2)
                {
                    conflictsText += "...";
                }
                Text.Font = GameFont.Tiny;
                GUI.color = Verse.ColorLibrary.RedReadable;
                Widgets.Label(conflictsRect, conflictsText);
                GUI.color = Color.white;
                Text.Font = GameFont.Small;
            }

            Widgets.EndGroup();
        }

        public static string ReplacePawnVariables(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            return text
                .Replace("[PAWN_nameDef]", "Timmy")
                .Replace("{PAWN_nameDef}", "Timmy") // Handle curly braces)
                .Replace("[PAWN_name]", "Timmy")
                .Replace("{PAWN_name}", "Timmy") // Handle curly braces
                .Replace("[PAWN_possessive]", "Timmy's") // Fixed this one
                .Replace("{PAWN_possessive}", "Timmy's") // Handle curly braces
                .Replace("[PAWN_objective]", "him")
                .Replace("{PAWN_objective}", "him") // Handle curly braces
                .Replace("[PAWN_pronoun]", "he")
                .Replace("{PAWN_pronoun}", "he") // Handle curly braces
                .Replace("[PANN_nameDef]", "Timmy") // Handle typos
                .Replace("[PANN_possessive]", "Timmy's") // Handle typos
                .Replace("[PANN_objective]", "him") // Handle typos
                .Replace("[PANN_pronoun]", "he") // Handle typos
                .Replace("[BAWN_announce]", "he"); // Handle typos
        }

        private void DrawTraitToggles(Rect rect, BuyableTrait trait)
        {
            Widgets.BeginGroup(rect);

            float toggleHeight = 20f;
            float spacing = 5f;
            float y = 0f;

            Rect canAddRect = new Rect(0f, y, rect.width, toggleHeight);
            bool canAddCurrent = trait.CanAdd;
            Widgets.CheckboxLabeled(canAddRect, "Can Add", ref canAddCurrent);
            if (canAddCurrent != trait.CanAdd)
            {
                trait.CanAdd = canAddCurrent;
                TraitsManager.SaveTraitsToJson();
            }
            y += toggleHeight + spacing;

            Rect canRemoveRect = new Rect(0f, y, rect.width, toggleHeight);
            bool canRemoveCurrent = trait.CanRemove;
            Widgets.CheckboxLabeled(canRemoveRect, "Can Remove", ref canRemoveCurrent);
            if (canRemoveCurrent != trait.CanRemove)
            {
                trait.CanRemove = canRemoveCurrent;
                TraitsManager.SaveTraitsToJson();
            }
            y += toggleHeight + spacing;

            Rect bypassRect = new Rect(0f, y, rect.width, toggleHeight);
            bool bypassCurrent = trait.BypassLimit;
            Widgets.CheckboxLabeled(bypassRect, "RICS.TraitsEditor.BypassLimit".Translate(), ref bypassCurrent);
            if (bypassCurrent != trait.BypassLimit)
            {
                trait.BypassLimit = bypassCurrent;
                TraitsManager.SaveTraitsToJson();
            }

            Widgets.EndGroup();
        }

        private void DrawPriceControls(Rect rect, BuyableTrait trait)
        {
            Widgets.BeginGroup(rect);

            float controlHeight = 30f;
            float spacing = 5f;
            float y = 0f;

            Rect addPriceRect = new Rect(0f, y, rect.width, controlHeight);
            DrawSinglePriceControl(addPriceRect, "Add Price:", trait.AddPrice, trait, true);
            y += controlHeight + spacing;

            Rect removePriceRect = new Rect(0f, y, rect.width, controlHeight);
            DrawSinglePriceControl(removePriceRect, "Remove Price:", trait.RemovePrice, trait, false);

            Widgets.EndGroup();
        }

        private void DrawSinglePriceControl(Rect rect, string label, int currentPrice, BuyableTrait trait, bool isAddPrice)
        {
            Widgets.BeginGroup(rect);

            // Label - give it more space
            Rect labelRect = new Rect(0f, 0f, 100f, 30f);
            Widgets.Label(labelRect, label);

            // Price input
            Rect inputRect = new Rect(105f, 0f, 80f, 30f);
            int priceBuffer = currentPrice;
            string stringBuffer = priceBuffer.ToString();
            UIUtilities.TextFieldNumericFlexible(inputRect, ref priceBuffer, ref stringBuffer, 0, 1000000);

            if (priceBuffer != currentPrice)
            {
                if (isAddPrice)
                    trait.AddPrice = priceBuffer;
                else
                    trait.RemovePrice = priceBuffer;
                TraitsManager.SaveTraitsToJson();
            }

            // Reset button - FIXED: Use proper BuyableTrait constructor to get correct default price
            Rect resetRect = new Rect(190f, 0f, 60f, 30f);
            if (Widgets.ButtonText(resetRect, "Reset"))
            {
                int defaultPrice = GetProperDefaultPrice(trait, isAddPrice);
                if (isAddPrice)
                    trait.AddPrice = defaultPrice;
                else
                    trait.RemovePrice = defaultPrice;
                TraitsManager.SaveTraitsToJson();
            }

            Widgets.EndGroup();
        }

        private int GetProperDefaultPrice(BuyableTrait trait, bool isAddPrice)
        {
            try
            {
                // Get the TraitDef from the game database
                var traitDef = DefDatabase<TraitDef>.GetNamedSilentFail(trait.DefName);
                if (traitDef == null)
                    return isAddPrice ? 300 : 500; // Fallback defaults

                // Create a new BuyableTrait instance to get the proper calculated price
                BuyableTrait freshTrait;

                if (traitDef.degreeDatas != null)
                {
                    var degreeData = traitDef.degreeDatas.FirstOrDefault(d => d.degree == trait.Degree);
                    if (degreeData != null)
                    {
                        freshTrait = new BuyableTrait(traitDef, degreeData);
                    }
                    else
                    {
                        freshTrait = new BuyableTrait(traitDef);
                    }
                }
                else
                {
                    freshTrait = new BuyableTrait(traitDef);
                }

                return isAddPrice ? freshTrait.AddPrice : freshTrait.RemovePrice;
            }
            catch (System.Exception ex)
            {
                Logger.Error($"Error calculating default price for {trait.DefName}: {ex.Message}");
                return isAddPrice ? 300 : 500; // Fallback defaults
            }
        }

        private void BuildModSourceCounts()
        {
            modSourceCounts.Clear();
            modSourceCounts["All"] = TraitsManager.AllBuyableTraits.Count;

            foreach (var trait in TraitsManager.AllBuyableTraits.Values)
            {
                string displayModSource = GetDisplayModName(trait.ModSource);
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

        private void FilterTraits()
        {
            lastSearch = searchQuery;
            filteredTraits.Clear();

            var allTraits = TraitsManager.AllBuyableTraits.Values.AsEnumerable();

            if (selectedModSource != "All")
            {
                allTraits = allTraits.Where(trait => GetDisplayModName(trait.ModSource) == selectedModSource);
            }

            if (!string.IsNullOrEmpty(searchQuery))
            {
                string searchLower = searchQuery.ToLower();
                allTraits = allTraits.Where(trait =>
                    trait.GetDisplayName().ToLower().Contains(searchLower) ||
                    trait.Description.ToLower().Contains(searchLower) ||
                    trait.DefName.ToLower().Contains(searchLower) ||
                    trait.ModSource.ToLower().Contains(searchLower)
                );
            }

            filteredTraits = allTraits.ToList();
            SortTraits();
        }

        private void SortTraits()
        {
            switch (sortMethod)
            {
                case TraitsSortMethod.Name:
                    filteredTraits = sortAscending ?
                        filteredTraits.OrderBy(trait => trait.GetDisplayName()).ToList() :
                        filteredTraits.OrderByDescending(trait => trait.GetDisplayName()).ToList();
                    break;
                case TraitsSortMethod.AddPrice:
                    filteredTraits = sortAscending ?
                        filteredTraits.OrderBy(trait => trait.AddPrice).ToList() :
                        filteredTraits.OrderByDescending(trait => trait.AddPrice).ToList();
                    break;
                case TraitsSortMethod.RemovePrice:
                    filteredTraits = sortAscending ?
                        filteredTraits.OrderBy(trait => trait.RemovePrice).ToList() :
                        filteredTraits.OrderByDescending(trait => trait.RemovePrice).ToList();
                    break;
                case TraitsSortMethod.ModSource:
                    filteredTraits = sortAscending ?
                        filteredTraits.OrderBy(trait => GetDisplayModName(trait.ModSource)).ThenBy(trait => trait.GetDisplayName()).ToList() :
                        filteredTraits.OrderByDescending(trait => GetDisplayModName(trait.ModSource)).ThenBy(trait => trait.GetDisplayName()).ToList();
                    break;
            }
        }

        private void SaveOriginalPrices()
        {
            originalPrices.Clear();
            foreach (var trait in TraitsManager.AllBuyableTraits.Values)
            {
                originalPrices[trait.DefName] = (trait.AddPrice, trait.RemovePrice);
            }
        }

        private void ResetAllPrices()
        {
            foreach (var trait in TraitsManager.AllBuyableTraits.Values)
            {
                // Use the proper pricing logic for both add and remove prices
                trait.AddPrice = GetProperDefaultPrice(trait, true);
                trait.RemovePrice = GetProperDefaultPrice(trait, false);
                trait.CanAdd = true;
                trait.CanRemove = true;
            }
            TraitsManager.SaveTraitsToJson();
            FilterTraits();
        }

        private void EnableAllTraits()
        {
            foreach (var trait in TraitsManager.AllBuyableTraits.Values)
            {
                trait.CanAdd = true;
                trait.CanRemove = true;
            }
            TraitsManager.SaveTraitsToJson();
            FilterTraits();
        }

        private void DisableAllTraits()
        {
            foreach (var trait in TraitsManager.AllBuyableTraits.Values)
            {
                trait.CanAdd = false;
                trait.CanRemove = false;
            }
            TraitsManager.SaveTraitsToJson();
            FilterTraits();
        }

        private void EnableModSourceTraits(string modSource)
        {
            int enabledCount = 0;
            foreach (var trait in TraitsManager.AllBuyableTraits.Values)
            {
                if (GetDisplayModName(trait.ModSource) == modSource && (!trait.CanAdd || !trait.CanRemove))
                {
                    trait.CanAdd = true;
                    trait.CanRemove = true;
                    enabledCount++;
                }
            }
            TraitsManager.SaveTraitsToJson();
            Messages.Message($"Enabled {enabledCount} {GetDisplayModName(modSource)} traits", MessageTypeDefOf.PositiveEvent);
            FilterTraits();
        }

        private void DisableModSourceTraits(string modSource)
        {
            int disabledCount = 0;
            foreach (var trait in TraitsManager.AllBuyableTraits.Values)
            {
                if (GetDisplayModName(trait.ModSource) == modSource && (trait.CanAdd || trait.CanRemove))
                {
                    trait.CanAdd = false;
                    trait.CanRemove = false;
                    disabledCount++;
                }
            }
            TraitsManager.SaveTraitsToJson();
            Messages.Message($"Disabled {disabledCount} {GetDisplayModName(modSource)} traits", MessageTypeDefOf.NeutralEvent);
            FilterTraits();
        }

        private void ShowSaveAsMenu()
        {
            List<FloatMenuOption> options = new List<FloatMenuOption>
            {
                new FloatMenuOption("Quick Timestamped Backup", () =>
                {
                    string json = JsonConvert.SerializeObject(TraitsManager.AllBuyableTraits, Formatting.Indented);
                    BackupUtility.SaveQuickBackup("TraitsEditor", json);
                    Messages.Message("Quick timestamped backup saved.", MessageTypeDefOf.NeutralEvent);
                }),
                new FloatMenuOption("Save as Named Theme (custom name)", () =>
                {
                    string json = JsonConvert.SerializeObject(TraitsManager.AllBuyableTraits, Formatting.Indented);

                    Find.WindowStack.Add(new Dialog_TextInput(
                        "Enter backup name (e.g. GrimwarTraits, RimMagic)",
                        name =>
                        {
                            if (!string.IsNullOrWhiteSpace(name))
                            {
                                BackupUtility.SaveNamedBackup("TraitsEditor", name, json);
                                Messages.Message($"Named backup saved as {name}.json", MessageTypeDefOf.NeutralEvent);
                            }
                            else
                            {
                                string fallback = "CustomTraits_" + DateTime.Now.ToString("yyyyMMdd_HHmm");
                                BackupUtility.SaveNamedBackup("TraitsEditor", fallback, json);
                                Messages.Message($"Saved with fallback name: {fallback}.json", MessageTypeDefOf.NeutralEvent);
                            }
                        }));
                })
            };

            Find.WindowStack.Add(new FloatMenu(options));
        }

        private void ShowLoadFileMenu()
        {
            var files = BackupUtility.GetAllBackupFiles("TraitsEditor");
            if (files.Count == 0)
            {
                Messages.Message("No backup files found for Traits Editor.", MessageTypeDefOf.RejectInput);
                return;
            }

            List<FloatMenuOption> options = new List<FloatMenuOption>();
            foreach (var file in files)
            {
                options.Add(new FloatMenuOption(file, () =>
                {
                    string json = BackupUtility.LoadBackupFile("TraitsEditor", file);
                    if (!string.IsNullOrEmpty(json))
                    {
                        try
                        {
                            var loaded = JsonConvert.DeserializeObject<Dictionary<string, BuyableTrait>>(json);
                            if (loaded != null)
                            {
                                TraitsManager.AllBuyableTraits.Clear();
                                foreach (var kvp in loaded)
                                    TraitsManager.AllBuyableTraits[kvp.Key] = kvp.Value;

                                TraitsManager.SaveTraitsToJson();
                                BuildModSourceCounts();
                                FilterTraits();

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
            var files = BackupUtility.GetAllBackupFiles("TraitsEditor");
            if (files.Count == 0)
            {
                Messages.Message("No backup files found for Traits Editor.", MessageTypeDefOf.RejectInput);
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
                            bool deleted = BackupUtility.DeleteBackupFile("TraitsEditor", file);
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
            TraitsManager.SaveTraitsToJson();
            base.PostClose();
        }
    }

    public enum TraitsSortMethod
    {
        Name,
        AddPrice,
        RemovePrice,
        ModSource
    }
}