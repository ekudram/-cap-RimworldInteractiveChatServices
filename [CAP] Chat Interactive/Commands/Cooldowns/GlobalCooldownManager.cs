// GlobalCooldownManager.cs
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace CAP_ChatInteractive.Commands.Cooldowns
{
    public class GlobalCooldownManager : GameComponent
    {
        public GlobalCooldownData data = new GlobalCooldownData();
        private int lastCleanupDay = 0;

        // REQUIRED: GameComponent constructor
        public GlobalCooldownManager(Game game) { }

        public override void ExposeData()
        {
            Scribe_Deep.Look(ref data, "globalCooldownData");
            Scribe_Values.Look(ref lastCleanupDay, "lastCleanupDay");

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
                CleanupOldRecords();
        }

        public bool CanUseEvent(string eventType, CAPGlobalChatSettings settings)
        {
            // 0 = infinite
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
            // Check per-command limits first (most restrictive)
            if (settings.UseEventCooldown && settings.MaxUsesPerCooldownPeriod > 0)
            {
                var cmdRecord = GetOrCreateCommandRecord(commandName);
                CleanupOldCommandUses(cmdRecord, globalSettings.EventCooldownDays);

                if (cmdRecord.CurrentPeriodUses >= settings.MaxUsesPerCooldownPeriod)
                    return false;
            }

            // Check global event limits if command respects them
            if (settings.RespectsGlobalEventCooldown && globalSettings.EventCooldownsEnabled)
            {
                // Determine event type from command
                string eventType = GetEventTypeForCommand(commandName);
                if (!CanUseEvent(eventType, globalSettings))
                    return false;
            }

            return true;
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
    }

    // Supporting data classes
    public class GlobalCooldownData : IExposable
    {
        public Dictionary<string, EventUsageRecord> EventUsage = new Dictionary<string, EventUsageRecord>();
        public Dictionary<string, CommandUsageRecord> CommandUsage = new Dictionary<string, CommandUsageRecord>();

        public void ExposeData()
        {
            Scribe_Collections.Look(ref EventUsage, "eventUsage", LookMode.Value, LookMode.Deep);
            Scribe_Collections.Look(ref CommandUsage, "commandUsage", LookMode.Value, LookMode.Deep);
        }
    }

    public class EventUsageRecord : IExposable
    {
        public string EventType; // "good", "bad", "neutral", "doom"
        public List<int> UsageDays = new List<int>(); // Game days when events were used
        public int CurrentPeriodUses => UsageDays.Count;

        public void ExposeData()
        {
            Scribe_Values.Look(ref EventType, "eventType");
            Scribe_Collections.Look(ref UsageDays, "usageDays", LookMode.Value);
        }
    }

    public class CommandUsageRecord : IExposable
    {
        public string CommandName;
        public List<int> UsageDays = new List<int>();
        public int CurrentPeriodUses => UsageDays.Count;

        public void ExposeData()
        {
            Scribe_Values.Look(ref CommandName, "commandName");
            Scribe_Collections.Look(ref UsageDays, "usageDays", LookMode.Value);
        }
    }
}