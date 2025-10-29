// StoreInventory.cs
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using System.Text;

namespace CAP_ChatInteractive.Store
{
    [StaticConstructorOnStartup]
    public static class StoreInventory
    {
        public static Dictionary<string, StoreItem> AllStoreItems { get; private set; } = new Dictionary<string, StoreItem>();
        private static bool isInitialized = false;
        private static readonly object lockObject = new object();


        // In InitializeStore() - remove the empty database check
        public static void InitializeStore()
        {
            if (isInitialized) return;

            lock (lockObject)
            {
                Logger.Debug("Initializing Store Inventory...");

                // Try to load existing store data
                if (!LoadStoreFromJson())
                {
                    // If no JSON exists, create default store
                    Logger.Debug("No store JSON found, creating default store...");
                    CreateDefaultStore();
                    SaveStoreToJson();
                }
                else
                {
                    // Validate and update store with any new items
                    ValidateAndUpdateStore();
                }

                isInitialized = true;
                Logger.Message($"[CAP] Store Inventory initialized with {AllStoreItems.Count} items");
            }
        }

        private static bool LoadStoreFromJson()
        {
            string jsonContent = JsonFileManager.LoadFile("StoreItems.json");
            if (string.IsNullOrEmpty(jsonContent))
                return false;

            try
            {
                var loadedItems = JsonFileManager.DeserializeStoreItems(jsonContent);
                AllStoreItems.Clear();

                foreach (var kvp in loadedItems)
                {
                    AllStoreItems[kvp.Key] = kvp.Value;
                }

                Logger.Debug($"Loaded {AllStoreItems.Count} items from JSON");
                return true;
            }
            catch (System.Exception e)
            {
                Logger.Error($"Error loading store JSON: {e.Message}");
                return false;
            }
        }

