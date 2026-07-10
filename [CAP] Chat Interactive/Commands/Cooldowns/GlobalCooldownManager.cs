// GlobalCooldownManager.cs
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
// Manages global cooldowns for chat events and commands in RimWorld.
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.UIElements;
using Verse;

namespace CAP_ChatInteractive.Commands.Cooldowns
{
    public class GlobalCooldownManager : GameComponent
    {
        public GlobalCooldownData data = new GlobalCooldownData();
        private int lastCleanupDay = 0;

        // REQUIRED: GameComponent constructor
        public GlobalCooldownManager(Game game)
        {
            // Ensure data and its dictionaries are properly initialized
            if (data == null)
            {
                data = new GlobalCooldownData();
                Logger.Debug("GlobalCooldownData initialized in constructor");
            }

            // Double-check all dictionaries exist
            if (data.BuyUsage == null)
            {
                data.BuyUsage = new Dictionary<string, BuyUsageRecord>();
                Logger.Debug("BuyUsage initialized in constructor");
            }

            if (data.EventUsage == null)
            {
                data.EventUsage = new Dictionary<string, EventUsageRecord>();
                Logger.Debug("EventUsage initialized in constructor");
            }

            if (data.CommandUsage == null)
            {
                data.CommandUsage = new Dictionary<string, CommandUsageRecord>();
                Logger.Debug("CommandUsage initialized in constructor");
            }

            if (data.IncidentUsage == null) // NEW
            {
                data.IncidentUsage = new Dictionary<string, IncidentUsageRecord>();
                Logger.Debug("IncidentUsage initialized in constructor");
            }
        }

        public override void GameComponentTick()
        {
            base.GameComponentTick();

            // Run full cleanup once per in-game day
            // 60000 ticks = 1 RimWorld day (24 in-game hours)
            if (Find.TickManager.TicksGame % 60000 == 0)
            {
                CleanupOldRecords();
            }
        }

        public override void ExposeData()
        {
            Scribe_Deep.Look(ref data, "globalCooldownData");
            Scribe_Values.Look(ref lastCleanupDay, "lastCleanupDay");

            // BACKWARD COMPATIBILITY: Initialize missing data structures
            if (data == null)
            {
                data = new GlobalCooldownData();
                Logger.Debug("GlobalCooldownData initialized in ExposeData (was null)");
            }

            // Ensure all dictionaries exist (for saves from older versions)
            if (data.BuyUsage == null)
            {
                data.BuyUsage = new Dictionary<string, BuyUsageRecord>();
                Logger.Debug("BuyUsage dictionary initialized for backward compatibility");
            }

            if (data.EventUsage == null)
            {
                data.EventUsage = new Dictionary<string, EventUsageRecord>();
                Logger.Debug("EventUsage dictionary initialized for backward compatibility");
            }

            if (data.CommandUsage == null)
            {
                data.CommandUsage = new Dictionary<string, CommandUsageRecord>();
                Logger.Debug("CommandUsage dictionary initialized for backward compatibility");
            }

            if (data.IncidentUsage == null) // NEW
            {
                data.IncidentUsage = new Dictionary<string, IncidentUsageRecord>();
                Logger.Debug("IncidentUsage dictionary initialized for backward compatibility");
            }

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
                CleanupOldRecords();
        }

        /// <summary>
        /// Normalize buyable karma strings for counters. doom shares the bad bucket (MaxBadEvents).
        /// </summary>
        public static string NormalizeEventType(string eventType)
        {
            if (string.IsNullOrEmpty(eventType))
                return "neutral";

            string lower = eventType.Trim().ToLowerInvariant();
            return lower switch
            {
                "good" => "good",
                "bad" => "bad",
                "doom" => "bad",
                "neutral" => "neutral",
                _ => "neutral"
            };
        }

