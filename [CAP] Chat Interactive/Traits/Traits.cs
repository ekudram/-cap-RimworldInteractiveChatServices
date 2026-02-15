// Traits.cs
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
// Manages the loading, saving, and retrieval of buyable traits for pawns.

/// <summary>
/// Manages the loading, saving, and retrieval of buyable traits for pawns.
/// Handles JSON persistence, trait validation/updates, and DLC-specific defaults.
/// Anomaly DLC traits are disabled by default to accommodate streamer preferences.
/// </summary>

using LudeonTK;
using RimWorld;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Verse;

namespace CAP_ChatInteractive.Traits
{
    public static class TraitsManager
    {
        public static Dictionary<string, BuyableTrait> AllBuyableTraits { get; private set; } = new Dictionary<string, BuyableTrait>();
        // Complete Settings data, includes inactive mods
        private static Dictionary<string, BuyableTrait> _completeTraitData = new Dictionary<string, BuyableTrait>();
        public static IReadOnlyDictionary<string, BuyableTrait> CompleteTraitData => _completeTraitData;
        private static bool isInitialized = false;
        private static readonly object lockObject = new object();

        // Add constant for Anomaly DLC identification
        private const string ANOMALY_DLC_NAME = "Anomaly";

        public static void InitializeTraits()
        {
            if (isInitialized) return;

            lock (lockObject)
            {
                if (isInitialized) return;

                Logger.Debug("Initializing Traits System...");

                bool loadedFromJson = LoadTraitsFromJson();

                if (!loadedFromJson)
                {
                    CreateDefaultTraits();
                    SaveTraitsToJson();
                }
                else
                {
                    ValidateAndUpdateTraits();
                }

                isInitialized = true;
                Logger.Message($"[CAP] Traits System initialized with {AllBuyableTraits.Count} traits");
            }
        }

        private static bool LoadTraitsFromJson()
        {
            string jsonContent = JsonFileManager.LoadFile("Traits.json");
            if (string.IsNullOrEmpty(jsonContent))
                return false;

            try
            {
                var loadedTraits = JsonFileManager.DeserializeTraits(jsonContent);

                if (loadedTraits == null)
                {
                    Logger.Error("Traits.json exists but contains no valid data");
                    return false;
                }

                // Store COMPLETE data (preserves all traits ever saved)
                _completeTraitData.Clear();
                foreach (var kvp in loadedTraits)
                {
                    _completeTraitData[kvp.Key] = kvp.Value;
                }

                // Now filter to ACTIVE traits for runtime use
                RebuildActiveTraitsFromCompleteData();

                Logger.Debug($"Loaded {_completeTraitData.Count} total traits from JSON, {AllBuyableTraits.Count} active");
                return true;
            }
            catch (Newtonsoft.Json.JsonException jsonEx)
            {
                Logger.Error($"JSON CORRUPTION in Traits.json: {jsonEx.Message}");
                HandleTraitsCorruption($"JSON parsing error: {jsonEx.Message}", jsonContent);
                return false;
            }
            catch (System.IO.IOException ioEx)
            {
                Logger.Error($"DISK ACCESS ERROR reading Traits.json: {ioEx.Message}");
                return false;
            }
            catch (System.Exception e)
            {
                Logger.Error($"Error loading traits JSON: {e.Message}");
                return false;
            }
        }

