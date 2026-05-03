// TwitchService.cs
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
// Service to manage Twitch chat connection and messaging

/*
 * IMPLEMENTATION NOTES:
 * - Twitch role hierarchy dictated by platform API requirements
 * - Karma systems are standard industry practice (non-protectable)
 * - Virtual currency management follows functional necessities
 * - All platform-specific structures follow external constraints
 */

using CAP_ChatInteractive.Incidents;
using CAP_ChatInteractive.Utilities;
using CAP_ChatInteractive.Windows;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TwitchLib.Api;
using TwitchLib.Client;
using TwitchLib.Client.Events;
using TwitchLib.Client.Models;
using TwitchLib.Communication.Clients;
using TwitchLib.Communication.Events;
using TwitchLib.Communication.Models;
using UnityEngine;
using Verse;


namespace CAP_ChatInteractive
{
    public class TwitchService
    {
        private readonly StreamServiceSettings _settings;
        private TwitchClient _client;
        private DateTime _lastMessageTime = DateTime.MinValue;
        private readonly TimeSpan _messageDelay = TimeSpan.FromMilliseconds(100);
        private bool _isConnecting = false;
        private CancellationTokenSource _connectionTimeoutToken;
        private DateTime _lastWhisperReminderTime = DateTime.MinValue;
        private TwitchAPI _helixApi;
        private readonly Dictionary<string, string> _userIdCache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // === Twitch Raids Feature ===
        private readonly Dictionary<string, DateTime> _recentRaiders = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
        private DateTime _lastRaidTriggerTime = DateTime.MinValue;
        private const int RaidDebounceSeconds = 30; // prevent duplicate triggers from the same raid

        // === Twitch Raids Feature - Join System ===
        private readonly List<string> _raidJoinList = new List<string>(20);
        private DateTime _raidStartTime = DateTime.MinValue;
        private const int RaidJoinWindowSeconds = 45; // How long viewers have to join
        private bool _raidJoinWindowActive = false;
        private DateTime _raidJoinStartTime = DateTime.MinValue;

        public bool IsConnected => _client?.IsConnected == true;

        // Events for other parts of your mod to subscribe to
        public event Action<string, string> OnWhisperReceived; // username, message 1.0.17
        public event Action<string, string> OnMessageReceived; // username, message
        public event Action<string> OnConnected;
        public event Action<string> OnDisconnected;

        public TwitchService(StreamServiceSettings settings)
        {
            // Logger.Debug($"TwitchService constructor called with settings: {settings != null}");
            _settings = settings;

            if (_settings != null)
            {
                Logger.Debug($"TwitchService - BotUsername: {_settings.BotUsername}, Channel: {_settings.ChannelName}, TokenLength: {_settings.AccessToken?.Length ?? 0}");
            }
        }

        public void Connect()
        {
            if (_isConnecting || IsConnected)
            {
                Logger.Debug("Already connecting or connected, skipping...");
                return;
            }

            Logger.Twitch("Attempting to connect to Twitch...");
            try
            {
                if (!_settings.CanConnect)
                {
                    Logger.Error("Cannot connect to Twitch: Missing credentials");
                    Messages.Message("Cannot connect to Twitch: Missing credentials", MessageTypeDefOf.NegativeEvent);
                    return;
                }

                _isConnecting = true;

                // Set up connection timeout
                _connectionTimeoutToken = new CancellationTokenSource();
                Task.Delay(15000, _connectionTimeoutToken.Token).ContinueWith(t =>
                {
                    if (t.IsCompleted && !IsConnected && _isConnecting)
                    {
                        Logger.Error("Twitch connection timeout - taking too long to connect");
                        Disconnect();
                        Messages.Message("Twitch connection timeout - check credentials", MessageTypeDefOf.NegativeEvent);
                    }
                });

                InitializeClient();
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to connect to Twitch: {ex.Message}");
                _settings.IsConnected = false;
                _isConnecting = false;
                _connectionTimeoutToken?.Cancel();
                Messages.Message($"Failed to connect to Twitch: {ex.Message}", MessageTypeDefOf.NegativeEvent);
            }
        }

