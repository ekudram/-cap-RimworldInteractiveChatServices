// ChatMessageWrapper.cs
// Copyright (c) Captolamia. All rights reserved.
// Licensed under the AGPLv3 License. See LICENSE file in the project root for full license information.
// A unified wrapper for chat messages from different platforms (Twitch, YouTube)
using System;
using System.Collections.Generic;
using System.Linq;

namespace CAP_ChatInteractive
{
    public class ChatMessageWrapper
    {
        public string Username { get; }
        public string DisplayName { get; }
        public string Message { get; }
        public string Platform { get; } // "Twitch" or "YouTube"
        public bool IsWhisper { get; }
        public string CustomRewardId { get; }
        public int Bits { get; }

        // Platform-specific IDs
        public string PlatformUserId { get; } // Unique ID from the platform
        public string ChannelId { get; } // Channel/stream ID

        // Platform-specific properties (optional)
        public object PlatformMessage { get; } // Original message object

        public DateTime Timestamp { get; }

        // Constructor for messages
        public ChatMessageWrapper(string username, string message, string platform,
                                string platformUserId = null, string channelId = null,
                                object platformMessage = null, bool isWhisper = false,
                                string customRewardId = null, int bits = 0)  // Add new parameters
        {
            Username = username?.ToLowerInvariant() ?? "";
            DisplayName = username ?? "";
            Message = message?.Trim() ?? "";
            Platform = platform;
            PlatformUserId = platformUserId;
            ChannelId = channelId;
            PlatformMessage = platformMessage;
            IsWhisper = isWhisper;
            CustomRewardId = customRewardId;  // Initialize new property
            Bits = bits;                      // Initialize new property
            Timestamp = DateTime.Now;
        }

        // Create a copy with modified message
        public ChatMessageWrapper WithMessage(string newMessage)
        {
            return new ChatMessageWrapper(this, newMessage);
        }

        private ChatMessageWrapper(ChatMessageWrapper original, string newMessage)
        {
            Username = original.Username;
            DisplayName = original.DisplayName;
            Message = newMessage;
            Platform = original.Platform;
            PlatformUserId = original.PlatformUserId;
            ChannelId = original.ChannelId;
            PlatformMessage = original.PlatformMessage;
            IsWhisper = original.IsWhisper;
            CustomRewardId = original.CustomRewardId;  // Copy the reward ID
            Bits = original.Bits;                      // Copy the bits
            Timestamp = original.Timestamp;
        }
        public string GetUniqueId()
        {
            // Combine platform and user ID for true uniqueness
            return $"{Platform}:{PlatformUserId ?? Username}";
        }
    }
}