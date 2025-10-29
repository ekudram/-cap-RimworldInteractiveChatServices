// DefLoaderTest.cs
using RimWorld;
using System.Linq;
using Verse;

namespace CAP_ChatInteractive
{
    [StaticConstructorOnStartup]
    public static class DefLoaderTest
    {
        static DefLoaderTest()
        {
            return; // Disable by default, enable for debugging
            Logger.Debug("=== DEF LOADER TEST ===");

            // Test if our custom def type is recognized
            var ourDefs = DefDatabase<ChatInteractiveAddonDef>.AllDefs;
            Logger.Debug($"ChatInteractiveAddonDef count: {ourDefs.Count()}");

            // Test if we can find our specific def
            var ourDef = DefDatabase<ChatInteractiveAddonDef>.GetNamed("CAPChatInteractive", false);
            if (ourDef != null)
            {
                Logger.Debug($"✅ Found our AddonDef: {ourDef.defName}");
                Logger.Debug($"  Label: {ourDef.label}");
                Logger.Debug($"  MenuClass: {ourDef.menuClass?.Name}");
                Logger.Debug($"  Enabled: {ourDef.enabled}");
            }
            else
            {
                Logger.Debug("❌ Our AddonDef not found!");

                // List all defs of our type to see what's loading
                Logger.Debug("All ChatInteractiveAddonDefs:");
                foreach (var def in ourDefs)
                {
                    Logger.Debug($"  - {def.defName}: {def.label}");
                }
            }

            Logger.Debug("=== END DEF LOADER TEST ===");
        }
    }
}