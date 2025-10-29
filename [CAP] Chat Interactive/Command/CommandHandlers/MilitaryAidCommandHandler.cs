// MilitaryAidCommandHandler.cs - Cleaned up version
using CAP_ChatInteractive.Incidents;
using LudeonTK;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace CAP_ChatInteractive.Commands.CommandHandlers
{
    public static class MilitaryAidCommandHandler
    {
        public static string HandleMilitaryAid(ChatMessageWrapper user, int wager)
        {
            try
            {
                var settings = CAPChatInteractiveMod.Instance.Settings.GlobalSettings;
                var currencySymbol = settings.CurrencyName?.Trim() ?? "¢";

                var viewer = Viewers.GetViewer(user.Username);

                if (viewer.Coins < wager)
                {
                    MessageHandler.SendFailureLetter("Military Aid Failed",
                        $"{user.Username} doesn't have enough {currencySymbol} for military aid\n\nNeeded: {wager}{currencySymbol}, Has: {viewer.Coins}{currencySymbol}");
                    return $"You need {wager}{currencySymbol} to call for military aid! You have {viewer.Coins}{currencySymbol}.";
                }

                if (!IsGameReadyForMilitaryAid())
                {
                    MessageHandler.SendFailureLetter("Military Aid Failed",
                        $"{user.Username} tried to call for military aid but the game isn't ready");
                    return "Game not ready for military aid (no colony, in menu, etc.)";
                }

                var result = TriggerMilitaryAid(user.Username, wager);

                if (result.Success)
                {
                    viewer.TakeCoins(wager);
                    viewer.GiveKarma(CalculateKarmaChange(wager));

                    // Build detailed letter using the result data
                    string factionInfo = result.AidingFaction != null ?
                        $"\n\nAiding Faction: {result.AidingFaction.Name}" +
                        $"\nGoodwill: {result.AidingFaction.PlayerGoodwill}"
                        : "";

                    string reinforcementInfo = result.HasReinforcementCount ?
                        $"\nReinforcements: {result.ReinforcementCount} troops" :
                        "\nReinforcements: Arriving soon";

                    MessageHandler.SendGreenLetter(
                        $"Military Aid Called by {user.Username}",
                        $"{user.Username} has called for military reinforcements!\n\nCost: {wager}{currencySymbol}\n{result.Message}{factionInfo}{reinforcementInfo}"
                    );

                    return result.Message;
                }
                else
                {
                    MessageHandler.SendFailureLetter("Military Aid Failed",
                        $"{user.Username} failed to call for military aid\n\n{result.Message}");
                    return $"{result.Message} No {currencySymbol} were deducted.";
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error handling military aid command: {ex}");
                MessageHandler.SendFailureLetter("Military Aid Error",
                    $"Error calling military aid: {ex.Message}");
                return "Error calling military aid. Please try again.";
            }
        }

        private static MilitaryAidResult TriggerMilitaryAid(string username, int wager)
        {
            var playerMaps = Current.Game.Maps.Where(map => map.IsPlayerHome).ToList();

            if (!playerMaps.Any())
            {
                return new MilitaryAidResult(false, "No player home maps found.");
            }

            foreach (var map in playerMaps)
            {
                try
                {
                    var parms = StorytellerUtility.DefaultParmsNow(IncidentCategoryDefOf.Misc, map);
                    parms.forced = true;

                    var incident = new IncidentWorker_CallForAid();
                    incident.def = IncidentDefOf.RaidFriendly;

                    if (incident.CanFireNow(parms))
                    {
                        bool executed = incident.TryExecute(parms);
                        if (executed && parms.faction != null)
                        {
                            // Logger.Debug($"Military aid triggered successfully for {username} on map {map}");

                            // For now, don't try to count - just indicate success
                            // The actual count might not be immediately available
                            return new MilitaryAidResult(
                                true,
                                $"{parms.faction.Name} are sending reinforcements to help!",
                                parms.faction
                            // Don't include count for now
                            );
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error($"Error triggering military aid on map {map}: {ex}");
                }
            }

            return new MilitaryAidResult(false, "No friendly factions are available to send aid right now.");
        }

        private static bool IsGameReadyForMilitaryAid()
        {
            return Current.Game != null &&
                   Current.ProgramState == ProgramState.Playing &&
                   Current.Game.Maps.Any(map => map.IsPlayerHome);
        }

        private static int CalculateKarmaChange(int wager)
        {
            return (int)(wager / 1500f * 5);
        }

        [DebugAction("CAP", "Test Military Aid", allowedGameStates = AllowedGameStates.Playing)]
        public static void DebugTestMilitaryAid()
        {
            if (Current.Game == null || !Current.Game.Maps.Any(m => m.IsPlayerHome))
            {
                Logger.Message("No player home maps available for testing military aid.");
                return;
            }

            var testUser = new ChatMessageWrapper("DebugUser", "Test message", "DebugPlatform");
            string result = HandleMilitaryAid(testUser, 1500);
            Logger.Message($"Military Aid Test Result: {result}");
        }
    }

    public class MilitaryAidResult
    {
        public bool Success { get; }
        public string Message { get; }
        public Faction AidingFaction { get; }
        public int ReinforcementCount { get; }
        public bool HasReinforcementCount => ReinforcementCount >= 0;

        public MilitaryAidResult(bool success, string message, Faction aidingFaction = null, int reinforcementCount = -1)
        {
            Success = success;
            Message = message;
            AidingFaction = aidingFaction;
            ReinforcementCount = reinforcementCount; // -1 means unknown
        }
    }
}