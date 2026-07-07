// ChatCommandDef.cs
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
// RimWorld Def for chat commands that can be loaded from XML
using System;
using Verse;
using System.Collections.Generic;

namespace CAP_ChatInteractive
{
    /// <summary>
    /// Defines a single custom/extra UI element for a command's CustomData section.
    /// Declared in Commands.xml inside &lt;CustomData&gt; ... &lt;/CustomData&gt; for a ChatCommandDef (order matters).
    /// Supported types: HeaderLabel, Label, CheckBox, LabelTextBox, NumericTextBox, Gap, Button.
    /// Values for inputs are stored in CommandSettings.CustomData (JSON) and rendered in Command Editor.
    /// Gap is a pure layout spacer (float pixels from defaultValue); it stores nothing.
    /// Button is an action item (label is the button text, name is used to identify it for hooks). It stores nothing.
    /// </summary>
    [Serializable]
    public class CommandCustomSetting
    {
        /// <summary>The type: "HeaderLabel", "Label", "CheckBox", "LabelTextBox", "NumericTextBox", "Gap", "Button".</summary>
        public string type = "string";

        /// <summary>Key/name for the value (for CheckBox, LabelTextBox, NumericTextBox). For Button this is the identifier passed to OnCustomDataButtonClicked. Not used for HeaderLabel, Label or Gap.</summary>
        public string name = "";

        /// <summary>UI label or the text content for Label/HeaderLabel type.</summary>
        public string label = "";

        /// <summary>String form of default value (parsed by type). E.g. "false", "500", "text here". For Gap this is the float gap amount in pixels.</summary>
        public string defaultValue = "";

        /// <summary>Tooltip / description (for input types).</summary>
        public string description = "";

        /// <summary>For NumericTextBox: min value.</summary>
        public float min = float.MinValue;

        /// <summary>For NumericTextBox: max value.</summary>
        public float max = float.MaxValue;
    }

    /// <summary>
    /// RimWorld Def for chat commands that can be loaded from XML
    /// This bridges the Def system with your existing ChatCommand processor
    /// </summary>
    public class ChatCommandDef : Def
    {
        /// <summary>The command text that triggers this command</summary>
        public string commandText = null;

        /// <summary>Whether this command is currently enabled</summary>
        public bool enabled = true;

        /// <summary>The type of command handler that processes this command</summary>
        public Type commandClass = typeof(ChatCommand);

        /// <summary>Whether this command requires mod privileges</summary>
        public bool requiresMod = false;

        /// <summary>Whether this command requires broadcaster privileges</summary>
        public bool requiresBroadcaster = false;

        /// <summary>Description of what the command does</summary>
        public string commandDescription = ""; // Changed from 'description' to avoid conflict

        /// <summary>Permission level required (everyone, subscriber, vip, moderator, broadcaster)</summary>
        public string permissionLevel = "everyone";

        /// <summary>Cooldown in seconds between uses</summary>
        public int cooldownSeconds = 1;

        /// <summary>
        /// is this an event command (purchased via chat interaction)
        /// </summary>
        public bool isEventCommand = false;  // NEW: Identifies event commands

        /// <summary>
        /// Cooldown, works oppisite false = uses standard cooldowns, true uses command cooldown
        /// </summary>
        public bool useCommandCooldown = false;
        /// think of it like this: public bool useCommandCooldown = false;

        /// <summary>
        /// The &lt;CustomData&gt; definition for this command (list of UI elements in order).
        /// Parsed from the &lt;CustomData&gt;...&lt;/CustomData&gt; section in XML.
        /// Enables dynamic per-command settings (HeaderLabel, Label, CheckBox, LabelTextBox, NumericTextBox, Gap, Button) in the editor.
        /// Values for interactive items stored in CommandSettings.CustomData.
        /// Buttons trigger OnCustomDataButtonClicked on the command (plus a built-in CustomData reset).
        /// Backwards compatible (empty = no extra UI).
        /// </summary>
        public List<CommandCustomSetting> CustomData = new List<CommandCustomSetting>();

        /// <summary>
        /// Gets the display label for this command, using defName if label is empty
        /// </summary>
        public string DisplayLabel
        {
            get
            {
                if (!string.IsNullOrEmpty(base.label))
                {
                    return base.label;
                }
                return base.defName;
            }
        }

        /// <summary>
        /// Registers this command with the ChatCommandProcessor
        /// </summary>
        public void RegisterCommand()
        {
            // FIX: Remove Def enabled check - register ALL commands so JSON settings control everything
            // if (!enabled) return;

            try
            {
                if (commandClass == null)
                {
                    Logger.Warning($"Command class type is null for command: {commandText}");
                    return;
                }

                // Create instance and register with processor
                if (Activator.CreateInstance(commandClass) is ChatCommand commandInstance)
                {
                    var wrappedCommand = new DefBasedChatCommand(this, commandInstance);
                    ChatCommandProcessor.RegisterCommand(wrappedCommand);
                    // Logger.Debug($"Registered command: {commandText}");
                }
                else
                {
                    Logger.Error($"Failed to create command instance for: {commandClass.FullName}");
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error registering command {commandText}: {ex}");
            }
        }

        private void EnsureSettingsAlignment(ChatCommandDef def, ChatCommand command)
        {
            try
            {
                var settings = CommandSettingsManager.GetSettings(def.defName);
                if (settings != null)
                {
                    // If we have settings stored by defName, also make them available by command name
                    var commandNameSettings = CommandSettingsManager.GetSettings(command.Name);

                    // Copy alias from defName settings to command name settings if they differ
                    if (!string.IsNullOrEmpty(settings.CommandAlias) &&
                        string.IsNullOrEmpty(commandNameSettings.CommandAlias))
                    {
                        commandNameSettings.CommandAlias = settings.CommandAlias;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error ensuring settings alignment for {def.defName}: {ex}");
            }
        }
    }

    /// <summary>
    /// Wrapper that adapts a ChatCommand instance to use Def-based properties
    /// </summary>
    public class DefBasedChatCommand : ChatCommand
    {
        private readonly ChatCommandDef _def;
        private readonly ChatCommand _wrappedCommand;

        public DefBasedChatCommand(ChatCommandDef def, ChatCommand wrappedCommand)
        {
            _def = def;
            _wrappedCommand = wrappedCommand;
        }

        public override string Name => _def.commandText;

        public override string Alias => _wrappedCommand.Alias;

        public override string Description => !string.IsNullOrEmpty(_def.commandDescription) ? _def.commandDescription : _wrappedCommand.Description;

        // FIX: Only use JSON settings, never the Def
        public override string PermissionLevel
        {
            get
            {
                // Allow per-command override from settings (for subscriber-only / paid access etc.)
                // Fall back to the Def default if not overridden.
                var s = GetCommandSettings();
                if (s != null && !string.IsNullOrEmpty(s.PermissionLevel))
                    return s.PermissionLevel;

                return _def.permissionLevel;
            }
        }

        public override int CooldownSeconds => GetCommandSettings()?.CooldownSeconds ?? 0;

        public override string Execute(ChatMessageWrapper user, string[] args)
        {
            return _wrappedCommand.Execute(user, args);
        }

        public override bool CanExecute(ChatMessageWrapper message)
        {
            var viewer = Viewers.GetViewer(message);
            if (viewer == null) return false;
            return viewer.HasPermission(PermissionLevel);
        }

        public override void OnCustomDataButtonClicked(string buttonName, CommandSettings settings)
        {
            _wrappedCommand.OnCustomDataButtonClicked(buttonName, settings);
        }
    }
}