        public bool CanUseEvent(string eventType, CAPGlobalChatSettings settings)
        {
            if (settings == null)
            {
                Logger.Error("CanUseEvent: settings is null");
                return false;
            }

            if (string.IsNullOrEmpty(eventType))
            {
                Logger.Error("CanUseEvent called with null/empty eventType");
                return false;
            }

            string original = eventType;
            eventType = NormalizeEventType(eventType);

            Logger.Debug(
                $"CanUseEvent: original='{original}' → bucket='{eventType}' " +
                $"(max G/B/N = {settings.MaxGoodEvents}/{settings.MaxBadEvents}/{settings.MaxNeutralEvents})");

            CleanupOldRecords();

            // 0 = infinite for that type
            if (eventType == "good" && settings.MaxGoodEvents == 0) return true;
            if (eventType == "bad" && settings.MaxBadEvents == 0) return true;
            if (eventType == "neutral" && settings.MaxNeutralEvents == 0) return true;

            var record = GetOrCreateEventRecord(eventType);
            CleanupOldEvents(record, settings.EventCooldownDays);

            int maxUses = eventType switch
            {
                "good" => settings.MaxGoodEvents,
                "bad" => settings.MaxBadEvents,
                "neutral" => settings.MaxNeutralEvents,
                _ => settings.MaxBadEvents
            };

            bool canUse = record.CurrentPeriodUses < maxUses;

            if (!canUse)
                Logger.Debug($"[LIMIT REACHED] {eventType} events at {record.CurrentPeriodUses}/{maxUses} — blocking");
            else
                Logger.Debug($"CanUseEvent OK for {eventType}: {record.CurrentPeriodUses}/{maxUses}");

            return canUse;
        }

        public bool CanUseCommand(string commandName, CommandSettings settings, CAPGlobalChatSettings globalSettings)
        {
            CleanupOldRecords();

            if (settings == null || globalSettings == null)
            {
                Logger.Error("CanUseCommand: null settings");
                return false;
            }

            // Per-command use limit when enabled and MaxUses > 0.
            // MaxUsesPerCooldownPeriod == 0 means unlimited for THIS command only —
            // do NOT skip global / karma-type event limits (that was the 8/3 doom bug).
            if (settings.useCommandCooldown && settings.MaxUsesPerCooldownPeriod > 0)
            {
                var cmdRecord = GetOrCreateCommandRecord(commandName);
                CleanupOldCommandUses(cmdRecord, globalSettings.EventCooldownDays);

                if (cmdRecord.CurrentPeriodUses >= settings.MaxUsesPerCooldownPeriod)
                {
                    Logger.Debug(
                        $"CanUseCommand: {commandName} at " +
                        $"{cmdRecord.CurrentPeriodUses}/{settings.MaxUsesPerCooldownPeriod} — blocked");
                    return false;
                }
            }

            if (!globalSettings.EventCooldownsEnabled)
                return true;

            // 1. Global total event cap
            if (!CanUseGlobalEvents(globalSettings))
            {
                Logger.Debug($"CanUseCommand: {commandName} blocked by global event total");
                return false;
            }

            // 2. Karma bucket for fixed commands (raid/militaryaid/weather).
            // Generic "!event" must also pass BuyableIncident.KarmaType via CanUseEvent
            // in IncidentCommandHandler — GetEventTypeForCommand("event") is only "neutral".
            if (globalSettings.KarmaTypeLimitsEnabled)
            {
                string eventType = GetEventTypeForCommand(commandName);
                if (!CanUseEvent(eventType, globalSettings))
                {
                    Logger.Debug($"CanUseCommand: {commandName} blocked by karma bucket '{eventType}'");
                    return false;
                }
            }

            return true;
        }

        public bool CanUseGlobalEvents(CAPGlobalChatSettings settings)
        {
            if (settings == null) return false;
            if (settings.EventsperCooldown == 0) return true; // Unlimited

            CleanupOldRecords();
            int totalEvents = data.EventUsage.Values.Sum(record => record.CurrentPeriodUses);
            bool ok = totalEvents < settings.EventsperCooldown;
            if (!ok)
                Logger.Debug($"[LIMIT REACHED] total events {totalEvents}/{settings.EventsperCooldown}");
            return ok;
        }
        // In GlobalCooldownManager.cs, inside GlobalCooldownManager.RecordEventUse()

