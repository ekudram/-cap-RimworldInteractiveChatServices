// Services/KickIntegrationResearch.cs
// Copyright (c) Captolamia — part of RICS (AGPL v3)
// Research + implementation notes for Kick.com support (no NuGet, pure .NET 4.7.2)

namespace CAP_ChatInteractive
{
    /// <summary>
    /// Kick.com notes for maintainers. Official public API + public Pusher read.
    /// Do not send via unofficial v2 messages/send — that path never worked.
    /// </summary>
    public static class KickIntegrationResearch
    {
        public const string PusherKey = "32cbd69e4b950bf97679";
        public const string PusherUrl = "wss://ws-us2.pusher.com/app/" + PusherKey;

        // Read:
        // 1. GET https://kick.com/api/v1/channels/{slug} → chatroom.id (public, unofficial, used for Pusher)
        // 2. ClientWebSocket subscribe chatrooms.{id}.v2
        // 3. ChatMessageEvent → ChatCommandProcessor
        //
        // Write (official, docs.kick.com/apis/chat):
        // 1. OAuth 2.1 authorization code + PKCE (NOT client_credentials)
        // 2. Scope chat:write — UserAccessToken only
        // 3. POST https://api.kick.com/public/v1/chat
        //    { broadcaster_user_id, content (max 500), type: "user" }
        // 4. type: "bot" has returned 500 for third-party apps (KickDevDocs #343, Mar 2026)
        // 5. App/client_credentials tokens cannot post chat
        //
        // Resolve broadcaster_user_id:
        // GET https://api.kick.com/public/v1/channels?slug={slug}
        //
        // Tokens:
        // POST https://id.kick.com/oauth/token  grant_type=authorization_code | refresh_token
        // POST https://id.kick.com/oauth/token/introspect  → token_type user|app
    }
}
