// PawnQueueCommandHandler.cs
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

                // Check if user already has a pawn
                if (assignmentManager.HasAssignedPawn(user.Username))
                {
                    return "You already have a pawn assigned! Use !leave to release your current pawn first.";
                }

                // Check if already in queue
                if (assignmentManager.IsInQueue(user.Username))
                {
                    int position = assignmentManager.GetQueuePosition(user.Username);
                    return $"You are already in the pawn queue at position #{position}.";
                }

                // Add to queue
                if (assignmentManager.AddToQueue(user.Username))
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

                if (!assignmentManager.IsInQueue(user.Username))
                {
                    return "You are not in the pawn queue.";
                }

                if (assignmentManager.RemoveFromQueue(user.Username))
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
                // This will be called when a user accepts a pawn offer
                // We'll implement this after the dialog system
                return "Pawn acceptance functionality coming soon!";
            }
            catch (Exception ex)
            {
                Logger.Error($"Error accepting pawn: {ex}");
                return "Error accepting pawn. Please try again.";
            }
        }
    }
}