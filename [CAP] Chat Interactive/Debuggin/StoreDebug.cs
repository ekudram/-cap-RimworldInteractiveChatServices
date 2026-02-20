// StoreDebug.cs
// Copyright (c) Captolamia
// This file is part of CAP Chat Interactive.
//
// CAP Chat Interactive is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License as published
// by the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// CAP Chat Interactive is distributed in the hope that it will be useful,
// WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
// GNU Affero General Public License for more details.
//
// You should have received a copy of the GNU Affero General Public License
// along with CAP Chat Interactive. If not, see <https://www.gnu.org/licenses/>.
// Debugging utilities for the in-game store system

using LudeonTK;
using RimWorld;
using System;
using System.IO;
using System.Linq;
using Verse;

namespace CAP_ChatInteractive
{
    [StaticConstructorOnStartup]
    public static class StoreDebug
    {
        static StoreDebug()
        {
            // Enable for active debugging sessions
            // RunStoreDebugTests();
        }

        public static void RunStoreDebugTests()
        {
            Logger.Debug("=== STORE DEBUG TESTS ===");

            Logger.Debug($"Store items count: {Store.StoreInventory.AllStoreItems.Count}");

            var categories = Store.StoreInventory.AllStoreItems.Values
                .GroupBy(item => item.Category)
                .OrderByDescending(g => g.Count());

            Logger.Debug("Store categories:");
            foreach (var category in categories)
            {
                Logger.Debug($"  {category.Key}: {category.Count()} items");
            }

            var testItem = Store.StoreInventory.GetStoreItem("MealSimple");
            if (testItem != null)
            {
                Logger.Debug($"Test item - MealSimple: Price={testItem.BasePrice}, Category={testItem.Category}");
            }

            var enabledItems = Store.StoreInventory.GetEnabledItems();
            Logger.Debug($"Enabled items: {enabledItems.Count()}");

            Logger.Debug("=== END STORE DEBUG TESTS ===");
        }

        // ────────────────────────────────────────────────────────────────
        // Debug Action: Delete JSON & Rebuild Store (exact Incidents pattern)
        // ────────────────────────────────────────────────────────────────
        [DebugAction("CAP", "Delete JSON & Rebuild Store", allowedGameStates = AllowedGameStates.Playing)]
        public static void DebugRebuildStore()
        {
            try
            {
                // Delete the store JSON file
                string filePath = JsonFileManager.GetFilePath("StoreItems.json");
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                    Logger.Message("Deleted StoreItems.json file");
                }
                else
                {
                    Logger.Message("No StoreItems.json file found to delete");
                }

                // Reset initialization and rebuild (exact same flow as Incidents)
                Store.StoreInventory.DebugResetForRebuild();
                Store.StoreInventory.InitializeStore();

                Logger.Message($"Store system rebuilt from scratch with current defs/filters. New count: {Store.StoreInventory.AllStoreItems.Count}");
            }
            catch (Exception ex)
            {
                Logger.Error($"Error rebuilding store: {ex.Message}");
            }
        }

        [DebugAction("CAP", "Store: Reset Packs Only (Wearable fix)", allowedGameStates = AllowedGameStates.Playing)]
        public static void DebugResetPacksOnly()
        {
            try
            {
                int fixedCount = 0;

                foreach (var kvp in Store.StoreInventory.AllStoreItems)
                {
                    string defNameLower = kvp.Key.ToLowerInvariant();
                    
                    // Catch anything with "pack" in defName — covers vanilla DLC + most modded utility packs
                    if (defNameLower.Contains("pack"))
                    {
                        var item = kvp.Value;

                        // Only fix if it's currently misclassified
                        if (!item.IsUsable || !item.IsEquippable )
                        {
                            item.IsUsable = false;
                            item.IsEquippable = false;
                            item.IsWearable = true;
                            fixedCount++;

                            Logger.Message($"[Debug] Fixed pack: {kvp.Key} → Wearable only");
                        }
                    }
                }

                if (fixedCount > 0)
                {
                    Store.StoreInventory.SaveStoreToJson();  // Async save
                    Messages.Message(
                        $"Fixed {fixedCount} 'pack' items → now set as Wearable only.\n" +
                        "Other store items unchanged.",
                        MessageTypeDefOf.PositiveEvent
                    );
                }
                else
                {
                    Messages.Message(
                        "No 'pack' items needed fixing — all already correct.",
                        MessageTypeDefOf.NeutralEvent
                    );
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error in Reset Packs debug action: {ex.Message}");
                Messages.Message($"Debug action failed: {ex.Message}", MessageTypeDefOf.NegativeEvent);
            }
        }
    }
}