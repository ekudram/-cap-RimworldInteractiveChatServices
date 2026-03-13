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

using RimWorld;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Verse;

namespace CAP_ChatInteractive
{
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

                _chatroomId = GetChatroomIdSynchronous();
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
            try
            {
                Logger.Debug("Kick: Starting OAuth request to https://id.kick.com/oauth/token");

                // User-Agent required by some Kick endpoints (prevents silent rejection on background threads)
                if (!_httpClient.DefaultRequestHeaders.Contains("User-Agent"))
                {
                    _httpClient.DefaultRequestHeaders.Add("User-Agent", "RICS-RimWorld-Mod/1.6");
                }

                var form = new Dictionary<string, string>
                {
                    { "grant_type", "client_credentials" },
                    { "client_id", _settings.ClientId },
                    { "client_secret", _settings.ClientSecret }
                    // NO scope parameter — exactly matches Kick's official client_credentials example
                };

                // Split .Result calls + explicit AggregateException handling (Mono state-machine crash fix)
                var response = _httpClient.PostAsync("https://id.kick.com/oauth/token", new FormUrlEncodedContent(form)).Result;
                string body = response.Content.ReadAsStringAsync().Result;

                Logger.Debug($"Kick OAuth response — Status: {response.StatusCode} | Body: {body}");

                if (!response.IsSuccessStatusCode)
                {
                    Logger.Error($"Kick OAuth failed — Status: {response.StatusCode} | Body: {body}");
                    return false;
                }

                var tokenResponse = JsonConvert.DeserializeObject<KickTokenResponse>(body);
                if (tokenResponse == null || string.IsNullOrEmpty(tokenResponse.AccessToken))
                {
                    Logger.Error("Kick OAuth succeeded but returned no token");
                    return false;
                }

                _accessToken = tokenResponse.AccessToken;
                _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _accessToken);
                Logger.Debug("✅ Kick OAuth token acquired");
                return true;
            }
            catch (AggregateException aex)
            {
                aex = aex.Flatten();
                var inner = aex.InnerException ?? aex;
                Logger.Error($"Kick OAuth AggregateException: {inner.Message}\n{inner.StackTrace}");
                LongEventHandler.ExecuteWhenFinished(() =>
                    Messages.Message("Kick: OAuth failed (credentials/network) — check Player.log", MessageTypeDefOf.NegativeEvent));
                return false;
            }
            catch (Exception ex)
            {
                Logger.Error($"Kick OAuth crashed: {ex.Message}\n{ex.StackTrace}");
                LongEventHandler.ExecuteWhenFinished(() =>
                    Messages.Message("Kick: OAuth unexpected crash — check Player.log", MessageTypeDefOf.NegativeEvent));
                return false;
            }
        }

        private string GetChatroomIdSynchronous()
        {
            try
            {
                var response = _httpClient.GetAsync($"https://api.kick.com/api/v2/channels/{_settings.ChannelName.ToLowerInvariant()}").Result;
                response.EnsureSuccessStatusCode();

                var json = JsonConvert.DeserializeObject<dynamic>(response.Content.ReadAsStringAsync().Result);
                string id = json?.data?.chatroom?.id?.ToString();

                if (string.IsNullOrEmpty(id))
                {
                    Logger.Warning($"Kick channel '{_settings.ChannelName}' found but no chatroom ID (not live?)");
                    LongEventHandler.ExecuteWhenFinished(() =>
                        Messages.Message($"Kick: Channel '{_settings.ChannelName}' not live or chat disabled", MessageTypeDefOf.NegativeEvent));
                }
                return id;
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to resolve Kick chatroom ID: {ex.Message}");
                LongEventHandler.ExecuteWhenFinished(() =>
                    Messages.Message($"Kick: Channel '{_settings.ChannelName}' not found (404?)", MessageTypeDefOf.NegativeEvent));
                return null;
            }
        }

        private void ConnectPusherSynchronous()
        {
            try
            {
                _webSocket = new ClientWebSocket();
                _webSocket.ConnectAsync(new Uri("wss://ws-us2.pusher.com/app/32cbd69e4b950bf97679"), CancellationToken.None).Wait();

                var subscribe = $@"{{""event"":""pusher:subscribe"",""data"":{{""channel"":""chatrooms.{_chatroomId}.v2""}}}}";
                _webSocket.SendAsync(Encoding.UTF8.GetBytes(subscribe), WebSocketMessageType.Text, true, CancellationToken.None).Wait();

                _ = Task.Run(() => PusherListenLoopAsync(CancellationToken.None));
                Logger.Debug("✅ Pusher WebSocket connected and subscribed");
            }
            catch (Exception ex)
            {
                Logger.Error($"Pusher connect failed: {ex.Message}");
                LongEventHandler.ExecuteWhenFinished(() =>
                    Messages.Message("Kick: Chatroom connected but Pusher failed — check Player.log", MessageTypeDefOf.NegativeEvent));
            }
        }

        private async Task PusherListenLoopAsync(CancellationToken token)
        {
            var buffer = new byte[8192];
            try
            {
                while (_webSocket.State == WebSocketState.Open)
                {
                    var result = await _webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), token);
                    if (result.MessageType == WebSocketMessageType.Close) break;

                    var json = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    if (json.Contains("App\\\\Events\\\\ChatMessageSentEvent"))
                    {
                        LongEventHandler.QueueLongEvent(() => ProcessKickMessage(json), null, false, null, showExtraUIInfo: false, forceHideUI: true);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Kick Pusher listener error: {ex.Message}");
            }
        }

        private void ProcessKickMessage(string json)
        {
            try
            {
                dynamic msg = JsonConvert.DeserializeObject<dynamic>(json);
                var chatData = msg.data.message;

                string username = chatData?.user?.username ?? "KickViewer";
                string messageText = chatData?.content ?? "[empty]";

                var wrapper = new ChatMessageWrapper(
                    username: username,
                    message: messageText,
                    platform: "Kick",
                    platformUserId: chatData?.user?.id?.ToString() ?? "",
                    channelId: _settings.ChannelName,
                    platformMessage: null,
                    isWhisper: false
                );

                ChatMessageLogger.AddMessage(wrapper.Username, wrapper.Message, "Kick");
                OnMessageReceived?.Invoke(wrapper.Username, wrapper.Message);

                if (!_settings.suspendFeedback)
                    ChatCommandProcessor.ProcessMessage(wrapper);
            }
            catch (Exception ex)
            {
                Logger.Error($"Kick message processing failed: {ex.Message}");
            }
        }

        public void SendMessage(string message)
        {
            if (!IsConnected || string.IsNullOrEmpty(_chatroomId)) return;

            var now = DateTime.Now;
            if (now - _lastMessageTime < _messageDelay)
                Thread.Sleep(_messageDelay - (now - _lastMessageTime));

            Task.Run(async () =>
            {
                try
                {
                    var payload = new { content = message };
                    var content = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json");
                    await _httpClient.PostAsync($"https://api.kick.com/api/v2/messages/send/{_chatroomId}", content);
                    _lastMessageTime = DateTime.Now;
                    Logger.Debug($"Sent Kick message: {message}");
                }
                catch (Exception ex)
                {
                    Logger.Error($"Failed to send Kick message: {ex.Message}");
                }
            });
        }

        public Task SendWhisperAsync(string username, string message)
        {
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