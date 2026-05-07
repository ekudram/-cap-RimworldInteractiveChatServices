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
// Manages viewer data including loading, saving, and updating viewer information.

/*
 * CONCEPTUAL INSPIRATION:
 * Viewer management concept inspired by hodlhodl1132's TwitchToolkit (AGPLv3)
 * However, this implementation includes substantial architectural differences:
 * - Platform-based user identification system
 * - Enhanced serialization with Newtonsoft.Json
 * - Multi-platform viewer tracking
 * - Different data persistence model
 * - Queue management and pending offer systems
 * 
 * Original TwitchToolkit Copyright: 2019 hodlhodl1132
 * Community Preservation Modifications © 2025 Captolamia
 */

/*
 * IMPLEMENTATION NOTES:
 * - Twitch role hierarchy dictated by platform API requirements
 * - Karma systems are standard industry practice (non-protectable)
 * - Virtual currency management follows functional necessities
 * - All platform-specific structures follow external constraints
 */

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
        public static readonly object _lock = new object();
        private static string _dataFilePath;

        static Viewers()
        {
            _dataFilePath = JsonFileManager.GetFilePath("viewers.json");
            LoadViewers();
        }

        public static Viewer GetViewer(ChatMessageWrapper message)
        {
            if (message == null || string.IsNullOrEmpty(message.Username))
            {
                Logger.Warning("GetViewer: Message or username is null");
                return null;
            }

            // First try to find by platform ID (most reliable)
            if (!string.IsNullOrEmpty(message.PlatformUserId))
            {
                var viewerByPlatform = GetViewerByPlatformId(message.Platform, message.PlatformUserId);
                if (viewerByPlatform != null)
                {
                    return viewerByPlatform;
                }
            }

            // Fall back to username lookup
            return GetViewer(message.Username);
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

        public static Viewer GetViewerByPlatformIdentifier(string identifier)
        {
            if (string.IsNullOrEmpty(identifier)) return null;

            if (identifier.Contains(':'))
            {
                var parts = identifier.Split(new[] { ':' }, 2);
                if (parts.Length == 2)
                    return GetViewerByPlatformId(parts[0], parts[1]);
            }

            if (identifier.All(char.IsDigit))
                return GetViewerByPlatformId("twitch", identifier); // most common case

            return GetViewer(identifier);
        }

        public static Viewer GetViewerNoAdd(string username)
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
                    return null;
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

        /// <summary>
        /// Updates viewer activity based on a new chat message. This method is called whenever a new message is received and is responsible for:
        /// - Creating a new viewer if one does not already exist
        /// - Updating the viewer's message count and platform IDs
        /// - Saving viewer data periodically
        /// </summary>
        /// <param name="message"></param>
        public static void UpdateViewerActivity(ChatMessageWrapper message)
        {
            // === BULLET-PROOF GUARD (fixes the reported NRE) ===
            if (message == null || string.IsNullOrEmpty(message.Username))
            {
                Logger.Warning("[RICS Viewers] UpdateViewerActivity received null or empty message");
                return;
            }

            if (Current.Game == null)
            {
                Logger.Debug("[RICS Viewers] UpdateViewerActivity skipped — Current.Game is null (still loading or on main menu)");
                return;
            }

            if (Find.TickManager == null)
            {
                Logger.Debug("[RICS Viewers] UpdateViewerActivity skipped — TickManager not ready yet");
                return;
            }

            try
            {
                // Optional periodic cleanup (safe even if called frequently)
                if (Find.TickManager.TicksGame % 300 == 0 && All.Count > 0)
                {
                    RemoveDuplicateViewers();
                }

                var viewer = GetViewer(message);
                if (viewer == null)
                {
                    Logger.Warning($"[RICS Viewers] GetViewer returned null for {message.Username}");
                    return;
                }

                // Check if this will add a platform ID
                bool hadPlatformIdBefore = viewer.HasPlatform(message.Platform);

                viewer.UpdateFromMessage(message);

                // Check if a new platform ID was added → immediate save
                bool hasPlatformIdAfter = viewer.HasPlatform(message.Platform);

                if (!hadPlatformIdBefore && hasPlatformIdAfter)
                {
                    SaveViewers();
                }
                else if (viewer.MessageCount % 10 == 0)
                {
                    SaveViewers();
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"[RICS Viewers] Exception in UpdateViewerActivity for viewer '{message.Username}': {ex.Message}\n{ex.StackTrace}");
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
                        float karmaMultiplier = viewer.Karma / 100f;

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
                        //Logger.Debug($"Awarded {coinsToAward} coins to {viewer.Username} " +
                        //              $"(base: {settings.BaseCoinReward}, karma: {viewer.Karma}, " +
                        //              $"sub: {viewer.IsSubscriber}, vip: {viewer.IsVip}, mod: {viewer.IsModerator})");
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
                    RemoveDuplicateViewers();

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
                    var uniqueViewers = new Dictionary<string, Viewer>(StringComparer.OrdinalIgnoreCase);
                    int duplicatesRemoved = 0;
                    int coinsMerged = 0;
                    int karmaMerged = 0;
                    int bogusMerged = 0;

                    // === Edge cases explicitly handled in this pass (reviewed 2026-05-06) ===
                    // 1. Null entries in All → skipped immediately
                    // 2. Null/empty Username → safe via GetPrimary guard + local fallback below
                    // 3. Bogus viewer (digit-only or ":" in username) with no platform IDs → Resolve tries ID extraction + GetViewerByPlatformId lookup
                    // 4. ResolveRealViewer returns self → prevented by explicit != check
                    // 5. Multiple bogus → same real viewer → coins/karma accumulate via Give* (order-independent, object mutation + ref in dict)
                    // 6. Real processed before or after bogus → both work (merge mutates live object already in uniqueViewers)
                    // 7. Nested lock (this method → GetViewerByPlatformId) → safe (Monitor is reentrant)
                    // 8. Viewer with only future platforms (Kick etc.) or no platforms → username: fallback key (acceptable collision risk on username only)
                    // 9. One bad viewer in list → isolated by per-viewer try-catch (graceful degradation, no whole-cleanup abort)
                    // 10. No changes at all → still rebuilds All (removes nulls), SaveViewers skipped

                    foreach (var viewer in All.ToList())
                    {
                        if (viewer == null) continue;

                        try // NEW: per-viewer isolation so one corrupt entry cannot crash the entire cleanup or mod
                        {
                            bool isBogus = IsBogusViewer(viewer);

                            string primaryKey;
                            try
                            {
                                primaryKey = viewer.GetPrimaryPlatformIdentifier();
                            }
                            catch (Exception exKey)
                            {
                                Logger.Warning($"[Viewer Cleanup] Failed to compute primary key for viewer '{viewer.Username}': {exKey.Message} — using safe username fallback");
                                primaryKey = $"username:{(viewer.Username ?? "unknown").ToLowerInvariant()}";
                            }

                            if (isBogus)
                            {
                                Logger.Warning($"[Viewer Cleanup] Found bogus viewer with platform-style username: '{viewer.Username}'");

                                Viewer realViewer = ResolveRealViewer(viewer);
                                if (realViewer != null && realViewer != viewer)
                                {
                                    // Merge economy + platform IDs from bogus into the real one
                                    coinsMerged += viewer.Coins;
                                    karmaMerged += (int)viewer.Karma;
                                    realViewer.GiveCoins(viewer.Coins);
                                    realViewer.GiveKarma(viewer.Karma);

                                    foreach (var plat in viewer.PlatformUserIds)
                                    {
                                        realViewer.AddPlatformUserId(plat.Key, plat.Value);
                                    }

                                    All.Remove(viewer);
                                    bogusMerged++;
                                    Logger.Message($"[Viewer Cleanup] Merged bogus viewer '{viewer.Username}' → real viewer '{realViewer.Username}'");
                                    continue; // skip normal dedup path
                                }
                                // No real viewer found → fall through to normal dedup using primaryKey (usually username: style)
                            }

                            // Normal deduplication by primary platform identifier
                            if (uniqueViewers.TryGetValue(primaryKey, out var existing))
                            {
                                coinsMerged += viewer.Coins;
                                karmaMerged += (int)viewer.Karma;
                                existing.GiveCoins(viewer.Coins);
                                existing.GiveKarma(viewer.Karma);

                                foreach (var plat in viewer.PlatformUserIds)
                                {
                                    existing.AddPlatformUserId(plat.Key, plat.Value);
                                }

                                duplicatesRemoved++;
                                Logger.Debug($"[Viewer Cleanup] Merged duplicate '{viewer.Username}' into '{existing.Username}'");
                            }
                            else
                            {
                                uniqueViewers[primaryKey] = viewer;
                            }
                        }
                        catch (Exception exViewer)
                        {
                            // Graceful degradation: log and drop only the bad entry
                            Logger.Error($"[Viewer Cleanup] Error processing viewer '{viewer?.Username}': {exViewer.Message} — skipping this entry (non-fatal)");
                        }
                    }

                    All = uniqueViewers.Values.ToList();

                    if (bogusMerged > 0 || duplicatesRemoved > 0)
                    {
                        Logger.Message($"[Viewer Cleanup] Completed: {bogusMerged} bogus viewers merged, {duplicatesRemoved} duplicates removed. " +
                                       $"+{coinsMerged} coins and +{karmaMerged} karma merged.");
                        SaveViewers();
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error in RemoveDuplicateViewers: {ex.Message}");
            }
        }

        // Helper to detect bogus viewers
        private static bool IsBogusViewer(Viewer viewer)
        {
            if (viewer == null) return false;

            string u = viewer.Username ?? "";
            return u.Contains(":") ||
                   (u.All(char.IsDigit) && u.Length >= 5); // Twitch IDs are usually 8+ digits
        }

        // Helper to resolve the real viewer from a bogus one
        private static Viewer ResolveRealViewer(Viewer bogusViewer)
        {
            // Try platform IDs first
            foreach (var plat in bogusViewer.PlatformUserIds)
            {
                var real = GetViewerByPlatformId(plat.Key, plat.Value);
                if (real != null) return real;
            }

            // Try extracting ID from username if it looks like "twitch:12345" or just "12345"
            string id = bogusViewer.Username;
            if (id.Contains(":"))
            {
                id = id.Split(new[] { ':' }, 2)[1];
            }

            if (!string.IsNullOrEmpty(id))
            {
                var real = GetViewerByPlatformId("twitch", id) ?? GetViewerByPlatformId("youtube", id);
                if (real != null) return real;
            }

            return null;
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
                        Logger.Debug($"  Platform ID: {platformId.Key}: {platformId.Value}");
                    }
                    if (viewer.PlatformUserIds.Count == 0)
                    {
                        Logger.Debug($"  No platform IDs found!");
                    }
                }
            }
        }

        /// <summary>
        /// Applies karma decay to all viewers based on the current settings.
        /// Called periodically by CAPChatInteractive_GameComponent.
        /// Prevents players from sitting at max karma forever by slowly draining it over time.
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

                    if (viewer.Karma <= settings.MinKarma)
                        continue; // Already at minimum — nothing to decay

                    // Calculate how much to lose this tick
                    float decayAmount = viewer.Karma * settings.KarmaDecayRate;

                    // Enforce minimum decay per interval (prevents tiny decay on high-karma players)
                    if (decayAmount < settings.KarmaMinDecay)
                        decayAmount = settings.KarmaMinDecay;

                    // Apply decay but never go below MinKarma
                    float newKarma = viewer.Karma - decayAmount;
                    if (newKarma < settings.MinKarma)
                        newKarma = settings.MinKarma;

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
                    Logger.Message($"[Karma Decay] Applied to {viewersAffected} viewers. " +
                                   $"Total lost: {totalKarmaLost:F1} karma " +
                                   $"(rate: {settings.KarmaDecayRate}, min: {settings.KarmaMinDecay})");
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
        public float karma;
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
            // Logger.Debug($"SimpleViewer created for {username} with {platformIds.Count} platform IDs:");
            //foreach (var platformId in platformIds)
            //{
            //    Logger.Debug($" SimpleViewer {platformId.Key}: {platformId.Value}");
            //}
        }

        public void UpdateViewer(Viewer viewer)
        {
            viewer.SetKarma(this.karma);   // float → float, no precision loss
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
            if (platformIds.TryGetValue("kick", out string kickId)) return $"kick:{kickId}";  // Add more platforms as needed
            return platformIds.Values.FirstOrDefault() ?? username;
        }
    }
}