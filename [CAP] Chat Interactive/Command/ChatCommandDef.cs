// ChatCommandDef.cs
// Copyright (c) Captolamia
// This file is part of CAP Chat Interactive (RICS).
// Licensed under the GNU Affero General Public License v3.0 or later.
// See LICENSE.txt in the project root for full license text.
//
// RimWorld Def for chat commands loaded from Commands.xml (modder-facing fields documented below).
using System;
using System.Collections.Generic;
using Verse;

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
    /// RimWorld Def for chat commands that can be loaded from XML.
    /// Bridges the Def system with <see cref="ChatCommandProcessor"/>.
    /// </summary>
    public class ChatCommandDef : Def
    {
        private static readonly HashSet<string> KnownCustomDataTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "HeaderLabel", "Label", "CheckBox", "LabelTextBox", "NumericTextBox", "Gap", "Button"
        };

        /// <summary>The command text that triggers this command (chat trigger, usually lowercase).</summary>
        public string commandText = null;

        /// <summary>Whether this command is currently enabled (Def default; runtime JSON settings override).</summary>
        public bool enabled = true;

        /// <summary>The type of command handler that processes this command (must inherit ChatCommand).</summary>
        public Type commandClass = typeof(ChatCommand);

        /// <summary>Whether this command requires mod privileges (legacy flag; prefer permissionLevel).</summary>
        public bool requiresMod = false;

        /// <summary>Whether this command requires broadcaster privileges (legacy flag; prefer permissionLevel).</summary>
        public bool requiresBroadcaster = false;

        /// <summary>Description of what the command does (shown in editor / help; not Def.label).</summary>
        public string commandDescription = "";

        /// <summary>Permission level required (everyone, subscriber, vip, moderator, broadcaster).</summary>
        public string permissionLevel = "everyone";

        /// <summary>
        /// When true, this command is omitted from public pricelist / docs exports (e.g. bot-only commands).
        /// Refreshed into CommandSettings.ExcludeFromPricelist from Def XML on load/save.
        /// </summary>
        public bool excludeFromPricelist = false;

        /// <summary>Cooldown in seconds between uses (default; overridable via CommandSettings JSON).</summary>
        public int cooldownSeconds = 1;

        /// <summary>True if this is an event-style command (purchased / triggered as a game event via chat).</summary>
        public bool isEventCommand = false;

        /// <summary>
        /// When false (default), uses standard global/event cooldowns.
        /// When true, uses this command's own cooldownSeconds setting.
        /// </summary>
        public bool useCommandCooldown = false;

        /// <summary>
        /// The &lt;CustomData&gt; definition for this command (list of UI elements in order).
        /// Parsed from the &lt;CustomData&gt;...&lt;/CustomData&gt; section in XML.
        /// Enables dynamic per-command settings (HeaderLabel, Label, CheckBox, LabelTextBox, NumericTextBox, Gap, Button) in the editor.
        /// Values for interactive items stored in CommandSettings.CustomData.
        /// Buttons trigger OnCustomDataButtonClicked on the command (plus a built-in CustomData reset).
        /// Backwards compatible (empty = no extra UI).
        /// </summary>
        public List<CommandCustomSetting> CustomData = new List<CommandCustomSetting>();

        /// <summary>Display label for this command (Def label, else defName).</summary>
        public string DisplayLabel =>
            !string.IsNullOrEmpty(label) ? label : defName;

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string err in base.ConfigErrors())
                yield return err;

            if (string.IsNullOrWhiteSpace(commandText))
                yield return "commandText is null or empty (chat trigger required)";

            if (commandClass == null)
                yield return "commandClass is null";
            else if (!typeof(ChatCommand).IsAssignableFrom(commandClass))
                yield return $"commandClass {commandClass.FullName} must inherit ChatCommand";
            else if (commandClass.IsAbstract)
                yield return $"commandClass {commandClass.FullName} is abstract and cannot be instantiated";

            if (cooldownSeconds < 0)
                yield return "cooldownSeconds cannot be negative";

            if (CustomData == null)
                yield break;

            for (int i = 0; i < CustomData.Count; i++)
            {
                var item = CustomData[i];
                if (item == null)
                {
                    yield return $"CustomData[{i}] is null";
                    continue;
                }

                if (string.IsNullOrWhiteSpace(item.type))
                {
                    yield return $"CustomData[{i}] has empty type";
                    continue;
                }

                if (!KnownCustomDataTypes.Contains(item.type))
                {
                    yield return
                        $"CustomData[{i}] unknown type '{item.type}' " +
                        "(expected HeaderLabel, Label, CheckBox, LabelTextBox, NumericTextBox, Gap, Button)";
                }

                string t = item.type.Trim();
                bool needsName =
                    t.Equals("CheckBox", StringComparison.OrdinalIgnoreCase) ||
                    t.Equals("LabelTextBox", StringComparison.OrdinalIgnoreCase) ||
                    t.Equals("NumericTextBox", StringComparison.OrdinalIgnoreCase) ||
                    t.Equals("Button", StringComparison.OrdinalIgnoreCase);

                if (needsName && string.IsNullOrWhiteSpace(item.name))
                    yield return $"CustomData[{i}] type '{item.type}' requires a non-empty name";

                if (t.Equals("NumericTextBox", StringComparison.OrdinalIgnoreCase) && item.min > item.max)
                    yield return $"CustomData[{i}] NumericTextBox min ({item.min}) > max ({item.max})";
            }
        }

        /// <summary>Registers this command with the ChatCommandProcessor (JSON settings control enable at runtime).</summary>
        public void RegisterCommand()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(commandText))
                {
                    Logger.Warning($"[ChatCommandDef] Skipping register: empty commandText on def '{defName}'");
                    return;
                }

                if (commandClass == null)
                {
                    Logger.Warning($"[ChatCommandDef] commandClass is null for '{commandText}' ({defName})");
                    return;
                }

                if (!typeof(ChatCommand).IsAssignableFrom(commandClass))
                {
                    Logger.Error(
                        $"[ChatCommandDef] commandClass {commandClass.FullName} must inherit ChatCommand " +
                        $"(command '{commandText}')");
                    return;
                }

                if (commandClass.IsAbstract)
                {
                    Logger.Error(
                        $"[ChatCommandDef] Cannot instantiate abstract commandClass {commandClass.FullName} " +
                        $"(command '{commandText}')");
                    return;
                }

                if (!(Activator.CreateInstance(commandClass) is ChatCommand commandInstance))
                {
                    Logger.Error($"[ChatCommandDef] Activator failed for {commandClass.FullName} ('{commandText}')");
                    return;
                }

                ChatCommandProcessor.RegisterCommand(new DefBasedChatCommand(this, commandInstance));
            }
            catch (Exception ex)
            {
                Logger.Error($"[ChatCommandDef] Error registering '{commandText}' ({defName}): {ex}");
            }
        }
    }

    /// <summary>
    /// Wrapper that adapts a ChatCommand instance to use Def-based properties
    /// (command text, description, default permission) while still honoring JSON overrides.
    /// </summary>
    public class DefBasedChatCommand : ChatCommand
    {
        private readonly ChatCommandDef _def;
        private readonly ChatCommand _wrappedCommand;

        public DefBasedChatCommand(ChatCommandDef def, ChatCommand wrappedCommand)
        {
            _def = def ?? throw new ArgumentNullException(nameof(def));
            _wrappedCommand = wrappedCommand ?? throw new ArgumentNullException(nameof(wrappedCommand));
        }

        public override string Name => _def.commandText;

        public override string Alias => _wrappedCommand.Alias;

        public override string Description =>
            !string.IsNullOrEmpty(_def.commandDescription)
                ? _def.commandDescription
                : _wrappedCommand.Description;

        /// <summary>JSON CommandSettings permission if set; otherwise Def permissionLevel.</summary>
        public override string PermissionLevel
        {
            get
            {
                var s = GetCommandSettings();
                if (s != null && !string.IsNullOrEmpty(s.PermissionLevel))
                    return s.PermissionLevel;

                return _def.permissionLevel ?? "everyone";
            }
        }

        public override int CooldownSeconds => GetCommandSettings()?.CooldownSeconds ?? 0;

        public override string Execute(ChatMessageWrapper user, string[] args)
        {
            try
            {
                return _wrappedCommand.Execute(user, args ?? Array.Empty<string>());
            }
            catch (Exception ex)
            {
                Logger.Error($"[ChatCommandDef] Execute failed for '{Name}': {ex}");
                return "Command error. Check the game log.";
            }
        }

        public override bool CanExecute(ChatMessageWrapper message)
        {
            if (message == null)
                return false;

            var viewer = Viewers.GetViewer(message);
            if (viewer == null)
                return false;

            return viewer.HasPermission(PermissionLevel);
        }

        public override void OnCustomDataButtonClicked(string buttonName, CommandSettings settings)
        {
            try
            {
                _wrappedCommand.OnCustomDataButtonClicked(buttonName, settings);
            }
            catch (Exception ex)
            {
                Logger.Error($"[ChatCommandDef] OnCustomDataButtonClicked '{buttonName}' on '{Name}': {ex.Message}");
            }
        }
    }
}
