// IncidentCommandHandler.cs
// Copyright (c) Captolamia
// This file is part of CAP Chat Interactive (RICS).
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
// !event — trigger buyable storyteller incidents from chat
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
                    return "RICS.ICH.RETURN.UnknownIncidentType".Translate(
                        incidentType, string.Join(", ", availableTypes));
                }

                if (!buyableIncident.Enabled)
                    return "RICS.ICH.RETURN.IncidentDisabled".Translate(buyableIncident.Label);

                var cooldownManager = Current.Game?.GetComponent<GlobalCooldownManager>();
                if (cooldownManager != null && settings.EventCooldownsEnabled)
                {
                    string karmaType = GetKarmaTypeForIncident(buyableIncident.KarmaType);
                    string karmaBucket = GlobalCooldownManager.NormalizeEventType(karmaType);

                    // 1. Global total event cap
                    if (!cooldownManager.CanUseGlobalEvents(settings))
                    {
                        int total = cooldownManager.data.EventUsage.Values.Sum(r => r.CurrentPeriodUses);
                        return "RICS.ICH.RETURN.GlobalEventLimitReached".Translate(total, settings.EventsperCooldown);
                    }

                    // 2. Karma-type bucket (doom counts as bad)
                    if (settings.KarmaTypeLimitsEnabled && !cooldownManager.CanUseEvent(karmaBucket, settings))
                        return GetCooldownMessage(karmaBucket, settings, cooldownManager);

                    // 3. Per-incident cooldown / uses-per-window
                    if (!cooldownManager.CanUseIncident(
                            buyableIncident.DefName,
                            buyableIncident.CooldownDays,
                            buyableIncident.UsesPerCooldownPeriod,
                            settings,
                            karmaType: buyableIncident.KarmaType))
                    {
                        if (buyableIncident.CooldownDays > 0)
                        {
                            int daysRemaining = GetRemainingCooldownDays(
                                buyableIncident.DefName, buyableIncident.CooldownDays, cooldownManager);
                            return GetIndividualCooldownMessage(
                                buyableIncident.Label, daysRemaining, buyableIncident.CooldownDays);
                        }

                        return "RICS.ICH.RETURN.CommandCooldownActive".Translate(buyableIncident.Label);
                    }
                }
                else if (cooldownManager == null && settings.EventCooldownsEnabled)
                {
                    Logger.Warning("[Incident] GlobalCooldownManager missing — event limits not enforced");
                }

                int cost = buyableIncident.BaseCost;
                if (viewer.Coins < cost)
                    return "RICS.WCH.InsufficientFunds".Translate(cost, currencySymbol, buyableIncident.Label);

                bool success = TriggerIncident(buyableIncident, messageWrapper.Username, out string resultMessage);
                if (!success)
                    return "RICS.ICH.RETURN.IncidentTriggerFailed".Translate(resultMessage, currencySymbol);

                viewer.TakeCoins(cost);

                float karmaChange = CalculateEventKarmaChange(
                    buyableIncident.KarmaType, buyableIncident.BaseCost, settings);

                string karmaLower = buyableIncident.KarmaType?.ToLowerInvariant() ?? "neutral";
                if (karmaChange > 0f)
                {
                    if (karmaLower == "good" || karmaLower == "neutral")
                        viewer.GiveKarma(karmaChange);
                    else
                        viewer.TakeKarma(karmaChange);
                }

                if (cooldownManager != null && settings.EventCooldownsEnabled)
                {
                    cooldownManager.RecordIncidentUse(
                        buyableIncident.DefName, buyableIncident.UsesPerCooldownPeriod);
                    cooldownManager.RecordEventUse(GetKarmaTypeForIncident(buyableIncident.KarmaType));
                }

                if (karmaLower == "doom")
                {
                    Messages.Message(
                        "RICS.ICH.RETURN.DoomEventPurchased".Translate(buyableIncident.Label),
                        MessageTypeDefOf.ThreatBig);
                }

                return resultMessage;
            }
            catch (Exception ex)
            {
                Logger.Error($"[Incident] Error handling incident command: {ex}");
                return "RICS.ICH.RETURN.ErrorTriggeringIncident".Translate();
            }
        }

        /// <summary>
        /// Positive karma amount: GiveKarma for good/neutral, TakeKarma for bad/doom.
        /// Includes price-based scaling via KarmaEventPriceMultiplier.
        /// </summary>
        private static float CalculateEventKarmaChange(string karmaType, int baseCost, CAPGlobalChatSettings settings)
        {
            if (settings == null)
            {
                return karmaType?.ToLowerInvariant() switch
                {
                    "good" or "neutral" => Mathf.Max(3f, baseCost / 300f),
                    "bad" or "doom" => Mathf.Max(8f, baseCost / 200f),
                    _ => 3f
                };
            }

            string typeLower = karmaType?.ToLowerInvariant() ?? "neutral";
            float total = typeLower switch
            {
                "good" => settings.KarmaGainPerGoodEvent + (baseCost * settings.KarmaEventPriceMultiplier / 100f),
                "bad" => settings.KarmaLossPerBadEvent - (baseCost * settings.KarmaEventPriceMultiplier / 100f),
                "doom" => settings.KarmaLossPerDoomEvent - (baseCost * settings.KarmaEventPriceMultiplier / 100f),
                _ => settings.KarmaGainPerNeutralEvent + (baseCost * settings.KarmaEventPriceMultiplier / 100f),
            };

            return Mathf.Max(total, 1f);
        }

        private static int GetRemainingCooldownDays(string incidentDefName, int incidentCooldownDays, GlobalCooldownManager cooldownManager)
        {
            if (cooldownManager?.data?.IncidentUsage == null ||
                !cooldownManager.data.IncidentUsage.ContainsKey(incidentDefName))
                return 0;

            var record = cooldownManager.data.IncidentUsage[incidentDefName];
            if (record.LastUsedDay < 0)
                return 0;

            int daysSinceUse = GenDate.DaysPassed - record.LastUsedDay;
            return Math.Max(0, incidentCooldownDays - daysSinceUse);
        }

        private static string GetIndividualCooldownMessage(string incidentLabel, int daysRemaining, int incidentCooldownDays)
        {
            if (daysRemaining > 0)
            {
                if (daysRemaining == 1)
                    return "RICS.ICH.RETURN.IncidentCooldownOneDay".Translate(incidentLabel);
                return "RICS.ICH.RETURN.IncidentCooldownMultipleDays".Translate(incidentLabel, daysRemaining);
            }

            return "RICS.ICH.RETURN.IncidentCooldownReset".Translate(incidentLabel, incidentCooldownDays);
        }

        private static string GetKarmaTypeForIncident(string karmaTypeFromBuyable)
        {
            if (string.IsNullOrEmpty(karmaTypeFromBuyable))
                return "neutral";

            return karmaTypeFromBuyable.Trim().ToLowerInvariant() switch
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
            string displayType = bucket == "bad"
                ? "Bad/Doom"
                : char.ToUpperInvariant(bucket[0]) + bucket.Substring(1);

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

            string inputLower = input.ToLowerInvariant();
            var allIncidents = GetAvailableIncidents();

            if (allIncidents.TryGetValue(inputLower, out var incident))
                return incident;

            var defNameMatch = allIncidents.Values.FirstOrDefault(i =>
                i.DefName.Equals(input, StringComparison.OrdinalIgnoreCase));
            if (defNameMatch != null)
                return defNameMatch;

            var labelMatch = allIncidents.Values.FirstOrDefault(i =>
                i.Label.Equals(input, StringComparison.OrdinalIgnoreCase));
            if (labelMatch != null)
                return labelMatch;

            return allIncidents.Values.FirstOrDefault(i =>
                i.DefName.IndexOf(input, StringComparison.OrdinalIgnoreCase) >= 0 ||
                i.Label.IndexOf(input, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        public static Dictionary<string, BuyableIncident> GetAvailableIncidents()
        {
            return IncidentsManager.AllBuyableIncidents
                .Where(kvp => IsIncidentSuitableForCommand(kvp.Value))
                .ToDictionary(kvp => kvp.Key.ToLowerInvariant(), kvp => kvp.Value);
        }

        private static bool IsIncidentSuitableForCommand(BuyableIncident incident)
        {
            return incident != null && incident.Enabled && incident.IsAvailableForCommands;
        }

        private static bool TriggerIncident(BuyableIncident incident, string username, out string resultMessage)
        {
            resultMessage = "";
            var incidentDef = DefDatabase<IncidentDef>.GetNamedSilentFail(incident.DefName);

            if (incidentDef == null)
            {
                resultMessage = "RICS.ICH.RETURN.IncidentDefMissing".Translate(incident.Label);
                return false;
            }

            var worker = incidentDef.Worker;
            if (worker == null)
            {
                resultMessage = "RICS.ICH.RETURN.IncidentNoWorker".Translate(incident.Label);
                return false;
            }

            var playerMaps = Current.Game?.Maps?.Where(map => map.IsPlayerHome).ToList();
            if (playerMaps == null || playerMaps.Count == 0)
            {
                resultMessage = "RICS.ICH.RETURN.IncidentNoMap".Translate(incident.Label);
                return false;
            }

            playerMaps.Shuffle();

            foreach (var map in playerMaps)
            {
                // Vanilla factory — category, population intent, DLC rules
                var parms = StorytellerUtility.DefaultParmsNow(incidentDef.category, map);
                parms.forced = true;

                if (worker.CanFireNow(parms) && !worker.FiredTooRecently(map))
                {
                    if (worker.TryExecute(parms))
                    {
                        resultMessage = "RICS.ICH.RETURN.IncidentSuccess".Translate(incident.Label);
                        return true;
                    }
                }
            }

            resultMessage = "RICS.ICH.RETURN.IncidentCannotFire".Translate(incident.Label);
            return false;
        }

        /// <summary>Dev-only: list which buyable incidents pass the !event filter.</summary>
        [DebugAction("CAP", "List Filtered Incidents", allowedGameStates = AllowedGameStates.Playing)]
        public static void DebugListFilteredIncidents()
        {
            var allIncidents = IncidentsManager.AllBuyableIncidents;
            var availableIncidents = GetAvailableIncidents();

            Logger.Message($"[Incident filter] total={allIncidents.Count} available={availableIncidents.Count} filtered={allIncidents.Count - availableIncidents.Count}");

            var rimworldIncidents = allIncidents.Values
                .Where(i => i.ModSource == "RimWorld" || i.ModSource == "Core").ToList();
            var dlcIncidents = allIncidents.Values.Where(i =>
                i.ModSource != null && (
                    i.ModSource.Contains("Royalty") ||
                    i.ModSource.Contains("Ideology") ||
                    i.ModSource.Contains("Biotech") ||
                    i.ModSource.Contains("Anomaly") ||
                    i.ModSource.Contains("Odyssey"))).ToList();
            var modIncidents = allIncidents.Values
                .Where(i => !rimworldIncidents.Contains(i) && !dlcIncidents.Contains(i)).ToList();

            void LogGroup(string title, List<BuyableIncident> list)
            {
                Logger.Message($"=== {title} ({list.Count}) ===");
                foreach (var incident in list.OrderBy(i => i.ModSource).ThenBy(i => i.DefName))
                {
                    string status = IsIncidentSuitableForCommand(incident) ? "AVAILABLE" : "FILTERED";
                    Logger.Message($"{status}: {incident.DefName} - {incident.Label} ({incident.ModSource})");
                }
            }

            LogGroup("RimWorld", rimworldIncidents);
            LogGroup("DLC", dlcIncidents);
            LogGroup("Mods", modIncidents);

            Logger.Message(
                $"[Incident filter summary] RW {rimworldIncidents.Count(IsIncidentSuitableForCommand)}/{rimworldIncidents.Count} | " +
                $"DLC {dlcIncidents.Count(IsIncidentSuitableForCommand)}/{dlcIncidents.Count} | " +
                $"Mods {modIncidents.Count(IsIncidentSuitableForCommand)}/{modIncidents.Count}");
        }
    }
}
