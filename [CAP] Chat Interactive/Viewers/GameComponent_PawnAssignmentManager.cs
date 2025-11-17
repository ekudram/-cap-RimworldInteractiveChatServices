// GameComponent_PawnAssignmentManager.cs
// Copyright (c) Captolamia. All rights reserved.
// Licensed under the AGPLv3 License. See LICENSE file in the project root for full license information.
// Manages the assignment of pawns to chat viewers, including queueing and pending offers.
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using CAP_ChatInteractive.Commands;

namespace CAP_ChatInteractive
{
    public class GameComponent_PawnAssignmentManager : GameComponent
    {
        // CHANGED: Use platform ID as primary key, with username as fallback
        public Dictionary<string, string> viewerPawnAssignments; // PlatformID_or_Username -> ThingID
        private List<string> pawnQueue; // PlatformID_or_Username in queue order
        private Dictionary<string, float> queueJoinTimes; // PlatformID_or_Username -> join time (ticks)
        private Dictionary<string, PendingPawnOffer> pendingOffers; // PlatformID_or_Username -> offer data
        private List<string> expiredOffers; // Offers that timed out

        public GameComponent_PawnAssignmentManager(Game game)
        {
            viewerPawnAssignments = new Dictionary<string, string>();
            pawnQueue = new List<string>();
            queueJoinTimes = new Dictionary<string, float>();
            pendingOffers = new Dictionary<string, PendingPawnOffer>();
            expiredOffers = new List<string>();
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref viewerPawnAssignments, "viewerPawnAssignments", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref pawnQueue, "pawnQueue", LookMode.Value);
            Scribe_Collections.Look(ref queueJoinTimes, "queueJoinTimes", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref pendingOffers, "pendingOffers", LookMode.Value, LookMode.Deep);
            Scribe_Collections.Look(ref expiredOffers, "expiredOffers", LookMode.Value);

            // Initialize if null after loading
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
        }

        public override void GameComponentTick()
        {
            base.GameComponentTick();

            // Check for expired offers every 60 ticks (about 1 second)
            if (Find.TickManager.TicksGame % 60 == 0)
            {
                CheckExpiredOffers();
            }
        }

        public override void LoadedGame()
        {
            base.LoadedGame();

            // Ensure race settings are initialized
            var raceSettings = JsonFileManager.LoadRaceSettings();
            if (raceSettings.Count == 0)
            {
                Logger.Debug("No race settings found, initializing defaults...");
                // This will trigger the initialization in Dialog_PawnSettings.LoadRaceSettings
                var dialog = new Dialog_PawnSettings();
                // Just creating the dialog will initialize the settings
            }
        }

        public void AssignPawnToViewer(ChatMessageWrapper message, Pawn pawn)
        {
            string identifier = GetViewerIdentifier(message);
            viewerPawnAssignments[identifier] = pawn.ThingID;
            Logger.Debug($"Assigned pawn {pawn.ThingID} to viewer {identifier}");
        }

        // NEW: Direct assignment method for dialog use
        public void AssignPawnToViewerDialog(string username, string platformID, Pawn pawn)
        {
            string identifier = GetLegacyIdentifier(username);
            viewerPawnAssignments[identifier] = pawn.ThingID;

            // Set pawn name to username
            if (pawn.Name is NameTriple nameTriple)
            {
                pawn.Name = new NameTriple(nameTriple.First, username, nameTriple.Last);
            }
            else
            {
                pawn.Name = new NameSingle(username);
            }

            Logger.Debug($"Directly assigned pawn {pawn.ThingID} to viewer {username}");
        }

        // === GetAssignedPawn Methods ===

        public Pawn GetAssignedPawn(ChatMessageWrapper message)
        {
            string identifier = FindViewerIdentifier(message.Username, message);

            if (viewerPawnAssignments.TryGetValue(identifier, out string thingId))
            {
                Logger.Debug($"Retrieving assigned pawn for {identifier}, ThingID: {thingId}");
                Pawn pawn = FindPawnByThingId(thingId);
                if (pawn != null)
                {
                    Logger.Debug($"Found pawn: {pawn.Name}, Dead: {pawn.Dead}, Destroyed: {pawn.Destroyed}");
                }
                else
                {
                    Logger.Debug($"No pawn found with ThingID: {thingId}");
                }
                return pawn;
            }
            Logger.Debug($"No assignment found for identifier: {identifier}");
            return null;
        }

        public Pawn GetAssignedPawn(string username)
        {
            string identifier = FindViewerIdentifier(username);
            return GetAssignedPawnIdentifier(identifier);
        }

        public Pawn GetAssingedPawn(string identifier)
        {
            return GetAssignedPawnIdentifier(identifier);
        }

