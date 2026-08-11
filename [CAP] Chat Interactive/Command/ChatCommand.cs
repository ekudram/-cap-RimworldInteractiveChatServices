// ChatCommand.cs
// Copyright (c) Captolamia
// This file is part of CAP Chat Interactive (RICS).
// Licensed under the GNU Affero General Public License v3.0 or later.
// See LICENSE.txt in the project root for full license text.
//
// Base chat command type + command settings load/save helpers.
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace CAP_ChatInteractive
{
    public abstract class ChatCommand
    {
        public abstract string Name { get; }

        public virtual string Alias
        {
            get
            {
                var settings = GetCommandSettings();
                if (!string.IsNullOrEmpty(settings?.CommandAlias))
                    return settings.CommandAlias.Trim().ToLowerInvariant();
                return null;
            }
        }

        public virtual string Description => "No description available";

        public virtual string PermissionLevel
        {
            get
            {
                if (this is DefBasedChatCommand defCommand)
                    return defCommand.PermissionLevel;

                var settings = GetCommandSettings();
                return settings?.PermissionLevel ?? "everyone";
            }
        }

        public virtual int CooldownSeconds => GetCommandSettings()?.CooldownSeconds ?? 0;

        public abstract string Execute(ChatMessageWrapper user, string[] args);

        public virtual bool CanExecute(ChatMessageWrapper message)
        {
            if (message == null)
                return false;

            var viewer = Viewers.GetViewer(message);
            if (viewer == null)
                return false;

            return viewer.HasPermission(PermissionLevel);
        }

        public virtual CommandSettings GetCommandSettings()
        {
            return CommandSettingsManager.GetSettings(Name);
        }

        public bool IsEnabled()
        {
            return GetCommandSettings()?.Enabled ?? true;
        }

        /// <summary>
        /// Optional hook for Command Editor &lt;type&gt;Button&lt;/type&gt; CustomData actions.
        /// </summary>
        public virtual void OnCustomDataButtonClicked(string buttonName, CommandSettings settings)
        {
        }
    }

    public static class CommandSettingsManager
    {
        public static CommandSettings GetSettings(string commandName)
        {
            try
            {
                if (string.IsNullOrEmpty(commandName))
                    return new CommandSettings();

                var dialog = Find.WindowStack?.WindowOfType<Dialog_CommandManager>();
                if (dialog?.commandSettings != null &&
                    dialog.commandSettings.TryGetValue(commandName, out var dialogSettings) &&
                    dialogSettings != null)
                {
                    return dialogSettings;
                }

                return LoadSettingsFromJson(commandName);
            }
            catch (Exception ex)
            {
                Logger.Error($"[ChatCommand] Error getting settings for {commandName}: {ex.Message}");
                return new CommandSettings();
            }
        }

        public static void SaveSettings()
        {
            try
            {
                string json = JsonFileManager.LoadFile("CommandSettings.json");
                var allSettings = string.IsNullOrEmpty(json)
                    ? new Dictionary<string, CommandSettings>()
                    : JsonConvert.DeserializeObject<Dictionary<string, CommandSettings>>(json)
                      ?? new Dictionary<string, CommandSettings>();

                string key = "togglestore";
                if (!allSettings.ContainsKey(key))
                    allSettings[key] = new CommandSettings();

                string newJson = JsonConvert.SerializeObject(allSettings, Formatting.Indented);
                JsonFileManager.SaveFile("CommandSettings.json", newJson);
            }
            catch (Exception ex)
            {
                Logger.Error($"[ChatCommand] Failed to save CommandSettings: {ex.Message}");
            }
        }

        private static CommandSettings LoadSettingsFromJson(string commandName)
        {
            string json = JsonFileManager.LoadFile("CommandSettings.json");
            if (string.IsNullOrEmpty(json))
                return new CommandSettings();

            try
            {
                var allSettings = JsonConvert.DeserializeObject<Dictionary<string, CommandSettings>>(json);
                if (allSettings == null)
                    return new CommandSettings();

                if (allSettings.TryGetValue(commandName, out var exact) && exact != null)
                    return exact;

                string commandNameLower = commandName.ToLowerInvariant();
                var matchingKey = allSettings.Keys.FirstOrDefault(k =>
                    k != null && k.ToLowerInvariant() == commandNameLower);

                if (matchingKey != null && allSettings.TryGetValue(matchingKey, out var matched) && matched != null)
                    return matched;
            }
            catch (Exception ex)
            {
                Logger.Error($"[ChatCommand] Error loading settings for {commandName}: {ex.Message}");
            }

            return new CommandSettings();
        }
    }

    public static class CommandUtility
    {
        public static bool AreStoreCommandsEnabled()
        {
            var settings = CAPChatInteractiveMod.Instance?.Settings?.GlobalSettings;
            return settings?.StoreCommandsEnabled ?? true;
        }

        public static string GetStoreStatusText()
        {
            return AreStoreCommandsEnabled()
                ? "Store & interaction commands are **ENABLED**"
                : "Store & interaction commands are **DISABLED** (emergency mode)";
        }
    }

    public class HelpCommand : ChatCommand
    {
        public override string Name => "help";

        public override string Execute(ChatMessageWrapper messageWrapper, string[] args)
        {
            return "Github Wiki: https://github.com/ekudram/-cap-RimworldInteractiveChatServices/wiki";
        }
    }
}
