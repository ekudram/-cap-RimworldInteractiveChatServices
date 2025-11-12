// ChatCommandProcessor.cs
// Copyright (c) Captolamia. All rights reserved.
// Licensed under the AGPLv3 License. See LICENSE file in the project root for full license information.
//
// Processes chat messages and commands from viewers.
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;
using CAP_ChatInteractive.Commands.Cooldowns;

namespace CAP_ChatInteractive
{
    public static class ChatCommandProcessor
    {
        private static readonly Dictionary<string, ChatCommand> _commands = new Dictionary<string, ChatCommand>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, DateTime> _userCooldowns = new Dictionary<string, DateTime>();

        public static event Action<ChatMessageWrapper> OnMessageProcessed;
        public static event Action<ChatMessageWrapper, string> OnCommandExecuted;


        public static void ProcessMessage(ChatMessageWrapper message)
        {
            Logger.Debug($"Processing message from {message.Username} on {message.Platform}: {message.Message}");
            try
            {
                // NEW: Check if game is ready before processing any messages
                if (!IsGameReady())
                {
                    SendPleaseWaitMessage(message);
                    return;
                }

                // Process lootbox welcome for ALL messages (commands and regular chat)
                // This ensures viewers get daily lootboxes when they first chat each day
                ProcessLootboxWelcome(message);

                // Check if it's a command
                if (IsCommand(message.Message))
                {
                    ProcessCommand(message);
                }
                else
                {
                    // Handle regular chat messages
                    ProcessChatMessage(message);
                }

                OnMessageProcessed?.Invoke(message);
            }
            catch (Exception ex)
            {
                Logger.Error($"Error processing chat message: {ex.Message}");
                // Send a generic error message to the user
                SendMessageToUser(message, "An error occurred while processing your message. Please try again.");
            }
        }

        // NEW: Check if the game is ready to process commands
        private static bool IsGameReady()
        {
            try
            {
                // Check if Current.Game is available and the game is in a playable state
                return Current.Game != null &&
                       Current.ProgramState == ProgramState.Playing &&
                       Find.CurrentMap != null;
            }
            catch
            {
                return false;
            }
        }

        // NEW: Send "please wait" message to user
        private static void SendPleaseWaitMessage(ChatMessageWrapper message)
        {
            SendMessageToUser(message, "Please wait until the game has fully started before using commands.");
        }

