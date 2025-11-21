// WeatherIncidentHelper.cs
/* // Copyright (c) Captolamia. All rights reserved.
// Licensed under the AGPLv3 License. See LICENSE file in the project root for full license information.
//
// A helper class to manage weather-related incidents triggered via chat commands.
using RimWorld;
using System.Linq;
using Verse;

namespace CAP_ChatInteractive.Incidents
{
    public class WeatherIncidentHelper : GenericIncidentHelper
    {
        public WeatherDef WeatherDef { get; private set; }

        public void SetWeatherDef(WeatherDef weatherDef)
        {
            this.WeatherDef = weatherDef;
        }

        public override bool IsPossible()
        {
            if (WeatherDef == null || IncidentDef == null)
                return false;

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

        public override void TryExecute()
        {
            if (worker != null && incidentParms != null && WeatherDef != null)
            {
                // For weather incidents, we might need special handling
                bool success = worker.TryExecute(incidentParms);
                HandleExecutionResult(success);
            }
        }

        protected override IncidentParms CreateIncidentParms()
        {
            return new IncidentParms
            {
                forced = true,
                points = CalculatePoints(),
                // Weather-specific parameters if needed
            };
        }

        protected override void SendSuccessMessage()
        {
            string message = CustomMessage ?? $"{WeatherDef.label} weather has been triggered!";

            if (OriginalMessage != null)
            {
                ChatCommandProcessor.SendMessageToUser(OriginalMessage, message);
            }
            else if (Viewer != null)
            {
                var messageWrapper = new ChatMessageWrapper(Viewer.Username, "", "Unknown");
                ChatCommandProcessor.SendMessageToUser(messageWrapper, message);
            }
        }

        protected override void SendFailureMessage()
        {
            if (OriginalMessage != null)
            {
                ChatCommandProcessor.SendMessageToUser(OriginalMessage, $"Failed to trigger {WeatherDef?.label ?? "weather"}.");
            }
            else if (Viewer != null)
            {
                var messageWrapper = new ChatMessageWrapper(Viewer.Username, "", "Unknown");
                ChatCommandProcessor.SendMessageToUser(messageWrapper, $"Failed to trigger {WeatherDef?.label ?? "weather"}.");
            }
        }
    }
}*/