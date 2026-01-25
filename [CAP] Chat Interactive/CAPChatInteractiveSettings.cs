// CAPChatInteractiveSettings.cs
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
// Global Settings classes for CAP Chat Interactive mod
// including per-streaming-service settings and global chat settings.


using System.Collections.Generic;
using Verse;

namespace CAP_ChatInteractive
{
    public class CAPChatInteractiveSettings : ModSettings
    {
        public StreamServiceSettings TwitchSettings = new StreamServiceSettings();
        public StreamServiceSettings YouTubeSettings = new StreamServiceSettings();
        public CAPGlobalChatSettings GlobalSettings = new CAPGlobalChatSettings();

        public override void ExposeData()
        {
            Scribe_Deep.Look(ref TwitchSettings, "twitchSettings");
            Scribe_Deep.Look(ref YouTubeSettings, "youtubeSettings");
            Scribe_Deep.Look(ref GlobalSettings, "globalSettings");
        }
    }

    public class StreamServiceSettings : IExposable
    {
        public bool Enabled = false;
        public string ChannelName = "";
        public string BotUsername = "";
        public string AccessToken = "";
        public bool AutoConnect = false;
        public bool IsConnected = false;
        public bool suspendFeedback = false;

        public void ExposeData()
        {
            Scribe_Values.Look(ref Enabled, "enabled", false);
            Scribe_Values.Look(ref ChannelName, "channelName", "");
            Scribe_Values.Look(ref BotUsername, "botUsername", "");
            Scribe_Values.Look(ref AccessToken, "accessToken", "");
            Scribe_Values.Look(ref AutoConnect, "autoConnect", false);
            Scribe_Values.Look(ref IsConnected, "isConnected", false);
            Scribe_Values.Look(ref suspendFeedback,"suspendFeedback",false);
        }

        public bool CanConnect
        {
            get
            {
                bool canConnect = !string.IsNullOrEmpty(BotUsername) &&
                                 !string.IsNullOrEmpty(AccessToken) &&
                                 !string.IsNullOrEmpty(ChannelName);

                //Logger.Debug($"CanConnect check - BotUsername: {!string.IsNullOrEmpty(BotUsername)}, " +
                //$"AccessToken: {!string.IsNullOrEmpty(AccessToken)}, " +
                //$"ChannelName: {!string.IsNullOrEmpty(ChannelName)}, " +
                // $"Result: {canConnect}");

                return canConnect;
            }
        }
    }

    public class CAPGlobalChatSettings : IExposable
    {
        // Existing properties...
        public string modVersion = "1.0.16b";  // Current mod version WE DONT SAVE THIS!
        public string modVersionSaved = "";
        public string priceListUrl = "https://github.com/ekudram/RICS-Pricelist";
        public bool EnableDebugLogging = false;
        public bool LogAllMessages = true;
        public int MessageCooldownSeconds = 1;

        // economy properties
        public bool StoreCommandsEnabled  = true;   // ← global kill-switch for buying/interacting commands
        public int StartingCoins = 100;
        public int StartingKarma = 100;
        public int BaseCoinReward = 10;
        public int SubscriberExtraCoins = 5;
        public int VipExtraCoins = 3;
        public int ModExtraCoins = 2;
        public int MinKarma = 0;
        public int MaxKarma = 999;
        public int MinutesForActive = 30;
        public int MaxTraits = 4;
        public string CurrencyName = " 💰 ";



        // Global event settings
        public bool EventCooldownsEnabled = true;
        public int EventCooldownDays = 5;
        public int EventsperCooldown = 25; // # of events per Cooldowndays 
        public bool KarmaTypeLimitsEnabled = false;
        public int MaxBadEvents = 3;
        public int MaxGoodEvents = 10;
        public int MaxNeutralEvents = 10;
        public int MaxItemPurchases = 50;

        // Event cooldown tracking
        public int EventsTriggeredThisPeriod = 0;
        public int LastEventTick = 0;
        public int CooldownPeriodStartTick = 0;

        // Event display settings
        public bool ShowUnavailableEvents = true;

        // Pawn queue settings
        public int PawnOfferTimeoutSeconds = 300; // 5 minutes default



        // Command settings could be added here in the future
        public string Prefix = "!";
        public string BuyPrefix = "$";

        // Lootbox settings
        public IntRange LootBoxRandomCoinRange = new IntRange(250, 750);
        public int LootBoxesPerDay = 1;
        public bool LootBoxShowWelcomeMessage = true;
        public bool LootBoxForceOpenAllAtOnce = false;