        public string GetUsernameFromPlatformId(string platformId)
        {
            // Find the viewer that has this platform ID
            foreach (var viewer in Viewers.All)
            {
                foreach (var platformUserId in viewer.PlatformUserIds)
                {
                    string viewerPlatformId = $"{platformUserId.Key}:{platformUserId.Value}";
                    if (viewerPlatformId == platformId)
                    {
                        return viewer.Username;
                    }
                }
            }

            return platformId; // Fallback to platform ID if not found
        }

        // PRIVATE: Internal method that takes identifier directly
        public Pawn GetAssignedPawnIdentifier(string identifier)
        {
            if (viewerPawnAssignments.TryGetValue(identifier, out string thingId))
            {
                Logger.Debug($"Retrieving assigned pawn for {identifier}, ThingID: {thingId}");
                Pawn pawn = FindPawnByThingId(thingId);
                if (pawn != null)
                {
                    Logger.Debug($"Found pawn: {pawn.Name}, Dead: {pawn.Dead}, Destroyed: {pawn.Destroyed}");
                }
                else
                {
                    Logger.Debug($"No pawn found with ThingID: {thingId}");
                }
                return pawn;
            }
            Logger.Debug($"No assignment found for identifier: {identifier}");
            return null;
        }

        // === HasAssignedPawn Methods ===

        public bool HasAssignedPawn(ChatMessageWrapper message)
        {
            string identifier = FindViewerIdentifier(message.Username, message);
            if (viewerPawnAssignments.TryGetValue(identifier, out string thingId))
            {
                Logger.Debug($"Checking assigned pawn for {identifier}, ThingID: {thingId}");
                Pawn pawn = FindPawnByThingId(thingId);
                // Return true even if pawn is dead - we still want to allow resurrection
                return pawn != null;
            }
            return false;
        }

        public bool HasAssignedPawn(string username)
        {
            string identifier = FindViewerIdentifier(username);
            return HasAssignedPawnIdentifier(identifier);
        }

        // PRIVATE: Internal method that takes identifier directly
        private bool HasAssignedPawnIdentifier(string identifier)
        {
            if (viewerPawnAssignments.TryGetValue(identifier, out string thingId))
            {
                Logger.Debug($"Checking assigned pawn for {identifier}, ThingID: {thingId}");
                Pawn pawn = FindPawnByThingId(thingId);
                return pawn != null;
            }
            return false;
        }

        // === UnassignPawn Methods ===

        public void UnassignPawn(ChatMessageWrapper message)
        {
            string identifier = GetViewerIdentifier(message);
            if (viewerPawnAssignments.Remove(identifier))
            {
                Logger.Debug($"Removed pawn assignment for {identifier}");
            }

            // Also remove any legacy username assignment
            string legacyId = GetLegacyIdentifier(message.Username);
            viewerPawnAssignments.Remove(legacyId);
        }

        // NEW: Legacy overload for username-only calls
        public void UnassignPawn(string username)
        {
            string legacyId = GetLegacyIdentifier(username);
            if (viewerPawnAssignments.Remove(legacyId))
            {
                Logger.Debug($"Removed pawn assignment for {legacyId}");
            }
        }

        public IEnumerable<string> GetAllAssignedUsernames()
        {
            return viewerPawnAssignments.Keys.ToList();
        }

        public static Pawn FindPawnByThingId(string thingId)
        {
            if (string.IsNullOrEmpty(thingId))
                return null;

            // Search all maps for the pawn (alive)
            foreach (var map in Find.Maps)
            {
                foreach (var pawn in map.mapPawns.AllPawns)
                {
                    if (pawn.ThingID == thingId)
                        return pawn;
                }

                // NEW: Also search for dead pawns/corpses
                foreach (var thing in map.listerThings.AllThings)
                {
                    if (thing.ThingID == thingId && thing is Corpse corpse)
                    {
                        return corpse.InnerPawn;
                    }
                }
            }

            // Also check world pawns (alive and dead)
            var worldPawn = Find.WorldPawns.AllPawnsAlive.FirstOrDefault(p => p.ThingID == thingId);
            if (worldPawn != null) return worldPawn;

            // NEW: Check dead world pawns
            var deadWorldPawn = Find.WorldPawns.AllPawnsDead.FirstOrDefault(p => p.ThingID == thingId);
            return deadWorldPawn;
        }

        public List<Pawn> GetAllViewerPawns()
        {
            var viewerPawns = new List<Pawn>();
            foreach (var thingId in viewerPawnAssignments.Values)
            {
                var pawn = FindPawnByThingId(thingId);
                if (pawn != null && !pawn.Dead)
                {
                    viewerPawns.Add(pawn);
                }
            }
            return viewerPawns;
        }

        public bool IsViewerPawn(Pawn pawn)
        {
            return viewerPawnAssignments.Values.Contains(pawn.ThingID);
        }

