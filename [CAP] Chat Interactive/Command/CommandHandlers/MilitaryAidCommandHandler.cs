// MilitaryAidCommandHandler.cs
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
// !militaryaid — call friendly raid reinforcements for coins
using CAP_ChatInteractive.Commands.Cooldowns;
using LudeonTK;
using RimWorld;
using System;
using System.Linq;
using UnityEngine;
using Verse;

namespace CAP_ChatInteractive.Commands.CommandHandlers
{
    public static class MilitaryAidCommandHandler
    {
        public static string HandleMilitaryAid(ChatMessageWrapper messageWrapper, int wager)
        {
            try
            {
                var settings = CAPChatInteractiveMod.Instance?.Settings?.GlobalSettings;
                if (settings == null)
                    return "RICS.MACH.GameNotReady".Translate();

                var currencySymbol = settings.CurrencyName?.Trim() ?? "¢";

                var viewer = Viewers.GetViewer(messageWrapper);
                if (viewer == null)
                    return "RICS.MACH.ViewerDataNotFound".Translate();

                var cooldownManager = Current.Game?.GetComponent<GlobalCooldownManager>();
                if (cooldownManager != null)
                {
                    var commandSettings = CommandSettingsManager.GetSettings("militaryaid");

                    if (!cooldownManager.CanUseCommand("militaryaid", commandSettings, settings))
                    {
                        if (!cooldownManager.CanUseGlobalEvents(settings))
                        {
                            int totalEvents = cooldownManager.data.EventUsage.Values.Sum(record => record.CurrentPeriodUses);
                            return "RICS.MACH.GlobalEventLimitReached".Translate(totalEvents, settings.EventsperCooldown);
                        }

                        if (settings.KarmaTypeLimitsEnabled && !cooldownManager.CanUseEvent("good", settings))
                        {
                            int goodUsed = 0;
                            if (cooldownManager.data.EventUsage.TryGetValue("good", out var goodRecord) && goodRecord != null)
                                goodUsed = goodRecord.CurrentPeriodUses;
                            return "RICS.MACH.GoodEventLimitReached".Translate(goodUsed, settings.MaxGoodEvents);
                        }

                        return "RICS.MACH.CommandOnCooldown".Translate();
                    }
                }

                if (viewer.Coins < wager)
                    return "RICS.MACH.InsufficientFunds".Translate(wager, currencySymbol, viewer.Coins);

                if (!IsGameReadyForMilitaryAid())
                    return "RICS.MACH.GameNotReady".Translate();

                var result = TriggerMilitaryAid(messageWrapper.Username, wager);

                if (!result.Success)
                    return "RICS.MACH.MilitaryAidFailed".Translate(result.Message, currencySymbol);

                viewer.TakeCoins(wager);
                viewer.GiveKarma(CalculateKarmaChange(wager, settings));

                // Cooldown only on success
                cooldownManager?.RecordEventUse("good");

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
                    reinforcementInfo);

                MessageHandler.SendGreenLetter(letterTitle, letterText);
                return result.Message;
            }
            catch (Exception ex)
            {
                Logger.Error($"[MilitaryAid] Error handling military aid command: {ex}");
                return "RICS.MACH.MilitaryAidErrorGeneric".Translate();
            }
        }

        private static MilitaryAidResult TriggerMilitaryAid(string username, int wager)
        {
            var playerMaps = Current.Game?.Maps?.Where(map => map.IsPlayerHome).ToList();
            if (playerMaps == null || !playerMaps.Any())
                return new MilitaryAidResult(false, "RICS.MACH.NoPlayerHomeMaps".Translate());

            foreach (var map in playerMaps)
            {
                try
                {
                    var parms = StorytellerUtility.DefaultParmsNow(IncidentCategoryDefOf.Misc, map);
                    parms.forced = true;
                    parms.points *= CalculateMilitaryAidMultiplier(wager);

                    var incident = new IncidentWorker_CallForAid();
                    incident.def = IncidentDefOf.RaidFriendly;

                    if (incident.CanFireNow(parms) && incident.TryExecute(parms) && parms.faction != null)
                    {
                        string returnMessage = "RICS.MACH.SendingAid".Translate(parms.faction.Name);
                        return new MilitaryAidResult(true, returnMessage, parms.faction);
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error($"[MilitaryAid] Error on map {map}: {ex}");
                }
            }

            return new MilitaryAidResult(false, "RICS.MACH.NoFriendlyFactions".Translate());
        }

        private static bool IsGameReadyForMilitaryAid()
        {
            return Current.Game != null &&
                   Current.ProgramState == ProgramState.Playing &&
                   Current.Game.Maps.Any(map => map.IsPlayerHome);
        }

        /// <summary>
        /// Positive karma for GiveKarma. Base good-event gain + price scaling + wager scale.
        /// </summary>
        private static float CalculateKarmaChange(int wager, CAPGlobalChatSettings settings)
        {
            if (settings == null)
                return Mathf.Max(3f, wager / 300f);

            float baseGain = settings.KarmaGainPerGoodEvent;
            float priceBasedGain = wager * settings.KarmaEventPriceMultiplier;
            float totalGain = baseGain + priceBasedGain;
            float wagerScale = Mathf.Clamp(wager / 1500f, 0.5f, 3.0f);
            totalGain *= wagerScale;

            return Mathf.Max(totalGain, 1f);
        }

        /// <summary>
        /// Incident points scale: 300 coins ≈ 1.0x baseline; clamped 0.7x–2.2x.
        /// </summary>
        private static float CalculateMilitaryAidMultiplier(int wager)
        {
            float normalized = wager / 300f;
            return Mathf.Clamp(normalized, 0.7f, 2.2f);
        }

        [DebugAction("CAP", "Test Military Aid", allowedGameStates = AllowedGameStates.Playing)]
        public static void DebugTestMilitaryAid()
        {
            if (Current.Game == null || !Current.Game.Maps.Any(m => m.IsPlayerHome))
            {
                Logger.Message("[MilitaryAid] No player home maps for test.");
                return;
            }

            var testUser = new ChatMessageWrapper("DebugUser", "Test message", "DebugPlatform");
            string result = HandleMilitaryAid(testUser, 1500);
            Logger.Message($"[MilitaryAid] Test result: {result}");
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
            ReinforcementCount = reinforcementCount;
        }
    }
}
