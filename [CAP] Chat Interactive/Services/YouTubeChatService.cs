// YouTubeChatService.cs
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
// Service to connect to YouTube Live Chat, read messages, and send messages
using CAP_ChatInteractive.Utilities;
using Google.Apis.Services;
using Google.Apis.YouTube.v3;
using Google.Apis.YouTube.v3.Data;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using Verse;

namespace CAP_ChatInteractive
{
    public class YouTubeChatService
    {
        private readonly StreamServiceSettings _settings;
        private YouTubeService _youTubeService;
        private string _liveChatId;
        private string _nextPageToken;
        private bool _pollingActive;
        private YouTubeOAuthService _oauthService;
        private YouTubeService _authenticatedService;
        private int _quotaUsedToday = 0;
        private DateTime _lastQuotaReset = DateTime.Today;
        private readonly object _quotaLock = new object();
        private int _failedConnectionAttempts = 0;
        private const int MAX_FAILED_ATTEMPTS = 5;

        public bool IsConnected => _pollingActive && !string.IsNullOrEmpty(_liveChatId);
        public int QuotaUsedToday => _quotaUsedToday;
        public int QuotaLimit => 10000; // YouTube's daily limit
        public float QuotaPercentage => (float)_quotaUsedToday / QuotaLimit * 100;
        public string QuotaStatus => $"{_quotaUsedToday:n0}/{QuotaLimit:n0} ({QuotaPercentage:0}%)";

        // For YouTube, we need OAuth for sending messages (different from reading API key)
        // The AccessToken field contains the API key for reading
        // For sending, we need a separate OAuth token - this would come from YouTubeOAuthService
        public bool CanSendMessages => _oauthService?.IsAuthenticated == true;

        // Events to match Twitch service interface
        public event Action<string, string> OnMessageReceived; // username, message
        public event Action<string> OnConnected;
        public event Action<string> OnDisconnected;

