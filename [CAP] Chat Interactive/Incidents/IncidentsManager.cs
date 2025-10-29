// IncidentsManager.cs - CLEANED
using LudeonTK;
using Newtonsoft.Json;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
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

                Logger.Debug("Initializing Incidents System...");

                if (!LoadIncidentsFromJson())
                {
                    Logger.Debug("No incidents JSON found, creating default incidents...");
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
                AllBuyableIncidents.Clear();

                foreach (var kvp in loadedIncidents)
                {
                    AllBuyableIncidents[kvp.Key] = kvp.Value;
                }

                Logger.Debug($"Loaded {AllBuyableIncidents.Count} incidents from JSON");
                return true;
            }
            catch (System.Exception e)
            {
                Logger.Error($"Error loading incidents JSON: {e.Message}");
                return false;
            }
        }

        private static void CreateDefaultIncidents()
        {
            AllBuyableIncidents.Clear();

            // Debug logging
            LogIncidentCategories();

            var allIncidentDefs = DefDatabase<IncidentDef>.AllDefs.ToList();
            Logger.Debug($"Processing {allIncidentDefs.Count} incident definitions");

            int incidentsCreated = 0;
            foreach (var incidentDef in allIncidentDefs)
            {
                try
                {
                    // Skip incidents that aren't suitable for player triggering
                    if (!IsIncidentSuitableForStore(incidentDef))
                        continue;

                    string key = GetIncidentKey(incidentDef);
                    if (!AllBuyableIncidents.ContainsKey(key))
                    {
                        var buyableIncident = new BuyableIncident(incidentDef);
                        AllBuyableIncidents[key] = buyableIncident;
                        incidentsCreated++;

                        // Log progress every 20 incidents
                        if (incidentsCreated % 20 == 0)
                        {
                            Logger.Debug($"Created {incidentsCreated} buyable incidents so far...");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error($"Error creating buyable incident for {incidentDef.defName}: {ex.Message}");
                }
            }

            Logger.Debug($"Created default incidents with {AllBuyableIncidents.Count} entries");
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
            // Skip incidents without workers
            if (incidentDef.Worker == null)
                return false;

            // Skip hidden incidents
            if (incidentDef.hidden)
                return false;

            // Skip certain incident types that don't make sense for store
            string defName = incidentDef.defName.ToLower();
            if (defName.Contains("test") || defName.Contains("debug"))
                return false;

            // Skip incidents not suitable for player map
            if (!IsIncidentSuitableForPlayerMap(incidentDef))
                return false;

            // Skip incidents that require specific conditions we can't guarantee
            if (incidentDef.earliestDay > 0 || incidentDef.minPopulation > 0)
            {
                Logger.Debug($"Incident {defName} has restrictions: earliestDay={incidentDef.earliestDay}, minPop={incidentDef.minPopulation}");
            }

            return true;
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

            // Add any new incidents that aren't in our system
            foreach (var incidentDef in allIncidentDefs)
            {
                if (!IsIncidentSuitableForStore(incidentDef))
                    continue;

                string key = GetIncidentKey(incidentDef);
                if (!AllBuyableIncidents.ContainsKey(key))
                {
                    var buyableIncident = new BuyableIncident(incidentDef);
                    AllBuyableIncidents[key] = buyableIncident;
                    addedIncidents++;
                }
            }

            // Remove incidents that no longer exist in the game
            var keysToRemove = new List<string>();
            foreach (var kvp in AllBuyableIncidents)
            {
                var incidentDef = DefDatabase<IncidentDef>.GetNamedSilentFail(kvp.Key);
                if (incidentDef == null || !IsIncidentSuitableForStore(incidentDef))
                {
                    keysToRemove.Add(kvp.Key);
                }
            }

            foreach (var key in keysToRemove)
            {
                AllBuyableIncidents.Remove(key);
                removedIncidents++;
            }

            if (addedIncidents > 0 || removedIncidents > 0)
            {
                Logger.Debug($"Incidents updated: +{addedIncidents} incidents, -{removedIncidents} incidents");
                SaveIncidentsToJson();
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

        private static bool IsIncidentSuitableForPlayerMap(IncidentDef incidentDef)
        {
            string defName = incidentDef.defName.ToLower();
            string workerName = incidentDef.Worker?.GetType().Name.ToLower() ?? "";

            // Skip caravan-specific incidents
            if (defName.Contains("caravan") || defName.Contains("ambush") ||
                workerName.Contains("caravan") || workerName.Contains("ambush"))
            {
                Logger.Debug($"Skipping caravan/ambush incident: {defName}");
                return false;
            }

            // Skip world map only incidents
            if (incidentDef.targetTags != null)
            {
                foreach (var tag in incidentDef.targetTags)
                {
                    if (tag.defName.Contains("World") || tag.defName.Contains("Caravan"))
                    {
                        Logger.Debug($"Skipping world/caravan target incident: {defName}");
                        return false;
                    }
                }
            }

            // Skip quest-giving incidents (handled separately)
            if (defName.Contains("quest") && !defName.Contains("refugee") && !defName.Contains("wanderer"))
            {
                Logger.Debug($"Skipping quest incident: {defName}");
                return false;
            }

            return true;
        }

        [DebugAction("CAP", "Reload Incidents", allowedGameStates = AllowedGameStates.Playing)]
        public static void DebugReloadIncidents()
        {
            isInitialized = false;
            InitializeIncidents();
        }
    }
}