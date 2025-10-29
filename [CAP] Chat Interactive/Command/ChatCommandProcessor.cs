// ChatCommandProcessor.cs
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
            }
        }

        private static bool IsCommand(string message)
        {
            Logger.Debug($"Checking if message is command: {message}");
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
                Logger.Debug("Empty message received, skipping");
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

            Logger.Debug($"Raw command text: '{commandText}'");  // Debug log

            // Prefix check (already have this)
            var globalSettings = CAPChatInteractiveMod.Instance.Settings.GlobalSettings as CAPGlobalChatSettings;
            if (commandText.StartsWith(globalSettings.Prefix) ||
                commandText.StartsWith(globalSettings.BuyPrefix))
            {
                commandText = commandText.Substring(1);
            }
            else
            {
                Logger.Debug($"Message '{message.Message}' doesn't match prefixes, skipping");
                return;
            }

            Logger.Debug($"Raw command text: '{commandText}'");  // Debug log after prefix removal?

            commandText = commandText.ToLowerInvariant();
            Logger.Debug($"Available commands in dictionary: {string.Join(", ", _commands.Keys)}");

            // Fast exit: Unknown command
            if (!_commands.TryGetValue(commandText, out var command))
            {
                Logger.Debug($"Unknown command: {commandText}");
                SendMessageToUser(message, $"Unknown command: {commandText}. Type {globalSettings.Prefix}help for available commands.");
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
            var viewer = Viewers.GetViewer(message.Username);

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
                Logger.Debug($"Cooldown check for {username}_{command.Name}: {onCooldown} (Cooldown: {command.CooldownSeconds}s)");
                Logger.Debug($"Time remaining: {remaining.TotalSeconds:F0}s");
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

        public static void RegisterCommand(ChatCommand command)
        {
            _commands[command.Name] = command;
            // Also register aliases
            foreach (var alias in command.Aliases)
            {
                _commands[alias] = command;
            }
        }

        private static void RegisterDefaultCommands()
        {
            RegisterCommand(new HelpCommand());
            RegisterCommand(new PointsCommand());
            // More commands will be added from XML Defs
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