// BuyableIncident.cs
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace CAP_ChatInteractive.Incidents
{
    public class BuyableIncident
    {
        // Core incident properties
        public string DefName { get; set; }
        public string Label { get; set; }
        public string Description { get; set; }
        public string WorkerClassName { get; set; }
        public string CategoryName { get; set; }

        // Purchase settings
        public int BaseCost { get; set; } = 500;
        public string KarmaType { get; set; } = "Neutral";
        public int EventCap { get; set; } = 2;
        public bool Enabled { get; set; } = true;
        public string DisabledReason { get; set; } = "";
        public bool ShouldBeInStore { get; set; } = true;
        public bool IsAvailableForCommands { get; set; } = true;

        // Additional data
        public string ModSource { get; set; } = "RimWorld";
        public int Version { get; set; } = 1;

        // Incident-specific properties
        public bool IsWeatherIncident { get; set; }
        public bool IsRaidIncident { get; set; }
        public bool IsDiseaseIncident { get; set; }
        public bool IsQuestIncident { get; set; }
        public float BaseChance { get; set; }
        public string MinTechLevel { get; set; }
        public bool PointsScaleable { get; set; }
        public float MinThreatPoints { get; set; }
        public float MaxThreatPoints { get; set; }


        public BuyableIncident() { }

        public BuyableIncident(IncidentDef incidentDef)
        {
            DefName = incidentDef.defName;
            Label = incidentDef.label;
            Description = incidentDef.description;
            WorkerClassName = incidentDef.Worker?.GetType()?.Name;
            CategoryName = incidentDef.category?.defName;
            ModSource = incidentDef.modContentPack?.Name ?? "RimWorld";
            BaseChance = incidentDef.baseChance;
            PointsScaleable = incidentDef.pointsScaleable;
            MinThreatPoints = incidentDef.minThreatPoints;
            MaxThreatPoints = incidentDef.maxThreatPoints;

            AnalyzeIncidentType(incidentDef);
            SetDefaultPricing(incidentDef);

            // Determine if this incident should be in store at all
            ShouldBeInStore = DetermineStoreSuitability(incidentDef);

            // Determine command availability
            IsAvailableForCommands = DetermineCommandAvailability(incidentDef);

            // Auto-disable if not suitable for store
            if (!ShouldBeInStore)
            {
                Enabled = false;
                DisabledReason = "Not suitable for store system";
            }
            else
            {
                // NEW: Auto-disable mod events (non-Core, non-DLC) for safety
                if (ShouldAutoDisableModEvent(incidentDef))
                {
                    Enabled = false;
                    DisabledReason = "Auto-disabled: Mod event (enable manually if desired)";
                }
            }

            Logger.Debug($"Created incident: {DefName}, Store: {ShouldBeInStore}, Commands: {IsAvailableForCommands}, Enabled: {Enabled}");
        }

        private bool DetermineStoreSuitability(IncidentDef incidentDef)
        {
            // Skip incidents without workers
            if (incidentDef.Worker == null)
                return false;

            // Skip hidden incidents
            if (incidentDef.hidden)
                return false;

            // Skip test/debug incidents
            string defName = incidentDef.defName.ToLower();
            if (defName.Contains("test") || defName.Contains("debug"))
                return false;

            // Skip incidents not suitable for player map
            if (!IsIncidentSuitableForPlayerMap(incidentDef))
                return false;

            // Skip specific incident types by defName
            if (ShouldSkipByDefName(incidentDef))
                return false;

            // Skip incidents with inappropriate target tags
            if (ShouldSkipByTargetTags(incidentDef))
                return false;

            // Skip endgame/story-specific incidents
            if (ShouldSkipBySpecialCriteria(incidentDef))
                return false;

            return true;
        }

        private bool ShouldSkipByDefName(IncidentDef incidentDef)
        {
            string[] skipDefNames = {
        "RaidEnemy", "RaidFriendly", "DeepDrillInfestation", "Infestation",
        "GiveQuest_EndGame_ShipEscape", "GiveQuest_EndGame_ArchonexusVictory",
        "ManhunterPack", "ShamblerAssault", "ShamblerSwarmAnimals", "SmallShamblerSwarm",
        "SightstealerArrival", "CreepJoinerJoin_Metalhorror", "CreepJoinerJoin",
        "DevourerWaterAssault", "HarbingerTreeProvoked", "GameEndedWanderersJoin"
    };

            return skipDefNames.Contains(incidentDef.defName);
        }

        private bool ShouldSkipByTargetTags(IncidentDef incidentDef)
        {
            if (incidentDef.targetTags == null)
                return false;

            string[] skipTargetTags = {
        "Caravan", "Map_TempIncident", "World", "Site"
    };

            string[] raidTags = {
        "Raid"
    };

            // Skip caravan/world incidents
            if (incidentDef.targetTags.Any(tag => skipTargetTags.Contains(tag.defName)))
                return true;

            // Skip raid incidents (handled by !raid command)
            if (incidentDef.targetTags.Any(tag => raidTags.Contains(tag.defName)))
                return true;

            return false;
        }

        private bool ShouldSkipBySpecialCriteria(IncidentDef incidentDef)
        {
            // Skip endgame quests
            if (incidentDef.defName.Contains("EndGame"))
                return true;

            // Skip incidents that require specific conditions not available via commands
            if (incidentDef.defName.Contains("Ambush"))
                return true;

            // Skip ransom demands (too specific)
            if (incidentDef.defName.Contains("Ransom"))
                return true;

            // Skip game-over specific incidents
            if (incidentDef.defName.Contains("GameEnded"))
                return true;

            // Skip provoked incidents that are duplicates
            if (incidentDef.defName.Contains("Provoked"))
                return true;

            return false;
        }

        private bool DetermineCommandAvailability(IncidentDef incidentDef)
        {
            string defName = incidentDef.defName.ToLower();

            // ONLY filter combat incidents that have dedicated commands
            string[] combatIncidentsToFilter = {
        "manhunterpack", "infestation", "mechcluster",
        "fleshmassheart", "shamblerswarm", "ghoulattack",
        "fleshbeastattack", "gorehulkassault", "devourerassault",
        "chimeraassault", "psychicritualsiege", "hatechanters"
    };

            return !combatIncidentsToFilter.Contains(defName);
        }

        private bool IsIncidentSuitableForPlayerMap(IncidentDef incidentDef)
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

            return true;
        }

        public void UpdateCommandAvailability()
        {
            // This will be called after the incident is created to set initial availability
            IsAvailableForCommands = CheckCommandSuitability();
        }

        private void AnalyzeIncidentType(IncidentDef incidentDef)
        {
            if (incidentDef.Worker == null) return;

            string workerName = incidentDef.Worker.GetType().Name.ToLower();
            string defName = incidentDef.defName.ToLower();
            string categoryName = incidentDef.category?.defName?.ToLower() ?? "";

            IsWeatherIncident = workerName.Contains("weather") || defName.Contains("weather") || categoryName.Contains("weather");
            IsRaidIncident = workerName.Contains("raid") || defName.Contains("raid") || categoryName.Contains("raid") ||
                            (incidentDef.targetTags?.Any(t => t.defName.Contains("Raid")) == true);
            IsDiseaseIncident = workerName.Contains("disease") || defName.Contains("sickness") || categoryName.Contains("disease") ||
                               incidentDef.diseaseIncident != null;
            IsQuestIncident = workerName.Contains("quest") || defName.Contains("quest") || categoryName.Contains("quest") ||
                             incidentDef.questScriptDef != null;

            // Additional specific checks
            if (defName.Contains("manhunter") || defName.Contains("infestation") || defName.Contains("mechcluster") ||
                defName.Contains("assault") || defName.Contains("swarm"))
                IsRaidIncident = true;

            if (defName.Contains("quest"))
                IsQuestIncident = true;

            // Check for threat categories
            if (categoryName.Contains("threatbig") || categoryName.Contains("threatsmall"))
                IsRaidIncident = true; // Treat threat categories as raid-like incidents
        }

        private bool CheckCommandSuitability()
        {
            string defName = DefName.ToLower();

            string[] combatIncidentsToFilter = {
            "manhunterpack", "infestation", "mechcluster",
            "fleshmassheart", "shamblerswarm", "ghoulattack",
            "fleshbeastattack", "gorehulkassault", "devourerassault",
            "chimeraassault", "psychicritualsiege", "hatechanters"
        };

            if (combatIncidentsToFilter.Contains(defName))
                return false;

            return true;
        }

        private string DetermineKarmaType(IncidentDef incidentDef)
        {
            // First check for specific defNames that we know are good or bad
            string[] badDefNames = {
        "NoxiousHaze", "CropBlight", "LavaEmergence", "LavaFlow", "ShortCircuit"
    };

            string[] goodDefNames = {
        "PsychicSoothe"
    };

            if (badDefNames.Contains(incidentDef.defName))
                return "Bad";

            if (goodDefNames.Contains(incidentDef.defName))
                return "Good";

            // Then check letter def for karma hints
            if (incidentDef.letterDef != null)
            {
                string letterDefName = incidentDef.letterDef.defName.ToLower();

                if (letterDefName.Contains("positive") || letterDefName.Contains("good"))
                    return "Good";
                else if (letterDefName.Contains("negative") || letterDefName.Contains("bad") ||
                         letterDefName.Contains("threat"))
                    return "Bad";
            }

            // Check category for threat indicators
            if (incidentDef.category != null)
            {
                string categoryName = incidentDef.category.defName.ToLower();
                if (categoryName.Contains("threatbig") || categoryName.Contains("threatsmall") ||
                    categoryName.Contains("threat"))
                {
                    return "Bad";
                }

                if (categoryName.Contains("positive") || categoryName.Contains("good") ||
                    categoryName.Contains("benefit"))
                {
                    return "Good";
                }

                if (categoryName.Contains("neutral") || categoryName.Contains("normal"))
                {
                    return "Neutral";
                }
            }

            // Fallback to type-based determination
            if (IsRaidIncident || IsDiseaseIncident)
                return "Bad";
            else if (IsQuestIncident)
                return "Good";
            else
                return "Neutral";
        }

        public string GetUnavailableReason()
        {
            var incidentDef = DefDatabase<IncidentDef>.GetNamedSilentFail(DefName);
            if (incidentDef == null)
                return "IncidentDef not found";

            if (incidentDef.targetTags?.Any(t => t.defName == "Caravan") == true)
                return "Caravan incident - not available on player map";

            if (incidentDef.targetTags?.Any(t => t.defName == "Raid") == true)
                return "Raid incident - use !raid command instead";

            if (incidentDef.defName.Contains("EndGame"))
                return "Endgame story incident - not suitable for commands";

            if (incidentDef.defName.Contains("Ambush"))
                return "Ambush incident - caravan specific";

            return "Not suitable for command system";
        }

        public bool IsKarmaTypeSimilar(string karmaType1, string karmaType2)
        {
            // Consider karma types similar if they're in the same category
            string[] goodTypes = { "Good", "Positive", "Friendly" };
            string[] badTypes = { "Bad", "Negative", "Hostile" };
            string[] neutralTypes = { "Neutral", "Normal", "Standard" };

            bool type1IsGood = goodTypes.Contains(karmaType1);
            bool type1IsBad = badTypes.Contains(karmaType1);
            bool type1IsNeutral = neutralTypes.Contains(karmaType1) || (!type1IsGood && !type1IsBad);

            bool type2IsGood = goodTypes.Contains(karmaType2);
            bool type2IsBad = badTypes.Contains(karmaType2);
            bool type2IsNeutral = neutralTypes.Contains(karmaType2) || (!type2IsGood && !type2IsBad);

            return (type1IsGood && type2IsGood) || (type1IsBad && type2IsBad) || (type1IsNeutral && type2IsNeutral);
        }

        private bool ShouldAutoDisableModEvent(IncidentDef incidentDef)
        {
            // Always enable Core RimWorld incidents
            if (ModSource == "RimWorld" || ModSource == "Core")
                return false;

            // Enable official DLCs
            string[] officialDLCs = {
        "Royalty", "Ideology", "Biotech", "Anomaly", "Odyssey"
    };

            if (officialDLCs.Any(dlc => ModSource.Contains(dlc)))
                return false;

            // Auto-disable all other mod events for safety
            Logger.Debug($"Auto-disabling mod event: {DefName} from {ModSource}");
            return true;
        }

        private void SetDefaultPricing(IncidentDef incidentDef)
        {
            int basePrice = 150; // Slightly higher than minimal since other income sources exist
            float impactFactor = 1.0f;

            // Determine karma type first
            KarmaType = DetermineKarmaType(incidentDef);

            // Tier-based pricing for different viewer engagement levels
            if (IsRaidIncident)
            {
                impactFactor *= 2.5f; // Premium tier - channel points/daily reward targets
                EventCap = 1;
            }
            else if (IsWeatherIncident)
            {
                impactFactor *= 1.3f; // Mid tier - regular watching + occasional bonuses
            }
            else if (IsDiseaseIncident)
            {
                impactFactor *= 1.7f; // High tier - requires some saving/daily rewards
            }
            else if (IsQuestIncident)
            {
                impactFactor *= 1.2f; // Low-mid tier - accessible with moderate engagement
            }

            // Threat points scaling (reduced impact since other income exists)
            if (PointsScaleable && MaxThreatPoints > 0)
            {
                impactFactor *= (MaxThreatPoints / 2500f);
            }

            // Rarity adjustment
            if (BaseChance > 0)
            {
                impactFactor *= (1.0f / BaseChance) * 0.08f;
            }

            BaseCost = (int)(basePrice * impactFactor);
            BaseCost = Math.Max(75, Math.Min(7500, BaseCost)); // Wider range for economy flexibility
        }
    }
}