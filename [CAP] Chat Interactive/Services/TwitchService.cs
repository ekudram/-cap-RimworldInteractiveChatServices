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
        private const int RaidDebounceSeconds = 30; // prevent duplicate raid triggers (global) or per-raider notifs

        // === Twitch Raids Feature - Join System ===
        // Supports multiple simultaneous raids by merging into one collection window + combined raid
        // Twitch IRC OnUserJoined (JOIN) is often late or missing; chat auto-add is the reliable fallback.
        private readonly List<string> _raidJoinList = new List<string>(30);
        private bool _raidJoinWindowActive = false;
        private DateTime _raidJoinStartTime = DateTime.MinValue;
        private string _primaryRaiderName = null;
        // Diagnostic counters for end-of-window summary (reset when a new window opens)
        private int _raidJoinCountFromIrc;
        private int _raidJoinCountFromChat;
        private int _raidJoinCountFromCommand;

        /// <summary>
        /// Public read-only view of whether a raid join window is currently active.
        /// Used by GameComponentTick to avoid polling UpdateRaidJoinTimer() on every throttled tick
        /// when no raid has occurred (eliminates the main source of stutter when the feature is enabled but idle).
        /// </summary>
        public bool IsRaidJoinWindowActive => _raidJoinWindowActive;

        /// <summary>
        /// Configured join-collection window in seconds (default 240). Twitch can delay JOIN ~2+ min.
        /// Clamped 60–360 so bad saves cannot break the feature.
        /// </summary>
        private int GetRaidJoinWindowSeconds()
        {
            int seconds = CAPChatInteractiveMod.Instance?.Settings?.GlobalSettings?.TwitchRaidJoinWindowSeconds ?? 240;
            return Mathf.Clamp(seconds, 60, 360);
        }

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
            if (_isConnecting)
            {
                Logger.Debug("Already connecting - skipping duplicate request");
                return;
            }

            if (IsConnected)
            {
                Logger.Debug("Already connected - skipping");
                return;
            }

            Logger.Twitch("Attempting to connect to Twitch...");
            try
            {
                if (_settings == null || !_settings.CanConnect)
                {
                    Logger.Error("Cannot connect to Twitch: Missing credentials or settings null");
                    Messages.Message("Cannot connect to Twitch: Missing credentials", MessageTypeDefOf.NegativeEvent);
                    return;
                }

                _isConnecting = true;

                // Clean up any previous stale state
                _connectionTimeoutToken?.Cancel();
                _connectionTimeoutToken = new CancellationTokenSource();

                // Improved timeout with better cleanup
                _connectionTimeoutToken.Token.Register(() =>
                {
                    if (_isConnecting && !IsConnected)
                    {
                        Logger.Error("Twitch connection timeout - cleaning up state");
                        Disconnect();  // This now safely resets everything
                    }
                });

                Task.Delay(15000, _connectionTimeoutToken.Token).ContinueWith(t =>
                {
                    if (!t.IsCanceled && _isConnecting && !IsConnected)
                    {
                        Logger.Error("Twitch connection timeout after 15s");
                        Disconnect();
                        Messages.Message("Twitch connection timeout - check credentials / internet", MessageTypeDefOf.NegativeEvent);
                    }
                });

                // Force client reset if it exists
                if (_client != null)
                {
                    try
                    {
                        SafeUnsubscribeFromEvents();
                        _client.Disconnect();
                    }
                    catch { }
                    _client = null;
                }

                InitializeClient();
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to start Twitch connection: {ex.Message}");
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

                // Safe token cancel
                _connectionTimeoutToken?.Cancel();
                _connectionTimeoutToken = null;  // Prevent reuse of disposed token

                // Safe client disconnect
                if (_client != null)
                {
                    try
                    {
                        SafeUnsubscribeFromEvents();  // Clean event handlers first
                        _client.Disconnect();
                    }
                    catch (Exception clientEx)
                    {
                        Logger.Warning($"Twitch client disconnect inner error: {clientEx.Message}");
                    }
                    _client = null;
                }

                if (_settings != null)
                {
                    _settings.IsConnected = false;
                }
                else
                {
                    Logger.Warning("Disconnect called with null _settings");
                }

                // Safe event invoke
                OnDisconnected?.Invoke(_settings?.ChannelName ?? "Unknown");

                Logger.Twitch("Disconnected from Twitch (clean shutdown)");
            }
            catch (Exception ex)
            {
                Logger.Error($"Error disconnecting from Twitch: {ex.Message}");
                // Fallback: force settings state
                if (_settings != null)
                    _settings.IsConnected = false;
            }
        }

        private void InitializeClient()
        {
            Logger.Debug("Initializing Twitch client...");

            string formattedToken = CleanOAuthToken(_settings.AccessToken);
            var credentials = new ConnectionCredentials(_settings.BotUsername, formattedToken);

            // Full reset
            if (_client != null)
            {
                SafeUnsubscribeFromEvents();
                try { _client.Disconnect(); } catch { }
                _client = null;
            }

            var clientOptions = new ClientOptions
            {
                MessagesAllowedInPeriod = 100,
                ThrottlingPeriod = TimeSpan.FromSeconds(30),
                ReconnectionPolicy = null,
                DisconnectWait = 1000
            };

            var customClient = new WebSocketClient(clientOptions);
            _client = new TwitchClient(customClient);

            _client.Initialize(credentials, _settings.ChannelName?.ToLowerInvariant());

            InitializeHelixApi(formattedToken);

            SubscribeToEvents();   // ← Must be BEFORE Connect()

            _client.Connect();

            Logger.Debug("Twitch client initialized and Connect() called");
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
            if (_client == null) return;

            try
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
                _client.OnRaidNotification += OnRaidNotificationReceived;

                Logger.Debug("Twitch events subscribed successfully");
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to subscribe to Twitch events: {ex.Message}");
            }
        }

        private void SafeUnsubscribeFromEvents()
        {
            if (_client == null) return;

            try
            {
                _client.OnConnected -= OnClientConnected;
                _client.OnJoinedChannel -= OnJoinedChannel;
                _client.OnMessageReceived -= OnChatMessageReceived;
                _client.OnWhisperReceived -= OnWhisperMessageReceived;
                _client.OnConnectionError -= OnConnectionError;
                _client.OnDisconnected -= OnClientDisconnected;
                _client.OnError -= OnClientError;
                _client.OnIncorrectLogin -= OnIncorrectLogin;
                _client.OnUserJoined -= OnUserJoined;
                _client.OnUserLeft -= OnUserLeft;
                _client.OnRaidNotification -= OnRaidNotificationReceived;

                Logger.Debug("Twitch events unsubscribed safely");
            }
            catch (Exception ex)
            {
                Logger.Warning($"SafeUnsubscribe partial failure: {ex.Message}");
            }
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

            _isConnecting = false;
            _connectionTimeoutToken?.Cancel();
            _settings.IsConnected = true;
            OnConnected?.Invoke(_settings.ChannelName);



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

            // Lets leave this one permanent for the logs since it's a critical milestone in the connection process and can help with debugging connection issues
            Logger.Twitch($"SUCCESS: Joined channel: {e.Channel}");
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

            // Per-raider debounce using the (now active) _recentRaiders map to ignore duplicate notifs for same channel
            if (_recentRaiders.TryGetValue(raiderChannel, out DateTime lastNotif) &&
                (DateTime.Now - lastNotif).TotalSeconds < RaidDebounceSeconds)
            {
                Logger.Debug($"Ignoring duplicate raid notification from @{raiderChannel} (within {RaidDebounceSeconds}s).");
                return;
            }
            _recentRaiders[raiderChannel] = DateTime.Now;

            Logger.Twitch($"TWITCH RAID DETECTED! From: {raiderChannel} with ~{viewerCount} viewers");

            // Queue to main thread: WindowStack.Add and SendMessage must be main-thread only
            LongEventHandler.QueueLongEvent(() =>
            {
                // Guard on main thread — ProgramState can change between IRC event and queue
                if (!IsColonyReadyForTwitchRaid(out string notReadyReason))
                {
                    Logger.Twitch(
                        $"[RAID JOIN] Ignored @{raiderChannel} raid — not in an active colony game ({notReadyReason}). " +
                        "No join window / no faction / no raid will run (prevents menu/load corruption).");
                    try
                    {
                        SendMessage(
                            $"[RICS] Twitch raid from @{raiderChannel} detected, but RimWorld is not in an active colony game " +
                            $"(menu/loading). Raid ignored — load your save first.");
                    }
                    catch { /* SendMessage may fail off-play */ }
                    return;
                }

                StartRaidJoinCollection(raiderChannel, viewerCount);
            }, null, false, null, showExtraUIInfo: false);
        }

        /// <summary>
        /// True only when it is safe to open join UI, create factions, and spawn a raid.
        /// False on main menu, during load, entry screens, or with no world/map.
        /// </summary>
        public static bool IsColonyReadyForTwitchRaid(out string reason)
        {
            try
            {
                if (Current.ProgramState != ProgramState.Playing)
                {
                    reason = $"ProgramState={Current.ProgramState}";
                    return false;
                }

                if (Current.Game == null)
                {
                    reason = "Current.Game is null";
                    return false;
                }

                // World + faction manager must exist (menu / mid-load do not)
                if (Find.World == null || Find.FactionManager == null)
                {
                    reason = "World or FactionManager not ready";
                    return false;
                }

                // Prefer current map if it is a player home; else any player home map
                Map map = Find.CurrentMap;
                if (map != null && map.IsPlayerHome)
                {
                    reason = null;
                    return true;
                }

                map = Find.AnyPlayerHomeMap;
                if (map == null)
                {
                    reason = "no player home map";
                    return false;
                }

                reason = null;
                return true;
            }
            catch (Exception ex)
            {
                reason = "exception: " + ex.Message;
                return false;
            }
        }

        /// <summary>Map to spawn the raid on (current home if possible, else any player home).</summary>
        public static Map GetTwitchRaidTargetMap()
        {
            if (Find.CurrentMap != null && Find.CurrentMap.IsPlayerHome)
                return Find.CurrentMap;
            return Find.AnyPlayerHomeMap;
        }

        /// <summary>Abort join window without spawning a raid (menu/quit mid-collection).</summary>
        public void CancelRaidJoinCollection(string reason)
        {
            if (!_raidJoinWindowActive && _raidJoinList.Count == 0)
                return;

            Logger.Twitch($"[RAID JOIN] Cancelled — {reason}");
            _raidJoinWindowActive = false;
            _raidJoinList.Clear();
            _primaryRaiderName = null;
            _raidJoinCountFromIrc = 0;
            _raidJoinCountFromChat = 0;
            _raidJoinCountFromCommand = 0;
            IncidentWorker_TwitchRaid.CurrentRaidUsernames.Clear();
        }

        public void StartRaidJoinCollection(string raiderName, int viewerCount)
        {
            // Safety: never open dialogs or arm the timer outside an active colony session
            if (!IsColonyReadyForTwitchRaid(out string notReadyReason))
            {
                Logger.Twitch($"[RAID JOIN] StartRaidJoinCollection blocked — {notReadyReason}");
                try
                {
                    SendMessage(
                        $"[RICS] Twitch raid from @{raiderName} ignored — not in an active colony game ({notReadyReason}).");
                }
                catch { /* ignore */ }
                return;
            }

            if (_raidJoinWindowActive)
            {
                // === MULTI-RAID SPECIAL CASE: merge instead of clobbering previous raiders ===
                bool added = false;
                if (!string.IsNullOrWhiteSpace(raiderName) &&
                    !_raidJoinList.Contains(raiderName, StringComparer.OrdinalIgnoreCase))
                {
                    _raidJoinList.Add(raiderName);
                    added = true;
                }

                Logger.Twitch($"[RAID JOIN] Merged raid from @{raiderName} (added={added}) | Total: {_raidJoinList.Count} | Primary: {_primaryRaiderName}");

                // Extend window slightly if near end so new raiders from this raid have time to !joinraid or auto-join
                float remaining = GetRaidJoinTimeLeft();
                int windowSec = GetRaidJoinWindowSeconds();
                if (remaining > 0f && remaining < 60f)
                {
                    // Ensure at least ~60s remain for the new group
                    _raidJoinStartTime = DateTime.Now - TimeSpan.FromSeconds(windowSec - 60);
                }

                if (added)
                {
                    SendMessage($"@{raiderName} also raided! Their viewers can still !joinraid (or chat) to join the combined raid.");
                }

                // Do not spawn duplicate dialogs; the existing one polls live data via GetCurrentRaidJoinList/GetRaidJoinTimeLeft
                return;
            }

            // Normal path: first (or new after previous ended) raid
            _raidJoinList.Clear();
            _raidJoinCountFromIrc = 0;
            _raidJoinCountFromChat = 0;
            _raidJoinCountFromCommand = 0;
            if (!string.IsNullOrWhiteSpace(raiderName))
                _raidJoinList.Add(raiderName);
            _raidJoinStartTime = DateTime.Now;
            _raidJoinWindowActive = true;
            _primaryRaiderName = raiderName;

            int joinWindowSec = GetRaidJoinWindowSeconds();
            bool chatFallback = CAPChatInteractiveMod.Instance?.Settings?.GlobalSettings?.TwitchRaidsAutoAddChatDuringWindow ?? true;
            Logger.Twitch($"[RAID JOIN] Window opened for @{raiderName} | list={_raidJoinList.Count} | window={joinWindowSec}s | chatAutoAdd={chatFallback}");

            // Show nice countdown dialog (only for the initiating raid of a collection)
            Find.WindowStack.Add(new Dialog_TwitchRaidJoin(raiderName, viewerCount));

            string chatHint = chatFallback
                ? "chat or type !joinraid"
                : "type !joinraid";
            SendMessage($"@{raiderName} just raided us! {chatHint.CapitalizeFirst()} — anyone who participates in the next {joinWindowSec} seconds will be in the raid!");
        }

        public void ProcessJoinRaidCommand(string username)
        {
            if (!_raidJoinWindowActive)
            {
                SendMessage($"@{username} { "RICS.CC.joinraid.noRaid".Translate() }");
                return;
            }

            if (_raidJoinList.Contains(username, StringComparer.OrdinalIgnoreCase))
            {
                SendMessage($"@{username} { "RICS.CC.joinraid.alreadyJoined".Translate() }");
                return;
            }

            if (TryAddRaidJoiner(username, "command"))
                SendMessage($"@{username} joined the raid! ({_raidJoinList.Count} total)");
        }

        /// <summary>
        /// Triggers the RimWorld raid immediately with the current list of raiders, bypassing any remaining time on the join window.
        /// </summary>
        /// <param name="raiderName">The name of the raider who initiated the raid.</param>
        /// <param name="totalRaiders">The total number of raiders participating in the raid.</param>
        public void TriggerRaidNow(string raiderName, int totalRaiders)
        {
            if (!IsColonyReadyForTwitchRaid(out string notReadyReason))
            {
                Logger.Twitch($"[RAID JOIN] TriggerRaidNow blocked — {notReadyReason}");
                CancelRaidJoinCollection("TriggerRaidNow while not in colony game");
                try
                {
                    SendMessage($"[RICS] Cannot start Twitch raid — not in an active colony game ({notReadyReason}).");
                }
                catch { /* ignore */ }
                return;
            }

            if (!_raidJoinWindowActive && _raidJoinList.Count == 0)
            {
                if (!string.IsNullOrWhiteSpace(raiderName))
                    _raidJoinList.Add(raiderName);
                Logger.Twitch($"[RAID JOIN] Fallback - added raider @{raiderName} to empty list");
            }

            _raidJoinWindowActive = false;

            // Snapshot so we can clear the live list immediately (prevents stale data / double-use)
            var snapshot = new List<string>(_raidJoinList);
            int count = snapshot.Count;
            string primary = !string.IsNullOrWhiteSpace(_primaryRaiderName) ? _primaryRaiderName :
                             (snapshot.Count > 0 ? snapshot[0] : raiderName);

            Logger.Twitch($"[RAID JOIN] TriggerRaidNow called | Snapshot count: {count} | Primary: {primary} | irc={_raidJoinCountFromIrc} chat={_raidJoinCountFromChat} cmd={_raidJoinCountFromCommand}");

            // Transfer to the static used by worker, then clear our collection list
            IncidentWorker_TwitchRaid.CurrentRaidUsernames.Clear();
            IncidentWorker_TwitchRaid.CurrentRaidUsernames.AddRange(snapshot);
            _raidJoinList.Clear();
            _primaryRaiderName = null;
            _raidJoinCountFromIrc = 0;
            _raidJoinCountFromChat = 0;
            _raidJoinCountFromCommand = 0;

            Logger.Twitch($"[RAID JOIN] Manual raid start. Final raiders: {IncidentWorker_TwitchRaid.CurrentRaidUsernames.Count}");
            Logger.Twitch($"[RAID JOIN] Names sent: {string.Join(", ", IncidentWorker_TwitchRaid.CurrentRaidUsernames)}");

            TryTriggerRimWorldRaid(primary, count > 0 ? count : totalRaiders);
        }

        private void TryTriggerRimWorldRaid(string raiderName, int viewerCount)
        {
            // === NEW: One-time guard to prevent timer + button double-fire ===
            if ((DateTime.Now - _lastRaidTriggerTime).TotalSeconds < 5)
            {
                Logger.Debug("Raid already triggered in last 5 seconds - ignoring duplicate call.");
                return;
            }

            // Hard gate: never create factions / run incidents on menu or during load
            if (!IsColonyReadyForTwitchRaid(out string notReadyReason))
            {
                Logger.Twitch(
                    $"[CUSTOM FACTION RAID] Aborted for @{raiderName} — not ready ({notReadyReason}). " +
                    "Clearing raid lists so a later load is not poisoned.");
                IncidentWorker_TwitchRaid.CurrentRaidUsernames.Clear();
                CancelRaidJoinCollection("TryTrigger while not ready");
                return;
            }

            Map map = GetTwitchRaidTargetMap();
            if (map == null)
            {
                Logger.Twitch($"[CUSTOM FACTION RAID] Aborted for @{raiderName} — no player home map.");
                IncidentWorker_TwitchRaid.CurrentRaidUsernames.Clear();
                try
                {
                    SendMessage($"[RICS] Twitch raid from @{raiderName} skipped — no colony map (traveling?).");
                }
                catch { /* ignore */ }
                return;
            }

            var globalSettings = CAPChatInteractiveMod.Instance.Settings.GlobalSettings;

            if (viewerCount < globalSettings.TwitchRaidMinRaiders)
            {
                Logger.Debug($"Raid from {raiderName} ignored - only {viewerCount} viewers (min required: {globalSettings.TwitchRaidMinRaiders})");
                IncidentWorker_TwitchRaid.CurrentRaidUsernames.Clear();
                return;
            }

            // Normal debounce
            if ((DateTime.Now - _lastRaidTriggerTime).TotalSeconds < RaidDebounceSeconds)
            {
                Logger.Debug("Raid debounce active - ignoring duplicate trigger.");
                return;
            }

            _lastRaidTriggerTime = DateTime.Now;

            int raiderCount = Math.Max(0, viewerCount); // the 'viewerCount' param here is actually the collected named raider count at trigger time

            // Static tier faction (research + start tech). Threat/points scale strength within tier.
            // Only reached when IsColonyReadyForTwitchRaid — safe for FactionManager.
            var gearTier = TwitchRaidGearTier.Resolve(map, out string tierNotes);
            Faction raidFaction = TwitchRaidGearTier.GetOrCreateFaction(gearTier);
            if (raidFaction == null)
            {
                Logger.Twitch($"[CUSTOM FACTION RAID] Aborted for @{raiderName} — no raid faction available.");
                IncidentWorker_TwitchRaid.CurrentRaidUsernames.Clear();
                return;
            }

            Logger.Twitch(
                $"[CUSTOM FACTION RAID] @{raiderName} | raiders={raiderCount} | onlyRaiders={globalSettings.TwitchRaidsOnlyRaiders} | {tierNotes} | faction={raidFaction?.def?.defName ?? "null"}");

            IncidentParms parms = StorytellerUtility.DefaultParmsNow(IncidentCategoryDefOf.ThreatBig, map);
            parms.forced = true;
            parms.faction = raidFaction;
            parms.target = map;

            float basePoints = parms.points;
            float raidBonus = Mathf.Clamp(raiderCount * 80f, 200f, 4000f);
            parms.points = Mathf.Min(basePoints + raidBonus, basePoints * 2.5f);
            Logger.Twitch($"[CUSTOM FACTION RAID] Threat points base={basePoints:F0} +bonus → {parms.points:F0} (scales combatPower within {gearTier})");

            parms.raidArrivalMode = PawnsArrivalModeDefOf.EdgeWalkIn;
            if (!HasValidEdgeForRaid(map))
            {
                parms.raidArrivalMode = Rand.Chance(0.6f) ? PawnsArrivalModeDefOf.EdgeDrop : PawnsArrivalModeDefOf.CenterDrop;
            }
            parms.raidStrategy = RaidStrategyDefOf.ImmediateAttack;

            var twitchRaidWorker = new IncidentWorker_TwitchRaid { def = IncidentDefOf.RaidEnemy };

            bool success = twitchRaidWorker.TryExecute(parms);

            string raidMessage = success
                ? $"[RICS] INCOMING TWITCH RAID! @{raiderName} brought {raiderCount} named raiders to the colony!"
                : $"[RICS] Twitch raid from @{raiderName} detected, but storyteller refused the raid.";

            Messages.Message(raidMessage, success ? MessageTypeDefOf.ThreatBig : MessageTypeDefOf.NegativeEvent);
            SendMessage(raidMessage);

            if (success)
            {
                Find.LetterStack.ReceiveLetter(
                    "RICS.TwitchRaid.LetterLabel".Translate(raiderName, raiderCount),
                    "RICS.TwitchRaid.LetterText".Translate(raiderName, raiderCount),
                    LetterDefOf.ThreatBig
                );
            }

            Logger.Twitch($"Custom faction raid completed. Faction: {raidFaction.Name} | Success: {success}");
        }

        // Per-streamer runtime FactionDefs removed — use TwitchRaidGearTier.GetOrCreateFaction
        // (static XML defs survive save/load; streamer name stays on the letter/chat only).

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

            // Player quit to menu / loading mid-window — never fire a raid or touch factions
            if (!IsColonyReadyForTwitchRaid(out string notReadyReason))
            {
                CancelRaidJoinCollection($"left active colony during join window ({notReadyReason})");
                return;
            }

            int windowSec = GetRaidJoinWindowSeconds();
            // Logger.Twitch($"[RAID JOIN] Timer tick - seconds left: {windowSec - (DateTime.Now - _raidJoinStartTime).TotalSeconds:F1} | Current list count: {_raidJoinList.Count}");

            if ((DateTime.Now - _raidJoinStartTime).TotalSeconds >= windowSec)
            {
                _raidJoinWindowActive = false;

                var snapshot = new List<string>(_raidJoinList);
                int count = snapshot.Count;
                string primary = !string.IsNullOrWhiteSpace(_primaryRaiderName) ? _primaryRaiderName :
                                 (count > 0 ? snapshot[0] : "UnknownRaider");

                Logger.Twitch($"[RAID JOIN] Window closed | total={count} | via IRC JOIN={_raidJoinCountFromIrc} | via chat={_raidJoinCountFromChat} | via !joinraid={_raidJoinCountFromCommand}");
                Logger.Twitch($"[RAID JOIN] Timer expired - final list: {string.Join(", ", snapshot)}");

                IncidentWorker_TwitchRaid.CurrentRaidUsernames.Clear();
                IncidentWorker_TwitchRaid.CurrentRaidUsernames.AddRange(snapshot);
                _raidJoinList.Clear();
                _primaryRaiderName = null;
                _raidJoinCountFromIrc = 0;
                _raidJoinCountFromChat = 0;
                _raidJoinCountFromCommand = 0;

                TryTriggerRimWorldRaid(primary, count);
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

                // Raid join fallback: Twitch IRC JOIN (OnUserJoined) is often delayed ~2+ min or missing entirely.
                // Anyone who chats during the window is added when the setting is on (default true).
                TryAutoAddChatterDuringRaidWindow(messageWrapper.Username);

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

        /// <summary>
        /// During an active raid join window, optionally add anyone who chats (not only IRC JOIN).
        /// Compensates for Twitch not sending reliable JOIN events for raiders.
        /// </summary>
        private void TryAutoAddChatterDuringRaidWindow(string username)
        {
            if (!_raidJoinWindowActive || string.IsNullOrWhiteSpace(username))
                return;

            var gs = CAPChatInteractiveMod.Instance?.Settings?.GlobalSettings;
            if (gs == null || !gs.TwitchRaidsAutoAddChatDuringWindow)
                return;

            TryAddRaidJoiner(username, "chat");
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
            var service = CAPChatInteractiveMod.Instance?.TwitchService;
            if (service == null)
                return;

            // Always log at Twitch level so we can tell platform vs RICS if JOINs never fire
            string name = e?.Username ?? "(null)";
            Logger.Twitch($"[RAID JOIN] OnUserJoined event received for @{name} | windowActive={service._raidJoinWindowActive}");

            // Queue to main thread because list mutation + logs are touched by UI/timer too; TwitchLib events can arrive off-thread
            LongEventHandler.QueueLongEvent(() => service.ProcessUserJoined(e.Username), null, false, null, showExtraUIInfo: false);
        }

        public void ProcessUserJoined(string username)
        {
            Logger.Twitch($"[RAID JOIN] ProcessUserJoined @{username} | Window active: {_raidJoinWindowActive} | list: {_raidJoinList.Count}");

            if (!_raidJoinWindowActive)
            {
                Logger.Twitch($"[RAID JOIN] Ignored @{username} - window is no longer active (JOIN may have arrived late — extend join window if this is common)");
                return;
            }

            TryAddRaidJoiner(username, "irc");
        }

        /// <summary>
        /// Shared add path for IRC JOIN, chat auto-add, and !joinraid.
        /// Ignores bot/self and duplicates. Returns true if the username was newly added.
        /// </summary>
        private bool TryAddRaidJoiner(string username, string source)
        {
            if (string.IsNullOrWhiteSpace(username))
                return false;

            username = username.Trim();

            // Never put the bot account in the raid roster
            if (!string.IsNullOrEmpty(_settings?.BotUsername) &&
                string.Equals(username, _settings.BotUsername, StringComparison.OrdinalIgnoreCase))
            {
                Logger.Twitch($"[RAID JOIN] Skipped bot account @{username} (source={source})");
                return false;
            }

            if (_raidJoinList.Contains(username, StringComparer.OrdinalIgnoreCase))
            {
                if (source == "irc")
                    Logger.Twitch($"[RAID JOIN] @{username} already in list - skipping (source={source})");
                return false;
            }

            _raidJoinList.Add(username);
            switch (source)
            {
                case "irc":
                    _raidJoinCountFromIrc++;
                    break;
                case "chat":
                    _raidJoinCountFromChat++;
                    break;
                case "command":
                    _raidJoinCountFromCommand++;
                    break;
            }

            Logger.Twitch($"[RAID JOIN] SUCCESS - Added @{username} via {source} | total={_raidJoinList.Count} (irc={_raidJoinCountFromIrc}, chat={_raidJoinCountFromChat}, cmd={_raidJoinCountFromCommand})");
            return true;
        }

        public string ProcessUserJoinRaidCommand(ChatMessageWrapper message)
        {
            if (!_raidJoinWindowActive)
            {
                return "RICS.CC.joinraid.noRaid".Translate();
            }
            if (_raidJoinList.Contains(message.Username, StringComparer.OrdinalIgnoreCase))
            {
                return "RICS.CC.joinraid.alreadyJoined".Translate();
            }
            if (TryAddRaidJoiner(message.Username, "command"))
                return "RICS.CC.joinraid.success".Translate();
            return "RICS.CC.joinraid.alreadyJoined".Translate();
        }

        public List<string> GetCurrentRaidJoinList()
        {
            return new List<string>(_raidJoinList);
        }

        /// <summary>
        /// Returns remaining seconds in the raid join window (single source of truth).
        /// Used by both full and compact dialogs so countdown stays accurate even
        /// if the mini dialog is opened later or after view switches.
        /// </summary>
        public float GetRaidJoinTimeLeft()
        {
            if (!_raidJoinWindowActive || _raidJoinStartTime == DateTime.MinValue)
                return 0f;
            float elapsed = (float)(DateTime.Now - _raidJoinStartTime).TotalSeconds;
            return Mathf.Max(0f, GetRaidJoinWindowSeconds() - elapsed);
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