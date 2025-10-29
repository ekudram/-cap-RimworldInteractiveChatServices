// Viewer.cs
using System;
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace CAP_ChatInteractive
{
    public class Viewer
    {
        public string Username { get; set; }
        public string DisplayName { get; set; }

        // Platform-specific IDs
        public Dictionary<string, string> PlatformUserIds { get; set; } // Platform -> UserId

        // Viewer status
        public bool IsModerator { get; set; }
        public bool IsSubscriber { get; set; }
        public bool IsVip { get; set; }
        public bool IsBroadcaster { get; set; }
        public bool IsBanned { get; set; }

        // Activity tracking
        public DateTime LastSeen { get; set; }
        public DateTime FirstSeen { get; set; }
        public int MessageCount { get; set; }

        // Economy
        public int Coins { get; set; }
        public int Karma { get; set; }
        public string AssignedPawnId { get; set; }

        // Platform-specific data
        public string ColorCode { get; set; }

        public Viewer(string username)
        {
            Username = username?.ToLowerInvariant() ?? "";
            DisplayName = username ?? ""; // Capitalize this
            PlatformUserIds = new Dictionary<string, string>();
            FirstSeen = DateTime.Now;
            LastSeen = DateTime.Now;

            // Initialize with default values from settings
            var settings = CAPChatInteractiveMod.Instance.Settings;
            Coins = settings.GlobalSettings.StartingCoins;
            Karma = settings.GlobalSettings.StartingKarma;
        }

        public void AddPlatformUserId(string platform, string userId)
        {
            if (!string.IsNullOrEmpty(platform) && !string.IsNullOrEmpty(userId))
            {
                // Always use lowercase for consistency
                string platformKey = platform.ToLowerInvariant();
                // Logger.Debug($"Adding platform ID: {platformKey} -> {userId} for user {Username}");
                PlatformUserIds[platformKey] = userId;
            }
            else
            {
                // Logger.Warning($"Cannot add platform ID - platform: '{platform}', userId: '{userId}'");
            }
        }

        public string GetPlatformUserId(string platform)
        {
            return PlatformUserIds.TryGetValue(platform.ToLowerInvariant(), out string userId)
                ? userId
                : null;
        }
        public string GetRoleString()
        {
            if (IsBroadcaster) return "Broadcaster";
            if (IsModerator) return "Moderator";
            if (IsVip) return "VIP";
            if (IsSubscriber) return "Subscriber";
            return "Viewer";
        }


        public bool HasPlatform(string platform)
        {
            return PlatformUserIds.ContainsKey(platform.ToLowerInvariant());
        }

        public bool HasAnySpecialRole()
        {
            return IsBroadcaster || IsModerator || IsVip || IsSubscriber;
        }

        public string GetPlatformRoleInfo()
        {
            var roles = new List<string>();

            if (IsBroadcaster) roles.Add("Broadcaster");
            if (IsModerator) roles.Add("Moderator");
            if (IsVip) roles.Add("VIP");
            if (IsSubscriber) roles.Add("Subscriber");

            return roles.Count > 0 ? string.Join(", ", roles) : "Regular Viewer";
        }
        // Coin management
        public int GetCoins() => Coins;

        public void SetCoins(int coins)
        {
            Coins = Math.Max(0, coins);
        }

        public void GiveCoins(int coins)
        {
            Coins = Math.Max(0, Coins + coins);
        }

        public bool TakeCoins(int coins)
        {
            if (Coins >= coins)
            {
                Coins -= coins;
                return true;
            }
            return false;
        }

        // Karma management
        public int GetKarma() => Karma;

        public void SetKarma(int karma)
        {
            var settings = CAPChatInteractiveMod.Instance.Settings;
            Karma = Math.Clamp(karma, settings.GlobalSettings.MinKarma, settings.GlobalSettings.MaxKarma);
        }

        public void GiveKarma(int karma)
        {
            SetKarma(Karma + karma);
        }

        public void TakeKarma(int karma)
        {
            SetKarma(Karma - karma);
        }

        // Activity tracking
        public void UpdateActivity()
        {
            LastSeen = DateTime.Now;
            MessageCount++;
        }

        public TimeSpan GetTimeSinceLastActivity()
        {
            return DateTime.Now - LastSeen;
        }

        public bool IsActive(int maxMinutesInactive = 30)
        {
            return GetTimeSinceLastActivity().TotalMinutes <= maxMinutesInactive;
        }

        // Permission checking
        public bool HasPermission(string permissionLevel)
        {
            return permissionLevel.ToLowerInvariant() switch
            {
                "broadcaster" => IsBroadcaster,
                "moderator" => IsModerator || IsBroadcaster,
                "vip" => IsVip || IsModerator || IsBroadcaster,
                "subscriber" => IsSubscriber || IsVip || IsModerator || IsBroadcaster,
                "everyone" => true,
                _ => false
            };
        }

        public void UpdateFromMessage(ChatMessageWrapper message)
        {
            // Logger.Debug($"Updating viewer {Username} from message on {message.Platform}");
            UpdateActivity();

            // Add platform user ID if available
            if (!string.IsNullOrEmpty(message.PlatformUserId))
            {
                AddPlatformUserId(message.Platform, message.PlatformUserId);
            }

            // Update display name if different
            if (!string.IsNullOrEmpty(message.DisplayName) && message.DisplayName != Username)
            {
                DisplayName = message.DisplayName;
            }

            // Update platform-specific roles from the message
            UpdatePlatformRoles(message);
        }
        private void UpdatePlatformRoles(ChatMessageWrapper message)
        {
            switch (message.Platform.ToLowerInvariant())
            {
                case "twitch":
                    UpdateTwitchRoles(message);
                    break;
                case "youtube":
                    UpdateYouTubeRoles(message);
                    break;
            }
        }
        private void UpdateTwitchRoles(ChatMessageWrapper message)
        {
            if (message.PlatformMessage is TwitchLib.Client.Models.ChatMessage twitchMessage)
            {
                // Extract roles from Twitch badges
                IsModerator = twitchMessage.IsModerator;
                IsSubscriber = twitchMessage.IsSubscriber;
                IsVip = twitchMessage.IsVip;
                IsBroadcaster = twitchMessage.IsBroadcaster;

                // You can also parse specific badges if needed
                if (twitchMessage.Badges != null)
                {
                    foreach (var badge in twitchMessage.Badges)
                    {
                        // Handle specific badge types
                        switch (badge.Key.ToLowerInvariant())
                        {
                            case "broadcaster":
                                IsBroadcaster = true;
                                break;
                            case "moderator":
                                IsModerator = true;
                                break;
                            case "vip":
                                IsVip = true;
                                break;
                            case "subscriber":
                                IsSubscriber = true;
                                break;
                                // Add more badge types as needed
                        }
                    }
                }
            }
        }
        private void UpdateYouTubeRoles(ChatMessageWrapper message)
        {
            if (message.PlatformMessage is Google.Apis.YouTube.v3.Data.LiveChatMessage youtubeMessage)
            {
                var authorDetails = youtubeMessage.AuthorDetails;
                if (authorDetails != null)
                {
                    // YouTube uses boolean properties, not a Role enum
                    IsModerator = authorDetails.IsChatModerator == true;
                    IsBroadcaster = authorDetails.IsChatOwner == true;
                    IsSubscriber = authorDetails.IsChatSponsor == true; // Sponsor ≈ Subscriber

                    // YouTube doesn't have a direct VIP equivalent
                    // You could track this manually or use custom logic

                    // Logger.Debug($"YouTube roles for {Username}: Moderator={IsModerator}, Broadcaster={IsBroadcaster}, Sponsor={IsSubscriber}");
                }
            }
        }

        public void AssignPawn(Pawn pawn)
        {
            // Store pawn reference - you might want to store the pawn's ThingID or other identifier
            this.AssignedPawnId = pawn.ThingID;

            // Save viewer data to persist the assignment
            Viewers.SaveViewers();
        }
        //public Pawn GetAssignedPawn()
        //{
        //    if (string.IsNullOrEmpty(AssignedPawnId))
        //        return null;

        //    return FindPawnByThingId(AssignedPawnId);
        //}

        private static Pawn FindPawnByThingId(string thingId)
        {
            if (string.IsNullOrEmpty(thingId))
                return null;

            // Search all maps for the pawn
            foreach (var map in Find.Maps)
            {
                foreach (var pawn in map.mapPawns.AllPawns)
                {
                    if (pawn.ThingID == thingId)
                        return pawn;
                }
            }

            // Also check world pawns
            var worldPawn = Find.WorldPawns.AllPawnsAlive.FirstOrDefault(p => p.ThingID == thingId);
            if (worldPawn != null)
                return worldPawn;

            return null;
        }
    }
}