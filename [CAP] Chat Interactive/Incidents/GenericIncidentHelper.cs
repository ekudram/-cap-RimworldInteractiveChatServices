/*
// GenericIncidentHelper.cs
// Copyright (c) Captolamia. All rights reserved.
// Licensed under the AGPLv3 License. See LICENSE file in the project root for full license information.
//
// A generic helper class for handling incidents triggered by chat commands.
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace CAP_ChatInteractive.Incidents
{
    public abstract class GenericIncidentHelper
    {
        public IncidentDef IncidentDef { get; protected set; }
        public Viewer Viewer { get; set; }
        public string CustomMessage { get; set; }
        public ChatMessageWrapper OriginalMessage { get; set; } // ADD THIS

        protected IncidentParms incidentParms;
        protected IncidentWorker worker;

        public virtual bool IsPossible()
        {
            if (IncidentDef == null) return false;

            worker = IncidentDef.Worker;
            incidentParms = CreateIncidentParms();

            var playerMaps = Current.Game.Maps.Where(map => map.IsPlayerHome).ToList();
            playerMaps.Shuffle();

            foreach (var map in playerMaps)
            {
                incidentParms.target = map;
                if (worker.CanFireNow(incidentParms) && !worker.FiredTooRecently(map))
                {
                    return true;
                }
            }
            return false;
        }

        public virtual void TryExecute()
        {
            if (worker != null && incidentParms != null)
            {
                bool success = worker.TryExecute(incidentParms);
                HandleExecutionResult(success);
            }
        }

        protected virtual IncidentParms CreateIncidentParms()
        {
            return new IncidentParms
            {
                forced = true,
                points = CalculatePoints()
            };
        }

        protected virtual float CalculatePoints()
        {
            // Default implementation - can be overridden
            return StorytellerUtility.DefaultThreatPointsNow(incidentParms?.target as Map);
        }

        protected virtual void HandleExecutionResult(bool success)
        {
            if (success)
            {
                SendSuccessMessage();
            }
            else
            {
                SendFailureMessage();
            }
        }

        protected virtual void SendSuccessMessage()
        {
            string message = CustomMessage ?? $"{IncidentDef.label} has been triggered!";

            if (OriginalMessage != null)
            {
                ChatCommandProcessor.SendMessageToUser(OriginalMessage, message);
            }
            else if (Viewer != null)
            {
                // Fallback if we only have viewer info
                var messageWrapper = new ChatMessageWrapper(Viewer.Username, "", "Unknown");
                ChatCommandProcessor.SendMessageToUser(messageWrapper, message);
            }
        }

        protected virtual void SendFailureMessage()
        {
            if (OriginalMessage != null)
            {
                ChatCommandProcessor.SendMessageToUser(OriginalMessage, $"Failed to trigger {IncidentDef.label}.");
            }
            else if (Viewer != null)
            {
                var messageWrapper = new ChatMessageWrapper(Viewer.Username, "", "Unknown");
                ChatCommandProcessor.SendMessageToUser(messageWrapper, $"Failed to trigger {IncidentDef.label}.");
            }
        }
        public void SetIncidentDef(IncidentDef incidentDef)
        {
            this.IncidentDef = incidentDef;
        }

    }
}
*/