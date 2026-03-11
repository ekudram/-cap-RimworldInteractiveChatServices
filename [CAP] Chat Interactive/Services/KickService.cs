// Source/RICS/Services/KickService.cs
// Copyright (c) Captolamia
// This file is part of RICS (Rimworld Interactive Chat Services).
//
// RICS is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License as published
// by the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// Service to manage Kick.com chat connection and messaging
// Official API only (no NuGet) — Pusher WS (read) + OAuth 2.1 REST (write)
// Designed to be drop-in parallel to TwitchService.cs for the rest of the mod.

using RimWorld;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;   // TwitchLib already pulls this in — safe to use. If missing, add via project reference.
using Verse;

namespace CAP_ChatInteractive
{
    public class KickService
    {
        private readonly StreamServiceSettings _settings;
        private ClientWebSocket _webSocket;
        private HttpClient _httpClient;
        private CancellationTokenSource _cts;
        private string _chatroomId;
        private string _accessToken;
        private DateTime _lastMessageTime = DateTime.MinValue;
        private readonly TimeSpan _messageDelay = TimeSpan.FromMilliseconds(350); // Kick rate limits are stricter

        public bool IsConnected => _webSocket?.State == WebSocketState.Open;

        // Events — identical signatures to TwitchService so ChatCommandProcessor, ChatMessageLogger, etc. need zero changes
        public event Action<string, string> OnMessageReceived; // username, message
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
            if (IsConnected || _cts != null)
            {
                Logger.Debug("KickService: Already connecting or connected, skipping");
                return;
            }

            if (string.IsNullOrEmpty(_settings.ClientId) || string.IsNullOrEmpty(_settings.ClientSecret))
            {
                Logger.Error("Kick: Missing ClientId or ClientSecret — cannot connect");
                Messages.Message("Kick connect failed: missing Client ID / Secret (dev.kick.com)", MessageTypeDefOf.NegativeEvent);
                return;
            }

            Logger.Twitch("Attempting to connect to Kick.com...");
            _cts = new CancellationTokenSource();
            Task.Run(() => InitializeAndConnectAsync(_cts.Token));
        }

        public void Disconnect()
        {
            try
            {
                _cts?.Cancel();
                _webSocket?.Abort();
                _webSocket?.Dispose();
                _webSocket = null;
                _settings.IsConnected = false;
                OnDisconnected?.Invoke(_settings.ChannelName);
                Logger.Warning("Disconnected from Kick.com");
            }
            catch (Exception ex)
            {
                Logger.Error($"Error disconnecting from Kick: {ex.Message}");
            }
        }

        private async Task InitializeAndConnectAsync(CancellationToken token)
        {
            try
            {
                // 1. OAuth 2.1 Client Credentials (required for chat:write)
                if (!await RefreshAccessTokenAsync(token)) return;

                // 2. Resolve slug → chatroom ID (official endpoint)
                _chatroomId = await GetChatroomIdAsync(token);
                if (string.IsNullOrEmpty(_chatroomId))
                {
                    Messages.Message($"Kick: Channel '{_settings.ChannelName}' not found", MessageTypeDefOf.NegativeEvent);
                    return;
                }

                // 3. Pusher WebSocket (public, no auth)
                await ConnectPusherAsync(token);

                _settings.IsConnected = true;
                OnConnected?.Invoke(_settings.ChannelName);
                Logger.Message($"[RICS] SUCCESS: Joined Kick channel {_settings.ChannelName}");
            }
            catch (Exception ex)
            {
                Logger.Error($"Kick connection failed: {ex.Message}");
                _settings.IsConnected = false;
                Messages.Message("Kick connection failed — check logs", MessageTypeDefOf.NegativeEvent);
            }
        }

        private async Task<bool> RefreshAccessTokenAsync(CancellationToken token)
        {
            try
            {
                var form = new Dictionary<string, string>
                {
                    { "grant_type", "client_credentials" },
                    { "client_id", _settings.ClientId },
                    { "client_secret", _settings.ClientSecret },
                    { "scope", "chat:write" }
                };

                var response = await _httpClient.PostAsync("https://id.kick.com/oauth/token", new FormUrlEncodedContent(form), token);
                response.EnsureSuccessStatusCode();

                var json = JsonConvert.DeserializeObject<dynamic>(await response.Content.ReadAsStringAsync());
                _accessToken = json.access_token;
                _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _accessToken);

                Logger.Debug("Kick OAuth token acquired");
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error($"Kick OAuth failed: {ex.Message}");
                Messages.Message("Kick: Invalid Client ID / Secret", MessageTypeDefOf.NegativeEvent);
                return false;
            }
        }

        private async Task<string> GetChatroomIdAsync(CancellationToken token)
        {
            try
            {
                var response = await _httpClient.GetAsync($"https://api.kick.com/api/v2/channels/{_settings.ChannelName.ToLowerInvariant()}", token);
                response.EnsureSuccessStatusCode();

                var json = JsonConvert.DeserializeObject<dynamic>(await response.Content.ReadAsStringAsync());
                return json?.data?.chatroom?.id?.ToString();
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to resolve Kick chatroom ID for {_settings.ChannelName}: {ex.Message}");
                return null;
            }
        }

        private async Task ConnectPusherAsync(CancellationToken token)
        {
            _webSocket = new ClientWebSocket();
            await _webSocket.ConnectAsync(new Uri("wss://ws-us2.pusher.com/app/32cbd69e4b950bf97679"), token);

            // Subscribe (stable public channel format)
            var subscribe = $@"{{""event"":""pusher:subscribe"",""data"":{{""channel"":""chatrooms.{_chatroomId}.v2""}}}}";
            await _webSocket.SendAsync(Encoding.UTF8.GetBytes(subscribe), WebSocketMessageType.Text, true, token);

            _ = Task.Run(() => PusherListenLoopAsync(token), token);
        }

        private async Task PusherListenLoopAsync(CancellationToken token)
        {
            var buffer = new byte[8192];
            try
            {
                while (_webSocket.State == WebSocketState.Open && !token.IsCancellationRequested)
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
            catch (Exception ex) when (!(ex is OperationCanceledException))
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
                    platformMessage: null, // not needed for Kick
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

        // Kick has no private whispers — fall back to public @mention (same as TwitchService fallback)
        public Task SendWhisperAsync(string username, string message)
        {
            SendMessage($"@{username} {message}");
            return Task.CompletedTask;
        }
    }
}