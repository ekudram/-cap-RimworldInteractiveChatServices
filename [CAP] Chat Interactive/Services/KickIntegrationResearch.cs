// Services/KickIntegrationResearch.cs
// Copyright (c) Captolamia — part of RICS (AGPL v3)
// Research + implementation notes for Kick.com support (no NuGet, pure .NET 4.7.2)

using System;
using Verse;

namespace CAP_ChatInteractive
{
    /// <summary>
    /// Research summary + future KickService skeleton.
    /// Official path chosen over KickLib (framework mismatch risk).
    /// </summary>
    public static class KickIntegrationResearch
    {
        public const string PusherKey = "32cbd69e4b950bf97679"; // public, stable
        public const string PusherUrl = "wss://ws-us2.pusher.com/app/" + PusherKey;

        // Usage in future KickService.Connect():
        // 1. GET https://api.kick.com/api/v2/channels/{slug} → extract chatroom.id
        // 2. ClientWebSocket → subscribe {"event":"pusher:subscribe","data":{"channel":"chatrooms.{ID}"}}
        // 3. Listen for event: "App\\Events\\ChatMessageSentEvent"
        // 4. Send via HttpClient Bearer token (chat:write)

        // Test scenario:
        // - Debug menu: "Force Kick connect"
        // - No internet: fallback to "Kick offline" message
        // - Invalid secret: Messages.Message + log (like Twitch IncorrectLogin)
    }
}
