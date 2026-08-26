// ChatCommandProcessor.cs
// Copyright (c) Captolamia
// This file is part of CAP Chat Interactive (RICS).
// Licensed under the GNU Affero General Public License v3.0 or later.
// See LICENSE.txt in the project root for full license text.
//
// Heartbeat: all platform chat → commands / replies / registration (modder-facing public API).
using CAP_ChatInteractive.Commands.Cooldowns;
using CAP_ChatInteractive.Utilities;
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
        /// <summary>Registered commands by name/alias (case-insensitive). Public for tools/UI that need the map.</summary>
        public static readonly Dictionary<string, ChatCommand> _commands =
            new Dictionary<string, ChatCommand>(StringComparer.OrdinalIgnoreCase);

        private static readonly Dictionary<string, DateTime> _userCooldowns = new Dictionary<string, DateTime>();

        public static event Action<ChatMessageWrapper> OnMessageProcessed;
        public static event Action<ChatMessageWrapper, string> OnCommandExecuted;

        // ── Message entry ───────────────────────────────────────────────

        /// <summary>All platform chat messages enter here (Twitch / YouTube / Kick / etc.).</summary>
        public static void ProcessMessage(ChatMessageWrapper message)
        {
            if (message == null || string.IsNullOrEmpty(message.Message))
                return;

            try
            {
                var viewer = Viewers.GetViewer(message);
                if (viewer != null)
                {
                    bool nameChanged = viewer.UpdateDisplayName(message.DisplayName);
                    if (nameChanged && Rand.Chance(0.4f))
                    {
                        SendMessageToUsername(
                            message.Username,
                            $"✨ {viewer.DisplayName} just got a fresh new name! Welcome back!");
                    }
                }

                ProcessLootboxWelcome(message);

                if (IsCommand(message.Message))
                    ProcessCommand(message);
                else
                    ProcessChatMessage(message);

                OnMessageProcessed?.Invoke(message);
            }
            catch (Exception ex)
            {
                Logger.Error($"[ChatCommandProcessor] Error processing chat message: {ex.Message}");
            }
        }

        /// <summary>
        /// AI ChatBot path: execute a command and return the result string (no outbound chat).
        /// Cooldowns are not applied (internal bot; spam protection is for live platforms).
        /// </summary>
        public static string ProcessAICommand(ChatMessageWrapper message)
        {
            if (message == null || string.IsNullOrWhiteSpace(message.Message))
                return "Error: Empty command";

            var settings = CAPChatInteractiveMod.Instance?.Settings?.GlobalSettings;
            if (settings == null || !settings.AIChatBotActive || !settings.AIChatBotCanExecuteCommands)
                return "Error: AI command execution is currently disabled";

            try
            {
                if (!IsGameReady())
                    return "Error: Game is not ready yet";

                var parts = message.Message.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 0)
                    return "Error: No command found";

                string commandText = parts[0].TrimStart('!', '$').ToLowerInvariant();
                string[] args = parts.Skip(1).ToArray();
                commandText = ResolveCommandFromAlias(commandText);

                if (!_commands.TryGetValue(commandText, out var command) || command == null)
                    return $"Error: Unknown command '{commandText}'";

                var viewer = Viewers.GetViewer(message);
                if (viewer == null)
                    return "Error: Could not create viewer";

                // Ensure aichatbot platform id for permission bypass
                viewer.UpdateFromMessage(message);

                if (!command.CanExecute(message))
                    return $"Error: Insufficient permission for command '{commandText}'";

                string result = command.Execute(message, args) ?? string.Empty;
                return string.IsNullOrWhiteSpace(result)
                    ? "Command executed successfully (no output)"
                    : result;
            }
            catch (Exception ex)
            {
                Logger.Error($"[ChatCommandProcessor] AI command '{message.Message}': {ex.Message}");
                return $"Error: {ex.Message}";
            }
        }

        public static bool IsGameReady()
        {
            try
            {
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
            if (string.IsNullOrEmpty(message))
                return false;

            var settings = CAPChatInteractiveMod.Instance?.Settings?.GlobalSettings;
            string prefix = settings?.Prefix ?? "!";
            string buyPrefix = settings?.BuyPrefix ?? "$";

            string trimmed = message.TrimStart();
            return (!string.IsNullOrEmpty(prefix) && trimmed.StartsWith(prefix)) ||
                   (!string.IsNullOrEmpty(buyPrefix) && trimmed.StartsWith(buyPrefix)) ||
                   trimmed.StartsWith("$");
        }

        private static void ProcessLootboxWelcome(ChatMessageWrapper message)
        {
            try
            {
                var lootboxComponent = Current.Game?.GetComponent<LootBoxComponent>();
                if (lootboxComponent == null)
                    return;

                if (CommandSettingsManager.GetSettings("openlootbox")?.Enabled == true)
                    lootboxComponent.ProcessViewerMessage(message);
            }
            catch (Exception ex)
            {
                Logger.Error($"[ChatCommandProcessor] Lootbox welcome: {ex.Message}");
            }
        }

        // ── Command pipeline ────────────────────────────────────────────

        private static void ProcessCommand(ChatMessageWrapper message)
        {
            // Silent when not in play (no chat spam during load)
            if (!IsGameReady())
                return;

            if (message == null || string.IsNullOrEmpty(message.Message))
                return;

            if (string.IsNullOrEmpty(message.Username))
            {
                Logger.Warning("[ChatCommandProcessor] Message with null username, skipping");
                return;
            }

            var globalSettings = CAPChatInteractiveMod.Instance?.Settings?.GlobalSettings;
            if (globalSettings == null)
                return;

            var parts = message.Message.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
                return;

            string commandText = parts[0];
            string[] args = parts.Skip(1).ToArray();

            string prefix = globalSettings.Prefix ?? "!";
            string buyPrefix = globalSettings.BuyPrefix ?? "$";

            if (!string.IsNullOrEmpty(prefix) && commandText.StartsWith(prefix))
                commandText = commandText.Substring(prefix.Length);
            else if (!string.IsNullOrEmpty(buyPrefix) && commandText.StartsWith(buyPrefix))
                commandText = commandText.Substring(buyPrefix.Length);

            commandText = commandText.ToLowerInvariant();
            commandText = ResolveCommandFromAlias(commandText);

            ChatCommand command = null;
            if (_commands.TryGetValue(commandText, out command) && command != null)
            {
                // Normal path
            }
            else
            {
                // Legacy $ → buy (when not already a registered command name)
                string trimmed = message.Message.TrimStart();
                if (trimmed.StartsWith("$"))
                {
                    commandText = "buy";
                    string rest = trimmed.Substring(1).Trim();
                    args = string.IsNullOrWhiteSpace(rest)
                        ? Array.Empty<string>()
                        : rest.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

                    if (!_commands.TryGetValue("buy", out command) || command == null)
                    {
                        Logger.Error("[ChatCommandProcessor] 'buy' command not registered");
                        return;
                    }
                }
                else
                {
                    return; // Unknown command — silent
                }
            }

            var viewer = Viewers.GetViewer(message);
            if (viewer == null)
                return;

            // Streamer bypass: channel owner is never blocked by ban
            bool isStreamer = IsChannelOwner(message, globalSettings);
            if (viewer.IsBanned && !isStreamer)
            {
                Logger.Warning(
                    $"[ChatCommandProcessor] Banned viewer {message.Username} attempted: {commandText}");
                return;
            }

            // Dev Twitch ID: may run disabled commands for testing
            bool isDevBypass = message.Username == "captolamia" &&
                              message.PlatformUserId == "58513264" &&
                              string.Equals(message.Platform, "twitch", StringComparison.OrdinalIgnoreCase);

            var cmdSettings = CommandSettingsManager.GetSettings(commandText);
            if (cmdSettings != null && !cmdSettings.Enabled)
            {
                if (isDevBypass)
                {
                    SendMessageToUser(message,
                        $"[DEV] Command '{commandText}' is currently disabled — executing anyway for testing.");
                }
                else
                {
                    SendMessageToUser(message, $"Command {commandText} is currently disabled.");
                    return;
                }
            }

            if (IsOnCooldown(message.Username, command))
            {
                SendCooldownMessage(message, command);
                return;
            }

            if (!command.CanExecute(message))
            {
                SendPermissionDeniedMessage(message, command);
                return;
            }

            try
            {
                string result = command.Execute(message, args);
                if (!string.IsNullOrEmpty(result))
                    SendMessageToUser(message, result);

                OnCommandExecuted?.Invoke(message, result);

                if (!string.IsNullOrEmpty(result) && !result.StartsWith("Error"))
                    GetCooldownManager()?.RecordCommandUse(command.Name);

                UpdateCooldown(message.Username, command);
            }
            catch (Exception ex)
            {
                Logger.Error($"[ChatCommandProcessor] Error executing '{commandText}': {ex.Message}");
                SendMessageToUser(message, $"Error executing command: {ex.Message}");
            }
        }

        private static bool IsChannelOwner(ChatMessageWrapper message, CAPGlobalChatSettings globalSettings)
        {
            if (message == null || globalSettings == null || string.IsNullOrEmpty(message.Username))
                return false;

            try
            {
                string user = message.Username;
                var mod = CAPChatInteractiveMod.Instance;
                string twitchChannel = mod?.Settings?.TwitchSettings?.ChannelName;
                string youtubeChannel = mod?.Settings?.YouTubeSettings?.ChannelName;

                if (!string.IsNullOrEmpty(twitchChannel) &&
                    user.Equals(twitchChannel, StringComparison.OrdinalIgnoreCase))
                    return true;

                if (!string.IsNullOrEmpty(youtubeChannel) &&
                    user.Equals(youtubeChannel, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            catch
            {
                return false;
            }

            return false;
        }

        private static string ResolveCommandFromAlias(string commandText)
        {
            if (string.IsNullOrEmpty(commandText))
                return commandText;

            if (_commands.ContainsKey(commandText))
                return commandText;

            foreach (var command in _commands.Values.Distinct())
            {
                if (command != null &&
                    !string.IsNullOrEmpty(command.Alias) &&
                    command.Alias.Equals(commandText, StringComparison.OrdinalIgnoreCase))
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
                if (message?.Platform != null &&
                    message.Platform.Equals("Twitch", StringComparison.OrdinalIgnoreCase) &&
                    !string.IsNullOrEmpty(message.CustomRewardId))
                {
                    ProcessChannelPointsReward(message);
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"[ChatCommandProcessor] Error processing chat message: {ex.Message}");
            }
        }

        private static bool IsOnCooldown(string username, ChatCommand command)
        {
            if (command == null || command.CooldownSeconds <= 0 || string.IsNullOrEmpty(username))
                return false;

            var key = $"{username}_{command.Name}";
            return _userCooldowns.TryGetValue(key, out var lastUsed) &&
                   DateTime.Now - lastUsed < TimeSpan.FromSeconds(command.CooldownSeconds);
        }

        public static GlobalCooldownManager GetCooldownManager()
        {
            if (Current.Game == null)
                return null;

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
            if (command == null || command.CooldownSeconds <= 0 || string.IsNullOrEmpty(username))
                return;

            _userCooldowns[$"{username}_{command.Name}"] = DateTime.Now;
        }

        private static void SendCooldownMessage(ChatMessageWrapper message, ChatCommand command)
        {
            if (message == null || command == null)
                return;

            var key = $"{message.Username}_{command.Name}";
            if (!_userCooldowns.TryGetValue(key, out var lastUsed))
            {
                SendMessageToUser(message, "Command is on cooldown.");
                return;
            }

            var remaining = TimeSpan.FromSeconds(command.CooldownSeconds) - (DateTime.Now - lastUsed);
            int secs = Math.Max(0, (int)Math.Ceiling(remaining.TotalSeconds));
            SendMessageToUser(message, $"Command is on cooldown. Try again in {secs} seconds.");
        }

        private static void SendPermissionDeniedMessage(ChatMessageWrapper message, ChatCommand command)
        {
            if (message == null || command == null)
                return;

            string prefix = CAPChatInteractiveMod.Instance?.Settings?.GlobalSettings?.Prefix ?? "!";
            SendMessageToUser(message,
                $"You don't have permission to use {prefix}{command.Name}. Required: {command.PermissionLevel}");
        }

        // ── Outbound send ───────────────────────────────────────────────

        /// <summary>Reply on the same platform as the incoming message (whisper if applicable).</summary>
        public static void SendMessageToUser(ChatMessageWrapper message, string text)
        {
            try
            {
                if (message == null || string.IsNullOrWhiteSpace(text))
                    return;

                string cleanText = XmlTextSanitizer.Sanitize(RemoveMarkupTags(text));
                if (string.IsNullOrWhiteSpace(cleanText))
                    return;

                var mod = CAPChatInteractiveMod.Instance;
                if (mod == null)
                    return;

                string platform = message.Platform?.ToLowerInvariant() ?? string.Empty;

                ChatMessageLogger.AddMessage(
                    username: message.Username,
                    message: cleanText,
                    platform: message.Platform);

                SendToPlatform(mod, platform, message.Username, cleanText, message.IsWhisper);
            }
            catch (Exception ex)
            {
                Logger.Error(
                    $"[ChatCommandProcessor] Error sending to user on {message?.Platform}: {ex.Message}");
            }
        }

        /// <summary>Send to a username using their known platform id (public chat, not whisper).</summary>
        public static void SendMessageToUsername(string username, string text)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(text))
                    return;

                string cleanText = XmlTextSanitizer.Sanitize(RemoveMarkupTags(text));
                if (string.IsNullOrWhiteSpace(cleanText))
                    return;

                var viewer = Viewers.GetViewer(username);
                if (viewer == null)
                    return;

                var mod = CAPChatInteractiveMod.Instance;
                if (mod == null)
                    return;

                string platform = DetermineUserPlatform(viewer);
                if (string.IsNullOrEmpty(platform))
                    return;

                ChatMessageLogger.AddMessage(
                    username: username,
                    message: cleanText,
                    platform: platform);

                // Match long-standing platform-specific addressing for username-only sends
                if (platform == "youtube")
                    SendToPlatform(mod, platform, username, $"@{username} {cleanText}", whisper: false, alreadyAddressed: true);
                else if (platform == "kick")
                    SendToPlatform(mod, platform, username, cleanText, whisper: false, alreadyAddressed: true);
                else
                    SendToPlatform(mod, platform, username, cleanText, whisper: false, alreadyAddressed: false);
            }
            catch (Exception ex)
            {
                Logger.Error($"[ChatCommandProcessor] Error sending to username {username}: {ex.Message}");
            }
        }

        /// <param name="alreadyAddressed">True when body already includes @user (e.g. YouTube username send).</param>
        private static void SendToPlatform(
            CAPChatInteractiveMod mod,
            string platform,
            string username,
            string cleanText,
            bool whisper,
            bool alreadyAddressed = false)
        {
            if (mod == null || string.IsNullOrEmpty(cleanText))
                return;

            switch (platform?.ToLowerInvariant())
            {
                case "twitch":
                    if (mod.TwitchService?.IsConnected == true)
                    {
                        if (whisper)
                            _ = mod.TwitchService.SendWhisperAsync(username, cleanText);
                        else
                            mod.TwitchService.SendMessage(
                                alreadyAddressed ? cleanText : $"@{username} {cleanText}");
                    }
                    break;

                case "youtube":
                    if (mod.YouTubeService?.IsConnected == true)
                    {
                        if (mod.YouTubeService.CanSendMessages)
                            mod.YouTubeService.SendMessage(cleanText);
                        else
                            Messages.Message(
                                alreadyAddressed ? $"[YouTube] {cleanText}" : $"[YouTube] @{username} {cleanText}",
                                MessageTypeDefOf.NeutralEvent);
                    }
                    break;

                case "kick":
                    if (mod.KickService?.IsConnected == true)
                    {
                        // Public Kick replies: include username for command replies; body-only for some username sends
                        mod.KickService.SendMessage(
                            alreadyAddressed ? cleanText : $"{username} {cleanText}");
                    }
                    break;

                default:
                    break;
            }
        }

        /// <summary>Strip XML-style markup tags for plain chat platforms.</summary>
        public static string RemoveMarkupTags(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            return Regex.Replace(text, @"<[^>]+>", string.Empty);
        }

        private static string DetermineUserPlatform(Viewer viewer)
        {
            if (viewer?.PlatformUserIds == null || viewer.PlatformUserIds.Count == 0)
                return null;

            if (viewer.PlatformUserIds.ContainsKey("twitch"))
                return "twitch";
            if (viewer.PlatformUserIds.ContainsKey("youtube"))
                return "youtube";
            if (viewer.PlatformUserIds.ContainsKey("kick"))
                return "kick";

            return null;
        }

        // ── Registration (modders / Def loader) ─────────────────────────

        public static void RegisterCommand(ChatCommand command)
        {
            if (command == null || string.IsNullOrEmpty(command.Name))
            {
                Logger.Error("[ChatCommandProcessor] RegisterCommand: null command or empty Name");
                return;
            }

            _commands[command.Name] = command;

            if (!string.IsNullOrEmpty(command.Alias))
                _commands[command.Alias] = command;
        }

        public static IEnumerable<ChatCommand> GetAvailableCommands(ChatMessageWrapper user)
        {
            return _commands.Values.Distinct().Where(cmd => cmd != null && cmd.CanExecute(user));
        }

        /// <summary>
        /// Look up a registered command by name (case-insensitive).
        /// Useful for UI (e.g. CustomData buttons) that need the live command object.
        /// </summary>
        public static bool TryGetCommand(string name, out ChatCommand command)
        {
            if (string.IsNullOrEmpty(name))
            {
                command = null;
                return false;
            }

            return _commands.TryGetValue(name, out command);
        }

        public static bool UsesPrefix(string message, string prefix)
        {
            return !string.IsNullOrEmpty(message) &&
                   !string.IsNullOrEmpty(prefix) &&
                   message.StartsWith(prefix);
        }

        public static string GetCommandPrefix(bool isBuyCommand = false)
        {
            var settings = CAPChatInteractiveMod.Instance?.Settings?.GlobalSettings;
            if (settings == null)
                return isBuyCommand ? "$" : "!";

            return isBuyCommand
                ? (settings.BuyPrefix ?? "$")
                : (settings.Prefix ?? "!");
        }

        // ── Channel points ──────────────────────────────────────────────

        private static void ProcessChannelPointsReward(ChatMessageWrapper message)
        {
            try
            {
                var globalSettings = CAPChatInteractiveMod.Instance?.Settings?.GlobalSettings;
                if (globalSettings == null || !globalSettings.ChannelPointsEnabled)
                    return;

                if (globalSettings.RewardSettings == null)
                {
                    Logger.Warning("[ChatCommandProcessor] RewardSettings list is null");
                    return;
                }

                string rewardId = message.CustomRewardId;
                var reward = globalSettings.RewardSettings.FirstOrDefault(r =>
                    r != null && r.RewardUUID == rewardId && r.Enabled);

                if (reward != null)
                {
                    if (int.TryParse(reward.CoinsToAward, out int coins) && coins != 0)
                    {
                        var viewer = Viewers.GetViewer(message);
                        if (viewer != null)
                        {
                            viewer.GiveCoins(coins);
                            SendMessageToUser(message,
                                $"Thank you for redeeming '{reward.RewardName}'! You received {coins} coins.");
                        }
                    }
                }
                else
                {
                    if (globalSettings.ShowChannelPointsDebugMessages)
                        Logger.Warning(
                            $"[ChatCommandProcessor] Unconfigured custom reward: {rewardId}");

                    var autoReward = globalSettings.RewardSettings.FirstOrDefault(r =>
                        r != null && r.AutomaticallyCaptureUUID && r.Enabled);

                    if (autoReward != null)
                    {
                        if (globalSettings.ShowChannelPointsDebugMessages)
                        {
                            Logger.Message(
                                $"[ChatCommandProcessor] Auto-capturing reward UUID {rewardId} " +
                                $"for '{autoReward.RewardName}'");
                        }

                        autoReward.AutomaticallyCaptureUUID = false;
                        autoReward.RewardUUID = rewardId;
                        SendMessageToUser(message,
                            $"Automatically configured '{autoReward.RewardName}' with this reward ID.");
                    }
                    else if (globalSettings.ShowChannelPointsDebugMessages)
                    {
                        Logger.Message(
                            "[ChatCommandProcessor] Unmatched reward — add this UUID in mod settings if desired.");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"[ChatCommandProcessor] Channel points reward: {ex.Message}");
            }
        }
    }
}
