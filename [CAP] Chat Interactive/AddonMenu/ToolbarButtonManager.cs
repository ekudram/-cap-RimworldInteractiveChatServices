// ToolbarButtonManager.cs
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
//
// Manages quick-access toolbar buttons for frequently used features
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace CAP_ChatInteractive
{
    [StaticConstructorOnStartup]
    public static class ToolbarButtonManager
    {
        private static List<EnhancedChatInteractiveAddonDef> toolbarButtons;
        private static Texture2D defaultButtonIcon;
        private const float DividerWidth = 12f;

        static ToolbarButtonManager()
        {
            defaultButtonIcon = ContentFinder<Texture2D>.Get("UI/Widgets/ButtonBG", true);
            RefreshToolbarButtons();
        }

        public static void RefreshToolbarButtons()
        {
            toolbarButtons = DefDatabase<EnhancedChatInteractiveAddonDef>.AllDefs
                .Where(def => def.enabled && def.showInToolbar)
                .OrderBy(def => def.displayOrder)
                .ToList();

            Logger.Debug($"Loaded {toolbarButtons.Count} toolbar buttons");
        }

        /// <summary>
        /// Draw the toolbar at the top of the screen
        /// </summary>
        // Update the DrawToolbar method to add separators between different mods:
        public static void DrawToolbar()
        {
            if (toolbarButtons.Count == 0 || Current.ProgramState != ProgramState.Playing)
                return;

            // Group buttons by source mod
            var groupedButtons = toolbarButtons
                .GroupBy(b => b.sourceMod)
                .OrderBy(g => g.Key == "RICS" ? 0 : 1) // RICS first
                .ThenBy(g => g.Key) // Then alphabetically
                .ToList();

            // Calculate total width cleanly (spacing ONLY between items + safety padding)
            float buttonSize = 32f;
            float spacing = 4f;
            float separatorWidth = 12f;
            float dividerWidth = DividerWidth;

            float totalWidth = 0f;
            bool firstItem = true;

            foreach (var def in toolbarButtons)
            {
                if (!firstItem)
                    totalWidth += spacing;

                totalWidth += (def.buttonType == ButtonType.Divider) ? dividerWidth : buttonSize;
                firstItem = false;
            }

            // Add mod-group separators
            int totalSeparators = groupedButtons.Count - 1;
            totalWidth += (separatorWidth * totalSeparators);

            // Safety padding so the final button (Version History) is never clipped
            totalWidth += 8f;

            // Position at top center of screen
            float screenWidth = UI.screenWidth;
            float x = (screenWidth - totalWidth) / 2f;
            float y = 35f; // Just below the top menu bar

            Rect toolbarRect = new Rect(x, y, totalWidth, buttonSize);

            // Background
            Widgets.DrawMenuSection(toolbarRect);

            // Draw buttons/dividers with separators
            float currentX = toolbarRect.x;

            for (int i = 0; i < groupedButtons.Count; i++)
            {
                var group = groupedButtons[i];

                foreach (var buttonDef in group.OrderBy(b => b.displayOrder))
                {
                    if (buttonDef.buttonType == ButtonType.Divider)
                    {
                        // Divider: thin vertical line
                        Rect dividerRect = new Rect(currentX, toolbarRect.y, dividerWidth, buttonSize);
                        float lineX = dividerRect.x + dividerWidth / 2f;
                        Widgets.DrawLineVertical(lineX, dividerRect.y + 6f, dividerRect.height - 12f);
                        currentX += dividerWidth;
                        Logger.Debug("Divider Drawn");
                    }
                    else
                    {
                        // Real button: full size
                        Rect buttonRect = new Rect(currentX, toolbarRect.y, buttonSize, buttonSize);
                        DrawToolbarButton(buttonRect, buttonDef);
                        currentX += buttonSize;
                        Logger.Debug("Button Drawn");
                    }

                    // Spacing after every item (except we already calculated it)
                    if (currentX + spacing < toolbarRect.xMax - 4f)
                        currentX += spacing;
                }

                // Mod-group separator (between different sourceMods)
                if (i < groupedButtons.Count - 1)
                {
                    Rect separatorRect = new Rect(currentX, toolbarRect.y, separatorWidth, buttonSize);
                    DrawModSeparator(separatorRect, group.Key, groupedButtons[i + 1].Key);
                    currentX += separatorWidth;
                }
            }
        }

        // Add this new method to draw separators between mods:
        private static void DrawModSeparator(Rect rect, string leftMod, string rightMod)
        {
            // Draw a vertical line
            float lineX = rect.x + rect.width / 2f;
            Widgets.DrawLineVertical(lineX, rect.y, rect.height);

            // Add tooltip showing mod transition
            string tooltipText = $"{leftMod} → {rightMod}";
            TooltipHandler.TipRegion(rect, tooltipText);
        }

        private static void DrawToolbarButton(Rect rect, EnhancedChatInteractiveAddonDef buttonDef)
        {
            if (buttonDef.buttonType == ButtonType.Divider) return;   // Safety — never called for dividers

            // Button background with hover effect
            if (Mouse.IsOver(rect))
            {
                Widgets.DrawHighlight(rect);
            }

            // Icon or label
            if (!string.IsNullOrEmpty(buttonDef.iconPath))
            {
                var icon = ContentFinder<Texture2D>.Get(buttonDef.iconPath, false);
                if (icon != null)
                {
                    Rect iconRect = rect.ContractedBy(4f);
                    GUI.DrawTexture(iconRect, icon);
                }
                else
                {
                    // Fallback: show first letter of label
                    string firstLetter = buttonDef.label.Length > 0 ? buttonDef.label[0].ToString() : "?";
                    Text.Anchor = TextAnchor.MiddleCenter;
                    Widgets.Label(rect, firstLetter);
                    Text.Anchor = TextAnchor.UpperLeft;
                }
            }
            else
            {
                // Show first letter if no icon
                string firstLetter = buttonDef.label.Length > 0 ? buttonDef.label[0].ToString() : "?";
                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(rect, firstLetter);
                Text.Anchor = TextAnchor.UpperLeft;
            }

            // Tooltip
            TooltipHandler.TipRegion(rect, buttonDef.tooltip);

            // Click handler
            if (Widgets.ButtonInvisible(rect))
            {
                buttonDef.ExecuteDirectly();
            }

            // Hotkey indicator (small star in corner)
            if (buttonDef.hotkey != null)
            {
                Rect hotkeyRect = new Rect(rect.x + rect.width - 10f, rect.y, 10f, 10f);
                GUI.color = Color.yellow;
                Widgets.DrawTextureFitted(hotkeyRect, BaseContent.WhiteTex, 1f);
                GUI.color = Color.white;
            }

            // Draw border
            Widgets.DrawBox(rect, 1);
        }

        /// <summary>
        /// Check for hotkey presses
        /// </summary>
        public static void CheckHotkeys()
        {
            if (Current.ProgramState != ProgramState.Playing) return;

            foreach (var buttonDef in toolbarButtons)
            {
                if (buttonDef.hotkey != null && buttonDef.hotkey.KeyDownEvent)
                {
                    buttonDef.ExecuteDirectly();
                }
            }
        }

        /// <summary>
        /// Get all toolbar buttons (for modders to extend)
        /// </summary>
        public static List<EnhancedChatInteractiveAddonDef> GetAllToolbarButtons()
        {
            return toolbarButtons ?? new List<EnhancedChatInteractiveAddonDef>();
        }

        /// <summary>
        /// Add a button dynamically (for modders)
        /// </summary>
        public static void AddToolbarButton(EnhancedChatInteractiveAddonDef buttonDef)
        {
            if (toolbarButtons == null)
                toolbarButtons = new List<EnhancedChatInteractiveAddonDef>();

            if (!toolbarButtons.Contains(buttonDef))
            {
                toolbarButtons.Add(buttonDef);
                toolbarButtons = toolbarButtons.OrderBy(b => b.displayOrder).ToList();
                Logger.Debug($"Added toolbar button: {buttonDef.defName}");
            }
        }

        /// <summary>
        /// Remove a toolbar button
        /// </summary>
        public static void RemoveToolbarButton(string defName)
        {
            if (toolbarButtons != null)
            {
                int removed = toolbarButtons.RemoveAll(b => b.defName == defName);
                if (removed > 0)
                {
                    Logger.Debug($"Removed toolbar button: {defName}");
                }
            }
        }
    }
}