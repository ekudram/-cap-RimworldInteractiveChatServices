// Utilities/ChatMessageLogger.cs
using CAP_ChatInteractive.Windows;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CAP_ChatInteractive
{
    public static class ChatMessageLogger
    {
        private static readonly List<ChatMessageDisplay> _messages = new List<ChatMessageDisplay>();
        private static readonly object _lock = new object();
        private const int MAX_MESSAGES = 1000;

        public static void AddMessage(string username, string message, string platform)
        {
            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(message))
                return;

            lock (_lock)
            {
                _messages.Add(new ChatMessageDisplay
                {
                    Username = username,
                    Text = message,
                    Platform = platform,
                    IsSystem = false,
                    Timestamp = DateTime.Now
                });

                // Trim old messages
                if (_messages.Count > MAX_MESSAGES)
                {
                    _messages.RemoveRange(0, _messages.Count - MAX_MESSAGES);
                }

                Logger.Debug($"Logged message from {username}: {message}");

                // Trigger auto-scroll in chat window
                Window_LiveChat.NotifyNewChatMessage();
            }
        }

        public static void AddSystemMessage(string message)
        {
            if (string.IsNullOrEmpty(message))
                return;

            lock (_lock)
            {
                _messages.Add(new ChatMessageDisplay
                {
                    Username = "System",
                    Text = message,
                    Platform = "system",
                    IsSystem = true,
                    Timestamp = DateTime.Now
                });

                if (_messages.Count > MAX_MESSAGES)
                {
                    _messages.RemoveRange(0, _messages.Count - MAX_MESSAGES);
                }

                Logger.Debug($"Logged system message: {message}");

                // Trigger auto-scroll in chat window
                Window_LiveChat.NotifyNewChatMessage();
            }
        }

        public static List<ChatMessageDisplay> GetRecentMessages(int count = 50)
        {
            lock (_lock)
            {
                return _messages.TakeLast(Math.Min(count, _messages.Count)).ToList();
            }
        }

        public static void Clear()
        {
            lock (_lock)
            {
                _messages.Clear();
            }
        }
    }
}