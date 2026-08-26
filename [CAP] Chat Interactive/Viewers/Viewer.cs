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
//
// Per-viewer model: economy, roles, multi-platform IDs, activity.
using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace CAP_ChatInteractive
{
    public class Viewer
    {
        public string Username { get; set; }
        public string DisplayName { get; set; }

        /// <summary>Platform name (lowercase) → platform user id.</summary>
        public Dictionary<string, string> PlatformUserIds { get; set; }

        public bool IsModerator { get; set; }
        public bool IsSubscriber { get; set; }
        public bool IsVip { get; set; }
        public bool IsBroadcaster { get; set; }
        public bool IsBanned { get; set; }

        public DateTime LastSeen { get; set; }
        public DateTime FirstSeen { get; set; }
        public int MessageCount { get; set; }

        public int Coins { get; set; }
        public float Karma { get; set; }
        public string AssignedPawnId { get; set; }

        public string ColorCode { get; set; }

        public Viewer(string username)
        {
            Username = username?.ToLowerInvariant() ?? string.Empty;
            DisplayName = username ?? string.Empty;

            PlatformUserIds = new Dictionary<string, string>();
            FirstSeen = DateTime.Now;
            LastSeen = DateTime.Now;

            int startingCoins = 100;
            float startingKarma = 100f;

            try
            {
                var settings = CAPChatInteractiveMod.Instance?.Settings?.GlobalSettings;
                if (settings != null)
                {
                    startingCoins = settings.StartingCoins;
                    startingKarma = settings.StartingKarma;
                }
            }
            catch (Exception ex)
            {
                Logger.Warning($"[Viewer] Error reading global settings for '{Username}': {ex.Message}");
            }

            Coins = startingCoins;
            Karma = startingKarma;
        }

        public void AddPlatformUserId(string platform, string userId)
        {
            if (string.IsNullOrEmpty(platform) || string.IsNullOrEmpty(userId))
                return;

            if (PlatformUserIds == null)
                PlatformUserIds = new Dictionary<string, string>();

            PlatformUserIds[platform.ToLowerInvariant()] = userId;
        }

        public string GetPlatformUserId(string platform)
        {
            if (string.IsNullOrEmpty(platform) || PlatformUserIds == null)
                return null;

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
            if (string.IsNullOrEmpty(platform) || PlatformUserIds == null)
                return false;

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
            if (coins < 0)
                return false;

            if (Coins >= coins)
            {
                Coins -= coins;
                return true;
            }

            return false;
        }

        public float GetKarma() => Karma;

        public void SetKarma(float karma)
        {
            try
            {
                var settings = CAPChatInteractiveMod.Instance?.Settings?.GlobalSettings;
                if (settings == null)
                {
                    Karma = Mathf.Clamp(karma, 0f, 200f);
                    return;
                }

                Karma = Mathf.Clamp(karma, settings.MinKarma, settings.MaxKarma);
            }
            catch
            {
                Karma = Mathf.Clamp(karma, 0f, 200f);
            }
        }

        public void GiveKarma(float karma)
        {
            SetKarma(Karma + karma);
        }

        public void TakeKarma(float karma)
        {
            SetKarma(Karma - karma);
        }

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

        /// <summary>
        /// Hierarchical permission check: broadcaster &gt; moderator &gt; vip &gt; subscriber &gt; everyone.
        /// AI chat bot viewers always pass.
        /// </summary>
        public bool HasPermission(string permissionLevel)
        {
            if (string.IsNullOrEmpty(permissionLevel))
                return false;

            try
            {
                var settings = CAPChatInteractiveMod.Instance?.Settings?.GlobalSettings;
                string aiBotName = settings?.AIChatBotName ?? "Masie";

                if (PlatformUserIds != null &&
                    (PlatformUserIds.ContainsKey("aichatbot") ||
                     (PlatformUserIds.Count == 0 &&
                      !string.IsNullOrEmpty(Username) &&
                      Username.Equals(aiBotName, StringComparison.OrdinalIgnoreCase))))
                {
                    return true;
                }

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
            catch (Exception ex)
            {
                Logger.Error($"[Viewer] HasPermission failed for '{Username}': {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Apply chat message data: activity, platform id, name changes, roles.
        /// Does not always persist — <see cref="Viewers.UpdateViewerActivity"/> owns save cadence,
        /// except real username/display-name changes which save immediately.
        /// </summary>
        public void UpdateFromMessage(ChatMessageWrapper message)
        {
            if (message == null)
                return;

            try
            {
                UpdateActivity();

                if (!string.IsNullOrEmpty(message.PlatformUserId) && !string.IsNullOrEmpty(message.Platform))
                    AddPlatformUserId(message.Platform, message.PlatformUserId);

                // Platform user id is authoritative for renames
                if (!string.IsNullOrEmpty(message.PlatformUserId))
                {
                    string incomingLower = message.Username?.ToLowerInvariant() ?? string.Empty;
                    if (!string.IsNullOrEmpty(incomingLower) && Username != incomingLower)
                    {
                        string oldUsername = Username;
                        Username = incomingLower;
                        Logger.Message(
                            $"[Viewer] Name change (platform id): '{oldUsername}' → '{incomingLower}' " +
                            $"(DisplayName='{message.DisplayName}', Platform={message.Platform})");

                        if (!string.IsNullOrEmpty(message.DisplayName))
                            UpdateDisplayName(message.DisplayName);
                        else
                            Viewers.SaveViewers();
                    }
                    else if (!string.IsNullOrEmpty(message.DisplayName))
                    {
                        UpdateDisplayName(message.DisplayName);
                    }
                }
                else if (!string.IsNullOrEmpty(message.DisplayName))
                {
                    UpdateDisplayName(message.DisplayName);
                }

                UpdatePlatformRoles(message);
            }
            catch (Exception ex)
            {
                Logger.Error($"[Viewer] UpdateFromMessage failed for '{Username}': {ex.Message}");
            }
        }

        private void UpdatePlatformRoles(ChatMessageWrapper message)
        {
            if (message == null || string.IsNullOrEmpty(message.Platform))
                return;

            switch (message.Platform.ToLowerInvariant())
            {
                case "twitch":
                    UpdateTwitchRoles(message);
                    break;
                case "youtube":
                    UpdateYouTubeRoles(message);
                    break;
                case "kick":
                    UpdateKickRoles(message);
                    break;
            }
        }

        private void UpdateTwitchRoles(ChatMessageWrapper message)
        {
            try
            {
                if (message.PlatformMessage is TwitchLib.Client.Models.ChatMessage twitchMessage)
                {
                    IsModerator = twitchMessage.IsModerator;
                    IsSubscriber = twitchMessage.IsSubscriber;
                    IsVip = twitchMessage.IsVip;
                    IsBroadcaster = twitchMessage.IsBroadcaster;

                    if (twitchMessage.Badges != null)
                    {
                        foreach (var badge in twitchMessage.Badges)
                        {
                            if (badge.Key == null)
                                continue;

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
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Warning($"[Viewer] UpdateTwitchRoles failed for '{Username}': {ex.Message}");
            }
        }

        private void UpdateYouTubeRoles(ChatMessageWrapper message)
        {
            try
            {
                if (message.PlatformMessage is Google.Apis.YouTube.v3.Data.LiveChatMessage youtubeMessage)
                {
                    var authorDetails = youtubeMessage.AuthorDetails;
                    if (authorDetails == null)
                        return;

                    IsModerator = authorDetails.IsChatModerator == true;
                    IsBroadcaster = authorDetails.IsChatOwner == true;
                    IsSubscriber = authorDetails.IsChatSponsor == true;
                }
            }
            catch (Exception ex)
            {
                Logger.Warning($"[Viewer] UpdateYouTubeRoles failed for '{Username}': {ex.Message}");
            }
        }

        private void UpdateKickRoles(ChatMessageWrapper message)
        {
            try
            {
                var sender = message.PlatformMessage as Newtonsoft.Json.Linq.JObject;
                if (sender == null)
                    return;

                if (sender["isSubscriber"] != null && (bool)sender["isSubscriber"])
                    IsSubscriber = true;
                if (sender["isVip"] != null && (bool)sender["isVip"])
                    IsVip = true;
                if (sender["isModerator"] != null && (bool)sender["isModerator"])
                    IsModerator = true;
                if (sender["isBroadcaster"] != null && (bool)sender["isBroadcaster"])
                    IsBroadcaster = true;

                ApplyKickBadges(sender["badges"] as Newtonsoft.Json.Linq.JArray);

                var identity = sender["identity"] as Newtonsoft.Json.Linq.JObject;
                if (identity != null)
                    ApplyKickBadges(identity["badges"] as Newtonsoft.Json.Linq.JArray);
            }
            catch
            {
                // Best effort — leave existing flags
            }
        }

        private void ApplyKickBadges(Newtonsoft.Json.Linq.JArray badges)
        {
            if (badges == null)
                return;

            foreach (var b in badges)
            {
                string badgeText = (string)(b["text"] ?? b["type"] ?? b) ?? string.Empty;
                string lower = badgeText.ToLowerInvariant();

                if (lower.Contains("subscriber") || lower.Contains("sub") || lower.Contains("member"))
                    IsSubscriber = true;
                if (lower.Contains("vip"))
                    IsVip = true;
                if (lower.Contains("mod"))
                    IsModerator = true;
                if (lower.Contains("broadcaster") || lower.Contains("owner") || lower.Contains("host"))
                    IsBroadcaster = true;
            }
        }

        /// <summary>
        /// Prefer twitch → youtube → kick → aichatbot → username:…
        /// </summary>
        public string GetPrimaryPlatformIdentifier()
        {
            string safeUsername = Username?.ToLowerInvariant() ?? "unknown";

            if (PlatformUserIds != null)
            {
                if (PlatformUserIds.TryGetValue("twitch", out string twitchId) && !string.IsNullOrEmpty(twitchId))
                    return $"twitch:{twitchId}";
                if (PlatformUserIds.TryGetValue("youtube", out string youtubeId) && !string.IsNullOrEmpty(youtubeId))
                    return $"youtube:{youtubeId}";
                if (PlatformUserIds.TryGetValue("kick", out string kickId) && !string.IsNullOrEmpty(kickId))
                    return $"kick:{kickId}";
                if (PlatformUserIds.TryGetValue("aichatbot", out string aiId) && !string.IsNullOrEmpty(aiId))
                    return $"aichatbot:{aiId}";
            }

            return $"username:{safeUsername}";
        }

        public bool MatchesChatMessage(ChatMessageWrapper message)
        {
            if (message == null)
                return false;

            try
            {
                if (!string.IsNullOrEmpty(message.PlatformUserId) &&
                    !string.IsNullOrEmpty(message.Platform) &&
                    PlatformUserIds != null &&
                    PlatformUserIds.TryGetValue(message.Platform.ToLowerInvariant(), out string storedId))
                {
                    return storedId == message.PlatformUserId;
                }

                if (string.IsNullOrEmpty(Username) || string.IsNullOrEmpty(message.Username))
                    return false;

                return Username.Equals(message.Username, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Updates display name and assigned pawn nickname. Saves when the name actually changes.
        /// </summary>
        public bool UpdateDisplayName(string newDisplayName)
        {
            if (string.IsNullOrWhiteSpace(newDisplayName))
                return false;

            string normalizedNew = newDisplayName.Trim();
            string current = DisplayName?.Trim() ?? string.Empty;

            if (normalizedNew.Equals(current, StringComparison.Ordinal))
                return false;

            string oldName = DisplayName;
            DisplayName = normalizedNew;

            Logger.Message($"[Viewer] '{Username}' display name: '{oldName}' → '{normalizedNew}'");

            try
            {
                var assignmentMgr = Current.Game?.GetComponent<GameComponent_PawnAssignmentManager>();
                if (assignmentMgr != null)
                {
                    Pawn assignedPawn = assignmentMgr.GetAssignedPawnIdentifier(GetPrimaryPlatformIdentifier());
                    if (assignedPawn != null && !assignedPawn.Destroyed)
                        UpdatePawnNickname(assignedPawn, normalizedNew);
                }
            }
            catch (Exception ex)
            {
                Logger.Warning($"[Viewer] Pawn nickname update failed for '{Username}': {ex.Message}");
            }

            try
            {
                Viewers.SaveViewers();
            }
            catch (Exception ex)
            {
                Logger.Warning($"[Viewer] Save after name change failed for '{Username}': {ex.Message}");
            }

            return true;
        }

        private static void UpdatePawnNickname(Pawn pawn, string newNick)
        {
            if (pawn == null || string.IsNullOrEmpty(newNick))
                return;

            if (pawn.Name is NameTriple triple)
                pawn.Name = new NameTriple(triple.First, newNick, triple.Last);
            else if (pawn.Name is NameSingle)
                pawn.Name = new NameSingle(newNick);
            else
                pawn.Name = new NameTriple(string.Empty, newNick, string.Empty);
        }
    }
}
