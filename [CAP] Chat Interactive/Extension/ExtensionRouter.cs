// ExtensionRouter.cs
// Copyright (c) Captolamia — RICS Twitch Extension bridge

using System;

namespace CAP_ChatInteractive.Extension
{
    /// <summary>
    /// Shared router for LocalHttp and (later) OutboundPoll jobs.
    /// Must run on main thread for game data.
    /// </summary>
    public static class ExtensionRouter
    {
        public static string Handle(ExtensionJob job)
        {
            if (job == null)
                return ExtensionEnvelope.Fail("BadRequest", "Null job");

            string path = NormalizePath(job.Path);
            string method = (job.Method ?? "GET").ToUpperInvariant();

            if (path == "ping" || path == "" || path == "health")
                return ExtensionEnvelope.Ping();

            if (path == "owned" || path == "ownership" || path == "character/owned")
            {
                if (method == "GET")
                    return ExtensionOwnedHandler.HandleGet(job);
                return ExtensionEnvelope.Fail("MethodNotAllowed", "Use GET for owned list, POST owned/disown to unclaim.");
            }

            if (path == "owned/disown" || path == "owned/unclaim" || path == "ownership/disown")
            {
                if (method == "POST")
                    return ExtensionOwnedHandler.HandleDisown(job);
                return ExtensionEnvelope.Fail("MethodNotAllowed", "POST { \"id\": thingId } to unclaim.");
            }

            return ExtensionEnvelope.Fail("NotImplemented", "Path not implemented yet: " + path + " (R1 skeleton — add builders in R2+)");
        }

        public static string NormalizePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return "";
            path = path.Trim().ToLowerInvariant().Replace('\\', '/');
            // Strip /extension prefix if present
            if (path.StartsWith("/extension/", StringComparison.Ordinal))
                path = path.Substring("/extension/".Length);
            else if (path.StartsWith("extension/", StringComparison.Ordinal))
                path = path.Substring("extension/".Length);
            else if (path.StartsWith("/extension", StringComparison.Ordinal))
                path = path.Substring("/extension".Length).TrimStart('/');
            path = path.Trim('/');
            return path;
        }
    }
}
