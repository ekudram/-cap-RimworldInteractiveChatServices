// IncidentCommandHandler.cs
// Copyright (c) Captolamia
// This file is part of CAP Chat Interactive.  RICS Rimwold Chat Interactive System
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
// Handles the !event command for triggering incidents via chat
using CAP_ChatInteractive.Commands.Cooldowns;
using CAP_ChatInteractive.Incidents;
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

                var viewer = Viewers.GetViewer(messageWrapper);
                if (viewer == null)
                    return "RICS.ICH.RETURN.ErrorFindingViewerData".Translate();

                var buyableIncident = FindBuyableIncident(incidentType);
                if (buyableIncident == null)
                {
                    var availableTypes = GetAvailableIncidents().Take(5).Select(i => i.Key);
                    return "RICS.ICH.RETURN.UnknownIncidentType".Translate(incidentType, string.Join(", ", availableTypes));
                }

                if (!buyableIncident.Enabled)
                    return "RICS.ICH.RETURN.IncidentDisabled".Translate(buyableIncident.Label);

                // === COOLDOWN DEBUG ===
                Logger.Debug($"=== INCIDENT COOLDOWN DEBUG ===");
                Logger.Debug($"Incident: {buyableIncident.Label}");
                Logger.Debug($"DefName: {buyableIncident.DefName}");
                Logger.Debug($"KarmaType: {buyableIncident.KarmaType}");
                Logger.Debug($"BaseCost: {buyableIncident.BaseCost}");
                Logger.Debug($"CooldownDays: {buyableIncident.CooldownDays}");

                var cooldownManager = Current.Game.GetComponent<GlobalCooldownManager>();
                if (cooldownManager != null)
                {
                    // === 1. Global + Karma-type limits FIRST (highest priority for feedback) ===
                    var commandSettings = CommandSettingsManager.GetSettings("event") ?? new CommandSettings { useCommandCooldown = true, MaxUsesPerCooldownPeriod = 0 };

                    if (!cooldownManager.CanUseCommand("event", commandSettings, settings))
                    {
                        if (!cooldownManager.CanUseGlobalEvents(settings))
                        {
                            int total = cooldownManager.data.EventUsage.Values.Sum(r => r.CurrentPeriodUses);
                            return "RICS.ICH.RETURN.GlobalEventLimitReached".Translate(total, settings.EventsperCooldown);
                        }

                        if (settings.KarmaTypeLimitsEnabled)
                        {
                            string eventType = GetKarmaTypeForIncident(buyableIncident.KarmaType);
                            if (!cooldownManager.CanUseEvent(eventType, settings))
                            {
                                return GetCooldownMessage(eventType, settings, cooldownManager);
                            }
                        }

                        return "RICS.ICH.RETURN.CommandCooldownActive".Translate(buyableIncident.Label);
                    }

                    // === 2. Individual incident cooldown (only checked if global limits passed) ===
                    if (settings.EventCooldownsEnabled && buyableIncident.CooldownDays > 0)
                    {
                        if (!cooldownManager.CanUseIncident(buyableIncident.DefName, buyableIncident.CooldownDays, settings))
                        {
                            int daysRemaining = GetRemainingCooldownDays(buyableIncident.DefName, buyableIncident.CooldownDays, cooldownManager);
                            string cooldownMessage = GetIndividualCooldownMessage(buyableIncident.Label, daysRemaining, settings.EventCooldownDays);
                            Logger.Debug($"Individual cooldown blocked: {cooldownMessage}");
                            return cooldownMessage;
                        }
                    }
                }

                // === AFFORDABILITY CHECK ===
                int cost = buyableIncident.BaseCost;
                if (viewer.Coins < cost)
                    return "RICS.WCH.InsufficientFunds".Translate(cost, currencySymbol, buyableIncident.Label);

                // === TRY TRIGGER ===
                bool success = TriggerIncident(buyableIncident, messageWrapper.Username, out string resultMessage);

                if (success)
                {
                    viewer.TakeCoins(cost);

                    string karmaLower = buyableIncident.KarmaType?.ToLowerInvariant() ?? "neutral";
                    float karmaAmount = 0f;

                    switch (karmaLower)
                    {
                        case "good":
                            karmaAmount = settings.KarmaGainPerGoodEvent;
                            if (karmaAmount > 0f)
                            {
                                viewer.GiveKarma(karmaAmount);
                                Logger.Debug($"Awarded {karmaAmount:F2} karma (settings.KarmaGainPerGoodEvent) for Good event purchase");
                            }
                            break;

                        case "bad":
                            karmaAmount = settings.KarmaLossPerBadEvent;
                            if (karmaAmount > 0f)
                            {
                                viewer.TakeKarma(karmaAmount);
                                Logger.Debug($"Deducted {karmaAmount:F2} karma (settings.KarmaLossPerBadEvent) for Bad event purchase");
                            }
                            break;

                        case "doom":
                            karmaAmount = settings.KarmaLossPerDoomEvent;
                            if (karmaAmount > 0f)
                            {
                                viewer.TakeKarma(karmaAmount);
                                Logger.Debug($"Deducted {karmaAmount:F2} karma (settings.KarmaLossPerDoomEvent) for Doom event purchase");
                            }
                            break;

                        default: // neutral
                            karmaAmount = settings.KarmaGainPerNeutralEvent;
                            if (karmaAmount > 0f)
                            {
                                viewer.GiveKarma(karmaAmount);
                                Logger.Debug($"Awarded {karmaAmount:F2} karma (settings.KarmaGainPerNeutralEvent) for Neutral event purchase");
                            }
                            break;
                    }

                    if (cooldownManager != null)
                    {
                        if (settings.EventCooldownsEnabled && buyableIncident.CooldownDays > 0)
                            cooldownManager.RecordIncidentUse(buyableIncident.DefName);

                        string eventType = GetKarmaTypeForIncident(buyableIncident.KarmaType);
                        cooldownManager.RecordEventUse(eventType);

                        string logKey = (eventType.ToLowerInvariant() == "doom") ? "bad" : eventType.ToLowerInvariant();
                        var record = cooldownManager.data.EventUsage.GetValueOrDefault(logKey);
                        Logger.Debug($"Recorded event usage for type: {eventType} (normalized {(eventType.ToLowerInvariant() == "doom" ? "to bad" : "")}) → Current: {record?.CurrentPeriodUses ?? 0}");
                    }

                    if (buyableIncident.KarmaType?.ToLowerInvariant() == "doom")
                        Messages.Message("RICS.ICH.RETURN.DoomEventPurchased".Translate(buyableIncident.Label), MessageTypeDefOf.ThreatBig);

                    return resultMessage;
                }
                else
                {
                    return "RICS.ICH.RETURN.IncidentTriggerFailed".Translate(resultMessage, currencySymbol);
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error handling incident command: {ex}");
                return "RICS.ICH.RETURN.ErrorTriggeringIncident".Translate();
            }
        }

        // NEW: Helper method to get remaining cooldown days
        private static int GetRemainingCooldownDays(string incidentDefName, int incidentCooldownDays, GlobalCooldownManager cooldownManager)
        {
            if (cooldownManager.data.IncidentUsage == null ||
                !cooldownManager.data.IncidentUsage.ContainsKey(incidentDefName))
                return 0;

            var record = cooldownManager.data.IncidentUsage[incidentDefName];
            int currentDay = GenDate.DaysPassed;

            // Find the most recent use of this incident
            if (record.UsageDays.Count == 0)
                return 0;

            int mostRecentUseDay = record.UsageDays.Max();
            int daysSinceUse = currentDay - mostRecentUseDay;
            int daysRemaining = incidentCooldownDays - daysSinceUse;

            return Math.Max(0, daysRemaining);
        }

        // NEW: Helper method for individual cooldown messages
        private static string GetIndividualCooldownMessage(string incidentLabel, int daysRemaining, int globalCooldownDays)
        {
            if (daysRemaining > 0)
            {
                if (daysRemaining == 1)
                    // return $"❌ {incidentLabel} is on cooldown for {daysRemaining} more day";
                    return "RICS.ICH.RETURN.IncidentCooldownOneDay".Translate(incidentLabel);   
                else
                    // return $"❌ {incidentLabel} is on cooldown for {daysRemaining} more days";
                    return "RICS.ICH.RETURN.IncidentCooldownMultipleDays".Translate(incidentLabel, daysRemaining);
            }
            else
            {
                // return $"❌ {incidentLabel} is on cooldown (resets every {globalCooldownDays} days)";
                return "RICS.ICH.RETURN.IncidentCooldownReset".Translate(incidentLabel, globalCooldownDays);
            }
        }

        private static string GetKarmaTypeForIncident(string karmaTypeFromBuyable)
        {
            if (string.IsNullOrEmpty(karmaTypeFromBuyable))
                return "neutral";

            string lower = karmaTypeFromBuyable.ToLowerInvariant();
            return lower switch
            {
                "good" => "good",
                "bad" => "bad",
                "doom" => "doom",
                _ => "neutral"
            };
        }

        private static string GetCooldownMessage(string eventType, CAPGlobalChatSettings settings, GlobalCooldownManager cooldownManager)
        {
            string displayType = eventType.ToLowerInvariant() == "doom" ? "Bad (Doom)" : eventType.ToUpper();

            int maxEvents = eventType.ToLowerInvariant() switch
            {
                "good" => settings.MaxGoodEvents,
                "bad" => settings.MaxBadEvents,
                "doom" => settings.MaxBadEvents,
                "neutral" => settings.MaxNeutralEvents,
                _ => 10
            };

            string lookupKey = (eventType.ToLowerInvariant() == "doom") ? "bad" : eventType.ToLowerInvariant();
            var record = cooldownManager.data.EventUsage.GetValueOrDefault(lookupKey);
            int currentUses = record?.CurrentPeriodUses ?? 0;

            return "RICS.ICH.RETURN.EventTypeLimitReached".Translate(displayType, currentUses, maxEvents);
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
                // Vanilla factory — respects IncidentDef.category, PopulationIntent, DLC rules, etc.
                // This alone already fixes most weird scaling.
                var parms = StorytellerUtility.DefaultParmsNow(incidentDef.category, map);
                parms.forced = true;

                // Friendly events (good/neutral) now use population-based scaling instead of wealth.
                // Visitors / traders / wanderers now spawn exactly like the storyteller (usually 2–8 pawns).
                // Bad/doom events keep full threat scaling (raids stay dangerous).
                //string karmaLower = incident.KarmaType?.ToLowerInvariant() ?? "neutral";
                //if (karmaLower == "good" || karmaLower == "neutral")
                //{
                //    // 85f is tuned from vanilla visitor/trader values (AdjustedPopulation * ~80–100 works perfectly).
                //    // You can tweak to 75f or 100f in settings later if you want.
                //    parms.points = StorytellerUtilityPopulation.AdjustedPopulation * 10f;
                //}
                // pawnCount deliberately left untouched — workers calculate it themselves from points

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

        // Add this debug action to IncidentCommandHandler.cs
        // Update the debug action
        [DebugAction("CAP", "Test Incident Cooldowns", allowedGameStates = AllowedGameStates.Playing)]
        public static void DebugTestIncidentCooldowns()
        {
            var cooldownManager = Current.Game.GetComponent<GlobalCooldownManager>();
            if (cooldownManager == null)
            {
                Logger.Message("No GlobalCooldownManager found!");
                return;
            }

            Logger.Message($"=== INCIDENT COOLDOWN REPORT ===");
            Logger.Message($"Current game day: {GenDate.DaysPassed}");

            if (cooldownManager.data.IncidentUsage == null || cooldownManager.data.IncidentUsage.Count == 0)
            {
                Logger.Message("No incident cooldown data recorded.");
            }
            else
            {
                foreach (var kvp in cooldownManager.data.IncidentUsage)
                {
                    Logger.Message($"Incident: {kvp.Key}");
                    Logger.Message($"  Total uses: {kvp.Value.CurrentPeriodUses}");

                    foreach (int usageDay in kvp.Value.UsageDays)
                    {
                        int daysAgo = GenDate.DaysPassed - usageDay;
                        Logger.Message($"    Used {daysAgo} days ago");
                    }
                }
            }

            // Test a specific incident
            var testIncident = GetAvailableIncidents().Values.FirstOrDefault();
            if (testIncident != null)
            {
                Logger.Message("");
                Logger.Message($"=== TESTING: {testIncident.DefName} ===");
                Logger.Message($"CooldownDays: {testIncident.CooldownDays}");

                var settings = CAPChatInteractiveMod.Instance.Settings.GlobalSettings;
                bool canUse = cooldownManager.CanUseIncident(
                    testIncident.DefName,
                    testIncident.CooldownDays,
                    settings);

                Logger.Message($"Can this incident be used now? {canUse}");

                if (!canUse)
                {
                    int daysRemaining = GetRemainingCooldownDays(
                        testIncident.DefName,
                        testIncident.CooldownDays,
                        cooldownManager);

                    Logger.Message($"Days remaining on cooldown: {daysRemaining}");
                }
            }
        }
    }

}