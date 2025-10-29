// MessageLogger.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Verse;

namespace CAP_ChatInteractive
{
    public static class MessageLogger
    {
        private static readonly List<ChatMessageWrapper> _messageHistory = new List<ChatMessageWrapper>();
        private const int MAX_HISTORY = 1000;

        public static void LogMessage(ChatMessageWrapper message)
        {
            _messageHistory.Add(message);

            // Trim history if needed
            if (_messageHistory.Count > MAX_HISTORY)
            {
                _messageHistory.RemoveRange(0, _messageHistory.Count - MAX_HISTORY);
            }

            Logger.Debug($"[{message.Platform}] {message.Username}: {message.Message}");
        }

        public static IEnumerable<ChatMessageWrapper> GetRecentMessages(int count = 50)
        {
            return _messageHistory.TakeLast(count);
        }

        public static void SaveChatLog()
        {
            try
            {
                var path = Path.Combine(GenFilePaths.SaveDataFolderPath, "CAP_ChatLog.txt");
                var lines = _messageHistory.Select(m =>
                    $"[{m.Timestamp:yyyy-MM-dd HH:mm:ss}] [{m.Platform}] {m.Username}: {m.Message}");

                File.WriteAllLines(path, lines);
            }
            catch (Exception ex)
            {
                Logger.Error($"Error saving chat log: {ex.Message}");
            }
        }
    }
}