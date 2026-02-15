// IncidentsManager.cs
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
//
// Manages the loading, saving, and updating of buyable incidents for the chat interactive mod.

/*
============================================================
RICS INCIDENTS DATA FLOW - CRITICAL ARCHITECTURE
============================================================

RUNTIME OPERATIONS:
• All commands use AllBuyableIncidents Dictionary (in-memory cache)
• JSON is ONLY for persistence, never read during normal operations
• Changes update Dictionary → SaveIncidentsToJson() asynchronously

PERFORMANCE NOTES:
• LongEventHandler prevents UI stutter during saves
• No auto-saves - only save on actual data changes
• No locks needed except for save operation itself

DO NOT CHANGE:
• Do NOT add timed auto-saves (disk I/O waste)
• Do NOT read JSON during runtime (use Dictionary)
• Do NOT replace async saving (causes stutter)
• Do NOT add redundant saves (PostClose handles it)
============================================================
*/
using LudeonTK;
using Newtonsoft.Json;
using RimWorld;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Verse;

namespace CAP_ChatInteractive.Incidents
{
    public static class IncidentsManager
    {
        // Point of truth
        public static Dictionary<string, BuyableIncident> AllBuyableIncidents { get; private set; } = new Dictionary<string, BuyableIncident>();
        // Complete Settings data, includes inactive mods
        private static Dictionary<string, BuyableIncident> _completeIncidentData = new Dictionary<string, BuyableIncident>();
        public static IReadOnlyDictionary<string, BuyableIncident> CompleteIncidentData => _completeIncidentData;
        private static bool isInitialized = false;
        private static readonly object lockObject = new object();

        public static void InitializeIncidents()
        {
            if (isInitialized) return;

            lock (lockObject)
            {
                if (isInitialized) return;

                Logger.Debug("Initializing Incidents/Events System...");

                bool loadedFromJson = LoadIncidentsFromJson();

                if (!loadedFromJson)
                {
                    CreateDefaultIncidents();
                    SaveIncidentsToJson();
                }
                else
                {
                    ValidateAndUpdateIncidents();
                }

                isInitialized = true;
                Logger.Message($"[CAP] Incidents System initialized with {AllBuyableIncidents.Count} incidents");
            }
        }
        // Loads incidents from the JSON file, with validation and error handling
        private static bool LoadIncidentsFromJson()
        {
            string jsonContent = JsonFileManager.LoadFile("Incidents.json");
            if (string.IsNullOrEmpty(jsonContent))
                return false;

            try
            {
                var loadedIncidents = JsonFileManager.DeserializeIncidents(jsonContent);

                if (loadedIncidents == null)
                {
                    Logger.Error("Incidents.json exists but contains no valid data");
                    return false;
                }

                // Store COMPLETE data (preserves all incidents ever saved)
                _completeIncidentData.Clear();
                foreach (var kvp in loadedIncidents)
                {
                    _completeIncidentData[kvp.Key] = kvp.Value;
                }

                // Now filter to ACTIVE incidents for runtime use
                RebuildActiveIncidentsFromCompleteData();

                Logger.Debug($"Loaded {_completeIncidentData.Count} total incidents from JSON, {AllBuyableIncidents.Count} active");
                return true;
            }
            catch (Newtonsoft.Json.JsonException jsonEx)
            {
                Logger.Error($"JSON CORRUPTION in Incidents.json: {jsonEx.Message}\n" +
                             $"File may be partially written, damaged, or from incompatible version.");
                HandleIncidentsCorruption($"JSON parsing error: {jsonEx.Message}", jsonContent);
                return false;
            }
            catch (System.IO.IOException ioEx)
            {
                // Disk-level failure - serious hardware issue
                Logger.Error($"DISK ACCESS ERROR reading Incidents.json: {ioEx.Message}\n" +
                             $"Streamer should check hard drive health immediately!");

                // Show urgent in-game warning
                if (Current.ProgramState == ProgramState.Playing && Find.LetterStack != null)
                {
                    Find.LetterStack.ReceiveLetter(
                        "Chat Interactive: Critical Storage Error",
                        "Chat Interactive cannot read incidents data due to a disk access error.\n\n" +
                        "This may indicate hardware failure. Check your hard drive health!",
                        LetterDefOf.NegativeEvent
                    );
                }
                return false;
            }
            catch (System.Exception e)
            {
                Logger.Error($"Unexpected error loading incidents JSON: {e.Message}");
                return false;
            }
        }
        // === Handles Curruption ===
        private static void HandleIncidentsCorruption(string errorDetails, string corruptedJson)
        {
            // Backup corrupted file for debugging
            if (!string.IsNullOrWhiteSpace(corruptedJson))
            {
                try
                {
                    string backupPath = JsonFileManager.GetBackupPath("Incidents.json");
                    System.IO.File.WriteAllText(backupPath, corruptedJson);
                    Logger.Debug($"Backed up corrupted Incidents.json to: {backupPath}");
                }
                catch (System.Exception ex)
                {
                    Logger.Error($"Failed to backup corrupted Incidents.json: {ex.Message}");
                }
            }

            // Show in-game notification
            if (Current.ProgramState == ProgramState.Playing)
            {
                string message = "Chat Interactive: Incidents configuration was corrupted.\n" +
                                "Rebuilt with default incidents. Custom settings have been lost.\n" +
                                "Check logs for details.";

                Messages.Message(message, MessageTypeDefOf.NegativeEvent);
            }

            // Log the corrupted content (first 500 chars for debugging)
            if (corruptedJson != null && corruptedJson.Length > 0)
            {
                string preview = corruptedJson.Length > 500 ?
                    corruptedJson.Substring(0, 500) + "..." :
                    corruptedJson;
                Logger.Debug($"Corrupted Incidents JSON preview: {preview}");
            }
        }

