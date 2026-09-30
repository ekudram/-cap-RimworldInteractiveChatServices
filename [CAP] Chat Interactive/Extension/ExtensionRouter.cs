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

            if (path == "header" || path == "character" || path == "character/header")
                return ExtensionCharacterHandler.HandleHeader(job);
            if (path == "character/body-health" || path == "body-health")
                return ExtensionCharacterHandler.HandleBodyHealth(job);
            if (path == "character/gear" || path == "gear")
                return ExtensionCharacterHandler.HandleGear(job);
            if (path == "character/implants" || path == "implants")
                return ExtensionCharacterHandler.HandleImplants(job);
            if (path == "character/needs" || path == "needs")
                return ExtensionCharacterHandler.HandleNeeds(job);
            if (path == "character/backstories-traits" || path == "backstories-traits" || path == "story")
                return ExtensionCharacterHandler.HandleBackstoriesTraits(job);
            if (path == "character/stats" || path == "stats")
                return ExtensionCharacterHandler.HandleStats(job);
            if (path == "character/relations" || path == "relations")
                return ExtensionCharacterHandler.HandleRelations(job);

            if (path == "colony")
            {
                if (method == "GET")
                    return ExtensionColonyHandler.HandleGet(job);
                return ExtensionEnvelope.Fail("MethodNotAllowed", "Use GET for colony.");
            }

            if (path == "commands" || path == "commands/list")
            {
                if (method == "GET")
                    return ExtensionCommandsHandler.HandleList(job);
                return ExtensionEnvelope.Fail("MethodNotAllowed", "Use GET for commands/list.");
            }

            if (path == "commands/run")
            {
                if (method == "POST")
                    return ExtensionCommandsHandler.HandleRun(job);
                return ExtensionEnvelope.Fail("MethodNotAllowed", "POST { \"commandId\", \"args\" } to run a command.");
            }

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

            if (path == "store/items")
            {
                if (method == "GET")
                    return ExtensionStoreHandler.HandleItems(job);
                return ExtensionEnvelope.Fail("MethodNotAllowed", "Use GET for store/items.");
            }
            if (path == "store/traits")
            {
                if (method == "GET")
                    return ExtensionStoreHandler.HandleTraits(job);
                return ExtensionEnvelope.Fail("MethodNotAllowed", "Use GET for store/traits.");
            }
            if (path == "store/events")
            {
                if (method == "GET")
                    return ExtensionStoreHandler.HandleEvents(job);
                return ExtensionEnvelope.Fail("MethodNotAllowed", "Use GET for store/events.");
            }
            if (path == "store/weather")
            {
                if (method == "GET")
                    return ExtensionStoreHandler.HandleWeather(job);
                return ExtensionEnvelope.Fail("MethodNotAllowed", "Use GET for store/weather.");
            }
            if (path == "store/races")
            {
                if (method == "GET")
                    return ExtensionStoreHandler.HandleRaces(job);
                return ExtensionEnvelope.Fail("MethodNotAllowed", "Use GET for store/races.");
            }
            if (path == "store/buy")
            {
                if (method == "POST")
                    return ExtensionStoreHandler.HandleBuy(job);
                return ExtensionEnvelope.Fail("MethodNotAllowed", "POST { \"category\", \"name\" } to buy.");
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
