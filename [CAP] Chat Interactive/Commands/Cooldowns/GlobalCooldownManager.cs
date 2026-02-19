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

        public bool CanUseEvent(string eventType, CAPGlobalChatSettings settings)
        {
            // Defensive programming: ensure eventType is valid and consistent
            eventType = eventType.ToLower();  

            // 0 = infinite
            Logger.Debug($"CanUseEvent eventType: {eventType}");
            Logger.Debug($"Max good events: {settings.MaxGoodEvents}");
            Logger.Debug($"Max Bad Events: {settings.MaxBadEvents}");
            Logger.Debug($"Max Neutral Events: {settings.MaxNeutralEvents}");

            CleanupOldRecords();

            if (settings.MaxGoodEvents == 0 && eventType == "good") return true;
            if (settings.MaxBadEvents == 0 && eventType == "bad") return true;
            if (settings.MaxNeutralEvents == 0 && eventType == "neutral") return true;

            var record = GetOrCreateEventRecord(eventType);
            CleanupOldEvents(record, settings.EventCooldownDays);

            int maxUses = eventType switch
            {
                "good" => settings.MaxGoodEvents,
                "bad" => settings.MaxBadEvents,
                "neutral" => settings.MaxNeutralEvents,
                "doom" => settings.MaxBadEvents, // Special case
                _ => 10
            };
            string message = $"Current {eventType} events in cooldown: {record.CurrentPeriodUses}/{maxUses}";
            Messages.Message(message, MessageTypeDefOf.CautionInput);
            return record.CurrentPeriodUses < maxUses;
        }

        public bool CanUseCommand(string commandName, CommandSettings settings, CAPGlobalChatSettings globalSettings)
        {
            CleanupOldRecords();
            // Always check per-command game days cooldown first (if enabled)
            if (settings.useCommandCooldown && settings.MaxUsesPerCooldownPeriod > 0)
            {
                var cmdRecord = GetOrCreateCommandRecord(commandName);
                CleanupOldCommandUses(cmdRecord, globalSettings.EventCooldownDays);

                if (cmdRecord.CurrentPeriodUses >= settings.MaxUsesPerCooldownPeriod)
                    return false;
            }

            // If per-command limit is 0 (unlimited), skip further checks for this command
            if (settings.useCommandCooldown && settings.MaxUsesPerCooldownPeriod == 0)
            {
                return true;
            }

            // If event cooldowns are disabled globally, we're done
            if (!globalSettings.EventCooldownsEnabled)
            {
                return true;
            }

            // EVENT COOLDOWN SYSTEM: Only for commands that should count toward event limits
            // 1. Check total global event limit first
            if (!CanUseGlobalEvents(globalSettings))
                return false;

            // 2. Check karma-type specific limits if enabled
            if (globalSettings.KarmaTypeLimitsEnabled)
            {
                string eventType = GetEventTypeForCommand(commandName);
                if (!CanUseEvent(eventType, globalSettings))
                    return false;
            }

            return true;
        }

        // NEW: Check global event count limit
        public bool CanUseGlobalEvents(CAPGlobalChatSettings settings)
        {
            if (settings.EventsperCooldown == 0) return true; // Unlimited

            int totalEvents = data.EventUsage.Values.Sum(record => record.CurrentPeriodUses);
            return totalEvents < settings.EventsperCooldown;
        }

        public void RecordEventUse(string eventType)
        {
            var record = GetOrCreateEventRecord(eventType);
            record.UsageDays.Add(CurrentGameDay);
        }

        public void RecordCommandUse(string commandName)
        {
            var record = GetOrCreateCommandRecord(commandName);
            record.UsageDays.Add(CurrentGameDay);
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

            foreach (var record in data.IncidentUsage.Values)
                CleanupOldIncidentUses(record, globalSettings.EventCooldownDays);

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

        private void CleanupOldIncidentUses(IncidentUsageRecord record, int cooldownDays)
        {
            if (cooldownDays == 0) return; // Never expire
            Logger.Debug($"Cleaning up for cooldown {cooldownDays}. Current day: {CurrentGameDay}. Before cleanup: {record.UsageDays.Count} uses.");
            // Remove usage days that are older than or equal to the cooldown period  // Changed comment for clarity
            record.UsageDays.RemoveAll(day => (CurrentGameDay - day) >= cooldownDays);  // Changed > to >=
            Logger.Debug($"After cleanup: {record.UsageDays.Count} uses remaining.");
        }

        // Update GlobalCooldownManager.cs - simplify the incident cooldown methods
        public bool CanUseIncident(string incidentDefName, int incidentCooldownDays, CAPGlobalChatSettings settings)
        {
            Logger.Debug($"CanUseIncident: {incidentDefName}, IncidentCooldownDays: {incidentCooldownDays}");
            CleanupOldRecords();
            // If event cooldowns are disabled globally, skip all cooldown checks
            if (!settings.EventCooldownsEnabled)
            {
                Logger.Debug("Event cooldowns disabled globally, allowing incident use");
                return true;
            }

            // If this specific incident has no cooldown (CooldownDays = 0), skip further checks
            if (incidentCooldownDays <= 0)
            {
                Logger.Debug($"Incident {incidentDefName} has no cooldown (CooldownDays = {incidentCooldownDays})");
                return true;
            }

            // Get or create the incident usage record
            var record = GetOrCreateIncidentRecord(incidentDefName);

            // Clean up old records
            CleanupOldIncidentUses(record, incidentCooldownDays);

            // Check if this incident has been used within the cooldown period
            bool incidentUsedRecently = false;
            foreach (int usageDay in record.UsageDays)
            {
                int daysSinceUse = CurrentGameDay - usageDay;
                if (daysSinceUse < incidentCooldownDays)  // Changed <= to < for consistency with new cleanup logic
                {
                    incidentUsedRecently = true;
                    Logger.Debug($"Incident {incidentDefName} was used {daysSinceUse} days ago (cooldown: {incidentCooldownDays} days)");
                    break;
                }
            }

            return !incidentUsedRecently;
        }

        public void RecordIncidentUse(string incidentDefName)
        {
            var record = GetOrCreateIncidentRecord(incidentDefName);
            record.UsageDays.Add(CurrentGameDay);

            Logger.Debug($"Recorded incident use: {incidentDefName} on day {CurrentGameDay}");
        }

        private IncidentUsageRecord GetOrCreateIncidentRecord(string incidentDefName)
        {
            if (!data.IncidentUsage.ContainsKey(incidentDefName))
            {
                data.IncidentUsage[incidentDefName] = new IncidentUsageRecord
                {
                    IncidentDefName = incidentDefName
                };
            }
            return data.IncidentUsage[incidentDefName];
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
    }


}