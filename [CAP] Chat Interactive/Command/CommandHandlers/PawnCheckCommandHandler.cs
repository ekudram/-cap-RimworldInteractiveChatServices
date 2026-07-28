// PawnCheckCommandHandler.cs
// !pawncheck <viewer> — injury-only report for another viewer's assigned pawn.

using System;
using System.Linq;
using Verse;

namespace CAP_ChatInteractive.Commands.CommandHandlers
{
    public static class PawnCheckCommandHandler
    {
        public static string HandlePawnCheck(ChatMessageWrapper messageWrapper, string[] args)
        {
            try
            {
                if (args == null || args.Length == 0 || string.IsNullOrWhiteSpace(args[0]))
                    return "RICS.PCH.Usage".Translate();

                string targetName = args[0].Trim();
                if (targetName.StartsWith("@"))
                    targetName = targetName.Substring(1);
                if (string.IsNullOrWhiteSpace(targetName))
                    return "RICS.PCH.Usage".Translate();

                var assignmentManager = CAPChatInteractiveMod.GetPawnAssignmentManager();
                if (assignmentManager == null)
                    return "RICS.PCH.NoAssignmentSystem".Translate();

                Pawn targetPawn = assignmentManager.GetAssignedPawn(targetName);
                if (targetPawn == null)
                    return "RICS.PCH.NoPawn".Translate(targetName);

                if (targetPawn.Dead || targetPawn.Destroyed)
                    return "RICS.PCH.PawnDead".Translate(FormatPatientLabel(targetName, targetPawn));

                string label = FormatPatientLabel(targetName, targetPawn);
                return MyPawnCommandHandler_Body.BuildInjuryOnlyReport(targetPawn, label);
            }
            catch (Exception ex)
            {
                Logger.Error($"Error in pawncheck: {ex}");
                return "RICS.PCH.Error".Translate();
            }
        }

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
            catch { /* ignore */ }

            return username;
        }
    }
}
