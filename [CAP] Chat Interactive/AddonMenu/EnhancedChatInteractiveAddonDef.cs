// EnhancedChatInteractiveAddonDef.cs
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
// ENHANCED VERSION: Supports multiple button types and quick-access functionality
using System;
using System.Collections.Generic;
using System.Linq;
using CAP_ChatInteractive.Interfaces;
using UnityEngine;
using Verse;

namespace CAP_ChatInteractive
{
    public class EnhancedChatInteractiveAddonDef : Def
    {
        // --- Original Fields (Backward Compatible) ---
        public Type menuClass = typeof(ChatInteractiveAddonMenu);
        public bool enabled = true;
        public int displayOrder = 10;
        public string sourceMod = "RICS";

        // Button type determines behavior
        public ButtonType buttonType = ButtonType.MenuButton;
        // For DirectDialogButton: Opens a specific dialog
        public Type dialogClass = null;
        // For ToggleWindowButton: Toggles a window
        public Type windowClass = null;
        // Optional hotkey for quick access
        public KeyBindingDef hotkey = null;
        // Optional icon path for toolbar buttons
        public string iconPath = null;
        // Tooltip that appears on hover
        public string tooltip = "";
        // Whether to show in quick toolbar (separate from main menu)
        public bool showInToolbar = false;
        // Category for organizing buttons
        public string category = "General";

        public override void ResolveReferences()
        {
            base.ResolveReferences();

            // Auto-fill tooltip if not specified
            if (string.IsNullOrEmpty(tooltip))
            {
                tooltip = description;
            }

            // Auto-detect source mod if not specified
            if (string.IsNullOrEmpty(sourceMod))
            {
                // Try to get mod from the def package
                if (modContentPack != null)
                {
                    sourceMod = modContentPack.Name;
                }
                else
                {
                    sourceMod = "Unknown";
                }
            }

            Logger.Debug($"EnhancedAddonDef resolved: {defName}, Type: {buttonType}, Source: {sourceMod}");
        }