        public string GetUsernameForPawn(Pawn pawn)
        {
            var entry = viewerPawnAssignments.FirstOrDefault(x => x.Value == pawn.ThingID);
            return entry.Key ?? null;
        }

        // Queue management methods
        public bool AddToQueue(ChatMessageWrapper message)
        {
            string identifier = GetViewerIdentifier(message);

            // Check if already in queue
            if (pawnQueue.Contains(identifier))
            {
                return false;
            }

            // Check if already has a pawn
            if (HasAssignedPawn(message))
            {
                return false;
            }

            pawnQueue.Add(identifier);
            queueJoinTimes[identifier] = Find.TickManager.TicksGame;
            Logger.Debug($"Added {identifier} to pawn queue");
            return true;
        }

        // NEW: Legacy overload for username-only calls
        public bool AddToQueue(string username)
        {
            string identifier = GetLegacyIdentifier(username);

            if (pawnQueue.Contains(identifier))
            {
                return false;
            }

            if (HasAssignedPawn(username))
            {
                return false;
            }

            pawnQueue.Add(identifier);
            queueJoinTimes[identifier] = Find.TickManager.TicksGame;
            return true;
        }

        // UPDATED: Remove from queue
        public bool RemoveFromQueue(ChatMessageWrapper message)
        {
            string identifier = GetViewerIdentifier(message);
            bool removed = pawnQueue.Remove(identifier);
            if (removed)
            {
                queueJoinTimes.Remove(identifier);
                Logger.Debug($"Removed {identifier} from pawn queue");
            }
            return removed;
        }

        // NEW: Legacy overload
        public bool RemoveFromQueue(string username)
        {
            // Find the viewer and get their platform ID
            var viewer = Viewers.GetViewer(username);
            if (viewer == null) return false;

            string platformId = viewer.GetPrimaryPlatformIdentifier();
            bool removed = pawnQueue.Remove(platformId);
            if (removed)
            {
                queueJoinTimes.Remove(platformId);
            }
            return removed;
        }

        public bool IsInQueue(string username)
        {
            // Find the viewer and get their platform ID
            var viewer = Viewers.GetViewer(username);
            if (viewer == null) return false;

            string platformId = viewer.GetPrimaryPlatformIdentifier();
            return pawnQueue.Contains(platformId);
        }

        // UPDATED: Pending offers to use platform IDs


        public string GetNextInQueue()
        {
            if (pawnQueue.Count == 0)
                return null;

            return pawnQueue[0];
        }

        public string PopNextInQueue()
        {
            if (pawnQueue.Count == 0)
                return null;

            string nextUser = pawnQueue[0];
            pawnQueue.RemoveAt(0);
            queueJoinTimes.Remove(nextUser);
            return nextUser;
        }

        public List<string> GetQueueList()
        {
            return new List<string>(pawnQueue);
        }

        public int GetQueuePosition(string username)
        {
            // Find the viewer and get their platform ID
            var viewer = Viewers.GetViewer(username);
            if (viewer == null) return -1;

            string platformId = viewer.GetPrimaryPlatformIdentifier();
            int position = pawnQueue.IndexOf(platformId);
            return position >= 0 ? position + 1 : -1;
        }

        public int GetQueueSize()
        {
            return pawnQueue.Count;
        }

        public void ClearQueue()
        {
            pawnQueue.Clear();
            queueJoinTimes.Clear();
        }

        public void AddPendingOffer(string username, string platformID, Pawn pawn, int timeoutSeconds = -1)
        {
            // Use global setting if not specified, default to 300 seconds (5 minutes)
            if (timeoutSeconds == -1)
            {
                var settings = CAPChatInteractiveMod.Instance?.Settings?.GlobalSettings;
                timeoutSeconds = settings?.PawnOfferTimeoutSeconds ?? 300;
            }

            // Use platform ID as key for security (prevents username spoofing)
            pendingOffers[platformID] = new PendingPawnOffer
            {
                Username = username,
                PlatformIdentifier = platformID, // Store the actual platform ID
                OfferTime = Find.TickManager.TicksGame,
                TimeoutTicks = timeoutSeconds * 60,
                PawnThingId = pawn?.ThingID
            };

            Logger.Debug($"Added pending offer for {username} with platform ID: {platformID}");
        }

        public bool HasPendingOffer(ChatMessageWrapper message)
        {
            string identifier = GetViewerIdentifier(message);
            return pendingOffers.ContainsKey(identifier);
        }

        // NEW: Legacy overload
        public bool HasPendingOffer(string username)
        {
            string identifier = GetLegacyIdentifier(username);
            return pendingOffers.ContainsKey(identifier);
        }