        public void Disconnect()
        {
            try
            {
                _isConnecting = false;
                _connectionTimeoutToken?.Cancel();
                _client?.Disconnect();
                _settings.IsConnected = false;
                // Logger.Twitch("Disconnected from Twitch");
                OnDisconnected?.Invoke(_settings.ChannelName);
            }
            catch (Exception ex)
            {
                Logger.Error($"Error disconnecting from Twitch: {ex.Message}");
            }
        }

        private void InitializeClient()
        {
            Logger.Debug("Initializing Twitch client...");

            // Ensure token has correct format and clean it
            string formattedToken = CleanOAuthToken(_settings.AccessToken);
            // Logger.Debug($"Using token: {formattedToken?.Substring(0, Math.Min(10, formattedToken?.Length ?? 0))}...");

            var credentials = new ConnectionCredentials(_settings.BotUsername, formattedToken);

            // Reset client if exists
            if (_client != null)
            {
                UnsubscribeFromEvents();
                _client.Disconnect();
                _client = null;
            }

            // Use minimal ClientOptions - sometimes simpler is better
            var clientOptions = new ClientOptions
            {
                MessagesAllowedInPeriod = 100,
                ThrottlingPeriod = TimeSpan.FromSeconds(30),
                ReconnectionPolicy = null, // No automatic reconnection
                DisconnectWait = 1000
            };

            var customClient = new WebSocketClient(clientOptions);
            _client = new TwitchClient(customClient);

            _client.Initialize(credentials, _settings.ChannelName?.ToLowerInvariant());

            // NEW: Helix API for private whispers (replaces obsolete SendWhisper)
            InitializeHelixApi(formattedToken);

            SubscribeToEvents();
            _client.Connect();

            Logger.Debug("Twitch client + Helix API (whispers) initialized");
        }

        private void InitializeHelixApi(string accessToken)
        {
            if (string.IsNullOrEmpty(_settings.ClientId))
            {
                Logger.Warning("No ClientId set — whispers will fallback to public chat (add in Twitch tab)");
                return;
            }

            _helixApi = new TwitchAPI();
            _helixApi.Settings.ClientId = _settings.ClientId;
            _helixApi.Settings.AccessToken = accessToken.Replace("oauth:", "");
            Logger.Debug("Helix API ready for private whispers");
        }

        private string CleanOAuthToken(string token)
        {
            if (string.IsNullOrEmpty(token))
                return token;

            // Remove any whitespace
            token = token.Trim();

            // Ensure it starts with oauth:
            if (!token.StartsWith("oauth:"))
            {
                token = "oauth:" + token;
            }

            // Remove any extra characters that might have been copied
            if (token.Contains(" "))
            {
                token = token.Split(' ')[0]; // Take first part only
            }

            // Logger.Debug($"Cleaned token to: {token.Substring(0, Math.Min(15, token.Length))}...");
            return token;
        }

        private void SubscribeToEvents()
        {
            _client.OnConnected += OnClientConnected;
            _client.OnJoinedChannel += OnJoinedChannel;
            _client.OnMessageReceived += OnChatMessageReceived;
            _client.OnWhisperReceived += OnWhisperMessageReceived;
            _client.OnConnectionError += OnConnectionError;
            _client.OnDisconnected += OnClientDisconnected;
            _client.OnError += OnClientError;
            _client.OnIncorrectLogin += OnIncorrectLogin;
            _client.OnUserJoined += OnUserJoined;
            _client.OnUserLeft += OnUserLeft;
            // Twitch Raids detection
            _client.OnRaidNotification += OnRaidNotificationReceived;
        }

        private void UnsubscribeFromEvents()
        {
            if (_client == null) return;

            _client.OnConnected += OnClientConnected;
            _client.OnJoinedChannel += OnJoinedChannel;
            _client.OnMessageReceived += OnChatMessageReceived;
            _client.OnWhisperReceived += OnWhisperMessageReceived;
            _client.OnConnectionError += OnConnectionError;
            _client.OnDisconnected += OnClientDisconnected;
            _client.OnError += OnClientError;
            _client.OnIncorrectLogin += OnIncorrectLogin;
            _client.OnUserJoined += OnUserJoined;
            _client.OnUserLeft += OnUserLeft;
            // Twitch Raids
            _client.OnRaidNotification -= OnRaidNotificationReceived;
        }

