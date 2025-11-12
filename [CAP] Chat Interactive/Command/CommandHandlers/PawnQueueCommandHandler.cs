// PawnQueueCommandHandler.cs
// Copyright (c) Captolamia. All rights reserved.
// Licensed under the AGPLv3 License. See LICENSE file in the project root for full license information.
//
// Handles pawn queue commands: !join, !leave, !queue, !accept
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace CAP_ChatInteractive.Commands.CommandHandlers
{
    public static class PawnQueueCommandHandler
    {
        public static string HandleJoinQueueCommand(ChatMessageWrapper user)
        {
            try
            {
                var assignmentManager = CAPChatInteractiveMod.GetPawnAssignmentManager();

                // Check if user already has a pawn - CHANGED: use message-based method
                if (assignmentManager.HasAssignedPawn(user))
                {
                    return "You already have a pawn assigned! Use !leave to release your current pawn first.";
                }

                // Check if already in queue - CHANGED: use message-based method
                if (assignmentManager.IsInQueue(user.Username))
                {
                    int position = assignmentManager.GetQueuePosition(user.Username);
                    return $"You are already in the pawn queue at position #{position}.";
                }

                // Add to queue - CHANGED: use message-based method
                if (assignmentManager.AddToQueue(user))
                {
                    int position = assignmentManager.GetQueuePosition(user.Username);
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

        public static string HandleLeaveQueueCommand(ChatMessageWrapper user)
        {
            try
            {
                var assignmentManager = CAPChatInteractiveMod.GetPawnAssignmentManager();

                // CHANGED: Use message-based check for queue membership
                if (!assignmentManager.IsInQueue(user.Username))
                {
                    return "You are not in the pawn queue.";
                }

                // CHANGED: This one is already correct (uses user parameter)
                if (assignmentManager.RemoveFromQueue(user))
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

        public static string HandleQueueStatusCommand(ChatMessageWrapper user)
        {
            try
            {
                var assignmentManager = CAPChatInteractiveMod.GetPawnAssignmentManager();

                // CHANGED: Use message-based check for queue membership
                if (assignmentManager.IsInQueue(user.Username))
                {
                    int position = assignmentManager.GetQueuePosition(user.Username);
                    int queueSize = assignmentManager.GetQueueSize();
                    return $"You are in the pawn queue at position #{position} of {queueSize}.";
                }
                else
                {
                    int queueSize = assignmentManager.GetQueueSize();
                    return $"You are not in the pawn queue. Current queue size: {queueSize}";
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error checking queue status: {ex}");
                return "Error checking queue status.";
            }
        }

        public static string HandleAcceptPawnCommand(ChatMessageWrapper user)
        {
            try
            {
                var assignmentManager = CAPChatInteractiveMod.GetPawnAssignmentManager();

                Logger.Debug($"Accept pawn command received from {user.Username}");

                // Check if user has a pending offer
                if (!assignmentManager.HasPendingOffer(user))
                {
                    Logger.Debug($"No pending offer found for {user.Username}");
                    return "You don't have a pending pawn offer. Join the queue with !join to get in line!";
                }

                // Check if user already has a pawn
                if (assignmentManager.HasAssignedPawn(user))
                {
                    Logger.Debug($"User {user.Username} already has an assigned pawn");
                    assignmentManager.RemovePendingOffer(user);
                    return "You already have a pawn assigned! Use !leave to release your current pawn first.";
                }

                // Accept the offer and get the assigned pawn
                Pawn assignedPawn = assignmentManager.AcceptPendingOffer(user);

                if (assignedPawn != null)
                {
                    Logger.Debug($"Successfully accepted pawn {assignedPawn.Name} for {user.Username}");
                    return $"🎉 @{user.Username}, you have accepted your pawn {assignedPawn.Name}. Welcome to the colony!";
                }
                else
                {
                    Logger.Debug($"Pawn acceptance failed for {user.Username} - pawn no longer available");
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