// Viewer.cs  
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

/*
 * CONCEPTUAL INSPIRATION:
 * Viewer data model concept inspired by hodlhodl1132's TwitchToolkit (AGPLv3)
 * This implementation includes significant architectural differences:
 * - Platform ID system for cross-platform user identification
 * - Enhanced role tracking with multi-platform support
 * - Different activity tracking mechanisms
 * - Expanded permission system
 * 
 * Original TwitchToolkit Copyright: 2019 hodlhodl1132
 * Community Preservation Modifications © 2025 Captolamia
 */

using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
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
        public float Karma { get; set; }
        public string AssignedPawnId { get; set; }

        // Platform-specific data
        public string ColorCode { get; set; }

        /// <summary>
        /// Initializes a new instance of the Viewer class with the specified username and default values for coins and
        /// karma.
        /// </summary>
        /// <remarks>If global settings are available, the initial coin and karma values are taken from
        /// those settings; otherwise, default values are used. The username is stored in lowercase for internal use and
        /// as provided for display purposes.</remarks>
        /// <param name="username">The username to associate with the viewer. If null, an empty string is used.</param>
        public Viewer(string username)
        {
            Username = username?.ToLowerInvariant() ?? "";
            DisplayName = username ?? "";

            PlatformUserIds = new Dictionary<string, string>();
            FirstSeen = DateTime.Now;
            LastSeen = DateTime.Now;

            // Defensive settings access — fallback to sensible defaults if mod not fully initialized
            int startingCoins = 100;
            float startingKarma = 100;

            try
            {
                var settings = CAPChatInteractiveMod.Instance?.Settings?.GlobalSettings;
                if (settings != null)
                {
                    startingCoins = settings.StartingCoins;
                    startingKarma = settings.StartingKarma;
                }
                else
                {
                    Logger.Debug($"[RICS Viewer] CAPChatInteractiveMod.Instance not ready yet for new viewer '{Username}' — using defaults");
                }
            }
            catch (Exception ex)
            {
                Logger.Warning($"[RICS Viewer] Error reading global settings in constructor for '{Username}': {ex.Message}");
            }

            Coins = startingCoins;
            Karma = startingKarma;
        }

        /// <summary>
        /// Associates a user identifier with the specified platform for the current instance.
        /// </summary>
        /// <remarks>If either parameter is null or empty, the method does not add an entry. Platform
        /// names are stored in lowercase to ensure consistency.</remarks>
        /// <param name="platform">The name of the platform to associate with the user identifier. Cannot be null or empty. The value is
        /// case-insensitive.</param>
        /// <param name="userId">The user identifier to associate with the specified platform. Cannot be null or empty.</param>
        public void AddPlatformUserId(string platform, string userId)
        {
            if (!string.IsNullOrEmpty(platform) && !string.IsNullOrEmpty(userId))
            {
                // Always use lowercase for consistency
                string platformKey = platform.ToLowerInvariant();
                PlatformUserIds[platformKey] = userId;
            }
            else
            {
                // Logger.Warning($"Cannot add platform ID - platform: '{platform}', userId: '{userId}'");
            }
        }

        /// <summary>
        /// Retrieves the user identifier associated with the specified platform.
        /// </summary>
        /// <param name="platform">The name of the platform for which to retrieve the user identifier. The comparison is case-insensitive.
        /// Cannot be null.</param>
        /// <returns>The user identifier for the specified platform, or null if no identifier is found.</returns>
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

        /// <summary>
        /// Determines whether a user ID is associated with the specified platform.
        /// </summary>
        /// <param name="platform">The name of the platform to check for an associated user ID. Cannot be null or empty. The comparison is
        /// case-insensitive.</param>
        /// <returns>true if a user ID exists for the specified platform; otherwise, false.</returns>
        public bool HasPlatform(string platform)
        {
            return PlatformUserIds.ContainsKey(platform.ToLowerInvariant());
        }

        /// <summary>
        /// Determines whether the user has any special role, such as broadcaster, moderator, VIP, or subscriber.
        /// </summary>
        /// <returns>true if the user is a broadcaster, moderator, VIP, or subscriber; otherwise, false.</returns>
        public bool HasAnySpecialRole()
        {
            return IsBroadcaster || IsModerator || IsVip || IsSubscriber;
        }

        /// <summary>
        /// Returns a comma-separated string describing the user's platform roles based on their current status.
        /// </summary>
        /// <remarks>The order of roles in the returned string reflects their relative significance, with
        /// "Broadcaster" listed first if present. This method is useful for displaying user status in a human-readable
        /// format.</remarks>
        /// <returns>A string containing the names of all applicable roles, such as "Broadcaster", "Moderator", "VIP", and
        /// "Subscriber". Returns "Regular Viewer" if the user has none of these roles.</returns>
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
        /// <summary>
        /// Gets the current number of coins available to the user.
        /// </summary>
        /// <returns>The number of coins currently held. The value is always greater than or equal to zero.</returns>
        public int GetCoins() => Coins;

        /// <summary>
        /// Sets the number of coins, ensuring the value is not negative.
        /// </summary>
        /// <param name="coins">The new coin value to assign. If the value is less than 0, the number of coins is set to 0.</param>
        public void SetCoins(int coins)
        {
            Coins = Math.Max(0, coins);
        }

        /// <summary>
        /// Adds the specified number of coins to the current total, ensuring the total does not fall below zero.
        /// </summary>
        /// <param name="coins">The number of coins to add. Can be negative to subtract coins. The resulting total will not be less than
        /// zero.</param>
        public void GiveCoins(int coins)
        {
            Coins = Math.Max(0, Coins + coins);
        }

        /// <summary>
        /// Attempts to remove the specified number of coins from the current balance.
        /// </summary>
        /// <remarks>No coins are removed if the requested amount exceeds the current balance.</remarks>
        /// <param name="coins">The number of coins to remove. Must be less than or equal to the current coin balance and greater than or
        /// equal to zero.</param>
        /// <returns>true if the specified number of coins was successfully removed; otherwise, false.</returns>
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
        /// <summary>
        /// Gets the current karma value for the instance.
        /// </summary>
        /// <returns>The current karma as a floating-point value.</returns>
        public float GetKarma() => Karma;

        /// <summary>
        /// Sets the viewer's karma, clamping it within the configured min/max bounds.
        /// </summary>
        /// <param name="karma">The new karma value to set.</param>
        public void SetKarma(float karma)
        {
            var settings = CAPChatInteractiveMod.Instance?.Settings?.GlobalSettings;
            if (settings == null)
            {
                Karma = Mathf.Clamp(karma, 0f, 200f); // safe fallback
                return;
            }
            Karma = Mathf.Clamp(karma, settings.MinKarma, settings.MaxKarma);
        }

        /// <summary>
        /// Adds the specified amount of karma to the viewer's current karma balance.
        /// </summary>
        /// <param name="karma">The amount of karma to add. Can be positive or negative to increase or decrease the balance.</param>
        public void GiveKarma(float karma)
        {
            Logger.Debug($"Giving {karma:F2} karma to viewer '{Username}'");
            SetKarma(Karma + karma);
        }

        /// <summary>
        /// Decreases the viewer's karma by the specified amount.
        /// </summary>
        /// <param name="karma">The amount of karma to subtract from the viewer. Must be a non-negative value.</param>
        public void TakeKarma(float karma)
        {
            Logger.Debug($"Taking {karma:F2} karma from viewer '{Username}'");
            SetKarma(Karma - karma);
        }

        /// <summary>
        /// Updates the activity state by recording the current time and incrementing the message count.
        /// </summary>
        /// <remarks>Call this method to indicate that activity has occurred, such as when a new message
        /// is processed. This method updates both the last seen timestamp and the total number of messages
        /// handled.</remarks>
        public void UpdateActivity()
        {
            LastSeen = DateTime.Now;
            MessageCount++;
        }

        /// <summary>
        /// Calculates the time elapsed since the last recorded activity.
        /// </summary>
        /// <remarks>The result is based on the current system time and the value of the LastSeen
        /// property. If LastSeen is set to a future time, the returned duration may be negative.</remarks>
        /// <returns>A <see cref="TimeSpan"/> representing the duration since the last activity. The value will be zero or
        /// positive.</returns>
        public TimeSpan GetTimeSinceLastActivity()
        {
            return DateTime.Now - LastSeen;
        }

        /// <summary>
        /// Determines whether the entity is considered active based on the time since the last activity.
        /// </summary>
        /// <param name="maxMinutesInactive">The maximum number of minutes allowed since the last activity for the entity to be considered active. Must
        /// be non-negative. The default is 30 minutes.</param>
        /// <returns>true if the time since the last activity is less than or equal to maxMinutesInactive; otherwise, false.</returns>
        public bool IsActive(int maxMinutesInactive = 30)
        {
            return GetTimeSinceLastActivity().TotalMinutes <= maxMinutesInactive;
        }

        /// <summary>
        /// Determines whether the viewer has the specified permission level based on their current roles.
        /// </summary>
        /// <remarks>Permission levels are hierarchical: higher roles include the permissions of lower
        /// roles. For example, a broadcaster has all permissions, and a moderator has moderator, VIP, subscriber, and
        /// everyone permissions. If an unrecognized permission level is provided, the method returns false.</remarks>
        /// <param name="permissionLevel">The required permission level to check. Valid values are "broadcaster", "moderator", "vip", "subscriber",
        /// and "everyone". The comparison is case-insensitive.</param>
        /// <returns>true if the viewer meets or exceeds the specified permission level; otherwise, false.</returns>
        public bool HasPermission(string permissionLevel)
        {
            Logger.Debug($"Checking permission for viewer '{Username}': Current roles - Broadcaster:{IsBroadcaster}, Moderator:{IsModerator}, VIP:{IsVip}, Subscriber:{IsSubscriber}");
            Logger.Debug($"Required permission level: '{permissionLevel}'");

            bool result = permissionLevel.ToLowerInvariant() switch
            {
                "broadcaster" => IsBroadcaster,
                "moderator" => IsModerator || IsBroadcaster,
                "vip" => IsVip || IsModerator || IsBroadcaster,
                "subscriber" => IsSubscriber || IsVip || IsModerator || IsBroadcaster,
                "everyone" => true,
                _ => false
            };

            Logger.Debug($"Permission result: {result}");
            return result;
        }

        /// <summary>
        /// Updates the viewer's information based on the data provided in the specified chat message.
        /// </summary>
        /// <remarks>This method ensures that the viewer's platform user ID, username, display name, and
        /// platform-specific roles remain consistent with the authoritative data from the chat message. If the platform
        /// user ID indicates a username change, the persistent username is updated to maintain correct identity
        /// mapping. Display name and platform roles are also refreshed as needed.</remarks>
        /// <param name="message">A chat message containing the latest platform user ID, username, display name, and platform-specific roles
        /// to update the viewer's state.</param>
        public void UpdateFromMessage(ChatMessageWrapper message)
        {
            UpdateActivity();

            // Always add/refresh platform ID when present — ID is the source of truth
            bool hadPlatformId = HasPlatform(message.Platform);
            if (!string.IsNullOrEmpty(message.PlatformUserId))
            {
                AddPlatformUserId(message.Platform, message.PlatformUserId);
            }

            // ─────────────────────────────────────────────────────────────
            //  Name change detection — PLATFORM ID IS AUTHORITATIVE
            // ─────────────────────────────────────────────────────────────
            // If we have a stable platformUserId, any difference in Username means
            // the viewer renamed on the streaming platform. We must update the
            // persistent Username key (not just DisplayName) so that:
            // - GetViewer(username) continues to work with the new name
            // - No orphan "newusername + empty PlatformUserIds" record is created
            // - RemoveDuplicateViewers / primary key logic stays consistent
            // This directly fixes the reported Foxxogirlrei / Reitheprettybunny case.
            if (!string.IsNullOrEmpty(message.PlatformUserId))
            {
                string incomingLower = message.Username?.ToLowerInvariant() ?? "";
                if (!string.IsNullOrEmpty(incomingLower) && Username != incomingLower)
                {
                    string oldUsername = Username;
                    Username = incomingLower;

                    Logger.Message($"[RICS Viewer] Platform ID confirmed name change (ID is truth): '{oldUsername}' → '{incomingLower}' (DisplayName='{message.DisplayName}', Platform={message.Platform})");

                    // Update display name + pawn nickname (this also calls SaveViewers)
                    if (!string.IsNullOrEmpty(message.DisplayName))
                    {
                        UpdateDisplayName(message.DisplayName);
                    }
                    else
                    {
                        // Still persist the Username change even if no display name was sent
                        Viewers.SaveViewers();
                    }
                }
                else if (!string.IsNullOrEmpty(message.DisplayName))
                {
                    UpdateDisplayName(message.DisplayName);
                }
            }
            else if (!string.IsNullOrEmpty(message.DisplayName))
            {
                // Fallback path (no platform ID in this message) — only touch display name
                UpdateDisplayName(message.DisplayName);
            }

            // Update platform-specific roles from the message (badges, sponsor, etc.)
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
                }
            }
        }

        /// <summary>
        /// Gets the primary platform identifier for the user, prioritizing Twitch, YouTube, and Kick platform IDs if
        /// available.
        /// </summary>
        /// <remarks>The method checks for a valid Twitch, YouTube, or Kick platform user ID in that order
        /// of precedence. If none are found, it falls back to using the username. This ensures a consistent identifier
        /// is always returned, even if some user data is missing or incomplete.</remarks>
        /// <returns>A string representing the user's primary platform identifier in the format 'platform:id' (e.g.,
        /// 'twitch:12345'). If no platform ID is available, returns 'username:{username}' with the username in
        /// lowercase or 'unknown' if the username is not set.</returns>
        public string GetPrimaryPlatformIdentifier()
        {
            // WHY: Original could throw NullReferenceException if Username is null
            // (possible via deserialization edge case, manual construction, or corrupt viewers.json).
            // This guard + empty-ID check makes RemoveDuplicateViewers and all other ID lookups safe.
            // Also future-proofs against empty platform ID strings being stored.
            string safeUsername = Username?.ToLowerInvariant() ?? "unknown";

            if (PlatformUserIds.TryGetValue("twitch", out string twitchId) && !string.IsNullOrEmpty(twitchId))
                return $"twitch:{twitchId}";
            if (PlatformUserIds.TryGetValue("youtube", out string youtubeId) && !string.IsNullOrEmpty(youtubeId))
                return $"youtube:{youtubeId}";
            if (PlatformUserIds.TryGetValue("kick", out string kickId) && !string.IsNullOrEmpty(kickId))
                return $"kick:{kickId}";

            return $"username:{safeUsername}";
        }

        // NEW: Check if this viewer matches a chat message (for platform ID verification)

        /// <summary>
        /// Determines whether the viewer matches the specified chat message based on platform user ID or username.
        /// </summary>
        /// <param name="message">The chat message to compare against.</param>
        /// <returns>true if the viewer matches the chat message; otherwise, false.</returns>
        public bool MatchesChatMessage(ChatMessageWrapper message)
        {
            if (!string.IsNullOrEmpty(message.PlatformUserId) &&
                PlatformUserIds.TryGetValue(message.Platform.ToLowerInvariant(), out string storedId))
            {
                return storedId == message.PlatformUserId;
            }
            return Username.Equals(message.Username, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Updates the viewer's display name and propagates the change to any assigned pawn's nickname.
        /// </summary>
        /// <param name="newDisplayName"></param>
        /// <returns></returns>
        public bool UpdateDisplayName(string newDisplayName)
        {
            if (string.IsNullOrWhiteSpace(newDisplayName))
                return false;

            string normalizedNew = newDisplayName.Trim();
            string current = DisplayName?.Trim() ?? "";

            // Same name (including case) → nothing to do
            if (normalizedNew.Equals(current, StringComparison.Ordinal))
                return false;

            string oldName = DisplayName;
            DisplayName = normalizedNew;

            Logger.Message($"Viewer '{Username}' changed display name: '{oldName}' → '{normalizedNew}'");

            // ───────────────────────────────────────────────────────
            // 1. Update pawn nickname if this viewer has an assigned pawn
            // ───────────────────────────────────────────────────────
            try
            {
                var assignmentMgr = Current.Game?.GetComponent<GameComponent_PawnAssignmentManager>();
                if (assignmentMgr != null)
                {
                    string primaryId = GetPrimaryPlatformIdentifier();

                    Pawn assignedPawn = assignmentMgr.GetAssignedPawnIdentifier(primaryId);
                    if (assignedPawn != null && !assignedPawn.Destroyed)
                    {
                        UpdatePawnNickname(assignedPawn, normalizedNew);
                        Logger.Message($"Updated pawn nickname for {Username} → {normalizedNew}");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Warning($"[RICS Viewer] Failed to update pawn nickname for '{Username}': {ex.Message}");
                // Non-fatal — name change on viewer still succeeded
            }

            // ───────────────────────────────────────────────────────
            // 2. Save viewers (safe even if pawn update failed)
            // ───────────────────────────────────────────────────────
            try
            {
                Viewers.SaveViewers();
            }
            catch (Exception ex)
            {
                Logger.Warning($"[RICS Viewer] Failed to save after name change for '{Username}': {ex.Message}");
            }

            return true;
        }

        private void UpdatePawnNickname(Pawn pawn, string newNick)
        {
            if (pawn.Name is NameTriple triple)
            {
                // Keep first/last, change only nick
                pawn.Name = new NameTriple(triple.First, newNick, triple.Last);
            }
            else if (pawn.Name is NameSingle single)
            {
                pawn.Name = new NameSingle(newNick);
            }
            else
            {
                // Fallback - create triple with new nick in middle
                pawn.Name = new NameTriple("", newNick, "");
            }
        }
    }
}