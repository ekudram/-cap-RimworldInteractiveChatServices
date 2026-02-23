// ChatMessageWrapper.cs
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
// A unified wrapper for chat messages from different platforms (Twitch, YouTube)
using System;
using System.Linq;
using Verse;
public class ChatMessageWrapper
{
    public string Username { get; }
    public string DisplayName { get; }
    public string Message { get; }
    public string Platform { get; } // "Twitch" or "YouTube"
    public bool IsWhisper { get; }
    public bool ShouldIgnoreForCommands { get; }  // NEW property
    public string CustomRewardId { get; }
    public int Bits { get; }
    public string PlatformUserId { get; }
    public string ChannelId { get; }
    public object PlatformMessage { get; }
    public DateTime Timestamp { get; }

    // Updated constructor with new parameter
    public ChatMessageWrapper(string username, string message, string platform,
                                string platformUserId = null, string channelId = null,
                                object platformMessage = null, bool isWhisper = false,
                                string customRewardId = null, int bits = 0,
                                bool shouldIgnoreForCommands = false)  // NEW parameter
    {
        Username = username?.ToLowerInvariant() ?? "";
        DisplayName = username ?? "";
        Message = CleanInput(message) ?? "";  // strips garbage + trims – now every command/arg is clean
        Platform = platform;
        PlatformUserId = platformUserId;
        ChannelId = channelId;
        PlatformMessage = platformMessage;
        IsWhisper = isWhisper;
        ShouldIgnoreForCommands = shouldIgnoreForCommands;  // Initialize
        CustomRewardId = customRewardId;
        Bits = bits;
        Timestamp = DateTime.Now;
    }

    // Copy constructor needs to copy the new property
    private ChatMessageWrapper(ChatMessageWrapper original, string newMessage)
    {
        Username = original.Username;
        DisplayName = original.DisplayName;
        Message = CleanInput(newMessage) ?? "";
        Platform = original.Platform;
        PlatformUserId = original.PlatformUserId;
        ChannelId = original.ChannelId;
        PlatformMessage = original.PlatformMessage;
        IsWhisper = original.IsWhisper;
        ShouldIgnoreForCommands = original.ShouldIgnoreForCommands;  // Copy
        CustomRewardId = original.CustomRewardId;
        Bits = original.Bits;
        Timestamp = original.Timestamp;
    }

    public ChatMessageWrapper WithMessage(string newMessage)
    {
        return new ChatMessageWrapper(this, newMessage);
    }

    public string GetUniqueId()
    {
        return $"{Platform}:{PlatformUserId ?? Username}";
    }

    private static string CleanInput(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;

        // Remove invisible/chat-platform garbage (zero-width spaces, joiners, CGJ, direction marks, etc.)
        // while keeping ALL spaces, letters, numbers, punctuation – exactly what commands need
        return new string(input.Where(c => !char.IsControl(c) &&
                                           c != '\u200B' && c != '\u200C' && c != '\u200D' &&
                                           c != '\uFEFF' && c != '\u3164' && c != '\u034F' &&
                                           c != '\u200E' && c != '\u200F' && c != '\u2060').ToArray()).Trim();
    }
}