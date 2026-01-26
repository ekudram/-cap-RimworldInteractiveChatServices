using System;

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
        Message = message?.Trim() ?? "";
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
        Message = newMessage;
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
}