        public IAddonMenu GetAddonMenu()
        {
            try
            {
                if (!enabled)
                {
                    Logger.Debug($"AddonDef {defName} is disabled");
                    return null;
                }

                if (buttonType == ButtonType.Divider) return null;

                // Create appropriate menu based on button type
                switch (buttonType)
                {
                    case ButtonType.MenuButton:
                        var menu = Activator.CreateInstance(menuClass) as IAddonMenu;
                        Logger.Debug($"MenuButton created: {menu != null}");
                        return menu;

                    case ButtonType.DirectDialogButton:
                        // Return a wrapper that opens the dialog directly
                        return new DirectDialogMenuWrapper(this);

                    case ButtonType.ToggleWindowButton:
                        // Return a wrapper that toggles the window
                        return new ToggleWindowMenuWrapper(this);

                    case ButtonType.Divider:
                        return null;

                    default:
                        Logger.Error($"Unknown button type: {buttonType} for {defName}");
                        return null;
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to create addon menu for {defName}: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Directly execute this button's action (for toolbar hotkeys)
        /// </summary>
        public void ExecuteDirectly()
        {
            if (!enabled) return;

            try
            {
                if (buttonType == ButtonType.Divider) return;   // Dividers have no action

                switch (buttonType)
                {
                    case ButtonType.DirectDialogButton when dialogClass != null:
                        var dialog = Activator.CreateInstance(dialogClass) as Window;
                        if (dialog != null)
                        {
                            Find.WindowStack.Add(dialog);
                        }
                        break;

                    case ButtonType.ToggleWindowButton when windowClass != null:
                        var existingWindow = Find.WindowStack.Windows.FirstOrDefault(w => w.GetType() == windowClass);
                        if (existingWindow != null)
                        {
                            existingWindow.Close();
                        }
                        else
                        {
                            var window = Activator.CreateInstance(windowClass) as Window;
                            if (window != null)
                            {
                                Find.WindowStack.Add(window);
                            }
                        }
                        break;

                    case ButtonType.MenuButton:
                        // For menu buttons, show the float menu
                        var menu = GetAddonMenu();
                        if (menu != null)
                        {
                            var options = menu.MenuOptions();
                            Find.WindowStack.Add(new FloatMenu(options));
                        }
                        break;
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to execute button {defName}: {ex.Message}");
            }
        }
    }

    // --- Button Type Enum ---
    public enum ButtonType
    {
        MenuButton,          // Opens a FloatMenu with options
        DirectDialogButton,  // Opens a dialog window directly
        ToggleWindowButton,  // Toggles a window open/closed
        SubmenuButton,       // Opens another FloatMenu (nested)
        Divider              // Visual separator bar — non-clickable, zero logic
    }

    // --- Wrapper Classes for Different Button Types ---

    public class DirectDialogMenuWrapper : IAddonMenu
    {
        private EnhancedChatInteractiveAddonDef def;

        public DirectDialogMenuWrapper(EnhancedChatInteractiveAddonDef def)
        {
            this.def = def;
        }

        public List<FloatMenuOption> MenuOptions()
        {
            Texture2D icon = LoadIcon(def.iconPath);

            // Create FloatMenuOption with icon if available
            FloatMenuOption option;
            if (icon != null)
            {
                // Use constructor with Texture2D icon
                option = new FloatMenuOption(
                    def.label,
                    () =>
                    {
                        if (def.dialogClass != null)
                        {
                            var dialog = Activator.CreateInstance(def.dialogClass) as Window;
                            if (dialog != null)
                            {
                                Find.WindowStack.Add(dialog);
                            }
                        }
                    },
                    iconTex: icon,
                    iconColor: Color.white
                );
            }
            else
            {
                // Use basic constructor without icon
                option = new FloatMenuOption(
                    def.label,
                    () =>
                    {
                        if (def.dialogClass != null)
                        {
                            var dialog = Activator.CreateInstance(def.dialogClass) as Window;
                            if (dialog != null)
                            {
                                Find.WindowStack.Add(dialog);
                            }
                        }
                    }
                );
            }

            // Add tooltip if provided (RimWorld handles tooltips differently)
            if (!string.IsNullOrEmpty(def.tooltip))
            {
                // You can set tooltip using TipSignal if needed
                // Note: RimWorld's FloatMenuOption doesn't have a direct tooltip property in constructor
                // Tooltips are usually handled by TooltipHandler elsewhere
            }

            return new List<FloatMenuOption> { option };
        }

        private static Texture2D LoadIcon(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            return ContentFinder<Texture2D>.Get(path, false);
        }
    }

    public class ToggleWindowMenuWrapper : IAddonMenu
    {
        private EnhancedChatInteractiveAddonDef def;

        public ToggleWindowMenuWrapper(EnhancedChatInteractiveAddonDef def)
        {
            this.def = def;
        }

        public List<FloatMenuOption> MenuOptions()
        {
            Texture2D icon = LoadIcon(def.iconPath);

            // Create FloatMenuOption with icon if available
            FloatMenuOption option;
            if (icon != null)
            {
                // Use constructor with Texture2D icon
                option = new FloatMenuOption(
                    def.label,
                    () =>
                    {
                        if (def.windowClass != null)
                        {
                            var existingWindow = Find.WindowStack.Windows.FirstOrDefault(w => w.GetType() == def.windowClass);
                            if (existingWindow != null)
                            {
                                existingWindow.Close();
                            }
                            else
                            {
                                var window = Activator.CreateInstance(def.windowClass) as Window;
                                if (window != null)
                                {
                                    Find.WindowStack.Add(window);
                                }
                            }
                        }
                    },
                    iconTex: icon,
                    iconColor: Color.white
                );
            }
            else
            {
                // Use basic constructor without icon
                option = new FloatMenuOption(
                    def.label,
                    () =>
                    {
                        if (def.windowClass != null)
                        {
                            var existingWindow = Find.WindowStack.Windows.FirstOrDefault(w => w.GetType() == def.windowClass);
                            if (existingWindow != null)
                            {
                                existingWindow.Close();
                            }
                            else
                            {
                                var window = Activator.CreateInstance(def.windowClass) as Window;
                                if (window != null)
                                {
                                    Find.WindowStack.Add(window);
                                }
                            }
                        }
                    }
                );
            }

            return new List<FloatMenuOption> { option };
        }

        private static Texture2D LoadIcon(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            return ContentFinder<Texture2D>.Get(path, false);
        }
    }
}