        #region Twitch Client Event Handlers

        private void OnClientConnected(object sender, OnConnectedArgs e)
        {
            // Logger.Twitch($"Connected to Twitch IRC as {e.BotUsername}");
            _isConnecting = false;
            _connectionTimeoutToken?.Cancel();

            // The old mod automatically joins channels after connection
            if (!string.IsNullOrEmpty(_settings.ChannelName))
            {
                // Logger.Debug($"Attempting to join channel: {_settings.ChannelName}");
                _client.JoinChannel(_settings.ChannelName.ToLowerInvariant());
            }
        }

        private void OnJoinedChannel(object sender, OnJoinedChannelArgs e)
        {
            var settings = CAPChatInteractiveMod.Instance.Settings.GlobalSettings;
            string modVer = settings.modVersion;
            // NOW we're fully connected and in the channel
            _settings.IsConnected = true;
            _isConnecting = false;
            _connectionTimeoutToken?.Cancel();
            OnConnected?.Invoke(_settings.ChannelName);
            //Logger.Twitch($"SUCCESS: Joined channel: {e.Channel}");
            //Logger.Debug($"Channel join confirmed for: {_settings.ChannelName}");

            // Send connection message if configured
            if (_settings.AutoConnect)
            {
                Task.Delay(1000).ContinueWith(t =>
                {
                    if (IsConnected)
                    {
                        _client.SendMessage(e.Channel, $"[CAP] Rimwold Interactive Chat Service version {modVer} activated!", false);
                    }
                });
            }

            // Causes error on startup with DefOf MessageTypeDefOf because not initialized yet
            // Messages.Message($"Connected to Twitch: {_settings.ChannelName}");
        }

        private void OnChatMessageReceived(object sender, OnMessageReceivedArgs e)
        {
            var message = e.ChatMessage;

            // Check if forceUseWhisper is enabled
            if (_settings.forceUseWhisper)
            {
                // Check if timer is enabled (> 0 seconds)
                if (_settings.forceUseWhisperMessageTimer > 0)
                {
                    // Calculate time since last reminder
                    var timeSinceLastReminder = DateTime.Now - _lastWhisperReminderTime;
                    int reminderIntervalSeconds = _settings.forceUseWhisperMessageTimer;

                    // Send reminder if enough time has passed
                    if (timeSinceLastReminder.TotalSeconds >= reminderIntervalSeconds)
                    {
                        // SendMessage($"Please use whispers for commands. Type: /w {_settings.BotUsername} [command]");
                        _lastWhisperReminderTime = DateTime.Now;
                    }
                }

                // Always ignore commands in public chat when forceUseWhisper is enabled
                // (Don't return early, just skip command processing)
                Logger.Debug($"Ignoring public chat message (forceUseWhisper enabled): {message.Message}");
            }

            // Always create the message wrapper and log/viewer activity
            var messageWrapper = new ChatMessageWrapper(
                username: message.Username,
                message: message.Message,
                platform: "Twitch",
                platformUserId: message.UserId,
                channelId: _settings.ChannelName,
                platformMessage: message,
                customRewardId: message.CustomRewardId,
                bits: message.Bits,
                shouldIgnoreForCommands: _settings.forceUseWhisper, // Add this flag
                isWhisper: false
            );

            LongEventHandler.QueueLongEvent(() =>
            {
                ProcessMessageOnMainThread(messageWrapper);
            }, null, false, null, showExtraUIInfo: false, forceHideUI: true);
        }

