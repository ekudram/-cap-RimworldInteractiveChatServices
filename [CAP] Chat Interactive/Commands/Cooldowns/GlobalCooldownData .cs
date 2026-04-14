// GlobalCooldownData.cs
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
// Data structures for tracking global cooldowns for events and commands
using System.Collections.Generic;
using Verse;

namespace CAP_ChatInteractive.Commands.Cooldowns
{
    // Update GlobalCooldownData.cs
    // Add this new class for incident-specific cooldowns
    public class IncidentUsageRecord : IExposable
    {
        public string IncidentDefName;
        public List<int> UsageDays = new List<int>(); // Game days when incidents were used

        public int CurrentPeriodUses => UsageDays.Count;

        public void ExposeData()
        {
            Scribe_Values.Look(ref IncidentDefName, "incidentDefName");
            Scribe_Collections.Look(ref UsageDays, "usageDays", LookMode.Value);
        }
    }

    // Update GlobalCooldownData class to include incident tracking
    // In GlobalCooldownData.cs, inside GlobalCooldownData class
    // Replace the entire GlobalCooldownData class with this:
    public class GlobalCooldownData : IExposable
    {
        public Dictionary<string, EventUsageRecord> EventUsage;
        public Dictionary<string, CommandUsageRecord> CommandUsage;
        public Dictionary<string, BuyUsageRecord> BuyUsage;
        public Dictionary<string, IncidentUsageRecord> IncidentUsage;

        public void ExposeData()
        {
            Scribe_Collections.Look(ref EventUsage, "eventUsage", LookMode.Value, LookMode.Deep);
            Scribe_Collections.Look(ref CommandUsage, "commandUsage", LookMode.Value, LookMode.Deep);
            Scribe_Collections.Look(ref BuyUsage, "buyUsage", LookMode.Value, LookMode.Deep);
            Scribe_Collections.Look(ref IncidentUsage, "incidentUsage", LookMode.Value, LookMode.Deep);

            // Backward compatibility: Initialize any missing dictionaries after loading
            if (EventUsage == null)
            {
                EventUsage = new Dictionary<string, EventUsageRecord>();
                Logger.Debug("EventUsage initialized in GlobalCooldownData.ExposeData");
            }
            if (CommandUsage == null)
            {
                CommandUsage = new Dictionary<string, CommandUsageRecord>();
                Logger.Debug("CommandUsage initialized in GlobalCooldownData.ExposeData");
            }
            if (BuyUsage == null)
            {
                BuyUsage = new Dictionary<string, BuyUsageRecord>();
                Logger.Debug("BuyUsage initialized in GlobalCooldownData.ExposeData");
            }
            if (IncidentUsage == null)
            {
                IncidentUsage = new Dictionary<string, IncidentUsageRecord>();
                Logger.Debug("IncidentUsage initialized in GlobalCooldownData.ExposeData");
            }
        }

        public GlobalCooldownData()
        {
            // Ensure all dictionaries are initialized
            EventUsage = new Dictionary<string, EventUsageRecord>();
            CommandUsage = new Dictionary<string, CommandUsageRecord>();
            BuyUsage = new Dictionary<string, BuyUsageRecord>();
            IncidentUsage = new Dictionary<string, IncidentUsageRecord>();
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
    public class BuyUsageRecord : IExposable
    {
        public string ItemType; // "weapon", "apparel", "item", "surgery", etc.
        public List<int> PurchaseDays = new List<int>(); // Game days when items were purchased
        public int CurrentPeriodPurchases => PurchaseDays.Count;

        public void ExposeData()
        {
            Scribe_Values.Look(ref ItemType, "itemType");
            Scribe_Collections.Look(ref PurchaseDays, "purchaseDays", LookMode.Value);
        }
    }


}

