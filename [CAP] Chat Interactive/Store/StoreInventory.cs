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
        private static Dictionary<string, StoreItem> _completeStoreData = new Dictionary<string, StoreItem>();
        public static IReadOnlyDictionary<string, StoreItem> CompleteStoreData => _completeStoreData;


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
            {
                Logger.Debug("No StoreItems.json found - will create defaults");
                return false;
            }

            try
            {
                var loadedItems = JsonFileManager.DeserializeStoreItems(jsonContent);

                if (loadedItems == null)
                {
                    Logger.Error("StoreItems.json exists but contains no valid data");
                    HandleJsonCorruption("File contains no valid data (empty or malformed JSON)", jsonContent);
                    return false;
                }

                // Store COMPLETE data (preserves all items ever saved)
                _completeStoreData.Clear();
                foreach (var kvp in loadedItems)
                {
                    _completeStoreData[kvp.Key] = kvp.Value;
                }

                // Now filter to ACTIVE items for runtime use
                RebuildActiveStoreFromCompleteData();

                Logger.Debug($"Loaded {_completeStoreData.Count} total items from JSON, {AllStoreItems.Count} active");
                return true;
            }
            catch (Newtonsoft.Json.JsonException jsonEx)
            {
                Logger.Error($"JSON CORRUPTION in StoreItems.json: {jsonEx.Message}");
                HandleJsonCorruption($"JSON parsing error: {jsonEx.Message}", jsonContent);
                return false;
            }
            catch (System.IO.IOException ioEx)
            {
                Logger.Error($"DISK ACCESS ERROR reading StoreItems.json: {ioEx.Message}");
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
                Logger.Error($"Unexpected error loading store JSON: {e}");
                HandleJsonCorruption($"Unexpected error: {e.Message}", jsonContent);
                return false;
            }
        }

        private static void RebuildActiveStoreFromCompleteData()
        {
            AllStoreItems.Clear();

            var tradeableItems = GetDefaultTradeableItems().ToList();
            var activeDefNames = new HashSet<string>(tradeableItems.Select(t => t.defName));

            // First, add all existing items from complete data (JSON settings)
            foreach (var kvp in _completeStoreData)
            {
                if (activeDefNames.Contains(kvp.Key))
                {
                    // Item is from an active mod - use JSON settings exactly as saved
                    AllStoreItems[kvp.Key] = kvp.Value;
                }
            }

            // Now check for NEW items that aren't in JSON
            foreach (var thingDef in tradeableItems)
            {
                string key = thingDef.defName;
                if (!_completeStoreData.ContainsKey(key))
                {
                    // This is a NEW item - create it
                    var storeItem = new StoreItem(thingDef);
                    _completeStoreData[key] = storeItem;

                    // Add to runtime
                    AllStoreItems[key] = storeItem;

                    Logger.Debug($"Added NEW store item from constructor: {key} from {thingDef.modContentPack?.Name ?? "Unknown"}");
                }
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
            _completeStoreData.Clear();

            var tradeableItems = GetDefaultTradeableItems().ToList();

            int itemsCreated = 0;
            foreach (var thingDef in tradeableItems)
            {
                try
                {
                    if (!_completeStoreData.ContainsKey(thingDef.defName))
                    {
                        var storeItem = new StoreItem(thingDef);
                        _completeStoreData[thingDef.defName] = storeItem;
                        AllStoreItems[thingDef.defName] = storeItem;
                        itemsCreated++;
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error($"Error creating store item for {thingDef.defName}: {ex.Message}");
                }
            }

            Logger.Message($"Created store with {AllStoreItems.Count} items");
        }

        // Migrate old StoreItem formats to new structure
        private static void MigrateStoreItemFormat(StoreItem storeItem, string defName)
        {
            // Ensure DefName is set (this was missing in old versions)
            if (string.IsNullOrEmpty(storeItem.DefName))
            {
                storeItem.DefName = defName;
            }
        }

        // Validate and update store items
        private static void ValidateAndUpdateStore()
        {
            var tradeableItems = GetDefaultTradeableItems().ToList();
            var activeDefNames = new HashSet<string>(tradeableItems.Select(t => t.defName));

            int addedItems = 0;
            int updatedItems = 0;
            int removedItems = 0;
            int migratedItems = 0;
            int removedInvalidItems = 0;

            Logger.Message("=== Validating and updating store items... ===");
            Logger.Debug($"Current store items: {AllStoreItems.Count}");
            Logger.Debug($"Tradeable items in game: {tradeableItems.Count}");

            // Check for NEW items not in JSON
            foreach (var thingDef in tradeableItems)
            {
                string key = thingDef.defName;

                if (!_completeStoreData.ContainsKey(key))
                {
                    // New item - create it
                    var newItem = new StoreItem(thingDef);
                    _completeStoreData[key] = newItem;
                    AllStoreItems[key] = newItem;
                    addedItems++;
                }
                else if (_completeStoreData.ContainsKey(key))
                {
                    // Existing item - preserve user settings but update game properties
                    var existingItem = _completeStoreData[key];

                    // Store user settings before any updates
                    bool userEnabled = existingItem.Enabled;
                    string userCustomName = existingItem.CustomName;
                    int userBasePrice = existingItem.BasePrice;
                    bool userHasQuantityLimit = existingItem.HasQuantityLimit;
                    int userQuantityLimit = existingItem.QuantityLimit;
                    QuantityLimitMode userLimitMode = existingItem.LimitMode;
                    bool userIsUsable = existingItem.IsUsable;
                    bool userIsEquippable = existingItem.IsEquippable;
                    bool userIsWearable = existingItem.IsWearable;

                    // MIGRATE: Update item format for existing items
                    MigrateStoreItemFormat(existingItem, thingDef.defName);
                    migratedItems++;

                    // Update game-derived properties
                    // Special case: rename old "Animal" category to "Mechs" for mechanoids
                    if (existingItem.Category == "Animal" && thingDef.race?.IsMechanoid == true)
                    {
                        existingItem.Category = "Mechs";
                    }
                    else if ("VehiclePawn".Equals(thingDef.thingClass?.Name, StringComparison.OrdinalIgnoreCase))
                    {
                        existingItem.Category = "Vehicles";
                    }
                    else if (thingDef.thingClass?.FullName?.Contains("vehiclePawn") == true)
                    {
                        existingItem.Category = "Vehicles";
                    }
                    else
                    {
                        // Update category from thing def
                        string newCategory = StoreItem.GetCategoryFromThingDef(thingDef) ?? "Uncategorized";
                        if (existingItem.Category != newCategory)
                        {
                            existingItem.Category = newCategory;
                        }
                    }

                    // Update mod source
                    existingItem.ModSource = thingDef.modContentPack?.Name ?? "RimWorld";

                    // Check if quantity limit needs fixing (0 or invalid)
                    if (existingItem.QuantityLimit <= 0)
                    {
                        int baseStack = Mathf.Max(1, thingDef.stackLimit);
                        existingItem.QuantityLimit = baseStack;
                        existingItem.LimitMode = QuantityLimitMode.OneStack;
                        existingItem.HasQuantityLimit = true;
                    }

                    // CRITICAL: Restore ALL user settings from JSON
                    existingItem.Enabled = userEnabled;
                    existingItem.CustomName = userCustomName;
                    existingItem.BasePrice = userBasePrice;
                    existingItem.HasQuantityLimit = userHasQuantityLimit;
                    existingItem.QuantityLimit = userQuantityLimit;
                    existingItem.LimitMode = userLimitMode;
                    existingItem.IsUsable = userIsUsable;
                    existingItem.IsEquippable = userIsEquippable;
                    existingItem.IsWearable = userIsWearable;

                    updatedItems++;
                }
            }

            // Remove items from runtime that are no longer active
            var keysToRemove = AllStoreItems.Keys.Where(k => !activeDefNames.Contains(k)).ToList();
            foreach (var key in keysToRemove)
            {
                AllStoreItems.Remove(key);
                removedItems++;
            }

            // Mark all active items as modactive = true for online store
            // Note: You'll need to add a modactive property to StoreItem class first
            foreach (var item in AllStoreItems.Values)
            {
                item.modactive = true; // Add this property to StoreItem
            }

            // Update logging
            if (addedItems > 0 || removedItems > 0 || updatedItems > 0 || migratedItems > 0)
            {
                StringBuilder changes = new StringBuilder("Store updated:");
                if (addedItems > 0) changes.Append($" +{addedItems} items");
                if (removedItems > 0) changes.Append($" -{removedItems} items");
                if (updatedItems > 0) changes.Append($" ~{updatedItems} items updated");
                if (migratedItems > 0) changes.Append($" {migratedItems} items migrated");

                Logger.Message(changes.ToString());
            }
            SaveStoreToJson(); // Save changes
        }

        private static bool IsItemValidForStore(ThingDef thingDef)
        {
            if (thingDef == null) return false;

            // Check for missing critical components
            //if (HasMissingGraphics(thingDef))
            //{
            //    Logger.Debug($"Excluding {thingDef.defName} - Missing graphics/components");
            //    return false;
            //}

            // Skip items that are likely vehicles or complex structures
            if (IsLikelyProblematicItem(thingDef))
            {
                Logger.Debug($"Excluding potentially problematic item: {thingDef.defName}");
                return false;
            }

            return true;
        }

        private static bool HasMissingGraphics(ThingDef thingDef)
        {
            //// Check for missing graphic data
            //if (thingDef.graphicData == null)
            //{
            //    Logger.Debug($"{thingDef.defName} has null graphicData");
            //    return true;
            //}

            // Check for missing icon textures
            //if (thingDef.uiIcon == null || thingDef.uiIcon == BaseContent.BadTex)
            //{
            //    //Logger.Debug($"{thingDef.defName} has missing/invalid uiIcon");
            //    return true;
            //}

            // Check for missing graphic class (for complex items like vehicles)
            // Some items might still be valid without graphicClass, but log it
            if (thingDef.graphicData?.graphicClass == null)
            {
                // Logger.Debug($"{thingDef.defName} has no graphicClass specified");
                // Don't return true here - some items might be valid without graphicClass
            }

            return false;
        }

        private static bool ShouldRemoveStoreItem(string defName,
            ThingDef thingDef, IEnumerable<ThingDef> tradeableItems, out string reason)
        {
            reason = null;

            if (thingDef == null)
            {
                // but is it in the dictionary?
                reason = "Def no longer exists in database";
                return true;
            }

            if (thingDef.race?.Humanlike == true || RaceUtils.IsRaceExcluded(thingDef))
            {
                reason = "Humanlike or excluded race";
                return true;
            }

            //if (!IsItemValidForStore(thingDef))
            //{
            //    reason = "Failed item validation (missing graphics, etc.)";
            //    return true;
            //}

            if (!tradeableItems.Any(t => t.defName == defName))
            {
                reason = "Not in valid tradeable items list";
                return true;
            }

            return false;
        }
        private static bool IsLikelyProblematicItem(ThingDef thingDef)
        {
            // Skip items that are clearly vehicles or complex structures
            string defName = thingDef.defName ?? "";

            // Check for vehicle-related patterns in defName From Looking at Mods for vehicles
            if (defName.Contains("VehiclePawn")) // ||
            {
                return true;
            }

            // Check for tradeability - items that can't be traded shouldn't be in store
            //if (thingDef.tradeability == Tradeability.None)
            //{
            //    return true;
            //}

            // Check if item has vehicle or complex components
            if (thingDef.comps != null)
            {
                foreach (var comp in thingDef.comps)
                {
                    string compClassName = comp.compClass?.FullName ?? "";
                    if (compClassName.Contains("CompVehicleMovementController") ||
                        compClassName.Contains("CompVehicleTurrets"))
                    {
                        return true;
                    }
                }
            }

            // Check for items that can't be placed/minified (like vehicles)
            //if (thingDef.placeWorkers != null && thingDef.placeWorkers.Count > 0)
            //{
            //    // Some placeWorkers might indicate complex placement logic
            //    Logger.Debug($"{thingDef.defName} has placeWorkers - may be complex item");
            //}

            // Check for items with special designators (vehicles often have these)
            //if (thingDef.designatorDropdown != null ||
            //    thingDef.inspectorTabs != null && thingDef.inspectorTabs.Count > 0)
            //{
            //    Logger.Debug($"{thingDef.defName} has complex UI elements - may be vehicle/structure");
            //}

            return false;
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
                            return false;
                        }

                        // Skip corpses of humanlike races
                        if (t.IsCorpse && t.race?.Humanlike == true)
                        {
                            return false;
                        }

                        // Skip if RaceUtils identifies it as excluded
                        if (RaceUtils.IsRaceExcluded(t))
                        {
                            return false;
                        }

                        if (t.thingClass != null && t.thingClass.Name == "VehiclePawn")
                        {
                            return false;
                        }

                        // NEW: Validate item graphics and problematic items
                        if (!IsItemValidForStore(t))
                        {
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

            Logger.Debug($"Found {tradeableItems.Count} tradeable items after filtering");
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
                    string jsonContent = JsonFileManager.SerializeStoreItems(_completeStoreData);
                    JsonFileManager.SaveFile("StoreItems.json", jsonContent);
                    Logger.Debug($"Store data saved successfully. Total items: {_completeStoreData?.Count ?? 0}, Active: {AllStoreItems?.Count ?? 0}");
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