        private void OnWhisperMessageReceived(object sender, OnWhisperReceivedArgs e)
        {
            var whisper = e.WhisperMessage;
            Logger.Debug($"Twitch whisper from {whisper.Username}: {whisper.Message}");

            // Create unified whisper wrapper
            var whisperWrapper = new ChatMessageWrapper(
                username: whisper.Username,
                message: whisper.Message,
                platform: "Twitch",
                platformUserId: whisper.UserId,
                channelId: "WHISPER", // Special identifier for whispers
                platformMessage: whisper,
                isWhisper: true
            );

            // Use RimWorld's thread-safe event handler
            LongEventHandler.QueueLongEvent(() =>
            {
                ProcessWhisperOnMainThread(whisperWrapper);
            }, null, false, null, showExtraUIInfo: false, forceHideUI: true);
        }

        private void OnRaidNotificationReceived(object sender, OnRaidNotificationArgs e)
        {
            if (!CAPChatInteractiveMod.Instance.Settings.GlobalSettings.TwitchRaidsEnabled)
            {
                Logger.Debug("Twitch raid detected but feature disabled in settings.");
                return;
            }

            var raid = e.RaidNotification;
            string raiderChannel = raid?.MsgParamLogin ?? raid?.Login ?? e.Channel ?? "UnknownRaider";

            int viewerCount = 0;
            if (int.TryParse(raid?.MsgParamViewerCount, out int parsedCount))
                viewerCount = parsedCount;

            Logger.Twitch($"TWITCH RAID DETECTED! From: {raiderChannel} with ~{viewerCount} viewers");

            StartRaidJoinCollection(raiderChannel, viewerCount);
        }

        public void StartRaidJoinCollection(string raiderName, int viewerCount)
        {
            _raidJoinList.Clear();
            _raidJoinList.Add(raiderName);                    // Streamer is always first
            _raidJoinStartTime = DateTime.Now;
            _raidJoinWindowActive = true;

            Logger.Twitch($"[RAID JOIN] Window opened for @{raiderName} | Initial list count: {_raidJoinList.Count} | Window active: {_raidJoinWindowActive}");

            // Show nice countdown dialog
            Find.WindowStack.Add(new Dialog_TwitchRaidJoin(raiderName, viewerCount));

            SendMessage($"@{raiderName} just raided us! Type !joinraid or just hang out — anyone who joins in the next {RaidJoinWindowSeconds} seconds will be in the raid!");
        }

        public void ProcessJoinRaidCommand(string username)
        {
            if (!_raidJoinWindowActive)
            {
                SendMessage($"@{username} → No raid join window is currently open.");
                return;
            }

            if (_raidJoinList.Contains(username, StringComparer.OrdinalIgnoreCase))
            {
                SendMessage($"@{username} → You're already in the raid!");
                return;
            }

            _raidJoinList.Add(username);
            SendMessage($"@{username} joined the raid! ({_raidJoinList.Count} total)");
        }

        /// <summary>
        /// Called by Dialog_TwitchRaidJoin when the streamer clicks "START RAID NOW!"
        /// </summary>
        public void TriggerRaidNow(string raiderName, int totalRaiders)
        {
            if (!_raidJoinWindowActive && _raidJoinList.Count == 0)
            {
                _raidJoinList.Add(raiderName);
                Logger.Twitch($"[RAID JOIN] Fallback - added raider @{raiderName} to empty list");
            }

            _raidJoinWindowActive = false;

            Logger.Twitch($"[RAID JOIN] TriggerRaidNow called | Final list count BEFORE clear: {_raidJoinList.Count} | Names: {string.Join(", ", _raidJoinList)}");

            // Pass all collected names to the worker
            IncidentWorker_TwitchRaid.CurrentRaidUsernames.Clear();
            IncidentWorker_TwitchRaid.CurrentRaidUsernames.AddRange(_raidJoinList);

            Logger.Twitch($"[RAID JOIN] Manual raid start triggered by streamer. Final raiders: {IncidentWorker_TwitchRaid.CurrentRaidUsernames.Count}");
            Logger.Twitch($"[RAID JOIN] Final names being sent to raid: {string.Join(", ", IncidentWorker_TwitchRaid.CurrentRaidUsernames)}");

            TryTriggerRimWorldRaid(raiderName, totalRaiders);
        }

