// GlobalCooldownManager.cs
// Copyright (c) Captolamia
// This file is part of CAP Chat Interactive (RICS).
// Licensed under the GNU Affero General Public License v3.0 or later.
// See LICENSE.txt in the project root for full license text.
//
// Global + per-command / incident / buy cooldowns and period use counters.
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
        private int lastCleanupDay;

        public GlobalCooldownManager(Game game)
        {
            EnsureData();
        }

        public override void GameComponentTick()
        {
            // Once per RimWorld day (60000 ticks).
            if (Find.TickManager.TicksGame % 60000 == 0)
                CleanupOldRecords();
        }

        public override void ExposeData()
        {
            Scribe_Deep.Look(ref data, "globalCooldownData");
            Scribe_Values.Look(ref lastCleanupDay, "lastCleanupDay");

            EnsureData();

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
                CleanupOldRecords();
        }

        private void EnsureData()
        {
            data ??= new GlobalCooldownData();
            data.EventUsage ??= new Dictionary<string, EventUsageRecord>();
            data.CommandUsage ??= new Dictionary<string, CommandUsageRecord>();
            data.BuyUsage ??= new Dictionary<string, BuyUsageRecord>();
            data.IncidentUsage ??= new Dictionary<string, IncidentUsageRecord>();
        }

        /// <summary>
        /// Normalize buyable karma strings for counters. doom shares the bad bucket (MaxBadEvents).
        /// </summary>
        public static string NormalizeEventType(string eventType)
        {
            if (string.IsNullOrEmpty(eventType))
                return "neutral";

            return eventType.Trim().ToLowerInvariant() switch
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
            if (settings == null || string.IsNullOrEmpty(eventType))
                return false;

            eventType = NormalizeEventType(eventType);
            CleanupOldRecords();

            // 0 = unlimited for that karma type
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

            return record.CurrentPeriodUses < maxUses;
        }

        public bool CanUseCommand(string commandName, CommandSettings settings, CAPGlobalChatSettings globalSettings)
        {
            CleanupOldRecords();

            if (settings == null || globalSettings == null)
                return false;

            // Per-command use limit when enabled and MaxUses > 0.
            // MaxUsesPerCooldownPeriod == 0 means unlimited for THIS command only —
            // do NOT skip global / karma-type event limits.
            if (settings.useCommandCooldown && settings.MaxUsesPerCooldownPeriod > 0)
            {
                var cmdRecord = GetOrCreateCommandRecord(commandName);
                CleanupOldCommandUses(cmdRecord, globalSettings.EventCooldownDays);

                if (cmdRecord.CurrentPeriodUses >= settings.MaxUsesPerCooldownPeriod)
                    return false;
            }

            if (!globalSettings.EventCooldownsEnabled)
                return true;

            if (!CanUseGlobalEvents(globalSettings))
                return false;

            // Karma bucket for fixed commands (raid / militaryaid / weather).
            // Generic "!event" must also pass BuyableIncident.KarmaType via CanUseEvent
            // in IncidentCommandHandler — GetEventTypeForCommand("event") is only "neutral".
            if (globalSettings.KarmaTypeLimitsEnabled)
            {
                string eventType = GetEventTypeForCommand(commandName);
                if (!CanUseEvent(eventType, globalSettings))
                    return false;
            }

            return true;
        }

        public bool CanUseGlobalEvents(CAPGlobalChatSettings settings)
        {
            if (settings == null)
                return false;
            if (settings.EventsperCooldown == 0)
                return true;

            EnsureData();
            CleanupOldRecords();
            int totalEvents = data.EventUsage.Values.Sum(record => record.CurrentPeriodUses);
            return totalEvents < settings.EventsperCooldown;
        }

        public void RecordEventUse(string eventType)
        {
            if (string.IsNullOrEmpty(eventType))
                return;

            eventType = NormalizeEventType(eventType);
            EnsureData();

            var record = GetOrCreateEventRecord(eventType);
            record.UsageDays ??= new List<int>();
            record.UsageDays.Add(CurrentGameDay);

            var settings = CAPChatInteractiveMod.Instance?.Settings?.GlobalSettings as CAPGlobalChatSettings;
            if (settings == null)
                return;

            if (settings.KarmaTypeLimitsEnabled)
            {
                int maxUses = eventType switch
                {
                    "good" => settings.MaxGoodEvents,
                    "bad" => settings.MaxBadEvents,
                    "neutral" => settings.MaxNeutralEvents,
                    _ => settings.MaxBadEvents
                };

                string displayType = eventType == "bad"
                    ? "Bad/Doom"
                    : char.ToUpperInvariant(eventType[0]) + eventType.Substring(1);

                Messages.Message(
                    $"Current {displayType} events this period: {record.CurrentPeriodUses}/{maxUses}",
                    eventType == "good" ? MessageTypeDefOf.PositiveEvent
                        : eventType == "bad" ? MessageTypeDefOf.NegativeEvent
                        : MessageTypeDefOf.NeutralEvent);
            }

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
            if (string.IsNullOrEmpty(commandName))
                return;

            var record = GetOrCreateCommandRecord(commandName);
            record.UsageDays ??= new List<int>();
            record.UsageDays.Add(CurrentGameDay);
        }

        /// <summary>
        /// Successful uses of this command in the current cooldown window (after pruning).
        /// </summary>
        public int GetCurrentCommandUses(string commandName)
        {
            CleanupOldRecords();
            var globalSettings = CAPChatInteractiveMod.Instance?.Settings?.GlobalSettings;
            if (globalSettings == null)
                return 0;

            var record = GetOrCreateCommandRecord(commandName);
            CleanupOldCommandUses(record, globalSettings.EventCooldownDays);
            return record.CurrentPeriodUses;
        }

        private void CleanupOldRecords()
        {
            int currentDay = CurrentGameDay;
            if (currentDay == lastCleanupDay)
                return;

            var globalSettings = CAPChatInteractiveMod.Instance?.Settings?.GlobalSettings as CAPGlobalChatSettings;
            if (globalSettings == null)
                return;

            EnsureData();

            foreach (var record in data.EventUsage.Values)
                CleanupOldEvents(record, globalSettings.EventCooldownDays);

            foreach (var record in data.CommandUsage.Values)
                CleanupOldCommandUses(record, globalSettings.EventCooldownDays);

            foreach (var record in data.BuyUsage.Values)
                CleanupOldPurchases(record, globalSettings.EventCooldownDays);

            lastCleanupDay = currentDay;
        }

        private void CleanupOldEvents(EventUsageRecord record, int cooldownDays)
        {
            if (cooldownDays == 0 || record?.UsageDays == null)
                return;
            record.UsageDays.RemoveAll(day => (CurrentGameDay - day) >= cooldownDays);
        }

        private void CleanupOldCommandUses(CommandUsageRecord record, int cooldownDays)
        {
            if (cooldownDays == 0 || record?.UsageDays == null)
                return;
            record.UsageDays.RemoveAll(day => (CurrentGameDay - day) >= cooldownDays);
        }

        /// <summary>
        /// Whether a specific incident can fire under per-incident "X uses / N days",
        /// global totals, and karma buckets. usesPerPeriod defaults to 1 for older data.
        /// </summary>
        /// <param name="karmaType">
        /// From BuyableIncident.KarmaType (good/bad/doom/neutral). Required for correct buckets.
        /// Prefer not relying on def-name heuristics alone.
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

            CleanupOldRecords();

            if (settings != null && !settings.EventCooldownsEnabled)
                return true;

            if (incidentCooldownDays > 0)
            {
                if (usesPerPeriod <= 0)
                    usesPerPeriod = 1;

                var record = GetOrCreateIncidentRecord(incidentDefName);
                CleanupOldIncidentUses(record, incidentCooldownDays);

                int usesInWindow = record.UsageDays?.Count(d => (CurrentGameDay - d) < incidentCooldownDays) ?? 0;
                if (usesInWindow >= usesPerPeriod)
                    return false;
            }

            if (settings != null && !CanUseGlobalEvents(settings))
                return false;

            if (settings != null && settings.KarmaTypeLimitsEnabled)
            {
                string bucket = NormalizeEventType(
                    !string.IsNullOrEmpty(karmaType) ? karmaType : GetKarmaTypeForIncident(incidentDefName));

                if (!CanUseEvent(bucket, settings))
                    return false;
            }

            return true;
        }

        public void RecordIncidentUse(string incidentDefName, int usesPerPeriod = 1)
        {
            if (string.IsNullOrEmpty(incidentDefName))
                return;

            var record = GetOrCreateIncidentRecord(incidentDefName);
            record.UsageDays ??= new List<int>();
            record.UsageDays.Add(CurrentGameDay);
            record.LastUsedDay = CurrentGameDay;

            var settings = CAPChatInteractiveMod.Instance?.Settings?.GlobalSettings;
            int window = settings != null && settings.EventCooldownDays > 0
                ? settings.EventCooldownDays
                : 30;
            CleanupOldIncidentUses(record, window);
        }

        private IncidentUsageRecord GetOrCreateIncidentRecord(string incidentDefName)
        {
            EnsureData();
            if (!data.IncidentUsage.TryGetValue(incidentDefName, out var record))
            {
                record = new IncidentUsageRecord
                {
                    IncidentDefName = incidentDefName,
                    LastUsedDay = -1,
                    UsageDays = new List<int>()
                };
                data.IncidentUsage[incidentDefName] = record;
            }
            return record;
        }

        private string GetKarmaTypeForIncident(string incidentDefNameOrKarma)
        {
            if (string.IsNullOrEmpty(incidentDefNameOrKarma))
                return "neutral";

            string lower = incidentDefNameOrKarma.ToLowerInvariant();

            if (lower == "good" || lower == "bad" || lower == "doom" || lower == "neutral")
                return lower;

            // Fallback def-name heuristics when karmaType was not passed.
            if (lower.Contains("trader") || lower.Contains("caravan") || lower.Contains("refugee")
                || lower.Contains("wanderer") || lower.Contains("ally") || lower.Contains("visitor"))
                return "neutral";

            if (lower.Contains("insanity") || lower.Contains("toxic") || lower.Contains("volcanic")
                || lower.Contains("defoliator") || lower.Contains("psychicemanator") || lower.Contains("raid"))
                return "bad";

            return "neutral";
        }

        private int CurrentGameDay => GenDate.DaysPassed;

        private EventUsageRecord GetOrCreateEventRecord(string eventType)
        {
            EnsureData();
            if (!data.EventUsage.TryGetValue(eventType, out var record))
            {
                record = new EventUsageRecord { EventType = eventType, UsageDays = new List<int>() };
                data.EventUsage[eventType] = record;
            }
            return record;
        }

        private CommandUsageRecord GetOrCreateCommandRecord(string commandName)
        {
            EnsureData();
            if (!data.CommandUsage.TryGetValue(commandName, out var record))
            {
                record = new CommandUsageRecord { CommandName = commandName, UsageDays = new List<int>() };
                data.CommandUsage[commandName] = record;
            }
            return record;
        }

        public string GetEventTypeForCommand(string commandName)
        {
            if (string.IsNullOrEmpty(commandName))
                return "neutral";

            return commandName.ToLowerInvariant() switch
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
                return true;

            if (!settings.EventCooldownsEnabled)
                return true;

            EnsureData();

            try
            {
                int totalPurchases = data.BuyUsage.Values.Sum(record => record.CurrentPeriodPurchases);
                return totalPurchases < settings.MaxItemPurchases;
            }
            catch (Exception ex)
            {
                Logger.Error($"[GlobalCooldown] Error calculating total purchases: {ex}");
                return true;
            }
        }

        public void RecordItemPurchase(string itemType = "general")
        {
            if (string.IsNullOrEmpty(itemType))
                itemType = "general";

            var record = GetOrCreateBuyRecord(itemType);
            record.PurchaseDays ??= new List<int>();
            record.PurchaseDays.Add(GenDate.DaysPassed);

            int cooldownDays = CAPChatInteractiveMod.Instance?.Settings?.GlobalSettings?.EventCooldownDays ?? 0;
            CleanupOldPurchases(record, cooldownDays);
        }

        private BuyUsageRecord GetOrCreateBuyRecord(string itemType)
        {
            EnsureData();
            if (!data.BuyUsage.TryGetValue(itemType, out var record))
            {
                record = new BuyUsageRecord { ItemType = itemType, PurchaseDays = new List<int>() };
                data.BuyUsage[itemType] = record;
            }
            return record;
        }

        private void CleanupOldPurchases(BuyUsageRecord record, int cooldownDays)
        {
            if (cooldownDays == 0 || record?.PurchaseDays == null)
                return;
            record.PurchaseDays.RemoveAll(day => (GenDate.DaysPassed - day) >= cooldownDays);
        }

        private void CleanupOldIncidentUses(IncidentUsageRecord record, int cooldownDays)
        {
            if (cooldownDays <= 0 || record?.UsageDays == null)
                return;
            record.UsageDays.RemoveAll(day => (CurrentGameDay - day) >= cooldownDays);
        }
    }
}