        public YouTubeChatService(StreamServiceSettings settings)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        public void Connect()
        {
            try
            {
                // YouTube only needs API key (AccessToken) and ChannelName
                if (string.IsNullOrEmpty(_settings.AccessToken))
                {
                    string error = "YouTube API Key is missing. Please add your Google Cloud API key in the Access Token field.";
                    Logger.Error(error);
                    Messages.Message(error, MessageTypeDefOf.RejectInput, false);
                    OnDisconnected?.Invoke(error);
                    return;
                }

                if (string.IsNullOrEmpty(_settings.ChannelName))
                {
                    string error = "YouTube Channel name/ID is missing. Please add your channel in the Channel Name field.";
                    Logger.Error(error);
                    Messages.Message(error, MessageTypeDefOf.RejectInput, false);
                    OnDisconnected?.Invoke(error);
                    return;
                }

                // BotUsername is NOT used for YouTube - just log if present for debugging
                if (!string.IsNullOrEmpty(_settings.BotUsername))
                {
                    Logger.Debug($"Note: BotUsername field '{_settings.BotUsername}' is ignored for YouTube");
                }

                Logger.Debug($"YouTube Connect - API Key present: {!string.IsNullOrEmpty(_settings.AccessToken)}");
                Logger.Debug($"YouTube Connect - Channel: {_settings.ChannelName}");

                // Reset failure counter on connection attempt
                _failedConnectionAttempts = 0;

                // Initialize YouTube service for reading (API key only)
                InitializeYouTubeService();

                // Validate API key works before proceeding
                if (!ValidateApiKey())
                {
                    string error = "YouTube API key validation failed. Please check your Google Cloud API key in the Access Token field.";
                    Logger.Error(error);
                    Messages.Message(error, MessageTypeDefOf.RejectInput, false);
                    OnDisconnected?.Invoke(error);
                    return;
                }

                // Initialize OAuth for message sending capability (optional)
                // This requires separate OAuth credentials - not the API key
                try
                {
                    _oauthService = new YouTubeOAuthService(_settings);

                    // Run authentication with timeout
                    Task<bool> authTask = null;
                    try
                    {
                        authTask = Task.Run(() => _oauthService.Authenticate());
                        if (authTask.Wait(TimeSpan.FromSeconds(10)))
                        {
                            if (authTask.Result)
                            {
                                _authenticatedService = _oauthService.CreateAuthenticatedService();
                                Logger.YouTube("YouTube OAuth authenticated successfully - can send messages");
                            }
                            else
                            {
                                Logger.Warning("YouTube OAuth failed - can read chat but cannot send messages");
                            }
                        }
                        else
                        {
                            Logger.Warning("YouTube OAuth timeout - can read chat but cannot send messages");
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Warning($"YouTube OAuth error: {ex.Message} - can read chat but cannot send messages");
                    }
                }
                catch (Exception ex)
                {
                    Logger.Warning($"YouTube OAuth service initialization failed: {ex.Message} - can read chat but cannot send messages");
                }

                // Start polling with timeout
                var pollingTask = Task.Run(() => StartChatPolling());
                if (pollingTask.Wait(TimeSpan.FromSeconds(15)))
                {
                    if (_pollingActive && !string.IsNullOrEmpty(_liveChatId))
                    {
                        _settings.IsConnected = true;
                        string message = $"Successfully connected to YouTube channel: {_settings.ChannelName}";
                        Logger.YouTube(message);
                        Messages.Message(message, MessageTypeDefOf.TaskCompletion, false);
                        OnConnected?.Invoke(_settings.ChannelName);
                    }
                    else
                    {
                        string error = "Connected but could not find active live stream";
                        Logger.Error(error);
                        Messages.Message(error, MessageTypeDefOf.RejectInput, false);
                        OnDisconnected?.Invoke(error);
                    }
                }
                else
                {
                    string error = "YouTube connection timeout after 15 seconds";
                    Logger.Error(error);
                    Messages.Message(error, MessageTypeDefOf.RejectInput, false);
                    OnDisconnected?.Invoke(error);
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to connect to YouTube: {ex.Message}");
                _settings.IsConnected = false;
                _pollingActive = false;
                OnDisconnected?.Invoke($"Connection error: {ex.Message}");
            }
        }

        private bool ValidateApiKey()
        {
            try
            {
                // Test API connectivity with minimal quota operation
                var testRequest = _youTubeService.Channels.List("id");
                testRequest.Mine = false;
                testRequest.MaxResults = 1;
                testRequest.Id = "UC_x5XG1OV2P6uZZ5FSM9Ttw"; // GoogleDevelopers channel

                var response = testRequest.Execute();
                TrackQuotaUsage(1);
                return response != null && response.Items != null && response.Items.Count > 0;
            }
            catch (Google.GoogleApiException ex)
            {
                if (ex.Error?.Code == 403)
                {
                    Logger.Error("YouTube API key is invalid or has insufficient permissions. Make sure YouTube Data API v3 is enabled in Google Cloud Console.");
                }
                return false;
            }
            catch (Exception ex)
            {
                Logger.Error($"API key validation failed: {ex.Message}");
                return false;
            }
        }

        public void Disconnect()
        {
            try
            {
                _pollingActive = false;
                _settings.IsConnected = false;
                _liveChatId = null;
                _nextPageToken = null;
                Logger.YouTube("Disconnected from YouTube Live Chat");
                Messages.Message("Disconnected from YouTube", MessageTypeDefOf.SilentInput, false);
                OnDisconnected?.Invoke(_settings.ChannelName);
            }
            catch (Exception ex)
            {
                Logger.Error($"Error disconnecting from YouTube: {ex.Message}");
            }
        }

        private void InitializeYouTubeService()
        {
            try
            {
                _youTubeService = new YouTubeService(new BaseClientService.Initializer()
                {
                    ApiKey = _settings.AccessToken, // This is the Google Cloud API key
                    ApplicationName = "CAP Chat Interactive",
                    GZipEnabled = true
                });

                Logger.Debug("YouTube service initialized successfully");
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to initialize YouTube service: {ex.Message}");
                throw;
            }
        }

        private async void StartChatPolling()
        {
            _pollingActive = true;
            _failedConnectionAttempts = 0;

            try
            {
                // Get the live chat ID for the channel
                _liveChatId = await GetLiveChatIdAsync();

                if (string.IsNullOrEmpty(_liveChatId))
                {
                    Logger.Error("Could not find active live stream for YouTube channel");
                    _pollingActive = false;
                    _settings.IsConnected = false;
                    OnDisconnected?.Invoke("No active live stream found");
                    return;
                }

                Logger.YouTube($"Found live chat ID: {_liveChatId}");
                _failedConnectionAttempts = 0;

                // Start polling for new messages
                while (_pollingActive)
                {
                    try
                    {
                        await PollChatMessagesAsync();
                        // Dynamic delay based on quota usage
                        int delay = _quotaUsedToday > 8000 ? 5000 :
                                   _quotaUsedToday > 5000 ? 3000 : 2000;
                        await Task.Delay(delay);
                        _failedConnectionAttempts = 0;
                    }
                    catch (Exception ex)
                    {
                        _failedConnectionAttempts++;
                        Logger.Warning($"YouTube poll error (attempt {_failedConnectionAttempts}/{MAX_FAILED_ATTEMPTS}): {ex.Message}");

                        if (_failedConnectionAttempts >= MAX_FAILED_ATTEMPTS)
                        {
                            Logger.Error("Too many polling failures, disconnecting");
                            break;
                        }
                        await Task.Delay(10000); // Wait 10 seconds on error
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"YouTube chat polling error: {ex.Message}");
            }
            finally
            {
                _pollingActive = false;
                _settings.IsConnected = false;
                OnDisconnected?.Invoke("Polling stopped");
            }
        }

        private async Task<string> GetLiveChatIdAsync()
        {
            try
            {
                string channelId = await ResolveChannelIdentifierAsync(_settings.ChannelName);
                if (string.IsNullOrEmpty(channelId))
                {
                    Logger.Error($"Could not resolve YouTube channel: {_settings.ChannelName}");
                    return null;
                }

                Logger.Debug($"Resolved channel ID: {channelId}");

                // Search for active live broadcasts
                var searchRequest = _youTubeService.Search.List("snippet");
                searchRequest.ChannelId = channelId;
                searchRequest.EventType = SearchResource.ListRequest.EventTypeEnum.Live;
                searchRequest.Type = "video";
                searchRequest.MaxResults = 1;

                var searchResponse = await searchRequest.ExecuteAsync();
                TrackQuotaUsage(100);

                if (searchResponse?.Items == null || searchResponse.Items.Count == 0)
                {
                    Logger.Warning($"No active live stream found for channel: {_settings.ChannelName}");
                    return null;
                }

                var liveVideoId = searchResponse.Items[0].Id.VideoId;
                Logger.Debug($"Found live video ID: {liveVideoId}");

                // Get the live chat ID from the video
                var videosRequest = _youTubeService.Videos.List("liveStreamingDetails");
                videosRequest.Id = liveVideoId;

                var videoResponse = await videosRequest.ExecuteAsync();
                TrackQuotaUsage(1);

                if (videoResponse.Items.Count > 0)
                {
                    var liveChatId = videoResponse.Items[0].LiveStreamingDetails?.ActiveLiveChatId;
                    if (!string.IsNullOrEmpty(liveChatId))
                    {
                        return liveChatId;
                    }
                    else
                    {
                        Logger.Warning("Video is live but chat is disabled or not available");
                    }
                }
            }
            catch (Google.GoogleApiException ex)
            {
                if (ex.Error?.Code == 403)
                {
                    Logger.Error("YouTube API key invalid or quota exceeded. Check your API key in Access Token field.");
                }
                else
                {
                    Logger.Error($"YouTube API error: {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error getting YouTube live chat ID: {ex.Message}");
            }

            return null;
        }

        private async Task<string> ResolveChannelIdentifierAsync(string channelInput)
        {
            try
            {
                if (string.IsNullOrEmpty(channelInput))
                    return null;

                // If it's already a channel ID format (starts with UC and is 24 chars), use it directly
                if (channelInput.StartsWith("UC") && channelInput.Length == 24)
                {
                    Logger.Debug($"Using direct channel ID: {channelInput}");
                    return channelInput;
                }

                // Try to find by username/handle
                try
                {
                    var channelsRequest = _youTubeService.Channels.List("id");
                    channelsRequest.ForUsername = channelInput;
                    var response = await channelsRequest.ExecuteAsync();
                    TrackQuotaUsage(1);

                    if (response.Items?.Count > 0)
                    {
                        string channelId = response.Items[0].Id;
                        Logger.Debug($"Resolved username '{channelInput}' to channel ID: {channelId}");
                        return channelId;
                    }
                }
                catch (Exception ex)
                {
                    Logger.Debug($"Username lookup failed: {ex.Message}");
                }

                // Try search by channel name
                try
                {
                    var searchRequest = _youTubeService.Search.List("snippet");
                    searchRequest.Q = channelInput;
                    searchRequest.Type = "channel";
                    searchRequest.MaxResults = 1;

                    var searchResponse = await searchRequest.ExecuteAsync();
                    TrackQuotaUsage(100);

                    if (searchResponse.Items?.Count > 0)
                    {
                        string channelId = searchResponse.Items[0].Snippet.ChannelId;
                        Logger.Debug($"Resolved search '{channelInput}' to channel ID: {channelId}");
                        return channelId;
                    }
                }
                catch (Exception ex)
                {
                    Logger.Debug($"Channel search failed: {ex.Message}");
                }

                // If all else fails, return the input (might be a direct ID in non-standard format)
                Logger.Warning($"Could not resolve channel '{channelInput}', using as-is");
                return channelInput;
            }
            catch (Exception ex)
            {
                Logger.Error($"Error resolving channel identifier: {ex.Message}");
                return channelInput;
            }
        }

        private async Task PollChatMessagesAsync()
        {
            try
            {
                if (string.IsNullOrEmpty(_liveChatId))
                {
                    // Attempt to reconnect if live chat ID is lost
                    _liveChatId = await GetLiveChatIdAsync();
                    if (string.IsNullOrEmpty(_liveChatId))
                        return;
                }

                var liveChatRequest = _youTubeService.LiveChatMessages.List(_liveChatId, "snippet,authorDetails");
                liveChatRequest.MaxResults = 200;

                if (!string.IsNullOrEmpty(_nextPageToken))
                    liveChatRequest.PageToken = _nextPageToken;

                var response = await liveChatRequest.ExecuteAsync();
                TrackQuotaUsage(1);

                _nextPageToken = response.NextPageToken;

                // Process new messages
                if (response.Items != null && response.Items.Count > 0)
                {
                    foreach (var message in response.Items)
                    {
                        ProcessYouTubeMessage(message);
                    }
                }
            }
            catch (Google.GoogleApiException ex) when (ex.Error?.Code == 403)
            {
                Logger.Error("YouTube API quota exceeded or permission denied");
                _pollingActive = false;
                _settings.IsConnected = false;
                OnDisconnected?.Invoke("API quota exceeded");
            }
            catch (Exception ex)
            {
                Logger.Error($"Error polling YouTube chat messages: {ex.Message}");
                throw;
            }
        }

        private void ProcessYouTubeMessage(LiveChatMessage message)
        {
            try
            {
                if (message?.AuthorDetails == null || message.Snippet == null)
                    return;

                string username = message.AuthorDetails.DisplayName ?? "Unknown";
                string text = message.Snippet.DisplayMessage ?? "";

                if (string.IsNullOrEmpty(text))
                    return;

                Logger.Debug($"YouTube message from {username}: {text}");

                var messageWrapper = new ChatMessageWrapper(
                    username: username,
                    message: text,
                    platform: "YouTube",
                    platformUserId: message.AuthorDetails.ChannelId ?? username,
                    channelId: _settings.ChannelName,
                    platformMessage: message,
                    customRewardId: null,
                    bits: 0
                );

                LongEventHandler.QueueLongEvent(() =>
                {
                    try
                    {
                        ProcessMessageOnMainThread(messageWrapper);
                    }
                    catch (Exception ex)
                    {
                        Logger.Error($"Error processing message on main thread: {ex.Message}");
                    }
                }, null, false, null, showExtraUIInfo: false, forceHideUI: true);
            }
            catch (Exception ex)
            {
                Logger.Error($"Error processing YouTube message: {ex.Message}");
            }
        }

        private void ProcessMessageOnMainThread(ChatMessageWrapper messageWrapper)
        {
            try
            {
                Viewers.UpdateViewerActivity(messageWrapper);
                ChatMessageLogger.AddMessage(messageWrapper.Username, messageWrapper.Message, "YouTube");
                OnMessageReceived?.Invoke(messageWrapper.Username, messageWrapper.Message);
                ChatCommandProcessor.ProcessMessage(messageWrapper);
            }
            catch (Exception ex)
            {
                Logger.Error($"Error processing YouTube message on main thread: {ex.Message}");
            }
        }

        public void SendMessage(string message)
        {
            try
            {
                if (!_pollingActive || string.IsNullOrEmpty(_liveChatId))
                {
                    Logger.Warning("Cannot send message: Not connected to live chat");
                    return;
                }

                if (string.IsNullOrEmpty(message))
                    return;

                var messages = MessageSplitter.SplitMessage(message, "youtube");

                if (!CanSendMessages || _authenticatedService == null)
                {
                    SendMessagesAsLetters(messages, "YouTube Chat");
                    return;
                }

                foreach (var msg in messages)
                {
                    try
                    {
                        SendSingleYouTubeMessage(msg);
                        System.Threading.Thread.Sleep(500);
                    }
                    catch (Exception ex)
                    {
                        Logger.Error($"Failed to send message part: {ex.Message}");
                        SendMessagesAsLetters(new List<string> { msg }, "YouTube Chat (Fallback)");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to send YouTube message: {ex.Message}");
                var messages = MessageSplitter.SplitMessage(message, "youtube");
                SendMessagesAsLetters(messages, "YouTube Chat Error");
            }
        }

        private void SendSingleYouTubeMessage(string message)
        {
            try
            {
                if (_authenticatedService == null)
                    throw new InvalidOperationException("Not authenticated to send messages");

                _oauthService?.RefreshTokenIfNeeded();

                var liveChatMessage = new LiveChatMessage
                {
                    Snippet = new LiveChatMessageSnippet
                    {
                        LiveChatId = _liveChatId,
                        Type = "textMessage",
                        TextMessageDetails = new LiveChatTextMessageDetails
                        {
                            MessageText = message
                        }
                    }
                };

                var insertRequest = _authenticatedService.LiveChatMessages.Insert(liveChatMessage, "snippet");
                insertRequest.Execute();
                TrackQuotaUsage(1);

                Logger.Debug($"Sent YouTube message: {message}");
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to send single YouTube message: {ex.Message}");
                throw;
            }
        }

        private void SendMessagesAsLetters(List<string> messages, string label)
        {
            if (messages == null || messages.Count == 0)
                return;

            try
            {
                if (messages.Count == 1)
                {
                    SendGreenLetter(label, messages[0]);
                }
                else
                {
                    string combinedMessage = string.Join("\n\n", messages.Select((msg, index) => $"[Part {index + 1}/{messages.Count}] {msg}"));
                    SendGreenLetter($"{label} - {messages.Count} Parts", combinedMessage);
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to send letters: {ex.Message}");
            }
        }

        private void SendGreenLetter(string label, string message)
        {
            try
            {
                MessageHandler.SendGreenLetter(label, message);
                Logger.Debug($"Sent green letter: {label}");
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to send green letter: {ex.Message}");
                try
                {
                    MessageHandler.SendInfoLetter(label, message);
                }
                catch
                {
                    Logger.Message($"{label}: {message}");
                }
            }
        }

        private void TrackQuotaUsage(int cost = 1)
        {
            lock (_quotaLock)
            {
                if (DateTime.Today > _lastQuotaReset)
                {
                    _quotaUsedToday = 0;
                    _lastQuotaReset = DateTime.Today;
                }

                _quotaUsedToday += cost;

                if (_quotaUsedToday >= 8000 && _quotaUsedToday - cost < 8000)
                {
                    Logger.Warning($"YouTube API quota: {_quotaUsedToday:n0}/10,000 used today ({QuotaPercentage:0}%)");
                    Messages.Message("YouTube API quota at 80% - consider upgrading", MessageTypeDefOf.CautionInput, false);
                }

                if (_quotaUsedToday >= 9500 && _quotaUsedToday - cost < 9500)
                {
                    Logger.Error("YouTube API quota nearly exhausted (95%+) - limiting operations");
                    Messages.Message("YouTube API quota critically low - chat may stop soon", MessageTypeDefOf.ThreatBig, false);
                }

                if (_quotaUsedToday >= 9900)
                {
                    Logger.Error("YouTube API quota exhausted - stopping chat polling");
                    _pollingActive = false;
                    _settings.IsConnected = false;
                    OnDisconnected?.Invoke("API quota exhausted");
                }
            }
        }

        public Color QuotaColor
        {
            get
            {
                if (_quotaUsedToday >= 9500) return Color.red;
                if (_quotaUsedToday >= 8000) return Color.yellow;
                return Color.green;
            }
        }

        public bool ValidateConnection()
        {
            try
            {
                if (_youTubeService == null)
                {
                    Logger.Error("YouTube service not initialized");
                    return false;
                }

                var testRequest = _youTubeService.Channels.List("id");
                testRequest.Mine = false;
                testRequest.MaxResults = 1;
                testRequest.Id = "UC_x5XG1OV2P6uZZ5FSM9Ttw";

                var response = testRequest.Execute();
                TrackQuotaUsage(1);

                return response != null && response.Items != null && response.Items.Count > 0;
            }
            catch (Exception ex)
            {
                Logger.Error($"Connection validation failed: {ex.Message}");
                return false;
            }
        }

        public string GetSettingsStatus()
        {
            return $"YouTube API Key: {(string.IsNullOrEmpty(_settings.AccessToken) ? "❌ MISSING" : "✅ Present")}\n" +
                   $"Channel: {(string.IsNullOrEmpty(_settings.ChannelName) ? "❌ MISSING" : $"✅ {_settings.ChannelName}")}\n" +
                   $"BotUsername: {(string.IsNullOrEmpty(_settings.BotUsername) ? "⚠️ Not used for YouTube" : $"⚠️ Ignored: {_settings.BotUsername}")}\n" +
                   $"Connected: {(IsConnected ? "✅ Yes" : "❌ No")}\n" +
                   $"Live Chat ID: {(!string.IsNullOrEmpty(_liveChatId) ? "✅ Found" : "❌ Not found")}\n" +
                   $"Can Send Messages: {(CanSendMessages ? "✅ Yes" : "❌ No (read-only)")}\n" +
                   $"Quota Used: {QuotaStatus}";
        }
    }
}