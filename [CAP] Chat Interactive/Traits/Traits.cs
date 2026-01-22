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

                // Validation
                if (loadedTraits == null || loadedTraits.Count == 0)
                {
                    Logger.Error("Traits.json exists but contains no valid data - corrupted");
                    HandleTraitsCorruption("File contains no valid data", jsonContent);
                    return false;
                }

                AllBuyableTraits.Clear();
                foreach (var kvp in loadedTraits)
                {
                    AllBuyableTraits[kvp.Key] = kvp.Value;
                }

                return true;
            }
            catch (Newtonsoft.Json.JsonException jsonEx)
            {
                Logger.Error($"JSON CORRUPTION in Traits.json: {jsonEx.Message}\n");
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
                            if (!AllBuyableTraits.ContainsKey(key))
                            {
                                var buyableTrait = new BuyableTrait(traitDef, degree);

                                // Disable Anomaly DLC traits by default
                                if (isAnomalyTrait)
                                {
                                    buyableTrait.CanAdd = false;
                                    buyableTrait.CanRemove = false;
                                    anomalyTraitsDisabled++;
                                }

                                AllBuyableTraits[key] = buyableTrait;
                                traitsCreated++;
                            }
                        }
                    }
                    else
                    {
                        string key = GetTraitKey(traitDef, 0);
                        if (!AllBuyableTraits.ContainsKey(key))
                        {
                            var buyableTrait = new BuyableTrait(traitDef);

                            // Disable Anomaly DLC traits by default
                            if (isAnomalyTrait)
                            {
                                buyableTrait.CanAdd = false;
                                buyableTrait.CanRemove = false;
                                anomalyTraitsDisabled++;
                            }

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
            var allTraitDefs = DefDatabase<TraitDef>.AllDefs;
            int addedTraits = 0;
            int removedTraits = 0;
            int updatedTraits = 0;
            int anomalyTraitsDisabled = 0;

            // Add any new traits that aren't in our system
            foreach (var traitDef in allTraitDefs)
            {
                bool isAnomalyTrait = IsAnomalyDlcTrait(traitDef);

                if (traitDef.degreeDatas != null)
                {
                    foreach (var degree in traitDef.degreeDatas)
                    {
                        string key = GetTraitKey(traitDef, degree.degree);
                        if (!AllBuyableTraits.ContainsKey(key))
                        {
                            var buyableTrait = new BuyableTrait(traitDef, degree);

                            // Disable new Anomaly DLC traits by default when adding them during validation
                            if (isAnomalyTrait)
                            {
                                buyableTrait.CanAdd = false;
                                buyableTrait.CanRemove = false;
                                anomalyTraitsDisabled++;
                            }

                            AllBuyableTraits[key] = buyableTrait;
                            addedTraits++;
                        }
                        else
                        {
                            // Check if existing trait needs to be updated
                            var existingTrait = AllBuyableTraits[key];
                            if (TraitNeedsUpdate(existingTrait, traitDef, degree))
                            {
                                var updatedTrait = new BuyableTrait(traitDef, degree);
                                // Preserve user settings (CanAdd, CanRemove, CustomName, etc.)
                                updatedTrait.CanAdd = existingTrait.CanAdd;
                                updatedTrait.CanRemove = existingTrait.CanRemove;
                                updatedTrait.CustomName = existingTrait.CustomName;
                                updatedTrait.KarmaTypeForAdding = existingTrait.KarmaTypeForAdding;
                                updatedTrait.KarmaTypeForRemoving = existingTrait.KarmaTypeForRemoving;
                                updatedTrait.BypassLimit = existingTrait.BypassLimit;

                                AllBuyableTraits[key] = updatedTrait;
                                updatedTraits++;
                            }
                        }
                    }
                }
                else
                {
                    string key = GetTraitKey(traitDef, 0);
                    if (!AllBuyableTraits.ContainsKey(key))
                    {
                        var buyableTrait = new BuyableTrait(traitDef);

                        // Disable new Anomaly DLC traits by default when adding them during validation
                        if (isAnomalyTrait)
                        {
                            buyableTrait.CanAdd = false;
                            buyableTrait.CanRemove = false;
                            anomalyTraitsDisabled++;
                        }

                        AllBuyableTraits[key] = buyableTrait;
                        addedTraits++;
                    }
                    else
                    {
                        // Check if existing trait needs to be updated
                        var existingTrait = AllBuyableTraits[key];
                        if (TraitNeedsUpdate(existingTrait, traitDef, null))
                        {
                            var updatedTrait = new BuyableTrait(traitDef);
                            // Preserve user settings
                            updatedTrait.CanAdd = existingTrait.CanAdd;
                            updatedTrait.CanRemove = existingTrait.CanRemove;
                            updatedTrait.CustomName = existingTrait.CustomName;
                            updatedTrait.KarmaTypeForAdding = existingTrait.KarmaTypeForAdding;
                            updatedTrait.KarmaTypeForRemoving = existingTrait.KarmaTypeForRemoving;
                            updatedTrait.BypassLimit = existingTrait.BypassLimit;

                            AllBuyableTraits[key] = updatedTrait;
                            updatedTraits++;
                        }
                    }
                }
            }

            // Remove traits that no longer exist in the game
            var keysToRemove = new List<string>();
            foreach (var kvp in AllBuyableTraits)
            {
                var traitDef = DefDatabase<TraitDef>.GetNamedSilentFail(kvp.Value.DefName);
                if (traitDef == null)
                {
                    keysToRemove.Add(kvp.Key);
                }
                else
                {
                    // Check if degree still exists
                    if (traitDef.degreeDatas != null)
                    {
                        bool degreeExists = traitDef.degreeDatas.Any(d => d.degree == kvp.Value.Degree);
                        if (!degreeExists)
                        {
                            keysToRemove.Add(kvp.Key);
                        }
                    }
                }
            }

            foreach (var key in keysToRemove)
            {
                AllBuyableTraits.Remove(key);
                removedTraits++;
            }

            if (anomalyTraitsDisabled > 0)
            {
                Logger.Message($"[CAP] Anomaly DLC: {anomalyTraitsDisabled} new traits disabled by default");
            }

            if (addedTraits > 0 || removedTraits > 0 || updatedTraits > 0)
            {
                Logger.Message($"Traits updated: +{addedTraits} traits, -{removedTraits} traits, ~{updatedTraits} traits modified");
                SaveTraitsToJson(); // Save changes
            }
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
                lock (lockObject)
                {
                    try
                    {
                        string jsonContent = JsonFileManager.SerializeTraits(AllBuyableTraits);
                        JsonFileManager.SaveFile("Traits.json", jsonContent);
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