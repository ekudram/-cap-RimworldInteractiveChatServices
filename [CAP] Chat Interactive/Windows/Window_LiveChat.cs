// Windows/Window_LiveChat.cs
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
// A live chat window for displaying and sending chat messages
using CAP_ChatInteractive.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace CAP_ChatInteractive.Windows
{
    public class Window_LiveChat : Window
    {
        private Vector2 _chatScrollPosition = Vector2.zero;
        private string _currentMessage = "";
        private float _lastMessageHeight;
        // Updated colors for better contrast
        private static readonly Color BackgroundColor = new Color(0.08f, 0.08f, 0.08f, 0.55f);   // 68% opacity
        private static readonly Color InputBackgroundColor = new Color(0.25f, 0.25f, 0.25f, 0.65f); // slightly more solid input
        private static readonly Color MessageTextColor = new Color(1f, 1f, 1f, 1f); // Pure white for maximum contrast

        private const float INPUT_HEIGHT = 30f;
        private const float PADDING = 8f; // Increased padding
        private bool _shouldScrollToBottom = true;

        public Window_LiveChat()
        {
            draggable = true;
            resizeable = true;
            doCloseButton = false;
            doCloseX = true;
            absorbInputAroundWindow = false;
            closeOnClickedOutside = false;
            closeOnAccept = false;
            doWindowBackground = false;
            preventCameraMotion = false;
            layer = WindowLayer.GameUI;
        }

        public override Vector2 InitialSize => new Vector2(400f, 300f);

        /// <summary>
        /// RimWorld's official hook for initial window position/size.
        /// Loads saved position or defaults to middle-left of screen (exactly as requested).
        /// </summary>
        protected override void SetInitialSizeAndPosition()   // ← protected (required by Verse.Window)
        {
            base.SetInitialSizeAndPosition();

            var settings = CAPChatInteractiveMod.Instance.Settings.GlobalSettings;

            // First time ever (or reset) → middle-left default
            if (settings.LiveChatWindowX < 0f)
            {
                windowRect = new Rect(
                    20f,                                      // left edge
                    (UI.screenHeight - 300f) / 2f,            // exact middle vertically
                    400f,
                    300f
                );
            }
            else
            {
                // Restore saved position/size
                windowRect = new Rect(
                    settings.LiveChatWindowX,
                    settings.LiveChatWindowY,
                    settings.LiveChatWindowWidth,
                    settings.LiveChatWindowHeight
                );
            }

            // Enforce minimum size (RimWorld standard)
            if (windowRect.width < 200f) windowRect.width = 200f;
            if (windowRect.height < 150f) windowRect.height = 150f;

            // Keep window fully on screen
            if (windowRect.xMax > UI.screenWidth) windowRect.x = UI.screenWidth - windowRect.width;
            if (windowRect.yMax > UI.screenHeight) windowRect.y = UI.screenHeight - windowRect.height;
            if (windowRect.x < 0f) windowRect.x = 0f;
            if (windowRect.y < 0f) windowRect.y = 0f;
        }

        /// <summary>
        /// Save exact position + size when player closes the window (drag/resize).
        /// </summary>
        public override void PreClose()
        {
            base.PreClose();

            var settings = CAPChatInteractiveMod.Instance.Settings.GlobalSettings;
            settings.LiveChatWindowX = windowRect.x;
            settings.LiveChatWindowY = windowRect.y;
            settings.LiveChatWindowWidth = windowRect.width;
            settings.LiveChatWindowHeight = windowRect.height;

            // Write immediately so Ctrl+V reopen is instant
            CAPChatInteractiveMod.Instance.WriteSettings();
        }

        /// <summary>
        /// Public static toggle so GameComponent and future commands can open/close instantly.
        /// </summary>
        public static void ToggleLiveChatWindow()
        {
            ChatUtility.ToggleLiveChatWindow();
        }

        public override void DoWindowContents(Rect inRect)
        {
            // Logger.Debug("Live Chat window start render. " + windowRect);
            try
            {
                // CUSTOM GLASS BACKGROUND (full control — map shows through perfectly)
                // This is the 100% reliable way in RimWorld 1.6 for transparent overlays
                Widgets.DrawBoxSolid(inRect, BackgroundColor);

                // Optional faint border (feels like modern game overlay)
                GUI.color = new Color(1f, 1f, 1f, 0.25f);
                Widgets.DrawBox(inRect, 1);
                GUI.color = Color.white;

                // Calculate areas - FIXED: Input at bottom with proper spacing
                float inputAreaHeight = INPUT_HEIGHT + (PADDING * 2);
                float chatAreaHeight = inRect.height - inputAreaHeight;

                // Chat messages area (top)
                var chatRect = new Rect(0f, 0f, inRect.width, chatAreaHeight);
                DrawChatMessages(chatRect);

                // Input area (bottom) - FIXED positioning
                var inputRect = new Rect(0f, chatAreaHeight, inRect.width, inputAreaHeight);

                DrawInputArea(inputRect);
                // Logger.Debug("Live Chat window rendered successfully. " + windowRect);
            }
            catch (Exception ex)
            {
                Logger.Error($"Error in chat window UI: {ex.Message}");
            }
        }

        private void DrawChatMessages(Rect rect)
        {
            // Background — now semi-transparent thanks to new DoWindowBackground + lower alpha
            Widgets.DrawBoxSolid(rect, BackgroundColor);

            // Get messages
            var messages = GetRecentMessages();
            float totalHeight = CalculateTotalHeight(messages, rect.width - 20f);

            var viewRect = new Rect(0f, 0f, rect.width - 16f, Mathf.Max(totalHeight, rect.height));

            // Auto-scroll to bottom if new messages
            if (_shouldScrollToBottom && Event.current.type == EventType.Repaint)
            {
                _chatScrollPosition.y = Mathf.Max(0f, totalHeight - rect.height);
                _shouldScrollToBottom = false;
            }

            // Scroll view
            _chatScrollPosition = GUI.BeginScrollView(rect, _chatScrollPosition, viewRect);
            {
                float yPos = 0f;
                foreach (var message in messages)
                {
                    float messageHeight = DrawMessage(viewRect, yPos, message);
                    yPos += messageHeight + 2f;
                }
                _lastMessageHeight = yPos;
            }
            GUI.EndScrollView();

            // Border
            Widgets.DrawBox(rect);
        }
        private float CalculateTotalHeight(List<ChatMessageDisplay> messages, float width)
        {
            float totalHeight = 4f; // Start with top padding

            foreach (var message in messages)
            {
                // Combine username and message for accurate height calculation
                string displayUsername = message.IsSystem && message.Username == "System" && message.Text.StartsWith("You:")
                    ? "You"
                    : message.Username;

                string displayText = message.IsSystem && message.Username == "System" && message.Text.StartsWith("You:")
                    ? message.Text.Substring(4)
                    : message.Text;

                string formattedMessage = $"{displayUsername}: {displayText}";

                float messageWidth = width - 24f; // Account for horizontal padding
                float messageHeight = Text.CalcHeight(formattedMessage, messageWidth);
                totalHeight += Mathf.Max(24f, messageHeight) + 2f;
            }

            return totalHeight;
        }

        private float DrawMessage(Rect container, float yPos, ChatMessageDisplay message)
        {
            float horizontalPadding = 12f;

            // Combine username and message into one string with formatting
            string displayUsername = message.IsSystem && message.Username == "System" && message.Text.StartsWith("You:")
                ? "You"
                : message.Username;

            string displayText = message.IsSystem && message.Username == "System" && message.Text.StartsWith("You:")
                ? message.Text.Substring(4)
                : message.Text;

            // Create formatted message - username in "bold" (using color and colon)
            string formattedMessage = $"{displayUsername}:<color=#FFFFFF> {displayText}</color>";

            // Calculate the full message height
            float messageWidth = container.width - (horizontalPadding * 2);
            float messageHeight = Text.CalcHeight(formattedMessage, messageWidth);
            float lineHeight = Mathf.Max(24f, messageHeight);

            // Add top padding to first message
            if (yPos == 0f) yPos += 4f;

            // Draw the combined message
            var messageRect = new Rect(horizontalPadding, yPos, messageWidth, lineHeight);

            // Set color based on platform
            var messageColor = GetPlatformColor(message.Platform);
            if (message.IsSystem)
            {
                messageColor = Color.yellow;
            }

            GUI.color = messageColor;
            Widgets.Label(messageRect, formattedMessage);
            GUI.color = Color.white;

            return lineHeight;
        }

        private void DrawInputArea(Rect rect)
        {
            // Background - glass style (slightly more opaque so input is readable)
            Widgets.DrawBoxSolid(rect, InputBackgroundColor);

            // Input field + button positioned at BOTTOM of the input area
            // (absolute coordinates because we are not inside a BeginGroup — this is the exact pattern that worked before)
            float localY = rect.y + (rect.height - INPUT_HEIGHT - PADDING);
            var inputRect = new Rect(PADDING, localY, rect.width - 70f - PADDING * 2, INPUT_HEIGHT);
            var buttonRect = new Rect(inputRect.xMax + PADDING, localY, 60f, INPUT_HEIGHT);

            GUI.SetNextControlName("ChatInput");
            _currentMessage = Widgets.TextField(inputRect, _currentMessage);

            if (Widgets.ButtonText(buttonRect, "Send"))
            {
                TrySendMessage();
            }

            // Visual hint when typing (appears just above the input field)
            if (ChatUtility.IsChatInputFocused())
            {
                Text.Font = GameFont.Tiny;
                Widgets.Label(new Rect(PADDING, localY - 28f, inputRect.width, 18f), "Press <color=#ffcc00>ESC</color> to unfocus • SEND to send");
                Text.Font = GameFont.Small;
            }

            // Separator line at top of input area + border
            Widgets.DrawLineHorizontal(0f, 0f, rect.width);
            Widgets.DrawBox(rect);
        }

        private void TrySendMessage()
        {
            if (!string.IsNullOrWhiteSpace(_currentMessage))
            {
                SendMessage(_currentMessage.Trim());
                _currentMessage = "";
                // Keep focus after send (fast typing flow) — change to GUI.FocusControl(null) if you prefer auto-unfocus
                GUI.FocusControl("ChatInput");
            }
        }

        // Call this when new messages arrive to trigger auto-scroll
        public void NotifyNewMessage()
        {
            _shouldScrollToBottom = true;
        }

        private Color GetPlatformColor(string platform)
        {
            return platform?.ToLowerInvariant() switch
            {
                "twitch" => new Color(0.64f, 0.41f, 0.93f), // Twitch purple
                "youtube" => new Color(1f, 0f, 0f),         // YouTube red
                "kick" => new Color(0.21f, 0.91f, 0.21f),   // Kick green
                "system" => Color.yellow,
                _ => Color.cyan
            };
        }

        private void SendMessage(string message)
        {
            try
            {
                var mod = CAPChatInteractiveMod.Instance;
                bool messageSent = false;

                // Send to Twitch (priority for streamer commands)
                if (mod.TwitchService?.IsConnected == true)
                {
                    mod.TwitchService.SendMessage(message);
                    messageSent = true;
                }

                // Send to YouTube (fallback / multi-stream)
                if (mod.YouTubeService?.IsConnected == true && mod.YouTubeService.CanSendMessages)
                {
                    mod.YouTubeService.SendMessage(message);
                    messageSent = true;
                }

                if (mod.KickService?.IsConnected == true && mod.KickService.CanSendMessages)
                {
                    mod.KickService.SendMessage(message);
                    messageSent = true;
                }

                if (messageSent)
                {
                    // Add to local display as "You"
                    ChatMessageLogger.AddSystemMessage($"You: {message}");
                }
                else
                {
                    ChatMessageLogger.AddSystemMessage("Not connected to any chat service");
                }

                // === NEW: Streamer command processing (this was the missing piece) ===
                // We already know the message was sent, so now check if it's a command
                // and feed it directly into our processor (services never echo broadcaster messages).
                if (ChatCommandProcessor.IsCommand(message))
                {
                    var wrapper = CreateBroadcasterCommandWrapper(message);
                    ChatCommandProcessor.ProcessMessage(wrapper);   // full command flow (cooldowns, pawn lookup, etc.)
                    Logger.Debug($"Broadcaster command processed locally: {message}");
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error sending message: {ex.Message}");
                ChatMessageLogger.AddSystemMessage($"Error: {ex.Message}");
            }
        }

        /// <summary>
        /// Creates a ChatMessageWrapper for the broadcaster so commands typed in the in-game window
        /// are processed exactly like normal viewer commands (pawn assignment, buy, heal, etc.).
        /// PlatformUserId is pulled from the existing Viewer entry (secure, works with pawn commands).
        /// </summary>
        private ChatMessageWrapper CreateBroadcasterCommandWrapper(string rawMessage)
        {
            var settings = CAPChatInteractiveMod.Instance.Settings;

            // Choose primary platform (Twitch first — matches 99% of streamers)
            string platform = "Twitch";
            string channelName = settings.TwitchSettings.ChannelName;
            if (string.IsNullOrEmpty(channelName) || !settings.TwitchSettings.IsConnected)
            {
                if (settings.YouTubeSettings.IsConnected && !string.IsNullOrEmpty(settings.YouTubeSettings.ChannelName))
                {
                    platform = "YouTube";
                    channelName = settings.YouTubeSettings.ChannelName;
                }
                else if (settings.KickSettings.IsConnected && !string.IsNullOrEmpty(settings.KickSettings.ChannelName))
                {
                    platform = "Kick";
                    channelName = settings.KickSettings.ChannelName;
                }
                else
                {
                    platform = "YouTube";
                    channelName = settings.YouTubeSettings.ChannelName;
                }
            }

            // Get or create the broadcaster viewer (guaranteed to exist after first chat)
            var broadcasterViewer = Viewers.GetViewer(channelName);
            string platformUserId = broadcasterViewer?.GetPlatformUserId(platform) ?? "streamer";

            return new ChatMessageWrapper(
                username: channelName,
                message: rawMessage,
                platform: platform,
                platformUserId: platformUserId,
                channelId: channelName,
                platformMessage: null,
                isWhisper: false,
                customRewardId: null,
                bits: 0,
                shouldIgnoreForCommands: false   // broadcaster always allowed
            );
        }

        private List<ChatMessageDisplay> GetRecentMessages()
        {
            return ChatMessageLogger.GetRecentMessages(100);
        }

        // Handle keyboard input and enforce minimum size
        public override void WindowUpdate()
        {
            base.WindowUpdate();

            // DYNAMIC CAMERA CONTROL — update every frame (cheap, exactly like professional overlays)
            // This is the RimWorld 1.6 way to have conditional camera blocking while using the field pattern Dubs Mint Minimap uses.
            preventCameraMotion = ChatUtility.ShouldPreventCameraMovement();

            if (Event.current.type != EventType.KeyDown) return;

            string focused = GUI.GetNameOfFocusedControl();

            // Custom overlay behavior (Esc unfocus + Enter focus/send)
            if (Event.current.keyCode == KeyCode.Escape && focused == "ChatInput")
            {
                GUI.FocusControl(null);           // Unfocus → full camera control returns
                Event.current.Use();
                return;
            }

            if (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter)
            {
                if (focused == "ChatInput")
                {
                    TrySendMessage();
                    Event.current.Use();
                }
                else
                {
                    // Press Enter anywhere on the window → focus chat (standard game chat UX)
                    GUI.FocusControl("ChatInput");
                    Event.current.Use();
                }
            }
        }

        public static void NotifyNewChatMessage()
        {
            Find.WindowStack.Windows.OfType<Window_LiveChat>().FirstOrDefault()?.NotifyNewMessage();
        }
    }
}