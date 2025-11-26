// GlobalCooldownManager.cs
// Copyright (c) Captolamia. All rights reserved.
// Licensed under the AGPLv3 License. See LICENSE file in the project root for full license information.
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

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
                CleanupOldRecords();
        }

        public bool CanUseEvent(string eventType, CAPGlobalChatSettings settings)
        {
            // 0 = infinite
            Logger.Debug($"CanUseEvent eventType: {eventType}");
            Logger.Debug($"Max good events: {settings.MaxGoodEvents}");
            Logger.Debug($"Max Bad Events: {settings.MaxBadEvents}");
            Logger.Debug($"Max Neutral Events: {settings.MaxNeutralEvents}");

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
                "doom" => 1, // Special case
                _ => 10
            };

            return record.CurrentPeriodUses < maxUses;
        }

        public bool CanUseCommand(string commandName, CommandSettings settings, CAPGlobalChatSettings globalSettings)
        {
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
            if (currentDay == lastCleanupDay) return;

            var globalSettings = CAPChatInteractiveMod.Instance.Settings.GlobalSettings as CAPGlobalChatSettings;

            foreach (var record in data.EventUsage.Values)
                CleanupOldEvents(record, globalSettings.EventCooldownDays);

            foreach (var record in data.CommandUsage.Values)
                CleanupOldCommandUses(record, globalSettings.EventCooldownDays);

            lastCleanupDay = currentDay;
        }

        private void CleanupOldEvents(EventUsageRecord record, int cooldownDays)
        {
            if (cooldownDays == 0) return; // Never expire
            record.UsageDays.RemoveAll(day => (CurrentGameDay - day) > cooldownDays);
        }

        private void CleanupOldCommandUses(CommandUsageRecord record, int cooldownDays)
        {
            if (cooldownDays == 0) return;
            record.UsageDays.RemoveAll(day => (CurrentGameDay - day) > cooldownDays);
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
            record.PurchaseDays.RemoveAll(day => (GenDate.DaysPassed - day) > cooldownDays);
        }
    }
}