// StoreDebug.cs
using RimWorld;
using System.Linq;
using Verse;

namespace CAP_ChatInteractive
{
    [StaticConstructorOnStartup]
    public static class StoreDebug
    {
        static StoreDebug()
        {
            // Enable for debugging, disable for release
            // RunStoreDebugTests();
        }

        public static void RunStoreDebugTests()
        {
            Logger.Debug("=== STORE DEBUG TESTS ===");

            // Test store initialization
            Logger.Debug($"Store items count: {Store.StoreInventory.AllStoreItems.Count}");

            // Test category breakdown
            var categories = Store.StoreInventory.AllStoreItems.Values
                .GroupBy(item => item.Category)
                .OrderByDescending(g => g.Count());

            Logger.Debug("Store categories:");
            foreach (var category in categories)
            {
                Logger.Debug($"  {category.Key}: {category.Count()} items");
            }

            // Test specific item lookup
            var testItem = Store.StoreInventory.GetStoreItem("MealSimple");
            if (testItem != null)
            {
                Logger.Debug($"Test item - MealSimple: Price={testItem.BasePrice}, Category={testItem.Category}");
            }

            // Test enabled items
            var enabledItems = Store.StoreInventory.GetEnabledItems();
            Logger.Debug($"Enabled items: {enabledItems.Count()}");

            Logger.Debug("=== END STORE DEBUG TESTS ===");
        }
    }
}