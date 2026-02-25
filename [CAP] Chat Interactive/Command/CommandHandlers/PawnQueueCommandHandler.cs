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
using Verse;


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
                    if (assignmentManager.viewerPawnAssignments.TryGetValue(platformId, out string thingId))
                    {
                        Verse.Pawn existingPawn = GameComponent_PawnAssignmentManager.FindPawnByThingId(thingId);
                        hasPawn = (existingPawn != null);
                    }

                    if (!hasPawn && assignmentManager.viewerPawnAssignments.TryGetValue(usernameLower, out thingId))
                    {
                        Verse.Pawn existingPawn = GameComponent_PawnAssignmentManager.FindPawnByThingId(thingId);
                        hasPawn = (existingPawn != null);
                    }
                }

                if (hasPawn)
                {
                    return "RICS.PQCH.AlreadyHasPawn".Translate();
                }

                if (assignmentManager.IsInQueue(messageWrapper.Username))
                {
                    int position = assignmentManager.GetQueuePosition(messageWrapper.Username);
                    return "RICS.PQCH.AlreadyInQueue".Translate(position);
                }

                if (assignmentManager.AddToQueue(messageWrapper))
                {
                    int position = assignmentManager.GetQueuePosition(messageWrapper.Username);
                    int queueSize = assignmentManager.GetQueueSize();
                    return "RICS.PQCH.JoinSuccess".Translate(position, queueSize);
                }

                return "RICS.PQCH.JoinFailed".Translate();
            }
            catch (Exception ex)
            {
                Logger.Error($"Error joining pawn queue: {ex}");
                return "RICS.PQCH.ErrorJoin".Translate();
            }
        }

        public static string HandleLeaveQueueCommand(ChatMessageWrapper messageWrapper)
        {
            try
            {
                var assignmentManager = CAPChatInteractiveMod.GetPawnAssignmentManager();

                if (!assignmentManager.IsInQueue(messageWrapper.Username))
                {
                    return "RICS.PQCH.NotInQueue".Translate();
                }

                if (assignmentManager.RemoveFromQueue(messageWrapper))
                {
                    return "RICS.PQCH.LeaveSuccess".Translate();
                }

                return "RICS.PQCH.LeaveFailed".Translate();
            }
            catch (Exception ex)
            {
                Logger.Error($"Error leaving pawn queue: {ex}");
                return "Error leaving pawn queue. Please try again."; // keep fallback simple or use a generic key
            }
        }

        public static string HandleQueueStatusCommand(ChatMessageWrapper messageWrapper)
        {
            var assignmentManager = CAPChatInteractiveMod.GetPawnAssignmentManager();

            if (assignmentManager.IsInQueue(messageWrapper.Username))
            {
                int position = assignmentManager.GetQueuePosition(messageWrapper.Username);
                int queueSize = assignmentManager.GetQueueSize();
                return "RICS.PQCH.QueueStatusIn".Translate(position, queueSize);
            }
            else
            {
                int queueSize = assignmentManager.GetQueueSize();
                return "RICS.PQCH.QueueStatusNot".Translate(queueSize);
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

                if (!assignmentManager.HasPendingOffer(messageWrapper))
                {
                    Logger.Debug($"No pending offer found for {messageWrapper.Username}");
                    return "RICS.PQCH.NoPendingOffer".Translate();
                }

                if (assignmentManager != null)
                {
                    if (assignmentManager.viewerPawnAssignments.TryGetValue(platformId, out string thingId))
                    {
                        Verse.Pawn existingPawn = GameComponent_PawnAssignmentManager.FindPawnByThingId(thingId);
                        hasPawn = (existingPawn != null);
                    }

                    if (!hasPawn && assignmentManager.viewerPawnAssignments.TryGetValue(usernameLower, out thingId))
                    {
                        Verse.Pawn existingPawn = GameComponent_PawnAssignmentManager.FindPawnByThingId(thingId);
                        hasPawn = (existingPawn != null);
                    }
                }

                if (hasPawn)
                {
                    return "RICS.PQCH.AcceptAlreadyHasPawn".Translate();
                }

                Verse.Pawn assignedPawn = assignmentManager.AcceptPendingOffer(messageWrapper);

                if (assignedPawn != null)
                {
                    Logger.Debug($"Successfully accepted pawn {assignedPawn.Name} for {messageWrapper.Username}");
                    return "RICS.PQCH.AcceptSuccess".Translate(messageWrapper.Username, assignedPawn.Name.ToString());
                }
                else
                {
                    Logger.Debug($"Pawn acceptance failed for {messageWrapper.Username} - pawn no longer available");
                    return "RICS.PQCH.AcceptFailed".Translate();
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error accepting pawn: {ex}");
                return "RICS.PQCH.AcceptError".Translate();
            }
        }
    }
}