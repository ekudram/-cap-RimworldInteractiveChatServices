// CAPChatInteractiveSettings.cs
// Copyright (c) Captolamia
// This file is part of CAP Chat Interactive. RICS - RimWorld Interactive Chat System
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


using RimWorld;
using System.Collections.Generic;
using Verse;

namespace CAP_ChatInteractive
{
    public class CAPChatInteractiveSettings : ModSettings
    {
        public StreamServiceSettings TwitchSettings = new StreamServiceSettings();
        public StreamServiceSettings YouTubeSettings = new StreamServiceSettings();
        public StreamServiceSettings KickSettings = new StreamServiceSettings();

        public CAPGlobalChatSettings GlobalSettings = new CAPGlobalChatSettings();

        // Defensive constructor — guarantees NO nulls even on first load or old config.xml
        public CAPChatInteractiveSettings()
        {
            TwitchSettings ??= new StreamServiceSettings();
            YouTubeSettings ??= new StreamServiceSettings();
            KickSettings ??= new StreamServiceSettings();
            GlobalSettings ??= new CAPGlobalChatSettings();
        }

        public override void ExposeData()
        {

            base.ExposeData();
            // Ensure settings objects are not null before loading/saving to prevent errors on first load or with old config.xml
            TwitchSettings ??= new StreamServiceSettings();
            YouTubeSettings ??= new StreamServiceSettings();
            KickSettings ??= new StreamServiceSettings();
            GlobalSettings ??= new CAPGlobalChatSettings();
            // Now we can safely call Scribe_Deep.Look on each settings object
            Scribe_Deep.Look(ref TwitchSettings, "twitchSettings");
            Scribe_Deep.Look(ref YouTubeSettings, "youtubeSettings");
            Scribe_Deep.Look(ref KickSettings, "kickSettings");
            Scribe_Deep.Look(ref GlobalSettings, "globalSettings");
            
        }
    }

    public class StreamServiceSettings : IExposable
    {
        public bool Enabled = false;
        public string ChannelName = "";
        public string BotUsername = "";
        public string AccessToken = "";
        public string ClientId = ""; // Used by
        public bool AutoConnect = false;
        public bool IsConnected = false;
        public bool suspendFeedback = false;
        public bool useWhisperForCommands = true;  // 1.0.17 addition
        public bool forceUseWhisper = false;  // 1.0.17 addition
        public int forceUseWhisperMessageTimer = 300; // 1.0.17 addition  if 0 do not use timer

        // === KICK.COM SPECIFIC (official OAuth 2.1 + Pusher WS) ===
        // All services now reuse the SAME generic fields below.
        // Twitch/YouTube ignore ClientSecret/RefreshToken.
        // Kick (and future Discord/Steam) use them via KickSettings.
        // This keeps the class clean and fully extensible.
        public string ClientSecret = "";   // Required for Kick OAuth 2.1 (dev.kick.com)
        public string RefreshToken = "";   // Future-proof for long sessions (Kick + future services)

        public void ExposeData()
        {
            Scribe_Values.Look(ref Enabled, "enabled", false);
            Scribe_Values.Look(ref ChannelName, "channelName", "");
            Scribe_Values.Look(ref BotUsername, "botUsername", "");
            Scribe_Values.Look(ref AccessToken, "accessToken", "");
            Scribe_Values.Look(ref ClientId, "clientId", ""); // Used by Twitch (Helix) + Kick (OAuth)
            Scribe_Values.Look(ref AutoConnect, "autoConnect", false);
            Scribe_Values.Look(ref IsConnected, "isConnected", false);
            Scribe_Values.Look(ref suspendFeedback, "suspendFeedback", false);
            Scribe_Values.Look(ref useWhisperForCommands, "useWhisperForCommands", true);  // 1.0.17 addition
            Scribe_Values.Look(ref forceUseWhisper, "forceUseWhisper", false);  // 1.0.17 addition (fixed typo)
            Scribe_Values.Look(ref forceUseWhisperMessageTimer, "forceUseWhisperMessageTimer", 300); // 1.0.17 addition

            // Kick fields — now generic (saved for all services, unused ones stay default "")
            Scribe_Values.Look(ref ClientSecret, "clientSecret", "");
            Scribe_Values.Look(ref RefreshToken, "refreshToken", "");
        }

