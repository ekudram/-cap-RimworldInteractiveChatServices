// File: ExtensionCommandsHandler.cs
//
// Copyright (c) Captolamia
// This file is part of CAP Chat Interactive (RICS).
// Licensed under the GNU Affero General Public License v3.0 or later.
// See LICENSE.txt in the project root for full license text.
//
// Viewer Hub command list + run. Panel-only results; not public chat.

using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Verse;

namespace CAP_ChatInteractive.Extension
{
    public static class ExtensionCommandsHandler
    {
        private static readonly string[] QuickActionIds = { "joinqueue", "openlootbox", "dye" };

        public static string HandleList(ExtensionJob job)
        {
            if (!ExtensionViewerContext.TryRequireGame(out string err))
                return err;

            string viewer = ExtensionViewerContext.ResolveViewerName(job);
            if (string.IsNullOrEmpty(viewer))
            {
                return ExtensionEnvelope.Fail(
                    "Unauthorized",
                    "No viewer identity. For LocalHttp send X-RICS-Dev-Viewer or ?viewer=");
            }

            try
            {
                string prefix = ChatCommandProcessor.GetCommandPrefix();
                var message = ExtensionViewerContext.CreateCommandMessage(viewer, prefix + "help");
                var visible = new List<object>();
                var byId = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (ChatCommand cmd in ChatCommandProcessor.EnumerateDistinctCommands()
                    .OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase))
                {
                    if (cmd == null || !cmd.IsEnabled())
                        continue;
                    if (ChatCommandProcessor.IsHiddenFromExtension(cmd.Name))
                        continue;

                    var settings = cmd.GetCommandSettings();
                    if (settings != null && settings.ExcludeFromPricelist)
                        continue;
                    if (!cmd.CanExecute(message))
                        continue;

                    string id = cmd.Name.ToLowerInvariant();
                    if (!byId.Add(id))
                        continue;

                    string helper = Plain(settings != null && !string.IsNullOrEmpty(settings.CommandDescription)
                        ? settings.CommandDescription
                        : cmd.Description);
                    if (string.IsNullOrEmpty(helper))
                        helper = "No extra arguments needed.";

                    visible.Add(new
                    {
                        id,
                        label = prefix + id,
                        helper,
                        argsHint = ArgsHint(id),
                        permission = cmd.PermissionLevel ?? "everyone"
                    });
                }

                var quick = new List<object>();
                foreach (string qid in QuickActionIds)
                {
                    if (!byId.Contains(qid))
                        continue;
                    quick.Add(new
                    {
                        id = qid,
                        label = QuickLabel(qid),
                        command = prefix + qid,
                        argsHint = ArgsHint(qid)
                    });
                }

                return ExtensionEnvelope.Ok(new
                {
                    prefix,
                    quickActions = quick,
                    commands = visible
                });
            }
            catch (Exception ex)
            {
                Logger.Error($"[RICS Extension] Commands list: {ex}");
                return ExtensionEnvelope.Fail("HandlerError", "Could not load commands.");
            }
        }

        public static string HandleRun(ExtensionJob job)
        {
            if (!ExtensionViewerContext.TryRequireGame(out string err))
                return err;

            string viewer = ExtensionViewerContext.ResolveViewerName(job);
            if (string.IsNullOrEmpty(viewer))
            {
                return ExtensionEnvelope.Fail(
                    "Unauthorized",
                    "No viewer identity. For LocalHttp send X-RICS-Dev-Viewer or ?viewer=");
            }

            ParseBody(job?.Body, out string commandId, out string args);
            var outcome = ChatCommandProcessor.ProcessExtensionCommand(viewer, commandId, args);
            if (!outcome.Success)
                return ExtensionEnvelope.Fail(outcome.ErrorCode, outcome.Message);

            return ExtensionEnvelope.Ok(new
            {
                panelOnly = true,
                command = (commandId ?? "").Trim().TrimStart('!', '$').ToLowerInvariant(),
                message = outcome.Message
            });
        }

        private static void ParseBody(string body, out string commandId, out string args)
        {
            commandId = "";
            args = "";
            if (string.IsNullOrWhiteSpace(body))
                return;
            try
            {
                var jo = JObject.Parse(body);
                commandId = jo.Value<string>("commandId")
                    ?? jo.Value<string>("id")
                    ?? jo.Value<string>("command")
                    ?? "";
                args = jo.Value<string>("args") ?? jo.Value<string>("arguments") ?? "";
            }
            catch
            {
                commandId = "";
                args = "";
            }
        }

        private static string QuickLabel(string id)
        {
            switch (id)
            {
                case "joinqueue": return "Join Queue";
                case "openlootbox": return "Open Lootbox";
                case "dye": return "Dye Clothing";
                default: return id;
            }
        }

        private static string ArgsHint(string id)
        {
            switch (id)
            {
                case "dye":
                case "setfavoritecolor":
                    return "color name or #hex";
                case "pawncheck":
                    return "viewer name";
                case "giftcoins":
                    return "viewer amount";
                case "buy":
                    return "item name";
                case "trait":
                case "addtrait":
                case "removetrait":
                    return "trait name";
                case "event":
                case "weather":
                case "raid":
                    return "optional name / wager";
                default:
                    return "";
            }
        }

        private static string Plain(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;
            string s = text;
            try { s = s.StripTags(); } catch { /* keep */ }
            s = Regex.Replace(s, @"<[^>]+>", string.Empty);
            return s.Trim();
        }
    }
}
