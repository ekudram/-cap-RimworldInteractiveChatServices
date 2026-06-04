// AIChatBotCommand.cs
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
                return "AI storyteller is sleeping... Enable it in RICS settings.";

            if (!ChatCommandProcessor.IsGameReady())
                return "Please wait until the colony is fully loaded.";

            string userInput = string.Join(" ", args).Trim();
            if (string.IsNullOrWhiteSpace(userInput))
                return "Nya? What would you like to talk about?";

            string botName = settings.AIChatBotName ?? "AI Storyteller";

            ChatMessageLogger.AddMessage(botName, "Thinking... *ear twitch*", "AI", isFromRICS: true);

            // Queue on main thread then fire async
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
                    string botName = settings.AIChatBotName ?? "Masie";
                    ChatMessageLogger.AddMessage(botName, response, "AI", isFromRICS: true);
                    ChatCommandProcessor.SendMessageToUsername(originalMessage.Username, response);
                }
                else
                {
                    ChatCommandProcessor.SendMessageToUsername(originalMessage.Username, "Nya~... I didn't catch that. Try again?");
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"[AI ChatBot Command] Error: {ex.Message}");
                ChatCommandProcessor.SendMessageToUsername(originalMessage.Username, "Nya~... something went wrong with the connection!");
            }
        }

        private object GetGameStateForAI()
        {
            var service = Current.Game?.GetComponent<CAPChatInteractive_GameComponent>()?._aiChatBotService;
            if (service != null)
            {
                string json = service.GetGameStateJson();
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
            try
            {
                var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

                // Clean URL construction
                string baseUrl = settings.AIChatBotListenUrl.TrimEnd('/');
                string botUrl = baseUrl.EndsWith("/chat", StringComparison.OrdinalIgnoreCase)
                    ? baseUrl
                    : baseUrl + "/chat";

                Logger.Debug($"[AI ChatBot] Sending request to: {botUrl}");

                // Increased timeout for Ollama + TTS
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(25));

                var httpResponse = await _httpClient.PostAsync(botUrl, content, cts.Token);

                if (httpResponse.IsSuccessStatusCode)
                {
                    string responseText = await httpResponse.Content.ReadAsStringAsync();
                    Logger.Debug($"[AI ChatBot] Received response ({responseText.Length} chars): {responseText.Substring(0, Math.Min(120, responseText.Length))}");
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
                Logger.Warning("[AI ChatBot] Request timed out - Ollama or TTS may be slow on first call.");
                return "I'm thinking really hard! I might even have responded... Try again in a moment if needed?";
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