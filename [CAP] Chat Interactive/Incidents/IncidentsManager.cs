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
        public static Dictionary<string, BuyableIncident> AllBuyableIncidents { get; private set; } = new Dictionary<string, BuyableIncident>();
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

        private static bool LoadIncidentsFromJson()
        {
            string jsonContent = JsonFileManager.LoadFile("Incidents.json");
            if (string.IsNullOrEmpty(jsonContent))
                return false;

            try
            {
                var loadedIncidents = JsonFileManager.DeserializeIncidents(jsonContent);

                // Validation: Check if data is valid
                if (loadedIncidents == null || loadedIncidents.Count == 0)
                {
                    Logger.Error("Incidents.json exists but contains no valid data - corrupted or empty");
                    HandleIncidentsCorruption("File contains no valid data", jsonContent);
                    return false;
                }

                // Success! Load into memory
                AllBuyableIncidents.Clear();
                foreach (var kvp in loadedIncidents)
                {
                    AllBuyableIncidents[kvp.Key] = kvp.Value;
                }

                Logger.Debug($"Loaded {AllBuyableIncidents.Count} incidents from JSON");
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

        private static void LogImplementationSummary()
        {
            var incidentsByMod = IncidentsManager.AllBuyableIncidents.Values
                .GroupBy(i => i.ModSource)
                .OrderByDescending(g => g.Count());

            Logger.Message("=== INCIDENT IMPLEMENTATION ROADMAP ===");
            foreach (var modGroup in incidentsByMod)
            {
                Logger.Message($"{modGroup.Key}: {modGroup.Count()} incidents");
            }

            Logger.Message("=== START WITH THESE CORE INCIDENTS ===");
            var easyCore = IncidentsManager.AllBuyableIncidents.Values
                .Where(i => i.ModSource == "Core")
                .Where(i => !i.DefName.Contains("Anomaly") && !i.PointsScaleable && !i.IsQuestIncident)
                .Take(5);

            foreach (var incident in easyCore)
            {
                Logger.Message($"  - {incident.DefName}: {incident.Label} (Cost: {incident.BaseCost})");
            }
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
            var allIncidentDefs = DefDatabase<IncidentDef>.AllDefs;
            int addedIncidents = 0;
            int removedIncidents = 0;
            int updatedCommandAvailability = 0;
            int updatedPricing = 0;
            int updatedKarmaTypes = 0;
            int updatedStoreSuitability = 0;
            int autoDisabledModEvents = 0;

            // Track all valid incident keys that should be in our system
            var validIncidentKeys = new HashSet<string>();

            // Process all incident definitions
            foreach (var incidentDef in allIncidentDefs)
            {
                string key = GetIncidentKey(incidentDef);
                validIncidentKeys.Add(key);

                // Check if this incident should be in store at all
                bool shouldBeInStore = IsIncidentSuitableForStore(incidentDef);

                if (!shouldBeInStore)
                {
                    // If it shouldn't be in store, remove it if it exists
                    if (AllBuyableIncidents.ContainsKey(key))
                    {
                        AllBuyableIncidents.Remove(key);
                        removedIncidents++;
                    }
                    continue; // Skip to next incident
                }

                // If we get here, the incident should be in store
                if (!AllBuyableIncidents.ContainsKey(key))
                {
                    // Add new incident
                    var buyableIncident = new BuyableIncident(incidentDef);
                    AllBuyableIncidents[key] = buyableIncident;
                    addedIncidents++;

                    // Count newly auto-disabled mod events
                    if (!buyableIncident.Enabled && buyableIncident.DisabledReason?.Contains("Auto-disabled") == true)
                    {
                        autoDisabledModEvents++;
                    }
                }
                else
                {
                    // Validate and update existing incidents
                    var existingIncident = AllBuyableIncidents[key];
                    var tempIncident = new BuyableIncident(incidentDef); // Create temp to get current values

                    // Store the original enabled state to check if we should preserve it
                    bool wasOriginallyEnabled = existingIncident.Enabled;

                    // Check if store suitability needs updating
                    bool currentStoreSuitability = existingIncident.ShouldBeInStore;
                    if (existingIncident.ShouldBeInStore != tempIncident.ShouldBeInStore)
                    {
                        existingIncident.ShouldBeInStore = tempIncident.ShouldBeInStore;
                        updatedStoreSuitability++;

                        // Auto-disable if no longer suitable for store
                        if (!existingIncident.ShouldBeInStore)
                        {
                            existingIncident.Enabled = false;
                            existingIncident.DisabledReason = "No longer suitable for store system";
                        }
                    }

                    // Check if command availability needs updating
                    bool currentAvailability = existingIncident.IsAvailableForCommands;
                    if (existingIncident.IsAvailableForCommands != tempIncident.IsAvailableForCommands)
                    {
                        existingIncident.IsAvailableForCommands = tempIncident.IsAvailableForCommands;
                        updatedCommandAvailability++;
                    }

                    // Check if pricing needs updating (if default pricing changed significantly)
                    int priceDifference = Math.Abs(existingIncident.BaseCost - tempIncident.BaseCost);
                    if (priceDifference > existingIncident.BaseCost * 0.2f) // More than 20% difference
                    {
                        // Only update if user hasn't customized the price (check against original)
                        if (IsPriceCloseToDefault(existingIncident, tempIncident.BaseCost))
                        {
                            existingIncident.BaseCost = tempIncident.BaseCost;
                            updatedPricing++;
                        }
                    }

                    // Check if karma type needs updating (if logic changed)
                    if (existingIncident.KarmaType != tempIncident.KarmaType)
                    {
                        // Use the new similarity check from BuyableIncident
                        if (existingIncident.IsKarmaTypeSimilar(existingIncident.KarmaType, tempIncident.KarmaType))
                        {
                            existingIncident.KarmaType = tempIncident.KarmaType;
                            updatedKarmaTypes++;
                        }
                    }

                    // NEW: For existing incidents, don't auto-disable if user already enabled them
                    // This preserves user choice while still applying auto-disable to new incidents
                    if (ShouldAutoDisableModEvent(incidentDef) && wasOriginallyEnabled)
                    {
                        // User already enabled this mod event, so don't auto-disable it
                        existingIncident.Enabled = true;
                        existingIncident.DisabledReason = "";
                    }
                }
            }

            // Remove incidents that no longer exist in the game OR are no longer valid
            var keysToRemove = new List<string>();
            foreach (var kvp in AllBuyableIncidents)
            {
                var incidentDef = DefDatabase<IncidentDef>.GetNamedSilentFail(kvp.Key);
                if (incidentDef == null || !validIncidentKeys.Contains(kvp.Key))
                {
                    keysToRemove.Add(kvp.Key);
                }
            }

            foreach (var key in keysToRemove)
            {
                AllBuyableIncidents.Remove(key);
                removedIncidents++;
            }

            // Log all changes
            if (addedIncidents > 0 || removedIncidents > 0 || updatedCommandAvailability > 0 ||
                updatedPricing > 0 || updatedKarmaTypes > 0 || updatedStoreSuitability > 0 || autoDisabledModEvents > 0)
            {
                StringBuilder changes = new StringBuilder("Incidents updated:");
                if (addedIncidents > 0) changes.Append($" +{addedIncidents} incidents");
                if (removedIncidents > 0) changes.Append($" -{removedIncidents} incidents");
                if (autoDisabledModEvents > 0) changes.Append($" {autoDisabledModEvents} mod events auto-disabled");
                if (updatedStoreSuitability > 0) changes.Append($" {updatedStoreSuitability} store suitability flags updated");
                if (updatedCommandAvailability > 0) changes.Append($" {updatedCommandAvailability} availability flags updated");
                if (updatedPricing > 0) changes.Append($" {updatedPricing} prices updated");
                if (updatedKarmaTypes > 0) changes.Append($" {updatedKarmaTypes} karma types updated");

                Logger.Message(changes.ToString());

                // Add a helpful message about auto-disabled mod events
                if (autoDisabledModEvents > 0)
                {
                    Logger.Message($"Safety feature: {autoDisabledModEvents} mod events were auto-disabled. Enable them manually in the Events Editor if desired.");
                }

                SaveIncidentsToJson(); // Save changes
            }
        }

        public static void SaveIncidentsToJson()
        {
            LongEventHandler.QueueLongEvent(() =>
            {
                lock (lockObject)
                {
                    try
                    {
                        string jsonContent = JsonFileManager.SerializeIncidents(AllBuyableIncidents);
                        JsonFileManager.SaveFile("Incidents.json", jsonContent);
                        Logger.Debug("Incidents JSON saved successfully");
                    }
                    catch (System.Exception e)
                    {
                        Logger.Error($"Error saving incidents JSON: {e.Message}");
                    }
                }
            }, null, false, null, showExtraUIInfo: false, forceHideUI: true);
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