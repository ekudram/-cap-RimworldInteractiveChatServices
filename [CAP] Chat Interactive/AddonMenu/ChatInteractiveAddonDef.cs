using System;
using CAP_ChatInteractive.Interfaces;
using Verse;

namespace CAP_ChatInteractive
{
    public class ChatInteractiveAddonDef : Def
    {
        public Type menuClass = typeof(ChatInteractiveAddonMenu);
        public bool enabled = true;
        public int displayOrder = 0;

        public override void ResolveReferences()
        {
            base.ResolveReferences();
            // Logger.Debug($"Resolving AddonDef: {defName}, MenuClass: {menuClass?.Name}");
        }

        public IAddonMenu GetAddonMenu()
        {
            try
            {
                if (!enabled)
                {
                    //Logger.Debug($"AddonDef {defName} is disabled");
                    return null;
                }

                // Logger.Debug($"Creating menu instance for {defName}");
                var menu = Activator.CreateInstance(menuClass) as IAddonMenu;
                // Logger.Debug($"Menu created: {menu != null}");
                return menu;
            }
            catch (Exception ex)
            {
                // Logger.Error($"Failed to create addon menu for {defName}: {ex.Message}");
                return null;
            }
        }
    }
}