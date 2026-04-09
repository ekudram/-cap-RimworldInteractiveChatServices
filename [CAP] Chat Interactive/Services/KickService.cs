// Source/RICS/Services/KickService.cs
// Copyright (c) Captolamia
// This file is part of RICS (Rimworld Interactive Chat Services).
//
// RICS (Rimworld Interactive Chat Services) is free software: you can redistribute it and/or modify
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
// Service to manage Kick.com chat connection and messaging
// Official API only — Pusher WS (read) + OAuth 2.1 REST (write)
// FULLY SYNCHRONOUS (.Result) to bypass Mono async state machine crash
// Matches YouTubeChatService.cs stability exactly. No Disconnect() needed (polling).

using Newtonsoft.Json;
using RimWorld;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Verse;

namespace CAP_ChatInteractive
{
    /// <summary>
    /// MONO ASYNC CRASH PATTERNS — RICS KICK SERVICE (EXPLANATION FOR MAINTAINERS)
    /// 
    /// RimWorld 1.6 uses Mono (Unity 2022.3 + .NET 4.7.2). Async/await state machines are broken in Mono's JIT:
    ///   • Any .Result / .Wait() inside an async method (or lambda) triggers "System.Runtime.CompilerServices.AsyncTaskMethodBuilder`1:Start"
    ///   • Mono cannot resume the state machine after the blocking call → hard crash with stack in Mono JIT Code.
    /// 
    /// Classic patterns that crash:
    ///   1. async void / async Task method calling .Result directly
    ///   2. Task.Run(() => SomeAsyncMethod().Result) without try/catch(AggregateException)
    ///   3. .Wait() on WebSocket.ConnectAsync / SendAsync inside Connect()
    /// 
    /// Our fix (matches YouTubeChatService.cs exactly):
    ///   • Everything runs inside Task.Run(() => InitializeAndConnectSynchronous())
    ///   • Split every .Result into separate statements (postTask.Result then readTask.Result)
    ///   • ALWAYS catch AggregateException + .Flatten() + use LongEventHandler.ExecuteWhenFinished for UI messages
    ///   • User-Agent header prevents silent Kick API rejection
    ///   • No Disconnect() needed — polling model like Twitch
    /// 
    /// This is why RefreshAccessToken was crashing before and why we hardened GetChatroomId + ConnectPusher.
    /// DO NOT change back to async/await or single-line .Result — it will crash again.
    /// </summary>
    public class KickService
    {
        private readonly StreamServiceSettings _settings;
        private ClientWebSocket _webSocket;
        private HttpClient _httpClient;
        private string _chatroomId;
        private string _accessToken;
        private DateTime _lastMessageTime = DateTime.MinValue;
        private readonly TimeSpan _messageDelay = TimeSpan.FromMilliseconds(350);
        private bool _isConnecting = false;

        public bool IsConnected => _webSocket?.State == WebSocketState.Open;

        public event Action<string, string> OnMessageReceived;
        public event Action<string> OnConnected;
        public event Action<string> OnDisconnected;

