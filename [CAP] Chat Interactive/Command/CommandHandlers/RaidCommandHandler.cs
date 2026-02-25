// Updated RaidCommandHandler.cs
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
// Handles the !raid command to trigger raids in exchange for in-game currency.
using CAP_ChatInteractive.Commands.Cooldowns;
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

                var viewer = Viewers.GetViewer(user);
                if (viewer == null)
                {
                    MessageHandler.SendFailureLetter("Raid Failed",
                        $"Could not find viewer data for {user.Username}");
                    return "RICS.Raid.ErrorViewerData".Translate();
                }

                var raidSettings = GetRaidCommandSettings();

                var cooldownManager = Current.Game.GetComponent<GlobalCooldownManager>();
                if (cooldownManager != null)
                {
                    if (!cooldownManager.CanUseCommand("raid", raidSettings, settings))
                    {
                        if (!cooldownManager.CanUseGlobalEvents(settings))
                        {
                            int totalEvents = cooldownManager.data.EventUsage.Values.Sum(record => record.CurrentPeriodUses);
                            return "RICS.Raid.GlobalLimitReached".Translate(totalEvents, settings.EventsperCooldown);
                        }

                        if (settings.KarmaTypeLimitsEnabled && !cooldownManager.CanUseEvent("bad", settings))
                        {
                            var badRecord = cooldownManager.data.EventUsage.GetValueOrDefault("bad");
                            int badUsed = badRecord?.CurrentPeriodUses ?? 0;
                            return "RICS.Raid.BadLimitReached".Translate(badUsed, settings.MaxBadEvents);
                        }

                        return "RICS.Raid.OnCooldown".Translate();
                    }
                }

                if (wager < raidSettings.MinRaidWager || wager > raidSettings.MaxRaidWager)
                {
                    return "RICS.Raid.WagerInvalid".Translate(raidSettings.MinRaidWager, raidSettings.MaxRaidWager, currencySymbol);
                }

                if (viewer.Coins < wager)
                {
                    return "RICS.Raid.NotEnoughCoins".Translate(wager, currencySymbol, viewer.Coins);
                }

                if (raidSettings.AllowedRaidTypes != null && raidSettings.AllowedRaidTypes.Count > 0 &&
                    !raidSettings.AllowedRaidTypes.Contains(raidType.ToLower()))
                {
                    return "RICS.Raid.TypeNotAllowed".Translate(raidType, string.Join(", ", raidSettings.AllowedRaidTypes));
                }

                if (!string.IsNullOrEmpty(strategy) && strategy.ToLower() != "default" &&
                    raidSettings.AllowedRaidStrategies != null && raidSettings.AllowedRaidStrategies.Count > 0 &&
                    !raidSettings.AllowedRaidStrategies.Contains(strategy.ToLower()))
                {
                    return "RICS.Raid.StrategyNotAllowed".Translate(strategy, string.Join(", ", raidSettings.AllowedRaidStrategies));
                }

                if (!IsGameReadyForRaid())
                {
                    return "RICS.Raid.GameNotReady".Translate();
                }

                var validationResult = ValidateRaidType(raidType);
                if (!validationResult.IsValid)
                {
                    return validationResult.Message;  // already contains translated content in most cases
                }

                var result = TriggerRaid(user.Username, raidType, strategy, wager);

                if (result.Success)
                {
                    viewer.TakeCoins(wager);
                    viewer.TakeKarma(CalculateKarmaChange(wager, raidType, strategy));

                    if (cooldownManager != null)
                    {
                        cooldownManager.RecordEventUse("bad");
                    }

                    string raidDetails = BuildRaidDetails(result, wager, currencySymbol);

                    MessageHandler.SendFailureLetter(
                        "RICS.Raid.LetterTitle".Translate(user.Username),
                        "RICS.Raid.LetterText".Translate(user.Username, raidType, wager, currencySymbol, raidDetails)
                    );

                    return "RICS.Raid.SuccessMessage".Translate(user.Username, wager, currencySymbol, raidType);
                }
                else
                {
                    return "RICS.Raid.FailureNoDeduction".Translate(result.Message, currencySymbol);
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error handling raid command: {ex}");
                return "RICS.Raid.GenericError".Translate();
            }
        }

        private static RaidValidationResult ValidateRaidType(string raidType)
        {
            switch (raidType.ToLower())
            {
                case "mechcluster":
                    if (!HasRoyaltyDLC)
                    {
                        return new RaidValidationResult(false, "RICS.Raid.MechClusterRequiresRoyalty".Translate());
                    }
                    break;

                case "water":
                case "wateredge":
                    if (!HasBiotechDLC)
                    {
                        return new RaidValidationResult(false, "RICS.Raid.WaterRequiresBiotech".Translate());
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
                // return new RaidResult(false, "No player home maps found.");
                return new RaidResult(false, "RICSRICS.Raid.NoValidMaps".Translate()); 
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

            // return new RaidResult(false, "No valid targets or factions available for raid right now.");
            return new RaidResult(false, "RICS.raid.NoValidTargets".Translate() );
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
                raidType.ToLower() != "mechcluster")
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
            int baseKarma = (int)(wager / 100f);

            // More negative karma for more destructive raid types
            switch (raidType.ToLower())
            {
                case "mechcluster":
                    return baseKarma - 8; // Most destructive
                case "siege":
                    return baseKarma - 6;
                case "mech":
                    return baseKarma - 5;
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