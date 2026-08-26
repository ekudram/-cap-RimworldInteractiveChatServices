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
// Def type for RICS main-tab / toolbar buttons. Third-party mods can ship their own
// EnhancedChatInteractiveAddonDef XML (preferred) or use ButtonUtils at runtime (toolbar only).
// See Defs/AddonDefs/ChatInteractiveAddon.xml for a full modder cookbook.
using System;
using System.Collections.Generic;
using CAP_ChatInteractive.Interfaces;
using CAP_ChatInteractive.Ownership;
using UnityEngine;
using Verse;

namespace CAP_ChatInteractive
{
    /// <summary>
    /// Registers a button on the RICS main tab and optionally the top quick toolbar.
    /// Place defs in any mod's Defs/ folder with this type name.
    /// </summary>
    public class EnhancedChatInteractiveAddonDef : Def
    {
        /// <summary>
        /// For <see cref="ButtonType.MenuButton"/>: type implementing <see cref="IAddonMenu"/>.
        /// Default is RICS's built-in menu when omitted on the main CAPChatInteractive def.
        /// </summary>
        public Type menuClass = typeof(ChatInteractiveAddonMenu);

        /// <summary>When false, def is ignored by AddonRegistry and toolbar.</summary>
        public bool enabled = true;

        /// <summary>
        /// Sort order. RICS uses ~85–101. Third-party mods should use 200+ to avoid collisions.
        /// </summary>
        public int displayOrder = 10;

        /// <summary>
        /// Short name for grouping (toolbar separators / main-tab headers). Defaults to package mod name.
        /// </summary>
        public string sourceMod = "RICS";

        /// <summary>Determines click behavior. See <see cref="ButtonType"/>.</summary>
        public ButtonType buttonType = ButtonType.MenuButton;

        /// <summary>For DirectDialogButton: Window type with a parameterless constructor.</summary>
        public Type dialogClass = null;

        /// <summary>For ToggleWindowButton: Window type toggled open/closed.</summary>
        public Type windowClass = null;

        /// <summary>Optional hotkey (toolbar / CheckHotkeys).</summary>
        public KeyBindingDef hotkey = null;

        /// <summary>Texture path under Textures/ (e.g. UI/QuickButtons/MyIcon).</summary>
        public string iconPath = null;

        /// <summary>Hover tip; falls back to description if empty.</summary>
        public string tooltip = "";

        /// <summary>When true, appears on the top-of-screen quick toolbar during play.</summary>
        public bool showInToolbar = false;

        /// <summary>Optional organizational tag (not currently required for layout).</summary>
        public string category = "General";

        /// <summary>
        /// When true, hide this button unless RICS pawn ownership is active
        /// (setting on and Possessions Plus not loaded). Checked every draw, not only at load.
        /// </summary>
        public bool requireRicsOwnership = false;

        public bool IsCurrentlyVisible()
        {
            if (!enabled)
                return false;
            if (requireRicsOwnership && !RICS_OwnershipUtility.IsRicsOwnershipActive())
                return false;
            return true;
        }

        public override void ResolveReferences()
        {
            base.ResolveReferences();

            if (string.IsNullOrEmpty(tooltip))
                tooltip = description ?? string.Empty;

            if (string.IsNullOrEmpty(sourceMod))
            {
                sourceMod = modContentPack != null
                    ? (modContentPack.Name ?? "Unknown")
                    : "Unknown";
            }
        }

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string err in base.ConfigErrors())
                yield return err;

            if (buttonType == ButtonType.Divider)
                yield break;