        public Pawn AcceptPendingOffer(ChatMessageWrapper message)
        {
            string platformIdentifier = GetViewerIdentifier(message);

            // Look for offer by platform ID (secure)
            if (pendingOffers.TryGetValue(platformIdentifier, out PendingPawnOffer offer))
            {
                pendingOffers.Remove(platformIdentifier);

                // Find the pawn by its stored ThingID
                Pawn pawn = FindPawnByThingId(offer.PawnThingId);

                // Only assign if pawn is still valid
                if (pawn != null && !pawn.Dead)
                {
                    // Set the pawn's nickname to the username
                    if (pawn.Name is NameTriple nameTriple)
                    {
                        pawn.Name = new NameTriple(nameTriple.First, message.Username, nameTriple.Last);
                    }
                    else
                    {
                        pawn.Name = new NameSingle(message.Username);
                    }

                    // Assign the pawn to the viewer using platform ID for security
                    AssignPawnToViewer(message, pawn);

                    Logger.Debug($"Successfully assigned pawn {pawn.Name} to viewer {message.Username}");
                    return pawn;
                }
                else
                {
                    Logger.Debug($"Pawn offer for {message.Username} failed - pawn null or dead");
                    return null;
                }
            }

            Logger.Debug($"No pending offer found for platform: {platformIdentifier}");
            return null;
        }

        public void RemovePendingOffer(ChatMessageWrapper message)
        {
            string identifier = GetViewerIdentifier(message);
            pendingOffers.Remove(identifier);
        }

        private void CheckExpiredOffers()
        {
            var currentTicks = Find.TickManager.TicksGame;
            var expired = new List<string>();

            foreach (var offer in pendingOffers)
            {
                if (currentTicks - offer.Value.OfferTime > offer.Value.TimeoutTicks)
                {
                    expired.Add(offer.Key);
                    expiredOffers.Add(offer.Key);

                    // Send timeout message to chat using the stored username
                    string timeoutMessage = $"⏰ Your pawn offer has expired! Join the queue again with !join";
                    ChatCommandProcessor.SendMessageToUsername(offer.Value.Username, timeoutMessage);
                }
            }

            // Remove expired offers
            foreach (string key in expired)
            {
                pendingOffers.Remove(key);
            }
        }

        public List<PendingPawnOffer> GetPendingOffers()
        {
            return pendingOffers.Values.ToList();
        }

        public List<string> GetExpiredOffers()
        {
            return new List<string>(expiredOffers);
        }

        public void ClearExpiredOffers()
        {
            expiredOffers.Clear();
        }

        // === Helper Methods ===

        private string GetViewerIdentifier(ChatMessageWrapper message)
        {
            // Priority 1: Platform User ID (most reliable, prevents spoofing)
            if (!string.IsNullOrEmpty(message.PlatformUserId))
            {
                return $"{message.Platform.ToLowerInvariant()}:{message.PlatformUserId}";
            }

            // Priority 2: Username (fallback for backwards compatibility)
            if (!string.IsNullOrEmpty(message.Username))
            {
                return $"username:{message.Username.ToLowerInvariant()}";
            }

            // Priority 3: Display Name (last resort)
            return $"name:{message.DisplayName?.ToLowerInvariant() ?? "unknown"}";
        }

        private string GetLegacyIdentifier(string username)
        {
            return $"username:{username.ToLowerInvariant()}";
        }

        private string FindViewerIdentifier(string username, ChatMessageWrapper message = null)
        {
            // If we have a message with platform info, use that first
            if (message != null)
            {
                string platformId = GetViewerIdentifier(message);
                if (viewerPawnAssignments.ContainsKey(platformId))
                    return platformId;
            }

            // Try legacy username identifier
            string legacyId = GetLegacyIdentifier(username);
            if (viewerPawnAssignments.ContainsKey(legacyId))
                return legacyId;

            // If no assignments found, return the best available identifier
            return message != null ? GetViewerIdentifier(message) : legacyId;
        }

    }

    // NEW: Pending offer data structure
    public class PendingPawnOffer : IExposable
    {
        public string Username;
        public string PlatformIdentifier; // NEW: Store the platform-based identifier
        public float OfferTime;
        public int TimeoutTicks;
        public string PawnThingId;

        public void ExposeData()
        {
            Scribe_Values.Look(ref Username, "username");
            Scribe_Values.Look(ref PlatformIdentifier, "platformIdentifier"); // NEW
            Scribe_Values.Look(ref OfferTime, "offerTime");
            Scribe_Values.Look(ref TimeoutTicks, "timeoutTicks");
            Scribe_Values.Look(ref PawnThingId, "pawnThingId");
        }

        public float TimeRemaining
        {
            get
            {
                float elapsed = Find.TickManager.TicksGame - OfferTime;
                return Mathf.Max(0, (TimeoutTicks - elapsed) / 60f); // Return seconds remaining
            }
        }

        public bool IsExpired
        {
            get
            {
                return TimeRemaining <= 0;
            }
        }
    }
}