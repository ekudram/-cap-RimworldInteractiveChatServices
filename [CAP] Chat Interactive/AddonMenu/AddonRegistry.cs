// AddonRegistry.cs
using CAP_ChatInteractive.Interfaces;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace CAP_ChatInteractive
{
    [StaticConstructorOnStartup]
    public static class AddonRegistry
    {
        public static List<ChatInteractiveAddonDef> AddonDefs { get; private set; }

        static AddonRegistry()
        {
            AddonDefs = DefDatabase<ChatInteractiveAddonDef>.AllDefs
                .Where(def => def.enabled)
                .OrderBy(def => def.displayOrder)
                .ToList();

            Logger.Debug($"Loaded {AddonDefs.Count} addon defs");
        }

        public static IAddonMenu GetMainMenu()
        {
            var mainDef = AddonDefs.FirstOrDefault();
            return mainDef?.GetAddonMenu();
        }
    }
}