        public void RecordEventUse(string eventType)
        {
            if (string.IsNullOrEmpty(eventType)) return;

            string original = eventType;
            eventType = NormalizeEventType(eventType);

            var record = GetOrCreateEventRecord(eventType);
            record.UsageDays.Add(CurrentGameDay);

            Logger.Debug(
                $"Recorded event use: original='{original}' → bucket='{eventType}' " +
                $"now {record.CurrentPeriodUses}");

            var settings = CAPChatInteractiveMod.Instance?.Settings?.GlobalSettings as CAPGlobalChatSettings;
            if (settings == null) return;

            // Karma-type usage feedback
            if (settings.KarmaTypeLimitsEnabled)
            {
                int maxUses = eventType switch
                {
                    "good" => settings.MaxGoodEvents,
                    "bad" => settings.MaxBadEvents,
                    "neutral" => settings.MaxNeutralEvents,
                    _ => settings.MaxBadEvents
                };

                string displayType = eventType == "bad" ? "Bad/Doom" : char.ToUpperInvariant(eventType[0]) + eventType.Substring(1);
                Messages.Message(
                    $"Current {displayType} events this period: {record.CurrentPeriodUses}/{maxUses}",
                    eventType == "good" ? MessageTypeDefOf.PositiveEvent
                        : eventType == "bad" ? MessageTypeDefOf.NegativeEvent
                        : MessageTypeDefOf.NeutralEvent);
            }

            // Global total feedback
            if (settings.EventCooldownsEnabled)
            {
                int totalEvents = data.EventUsage.Values.Sum(r => r.CurrentPeriodUses);
                int globalMax = settings.EventsperCooldown;
                string globalMsg = globalMax > 0
                    ? $"Current total events this period: {totalEvents}/{globalMax}"
                    : $"Current total events this period: {totalEvents} (unlimited)";
                Messages.Message(globalMsg, MessageTypeDefOf.NeutralEvent);
            }
        }

        public void RecordCommandUse(string commandName)
        {
            var record = GetOrCreateCommandRecord(commandName);
            record.UsageDays.Add(CurrentGameDay);
        }

        /// <summary>
        /// Returns the number of times this command has been successfully used in the current cooldown window.
        /// Cleans up old entries first.
        /// </summary>
        public int GetCurrentCommandUses(string commandName)
        {
            CleanupOldRecords();
            var globalSettings = CAPChatInteractiveMod.Instance?.Settings?.GlobalSettings;
            if (globalSettings == null) return 0;

            var record = GetOrCreateCommandRecord(commandName);
            CleanupOldCommandUses(record, globalSettings.EventCooldownDays);
            return record.CurrentPeriodUses;
        }

        private void CleanupOldRecords()
        {
            int currentDay = CurrentGameDay;
            if (currentDay == lastCleanupDay) return;  // Already did today

            var globalSettings = CAPChatInteractiveMod.Instance?.Settings?.GlobalSettings as CAPGlobalChatSettings;
            if (globalSettings == null) return;

            // Clean everything
            foreach (var record in data.EventUsage.Values)
                CleanupOldEvents(record, globalSettings.EventCooldownDays);

            foreach (var record in data.CommandUsage.Values)
                CleanupOldCommandUses(record, globalSettings.EventCooldownDays);


            foreach (var record in data.BuyUsage.Values)
                CleanupOldPurchases(record, globalSettings.EventCooldownDays);

            lastCleanupDay = currentDay;

            // Optional: log once per real cleanup for debugging
            Logger.Debug($"Global cooldown cleanup performed on day {currentDay}");
        }

        private void CleanupOldEvents(EventUsageRecord record, int cooldownDays)
        {
            if (cooldownDays == 0) return; // Never expire
            // Logger.Debug($"Cleaning up for cooldown {cooldownDays}. Current day: {CurrentGameDay}. Before cleanup: {record.UsageDays.Count} uses.");
            record.UsageDays.RemoveAll(day => (CurrentGameDay - day) >= cooldownDays);  // Changed > to >=
            // Logger.Debug($"After cleanup: {record.UsageDays.Count} uses remaining.");
        }

        private void CleanupOldCommandUses(CommandUsageRecord record, int cooldownDays)
        {
            if (cooldownDays == 0) return;
            Logger.Debug($"Cleaning up for cooldown {cooldownDays}. Current day: {CurrentGameDay}. Before cleanup: {record.UsageDays.Count} uses.");
            record.UsageDays.RemoveAll(day => (CurrentGameDay - day) >= cooldownDays);  // Changed > to >=
            Logger.Debug($"After cleanup: {record.UsageDays.Count} uses remaining.");
        }

