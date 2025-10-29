using RimWorld;
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

        public void ExposeData()
        {
            Scribe_Values.Look(ref Enabled, "enabled", false);
            Scribe_Values.Look(ref ChannelName, "channelName", "");
            Scribe_Values.Look(ref BotUsername, "botUsername", "");
            Scribe_Values.Look(ref AccessToken, "accessToken", "");
            Scribe_Values.Look(ref AutoConnect, "autoConnect", false);
            Scribe_Values.Look(ref IsConnected, "isConnected", false);
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
        public bool EnableDebugLogging = false;
        public bool LogAllMessages = true;
        public int MessageCooldownSeconds = 1;

        // economy properties
        public int StartingCoins = 100;
        public int StartingKarma = 100;
        public int BaseCoinReward = 10;
        public int SubscriberExtraCoins = 5;
        public int VipExtraCoins = 3;
        public int ModExtraCoins = 2;
        public int MinKarma = 0;
        public int MaxKarma = 200;
        public int MinutesForActive = 30;
        public string CurrencyName = " 💰 ";

        // Global event settings
        public bool EventCooldownsEnabled = true;
        public int EventCooldownDays = 15;
        public int EventsperCooldown = 5;

        public bool KarmaTypeLimitsEnabled = false;
        public int MaxBadEvents = 3;
        public int MaxGoodEvents = 10;
        public int MaxNeutralEvents = 10;
        public int MaxItemPurchases = 10;

        // Command settings could be added here in the future
        public string Prefix = "!";
        public string BuyPrefix = "$";

        public void ExposeData()
        {
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
            Scribe_Values.Look(ref CurrencyName, "currencyName", " 💰 ");

            // Cooldown settings
            Scribe_Values.Look(ref EventCooldownsEnabled, "eventCooldownsEnabled", true);
            Scribe_Values.Look(ref EventCooldownDays, "eventCooldownDays", 15);
            Scribe_Values.Look(ref EventsperCooldown, "eventsperCooldown", 5);
            Scribe_Values.Look(ref KarmaTypeLimitsEnabled, "karmaTypeLimitsEnabled", false);
            Scribe_Values.Look(ref MaxBadEvents, "maxBadEvents", 3);
            Scribe_Values.Look(ref MaxGoodEvents, "maxGoodEvents", 10);
            Scribe_Values.Look(ref MaxNeutralEvents, "maxNeutralEvents", 10);
            Scribe_Values.Look(ref MaxItemPurchases, "maxItemPurchases", 10);

            Scribe_Values.Look(ref Prefix, "prefix", "!");
            Scribe_Values.Look(ref BuyPrefix, "buyPrefix", "$");
        }
    }

}