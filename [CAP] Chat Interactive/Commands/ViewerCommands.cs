// ViewerCommands.cs
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
// Commands that viewers can use to interact with the game
using CAP_ChatInteractive.Commands.CommandHandlers;
using RimWorld;
using RimWorld.BaseGen;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;
using CAP_ChatInteractive;

namespace CAP_ChatInteractive.Commands.ViewerCommands
{
    public class Bal : ChatCommand
    {
        public override string Name => "bal";

        public override string Execute(ChatMessageWrapper messageWrapper, string[] args)
        {
            var viewer = Viewers.GetViewer(messageWrapper.Username);
            if (viewer != null)
            {
                var settings = CAPChatInteractiveMod.Instance.Settings.GlobalSettings;
                var currencySymbol = settings.CurrencyName?.Trim() ?? "¢";

                // Format coins with commas for thousands
                var formattedCoins = viewer.Coins.ToString("N0");

                // Use the shared karma emoji method
                string karmaEmoji = GetKarmaEmoji(viewer.Karma);

                // Calculate coins earned per award cycle (every 2 minutes)
                int baseCoins = settings.BaseCoinReward;
                float karmaMultiplier = (float)viewer.Karma / 100f;

                // Apply role multipliers
                int coinsPerAward = (int)(baseCoins * karmaMultiplier);

                if (viewer.IsSubscriber)
                    coinsPerAward += settings.SubscriberExtraCoins;
                if (viewer.IsVip)
                    coinsPerAward += settings.VipExtraCoins;
                if (viewer.IsModerator)
                    coinsPerAward += settings.ModExtraCoins;

                // Calculate coins per hour (30 cycles per hour)
                int coinsPerHour = coinsPerAward * 30;

                // Calculate remaining active time
                string activeTimeInfo = GetRemainingActiveTimeInfo(viewer, settings);

                return $"💰 Balance: {formattedCoins} {currencySymbol}\n" +
                       $"📊 Karma: {viewer.Karma} {karmaEmoji}\n" +
                       $"💸 Earnings: {coinsPerAward} {currencySymbol} every 2 minutes\n" +
                       $"⏱️ Rate: ~{coinsPerHour} {currencySymbol}/hour"; // +
                                                                          //activeTimeInfo;
            }
            return "Could not find your viewer data.";
        }

        private string GetRemainingActiveTimeInfo(Viewer viewer, CAPGlobalChatSettings settings)
        {
            try
            {

                // Use LastSeen instead of LastActivityTime
                var timeSinceLastActivity = DateTime.UtcNow - viewer.LastSeen;
                int minutesActive = (int)timeSinceLastActivity.TotalMinutes;
                int minutesRemaining = Math.Max(0, settings.MinutesForActive - minutesActive);

                if (minutesRemaining > 0)
                {
                    return $"\n⏰ Active for: {minutesRemaining} more minutes";
                }
                else
                {
                    return "\n⚠️ Not currently active (chat to become active!)";
                }
            }
            catch
            {
                // If we can't calculate it, that's okay
                return "";
            }
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
        public override string Execute(ChatMessageWrapper messageWrapper, string[] args)
        {
            var availableCommands = ChatCommandProcessor.GetAvailableCommands(messageWrapper);
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

    public class ModSettings : ChatCommand
    {
        public override string Name => "modsettings";

        public override string Execute(ChatMessageWrapper messageWrapper, string[] args)
        {
            var settings = CAPChatInteractiveMod.Instance.Settings.GlobalSettings;
            var currencySymbol = settings.CurrencyName?.Trim() ?? "¢";

            string response = $"👋 Coins: {settings.BaseCoinReward}{currencySymbol}/2 min | Karma Max: {settings.MaxKarma} 🎯";

            if (settings.EventCooldownsEnabled)
            {
                response += $" | Events: {settings.EventsperCooldown}/{settings.EventCooldownDays}d";

                if (settings.KarmaTypeLimitsEnabled)
                    response += $" (🔴{settings.MaxBadEvents} 🟢{settings.MaxGoodEvents} ⚪{settings.MaxNeutralEvents})";

                response += $" | Purchases: {settings.MaxItemPurchases}/{settings.EventCooldownDays}d";
            }
            else
            {
                response += " | Event cooldowns: OFF ❌";
            }

            response += $" | 🎭Trait Max: {settings.MaxTraits}";

            return response;
        }
    }

    public class ModInfo : ChatCommand
    {
        public override string Name => "modinfo";

        public override string Execute(ChatMessageWrapper messageWrapper, string[] args)
        {
            var globalChatSettings = CAPChatInteractiveMod.Instance.Settings.GlobalSettings;
            return $"RICS ver {globalChatSettings.modVersion} --- GitHub Releases:  https://github.com/ekudram/-cap-RimworldInteractiveChatServices/releases";
        }
    }

    public class Wealth : ChatCommand
    {
        public override string Name => "wealth";
        public override string Execute(ChatMessageWrapper messageWrapper, string[] args)
        {
            return WealthCommandHandler.HandleWealthCommand(messageWrapper, args);
        }
    }

    public class Factions : ChatCommand
    {
        public override string Name => "factions";
        public override string Execute(ChatMessageWrapper messageWrapper, string[] args)
        {
            var factionManager = Current.Game.World.factionManager;
            var factions = factionManager.AllFactionsVisible
                .Where(f => !f.IsPlayer)
                .OrderByDescending(f => f.PlayerGoodwill);

            var allies = new List<string>();
            var neutrals = new List<string>();
            var enemies = new List<string>();

            foreach (Faction faction in factions)
            {
                string entry = $"{faction.Name}[{faction.PlayerGoodwill}]";

                switch (faction.PlayerRelationKind)
                {
                    case FactionRelationKind.Ally:
                        allies.Add(entry);
                        break;
                    case FactionRelationKind.Neutral:
                        neutrals.Add(entry);
                        break;
                    case FactionRelationKind.Hostile:
                        enemies.Add(entry);
                        break;
                }
            }

            var resultParts = new List<string>();

            if (allies.Count > 0)
                resultParts.Add($"Allies: {string.Join(" • ", allies)}");

            if (neutrals.Count > 0)
                resultParts.Add($"Neutrals: {string.Join(" • ", neutrals)}");

            if (enemies.Count > 0)
                resultParts.Add($"Enemies: {string.Join(" • ", enemies)}");

            return string.Join(" | ", resultParts);
        }
    }
    public class Colonists : ChatCommand
    {
        public override string Name => "colonists";
        public override string Execute(ChatMessageWrapper messageWrapper, string[] args)
        {
            var assignmentManager = CAPChatInteractiveMod.GetPawnAssignmentManager();
            //var colonistList = Current.Game.PlayerHomeMaps.SelectMany(m => m.mapPawns.FreeColonistsSpawned);
            //var animalsList = Current.Game.PlayerHomeMaps.SelectMany(m => m.mapPawns.ColonyAnimals);
            int colonistCount = Current.Game.PlayerHomeMaps.Sum(m => m.mapPawns.FreeColonistsSpawnedCount);
            int animalCount = Current.Game.PlayerHomeMaps.Sum(m => m.mapPawns.ColonyAnimals.Count);
            int viewerCount = assignmentManager.viewerPawnAssignments.Count;

            return $"There are {colonistCount} colonists({viewerCount} viewers) and {animalCount} colony animals.";
        }
    }

    public class Stockpile : ChatCommand
    {
        public override string Name => "stockpile";
        public override string Execute(ChatMessageWrapper messageWrapper, string[] args)
        {


            return "";
        }
    }
}