using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace _CAP__Chat_Interactive.Commands.Cooldowns
{
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
