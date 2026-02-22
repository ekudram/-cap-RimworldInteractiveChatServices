// MilitaryAidCommandHandler.cs - Updated version
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
                if (viewer == null)
                {
                    // return "Error: Could not find your viewer data.";
                    return "RICS.MACH.ViewerDataNotFound".Translate();
                }

                // NEW: Check global cooldowns using the unified system
                var cooldownManager = Current.Game.GetComponent<GlobalCooldownManager>();
                if (cooldownManager != null)
                {
                    Logger.Debug($"=== MILITARY AID COOLDOWN DEBUG ===");
                    Logger.Debug($"Wager: {wager}");

                    // Get command settings for militaryaid command
                    var commandSettings = CommandSettingsManager.GetSettings("militaryaid");

                    // Use the unified cooldown check
                    if (!cooldownManager.CanUseCommand("militaryaid", commandSettings, settings))
                    {
                        // Provide appropriate feedback based on what failed
                        if (!cooldownManager.CanUseGlobalEvents(settings))
                        {
                            int totalEvents = cooldownManager.data.EventUsage.Values.Sum(record => record.CurrentPeriodUses);
                            Logger.Debug($"Global event limit reached: {totalEvents}/{settings.EventsperCooldown}");
                            // return $"❌ Global event limit reached! ({totalEvents}/{settings.EventsperCooldown} used this period)";
                            return "RICS.MACH.GlobalEventLimitReached".Translate(totalEvents, settings.EventsperCooldown);
                        }

                        // Check good event limit specifically
                        if (settings.KarmaTypeLimitsEnabled && !cooldownManager.CanUseEvent("good", settings))
                        {
                            var goodRecord = cooldownManager.data.EventUsage.GetValueOrDefault("good");
                            int goodUsed = goodRecord?.CurrentPeriodUses ?? 0;
                            // string cooldownMessage = $"❌ GOOD event limit reached! ({goodUsed}/{settings.MaxGoodEvents} used this period)";
                            string cooldownMessage = "RICS.MACH.GoodEventLimitReached".Translate(goodUsed, settings.MaxGoodEvents);
                            Logger.Debug($"Good event limit reached: {goodUsed}/{settings.MaxGoodEvents}");
                            return cooldownMessage;
                        }

                        // return $"❌ Military aid command is on cooldown.";
                        return "RICS.MACH.CommandOnCooldown".Translate();
                    }

                    Logger.Debug($"Military aid cooldown check passed");
                }

                if (viewer.Coins < wager)
                {
                    // return $"You need {wager}{currencySymbol} to call for military aid! You have {viewer.Coins}{currencySymbol}.";
                    return "RICS.MACH.InsufficientFunds".Translate(wager, currencySymbol, viewer.Coins, currencySymbol);
                }

                if (!IsGameReadyForMilitaryAid())
                {
                    // return "Game not ready for military aid (no colony, in menu, etc.)";
                    return "RICS.MACH.GameNotReady".Translate();
                }

                var result = TriggerMilitaryAid(messageWrapper.Username, wager);

                if (result.Success)
                {
                    viewer.TakeCoins(wager);
                    viewer.GiveKarma(CalculateKarmaChange(wager));

                    // Record military aid usage for cooldowns ONLY ON SUCCESS
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
                    string letterTitle = "RICS.MACH.LetterTitleMilitaryAidCalled".Translate(messageWrapper.Username);

                    string factionInfo = result.AidingFaction != null
                        ? "RICS.MACH.LetterPartAidingFaction".Translate(
                            result.AidingFaction.Name,
                            result.AidingFaction.PlayerGoodwill)
                        : "";

                    string reinforcementInfo = result.HasReinforcementCount
                        ? "RICS.MACH.LetterPartReinforcementsCount".Translate(result.ReinforcementCount)
                        : "RICS.MACH.LetterPartReinforcementsSoon".Translate();

                    string letterText = "RICS.MACH.LetterTextMilitaryAidCalled".Translate(
                        messageWrapper.Username,
                        wager,
                        currencySymbol,
                        result.Message,
                        factionInfo,
                        reinforcementInfo
                    );

                    MessageHandler.SendGreenLetter(letterTitle, letterText);

                    return result.Message;
                }
                else
                {
                    // return $"{result.Message} No {currencySymbol} were deducted.";
                    return "RICS.MACH.MilitaryAidFailed".Translate(result.Message, currencySymbol);
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error handling military aid command: {ex}");
                MessageHandler.SendFailureLetter("Military Aid Error",
                    $"Error calling military aid: {ex.Message}");
                // return "Error calling military aid. [errorMessage].";
                string errorMessage = ex.Message;
                return "RICS.MACH.MilitaryAidError".Translate(errorMessage);    
            }
        }

        private static MilitaryAidResult TriggerMilitaryAid(string username, int wager)
        {
            var playerMaps = Current.Game.Maps.Where(map => map.IsPlayerHome).ToList();

            if (!playerMaps.Any())
            {
                // return new MilitaryAidResult(false, "No player home maps found.");
                return new MilitaryAidResult(false, "RICS.MACH.NoPlayerHomeMaps".Translate());
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
                            string returnMessage = "RICS.MACH.SendingAid".Translate(parms.faction.Name);
                            return new MilitaryAidResult(
                                true,
                                returnMessage,
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
            // return new MilitaryAidResult(false, "No friendly factions are available to send aid right now.");
            return new MilitaryAidResult(false, "RICS.MACH.NoFriendlyFactions".Translate());
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