        private void TryTriggerRimWorldRaid(string raiderName, int viewerCount)
        {
            if (Current.ProgramState != ProgramState.Playing)
            {
                Logger.Debug($"Raid from {raiderName} queued - no game loaded yet.");
                return;
            }

            Map map = Find.CurrentMap;
            if (map == null || !map.IsPlayerHome)
            {
                Logger.Debug($"Raid from {raiderName} skipped - player not on home map.");
                SendMessage($"[RICS] Twitch raid from @{raiderName} detected, but colony is traveling - raid postponed!");
                return;
            }

            var globalSettings = CAPChatInteractiveMod.Instance.Settings.GlobalSettings;

            if (viewerCount < globalSettings.TwitchRaidMinRaiders)
            {
                Logger.Debug($"Raid from {raiderName} ignored - only {viewerCount} viewers (min required: {globalSettings.TwitchRaidMinRaiders})");
                return;
            }

            // Debounce
            if ((DateTime.Now - _lastRaidTriggerTime).TotalSeconds < RaidDebounceSeconds)
            {
                Logger.Debug("Raid debounce active - ignoring duplicate trigger.");
                return;
            }
            _lastRaidTriggerTime = DateTime.Now;

            // === Use the join list (this was the main bug) ===
            IncidentWorker_TwitchRaid.CurrentRaidUsernames.Clear();
            IncidentWorker_TwitchRaid.CurrentRaidUsernames.AddRange(_raidJoinList);

            // === Build base parameters ===
            IncidentParms parms = StorytellerUtility.DefaultParmsNow(IncidentCategoryDefOf.ThreatBig, map);
            parms.forced = true;

            float basePoints = parms.points;
            float raidBonus = Mathf.Clamp(viewerCount * 80f, 200f, 4000f);
            parms.points = Mathf.Min(basePoints + raidBonus, basePoints * 3.5f);

            // Force human faction only
            parms.faction = Find.FactionManager.RandomEnemyFaction(allowHidden: false, allowDefeated: false, allowNonHumanlike: false);
            if (parms.faction == null)
                parms.faction = Find.FactionManager.RandomEnemyFaction();

            // Smart arrival mode
            parms.raidArrivalMode = PawnsArrivalModeDefOf.EdgeWalkIn;
            if (!HasValidEdgeForRaid(map))
            {
                parms.raidArrivalMode = Rand.Chance(0.6f) ? PawnsArrivalModeDefOf.EdgeDrop : PawnsArrivalModeDefOf.CenterDrop;
            }

            parms.raidStrategy = RaidStrategyDefOf.ImmediateAttack;

            var twitchRaidWorker = new IncidentWorker_TwitchRaid { def = IncidentDefOf.RaidEnemy };

            bool success = twitchRaidWorker.TryExecute(parms);

            string raidMessage = success
                ? $"[RICS] INCOMING TWITCH RAID! @{raiderName} brought ~{viewerCount} raiders to the colony!"
                : $"[RICS] Twitch raid from @{raiderName} detected, but storyteller refused the raid.";

            Messages.Message(raidMessage, success ? MessageTypeDefOf.ThreatBig : MessageTypeDefOf.NegativeEvent);
            SendMessage(raidMessage);

            if (success)
            {
                Find.LetterStack.ReceiveLetter(
                    "RICS.TwitchRaid.LetterLabel".Translate(raiderName, viewerCount),
                    "RICS.TwitchRaid.LetterText".Translate(raiderName, viewerCount),
                    LetterDefOf.ThreatBig
                );
            }

            Logger.Twitch($"Raid triggered with {IncidentWorker_TwitchRaid.CurrentRaidUsernames.Count} named raiders.");
        }

        /// <summary>
        /// Simple check if EdgeWalkIn is likely to work on this map.
        /// Returns false on very small maps, islands, or certain Anomaly setups with no usable edge cells.
        /// </summary>
        private static bool HasValidEdgeForRaid(Map map)
        {
            if (map == null) return false;

            // Quick heuristic: if the map is tiny (< 50x50) or has very few edge cells, drop is safer
            if (map.Size.x < 50 || map.Size.z < 50)
                return false;

            // More accurate check - see if we can find at least a few valid edge cells
            int validEdgeCells = 0;
            foreach (IntVec3 cell in map.AllCells)
            {
                if (cell.x == 0 || cell.x == map.Size.x - 1 || cell.z == 0 || cell.z == map.Size.z - 1)
                {
                    if (cell.Walkable(map))
                    {
                        validEdgeCells++;
                        if (validEdgeCells >= 8) return true;   // good enough
                    }
                }
            }

            return validEdgeCells >= 4;   // at least a small edge available
        }