        /// <summary>
        /// Checks whether a specific incident can be triggered, supporting the new
        /// "X uses every N days" system (UsesPerCooldownPeriod from BuyableIncident).
        /// Fully backward compatible when usesPerPeriod = 1.
        /// </summary>
        /// <param name="karmaType">
        /// From BuyableIncident.KarmaType (good/bad/doom/neutral). Required for correct bucket limits.
        /// Do not pass DefName — def-name heuristics mis-classified doom/bad as neutral (8/3 bug).
        /// </param>
        public bool CanUseIncident(
            string incidentDefName,
            int incidentCooldownDays,
            int usesPerPeriod = 1,
            CAPGlobalChatSettings settings = null,
            string karmaType = null)
        {
            if (settings == null)
                settings = CAPChatInteractiveMod.Instance?.Settings?.GlobalSettings;

            Logger.Debug(
                $"CanUseIncident: {incidentDefName}, CD={incidentCooldownDays}, UsesPer={usesPerPeriod}, " +
                $"karma='{karmaType}'");

            CleanupOldRecords();

            if (settings != null && !settings.EventCooldownsEnabled)
            {
                Logger.Debug("Event cooldowns disabled globally → allowing incident");
                return true;
            }

            // Per-incident window (independent of global totals)
            if (incidentCooldownDays > 0)
            {
                if (usesPerPeriod <= 0) usesPerPeriod = 1;

                var record = GetOrCreateIncidentRecord(incidentDefName);
                CleanupOldIncidentUses(record, incidentCooldownDays);

                int usesInWindow = record.UsageDays?.Count(d => (CurrentGameDay - d) < incidentCooldownDays) ?? 0;
                if (usesInWindow >= usesPerPeriod)
                {
                    Logger.Debug(
                        $"Incident {incidentDefName} at {usesInWindow}/{usesPerPeriod} " +
                        $"in last {incidentCooldownDays} days → BLOCKED");
                    return false;
                }
            }

            // Global total
            if (settings != null && !CanUseGlobalEvents(settings))
            {
                Logger.Debug($"Global event total reached → blocking {incidentDefName}");
                return false;
            }

            // Karma-type bucket — use explicit type from BuyableIncident, not def name
            if (settings != null && settings.KarmaTypeLimitsEnabled)
            {
                string bucket = NormalizeEventType(
                    !string.IsNullOrEmpty(karmaType) ? karmaType : GetKarmaTypeForIncident(incidentDefName));

                if (!CanUseEvent(bucket, settings))
                {
                    Logger.Debug($"Karma bucket '{bucket}' limit reached → blocking {incidentDefName}");
                    return false;
                }
            }

            Logger.Debug($"Incident {incidentDefName} allowed");
            return true;
        }

        public void RecordIncidentUse(string incidentDefName, int usesPerPeriod = 1)
        {
            var record = GetOrCreateIncidentRecord(incidentDefName);

            if (record.UsageDays == null)
                record.UsageDays = new List<int>();

            record.UsageDays.Add(CurrentGameDay);
            record.LastUsedDay = CurrentGameDay;

            var settings = CAPChatInteractiveMod.Instance?.Settings?.GlobalSettings;
            if (settings != null)
                CleanupOldIncidentUses(record, settings.EventCooldownDays > 0 ? settings.EventCooldownDays : 30);

            Logger.Debug($"Recorded incident use: {incidentDefName} on day {CurrentGameDay}");
        }

        private IncidentUsageRecord GetOrCreateIncidentRecord(string incidentDefName)
        {
            if (!data.IncidentUsage.ContainsKey(incidentDefName))
            {
                data.IncidentUsage[incidentDefName] = new IncidentUsageRecord
                {
                    IncidentDefName = incidentDefName,
                    LastUsedDay = -1
                };
            }
            return data.IncidentUsage[incidentDefName];
        }