        private static void ProcessLootboxWelcome(ChatMessageWrapper message)
        {
            try
            {
                var lootboxComponent = Current.Game?.GetComponent<LootBoxComponent>();
                if (lootboxComponent == null) return;

                // Check if the openlootbox command is enabled before processing
                if (_commands.TryGetValue("openlootbox", out var lootboxCommand) && lootboxCommand.IsEnabled())
                {
                    // Process viewer message to check for daily lootboxes
                    lootboxComponent.ProcessViewerMessage(message.Username);
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error processing lootbox welcome: {ex.Message}");
            }
        }

        private static bool IsCommand(string message)
        {
            if (string.IsNullOrEmpty(message)) return false;

            var settings = CAPChatInteractiveMod.Instance.Settings.GlobalSettings;
            return message.StartsWith(settings.Prefix) ||
                   message.StartsWith(settings.BuyPrefix);
        }

        private static void ProcessCommand(ChatMessageWrapper message)
        {
            // Fast exit: Empty message
            if (string.IsNullOrEmpty(message.Message))
            {
                return;
            }

            // Fast exit: Empty username (shouldn't happen, but safety first)
            if (string.IsNullOrEmpty(message.Username))
            {
                Logger.Warning("Message received with null username, skipping");
                return;
            }

            var parts = message.Message.Split(' ');
            if (parts.Length == 0) return;

            var commandText = parts[0];
            var args = parts.Skip(1).ToArray();
            var globalSettings = CAPChatInteractiveMod.Instance.Settings.GlobalSettings as CAPGlobalChatSettings;

            //// Prefix check (already have this)
            //if (commandText.StartsWith(globalSettings.Prefix) ||
            //    commandText.StartsWith(globalSettings.BuyPrefix))
            //{
            //    commandText = commandText.Substring(1);
            //}
            //else
            //{
            //    return;
            //}

            commandText = commandText.ToLowerInvariant();
            Logger.Debug($"Identified command: {commandText} with args: {string.Join(", ", args)}");

            // NEW: Alias resolution - check if this is an alias and resolve to main command
            string resolvedCommandName = ResolveCommandFromAlias(commandText);
            Logger.Debug($"Resolved command name: {resolvedCommandName}");
            if (resolvedCommandName != commandText)
            {
                Logger.Debug($"Resolved alias '{commandText}' to command '{resolvedCommandName}'");
                commandText = resolvedCommandName;
            }

            // Fast exit: Unknown command
            if (!_commands.TryGetValue(commandText, out var command))
            {
                // SendMessageToUser(message, $"Unknown command: {commandText}. Type {globalSettings.Prefix}help for available commands.");
                return;
            }

            // NEW: Fast exit: Command disabled
            if (!command.IsEnabled())
            {
                SendMessageToUser(message, $"Command {commandText} is currently disabled.");
                return;
            }
            // NEW: Global Cooldown Check (before individual user checks)
            var cooldownManager = GetCooldownManager();
            var commandSettings = command.GetCommandSettings();

            if (!cooldownManager.CanUseCommand(command.Name, commandSettings, globalSettings))
            {
                SendGlobalCooldownMessage(message, command, cooldownManager);
                return;
            }

            // Get viewer (this creates if doesn't exist - no need to check)
            var viewer = Viewers.GetViewer(message);

            // Fast exit: Banned viewer
            if (viewer.IsBanned)
            {
                Logger.Debug($"Banned viewer {message.Username} attempted command: {commandText}");
                return; // Silent fail for banned users
            }

            // Fast exit: Individual Cooldown (per-user)
            if (IsOnCooldown(message.Username, command))
            {
                SendCooldownMessage(message, command);
                return;
            }

            // Fast exit: Permissions
            if (!command.CanExecute(message))
            {
                Logger.Debug($"Permission denied for {message.Username} on command {command.Name}. Required: {command.PermissionLevel}");
                SendPermissionDeniedMessage(message, command);
                return;
            }

            // EXECUTE - we've passed all checks
            try
            {
                var result = command.Execute(message, args);
                // If the command returns a result, send it to chat
                if (!string.IsNullOrEmpty(result))
                {
                    SendMessageToUser(message, result);
                }
                OnCommandExecuted?.Invoke(message, result);

                // NEW: Record successful command usage for global cooldowns
                if (!string.IsNullOrEmpty(result) && !result.StartsWith("Error"))
                {
                    cooldownManager.RecordCommandUse(command.Name);
                }

                UpdateCooldown(message.Username, command); // Existing individual cooldown
            }
            catch (Exception ex)
            {
                Logger.Error($"Error executing command {commandText}: {ex.Message}");
                SendMessageToUser(message, $"Error executing command: {ex.Message}");
            }
        }

        private static string ResolveCommandFromAlias(string commandText)
        {
            // First, check if it's a direct command match
            if (_commands.ContainsKey(commandText))
                return commandText;

            // Then check aliases
            foreach (var command in _commands.Values.Distinct())
            {
                Logger.Debug($"Checking alias for command '{command.Name}': Alias='{command.Alias}'");
                if (!string.IsNullOrEmpty(command.Alias) && command.Alias == commandText)
                {
                    return command.Name;
                }
            }

            return commandText;
        }

        private static void ProcessChatMessage(ChatMessageWrapper message)
        {
            // TODO: Handle regular chat messages (for chat-to-game features)  (this is done in the chat interface now)
            // This could include voting systems, chat interactions, etc.
        }

        private static bool IsOnCooldown(string username, ChatCommand command)
        {
            if (command.CooldownSeconds <= 0) return false;

            var key = $"{username}_{command.Name}";
            bool onCooldown = _userCooldowns.TryGetValue(key, out var lastUsed) &&
                   DateTime.Now - lastUsed < TimeSpan.FromSeconds(command.CooldownSeconds);

            if (onCooldown)
            {
                var remaining = TimeSpan.FromSeconds(command.CooldownSeconds) - (DateTime.Now - lastUsed);
            }
            return onCooldown;
        }

        private static GlobalCooldownManager GetCooldownManager()
        {
            var manager = Current.Game.GetComponent<GlobalCooldownManager>();
            if (manager == null)
            {
                manager = new GlobalCooldownManager(Current.Game);
                Current.Game.components.Add(manager);
            }
            return manager;
        }

        private static void UpdateCooldown(string username, ChatCommand command)
        {
            if (command.CooldownSeconds <= 0) return;

            var key = $"{username}_{command.Name}";
            _userCooldowns[key] = DateTime.Now;
        }

        private static void SendCooldownMessage(ChatMessageWrapper message, ChatCommand command)
        {
            var key = $"{message.Username}_{command.Name}";
            var lastUsed = _userCooldowns[key];
            var remaining = TimeSpan.FromSeconds(command.CooldownSeconds) - (DateTime.Now - lastUsed);

            SendMessageToUser(message, $"Command is on cooldown. Try again in {remaining.Seconds} seconds.");
        }

        private static void SendGlobalCooldownMessage(ChatMessageWrapper message, ChatCommand command, GlobalCooldownManager cooldownManager)
        {
            var globalSettings = CAPChatInteractiveMod.Instance.Settings.GlobalSettings as CAPGlobalChatSettings;
            string eventType = cooldownManager.GetEventTypeForCommand(command.Name);

            string cooldownMessage = eventType switch
            {
                "good" => $"Global good event limit reached ({globalSettings.MaxGoodEvents} per {globalSettings.EventCooldownDays} days)",
                "bad" => $"Global bad event limit reached ({globalSettings.MaxBadEvents} per {globalSettings.EventCooldownDays} days)",
                "neutral" => $"Global event limit reached ({globalSettings.MaxNeutralEvents} per {globalSettings.EventCooldownDays} days)",
                _ => $"Command {command.Name} is currently on global cooldown"
            };

            SendMessageToUser(message, cooldownMessage);
        }

        private static void SendPermissionDeniedMessage(ChatMessageWrapper message, ChatCommand command)
        {
            var settings = CAPChatInteractiveMod.Instance.Settings.GlobalSettings;
            SendMessageToUser(message, $"You don't have permission to use {settings.Prefix}{command.Name}. Required: {command.PermissionLevel}");
        }

        public static void SendMessageToUser(ChatMessageWrapper message, string text)
        {
            try
            {
                var mod = CAPChatInteractiveMod.Instance;
                if (mod == null) return;

                var service = mod.GetChatService(message.Platform);

                if (service is TwitchService twitchService)
                {
                    twitchService.SendMessage($"@{message.Username} {text}");
                }
                else if (service is YouTubeChatService youtubeService)
                {
                    // YouTube has API limitations, use fallback
                    if (youtubeService.CanSendMessages)
                    {
                        youtubeService.SendMessage(text);
                    }
                    else
                    {
                        // Fallback to in-game notification for YouTube
                        Messages.Message($"[YouTube] @{message.Username} {text}", MessageTypeDefOf.NeutralEvent);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error sending message to user: {ex.Message}");
            }
        }

        public static void SendMessageToUsername(string username, string text)
        {
            try
            {
                var viewer = Viewers.GetViewer(username);
                if (viewer == null) return;

                var mod = CAPChatInteractiveMod.Instance;
                if (mod == null) return;

                // Determine which platform this user is from
                string platform = DetermineUserPlatform(viewer);

                if (platform == "twitch" && mod.TwitchService?.IsConnected == true)
                {
                    mod.TwitchService.SendMessage($"@{username} {text}");
                }
                else if (platform == "youtube" && mod.YouTubeService?.IsConnected == true)
                {
                    // YouTube has API limitations
                    if (mod.YouTubeService.CanSendMessages)
                    {
                        mod.YouTubeService.SendMessage($"@{username} {text}");
                    }
                    else
                    {
                        // Fallback to in-game notification for YouTube
                        Messages.Message($"[YouTube] @{username} {text}", MessageTypeDefOf.NeutralEvent);
                    }
                }
                else
                {
                    // Fallback - user platform unknown or service not connected
                    Messages.Message($"[Chat] @{username} {text}", MessageTypeDefOf.NeutralEvent);
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error sending message to username {username}: {ex.Message}");
            }
        }

        private static string DetermineUserPlatform(Viewer viewer)
        {
            // Check which platform IDs the user has
            if (viewer.PlatformUserIds.ContainsKey("twitch"))
                return "twitch";
            if (viewer.PlatformUserIds.ContainsKey("youtube"))
                return "youtube";

            // Default to Twitch if we can't determine (most common case)
            return "twitch";
        }

        public static void RegisterCommand(ChatCommand command)
        {
            _commands[command.Name] = command;
            //Logger.Debug($"Registered command: '{command.Name}'");

            // Register the single alias if it exists
            if (!string.IsNullOrEmpty(command.Alias))
            {
                _commands[command.Alias] = command;
                //Logger.Debug($"  -> Registered alias: '{command.Alias}'");
            }

            // Also log the CommandAlias value directly
            var settings = command.GetCommandSettings();
            // Logger.Debug($"  -> CommandAlias value: '{settings.CommandAlias}'");
        }

        public static IEnumerable<ChatCommand> GetAvailableCommands(ChatMessageWrapper user)
        {
            return _commands.Values.Distinct().Where(cmd => cmd.CanExecute(user));
        }

        // Helper method to check if a specific prefix is used
        public static bool UsesPrefix(string message, string prefix)
        {
            return !string.IsNullOrEmpty(message) && message.StartsWith(prefix);
        }

        // Helper method to get the appropriate prefix for a command type
        public static string GetCommandPrefix(bool isBuyCommand = false)
        {
            var settings = CAPChatInteractiveMod.Instance.Settings.GlobalSettings;
            return isBuyCommand ? settings.BuyPrefix : settings.Prefix;
        }
    }
}