        public KickService(StreamServiceSettings settings)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
            _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "RICS-RimWorld-Mod/1.6");
            Logger.Debug($"KickService constructor — Channel: {_settings.ChannelName}, HasClientId: {!string.IsNullOrEmpty(_settings.ClientId)}");
        }

        public void Connect()
        {
            if (IsConnected || _isConnecting)
            {
                Logger.Debug("KickService: Already connected or connecting, skipping");
                return;
            }

            if (string.IsNullOrEmpty(_settings.ClientId) || string.IsNullOrEmpty(_settings.ClientSecret) || string.IsNullOrEmpty(_settings.ChannelName))
            {
                Logger.Error("Kick: Missing credentials or channel name");
                Messages.Message("Kick connect failed: missing Client ID / Secret / Channel Name", MessageTypeDefOf.NegativeEvent);
                return;
            }

            Logger.Twitch("Attempting to connect to Kick.com...");

            _isConnecting = true;

            var connectTask = Task.Run(() =>
            {
                try
                {
                    InitializeAndConnectSynchronous();
                }
                catch (Exception ex)
                {
                    Logger.Error($"Kick connect failed: {ex.Message}\n{ex.StackTrace}");
                    throw;
                }
            });

            try
            {
                if (!connectTask.Wait(TimeSpan.FromSeconds(15)))
                {
                    LongEventHandler.ExecuteWhenFinished(() =>
                        Messages.Message("Kick.com timeout failure.\n\nPossible reasons:\n• Not currently streaming\n• Internet / Kick.com outage\n• Wrong Client ID/Secret", MessageTypeDefOf.NegativeEvent));
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Kick synchronous connect crashed: {ex.Message}");
                LongEventHandler.ExecuteWhenFinished(() =>
                    Messages.Message("Kick connection failed — check Player.log", MessageTypeDefOf.NegativeEvent));
            }
            finally
            {
                _isConnecting = false;
            }
        }

        private void InitializeAndConnectSynchronous()
        {
            try
            {
                Logger.Debug("Kick: Starting full connection (15s timeout)...");

                if (!RefreshAccessTokenSynchronous()) return;

                // === NEW TRANSITION LOGS (this is where the old crash happened) ===
                Logger.Debug("✅ OAuth succeeded — calling GetChatroomIdSynchronous() now...");
                Logger.Debug($"Token length: {_accessToken?.Length ?? 0}");

                _chatroomId = GetChatroomIdSynchronous();

                Logger.Debug($"GetChatroomIdSynchronous() returned: '{_chatroomId ?? "NULL"}'");

                if (string.IsNullOrEmpty(_chatroomId))
                {
                    LongEventHandler.ExecuteWhenFinished(() =>
                        Messages.Message($"Kick: Channel '{_settings.ChannelName}' not found or not live", MessageTypeDefOf.NegativeEvent));
                    return;
                }

                ConnectPusherSynchronous();

                _settings.IsConnected = true;
                LongEventHandler.ExecuteWhenFinished(() =>
                {
                    OnConnected?.Invoke(_settings.ChannelName);
                    Logger.Message($"[RICS] SUCCESS: Joined Kick channel {_settings.ChannelName}");
                    Messages.Message($"[RICS] Successfully connected to Kick: {_settings.ChannelName}", MessageTypeDefOf.TaskCompletion);
                });
            }
            catch (Exception ex)
            {
                Logger.Error($"Kick full connection crashed: {ex.Message}\n{ex.StackTrace}");
                _settings.IsConnected = false;
                LongEventHandler.ExecuteWhenFinished(() =>
                    Messages.Message("Kick connection failed — check Player.log", MessageTypeDefOf.NegativeEvent));
            }
        }

        private bool RefreshAccessTokenSynchronous()
        {
            Logger.Debug("Kick: Starting OAuth request (HttpClient version)");

            try
            {
                var form = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    {"grant_type", "client_credentials"},
                    {"client_id", _settings.ClientId},
                    {"client_secret", _settings.ClientSecret}
                });

                var postTask = _httpClient.PostAsync("https://id.kick.com/oauth/token", form);
                var response = postTask.Result;

                var readTask = response.Content.ReadAsStringAsync();
                string body = readTask.Result;

                Logger.Debug($"Kick OAuth response — Status: {response.StatusCode} | Body length: {body.Length}");

                if (!response.IsSuccessStatusCode)
                {
                    Logger.Error($"OAuth failed: {response.StatusCode} - {body}");
                    return false;
                }

                var tokenResponse = JsonConvert.DeserializeObject<KickTokenResponse>(body);
                _accessToken = tokenResponse?.AccessToken;

                if (string.IsNullOrEmpty(_accessToken))
                {
                    Logger.Error("Kick OAuth succeeded but returned no token");
                    return false;
                }

                Logger.Debug("✅ Kick OAuth token acquired (HttpClient)");
                return true;
            }
            catch (AggregateException aex)
            {
                aex = aex.Flatten();
                Logger.Error($"OAuth AggregateException: {aex.InnerException?.Message ?? aex.Message}");
                return false;
            }
            catch (Exception ex)
            {
                Logger.Error($"Kick OAuth sync crashed: {ex.Message}\n{ex.StackTrace}");
                LongEventHandler.ExecuteWhenFinished(() =>
                    Messages.Message("Kick: OAuth failed (credentials/network) — check Player.log", MessageTypeDefOf.NegativeEvent));
                return false;
            }
        }

        /// <summary>
        /// Retrieves the chatroom ID for the configured channel using the public v1 Kick API endpoint.
        /// </summary>
        /// <remarks>This method performs a synchronous HTTP request to the Kick v1 API and may block the
        /// calling thread. It returns null if the channel does not exist, the chatroom ID is not available, or if an
        /// error occurs during the request.
        /// Mono's JIT hates dynamic + Newtonsoft.Json in certain contexts (especially inside the Task.Run thread).
        /// That's why it hard-crashes at InitializeAndConnectSynchronous().
        /// </remarks>
        /// <returns>A string containing the chatroom ID if found; otherwise, null.</returns>
        private string GetChatroomIdSynchronous()
        {
            Logger.Debug($"Kick: Fetching chatroom ID for channel '{_settings.ChannelName}' using v1 endpoint (public, no token)");

            string v1Url = $"https://kick.com/api/v1/channels/{_settings.ChannelName.ToLowerInvariant()}";

            try
            {
                Logger.Debug($"Kick: GET {v1Url}");

                using var request = new HttpRequestMessage(HttpMethod.Get, v1Url);
                // No Authorization header for public v1 endpoint

                var sendTask = _httpClient.SendAsync(request);
                var response = sendTask.Result;

                Logger.Debug($"Kick v1 response — Status: {response.StatusCode}");

                if (!response.IsSuccessStatusCode)
                {
                    var errorBodyTask = response.Content.ReadAsStringAsync();
                    string errorBody = errorBodyTask.Result;
                    Logger.Warning($"Kick v1 failed: {response.StatusCode} - {errorBody}");
                    return null;
                }

                var readTask = response.Content.ReadAsStringAsync();
                string jsonText = readTask.Result;

                Logger.Debug($"Kick v1 success — Body length: {jsonText.Length}");

                // Strongly-typed parse (matches the JSON you provided exactly)
                var channelData = JsonConvert.DeserializeObject<KickV1ChannelResponse>(jsonText);

                if (channelData?.chatroom?.id > 0)
                {
                    string id = channelData.chatroom.id.ToString();
                    Logger.Debug($"✅ Kick chatroom ID resolved: {id}");
                    return id;
                }

                Logger.Warning($"Kick: Channel data received but no chatroom.id found");
            }
            catch (AggregateException aex)
            {
                aex = aex.Flatten();
                Logger.Error($"GetChatroomId AggregateException: {aex.InnerException?.Message ?? aex.Message}");
            }
            catch (Exception ex)
            {
                Logger.Error($"Kick v1 channel fetch crashed: {ex.Message}\n{ex.StackTrace}");
            }

            Logger.Error($"Kick: Failed to get chatroom ID for '{_settings.ChannelName}'");
            LongEventHandler.ExecuteWhenFinished(() =>
                Messages.Message($"Kick: Could not get chatroom for '{_settings.ChannelName}' — check Player.log", MessageTypeDefOf.NegativeEvent));

            return null;
        }

        // ──────────────────────────────────────────────────────────────
        // Clean response classes (replace the previous ones)
        // ──────────────────────────────────────────────────────────────

        private class KickV1ChannelResponse
        {
            public KickV1Chatroom chatroom { get; set; }
        }

        private class KickV1Chatroom
        {
            public long id { get; set; }   // 79588321 for your channel
        }

        /// <summary>
        /// Establishes a synchronous WebSocket connection to the Pusher service and subscribes to the specified
        /// chatroom channel.
        /// </summary>
        /// <remarks>This method blocks the calling thread until the connection and subscription are
        /// complete. If the connection fails, error messages are logged and a notification is displayed to the user.
        /// This method is intended for internal use and should not be called from performance-sensitive or UI threads,
        /// as it may block execution.</remarks>
        private void ConnectPusherSynchronous()
        {
            try
            {
                _webSocket = new ClientWebSocket();

                Logger.Debug("Kick: Starting WebSocket connection to Pusher...");
                _webSocket.ConnectAsync(new Uri("wss://ws-us2.pusher.com/app/32cbd69e4b950bf97679"), CancellationToken.None).Wait();
                Logger.Debug("Kick: WebSocket connected");

                var subscribe = $@"{{""event"":""pusher:subscribe"",""data"":{{""channel"":""chatrooms.{_chatroomId}.v2""}}}}";
                Logger.Debug($"Kick: Sending subscribe for channel chatrooms.{_chatroomId}.v2");
                _webSocket.SendAsync(Encoding.UTF8.GetBytes(subscribe), WebSocketMessageType.Text, true, CancellationToken.None).Wait();
                Logger.Debug("Kick: Subscribe message sent");

                Logger.Debug("Kick: Starting background listen task...");
                _ = Task.Run(() => PusherListenLoopAsync(CancellationToken.None));
                Logger.Debug("✅ Pusher WebSocket connected and subscribed - listen task launched");
            }
            catch (AggregateException aex)
            {
                aex = aex.Flatten();
                Logger.Error($"Pusher connect AggregateException: {aex.InnerException?.Message ?? aex.Message}");
                LongEventHandler.ExecuteWhenFinished(() =>
                    Messages.Message("Kick: Pusher WebSocket failed — check Player.log", MessageTypeDefOf.NegativeEvent));
            }
            catch (Exception ex)
            {
                Logger.Error($"Pusher connect failed: {ex.Message}\n{ex.StackTrace}");
                LongEventHandler.ExecuteWhenFinished(() =>
                    Messages.Message("Kick: Chatroom connected but Pusher failed — check Player.log", MessageTypeDefOf.NegativeEvent));
            }
        }

        /// <summary>
        /// Continuously listens for and processes incoming WebSocket messages from the Pusher service until the
        /// connection is closed or cancellation is requested.
        /// </summary>
        /// <remarks>This method processes incoming messages, including chat messages and subscription
        /// events, and logs relevant information. The listen loop terminates when the WebSocket connection is closed or
        /// when cancellation is requested via the provided token.</remarks>
        /// <param name="token">A cancellation token that can be used to request termination of the listen loop.</param>
        /// <returns>A task that represents the asynchronous listen operation.</returns>
        private async Task PusherListenLoopAsync(CancellationToken token)
        {
            Logger.Debug("=== PusherListenLoopAsync BACKGROUND TASK STARTED ===");
            var buffer = new byte[8192];

            try
            {
                while (_webSocket.State == WebSocketState.Open)
                {
                    var result = await _webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), token);
                    if (result.MessageType == WebSocketMessageType.Close) break;

                    var json = Encoding.UTF8.GetString(buffer, 0, result.Count).Trim();

                    try
                    {
                        var obj = Newtonsoft.Json.Linq.JObject.Parse(json);
                        string eventName = (string)obj["event"] ?? "unknown";
                        Logger.Debug($"Kick Pusher event: {eventName}");

                        if (eventName.Contains("ChatMessageEvent"))
                        {
                            Logger.Debug($"Kick Chat Event Data: {obj["data"]}");
                            LongEventHandler.QueueLongEvent(() => ProcessKickMessage(json), null, false, null, showExtraUIInfo: false, forceHideUI: true);
                        }
                    }
                    catch { }
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Kick Pusher listener error: {ex.Message}");
            }
            finally
            {
                Logger.Debug("=== PusherListenLoopAsync BACKGROUND TASK ENDED ===");
            }
        }

        private void ProcessKickMessage(string json)
        {
            try
            {
                Logger.Debug($"Kick: Processing message JSON (length {json.Length})");

                var obj = Newtonsoft.Json.Linq.JObject.Parse(json);

                // Kick sends "data" as a JSON string, not an object
                var dataStr = (string)obj["data"];
                if (string.IsNullOrEmpty(dataStr))
                {
                    Logger.Debug("Kick: Skipped - no data string");
                    return;
                }

                // Parse the inner data string
                var dataObj = Newtonsoft.Json.Linq.JObject.Parse(dataStr);

                // The actual message is under "sender" and "content"
                var sender = dataObj["sender"];
                string username = (string)(sender?["username"] ?? "KickViewer");
                string messageText = (string)(dataObj["content"] ?? "[empty]");

                if (string.IsNullOrWhiteSpace(messageText))
                {
                    Logger.Debug("Kick: Skipped - empty message");
                    return;
                }

                var wrapper = new ChatMessageWrapper(
                    username: username,
                    message: messageText,
                    platform: "kick",
                    platformUserId: (string)(sender?["id"] ?? username),
                    channelId: _settings.ChannelName,
                    platformMessage: null,
                    isWhisper: false
                );

                ChatMessageLogger.AddMessage(wrapper.Username, wrapper.Message, "Kick");
                OnMessageReceived?.Invoke(wrapper.Username, wrapper.Message);

                if (!_settings.suspendFeedback)
                    ChatCommandProcessor.ProcessMessage(wrapper);

                Logger.Debug($"[Kick] {username}: {messageText}");
            }
            catch (Exception ex)
            {
                Logger.Error($"Kick message processing failed: {ex.Message}");
            }
        }

        public void SendMessage(string message)
        {
            if (!IsConnected || string.IsNullOrEmpty(_chatroomId))
            {
                Logger.Warning("Kick: Cannot send message - not connected or chatroom ID missing");
                return;
            }

            // Clean the message
            message = message.Replace("\r\n", "\n").Replace("\n\n", "\n").Trim();

            var now = DateTime.Now;
            if (now - _lastMessageTime < _messageDelay)
                Thread.Sleep(_messageDelay - (now - _lastMessageTime));

            Task.Run(async () =>
            {
                try
                {
                    var payload = new
                    {
                        // chatroom_id = long.Parse(_chatroomId),
                        content = message          // ← This is the correct field name
                    };

                    string url = $"https://kick.com/api/v2/messages/send/{_chatroomId}";
                    var content = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json");

                    using var request = new HttpRequestMessage(HttpMethod.Post, url);
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
                    request.Content = content;

                    Logger.Debug($"Kick: Sending via /api/v1/chat-messages → {message}");

                    var response = await _httpClient.SendAsync(request);
                    string responseBody = await response.Content.ReadAsStringAsync();

                    if (response.IsSuccessStatusCode)
                    {
                        _lastMessageTime = DateTime.Now;
                        Logger.Debug($"[Kick] Sent: {message}");
                    }
                    else
                    {
                        Logger.Error($"Kick send failed - {response.StatusCode}: {responseBody}");
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error($"Failed to send Kick message: {ex.Message}");
                }
            });
        }

        public Task SendWhisperAsync(string username, string message)
        {
            // Kick doesn't have true whispers, so we just mention the user
            SendMessage($"@{username} {message}");
            return Task.CompletedTask;
        }

        private class KickTokenResponse
        {
            [JsonProperty("access_token")]
            public string AccessToken { get; set; }
        }
    }
}