        public void UpdateRaidJoinTimer()
        {
            if (!_raidJoinWindowActive) return;

            float secondsLeft = RaidJoinWindowSeconds - (float)(DateTime.Now - _raidJoinStartTime).TotalSeconds;
            Logger.Twitch($"[RAID JOIN] Timer tick - seconds left: {secondsLeft:F1} | Current list count: {_raidJoinList.Count}");

            if ((DateTime.Now - _raidJoinStartTime).TotalSeconds >= RaidJoinWindowSeconds)
            {
                _raidJoinWindowActive = false;

                Logger.Twitch($"[RAID JOIN] Timer expired - final list: {string.Join(", ", _raidJoinList)}");

                IncidentWorker_TwitchRaid.CurrentRaidUsernames.Clear();
                IncidentWorker_TwitchRaid.CurrentRaidUsernames.AddRange(_raidJoinList);

                TryTriggerRimWorldRaid(_raidJoinList[0], _raidJoinList.Count);
            }
        }

        #region Twitch Client Event Handlers

        private void ProcessMessageOnMainThread(ChatMessageWrapper messageWrapper)
        {
            try
            {
                // Update viewer activity
                Viewers.UpdateViewerActivity(messageWrapper);

                // Log message for chat display
                ChatMessageLogger.AddMessage(messageWrapper.Username, messageWrapper.Message, "Twitch");

                // Notify subscribers about the message
                OnMessageReceived?.Invoke(messageWrapper.Username, messageWrapper.Message);

                // Check if we should process commands for this message
                if (!messageWrapper.ShouldIgnoreForCommands)
                {
                    ChatCommandProcessor.ProcessMessage(messageWrapper);
                }
                else
                {
                    Logger.Debug($"Skipping command processing (ShouldIgnoreForCommands=true): {messageWrapper.Message}");
                }

                // Example: Check for first-time chatters
                if (messageWrapper.PlatformMessage is ChatMessage twitchMessage &&
                    twitchMessage.IsFirstMessage)
                {
                    SendMessage($"Welcome to the stream, @{messageWrapper.Username}! Type !help for available commands.");
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error processing Twitch message: {ex.Message}");
            }
        }

        private void ProcessWhisperOnMainThread(ChatMessageWrapper whisperWrapper)
        {
            try
            {
                Logger.Debug($"Processing whisper from {whisperWrapper.Username}: {whisperWrapper.Message}");

                // Log whisper for display (check if AddMessage supports isWhisper parameter)
                ChatMessageLogger.AddMessage(
                    whisperWrapper.Username,
                    whisperWrapper.Message,
                    "Twitch"
                );

                // Check service-specific whisper settings
                bool shouldProcessWhisper = _settings.useWhisperForCommands;

                if (shouldProcessWhisper)
                {
                    Logger.Debug($"Processing whisper as command: {whisperWrapper.Message}");
                    ChatCommandProcessor.ProcessMessage(whisperWrapper);
                }
                else
                {
                    Logger.Debug($"Whisper commands disabled for Twitch service, ignoring whisper from {whisperWrapper.Username}");
                }

                // Fire the whisper received event
                OnWhisperReceived?.Invoke(whisperWrapper.Username, whisperWrapper.Message);
            }
            catch (Exception ex)
            {
                Logger.Error($"Error processing Twitch whisper: {ex.Message}");
            }
        }

        private void OnConnectionError(object sender, OnConnectionErrorArgs e)
        {
            Logger.Error($"Twitch connection error: {e.Error}");
            _settings.IsConnected = false;
            _isConnecting = false;
            _connectionTimeoutToken?.Cancel();
            Messages.Message($"Twitch connection error: {e.Error}", MessageTypeDefOf.NegativeEvent);
        }

        private void OnClientDisconnected(object sender, OnDisconnectedEventArgs e)
        {
            Logger.Warning("Disconnected from Twitch");
            _settings.IsConnected = false;
            _isConnecting = false;
            _connectionTimeoutToken?.Cancel();
            OnDisconnected?.Invoke(_settings.ChannelName);
        }

        private void OnClientError(object sender, OnErrorEventArgs e)
        {
            Logger.Error($"Twitch client error: {e.Exception.Message}");
            _isConnecting = false;
            _connectionTimeoutToken?.Cancel();
        }

        private void OnIncorrectLogin(object sender, OnIncorrectLoginArgs e)
        {
            Logger.Error($"Twitch login failed: Invalid credentials - Exception: {e.Exception.Message}");
            _settings.IsConnected = false;
            _isConnecting = false;
            _connectionTimeoutToken?.Cancel();

            // More detailed error message
            string errorMsg = "Twitch login failed. Possible issues:\n" +
                             "• OAuth token expired or invalid\n" +
                             "• Bot username doesn't match token account\n" +
                             "• Token not a 'Bot Chat Token'\n" +
                             "• Try regenerating token at twitchtokengenerator.com";

            Messages.Message(errorMsg, MessageTypeDefOf.NegativeEvent);
        }

        public static void OnUserJoined(object sender, OnUserJoinedArgs e)
        {
            var service = CAPChatInteractiveMod.Instance.TwitchService;
            if (service != null)
            {
                service.ProcessUserJoined(e.Username);
            }
        }

        public void ProcessUserJoined(string username)
        {
            Logger.Twitch($"[RAID JOIN] OnUserJoined fired for @{username} | Window active: {_raidJoinWindowActive} | Current list count: {_raidJoinList.Count}");

            if (!_raidJoinWindowActive)
            {
                Logger.Twitch($"[RAID JOIN] Ignored @{username} - window is no longer active");
                return;
            }

            if (_raidJoinList.Contains(username, StringComparer.OrdinalIgnoreCase))
            {
                Logger.Twitch($"[RAID JOIN] @{username} already in list - skipping");
                return;
            }

            _raidJoinList.Add(username);
            Logger.Twitch($"[RAID JOIN] SUCCESS - Added @{username} to raid list | New total: {_raidJoinList.Count}");
        }

        public string ProcessUserJoinRaidCommmand(ChatMessageWrapper message)
            {
            if (!_raidJoinWindowActive)
            {
                return $"RICS.CC.joinraid.noRaid".Translate(message.Username);
                //return $"@{message.Username} → No raid join window is currently open.";
            }
            if (_raidJoinList.Contains(message.Username, StringComparer.OrdinalIgnoreCase))
            {
                return $"RICS.CC.joinraid.alreadyJoined".Translate(message.Username);
                // return $"@{message.Username} → You're already in the raid!";
            }
            _raidJoinList.Add(message.Username);
            return $"RICS.CC.joinraid.success".Translate(message.Username, _raidJoinList.Count);
            // return $"@{message.Username} joined the raid! ({_raidJoinList.Count} total)";
        }

        public List<string> GetCurrentRaidJoinList()
        {
            return new List<string>(_raidJoinList);
        }

        public static void OnUserLeft(object sender, OnUserLeftArgs e)
        {
            Logger.Message($"User left: {e.Username}  {sender}");
            // Additional logic for mods to hook into
        }

        #endregion

        public void SendMessage(string message)
        {
            if (!IsConnected || string.IsNullOrEmpty(_settings.ChannelName))
                return;

            // Split message if needed
            var messages = MessageSplitter.SplitMessage(message, "twitch");

            foreach (var msg in messages)
            {
                SendSingleMessage(msg);

                // Add a small delay between messages to ensure they're sent in order
                if (messages.Count > 1)
                {
                    System.Threading.Thread.Sleep(200); // 200ms delay between messages
                }
            }
        }


        /// <summary>
        /// Sends private whisper via Helix API (required since Twitch deprecated IRC whispers).
        /// Falls back to public @reply if ClientId missing or API fails.
        /// </summary>
        /// <summary>
        /// Sends private whisper via Helix API (replaces obsolete IRC whisper).
        /// Uses newRecipient: false (allows 10k chars). Our messages are always < 500 chars so safe.
        /// Falls back to public @reply on any error (no ClientId, rate limit, missing scope, etc.).
        /// </summary>
        /// <summary>
        /// Sends private whisper via Helix API (replaces obsolete IRC whisper).
        /// Uses newRecipient: false (allows 10k chars). Our messages are always < 500 chars so safe.
        /// Falls back to public @reply on any error (no ClientId, rate limit, missing scope, etc.).
        ///
        /// Sends private whisper via Helix API (replaces obsolete IRC whisper).
        /// 
        /// OFFICIAL TWITCH HELIX WHISPER LIMITS (March 2026):
        /// 
        /// 1. Daily unique recipients: MAX 40 per day (resets midnight Pacific Time)
        ///    → This is the hard limit that matters most for big streamers.
        /// 
        /// 2. Rate limits:
        ///    • 3 whispers per second
        ///    • 100 whispers per minute
        /// 
        /// 3. Message length:
        ///    • First whisper to a user today = 500 characters max
        ///    • Repeat whispers to same user = 10,000 characters max
        ///    (We use newRecipient: false so we always get the higher limit)
        /// 
        /// 4. Other requirements:
        ///    • Bot account MUST have a verified phone number
        ///    • Twitch can silently drop whispers (returns 204 success anyway)
        ///    • Verified bots get NO higher limits
        /// 
        /// RICS impact: We only whisper in response to viewer commands, so we stay well under limits.
        /// Fallback to public @reply is already built-in and safe.
        /// 
        /// Source: https://dev.twitch.tv/docs/chat/whispers/
        /// </summary>
        public async Task SendWhisperAsync(string username, string message)
        {
            if (!IsConnected || string.IsNullOrEmpty(username) || _helixApi == null)
            {
                SendMessage($"@{username} {message}");
                return;
            }

            try
            {
                string fromUserId = await GetBotUserIdAsync();
                string toUserId = await GetUserIdAsync(username);

                if (string.IsNullOrEmpty(fromUserId) || string.IsNullOrEmpty(toUserId))
                {
                    Logger.Warning($"Could not resolve User ID for whisper to {username} — falling back to public");
                    SendMessage($"@{username} {message}");
                    return;
                }

                await _helixApi.Helix.Whispers.SendWhisperAsync(
                    fromUserId,
                    toUserId,
                    message,
                    newRecipient: false
                );

                _lastMessageTime = DateTime.Now;
                Logger.Debug($"✅ Private whisper sent to @{username}");
            }
            catch (Exception ex)
            {
                Logger.Error($"Helix whisper failed to @{username}: {ex.Message} — falling back to public");
                SendMessage($"@{username} {message}");
            }
        }

        private async Task<string> GetBotUserIdAsync()
        {
            return await GetUserIdAsync(_settings.BotUsername ?? _settings.ChannelName);
        }

        private async Task<string> GetUserIdAsync(string login)
        {
            if (_userIdCache.TryGetValue(login.ToLowerInvariant(), out var cached)) return cached;

            try
            {
                var response = await _helixApi.Helix.Users.GetUsersAsync(logins: new List<string> { login });
                var user = response.Users.FirstOrDefault();
                if (user != null)
                {
                    _userIdCache[login.ToLowerInvariant()] = user.Id;
                    return user.Id;
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"User ID lookup failed for {login}: {ex.Message}");
            }
            return null;
        }



        private void SendSingleMessage(string message)
        {
            // Rate limiting
            var now = DateTime.Now;
            if (now - _lastMessageTime < _messageDelay)
            {
                System.Threading.Thread.Sleep(_messageDelay - (now - _lastMessageTime));
            }

            try
            {
                _client.SendMessage(_settings.ChannelName.ToLowerInvariant(), message);
                _lastMessageTime = DateTime.Now;
                Logger.Debug($"Sent Twitch message: {message}");
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to send Twitch message: {ex.Message}");
            }
        }
    }
    #endregion


}