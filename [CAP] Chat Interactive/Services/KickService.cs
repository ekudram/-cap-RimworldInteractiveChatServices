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
// Official API: Pusher WS (read) + OAuth 2.1 authorization-code/PKCE (write)
// FULLY SYNCHRONOUS (.Result) to bypass Mono async state machine crash
// Matches YouTubeChatService.cs stability. Do not use async/await with .Result.

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Verse;

namespace CAP_ChatInteractive
{
    /// <summary>
    /// MONO ASYNC CRASH PATTERNS — RICS KICK SERVICE
    /// RimWorld 1.6 uses Mono (Unity 2022.3 + .NET 4.7.2). Async/await state machines are broken
    /// when mixed with .Result / .Wait() inside async methods.
    /// Everything that talks HTTP runs as synchronous .Result split across statements, inside Task.Run.
    /// </summary>
    public class KickService
    {
        public const string DefaultRedirectUri = "http://localhost:17890/kick/callback";
        private const string AuthorizeUrl = "https://id.kick.com/oauth/authorize";
        private const string TokenUrl = "https://id.kick.com/oauth/token";
        private const string IntrospectUrl = "https://id.kick.com/oauth/token/introspect";
        private const string ChatUrl = "https://api.kick.com/public/v1/chat";
        private const string ChannelsUrl = "https://api.kick.com/public/v1/channels";
        private const string UsersUrl = "https://api.kick.com/public/v1/users";
        private const string OAuthScopes = "chat:write user:read channel:read";
        private const int MaxChatLength = 500;

        private readonly StreamServiceSettings _settings;
        private ClientWebSocket _webSocket;
        private HttpClient _httpClient;
        private string _chatroomId;
        private long _broadcasterUserId;
        private string _userAccessToken;
        private string _tokenType;
        private DateTime _userTokenExpiresAt = DateTime.MinValue;
        private DateTime _lastMessageTime = DateTime.MinValue;
        private readonly TimeSpan _messageDelay = TimeSpan.FromMilliseconds(350);
        private bool _isConnecting;
        private bool _isAuthorizing;
        private DateTime _lastSendFailureNotice = DateTime.MinValue;

        private string _pkceVerifier;
        private string _oauthState;
        private HttpListener _oauthListener;

        public bool IsConnected => _webSocket?.State == WebSocketState.Open;

        /// <summary>True when a Kick user token with chat:write is available (send will be attempted).</summary>
        public bool CanSendMessages =>
            IsConnected &&
            !string.IsNullOrEmpty(_userAccessToken) &&
            !string.Equals(_tokenType, "app", StringComparison.OrdinalIgnoreCase);

        public string AuthorizedAs =>
            !string.IsNullOrEmpty(_settings.BotUsername) ? _settings.BotUsername : null;

        public event Action<string, string> OnMessageReceived;
        public event Action<string> OnConnected;
        public event Action<string> OnDisconnected;