        private string GetKarmaTypeForIncident(string incidentDefNameOrKarma)
        {
            if (string.IsNullOrEmpty(incidentDefNameOrKarma))
                return "neutral";

            string lower = incidentDefNameOrKarma.ToLowerInvariant();

            // Direct karma type from BuyableIncident
            if (lower == "good" || lower == "bad" || lower == "doom" || lower == "neutral")
                return lower;

            // Fallback mapping
            if (lower.Contains("trader") || lower.Contains("caravan") || lower.Contains("refugee") ||
                lower.Contains("wanderer") || lower.Contains("ally") || lower.Contains("visitor"))
                return "neutral";   // or "good" depending on your preference

            if (lower.Contains("insanity") || lower.Contains("toxic") || lower.Contains("volcanic") ||
                lower.Contains("defoliator") || lower.Contains("psychicemanator") || lower.Contains("raid"))
                return "bad";

            return "neutral";   // safer default
        }

        private int CurrentGameDay => GenDate.DaysPassed;

        // Helper methods
        private EventUsageRecord GetOrCreateEventRecord(string eventType)
        {
            if (!data.EventUsage.ContainsKey(eventType))
                data.EventUsage[eventType] = new EventUsageRecord { EventType = eventType };
            return data.EventUsage[eventType];
        }

        private CommandUsageRecord GetOrCreateCommandRecord(string commandName)
        {
            if (!data.CommandUsage.ContainsKey(commandName))
                data.CommandUsage[commandName] = new CommandUsageRecord { CommandName = commandName };
            return data.CommandUsage[commandName];
        }

        public string GetEventTypeForCommand(string commandName)
        {
            // Map commands to event types
            return commandName.ToLower() switch
            {
                "raid" => "bad",
                "militaryaid" => "good",
                "weather" => "neutral",
                _ => "neutral"
            };
        }

        public bool CanPurchaseItem()
        {
            CleanupOldRecords();
            var settings = CAPChatInteractiveMod.Instance?.Settings?.GlobalSettings as CAPGlobalChatSettings;
            if (settings == null)
            {
                Logger.Error("GlobalSettings is null in CanPurchaseItem");
                return true; // Allow purchases as fallback
            }

            if (!settings.EventCooldownsEnabled) return true;

            // Defensive programming for backward compatibility
            if (data == null)
            {
                Logger.Error("GlobalCooldownData is null in CanPurchaseItem");
                return true; // Allow purchases as fallback
            }

            if (data.BuyUsage == null)
            {
                Logger.Error("BuyUsage dictionary is null in CanPurchaseItem");
                data.BuyUsage = new Dictionary<string, BuyUsageRecord>();
                return true; // Allow purchases as fallback
            }

            try
            {
                int totalPurchases = data.BuyUsage.Values.Sum(record => record.CurrentPeriodPurchases);
                return totalPurchases < settings.MaxItemPurchases;
            }
            catch (Exception ex)
            {
                Logger.Error($"Error calculating total purchases: {ex}");
                return true; // Allow purchases as fallback
            }
        }

        public void RecordItemPurchase(string itemType = "general")
        {
            var record = GetOrCreateBuyRecord(itemType);
            record.PurchaseDays.Add(GenDate.DaysPassed);

            // Also cleanup old records
            CleanupOldPurchases(record, CAPChatInteractiveMod.Instance.Settings.GlobalSettings.EventCooldownDays);
        }

        private BuyUsageRecord GetOrCreateBuyRecord(string itemType)
        {
            if (!data.BuyUsage.ContainsKey(itemType))
                data.BuyUsage[itemType] = new BuyUsageRecord { ItemType = itemType };
            return data.BuyUsage[itemType];
        }

        private void CleanupOldPurchases(BuyUsageRecord record, int cooldownDays)
        {
            if (cooldownDays == 0) return;
            record.PurchaseDays.RemoveAll(day => (GenDate.DaysPassed - day) >= cooldownDays);  // Changed > to >= (note: uses GenDate.DaysPassed directly here)
        }

        /// <summary>
        /// Prunes usage days older than the cooldown window for incidents.
        /// </summary>
        private void CleanupOldIncidentUses(IncidentUsageRecord record, int cooldownDays)
        {
            if (cooldownDays <= 0 || record?.UsageDays == null) return;

            record.UsageDays.RemoveAll(day => (CurrentGameDay - day) >= cooldownDays);
        }
    }
}