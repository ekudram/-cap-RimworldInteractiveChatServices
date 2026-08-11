// File: PawnCheckCommandHandler.cs
//
// Copyright (c) Captolamia
// This file is part of CAP Chat Interactive (RICS).
// Licensed under the GNU Affero General Public License v3.0 or later.
// See LICENSE.txt in the project root for full license text.
//
// !pawncheck <viewer> — injury-only report for another viewer's assigned pawn.
using System;
using System.Linq;
using Verse;

namespace CAP_ChatInteractive.Commands.CommandHandlers
{
    public static class PawnCheckCommandHandler
    {
        private const string ReturnDivider = " | ";

        public static string HandlePawnCheck(ChatMessageWrapper messageWrapper, string[] args)
        {
            try
            {
                if (args == null || args.Length == 0)
                    return "RICS.PCH.Usage".Translate();

                // Allow multi-word names: !pawncheck Cool Viewer
                string targetName = string.Join(" ", args.Where(a => !string.IsNullOrWhiteSpace(a)).Select(a => a.Trim()));
                if (targetName.StartsWith("@"))
                    targetName = targetName.Substring(1).Trim();
                if (string.IsNullOrWhiteSpace(targetName))
                    return "RICS.PCH.Usage".Translate();

                var assignmentManager = CAPChatInteractiveMod.GetPawnAssignmentManager();
                if (assignmentManager == null)
                    return "RICS.PCH.NoAssignmentSystem".Translate();

                Pawn targetPawn = assignmentManager.GetAssignedPawn(targetName);
                if (targetPawn == null)
                    return "RICS.PCH.NoPawn".Translate(targetName);

                string label = FormatPatientLabel(targetName, targetPawn);

                if (targetPawn.Dead || targetPawn.Destroyed)
                {
                    string deathDetails;
                    try
                    {
                        deathDetails = GameComponent_PawnAssignmentManager.GetPawnDeathInfo(targetPawn).ToString();
                    }
                    catch
                    {
                        deathDetails = "deceased";
                    }

                    return "RICS.PCH.PawnDead".Translate(label)
                           + ReturnDivider
                           + "RICS.Return.PawnDeadReason".Translate(deathDetails);
                }

                return MyPawnCommandHandler_Body.BuildInjuryOnlyReport(targetPawn, label);
            }
            catch (Exception ex)
            {
                Logger.Error($"[PawnCheck] Error: {ex}");
                return "RICS.PCH.Error".Translate();
            }
        }

        /// <summary>Prefer pawn nick / short name, then viewer display name, then raw username.</summary>
        private static string FormatPatientLabel(string username, Pawn pawn)
        {
            if (pawn?.Name is NameTriple triple && !string.IsNullOrWhiteSpace(triple.Nick))
                return triple.Nick;

            if (pawn?.Name != null)
            {
                string shortName = pawn.Name.ToStringShort;
                if (!string.IsNullOrWhiteSpace(shortName))
                    return shortName;
            }

            try
            {
                var viewer = Viewers.GetViewerNoAdd(username)
                    ?? Viewers.All.FirstOrDefault(v =>
                        string.Equals(v.Username, username, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(v.DisplayName, username, StringComparison.OrdinalIgnoreCase));
                if (viewer != null)
                {
                    if (!string.IsNullOrWhiteSpace(viewer.DisplayName))
                        return viewer.DisplayName;
                    if (!string.IsNullOrWhiteSpace(viewer.Username) && viewer.Username.IndexOf(':') < 0)
                        return viewer.Username;
                }
            }
            catch
            {
                // best effort
            }

            return username;
        }
    }
}
