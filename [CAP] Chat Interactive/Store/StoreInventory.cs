// StoreInventory.cs
// Copyright (c) Captolamia
// This file is part of CAP Chat Interactive.
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

/*
============================================================
RICS STORE ARCHITECTURE - DATA FLOW
============================================================

DATA FLOW:
1. STARTUP: JSON → LoadStoreFromJson() → AllStoreItems Dictionary
2. RUNTIME: All commands use AllStoreItems Dictionary (37 references)
3. CHANGES: UI edits → Update Dictionary → SaveStoreToJson()
4. SHUTDOWN: Dictionary persists in memory/ until next load

KEY PRINCIPLES:
• JSON is PURELY PERSISTENCE - not runtime cache
• All operations use in-memory Dictionary for performance
• Save only on actual data changes (not timed intervals)
• Async saving (LongEventHandler) prevents gameplay stutter

DO NOT:
• Add timed auto-saves (unnecessary disk I/O)
• Read JSON during runtime operations
• Remove async saving without performance testing
• Add locks unless you prove thread contention exists
============================================================
*/
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

            // Case 1: No file exists (fresh install or deleted)
            if (string.IsNullOrEmpty(jsonContent))
            {
                Logger.Debug("No StoreItems.json found - will create defaults");
                return false;
            }

            try
            {
                // Attempt to deserialize
                var loadedItems = JsonFileManager.DeserializeStoreItems(jsonContent);

                // Case 2: File exists but is empty/whitespace
                if (loadedItems == null || loadedItems.Count == 0)
                {
                    Logger.Error("StoreItems.json exists but contains no valid data - corrupted or empty");
                    HandleJsonCorruption("File contains no valid data (empty or malformed JSON)", jsonContent);
                    return false;
                }

                // Success! Load into memory
                AllStoreItems.Clear();
                foreach (var kvp in loadedItems)
                {
                    AllStoreItems[kvp.Key] = kvp.Value;
                }

                Logger.Debug($"Successfully loaded {AllStoreItems.Count} store items from JSON");
                return true;
            }
            catch (Newtonsoft.Json.JsonException jsonEx)  // If using Newtonsoft
            {
                Logger.Error($"JSON CORRUPTION in StoreItems.json: {jsonEx.Message}\n" +
                             $"File may be partially written, damaged, or from incompatible version.\n" +
                             $"Rebuilding with defaults...");
                HandleJsonCorruption($"JSON parsing error: {jsonEx.Message}", jsonContent);
                return false;
            }
            catch (System.IO.IOException ioEx)
            {
                // Disk-level failure - serious hardware issue
                Logger.Error($"DISK ACCESS ERROR reading StoreItems.json: {ioEx.Message}\n" +
                             $"Streamer should check hard drive health immediately!");

                // Show urgent in-game warning
                if (Current.ProgramState == ProgramState.Playing && Find.LetterStack != null)
                {
                    Find.LetterStack.ReceiveLetter(
                        "Chat Interactive: Critical Storage Error",
                        "Chat Interactive cannot read store data due to a disk access error.\n\n" +
                        "This may indicate hardware failure. Check your hard drive health!",
                        LetterDefOf.NegativeEvent
                    );
                }
                return false;
            }
            catch (System.Exception e)
            {
                Logger.Error($"Unexpected error loading store JSON: {e}\n" +
                             $"Rebuilding with defaults...");
                HandleJsonCorruption($"Unexpected error: {e.Message}", jsonContent);
                return false;
            }
        }

        private static void HandleJsonCorruption(string errorDetails, string corruptedJson = null)
        {
            // Option 1: Backup corrupted file (recommended for debugging)
            if (corruptedJson != null && !string.IsNullOrWhiteSpace(corruptedJson))
            {
                try
                {
                    string backupPath = JsonFileManager.GetBackupPath("StoreItems.json");
                    System.IO.File.WriteAllText(backupPath, corruptedJson);
                    Logger.Debug($"Backed up corrupted JSON to: {backupPath}");
                }
                catch { /* Silent fail on backup */ }
            }

            // Option 2: Show in-game notification
            if (Current.ProgramState == ProgramState.Playing)
            {
                string message = "Chat Interactive: Store configuration was corrupted or unreadable.\n" +
                                "Rebuilt with default items. Custom settings have been lost.\n" +
                                "Check logs for details.";

                // Use RimWorld's message system
                Messages.Message(message, MessageTypeDefOf.NegativeEvent);
            }

            // Option 3: Log the corrupted content (first 500 chars for debugging)
            if (corruptedJson != null && corruptedJson.Length > 0)
            {
                string preview = corruptedJson.Length > 500 ?
                    corruptedJson.Substring(0, 500) + "..." :
                    corruptedJson;
                Logger.Debug($"Corrupted JSON preview: {preview}");
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

        // Add this method to StoreInventory.cs
        private static void MigrateStoreItemFormat(StoreItem storeItem, string defName)
        {
            // Ensure DefName is set (this was missing in old versions)
            if (string.IsNullOrEmpty(storeItem.DefName))
            {
                storeItem.DefName = defName;
            }

            // Remove deprecated fields by ensuring they're set to default values
            // These will be ignored during serialization since they no longer exist in the class

            // The following properties are being removed:
            // - Weight (always 1.0)
            // - KarmaType (null)
            // - KarmaTypeForUsing (null) 
            // - KarmaTypeForWearing (null)
            // - KarmaTypeForEquipping (null)
            // - Version (always 2)

            // No action needed - these properties will simply not be serialized in the new format
        }

        // Update the ValidateAndUpdateStore method to include migration
        private static void ValidateAndUpdateStore()
        {
            var tradeableItems = GetDefaultTradeableItems();
            int addedItems = 0;
            int removedItems = 0;
            int updatedQuantityLimits = 0;
            int updatedCategories = 0;
            int updatedTypeFlags = 0;
            int migratedItems = 0; // NEW: Track migrated items

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

                    // MIGRATE: Update item format for existing items
                    MigrateStoreItemFormat(existingItem, thingDef.defName);
                    migratedItems++;

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

                    // Update type flags if they don't match current logic
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
            if (addedItems > 0 || removedItems > 0 || updatedQuantityLimits > 0 || updatedCategories > 0 || updatedTypeFlags > 0 || migratedItems > 0)
            {
                StringBuilder changes = new StringBuilder("Store updated:");
                if (addedItems > 0) changes.Append($" +{addedItems} items");
                if (removedItems > 0) changes.Append($" -{removedItems} items");
                if (updatedQuantityLimits > 0) changes.Append($" {updatedQuantityLimits} quantity limits fixed");
                if (updatedCategories > 0) changes.Append($" {updatedCategories} categories updated");
                if (updatedTypeFlags > 0) changes.Append($" {updatedTypeFlags} type flags updated");
                if (migratedItems > 0) changes.Append($" {migratedItems} items migrated to new format"); // NEW

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
        // Background save method
        public static void SaveStoreToJson()
        {
            LongEventHandler.QueueLongEvent(() =>
            {
                SaveStoreToJsonImmediate();
            }, null, false, null, showExtraUIInfo: false, forceHideUI: true);
        }

        public static void SaveStoreToJsonImmediate()
        {
            lock (lockObject)
            {
                try
                {
                    string jsonContent = JsonFileManager.SerializeStoreItems(AllStoreItems);
                    JsonFileManager.SaveFile("StoreItems.json", jsonContent);
                    Logger.Debug("Store data saved successfully");
                }
                catch (System.Exception e)
                {
                    Logger.Error($"Error saving store JSON: {e.Message}");
                }
            }
        }
        // Background save method unused
        public static void SaveStoreToJsonAsync()
        {
            LongEventHandler.QueueLongEvent(() =>
            {
                SaveStoreToJsonImmediate();
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