// Viewers.cs
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
// Static registry: load/save viewers.json, lookup, awards, dedup, karma decay.
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace CAP_ChatInteractive
{
    public static class Viewers
    {
        public static List<Viewer> All = new List<Viewer>();
        public static readonly object _lock = new object();

        private static volatile bool _isSaving;

        static Viewers()
        {
            try
            {
                LoadViewers();
            }
            catch (Exception ex)
            {
                Logger.Error($"[Viewers] Static init failed: {ex.Message}");
                All = new List<Viewer>();
            }
        }

        /// <summary>
        /// Resolve by (platform, platform user id). Same display name on Kick vs Twitch is not the same wallet.
        /// Username fallback is only used when the message has no platform id (legacy).
        /// </summary>
        public static Viewer GetViewer(ChatMessageWrapper message)
        {
            if (message == null || string.IsNullOrEmpty(message.Username))
                return null;

            try
            {
                string platform = message.Platform?.ToLowerInvariant();
                string userId = message.PlatformUserId;

                if (!string.IsNullOrEmpty(platform) && !string.IsNullOrEmpty(userId))
                {
                    lock (_lock)
                    {
                        var byPlatform = All.Find(v =>
                            v != null && v.GetPlatformUserId(platform) == userId);

                        if (byPlatform != null)
                        {
                            if (HasForeignPlatform(byPlatform, platform))
                            {
                                byPlatform.RemovePlatformUserId(platform);
                                var split = CreateViewerForPlatform_Locked(
                                    message.Username, platform, userId);
                                Logger.Warning(
                                    $"[Viewers] Split '{message.Username}' {platform}:{userId} " +
                                    "off a multi-platform record (same handle is not the same account).");
                                SaveViewers();
                                return split;
                            }

                            return byPlatform;
                        }

                        return CreateViewerForPlatform_Locked(message.Username, platform, userId);
                    }
                }

                return GetViewer(message.Username);
            }
            catch (Exception ex)
            {
                Logger.Error($"[Viewers] GetViewer(message) failed: {ex.Message}");
                return null;
            }
        }

        private static bool HasForeignPlatform(Viewer viewer, string platform)
        {
            if (viewer?.PlatformUserIds == null || viewer.PlatformUserIds.Count == 0)
                return false;

            foreach (var key in viewer.PlatformUserIds.Keys)
            {
                if (!key.Equals(platform, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        /// <summary>Caller must hold <see cref="_lock"/>.</summary>
        private static Viewer CreateViewerForPlatform_Locked(string username, string platform, string userId)
        {
            var viewer = new Viewer(username);
            viewer.AddPlatformUserId(platform, userId);
            All.Add(viewer);
            SaveViewers();
            return viewer;
        }

        /// <summary>Get or create viewer by username (stored lowercase).</summary>
        public static Viewer GetViewer(string username)
        {
            if (string.IsNullOrEmpty(username))
                return null;

            var usernameLower = username.ToLowerInvariant();

            lock (_lock)
            {
                var viewer = All.Find(v => v != null && v.Username == usernameLower);
                if (viewer != null)
                    return viewer;

                viewer = new Viewer(username);
                All.Add(viewer);
                SaveViewers();
                return viewer;
            }
        }

        public static Viewer GetViewerByPlatformIdentifier(string identifier)
        {
            if (string.IsNullOrEmpty(identifier))
                return null;

            try
            {
                if (identifier.Contains(':'))
                {
                    var parts = identifier.Split(new[] { ':' }, 2);
                    if (parts.Length == 2)
                        return GetViewerByPlatformId(parts[0], parts[1]);
                }

                if (identifier.All(char.IsDigit))
                    return GetViewerByPlatformId("twitch", identifier);

                return GetViewer(identifier);
            }
            catch (Exception ex)
            {
                Logger.Error($"[Viewers] GetViewerByPlatformIdentifier failed: {ex.Message}");
                return null;
            }
        }

        public static Viewer GetViewerNoAdd(string username)
        {
            if (string.IsNullOrEmpty(username))
                return null;

            var usernameLower = username.ToLowerInvariant();

            lock (_lock)
            {
                return All.Find(v => v != null && v.Username == usernameLower);
            }
        }

        public static Viewer GetViewerByPlatformId(string platform, string userId)
        {
            if (string.IsNullOrEmpty(platform) || string.IsNullOrEmpty(userId))
                return null;

            lock (_lock)
            {
                return All.Find(v => v != null && v.GetPlatformUserId(platform) == userId);
            }
        }

        /// <summary>
        /// On each chat message: get/create viewer, update activity/roles, save periodically.
        /// </summary>
        public static void UpdateViewerActivity(ChatMessageWrapper message)
        {
            if (message == null || string.IsNullOrEmpty(message.Username))
                return;

            if (Current.Game == null || Find.TickManager == null)
                return;

            try
            {
                if (Find.TickManager.TicksGame % 300 == 0 && All.Count > 0)
                    RemoveDuplicateViewers(saveIfChanged: true);

                var viewer = GetViewer(message);
                if (viewer == null)
                    return;

                bool hadPlatformIdBefore = !string.IsNullOrEmpty(message.Platform) &&
                                           viewer.HasPlatform(message.Platform);

                viewer.UpdateFromMessage(message);

                bool hasPlatformIdAfter = !string.IsNullOrEmpty(message.Platform) &&
                                          viewer.HasPlatform(message.Platform);

                // First platform id or every 10 messages — avoid save-on-every-message
                if ((!hadPlatformIdBefore && hasPlatformIdAfter) || viewer.MessageCount % 10 == 0)
                    SaveViewers();
            }
            catch (Exception ex)
            {
                Logger.Error($"[Viewers] UpdateViewerActivity for '{message.Username}': {ex.Message}");
            }
        }

        /// <summary>
        /// Award coins to active non-banned viewers.
        /// Null amount uses BaseCoinReward + role extras × karma/100.
        /// </summary>
        public static int AwardActiveViewersCoins(int? fixedAmountPerViewer = null)
        {
            try
            {
                var settings = CAPChatInteractiveMod.Instance?.Settings?.GlobalSettings;
                if (settings == null)
                    return 0;

                var activeViewers = GetActiveViewers(settings.MinutesForActive);
                int awardedCount = 0;

                lock (_lock)
                {
                    foreach (var viewer in activeViewers)
                    {
                        if (viewer == null || viewer.IsBanned)
                            continue;

                        int coinsToAward;
                        if (fixedAmountPerViewer.HasValue)
                        {
                            coinsToAward = Math.Max(0, fixedAmountPerViewer.Value);
                        }
                        else
                        {
                            int baseCoins = settings.BaseCoinReward;
                            float karmaMultiplier = viewer.Karma / 100f;

                            if (viewer.IsSubscriber)
                                baseCoins += settings.SubscriberExtraCoins;
                            if (viewer.IsVip)
                                baseCoins += settings.VipExtraCoins;
                            if (viewer.IsModerator)
                                baseCoins += settings.ModExtraCoins;

                            coinsToAward = (int)(baseCoins * karmaMultiplier);
                        }

                        if (coinsToAward <= 0)
                            continue;

                        viewer.GiveCoins(coinsToAward);
                        awardedCount++;
                    }
                }

                if (awardedCount > 0)
                    SaveViewers();

                return awardedCount;
            }
            catch (Exception ex)
            {
                Logger.Error($"[Viewers] AwardActiveViewersCoins: {ex.Message}");
                return 0;
            }
        }

        public static List<Viewer> GetActiveViewers(int maxMinutesInactive = 30)
        {
            lock (_lock)
            {
                return All.Where(v => v != null && v.IsActive(maxMinutesInactive)).ToList();
            }
        }

        public static void GiveAllViewersCoins(int amount, List<Viewer> specificViewers = null)
        {
            lock (_lock)
            {
                var viewers = specificViewers ?? All;
                foreach (var viewer in viewers)
                    viewer?.GiveCoins(amount);
            }

            SaveViewers();
        }

        public static void GiveAllViewersKarma(float amount)
        {
            lock (_lock)
            {
                foreach (var viewer in All)
                    viewer?.GiveKarma(amount);
            }

            SaveViewers();
        }

        public static void SetAllViewersCoins(int amount, List<Viewer> specificViewers = null)
        {
            lock (_lock)
            {
                var viewers = specificViewers ?? All;
                foreach (var viewer in viewers)
                    viewer?.SetCoins(amount);
            }

            SaveViewers();
        }

        public static void SaveViewers()
        {
            if (_isSaving)
                return;

            try
            {
                _isSaving = true;

                lock (_lock)
                {
                    // Dedup without nested SaveViewers (re-entrancy guarded above)
                    RemoveDuplicateViewers(saveIfChanged: false);

                    var data = new ViewerData(All);
                    string json = Newtonsoft.Json.JsonConvert.SerializeObject(
                        data, Newtonsoft.Json.Formatting.Indented);

                    if (!JsonFileManager.SaveFile("viewers.json", json))
                        Logger.Error("[Viewers] Failed to write viewers.json");
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"[Viewers] SaveViewers: {ex.Message}");
            }
            finally
            {
                _isSaving = false;
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
                            RemoveDuplicateViewers(saveIfChanged: false);
                        }

                        Logger.Message($"[Viewers] Loaded {All.Count} viewers");
                    }
                    else
                    {
                        Logger.Warning("[Viewers] viewers.json had no viewer list");
                        All = new List<Viewer>();
                    }
                }
                else
                {
                    All = new List<Viewer>();
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"[Viewers] LoadViewers: {ex.Message}");
                All = new List<Viewer>();
            }
        }

        /// <summary>
        /// Merge duplicate / bogus entries by primary platform identifier.
        /// </summary>
        private static void RemoveDuplicateViewers(bool saveIfChanged)
        {
            try
            {
                lock (_lock)
                {
                    var uniqueViewers = new Dictionary<string, Viewer>(StringComparer.OrdinalIgnoreCase);
                    int duplicatesRemoved = 0;
                    int coinsMerged = 0;
                    int karmaMerged = 0;
                    int bogusMerged = 0;

                    foreach (var viewer in All.ToList())
                    {
                        if (viewer == null)
                            continue;

                        try
                        {
                            bool isBogus = IsBogusViewer(viewer);

                            string primaryKey;
                            try
                            {
                                primaryKey = viewer.GetPrimaryPlatformIdentifier();
                            }
                            catch
                            {
                                primaryKey = $"username:{(viewer.Username ?? "unknown").ToLowerInvariant()}";
                            }

                            if (isBogus)
                            {
                                Viewer realViewer = ResolveRealViewer(viewer);
                                if (realViewer != null && realViewer != viewer)
                                {
                                    coinsMerged += viewer.Coins;
                                    karmaMerged += (int)viewer.Karma;
                                    realViewer.GiveCoins(viewer.Coins);
                                    realViewer.GiveKarma(viewer.Karma);

                                    if (viewer.PlatformUserIds != null)
                                    {
                                        foreach (var plat in viewer.PlatformUserIds)
                                            realViewer.AddPlatformUserId(plat.Key, plat.Value);
                                    }

                                    All.Remove(viewer);
                                    bogusMerged++;
                                    continue;
                                }
                            }

                            if (uniqueViewers.TryGetValue(primaryKey, out var existing))
                            {
                                coinsMerged += viewer.Coins;
                                karmaMerged += (int)viewer.Karma;
                                existing.GiveCoins(viewer.Coins);
                                existing.GiveKarma(viewer.Karma);

                                if (viewer.PlatformUserIds != null)
                                {
                                    foreach (var plat in viewer.PlatformUserIds)
                                        existing.AddPlatformUserId(plat.Key, plat.Value);
                                }

                                duplicatesRemoved++;
                            }
                            else
                            {
                                uniqueViewers[primaryKey] = viewer;
                            }
                        }
                        catch (Exception exViewer)
                        {
                            Logger.Error(
                                $"[Viewers] Cleanup skipped corrupt entry '{viewer?.Username}': {exViewer.Message}");
                        }
                    }

                    All = uniqueViewers.Values.ToList();

                    if (bogusMerged > 0 || duplicatesRemoved > 0)
                    {
                        Logger.Message(
                            $"[Viewers] Cleanup: {bogusMerged} bogus merged, {duplicatesRemoved} duplicates removed " +
                            $"(+{coinsMerged} coins, +{karmaMerged} karma).");

                        if (saveIfChanged && !_isSaving)
                            SaveViewers();
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"[Viewers] RemoveDuplicateViewers: {ex.Message}");
            }
        }

        private static bool IsBogusViewer(Viewer viewer)
        {
            if (viewer == null)
                return false;

            string u = viewer.Username ?? string.Empty;
            return u.Contains(":") ||
                   (u.Length >= 5 && u.All(char.IsDigit));
        }

        private static Viewer ResolveRealViewer(Viewer bogusViewer)
        {
            if (bogusViewer?.PlatformUserIds != null)
            {
                foreach (var plat in bogusViewer.PlatformUserIds)
                {
                    var real = GetViewerByPlatformId(plat.Key, plat.Value);
                    if (real != null && real != bogusViewer)
                        return real;
                }
            }

            string id = bogusViewer?.Username ?? string.Empty;
            if (id.Contains(":"))
                id = id.Split(new[] { ':' }, 2)[1];

            if (!string.IsNullOrEmpty(id))
            {
                var real = GetViewerByPlatformId("twitch", id) ?? GetViewerByPlatformId("youtube", id);
                if (real != null && real != bogusViewer)
                    return real;
            }

            return null;
        }

        public static void ResetAllCoins()
        {
            var settings = CAPChatInteractiveMod.Instance?.Settings?.GlobalSettings;
            int amount = settings?.StartingCoins ?? 100;
            SetAllViewersCoins(amount);
        }

        public static void ResetAllKarma()
        {
            var settings = CAPChatInteractiveMod.Instance?.Settings?.GlobalSettings;
            float amount = settings?.StartingKarma ?? 100f;

            lock (_lock)
            {
                foreach (var viewer in All)
                    viewer?.SetKarma(amount);
            }

            SaveViewers();
        }

        /// <summary>
        /// Periodic karma decay (GameComponent). Summary log only when anyone is affected.
        /// </summary>
        public static void ApplyKarmaDecayToAll(CAPGlobalChatSettings settings)
        {
            if (settings == null || settings.KarmaDecayRate <= 0f || settings.KarmaDecayIntervalMinutes <= 0)
                return;

            lock (_lock)
            {
                int viewersAffected = 0;
                float totalKarmaLost = 0f;

                foreach (var viewer in All)
                {
                    if (viewer == null || viewer.IsBanned)
                        continue;

                    if (viewer.Karma <= settings.KarmaMinDecayFloor)
                        continue;

                    float decayAmount = viewer.Karma * settings.KarmaDecayRate / 100f;
                    if (decayAmount < settings.KarmaMinDecay)
                        decayAmount = settings.KarmaMinDecay;

                    float newKarma = viewer.Karma - decayAmount;
                    if (newKarma < settings.KarmaMinDecayFloor)
                        newKarma = settings.KarmaMinDecayFloor;

                    float actuallyLost = viewer.Karma - newKarma;
                    if (actuallyLost > 0f)
                    {
                        viewer.SetKarma(newKarma);
                        viewersAffected++;
                        totalKarmaLost += actuallyLost;
                    }
                }

                if (viewersAffected > 0)
                {
                    Logger.Message(
                        $"[Viewers] Karma decay: {viewersAffected} viewers, total −{totalKarmaLost:F1}");
                    SaveViewers();
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
                    if (viewer != null)
                        viewers.Add(new SimpleViewer(viewer));
                }
            }

            total = viewers.Count;
        }

        public List<Viewer> ToFullViewers()
        {
            var fullViewers = new List<Viewer>();
            if (viewers == null)
                return fullViewers;

            foreach (var simpleViewer in viewers)
            {
                if (simpleViewer == null || string.IsNullOrEmpty(simpleViewer.username))
                    continue;

                try
                {
                    var viewer = new Viewer(simpleViewer.username);
                    simpleViewer.UpdateViewer(viewer);
                    fullViewers.Add(viewer);
                }
                catch (Exception ex)
                {
                    Logger.Warning($"[Viewers] Skipping corrupt simple viewer '{simpleViewer.username}': {ex.Message}");
                }
            }

            return fullViewers;
        }
    }

    [Serializable]
    public class SimpleViewer
    {
        public string username;
        public float karma;
        public int coins;
        public bool isBanned;
        public Dictionary<string, string> platformIds;

        public SimpleViewer()
        {
            platformIds = new Dictionary<string, string>();
        }

        public SimpleViewer(Viewer viewer)
        {
            username = viewer?.Username;
            karma = viewer?.Karma ?? 0f;
            coins = viewer?.Coins ?? 0;
            isBanned = viewer?.IsBanned ?? false;
            platformIds = viewer?.PlatformUserIds != null
                ? new Dictionary<string, string>(viewer.PlatformUserIds)
                : new Dictionary<string, string>();
        }

        public void UpdateViewer(Viewer viewer)
        {
            if (viewer == null)
                return;

            viewer.SetKarma(karma);
            viewer.SetCoins(coins);
            viewer.IsBanned = isBanned;

            if (platformIds == null)
                return;

            foreach (var platformId in platformIds)
            {
                if (!string.IsNullOrEmpty(platformId.Key) && !string.IsNullOrEmpty(platformId.Value))
                    viewer.AddPlatformUserId(platformId.Key, platformId.Value);
            }
        }

        public string GetPrimaryPlatformId()
        {
            if (platformIds != null)
            {
                if (platformIds.TryGetValue("twitch", out string twitchId))
                    return $"twitch:{twitchId}";
                if (platformIds.TryGetValue("youtube", out string youtubeId))
                    return $"youtube:{youtubeId}";
                if (platformIds.TryGetValue("kick", out string kickId))
                    return $"kick:{kickId}";
            }

            return platformIds?.Values.FirstOrDefault() ?? username;
        }
    }
}
