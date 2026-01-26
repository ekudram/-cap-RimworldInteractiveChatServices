// PawnQueueCommandHandler.cs
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
// Handles pawn queue commands: !join, !leave, !queue, !accept
using System;


namespace CAP_ChatInteractive.Commands.CommandHandlers
{
    public static class PawnQueueCommandHandler
    {
        public static string HandleJoinQueueCommand(ChatMessageWrapper messageWrapper)
        {
            try
            {
                var assignmentManager = CAPChatInteractiveMod.GetPawnAssignmentManager();

                // DIRECT DICTIONARY CHECK: Check if user already has a pawn assigned
                string platformId = $"{messageWrapper.Platform.ToLowerInvariant()}:{messageWrapper.PlatformUserId}";
                string usernameLower = messageWrapper.Username.ToLowerInvariant();

                bool hasPawn = false;
                if (assignmentManager != null)
                {
                    // Check platform ID
                    if (assignmentManager.viewerPawnAssignments.TryGetValue(platformId, out string thingId))
                    {
                        Verse.Pawn existingPawn = GameComponent_PawnAssignmentManager.FindPawnByThingId(thingId);
                        hasPawn = (existingPawn != null);
                    }

                    // Check legacy username if platform ID didn't find anything
                    if (!hasPawn && assignmentManager.viewerPawnAssignments.TryGetValue(usernameLower, out thingId))
                    {
                        Verse.Pawn existingPawn = GameComponent_PawnAssignmentManager.FindPawnByThingId(thingId);
                        hasPawn = (existingPawn != null);
                    }
                }

                if (hasPawn)
                {
                    return "You already have a pawn assigned! Use !leave to release your current pawn first.";
                }

                // Check if already in queue - CHANGED: use message-based method
                if (assignmentManager.IsInQueue(messageWrapper.Username))
                {
                    int position = assignmentManager.GetQueuePosition(messageWrapper.Username);
                    return $"You are already in the pawn queue at position #{position}.";
                }

                // Add to queue - CHANGED: use message-based method
                if (assignmentManager.AddToQueue(messageWrapper))
                {
                    int position = assignmentManager.GetQueuePosition(messageWrapper.Username);
                    int queueSize = assignmentManager.GetQueueSize();
                    return $"✅ You have joined the pawn queue! Position: #{position} of {queueSize}";
                }

                return "Failed to join the pawn queue. Please try again.";
            }
            catch (Exception ex)
            {
                Logger.Error($"Error joining pawn queue: {ex}");
                return "Error joining pawn queue. Please try again.";
            }
        }

        public static string HandleLeaveQueueCommand(ChatMessageWrapper messageWrapper)
        {
            try
            {
                var assignmentManager = CAPChatInteractiveMod.GetPawnAssignmentManager();

                // CHANGED: Use message-based check for queue membership
                if (!assignmentManager.IsInQueue(messageWrapper.Username))
                {
                    return "You are not in the pawn queue.";
                }

                // CHANGED: This one is already correct (uses user parameter)
                if (assignmentManager.RemoveFromQueue(messageWrapper))
                {
                    return "✅ You have left the pawn queue.";
                }

                return "Failed to leave the pawn queue. Please try again.";
            }
            catch (Exception ex)
            {
                Logger.Error($"Error leaving pawn queue: {ex}");
                return "Error leaving pawn queue. Please try again.";
            }
        }

        public static string HandleQueueStatusCommand(ChatMessageWrapper messageWrapper)
        {
            var assignmentManager = CAPChatInteractiveMod.GetPawnAssignmentManager();

            if (assignmentManager.IsInQueue(messageWrapper.Username)) // Now this will work correctly
            {
                int position = assignmentManager.GetQueuePosition(messageWrapper.Username); // And this
                int queueSize = assignmentManager.GetQueueSize();
                return $"You are in the pawn queue at position #{position} of {queueSize}.";
            }
            else
            {
                int queueSize = assignmentManager.GetQueueSize();
                return $"You are not in the pawn queue. Current queue size: {queueSize}";
            }
        }

        public static string HandleAcceptPawnCommand(ChatMessageWrapper messageWrapper)
        {
            try
            {
                var assignmentManager = CAPChatInteractiveMod.GetPawnAssignmentManager();
                string platformId = $"{messageWrapper.Platform.ToLowerInvariant()}:{messageWrapper.PlatformUserId}";
                string usernameLower = messageWrapper.Username.ToLowerInvariant();
                bool hasPawn = false;

                Logger.Debug($"Accept pawn command received from {messageWrapper.Username}");

                // Check if user has a pending offer
                if (!assignmentManager.HasPendingOffer(messageWrapper))
                {
                    Logger.Debug($"No pending offer found for {messageWrapper.Username}");
                    return "You don't have a pending pawn offer. Join the queue with !join to get in line!";
                }

                if (assignmentManager != null)
                {
                    // Check platform ID
                    if (assignmentManager.viewerPawnAssignments.TryGetValue(platformId, out string thingId))
                    {
                        Verse.Pawn existingPawn = GameComponent_PawnAssignmentManager.FindPawnByThingId(thingId);
                        hasPawn = (existingPawn != null);
                    }

                    // Check legacy username if platform ID didn't find anything
                    if (!hasPawn && assignmentManager.viewerPawnAssignments.TryGetValue(usernameLower, out thingId))
                    {
                        Verse.Pawn existingPawn = GameComponent_PawnAssignmentManager.FindPawnByThingId(thingId);
                        hasPawn = (existingPawn != null);
                    }
                }

                if (hasPawn)
                {
                    return "You already have a pawn assigned! Use !leave to release your current pawn first.";
                }

                // Accept the offer and get the assigned pawn
                Verse.Pawn assignedPawn = assignmentManager.AcceptPendingOffer(messageWrapper);

                if (assignedPawn != null)
                {
                    Logger.Debug($"Successfully accepted pawn {assignedPawn.Name} for {messageWrapper.Username}");
                    return $"🎉 @{messageWrapper.Username}, you have accepted your pawn {assignedPawn.Name}. Welcome to the colony!";
                }
                else
                {
                    Logger.Debug($"Pawn acceptance failed for {messageWrapper.Username} - pawn no longer available");
                    return "❌ Your pawn offer is no longer valid. Please join the queue again with !join";
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error accepting pawn: {ex}");
                return "Error accepting pawn. Please try again.";
            }
        }
    }
}