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
using _CAP__Chat_Interactive.Utilities;
using CAP_ChatInteractive;
using CAP_ChatInteractive.Commands.CommandHandlers;
using CAP_ChatInteractive.Utilities;
using RimWorld;
using RimWorld.BaseGen;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

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


                string line1 = "RICS.CC.bal.line1".Translate(viewer.Coins.ToString("N0"), currencySymbol);
                string line2 = "RICS.CC.bal.line2".Translate(viewer.Karma, karmaEmoji);

                string line3 = "RICS.CC.bal.line3".Translate(coinsPerAward.ToString("N0"), currencySymbol);
                string line4 = "RICS.CC.bal.line4".Translate(coinsPerHour.ToString("N0"), currencySymbol);

                // Change to (with dividers; assumes you want to keep activeTimeInfo commented):

                return $"{line1} | {line2} | {line3} | {line4}"; // + (string.IsNullOrEmpty(activeTimeInfo) ? "" : $" | {activeTimeInfo}");
                //return $"💰 Balance: {formattedCoins} {currencySymbol}\n" +
                //       $"📊 Karma: {viewer.Karma} {karmaEmoji}\n" +
                //       $"💸 Earnings: {coinsPerAward} {currencySymbol} every 2 minutes\n" +
                //       $"⏱️ Rate: ~{coinsPerHour} {currencySymbol}/hour"; // +
                //                                                          //activeTimeInfo;
            }
            return "RICS.CC.bal.notfound".Translate();
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
                    return "RICS.CC.bal.activeRemaining".Translate(minutesRemaining);
                }
                else
                {
                    return "RICS.CC.bal.notActive".Translate();
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
            return "RICS.CC.whatiskarma".Translate();
        }
    }

    public class help : ChatCommand
    {
        public override string Name => "help";

        public override string Execute(ChatMessageWrapper user, string[] args)
        {
            return "RICS.CC.help".Translate();
        }
    }

    public class commands : ChatCommand
    {
        public override string Name => "commands";
        public override string Execute(ChatMessageWrapper messageWrapper, string[] args)
        {
            var available = ChatCommandProcessor.GetAvailableCommands(messageWrapper);
            var cmdList = string.Join(", ", available.Select(c => $"!{c.Name}"));
            return "RICS.CC.commands.header".Translate() + " " + cmdList;
        }
    }

    public class lookup : ChatCommand
    {
        public override string Name => "lookup";

        public override string Execute(ChatMessageWrapper user, string[] args)
        {
            if (args.Length == 0)
            {
                return "RICS.CC.lookup.usage".Translate();
                // return "Usage: !lookup [item|event|weather|trait|race|xenotype] [name] - Search specific categories. Examples: !lookup item rifle, !lookup race (lists all races), !lookup event raid";

            }

            string searchType = args[0].ToLower();
            string searchTerm = null;

            bool isFullListRequest = args.Length == 1;

            if (searchType == "item" || searchType == "event" || searchType == "weather" ||
                searchType == "trait" || searchType == "race" || searchType == "xenotype")
            {
                if (isFullListRequest)
                {
                    // Special case: !lookup <category> with no term → show all enabled for that category
                    // For now, implement only for "race" as requested
                    if (searchType == "race")
                    {
                        return GetAllRacesList();
                    }
                    else
                    {
                        return "RICS.CC.lookup.needTerm".Translate(searchType, searchType);
                        // For other categories: tell user they need a search term (or expand later)
                        // return $"Usage: !lookup {searchType} <name> - Search for {searchType}s.";
                    }
                }

                // Normal search with term
                if (args.Length < 2)
                {
                    return "RICS.CC.lookup.needTerm".Translate(searchType, searchType);
                }

                searchTerm = string.Join(" ", args.Skip(1)).ToLower();
                return LookupCommandHandler.HandleLookupCommand(user, searchTerm, searchType);
            }
            else
            {
                // No valid category → treat whole input as search term for "all"
                searchTerm = string.Join(" ", args).ToLower();
                return LookupCommandHandler.HandleLookupCommand(user, searchTerm, "all");
            }
        }
        private static string GetAllRacesList()
        {
            try
            {
                var enabledRaces = RaceUtils.GetEnabledRaces()
                    .Where(r => r != null)
                    .OrderBy(r => r.LabelCap.RawText)
                    .ToList();

                if (!enabledRaces.Any())
                {
                    return "RICS.LCH.NoRacesEnabled".Translate();  // add this key: "No races are currently enabled."
                }

                var settings = CAPChatInteractiveMod.Instance.Settings.GlobalSettings;
                var currency = settings.CurrencyName?.Trim() ?? "¢";

                var lines = new List<string>();

                foreach (var race in enabledRaces)
                {
                    var raceSettings = RaceSettingsManager.GetRaceSettings(race.defName);
                    if (raceSettings == null) continue;

                    string name = race.LabelCap.RawText;
                    int cost = raceSettings.BasePrice;

                    lines.Add($"{TextUtilities.StripTags(name)}: {cost} {currency}");
                }

                if (!lines.Any())
                {
                    return "RICS.LCH.NoRacesEnabled".Translate();
                }

                // Join with | like lookup results, or use \n for readability if many
                // Using | to stay consistent with normal lookup output
                string resultList = string.Join(" | ", lines);

                string header = "RICS.LCH.AllRaces".Translate();   // "All available races"

                return $"🔍 {header}: {resultList}";
            }
            catch (Exception ex)
            {
                Logger.Error($"Error listing all races: {ex}");
                return "RICS.LCH.ErrorListingRaces".Translate(ex.Message.Length > 80 ? ex.Message.Substring(0, 80) + "..." : ex.Message);
            }
        }
    }

    public class GiftCoins : ChatCommand
    {
        public override string Name => "giftcoins";

        public override string Execute(ChatMessageWrapper messageWrapper, string[] args)
        {
            // Check if we have enough arguments
            if (args.Length < 2)
            {
                return "RICS.CC.giftcoins.usage".Translate();
            }

            // Handle @username format - remove @ if present
            string targetUsername = args[0];
            if (targetUsername.StartsWith("@"))
            {
                targetUsername = targetUsername.Substring(1);
            }
            targetUsername = targetUsername.ToLowerInvariant().Trim(); // Normalize case and trim

            // Parse the coin amount
            if (!int.TryParse(args[1], out int coinAmount) || coinAmount <= 0)
            {
                return "RICS.CC.giftcoins.invalidAmount".Translate();
            }

            // Early self-check (case-insensitive)
            if (targetUsername.Equals(messageWrapper.Username.ToLowerInvariant()))
            {
                return "RICS.CC.giftcoins.selfGift".Translate();
            }

            // Get both viewers WITHIN THE SAME LOCK to ensure consistency
            string result;
            lock (Viewers._lock)
            {
                // Get the sender's viewer data
                Viewer sender = Viewers.GetViewer(messageWrapper);
                if (sender == null)
                {
                    return "RICS.CC.bal.notfound".Translate();
                }

                // Check if sender has enough coins
                if (sender.GetCoins() < coinAmount)
                {
                    var formattedSenderCoins = sender.GetCoins().ToString("N0");
                    var formattedCoinAmount = coinAmount.ToString("N0");
                    // return $"You don't have enough coins. You have {formattedSenderCoins} coins but tried to give {formattedCoinAmount}.";
                    return "RICS.CC.giftcoins.notEnough".Translate(formattedSenderCoins, formattedCoinAmount);
                }

                // Get the target viewer
                Viewer target = Viewers.GetViewer(targetUsername);
                if (target == null)
                {
                    return "RICS.CC.giftcoins.targetNotFound".Translate(targetUsername);
                }

                // Final self-check (in case GetViewer normalized the username differently)
                if (sender.Username.Equals(target.Username, StringComparison.OrdinalIgnoreCase))
                {
                    return "RICS.CC.giftcoins.selfGift".Translate();
                }

                // Ensure target can receive coins (not banned, etc.)
                if (target.IsBanned)
                {
                    return "RICS.CC.giftcoins.targetBanned".Translate(target.DisplayName);
                }

                // Perform atomic transaction
                try
                {
                    // Take coins from sender first
                    sender.TakeCoins(coinAmount);

                    // Give coins to target
                    target.GiveCoins(coinAmount);

                    // Save immediately for transaction safety
                    Viewers.SaveViewers();

                    // Log the transaction for debugging
                    Logger.Debug($"GiftCoins: {sender.Username} gave {coinAmount} coins to {target.Username}. " +
                               $"Sender now has {sender.GetCoins()}, receiver now has {target.GetCoins()}");

                    //result = $"Successfully gave {coinAmount} coins to {target.DisplayName}. You now have {sender.GetCoins():N0} coins remaining.";
                    result = "RICS.CC.giftcoins.success".Translate(
                        coinAmount.ToString("N0"),
                        target.DisplayName,
                        sender.GetCoins().ToString("N0"));
                }
                catch (Exception ex)
                {
                    // If anything fails, attempt to rollback
                    Logger.Error($"GiftCoins transaction failed: {ex.Message}");
                    // In a real implementation, you might want to rollback here
                    // But since we SaveViewers() after both operations, they should be atomic
                    result = "An error occurred during the transaction. Please try again.";
                }
            }
            return result;
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
            // Logger.Debug("research command Called");
            return ResearchCommandHandler.HandleResearchCommand(messageWrapper, args);
        }
    }

    public class Study : ChatCommand
    {
        public override string Name => "study";

        public override string Execute(ChatMessageWrapper messageWrapper, string[] args)
        {
            if (!ModsConfig.AnomalyActive)
            {
                return "RICS.CC.study.dlcrequired".Translate();
                // return "Requires anomaly DLC";
            }
            return ResearchCommandHandler.HandleStudyCommand(messageWrapper, args);
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

            //string response = $"👋 Currency: {settings.BaseCoinReward}{currencySymbol}/2 min | Karma Max: {settings.MaxKarma} 🎯";
            string response = "RICS.CC.modsettings.currencysettings".Translate(settings.BaseCoinReward, currencySymbol, settings.MaxKarma);
            if (settings.EventCooldownsEnabled)
            {
                response += " | " + "RICS.CC.modsettings.eventsOn".Translate(settings.EventsperCooldown, settings.EventCooldownDays);
                if (settings.KarmaTypeLimitsEnabled)
                    response += "RICS.CC.modsettings.karmaLimits".Translate(settings.MaxBadEvents, settings.MaxGoodEvents, settings.MaxNeutralEvents);
                response += " | " + "RICS.CC.modsettings.purchases".Translate(settings.MaxItemPurchases, settings.EventCooldownDays);
            }
            else
            {
                response += "RICS.CC.modsettings.eventoff".Translate();
            }
            response += " | " + "RICS.CC.modsettings.maxtraits".Translate(settings.MaxTraits);
            return response;
        }
    }

    public class ModInfo : ChatCommand
    {
        public override string Name => "modinfo";

        public override string Execute(ChatMessageWrapper messageWrapper, string[] args)
        {
            var globalChatSettings = CAPChatInteractiveMod.Instance.Settings.GlobalSettings;
            return "RICS.CC.modinfo".Translate(globalChatSettings.modVersion);
            // return $"RICS ver {globalChatSettings.modVersion} --- GitHub Releases:  https://github.com/ekudram/-cap-RimworldInteractiveChatServices/releases";
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

            var parts = new List<string>();

            if (allies.Any())
                parts.Add("RICS.CC.factions.allies".Translate() + ": " + string.Join("RICS.CC.factions.separator".Translate(), allies));

            if (neutrals.Any())
                parts.Add("RICS.CC.factions.neutrals".Translate() + ": " + string.Join("RICS.CC.factions.separator".Translate(), neutrals));

            if (enemies.Any())
                parts.Add("RICS.CC.factions.enemies".Translate() + ": " + string.Join("RICS.CC.factions.separator".Translate(), enemies));

            return string.Join(" | ", parts);
        }
    }

    public class Colonists : ChatCommand
    {
        public override string Name => "colonists";

        public override string Execute(ChatMessageWrapper messageWrapper, string[] args)
        {
            // Get total living free colonists (standard RimWorld count)
            int colonistCount = Current.Game.PlayerHomeMaps
                .Sum(m => m.mapPawns.FreeColonistsSpawnedCount);

            // Get total colony animals
            int animalCount = Current.Game.PlayerHomeMaps
                .Sum(m => m.mapPawns.ColonyAnimals.Count);

            // Count only viewers who have an **assigned living pawn**
            var assignmentManager = Current.Game.GetComponent<GameComponent_PawnAssignmentManager>();
            if (assignmentManager == null)
            {
                // Fallback / safety
                return "RICS.CC.colonists.basic".Translate(colonistCount, animalCount);
            }

            int viewerPawnCount = assignmentManager.GetAllViewerPawns().Count;  // This method already filters out dead pawns

            return "RICS.CC.colonists.withViewers".Translate(colonistCount, viewerPawnCount, animalCount);
        }
    }
    // Slated to be moved to its own command handler
    public class Storage : ChatCommand
    {
        public override string Name => "storage";

        public override string Execute(ChatMessageWrapper messageWrapper, string[] args)
        {
            if (args.Length == 0)
                return "RICS.CC.storage.usage".Translate();

            var map = Current.Game?.CurrentMap;
            if (map == null)
                return "RICS.CC.storage.noMap".Translate();

            // Use the shared parser (most fields are ignored here, but we get clean ItemName)
            var parsed = CommandParserUtility.ParseCommandArguments(
                args,
                allowQuality: true,     // we can ignore it or use it later
                allowMaterial: true,    // we can ignore or use later
                allowSide: false,
                allowQuantity: false    // quantity doesn't make sense for "how many in storage"
            );

            if (parsed.HasError)
            {
                return parsed.Error;
            }

            string searchName = parsed.ItemName;
            if (string.IsNullOrWhiteSpace(searchName))
            {
                return "RICS.CC.storage.noMatch".Translate(searchName);
                // return "No valid item name could be parsed.";
            }

            // Optional: log what was parsed for debugging
            // Logger.Debug($"Storage command parsed: Item='{searchName}', Quality='{parsed.Quality}', Material='{parsed.Material}'");

            // ────────────────────────────────────────────────
            // Collect all things in stockpiles + storage buildings
            // ────────────────────────────────────────────────
            var zoneThings = map.zoneManager.AllZones
                .OfType<Zone_Stockpile>()
                .SelectMany(z => z.Cells)
                .SelectMany(c => c.GetThingList(map));

            var storageThings = map.listerBuildings.AllBuildingsColonistOfClass<Building_Storage>()
                .SelectMany(shelf => shelf.GetSlotGroup().HeldThings ?? Enumerable.Empty<Thing>());

            var allThings = zoneThings.Concat(storageThings).ToList();

            if (!allThings.Any())
                return "RICS.CC.storage.noItemsAtAll".Translate();

            // Find matching ThingDef(s) using flexible matching on defName or label
            var matchingDefs = allThings
                .Select(t => t.def)
                .Distinct()
                .Where(def =>
                    def.defName.IndexOf(searchName, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    (def.label ?? "").IndexOf(searchName, StringComparison.OrdinalIgnoreCase) >= 0)
                .ToList();

            if (!matchingDefs.Any())
                return "RICS.CC.storage.noMatch".Translate(searchName);

            // If multiple defs match, we can either:
            // A) Show all of them (most user-friendly)
            // B) Take the first/best match (simpler)
            // Here we go with A) — show grouped counts

            var results = new System.Text.StringBuilder();

            foreach (var def in matchingDefs.OrderBy(d => d.label))
            {
                int count = allThings
                    .Where(t => t.def == def)
                    .Sum(t => t.stackCount);

                if (count > 0)
                {
                    results.AppendLine("RICS.CC.storage.line".Translate(count.ToString(), def.label) + " | ");
                    // results.AppendLine($"{count}× {def.label}");
                }
            }

            if (results.Length == 0)
            {
                return "RICS.CC.storage.defsButZero".Translate(searchName);
                // return $"Found matching definitions for '{searchName}', but none in storage right now.";
            }

            string header = matchingDefs.Count == 1
                ? "RICS.CC.storage.headerOne".Translate() : "RICS.CC.storage.headerMultiple".Translate(matchingDefs.Count, searchName);
            // : $"Found {matchingDefs.Count} matching item types for '{searchName}':";

            string resultsStr = results.ToString().TrimEnd(' ', '|');
            return header + " " + resultsStr;
        }
    }

    public class ShuffleChildhood : ChatCommand
    {
        public override string Name => "shufflechildhood";

        public override string Execute(ChatMessageWrapper messageWrapper, string[] args)
        {
            return ShuffleChildhoodCommandHandler.HandleShuffledChildhood(messageWrapper, args);
        }
    }

    public class ShuffleAdulthood : ChatCommand
    {
        public override string Name => "shufflechildhood";

        public override string Execute(ChatMessageWrapper messageWrapper, string[] args)
        {
            return ShuffleAdulthoodCommandHandler.HandleShuffledAdulthood(messageWrapper, args);
        }
    }
}
