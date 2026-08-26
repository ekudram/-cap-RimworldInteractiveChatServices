// AddonRegistry.cs
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
//
// Loads all enabled EnhancedChatInteractiveAddonDef at startup for the main tab.
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
        /// <summary>Enabled addon defs sorted by displayOrder (main tab source of truth).</summary>
        public static List<EnhancedChatInteractiveAddonDef> AddonDefs { get; private set; }
            = new List<EnhancedChatInteractiveAddonDef>();

        static AddonRegistry()
        {
            try
            {
                Refresh();
            }
            catch (Exception ex)
            {
                Logger.Error($"[AddonMenu] AddonRegistry failed to initialize: {ex}");
                AddonDefs = new List<EnhancedChatInteractiveAddonDef>();
            }
        }

        /// <summary>Rebuild the enabled-def list (e.g. after dynamic registration if ever needed).</summary>
        public static void Refresh()
        {
            AddonDefs = DefDatabase<EnhancedChatInteractiveAddonDef>.AllDefs
                .Where(def => def != null && def.enabled)
                .OrderBy(def => def.displayOrder)
                .ToList();
        }

        /// <summary>
        /// Primary RICS menu (def CAPChatInteractive), or first MenuButton fallback.
        /// </summary>
        public static IAddonMenu GetMainMenu()
        {
            try
            {
                var mainDef = AddonDefs.FirstOrDefault(d => d.defName == "CAPChatInteractive")
                              ?? AddonDefs.FirstOrDefault(d => d.buttonType == ButtonType.MenuButton);
                return mainDef?.GetAddonMenu();
            }
            catch (Exception ex)
            {
                Logger.Error($"[AddonMenu] GetMainMenu failed: {ex}");
                return null;
            }
        }

        /// <summary>
        /// Execute an addon def (same as <see cref="EnhancedChatInteractiveAddonDef.ExecuteDirectly"/>).
        /// Kept for callers that used the old Registry API.
        /// </summary>
        public static void ExecuteAddonDirectly(EnhancedChatInteractiveAddonDef addonDef)
        {
            if (addonDef == null || !addonDef.IsCurrentlyVisible())
                return;

            addonDef.ExecuteDirectly();
        }
    }
}
