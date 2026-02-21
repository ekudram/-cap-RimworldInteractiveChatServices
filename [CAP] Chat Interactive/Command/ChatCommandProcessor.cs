// ChatCommandProcessor.cs
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
// Processes chat messages and commands from viewers.
using CAP_ChatInteractive.Commands.Cooldowns;
using CAP_ChatInteractive.Commands.ModCommands;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Verse;

namespace CAP_ChatInteractive
{
    public static class ChatCommandProcessor
    {
        public static readonly Dictionary<string, ChatCommand> _commands = new Dictionary<string, ChatCommand>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, DateTime> _userCooldowns = new Dictionary<string, DateTime>();

        public static event Action<ChatMessageWrapper> OnMessageProcessed;
        public static event Action<ChatMessageWrapper, string> OnCommandExecuted;

        // ========== MESSAGE PROCESSING ==========
        /// <summary>
        /// ALL chat messages funnel through here
        /// From all platforms
        /// </summary>
        /// <param name="message"></param>
        public static void ProcessMessage(ChatMessageWrapper message)
        {
            Logger.Debug($"Processing message from {message.Username} on {message.Platform}: {message.Message}");
            try
            {
                // add validate viewer name here
                // In ChatCommandProcessor.cs / ProcessMessage
                var viewer = Viewers.GetViewer(message);
                if (viewer != null)
                {
                    bool nameChanged = viewer.UpdateDisplayName(message.DisplayName);

                    // Optional: you could send a fun message only on actual change
                    if (nameChanged && Rand.Chance(0.4f))
                    {
                        ChatCommandProcessor.SendMessageToUsername(
                            message.Username,
                            $"✨ {viewer.DisplayName} just got a fresh new name! Welcome back!");
                    }
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
                // SendMessageToUser(message, "An error occurred while processing your message. Please try again.");
            }
        }

        // Do we check this in other places too?
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

        public static bool IsCommand(string message)
        {
            if (string.IsNullOrEmpty(message)) return false;

            var settings = CAPChatInteractiveMod.Instance.Settings.GlobalSettings;

            string trimmed = message.TrimStart();

            return message.StartsWith(settings.Prefix) ||
                   message.StartsWith(settings.BuyPrefix) ||
                   message.StartsWith("$");

        }

        private static void SendPleaseWaitMessage(ChatMessageWrapper message)
        {
            // SendMessageToUser(message, "Please wait until the game has fully started before using commands.");
            return;
        }

        private static void ProcessLootboxWelcome(ChatMessageWrapper message)
        {
            try
            {
                var lootboxComponent = Current.Game?.GetComponent<LootBoxComponent>();
                if (lootboxComponent == null) return;

                // Check if the openlootbox command is enabled before processing
                if (CommandSettingsManager.GetSettings("openlootbox").Enabled)
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
        


        // ========== COMMAND PROCESSING ==========
        private static void ProcessCommand(ChatMessageWrapper message)
        {
            if (!IsGameReady())
            {
                SendPleaseWaitMessage(message);
                return;
            }

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
            var globalSettings = CAPChatInteractiveMod.Instance.Settings.GlobalSettings;

            string originalCommandText = commandText; // preserve for logging if needed

            // Strip prefix if present (normal path)
            if (commandText.StartsWith(globalSettings.Prefix) ||
                commandText.StartsWith(globalSettings.BuyPrefix))
            {
                commandText = commandText.Substring(1);
            }

            commandText = commandText.ToLowerInvariant();

            // NEW: Alias resolution (your existing code)
            string resolvedCommandName = ResolveCommandFromAlias(commandText);
            if (resolvedCommandName != commandText)
            {
                commandText = resolvedCommandName;
            }

            // Normal command lookup
            if (_commands.TryGetValue(commandText, out var command))
            {
                // → proceed with normal cooldown / permission / execute flow
                // (rest of your function continues here unchanged)
            }
            else
            {
                // Command not found → check for legacy $ buy fallback
                Logger.Debug($"Command '{commandText}' not found. Checking for legacy $ buy fallback.");
                string trimmed = message.Message.TrimStart();
                if (trimmed.StartsWith("$"))
                {
                    // It's a $ message and no normal command matched → treat as buy
                    commandText = "buy";

                    // Extract the actual buy arguments (skip the $)
                    string rest = trimmed.Substring(1).Trim();
                    args = string.IsNullOrWhiteSpace(rest)
                        ? Array.Empty<string>()
                        : rest.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);  
                    Logger.Debug($"Legacy $ command detected. Interpreting as 'buy' with args: {string.Join(", ", args)}");
                    // Optional: mild logging so you can see when legacy path is hit
                    // Logger.Debug($"$ buy used by {message.Username}: {message.Message}");

                    // Now look up the "buy" command (should exist)
                    if (_commands.TryGetValue("buy", out command))
                    {
                        Logger.Debug($"'buy' command found for legacy $ command. Proceeding with execution.");
                        // → continue to cooldown/permission/execute as normal
                        // The buy command will validate args the same way it does for !buy
                    }
                    else
                    {
                        Logger.Error($"'buy' command not found in command dictionary. This should never happen if the command is properly registered.");
                        // Very unlikely, but safety
                        return;
                    }
                }
                else
                {
                    Logger.Debug($"No command found for '{commandText}' and message does not start with '$'. Ignoring message.");
                    // Truly unknown, even after $ check
                    // You can optionally send "Unknown command" here if you want
                    return;
                }
            }

            // Get viewer (this creates if doesn't exist - no need to check)
            var viewer = Viewers.GetViewer(message);

            // Fast exit: Banned viewer
            if (viewer.IsBanned)
            {
                Logger.Debug($"Banned viewer {message.Username} attempted command: {commandText}");
                return; // Silent fail for banned users
            }

            // Fast exit: Command disabled
            if (!CommandSettingsManager.GetSettings(commandText).Enabled)
            {
                SendMessageToUser(message, $"Command {commandText} is currently disabled.");
                return;
            }

            // NEW: Global Cooldown Check (before individual user checks)
            var cooldownManager = GetCooldownManager();
            var commandSettings = command.GetCommandSettings();

            // 1. Individual user cooldown (cheapest check first)
            if (IsOnCooldown(message.Username, command))
            {
                SendCooldownMessage(message, command);
                return;
            }

            // 2. Permissions check
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
            try
            {
                // Check if this is a Twitch channel points reward redemption
                if (message.Platform == "Twitch" && !string.IsNullOrEmpty(message.CustomRewardId))
                {
                    ProcessChannelPointsReward(message);
                }

                // TODO: Handle other regular chat messages (for chat-to-game features)
                // This could include voting systems, chat interactions, etc.
            }
            catch (Exception ex)
            {
                Logger.Error($"Error processing chat message: {ex.Message}");
            }
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

        public static GlobalCooldownManager GetCooldownManager()
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

        private static void SendPermissionDeniedMessage(ChatMessageWrapper message, ChatCommand command)
        {
            var settings = CAPChatInteractiveMod.Instance.Settings.GlobalSettings;
            SendMessageToUser(message, $"You don't have permission to use {settings.Prefix}{command.Name}. Required: {command.PermissionLevel}");
        }

        public static void SendMessageToUser(ChatMessageWrapper message, string text)
        {
            try
            {
                // Check if text is empty or whitespace
                if (string.IsNullOrWhiteSpace(text))
                {
                    Logger.Warning("Attempted to send empty message to user");
                    return;
                }

                // Remove any markup tags like <color>, <b>, <i>, etc.
                string cleanText = RemoveMarkupTags(text);

                // Check again if the cleaned text is empty
                if (string.IsNullOrWhiteSpace(cleanText))
                {
                    Logger.Warning("Message contained only markup tags, nothing to send");
                    return;
                }

                var mod = CAPChatInteractiveMod.Instance;
                if (mod == null) return;

                var service = mod.GetChatService(message.Platform);

                if (service is TwitchService twitchService)
                {
                    twitchService.SendMessage($"{message.Username} {cleanText}");
                }
                else if (service is YouTubeChatService youtubeService)
                {
                    // YouTube has API limitations, use fallback
                    if (youtubeService.CanSendMessages)
                    {
                        youtubeService.SendMessage(cleanText);
                    }
                    else
                    {
                        // Fallback to in-game notification for YouTube
                        Messages.Message($"[YouTube] @{message.Username} {cleanText}", MessageTypeDefOf.NeutralEvent);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error sending message to user: {ex.Message}");
            }
        }

        /// <summary>
        /// Removes common markup tags from text for chat compatibility
        /// </summary>
        public static string RemoveMarkupTags(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            // Remove common XML-style tags: <color=...>, </color>, <b>, </b>, <i>, </i>, etc.
            string cleaned = Regex.Replace(text, @"<[^>]+>", string.Empty);

            return cleaned;
        }

        public static void SendMessageToUsername(string username, string text)
        {
            try
            {
                // Check if text is empty or whitespace
                if (string.IsNullOrWhiteSpace(text))
                {
                    Logger.Warning($"Attempted to send empty message to user {username}");
                    return;
                }

                // Remove any markup tags like <color>, <b>, <i>, etc.
                string cleanText = RemoveMarkupTags(text);

                // Check again if the cleaned text is empty
                if (string.IsNullOrWhiteSpace(cleanText))
                {
                    Logger.Warning($"Message to {username} contained only markup tags, nothing to send");
                    return;
                }

                var viewer = Viewers.GetViewer(username);
                if (viewer == null) return;

                var mod = CAPChatInteractiveMod.Instance;
                if (mod == null) return;

                // Determine which platform this user is from
                string platform = DetermineUserPlatform(viewer);

                if (platform == "twitch" && mod.TwitchService?.IsConnected == true)
                {
                    mod.TwitchService.SendMessage($"@{username} {cleanText}");
                }
                else if (platform == "youtube" && mod.YouTubeService?.IsConnected == true)
                {
                    // YouTube has API limitations
                    if (mod.YouTubeService.CanSendMessages)
                    {
                        mod.YouTubeService.SendMessage($"@{username} {cleanText}");
                    }
                    else
                    {
                        // Fallback to in-game notification for YouTube
                        Messages.Message($"[YouTube] @{username} {cleanText}", MessageTypeDefOf.NeutralEvent);
                    }
                }
                else
                {
                    // Fallback - user platform unknown or service not connected
                    Messages.Message($"[Chat] @{username} {cleanText}", MessageTypeDefOf.NeutralEvent);
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

        private static void ProcessChannelPointsReward(ChatMessageWrapper message)
        {
            try
            {
                var globalSettings = CAPChatInteractiveMod.Instance.Settings.GlobalSettings as CAPGlobalChatSettings;

                // Fast exit if channel points are disabled or RewardSettings is null
                if (!globalSettings.ChannelPointsEnabled)
                {
                    Logger.Debug("Channel points processing disabled in settings");
                    return;
                }

                if (globalSettings.RewardSettings == null)
                {
                    Logger.Debug("RewardSettings list is null - channel points processing skipped");
                    return;
                }
                string rewardId = message.CustomRewardId;

                // Find the reward configuration
                var reward = globalSettings.RewardSettings.FirstOrDefault(r => r.RewardUUID == rewardId && r.Enabled);

                if (reward != null)
                {
                    // Award coins to the user
                    if (int.TryParse(reward.CoinsToAward, out int coins))
                    {
                        var viewer = Viewers.GetViewer(message);
                        viewer.Coins += coins;

                        if (globalSettings.ShowChannelPointsDebugMessages)
                        {
                            Logger.Debug($"Awarded {coins} coins to {message.Username} for reward: {reward.RewardName}");
                        }

                        // Optional: Send confirmation message
                        SendMessageToUser(message, $"Thank you for redeeming '{reward.RewardName}'! You received {coins} coins.");
                    }
                }
                else
                {
                    // Handle unconfigured reward with automatic capture
                    if (globalSettings.ShowChannelPointsDebugMessages)
                    {
                        Logger.Debug($"Detected a custom reward that wasn't configured: {rewardId}");
                    }

                    var autoReward = globalSettings.RewardSettings.FirstOrDefault(r => r.AutomaticallyCaptureUUID && r.Enabled);
                    if (autoReward != null)
                    {
                        if (globalSettings.ShowChannelPointsDebugMessages)
                        {
                            Logger.Debug($"A reward with Automatic UUID capture enabled was found. Configuring this reward to use {rewardId}.");
                        }

                        autoReward.AutomaticallyCaptureUUID = false;
                        autoReward.RewardUUID = rewardId;

                        // Optional: Notify about auto-configuration
                        SendMessageToUser(message, $"Automatically configured '{autoReward.RewardName}' with this reward ID.");
                    }
                    else
                    {
                        if (globalSettings.ShowChannelPointsDebugMessages)
                        {
                            Logger.Debug("If this is the reward you would like to use, add this UUID to the mod settings.");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error processing channel points reward: {ex.Message}");
            }
        }

        private static bool IsStoreCommandDisabled(ChatMessageWrapper message, string commandText)
        {
            // Early exit if global store is enabled → nothing to block
            var globalSettings = CAPChatInteractiveMod.Instance.Settings.GlobalSettings;
            if (globalSettings?.StoreCommandsEnabled ?? true)
            {
                return false; // not disabled
            }

            // Only block if it's one of our managed store commands
            return ToggleStore.StoreCommands.Contains(commandText);
        }

        private static string GetStoreDisabledMessage()
        {
            return "Store and interaction commands are currently **DISABLED** (streamer emergency mode). Try again later!";
        }
    }
}