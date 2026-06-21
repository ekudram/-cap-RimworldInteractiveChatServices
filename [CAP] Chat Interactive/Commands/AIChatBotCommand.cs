// AIChatBotCommand.cs

// Copyright (c) Captolamia
// This file is part of CAP Chat Interactive. aka Rimworld Interactive Chat System (RICS)
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

using Newtonsoft.Json;
using RimWorld;
using System;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Verse;

namespace CAP_ChatInteractive.Commands.AICommands
{
    public class AIChatBotCommand : ChatCommand
    {
        public override string Name => "ricsaichatbot";

        private static readonly HttpClient _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(12) };

        public override string Execute(ChatMessageWrapper message, string[] args)
        {
            var settings = CAPChatInteractiveMod.Instance?.Settings?.GlobalSettings;
            if (settings == null || !settings.AIChatBotActive)
                return "The AI ChatBot is currently disabled. Enable 'AI ChatBot Active' in the RICS mod settings.";

            if (!ChatCommandProcessor.IsGameReady())
                return "The colony is still loading. Please try again shortly.";

            string userInput = string.Join(" ", args).Trim();
            if (string.IsNullOrWhiteSpace(userInput))
                return "Please provide a message for the AI ChatBot.";

            string botName = settings.AIChatBotName ?? "AI Storyteller";

            // Generic "thinking" message — personality / cat sounds now come from the Python bot
            ChatMessageLogger.AddMessage(botName, "Thinking...", "AI", isFromRICS: true);

            // Safe: Queue on main thread before going async
            LongEventHandler.QueueLongEvent(() =>
            {
                _ = ProcessAIResponseAsync(message, userInput, settings);
            }, "AIChatBotThinking", false, null);

            return null; // No immediate reply
        }

        private async Task ProcessAIResponseAsync(ChatMessageWrapper originalMessage, string userInput, CAPGlobalChatSettings settings)
        {
            try
            {
                var recentChat = ChatMessageLogger.GetRecentMessagesForAI(20);
                var gameState = GetGameStateForAI();

                var payload = new
                {
                    input = userInput,
                    username = originalMessage.Username,
                    gameState = gameState,
                    recentChat = recentChat.Select(m => new
                    {
                        username = m.Username,
                        text = m.Text,
                        platform = m.Platform,
                        isFromRICS = m.IsFromRICS,
                        timestamp = m.Timestamp
                    }).ToList()
                };

                string jsonPayload = JsonConvert.SerializeObject(payload);

                string response = await SendToAIBotAsync(jsonPayload, settings);
                if (!string.IsNullOrWhiteSpace(response))
                {
                    string botName = settings.AIChatBotName ?? "AI Storyteller";
                    ChatMessageLogger.AddMessage(botName, response, "AI", isFromRICS: true);
                    ChatCommandProcessor.SendMessageToUsername(originalMessage.Username, response);
                }
                else
                {
                    // Let the Python bot own personality — keep C# side neutral
                    ChatCommandProcessor.SendMessageToUsername(originalMessage.Username, "The AI did not return a response. Please try again.");
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"[AI ChatBot Command] Error: {ex.Message}");
                ChatCommandProcessor.SendMessageToUsername(originalMessage.Username, "An error occurred while contacting the AI ChatBot. Please try again later.");
            }
        }

private object GetGameStateForAI()
        {
            var service = Current.Game?.GetComponent<CAPChatInteractive_GameComponent>()?._aiChatBotService;
            if (service != null)
            {
                // Use cached version (main-thread safe)
                string json = service.GetCachedGameStateJson();
                try
                {
                    return JsonConvert.DeserializeObject(json);
                }
                catch
                {
                    return new { status = "ok", raw = json };
                }
            }
            return new { status = "unavailable" };
        }

        private async Task<string> SendToAIBotAsync(string jsonPayload, CAPGlobalChatSettings settings)
        {
            // Declare timeout here so it is visible in both try and catch blocks
            int timeoutSeconds = 240; // Masie V6+ improved + voice queue → longer generation is safe

            try
            {
                var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

                string baseUrl = settings.AIChatBotListenUrl.TrimEnd('/');
                string botUrl = baseUrl.EndsWith("/chat", StringComparison.OrdinalIgnoreCase)
                    ? baseUrl
                    : baseUrl + "/chat";

                Logger.Debug($"[AI ChatBot] Sending request to: {botUrl}");

                // Running inside a Task via LongEventHandler — waiting is acceptable.
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));

                var httpResponse = await _httpClient.PostAsync(botUrl, content, cts.Token);

                if (httpResponse.IsSuccessStatusCode)
                {
                    string responseText = await httpResponse.Content.ReadAsStringAsync();
                    Logger.Debug($"[AI ChatBot] Successfully received response ({responseText.Length} characters)");
                    return responseText;
                }
                else
                {
                    Logger.Warning($"AI Bot returned HTTP {(int)httpResponse.StatusCode} from {botUrl}");
                    return null;
                }
            }
            catch (TaskCanceledException)
            {
                Logger.Warning($"[AI ChatBot] Request timed out after {timeoutSeconds}s (Ollama/TTS generation).");
                return "The AI request timed out. Please try again in a moment.";
            }
            catch (Exception ex)
            {
                Logger.Error($"[AI ChatBot] Failed to reach bot at '{settings.AIChatBotListenUrl}'.\n" +
                             $"   → Error: {ex.Message}");
                return null;
            }
        }
    }
}