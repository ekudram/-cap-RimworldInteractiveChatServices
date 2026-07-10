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
using UnityEngine;
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
                if (cooldownManager != null && settings.EventCooldownsEnabled)
                {
                    // Use BuyableIncident.KarmaType for all limit checks (not command name "event" → neutral).
                    string karmaType = GetKarmaTypeForIncident(buyableIncident.KarmaType);
                    string karmaBucket = GlobalCooldownManager.NormalizeEventType(karmaType);

                    Logger.Debug(
                        $"Event limits: cooldownsOn={settings.EventCooldownsEnabled}, " +
                        $"karmaLimitsOn={settings.KarmaTypeLimitsEnabled}, " +
                        $"karma='{buyableIncident.KarmaType}' → bucket='{karmaBucket}', " +
                        $"globalMax={settings.EventsperCooldown}, " +
                        $"maxBad={settings.MaxBadEvents}, maxGood={settings.MaxGoodEvents}, maxNeutral={settings.MaxNeutralEvents}, " +
                        $"incidentCD={buyableIncident.CooldownDays}d usesPer={buyableIncident.UsesPerCooldownPeriod}");

                    // === 1. Global total event cap ===
                    if (!cooldownManager.CanUseGlobalEvents(settings))
                    {
                        int total = cooldownManager.data.EventUsage.Values.Sum(r => r.CurrentPeriodUses);
                        Logger.Debug($"BLOCKED: global events {total}/{settings.EventsperCooldown}");
                        return "RICS.ICH.RETURN.GlobalEventLimitReached".Translate(total, settings.EventsperCooldown);
                    }

                    // === 2. Karma-type bucket (doom counts as bad) ===
                    if (settings.KarmaTypeLimitsEnabled)
                    {
                        if (!cooldownManager.CanUseEvent(karmaBucket, settings))
                        {
                            Logger.Debug($"BLOCKED: karma bucket '{karmaBucket}' for {buyableIncident.DefName}");
                            return GetCooldownMessage(karmaBucket, settings, cooldownManager);
                        }
                    }

                    // === 3. Per-incident cooldown / uses-per-window ===
                    if (!cooldownManager.CanUseIncident(
                            buyableIncident.DefName,
                            buyableIncident.CooldownDays,
                            buyableIncident.UsesPerCooldownPeriod,
                            settings,
                            karmaType: buyableIncident.KarmaType))
                    {
                        // Prefer individual-CD message when that is the gate
                        if (buyableIncident.CooldownDays > 0)
                        {
                            int daysRemaining = GetRemainingCooldownDays(
                                buyableIncident.DefName, buyableIncident.CooldownDays, cooldownManager);
                            string cooldownMessage = GetIndividualCooldownMessage(
                                buyableIncident.Label, daysRemaining, buyableIncident.CooldownDays);
                            Logger.Debug($"BLOCKED: individual incident CD — {cooldownMessage}");
                            return cooldownMessage;
                        }

                        Logger.Debug($"BLOCKED: CanUseIncident for {buyableIncident.DefName}");
                        return "RICS.ICH.RETURN.CommandCooldownActive".Translate(buyableIncident.Label);
                    }
                }
                else if (cooldownManager == null)
                {
                    Logger.Warning("GlobalCooldownManager missing — event limits not enforced this purchase");
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

                    // NEW: unified karma calculation that includes price-based scaling
                    // (exactly symmetric to RaidCommandHandler + MilitaryAidCommandHandler)
                    float karmaChange = CalculateEventKarmaChange(
                        buyableIncident.KarmaType,
                        buyableIncident.BaseCost,
                        settings);

                    string karmaLower = buyableIncident.KarmaType?.ToLowerInvariant() ?? "neutral";
                    if (karmaChange > 0f)
                    {
                        if (karmaLower == "good" || karmaLower == "neutral")
                        {
                            viewer.GiveKarma(karmaChange);
                            Logger.Debug($"Awarded {karmaChange:F2} karma (base + price multiplier) for {karmaLower} event '{buyableIncident.Label}'");
                        }
                        else // bad or doom
                        {
                            viewer.TakeKarma(karmaChange);
                            Logger.Debug($"Deducted {karmaChange:F2} karma (base + price multiplier) for {karmaLower} event '{buyableIncident.Label}'");
                        }
                    }

                    if (cooldownManager != null && settings.EventCooldownsEnabled)
                    {
                        // Always record both: per-incident window + karma/global bucket
                        cooldownManager.RecordIncidentUse(
                            buyableIncident.DefName, buyableIncident.UsesPerCooldownPeriod);

                        string eventType = GetKarmaTypeForIncident(buyableIncident.KarmaType);
                        cooldownManager.RecordEventUse(eventType);

                        string logKey = GlobalCooldownManager.NormalizeEventType(eventType);
                        var record = cooldownManager.data.EventUsage.GetValueOrDefault(logKey);
                        Logger.Debug(
                            $"Recorded success: incident={buyableIncident.DefName}, " +
                            $"karma='{eventType}' → bucket='{logKey}' uses={record?.CurrentPeriodUses ?? 0}");
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

        /// <summary>
        /// Unified karma calculator for ANY buyable incident (good/neutral/bad/doom).
        /// Returns a POSITIVE number:
        ///   • Good/Neutral → pass to GiveKarma()
        ///   • Bad/Doom     → pass to TakeKarma()
        /// Now includes price-based scaling via KarmaEventPriceMultiplier
        /// (default 5f = ±5 karma per 100 coins of BaseCost).
        /// </summary>
        private static float CalculateEventKarmaChange(string karmaType, int baseCost, CAPGlobalChatSettings settings)
        {
            if (settings == null)
            {
                // Safe fallback based on karma type
                return karmaType?.ToLowerInvariant() switch
                {
                    "good" or "neutral" => Mathf.Max(3f, baseCost / 300f),
                    "bad" or "doom" => Mathf.Max(8f, baseCost / 200f),
                    _ => 3f
                };
            }

            string typeLower = karmaType?.ToLowerInvariant() ?? "neutral";

            // Base value from Economy settings
            // Gives negative values for bad/doom events, positive for good/neutral
            float total = typeLower switch
            {
                "good" => settings.KarmaGainPerGoodEvent + (baseCost * settings.KarmaEventPriceMultiplier / 100f),
                "bad" => settings.KarmaLossPerBadEvent - (baseCost * settings.KarmaEventPriceMultiplier / 100f),
                "doom" => settings.KarmaLossPerDoomEvent - (baseCost * settings.KarmaEventPriceMultiplier / 100f),
                _ => settings.KarmaGainPerNeutralEvent + (baseCost * settings.KarmaEventPriceMultiplier / 100f),  // neutral or unknown
            };

            // NEW: price-based karma scaling (same formula used everywhere)
            //float priceBased = baseCost * settings.KarmaEventPriceMultiplier/100f;

            //float total = baseValue + priceBased;

            // Never return zero or negative for a good/neutral event
            // (bad/doom events are always positive loss amounts)
            return typeLower == "good" || typeLower == "neutral"
                ? Mathf.Max(total, 1f)
                : Mathf.Max(total, 1f);
        }

        private static int GetRemainingCooldownDays(string incidentDefName, int incidentCooldownDays, GlobalCooldownManager cooldownManager)
        {
            if (cooldownManager?.data?.IncidentUsage == null ||
                !cooldownManager.data.IncidentUsage.ContainsKey(incidentDefName))
                return 0;

            var record = cooldownManager.data.IncidentUsage[incidentDefName];
            int currentDay = GenDate.DaysPassed;

            if (record.LastUsedDay < 0)
                return 0;

            int daysSinceUse = currentDay - record.LastUsedDay;
            int daysRemaining = incidentCooldownDays - daysSinceUse;

            Logger.Debug($"GetRemainingCooldownDays: {incidentDefName} - Last used day {record.LastUsedDay}, days since: {daysSinceUse}, remaining: {daysRemaining}");

            return Math.Max(0, daysRemaining);
        }

        private static string GetIndividualCooldownMessage(string incidentLabel, int daysRemaining, int incidentCooldownDays)
        {
            if (daysRemaining > 0)
            {
                if (daysRemaining == 1)
                    return "RICS.ICH.RETURN.IncidentCooldownOneDay".Translate(incidentLabel);
                else
                    return "RICS.ICH.RETURN.IncidentCooldownMultipleDays".Translate(incidentLabel, daysRemaining);
            }
            else
            {
                return "RICS.ICH.RETURN.IncidentCooldownReset".Translate(incidentLabel, incidentCooldownDays);
            }
        }

        /// <summary>Normalize BuyableIncident.KarmaType for logging; bucket limits use NormalizeEventType.</summary>
        private static string GetKarmaTypeForIncident(string karmaTypeFromBuyable)
        {
            if (string.IsNullOrEmpty(karmaTypeFromBuyable))
                return "neutral";

            string lower = karmaTypeFromBuyable.Trim().ToLowerInvariant();
            return lower switch
            {
                "good" => "good",
                "bad" => "bad",
                "doom" => "doom",
                "neutral" => "neutral",
                _ => "neutral"
            };
        }

        private static string GetCooldownMessage(string eventType, CAPGlobalChatSettings settings, GlobalCooldownManager cooldownManager)
        {
            string bucket = GlobalCooldownManager.NormalizeEventType(eventType);
            string displayType = bucket == "bad" ? "Bad/Doom" : char.ToUpperInvariant(bucket[0]) + bucket.Substring(1);

            int maxEvents = bucket switch
            {
                "good" => settings.MaxGoodEvents,
                "bad" => settings.MaxBadEvents,
                "neutral" => settings.MaxNeutralEvents,
                _ => 10
            };

            var record = cooldownManager.data.EventUsage.GetValueOrDefault(bucket);
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
    }

}