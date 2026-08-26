// AddonButtonActions.cs
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
// Shared safe open/toggle/menu helpers for the RICS addon button system.
// Third-party mods should not call this directly — use EnhancedChatInteractiveAddonDef
// or ButtonUtils / XML Defs instead.
using CAP_ChatInteractive.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace CAP_ChatInteractive
{
    internal static class AddonButtonActions
    {
        /// <summary>Run an action with logging; never throw into the UI thread.</summary>
        public static void SafeRun(string context, Action action)
        {
            if (action == null)
                return;

            try
            {
                action();
            }
            catch (Exception ex)
            {
                Logger.Error($"[AddonMenu] {context}: {ex}");
            }
        }

        public static bool TryOpenDialog(Type dialogClass, string context = null)
        {
            string ctx = context ?? dialogClass?.Name ?? "dialog";

            if (dialogClass == null)
            {
                Logger.Error($"[AddonMenu] {ctx}: dialogClass is null");
                return false;
            }

            if (!typeof(Window).IsAssignableFrom(dialogClass))
            {
                Logger.Error($"[AddonMenu] {ctx}: {dialogClass.FullName} must inherit from Verse.Window");
                return false;
            }

            if (Find.WindowStack == null)
            {
                Logger.Warning($"[AddonMenu] {ctx}: WindowStack not ready");
                return false;
            }

            try
            {
                if (Activator.CreateInstance(dialogClass) is Window window)
                {
                    Find.WindowStack.Add(window);
                    return true;
                }

                Logger.Error($"[AddonMenu] {ctx}: CreateInstance did not return a Window");
                return false;
            }
            catch (Exception ex)
            {
                Logger.Error($"[AddonMenu] {ctx}: failed to open dialog: {ex}");
                return false;
            }
        }

        public static bool TryToggleWindow(Type windowClass, string context = null)
        {
            string ctx = context ?? windowClass?.Name ?? "window";

            if (windowClass == null)
            {
                Logger.Error($"[AddonMenu] {ctx}: windowClass is null");
                return false;
            }

            if (!typeof(Window).IsAssignableFrom(windowClass))
            {
                Logger.Error($"[AddonMenu] {ctx}: {windowClass.FullName} must inherit from Verse.Window");
                return false;
            }

            if (Find.WindowStack == null)
            {
                Logger.Warning($"[AddonMenu] {ctx}: WindowStack not ready");
                return false;
            }

            try
            {
                Window existing = Find.WindowStack.Windows.FirstOrDefault(w => w != null && w.GetType() == windowClass);
                if (existing != null)
                {
                    existing.Close();
                    return true;
                }

                if (Activator.CreateInstance(windowClass) is Window window)
                {
                    Find.WindowStack.Add(window);
                    return true;
                }

                Logger.Error($"[AddonMenu] {ctx}: CreateInstance did not return a Window");
                return false;
            }
            catch (Exception ex)
            {
                Logger.Error($"[AddonMenu] {ctx}: failed to toggle window: {ex}");
                return false;
            }
        }

        public static bool TryShowMenu(IAddonMenu menu, string context = null)
        {
            string ctx = context ?? "menu";

            if (menu == null)
            {
                Logger.Error($"[AddonMenu] {ctx}: menu is null");
                return false;
            }

            if (Find.WindowStack == null)
            {
                Logger.Warning($"[AddonMenu] {ctx}: WindowStack not ready");
                return false;
            }

            try
            {
                List<FloatMenuOption> options = menu.MenuOptions();
                if (options == null || options.Count == 0)
                {
                    Logger.Warning($"[AddonMenu] {ctx}: MenuOptions returned no options");
                    return false;
                }

                Find.WindowStack.Add(new FloatMenu(options));
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error($"[AddonMenu] {ctx}: failed to show float menu: {ex}");
                return false;
            }
        }

        public static FloatMenuOption CreateFloatOption(string label, Action action, Texture2D icon = null)
        {
            string safeLabel = string.IsNullOrEmpty(label) ? "…" : label;
            Action wrapped = () => SafeRun(safeLabel, action);

            if (icon != null)
            {
                return new FloatMenuOption(
                    safeLabel,
                    wrapped,
                    iconTex: icon,
                    iconColor: Color.white);
            }

            return new FloatMenuOption(safeLabel, wrapped);
        }

        public static Texture2D LoadIcon(string path)
        {
            if (string.IsNullOrEmpty(path))
                return null;

            try
            {
                return ContentFinder<Texture2D>.Get(path, false);
            }
            catch (Exception ex)
            {
                Logger.Warning($"[AddonMenu] Failed to load icon '{path}': {ex.Message}");
                return null;
            }
        }
    }
}
