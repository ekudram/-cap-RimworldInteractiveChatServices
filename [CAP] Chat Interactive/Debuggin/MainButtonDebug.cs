// MainButtonDebug.cs
using RimWorld;
using Verse;

namespace CAP_ChatInteractive
{
    [StaticConstructorOnStartup]
    public static class MainButtonDebug
    {
        static MainButtonDebug()
        {
            return; // Disable debug logging by default
            Logger.Debug("=== MAIN BUTTON DEBUG ===");

            // Check if our main button def exists
            var ourButton = DefDatabase<MainButtonDef>.GetNamed("CAPChatInteractive", false);
            if (ourButton != null)
            {
                Logger.Debug($"✅ Found MainButtonDef: {ourButton.defName}");
                Logger.Debug($"  Label: {ourButton.label}");
                Logger.Debug($"  TabWindowClass: {ourButton.tabWindowClass?.Name}");
                Logger.Debug($"  Order: {ourButton.order}");
            }
            else
            {
                Logger.Debug("❌ MainButtonDef 'CAPChatInteractive' not found!");
            }

            // List all main buttons for debugging
            Logger.Debug("All MainButtonDefs:");
            foreach (var button in DefDatabase<MainButtonDef>.AllDefs)
            {
                Logger.Debug($"  - {button.defName} (Order: {button.order}, TabWindow: {button.tabWindowClass?.Name})");
            }

            Logger.Debug("=== END MAIN BUTTON DEBUG ===");
        }
    }
}