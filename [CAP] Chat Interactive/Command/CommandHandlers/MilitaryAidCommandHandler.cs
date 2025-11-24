// MilitaryAidCommandHandler.cs - Cleaned up version
// Copyright (c) Captolamia. All rights reserved.
// Licensed under the AGPLv3 License. See LICENSE file in the project root for full license information.
//
// Handles the !militaryaid command to call for military reinforcements in exchange for in-game currency.
using CAP_ChatInteractive.Commands.Cooldowns;
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
        public static string HandleMilitaryAid(ChatMessageWrapper messageWrapper, int wager)
        {
            try
            {
                var settings = CAPChatInteractiveMod.Instance.Settings.GlobalSettings;
                var currencySymbol = settings.CurrencyName?.Trim() ?? "¢";

                var viewer = Viewers.GetViewer(messageWrapper);

                // NEW: Check global cooldowns for military aid (always "good" karma type)
                var cooldownManager = Current.Game.GetComponent<GlobalCooldownManager>();
                if (cooldownManager != null)
                {
                    Logger.Debug($"=== MILITARY AID COOLDOWN DEBUG ===");
                    Logger.Debug($"Wager: {wager}");

                    // First check global event limit (if enabled)
                    if (settings.EventCooldownsEnabled && !cooldownManager.CanUseGlobalEvents(settings))
                    {
                        int totalEvents = cooldownManager.data.EventUsage.Values.Sum(record => record.CurrentPeriodUses);
                        Logger.Debug($"Global event limit reached: {totalEvents}/{settings.EventsperCooldown}");
                        MessageHandler.SendFailureLetter("Military Aid Blocked",
                            $"{messageWrapper.Username} tried to call military aid but global limit reached\n\n{totalEvents}/{settings.EventsperCooldown} events used");
                        return $"❌ Global event limit reached! ({totalEvents}/{settings.EventsperCooldown} used this period)";
                    }

                    // Then check good event limit (if enabled) - military aid is always "good"
                    if (settings.KarmaTypeLimitsEnabled)
                    {
                        if (!cooldownManager.CanUseEvent("good", settings))
                        {
                            var goodRecord = cooldownManager.data.EventUsage.GetValueOrDefault("good");
                            int goodUsed = goodRecord?.CurrentPeriodUses ?? 0;
                            string cooldownMessage = $"❌ GOOD event limit reached! ({goodUsed}/{settings.MaxGoodEvents} used this period)";
                            Logger.Debug($"Good event limit reached: {goodUsed}/{settings.MaxGoodEvents}");
                            MessageHandler.SendFailureLetter("Military Aid Blocked",
                                $"{messageWrapper.Username} tried to call military aid but good event limit reached\n\n{goodUsed}/{settings.MaxGoodEvents} good events used");
                            return cooldownMessage;
                        }
                    }

                    Logger.Debug($"Military aid cooldown check passed");
                }

                if (viewer.Coins < wager)
                {
                    MessageHandler.SendFailureLetter("Military Aid Failed",
                        $"{messageWrapper.Username} doesn't have enough {currencySymbol} for military aid\n\nNeeded: {wager}{currencySymbol}, Has: {viewer.Coins}{currencySymbol}");
                    return $"You need {wager}{currencySymbol} to call for military aid! You have {viewer.Coins}{currencySymbol}.";
                }

                if (!IsGameReadyForMilitaryAid())
                {
                    MessageHandler.SendFailureLetter("Military Aid Failed",
                        $"{messageWrapper.Username} tried to call for military aid but the game isn't ready");
                    return "Game not ready for military aid (no colony, in menu, etc.)";
                }

                var result = TriggerMilitaryAid(messageWrapper.Username, wager);

                if (result.Success)
                {
                    viewer.TakeCoins(wager);
                    viewer.GiveKarma(CalculateKarmaChange(wager));

                    // Record military aid usage for cooldowns
                    if (cooldownManager != null)
                    {
                        cooldownManager.RecordEventUse("good"); // Military aid is always good events
                        Logger.Debug($"Recorded military aid usage as good event");

                        // Log current state after recording
                        var goodRecord = cooldownManager.data.EventUsage.GetValueOrDefault("good");
                        if (goodRecord != null)
                        {
                            Logger.Debug($"Current good event usage: {goodRecord.CurrentPeriodUses}");
                        }
                    }

                    // Build detailed letter using the result data
                    string factionInfo = result.AidingFaction != null ?
                        $"\n\nAiding Faction: {result.AidingFaction.Name}" +
                        $"\nGoodwill: {result.AidingFaction.PlayerGoodwill}"
                        : "";

                    string reinforcementInfo = result.HasReinforcementCount ?
                        $"\nReinforcements: {result.ReinforcementCount} troops" :
                        "\nReinforcements: Arriving soon";

                    MessageHandler.SendGreenLetter(
                        $"Military Aid Called by {messageWrapper.Username}",
                        $"{messageWrapper.Username} has called for military reinforcements!\n\nCost: {wager}{currencySymbol}\n{result.Message}{factionInfo}{reinforcementInfo}"
                    );

                    return result.Message;
                }
                else
                {
                    MessageHandler.SendFailureLetter("Military Aid Failed",
                        $"{messageWrapper.Username} failed to call for military aid\n\n{result.Message}");
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