        private static void RebuildActiveTraitsFromCompleteData()
        {
            AllBuyableTraits.Clear();

            var allTraitDefs = DefDatabase<TraitDef>.AllDefs.ToList();

            // Build a set of active trait keys (defName_degree)
            var activeTraitKeys = new HashSet<string>();
            foreach (var traitDef in allTraitDefs)
            {
                if (traitDef.degreeDatas != null)
                {
                    foreach (var degree in traitDef.degreeDatas)
                    {
                        activeTraitKeys.Add(GetTraitKey(traitDef, degree.degree));
                    }
                }
                else
                {
                    activeTraitKeys.Add(GetTraitKey(traitDef, 0));
                }
            }

            // First, add all existing traits from complete data (JSON settings)
            foreach (var kvp in _completeTraitData)
            {
                if (activeTraitKeys.Contains(kvp.Key))
                {
                    // Trait is from an active mod - use JSON settings exactly as saved
                    AllBuyableTraits[kvp.Key] = kvp.Value;
                }
            }

            // Now check for NEW traits that aren't in JSON
            foreach (var traitDef in allTraitDefs)
            {
                bool isAnomalyTrait = IsAnomalyDlcTrait(traitDef);

                if (traitDef.degreeDatas != null)
                {
                    foreach (var degree in traitDef.degreeDatas)
                    {
                        string key = GetTraitKey(traitDef, degree.degree);
                        if (!_completeTraitData.ContainsKey(key))
                        {
                            // This is NEW trait - create it
                            var buyableTrait = new BuyableTrait(traitDef, degree);

                            // Disable new Anomaly DLC traits by default
                            if (isAnomalyTrait)
                            {
                                buyableTrait.CanAdd = false;
                                buyableTrait.CanRemove = false;
                            }

                            _completeTraitData[key] = buyableTrait;
                            AllBuyableTraits[key] = buyableTrait;

                            Logger.Debug($"Added NEW trait from constructor: {key} from {traitDef.modContentPack?.Name ?? "Unknown"}");
                        }
                    }
                }
                else
                {
                    string key = GetTraitKey(traitDef, 0);
                    if (!_completeTraitData.ContainsKey(key))
                    {
                        // This is NEW trait - create it
                        var buyableTrait = new BuyableTrait(traitDef);

                        // Disable new Anomaly DLC traits by default
                        if (isAnomalyTrait)
                        {
                            buyableTrait.CanAdd = false;
                            buyableTrait.CanRemove = false;
                        }

                        _completeTraitData[key] = buyableTrait;
                        AllBuyableTraits[key] = buyableTrait;

                        Logger.Debug($"Added NEW trait from constructor: {key} from {traitDef.modContentPack?.Name ?? "Unknown"}");
                    }
                }
            }
        }

        private static void HandleTraitsCorruption(string errorDetails, string corruptedJson)
        {
            // Backup corrupted file
            try
            {
                string backupPath = JsonFileManager.GetBackupPath("Traits.json");
                System.IO.File.WriteAllText(backupPath, corruptedJson);
                Logger.Debug($"Backed up corrupted Traits.json to: {backupPath}");
            }
            catch { /* Silent fail */ }

            // Show in-game notification
            if (Current.ProgramState == ProgramState.Playing)
            {
                Messages.Message(
                    "Chat Interactive: Traits data was corrupted. Rebuilt with defaults.",
                    MessageTypeDefOf.NegativeEvent
                );
            }
        }