        public bool CanConnect
        {
            get
            {
                bool canConnect = !string.IsNullOrEmpty(AccessToken) &&
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
        public string modVersion = "1.34";  // Current mod version WE DONT SAVE THIS! Used in history control
        public string modVersionSaved = "";
        public string priceListUrl = "https://github.com/ekudram/RICS-Pricelist";
        public bool EnableDebugLogging = false;
        public bool LogAllMessages = true;
        public int MessageCooldownSeconds = 1;

        // Live Chat window persistence
        public float LiveChatWindowX = -1f;     // -1 = use default middle-left on first open
        public float LiveChatWindowY = -1f;
        public float LiveChatWindowWidth = 400f;
        public float LiveChatWindowHeight = 300f;

        // Economy properties
        public bool StoreCommandsEnabled = true;   // ← global kill-switch for buying/interacting commands
        public int StartingCoins = 100;
        public float StartingKarma = 100f;   // Now float for precision (affects coin multiplier heavily)
        public int BaseCoinReward = 10;
        public int SubscriberExtraCoins = 5;
        public int VipExtraCoins = 3;
        public int ModExtraCoins = 2;

        // === Enhanced Karma System (more tunable + stronger punishment) ===
        public float MinKarma = 0f;
        public float MaxKarma = 200f;           // Default cap (was 999) — players were sitting at 200 forever
        public float KarmaDecayRate = 0.01f;    // % of CURRENT karma lost per decay tick 
        public int KarmaDecayIntervalMinutes = 30; // NEW: how often decay runs (prevents permanent max karma)
        public float KarmaMinDecay = 0f;        // Minimum absolute loss per decay 
        public float KarmaPerStoreItem = 0.01f; // Slightly reduced gain from store spam
        public float KarmaMinDecayFloor = 100f;     // Minium Karma that will decay too

        public float KarmaGainPerGoodEvent = 5f;
        public float KarmaGainPerNeutralEvent = 1f;
        public float KarmaLossPerBadEvent = 12f;     // MUCH stronger punishment (was 1f)
        public float KarmaLossPerDoomEvent = 25f;    // Stronger doom penalty (was 2f)

        /// <summary>
        /// Multiplier applied to the event's price (in coins) to calculate additional karma.
        /// Good/Neutral → + (price * multiplier)
        /// Bad/Doom     → - (price * multiplier)
        /// Default 0.05f = +5 karma per 100 coins of event price (balanced default).
        /// Set to 0 to disable price-based karma entirely.
        /// This is capped at 0f and 5f in the UI to prevent extreme values, but can be set higher via config for fun or testing (e.g. 0.1f = +10 karma per 100 coins, 1.0f = +100 karma per 100 coins, etc.).
        /// </summary>
        public float KarmaEventPriceMultiplier = 0.05f; // Multiplier for how much karma gain/loss you get per event based on its price (default 1.0 = no change, 0.5 = half karma, 2.0 = double karma, etc.) as a percentage.

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
        public int MinPassionWager = 500;
        public int MaxPassionWager = 1000;
        public float BasePassionSuccessChance = 15.0f; // 15% base chance
        public float MaxPassionSuccessChance = 60.0f; // 60% max chance
        // Additional customizable passion gambling parameters (defaults = old hardcoded behavior)
        public float PassionWagerBonusPer100 = 1.0f;      // % bonus per 100 coins wagered
        public float MaxPassionWagerBonus = 30.0f;        // cap on wager bonus
        public float CriticalSuccessRatio = 0.2f;         // crit success = baseSuccess * this
        public float MaxCriticalSuccessChance = 5.0f;
        public float CriticalFailBaseChance = 10.0f;
        public float CriticalFailReductionFactor = 0.1f;  // subtract (baseSuccess * this) from base crit-fail
        public float MinCriticalFailChance = 2.0f;
        public float CritSuccessUpgradeVsNewChance = 0.5f;   // upgrade existing vs gain new on crit success
        public float CritFailLoseVsWrongChance = 0.6f;       // lose passion vs gain useless on crit failure
        public float TargetedCritFailAffectTargetChance = 0.7f; // targeted crit-fail: hit chosen skill vs random

        // Backstory Settings

        public int ChildhoodWager = 1000;
        public int AdulthoodWager = 1000;


        // Channel Points settings
        public bool ChannelPointsEnabled = true;
        public bool ShowChannelPointsDebugMessages = false;
        public List<ChannelPoints_RewardSettings> RewardSettings = new List<ChannelPoints_RewardSettings>();

        // === Twitch Raids feature (Phase 1) ===
        public bool TwitchRaidsEnabled = false;           // global kill-switch
        public bool TwitchRaidsOnlyRaiders = true;     // only twitch raiders and !joinraid in the raid 
        public int TwitchRaidDelayMinutes = 1;            // delay before triggering in-game raid (minutes)
        public int TwitchRaidMinRaiders = 5;              // anti-troll protection (default 5)

        public CAPGlobalChatSettings()
        {
            // List is already initialized by the field initializer above.
            // Do NOT add the example reward here anymore.
            // The PostLoadInit logic below will add it only for brand new configs.
        }

        // In CAPChatInteractiveSettings.cs, inside CAPGlobalChatSettings.ExposeData()
        public void ExposeData()
        {
            Scribe_Values.Look(ref modVersionSaved, "modVersionSaved", "");
            Scribe_Values.Look(ref priceListUrl, "priceListUrl", "https://github.com/ekudram/RICS-Pricelist");
            Scribe_Values.Look(ref EnableDebugLogging, "enableDebugLogging", false);
            Scribe_Values.Look(ref LogAllMessages, "logAllMessages", true);
            Scribe_Values.Look(ref MessageCooldownSeconds, "messageCooldownSeconds", 1);

            // Live Chat window position/size persistence
            Scribe_Values.Look(ref LiveChatWindowX, "liveChatWindowX", -1f);
            Scribe_Values.Look(ref LiveChatWindowY, "liveChatWindowY", -1f);
            Scribe_Values.Look(ref LiveChatWindowWidth, "liveChatWindowWidth", 400f);
            Scribe_Values.Look(ref LiveChatWindowHeight, "liveChatWindowHeight", 300f);

            // New economy settings
            Scribe_Values.Look(ref StartingCoins, "startingCoins", 100);
            Scribe_Values.Look(ref StartingKarma, "startingKarma", 100f);  // float now
            Scribe_Values.Look(ref BaseCoinReward, "baseCoinReward", 10);
            Scribe_Values.Look(ref SubscriberExtraCoins, "subscriberExtraCoins", 5);
            Scribe_Values.Look(ref VipExtraCoins, "vipExtraCoins", 3);
            Scribe_Values.Look(ref ModExtraCoins, "modExtraCoins", 2);

            // Enhanced Karma System
            Scribe_Values.Look(ref MinKarma, "minKarma", 0f);
            Scribe_Values.Look(ref MaxKarma, "maxKarma", 200f);           // FIXED default to match field initializer (was 200f)
            Scribe_Values.Look(ref KarmaDecayRate, "karmaDecayRate", 0.01f);
            Scribe_Values.Look(ref KarmaDecayIntervalMinutes, "karmaDecayIntervalMinutes", 30);
            Scribe_Values.Look(ref KarmaMinDecay, "karmaMinDecay", 0f);
            Scribe_Values.Look(ref KarmaPerStoreItem, "karmaPerStoreItem", 0.01f);
            Scribe_Values.Look(ref KarmaMinDecayFloor, "minDecayKarma", 100f);

            Scribe_Values.Look(ref KarmaLossPerBadEvent, "karmaLossPerBadEvent", 12f);
            Scribe_Values.Look(ref KarmaGainPerGoodEvent, "karmaGainPerGoodEvent", 5f);
            Scribe_Values.Look(ref KarmaGainPerNeutralEvent, "karmaGainPerNeutralEvent", 1f);
            Scribe_Values.Look(ref KarmaLossPerDoomEvent, "karmaLossPerDoomEvent", 25f);
            Scribe_Values.Look(ref KarmaEventPriceMultiplier, "karmaEventPriceMultiplier", 0.05f);

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
            Scribe_Values.Look(ref RequireResearch, "requireResearch", true);  // FIXED: now matches field initializer (was false)

            // Passion Command
            Scribe_Values.Look(ref MinPassionWager, "minPassionWager", 500);  // FIXED: now matches field initializer (was 10)
            Scribe_Values.Look(ref MaxPassionWager, "maxPassionWager", 1000);
            Scribe_Values.Look(ref BasePassionSuccessChance, "basePassionSuccessChance", 15.0f);
            Scribe_Values.Look(ref MaxPassionSuccessChance, "maxPassionSuccessChance", 60.0f);

            Scribe_Values.Look(ref PassionWagerBonusPer100, "passionWagerBonusPer100", 1.0f);
            Scribe_Values.Look(ref MaxPassionWagerBonus, "maxPassionWagerBonus", 30.0f);
            Scribe_Values.Look(ref CriticalSuccessRatio, "criticalSuccessRatio", 0.2f);
            Scribe_Values.Look(ref MaxCriticalSuccessChance, "maxCriticalSuccessChance", 5.0f);
            Scribe_Values.Look(ref CriticalFailBaseChance, "criticalFailBaseChance", 10.0f);
            Scribe_Values.Look(ref CriticalFailReductionFactor, "criticalFailReductionFactor", 0.1f);
            Scribe_Values.Look(ref MinCriticalFailChance, "minCriticalFailChance", 2.0f);
            Scribe_Values.Look(ref CritSuccessUpgradeVsNewChance, "critSuccessUpgradeVsNewChance", 0.5f);
            Scribe_Values.Look(ref CritFailLoseVsWrongChance, "critFailLoseVsWrongChance", 0.6f);
            Scribe_Values.Look(ref TargetedCritFailAffectTargetChance, "targetedCritFailAffectTargetChance", 0.7f);

            // === Surgery Command Settings — now fully persisted ===
            // Previously only the first two costs had Scribe lines. The remaining 7 costs + all 9 Allow* flags
            // were declared with proper defaults but never saved/loaded. This caused them to reset on every
            // world load or mod settings reload. All lines below use the exact field initializer values
            // so old saves and new installs behave identically.
            Scribe_Values.Look(ref SurgeryGenderSwapCost, "surgeryGenderSwapCost", 1000);
            Scribe_Values.Look(ref SurgeryBodyChangeCost, "surgeryBodyChangeCost", 800);
            Scribe_Values.Look(ref SurgerySterilizeCost, "surgerySterilizeCost", 400);
            Scribe_Values.Look(ref SurgeryIUDCost, "surgeryIUDCost", 250);
            Scribe_Values.Look(ref SurgeryVasReverseCost, "surgeryVasReverseCost", 500);
            Scribe_Values.Look(ref SurgeryTerminateCost, "surgeryTerminateCost", 300);
            Scribe_Values.Look(ref SurgeryHemogenCost, "surgeryHemogenCost", -100);
            Scribe_Values.Look(ref SurgeryTransfusionCost, "surgeryTransfusionCost", 200);
            Scribe_Values.Look(ref SurgeryMiscBiotechCost, "surgeryMiscBiotechCost", 350);

            Scribe_Values.Look(ref SurgeryAllowGenderSwap, "surgeryAllowGenderSwap", true);
            Scribe_Values.Look(ref SurgeryAllowBodyChange, "surgeryAllowBodyChange", true);
            Scribe_Values.Look(ref SurgeryAllowSterilize, "surgeryAllowSterilize", true);
            Scribe_Values.Look(ref SurgeryAllowIUD, "surgeryAllowIUD", true);
            Scribe_Values.Look(ref SurgeryAllowVasReverse, "surgeryAllowVasReverse", true);
            Scribe_Values.Look(ref SurgeryAllowTerminate, "surgeryAllowTerminate", true);
            Scribe_Values.Look(ref SurgeryAllowHemogen, "surgeryAllowHemogen", true);
            Scribe_Values.Look(ref SurgeryAllowTransfusion, "surgeryAllowTransfusion", true);
            Scribe_Values.Look(ref SurgeryAllowMiscBiotech, "surgeryAllowMiscBiotech", true);

            // Backstory Settings 
            Scribe_Values.Look(ref ChildhoodWager, "childhoodWager", 1000);
            Scribe_Values.Look(ref AdulthoodWager, "adulthoodWager", 1000);

            // Channel Points settings
            Scribe_Values.Look(ref ChannelPointsEnabled, "channelPointsEnabled", true);
            Scribe_Values.Look(ref ShowChannelPointsDebugMessages, "showChannelPointsDebugMessages", false);
            // === Channel Points Reward Settings ===
            // Special handling so the "Example Reward" only appears for new users
            // and never duplicates or disappears on existing saves.
            if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                RewardSettings.Clear();
            }

            Scribe_Collections.Look(ref RewardSettings, "rewardSettings", LookMode.Deep);

            // Only add the example reward for completely new configs (first install or empty list)
            if (Scribe.mode == LoadSaveMode.PostLoadInit && (RewardSettings == null || RewardSettings.Count == 0))
            {
                RewardSettings ??= new List<ChannelPoints_RewardSettings>();
                RewardSettings.Add(new ChannelPoints_RewardSettings(
                    "Example Reward",
                    "",
                    "300",
                    false,
                    true
                ));
            }

            // Twitch Raids (new in 1.31+)
            Scribe_Values.Look(ref TwitchRaidsEnabled, "twitchRaidsEnabled", false);
            Scribe_Values.Look(ref TwitchRaidsOnlyRaiders, "twitchRaidsOnlyRaiders", true);
            Scribe_Values.Look(ref TwitchRaidDelayMinutes, "twitchRaidDelayMinutes", 1);
            Scribe_Values.Look(ref TwitchRaidMinRaiders, "twitchRaidMinRaiders", 5);

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