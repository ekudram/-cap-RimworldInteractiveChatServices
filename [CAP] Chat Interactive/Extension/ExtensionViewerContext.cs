// ExtensionViewerContext.cs
// Copyright (c) Captolamia — RICS Twitch Extension bridge

using System;
using Verse;

namespace CAP_ChatInteractive.Extension
{
    /// <summary>
    /// Resolve the calling viewer from LocalHttp identity (dev header / query).
    /// Twitch JWT identity comes later with EBS.
    /// </summary>
    public static class ExtensionViewerContext
    {
        public static string ResolveViewerName(ExtensionJob job)
        {
            if (job == null || string.IsNullOrWhiteSpace(job.DevViewer))
                return null;
            string name = job.DevViewer.Trim();
            if (name.StartsWith("@", StringComparison.Ordinal))
                name = name.Substring(1);
            return string.IsNullOrWhiteSpace(name) ? null : name;
        }

        public static bool TryRequireGame(out string errorJson)
        {
            errorJson = null;
            if (Current.Game == null)
            {
                errorJson = ExtensionEnvelope.Fail("NoGame", "Load a colony first.");
                return false;
            }
            return true;
        }

        public static Pawn TryGetAssignedPawn(ExtensionJob job, out string errorJson)
        {
            errorJson = null;
            if (!TryRequireGame(out errorJson))
                return null;

            string name = ResolveViewerName(job);
            if (string.IsNullOrEmpty(name))
            {
                errorJson = ExtensionEnvelope.Fail(
                    "Unauthorized",
                    "No viewer identity. For LocalHttp send X-RICS-Dev-Viewer or ?viewer=");
                return null;
            }

            var mgr = CAPChatInteractiveMod.GetPawnAssignmentManager();
            if (mgr == null)
            {
                errorJson = ExtensionEnvelope.Fail("NoGame", "Pawn assignment is not ready.");
                return null;
            }

            return mgr.GetAssignedPawn(name);
        }

        /// <summary>
        /// Build a command wrapper that carries the viewer's real Twitch/YouTube/Kick id
        /// so pawn assignment (keys like twitch:123) still resolves. Does not create viewers.
        /// </summary>
        public static ChatMessageWrapper CreateCommandMessage(string viewerName, string rawMessage)
        {
            string name = (viewerName ?? "").Trim();
            if (name.StartsWith("@", StringComparison.Ordinal))
                name = name.Substring(1);

            Viewer viewer = Viewers.GetViewerNoAdd(name);
            string platform = "extension";
            string platformUserId = null;
            if (viewer != null)
            {
                if (!string.IsNullOrEmpty(viewer.Username))
                    name = viewer.Username;

                string ident = viewer.GetPrimaryPlatformIdentifier();
                int colon = ident != null ? ident.IndexOf(':') : -1;
                if (colon > 0)
                {
                    string plat = ident.Substring(0, colon);
                    string id = ident.Substring(colon + 1);
                    if (!string.IsNullOrEmpty(id)
                        && !plat.Equals("username", StringComparison.OrdinalIgnoreCase)
                        && !plat.Equals("aichatbot", StringComparison.OrdinalIgnoreCase))
                    {
                        platform = plat;
                        platformUserId = id;
                    }
                }
            }

            return new ChatMessageWrapper(name, rawMessage ?? "", platform, platformUserId);
        }
    }
}
