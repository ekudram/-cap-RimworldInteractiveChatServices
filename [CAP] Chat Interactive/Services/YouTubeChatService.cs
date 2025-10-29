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

        public bool IsConnected => _pollingActive;
        public int QuotaUsedToday => _quotaUsedToday;
        public int QuotaLimit => 10000; // YouTube's daily limit
        public float QuotaPercentage => (float)_quotaUsedToday / QuotaLimit * 100;
        public string QuotaStatus => $"{_quotaUsedToday:n0}/{QuotaLimit:n0} ({QuotaPercentage:0}%)";
        public bool CanSendMessages => _oauthService?.IsAuthenticated == true;


        // Events to match Twitch service interface
        public event Action<string, string> OnMessageReceived; // username, message
        public event Action<string> OnConnected;
        public event Action<string> OnDisconnected;

        public YouTubeChatService(StreamServiceSettings settings)
        {
            _settings = settings;
        }

        public void Connect()
        {
            try
            {
                if (!_settings.CanConnect)
                {
                    Logger.Error("Cannot connect to YouTube: Missing credentials");
                    return;
                }

                // Initialize OAuth for message sending capability
                _oauthService = new YouTubeOAuthService(_settings);

                if (!_oauthService.Authenticate())
                {
                    Logger.Warning("YouTube OAuth failed - can read chat but cannot send messages");
                }
                else
                {
                    _authenticatedService = _oauthService.CreateAuthenticatedService();
                }

                InitializeYouTubeService(); // For reading (API key only)
                StartChatPolling();
                _settings.IsConnected = true;
                Logger.YouTube($"Connected to YouTube channel: {_settings.ChannelName}");
                OnConnected?.Invoke(_settings.ChannelName);
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to connect to YouTube: {ex.Message}");
                _settings.IsConnected = false;
            }
        }


        public void Disconnect()
        {
            try
            {
                _pollingActive = false;
                _settings.IsConnected = false;
                Logger.YouTube("Disconnected from YouTube Live Chat");
                OnDisconnected?.Invoke(_settings.ChannelName);
            }
            catch (Exception ex)
            {
                Logger.Error($"Error disconnecting from YouTube: {ex.Message}");
            }
        }

        private void InitializeYouTubeService()
        {
            // YouTube API uses API key authentication
            _youTubeService = new YouTubeService(new BaseClientService.Initializer()
            {
                ApiKey = _settings.AccessToken,
                ApplicationName = "CAP Chat Interactive"
            });

            Logger.Debug("YouTube service initialized");
        }

        private async void StartChatPolling()
        {
            _pollingActive = true;

            try
            {
                // Get the live chat ID for the channel
                _liveChatId = await GetLiveChatIdAsync();

                if (string.IsNullOrEmpty(_liveChatId))
                {
                    Logger.Error("Could not find active live stream for YouTube channel");
                    return;
                }

                Logger.YouTube($"Found live chat: {_liveChatId}");

                // Start polling for new messages
                while (_pollingActive)
                {
                    await PollChatMessagesAsync();
                    await Task.Delay(2000); // Poll every 2 seconds (YouTube API quotas)
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"YouTube chat polling error: {ex.Message}");
                _pollingActive = false;
                _settings.IsConnected = false;
            }
        }

        private async Task<string> GetLiveChatIdAsync()
        {
            try
            {
                // Search for active live broadcasts
                var searchRequest = _youTubeService.Search.List("snippet");
                searchRequest.ChannelId = _settings.ChannelName; // Can use channel ID or custom name
                searchRequest.EventType = SearchResource.ListRequest.EventTypeEnum.Live;
                searchRequest.Type = "video";
                searchRequest.MaxResults = 1;

                var searchResponse = await searchRequest.ExecuteAsync();

                if (searchResponse.Items.Count == 0)
                {
                    Logger.Warning("No active live stream found for YouTube channel");
                    return null;
                }

                var liveVideoId = searchResponse.Items[0].Id.VideoId;

                // Get the live chat ID from the video
                var videosRequest = _youTubeService.Videos.List("liveStreamingDetails");
                videosRequest.Id = liveVideoId;

                var videoResponse = await videosRequest.ExecuteAsync();

                if (videoResponse.Items.Count > 0)
                {
                    return videoResponse.Items[0].LiveStreamingDetails.ActiveLiveChatId;
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error getting YouTube live chat ID: {ex.Message}");
            }

            return null;
        }

        private async Task PollChatMessagesAsync()
        {
            // Dynamic polling based on quota usage
            int pollDelay = _quotaUsedToday > 5000 ? 5000 :  // 5 seconds if high usage
                           _quotaUsedToday > 2000 ? 3000 :  // 3 seconds if medium usage
                           2000;                           // 2 seconds normal

            await Task.Delay(pollDelay);
            try
            {
                if (string.IsNullOrEmpty(_liveChatId)) return;

                var liveChatRequest = _youTubeService.LiveChatMessages.List(_liveChatId, "snippet,authorDetails");

                if (!string.IsNullOrEmpty(_nextPageToken))
                    liveChatRequest.PageToken = _nextPageToken;

                var response = await liveChatRequest.ExecuteAsync();
                _nextPageToken = response.NextPageToken;

                // Process new messages
                foreach (var message in response.Items)
                {
                    ProcessYouTubeMessage(message);
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error polling YouTube chat messages: {ex.Message}");
            }
        }

        private void ProcessYouTubeMessage(LiveChatMessage message)
        {
            try
            {
                string username = message.AuthorDetails.DisplayName;
                string text = message.Snippet.DisplayMessage;

                Logger.Debug($"YouTube message from {username}: {text}");

                // Create unified message wrapper with YouTube-specific data
                var messageWrapper = new ChatMessageWrapper(
                    username: username,
                    message: text,
                    platform: "YouTube",
                    platformUserId: message.AuthorDetails.ChannelId,
                    channelId: _settings.ChannelName,
                    platformMessage: message
                );

                // Use RimWorld's thread-safe event handler - NO POPUP BOX  
                LongEventHandler.QueueLongEvent(() =>
                {
                    ProcessMessageOnMainThread(messageWrapper);
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
                // Update viewer activity
                Viewers.UpdateViewerActivity(messageWrapper);

                // Log message for chat display
                ChatMessageLogger.AddMessage(messageWrapper.Username, messageWrapper.Message, "YouTube");

                // Same interface as Twitch - unified command processing!
                OnMessageReceived?.Invoke(messageWrapper.Username, messageWrapper.Message);

                // Process through unified command system
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
                    return;

                // Split message for YouTube (conservative limit)
                var messages = MessageSplitter.SplitMessage(message, "youtube");

                // Check if we have OAuth for sending messages
                if (!CanSendMessages)
                {
                    // Fallback: Use letters instead of game notifications
                    SendMessagesAsLetters(messages, "YouTube Chat");
                    return;
                }

                // Try to send via OAuth with splitting
                foreach (var msg in messages)
                {
                    SendSingleYouTubeMessage(msg);
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to send YouTube message: {ex.Message}");
                // Fallback to letters
                var messages = MessageSplitter.SplitMessage(message, "youtube");
                SendMessagesAsLetters(messages, "YouTube Chat Error");
            }
        }

        private void SendSingleYouTubeMessage(string message)
        {
            try
            {
                _oauthService.RefreshTokenIfNeeded();

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
                insertRequest.ExecuteAsync().Wait();

                Logger.Debug($"Sent YouTube message: {message}");
                TrackQuotaUsage();
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to send single YouTube message: {ex.Message}");
                throw; // Re-throw to handle in calling method
            }
        }

        // In YouTubeChatService.cs - update the SendMessagesAsLetters method
        private void SendMessagesAsLetters(List<string> messages, string label)
        {
            if (messages == null || messages.Count == 0)
                return;

            // Use your custom green letter for chat messages
            if (messages.Count == 1)
            {
                SendGreenLetter(label, messages[0]);
            }
            else
            {
                // For multiple messages, combine with clear separation
                string combinedMessage = string.Join("\n\n", messages.Select((msg, index) => $"[Part {index + 1}/{messages.Count}] {msg}"));
                SendGreenLetter($"{label} - {messages.Count} Parts", combinedMessage);
            }
        }

        private void SendGreenLetter(string label, string message)
        {
            try
            {
                // Use your custom green letter
                MessageHandler.SendGreenLetter(label, message);
                Logger.Debug($"Sent green letter: {label}");
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to send green letter: {ex.Message}");
                // Fallback to neutral letter
                MessageHandler.SendInfoLetter(label, message);
            }
        }

        private void SendLetter(string label, string message)
        {
            try
            {
                // Use a neutral letter def that won't affect mood
                LetterDef letterDef = LetterDefOf.NeutralEvent;

                // Create a custom letter with better formatting
                string formattedMessage = $"[{DateTime.Now:HH:mm}] {message}";

                Find.LetterStack.ReceiveLetter(label, formattedMessage, letterDef);

                Logger.Debug($"Sent letter: {label} - {message.Substring(0, Math.Min(50, message.Length))}...");
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to send letter: {ex.Message}");
                // Ultimate fallback - game notification
                Messages.Message($"[{label}] {message}", MessageTypeDefOf.NeutralEvent);
            }
        }

        private void SendGameNotification(string message)
        {
            // Use RimWorld's notification system for urgent messages
            Messages.Message(message, MessageTypeDefOf.NeutralEvent);

            // ALSO send to our chat window system
            ChatMessageLogger.AddSystemMessage(message);

            Logger.Debug($"YouTube notification: {message}");
        }
        private int _quotaUsedToday = 0;
        private DateTime _lastQuotaReset = DateTime.Today;

        private void TrackQuotaUsage()
        {
            // Reset daily quota counter
            if (DateTime.Today > _lastQuotaReset)
            {
                _quotaUsedToday = 0;
                _lastQuotaReset = DateTime.Today;
            }

            _quotaUsedToday++;

            // Warn at 80% usage
            if (_quotaUsedToday >= 8000)
            {
                Logger.Warning($"YouTube API quota: {_quotaUsedToday}/10,000 used today");
                Logger.Message("Consider upgrading to Google Cloud paid tier for higher limits");
            }

            // Hard stop at 95% to avoid complete cutoff
            if (_quotaUsedToday >= 9500)
            {
                Logger.Error("YouTube API quota nearly exhausted - stopping message sending");
                _pollingActive = false;
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
    }
}