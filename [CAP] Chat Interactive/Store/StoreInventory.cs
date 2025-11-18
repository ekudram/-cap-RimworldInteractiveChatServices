// StoreInventory.cs
// Copyright (c) Captolamia. All rights reserved.
// Licensed under the AGPLv3 License. See LICENSE file in the project root for full license information.
// Manages the store inventory for the chat interactive mod
using _CAP__Chat_Interactive.Utilities;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using Verse;

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
            AllStoreItems.Clear();

            var tradeableItems = GetDefaultTradeableItems().ToList();

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
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error($"Error creating store item for {thingDef.defName}: {ex.Message}");
                }
            }

            Logger.Message($"Created store with {AllStoreItems.Count} items");
        }

        private static void ValidateAndUpdateStore()
        {
            var tradeableItems = GetDefaultTradeableItems();
            int addedItems = 0;
            int removedItems = 0;
            int updatedQuantityLimits = 0;
            int updatedCategories = 0;
            int updatedTypeFlags = 0; // NEW: Track type flag updates

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
                    var tempStoreItem = new StoreItem(thingDef); // Create temp to get current values

                    // Special case: rename old "Animal" category to "Mechs" for mechanoids
                    if (existingItem.Category == "Animal" && thingDef.race?.IsMechanoid == true)
                    {
                        existingItem.Category = "Mechs";
                        updatedCategories++;
                    }
                    // Check if category needs updating (if Def category changed)
                    else if (existingItem.Category != tempStoreItem.Category)
                    {
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
                        updatedQuantityLimits++;
                    }

                    // NEW: Update type flags if they don't match current logic
                    if (existingItem.IsUsable != tempStoreItem.IsUsable ||
                        existingItem.IsWearable != tempStoreItem.IsWearable ||
                        existingItem.IsEquippable != tempStoreItem.IsEquippable)
                    {
                        existingItem.IsUsable = tempStoreItem.IsUsable;
                        existingItem.IsWearable = tempStoreItem.IsWearable;
                        existingItem.IsEquippable = tempStoreItem.IsEquippable;
                        updatedTypeFlags++;
                    }
                }
            }

            // Remove items that no longer exist in the game OR are humanlike races
            var defNamesToRemove = new List<string>();
            foreach (var storeItem in AllStoreItems.Values)
            {
                var thingDef = DefDatabase<ThingDef>.GetNamedSilentFail(storeItem.DefName);
                if (thingDef == null)
                {
                    defNamesToRemove.Add(storeItem.DefName);
                }
                else if (thingDef.race?.Humanlike == true || RaceUtils.IsRaceExcluded(thingDef))
                {
                    Logger.Debug($"Removing humanlike race from store during validation: {storeItem.DefName}");
                    defNamesToRemove.Add(storeItem.DefName);
                }
            }

            foreach (var defName in defNamesToRemove)
            {
                AllStoreItems.Remove(defName);
                removedItems++;
            }

            // Log all changes
            if (addedItems > 0 || removedItems > 0 || updatedQuantityLimits > 0 || updatedCategories > 0 || updatedTypeFlags > 0)
            {
                StringBuilder changes = new StringBuilder("Store updated:");
                if (addedItems > 0) changes.Append($" +{addedItems} items");
                if (removedItems > 0) changes.Append($" -{removedItems} items");
                if (updatedQuantityLimits > 0) changes.Append($" {updatedQuantityLimits} quantity limits fixed");
                if (updatedCategories > 0) changes.Append($" {updatedCategories} categories updated");
                if (updatedTypeFlags > 0) changes.Append($" {updatedTypeFlags} type flags updated"); // NEW

                Logger.Message(changes.ToString());
                SaveStoreToJson(); // Save changes
            }
        }

        private static IEnumerable<ThingDef> GetDefaultTradeableItems()
        {
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

            var tradeableItems = allThingDefs
                .Where(t =>
                {
                    try
                    {
                        // Skip humanlike races using RaceUtils
                        if (t.race?.Humanlike == true)
                        {
                            //Logger.Debug($"Excluding humanlike race from store: {t.defName} ({t.label})");
                            return false;
                        }

                        // Skip corpses of humanlike races
                        if (t.IsCorpse && t.race?.Humanlike == true)
                        {
                            //Logger.Debug($"Excluding humanlike corpse from store: {t.defName}");
                            return false;
                        }

                        // Skip if RaceUtils identifies it as excluded
                        if (RaceUtils.IsRaceExcluded(t))
                        {
                            //Logger.Debug($"Excluding race via RaceUtils: {t.defName} ({t.label})");
                            return false;
                        }

                        // Basic tradeable criteria
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

            Logger.Debug($"Found {tradeableItems.Count} tradeable items after filtering out humanlike races");
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