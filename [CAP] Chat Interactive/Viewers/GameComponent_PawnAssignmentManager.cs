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
// Manages the assignment of pawns to chat viewers, including queueing and pending offers.
//
/*
 * ARCHITECTURAL NOTES:
 * - Platform-based identification system (vs username-only)
 * - Secure assignment using platform user IDs  
 * - Multi-platform role derivation (Twitch + YouTube)
 * - Queue management with fairness algorithms
 * - Timeout-based pending offer system
 * - These represent substantial architectural differences from basic pawn assignment
 */

using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using static Mono.Security.X509.X520;


namespace CAP_ChatInteractive
{
    public class GameComponent_PawnAssignmentManager : GameComponent
    {
        // CHANGED: Use platform ID as primary key, with username as fallback
        public Dictionary<string, string> viewerPawnAssignments; // PlatformID -> ThingID
        private List<string> pawnQueue; // PlatformID in queue order
        private Dictionary<string, float> queueJoinTimes; // PlatformID -> join time (ticks)
        private Dictionary<string, PendingPawnOffer> pendingOffers; // PlatformID -> offer data
        private List<string> expiredOffers; // Offers that timed out
        private Dictionary<string, string> pawnOriginalNicknames = new Dictionary<string, string>(); // ThingID -> OriginalNickname


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
                var dialog = new Dialog_PawnRaceSettings();
                // Just creating the dialog will initialize the settings
            }
        }

        public void AssignPawnToViewer(ChatMessageWrapper message, Pawn pawn)
        {
            string identifier = GetViewerIdentifier(message);
            viewerPawnAssignments[identifier] = pawn.ThingID;

            // Remove from queue if they're in it
            if (pawnQueue.Contains(identifier))
            {
                pawnQueue.Remove(identifier);
                queueJoinTimes.Remove(identifier);
                Logger.Debug($"Removed {message.Username} from pawn queue after assignment");
            }

            // Store original nickname before changing it
            if (pawn.Name is NameTriple nameTriple && !pawnOriginalNicknames.ContainsKey(pawn.ThingID))
            {
                pawnOriginalNicknames[pawn.ThingID] = nameTriple.Nick;
            }

            // Set pawn nickname to username (keeping first/last names)
            if (pawn.Name is NameTriple currentName)
            {
                pawn.Name = new NameTriple(currentName.First, message.Username, currentName.Last);
            }
            else
            {
                pawn.Name = new NameSingle(message.Username);
            }

            Logger.Debug($"Assigned pawn {pawn.ThingID} to viewer {identifier}");
        }

        // NEW: Direct assignment method for dialog use
        public void AssignPawnToViewerDialog(string username, string platformID, Pawn pawn)
        {
            // Now we can assume platformID is valid (checked by the caller)
            viewerPawnAssignments[platformID] = pawn.ThingID;

            // Remove from queue if they're in it
            if (pawnQueue.Contains(platformID))
            {
                pawnQueue.Remove(platformID);
                queueJoinTimes.Remove(platformID);
                Logger.Debug($"Removed {username} (platform ID: {platformID}) from pawn queue after direct assignment");
            }

            // Store original nickname before changing it
            if (pawn.Name is NameTriple nameOldTriple && !pawnOriginalNicknames.ContainsKey(pawn.ThingID))
            {
                pawnOriginalNicknames[pawn.ThingID] = nameOldTriple.Nick;
            }

            // Set pawn name to username
            if (pawn.Name is NameTriple nameTriple)
            {
                pawn.Name = new NameTriple(nameTriple.First, username, nameTriple.Last);
            }
            else
            {
                pawn.Name = new NameSingle(username);
            }

            Logger.Debug($"Directly assigned pawn {pawn.ThingID} to viewer {username} using platform ID: {platformID}");
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
            // FIX: Check if identifier is null before using it
            if (string.IsNullOrEmpty(identifier))
            {
                Logger.Debug($"GetAssignedPawnIdentifier: No identifier provided for pawn lookup");
                return null;
            }

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
                Logger.Debug($" Checking assigned pawn for {identifier}, ThingID: {thingId}");
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

            // Reset nickname for platform ID assignment
            if (viewerPawnAssignments.TryGetValue(identifier, out string thingId))
            {
                Pawn pawn = FindPawnByThingId(thingId);
                if (pawn != null)
                {
                    ResetPawnNickname(pawn, thingId);
                }
                viewerPawnAssignments.Remove(identifier);
                Logger.Debug($"Removed pawn assignment for {identifier} and reset nickname");
            }

            // Also remove any legacy username assignment and reset nickname
            string legacyId = GetLegacyIdentifier(message.Username);
            if (viewerPawnAssignments.TryGetValue(legacyId, out thingId))
            {
                Pawn pawn = FindPawnByThingId(thingId);
                if (pawn != null)
                {
                    ResetPawnNickname(pawn, thingId);
                }
                viewerPawnAssignments.Remove(legacyId);
                Logger.Debug($"Removed legacy pawn assignment for {legacyId} and reset nickname");
            }
        }

        // NEW: Legacy overload for username-only calls
        public void UnassignPawn(string platformId)
        {
            if (viewerPawnAssignments.TryGetValue(platformId, out string thingId))
            {
                // Restore original nickname before removing assignment
                Pawn pawn = FindPawnByThingId(thingId);
                if (pawn != null)
                {
                    ResetPawnNickname(pawn, thingId);
                }

                viewerPawnAssignments.Remove(platformId);
                Logger.Debug($"Removed pawn assignment for platform ID: {platformId} and restored nickname");
            }
            else
            {
                // Also try legacy username format as fallback
                string legacyId = GetLegacyIdentifier(platformId);
                if (viewerPawnAssignments.TryGetValue(legacyId, out thingId))
                {
                    Pawn pawn = FindPawnByThingId(thingId);
                    if (pawn != null)
                    {
                        ResetPawnNickname(pawn, thingId);
                    }

                    viewerPawnAssignments.Remove(legacyId);
                    Logger.Debug($"Removed pawn assignment using legacy ID: {legacyId} and restored nickname");
                }
                else
                {
                    Logger.Debug($"No pawn assignment found for: {platformId}");
                }
            }
        }

        private void ResetPawnNickname(Pawn pawn, string thingId)
        {
            if (pawn.Name is NameTriple currentName)
            {
                string newNickname;

                if (pawnOriginalNicknames.TryGetValue(thingId, out string originalNickname))
                {
                    // Restore original nickname
                    newNickname = originalNickname;
                    pawnOriginalNicknames.Remove(thingId);
                    Logger.Debug($"Restored original nickname: '{newNickname}'");
                }
                else
                {
                    // Generate new RimWorld-style nickname
                    newNickname = GenerateRimWorldNickname(pawn);
                    Logger.Debug($"Generated new nickname: '{newNickname}'");
                }

                pawn.Name = new NameTriple(currentName.First, newNickname, currentName.Last);
            }
        }

        // Generate a RimWorld-appropriate nickname
        private string GenerateRimWorldNickname(Pawn pawn)
        {
            // Use RimWorld's name generation for nicknames
            // You could also use specific nickname lists based on pawn traits, background, etc.

            // Simple approach - use first name as nickname (common in RimWorld)
            if (pawn.Name is NameTriple nameTriple && !string.IsNullOrEmpty(nameTriple.First))
            {
                return nameTriple.First;
            }

            // Fallback - generate a simple nickname
            string[] simpleNicks = { "Buddy", "Chief", "Mate", "Pal", "Friend", "Traveler" };
            return simpleNicks[Rand.Range(0, simpleNicks.Length)];
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
        public bool AddToQueue(ChatMessageWrapper messageWrapper)
        {
            string platformId = $"{messageWrapper.Platform.ToLowerInvariant()}:{messageWrapper.PlatformUserId}";
            string usernameLower = messageWrapper.Username.ToLowerInvariant();

            // Check if already in queue
            if (pawnQueue.Contains(platformId))
            {
                return false;
            }

            // DIRECT DICTIONARY CHECK: Check if already has a pawn
            bool hasPawn = false;

            // Check platform ID
            if (viewerPawnAssignments.TryGetValue(platformId, out string thingId))
            {
                Pawn existingPawn = FindPawnByThingId(thingId);
                hasPawn = (existingPawn != null);
            }

            // Check legacy username if platform ID didn't find anything
            if (!hasPawn && viewerPawnAssignments.TryGetValue(usernameLower, out thingId))
            {
                Pawn existingPawn = FindPawnByThingId(thingId);
                hasPawn = (existingPawn != null);
            }

            if (hasPawn)
            {
                return false;
            }

            pawnQueue.Add(platformId);
            queueJoinTimes[platformId] = Find.TickManager.TicksGame;
            Logger.Debug($"Added {platformId} to pawn queue");
            return true;
        }

        // UPDATED: Remove from queue
        public bool RemoveFromQueue(ChatMessageWrapper messageWrapper)
        {
            string platformId = $"{messageWrapper.Platform.ToLowerInvariant()}:{messageWrapper.PlatformUserId}";
            bool removed = pawnQueue.Remove(platformId);
            if (removed)
            {
                queueJoinTimes.Remove(platformId);
                Logger.Debug($"Removed {platformId} from pawn queue");
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
            Logger.Debug($"Attempting to remove {username} : {platformId} from pawn queue");
            bool removed = pawnQueue.Remove(platformId);
            Logger.Debug(removed ? $"Successfully removed {platformId} from pawn queue" : $"{platformId} not found in pawn queue");
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

        public bool HasPendingOffer(ChatMessageWrapper messageWrapper)
        {
            string platformId = $"{messageWrapper.Platform.ToLowerInvariant()}:{messageWrapper.PlatformUserId}";
            return pendingOffers.ContainsKey(platformId);
        }

        // NEW: Legacy overload
        public bool HasPendingOffer(string username)
        {
            string identifier = GetLegacyIdentifier(username);
            return pendingOffers.ContainsKey(identifier);
        }

        public Pawn AcceptPendingOffer(ChatMessageWrapper messageWrapper)
        {
            string platformId = $"{messageWrapper.Platform.ToLowerInvariant()}:{messageWrapper.PlatformUserId}";

            // Look for offer by platform ID (secure)
            if (pendingOffers.TryGetValue(platformId, out PendingPawnOffer offer))
            {
                pendingOffers.Remove(platformId);

                // Find the pawn by its stored ThingID
                Pawn pawn = FindPawnByThingId(offer.PawnThingId);

                // Only assign if pawn is still valid
                if (pawn != null && !pawn.Dead)
                {
                    // Set the pawn's nickname to the username
                    if (pawn.Name is NameTriple nameTriple)
                    {
                        pawn.Name = new NameTriple(nameTriple.First, messageWrapper.Username, nameTriple.Last);
                    }
                    else
                    {
                        pawn.Name = new NameSingle(messageWrapper.Username);
                    }

                    // Assign the pawn to the viewer using platform ID for security
                    AssignPawnToViewer(messageWrapper, pawn);
                    RemoveFromQueue(messageWrapper);
                    Logger.Debug($"Successfully assigned pawn {pawn.Name} to viewer {messageWrapper.Username}");
                    return pawn;
                }
                else
                {
                    Logger.Debug($"Pawn offer for {messageWrapper.Username} failed - pawn null or dead");
                    return null;
                }
            }

            Logger.Debug($"No pending offer found for platform: {platformId}");
            return null;
        }

        public void RemovePendingOffer(ChatMessageWrapper messageWrapper)
        {
            string platformId = $"{messageWrapper.Platform.ToLowerInvariant()}:{messageWrapper.PlatformUserId}";
            pendingOffers.Remove(platformId);
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
                return $"{message.Username.ToLowerInvariant()}";
            }

            // Priority 3: Display Name (last resort)
            return $"name:{message.DisplayName?.ToLowerInvariant() ?? "unknown"}";
        }

        private string GetViewerIdentifier(string username)
        {
            
            return $"{username.ToLowerInvariant()}";
        }

        private string GetLegacyIdentifier(string username)
        {
            return $"{username.ToLowerInvariant()}";
        }

        /// <summary>
        /// Finds and returns platform ID by username
        /// improved and fixed to prevent spoofing and ensure accurate assignments
        /// version 1.0.15
        /// </summary>
        /// <param name="username"></param>
        /// <param name="message"></param>
        /// <returns></returns>
        private string FindViewerIdentifier(string username, ChatMessageWrapper message = null)
        {
            if (string.IsNullOrEmpty(username))
                return null;

            // Strip @ symbol from the beginning if present
            string usernameClean = username.StartsWith("@") ? username.Substring(1) : username;
            string usernameLower = usernameClean.ToLowerInvariant();

            // First, check if we have a direct assignment with any known platform IDs
            var viewer = Viewers.GetViewerNoAdd(usernameClean);
            if (viewer != null)
            {
                // Check each platform ID associated with this viewer
                foreach (var platformEntry in viewer.PlatformUserIds)
                {
                    string platId = $"{platformEntry.Key.ToLowerInvariant()}:{platformEntry.Value}";
                    if (viewerPawnAssignments.ContainsKey(platId))
                    {
                        Logger.Debug($"Found assignment under platform ID: {platId} for username: {usernameClean}");
                        return platId;
                    }
                }

                // Check if primary platform ID has assignment
                string primaryId = viewer.GetPrimaryPlatformIdentifier();
                if (viewerPawnAssignments.ContainsKey(primaryId))
                {
                    Logger.Debug($"Found assignment under primary ID: {primaryId} for username: {usernameClean}");
                    return primaryId;
                }
            }

            // Check if message provided has platform ID that matches an assignment
            if (message != null)
            {
                string platformId = GetViewerIdentifier(message);
                if (viewerPawnAssignments.ContainsKey(platformId))
                {
                    Logger.Debug($"Found assignment for message platform ID: {platformId}");
                    return platformId;
                }
            }

            // Legacy fallback: check if username was used as key (for backwards compatibility)
            if (viewerPawnAssignments.ContainsKey(usernameLower))
            {
                Logger.Debug($"Found legacy assignment under username: {usernameLower}");
                return usernameLower;
            }

            // Also check username with "username:" prefix
            string prefixedUsername = $"username:{usernameLower}";
            if (viewerPawnAssignments.ContainsKey(prefixedUsername))
            {
                Logger.Debug($"Found assignment under prefixed username: {prefixedUsername}");
                return prefixedUsername;
            }

            // If we get here, we couldn't find any assignment
            Logger.Debug($"No assignment found for {usernameClean}");
            return null;
        }

        // Fix Viewer Pawns
        // Add this method to fix legacy pawn assignments
        public void FixAllPawnAssignments()
        {
            int fixedCount = 0;
            int removedCount = 0;
            var assignmentsToRemove = new List<string>();
            var assignmentsToAdd = new Dictionary<string, string>();

            Logger.Debug("Starting pawn assignment fix...");

            foreach (var assignment in viewerPawnAssignments.ToList())
            {
                string key = assignment.Key;
                string thingId = assignment.Value;

                // Check if this is a legacy username assignment (no platform prefix)
                if (!key.Contains(":") && !key.StartsWith("username:"))
                {
                    Logger.Debug($"Found legacy assignment: {key} -> {thingId}");

                    // Try to find the viewer by username
                    Viewer viewer = Viewers.GetViewerNoAdd(key);
                    if (viewer != null)
                    {
                        string platformId = viewer.GetPrimaryPlatformIdentifier();

                        // Only proceed if we found a proper platform ID (not the fallback)
                        if (platformId.Contains(":"))
                        {
                            Logger.Debug($"Found platform ID for {key}: {platformId}");

                            // Mark legacy assignment for removal
                            assignmentsToRemove.Add(key);

                            // Add new assignment with platform ID
                            assignmentsToAdd[platformId] = thingId;
                            fixedCount++;

                            Logger.Debug($"Fixed assignment: {key} -> {platformId}");
                        }
                        else
                        {
                            Logger.Debug($"No valid platform ID found for {key}, keeping legacy assignment");
                        }
                    }
                    else
                    {
                        Logger.Debug($"Viewer {key} not found in database, removing assignment");
                        assignmentsToRemove.Add(key);
                        removedCount++;
                    }
                }
            }

            // Apply changes
            foreach (string keyToRemove in assignmentsToRemove)
            {
                viewerPawnAssignments.Remove(keyToRemove);
            }

            foreach (var newAssignment in assignmentsToAdd)
            {
                viewerPawnAssignments[newAssignment.Key] = newAssignment.Value;
            }

            // Save the changes
            if (fixedCount > 0 || removedCount > 0)
            {
                Logger.Debug($"Pawn assignment fix completed: {fixedCount} fixed, {removedCount} removed");
                Messages.Message($"Fixed {fixedCount} pawn assignments and removed {removedCount} invalid assignments",
                                fixedCount > 0 ? MessageTypeDefOf.PositiveEvent : MessageTypeDefOf.NeutralEvent);
            }
            else
            {
                Logger.Debug("No legacy pawn assignments found to fix");
                Messages.Message("No legacy pawn assignments found to fix", MessageTypeDefOf.NeutralEvent);
            }
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