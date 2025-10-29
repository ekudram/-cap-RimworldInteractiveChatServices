using RimWorld;
using UnityEngine;
using Verse;

namespace CAP_ChatInteractive.Windows
{
    public class MainTabWindow_ChatInteractive : MainTabWindow
    {
        public MainTabWindow_ChatInteractive()
        {
            // Logger.Debug("MainTabWindow_ChatInteractive constructor called");
        }

        public override void DoWindowContents(Rect inRect)
        {
            // Logger.Debug("MainTabWindow_ChatInteractive.DoWindowContents called");

            var listing = new Listing_Standard();
            listing.Begin(inRect);

            listing.Label("CAP Chat Interactive - Quick Menu");
            listing.GapLine();

            // Logger.Debug($"Found {AddonRegistry.AddonDefs.Count} addon defs");

            foreach (var addonDef in AddonRegistry.AddonDefs)
            {
                Logger.Debug($"Processing addon: {addonDef.defName}");
                if (listing.ButtonText(addonDef.label))
                {
                    var menu = addonDef.GetAddonMenu();
                    if (menu != null)
                    {
                        var options = menu.MenuOptions();
                        if (options != null && options.Count > 0)
                        {
                            Find.WindowStack.Add(new FloatMenu(options));
                        }
                        else
                        {
                            Messages.Message("No menu options available for this addon", MessageTypeDefOf.NeutralEvent);
                        }
                    }
                }
            }

            listing.End();
        }

        public override Vector2 RequestedTabSize => new Vector2(300f, 100f + (AddonRegistry.AddonDefs.Count * 32f));

        public override MainTabWindowAnchor Anchor => MainTabWindowAnchor.Left;

        public override void PostOpen()
        {
            base.PostOpen();
            Logger.Debug("MainTabWindow_ChatInteractive opened");
        }
    }
}