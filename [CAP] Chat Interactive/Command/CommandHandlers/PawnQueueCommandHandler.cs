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
// !join / !leave / !queue / !accept — pawn offer queue
using System;
using RimWorld;
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
                if (assignmentManager == null)
                    return "RICS.PQCH.JoinFailed".Translate();

                if (HasBlockingAssignedPawn(assignmentManager, messageWrapper))
                    return "RICS.PQCH.AlreadyHasPawn".Translate();

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
                Logger.Error($"[PawnQueue] Error joining queue: {ex}");
                return "RICS.PQCH.ErrorJoin".Translate();
            }
        }

        public static string HandleLeaveQueueCommand(ChatMessageWrapper messageWrapper)
        {
            try
            {
                var assignmentManager = CAPChatInteractiveMod.GetPawnAssignmentManager();
                if (assignmentManager == null)
                    return "RICS.PQCH.LeaveFailed".Translate();

                if (!assignmentManager.IsInQueue(messageWrapper.Username))
                    return "RICS.PQCH.NotInQueue".Translate();

                if (assignmentManager.RemoveFromQueue(messageWrapper))
                    return "RICS.PQCH.LeaveSuccess".Translate();

                return "RICS.PQCH.LeaveFailed".Translate();
            }
            catch (Exception ex)
            {
                Logger.Error($"[PawnQueue] Error leaving queue: {ex}");
                return "RICS.PQCH.Error.LeaveQueue".Translate();
            }
        }

        public static string HandleQueueStatusCommand(ChatMessageWrapper messageWrapper)
        {
            try
            {
                var assignmentManager = CAPChatInteractiveMod.GetPawnAssignmentManager();
                if (assignmentManager == null)
                    return "RICS.PQCH.QueueStatusNot".Translate(0);

                int queueSize = assignmentManager.GetQueueSize();
                if (assignmentManager.IsInQueue(messageWrapper.Username))
                {
                    int position = assignmentManager.GetQueuePosition(messageWrapper.Username);
                    return "RICS.PQCH.QueueStatusIn".Translate(position, queueSize);
                }

                return "RICS.PQCH.QueueStatusNot".Translate(queueSize);
            }
            catch (Exception ex)
            {
                Logger.Error($"[PawnQueue] Error reading queue status: {ex}");
                return "RICS.PQCH.QueueStatusNot".Translate(0);
            }
        }

        public static string HandleAcceptPawnCommand(ChatMessageWrapper messageWrapper)
        {
            try
            {
                var assignmentManager = CAPChatInteractiveMod.GetPawnAssignmentManager();
                if (assignmentManager == null)
                    return "RICS.PQCH.AcceptFailed".Translate();

                if (!assignmentManager.HasPendingOffer(messageWrapper))
                    return "RICS.PQCH.NoPendingOffer".Translate();

                if (HasBlockingAssignedPawn(assignmentManager, messageWrapper))
                    return "RICS.PQCH.AcceptAlreadyHasPawn".Translate();

                Pawn assignedPawn = assignmentManager.AcceptPendingOffer(messageWrapper);
                if (assignedPawn != null)
                    return "RICS.PQCH.AcceptSuccess".Translate(messageWrapper.Username, assignedPawn.Name.ToString());

                return "RICS.PQCH.AcceptFailed".Translate();
            }
            catch (Exception ex)
            {
                Logger.Error($"[PawnQueue] Error accepting pawn: {ex}");
                return "RICS.PQCH.AcceptError".Translate();
            }
        }

        /// <summary>
        /// Living player-faction pawn already assigned (platform id, then legacy username key).
        /// Dead/missing entries do not block join/accept.
        /// </summary>
        private static bool HasBlockingAssignedPawn(
            GameComponent_PawnAssignmentManager assignmentManager,
            ChatMessageWrapper messageWrapper)
        {
            if (assignmentManager == null || messageWrapper == null)
                return false;

            string platformId = $"{messageWrapper.Platform?.ToLowerInvariant()}:{messageWrapper.PlatformUserId}";
            string usernameLower = messageWrapper.Username?.ToLowerInvariant() ?? "";

            if (assignmentManager.viewerPawnAssignments.TryGetValue(platformId, out string thingId)
                && IsBlockingPawn(GameComponent_PawnAssignmentManager.FindPawnByThingId(thingId)))
            {
                return true;
            }

            if (!string.IsNullOrEmpty(usernameLower)
                && assignmentManager.viewerPawnAssignments.TryGetValue(usernameLower, out thingId)
                && IsBlockingPawn(GameComponent_PawnAssignmentManager.FindPawnByThingId(thingId)))
            {
                return true;
            }

            // Prefer GetAssignedPawn when available (covers edge key formats)
            try
            {
                var viaApi = assignmentManager.GetAssignedPawn(messageWrapper);
                if (IsBlockingPawn(viaApi))
                    return true;
            }
            catch
            {
                // best effort
            }

            return false;
        }

        private static bool IsBlockingPawn(Pawn pawn)
        {
            if (pawn == null || pawn.Destroyed || pawn.Dead)
                return false;
            try
            {
                return pawn.Faction == Faction.OfPlayer || (pawn.Faction?.IsPlayer ?? false);
            }
            catch
            {
                return pawn.Spawned;
            }
        }
    }
}
