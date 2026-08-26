// File: EventCommands.cs
//
// Copyright (c) Captolamia
// This file is part of CAP Chat Interactive (RICS).
// Licensed under the GNU Affero General Public License v3.0 or later.
// See LICENSE.txt in the project root for full license text.
//
using CAP_ChatInteractive.Commands.CommandHandlers;
using Verse;

namespace CAP_ChatInteractive.Commands.ViewerCommands
{

    public class Event : ChatCommand
    {
        public override string Name => "event";

        public override string Execute(ChatMessageWrapper messageWrapper, string[] args)
        {

            if (args.Length == 0)
            {
                return "RICS.CC.event.usage".Translate();
            }

            string incidentType = string.Join(" ", args).Trim();
            return IncidentCommandHandler.HandleIncidentCommand(messageWrapper, incidentType);
        }
    }

    public class Weather : ChatCommand
    {
        public override string Name => "weather";

        public override string Execute(ChatMessageWrapper messageWrapper, string[] args)
        {

            if (args.Length == 0)
            {
                return "RICS.CC.weather.usage".Translate();
            }

            string weatherType = args[0].ToLower();
            return WeatherCommandHandler.HandleWeatherCommand(messageWrapper, weatherType);
        }
    }
}