        // === JSON for Store ===
        private static void CreateDefaultIncidents()
        {
            AllBuyableIncidents.Clear();
            LogIncidentCategories();

            var allIncidentDefs = DefDatabase<IncidentDef>.AllDefs.ToList();
            Logger.Debug($"Processing {allIncidentDefs.Count} incident definitions");

            int incidentsCreated = 0;
            foreach (var incidentDef in allIncidentDefs)
            {
                try
                {
                    // Just create the incident - let it self-filter
                    var buyableIncident = new BuyableIncident(incidentDef);

                    // Only add if it should be in store
                    if (buyableIncident.ShouldBeInStore)
                    {
                        string key = GetIncidentKey(incidentDef);
                        if (!AllBuyableIncidents.ContainsKey(key))
                        {
                            AllBuyableIncidents[key] = buyableIncident;
                            incidentsCreated++;
                        }
                    }
                    else
                    {
                        Logger.Debug($"Skipping store-unsuitable incident: {incidentDef.defName}");
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error($"Error creating buyable incident for {incidentDef.defName}: {ex.Message}");
                }
            }

            Logger.Debug($"Created {AllBuyableIncidents.Count} store-suitable incidents");
        }

        private static void RebuildActiveIncidentsFromCompleteData()
        {
            AllBuyableIncidents.Clear();

            var allIncidentDefs = DefDatabase<IncidentDef>.AllDefs.ToList();
            var activeDefNames = new HashSet<string>(allIncidentDefs.Select(d => d.defName));

            // First, add all existing incidents from complete data (JSON settings)
            foreach (var kvp in _completeIncidentData)
            {
                if (activeDefNames.Contains(kvp.Key))
                {
                    // Incident is from an active mod - use JSON settings exactly as saved
                    AllBuyableIncidents[kvp.Key] = kvp.Value;

                    // Still validate game-state properties (like BaseChance, PointsScaleable)
                    // but NEVER change Enabled/DisabledReason from JSON
                    var incidentDef = allIncidentDefs.FirstOrDefault(d => d.defName == kvp.Key);
                    if (incidentDef != null)
                    {
                        // Update only game-derived properties, not user settings
                        kvp.Value.BaseChance = incidentDef.baseChance;
                        kvp.Value.PointsScaleable = incidentDef.pointsScaleable;
                        kvp.Value.MinThreatPoints = incidentDef.minThreatPoints;
                        kvp.Value.MaxThreatPoints = incidentDef.maxThreatPoints;

                        // Update availability flags based on current game state
                        kvp.Value.ShouldBeInStore = BuyableIncident.DetermineStoreSuitability(incidentDef);
                        kvp.Value.IsAvailableForCommands = BuyableIncident.DetermineCommandAvailability(incidentDef);

                        // But NEVER change Enabled/DisabledReason - those come from JSON
                    }
                }
            }

            // Now check for NEW incidents that aren't in JSON
            foreach (var incidentDef in allIncidentDefs)
            {
                string key = GetIncidentKey(incidentDef);
                if (!_completeIncidentData.ContainsKey(key) && IsIncidentSuitableForStore(incidentDef))
                {
                    // This is a NEW incident - let the constructor handle auto-disabling
                    var buyableIncident = new BuyableIncident(incidentDef);

                    _completeIncidentData[key] = buyableIncident;

                    // Add to runtime if suitable
                    if (buyableIncident.ShouldBeInStore)
                    {
                        AllBuyableIncidents[key] = buyableIncident;
                    }

                    Logger.Debug($"Added NEW incident from constructor: {key} from {incidentDef.modContentPack?.Name ?? "Unknown"} - Enabled: {buyableIncident.Enabled}");
                }
            }
        }

        private static void ValidateSingleIncident(BuyableIncident incident, IncidentDef incidentDef)
        {
            if (incidentDef == null) return;

            // Check if store suitability changed
            bool shouldBeInStore = BuyableIncident.DetermineStoreSuitability(incidentDef);
            if (incident.ShouldBeInStore != shouldBeInStore)
            {
                incident.ShouldBeInStore = shouldBeInStore;
                if (!shouldBeInStore)
                {
                    incident.Enabled = false;
                    incident.DisabledReason = "No longer suitable for store system";
                }
            }

            // Update command availability
            incident.IsAvailableForCommands = BuyableIncident.DetermineCommandAvailability(incidentDef);

            // Update other properties that might have changed due to game updates
            incident.BaseChance = incidentDef.baseChance;
            incident.PointsScaleable = incidentDef.pointsScaleable;
            incident.MinThreatPoints = incidentDef.minThreatPoints;
            incident.MaxThreatPoints = incidentDef.maxThreatPoints;
        }

        private static bool IsIncidentSuitableForStore(IncidentDef incidentDef)
        {
            // Delegate all logic to BuyableIncident constructor
            // Just do basic null checks here
            if (incidentDef == null) return false;
            if (incidentDef.Worker == null) return false;

            return true; // Let BuyableIncident handle the real filtering
        }

        private static string GetIncidentKey(IncidentDef incidentDef)
        {
            return incidentDef.defName;
        }

        private static void ValidateAndUpdateIncidents()
        {
            var allIncidentDefs = DefDatabase<IncidentDef>.AllDefs.ToList();
            var activeDefNames = new HashSet<string>(allIncidentDefs.Select(d => d.defName));

            int addedIncidents = 0;
            int updatedGameProperties = 0;
            int removedIncidents = 0;

            // Check for NEW incidents not in JSON
            foreach (var incidentDef in allIncidentDefs)  // 'incidentDef' is the loop variable
            {
                string key = GetIncidentKey(incidentDef);

                if (!_completeIncidentData.ContainsKey(key) && IsIncidentSuitableForStore(incidentDef))
                {
                    // New incident - let constructor handle it (including auto-disable)
                    var newIncident = new BuyableIncident(incidentDef);
                    _completeIncidentData[key] = newIncident;
                    AllBuyableIncidents[key] = newIncident;
                    addedIncidents++;
                }
                else if (_completeIncidentData.ContainsKey(key))
                {
                    // Existing incident - update game properties but preserve user settings
                    var existingIncident = _completeIncidentData[key];

                    // FIX: Rename this variable to avoid conflict with loop variable
                    var currentIncidentDef = allIncidentDefs.FirstOrDefault(d => d.defName == key);

                    if (currentIncidentDef != null)
                    {
                        // Store user settings before any updates
                        bool userEnabled = existingIncident.Enabled;
                        string userDisabledReason = existingIncident.DisabledReason;
                        int userPrice = existingIncident.BaseCost;
                        string userKarma = existingIncident.KarmaType;

                        // Update game-derived properties
                        existingIncident.BaseChance = currentIncidentDef.baseChance;
                        existingIncident.PointsScaleable = currentIncidentDef.pointsScaleable;
                        existingIncident.MinThreatPoints = currentIncidentDef.minThreatPoints;
                        existingIncident.MaxThreatPoints = currentIncidentDef.maxThreatPoints;

                        // Update flags based on current game state
                        existingIncident.ShouldBeInStore = BuyableIncident.DetermineStoreSuitability(currentIncidentDef);
                        existingIncident.IsAvailableForCommands = BuyableIncident.DetermineCommandAvailability(currentIncidentDef);

                        // CRITICAL: Restore ALL user settings from JSON
                        existingIncident.Enabled = userEnabled;
                        existingIncident.DisabledReason = userDisabledReason;
                        existingIncident.BaseCost = userPrice;
                        existingIncident.KarmaType = userKarma;

                        // Don't change EventCap either if user might have customized it
                        // But if you want to allow EventCap updates, add logic here

                        updatedGameProperties++;
                    }
                }
            }

            // Remove incidents from runtime that are no longer active
            var keysToRemove = AllBuyableIncidents.Keys.Where(k => !activeDefNames.Contains(k)).ToList();
            foreach (var key in keysToRemove)
            {
                AllBuyableIncidents.Remove(key);
                removedIncidents++;
            }

            // Mark all active incidents as modactive = true for online store
            foreach (var incident in AllBuyableIncidents.Values)
            {
                incident.modactive = true;
            }

            // Log changes
            if (addedIncidents > 0 || removedIncidents > 0 || updatedGameProperties > 0)
            {
                Logger.Message($"Incidents updated: +{addedIncidents} new, -{removedIncidents} removed, {updatedGameProperties} game properties refreshed");
            }
            SaveIncidentsToJson();
        }

        public static void SaveIncidentsToJson()
        {
            LongEventHandler.QueueLongEvent(() =>
            {
                lock (lockObject)
                {
                    // Sync runtime changes to complete data before saving
                    foreach (var kvp in AllBuyableIncidents)
                    {
                        _completeIncidentData[kvp.Key] = kvp.Value;
                    }

                    try
                    {
                        string jsonContent = JsonFileManager.SerializeIncidents(_completeIncidentData);
                        JsonFileManager.SaveFile("Incidents.json", jsonContent);
                        Logger.Debug($"Incidents JSON saved. Total items: {_completeIncidentData?.Count ?? 0}, Active: {AllBuyableIncidents?.Count ?? 0}");
                    }
                    catch (JsonException jsonEx)
                    {
                        HandleJsonException(jsonEx, "Incidents.json");
                    }
                    catch (IOException ioEx)
                    {
                        HandleIOException(ioEx, "Incidents.json", isCritical: false);
                    }
                    catch (Exception e)
                    {
                        Logger.Error($"Error saving incidents JSON: {e}");
                    }
                }
            },
            textKey: null,
            doAsynchronously: false,
            exceptionHandler: null,
            showExtraUIInfo: false,
            forceHideUI: true);
        }

        public static void SaveIncidentsToJsonPostClose()
        {
            LongEventHandler.QueueLongEvent(() =>
            {
                lock (lockObject)
                {
                    // Sync runtime changes to complete data before saving
                    foreach (var kvp in AllBuyableIncidents)
                    {
                        _completeIncidentData[kvp.Key] = kvp.Value;
                    }

                    try
                    {
                        string jsonContent = JsonFileManager.SerializeIncidents(_completeIncidentData);
                        JsonFileManager.SaveFile("Incidents.json", jsonContent);
                        Logger.Message("Incidents JSON saved successfully");
                    }
                    catch (JsonException jsonEx)
                    {
                        HandleJsonException(jsonEx, "Incidents.json");

                        // Additional UI feedback for post-close
                        Messages.Message("RICS: JSON error saving Incidents. Check logs.",
                            MessageTypeDefOf.RejectInput);
                    }
                    catch (IOException ioEx)
                    {
                        HandleIOException(ioEx, "Incidents.json", isCritical: true);

                        // Always show disk errors
                        Messages.Message("RICS: Disk error! Incidents not saved.",
                            MessageTypeDefOf.RejectInput);
                    }
                    catch (Exception e)
                    {
                        Logger.Error($"Error saving incidents JSON: {e}");
                        Messages.Message($"RICS: Save error: {e.GetType().Name}",
                            MessageTypeDefOf.RejectInput);
                    }
                }
            },
            textKey: null,
            doAsynchronously: false,
            exceptionHandler: HandleSaveException,
            showExtraUIInfo: true,
            forceHideUI: false);
        }

        private static void HandleSaveException(Exception ex)
        {
            Logger.Error($"LongEventHandler caught exception during save: {ex}");

            // Only show message if not already handled in try/catch
            if (ex is not JsonException && ex is not IOException)
            {
                Logger.Error($"Error saving incidents JSON: {ex}");
                Messages.Message($"RICS: Save failed! {ex.Message}",
                    MessageTypeDefOf.RejectInput);
            }
        }

        private static void HandleJsonException(JsonException jsonEx, string filename)
        {
            Logger.Error($"JSON CORRUPTION in {filename}: {jsonEx.Message}\n" +
                        $"File may be corrupted or from incompatible version.\n" +
                        $"Attempting to create backup and rebuild...");

            // Create emergency backup
            try
            {
                string backupPath = Path.Combine(JsonFileManager.GetFilePath(filename),
                    $"{Path.GetFileNameWithoutExtension(filename)}_CORRUPTED_{DateTime.Now:yyyyMMdd_HHmmss}.json.bak");
                string originalPath = Path.Combine(JsonFileManager.GetFilePath(filename));

                if (File.Exists(originalPath))
                {
                    File.Copy(originalPath, backupPath, true);
                    Logger.Message($"Corrupted file backed up to: {backupPath}");
                }
            }
            catch (Exception backupEx)
            {
                Logger.Error($"Failed to create backup: {backupEx.Message}");
            }

            // Could add logic here to rebuild from defaults if needed
            if (AllBuyableIncidents == null || AllBuyableIncidents.Count == 0)
            {
                Logger.Warning($"No incidents data available after JSON error. May need to reset to defaults.");
            }
        }

        private static void HandleIOException(IOException ioEx, string filename, bool isCritical)
        {
            string errorType = isCritical ? "CRITICAL DISK ACCESS ERROR" : "Disk access error";
            Logger.Error($"{errorType} reading {filename}: {ioEx.Message}\n" +
                        $"Error Code: {ioEx.HResult}\n" +
                        $"Check disk health and free space!");

            if (isCritical && Current.ProgramState == ProgramState.Playing)
            {
                // Show urgent letter for critical disk errors
                ShowCriticalDiskError(ioEx, filename);
            }
        }

        private static void ShowCriticalDiskError(IOException ioEx, string filename)
        {
            try
            {
                if (Find.LetterStack != null)
                {
                    Find.LetterStack.ReceiveLetter(
                        "RICS: Critical Storage Error",
                        $"RICS cannot save incidents  data due to a disk error.\n\n" +
                        $"File: {filename}\n" +
                        $"Error: {ioEx.Message}\n\n" +
                        $"This may indicate:\n" +
                        $"• Hard drive failure\n" +
                        $"• Disk full\n" +
                        $"• File permission issues\n" +
                        $"• Antivirus blocking access\n\n" +
                        $"Check your storage device health immediately!",
                        LetterDefOf.ThreatBig,
                        lookTargets: null,
                        debugInfo: null,
                        relatedFaction: null,
                        hyperlinkThingDefs: null
                    );
                }
            }
            catch (Exception letterEx)
            {
                Logger.Error($"Failed to show critical error letter: {letterEx.Message}");
            }
        }

        private static void LogIncidentCategories()
        {
            var categories = DefDatabase<IncidentCategoryDef>.AllDefs;
            Logger.Debug($"Found {categories.Count()} incident categories:");
            foreach (var category in categories)
            {
                Logger.Debug($"  - {category.defName}: {category.LabelCap}");
            }

            var allIncidents = DefDatabase<IncidentDef>.AllDefs.ToList();
            Logger.Debug($"Total incidents found: {allIncidents.Count}");

            var suitableIncidents = allIncidents.Where(IsIncidentSuitableForStore).ToList();
            Logger.Debug($"Suitable incidents for store: {suitableIncidents.Count}");

            // Log first 10 incidents as sample
            foreach (var incident in suitableIncidents.Take(10))
            {
                Logger.Debug($"Sample: {incident.defName} - {incident.label} - Worker: {incident.Worker?.GetType().Name}");
            }
        }

        private static bool ShouldAutoDisableModEvent(IncidentDef incidentDef)
        {
            string modSource = incidentDef.modContentPack?.Name ?? "RimWorld";

            // Always enable Core RimWorld incidents
            if (modSource == "RimWorld" || modSource == "Core")
                return false;

            // Enable official DLCs
            string[] officialDLCs = {
        "Royalty", "Ideology", "Biotech", "Anomaly"
    };

            if (officialDLCs.Any(dlc => modSource.Contains(dlc)))
                return false;

            // Auto-disable all other mod events for safety
            return true;
        }

        private static bool IsPriceCloseToDefault(BuyableIncident incident, int defaultPrice)
        {
            // Consider price "close to default" if within 30% of default
            float ratio = (float)incident.BaseCost / defaultPrice;
            return ratio >= 0.7f && ratio <= 1.3f;
        }


        // Removes the incidents JSON file and rebuilds the incidents from scratch
        [DebugAction("CAP", "Delete JSON & Rebuild Incidents", allowedGameStates = AllowedGameStates.Playing)]
        public static void DebugRebuildIncidents()
        {
            try
            {
                // Delete the incidents JSON file
                string filePath = JsonFileManager.GetFilePath("Incidents.json");
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                    Logger.Message("Deleted Incidents.json file");
                }
                else
                {
                    Logger.Message("No Incidents.json file found to delete");
                }

                // Reset initialization and rebuild
                isInitialized = false;
                AllBuyableIncidents.Clear();
                InitializeIncidents();

                Logger.Message("Incidents system rebuilt from scratch with current filtering rules");
            }
            catch (Exception ex)
            {
                Logger.Error($"Error rebuilding incidents: {ex.Message}");
            }
        }

