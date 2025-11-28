using CAP_ChatInteractive;
using CAP_ChatInteractive.Commands.CommandHandlers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
