// Updated RaidCommandHandler.cs
// Copyright (c) Captolamia. All rights reserved.
// Licensed under the AGPLv3 License. See LICENSE file in the project root for full license information.
//
// Handles the !raid command to trigger raids in exchange for in-game currency.
using LudeonTK;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace CAP_ChatInteractive.Commands.CommandHandlers
{
    public static class RaidCommandHandler
    {
        private static readonly Dictionary<string, RaidStrategyDef> StrategyAliases = new Dictionary<string, RaidStrategyDef>(StringComparer.OrdinalIgnoreCase)
        {
            { "immediate", RaidStrategyDefOf.ImmediateAttack },
            { "smart", DefDatabase<RaidStrategyDef>.GetNamedSilentFail("ImmediateAttackSmart") ?? RaidStrategyDefOf.ImmediateAttack },
            { "sappers", DefDatabase<RaidStrategyDef>.GetNamedSilentFail("ImmediateAttackSappers") ?? RaidStrategyDefOf.ImmediateAttack },
            { "breach", DefDatabase<RaidStrategyDef>.GetNamedSilentFail("ImmediateAttackBreaching") ?? RaidStrategyDefOf.ImmediateAttack },
            { "breachsmart", DefDatabase<RaidStrategyDef>.GetNamedSilentFail("ImmediateAttackBreachingSmart") ?? RaidStrategyDefOf.ImmediateAttack },
            { "stage", DefDatabase<RaidStrategyDef>.GetNamedSilentFail("StageThenAttack") ?? RaidStrategyDefOf.ImmediateAttack },
            { "siege", DefDatabase<RaidStrategyDef>.GetNamedSilentFail("Siege") ?? RaidStrategyDefOf.ImmediateAttack }
        };

        private static CommandSettings GetRaidCommandSettings()
        {
            return CommandSettingsManager.GetSettings("raid"); // CORRECT
        }

        // Check for Royalty DLC (Mech Clusters)
        public static bool HasRoyaltyDLC => ModsConfig.RoyaltyActive;

        // Check for Biotech DLC 
        public static bool HasBiotechDLC => ModsConfig.BiotechActive;

        public static string HandleRaidCommand(ChatMessageWrapper user, string raidType, string strategy, int wager)
        {
            try
            {
                var settings = CAPChatInteractiveMod.Instance.Settings.GlobalSettings;
                var currencySymbol = settings.CurrencyName?.Trim() ?? "¢";

                var viewer = Viewers.GetViewer(user.Username);

                // NEW: Get raid command settings
                var raidSettings = GetRaidCommandSettings();

                // Validate wager amount against settings
                if (wager < raidSettings.MinRaidWager || wager > raidSettings.MaxRaidWager)
                {
                    MessageHandler.SendFailureLetter("Raid Failed",
                        $"{user.Username} tried invalid wager amount\n\nMin: {raidSettings.MinRaidWager}{currencySymbol}, Max: {raidSettings.MaxRaidWager}{currencySymbol}");
                    return $"Wager must be between {raidSettings.MinRaidWager} and {raidSettings.MaxRaidWager}{currencySymbol}.";
                }

                // Validate wager amount
                if (viewer.Coins < wager)
                {
                    MessageHandler.SendFailureLetter("Raid Failed",
                        $"{user.Username} doesn't have enough {currencySymbol} for raid\n\nNeeded: {wager}{currencySymbol}, Has: {viewer.Coins}{currencySymbol}");
                    return $"You need {wager}{currencySymbol} to call a raid! You have {viewer.Coins}{currencySymbol}.";
                }

                // NEW: Check if raid type is allowed
                if (raidSettings.AllowedRaidTypes != null && raidSettings.AllowedRaidTypes.Count > 0 &&
                    !raidSettings.AllowedRaidTypes.Contains(raidType.ToLower()))
                {
                    MessageHandler.SendFailureLetter("Raid Failed",
                        $"{user.Username} tried disabled raid type: {raidType}");
                    return $"Raid type '{raidType}' is not allowed. Allowed types: {string.Join(", ", raidSettings.AllowedRaidTypes)}";
                }

                // NEW: Check if strategy is allowed
                if (!string.IsNullOrEmpty(strategy) && strategy.ToLower() != "default" &&
                    raidSettings.AllowedRaidStrategies != null && raidSettings.AllowedRaidStrategies.Count > 0 &&
                    !raidSettings.AllowedRaidStrategies.Contains(strategy.ToLower()))
                {
                    MessageHandler.SendFailureLetter("Raid Failed",
                        $"{user.Username} tried disabled strategy: {strategy}");
                    return $"Strategy '{strategy}' is not allowed. Allowed strategies: {string.Join(", ", raidSettings.AllowedRaidStrategies)}";
                }

                if (!IsGameReadyForRaid())
                {
                    MessageHandler.SendFailureLetter("Raid Failed",
                        $"{user.Username} tried to call a raid but the game isn't ready");
                    return "Game not ready for raid (no colony, in menu, etc.)";
                }

                // Validate raid type availability
                var validationResult = ValidateRaidType(raidType);
                if (!validationResult.IsValid)
                {
                    MessageHandler.SendFailureLetter("Raid Failed", validationResult.Message);
                    return validationResult.Message;
                }

                var result = TriggerRaid(user.Username, raidType, strategy, wager);

                if (result.Success)
                {
                    viewer.TakeCoins(wager);
                    viewer.GiveKarma(CalculateKarmaChange(wager, raidType, strategy));

                    string raidDetails = BuildRaidDetails(result, wager, currencySymbol);

                    MessageHandler.SendFailureLetter(
                        $"Raid Called by {user.Username}",
                        $"{user.Username} has called for a {raidType} raid!\n\nCost: {wager}{currencySymbol}\n{raidDetails}"
                    );

                    return result.Message;
                }
                else
                {
                    MessageHandler.SendFailureLetter("Raid Failed",
                        $"{user.Username} failed to call a raid\n\n{result.Message}");
                    return $"{result.Message} No {currencySymbol} were deducted.";
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error handling raid command: {ex}");
                MessageHandler.SendFailureLetter("Raid Error",
                    $"Error calling raid: {ex.Message}");
                return "Error calling raid. Please try again.";
            }
        }

        private static RaidValidationResult ValidateRaidType(string raidType)
        {
            switch (raidType.ToLower())
            {
                case "mechcluster":
                    if (!HasRoyaltyDLC)
                    {
                        return new RaidValidationResult(false,
                            "Mech Cluster raids require the Royalty DLC. Use '!raid mech' for standard mechanoid raids.");
                    }
                    break;

                case "water":
                case "wateredge":
                    if (!HasBiotechDLC)
                    {
                        return new RaidValidationResult(false,
                            "Water edge raids require the Biotech DLC.");
                    }
                    break;
            }

            return new RaidValidationResult(true, "");
        }

        private static RaidResult TriggerRaid(string username, string raidType, string strategy, int wager)
        {
            var playerMaps = Current.Game.Maps.Where(map => map.IsPlayerHome).ToList();

            if (!playerMaps.Any())
            {
                return new RaidResult(false, "No player home maps found.");
            }

            // Try each player map until we find one that works
            foreach (var map in playerMaps)
            {
                try
                {
                    var raidConfig = GetRaidConfiguration(raidType, strategy, wager, map);

                    if (raidConfig.IncidentWorker.CanFireNow(raidConfig.Params))
                    {
                        bool executed = raidConfig.IncidentWorker.TryExecute(raidConfig.Params);
                        if (executed)
                        {
                            // Build better description
                            string description = BuildRaidDescription(raidConfig, raidType);

                            return new RaidResult(
                                true,
                                description,
                                raidConfig.Faction,
                                raidConfig.ArrivalMode,
                                raidType,
                                raidConfig.Strategy
                            );
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error($"Error triggering raid on map {map}: {ex}");
                }
            }

            return new RaidResult(false, "No valid targets or factions available for raid right now.");
        }

        private static string BuildRaidDescription(RaidConfiguration config, string raidType)
        {
            var desc = new System.Text.StringBuilder();

            if (config.Faction != null)
            {
                desc.Append($"{config.Faction.Name} ");
            }
            else
            {
                desc.Append("Hostiles ");
            }

            // Add arrival method description
            if (config.ArrivalMode != null)
            {
                desc.Append($"are {GetArrivalDescription(config.ArrivalMode)}");
            }
            else
            {
                desc.Append("are attacking");
            }

            // Add strategy description if available
            if (config.Strategy != null && config.Strategy != RaidStrategyDefOf.ImmediateAttack)
            {
                desc.Append($" using {config.Strategy.label} tactics");
            }

            desc.Append("!");

            return desc.ToString();
        }

        private static string GetArrivalDescription(PawnsArrivalModeDef arrivalMode)
        {
            return arrivalMode.defName switch
            {
                "EdgeWalkIn" => "approaching from the edge",
                "EdgeWalkInGroups" => "approaching in groups from the edge",
                "CenterDrop" => "dropping into the center",
                "EdgeDrop" => "dropping at the edge",
                "RandomDrop" => "dropping randomly across the map",
                "EmergeFromWater" => "emerging from the water",
                "EdgeWalkInDarkness" => "approaching through the darkness",
                "EdgeDropGroups" => "dropping in groups at the edge",
                _ => arrivalMode.label.ToLower() ?? "attacking"
            };
        }

        private static RaidConfiguration GetRaidConfiguration(string raidType, string strategy, int wager, Map map)
        {
            var parms = StorytellerUtility.DefaultParmsNow(IncidentCategoryDefOf.ThreatBig, map);
            parms.forced = true;

            // Instead of overriding points, scale the existing storyteller points
            float basePoints = parms.points; // This is already scaled by storyteller
            float wagerMultiplier = CalculateWagerMultiplier(wager);
            parms.points = basePoints * wagerMultiplier;

            var config = new RaidConfiguration { Params = parms };

            if (raidType.ToLower() == "mechcluster" && !HasRoyaltyDLC)
            {
                // This shouldn't happen due to earlier validation, but just in case
                raidType = "mech";
            }

            // Handle siege strategy specially - prevent mechs from using it
            if (strategy.ToLower() == "siege" && raidType.ToLower() == "mech")
            {
                  // Mech clusters from Royalty DLC can siege since they drop with built structures
                if (HasRoyaltyDLC)
                {
                    // Allow mech clusters to use siege - they drop with turrets and buildings
                    raidType = "mechcluster";
                    strategy = null;
                }
                else if (!HasRoyaltyDLC)
                {
                    // This shouldn't happen due to earlier validation, but just in case
                    raidType = "mech";
                    strategy = "immediate";  // get random strategy ??
                }
            }

            // Handle special raid types first
            switch (raidType.ToLower())
            {
                case "mechcluster":
                    return ConfigureMechClusterRaid(parms, map, strategy);

                case "mech":
                    return ConfigureMechRaid(parms, map, strategy);

                case "drop":
                    var dropResult = GetRandomDropType();
                    config.ArrivalMode = dropResult.Mode;
                    config.IncidentWorker = new IncidentWorker_RaidEnemy();
                    config.IncidentWorker.def = IncidentDefOf.RaidEnemy;
                    parms.points *= dropResult.PointMultiplier;

                    // Ensure strategy is compatible with drop mode
                    if (parms.raidStrategy != null && !IsStrategyCompatibleWithArrival(parms.raidStrategy, dropResult.Mode))
                    {
                        parms.raidStrategy = RaidStrategyDefOf.ImmediateAttack;
                        config.Strategy = RaidStrategyDefOf.ImmediateAttack;
                    }
                    break;

                case "dropcenter":
                    config.ArrivalMode = PawnsArrivalModeDefOf.CenterDrop;
                    config.IncidentWorker = new IncidentWorker_RaidEnemy();
                    config.IncidentWorker.def = IncidentDefOf.RaidEnemy;
                    parms.points *= 0.85f; // Significant reduction for deadliest type
                    break;

                case "dropedge":
                    config.ArrivalMode = PawnsArrivalModeDefOf.EdgeDrop;
                    config.IncidentWorker = new IncidentWorker_RaidEnemy();
                    config.IncidentWorker.def = IncidentDefOf.RaidEnemy;
                    parms.points *= 1.1f; // Increase for easier type
                    break;

                case "dropchaos":
                    config.ArrivalMode = PawnsArrivalModeDefOf.RandomDrop;
                    config.IncidentWorker = new IncidentWorker_RaidEnemy();
                    config.IncidentWorker.def = IncidentDefOf.RaidEnemy;
                    parms.points *= 1.0f; // No adjustment - chaotic but unpredictable
                    break;

                case "dropgroups":
                    config.ArrivalMode = PawnsArrivalModeDefOf.SpecificDropDebug;
                    config.IncidentWorker = new IncidentWorker_RaidEnemy();
                    config.IncidentWorker.def = IncidentDefOf.RaidEnemy;
                    parms.points *= 1.2f; // Increase for easiest type
                    break;

                case "manhunter":
                    config.ArrivalMode = PawnsArrivalModeDefOf.EdgeWalkIn;
                    config.IncidentWorker = new IncidentWorker_Ambush_ManhunterPack();
                    config.IncidentWorker.def = IncidentDefOf.ManhunterPack;
                    parms.faction = null;
                    break;

                case "infestation":
                    config.ArrivalMode = null;
                    config.IncidentWorker = new IncidentWorker_Infestation();
                    config.IncidentWorker.def = IncidentDefOf.Infestation;
                    parms.faction = null;
                    break;

                case "water":
                case "wateredge":
                    var waterMode = DefDatabase<PawnsArrivalModeDef>.GetNamedSilentFail("EmergeFromWater");
                    if (waterMode != null)
                    {
                        config.ArrivalMode = waterMode;
                        config.IncidentWorker = new IncidentWorker_RaidEnemy();
                        config.IncidentWorker.def = IncidentDefOf.RaidEnemy;
                    }
                    else
                    {
                        // Fallback to edge walk-in
                        config.ArrivalMode = PawnsArrivalModeDefOf.EdgeWalkIn;
                        config.IncidentWorker = new IncidentWorker_RaidEnemy();
                        config.IncidentWorker.def = IncidentDefOf.RaidEnemy;
                    }
                    break;

                default: // Standard raid
                    config.ArrivalMode = PawnsArrivalModeDefOf.EdgeWalkIn;
                    config.IncidentWorker = new IncidentWorker_RaidEnemy();
                    config.IncidentWorker.def = IncidentDefOf.RaidEnemy;
                    break;
            }

            // Apply strategy if specified and applicable
            if (!string.IsNullOrEmpty(strategy) && strategy.ToLower() != "default" &&
                raidType.ToLower() != "mechcluster" && raidType.ToLower() != "infestation")
            {
                var strategyDef = ResolveStrategy(strategy); 
                if (strategyDef != null)
                {
                    parms.raidStrategy = strategyDef;
                    config.Strategy = strategyDef;
                }
            }

            // Set arrival mode if not already set by specific raid type
            if (config.ArrivalMode != null && raidType.ToLower() != "mechcluster")
            {
                parms.raidArrivalMode = config.ArrivalMode;
            }

            config.Faction = parms.faction;
            return config;
        }

        // Helper method for weighted random drop selection
        private static DropResult GetRandomDropType()
        {
            var dropOptions = new List<DropOption>
    {
        // High danger - rare
        new DropOption {
            Mode = PawnsArrivalModeDefOf.CenterDrop,
            Weight = 1.0f,
            PointMultiplier = 0.85f,
            Description = "Center Drop (Deadliest)"
        },
        
        // Medium danger - common  
        new DropOption {
            Mode = PawnsArrivalModeDefOf.EdgeDrop,
            Weight = 2.5f,
            PointMultiplier = 1.0f,
            Description = "Edge Drop"
        },
        
        // Chaos - medium rarity
        new DropOption {
            Mode = PawnsArrivalModeDefOf.RandomDrop,
            Weight = 1.5f,
            PointMultiplier = 1.0f,
            Description = "Random Drop (Chaotic)"
        }
    };

            // Try to find EdgeDropGroups if it exists
            var edgeDropGroups = DefDatabase<PawnsArrivalModeDef>.GetNamedSilentFail("EdgeDropGroups");
            if (edgeDropGroups != null)
            {
                dropOptions.Add(new DropOption
                {
                    Mode = edgeDropGroups,
                    Weight = 3.0f,
                    PointMultiplier = 1.15f,
                    Description = "Edge Drop Groups (Easiest)"
                });
            }
            else
            {
                // Fallback to EdgeWalkInGroups if EdgeDropGroups not found
                dropOptions.Add(new DropOption
                {
                    Mode = PawnsArrivalModeDefOf.EdgeWalkInGroups,
                    Weight = 3.0f,
                    PointMultiplier = 1.15f,
                    Description = "Edge Walk In Groups (Easiest)"
                });
            }

            var selectedOption = dropOptions.RandomElementByWeight(opt => opt.Weight);

            return new DropResult
            {
                Mode = selectedOption.Mode,
                PointMultiplier = selectedOption.PointMultiplier,
                Description = selectedOption.Description
            };
        }

        private static bool IsStrategyCompatibleWithArrival(RaidStrategyDef strategy, PawnsArrivalModeDef arrivalMode)
        {
            if (strategy.arriveModes == null || strategy.arriveModes.Count == 0)
                return true; // No restrictions

            return strategy.arriveModes.Contains(arrivalMode);
        }

        private static float CalculateWagerMultiplier(int wager)
        {
            // Scale wager to reasonable multiplier
            // 1000 coins = 0.5x, 5000 coins = 1.0x, 20000 coins = 2.0x
            return Mathf.Clamp(wager / 5000f, 0.5f, 2.0f);
        }

        private static RaidConfiguration ConfigureMechClusterRaid(IncidentParms parms, Map map, string strategy)
        {
            var config = new RaidConfiguration();

            // Mech Cluster is a specific incident type from Royalty DLC
            var mechClusterIncident = DefDatabase<IncidentDef>.GetNamedSilentFail("MechCluster");
            if (mechClusterIncident != null)
            {
                config.IncidentWorker = (IncidentWorker)Activator.CreateInstance(mechClusterIncident.Worker.GetType());
                config.IncidentWorker.def = mechClusterIncident;
                config.ArrivalMode = null; // Mech clusters have special spawn logic
                config.Strategy = null;
  
                parms.faction = Faction.OfMechanoids;
            }
            else
            {
                // Fallback to standard mech raid if cluster not available
                return ConfigureMechRaid(parms, map, strategy);
            }

            config.Params = parms;
            config.Faction = parms.faction;
            return config;
        }

        private static RaidConfiguration ConfigureMechRaid(IncidentParms parms, Map map, string strategy)
        {
            var config = new RaidConfiguration();
    
            config.IncidentWorker = new IncidentWorker_RaidEnemy();
            config.IncidentWorker.def = IncidentDefOf.RaidEnemy;
            config.ArrivalMode = PawnsArrivalModeDefOf.EdgeWalkIn;
    
            // Force mechanoid faction
            parms.faction = Faction.OfMechanoids;
    
            // Apply strategy if not siege (already handled above)
            var strategyDef = ResolveStrategy(strategy);
            if (strategyDef != null)
            {
                parms.raidStrategy = strategyDef;
                config.Strategy = strategyDef;
            }
            else
            {
                // Default to immediate attack for mechs
                parms.raidStrategy = RaidStrategyDefOf.ImmediateAttack;
                config.Strategy = RaidStrategyDefOf.ImmediateAttack;
            }

            config.Params = parms;
            config.Faction = parms.faction;
            return config;
        }

        private static RaidConfiguration ConfigureSiegeRaid(IncidentParms parms, Map map)
        {
            var config = new RaidConfiguration();

            // Try to use Siege incident def if available
            var siegeIncident = DefDatabase<IncidentDef>.GetNamedSilentFail("Siege");
            if (siegeIncident != null)
            {
                config.IncidentWorker = (IncidentWorker)Activator.CreateInstance(siegeIncident.Worker.GetType());
                config.IncidentWorker.def = siegeIncident;
                config.Strategy = null; // Siege incident handles its own strategy
            }
            else
            {
                // Fallback to standard raid with siege strategy
                config.IncidentWorker = new IncidentWorker_RaidEnemy();
                config.IncidentWorker.def = IncidentDefOf.RaidEnemy;

                var siegeStrategy = DefDatabase<RaidStrategyDef>.GetNamedSilentFail("Siege");
                if (siegeStrategy != null)
                {
                    parms.raidStrategy = siegeStrategy;
                    config.Strategy = siegeStrategy;
                }
                else
                {
                    // Ultimate fallback - immediate attack
                    parms.raidStrategy = RaidStrategyDefOf.ImmediateAttack;
                    config.Strategy = RaidStrategyDefOf.ImmediateAttack;
                }
            }

            config.ArrivalMode = PawnsArrivalModeDefOf.EdgeWalkIn;
            config.Params = parms;

            // Siege needs enemy faction with siege capability
            // Exclude mechanoids since they can't build
            parms.faction = Find.FactionManager.AllFactions
                .Where(f => f.HostileTo(Faction.OfPlayer) &&
                           f.def != FactionDefOf.Mechanoid &&
                           f.def.techLevel >= TechLevel.Industrial)
                .RandomElementWithFallback();

            config.Faction = parms.faction;
            return config;
        }

        private static RaidStrategyDef ResolveStrategy(string strategyName)
        {
            // Try direct def name first
            var strategyDef = DefDatabase<RaidStrategyDef>.GetNamedSilentFail(strategyName);
            if (strategyDef != null)
            {
                return strategyDef;
            }

            // Try aliases
            if (StrategyAliases.TryGetValue(strategyName, out strategyDef))
            {
                return strategyDef;
            }

            return null;
        }

        private static bool IsGameReadyForRaid()
        {
            return Current.Game != null &&
                   Current.ProgramState == ProgramState.Playing &&
                   Current.Game.Maps.Any(map => map.IsPlayerHome);
        }

        private static int CalculateKarmaChange(int wager, string raidType, string strategy)
        {
            // Negative karma for hostile actions, scaled by raid severity
            int baseKarma = (int)(wager / 1000f * -3);

            // More negative karma for more destructive raid types
            switch (raidType.ToLower())
            {
                case "mechcluster":
                    return baseKarma - 8; // Most destructive
                case "siege":
                    return baseKarma - 6;
                case "mech":
                    return baseKarma - 5;
                case "infestation":
                    return baseKarma - 4;
                case "manhunter":
                    return baseKarma - 3;
                default:
                    return baseKarma - 2;
            }
        }

        private static string BuildRaidDetails(RaidResult result, int wager, string currencySymbol)
        {
            var details = new System.Text.StringBuilder();

            // The main message already includes arrival info, so don't duplicate it
            details.AppendLine(result.Message);

            // Only show additional details if they add value
            if (result.Faction != null)
            {
                details.AppendLine($"Faction: {result.Faction.Name}");
            }

            if (result.Strategy != null && result.Strategy != RaidStrategyDefOf.ImmediateAttack)
            {
                details.AppendLine($"Strategy: {result.Strategy.label}");
            }

            details.AppendLine($"Type: {result.RaidType}");
            details.AppendLine($"Cost: {wager}{currencySymbol}");

            return details.ToString();
        }

        // Debug method to test DLC detection
        [DebugAction("CAP", "Test DLC Detection", allowedGameStates = AllowedGameStates.Playing)]
        public static void DebugTestDLCDetection()
        {
            Logger.Message($"Royalty DLC Active: {HasRoyaltyDLC}");
            Logger.Message($"Biotech DLC Active: {HasBiotechDLC}");

            // Test mech cluster availability
            var mechCluster = DefDatabase<IncidentDef>.GetNamedSilentFail("MechCluster");
            Logger.Message($"Mech Cluster Available: {mechCluster != null}");

            // Test water arrival mode
            var waterMode = DefDatabase<PawnsArrivalModeDef>.GetNamedSilentFail("EmergeFromWater");
            Logger.Message($"Water Arrival Mode Available: {waterMode != null}");
        }
    }

    public class RaidValidationResult
    {
        public bool IsValid { get; }
        public string Message { get; }

        public RaidValidationResult(bool isValid, string message)
        {
            IsValid = isValid;
            Message = message;
        }
    }

    public class RaidResult
    {
        public bool Success { get; }
        public string Message { get; }
        public Faction Faction { get; }
        public PawnsArrivalModeDef ArrivalMode { get; }
        public string RaidType { get; }
        public RaidStrategyDef Strategy { get; }

        public RaidResult(bool success, string message, Faction faction = null,
                         PawnsArrivalModeDef arrivalMode = null, string raidType = "standard",
                         RaidStrategyDef strategy = null)
        {
            Success = success;
            Message = message;
            Faction = faction;
            ArrivalMode = arrivalMode;
            RaidType = raidType;
            Strategy = strategy;
        }
    }

    public class RaidConfiguration
    {
        public IncidentParms Params { get; set; }
        public IncidentWorker IncidentWorker { get; set; }
        public Faction Faction { get; set; }
        public PawnsArrivalModeDef ArrivalMode { get; set; }
        public RaidStrategyDef Strategy { get; set; }
    }

    public class DropOption
    {
        public PawnsArrivalModeDef Mode { get; set; }
        public float Weight { get; set; }
        public float PointMultiplier { get; set; }
        public string Description { get; set; }
    }

    public class DropResult
    {
        public PawnsArrivalModeDef Mode { get; set; }
        public float PointMultiplier { get; set; }
        public string Description { get; set; }
    }
}