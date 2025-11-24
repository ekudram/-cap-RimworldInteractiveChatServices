// ViewerCommands.cs
// Copyright (c) Captolamia. All rights reserved.
// Licensed under the AGPLv3 License. See LICENSE file in the project root for full license information.
//
// Commands that viewers can use to interact with the game
using CAP_ChatInteractive.Commands.CommandHandlers;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace CAP_ChatInteractive.Commands.ViewerCommands
{
    public class Bal : ChatCommand
    {
        public override string Name => "bal";

        public override string Execute(ChatMessageWrapper user, string[] args)
        {
            // Get command settings
            var settingsCommand = GetCommandSettings();

            var viewer = Viewers.GetViewer(user.Username);
            if (viewer != null)
            {
                var settings = CAPChatInteractiveMod.Instance.Settings.GlobalSettings;
                var currencySymbol = settings.CurrencyName?.Trim() ?? "¢";

                // Format coins with commas for thousands
                var formattedCoins = viewer.Coins.ToString("N0");

                // Use the shared karma emoji method
                string karmaEmoji = GetKarmaEmoji(viewer.Karma);

                return $"You have {formattedCoins} {currencySymbol} and {viewer.Karma} karma! {karmaEmoji}";
            }
            return "Could not find your viewer data.";
        }
    }

    public class WhatIsKarma : ChatCommand
    {
        public override string Name => "whatiskarma";

        public override string Execute(ChatMessageWrapper user, string[] args)
        {
            return "Karma affects your coin rewards! Higher karma = more coins per message. Be active and positive to increase your karma!";
        }
    }

    // Event command
    public class Event : ChatCommand
    {
        public override string Name => "event";

        public override string Execute(ChatMessageWrapper user, string[] args)
        {
            if (args.Length == 0)
            {
                return "Usage: !event <event_name> or !event list. Examples: !event resourcepod, !event heatwave, !event psychicsoothe";
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

    public class help : ChatCommand
    {
        public override string Name => "help";

        public override string Execute(ChatMessageWrapper user, string[] args)
        {
            return "Full command list: https://tinyurl.com/RICSWiki (mobile friendly!)";
        }
    }

    public class commands : ChatCommand
    {
        public override string Name => "commands";
        public override string Execute(ChatMessageWrapper user, string[] args)
        {
            var availableCommands = ChatCommandProcessor.GetAvailableCommands(user);
            var commandList = string.Join(", ", availableCommands.Select(cmd => $"!{cmd.Name}"));
            return $"Available commands: {commandList}";
        }
    }

    public class lookup : ChatCommand
    {
        public override string Name => "lookup";

        public override string Execute(ChatMessageWrapper user, string[] args)
        {
            if (args.Length == 0)
            {
                return "Usage: !lookup [item|event|weather|trait] <name> - Search specific categories. Example: !lookup item rifle, !lookup event raid, !lookup weather rain, !lookup trait kind";
            }

            // Parse the category if specified
            string searchType = args[0].ToLower();
            string searchTerm;

            if (searchType == "item" || searchType == "event" || searchType == "weather" || searchType == "trait")
            {
                if (args.Length < 2)
                {
                    return $"Usage: !lookup {searchType} <name> - Search for {searchType}s. Example: !lookup {searchType} {GetExampleForType(searchType)}";
                }
                searchTerm = string.Join(" ", args.Skip(1)).ToLower();
                return LookupCommandHandler.HandleLookupCommand(user, searchTerm, searchType);
            }
            else
            {
                // No category specified - search all
                searchTerm = string.Join(" ", args).ToLower();
                return LookupCommandHandler.HandleLookupCommand(user, searchTerm, "all");
            }
        }

        private static string GetExampleForType(string type)
        {
            return type switch
            {
                "item" => "rifle",
                "event" => "raid",
                "weather" => "rain",
                "trait" => "kind",
                _ => "search_term"
            };
        }
    }

    public class GiftCoins : ChatCommand
    {
        public override string Name => "giftcoins"; // Changed from "givecoins" to match XML

        public override string Execute(ChatMessageWrapper messageWrapper, string[] args)
        {
            // Check if we have enough arguments
            if (args.Length < 2)
            {
                return "Usage: !giftcoins <viewer> <amount>";
            }

            string targetUsername = args[0];

            // Parse the coin amount
            if (!int.TryParse(args[1], out int coinAmount) || coinAmount <= 0)
            {
                return "Please specify a valid positive number of coins to give.";
            }

            // Get the sender's viewer data - USING STATIC METHOD
            Viewer sender = Viewers.GetViewer(messageWrapper);
            if (sender == null)
            {
                return "Error: Could not find your viewer data.";
            }

            // Check if sender has enough coins
            if (sender.GetCoins() < coinAmount)
            {
                var formattedSenderCoins = sender.GetCoins().ToString("N0");
                var formattedCoinAmount = coinAmount.ToString("N0");
                return $"You don't have enough coins. You have {formattedSenderCoins} coins but tried to give {formattedCoinAmount}.";


            }

            // Get the target viewer - USING STATIC METHOD
            Viewer target = Viewers.GetViewer(targetUsername);
            if (target == null)
            {
                return $"Viewer '{targetUsername}' not found.";
            }

            // Cannot give coins to yourself
            if (sender.Username.Equals(target.Username, StringComparison.OrdinalIgnoreCase))
            {
                return "You cannot give coins to yourself.";
            }

            // Transfer coins
            sender.TakeCoins(coinAmount);
            target.GiveCoins(coinAmount);

            // Save the changes - USING STATIC METHOD
            Viewers.SaveViewers();

            return $"Successfully gave {coinAmount} coins to {target.DisplayName}. You now have {sender.GetCoins()} coins remaining.";
        }
    }

    public class OpenLootBox : ChatCommand
    {
        public override string Name => "openlootbox";

        public override string Execute(ChatMessageWrapper messageWrapper, string[] args)
        {
            return LootBoxCommandHandler.HandleLootboxCommand(messageWrapper, args); // args are passed for potential future use
        }
    }

    public class Research : ChatCommand
    {
        public override string Name => "research";

        public override string Execute(ChatMessageWrapper messageWrapper, string[] args)
        {
            Logger.Debug("research command Called");
            return ResearchCommandHandler.HandleResearchCommand(messageWrapper, args);
        }
    }

    public class Passion : ChatCommand
    {
        public override string Name => "passion";

        public override string Execute(ChatMessageWrapper messageWrapper, string[] args)
        {
            return PassionCommandhandler.HandlePassionCommand(messageWrapper, args);
        }
    }
}