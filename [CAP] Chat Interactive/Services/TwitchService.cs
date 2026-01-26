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

using CAP_ChatInteractive.Utilities;
using RimWorld;
using System;
using System.Threading;
using System.Threading.Tasks;
using TwitchLib.Client;
using TwitchLib.Client.Events;
using TwitchLib.Client.Models;
using TwitchLib.Communication.Clients;
using TwitchLib.Communication.Events;
using TwitchLib.Communication.Models;
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

            SubscribeToEvents();
            _client.Connect();

            Logger.Debug("Twitch client initialized and connecting...");
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
            _client.OnWhisperReceived += OnWhisperReceivedHandler;
            _client.OnConnectionError += OnConnectionError;
            _client.OnDisconnected += OnClientDisconnected;
            _client.OnError += OnClientError;
            _client.OnIncorrectLogin += OnIncorrectLogin;
            _client.OnUserJoined += OnUserJoined;
            _client.OnUserLeft += OnUserLeft;
        }

        private void UnsubscribeFromEvents()
        {
            if (_client == null) return;

            _client.OnConnected += OnClientConnected;
            _client.OnJoinedChannel += OnJoinedChannel;
            _client.OnMessageReceived += OnChatMessageReceived;
            _client.OnWhisperReceived += OnWhisperReceivedHandler;
            _client.OnConnectionError += OnConnectionError;
            _client.OnDisconnected += OnClientDisconnected;
            _client.OnError += OnClientError;
            _client.OnIncorrectLogin += OnIncorrectLogin;
            _client.OnUserJoined += OnUserJoined;
            _client.OnUserLeft += OnUserLeft;
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
                        SendMessage($"Please use whispers for commands. Type: /w {_settings.BotUsername} [command]");
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
                shouldIgnoreForCommands: _settings.forceUseWhisper // Add this flag
            );

            LongEventHandler.QueueLongEvent(() =>
            {
                ProcessMessageOnMainThread(messageWrapper);
            }, null, false, null, showExtraUIInfo: false, forceHideUI: true);
        }

        private void OnWhisperReceivedHandler(object sender, OnWhisperReceivedArgs e)
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

        #region Twitch Client Event Handlers

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
            Logger.Message($"User joined: {e.Username}  {sender}");
            // Additional logic for mods to hook into
        }
        public static void OnUserLeft(object sender, OnUserLeftArgs e)
        {
            Logger.Message($"User left: {e.Username}  {sender}");
            // Additional logic for mods to hook into
        }

        #endregion

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

        [Obsolete("Usage of SendWhisper through chat is not possible anymore. Use TwitchLib.Api.Helix.Whispers.SendWhisperAsync() instead.")]
        public void SendWhisper(string username, string message)
        {
            if (!IsConnected || string.IsNullOrEmpty(username))
                return;

            try
            {
                // Rate limiting for whispers (different from regular messages)
                var now = DateTime.Now;
                if (now - _lastMessageTime < _messageDelay)
                {
                    System.Threading.Thread.Sleep(_messageDelay - (now - _lastMessageTime));
                }
                // Obsolete
                _client.SendWhisper(username, message);
                _lastMessageTime = DateTime.Now;
                Logger.Debug($"Sent Twitch whisper to {username}: {message}");
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to send Twitch whisper: {ex.Message}");
            }
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