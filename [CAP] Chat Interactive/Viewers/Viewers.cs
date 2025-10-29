// Viewers.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using Verse;

namespace CAP_ChatInteractive
{
    public static class Viewers
    {
        public static List<Viewer> All = new List<Viewer>();
        private static readonly object _lock = new object();
        private static string _dataFilePath;

        static Viewers()
        {
            _dataFilePath = JsonFileManager.GetFilePath("viewers.json");
            LoadViewers();
        }

        public static Viewer GetViewer(string username)
        {
            if (string.IsNullOrEmpty(username))
            {
                Logger.Warning("GetViewer: Username is null or empty");
                return null;
            }

            var usernameLower = username.ToLowerInvariant();

            lock (_lock)
            {
                var viewer = All.Find(v => v.Username == usernameLower);

                if (viewer == null)
                {
                    viewer = new Viewer(username);
                    All.Add(viewer);

                    // Save immediately for new viewers during debugging
                    SaveViewers();
                }
                // DebugSaveAndLog();
                return viewer;
            }
        }

        public static Viewer GetViewerByPlatformId(string platform, string userId)
        {
            if (string.IsNullOrEmpty(platform) || string.IsNullOrEmpty(userId))
                return null;

            lock (_lock)
            {
                return All.Find(v => v.GetPlatformUserId(platform) == userId);
            }
        }

