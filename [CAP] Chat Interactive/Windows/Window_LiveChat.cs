// Windows/Window_LiveChat.cs
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
        private static readonly Color BackgroundColor = new Color(0.08f, 0.08f, 0.08f, 0.95f);
        private static readonly Color InputBackgroundColor = new Color(0.25f, 0.25f, 0.25f, 1f);
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

            // Set larger initial size and enforce minimum through windowRect
            windowRect = new Rect(20f , UI.screenHeight - (UI.screenHeight/3) * 2, 400f, 200f);

            // Enforce minimum size in constructor
            if (windowRect.width < 200f) windowRect.width = 200f;  // Half of 400
            if (windowRect.height < 150f) windowRect.height = 150f; // Half of 300
            // Logger.Debug("Live Chat window initialized: "+windowRect);
        }
        public override void PreOpen()
        {
            base.PreOpen();

            // Force the window to your desired position and size
            // windowRect = new Rect(20f, UI.screenHeight - 320f, 400f, 300f);
            windowRect = new Rect(20f, UI.screenHeight * 0.66f, 400f, UI.screenHeight * 0.33f);
            // Logger.Debug($"Live Chat window PreOpen: {windowRect}");
        }
        public override void DoWindowContents(Rect inRect)
        {
            // Logger.Debug("Live Chat window start render. " + windowRect);
            try
            {
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
            // Background
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
            // Background - make input area stand out with distinct background
            Widgets.DrawBoxSolid(rect, InputBackgroundColor);

            // Input field and button positioned at bottom of THIS rect
            float localY = rect.yMax - INPUT_HEIGHT - PADDING; // This positions it at bottom
            var inputRect = new Rect(PADDING, localY, rect.width - 70f - PADDING * 2, INPUT_HEIGHT);
            var buttonRect = new Rect(inputRect.xMax + PADDING, localY, 60f, INPUT_HEIGHT);
            // Message input
            GUI.SetNextControlName("ChatInput");
            _currentMessage = Widgets.TextField(inputRect, _currentMessage);
            // Send button
            if (Widgets.ButtonText(buttonRect, "Send"))
            {
                TrySendMessage();
            }
            // Add a separator line at top of input area to clearly separate from chat
            Widgets.DrawLineHorizontal(0f, 0f, rect.width);
            // Border around entire input area
            Widgets.DrawBox(rect);
        }

        private void TrySendMessage()
        {
            if (!string.IsNullOrWhiteSpace(_currentMessage))
            {
                SendMessage(_currentMessage.Trim());
                _currentMessage = "";
                // Keep focus on input field
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

                // Send to Twitch
                if (mod.TwitchService?.IsConnected == true)
                {
                    mod.TwitchService.SendMessage(message);
                    messageSent = true;
                }

                // Send to YouTube
                if (mod.YouTubeService?.IsConnected == true && mod.YouTubeService.CanSendMessages)
                {
                    mod.YouTubeService.SendMessage(message);
                    messageSent = true;
                }

                if (messageSent)
                {
                    // Add to local display as "You" - this will now display correctly
                    ChatMessageLogger.AddSystemMessage($"You: {message}");
                }
                else
                {
                    ChatMessageLogger.AddSystemMessage("Not connected to any chat service");
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error sending message: {ex.Message}");
                ChatMessageLogger.AddSystemMessage($"Error: {ex.Message}");
            }
        }

        private List<ChatMessageDisplay> GetRecentMessages()
        {
            return ChatMessageLogger.GetRecentMessages(100);
        }

        // Handle keyboard input and enforce minimum size
        public override void WindowUpdate()
        {
            base.WindowUpdate();

            // Check for Enter key in a better way
            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Return)
            {
                if (GUI.GetNameOfFocusedControl() == "ChatInput")
                {
                    TrySendMessage();
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