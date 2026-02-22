// Copyright (c) Captolamia
// This file is part of: RICS - Rimworld Interactive Chat Services
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
using CAP_ChatInteractive.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace CAP_ChatInteractive
{
    [StaticConstructorOnStartup]
    public static class AddonRegistry
    {
        public static List<EnhancedChatInteractiveAddonDef> AddonDefs { get; private set; }

        static AddonRegistry()
        {
            AddonDefs = DefDatabase<EnhancedChatInteractiveAddonDef>.AllDefs
                .Where(def => def.enabled)
                .OrderBy(def => def.displayOrder)
                .ToList();

            // Logger.Debug($"Loaded {AddonDefs.Count} addon defs");
        }

        public static IAddonMenu GetMainMenu()
        {
            var mainDef = AddonDefs.FirstOrDefault();
            return mainDef?.GetAddonMenu();
        }

        public static void ExecuteAddonDirectly(EnhancedChatInteractiveAddonDef addonDef)
        {
            if (addonDef == null || !addonDef.enabled) return;

            switch (addonDef.buttonType)
            {
                case ButtonType.DirectDialogButton when addonDef.dialogClass != null:
                    var dialog = Activator.CreateInstance(addonDef.dialogClass) as Window;
                    if (dialog != null)
                    {
                        Find.WindowStack.Add(dialog);
                    }
                    break;

                case ButtonType.ToggleWindowButton when addonDef.windowClass != null:
                    var existingWindow = Find.WindowStack.Windows.FirstOrDefault(w => w.GetType() == addonDef.windowClass);
                    if (existingWindow != null)
                    {
                        existingWindow.Close();
                    }
                    else
                    {
                        var window = Activator.CreateInstance(addonDef.windowClass) as Window;
                        if (window != null)
                        {
                            Find.WindowStack.Add(window);
                        }
                    }
                    break;

                case ButtonType.MenuButton:
                    // Fall back to original menu behavior
                    var menu = addonDef.GetAddonMenu();
                    if (menu != null)
                    {
                        var options = menu.MenuOptions();
                        if (options != null && options.Count > 0)
                        {
                            Find.WindowStack.Add(new FloatMenu(options));
                        }
                    }
                    break;
            }
        }
    }
}