        public KickService(StreamServiceSettings settings)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
            _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "RICS-RimWorld-Mod/1.6");
            _userAccessToken = _settings.AccessToken;
            Logger.Debug($"KickService constructor — Channel: {_settings.ChannelName}, HasClientId: {!string.IsNullOrEmpty(_settings.ClientId)}, HasUserToken: {!string.IsNullOrEmpty(_userAccessToken)}");
        }

        public void Connect()
        {
            if (IsConnected || _isConnecting)
            {
                Logger.Debug("KickService: Already connected or connecting, skipping");
                return;
            }

            if (string.IsNullOrEmpty(_settings.ChannelName))
            {
                Logger.Error("Kick: Missing channel name");
                Messages.Message("Kick connect failed: missing Channel Name", MessageTypeDefOf.NegativeEvent);
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
                if (!connectTask.Wait(TimeSpan.FromSeconds(20)))
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
                Logger.Debug("Kick: Starting full connection (20s timeout)...");

                EnsureUserTokenSynchronous();
                IntrospectUserTokenSynchronous();

                _chatroomId = GetChatroomIdSynchronous();
                Logger.Debug($"GetChatroomIdSynchronous() returned: '{_chatroomId ?? "NULL"}'");

                if (string.IsNullOrEmpty(_chatroomId))
                {
                    LongEventHandler.ExecuteWhenFinished(() =>
                        Messages.Message($"Kick: Channel '{_settings.ChannelName}' not found or not live", MessageTypeDefOf.NegativeEvent));
                    return;
                }

                TryResolveBroadcasterUserIdSynchronous();
                ConnectPusherSynchronous();

                _settings.IsConnected = true;
                bool canWrite = CanSendMessages && _broadcasterUserId > 0;
                LongEventHandler.ExecuteWhenFinished(() =>
                {
                    OnConnected?.Invoke(_settings.ChannelName);
                    Logger.Message($"SUCCESS: Joined Kick channel {_settings.ChannelName} (write={(canWrite ? "yes" : "read-only")})");
                    string extra = canWrite
                        ? (string.IsNullOrEmpty(AuthorizedAs) ? " Chat send ready." : $" Sending as {AuthorizedAs}.")
                        : " Read-only until you click Authorize Kick (user login with chat:write).";
                    Messages.Message($"[RICS] Connected to Kick: {_settings.ChannelName}.{extra}", MessageTypeDefOf.TaskCompletion);
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

        /// <summary>
        /// Opens the Kick consent page (OAuth 2.1 + PKCE) and stores the user access/refresh tokens.
        /// Required for POST /public/v1/chat. Read (Pusher) does not need this.
        /// </summary>
        public void BeginUserAuthorization()
        {
            if (_isAuthorizing)
            {
                Messages.Message("Kick authorization already in progress — finish in the browser.", MessageTypeDefOf.NeutralEvent);
                return;
            }

            if (string.IsNullOrEmpty(_settings.ClientId) || string.IsNullOrEmpty(_settings.ClientSecret))
            {
                Messages.Message("Kick: set Client ID and Client Secret first.", MessageTypeDefOf.NegativeEvent);
                return;
            }

            string redirectUri = GetRedirectUri();
            if (!Uri.TryCreate(redirectUri, UriKind.Absolute, out var redirect) ||
                (redirect.Scheme != "http" && redirect.Scheme != "https"))
            {
                Messages.Message("Kick: Redirect URI is invalid.", MessageTypeDefOf.NegativeEvent);
                return;
            }

            _isAuthorizing = true;
            _pkceVerifier = CreatePkceVerifier();
            _oauthState = CreatePkceVerifier().Substring(0, 32);
            string challenge = CreatePkceChallenge(_pkceVerifier);

            var authUri = new StringBuilder();
            authUri.Append(AuthorizeUrl).Append('?');
            // Kick Next.js rewrites the first 127.0.0.1 occurrence; dummy param protects a 127.0.0.1 redirect.
            if (redirect.Host == "127.0.0.1")
                authUri.Append("redirect=").Append(Uri.EscapeDataString("127.0.0.1")).Append('&');
            authUri.Append("response_type=code");
            authUri.Append("&client_id=").Append(Uri.EscapeDataString(_settings.ClientId));
            authUri.Append("&redirect_uri=").Append(Uri.EscapeDataString(redirectUri));
            authUri.Append("&scope=").Append(Uri.EscapeDataString(OAuthScopes));
            authUri.Append("&code_challenge=").Append(Uri.EscapeDataString(challenge));
            authUri.Append("&code_challenge_method=S256");
            authUri.Append("&state=").Append(Uri.EscapeDataString(_oauthState));

            string prefix = $"{redirect.Scheme}://{redirect.Authority}/";
            Logger.Debug($"Kick OAuth: listening on {prefix}, redirect {redirectUri}");

            Task.Run(() => RunAuthorizationListener(prefix, redirectUri));
            Application.OpenURL(authUri.ToString());
            Messages.Message("Kick: browser opened — approve chat:write, then return here.", MessageTypeDefOf.SilentInput);
        }

        private void RunAuthorizationListener(string prefix, string redirectUri)
        {
            HttpListener listener = null;
            try
            {
                listener = new HttpListener();
                listener.Prefixes.Add(prefix);
                _oauthListener = listener;
                listener.Start();
                Logger.Debug("Kick OAuth: HttpListener started. Waiting for callback (180s).");

                HttpListenerContext context = null;
                var wait = Task.Run(() => listener.GetContext());
                if (!wait.Wait(TimeSpan.FromSeconds(180)))
                {
                    try { listener.Abort(); } catch { }
                    NotifyUi("Kick authorization timed out. Click Authorize Kick and try again.", negative: true);
                    return;
                }
                context = wait.Result;
                string query = context.Request.Url?.Query ?? "";
                var args = ParseQuery(query);

                string html;
                if (args.TryGetValue("error", out string oauthError))
                {
                    html = HtmlPage("Kick authorization failed", $"Kick returned: {WebUtility.HtmlEncode(oauthError)}");
                    WriteCallback(context, html);
                    NotifyUi($"Kick authorization failed: {oauthError}", negative: true);
                    return;
                }

                if (!args.TryGetValue("state", out string state) || state != _oauthState)
                {
                    html = HtmlPage("Kick authorization failed", "State mismatch. Try Authorize Kick again.");
                    WriteCallback(context, html);
                    NotifyUi("Kick authorization failed: state mismatch.", negative: true);
                    return;
                }

                if (!args.TryGetValue("code", out string code) || string.IsNullOrEmpty(code))
                {
                    html = HtmlPage("Kick authorization failed", "No code in callback.");
                    WriteCallback(context, html);
                    NotifyUi("Kick authorization failed: no code returned.", negative: true);
                    return;
                }

                html = HtmlPage("RICS authorized", "You can close this tab and return to RimWorld.");
                WriteCallback(context, html);

                if (!ExchangeAuthorizationCodeSynchronous(code, redirectUri))
                {
                    NotifyUi("Kick: token exchange failed — check Player.log", negative: true);
                    return;
                }

                IntrospectUserTokenSynchronous();
                TryLoadAuthorizedUserSynchronous();
                TryResolveBroadcasterUserIdSynchronous();

                LongEventHandler.ExecuteWhenFinished(() =>
                {
                    try { CAPChatInteractiveMod.Instance?.WriteSettings(); }
                    catch (Exception ex) { Logger.Warning($"Kick: could not persist tokens: {ex.Message}"); }

                    string who = AuthorizedAs ?? "Kick user";
                    bool writeReady = !string.IsNullOrEmpty(_userAccessToken) &&
                                      !string.Equals(_tokenType, "app", StringComparison.OrdinalIgnoreCase);
                    Logger.Message($"Kick user OAuth succeeded as {who} (token_type={_tokenType ?? "?"}, writeReady={writeReady}, broadcaster={_broadcasterUserId})");
                    Messages.Message(
                        writeReady
                            ? $"[RICS] Kick authorized as {who}. Connect (or reconnect) to send chat."
                            : "[RICS] Kick login stored but token is not a user chat:write token. Check Player.log.",
                        writeReady ? MessageTypeDefOf.TaskCompletion : MessageTypeDefOf.NegativeEvent);
                });
            }
            catch (HttpListenerException hex)
            {
                Logger.Error($"Kick OAuth HttpListener failed: {hex.Message}");
                NotifyUi($"Kick: could not listen on {prefix}. Close other apps on that port, or pick another Redirect URI and register it on kick.com/settings/developer.", negative: true);
            }
            catch (AggregateException aex)
            {
                aex = aex.Flatten();
                Logger.Error($"Kick OAuth AggregateException: {aex.InnerException?.Message ?? aex.Message}");
                NotifyUi("Kick authorization failed — check Player.log", negative: true);
            }
            catch (Exception ex)
            {
                Logger.Error($"Kick OAuth crashed: {ex.Message}\n{ex.StackTrace}");
                NotifyUi("Kick authorization failed — check Player.log", negative: true);
            }
            finally
            {
                _isAuthorizing = false;
                _pkceVerifier = null;
                _oauthState = null;
                try { listener?.Stop(); listener?.Close(); } catch { }
                _oauthListener = null;
            }
        }

        private bool ExchangeAuthorizationCodeSynchronous(string code, string redirectUri)
        {
            try
            {
                var form = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    { "grant_type", "authorization_code" },
                    { "client_id", _settings.ClientId },
                    { "client_secret", _settings.ClientSecret },
                    { "redirect_uri", redirectUri },
                    { "code_verifier", _pkceVerifier ?? "" },
                    { "code", code }
                });

                var postTask = _httpClient.PostAsync(TokenUrl, form);
                var response = postTask.Result;
                var readTask = response.Content.ReadAsStringAsync();
                string body = readTask.Result;

                Logger.Debug($"Kick auth-code token — Status: {response.StatusCode} | Body length: {body.Length}");
                if (!response.IsSuccessStatusCode)
                {
                    Logger.Error($"Kick token exchange failed: {response.StatusCode} - {body}");
                    return false;
                }

                return ApplyUserTokenResponse(body);
            }
            catch (AggregateException aex)
            {
                aex = aex.Flatten();
                Logger.Error($"Kick token exchange AggregateException: {aex.InnerException?.Message ?? aex.Message}");
                return false;
            }
            catch (Exception ex)
            {
                Logger.Error($"Kick token exchange crashed: {ex.Message}\n{ex.StackTrace}");
                return false;
            }
        }

        private bool EnsureUserTokenSynchronous()
        {
            _userAccessToken = _settings.AccessToken;
            if (!string.IsNullOrEmpty(_settings.RefreshToken))
                return RefreshUserAccessTokenSynchronous();

            if (!string.IsNullOrEmpty(_userAccessToken))
            {
                Logger.Debug("Kick: using stored user access token (no refresh token)");
                return true;
            }

            Logger.Debug("Kick: no user access/refresh token — chat send disabled until Authorize Kick");
            return false;
        }

        private bool RefreshUserAccessTokenSynchronous()
        {
            if (string.IsNullOrEmpty(_settings.RefreshToken) ||
                string.IsNullOrEmpty(_settings.ClientId) ||
                string.IsNullOrEmpty(_settings.ClientSecret))
            {
                return !string.IsNullOrEmpty(_userAccessToken);
            }

            Logger.Debug("Kick: Refreshing user access token");
            try
            {
                var form = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    { "grant_type", "refresh_token" },
                    { "client_id", _settings.ClientId },
                    { "client_secret", _settings.ClientSecret },
                    { "refresh_token", _settings.RefreshToken }
                });

                var postTask = _httpClient.PostAsync(TokenUrl, form);
                var response = postTask.Result;
                var readTask = response.Content.ReadAsStringAsync();
                string body = readTask.Result;

                Logger.Debug($"Kick refresh token — Status: {response.StatusCode} | Body length: {body.Length}");
                if (!response.IsSuccessStatusCode)
                {
                    Logger.Error($"Kick refresh failed: {response.StatusCode} - {body}");
                    return !string.IsNullOrEmpty(_userAccessToken);
                }

                return ApplyUserTokenResponse(body);
            }
            catch (AggregateException aex)
            {
                aex = aex.Flatten();
                Logger.Error($"Kick refresh AggregateException: {aex.InnerException?.Message ?? aex.Message}");
                return !string.IsNullOrEmpty(_userAccessToken);
            }
            catch (Exception ex)
            {
                Logger.Error($"Kick refresh crashed: {ex.Message}\n{ex.StackTrace}");
                return !string.IsNullOrEmpty(_userAccessToken);
            }
        }

        private bool ApplyUserTokenResponse(string body)
        {
            var tokenResponse = JsonConvert.DeserializeObject<KickTokenResponse>(body);
            if (string.IsNullOrEmpty(tokenResponse?.AccessToken))
            {
                Logger.Error("Kick token response had no access_token");
                return false;
            }

            _userAccessToken = tokenResponse.AccessToken;
            _settings.AccessToken = tokenResponse.AccessToken;
            if (!string.IsNullOrEmpty(tokenResponse.RefreshToken))
                _settings.RefreshToken = tokenResponse.RefreshToken;

            int expires = tokenResponse.ExpiresIn > 0 ? tokenResponse.ExpiresIn : 3600;
            _userTokenExpiresAt = DateTime.UtcNow.AddSeconds(Math.Max(60, expires - 60));
            _tokenType = "user";
            Logger.Debug($"Kick: user token stored (expires_in={expires}, hasRefresh={!string.IsNullOrEmpty(_settings.RefreshToken)})");
            return true;
        }

        private void IntrospectUserTokenSynchronous()
        {
            if (string.IsNullOrEmpty(_userAccessToken))
                return;

            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, IntrospectUrl);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _userAccessToken);

                var sendTask = _httpClient.SendAsync(request);
                var response = sendTask.Result;
                var readTask = response.Content.ReadAsStringAsync();
                string body = readTask.Result;

                Logger.Debug($"Kick introspect — Status: {response.StatusCode} | Body: {TrimForLog(body, 400)}");
                if (!response.IsSuccessStatusCode)
                    return;

                var obj = JObject.Parse(body);
                var data = obj["data"] as JObject ?? obj;
                _tokenType = (string)data["token_type"];
                string scope = (string)data["scope"];
                bool? active = (bool?)data["active"];
                Logger.Message($"Kick token introspect: type={_tokenType ?? "?"}, active={active}, scope={scope ?? "?"}");
            }
            catch (Exception ex)
            {
                Logger.Warning($"Kick introspect skipped: {ex.Message}");
            }
        }

        private void TryLoadAuthorizedUserSynchronous()
        {
            if (string.IsNullOrEmpty(_userAccessToken))
                return;

            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, UsersUrl);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _userAccessToken);

                var sendTask = _httpClient.SendAsync(request);
                var response = sendTask.Result;
                var readTask = response.Content.ReadAsStringAsync();
                string body = readTask.Result;
                if (!response.IsSuccessStatusCode)
                {
                    Logger.Warning($"Kick GET /users failed: {response.StatusCode} - {TrimForLog(body, 300)}");
                    return;
                }

                var obj = JObject.Parse(body);
                var data = obj["data"] as JArray;
                var first = data?.First as JObject;
                string name = (string)first?["name"];
                if (!string.IsNullOrEmpty(name))
                {
                    _settings.BotUsername = name;
                    Logger.Debug($"Kick authorized user: {name} (id={first?["user_id"]})");
                }
            }
            catch (Exception ex)
            {
                Logger.Warning($"Kick GET /users skipped: {ex.Message}");
            }
        }

        private void TryResolveBroadcasterUserIdSynchronous()
        {
            if (string.IsNullOrEmpty(_settings.ChannelName))
                return;

            string slug = _settings.ChannelName.Trim().TrimStart('@').ToLowerInvariant();
            string token = _userAccessToken;
            if (string.IsNullOrEmpty(token))
                return;

            try
            {
                string url = $"{ChannelsUrl}?slug={Uri.EscapeDataString(slug)}";
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

                var sendTask = _httpClient.SendAsync(request);
                var response = sendTask.Result;
                var readTask = response.Content.ReadAsStringAsync();
                string body = readTask.Result;
                Logger.Debug($"Kick GET channels — Status: {response.StatusCode} | Body length: {body.Length}");
                if (!response.IsSuccessStatusCode)
                {
                    Logger.Warning($"Kick GET /channels failed: {response.StatusCode} - {TrimForLog(body, 300)}");
                    return;
                }

                var obj = JObject.Parse(body);
                var data = obj["data"] as JArray;
                var first = data?.First as JObject;
                long id = (long?)first?["broadcaster_user_id"] ?? 0;
                if (id > 0)
                {
                    _broadcasterUserId = id;
                    Logger.Debug($"Kick broadcaster_user_id={id} for slug '{slug}'");
                }
                else
                {
                    Logger.Warning($"Kick: no broadcaster_user_id in channels response for '{slug}'");
                }
            }
            catch (Exception ex)
            {
                Logger.Warning($"Kick channel id resolve skipped: {ex.Message}");
            }
        }

        private string GetChatroomIdSynchronous()
        {
            Logger.Debug($"Kick: Fetching chatroom ID for channel '{_settings.ChannelName}' using v1 endpoint (public, no token)");
            string v1Url = $"https://kick.com/api/v1/channels/{_settings.ChannelName.ToLowerInvariant()}";

            try
            {
                Logger.Debug($"Kick: GET {v1Url}");
                using var request = new HttpRequestMessage(HttpMethod.Get, v1Url);
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

                var channelData = JsonConvert.DeserializeObject<KickV1ChannelResponse>(jsonText);
                if (channelData?.chatroom?.id > 0)
                {
                    string id = channelData.chatroom.id.ToString();
                    Logger.Debug($"Kick chatroom ID resolved: {id}");
                    return id;
                }

                Logger.Warning("Kick: Channel data received but no chatroom.id found");
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

        private class KickV1ChannelResponse
        {
            public KickV1Chatroom chatroom { get; set; }
        }

        private class KickV1Chatroom
        {
            public long id { get; set; }
        }

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
                Logger.Debug("Pusher WebSocket connected and subscribed - listen task launched");
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
                        var obj = JObject.Parse(json);
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
                var obj = JObject.Parse(json);

                var dataStr = (string)obj["data"];
                if (string.IsNullOrEmpty(dataStr))
                {
                    Logger.Debug("Kick: Skipped - no data string");
                    return;
                }

                var dataObj = JObject.Parse(dataStr);
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
                    platformMessage: sender,
                    isWhisper: false
                );

                ChatMessageLogger.AddMessage(wrapper.Username, wrapper.Message, "Kick");
                OnMessageReceived?.Invoke(wrapper.Username, wrapper.Message);
                Viewers.UpdateViewerActivity(wrapper);

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
            if (string.IsNullOrWhiteSpace(message))
                return;

            if (!IsConnected)
            {
                Logger.Warning("Kick: Cannot send message - not connected");
                return;
            }

            if (string.IsNullOrEmpty(_userAccessToken) ||
                string.Equals(_tokenType, "app", StringComparison.OrdinalIgnoreCase))
            {
                Logger.Warning("Kick: Cannot send — need user OAuth (Authorize Kick) with chat:write. App/client-credentials tokens cannot post chat.");
                NoticeSendFailureOnce("Kick chat send needs Authorize Kick (user login). Reading still works.");
                return;
            }

            message = message.Replace("\r\n", "\n").Replace("\n\n", "\n").Trim();
            if (message.Length > MaxChatLength)
                message = message.Substring(0, MaxChatLength);

            var now = DateTime.Now;
            if (now - _lastMessageTime < _messageDelay)
                Thread.Sleep(_messageDelay - (now - _lastMessageTime));

            // Background + fully synchronous HTTP (no async lambda — Mono crash).
            Task.Run(() =>
            {
                try
                {
                    SendMessageSynchronous(message, retryOnUnauthorized: true);
                }
                catch (Exception ex)
                {
                    Logger.Error($"Failed to send Kick message: {ex.Message}");
                }
            });
        }

        private void SendMessageSynchronous(string message, bool retryOnUnauthorized)
        {
            if (_broadcasterUserId <= 0)
                TryResolveBroadcasterUserIdSynchronous();

            if (_broadcasterUserId <= 0)
            {
                Logger.Error("Kick send aborted: broadcaster_user_id unknown (GET /public/v1/channels failed)");
                NoticeSendFailureOnce("Kick send failed: could not resolve channel id. Check Player.log.");
                return;
            }

            if (_userTokenExpiresAt != DateTime.MinValue && DateTime.UtcNow >= _userTokenExpiresAt)
                RefreshUserAccessTokenSynchronous();

            var payload = new
            {
                broadcaster_user_id = _broadcasterUserId,
                content = message,
                type = "user"
            };

            var content = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json");
            using var request = new HttpRequestMessage(HttpMethod.Post, ChatUrl);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _userAccessToken);
            request.Content = content;

            Logger.Debug($"Kick: POST {ChatUrl} type=user broadcaster={_broadcasterUserId} → {TrimForLog(message, 80)}");

            var sendTask = _httpClient.SendAsync(request);
            var response = sendTask.Result;
            var readTask = response.Content.ReadAsStringAsync();
            string responseBody = readTask.Result;

            if (response.IsSuccessStatusCode)
            {
                _lastMessageTime = DateTime.Now;
                Logger.Debug($"[Kick] Successfully sent ({response.StatusCode}): {TrimForLog(responseBody, 200)}");
                return;
            }

            if (retryOnUnauthorized && (int)response.StatusCode == 401)
            {
                Logger.Warning($"Kick send 401 — refreshing user token and retrying. Body: {TrimForLog(responseBody, 300)}");
                if (RefreshUserAccessTokenSynchronous())
                {
                    SendMessageSynchronous(message, retryOnUnauthorized: false);
                    return;
                }
            }

            Logger.Error($"Kick send failed — Status: {response.StatusCode} | Body: {responseBody}");
            NoticeSendFailureOnce($"Kick send failed ({(int)response.StatusCode}). See Player.log.");
        }

        public Task SendWhisperAsync(string username, string message)
        {
            SendMessage($"@{username} {message}");
            return Task.CompletedTask;
        }

        public string GetRedirectUri()
        {
            if (!string.IsNullOrWhiteSpace(_settings.RedirectUri))
                return _settings.RedirectUri.Trim();
            return DefaultRedirectUri;
        }

        public static bool HasKickConnectCredentials(StreamServiceSettings settings)
        {
            return settings != null &&
                   !string.IsNullOrEmpty(settings.ChannelName) &&
                   !string.IsNullOrEmpty(settings.ClientId) &&
                   !string.IsNullOrEmpty(settings.ClientSecret);
        }

        private void NoticeSendFailureOnce(string message)
        {
            if (DateTime.Now - _lastSendFailureNotice < TimeSpan.FromSeconds(20))
                return;
            _lastSendFailureNotice = DateTime.Now;
            LongEventHandler.ExecuteWhenFinished(() =>
                Messages.Message(message, MessageTypeDefOf.NegativeEvent));
        }

        private static void NotifyUi(string message, bool negative)
        {
            LongEventHandler.ExecuteWhenFinished(() =>
                Messages.Message(message, negative ? MessageTypeDefOf.NegativeEvent : MessageTypeDefOf.TaskCompletion));
        }

        private static Dictionary<string, string> ParseQuery(string query)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrEmpty(query))
                return result;
            if (query.StartsWith("?"))
                query = query.Substring(1);
            foreach (var part in query.Split('&'))
            {
                if (string.IsNullOrEmpty(part))
                    continue;
                int eq = part.IndexOf('=');
                if (eq <= 0)
                    continue;
                string key = Uri.UnescapeDataString(part.Substring(0, eq).Replace('+', ' '));
                string value = Uri.UnescapeDataString(part.Substring(eq + 1).Replace('+', ' '));
                result[key] = value;
            }
            return result;
        }

        private static void WriteCallback(HttpListenerContext context, string html)
        {
            try
            {
                byte[] bytes = Encoding.UTF8.GetBytes(html);
                context.Response.ContentType = "text/html; charset=utf-8";
                context.Response.ContentLength64 = bytes.Length;
                context.Response.StatusCode = 200;
                context.Response.OutputStream.Write(bytes, 0, bytes.Length);
                context.Response.OutputStream.Close();
            }
            catch (Exception ex)
            {
                Logger.Warning($"Kick OAuth callback write: {ex.Message}");
            }
        }

        private static string HtmlPage(string title, string body)
        {
            return "<!DOCTYPE html><html><head><meta charset=\"utf-8\"><title>" +
                   WebUtility.HtmlEncode(title) +
                   "</title></head><body style=\"font-family:sans-serif;background:#111;color:#eee;padding:2rem\">" +
                   "<h1>" + WebUtility.HtmlEncode(title) + "</h1><p>" +
                   WebUtility.HtmlEncode(body) +
                   "</p></body></html>";
        }

        private static string CreatePkceVerifier()
        {
            var bytes = new byte[32];
            using (var rng = RandomNumberGenerator.Create())
                rng.GetBytes(bytes);
            return Base64Url(bytes);
        }

        private static string CreatePkceChallenge(string verifier)
        {
            using var sha = SHA256.Create();
            byte[] hash = sha.ComputeHash(Encoding.ASCII.GetBytes(verifier));
            return Base64Url(hash);
        }

        private static string Base64Url(byte[] data)
        {
            return Convert.ToBase64String(data).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        }

        private static string TrimForLog(string text, int max)
        {
            if (string.IsNullOrEmpty(text))
                return "";
            return text.Length <= max ? text : text.Substring(0, max) + "…";
        }

        private class KickTokenResponse
        {
            [JsonProperty("access_token")]
            public string AccessToken { get; set; }

            [JsonProperty("refresh_token")]
            public string RefreshToken { get; set; }

            [JsonProperty("expires_in")]
            public int ExpiresIn { get; set; }

            [JsonProperty("scope")]
            public string Scope { get; set; }

            [JsonProperty("token_type")]
            public string TokenType { get; set; }
        }
    }
}
