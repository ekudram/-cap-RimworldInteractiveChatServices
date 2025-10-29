// Models/ChatMessageDisplay.cs
using System;

namespace CAP_ChatInteractive
{
    public class ChatMessageDisplay
    {
        public string Username { get; set; }
        public string Text { get; set; }
        public string Platform { get; set; }
        public bool IsSystem { get; set; }
        public DateTime Timestamp { get; set; }
    }
}