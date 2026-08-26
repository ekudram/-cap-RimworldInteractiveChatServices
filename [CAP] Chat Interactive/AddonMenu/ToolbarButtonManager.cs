// ToolbarButtonManager.cs
// Copyright (c) Captolamia
// This file is part of CAP Chat Interactive aka RICS (Rimworld Interactive Chat System).
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
// Top-of-screen quick toolbar for EnhancedChatInteractiveAddonDef with showInToolbar=true.
// Modders: prefer XML Defs, or ButtonUtils.Add* for runtime toolbar-only buttons.
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace CAP_ChatInteractive
{
    [StaticConstructorOnStartup]
    public static class ToolbarButtonManager
    {
        private static List<EnhancedChatInteractiveAddonDef> toolbarButtons
            = new List<EnhancedChatInteractiveAddonDef>();

        private const float DividerWidth = 12f;

        static ToolbarButtonManager()
        {
            try
            {
                RefreshToolbarButtons();
            }
            catch (Exception ex)
            {
                Logger.Error($"[AddonMenu] ToolbarButtonManager init failed: {ex}");
                toolbarButtons = new List<EnhancedChatInteractiveAddonDef>();
            }
        }

        public static void RefreshToolbarButtons()
        {
            toolbarButtons = DefDatabase<EnhancedChatInteractiveAddonDef>.AllDefs
                .Where(def => def != null && def.enabled && def.showInToolbar)
                .OrderBy(def => def.displayOrder)
                .ToList();
        }

        /// <summary>Draw the toolbar just below the top menu bar (play mode only).</summary>
        public static void DrawToolbar()
        {
            if (toolbarButtons == null || toolbarButtons.Count == 0)
                return;

            if (Current.ProgramState != ProgramState.Playing)
                return;

            try
            {
                DrawToolbarInternal();
            }
            catch (Exception ex)
            {
                Logger.Error($"[AddonMenu] DrawToolbar failed: {ex}");
            }
        }

        private static void DrawToolbarInternal()
        {
            var groupedButtons = toolbarButtons
                .GroupBy(b => b.sourceMod ?? "Unknown")
                .OrderBy(g => g.Key == "RICS" ? 0 : 1)
                .ThenBy(g => g.Key)
                .ToList();

            const float buttonSize = 32f;
            const float spacing = 4f;
            const float separatorWidth = 12f;
            float dividerWidth = DividerWidth;

            float totalWidth = 0f;
            bool firstItem = true;

            foreach (var def in visibleButtons)
            {
                if (!firstItem)
                    totalWidth += spacing;

                totalWidth += def.buttonType == ButtonType.Divider ? dividerWidth : buttonSize;
                firstItem = false;
            }

            int totalSeparators = Math.Max(0, groupedButtons.Count - 1);
            totalWidth += separatorWidth * totalSeparators;
            totalWidth += 8f;

            float screenWidth = UI.screenWidth;
            float x = (screenWidth - totalWidth) / 2f;
            float y = 35f;

            Rect toolbarRect = new Rect(x, y, totalWidth, buttonSize);
            Widgets.DrawMenuSection(toolbarRect);

            float currentX = toolbarRect.x;

            for (int i = 0; i < groupedButtons.Count; i++)
            {
                var group = groupedButtons[i];

                foreach (var buttonDef in group.OrderBy(b => b.displayOrder))
                {
                    if (buttonDef.buttonType == ButtonType.Divider)
                    {
                        Rect dividerRect = new Rect(currentX, toolbarRect.y, dividerWidth, buttonSize);
                        float lineX = dividerRect.x + dividerWidth / 2f;
                        Widgets.DrawLineVertical(lineX, dividerRect.y + 6f, dividerRect.height - 12f);
                        currentX += dividerWidth;
                    }
                    else
                    {
                        Rect buttonRect = new Rect(currentX, toolbarRect.y, buttonSize, buttonSize);
                        DrawToolbarButton(buttonRect, buttonDef);
                        currentX += buttonSize;
                    }

                    if (currentX + spacing < toolbarRect.xMax - 4f)
                        currentX += spacing;
                }

                if (i < groupedButtons.Count - 1)
                {
                    Rect separatorRect = new Rect(currentX, toolbarRect.y, separatorWidth, buttonSize);
                    DrawModSeparator(separatorRect, group.Key, groupedButtons[i + 1].Key);
                    currentX += separatorWidth;
                }
            }
        }

        private static void DrawModSeparator(Rect rect, string leftMod, string rightMod)
        {
            float lineX = rect.x + rect.width / 2f;
            Widgets.DrawLineVertical(lineX, rect.y, rect.height);
            TooltipHandler.TipRegion(rect, $"{leftMod} → {rightMod}");
        }

        private static void DrawToolbarButton(Rect rect, EnhancedChatInteractiveAddonDef buttonDef)
        {
            if (buttonDef == null || buttonDef.buttonType == ButtonType.Divider)
                return;

            if (Mouse.IsOver(rect))
                Widgets.DrawHighlight(rect);

            string label = buttonDef.label ?? string.Empty;
            if (!string.IsNullOrEmpty(buttonDef.iconPath))
            {
                Texture2D icon = AddonButtonActions.LoadIcon(buttonDef.iconPath);
                if (icon != null)
                {
                    GUI.DrawTexture(rect.ContractedBy(4f), icon);
                }
                else
                {
                    DrawFallbackLetter(rect, label);
                }
            }
            else
            {
                DrawFallbackLetter(rect, label);
            }

            if (!string.IsNullOrEmpty(buttonDef.tooltip))
                TooltipHandler.TipRegion(rect, buttonDef.tooltip);
            else if (!string.IsNullOrEmpty(label))
                TooltipHandler.TipRegion(rect, label);

            if (Widgets.ButtonInvisible(rect))
                buttonDef.ExecuteDirectly();

            if (buttonDef.hotkey != null)
            {
                Rect hotkeyRect = new Rect(rect.x + rect.width - 10f, rect.y, 10f, 10f);
                GUI.color = Color.yellow;
                Widgets.DrawTextureFitted(hotkeyRect, BaseContent.WhiteTex, 1f);
                GUI.color = Color.white;
            }

            Widgets.DrawBox(rect, 1);
        }

        private static void DrawFallbackLetter(Rect rect, string label)
        {
            string firstLetter = !string.IsNullOrEmpty(label) ? label[0].ToString() : "?";
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(rect, firstLetter);
            Text.Anchor = TextAnchor.UpperLeft;
        }

        public static void CheckHotkeys()
        {
            if (Current.ProgramState != ProgramState.Playing || toolbarButtons == null)
                return;

            try
            {
                foreach (var buttonDef in toolbarButtons)
                {
                    if (buttonDef?.hotkey != null && buttonDef.hotkey.KeyDownEvent)
                        buttonDef.ExecuteDirectly();
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"[AddonMenu] CheckHotkeys failed: {ex}");
            }
        }

        /// <summary>Snapshot of toolbar buttons (for modders inspecting/extending).</summary>
        public static List<EnhancedChatInteractiveAddonDef> GetAllToolbarButtons()
        {
            return toolbarButtons != null
                ? new List<EnhancedChatInteractiveAddonDef>(toolbarButtons)
                : new List<EnhancedChatInteractiveAddonDef>();
        }

        /// <summary>
        /// Add a button at runtime (toolbar only — not the main tab DefDatabase list).
        /// Prefer shipping an XML EnhancedChatInteractiveAddonDef for full integration.
        /// </summary>
        public static void AddToolbarButton(EnhancedChatInteractiveAddonDef buttonDef)
        {
            if (buttonDef == null)
                return;

            if (toolbarButtons == null)
                toolbarButtons = new List<EnhancedChatInteractiveAddonDef>();

            if (toolbarButtons.Any(b => b != null && b.defName == buttonDef.defName))
                return;

            toolbarButtons.Add(buttonDef);
            toolbarButtons = toolbarButtons.OrderBy(b => b.displayOrder).ToList();
        }

        /// <summary>Remove a toolbar button by defName.</summary>
        public static void RemoveToolbarButton(string defName)
        {
            if (toolbarButtons == null || string.IsNullOrEmpty(defName))
                return;

            toolbarButtons.RemoveAll(b => b != null && b.defName == defName);
        }
    }
}
