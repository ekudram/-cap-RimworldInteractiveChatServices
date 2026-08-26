// ButtonUtils.cs
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
// Runtime API for third-party mods to add RICS toolbar buttons.
// Prefer XML EnhancedChatInteractiveAddonDef for main-tab + toolbar; this API is toolbar-only.
// See Defs/AddonDefs/ChatInteractiveAddon.xml for the full modder cookbook.
using CAP_ChatInteractive.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace CAP_ChatInteractive
{
    /// <summary>
    /// Helper methods for other mods to register buttons on the RICS quick toolbar.
    /// Call after game defs are loaded (e.g. StaticConstructorOnStartup or LongEvent).
    /// For main-tab listing + load-order safety, ship an XML Def instead.
    /// </summary>
    public static class ButtonUtils
    {
        /// <summary>
        /// Add a DirectDialogButton that opens a settings-style Window.
        /// </summary>
        public static void AddModSettingsButton(
            string modName,
            Type settingsDialogType,
            string buttonLabel = null,
            string iconPath = null,
            int displayOrder = 1000)
        {
            if (!ValidateWindowType(settingsDialogType, modName, buttonLabel ?? "Settings"))
                return;

            var buttonDef = new EnhancedChatInteractiveAddonDef
            {
                defName = $"{SanitizeId(modName)}_Settings",
                label = buttonLabel ?? $"{modName} Settings",
                description = $"Open {modName} settings",
                dialogClass = settingsDialogType,
                buttonType = ButtonType.DirectDialogButton,
                sourceMod = modName ?? "Unknown",
                enabled = true,
                displayOrder = displayOrder,
                showInToolbar = true,
                iconPath = iconPath
            };

            ToolbarButtonManager.AddToolbarButton(buttonDef);
        }

        /// <summary>
        /// Add a button that opens a dialog immediately (toolbar only unless you also ship XML).
        /// Use displayOrder 200+ (or 1000+) to stay clear of RICS built-ins (~85–101).
        /// </summary>
        public static void AddDirectDialogButton(
            string modName,
            string defName,
            string label,
            Type dialogClass,
            string description = null,
            string iconPath = null,
            int displayOrder = 1000,
            bool showInToolbar = true)
        {
            if (!ValidateWindowType(dialogClass, modName, label))
                return;

            var buttonDef = new EnhancedChatInteractiveAddonDef
            {
                defName = $"{SanitizeId(modName)}_{SanitizeId(defName)}",
                label = label,
                description = description ?? $"Open {label}",
                dialogClass = dialogClass,
                buttonType = ButtonType.DirectDialogButton,
                sourceMod = modName ?? "Unknown",
                enabled = true,
                displayOrder = displayOrder,
                showInToolbar = showInToolbar,
                iconPath = iconPath
            };

            ToolbarButtonManager.AddToolbarButton(buttonDef);
        }

        /// <summary>Add a button that toggles a Window open/closed.</summary>
        public static void AddToggleWindowButton(
            string modName,
            string defName,
            string label,
            Type windowClass,
            string description = null,
            string iconPath = null,
            int displayOrder = 1000,
            bool showInToolbar = true)
        {
            if (!ValidateWindowType(windowClass, modName, label))
                return;

            var buttonDef = new EnhancedChatInteractiveAddonDef
            {
                defName = $"{SanitizeId(modName)}_{SanitizeId(defName)}",
                label = label,
                description = description ?? $"Toggle {label} window",
                windowClass = windowClass,
                buttonType = ButtonType.ToggleWindowButton,
                sourceMod = modName ?? "Unknown",
                enabled = true,
                displayOrder = displayOrder,
                showInToolbar = showInToolbar,
                iconPath = iconPath
            };

            ToolbarButtonManager.AddToolbarButton(buttonDef);
        }

        /// <summary>
        /// Add a MenuButton whose menuClass implements <see cref="IAddonMenu"/>.
        /// </summary>
        public static void AddMenuButton(
            string modName,
            string defName,
            string label,
            Type menuClass,
            string description = null,
            string iconPath = null,
            int displayOrder = 1000,
            bool showInToolbar = true)
        {
            if (menuClass == null)
            {
                Logger.Error($"[AddonMenu] Cannot add menu button {label} for {modName}: menuClass is null");
                return;
            }

            if (!typeof(IAddonMenu).IsAssignableFrom(menuClass))
            {
                Logger.Error(
                    $"[AddonMenu] Cannot add menu button {label} for {modName}: " +
                    $"{menuClass.Name} must implement IAddonMenu");
                return;
            }

            var buttonDef = new EnhancedChatInteractiveAddonDef
            {
                defName = $"{SanitizeId(modName)}_{SanitizeId(defName)}",
                label = label,
                description = description ?? $"Open {label} menu",
                menuClass = menuClass,
                buttonType = ButtonType.MenuButton,
                sourceMod = modName ?? "Unknown",
                enabled = true,
                displayOrder = displayOrder,
                showInToolbar = showInToolbar,
                iconPath = iconPath
            };

            ToolbarButtonManager.AddToolbarButton(buttonDef);
        }

        /// <summary>Remove all runtime toolbar buttons registered under this sourceMod name.</summary>
        public static void RemoveAllButtonsFromMod(string modName)
        {
            if (string.IsNullOrEmpty(modName))
                return;

            foreach (var button in ToolbarButtonManager.GetAllToolbarButtons()
                         .Where(b => b != null && b.sourceMod == modName)
                         .ToList())
            {
                ToolbarButtonManager.RemoveToolbarButton(button.defName);
            }
        }

        /// <summary>Remove a toolbar button by full defName.</summary>
        public static void RemoveButton(string defName)
        {
            ToolbarButtonManager.RemoveToolbarButton(defName);
        }

        /// <summary>List toolbar buttons currently attributed to a sourceMod.</summary>
        public static List<EnhancedChatInteractiveAddonDef> GetButtonsFromMod(string modName)
        {
            return ToolbarButtonManager.GetAllToolbarButtons()
                .Where(b => b != null && b.sourceMod == modName)
                .ToList();
        }

        /// <summary>
        /// True if RICS (any package id used by this project) is active.
        /// </summary>
        public static bool IsRICSLoaded()
        {
            return ModLister.GetActiveModWithIdentifier("Captolamia.RICS.Beta") != null
                   || ModLister.GetActiveModWithIdentifier("Captolamia.CAPChatInteractive") != null
                   || ModLister.GetActiveModWithIdentifier("Captolamia.RICS") != null;
        }

        private static bool ValidateWindowType(Type windowClass, string modName, string label)
        {
            if (windowClass == null)
            {
                Logger.Error($"[AddonMenu] Cannot add button {label} for {modName}: type is null");
                return false;
            }

            if (!typeof(Window).IsAssignableFrom(windowClass))
            {
                Logger.Error(
                    $"[AddonMenu] Cannot add button {label} for {modName}: " +
                    $"{windowClass.Name} must inherit from Verse.Window");
                return false;
            }

            return true;
        }

        private static string SanitizeId(string raw)
        {
            if (string.IsNullOrEmpty(raw))
                return "Mod";

            char[] chars = raw.Where(c => char.IsLetterOrDigit(c) || c == '_').ToArray();
            return chars.Length > 0 ? new string(chars) : "Mod";
        }
    }
}