        private static void CreateDefaultTraits()
        {
            AllBuyableTraits.Clear();
            _completeTraitData.Clear();

            var allTraitDefs = DefDatabase<TraitDef>.AllDefs.ToList();

            int traitsCreated = 0;
            int anomalyTraitsDisabled = 0;

            foreach (var traitDef in allTraitDefs)
            {
                try
                {
                    bool isAnomalyTrait = IsAnomalyDlcTrait(traitDef);

                    if (traitDef.degreeDatas != null)
                    {
                        foreach (var degree in traitDef.degreeDatas)
                        {
                            string key = GetTraitKey(traitDef, degree.degree);
                            if (!_completeTraitData.ContainsKey(key))
                            {
                                var buyableTrait = new BuyableTrait(traitDef, degree);

                                // Disable Anomaly DLC traits by default
                                if (isAnomalyTrait)
                                {
                                    buyableTrait.CanAdd = false;
                                    buyableTrait.CanRemove = false;
                                    anomalyTraitsDisabled++;
                                }

                                _completeTraitData[key] = buyableTrait;
                                AllBuyableTraits[key] = buyableTrait;
                                traitsCreated++;
                            }
                        }
                    }
                    else
                    {
                        string key = GetTraitKey(traitDef, 0);
                        if (!_completeTraitData.ContainsKey(key))
                        {
                            var buyableTrait = new BuyableTrait(traitDef);

                            // Disable Anomaly DLC traits by default
                            if (isAnomalyTrait)
                            {
                                buyableTrait.CanAdd = false;
                                buyableTrait.CanRemove = false;
                                anomalyTraitsDisabled++;
                            }

                            _completeTraitData[key] = buyableTrait;
                            AllBuyableTraits[key] = buyableTrait;
                            traitsCreated++;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error($"Error creating buyable trait for {traitDef.defName}: {ex.Message}");
                }
            }

            if (anomalyTraitsDisabled > 0)
            {
                Logger.Message($"[CAP] Anomaly DLC: {anomalyTraitsDisabled} traits disabled by default");
            }
        }

        private static void ValidateAndUpdateTraits()
        {
            var allTraitDefs = DefDatabase<TraitDef>.AllDefs.ToList();

            // Build a set of active trait keys (defName_degree)
            var activeTraitKeys = new HashSet<string>();
            foreach (var traitDef in allTraitDefs)
            {
                if (traitDef.degreeDatas != null)
                {
                    foreach (var degree in traitDef.degreeDatas)
                    {
                        activeTraitKeys.Add(GetTraitKey(traitDef, degree.degree));
                    }
                }
                else
                {
                    activeTraitKeys.Add(GetTraitKey(traitDef, 0));
                }
            }

            int addedTraits = 0;
            int updatedTraits = 0;
            int removedTraits = 0;
            int anomalyTraitsDisabled = 0;

            // Check for NEW traits not in JSON
            foreach (var traitDef in allTraitDefs)
            {
                bool isAnomalyTrait = IsAnomalyDlcTrait(traitDef);

                if (traitDef.degreeDatas != null)
                {
                    foreach (var degree in traitDef.degreeDatas)
                    {
                        string key = GetTraitKey(traitDef, degree.degree);

                        if (!_completeTraitData.ContainsKey(key))
                        {
                            // New trait - create it
                            var newTrait = new BuyableTrait(traitDef, degree);

                            // Disable new Anomaly DLC traits by default
                            if (isAnomalyTrait)
                            {
                                newTrait.CanAdd = false;
                                newTrait.CanRemove = false;
                                anomalyTraitsDisabled++;
                            }

                            _completeTraitData[key] = newTrait;
                            AllBuyableTraits[key] = newTrait;
                            addedTraits++;
                        }
                        else if (_completeTraitData.ContainsKey(key))
                        {
                            // Existing trait - preserve user settings but update if needed
                            var existingTrait = _completeTraitData[key];

                            // Store user settings before any updates
                            bool userCanAdd = existingTrait.CanAdd;
                            bool userCanRemove = existingTrait.CanRemove;
                            bool userCustomName = existingTrait.CustomName;
                            string userKarmaAdd = existingTrait.KarmaTypeForAdding;
                            string userKarmaRemove = existingTrait.KarmaTypeForRemoving;
                            bool userBypassLimit = existingTrait.BypassLimit;

                            // Check if core trait data has changed
                            if (TraitNeedsUpdate(existingTrait, traitDef, degree))
                            {
                                // Create updated version with new game data
                                var updatedTrait = new BuyableTrait(traitDef, degree);

                                // Restore user settings
                                updatedTrait.CanAdd = userCanAdd;
                                updatedTrait.CanRemove = userCanRemove;
                                updatedTrait.CustomName = userCustomName;
                                updatedTrait.KarmaTypeForAdding = userKarmaAdd;
                                updatedTrait.KarmaTypeForRemoving = userKarmaRemove;
                                updatedTrait.BypassLimit = userBypassLimit;

                                _completeTraitData[key] = updatedTrait;
                                AllBuyableTraits[key] = updatedTrait;
                                updatedTraits++;
                            }
                        }
                    }
                }
                else
                {
                    string key = GetTraitKey(traitDef, 0);

                    if (!_completeTraitData.ContainsKey(key))
                    {
                        // New trait - create it
                        var newTrait = new BuyableTrait(traitDef);

                        // Disable new Anomaly DLC traits by default
                        if (isAnomalyTrait)
                        {
                            newTrait.CanAdd = false;
                            newTrait.CanRemove = false;
                            anomalyTraitsDisabled++;
                        }

                        _completeTraitData[key] = newTrait;
                        AllBuyableTraits[key] = newTrait;
                        addedTraits++;
                    }
                    else if (_completeTraitData.ContainsKey(key))
                    {
                        // Existing trait - preserve user settings but update if needed
                        var existingTrait = _completeTraitData[key];

                        // Store user settings before any updates
                        bool userCanAdd = existingTrait.CanAdd;
                        bool userCanRemove = existingTrait.CanRemove;
                        bool userCustomName = existingTrait.CustomName;
                        string userKarmaAdd = existingTrait.KarmaTypeForAdding;
                        string userKarmaRemove = existingTrait.KarmaTypeForRemoving;
                        bool userBypassLimit = existingTrait.BypassLimit;

                        // Check if core trait data has changed
                        if (TraitNeedsUpdate(existingTrait, traitDef, null))
                        {
                            // Create updated version with new game data
                            var updatedTrait = new BuyableTrait(traitDef);

                            // Restore user settings
                            updatedTrait.CanAdd = userCanAdd;
                            updatedTrait.CanRemove = userCanRemove;
                            updatedTrait.CustomName = userCustomName;
                            updatedTrait.KarmaTypeForAdding = userKarmaAdd;
                            updatedTrait.KarmaTypeForRemoving = userKarmaRemove;
                            updatedTrait.BypassLimit = userBypassLimit;

                            _completeTraitData[key] = updatedTrait;
                            AllBuyableTraits[key] = updatedTrait;
                            updatedTraits++;
                        }
                    }
                }
            }

            // Remove traits from runtime that are no longer active
            var keysToRemove = AllBuyableTraits.Keys.Where(k => !activeTraitKeys.Contains(k)).ToList();
            foreach (var key in keysToRemove)
            {
                AllBuyableTraits.Remove(key);
                removedTraits++;
            }

            // Mark all active traits as modactive = true for online store
            // Note: You'll need to add a modactive property to BuyableTrait class first
            foreach (var trait in AllBuyableTraits.Values)
            {
                trait.modactive = true; // Add this property to BuyableTrait
            }

            if (anomalyTraitsDisabled > 0)
            {
                Logger.Message($"[CAP] Anomaly DLC: {anomalyTraitsDisabled} new traits disabled by default");
            }

            if (addedTraits > 0 || removedTraits > 0 || updatedTraits > 0)
            {
                Logger.Message($"Traits updated: +{addedTraits} traits, -{removedTraits} traits, ~{updatedTraits} traits modified");

            }
            SaveTraitsToJson(); // Save changes
        }

        private static bool TraitNeedsUpdate(BuyableTrait existingTrait, TraitDef traitDef, TraitDegreeData degreeData)
        {
            // Check if core trait data has changed
            string expectedName = degreeData?.label?.CapitalizeFirst() ?? traitDef.LabelCap;
            string expectedDescription = (degreeData?.description ?? traitDef.description)?.Replace("[PAWN_nameDef]", "[PAWN_name]");

            // Check name changes
            if (existingTrait.Name != expectedName && !existingTrait.CustomName)
                return true;

            // Check description changes
            if (existingTrait.Description != expectedDescription)
                return true;

            // Check if stat offsets have changed
            var expectedStats = new List<string>();
            if (degreeData?.statOffsets != null)
            {
                foreach (var statOffset in degreeData.statOffsets)
                {
                    string sign = statOffset.value > 0 ? "+" : "";
                    if (statOffset.stat.formatString == "F1" ||
                        statOffset.stat.ToString().Contains("Factor") ||
                        statOffset.stat.ToString().Contains("Percent"))
                    {
                        expectedStats.Add($"{sign}{statOffset.value * 100:f1}% {statOffset.stat.LabelCap}");
                    }
                    else
                    {
                        expectedStats.Add($"{sign}{statOffset.value} {statOffset.stat.LabelCap}");
                    }
                }
            }

            if (!existingTrait.Stats.SequenceEqual(expectedStats))
                return true;

            // Check if conflicts have changed
            var expectedConflicts = new List<string>();
            if (traitDef.conflictingTraits != null)
            {
                foreach (var conflict in traitDef.conflictingTraits)
                {
                    if (conflict != null && !string.IsNullOrEmpty(conflict.LabelCap))
                    {
                        expectedConflicts.Add(conflict.LabelCap);
                    }
                }
            }

            if (!existingTrait.Conflicts.SequenceEqual(expectedConflicts))
                return true;

            // Check if mod source has changed
            string expectedModSource = traitDef.modContentPack?.Name ?? "RimWorld";
            if (existingTrait.ModSource != expectedModSource)
                return true;

            return false;
        }

        private static bool IsAnomalyDlcTrait(TraitDef traitDef)
        {
            // Check if this trait is from the Anomaly DLC
            // Anomaly DLC traits typically come from the "Anomaly" mod content pack
            return traitDef.modContentPack?.Name?.Contains(ANOMALY_DLC_NAME, StringComparison.OrdinalIgnoreCase) == true;
        }

        private static string GetTraitKey(TraitDef traitDef, int degree)
        {
            return $"{traitDef.defName}_{degree}";
        }

        public static void SaveTraitsToJson()
        {
            LongEventHandler.QueueLongEvent(() =>
            {
                // Sync runtime changes to complete data before saving
                lock (lockObject)
                {
                    foreach (var kvp in AllBuyableTraits)
                    {
                        _completeTraitData[kvp.Key] = kvp.Value;
                    }

                    try
                    {
                        string jsonContent = JsonFileManager.SerializeTraits(_completeTraitData);
                        JsonFileManager.SaveFile("Traits.json", jsonContent);
                        Logger.Debug($"Traits JSON saved. Total items: {_completeTraitData?.Count ?? 0}, Active: {AllBuyableTraits?.Count ?? 0}");
                    }
                    catch (System.Exception e)
                    {
                        Logger.Error($"Error saving traits JSON: {e.Message}");
                    }
                }
            }, null, false, null, showExtraUIInfo: false, forceHideUI: true);
        }

        public static BuyableTrait GetBuyableTrait(string defName, int degree = 0)
        {
            string key = GetTraitKey(DefDatabase<TraitDef>.GetNamed(defName), degree);
            return AllBuyableTraits.TryGetValue(key, out BuyableTrait trait) ? trait : null;
        }

        public static IEnumerable<BuyableTrait> GetEnabledTraits()
        {
            return AllBuyableTraits.Values.Where(trait => trait.CanAdd || trait.CanRemove);
        }

        public static IEnumerable<BuyableTrait> GetTraitsByMod(string modName)
        {
            return GetEnabledTraits().Where(trait => trait.ModSource == modName);
        }

        public static IEnumerable<string> GetAllModSources()
        {
            return AllBuyableTraits.Values
                .Select(trait => trait.ModSource)
                .Distinct()
                .OrderBy(source => source);
        }

        public static (int total, int enabled, int disabled) GetTraitsStatistics()
        {
            int total = AllBuyableTraits.Count;
            int enabled = GetEnabledTraits().Count();
            int disabled = total - enabled;
            return (total, enabled, disabled);
        }

        // Removes the traits JSON file and rebuilds the traits from scratch
        [DebugAction("CAP", "Delete JSON & Rebuild Traits", allowedGameStates = AllowedGameStates.Playing)]
        public static void DebugRebuildTraits()
        {
            try
            {
                // Delete the traits JSON file
                string filePath = JsonFileManager.GetFilePath("Traits.json");
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                    Logger.Message("Deleted Traits.json file");
                }
                else
                {
                    Logger.Message("No Traits.json file found to delete");
                }

                // Reset initialization and rebuild
                isInitialized = false;
                AllBuyableTraits.Clear();
                InitializeTraits();

                Logger.Message("Traits system rebuilt from scratch with current pricing and display rules");
            }
            catch (Exception ex)
            {
                Logger.Error($"Error rebuilding traits: {ex.Message}");
            }
        }
    }
}