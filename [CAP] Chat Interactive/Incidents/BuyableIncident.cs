// BuyableIncident.cs
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
// Represents a buyable incident with properties for pricing, availability, and type analysis.
using RimWorld;
using System;
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
        public bool Enabled { get; set; } = false;
        public string DisabledReason { get; set; } = "";
        public bool ShouldBeInStore { get; set; } = true;
        public bool IsAvailableForCommands { get; set; } = true;
        public int CooldownDays { get; set; } = 0;

        // Additional data
        public string ModSource { get; set; } = "RimWorld";
        public bool modactive { get; set; } = false;
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
            Label = Label = CleanIncidentName(incidentDef.label);
            Description = incidentDef.description;
            WorkerClassName = incidentDef.Worker?.GetType()?.Name;
            CategoryName = incidentDef.category?.defName;
            ModSource = incidentDef.modContentPack?.Name ?? "RimWorld";
            BaseChance = incidentDef.baseChance;
            PointsScaleable = incidentDef.pointsScaleable;
            MinThreatPoints = incidentDef.minThreatPoints;
            MaxThreatPoints = incidentDef.maxThreatPoints;

            AnalyzeIncidentType(incidentDef);
            KarmaType = DetermineKarmaType(incidentDef);
            SetDefaultPricing(incidentDef);

            // Determine if this incident should be in store at all, removes the item from the store
            ShouldBeInStore = DetermineStoreSuitability(incidentDef);

            // Determine command availability, this will make the incident UNAVAILABLE for commands
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

            // Logger.Debug($"Created incident: {DefName}, Store: {ShouldBeInStore}, Commands: {IsAvailableForCommands}, Enabled: {Enabled}");
        }

        // Determine if the incident is suitable for inclusion in the store
        public static bool DetermineStoreSuitability(IncidentDef incidentDef)
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
            //if (!IsIncidentSuitableForPlayerMap(incidentDef))
              //  return false;

            // Skip specific incident types by defName
            if (ShouldSkipByDefName(incidentDef))
                return false;

            // Skip incidents with inappropriate target tags
            // if (ShouldSkipByTargetTags(incidentDef))
               // return false;

            // Skip endgame/story-specific incidents
            if (ShouldSkipBySpecialCriteria(incidentDef))
                return false;

            return true;
        }

        public static bool ShouldSkipByDefName(IncidentDef incidentDef)
        {
            string[] skipDefNames = {
                "RaidEnemy", "RaidFriendly", "MechCluster", "DeepDrillInfestation",
                "GiveQuest_EndGame_ShipEscape", "GiveQuest_EndGame_ArchonexusVictory",
            };

            return skipDefNames.Contains(incidentDef.defName, StringComparer.OrdinalIgnoreCase);
        }

        public static bool ShouldSkipBySpecialCriteria(IncidentDef incidentDef)
        {
            // Skip endgame quests
            if (incidentDef.defName.Contains("EndGame"))
                return true;

            // Skip incidents that require specific conditions not available via commands
            //if (incidentDef.defName.Contains("Ambush"))
            //    return true;

            // Skip ransom demands (too specific)
            if (incidentDef.defName.Contains("Ransom"))
                return true;

            // Skip game-over specific incidents
            //if (incidentDef.defName.Contains("GameEnded"))
              //  return true;

            // Skip provoked incidents that are duplicates
            //if (incidentDef.defName.Contains("Provoked"))
              //  return true;

            return false;
        }
        /// <summary>
        /// Checks to see if we want this in the event store.
        /// </summary>
        /// <param name="incidentDef"></param>
        /// <returns></returns>
        public static bool DetermineCommandAvailability(IncidentDef incidentDef)
        {
            string defName = incidentDef.defName;

            string[] combatIncidentsToFilter = {
                "raidenemy", "raidFriendly", "MechCluster", "DeepDrillInfestation",
                "GiveQuest_EndGame_ShipEscape", "GiveQuest_EndGame_ArchonexusVictory",
            };

            return !combatIncidentsToFilter.Contains(defName, StringComparer.OrdinalIgnoreCase);
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
        "RaidEnemy", "RaidFriendly", "DeepDrillInfestation", "Infestation",
        "GiveQuest_EndGame_ShipEscape", "GiveQuest_EndGame_ArchonexusVictory",
        "SightstealerArrival", "CreepJoinerJoin_Metalhorror", "CreepJoinerJoin",
         "HarbingerTreeProvoked",
        };

            if (combatIncidentsToFilter.Contains(defName))
                return false;

            return true;
        }

        private string DetermineKarmaType(IncidentDef incidentDef)
        {
            if (incidentDef == null) return "Neutral";

            string defNameLower = incidentDef.defName.ToLowerInvariant();

            // ────────────────────────────────────────────────
            // NEW: Doom-tier — colony-threatening apocalypse events
            // These should feel "oh no, chat is about to end us" expensive
            string[] doomTriggers = {
        "toxicfallout", "volcanicwinter", "defoliatorshippart", "psychicemanatorshippart",
        "animalinsanitymass", "infestation", "manhunterpack", // mass manhunter can be doom if big
        "psychicdrone" // especially when high level, but we catch it via letter/category below too
    };

            if (doomTriggers.Any(trigger => defNameLower.Contains(trigger)))
            {
                return "Doom";
            }

            // Also catch via letter/category for cases where defName doesn't match exactly
            if (incidentDef.letterDef != null)
            {
                string letterName = incidentDef.letterDef.defName.ToLowerInvariant();
                if (letterName.Contains("negative") || letterName.Contains("threat"))
                {
                    if (incidentDef.category?.defName?.ToLowerInvariant().Contains("threatbig") == true &&
                        (defNameLower.Contains("fallout") || defNameLower.Contains("winter") ||
                         defNameLower.Contains("insanity") || defNameLower.Contains("defoliator") ||
                         defNameLower.Contains("emi") || BaseChance <= 0.4f)) // rarer = more doom-like
                    {
                        return "Doom";
                    }
                }
            }

            // ────────────────────────────────────────────────
            // Original logic below (slightly cleaned up)
            string[] badDefNames = { "NoxiousHaze", "CropBlight", "LavaEmergence", "LavaFlow", "ShortCircuit" };
            string[] goodDefNames = { "PsychicSoothe" };

            if (badDefNames.Contains(incidentDef.defName)) return "Bad";
            if (goodDefNames.Contains(incidentDef.defName)) return "Good";

            if (incidentDef.letterDef != null)
            {
                string letter = incidentDef.letterDef.defName.ToLowerInvariant();
                if (letter.Contains("positive") || letter.Contains("good")) return "Good";
                if (letter.Contains("negative") || letter.Contains("bad") || letter.Contains("threat"))
                    return "Bad";  // regular bad, not doom
            }

            if (incidentDef.category != null)
            {
                string cat = incidentDef.category.defName.ToLowerInvariant();
                if (cat.Contains("threatbig") || cat.Contains("threatsmall") || cat.Contains("threat"))
                    return "Bad";
                if (cat.Contains("positive") || cat.Contains("good") || cat.Contains("benefit"))
                    return "Good";
                if (cat.Contains("neutral") || cat.Contains("normal"))
                    return "Neutral";
            }

            // Type-based fallback
            if (IsRaidIncident || IsDiseaseIncident) return "Bad";
            if (IsQuestIncident) return "Good";

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

            // NEW: More specific reason for map targeting
            if (incidentDef.targetTags != null)
            {
                bool hasPlayerHome = incidentDef.targetTags.Any(t => t.defName == "Map_PlayerHome");
                bool hasMapTag = incidentDef.targetTags.Any(t =>
                    t.defName == "Map_TempIncident" ||
                    t.defName == "Map_Misc" ||
                    t.defName == "Map_RaidBeacon");

                if (hasMapTag && !hasPlayerHome)
                    return "Incident targets temporary maps but not player home";
            }

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
            "Royalty", "Ideology", "Biotech", "Odyssey"  // Removed "Anomaly" to be disabled by default
            };

            if (officialDLCs.Any(dlc => ModSource.Contains(dlc)))
                return false;
            
            return true;
        }

        private void SetDefaultPricing(IncidentDef incidentDef)
        {
            if (incidentDef == null)
            {
                BaseCost = 400;
                return;
            }

            float basePrice = 380f;          // lowered starting point for accessibility
            float impactFactor = 1.0f;

            // 1. Karma weighting – VERY negative events now heavily up-priced
            string karma = KarmaType?.ToLowerInvariant() ?? "neutral";
            switch (karma)
            {
                case "Doom":
                    impactFactor *= 3.2f;     // Doom still premium, but ~3× neutral
                    EventCap = 1;
                    break;
                case "Bad":
                    impactFactor *= 1.9f;     // Bad is noticeable but not extreme
                    break;
                case "Good":
                    impactFactor *= 0.85f;    // Encourage buying positives often
                    break;
                default: // Neutral
                    impactFactor *= 1.0f;
                    break;
            }

            // 2. Type multipliers (RimWorld classification you already compute)
            if (IsRaidIncident)
            {
                impactFactor *= 1.4f;
                EventCap = 1;
            }
            else if (IsDiseaseIncident)
            {
                impactFactor *= 1.5f;
                EventCap = 1;
            }
            else if (IsWeatherIncident ||
                     incidentDef.defName.ToLowerInvariant().Contains("wave") ||
                     incidentDef.defName.ToLowerInvariant().Contains("snap") ||
                     incidentDef.defName.ToLowerInvariant().Contains("storm") ||
                     incidentDef.defName.ToLowerInvariant().Contains("fallout") ||
                     incidentDef.defName.ToLowerInvariant().Contains("eclipse") ||
                     incidentDef.defName.ToLowerInvariant().Contains("flare"))
            {
            if (karma == "doom")
                impactFactor *= 1.25f;
            if (karma == "bad")
                impactFactor *= 1.15f;
            else
                impactFactor *= 1.0f;
            }
            else if (IsQuestIncident)
            {
                impactFactor *= 0.9f;
            }

            // Threat-point scaling – very gentle (many big threats won't explode in price)
            if (PointsScaleable && MaxThreatPoints > 0)
            {
                float ts = Math.Clamp(MaxThreatPoints / 2500f, 1f, 2.8f);
                impactFactor *= ts;
            }

            // Rarity / on-demand adjustment – minimal
            if (BaseChance > 0f)
            {
                impactFactor *= Math.Clamp(1.8f / BaseChance, 0.9f, 1.8f);
            }
            else
            {
                impactFactor *= 1.25f;   // slight premium for buyable events
            }

            // Final very-negative boost – small
            string defLower = incidentDef.defName.ToLowerInvariant();
            string[] veryNegative = {
        "toxicfallout", "volcanicwinter", "defoliatorshippart", "psychicemanatorshippart",
        "animalinsanitymass", "wastepackinfestation"
    };
            if (veryNegative.Any(d => defLower.Contains(d)))
            {
                impactFactor *= 1.25f;
            }

            // Hard clamp – this is the key for your targets
            BaseCost = (int)Math.Round(basePrice * impactFactor);
            BaseCost = Math.Max(180, Math.Min(1800, BaseCost));  // ← 1800 cap = ~3h normal, 180 min = ~18 min
        }

        private string CleanIncidentName(string originalName)
        {
            if (string.IsNullOrEmpty(originalName))
                return originalName;

            string cleaned = originalName;

            // Remove anything in parentheses including the parentheses
            cleaned = System.Text.RegularExpressions.Regex.Replace(cleaned, @"\s*\([^)]*\)", "");

            // Remove anything in brackets including the brackets  
            cleaned = System.Text.RegularExpressions.Regex.Replace(cleaned, @"\s*\[[^\]]*\]", "");

            // Trim any extra whitespace
            cleaned = cleaned.Trim();

            // If we ended up with empty string, fall back to original
            if (string.IsNullOrEmpty(cleaned))
                return originalName;

            return cleaned;
        }
    }
}