        private static void CreateDefaultStore()
        {

            TestSpecificItems();
            AllStoreItems.Clear();

            var tradeableItems = GetDefaultTradeableItems().ToList();
            Logger.Debug($"Processing {tradeableItems.Count} tradeable items into store items");

            int itemsCreated = 0;
            foreach (var thingDef in tradeableItems)
            {
                try
                {
                    if (!AllStoreItems.ContainsKey(thingDef.defName))
                    {
                        var storeItem = new StoreItem(thingDef);
                        AllStoreItems[thingDef.defName] = storeItem;
                        itemsCreated++;

                        // Log every 100 items to see progress
                        if (itemsCreated % 100 == 0)
                        {
                            Logger.Debug($"Created {itemsCreated} store items so far...");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error($"Error creating store item for {thingDef.defName}: {ex.Message}");
                }
            }

            Logger.Debug($"Created default store with {AllStoreItems.Count} items");

            // Log some final examples
            if (AllStoreItems.Count > 0)
            {
                var finalExamples = AllStoreItems.Take(5).Select(kvp => $"{kvp.Key} (Price: {kvp.Value.BasePrice})");
                Logger.Debug($"Final store examples: {string.Join(", ", finalExamples)}");
            }
        }

        private static void ValidateAndUpdateStore()
        {
            var tradeableItems = GetDefaultTradeableItems();
            int addedItems = 0;
            int removedItems = 0;
            int updatedQuantityLimits = 0;
            int updatedCategories = 0;

            // Add any new items that aren't in the store
            foreach (var thingDef in tradeableItems)
            {
                if (!AllStoreItems.ContainsKey(thingDef.defName))
                {
                    var storeItem = new StoreItem(thingDef);
                    AllStoreItems[thingDef.defName] = storeItem;
                    addedItems++;
                }
                else
                {
                    // Validate and update existing items
                    var existingItem = AllStoreItems[thingDef.defName];
                    var tempStoreItem = new StoreItem(thingDef); // Create temp to get current category

                    // Special case: rename old "Animal" category to "Mechs" for mechanoids
                    if (existingItem.Category == "Animal" && thingDef.race?.IsMechanoid == true)
                    {
                        existingItem.Category = "Mechs";
                        updatedCategories++;
                        Logger.Debug($"Renamed category for {thingDef.defName}: Animal -> Mechs");
                    }
                    // Check if category needs updating (if Def category changed)
                    else if (existingItem.Category != tempStoreItem.Category)
                    {
                        Logger.Debug($"Updating category for {thingDef.defName}: {existingItem.Category} -> {tempStoreItem.Category}");
                        existingItem.Category = tempStoreItem.Category;
                        updatedCategories++;
                    }

                    // Check if quantity limit needs fixing (0 or invalid)
                    if (existingItem.QuantityLimit <= 0)
                    {
                        int baseStack = Mathf.Max(1, thingDef.stackLimit);
                        existingItem.QuantityLimit = baseStack;
                        existingItem.LimitMode = QuantityLimitMode.OneStack;
                        existingItem.HasQuantityLimit = true;
                        Logger.Debug($"Fixed quantity limit for {thingDef.defName}: {existingItem.QuantityLimit}");
                        updatedQuantityLimits++;
                    }
                }
            }

            // Remove items that no longer exist in the game
            var defNamesToRemove = new List<string>();
            foreach (var storeItem in AllStoreItems.Values)
            {
                if (DefDatabase<ThingDef>.GetNamedSilentFail(storeItem.DefName) == null)
                {
                    defNamesToRemove.Add(storeItem.DefName);
                }
            }

            foreach (var defName in defNamesToRemove)
            {
                AllStoreItems.Remove(defName);
                removedItems++;
            }

            // Log all changes
            if (addedItems > 0 || removedItems > 0 || updatedQuantityLimits > 0 || updatedCategories > 0)
            {
                StringBuilder changes = new StringBuilder("Store updated:");
                if (addedItems > 0) changes.Append($" +{addedItems} items");
                if (removedItems > 0) changes.Append($" -{removedItems} items");
                if (updatedQuantityLimits > 0) changes.Append($" {updatedQuantityLimits} quantity limits fixed");
                if (updatedCategories > 0) changes.Append($" {updatedCategories} categories updated");

                Logger.Debug(changes.ToString());
                SaveStoreToJson(); // Save changes
            }
        }

        private static IEnumerable<ThingDef> GetDefaultTradeableItems()
        {
            Logger.Debug("Searching for tradeable items...");

            List<ThingDef> allThingDefs;
            try
            {
                allThingDefs = DefDatabase<ThingDef>.AllDefs.ToList();
            }
            catch (System.Exception ex)
            {
                Logger.Error($"Error accessing ThingDef database: {ex.Message}");
                return new List<ThingDef>();
            }

            Logger.Debug($"Total ThingDefs in database: {allThingDefs.Count}");

            var tradeableItems = allThingDefs
                .Where(t =>
                {
                    try
                    {
                        return t.BaseMarketValue > 0f &&
                               !t.IsCorpse &&
                               t.defName != "Human" &&
                               (t.FirstThingCategory != null || t.race != null);
                    }
                    catch
                    {
                        return false;
                    }
                })
                .ToList();

            Logger.Debug($"Found {tradeableItems.Count} tradeable items");

            return tradeableItems;
        }



        public static void SaveStoreToJson()
        {
            LongEventHandler.QueueLongEvent(() =>
            {
                lock (lockObject)
                {
                    try
                    {
                        string jsonContent = JsonFileManager.SerializeStoreItems(AllStoreItems);
                        JsonFileManager.SaveFile("StoreItems.json", jsonContent);
                        Logger.Debug("Store JSON saved successfully");
                    }
                    catch (System.Exception e)
                    {
                        Logger.Error($"Error saving store JSON: {e.Message}");
                    }
                }
            }, null, false, null, showExtraUIInfo: false, forceHideUI: true);
        }

        public static StoreItem GetStoreItem(string defName)
        {
            return AllStoreItems.TryGetValue(defName, out StoreItem item) ? item : null;
        }

        public static IEnumerable<StoreItem> GetEnabledItems()
        {
            return AllStoreItems.Values.Where(item => item.Enabled);
        }

        public static IEnumerable<StoreItem> GetItemsByCategory(string category)
        {
            return GetEnabledItems().Where(item => item.Category == category);
        }

        public static void OpenQualitySettings()
        {
            Find.WindowStack.Add(new Dialog_QualityResearchSettings());
        }
        private static void DebugThingDefProperties(ThingDef thingDef)
        {
            Logger.Debug($"ThingDef: {thingDef.defName}");
            Logger.Debug($"  Tradeability: {thingDef.tradeability}");
            Logger.Debug($"  BaseMarketValue: {thingDef.BaseMarketValue}");
            Logger.Debug($"  FirstCategory: {thingDef.FirstThingCategory?.defName ?? "None"}");
            Logger.Debug($"  IsCorpse: {thingDef.IsCorpse}");
            Logger.Debug($"  IsBlueprint: {thingDef.IsBlueprint}");
            Logger.Debug($"  IsFrame: {thingDef.IsFrame}");
            // Logger.Debug($"  abstractDef: {thingDef.abstractDef}");
            Logger.Debug($"  Building: {thingDef.building != null}");
            Logger.Debug($"  Minifiable: {thingDef.Minifiable}");
            Logger.Debug($"  Race: {thingDef.race != null}");
        }
        private static void TestSpecificItems()
        {
            var testDefNames = new[] { "MealSimple", "WoodLog", "Steel", "Pemmican", "MedicineHerbal" };

            foreach (var defName in testDefNames)
            {
                var thingDef = DefDatabase<ThingDef>.GetNamedSilentFail(defName);
                if (thingDef != null)
                {
                    Logger.Debug($"Testing {defName}:");
                    DebugThingDefProperties(thingDef);
                }
                else
                {
                    Logger.Debug($"Could not find {defName} in DefDatabase");
                }
            }
        }

    }
    public static class ThingDefExtensions
    {
        public static bool Stackable(this ThingDef thing) => thing.stackLimit > 1;

        public static int GetStackBasedLimit(this ThingDef def, QuantityLimitMode mode)
        {
            int stack = Mathf.Max(1, def.stackLimit);
            return mode switch
            {
                QuantityLimitMode.Each => 1,
                QuantityLimitMode.OneStack => stack,
                QuantityLimitMode.ThreeStacks => stack * 3,
                QuantityLimitMode.FiveStacks => stack * 5,
                _ => 1
            };
        }
    }


}