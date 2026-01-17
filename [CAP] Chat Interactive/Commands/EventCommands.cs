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


using CAP_ChatInteractive.Commands.CommandHandlers;

namespace CAP_ChatInteractive.Commands.ViewerCommands
{
    // Event command
    public class Event : ChatCommand
    {
        public override string Name => "event";

        public override string Execute(ChatMessageWrapper user, string[] args)
        {

            if (args.Length == 0)
            {
                return "Usage: !event <event_name> or !lookup event <name>.";
            }

            string incidentType = string.Join(" ", args).Trim();
            return IncidentCommandHandler.HandleIncidentCommand(user, incidentType);
        }
    }

    public class Weather : ChatCommand
    {
        public override string Name => "weather";

        public override string Execute(ChatMessageWrapper user, string[] args)
        {

            if (args.Length == 0)
            {
                return "Usage: !weather <type>. Types: rain, snow, fog, thunderstorm, clear, etc.";
            }

            string weatherType = args[0].ToLower();
            return WeatherCommandHandler.HandleWeatherCommand(user, weatherType);
        }
    }
}
