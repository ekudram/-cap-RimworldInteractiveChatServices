// GlobalCooldownData.cs
// Copyright (c) Captolamia
// This file is part of CAP Chat Interactive (RICS).
// Licensed under the GNU Affero General Public License v3.0 or later.
// See LICENSE.txt in the project root for full license text.
//
// Save-backed usage records for event / command / buy / incident cooldowns.
using System.Collections.Generic;
using Verse;

namespace CAP_ChatInteractive.Commands.Cooldowns
{
    /// <summary>
    /// Root cooldown blob scribed on GlobalCooldownManager.
    /// Dictionaries may be null after older saves — ExposeData re-inits them.
    /// </summary>
    public class GlobalCooldownData : IExposable
    {
        public Dictionary<string, EventUsageRecord> EventUsage;
        public Dictionary<string, CommandUsageRecord> CommandUsage;
        public Dictionary<string, BuyUsageRecord> BuyUsage;
        public Dictionary<string, IncidentUsageRecord> IncidentUsage;

        public GlobalCooldownData()
        {
            EventUsage = new Dictionary<string, EventUsageRecord>();
            CommandUsage = new Dictionary<string, CommandUsageRecord>();
            BuyUsage = new Dictionary<string, BuyUsageRecord>();
            IncidentUsage = new Dictionary<string, IncidentUsageRecord>();
        }

        public void ExposeData()
        {
            Scribe_Collections.Look(ref EventUsage, "eventUsage", LookMode.Value, LookMode.Deep);
            Scribe_Collections.Look(ref CommandUsage, "commandUsage", LookMode.Value, LookMode.Deep);
            Scribe_Collections.Look(ref BuyUsage, "buyUsage", LookMode.Value, LookMode.Deep);
            Scribe_Collections.Look(ref IncidentUsage, "incidentUsage", LookMode.Value, LookMode.Deep);

            EventUsage ??= new Dictionary<string, EventUsageRecord>();
            CommandUsage ??= new Dictionary<string, CommandUsageRecord>();
            BuyUsage ??= new Dictionary<string, BuyUsageRecord>();
            IncidentUsage ??= new Dictionary<string, IncidentUsageRecord>();
        }
    }

    public class EventUsageRecord : IExposable
    {
        public string EventType; // good / bad / neutral (doom folds into bad at record time)
        // Field (not property) — RimWorld/Mono collection serialization + ref safety.
        public List<int> UsageDays = new List<int>();
        public int CurrentPeriodUses => UsageDays?.Count ?? 0;

        public void ExposeData()
        {
            Scribe_Values.Look(ref EventType, "eventType");
            Scribe_Collections.Look(ref UsageDays, "usageDays", LookMode.Value);
            UsageDays ??= new List<int>();
        }
    }

    public class CommandUsageRecord : IExposable
    {
        public string CommandName;
        public List<int> UsageDays = new List<int>();
        public int CurrentPeriodUses => UsageDays?.Count ?? 0;

        public void ExposeData()
        {
            Scribe_Values.Look(ref CommandName, "commandName");
            Scribe_Collections.Look(ref UsageDays, "usageDays", LookMode.Value);
            UsageDays ??= new List<int>();
        }
    }

    public class BuyUsageRecord : IExposable
    {
        public string ItemType;
        public List<int> PurchaseDays = new List<int>();
        public int CurrentPeriodPurchases => PurchaseDays?.Count ?? 0;

        public void ExposeData()
        {
            Scribe_Values.Look(ref ItemType, "itemType");
            Scribe_Collections.Look(ref PurchaseDays, "purchaseDays", LookMode.Value);
            PurchaseDays ??= new List<int>();
        }
    }

    public class IncidentUsageRecord : IExposable
    {
        public string IncidentDefName;
        public int LastUsedDay = -1;
        public List<int> UsageDays = new List<int>();

        public void ExposeData()
        {
            Scribe_Values.Look(ref IncidentDefName, "incidentDefName");
            Scribe_Values.Look(ref LastUsedDay, "lastUsedDay", -1);
            Scribe_Collections.Look(ref UsageDays, "usageDays", LookMode.Value);
            UsageDays ??= new List<int>();
        }
    }
}