        [DebugAction("CAP", "Analyze Incident Filtering", allowedGameStates = AllowedGameStates.Playing)]
        public static void DebugAnalyzeFiltering()
        {
            StringBuilder report = new StringBuilder();
            report.AppendLine("=== INCIDENT FILTERING ANALYSIS ===");

            var allIncidentDefs = DefDatabase<IncidentDef>.AllDefs.ToList();
            report.AppendLine($"Total IncidentDefs in game: {allIncidentDefs.Count}");

            // Track filtering reasons
            var filteredOut = new Dictionary<string, List<string>>();
            var included = new List<string>();

            foreach (var incidentDef in allIncidentDefs)
            {
                var reasons = new List<string>();

                // Check each filtering criterion
                if (incidentDef.Worker == null)
                    reasons.Add("No worker");

                if (incidentDef.hidden)
                    reasons.Add("Hidden incident");

                if (incidentDef.defName.ToLower().Contains("test") || incidentDef.defName.ToLower().Contains("debug"))
                    reasons.Add("Test/debug incident");

                // Check target tags
                if (incidentDef.targetTags != null)
                {
                    if (incidentDef.targetTags.Any(t => t.defName == "Caravan" || t.defName == "World" || t.defName == "Site"))
                        reasons.Add("Caravan/World/Site target");

                    if (incidentDef.targetTags.Any(t => t.defName == "Raid"))
                        reasons.Add("Raid incident");

                    // Check map targeting
                    bool hasPlayerHome = incidentDef.targetTags.Any(t => t.defName == "Map_PlayerHome");
                    bool hasMapTag = incidentDef.targetTags.Any(t => t.defName == "Map_TempIncident" || t.defName == "Map_Misc" || t.defName == "Map_RaidBeacon");
                    if (hasMapTag && !hasPlayerHome)
                        reasons.Add("Temporary map target only");
                }

                // Check specific defNames to skip
                string[] skipDefNames = {
            "RaidEnemy", "RaidFriendly", "DeepDrillInfestation", "Infestation",
            "GiveQuest_EndGame_ShipEscape", "GiveQuest_EndGame_ArchonexusVictory",
            "ManhunterPack", "ShamblerAssault", "ShamblerSwarmAnimals", "SmallShamblerSwarm",
            "SightstealerArrival", "CreepJoinerJoin_Metalhorror", "CreepJoinerJoin",
            "DevourerWaterAssault", "HarbingerTreeProvoked", "GameEndedWanderersJoin"
        };

                if (skipDefNames.Contains(incidentDef.defName))
                    reasons.Add("Specific defName exclusion");

                // Check endgame/story
                if (incidentDef.defName.Contains("EndGame") || incidentDef.defName.Contains("Ambush") ||
                    incidentDef.defName.Contains("Ransom") || incidentDef.defName.Contains("GameEnded"))
                    reasons.Add("Endgame/story incident");

                // Mod safety filtering
                string modSource = incidentDef.modContentPack?.Name ?? "RimWorld";
                if (modSource != "RimWorld" && modSource != "Core")
                {
                    string[] officialDLCs = { "Royalty", "Ideology", "Biotech", "Anomaly" };
                    if (!officialDLCs.Any(dlc => modSource.Contains(dlc)))
                        reasons.Add("Mod event (auto-disabled for safety)");
                }

                if (reasons.Count > 0)
                {
                    filteredOut[incidentDef.defName] = reasons;
                }
                else
                {
                    included.Add(incidentDef.defName);
                }
            }

            report.AppendLine($"\nINCLUDED INCIDENTS ({included.Count}):");
            foreach (var incident in included.OrderBy(x => x))
            {
                report.AppendLine($"  - {incident}");
            }

            report.AppendLine($"\nFILTERED OUT INCIDENTS ({filteredOut.Count}):");
            foreach (var kvp in filteredOut.OrderBy(x => x.Key))
            {
                report.AppendLine($"  - {kvp.Key}: {string.Join(", ", kvp.Value)}");
            }

            // Also show what's actually in our system
            report.AppendLine($"\nACTUALLY IN OUR SYSTEM ({AllBuyableIncidents.Count}):");
            foreach (var kvp in AllBuyableIncidents.OrderBy(x => x.Key))
            {
                var incident = kvp.Value;
                string status = incident.Enabled ? "ENABLED" : "DISABLED";
                string availability = incident.IsAvailableForCommands ? "COMMANDS" : "NO_COMMANDS";
                string reason = incident.DisabledReason;

                report.AppendLine($"  - {incident.DefName} [{status}] [{availability}] {reason}");
            }

            Logger.Message(report.ToString());

            // Also log to file for easier analysis
            string folderPath = Path.Combine(GenFilePaths.ConfigFolderPath, "CAP_Debug");
            string filePath = Path.Combine(folderPath, "IncidentFilteringAnalysis.txt");

            // Ensure the directory exists
            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
            }

            File.WriteAllText(filePath, report.ToString());
            Logger.Message($"Full analysis saved to: {filePath}");
        }
    }
}