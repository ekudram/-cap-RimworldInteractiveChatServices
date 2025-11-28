// IncidentCommandHandler.cs
// Copyright (c) Captolamia. All rights reserved.
// Licensed under the AGPLv3 License. See LICENSE file in the project root for full license information.
//
// Handles the !event command for triggering incidents via chat
using CAP_ChatInteractive.Commands.Cooldowns;
using CAP_ChatInteractive.Incidents;
using CAP_ChatInteractive.Utilities;
using LudeonTK;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace CAP_ChatInteractive.Commands.CommandHandlers
{
    public static class IncidentCommandHandler
    {
        public static string HandleIncidentCommand(ChatMessageWrapper messageWrapper, string incidentType)
        {
            try
            {
                var settings = CAPChatInteractiveMod.Instance.Settings.GlobalSettings;
                var currencySymbol = settings.CurrencyName?.Trim() ?? "¢";

                // Check if viewer exists
                var viewer = Viewers.GetViewer(messageWrapper);
                if (viewer == null)
                {
                    MessageHandler.SendFailureLetter("Incident Failed",
                        $"Could not find viewer data for {messageWrapper.Username}");
                    return "Error: Could not find your viewer data.";
                }

                // Find the incident by command input
                var buyableIncident = FindBuyableIncident(incidentType);
                if (buyableIncident == null)
                {
                    var availableTypes = GetAvailableIncidents().Take(5).Select(i => i.Key);
                    // removed to much letter spam, just tell the viewer
                    // MessageHandler.SendFailureLetter("Incident Failed", $"{messageWrapper.Username} tried unknown incident: {incidentType}");
                    return $"Unknown incident type: {incidentType}. Try !event list";
                }

                // Check if incident is enabled
                if (!buyableIncident.Enabled)
                {
                    MessageHandler.SendFailureLetter("Incident Failed",
                        $"{messageWrapper.Username} tried disabled incident: {buyableIncident.Label}");
                    return $"{buyableIncident.Label} is currently disabled.";
                }

                // DEBUG: Log incident details
                Logger.Debug($"=== INCIDENT COOLDOWN DEBUG ===");
                Logger.Debug($"Incident: {buyableIncident.Label}");
                Logger.Debug($"DefName: {buyableIncident.DefName}");
                Logger.Debug($"KarmaType: {buyableIncident.KarmaType}");
                Logger.Debug($"BaseCost: {buyableIncident.BaseCost}");

                // Check global cooldowns using your existing system
                var cooldownManager = Current.Game.GetComponent<GlobalCooldownManager>();
                if (cooldownManager != null)
                {
                    // Get command settings for the "event" command
                    var commandSettings = CommandSettingsManager.GetSettings("event");
                    if (commandSettings == null)
                    {
                        // Fallback settings if specific command settings aren't found
                        commandSettings = new CommandSettings
                        {
                            useCommandCooldown = true,
                            MaxUsesPerCooldownPeriod = 0 // Use global event system
                        };
                    }

                    // Use the corrected cooldown check
                    if (!cooldownManager.CanUseCommand("event", commandSettings, settings))
                    {
                        // Provide appropriate feedback based on what failed
                        if (!cooldownManager.CanUseGlobalEvents(settings))
                        {
                            int totalEvents = cooldownManager.data.EventUsage.Values.Sum(record => record.CurrentPeriodUses);
                            return $"❌ Global event limit reached! ({totalEvents}/{settings.EventsperCooldown} used this period)";
                        }

                        string eventType = GetKarmaTypeForIncident(buyableIncident.KarmaType);
                        if (settings.KarmaTypeLimitsEnabled && !cooldownManager.CanUseEvent(eventType, settings))
                        {
                            return GetCooldownMessage(eventType, settings, cooldownManager);
                        }

                        return $"❌ Command cooldown active for {buyableIncident.Label}";
                    }
                }

                // Check if viewer can afford it
                int cost = buyableIncident.BaseCost;
                if (viewer.Coins < cost)
                {
                    MessageHandler.SendFailureLetter("Incident Failed",
                        $"{messageWrapper.Username} can't afford {buyableIncident.Label}");
                    return $"You need {cost}{currencySymbol} for {buyableIncident.Label}!";
                }

                // Try to trigger the incident
                bool success = TriggerIncident(buyableIncident, messageWrapper.Username, out string resultMessage);

                // Handle result
                if (success)
                {
                    // Deduct coins and process purchase
                    viewer.TakeCoins(cost);

                    // Award karma based on purchase
                    int karmaEarned = cost / 100;
                    if (karmaEarned > 0)
                    {
                        viewer.GiveKarma(karmaEarned);
                        Logger.Debug($"Awarded {karmaEarned} karma for {cost} coin purchase");
                    }
                    else if (karmaEarned < 0)
                    {
                        viewer.TakeKarma(Math.Abs(karmaEarned));
                        Logger.Debug($"Deducted {Math.Abs(karmaEarned)} karma for {cost} coin purchase");
                    }

                    // Record event usage for cooldowns
                    if (cooldownManager != null)
                    {
                        string eventType = GetKarmaTypeForIncident(buyableIncident.KarmaType);
                        cooldownManager.RecordEventUse(eventType);
                        Logger.Debug($"Recorded event usage for type: {eventType}");

                        // Log current state after recording
                        var record = cooldownManager.data.EventUsage.GetValueOrDefault(eventType);
                        if (record != null)
                        {
                            Logger.Debug($"Current usage for {eventType}: {record.CurrentPeriodUses}");
                        }
                    }

                    return resultMessage;
                }
                else
                {
                    return $"{resultMessage} No {currencySymbol} were deducted.";
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error handling incident command: {ex}");
                return "Error triggering incident. Please try again.";
            }
        }

        // Helper methods for cooldown integration
        private static string GetKarmaTypeForIncident(string karmaType)
        {
            if (string.IsNullOrEmpty(karmaType))
                return "neutral";

            return karmaType?.ToLower() switch
            {
                "good" => "good",
                "bad" => "bad",
                _ => "neutral"
            };
        }

        private static string GetCooldownMessage(string eventType, CAPGlobalChatSettings settings, GlobalCooldownManager cooldownManager)
        {
            int maxEvents = eventType switch
            {
                "good" => settings.MaxGoodEvents,
                "bad" => settings.MaxBadEvents,
                "neutral" => settings.MaxNeutralEvents,
                _ => 10
            };

            var record = cooldownManager.data.EventUsage.GetValueOrDefault(eventType);
            int currentUses = record?.CurrentPeriodUses ?? 0;

            return $"❌ {eventType.ToUpper()} event limit reached! ({currentUses}/{maxEvents} used this period)";
        }

        private static BuyableIncident FindBuyableIncident(string input)
        {
            if (string.IsNullOrEmpty(input))
                return null;

            string inputLower = input.ToLower();
            var allIncidents = GetAvailableIncidents();

            // Exact key match
            if (allIncidents.TryGetValue(inputLower, out var incident))
                return incident;

            // Case-insensitive def name match
            var defNameMatch = allIncidents.Values.FirstOrDefault(i =>
                i.DefName.Equals(input, StringComparison.OrdinalIgnoreCase));
            if (defNameMatch != null)
                return defNameMatch;

            // Label match
            var labelMatch = allIncidents.Values.FirstOrDefault(i =>
                i.Label.Equals(input, StringComparison.OrdinalIgnoreCase));
            if (labelMatch != null)
                return labelMatch;

            // Partial match
            var partialMatch = allIncidents.Values.FirstOrDefault(i =>
                i.DefName.ToLower().Contains(inputLower) ||
                i.Label.ToLower().Contains(inputLower));

            return partialMatch;
        }

        public static Dictionary<string, BuyableIncident> GetAvailableIncidents()
        {
            return IncidentsManager.AllBuyableIncidents
                .Where(kvp => IsIncidentSuitableForCommand(kvp.Value))
                .ToDictionary(kvp => kvp.Key.ToLower(), kvp => kvp.Value);
        }

        private static bool IsIncidentSuitableForCommand(BuyableIncident incident)
        {
            // Now just check the properties set during creation
            return incident.Enabled && incident.IsAvailableForCommands;
        }

        private static bool TriggerIncident(BuyableIncident incident, string username, out string resultMessage)
        {
            resultMessage = "";
            var incidentDef = DefDatabase<IncidentDef>.GetNamedSilentFail(incident.DefName);

            if (incidentDef == null)
            {
                resultMessage = $"Incident {incident.Label} not found.";
                return false;
            }

            var worker = incidentDef.Worker;
            if (worker == null)
            {
                resultMessage = $"No worker for incident {incident.Label}.";
                return false;
            }

            var playerMaps = Current.Game.Maps.Where(map => map.IsPlayerHome).ToList();
            playerMaps.Shuffle();

            foreach (var map in playerMaps)
            {
                var parms = new IncidentParms
                {
                    target = map,
                    forced = true,
                    points = StorytellerUtility.DefaultThreatPointsNow(map)
                };

                if (worker.CanFireNow(parms) && !worker.FiredTooRecently(map))
                {
                    bool executed = worker.TryExecute(parms);
                    if (executed)
                    {
                        resultMessage = GetIncidentSuccessMessage(incident);
                        return true;
                    }
                }
            }

            resultMessage = $"{incident.Label} cannot be triggered right now.";
            return false;
        }

        private static string GetIncidentSuccessMessage(BuyableIncident incident)
        {
            return incident.DefName switch
            {
                "ResourcePodCrash" => "A resource pod crashes from the sky!",
                "PsychicSoothe" => "A calming psychic wave soothes the colonists.",
                "SelfTame" => "A wild animal decides to join the colony!",
                "AmbrosiaSprout" => "Ambrosia plants sprout nearby!",
                "FarmAnimalsWanderIn" => "Farm animals wander into the area.",
                "WandererJoin" => "A wanderer joins the colony!",
                "RefugeePodCrash" => "A refugee pod crashes nearby!",
                "ThrumboPasses" => "Rare thrumbos pass through the area!",
                "MeteoriteImpact" => "A meteorite crashes nearby!",
                "HerdMigration" => "A herd of animals migrates through!",
                "ShortCircuit" => "An electrical short circuit occurs!",
                "OrbitalTraderArrival" => "An orbital trader arrives!",

                // Weather events that work as dramatic incidents
                "HeatWave" => "A blistering heat wave settles over the land!",
                "ColdSnap" => "An icy cold snap freezes the air!",
                "Flashstorm" => "Dark clouds gather as a flashstorm crackles to life!",
                "PsychicDrone" => "A disturbing psychic drone affects the colonists!",
                "ToxicFallout" => "Deadly toxic fallout begins to rain down!",
                "VolcanicWinter" => "Volcanic ash clouds bring endless winter!",
                "Eclipse" => "An unnatural darkness falls as the sun is eclipsed!",
                "SolarFlare" => "A solar flare disrupts all electronics!",

                // Other dramatic events
                "CropBlight" => "A terrible blight strikes the crops!",
                "Alphabeavers" => "A pack of alphabeavers arrives to chew everything!",
                "ShipChunkDrop" => "Ship chunks rain from the sky!",

                // DLC incidents that are great for events
                "NoxiousHaze" => "Acidic smog blankets the area!",
                "WastepackInfestation" => "Wastepack insects emerge!",
                "BloodRain" => "Creepy blood rain starts falling!",
                "DeathPall" => "A death pall settles over the colony!",

                _ => $"{incident.Label} occurs!"
            };
        }

        [DebugAction("CAP", "List Filtered Incidents", allowedGameStates = AllowedGameStates.Playing)]
        public static void DebugListFilteredIncidents()
        {
            var allIncidents = IncidentsManager.AllBuyableIncidents;
            var availableIncidents = GetAvailableIncidents();

            Logger.Message($"=== INCIDENT FILTERING REPORT ===");
            Logger.Message($"Total incidents: {allIncidents.Count}");
            Logger.Message($"Available for !event command: {availableIncidents.Count}");
            Logger.Message($"Filtered out: {allIncidents.Count - availableIncidents.Count}");
            Logger.Message("");

            // Group incidents by source
            var rimworldIncidents = allIncidents.Values.Where(i => i.ModSource == "RimWorld" || i.ModSource == "Core").ToList();
            var dlcIncidents = allIncidents.Values.Where(i =>
                i.ModSource.Contains("Royalty") ||
                i.ModSource.Contains("Ideology") ||
                i.ModSource.Contains("Biotech") ||
                i.ModSource.Contains("Anomaly") ||
                i.ModSource.Contains("Odyssey")).ToList();
            var modIncidents = allIncidents.Values.Where(i =>
                !rimworldIncidents.Contains(i) && !dlcIncidents.Contains(i)).ToList();

            // Log RimWorld incidents
            Logger.Message($"=== RIMWORLD INCIDENTS ({rimworldIncidents.Count}) ===");
            foreach (var incident in rimworldIncidents.OrderBy(i => i.DefName))
            {
                string status = IsIncidentSuitableForCommand(incident) ? "AVAILABLE" : "FILTERED";
                Logger.Message($"{status}: {incident.DefName} - {incident.Label} (Source: {incident.ModSource})");
            }
            Logger.Message("");

            // Log DLC incidents
            Logger.Message($"=== DLC INCIDENTS ({dlcIncidents.Count}) ===");
            foreach (var incident in dlcIncidents.OrderBy(i => i.ModSource).ThenBy(i => i.DefName))
            {
                string status = IsIncidentSuitableForCommand(incident) ? "AVAILABLE" : "FILTERED";
                Logger.Message($"{status}: {incident.DefName} - {incident.Label} (Source: {incident.ModSource})");
            }
            Logger.Message("");

            // Log mod incidents
            Logger.Message($"=== MOD INCIDENTS ({modIncidents.Count}) ===");
            foreach (var incident in modIncidents.OrderBy(i => i.ModSource).ThenBy(i => i.DefName))
            {
                string status = IsIncidentSuitableForCommand(incident) ? "AVAILABLE" : "FILTERED";
                Logger.Message($"{status}: {incident.DefName} - {incident.Label} (Source: {incident.ModSource})");
            }

            // Summary by source
            Logger.Message("");
            Logger.Message($"=== SUMMARY BY SOURCE ===");
            Logger.Message($"RimWorld: {rimworldIncidents.Count(i => IsIncidentSuitableForCommand(i))}/{rimworldIncidents.Count} available");
            Logger.Message($"DLC: {dlcIncidents.Count(i => IsIncidentSuitableForCommand(i))}/{dlcIncidents.Count} available");
            Logger.Message($"Mods: {modIncidents.Count(i => IsIncidentSuitableForCommand(i))}/{modIncidents.Count} available");
        }

    }

}