// GameComponent_PawnAssignmentManager.cs
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
// Assigns pawns to chat viewers (platform ID keys), queue, pending offers, death info.
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace CAP_ChatInteractive
{
    public class GameComponent_PawnAssignmentManager : GameComponent
    {
        /// <summary>PlatformID (or legacy username key) → ThingID</summary>
        public Dictionary<string, string> viewerPawnAssignments;

        private List<string> pawnQueue;
        private Dictionary<string, float> queueJoinTimes;
        private Dictionary<string, PendingPawnOffer> pendingOffers;
        private List<string> expiredOffers;
        private Dictionary<string, string> pawnOriginalNicknames;

        public GameComponent_PawnAssignmentManager(Game game)
        {
            EnsureInitialized();
        }

        private void EnsureInitialized()
        {
            if (viewerPawnAssignments == null)
                viewerPawnAssignments = new Dictionary<string, string>();
            if (pawnQueue == null)
                pawnQueue = new List<string>();
            if (queueJoinTimes == null)
                queueJoinTimes = new Dictionary<string, float>();
            if (pendingOffers == null)
                pendingOffers = new Dictionary<string, PendingPawnOffer>();
            if (expiredOffers == null)
                expiredOffers = new List<string>();
            if (pawnOriginalNicknames == null)
                pawnOriginalNicknames = new Dictionary<string, string>();
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref viewerPawnAssignments, "viewerPawnAssignments", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref pawnQueue, "pawnQueue", LookMode.Value);
            Scribe_Collections.Look(ref queueJoinTimes, "queueJoinTimes", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref pendingOffers, "pendingOffers", LookMode.Value, LookMode.Deep);
            Scribe_Collections.Look(ref expiredOffers, "expiredOffers", LookMode.Value);
            Scribe_Collections.Look(ref pawnOriginalNicknames, "pawnOriginalNicknames", LookMode.Value, LookMode.Value);
            EnsureInitialized();
        }

        public override void GameComponentTick()
        {
            base.GameComponentTick();

            if (Find.TickManager == null)
                return;

            if (Find.TickManager.TicksGame % 60 == 0)
            {
                try
                {
                    CheckExpiredOffers();
                }
                catch (Exception ex)
                {
                    Logger.Error($"[PawnAssign] CheckExpiredOffers: {ex.Message}");
                }
            }
        }

        // ── Assign ──────────────────────────────────────────────────────

        public void AssignPawnToViewer(ChatMessageWrapper message, Pawn pawn)
        {
            if (message == null || pawn == null)
            {
                Logger.Warning("[PawnAssign] AssignPawnToViewer: null message or pawn");
                return;
            }

            string identifier = GetViewerIdentifier(message);
            if (string.IsNullOrEmpty(identifier))
            {
                Logger.Warning("[PawnAssign] AssignPawnToViewer: empty identifier");
                return;
            }

            AssignCore(identifier, message.Username, pawn);
        }

        public void AssignPawnToViewerDialog(string username, string platformID, Pawn pawn)
        {
            if (string.IsNullOrEmpty(platformID) || pawn == null)
            {
                Logger.Warning("[PawnAssign] AssignPawnToViewerDialog: null platformID or pawn");
                return;
            }

            AssignCore(platformID, username, pawn);
        }

        private void AssignCore(string platformId, string username, Pawn pawn)
        {
            try
            {
                EnsureInitialized();

                viewerPawnAssignments[platformId] = pawn.ThingID;

                if (pawnQueue.Contains(platformId))
                {
                    pawnQueue.Remove(platformId);
                    queueJoinTimes.Remove(platformId);
                }

                StoreOriginalNickname(pawn);
                SetPawnNickname(pawn, username ?? platformId);
            }
            catch (Exception ex)
            {
                Logger.Error($"[PawnAssign] AssignCore failed for '{platformId}': {ex.Message}");
            }
        }

        // ── Get assigned ────────────────────────────────────────────────

        public Pawn GetAssignedPawn(ChatMessageWrapper message)
        {
            if (message == null)
                return null;

            return GetAssignedPawnIdentifier(GetViewerIdentifier(message));
        }

        public Pawn GetAssignedPawn(string username)
        {
            if (string.IsNullOrEmpty(username))
                return null;

            string identifier = FindViewerIdentifier(username);
            return GetAssignedPawnIdentifier(identifier);
        }

        public string GetUsernameFromPlatformId(string platformId)
        {
            if (string.IsNullOrEmpty(platformId) || Viewers.All == null)
                return platformId;

            try
            {
                foreach (var viewer in Viewers.All)
                {
                    if (viewer?.PlatformUserIds == null)
                        continue;

                    foreach (var platformUserId in viewer.PlatformUserIds)
                    {
                        string viewerPlatformId = $"{platformUserId.Key}:{platformUserId.Value}";
                        if (viewerPlatformId == platformId)
                            return viewer.Username;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Warning($"[PawnAssign] GetUsernameFromPlatformId: {ex.Message}");
            }

            return platformId;
        }

        public Pawn GetAssignedPawnIdentifier(string identifier)
        {
            if (!TryGetPawnAssignment(identifier, out string thingId))
                return null;

            return FindPawnByThingId(thingId);
        }

        private bool TryGetPawnAssignment(string identifier, out string thingId)
        {
            thingId = null;
            EnsureInitialized();

            if (string.IsNullOrEmpty(identifier))
                return false;

            return viewerPawnAssignments.TryGetValue(identifier, out thingId);
        }

        // ── Has assigned ────────────────────────────────────────────────

        public bool HasAssignedPawn(ChatMessageWrapper message)
        {
            if (message == null)
                return false;

            string identifier = FindViewerIdentifier(message.Username, message);
            return HasAssignedPawnIdentifier(identifier);
        }

        public bool HasAssignedPawn(string username)
        {
            if (string.IsNullOrEmpty(username))
                return false;

            return HasAssignedPawnIdentifier(FindViewerIdentifier(username));
        }

        private bool HasAssignedPawnIdentifier(string identifier)
        {
            if (!TryGetPawnAssignment(identifier, out string thingId))
                return false;

            // True even if dead — resurrection paths still need the link
            return FindPawnByThingId(thingId) != null;
        }

        // ── Unassign ────────────────────────────────────────────────────

        public void UnassignPawn(ChatMessageWrapper message)
        {
            if (message == null)
                return;

            try
            {
                EnsureInitialized();
                string identifier = GetViewerIdentifier(message);
                UnassignCore(identifier);

                // Legacy username: keys still cleared for old saves
                if (!string.IsNullOrEmpty(message.Username))
                    UnassignCore(GetLegacyIdentifier(message.Username));
            }
            catch (Exception ex)
            {
                Logger.Error($"[PawnAssign] UnassignPawn(message): {ex.Message}");
            }
        }

        public void UnassignPawn(string platformId)
        {
            if (string.IsNullOrEmpty(platformId))
                return;

            try
            {
                EnsureInitialized();
                if (UnassignCore(platformId))
                    return;

                // Fallback legacy username form
                string legacyId = GetLegacyIdentifier(platformId);
                UnassignCore(legacyId);
            }
            catch (Exception ex)
            {
                Logger.Error($"[PawnAssign] UnassignPawn(id): {ex.Message}");
            }
        }

        private bool UnassignCore(string identifier)
        {
            if (string.IsNullOrEmpty(identifier))
                return false;

            if (!TryGetPawnAssignment(identifier, out string thingId))
                return false;

            Pawn pawn = FindPawnByThingId(thingId);
            if (pawn != null)
                ResetPawnNickname(pawn, thingId);

            viewerPawnAssignments.Remove(identifier);
            return true;
        }

        private void StoreOriginalNickname(Pawn pawn)
        {
            if (pawn == null)
                return;

            EnsureInitialized();
            if (pawn.Name is NameTriple nameTriple && !pawnOriginalNicknames.ContainsKey(pawn.ThingID))
                pawnOriginalNicknames[pawn.ThingID] = nameTriple.Nick;
        }

        private void SetPawnNickname(Pawn pawn, string nick)
        {
            if (pawn == null)
                return;

            string safeNick = string.IsNullOrEmpty(nick) ? "Viewer" : nick;

            if (pawn.Name is NameTriple currentName)
                pawn.Name = new NameTriple(currentName.First, safeNick, currentName.Last);
            else
                pawn.Name = new NameSingle(safeNick);
        }

        private void ResetPawnNickname(Pawn pawn, string thingId)
        {
            if (pawn == null)
                return;

            EnsureInitialized();

            try
            {
                if (pawnOriginalNicknames.TryGetValue(thingId, out string originalNick))
                {
                    SetPawnNickname(pawn, originalNick);
                    pawnOriginalNicknames.Remove(thingId);
                }
                else
                {
                    SetPawnNickname(pawn, GenerateNewNickname(pawn));
                }
            }
            catch (Exception ex)
            {
                Logger.Warning($"[PawnAssign] ResetPawnNickname: {ex.Message}");
            }
        }

        private static string GenerateNewNickname(Pawn pawn)
        {
            if (pawn?.Name is NameTriple nameTriple && !string.IsNullOrEmpty(nameTriple.First))
                return nameTriple.First;

            string[] simpleNicks = { "Buddy", "Chief", "Mate", "Pal", "Friend", "Traveler" };
            return simpleNicks[Rand.Range(0, simpleNicks.Length)];
        }

        public IEnumerable<string> GetAllAssignedUsernames()
        {
            EnsureInitialized();
            return viewerPawnAssignments.Keys.ToList();
        }

        public static Pawn FindPawnByThingId(string thingId)
        {
            if (string.IsNullOrEmpty(thingId))
                return null;

            try
            {
                if (Find.Maps != null)
                {
                    foreach (var map in Find.Maps)
                    {
                        if (map?.mapPawns?.AllPawns == null)
                            continue;

                        foreach (var pawn in map.mapPawns.AllPawns)
                        {
                            if (pawn != null && pawn.ThingID == thingId)
                                return pawn;
                        }

                        if (map.listerThings?.AllThings == null)
                            continue;

                        foreach (var thing in map.listerThings.AllThings)
                        {
                            if (thing is Corpse corpse && thing.ThingID == thingId)
                                return corpse.InnerPawn;
                        }
                    }
                }

                if (Find.WorldPawns != null)
                {
                    var worldPawn = Find.WorldPawns.AllPawnsAlive?.FirstOrDefault(p => p != null && p.ThingID == thingId);
                    if (worldPawn != null)
                        return worldPawn;

                    return Find.WorldPawns.AllPawnsDead?.FirstOrDefault(p => p != null && p.ThingID == thingId);
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"[PawnAssign] FindPawnByThingId: {ex.Message}");
            }

            return null;
        }

        public struct PawnDeathInfo
        {
            public bool IsDead;
            public bool BodyExists;
            public string CauseOfDeath;
            public string BodyStatus;

            public override string ToString()
            {
                if (!IsDead)
                    return "Alive";

                string status = BodyExists ? "Deceased (body remains)" : "Deceased (body destroyed or missing)";
                if (!string.IsNullOrEmpty(CauseOfDeath))
                    return $"{status} — {CauseOfDeath}";
                return status;
            }
        }

        public static PawnDeathInfo GetPawnDeathInfo(Pawn pawn)
        {
            if (pawn == null)
            {
                return new PawnDeathInfo
                {
                    IsDead = true,
                    BodyExists = false,
                    CauseOfDeath = "Unknown",
                    BodyStatus = "Completely missing from records"
                };
            }

            bool isDead = pawn.Dead;
            bool bodyExists = !pawn.Destroyed;
            string cause = string.Empty;
            string bodyStatus;

            if (!isDead)
            {
                bodyStatus = "Alive";
            }
            else
            {
                cause = ExtractDeathCauseFromHediffs(pawn);
                bodyStatus = "Deceased (body remains)";
                if (!bodyExists)
                    bodyStatus = "Deceased (body destroyed or missing)";
            }

            return new PawnDeathInfo
            {
                IsDead = isDead,
                BodyExists = bodyExists,
                CauseOfDeath = cause,
                BodyStatus = bodyStatus
            };
        }

        public static PawnDeathInfo GetPawnDeathInfo(string thingId)
        {
            if (string.IsNullOrEmpty(thingId))
            {
                return new PawnDeathInfo
                {
                    IsDead = true,
                    BodyExists = false,
                    CauseOfDeath = "Unknown",
                    BodyStatus = "No identifier provided"
                };
            }

            return GetPawnDeathInfo(FindPawnByThingId(thingId));
        }

        private static string ExtractDeathCauseFromHediffs(Pawn pawn)
        {
            try
            {
                var hediffSet = pawn?.health?.hediffSet;
                if (hediffSet?.hediffs == null || hediffSet.hediffs.Count == 0)
                    return "mysterious or unknown causes";

                Hediff mostSevereBad = null;
                float highestSeverity = -1f;

                foreach (var hediff in hediffSet.hediffs)
                {
                    if (hediff?.def == null || !hediff.def.isBad)
                        continue;

                    if (hediff.def.lethalSeverity > 0f && hediff.Severity >= hediff.def.lethalSeverity)
                    {
                        mostSevereBad = hediff;
                        break;
                    }

                    if (hediff.Severity > highestSeverity)
                    {
                        highestSeverity = hediff.Severity;
                        mostSevereBad = hediff;
                    }
                }

                if (mostSevereBad != null)
                {
                    string cause = mostSevereBad.LabelCap ?? mostSevereBad.def.label ?? "fatal condition";
                    if (mostSevereBad is Hediff_Injury injury && injury.sourceDef != null)
                        cause += $" caused by {injury.sourceDef.label}";
                    return cause;
                }

                List<Hediff_Injury> injuries = new List<Hediff_Injury>();
                hediffSet.GetHediffs(ref injuries);

                var lastInjury = injuries.OrderByDescending(i => i.ageTicks).FirstOrDefault();
                if (lastInjury != null)
                {
                    string cause = lastInjury.LabelCap ?? lastInjury.def?.label ?? "fatal injuries";
                    if (lastInjury.sourceDef != null)
                        cause += $" caused by {lastInjury.sourceDef.label}";
                    return cause;
                }

                return "mysterious or unknown causes";
            }
            catch (Exception ex)
            {
                Logger.Error($"[PawnAssign] ExtractDeathCause: {ex.Message}");
                return "unknown causes (investigation error)";
            }
        }

        public static string GetPawnDeathReason(Pawn pawn) => GetPawnDeathInfo(pawn).ToString();

        public static string GetPawnDeathReason(string thingId) => GetPawnDeathInfo(thingId).ToString();

        public List<Pawn> GetAllViewerPawns()
        {
            EnsureInitialized();
            var viewerPawns = new List<Pawn>();

            foreach (var thingId in viewerPawnAssignments.Values)
            {
                var pawn = FindPawnByThingId(thingId);
                if (pawn != null && !pawn.Dead)
                    viewerPawns.Add(pawn);
            }

            return viewerPawns;
        }

        public bool IsViewerPawn(Pawn pawn)
        {
            if (pawn == null)
                return false;

            EnsureInitialized();
            return viewerPawnAssignments.Values.Contains(pawn.ThingID);
        }

        public string GetUsernameForPawn(Pawn pawn)
        {
            if (pawn == null)
                return null;

            EnsureInitialized();
            var entry = viewerPawnAssignments.FirstOrDefault(x => x.Value == pawn.ThingID);
            return entry.Key;
        }

        // ── Queue ───────────────────────────────────────────────────────

        public bool AddToQueue(ChatMessageWrapper messageWrapper)
        {
            if (messageWrapper == null || string.IsNullOrEmpty(messageWrapper.PlatformUserId))
                return false;

            try
            {
                EnsureInitialized();
                string platformId = BuildPlatformId(messageWrapper);
                if (string.IsNullOrEmpty(platformId))
                    return false;

                if (pawnQueue.Contains(platformId))
                    return false;

                string usernameLower = messageWrapper.Username?.ToLowerInvariant() ?? string.Empty;

                if (HasLivingAssignedPawn(platformId))
                    return false;

                if (!string.IsNullOrEmpty(usernameLower) && HasLivingAssignedPawn(usernameLower))
                    return false;

                pawnQueue.Add(platformId);
                if (Find.TickManager != null)
                    queueJoinTimes[platformId] = Find.TickManager.TicksGame;
                else
                    queueJoinTimes[platformId] = 0f;

                return true;
            }
            catch (Exception ex)
            {
                Logger.Error($"[PawnAssign] AddToQueue: {ex.Message}");
                return false;
            }
        }

        private bool HasLivingAssignedPawn(string key)
        {
            if (!TryGetPawnAssignment(key, out string thingId))
                return false;

            return FindPawnByThingId(thingId) != null;
        }

        public bool RemoveFromQueue(ChatMessageWrapper messageWrapper)
        {
            if (messageWrapper == null)
                return false;

            try
            {
                EnsureInitialized();
                string platformId = BuildPlatformId(messageWrapper);
                bool removed = pawnQueue.Remove(platformId);
                if (removed)
                    queueJoinTimes.Remove(platformId);
                return removed;
            }
            catch (Exception ex)
            {
                Logger.Error($"[PawnAssign] RemoveFromQueue: {ex.Message}");
                return false;
            }
        }

        public bool RemoveFromQueue(string username)
        {
            var viewer = Viewers.GetViewer(username);
            if (viewer == null)
                return false;

            try
            {
                EnsureInitialized();
                string platformId = viewer.GetPrimaryPlatformIdentifier();
                bool removed = pawnQueue.Remove(platformId);
                if (removed)
                    queueJoinTimes.Remove(platformId);
                return removed;
            }
            catch (Exception ex)
            {
                Logger.Error($"[PawnAssign] RemoveFromQueue(username): {ex.Message}");
                return false;
            }
        }

        public bool IsInQueue(string username)
        {
            var viewer = Viewers.GetViewer(username);
            if (viewer == null)
                return false;

            EnsureInitialized();
            return pawnQueue.Contains(viewer.GetPrimaryPlatformIdentifier());
        }

        public string GetNextInQueue()
        {
            EnsureInitialized();
            return pawnQueue.Count == 0 ? null : pawnQueue[0];
        }

        public string PopNextInQueue()
        {
            EnsureInitialized();
            if (pawnQueue.Count == 0)
                return null;

            string nextUser = pawnQueue[0];
            pawnQueue.RemoveAt(0);
            queueJoinTimes.Remove(nextUser);
            return nextUser;
        }

        public List<string> GetQueueList()
        {
            EnsureInitialized();
            return new List<string>(pawnQueue);
        }

        public int GetQueuePosition(string username)
        {
            var viewer = Viewers.GetViewer(username);
            if (viewer == null)
                return -1;

            EnsureInitialized();
            int position = pawnQueue.IndexOf(viewer.GetPrimaryPlatformIdentifier());
            return position >= 0 ? position + 1 : -1;
        }

        public int GetQueueSize()
        {
            EnsureInitialized();
            return pawnQueue.Count;
        }

        public void ClearQueue()
        {
            EnsureInitialized();
            pawnQueue.Clear();
            queueJoinTimes.Clear();
        }

        // ── Pending offers ──────────────────────────────────────────────

        public void AddPendingOffer(string username, string platformID, Pawn pawn, int timeoutSeconds = -1)
        {
            if (string.IsNullOrEmpty(platformID))
            {
                Logger.Warning("[PawnAssign] AddPendingOffer: empty platformID");
                return;
            }

            try
            {
                EnsureInitialized();

                if (timeoutSeconds == -1)
                {
                    var settings = CAPChatInteractiveMod.Instance?.Settings?.GlobalSettings;
                    timeoutSeconds = settings?.PawnOfferTimeoutSeconds ?? 300;
                }

                float offerTime = Find.TickManager?.TicksGame ?? 0;

                pendingOffers[platformID] = new PendingPawnOffer
                {
                    Username = username,
                    PlatformIdentifier = platformID,
                    OfferTime = offerTime,
                    TimeoutTicks = timeoutSeconds * 60,
                    PawnThingId = pawn?.ThingID
                };
            }
            catch (Exception ex)
            {
                Logger.Error($"[PawnAssign] AddPendingOffer: {ex.Message}");
            }
        }

        public bool HasPendingOffer(ChatMessageWrapper messageWrapper)
        {
            if (messageWrapper == null)
                return false;

            EnsureInitialized();
            string platformId = BuildPlatformId(messageWrapper);
            return !string.IsNullOrEmpty(platformId) && pendingOffers.ContainsKey(platformId);
        }

        public Pawn AcceptPendingOffer(ChatMessageWrapper messageWrapper)
        {
            if (messageWrapper == null)
                return null;

            try
            {
                EnsureInitialized();
                string platformId = BuildPlatformId(messageWrapper);
                if (string.IsNullOrEmpty(platformId))
                    return null;

                if (!pendingOffers.TryGetValue(platformId, out PendingPawnOffer offer))
                    return null;

                pendingOffers.Remove(platformId);

                Pawn pawn = FindPawnByThingId(offer.PawnThingId);
                if (pawn == null || pawn.Dead)
                {
                    Logger.Warning($"[PawnAssign] Pending offer for {messageWrapper.Username} failed — pawn missing or dead");
                    return null;
                }

                AssignPawnToViewer(messageWrapper, pawn);
                RemoveFromQueue(messageWrapper);
                return pawn;
            }
            catch (Exception ex)
            {
                Logger.Error($"[PawnAssign] AcceptPendingOffer: {ex.Message}");
                return null;
            }
        }

        public void RemovePendingOffer(ChatMessageWrapper messageWrapper)
        {
            if (messageWrapper == null)
                return;

            EnsureInitialized();
            string platformId = BuildPlatformId(messageWrapper);
            if (!string.IsNullOrEmpty(platformId))
                pendingOffers.Remove(platformId);
        }

        private void CheckExpiredOffers()
        {
            EnsureInitialized();
            if (Find.TickManager == null || pendingOffers.Count == 0)
                return;

            var currentTicks = Find.TickManager.TicksGame;
            var expired = new List<string>();

            foreach (var offer in pendingOffers)
            {
                if (offer.Value == null)
                {
                    expired.Add(offer.Key);
                    continue;
                }

                if (currentTicks - offer.Value.OfferTime > offer.Value.TimeoutTicks)
                {
                    expired.Add(offer.Key);
                    expiredOffers.Add(offer.Key);

                    try
                    {
                        if (!string.IsNullOrEmpty(offer.Value.Username))
                        {
                            ChatCommandProcessor.SendMessageToUsername(
                                offer.Value.Username,
                                "Your pawn offer has expired! Join the queue again with !join");
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Warning($"[PawnAssign] Timeout notify failed: {ex.Message}");
                    }
                }
            }

            foreach (string key in expired)
                pendingOffers.Remove(key);
        }

        public List<PendingPawnOffer> GetPendingOffers()
        {
            EnsureInitialized();
            return pendingOffers.Values.ToList();
        }

        public List<string> GetExpiredOffers()
        {
            EnsureInitialized();
            return new List<string>(expiredOffers);
        }

        public void ClearExpiredOffers()
        {
            EnsureInitialized();
            expiredOffers.Clear();
        }

        // ── Identifiers ─────────────────────────────────────────────────

        private static string BuildPlatformId(ChatMessageWrapper message)
        {
            if (message == null || string.IsNullOrEmpty(message.PlatformUserId))
                return null;

            string plat = message.Platform?.ToLowerInvariant() ?? "unknown";
            return $"{plat}:{message.PlatformUserId}";
        }

        private string GetViewerIdentifier(ChatMessageWrapper message)
        {
            if (message == null)
                return null;

            if (!string.IsNullOrEmpty(message.PlatformUserId))
            {
                string plat = message.Platform?.ToLowerInvariant() ?? "unknown";
                return $"{plat}:{message.PlatformUserId}";
            }

            if (!string.IsNullOrEmpty(message.Username))
                return message.Username.ToLowerInvariant();

            if (!string.IsNullOrEmpty(message.DisplayName))
                return $"name:{message.DisplayName.ToLowerInvariant()}";

            return null;
        }

        private static string GetLegacyIdentifier(string username)
        {
            return username?.ToLowerInvariant() ?? string.Empty;
        }

        /// <summary>
        /// Resolve assignment key for a username (platform id preferred, then legacy).
        /// </summary>
        private string FindViewerIdentifier(string username, ChatMessageWrapper message = null)
        {
            EnsureInitialized();

            if (string.IsNullOrEmpty(username))
                return null;

            string usernameClean = username.StartsWith("@") ? username.Substring(1) : username;
            string usernameLower = usernameClean.ToLowerInvariant();

            try
            {
                var viewer = Viewers.GetViewerNoAdd(usernameClean);
                if (viewer?.PlatformUserIds != null)
                {
                    foreach (var platformEntry in viewer.PlatformUserIds)
                    {
                        if (string.IsNullOrEmpty(platformEntry.Key) || string.IsNullOrEmpty(platformEntry.Value))
                            continue;

                        string platId = $"{platformEntry.Key.ToLowerInvariant()}:{platformEntry.Value}";
                        if (viewerPawnAssignments.ContainsKey(platId))
                            return platId;
                    }

                    string primaryId = viewer.GetPrimaryPlatformIdentifier();
                    if (!string.IsNullOrEmpty(primaryId) && viewerPawnAssignments.ContainsKey(primaryId))
                        return primaryId;
                }

                if (message != null)
                {
                    string platformId = GetViewerIdentifier(message);
                    if (!string.IsNullOrEmpty(platformId) && viewerPawnAssignments.ContainsKey(platformId))
                        return platformId;
                }

                if (viewerPawnAssignments.ContainsKey(usernameLower))
                    return usernameLower;

                string prefixedUsername = $"username:{usernameLower}";
                if (viewerPawnAssignments.ContainsKey(prefixedUsername))
                    return prefixedUsername;
            }
            catch (Exception ex)
            {
                Logger.Warning($"[PawnAssign] FindViewerIdentifier: {ex.Message}");
            }

            return null;
        }

        /// <summary>Migrate legacy username keys and drop missing-pawn links.</summary>
        public void FixAllPawnAssignments()
        {
            try
            {
                EnsureInitialized();

                int fixedCount = 0;
                int removedCount = 0;
                var assignmentsToRemove = new List<string>();
                var assignmentsToAdd = new Dictionary<string, string>();

                foreach (var assignment in viewerPawnAssignments.ToList())
                {
                    string key = assignment.Key;
                    string thingId = assignment.Value;

                    Pawn pawn = FindPawnByThingId(thingId);
                    if (pawn == null)
                    {
                        assignmentsToRemove.Add(key);
                        removedCount++;
                        continue;
                    }

                    bool isLegacy = !key.Contains(":") && !key.StartsWith("username:");
                    Viewer realViewer = isLegacy
                        ? Viewers.GetViewerNoAdd(key)
                        : Viewers.GetViewerByPlatformIdentifier(key);

                    if (realViewer == null)
                    {
                        assignmentsToRemove.Add(key);
                        removedCount++;
                        continue;
                    }

                    string correctPlatformID = realViewer.GetPrimaryPlatformIdentifier();
                    if (correctPlatformID != key)
                    {
                        assignmentsToRemove.Add(key);
                        assignmentsToAdd[correctPlatformID] = thingId;
                        fixedCount++;
                    }
                }

                foreach (string keyToRemove in assignmentsToRemove)
                    viewerPawnAssignments.Remove(keyToRemove);

                foreach (var newAssignment in assignmentsToAdd)
                    viewerPawnAssignments[newAssignment.Key] = newAssignment.Value;

                if (fixedCount > 0 || removedCount > 0)
                {
                    Logger.Message(
                        $"[PawnAssign] Cleanup: {fixedCount} fixed, {removedCount} removed.");
                    Messages.Message(
                        $"Fixed {fixedCount} pawn assignments and removed {removedCount} invalid ones.",
                        fixedCount > 0 ? MessageTypeDefOf.PositiveEvent : MessageTypeDefOf.NeutralEvent);
                }
                else
                {
                    Messages.Message("No invalid pawn assignments found.", MessageTypeDefOf.NeutralEvent);
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"[PawnAssign] FixAllPawnAssignments: {ex.Message}");
            }
        }
    }

    public class PendingPawnOffer : IExposable
    {
        public string Username;
        public string PlatformIdentifier;
        public float OfferTime;
        public int TimeoutTicks;
        public string PawnThingId;

        public void ExposeData()
        {
            Scribe_Values.Look(ref Username, "username");
            Scribe_Values.Look(ref PlatformIdentifier, "platformIdentifier");
            Scribe_Values.Look(ref OfferTime, "offerTime");
            Scribe_Values.Look(ref TimeoutTicks, "timeoutTicks");
            Scribe_Values.Look(ref PawnThingId, "pawnThingId");
        }

        public float TimeRemaining
        {
            get
            {
                if (Find.TickManager == null)
                    return 0f;

                float elapsed = Find.TickManager.TicksGame - OfferTime;
                return Mathf.Max(0, (TimeoutTicks - elapsed) / 60f);
            }
        }

        public bool IsExpired => TimeRemaining <= 0;
    }
}