            if (buttonType == ButtonType.MenuButton)
            {
                if (menuClass == null)
                    yield return "MenuButton requires menuClass";
                else if (!typeof(IAddonMenu).IsAssignableFrom(menuClass))
                    yield return $"menuClass {menuClass.Name} must implement IAddonMenu";
            }
            else if (buttonType == ButtonType.DirectDialogButton)
            {
                if (dialogClass == null)
                    yield return "DirectDialogButton requires dialogClass";
                else if (!typeof(Window).IsAssignableFrom(dialogClass))
                    yield return $"dialogClass {dialogClass.Name} must inherit from Window";
            }
            else if (buttonType == ButtonType.ToggleWindowButton)
            {
                if (windowClass == null)
                    yield return "ToggleWindowButton requires windowClass";
                else if (!typeof(Window).IsAssignableFrom(windowClass))
                    yield return $"windowClass {windowClass.Name} must inherit from Window";
            }
            else if (buttonType == ButtonType.SubmenuButton)
            {
                // Reserved for nested menus; treat like MenuButton if menuClass set
                if (menuClass == null || !typeof(IAddonMenu).IsAssignableFrom(menuClass))
                    yield return "SubmenuButton requires menuClass implementing IAddonMenu (same as MenuButton)";
            }
        }

        /// <summary>
        /// Build the IAddonMenu used when this def is shown as a FloatMenu entry list.
        /// </summary>
        public IAddonMenu GetAddonMenu()
        {
            try
            {
                if (!IsCurrentlyVisible() || buttonType == ButtonType.Divider)
                    return null;

                switch (buttonType)
                {
                    case ButtonType.MenuButton:
                    case ButtonType.SubmenuButton:
                        if (menuClass == null || !typeof(IAddonMenu).IsAssignableFrom(menuClass))
                        {
                            Logger.Error($"[AddonMenu] {defName}: invalid menuClass");
                            return null;
                        }

                        return Activator.CreateInstance(menuClass) as IAddonMenu;

                    case ButtonType.DirectDialogButton:
                        return new DirectDialogMenuWrapper(this);

                    case ButtonType.ToggleWindowButton:
                        return new ToggleWindowMenuWrapper(this);

                    default:
                        Logger.Error($"[AddonMenu] Unknown button type {buttonType} for {defName}");
                        return null;
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"[AddonMenu] Failed to create menu for {defName}: {ex}");
                return null;
            }
        }

        /// <summary>
        /// Run this button's primary action (toolbar click, main-tab click, hotkey).
        /// Safe: errors are logged, never thrown to UI.
        /// </summary>
        public void ExecuteDirectly()
        {
            if (!IsCurrentlyVisible() || buttonType == ButtonType.Divider)
                return;

            string ctx = defName ?? label ?? "addon";

            switch (buttonType)
            {
                case ButtonType.DirectDialogButton:
                    AddonButtonActions.TryOpenDialog(dialogClass, ctx);
                    break;

                case ButtonType.ToggleWindowButton:
                    AddonButtonActions.TryToggleWindow(windowClass, ctx);
                    break;

                case ButtonType.MenuButton:
                case ButtonType.SubmenuButton:
                    AddonButtonActions.TryShowMenu(GetAddonMenu(), ctx);
                    break;
            }
        }
    }

    /// <summary>
    /// Behavior for <see cref="EnhancedChatInteractiveAddonDef.buttonType"/>.
    /// </summary>
    public enum ButtonType
    {
        /// <summary>Opens a FloatMenu from menuClass.MenuOptions().</summary>
        MenuButton,

        /// <summary>Opens dialogClass as a Window immediately.</summary>
        DirectDialogButton,

        /// <summary>Toggles windowClass open/closed.</summary>
        ToggleWindowButton,

        /// <summary>Same as MenuButton (nested menu entry). Prefer MenuButton for new content.</summary>
        SubmenuButton,

        /// <summary>Visual separator only — unique defName required; no click action.</summary>
        Divider
    }

    /// <summary>Wraps a DirectDialogButton as a single FloatMenu option (for menus that list it).</summary>
    public class DirectDialogMenuWrapper : IAddonMenu
    {
        private readonly EnhancedChatInteractiveAddonDef def;

        public DirectDialogMenuWrapper(EnhancedChatInteractiveAddonDef def)
        {
            this.def = def;
        }

        public List<FloatMenuOption> MenuOptions()
        {
            if (def == null)
                return new List<FloatMenuOption>();

            Texture2D icon = AddonButtonActions.LoadIcon(def.iconPath);
            return new List<FloatMenuOption>
            {
                AddonButtonActions.CreateFloatOption(
                    def.label,
                    () => AddonButtonActions.TryOpenDialog(def.dialogClass, def.defName),
                    icon)
            };
        }
    }

    /// <summary>Wraps a ToggleWindowButton as a single FloatMenu option.</summary>
    public class ToggleWindowMenuWrapper : IAddonMenu
    {
        private readonly EnhancedChatInteractiveAddonDef def;

        public ToggleWindowMenuWrapper(EnhancedChatInteractiveAddonDef def)
        {
            this.def = def;
        }

        public List<FloatMenuOption> MenuOptions()
        {
            if (def == null)
                return new List<FloatMenuOption>();

            Texture2D icon = AddonButtonActions.LoadIcon(def.iconPath);
            return new List<FloatMenuOption>
            {
                AddonButtonActions.CreateFloatOption(
                    def.label,
                    () => AddonButtonActions.TryToggleWindow(def.windowClass, def.defName),
                    icon)
            };
        }
    }
}