        public static void UpdateViewerActivity(ChatMessageWrapper message)
        {
            try
            {
                var viewer = GetViewer(message.Username);
                if (viewer != null)
                {
                    // Check if this will add a platform ID
                    bool hadPlatformIdBefore = viewer.HasPlatform(message.Platform);

                    viewer.UpdateFromMessage(message);

                    // Check if a new platform ID was added
                    bool hasPlatformIdAfter = viewer.HasPlatform(message.Platform);

                    // If a new platform ID was added, save immediately
                    if (!hadPlatformIdBefore && hasPlatformIdAfter)
                    {
                        SaveViewers();
                    }
                    // Otherwise use periodic saving
                    else if (viewer.MessageCount % 10 == 0)
                    {
                        SaveViewers();
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error updating viewer activity: {ex.Message}");
            }
        }

        // Viewers.cs - The existing method should work, but let's add some debugging
        public static void AwardActiveViewersCoins()
        {
            try
            {
                var settings = CAPChatInteractiveMod.Instance.Settings.GlobalSettings;
                var activeViewers = GetActiveViewers(settings.MinutesForActive);

                lock (_lock)
                {
                    foreach (var viewer in activeViewers)
                    {
                        if (viewer.IsBanned) continue;

                        int baseCoins = settings.BaseCoinReward;
                        float karmaMultiplier = (float)viewer.Karma / 100f;

                        // Apply role multipliers - THIS IS WORKING CORRECTLY
                        if (viewer.IsSubscriber)
                            baseCoins += settings.SubscriberExtraCoins;
                        if (viewer.IsVip)
                            baseCoins += settings.VipExtraCoins;
                        if (viewer.IsModerator)
                            baseCoins += settings.ModExtraCoins;

                        int coinsToAward = (int)(baseCoins * karmaMultiplier);
                        viewer.GiveCoins(coinsToAward);

                        // Add debug logging if needed:
                        Logger.Debug($"Awarded {coinsToAward} coins to {viewer.Username} " +
                                      $"(base: {settings.BaseCoinReward}, karma: {viewer.Karma}, " +
                                      $"sub: {viewer.IsSubscriber}, vip: {viewer.IsVip}, mod: {viewer.IsModerator})");
                    }
                }

                SaveViewers();
            }
            catch (Exception ex)
            {
                Logger.Error($"Error awarding coins to active viewers: {ex.Message}");
            }
        }

        public static List<Viewer> GetActiveViewers(int maxMinutesInactive = 30)
        {
            lock (_lock)
            {
                return All.Where(v => v.IsActive(maxMinutesInactive)).ToList();
            }
        }

        public static void GiveAllViewersCoins(int amount, List<Viewer> specificViewers = null)
        {
            lock (_lock)
            {
                var viewers = specificViewers ?? All;
                foreach (var viewer in viewers)
                {
                    viewer?.GiveCoins(amount);
                }
                SaveViewers();
            }
        }

        public static void SetAllViewersCoins(int amount, List<Viewer> specificViewers = null)
        {
            lock (_lock)
            {
                var viewers = specificViewers ?? All;
                foreach (var viewer in viewers)
                {
                    viewer?.SetCoins(amount);
                }
                SaveViewers();
            }
        }

        public static void SaveViewers()
        {
            try
            {
                lock (_lock)
                {
                    var data = new ViewerData(All);

                    // Use Newtonsoft.Json instead of JsonUtility
                    string json = Newtonsoft.Json.JsonConvert.SerializeObject(data, Newtonsoft.Json.Formatting.Indented);

                    bool success = JsonFileManager.SaveFile("viewers.json", json);
                    if (success)
                    {
                        Logger.Debug($"Successfully saved {All.Count} viewers to file");
                    }
                    else
                    {
                        Logger.Error("Failed to save viewers file");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error saving viewers: {ex.Message}. Stack: {ex.StackTrace}");
            }
        }

        private static void LoadViewers()
        {
            try
            {
                string json = JsonFileManager.LoadFile("viewers.json");
                if (!string.IsNullOrEmpty(json))
                {
                    var data = Newtonsoft.Json.JsonConvert.DeserializeObject<ViewerData>(json);

                    if (data?.viewers != null)
                    {
                        lock (_lock)
                        {
                            All = data.ToFullViewers();
                            RemoveDuplicateViewers();
                        }
                        Logger.Message($"Loaded {All.Count} viewers from save file");
                    }
                    else
                    {
                        Logger.Warning("Loaded viewers data but viewers list was null");
                        All = new List<Viewer>();
                    }
                }
                else
                {
                    Logger.Message("No existing viewers file found, starting fresh");
                    All = new List<Viewer>();
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error loading viewers: {ex.Message}. Stack: {ex.StackTrace}");
                All = new List<Viewer>();
            }
        }

        private static void RemoveDuplicateViewers()
        {
            try
            {
                lock (_lock)
                {
                    var uniqueViewers = new Dictionary<string, Viewer>();
                    var duplicatesRemoved = 0;
                    var coinsMerged = 0;

                    foreach (var viewer in All)
                    {
                        if (viewer == null) continue;

                        if (uniqueViewers.TryGetValue(viewer.Username, out var existingViewer))
                        {
                            // Merge coins from duplicate
                            coinsMerged += viewer.Coins;
                            existingViewer.GiveCoins(viewer.Coins);

                            // Merge platform IDs
                            foreach (var platformId in viewer.PlatformUserIds)
                            {
                                existingViewer.AddPlatformUserId(platformId.Key, platformId.Value);
                            }

                            duplicatesRemoved++;
                        }
                        else
                        {
                            uniqueViewers[viewer.Username] = viewer;
                        }
                    }

                    All = uniqueViewers.Values.ToList();

                    if (duplicatesRemoved > 0)
                    {
                        Logger.Message($"Removed {duplicatesRemoved} duplicate viewers, merged {coinsMerged} coins");
                        SaveViewers();
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error removing duplicate viewers: {ex.Message}");
            }
        }

        public static void ResetAllCoins()
        {
            var settings = CAPChatInteractiveMod.Instance.Settings.GlobalSettings;
            SetAllViewersCoins(settings.StartingCoins);
        }

        public static void ResetAllKarma()
        {
            var settings = CAPChatInteractiveMod.Instance.Settings.GlobalSettings;
            lock (_lock)
            {
                foreach (var viewer in All)
                {
                    viewer.SetKarma(settings.StartingKarma);
                }
                SaveViewers();
            }
        }
        public static void DebugSaveAndLog()
        {
            lock (_lock)
            {
                // Logger.Debug($"Current viewers in memory: {All.Count}");
                foreach (var viewer in All.Take(5)) // Show first 5
                {
                    Logger.Debug($"Viewer: {viewer.Username}, Coins: {viewer.Coins}, Karma: {viewer.Karma}");
                }
                SaveViewers();
            }
        }
        public static void DebugSerialization()
        {
            lock (_lock)
            {
                var data = new ViewerData(All);

                // Test JsonUtility
                string jsonUtilityJson = JsonUtility.ToJson(data, true);
                Logger.Debug($"JsonUtility result: {jsonUtilityJson}");

                // Test Newtonsoft.Json
                string newtonsoftJson = Newtonsoft.Json.JsonConvert.SerializeObject(data, Newtonsoft.Json.Formatting.Indented);
                Logger.Debug($"Newtonsoft result: {newtonsoftJson}");

                Logger.Debug($"First viewer details: Username={All[0].Username}, Coins={All[0].Coins}, Karma={All[0].Karma}");
            }
        }

        public static void DebugPlatformIds()
        {
            lock (_lock)
            {
                Logger.Debug($"=== Platform IDs Debug ===");
                foreach (var viewer in All.Take(5))
                {
                    Logger.Debug($"Viewer: {viewer.Username}");
                    foreach (var platformId in viewer.PlatformUserIds)
                    {
                        Logger.Debug($"  {platformId.Key}: {platformId.Value}");
                    }
                    if (viewer.PlatformUserIds.Count == 0)
                    {
                        Logger.Debug($"  No platform IDs found!");
                    }
                }
            }
        }
    }

    [Serializable]
    public class ViewerData
    {
        public int total;
        public List<SimpleViewer> viewers;

        public ViewerData()
        {
            viewers = new List<SimpleViewer>();
        }

        public ViewerData(List<Viewer> viewersList)
        {
            viewers = new List<SimpleViewer>();

            if (viewersList != null)
            {
                foreach (var viewer in viewersList)
                {
                    viewers.Add(new SimpleViewer(viewer));
                }
            }

            total = viewers.Count;
        }

        public List<Viewer> ToFullViewers()
        {
            var fullViewers = new List<Viewer>();

            foreach (var simpleViewer in viewers)
            {
                var viewer = new Viewer(simpleViewer.username);
                simpleViewer.UpdateViewer(viewer);
                fullViewers.Add(viewer);
            }

            return fullViewers;
        }
    }

    [Serializable]
    public class SimpleViewer
    {
        // Remove the sequential 'id' field and use platform IDs instead
        public string username;
        public int karma;
        public int coins;
        public bool isBanned;
        public Dictionary<string, string> platformIds; // Platform -> UserId

        public SimpleViewer()
        {
            platformIds = new Dictionary<string, string>();
        }

        public SimpleViewer(Viewer viewer)
        {
            this.username = viewer.Username;
            this.karma = viewer.Karma;
            this.coins = viewer.Coins;
            this.isBanned = viewer.IsBanned;
            this.platformIds = new Dictionary<string, string>(viewer.PlatformUserIds);

            // DEBUG: Log what's being copied
            Logger.Debug($"SimpleViewer created for {username} with {platformIds.Count} platform IDs:");
            foreach (var platformId in platformIds)
            {
                Logger.Debug($"  {platformId.Key}: {platformId.Value}");
            }
        }

        public void UpdateViewer(Viewer viewer)
        {
            viewer.SetKarma(this.karma);
            viewer.SetCoins(this.coins);

            // Update platform IDs
            foreach (var platformId in platformIds)
            {
                viewer.AddPlatformUserId(platformId.Key, platformId.Value);
            }
        }

        // Get a unique ID for this viewer across platforms
        public string GetPrimaryPlatformId()
        {
            // Prefer Twitch if available, then YouTube, then first available
            if (platformIds.TryGetValue("twitch", out string twitchId)) return $"twitch:{twitchId}";
            if (platformIds.TryGetValue("youtube", out string youtubeId)) return $"youtube:{youtubeId}";
            return platformIds.Values.FirstOrDefault() ?? username;
        }
    }
}