        // Quality settings
        public bool AllowAwfulQuality = true;
        public bool AllowPoorQuality = true;
        public bool AllowNormalQuality = true;
        public bool AllowGoodQuality = true;
        public bool AllowExcellentQuality = true;
        public bool AllowMasterworkQuality = true;
        public bool AllowLegendaryQuality = true;
        // multiplyiers
        public float AwfulQuality = 0.5f;
        public float PoorQuality = 0.75f;
        public float NormalQuality = 1.0f;
        public float GoodQuality = 1.5f;
        public float ExcellentQuality = 2.0f;
        public float MasterworkQuality = 3.0f;
        public float LegendaryQuality = 5.0f;

        // Research settings
        public bool RequireResearch = true;

        // Command settings that need to be global for now

        // Surgery Command Settings
        public int SurgeryGenderSwapCost = 1000;
        public int SurgeryBodyChangeCost = 800;
        public int SurgerySterilizeCost = 400;
        public int SurgeryIUDCost = 250;
        public int SurgeryVasReverseCost = 500;
        public int SurgeryTerminateCost = 300;
        public int SurgeryHemogenCost = -100;
        public int SurgeryTransfusionCost = 200;
        public int SurgeryMiscBiotechCost = 350;

        public bool SurgeryAllowGenderSwap = true;
        public bool SurgeryAllowBodyChange = true;
        public bool SurgeryAllowSterilize = true;
        public bool SurgeryAllowIUD = true;
        public bool SurgeryAllowVasReverse = true;
        public bool SurgeryAllowTerminate = true;
        public bool SurgeryAllowHemogen = true;
        public bool SurgeryAllowTransfusion = true;
        public bool SurgeryAllowMiscBiotech = true;

        // Passion Settings
        public int MinPassionWager = 10;
        public int MaxPassionWager = 1000;
        public float BasePassionSuccessChance = 15.0f; // 15% base chance
        public float MaxPassionSuccessChance = 60.0f; // 60% max chance

        // Channel Points settings
        public bool ChannelPointsEnabled = true;
        public bool ShowChannelPointsDebugMessages = false;
        public List<ChannelPoints_RewardSettings> RewardSettings = new List<ChannelPoints_RewardSettings>();

        public CAPGlobalChatSettings()
        {
            RewardSettings = new List<ChannelPoints_RewardSettings>();
            // Optionally add a default reward
            RewardSettings.Add(new ChannelPoints_RewardSettings(
                "Example Reward",
                "",
                "300",
                false,
                true
            ));
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref modVersionSaved, "modVersionSaved", "");
            Scribe_Values.Look(ref priceListUrl, "priceListUrl", "https://github.com/ekudram/RICS-Pricelist");
            Scribe_Values.Look(ref EnableDebugLogging, "enableDebugLogging", false);
            Scribe_Values.Look(ref LogAllMessages, "logAllMessages", true);
            Scribe_Values.Look(ref MessageCooldownSeconds, "messageCooldownSeconds", 1);

            // New economy settings
            Scribe_Values.Look(ref StartingCoins, "startingCoins", 100);
            Scribe_Values.Look(ref StartingKarma, "startingKarma", 100);
            Scribe_Values.Look(ref BaseCoinReward, "baseCoinReward", 10);
            Scribe_Values.Look(ref SubscriberExtraCoins, "subscriberExtraCoins", 5);
            Scribe_Values.Look(ref VipExtraCoins, "vipExtraCoins", 3);
            Scribe_Values.Look(ref ModExtraCoins, "modExtraCoins", 2);
            Scribe_Values.Look(ref MinKarma, "minKarma", 0);
            Scribe_Values.Look(ref MaxKarma, "maxKarma", 200);
            Scribe_Values.Look(ref MinutesForActive, "minutesForActive", 30);
            Scribe_Values.Look(ref MaxTraits, "maxTraits", 4);
            Scribe_Values.Look(ref CurrencyName, "currencyName", " 💰 ");
            Scribe_Values.Look(ref StoreCommandsEnabled, "storeCommandsEnabled", true);  // For !togglestore command

            // Cooldown settings
            Scribe_Values.Look(ref EventCooldownsEnabled, "eventCooldownsEnabled", true);
            Scribe_Values.Look(ref EventCooldownDays, "eventCooldownDays", 5);
            Scribe_Values.Look(ref EventsperCooldown, "eventsperCooldown", 25);
            Scribe_Values.Look(ref KarmaTypeLimitsEnabled, "karmaTypeLimitsEnabled", false);
            Scribe_Values.Look(ref MaxBadEvents, "maxBadEvents", 3);
            Scribe_Values.Look(ref MaxGoodEvents, "maxGoodEvents", 10);
            Scribe_Values.Look(ref MaxNeutralEvents, "maxNeutralEvents", 10);
            Scribe_Values.Look(ref MaxItemPurchases, "maxItemPurchases", 50);
            Scribe_Values.Look(ref PawnOfferTimeoutSeconds, "pawnOfferTimeoutSeconds", 300);
            Scribe_Values.Look(ref EventsTriggeredThisPeriod, "eventsTriggeredThisPeriod", 0);
            Scribe_Values.Look(ref LastEventTick, "lastEventTick", 0);
            Scribe_Values.Look(ref CooldownPeriodStartTick, "cooldownPeriodStartTick", 0);
            Scribe_Values.Look(ref ShowUnavailableEvents, "showUnavailableEvents", true);

            Scribe_Values.Look(ref Prefix, "prefix", "!");
            Scribe_Values.Look(ref BuyPrefix, "buyPrefix", "$");

            // lootbox settings
            Scribe_Values.Look(ref LootBoxRandomCoinRange, "lootBoxRandomCoinRange", new IntRange(250, 750));
            Scribe_Values.Look(ref LootBoxesPerDay, "lootBoxesPerDay", 1);
            Scribe_Values.Look(ref LootBoxShowWelcomeMessage, "lootBoxShowWelcomeMessage", true);
            Scribe_Values.Look(ref LootBoxForceOpenAllAtOnce, "lootBoxForceOpenAllAtOnce", false);

            // Quality settings
            Scribe_Values.Look(ref AllowAwfulQuality, "allowAwfulQuality", true);
            Scribe_Values.Look(ref AllowPoorQuality, "allowPoorQuality", true);
            Scribe_Values.Look(ref AllowNormalQuality, "allowNormalQuality", true);
            Scribe_Values.Look(ref AllowGoodQuality, "allowGoodQuality", true);
            Scribe_Values.Look(ref AllowExcellentQuality, "allowExcellentQuality", true);
            Scribe_Values.Look(ref AllowMasterworkQuality, "allowMasterworkQuality", true);
            Scribe_Values.Look(ref AllowLegendaryQuality, "allowLegendaryQuality", true);
            Scribe_Values.Look(ref AwfulQuality, "awfulQuality", 0.5f);
            Scribe_Values.Look(ref PoorQuality, "poorQuality", 0.75f);
            Scribe_Values.Look(ref NormalQuality, "normalQuality", 1.0f);
            Scribe_Values.Look(ref GoodQuality, "goodQuality", 1.5f);
            Scribe_Values.Look(ref ExcellentQuality, "excellentQuality", 2.0f);
            Scribe_Values.Look(ref MasterworkQuality, "masterworkQuality", 3.0f);
            Scribe_Values.Look(ref LegendaryQuality, "legendaryQuality", 5.0f);

            // Research settings
            Scribe_Values.Look(ref RequireResearch, "requireResearch", false);

            // Passion Command
            Scribe_Values.Look(ref MinPassionWager, "minPassionWager", 10);
            Scribe_Values.Look(ref MaxPassionWager, "maxPassionWager", 1000);
            Scribe_Values.Look(ref BasePassionSuccessChance, "basePassionSuccessChance", 15.0f);
            Scribe_Values.Look(ref MaxPassionSuccessChance, "maxPassionSuccessChance", 60.0f);

            // Surgery Command
            Scribe_Values.Look(ref SurgeryGenderSwapCost, "surgeryGenderSwapCost", 1000);
            Scribe_Values.Look(ref SurgeryBodyChangeCost, "surgeryBodyChangeCost", 800);

            // Channel Points settings
            Scribe_Values.Look(ref ChannelPointsEnabled, "channelPointsEnabled", true);
            Scribe_Values.Look(ref ShowChannelPointsDebugMessages, "showChannelPointsDebugMessages", false);
            Scribe_Collections.Look(ref RewardSettings, "rewardSettings", LookMode.Deep);
        }
    }

    public class ChannelPoints_RewardSettings : IExposable
    {
        public string RewardName = "";
        public string RewardUUID = "";
        public string CoinsToAward = "300";
        public bool AutomaticallyCaptureUUID = false;
        public bool Enabled = true;

        public ChannelPoints_RewardSettings()
        {
            RewardName = "";
            RewardUUID = "";
            CoinsToAward = "300";
            AutomaticallyCaptureUUID = false;
            Enabled = true;
        }

        public ChannelPoints_RewardSettings(string rewardName, string rewardUUID, string coinsToAward, bool autoCapture = false, bool enabled = true)
        {
            RewardName = rewardName;
            RewardUUID = rewardUUID;
            CoinsToAward = coinsToAward;
            AutomaticallyCaptureUUID = autoCapture;
            Enabled = enabled;
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref RewardName, "RewardName", "");
            Scribe_Values.Look(ref RewardUUID, "RewardUUID", "");
            Scribe_Values.Look(ref CoinsToAward, "CoinsToAward", "300");
            Scribe_Values.Look(ref AutomaticallyCaptureUUID, "AutomaticallyCaptureUUID", false);
            Scribe_Values.Look(ref Enabled, "RewardEnabled", true);
        }
    }
}